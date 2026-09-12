using System;
using System.Collections.Generic;
using Animancer;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// AI 分层状态机（挂 AI 物体上，由 AIPlayer 构建驱动）
/// 结构：Root 容器下挂 Idle/Walk/Die/Attack 四个状态,Attack 容器内再挂 Attack1/2/3
/// 职责：状态构建与切换（行为树调用入口）、目标查找、连招索引、动画播放（Animancer）、位移
/// 不接输入、不做网络同步——决策在外部（行为树）,执行在本类与各状态内部
/// </summary>
public class AIStateMachine : MonoBehaviour
{
    /// <summary>角色数据资产（由 AIPlayer 在 Awake 里传入，模仿 Player→PlayerSO）</summary>
    public AIPlayerSO AiSO { get; private set; }

    private AIMovementData movementData => AiSO != null ? AiSO.movementData : null;

    /// <summary>目标查找的物理层过滤（LayerMask,默认指向 "player" 层,Build 时兜底注入）</summary>
    public LayerMask targetLayer;

    [HideInInspector] public AnimancerComponent animancer;

    // ============ 行为参数（优先读 SO，未配置用默认值） ============

    public float walkSpeed => movementData?.walkSpeed ?? 3f;
    public float detectRange => movementData?.detectRange ?? 8f;
    public float chaseRange => movementData?.chaseRange ?? 10f;

    /// <summary>按状态类型取状态数据（动画片段/播放参数）</summary>
    public AIStateData GetStateData(AIStateType type) => type switch
    {
        AIStateType.Idle => movementData?.idleData,
        AIStateType.Walk => movementData?.walkData,
        AIStateType.Hurt => movementData?.hurtData,
        AIStateType.Die => movementData?.dieData,
        AIStateType.Attack1 => movementData?.attack1Data,
        AIStateType.Attack2 => movementData?.attack2Data,
        AIStateType.Attack3 => movementData?.attack3Data,
        _ => null,
    };

    /// <summary>按攻击状态类型取攻击数据（时长/伤害/范围）</summary>
    public AIAttackData GetAttackData(AIStateType type) => type switch
    {
        AIStateType.Attack1 => movementData?.attack1Data,
        AIStateType.Attack2 => movementData?.attack2Data,
        AIStateType.Attack3 => movementData?.attack3Data,
        _ => null,
    };

    // ============ 状态表与连招 ============
    private AIState currentState;

    // ============ 连招索引 ============
    private int comboIndex;                  // 当前连招段（指向序列中的下一个出招位）
    private float lastAttackTime = -999f;    // 上一次实际出招/收招的时间（超时归零用）

    /// <summary>当前连招索引（调试/行为树读取用）</summary>
    public int ComboIndex => comboIndex;

    [HideInInspector] public Transform target;
    [HideInInspector] public bool isDead = false;

    /// <summary>受击标记:收到攻击由 AIPlayer 置 true,受击结束(HurtState 动画播完)由状态置 false。
    /// 行为树 AIIsStunned 条件检测它,AIStunRecover 据此切入 Hurt 状态</summary>
    public bool IsHurt { get; set; } = false;

    /// <summary>受击次数计数(每被攻击一次 +1)。
    /// HurtState 用它检测"硬直期间再次受击"→ 重播受击动画并刷新硬直计时</summary>
    public int HurtHitCount { get; private set; }

    /// <summary>受击请求:置标记 + 计数。由 AIPlayer 收到 E_OnDamage 时调用,不切状态</summary>
    public void RequestHurt()
    {
        IsHurt = true;
        HurtHitCount++;
    }

    private CharacterController characterController;
    private NavMeshAgent navAgent;           // NavMesh 寻路(只算路径不位移,可选:没挂组件时退回直线移动)

    /// <summary>寻路代理(预制体上挂了 NavMeshAgent 才有,Walk 状态追击/巡逻用)</summary>
    public NavMeshAgent NavAgent => navAgent;

    // FindTarget 用命中 buffer:复用同一数组,避免每帧 new Collider[](GC)
    private readonly Collider[] targetBuffer = new Collider[32];

    /// <summary>
    /// 构建状态机（由 AIPlayer.Awake 调用并传入 SO，保证建状态机前数据已就位；
    /// 组件引用也在这里取，不依赖本组件 Awake 的执行顺序）
    /// </summary>
    public void Build(AIPlayerSO so)
    {
        AiSO = so;
        animancer = GetComponent<AnimancerComponent>();
        characterController = GetComponent<CharacterController>();

        // 目标层兜底:没配置就指向 "player" 层(AI 索敌/AOE 都用它做物理过滤)
        if (targetLayer.value == 0)
            targetLayer = LayerMask.GetMask("player");

        // NavMeshAgent 只负责算路径:位移仍走 CharacterController(和重力系统共用),
        // 位置/转向都由外部同步,避免两套移动打架
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;
        }

        BuildStateMachine();
    }

    /// <summary>清除寻路路径(离开 Walk 状态时调用,防止残留旧目标点)</summary>
    public void ResetNavPath()
    {
        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            navAgent.ResetPath();
    }

    /// <summary>
    /// 播放状态动画（Animancer，与 Player 一致），片段来自 AIPlayerSO
    /// 返回播放状态（可用其 Length 作为"动画播完"的时长依据），异常时返回 null
    /// </summary>
    public AnimancerState PlayAnim(AIStateType type)
    {
        AIStateData data = GetStateData(type);

        if (animancer == null || data?.animationClip == null)
        {
            Debug.LogWarning($"[AI] 状态 {type} 没有可播放的动画（未挂 Animancer 或 AIPlayerSO 未配 animationClip）", this);
            return null;
        }

        var state = animancer.Play(data.animationClip, data.fadeDuration);
        state.Speed = data.playSpeed;
        return state;
    }

    /// <summary>
    /// 从头播放状态动画:先 Play 再归零播放头。
    /// 专给受击刷新用——Animancer 对"已在播的同一段动画"调用 Play 只会继续播,
    /// 硬直中再次受击重入 Hurt 节点时,必须归零受击动画才会重播
    /// </summary>
    public AnimancerState ReplayAnim(AIStateType type)
    {
        var state = PlayAnim(type);
        if (state != null)
            state.Time = 0f;          // 播放头拨回开头,保证从头播
        return state;
    }

    /// <summary>水平位移：优先 CharacterController（有碰撞，不穿墙），无则直接改坐标</summary>
    public void MoveHorizontal(Vector3 dir, float speed)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        Vector3 delta = dir.normalized * speed * Time.deltaTime;
        if (characterController != null)
            characterController.Move(delta);
        else
            transform.position += delta;
    }

    /// <summary>面向目标（水平面平滑转向）——行为树 Action:FaceTarget 每帧调用，状态不管朝向</summary>
    public void FaceTarget(float turnSpeed = 8f)
    {
        if (target == null) return;
        Vector3 dir = target.position - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * turnSpeed);
    }

    private void BuildStateMachine()
    {
        // ---------- Root 顶层容器 ----------
        var root = new AIHierarchicalState(this, gameObject, AIStateType.Root, AIStateType.Idle);

        // ---------- Idle / Walk / Hurt / Die ----------
        root.AddSubState(new IdleState(this, gameObject));
        root.AddSubState(new WalkState(this, gameObject));
        root.AddSubState(new HurtState(this, gameObject));
        root.AddSubState(new DieState(this, gameObject));

        // ---------- Attack 分层容器(默认进 Attack1) ----------
        var attack = new AIHierarchicalState(this, gameObject, AIStateType.Attack, AIStateType.Attack1);
        attack.AddSubState(new AttackState1(this, gameObject));
        attack.AddSubState(new AttackState2(this, gameObject));
        attack.AddSubState(new AttackState3(this, gameObject));
        root.AddSubState(attack);

        currentState = root;
        currentState.OnEnter();
    }

    private void Update()
    {
        if (currentState == null) return;

        // 顶层检查转移(死亡优先)
        AIStateType? next = currentState.CheckTransitions();
        if (next != null)
            SwitchState(next.Value);

        currentState.OnUpdate();
        ResetComboIfExpired();
    }

    /// <summary>连招超时检查：resetTime 内没出下一招，索引归零重新开始。
    /// 攻击进行中不判超时（计时从收招那一刻起算，长招不会被中途误归零）</summary>
    private void ResetComboIfExpired()
    {
        var combo = movementData?.comboData;
        if (combo == null || combo.comboSequence == null || combo.comboSequence.Count == 0) return;
        if (comboIndex == 0) return;
        if (IsAttacking) return;

        if (Time.time - lastAttackTime > combo.resetTime)
        {
            comboIndex = 0;
            Debug.Log($"[AI] 连招超时({combo.resetTime}s)未继续,索引归零");
        }
    }

    /// <summary>
    /// 攻击动画结束(收招决策)时由攻击状态调用：
    /// 超时计时从收招那一刻重新起算，收招后 resetTime 内没出下一招才归零
    /// </summary>
    public void NotifyAttackEnded() => lastAttackTime = Time.time;

    // 顶层切换:Root 换子状态(行为树调用入口;同状态重复调用是幂等的)
    /// <summary>
    /// 切换 Root 层状态
    /// </summary>
    /// <param name="type">目标状态</param>
    /// <param name="forceRestart">目标就是当前状态时是否重入(Exit→Enter,重播动画);
    /// 行为树用它实现"硬直中再次受击 → 重新进入 Hurt 节点"</param>
    public void SwitchState(AIStateType type, bool forceRestart = false)
    {
        if (currentState is AIHierarchicalState root)
        {
            root.SwitchSubState(type, forceRestart);
        }
    }

    // 切换到 Attack 并指定进入哪个攻击子状态(行为树 Action:Attack 调用入口)
    public void EnterAttack(AIStateType attackType)
    {
        if (currentState is not AIHierarchicalState root) return;
        var attack = root.GetSubState(AIStateType.Attack) as AIHierarchicalState;
        if (attack == null) return;

        if (root.CurrentSubStateType == AIStateType.Attack)
        {
            // 已在 Attack 容器内:直接重入指定招式
            // (Root 层切换是幂等的,SetEntry 不会生效,必须直接切子状态;
            //  forceRestart 保证重复触发同一招时动画会重播)
            attack.SwitchSubState(attackType, forceRestart: true);
        }
        else
        {
            // 从 Idle/Walk 等状态进入:设入口再切容器
            attack.SetEntry(attackType);
            root.SwitchSubState(AIStateType.Attack);
        }
    }

    /// <summary>
    /// 当前攻击是否已收招(行为树攻击分支的判定条件之一):
    /// true = 在 Attack 中且当前招式已打完,行为树可以接下一招/切走;
    /// 不在 Attack 中返回 false
    /// </summary>
    public bool IsAttackFinished
    {
        get
        {
            if (CurrentRootState != AIStateType.Attack) return false;
            var attack = (currentState as AIHierarchicalState)?.GetSubState(AIStateType.Attack) as AIHierarchicalState;
            return attack?.CurrentSubState?.IsFinished ?? false;
        }
    }

    /// <summary>当前是否处于攻击进行中(收招前);连招超时检查用</summary>
    public bool IsAttacking => CurrentRootState == AIStateType.Attack && !IsAttackFinished;

    /// <summary>当前 Root 层状态(行为树 Conditional 读取用)</summary>
    public AIStateType? CurrentRootState =>
        (currentState as AIHierarchicalState)?.CurrentSubStateType;

    /// <summary>当前 Attack 子状态(不在 Attack 中则返回 null)</summary>
    public AIStateType? CurrentAttackState =>
        CurrentRootState == AIStateType.Attack
            ? ((currentState as AIHierarchicalState)?.GetSubState(AIStateType.Attack) as AIHierarchicalState)?.CurrentSubStateType
            : null;

    public string CurrentStateName => currentState?.StateName;

    public void TriggerDeath() => isDead = true;

    /// <summary>直线兜底索敌(行为树 AIHasTarget 是主索敌入口):目标层内找最近的一个</summary>
    public Transform FindTarget()
    {
        // NonAlloc 写入复用 buffer,不 new 数组 → 零 GC;物理计算不变
        int count = Physics.OverlapSphereNonAlloc(transform.position, detectRange, targetBuffer, targetLayer);
        if (count == targetBuffer.Length)
            Debug.LogWarning($"[AI] {name} 索敌命中数达 buffer 上限({count}),可能漏检,建议调大 targetBuffer", this);

        Transform nearest = null;
        float minDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Transform t = targetBuffer[i].transform;
            if (t.IsChildOf(transform)) continue;   // 排除自己及子物体
            float d = Vector3.Distance(transform.position, t.position);
            if (d < minDist)
            {
                minDist = d;
                nearest = t;
            }
        }
        return nearest;
    }

    // 决策用哪个攻击(行为树可直接调,也可自己定)
    // 配了连招:按序列索引依次出招,出招即 ++;没配连招:按距离/血量随机选(旧行为)
    public AIStateType ChooseAttack()
    {
        var combo = movementData?.comboData;
        if (combo != null && combo.comboSequence != null && combo.comboSequence.Count > 0)
        {
            AIStateType next = combo.comboSequence[comboIndex % combo.comboSequence.Count];
            comboIndex++;
            lastAttackTime = Time.time;
            Debug.Log($"[AI] 连招第{comboIndex}招 → {next}");
            return next;
        }

        if (target == null) return AIStateType.Attack1;

        float dist = Vector3.Distance(transform.position, target.position);
        float targetHp = GetTargetHpNormalized();

        if (dist <= (GetAttackData(AIStateType.Attack3)?.range ?? 3f) && targetHp < 0.3f)
            return AIStateType.Attack3;
        if (dist <= (GetAttackData(AIStateType.Attack2)?.range ?? 2.5f) && targetHp < 0.6f)
            return AIStateType.Attack2;

        return UnityEngine.Random.value < 0.5f ? AIStateType.Attack1 : AIStateType.Attack2;
    }

    private float GetTargetHpNormalized()
    {
        // TODO: 替换成实际敌人血量(如 HealthModel / DamageCalculator)
        return UnityEngine.Random.value;
    }

    // 辅助:判断目标是否在攻击范围内
    public bool TargetInRange(float range)
    {
        if (target == null) return false;
        return Vector3.Distance(transform.position, target.position) <= range + 0.3f;
    }

    // ================================================================
    // 伤害结算(走项目统一管道 DamageCalculator,模仿 CharacterCombo.ATK)
    // ================================================================

    /// <summary>
    /// 播放攻击动画并注册 Animancer 事件(与 PlayerComboState 的连击事件同款机制):
    /// - 每个命中帧时刻触发一次 onHit(伤害判定跟动画走,不靠秒表)
    /// - 动画播完触发一次 onEnd(收招时机跟动画走)
    /// - hitTimesSec 单位为秒,内部换算成 Animancer 的归一化时间(0~1)
    /// 返回播放状态(可用 Length 算冷却),未配动画返回 null(调用方退回秒表模式)
    /// </summary>
    public AnimancerState PlayAttackAnim(AIStateType type, float[] hitTimesSec, Action onHit, Action onEnd)
    {
        var state = PlayAnim(type);
        if (state == null) return null;

        // 同一片段复用同一个 AnimancerState,必须清掉残留事件,否则检查点/命中帧会重复触发
        state.Events.Clear();

        float length = Mathf.Max(state.Length, 0.01f);
        foreach (var t in hitTimesSec)
        {
            float normalized = Mathf.Clamp01(t / length);
            state.Events.Add(normalized, onHit);
        }
        state.Events.OnEnd = onEnd;
        return state;
    }

    /// <summary>对锁定目标结算一次伤害(目标必须在 range 内且存活)</summary>
    public void DealDamage(float range, float baseDamage)
    {
        if (target == null || !TargetInRange(range)) return;

        var health = target.GetComponentInParent<HealthModel>();
        if (health == null || !health.IsAlive) return;

        ApplyHit(health, baseDamage);
    }

    /// <summary>AOE 结算:radius 范围内所有目标层内的单位各吃一次伤害</summary>
    public void DealAoeDamage(float radius, float baseDamage)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, targetLayer);
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(transform)) continue;   // 排除自己及子物体

            var health = h.GetComponentInParent<HealthModel>();
            if (health == null || !health.IsAlive) continue;

            ApplyHit(health, baseDamage);
        }
    }

    /// <summary>单次命中结算:伤害管道 → 本地/网络分发</summary>
    private void ApplyHit(HealthModel health, float baseDamage)
    {
        var result = DamageCalculator.Calculate(new DamageContext
        {
            baseDamage = baseDamage,
            critRate = 0f,          // TODO: 需要暴击时把 critRate/critMultiplier 加进 AIAttackData
            critMultiplier = 1f,
            attacker = gameObject,
            defender = health.gameObject,
        });

        // 联网目标走网络伤害(转发到拥有者端结算);单机/未联网直接本地结算
        var attackerNetObj = GetComponentInParent<NetworkObject>();
        if (health.IsSpawned && attackerNetObj != null && attackerNetObj.IsSpawned)
            health.ApplyNetworkDamage(result.finalDamage, attackerNetObj.NetworkObjectId, result.isCritical);
        else
            health.TakeDamage(result.finalDamage, gameObject, result.isCritical);

        Debug.Log($"[AI] 命中 {health.name} 伤害 {result.finalDamage:F1}{(result.isCritical ? "(暴击)" : "")}");
    }
}

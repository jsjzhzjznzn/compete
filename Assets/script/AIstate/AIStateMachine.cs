using System.Collections.Generic;
using Animancer;
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

    [Header("目标查找（要攻击的对象 Tag）")]
    public string targetTag = "player";

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

    /// <summary>
    /// 构建状态机（由 AIPlayer.Awake 调用并传入 SO，保证建状态机前数据已就位；
    /// 组件引用也在这里取，不依赖本组件 Awake 的执行顺序）
    /// </summary>
    public void Build(AIPlayerSO so)
    {
        AiSO = so;
        animancer = GetComponent<AnimancerComponent>();
        characterController = GetComponent<CharacterController>();

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
    public void SwitchState(AIStateType type)
    {
        if (currentState is AIHierarchicalState root)
        {
            root.SwitchSubState(type);
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

    public Transform FindTarget()
    {
        var enemies = GameObject.FindGameObjectsWithTag(targetTag);
        Transform nearest = null;
        float minDist = float.MaxValue;
        foreach (var e in enemies)
        {
            if (e == gameObject) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d < minDist && d <= detectRange)
            {
                minDist = d;
                nearest = e.transform;
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

        return Random.value < 0.5f ? AIStateType.Attack1 : AIStateType.Attack2;
    }

    private float GetTargetHpNormalized()
    {
        // TODO: 替换成实际敌人血量(如 HealthModel / DamageCalculator)
        return Random.value;
    }

    // 辅助:判断目标是否在攻击范围内
    public bool TargetInRange(float range)
    {
        if (target == null) return false;
        return Vector3.Distance(transform.position, target.position) <= range + 0.3f;
    }
}

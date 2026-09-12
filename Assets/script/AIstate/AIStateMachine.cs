using System.Collections.Generic;
using Animancer;
using UnityEngine;

public class AIStateMachine : MonoBehaviour
{
    [Header("角色数据资产（动画/范围/伤害配置，Inspector 拖入）")]
    [SerializeField] private AIPlayerSO aiSO;

    /// <summary>角色数据资产（未配置时各参数用代码内默认值）</summary>
    public AIPlayerSO AiSO => aiSO;

    private AIMovementData movementData => aiSO != null ? aiSO.movementData : null;

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

    private Dictionary<string, AIState> states = new Dictionary<string, AIState>();
    private AIState currentState;

    [HideInInspector] public Transform target;
    [HideInInspector] public bool isDead = false;

    private CharacterController characterController;

    private void Awake()
    {
        animancer = GetComponent<AnimancerComponent>();
        characterController = GetComponent<CharacterController>();
        BuildStateMachine();
    }

    /// <summary>
    /// 播放状态动画（Animancer，与 Player 一致），片段来自 AIPlayerSO
    /// </summary>
    public void PlayAnim(AIStateType type)
    {
        AIStateData data = GetStateData(type);

        if (animancer == null || data?.animationClip == null)
        {
            Debug.LogWarning($"[AI] 状态 {type} 没有可播放的动画（未挂 Animancer 或 AIPlayerSO 未配 animationClip）", this);
            return;
        }

        var state = animancer.Play(data.animationClip, data.fadeDuration);
        state.Speed = data.playSpeed;
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

    private void BuildStateMachine()
    {
        // ---------- Root 顶层容器 ----------
        var root = new AIHierarchicalState(this, gameObject, AIStateType.Root, AIStateType.Idle);

        // ---------- Idle / Walk / Die ----------
        root.AddSubState(new IdleState(this, gameObject));
        root.AddSubState(new WalkState(this, gameObject));
        root.AddSubState(new DieState(this, gameObject));

        // ---------- Attack 分层容器(默认进 Attack1) ----------
        var attack = new AIHierarchicalState(this, gameObject, AIStateType.Attack, AIStateType.Attack1);
        attack.AddSubState(new AttackState1(this, gameObject, attack));
        attack.AddSubState(new AttackState2(this, gameObject, attack));
        attack.AddSubState(new AttackState3(this, gameObject, attack));
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
    }

    // 顶层切换:Root 换子状态(行为树调用入口;同状态重复调用是幂等的)
    public void SwitchState(AIStateType type)
    {
        if (currentState is AIHierarchicalState root)
        {
            root.SwitchSubState(type);
        }
    }

    // 切换到 Attack 并指定进入哪个攻击子状态(行为树调用入口)
    public void EnterAttack(AIStateType attackType)
    {
        if (currentState is AIHierarchicalState root)
        {
            var attack = root.GetSubState(AIStateType.Attack) as AIHierarchicalState;
            if (attack != null)
            {
                attack.SetEntry(attackType);   // 设定入口
            }
            root.SwitchSubState(AIStateType.Attack);
        }
    }

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
    public AIStateType ChooseAttack()
    {
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

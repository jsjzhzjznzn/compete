using UnityEngine;

/// <summary>
/// AI 角色（模仿 Player 的"身体"，但不接输入）：
/// - 继承 CharacterMoveControllerBase：复用重力/地面检测/斜坡修正
/// - Animancer 播放动画（与 Player 一致，片段在 AIStateMachine Inspector 上配置）
/// - HealthModel 血量，死亡事件 → 通知 AIStateMachine 切 Die
/// - 决策完全由同物体上的 AIStateMachine（分层状态机）驱动，不碰 CharacterInputSystem
/// 挂载：复制 Player 预制体，移除 Player 脚本，挂 AIPlayer（AIStateMachine 会自动要求）
/// </summary>
[RequireComponent(typeof(AIStateMachine))]
public class AIPlayer : CharacterMoveControllerBase
{
    [Header("目标查找（状态机搜索该 Tag 作为攻击目标）")]
    [SerializeField] private string targetTag = "Enemy";

    private AIStateMachine aiStateMachine;
    private HealthModel health;

    /// <summary>AI 决策状态机</summary>
    public AIStateMachine AIStateMachine => aiStateMachine;

    /// <summary>是否已死亡</summary>
    public bool IsDead => aiStateMachine != null && aiStateMachine.isDead;

    protected override void Awake()
    {
        base.Awake();
        aiStateMachine = GetComponent<AIStateMachine>();

        // 血量组件：预制体上应已挂好（联网组件不能运行时 AddComponent），漏挂只警告
        health = GetComponent<HealthModel>();
        if (health == null)
            Debug.LogWarning($"[{name}] 缺少 HealthModel，AI 不会掉血/死亡，请在预制体上挂好", this);

        // Buff 组件兜底挂载（与 Player 一致：增伤/减伤/持续伤害都依赖它）
        if (GetComponent<BuffComponent>() == null)
            gameObject.AddComponent<BuffComponent>();
    }

    protected override void Start()
    {
        base.Start();
        if (aiStateMachine != null)
            aiStateMachine.targetTag = targetTag;
    }

    private void OnEnable()
    {
        // 死亡事件（HealthModel 派发 E_OnDeath）
        EventCenter.MainInstance.AddListener<DeathData>(E_EventType.E_OnDeath, this, OnDeath);
    }

    private void OnDisable()
    {
        EventCenter.MainInstance.UnregisterTarget(this);
    }

    private void OnDeath(DeathData data)
    {
        if (data.target != gameObject) return;   // 只处理自己的死亡
        if (IsDead) return;
        aiStateMachine?.TriggerDeath();
    }

    // AI 的水平位移由 AIStateMachine 通过 CharacterController 驱动，
    // 不应用动画根运动水平位移（避免双重位移）；竖直方向仍走基类重力
    protected override void OnAnimatorMove() { }
}

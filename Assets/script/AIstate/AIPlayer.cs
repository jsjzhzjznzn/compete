using UnityEngine;

/// <summary>
/// AI 角色（模仿 Player 的"身体"，但不接输入）：
/// - 继承 CharacterMoveControllerBase：复用重力/地面检测/斜坡修正
/// - 持有 AIPlayerSO 角色数据资产（模仿 Player 持有 PlayerSO），Awake 时传给状态机构建
/// - Animancer 播放动画（与 Player 一致，片段在 AIPlayerSO 上配置）
/// - HealthModel 血量，死亡事件 → 通知 AIStateMachine 切 Die
/// - 决策完全由同物体上的 AIStateMachine（分层状态机）驱动，不碰 CharacterInputSystem
/// 挂载：复制 Player 预制体，移除 Player 脚本，挂 AIPlayer（AIStateMachine 会自动要求）
/// </summary>
[RequireComponent(typeof(AIStateMachine))]
public class AIPlayer : CharacterMoveControllerBase
{
    [Header("角色数据资产（动画/连招/伤害配置，Inspector 拖入）")]
    [SerializeField] private AIPlayerSO aiSO;

    /// <summary>角色数据资产（模仿 PlayerSO 属性暴露方式）</summary>
    public AIPlayerSO AIPlayerSO => aiSO;

    // 角色音效组件缓存（懒获取：首次访问时 GetComponent 并缓存，避免状态机频繁调用重复查找）
    private ActorAudioComponent actorAudio;

    /// <summary>角色音效组件（播放 3D 空间音效用；未挂载返回 null）</summary>
    public ActorAudioComponent ActorAudio
    {
        get
        {
            // 本对象可能已被销毁（切场景/退出战斗后的残留状态机回调会访问到这里）：
            // Unity 的 == 能判"已销毁"，先挡掉，避免走进 GetComponent 抛 MissingReferenceException
            if (this == null) return null;
            if (actorAudio == null)
            {
                actorAudio = GetComponent<ActorAudioComponent>();
            }
            return actorAudio;
        }
    }

    [Header("目标查找（状态机搜索该 Tag 作为攻击目标）")]
    [SerializeField] private string targetTag = "player";

    // ============ 组件引用 ============
    private AIStateMachine aiStateMachine;   // AI 决策状态机（同物体上,Awake 里构建）
    private HealthModel health;              // 血量组件（E_OnDeath 派发源,预制体上挂好）

    /// <summary>AI 决策状态机</summary>
    public AIStateMachine AIStateMachine => aiStateMachine;

    /// <summary>是否已死亡</summary>
    public bool IsDead => aiStateMachine != null && aiStateMachine.isDead;

    // ================================================================
    // 生命周期
    // ================================================================

    protected override void Awake()
    {
        base.Awake();
        aiStateMachine = GetComponent<AIStateMachine>();

        // 先传 SO 构建状态机（模仿 Player.Awake 里 new PlayerMovementStateMachine(this, playerSO)）
        aiStateMachine.Build(aiSO);
        aiStateMachine.targetTag = targetTag;

        // 血量组件：预制体上应已挂好（联网组件不能运行时 AddComponent），漏挂只警告
        health = GetComponent<HealthModel>();
        if (health == null)
            Debug.LogWarning($"[{name}] 缺少 HealthModel，AI 不会掉血/死亡，请在预制体上挂好", this);

        // Buff 组件兜底挂载（与 Player 一致：增伤/减伤/持续伤害都依赖它）
        if (GetComponent<BuffComponent>() == null)
            gameObject.AddComponent<BuffComponent>();
    }

    private void OnEnable()
    {
        // 死亡事件（HealthModel 派发 E_OnDeath）
        EventCenter.MainInstance.AddListener<DeathData>(E_EventType.E_OnDeath, this, OnDeath);
        // 受击事件（HealthModel.TakeDamage 派发 E_OnDamage）→ 反射进硬直
        EventCenter.MainInstance.AddListener<DamageData>(E_EventType.E_OnDamage, this, OnDamageTaken);
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

    /// <summary>受击入口:只发受击请求(置标记+计数),不直接切状态(切状态是行为树硬直分支的事)。
    /// DoT(灼烧类持续伤害)不触发,与 Player 一致</summary>
    private void OnDamageTaken(DamageData data)
    {
        if (IsDead) return;   // 死亡后不接受受击
        if (data.target == gameObject && !data.isDoT)
            aiStateMachine.RequestHurt();
    }
}

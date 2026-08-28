using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 血量组件（MonoBehaviour），挂载角色身上
///
/// 职责：血量数据的存储 + 加减血业务逻辑。
/// 服务器权威：联网时血量/无敌状态只在服务器结算（IsServer），
/// 结算结果通过 NetworkVariable(Server 写) 广播给所有端刷新血条，
/// 客户端表现（受击/飘字/震屏/死亡）由服务器 ClientRpc 广播通知各端，订阅方按 data.target 过滤。
/// 单机模式（未 spawn）：全部走本地链路，事件本地派发，行为与迁移前一致。
///
/// 事件（走 EventCenter 全局总线，不设本地 C# 事件中转）：
///   - 扣血 → E_OnDamage（DamageData）：血条/飘字/震屏订阅
///   - 死亡 → E_OnDeath（DeathData）：生命归零派发一次
/// 血条 UI 同时直接订阅 CurrentHP / MaxHP 的 BindableProperty 变化刷新。
/// </summary>
public class HealthModel : NetworkBehaviour
{
    [Header("初始属性")]
    [SerializeField] private float initialMaxHP = 100f;
    [SerializeField] private float initialHP = 100f;

    // ============ 属性（外部只读属性值，修改必须走 TakeDamage / Heal / SetMaxHP / ResetHealth） ============

    /// <summary>当前血量（外部只读）</summary>
    public BindableProperty<float> CurrentHP { get; } = new();

    /// <summary>最大血量（外部只读）</summary>
    public BindableProperty<float> MaxHP { get; } = new();

    // ============ 网络同步（服务器写入，全员可读） ============

    /// <summary>当前血量网络同步变量（服务器写入；远程端据此刷新本地血条）</summary>
    private readonly NetworkVariable<float> netCurrentHP =
        new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>最大血量网络同步变量（服务器写入）</summary>
    private readonly NetworkVariable<float> netMaxHP =
        new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>是否存活</summary>
    public bool IsAlive => CurrentHP.Value > 0f;

    private bool _isDead;               // 死亡锁（服务器维护，保证死亡事件只结算一次）
    private bool _isInvincible;         // 是否处于无敌窗口（闪避等）
    private GameTimer _invincibleTimer; // 无敌计时器（服务器计时，到期自动关闭）

    /// <summary>服务器端闪避冷却：下次允许开无敌的 ServerTime 时间点（防连点/改内存刷无敌）</summary>
    private float _nextInvincibleAllowedServerTime;

    /// <summary>单次伤害上限（基础校验用；真正的数值权威在阶段 2：服务器按攻击配置重算伤害）</summary>
    private const float MaxHitDamage = 99999f;

    private void Awake()
    {
        ResetHealth(initialHP, initialMaxHP);
    }

    // ============ 网络同步：血量值广播给所有端（远程端血条实时刷新） ============

    public override void OnNetworkSpawn()
    {
        netCurrentHP.OnValueChanged += OnNetHPChanged;
        netMaxHP.OnValueChanged += OnNetHPChanged;

        // 服务器把初始血量推上网络；远程端随后收到并刷新本地血条
        if (IsServer) SyncHPToNetwork();
    }

    public override void OnNetworkDespawn()
    {
        netCurrentHP.OnValueChanged -= OnNetHPChanged;
        netMaxHP.OnValueChanged -= OnNetHPChanged;
    }

    /// <summary>网络血量变化 → 刷新本地 BindableProperty（血条 UI 订阅的就是它）；服务器本地值即权威，不回写</summary>
    private void OnNetHPChanged(float previous, float current)
    {
        if (IsServer) return;
        CurrentHP.Value = netCurrentHP.Value;
        MaxHP.Value = netMaxHP.Value;
    }

    /// <summary>把本地血量推送到网络变量（只有服务器能写；单机未 spawn 时为空操作）</summary>
    private void SyncHPToNetwork()
    {
        if (!IsSpawned || !IsServer) return;
        netCurrentHP.Value = CurrentHP.Value;
        netMaxHP.Value = MaxHP.Value;
    }

    // ============ 网络伤害入口（攻击者端命中远程玩家时走这里，服务器结算） ============

    /// <summary>
    /// 网络伤害入口：攻击者（任意端）命中远程玩家时调用。
    /// 伤害通过 ServerRpc 发到服务器，服务器校验后结算，结算结果：
    ///   - NetworkVariable 广播血量（所有端血条刷新）
    ///   - 定向 ClientRpc 通知目标拥有者端派发表现事件（受击/飘字/震屏/死亡）
    /// 单机模式（未 spawn）直接本地结算，保持原逻辑。
    /// </summary>
    /// <param name="amount">扣血量</param>
    /// <param name="sourceId">伤害来源 NetworkObjectId（未 spawn 时忽略）</param>
    /// <param name="isCritical">是否暴击（飘字样式区分）</param>
    /// <param name="isDoT">是否持续伤害（Buff 灼烧类 tick；订阅方据此跳过受击硬直/闪避触发）</param>
    public void ApplyNetworkDamage(float amount, ulong sourceId, bool isCritical = false, bool isDoT = false)
    {
        if (!IsSpawned)
        {
            // 单机模式：攻击者即本地玩家，直接走本地结算链路
            TakeDamage(amount, GetSourceObject(sourceId), isCritical, isDoT);
            return;
        }
        SendDamageServerRpc(amount, sourceId, isCritical, isDoT);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendDamageServerRpc(float amount, ulong sourceId, bool isCritical, bool isDoT)
    {
        if (!IsServer) return;

        // 基础校验：数值非法/超上限直接丢弃（阶段 2 换成服务器按攻击配置重算伤害，这里才是真正的数值权威）
        if (!float.IsFinite(amount) || amount <= 0f || amount > MaxHitDamage) return;

        TakeDamage(amount, GetSourceObject(sourceId), isCritical, isDoT);   // 服务器结算 + 内部定向通知
    }

    // ============ 扣血逻辑（联网时只在服务器执行） ============

    /// <summary>
    /// 扣血逻辑：联网时只在服务器执行（IsServer），修改血量并写入网络变量，
    /// 然后定向 ClientRpc 通知目标拥有者端派发表现事件；
    /// 单机（未 spawn）直接本地结算并本地派发事件（避免双端重复表现）。
    /// </summary>
    /// <param name="amount">扣血量</param>
    /// <param name="source">伤害来源（可为 null）</param>
    /// <param name="isCritical">是否暴击（飘字样式区分）</param>
    /// <param name="isDoT">是否持续伤害（Buff 灼烧类 tick；订阅方据此跳过受击硬直/闪避触发）</param>
    public void TakeDamage(float amount, GameObject source = null, bool isCritical = false, bool isDoT = false)
    {
        if (!IsAlive || _isDead) return;
        // 血量只在服务器结算；单机（未 spawn）保持本地结算
        if (IsSpawned && !IsServer) return;

        // 无敌窗口（闪避等）：普通打击被拦下（闪避触发/UI 表现听 E_DamageBlocked）。
        // DoT（灼烧类）无视无敌：闪避只能躲开"一下下的打击"，解不了已经挂在身上的持续伤害
        if (IsInvincible && !isDoT)
        {
            if (!IsSpawned)
            {
                // 单机直接派发闪避触发事件
                EventCenter.MainInstance.Dispatch(E_EventType.E_DamageBlocked, new DamageData
                {
                    source = source != null ? source : gameObject,
                    target = gameObject,
                    amount = amount,
                    isCritical = isCritical,
                    isDoT = isDoT
                });
                return;
            }

            // 服务器：拦下伤害的同时授予闪避动画全程无敌（闪避由被打触发，此时即将进入闪避状态）
            GrantDodgeInvincible();
            NotifyDamageClientRpc(GetSourceId(source), amount, isCritical, isDoT, true, false);
            return;
        }

        float nextHP = Mathf.Max(0f, CurrentHP.Value - amount);
        CurrentHP.Value = nextHP;
        SyncHPToNetwork();   // 血量变化推上网络，远程端血条同步

        // 死亡：生命归零只结算一次（服务器维护死亡锁）
        bool dead = nextHP <= 0f && !_isDead;
        if (dead) _isDead = true;

        if (!IsSpawned)
        {
            // 单机：本地派发受伤/死亡事件（飘字/受击/闪避订阅）
            EventCenter.MainInstance.Dispatch(E_EventType.E_OnDamage, new DamageData
            {
                source = source != null ? source : gameObject,
                target = gameObject,
                amount = amount,
                isCritical = isCritical,
                isDoT = isDoT
            });
            if (dead)
            {
                EventCenter.MainInstance.Dispatch(E_EventType.E_OnDeath, new DeathData
                {
                    target = gameObject
                });
            }
            return;
        }

        // 联网：广播通知所有端做表现（订阅方按 data.target 过滤，只在自己屏幕上表现对应角色）
        NotifyDamageClientRpc(GetSourceId(source), amount, isCritical, isDoT, false, dead);
    }

    /// <summary>
    /// 伤害表现通知（全员执行）：本地派发 E_DamageBlocked / E_OnDamage / E_OnDeath。
    /// 各订阅方按 data.target 过滤：
    ///   - Player.OnDamageTaken / OnDamageBlocked 只响应自己的角色（受击硬直/闪避只在被击者端切状态机）
    ///   - DamageTextManager 按受击者世界坐标飘字，攻击者端也能看到自己打出的伤害数字
    /// </summary>
    [ClientRpc]
    private void NotifyDamageClientRpc(ulong sourceId, float amount, bool isCritical, bool isDoT, bool isBlocked, bool isDead)
    {
        var source = GetSourceObject(sourceId);

        if (isBlocked)
        {
            // 无敌窗口内被打中：伤害已拦下，触发闪避（Player.OnDamageBlocked 订阅）
            EventCenter.MainInstance.Dispatch(E_EventType.E_DamageBlocked, new DamageData
            {
                source = source != null ? source : gameObject,
                target = gameObject,
                amount = amount,
                isCritical = isCritical,
                isDoT = isDoT
            });
            return;
        }

        // 受伤表现（Player.OnDamageTaken → 受击硬直；DamageTextManager → 飘字；CameraHitFeel → 震屏）
        EventCenter.MainInstance.Dispatch(E_EventType.E_OnDamage, new DamageData
        {
            source = source != null ? source : gameObject,
            target = gameObject,
            amount = amount,
            isCritical = isCritical,
            isDoT = isDoT
        });

        // 死亡表现（服务器已用 _isDead 保证只结算一次，这里只会收到一次）
        if (isDead)
        {
            EventCenter.MainInstance.Dispatch(E_EventType.E_OnDeath, new DeathData
            {
                target = gameObject
            });
        }
    }

    /// <summary>伤害来源 GameObject → NetworkObjectId（服务器端从结算上下文解析用）</summary>
    private static ulong GetSourceId(GameObject source)
    {
        if (source == null) return 0;
        var netObj = source.GetComponentInParent<NetworkObject>();
        return netObj != null ? netObj.NetworkObjectId : 0;
    }

    /// <summary>按 NetworkObjectId 查伤害来源 GameObject（查不到返回 null）</summary>
    private static GameObject GetSourceObject(ulong sourceId)
    {
        if (sourceId == 0 || NetworkManager.Singleton == null) return null;
        return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(sourceId, out var sourceNo)
            ? sourceNo.gameObject
            : null;
    }

    /// <summary>授予闪避动画全程无敌（服务器在拦下伤害时调用；时长 = 闪避动画长度，读角色配置）</summary>
    private void GrantDodgeInvincible()
    {
        var player = GetComponent<Player>();
        var clip = player?.PlayerSO?.movementData?.dodgeData?.animationClip;
        SetInvincible(clip != null ? clip.length : 0.5f);
    }

    // ============ 回血逻辑 ============

    /// <summary>回血逻辑（血条 UI 通过 CurrentHP/MaxHP 的 BindableProperty 自动刷新，无需额外事件）</summary>
    public void Heal(float amount)
    {
        if (!IsAlive || _isDead) return;
        if (IsSpawned && !IsServer) return;   // 血量只在服务器结算

        CurrentHP.Value = Mathf.Min(MaxHP.Value, CurrentHP.Value + amount);
        SyncHPToNetwork();   // 回血也要同步给远程端
    }

    // ============ 无敌 ============

    /// <summary>当前是否无敌（无敌窗口内 TakeDamage 被拦截，不掉血）</summary>
    public bool IsInvincible => _isInvincible;

    /// <summary>
    /// 请求开启无敌窗口（拥有者端输入调用）：联网时发 ServerRpc 由服务器开窗（带服务器冷却限流），
    /// 单机直接本地开。
    /// </summary>
    public void RequestInvincible(float windowSeconds, float cooldownSeconds)
    {
        if (!IsSpawned)
        {
            SetInvincible(windowSeconds);
            return;
        }
        RequestInvincibleServerRpc(windowSeconds, cooldownSeconds);
    }

    [ServerRpc]
    private void RequestInvincibleServerRpc(float windowSeconds, float cooldownSeconds)
    {
        if (!IsServer) return;

        // 参数钳制 + 服务器端冷却：防连点/改内存无限无敌
        windowSeconds = Mathf.Clamp(windowSeconds, 0.05f, 1f);
        cooldownSeconds = Mathf.Clamp(cooldownSeconds, 0f, 10f);
        float now = (float)NetworkManager.ServerTime.Time;
        if (now < _nextInvincibleAllowedServerTime) return;
        _nextInvincibleAllowedServerTime = now + cooldownSeconds;

        SetInvincible(windowSeconds);
    }

    /// <summary>设置无敌时间（秒）：联网时只在服务器计时，到期自动关闭无敌</summary>
    public void SetInvincible(float seconds)
    {
        if (seconds <= 0f) return;
        if (IsSpawned && !IsServer) return;   // 无敌状态只在服务器维护

        // 取消上一次计时（若仍在跑），重新计时
        if (_invincibleTimer != null)
            TimerManager.MainInstance.UnregisterTimer(_invincibleTimer);

        _isInvincible = true;
        _invincibleTimer = TimerManager.MainInstance.GetOneTimer(seconds, () =>
        {
            _isInvincible = false;
            _invincibleTimer = null;
        });
    }

    // ============ 属性修改 ============

    /// <summary>修改最大血量上限</summary>
    public void SetMaxHP(float value)
    {
        if (IsSpawned && !IsServer) return;   // 血量只在服务器结算

        MaxHP.Value = Mathf.Max(0f, value);
        CurrentHP.Value = Mathf.Min(CurrentHP.Value, MaxHP.Value);
        SyncHPToNetwork();   // 上限变化同步给远程端
    }

    /// <summary>重置血量（复活/读档使用）</summary>
    public void ResetHealth(float current, float max)
    {
        if (IsSpawned && !IsServer) return;   // 血量只在服务器结算

        max = Mathf.Max(0f, max);
        current = Mathf.Clamp(current, 0f, max);
        MaxHP.Value = max;
        CurrentHP.Value = current;
        _isDead = false;
        SyncHPToNetwork();   // 复活/重置后同步给远程端
    }
}

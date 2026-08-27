using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 血量组件（MonoBehaviour），挂载角色身上
///
/// 职责：血量数据的存储 + 加减血业务逻辑，事件直接派发到全局事件中心。
/// 每个角色各自挂一份，血量互不共享（无全局单例，天然支持 1v1 双方各挂一个）。
///
/// 网络：血量在拥有者端结算（权威），结算结果通过 NetworkVariable 广播给所有端，
/// 远程端据此刷新本地血条显示（CurrentHP / MaxHP 变化驱动 UI）。
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

    // ============ 网络同步（拥有者写入，全员可读） ============

    /// <summary>当前血量网络同步变量（拥有者写入；远程端据此刷新本地血条）</summary>
    private readonly NetworkVariable<float> netCurrentHP =
        new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    /// <summary>最大血量网络同步变量（拥有者写入）</summary>
    private readonly NetworkVariable<float> netMaxHP =
        new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    /// <summary>是否存活</summary>
    public bool IsAlive => CurrentHP.Value > 0f;

    private bool _isDead; // 死亡锁，防止多次触发死亡事件

    private bool _isInvincible;        // 是否处于无敌窗口（闪避等）
    private GameTimer _invincibleTimer; // 无敌计时器（到期自动关闭无敌）

    private void Awake()
    {
        ResetHealth(initialHP, initialMaxHP);
    }

    // ============ 网络同步：血量值广播给所有端（远程端血条实时刷新） ============

    public override void OnNetworkSpawn()
    {
        netCurrentHP.OnValueChanged += OnNetHPChanged;
        netMaxHP.OnValueChanged += OnNetHPChanged;

        // 拥有者端把本地初始血量推上网络；远程端随后收到并刷新本地血条
        if (IsOwner) SyncHPToNetwork();
    }

    public override void OnNetworkDespawn()
    {
        netCurrentHP.OnValueChanged -= OnNetHPChanged;
        netMaxHP.OnValueChanged -= OnNetHPChanged;
    }

    /// <summary>网络血量变化 → 刷新本地 BindableProperty（血条 UI 订阅的就是它）</summary>
    private void OnNetHPChanged(float previous, float current)
    {
        if (IsOwner) return;   // 拥有者端本地值即权威，无需回写
        CurrentHP.Value = netCurrentHP.Value;
        MaxHP.Value = netMaxHP.Value;
    }

    /// <summary>把本地血量推送到网络变量（所有改血入口在改完后调用；单机未 spawn 时为空操作）</summary>
    private void SyncHPToNetwork()
    {
        if (!IsSpawned || !IsOwner) return;
        netCurrentHP.Value = CurrentHP.Value;
        netMaxHP.Value = MaxHP.Value;
    }

    // ============ 网络伤害入口（攻击者端命中远程玩家时走这里） ============

    /// <summary>
    /// 网络伤害入口：攻击者（任意端）命中远程玩家时调用。
    /// 血量只在拥有者端结算，这里把伤害通过 ServerRpc → ClientRpc 转发到目标拥有者端执行本地 TakeDamage。
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
        ApplyDamageServerRpc(amount, sourceId, isCritical, isDoT);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ApplyDamageServerRpc(float amount, ulong sourceId, bool isCritical, bool isDoT)
    {
        // 服务器不直接结算血量（血量归拥有者端），只做转发
        ApplyDamageClientRpc(amount, sourceId, isCritical, isDoT);
    }

    [ClientRpc]
    private void ApplyDamageClientRpc(float amount, ulong sourceId, bool isCritical, bool isDoT)
    {
        if (!IsOwner) return;   // 只有目标拥有者端执行本地结算（受击/飘字/血条全走本地链路）
        TakeDamage(amount, GetSourceObject(sourceId), isCritical, isDoT);
    }

    /// <summary>按 NetworkObjectId 查伤害来源 GameObject（查不到返回 null）</summary>
    private static GameObject GetSourceObject(ulong sourceId)
    {
        if (sourceId == 0 || NetworkManager.Singleton == null) return null;
        return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(sourceId, out var sourceNo)
            ? sourceNo.gameObject
            : null;
    }

    // ============ 扣血逻辑 ============

    /// <summary>扣血逻辑：直接派发 E_OnDamage / E_OnDeath 到全局事件中心</summary>
    /// <param name="amount">扣血量</param>
    /// <param name="source">伤害来源（可为 null）</param>
    /// <param name="isCritical">是否暴击（飘字样式区分）</param>
    /// <param name="isDoT">是否持续伤害（Buff 灼烧类 tick；订阅方据此跳过受击硬直/闪避触发）</param>
    public void TakeDamage(float amount, GameObject source = null, bool isCritical = false, bool isDoT = false)
    {
        if (!IsAlive || _isDead) return;
        // 血量只在拥有者端结算（网络伤害走 RPC 转发到拥有者端执行）
        if (IsSpawned && !IsOwner) return;

        // 无敌窗口（闪避等）：普通打击被拦下，派发 E_DamageBlocked（闪避触发/UI 表现听这个）。
        // DoT（灼烧类）无视无敌：闪避只能躲开"一下下的打击"，解不了已经挂在身上的持续伤害
        if (IsInvincible && !isDoT)
        {
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

        float nextHP = Mathf.Max(0f, CurrentHP.Value - amount);
        CurrentHP.Value = nextHP;
        SyncHPToNetwork();   // 血量变化推上网络，远程端血条同步

        // 派发受伤事件（飘字/受击/闪避订阅）
        EventCenter.MainInstance.Dispatch(E_EventType.E_OnDamage, new DamageData
        {
            source = source != null ? source : gameObject,
            target = gameObject,
            amount = amount,
            isCritical = isCritical,
            isDoT = isDoT
        });

        // 死亡：生命归零只派发一次
        if (nextHP <= 0f && !_isDead)
        {
            _isDead = true;
            EventCenter.MainInstance.Dispatch(E_EventType.E_OnDeath, new DeathData
            {
                target = gameObject
            });
        }
    }

    // ============ 回血逻辑 ============

    /// <summary>回血逻辑（血条 UI 通过 CurrentHP/MaxHP 的 BindableProperty 自动刷新，无需额外事件）</summary>
    public void Heal(float amount)
    {
        if (!IsAlive || _isDead) return;
        if (IsSpawned && !IsOwner) return;   // 血量只在拥有者端结算

        CurrentHP.Value = Mathf.Min(MaxHP.Value, CurrentHP.Value + amount);
        SyncHPToNetwork();   // 回血也要同步给远程端
    }

    // ============ 无敌 ============

    /// <summary>当前是否无敌（无敌窗口内 TakeDamage 被拦截，不掉血）</summary>
    public bool IsInvincible => _isInvincible;

    /// <summary>设置无敌时间（秒）：重启计时器，到期自动关闭无敌</summary>
    public void SetInvincible(float seconds)
    {
        if (seconds <= 0f) return;
        if (IsSpawned && !IsOwner) return;   // 无敌状态只在拥有者端维护

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
        if (IsSpawned && !IsOwner) return;   // 血量只在拥有者端结算

        MaxHP.Value = Mathf.Max(0f, value);
        CurrentHP.Value = Mathf.Min(CurrentHP.Value, MaxHP.Value);
        SyncHPToNetwork();   // 上限变化同步给远程端
    }

    /// <summary>重置血量（复活/读档使用）</summary>
    public void ResetHealth(float current, float max)
    {
        if (IsSpawned && !IsOwner) return;   // 血量只在拥有者端结算

        max = Mathf.Max(0f, max);
        current = Mathf.Clamp(current, 0f, max);
        MaxHP.Value = max;
        CurrentHP.Value = current;
        _isDead = false;
        SyncHPToNetwork();   // 复活/重置后同步给远程端
    }
}

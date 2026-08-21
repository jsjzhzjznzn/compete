using UnityEngine;

/// <summary>
/// 血量组件（MonoBehaviour），挂载角色身上
///
/// 职责：血量数据的存储 + 加减血业务逻辑，事件直接派发到全局事件中心。
/// 每个角色各自挂一份，血量互不共享（无全局单例，天然支持 1v1 双方各挂一个）。
///
/// 事件（走 EventCenter 全局总线，不设本地 C# 事件中转）：
///   - 扣血 → E_OnDamage（DamageData）：血条/飘字/震屏订阅
///   - 死亡 → E_OnDeath（DeathData）：生命归零派发一次
/// 血条 UI 同时直接订阅 CurrentHP / MaxHP 的 BindableProperty 变化刷新。
/// </summary>
public class HealthModel : MonoBehaviour
{
    [Header("初始属性")]
    [SerializeField] private float initialMaxHP = 100f;
    [SerializeField] private float initialHP = 100f;

    // ============ 属性（外部只读属性值，修改必须走 TakeDamage / Heal / SetMaxHP / ResetHealth） ============

    /// <summary>当前血量（外部只读）</summary>
    public BindableProperty<float> CurrentHP { get; } = new();

    /// <summary>最大血量（外部只读）</summary>
    public BindableProperty<float> MaxHP { get; } = new();

    /// <summary>是否存活</summary>
    public bool IsAlive => CurrentHP.Value > 0f;

    private bool _isDead; // 死亡锁，防止多次触发死亡事件

    private void Awake()
    {
        ResetHealth(initialHP, initialMaxHP);
    }

    // ============ 扣血逻辑 ============

    /// <summary>扣血逻辑：直接派发 E_OnDamage / E_OnDeath 到全局事件中心</summary>
    public void TakeDamage(float amount, GameObject source = null, bool isCritical = false)
    {
        if (!IsAlive || _isDead) return;

        float nextHP = Mathf.Max(0f, CurrentHP.Value - amount);
        CurrentHP.Value = nextHP;

        // 派发受伤事件（血条/飘字/震屏订阅）
        EventCenter.MainInstance.Dispatch(E_EventType.E_OnDamage, new DamageData
        {
            source = source != null ? source : gameObject,
            target = gameObject,
            amount = amount,
            currentHP = nextHP,
            maxHP = MaxHP.Value,
            isCritical = isCritical
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

        CurrentHP.Value = Mathf.Min(MaxHP.Value, CurrentHP.Value + amount);
    }

    // ============ 属性修改 ============

    /// <summary>修改最大血量上限</summary>
    public void SetMaxHP(float value)
    {
        MaxHP.Value = Mathf.Max(0f, value);
        CurrentHP.Value = Mathf.Min(CurrentHP.Value, MaxHP.Value);
    }

    /// <summary>重置血量（复活/读档使用）</summary>
    public void ResetHealth(float current, float max)
    {
        max = Mathf.Max(0f, max);
        current = Mathf.Clamp(current, 0f, max);
        MaxHP.Value = max;
        CurrentHP.Value = current;
        _isDead = false;
    }
}

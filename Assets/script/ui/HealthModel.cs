using UnityEngine;

/// <summary>
/// 血量组件 Model 层，挂载角色身上，只处理业务数据逻辑，无UI依赖
/// </summary>
public class HealthModel : MonoBehaviour
{
    [Header("初始血量")]
    [SerializeField] private float initialMaxHP = 100f;
    [SerializeField] private float initialHP = 100f;

    /// <summary>当前血量（外部只读，修改必须走 TakeDamage / Heal / SetMaxHP）</summary>
    public BindableProperty<float> CurrentHP { get; } = new();
    /// <summary>最大血量（外部只读）</summary>
    public BindableProperty<float> MaxHP { get; } = new();

    public bool IsAlive => CurrentHP.Value > 0f;

    private bool _isDead; // 死亡锁，防止多次触发死亡事件

    private void Awake()
    {
        MaxHP.Value = initialMaxHP;
        CurrentHP.Value = Mathf.Clamp(initialHP, 0f, MaxHP.Value);
        _isDead = false;
    }

    /// <summary>扣血逻辑</summary>
    public void TakeDamage(float amount, GameObject source = null, bool isCritical = false)
    {
        if (!IsAlive || _isDead) return;

        float nextHP = Mathf.Max(0f, CurrentHP.Value - amount);
        CurrentHP.Value = nextHP;

        // 派发受伤事件，携带自身对象
        EventCenter.MainInstance.Dispatch(E_EventType.E_OnDamage, new DamageData
        {
            target = gameObject,
            source = source != null ? source : gameObject,
            amount = amount,
            currentHP = nextHP,
            maxHP = MaxHP.Value,
            isCritical = isCritical
        });

        if (nextHP <= 0f && !_isDead)
        {
            _isDead = true;
            EventCenter.MainInstance.Dispatch(E_EventType.E_OnDeath, new DeathData
            {
                target = gameObject
            });
        }
    }

    /// <summary>回血逻辑</summary>
    public void Heal(float amount)
    {
        if (!IsAlive || _isDead) return;
        CurrentHP.Value = Mathf.Min(MaxHP.Value, CurrentHP.Value + amount);
    }

    /// <summary>修改最大血量上限</summary>
    public void SetMaxHP(float value)
    {
        MaxHP.Value = Mathf.Max(0f, value);
        CurrentHP.Value = Mathf.Min(CurrentHP.Value, MaxHP.Value);
    }

    /// <summary>重置血量（复活使用）</summary>
    public void ResetHealth(float current, float max)
    {
        max = Mathf.Max(0, max);
        current = Mathf.Clamp(current, 0, max);
        MaxHP.Value = max;
        CurrentHP.Value = current;
        _isDead = false;
    }
}

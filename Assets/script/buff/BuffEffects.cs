using UnityEngine;

// ============================================================================
// Buff 效果策略具体实现（每类一个策略资产，Inspector 右键 Create/Buff/... 创建）
// 新增效果类型：新建一个继承 BuffEffect 的类即可，BuffComponent 不用改。
// ============================================================================

/// <summary>
/// 增伤策略：提升造成的伤害（乘区）
/// 资产配置：每层增伤系数，如 0.3 = 伤害 +30%
/// </summary>
[CreateAssetMenu(fileName = "BuffDamageUp", menuName = "Create/Buff/增伤")]
public class BuffDamageUpEffect : BuffEffect
{
    [SerializeField, Header("每层增伤系数（0.3 = 伤害 +30%）")]
    private float _percent = 0.3f;

    public override BuffEffectType effectType => BuffEffectType.DamageUp;

    public override float GetModifier(BuffInstance buff) => _percent * buff.stacks;
}

/// <summary>
/// 减伤策略：降低受到的伤害（乘区）
/// 资产配置：每层减伤系数，如 0.2 = 受到的伤害 -20%（DamageCalculator 统一 clamp 上限 90%）
/// </summary>
[CreateAssetMenu(fileName = "BuffDamageDown", menuName = "Create/Buff/减伤")]
public class BuffDamageDownEffect : BuffEffect
{
    [SerializeField, Header("每层减伤系数（0.2 = 受到的伤害 -20%）")]
    private float _percent = 0.2f;

    public override BuffEffectType effectType => BuffEffectType.DamageDown;

    public override float GetModifier(BuffInstance buff) => _percent * buff.stacks;
}

/// <summary>
/// 持续伤害策略（DoT，灼烧/中毒）：
/// 按 tickInterval 周期扣血，isDoT=true → 不触发受击硬直、无视无敌窗口
/// 资产配置：每次 tick 扣血量 + tick 间隔
/// </summary>
[CreateAssetMenu(fileName = "BuffDot", menuName = "Create/Buff/持续伤害DoT")]
public class BuffDotEffect : BuffEffect
{
    [SerializeField, Header("每次 tick 扣血量（实际 = 此值 × 层数）")]
    private float _tickDamage = 3f;

    [SerializeField, Header("tick 间隔（秒）")]
    private float _tickInterval = 0.5f;

    public override BuffEffectType effectType => BuffEffectType.DoT;

    public override float tickInterval => _tickInterval;

    public override void OnTick(BuffInstance buff, HealthModel health)
    {
        health.TakeDamage(_tickDamage * buff.stacks, buff.source, false, true);
    }
}

/// <summary>
/// 持续回血策略（HoT，治疗/再生）：按 tickInterval 周期回血
/// 资产配置：每次 tick 回血量 + tick 间隔
/// </summary>
[CreateAssetMenu(fileName = "BuffHot", menuName = "Create/Buff/持续回血HoT")]
public class BuffHoTEffect : BuffEffect
{
    [SerializeField, Header("每次 tick 回血量（实际 = 此值 × 层数）")]
    private float _tickHeal = 5f;

    [SerializeField, Header("tick 间隔（秒）")]
    private float _tickInterval = 1f;

    public override BuffEffectType effectType => BuffEffectType.HoT;

    public override float tickInterval => _tickInterval;

    public override void OnTick(BuffInstance buff, HealthModel health)
    {
        health.Heal(_tickHeal * buff.stacks);
    }
}

/// <summary>
/// 加速策略：提升移动速度（乘区）
/// 资产配置：每层移速加成系数，如 0.2 = 移速 +20%
/// （移动状态机接入见二期，接口已提供 GetSpeedModifier）
/// </summary>
[CreateAssetMenu(fileName = "BuffSpeedUp", menuName = "Create/Buff/加速")]
public class BuffSpeedUpEffect : BuffEffect
{
    [SerializeField, Header("每层移速加成系数（0.2 = +20%）")]
    private float _percent = 0.2f;

    public override BuffEffectType effectType => BuffEffectType.SpeedUp;

    public override float GetModifier(BuffInstance buff) => _percent * buff.stacks;
}

/// <summary>
/// 减速策略：降低移动速度（乘区）
/// 资产配置：每层移速降低系数，如 0.2 = -20%
/// GetModifier 返回负值，便于 GetSpeedModifier 直接把增/减速求和
/// </summary>
[CreateAssetMenu(fileName = "BuffSpeedDown", menuName = "Create/Buff/减速")]
public class BuffSpeedDownEffect : BuffEffect
{
    [SerializeField, Header("每层移速降低系数（0.2 = -20%）")]
    private float _percent = 0.2f;

    public override BuffEffectType effectType => BuffEffectType.SpeedDown;

    public override float GetModifier(BuffInstance buff) => -_percent * buff.stacks;
}

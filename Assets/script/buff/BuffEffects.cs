using UnityEngine;

// ============================================================================
// Buff 效果策略具体实现（每类一个策略资产，Inspector 右键 Create/Buff/... 创建）
// 注意：策略资产全局共享，禁止存运行时状态；状态一律放 BuffInstance。
// ============================================================================

/// <summary>
/// 属性加成策略（系数型）：给携带者的指定属性增/删一条 Modifier。
/// 资产配置：作用属性(AttrType) + 修改类型(加/乘) + 每层数值。
///
/// 叠层正确性：每次重算都先按来源删掉旧 Modifier 再加新的，
/// 因此 3 层 = perStack × 3（而不是重复 Add 造成的 1+2+3 三角叠加）。
/// 清理放在 OnExpire（OnRemove 默认转 OnExpire），主动移除与自然到期都会清账。
/// </summary>
[CreateAssetMenu(fileName = "BuffAttribute", menuName = "Create/Buff/属性加成")]
public class BuffAttributeEffect : BuffEffect
{
    [SerializeField, Header("作用的属性（系数型）")]
    private AttrType _attrType = AttrType.DamageUp;

    [SerializeField, Header("修改类型（加法/乘法）")]
    private ModType _modType = ModType.Add;

    [SerializeField, Header("每层数值（最终 = 此值 × 层数）")]
    private float _valuePerStack = 0.3f;

    public override void OnApply(BuffInstance buff) => ApplyModifier(buff);

    public override void OnStackChanged(BuffInstance buff) => ApplyModifier(buff);

    public override void OnExpire(BuffInstance buff) => RemoveModifier(buff);

    private void ApplyModifier(BuffInstance buff)
    {
        var attr = GetAttr(buff);
        if (attr == null) return;

        var container = attr.Get(_attrType);
        container.RemoveAllFromSource(buff);   // 先删旧（叠层重算/重复挂），避免累加出错
        container.AddModifier(new AttributeModifier(_modType, _valuePerStack * buff.stacks, buff));
    }

    private void RemoveModifier(BuffInstance buff)
    {
        GetAttr(buff)?.Get(_attrType).RemoveAllFromSource(buff);
    }

    private static AttributeComponent GetAttr(BuffInstance buff)
        => buff.owner != null ? buff.owner.GetComponent<AttributeComponent>() : null;
}

/// <summary>
/// 持续伤害策略（DoT，灼烧/中毒）：按 tickInterval 周期扣血，isDoT=true → 不触发受击硬直、无视无敌。
/// 资产配置：每次 tick 扣血量 + tick 间隔（实际扣血 = 此值 × 层数）。
/// 服务端结算（BuffComponent 只在服务端 tick）。
/// </summary>
[CreateAssetMenu(fileName = "BuffDot", menuName = "Create/Buff/持续伤害DoT")]
public class BuffDotEffect : BuffEffect
{
    [SerializeField, Header("每次 tick 扣血量（实际 = 此值 × 层数）")]
    private float _tickDamage = 3f;

    [SerializeField, Header("tick 间隔（秒）")]
    private float _tickInterval = 0.5f;

    public override float tickInterval => _tickInterval;

    public override void OnTick(BuffInstance buff, HealthModel health)
    {
        if (health == null) return;
        health.TakeDamage(_tickDamage * buff.stacks, buff.source, false, true);
    }
}

/// <summary>
/// 持续回血策略（HoT，治疗/再生）：按 tickInterval 周期回血。
/// 资产配置：每次 tick 回血量 + tick 间隔（实际回血 = 此值 × 层数）。
/// </summary>
[CreateAssetMenu(fileName = "BuffHot", menuName = "Create/Buff/持续回血HoT")]
public class BuffHoTEffect : BuffEffect
{
    [SerializeField, Header("每次 tick 回血量（实际 = 此值 × 层数）")]
    private float _tickHeal = 5f;

    [SerializeField, Header("tick 间隔（秒）")]
    private float _tickInterval = 1f;

    public override float tickInterval => _tickInterval;

    public override void OnTick(BuffInstance buff, HealthModel health)
    {
        if (health == null) return;
        health.Heal(_tickHeal * buff.stacks);
    }
}

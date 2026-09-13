/// <summary>
/// 系数型属性枚举（MutableAttribute 管理的属性种类）
/// 全部是"系数"（0 = 无加成），不是攻击力白值：
/// 最终值 = 基础值 × (1 + 该系数)，例如 DamageUp 0.3 = 造成伤害 +30%。
/// 新增属性：在此加枚举值即可，AttributeComponent 会自动多一个容器。
/// </summary>
public enum AttrType
{
    /// <summary>增伤系数（0.3 = 造成伤害 +30%）</summary>
    DamageUp,

    /// <summary>减伤系数（0.2 = 受到伤害 -20%，DamageCalculator 统一 clamp 上限 90%）</summary>
    DamageDown,

    /// <summary>移速系数（0.2 = 移速 +20%；移动状态机接入见二期）</summary>
    MoveSpeed,
}

/// <summary>
/// 属性枚举（MutableAttribute 管理的属性种类），分两类：
///
///   1) 系数型（0 = 无加成）：最终值 = 基础值 × (1 + 系数)，例如 DamageUp 0.3 = 造成伤害 +30%
///   2) 白值型（0 = 没有）：最终值就是属性白值本身，装备/Buff 直接在其上加减乘，
///      例如 Attack 100 = 攻击力 100（伤害 = 攻击力 × 招式倍率）
///
/// ⚠ 新增属性一律**追加在末尾**：本枚举被 BuffAttributeEffect 等资产序列化（存的是 int），
///   插到中间或改动顺序会让已有资产的属性错位。
/// AttributeComponent 按枚举自动建容器，加值后无需改它。
/// </summary>
public enum AttrType
{
    // ==================== 系数型（0 = 无加成） ====================

    /// <summary>增伤系数（0.3 = 造成伤害 +30%）</summary>
    DamageUp,

    /// <summary>减伤系数（0.2 = 受到伤害 -20%，DamageCalculator 统一 clamp 上限 90%）</summary>
    DamageDown,

    /// <summary>移速系数（0.2 = 移速 +20%；移动状态机接入见二期）</summary>
    MoveSpeed,

    // ==================== 白值型（0 = 没有） ====================

    /// <summary>攻击力（白值）：伤害 = 攻击力 × 招式倍率；装备加攻就加这个</summary>
    Attack,

    /// <summary>防御力（白值）：减伤用（伤害公式接入见第二步）</summary>
    Defense,
}

/// <summary>AttrType 的分类帮助：区分"系数型"与"白值型"。</summary>
public static class AttrTypeUtil
{
    /// <summary>
    /// 是否为"系数型"属性（0 = 无加成）。系数型由 MutableAttribute 以中性值 1 计算
    /// （Final = (1 + ΣAdd) × ΠMul - 1），Add 与 Multiply 均可用。
    /// 白值型（Attack/Defense）Base 是真实数值，按 (Base + ΣAdd) × ΠMul 计算。
    /// </summary>
    public static bool IsRatio(AttrType type) => type switch
    {
        AttrType.DamageUp => true,
        AttrType.DamageDown => true,
        AttrType.MoveSpeed => true,
        _ => false,
    };
}

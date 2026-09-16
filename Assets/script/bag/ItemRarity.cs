using UnityEngine;

/// <summary>
/// 物品稀有度。驱动表现（格子底色、详情名字颜色），以后也能驱动掉落权重 / 分解产出。
///
/// ⚠ 会被 ItemData 序列化成 int —— 新增稀有度一律**追加在末尾**。
///   Common 刻意放在 0：老配置加字段后自动就是"普通"，不需要任何迁移。
/// </summary>
public enum ItemRarity
{
    /// <summary>普通</summary>
    Common = 0,

    /// <summary>稀有</summary>
    Rare = 1,

    /// <summary>史诗</summary>
    Epic = 2,

    /// <summary>传说</summary>
    Legendary = 3,
}

/// <summary>
/// 稀有度 → 颜色。写法对齐工程里的 AttrTypeUtil / BagTypeUtil（静态查表）。
///
/// 提供两个颜色而不是一个，因为底色和文字对亮度的要求相反：
///   - ToTextColor：亮色，给文字 / 描边用（在深色窗口上要醒目）
///   - ToCellColor：压暗的版本，给格子底框用（满格铺开，太亮会很吵）
/// </summary>
public static class ItemRarityUtil
{
    /// <summary>格子底框的基础色（普通品质的底色，也是压暗其它稀有度的基准）</summary>
    public static readonly Color CellBaseColor = new Color(0.21f, 0.23f, 0.28f, 1f);

    /// <summary>压暗程度：0 = 完全用基础深灰，1 = 完全用稀有度亮色</summary>
    private const float CellTintAmount = 0.4f;

    /// <summary>稀有度亮色（文字用）</summary>
    public static Color ToTextColor(this ItemRarity rarity) => rarity switch
    {
        ItemRarity.Rare => new Color(0.29f, 0.62f, 1.00f),   // 蓝
        ItemRarity.Epic => new Color(0.66f, 0.36f, 1.00f),   // 紫
        ItemRarity.Legendary => new Color(1.00f, 0.69f, 0.13f), // 橙金
        _ => new Color(0.72f, 0.75f, 0.80f),                 // 普通：灰白
    };

    /// <summary>格子底框色（稀有度亮色往基础深灰里掺一点，保留色相但压暗）</summary>
    public static Color ToCellColor(this ItemRarity rarity)
    {
        // 普通就用基础色，跟没有稀有度之前的样子完全一致
        if (rarity == ItemRarity.Common) return CellBaseColor;

        return Color.Lerp(CellBaseColor, rarity.ToTextColor(), CellTintAmount);
    }
}

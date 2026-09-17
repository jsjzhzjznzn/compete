using UnityEngine;

/// <summary>
/// 背包排序用的比较器。写法对齐工程里的 Util 静态类。
///
/// 【排序的单位是"堆"（BagSlot），不是"物品"】
///   同一种药水占两格（99 + 51）就是两个独立的堆，排序后它们相邻，但互相之间不会合并。
///   想让它们先聚拢，调用方应该先 BagData.MergeAll() 再排 —— ItemActionTidy 就是按这个顺序做的。
/// </summary>
public static class BagSlotComparers
{
    /// <summary>
    /// 按品质整理：品质降序 → itemId 升序 → 数量降序。
    ///
    /// 【为什么必须有第二级 itemId】
    ///   只按品质排的话，同品质的不同物品每次排完顺序都可能不一样（List.Sort 不是稳定排序），
    ///   玩家连点两次"整理"看到两种排列，会以为坏了。加上 itemId 做次级键，
    ///   同品质内同类物品必然聚拢、且顺序完全确定。
    /// </summary>
    public static int ByRarity(BagSlot a, BagSlot b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        var configA = ItemDatabase.Resolve(a.itemId);
        var configB = ItemDatabase.Resolve(b.itemId);

        // ① 配置查不到的堆统一沉到最后 —— 不能让一条缺失的配置把整批顺序搅乱
        if (configA == null || configB == null)
        {
            if (configA == configB) return a.itemId.CompareTo(b.itemId);
            return configA == null ? 1 : -1;
        }

        // ② 品质降序（传说 → 普通）
        int result = configB.rarity.CompareTo(configA.rarity);
        if (result != 0) return result;

        // ③ itemId 升序：保证稳定性 + 同品质内同类聚拢
        result = a.itemId.CompareTo(b.itemId);
        if (result != 0) return result;

        // ④ 同种物品：大堆在前
        return b.count.CompareTo(a.count);
    }
}

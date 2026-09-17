using UnityEngine;

/// <summary>
/// 【整理】：**合并散堆 + 按品质排序**，一次点完。
///
/// 【为什么两件事合成一个按钮】
///   玩家点"整理"想要的是"东西聚拢、按稀有度排好"，而不是"先点合并、再点排序"。
///   拆成两个按钮不仅多一步操作，还会多占一个按钮池槽位（现在只有 3 个）。
///
/// 【为什么顺序是"先合并再排序"】
///   反过来的话，`40 / 30 / 20` 三段药水会被排到三个不同位置（因为它们是三个独立的堆），
///   看起来就是"整理了但没聚拢"。先 MergeAll 把它们并成一堆，再排序才有意义。
///
/// 算法本身都在 BagData（MergeAll / Sort），这里只负责"按什么顺序调"和"这是个能点的操作"。
/// </summary>
[CreateAssetMenu(fileName = "ItemAction_Tidy", menuName = "Create/Bag/ItemAction/Tidy")]
public class ItemActionTidy : ItemAction
{
    public override bool CanRun(BagData bag, int slotIndex) => bag != null;

    /// <summary>会重排槽位顺序 → 让 BagPanel 清掉选中态（选中按槽位号记，排完就指偏了）</summary>
    public override bool invalidatesSelection => true;

    public override bool Run(BagData bag, int slotIndex)
    {
        if (bag == null) return false;

        bool merged = bag.MergeAll();                        // ① 先把散堆合并到最少格数
        bool sorted = bag.Sort(BagSlotComparers.ByRarity);   // ② 再按品质排（传说→普通，空槽沉底）

        return merged || sorted;
    }
}

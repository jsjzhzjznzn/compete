using UnityEngine;

/// <summary>
/// 【丢弃】：把这一格的东西整个删掉。
///
/// 以后要变成"丢到地上生成拾取物"怎么办？**新建一个 ItemActionDrop 挂上去就行**，
/// 面板、背包、其他策略一行都不用改 —— 这就是策略模式在这里的价值。
/// </summary>
[CreateAssetMenu(fileName = "ItemAction_Discard", menuName = "Create/Bag/ItemAction/Discard")]
public class ItemActionDiscard : ItemAction
{
    public override bool CanRun(BagData bag, int slotIndex)
    {
        var slot = bag != null ? bag.GetSlot(slotIndex) : null;
        return slot != null && !slot.IsEmpty;
    }

    public override bool Run(BagData bag, int slotIndex)
    {
        var slot = bag != null ? bag.GetSlot(slotIndex) : null;
        if (slot == null || slot.IsEmpty) return false;

        // 丢整叠。数据修改走 BagData —— 它会发变更通知，UI 据此刷新、以后存档/联网也挂在那
        return bag.RemoveAt(slotIndex, slot.count);
    }
}

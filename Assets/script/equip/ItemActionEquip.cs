using UnityEngine;

/// <summary>
/// 【装备】：把这一格的装备穿到对应槽位，被替换下来的旧装备放回背包。
///
/// 【为什么所有校验都做在动数据之前】
///   穿脱要动两个容器（背包 + 装备栏），中途失败就是丢件 ——
///   东西从背包清掉了、又没进装备栏，玩家白丢一件。
///   所以先把"能不能做"算清楚，再动数据。和 BagData.TryAdd 是同一种思路。
///
/// 【作用对象】走 ItemActionTarget.LocalOwner，与 ItemActionUse 一致。
///   还没接入时 CanRun 返回 false → 按钮置灰，不会出错。
///
/// 【怎么挂上】ItemActionDefaults.asset 里给 Weapon 类型挂上这份资产，
///   所有武器就都有【装备】按钮了 —— 走 ItemData.GetActions() 那套默认表机制，
///   不用改面板、不用改背包、不用加 if。
/// </summary>
[CreateAssetMenu(fileName = "ItemAction_Equip", menuName = "Create/Bag/ItemAction/Equip")]
public class ItemActionEquip : ItemAction
{
    public override bool CanRun(BagData bag, int slotIndex)
    {
        var equip = GetEquipment();
        var config = ResolveConfig(bag, slotIndex);
        if (equip == null || config == null) return false;

        return equip.CanEquip(config.itemIdHash, out _);
    }

    public override bool Run(BagData bag, int slotIndex)
    {
        if (bag == null) return false;

        var equip = GetEquipment();
        var config = ResolveConfig(bag, slotIndex);
        if (equip == null || config == null) return false;
        if (!equip.CanEquip(config.itemIdHash, out var slotType)) return false;

        // 校验全过了，下面三步都不会失败
        // ① 先穿：槽位换人 + 旧装备的加成撤掉、新装备的加上（顺序上先穿，
        //    避免出现"背包里没了、装备栏里也没有"的中间态）
        equip.Equip(slotType, config.itemIdHash, out int replaced);

        // ② 再从背包取走（空出一格）
        bag.RemoveAt(slotIndex, 1);

        // ③ 被替换的旧装备放回背包。
        //    刚空出一格，所以一定放得下，不必 TryAdd。
        //    （以后如果"待装备来源"和"回收目标"不是同一个包，这里要换成 TryAdd + 失败回滚）
        if (replaced != 0) bag.Add(replaced, 1);

        return true;
    }

    private static EquipmentComponent GetEquipment()
    {
        var owner = ItemActionTarget.LocalOwner;
        return owner != null ? owner.GetComponent<EquipmentComponent>() : null;
    }
}

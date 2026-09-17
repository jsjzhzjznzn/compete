using System;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 装备栏组件：挂在角色根上，与 AttributeComponent / BuffComponent 并列。
///
/// 【职责】
///   1) 存"每个槽穿的是什么"（EquipSlot[]，定长，下标 = (int)EquipSlotType）
///   2) 穿 / 脱时把 ItemData.stats 变成 AttributeModifier 挂到角色的属性账本上
///   3) 发 OnSlotChanged，给装备栏 UI 订阅（和 BagData.OnSlotChanged 同款）
///
/// 【为什么不自己聚合属性】
///   AttributeComponent / MutableAttribute 已经实现了：
///     白值型 Final = (Base + ΣAdd) × ΠMul
///     系数型 Final = (1 + ΣAdd) × ΠMul - 1        （MutableAttribute.cs:88-104）
///   而且 Buff 的加成也挂在同一个账本上（BuffEffects.cs:41）—— 走同一条路，
///   装备和 Buff 谁先谁后、百分比作用在谁头上天然统一，不用再定义一套。
///
///   自己搞一份 EquipAttack += ... 只能表达加法：策划一说"攻击 +20 且造成伤害 +15%"
///   就得推倒重来；而且会和 Buff 加成变成两套互不认识的管线。
///
/// 【性能】不用担心"每次读属性都遍历装备"：MutableAttribute 是脏标记 + 延迟计算，
///   写的时候只打标记，读 FinalValue 时才重算一次并缓存。
///
/// 【联机】AttributeComponent 只允许服务端写 Modifier（AttributeComponent.cs:9），
///   所以穿 / 脱必须由服务端执行。单机（对象未 Spawn）不受影响；
///   联机时把 Equip / Unequip 包一层 ServerRpc（传 itemId 让服务端自己查配置还原）。
/// </summary>
public class EquipmentComponent : NetworkBehaviour
{
    /// <summary>有效槽位数。None 排在枚举最后，它的值正好是槽位个数</summary>
    public const int SlotCount = (int)EquipSlotType.None;

    private readonly EquipSlot[] _slots = new EquipSlot[SlotCount];

    /// <summary>槽位变化（穿上 / 卸下 / 替换），参数 = 哪个槽。UI 订阅它刷新</summary>
    public event Action<EquipSlotType> OnSlotChanged;

    /// <summary>
    /// 全部属性类型（清账用）。
    /// 缓存成静态数组：ClearStats 每次穿脱都要用，不能每次都 Enum.GetValues（会分配）。
    /// </summary>
    private static readonly AttrType[] AllAttrs = (AttrType[])Enum.GetValues(typeof(AttrType));

    private void Awake()
    {
        // 预建所有槽位实例，之后只改 itemId、永不重建 —— 保证 source 引用始终有效
        for (int i = 0; i < SlotCount; i++)
            _slots[i] = new EquipSlot((EquipSlotType)i);
    }

    /// <summary>取槽位（越界返回 null，调用方不必自己判范围）</summary>
    public EquipSlot GetSlot(EquipSlotType type)
    {
        int index = (int)type;
        return (index >= 0 && index < SlotCount) ? _slots[index] : null;
    }

    /// <summary>这个槽穿的是什么（0 = 空）</summary>
    public int GetItemId(EquipSlotType type) => GetSlot(type)?.itemId ?? 0;

    /// <summary>
    /// 能不能穿这一件（**只读判断，一个字段都不动**）。
    /// 调用方（尤其是 ItemActionEquip.CanRun）用它决定按钮灰不灰。
    /// </summary>
    public bool CanEquip(int itemId, out EquipSlotType type)
    {
        type = EquipSlotType.None;

        var config = ItemDatabase.Resolve(itemId);
        if (config == null || !config.equippable) return false;
        if (GetSlot(config.equipSlot) == null) return false;

        type = config.equipSlot;
        return true;
    }

    /// <summary>
    /// 穿上一件装备。调用方应该先走 CanEquip —— 这里只在"没校验就来调"时报错返回。
    ///
    /// 不用 null 兼表两种含义（"失败"和"槽位原本为空"），所以返回值拆成 bool + out。
    /// </summary>
    /// <param name="replacedItemId">被替换下来的旧装备 id（0 = 槽位原本就是空的）</param>
    /// <returns>是否成功</returns>
    public bool Equip(EquipSlotType type, int itemId, out int replacedItemId)
    {
        replacedItemId = 0;

        var slot = GetSlot(type);
        if (slot == null)
        {
            Debug.LogError($"[EquipmentComponent] 槽位 {type} 不存在，调用方应先走 CanEquip");
            return false;
        }

        var config = ItemDatabase.Resolve(itemId);
        if (config == null)
        {
            Debug.LogError($"[EquipmentComponent] 找不到 itemId={itemId} 的配置，调用方应先走 CanEquip");
            return false;
        }

        replacedItemId = slot.itemId;

        ClearStats(slot);            // ① 先撤旧装备的加成（同一个 source，天然覆盖）
        slot.itemId = itemId;        // ② 换上新装备
        ApplyStats(slot, config);    // ③ 加上新装备的加成

        OnSlotChanged?.Invoke(type);
        return true;
    }

    /// <summary>卸下。返回是否真的卸了东西（槽位本来就空 → false）。</summary>
    /// <param name="removedItemId">卸下来的物品 id</param>
    public bool Unequip(EquipSlotType type, out int removedItemId)
    {
        removedItemId = 0;

        var slot = GetSlot(type);
        if (slot == null || slot.IsEmpty) return false;

        removedItemId = slot.itemId;

        ClearStats(slot);
        slot.Clear();

        OnSlotChanged?.Invoke(type);
        return true;
    }

    // ==================== 属性同步 ====================

    /// <summary>
    /// 把 config.stats 里每条加成挂到属性账本上，**source = 槽位对象**。
    /// 不能用 ItemData 当 source：两件同 id 的装备会互相删掉对方的加成（按引用相等匹配）。
    /// </summary>
    private void ApplyStats(EquipSlot slot, ItemData config)
    {
        var attrs = GetComponent<AttributeComponent>();
        if (attrs == null) return;

        var stats = config.stats;
        for (int i = 0; i < stats.Count; i++)
        {
            var entry = stats[i];
            attrs.Get(entry.attr).AddModifier(
                new AttributeModifier(entry.modType, entry.value, slot));
        }
    }

    /// <summary>
    /// 撤掉这个槽带来的全部加成。
    ///
    /// 【为什么遍历所有属性，而不是记住"上次加了哪几条"】
    ///   属性总共就几个，暴力扫的开销可忽略；而记 list 在配置改过之后会漏
    ///   （旧状态下加过 DamageUp，新配置里这条没了，按记录的 list 就清不掉）。
    ///   按 source 全清永远是对的。
    /// </summary>
    private void ClearStats(EquipSlot slot)
    {
        var attrs = GetComponent<AttributeComponent>();
        if (attrs == null) return;

        for (int i = 0; i < AllAttrs.Length; i++)
            attrs.Get(AllAttrs[i]).RemoveAllFromSource(slot);
    }
}

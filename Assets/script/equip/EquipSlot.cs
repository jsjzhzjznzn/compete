/// <summary>
/// 一个装备槽。每个槽一个实例、随角色常驻，同时承担两个角色：
///   1) 数据：这个槽当前穿的是哪件（itemId，0 = 空）
///   2) 身份：AttributeModifier.source —— 属性账本按它精准增删
///
/// 【为什么必须是 class，不能只用 EquipSlotType 那些枚举值】
///   AttributeModifier.source 的比较是 ReferenceEquals（MutableAttribute.cs:70）。
///   EquipSlotType 是值类型，赋给 object 字段会**装箱**，每次调用都产生一个新的装箱对象，
///   RemoveAllFromSource 永远匹配不上 —— 表现就是"装上攻击力涨了，脱下不减"。
///   用 EquipSlot 实例当 source 就没有这个问题：同一个槽永远是同一个对象。
///
/// 【为什么存 int 不存 ItemData（SO 引用）】
///   和 BagSlot 保持一致：槽位里只存 id，显示 / 使用 / 存档 / 联网都靠 ItemDatabase 还原。
///   存 SO 引用会导致存档两套格式（背包转 id、装备栏直接序列化引用）、联网时发不出去。
///
/// 【顺带解决"两件同 id 装备"】
///   source 是槽位而不是配置，所以两枚同 id 的戒指戴在两个手指上，
///   卸一枚只会删掉那一枚的加成，不会把另一枚的一起删掉。
/// </summary>
public class EquipSlot
{
    /// <summary>哪个部位</summary>
    public readonly EquipSlotType type;

    /// <summary>当前穿的物品 id（存 ItemData.itemIdHash，0 = 空槽）</summary>
    public int itemId;

    public EquipSlot(EquipSlotType type) => this.type = type;

    /// <summary>是否为空槽</summary>
    public bool IsEmpty => itemId == 0;

    /// <summary>清空（不触发任何通知，通知由 EquipmentComponent 统一发）</summary>
    public void Clear() => itemId = 0;

    public override string ToString() => IsEmpty ? $"{type}: Empty" : $"{type}: {itemId}";
}

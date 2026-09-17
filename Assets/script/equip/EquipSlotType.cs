/// <summary>
/// 装备槽位类型。
///
/// ⚠ 会被 ItemData 序列化成 int —— 新增一律**追加在末尾**，插到中间会让已有配置错位。
///
/// None 排在最后还有个用处：它的值正好等于有效槽位数（见 EquipmentComponent.SlotCount），
/// 不用再单独维护一个常量、也不会出现"加了槽位忘了改数量"。
/// </summary>
public enum EquipSlotType
{
    /// <summary>武器</summary>
    Weapon = 0,

    /// <summary>头盔</summary>
    Helmet = 1,

    /// <summary>胸甲</summary>
    Armor = 2,

    /// <summary>鞋子</summary>
    Shoes = 3,

    /// <summary>不可穿戴（ItemData 上这个字段的默认值）</summary>
    None = 4,
}

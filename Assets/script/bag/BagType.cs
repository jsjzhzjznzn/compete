/// <summary>
/// 背包类型：这是"哪个容器"。
///
/// 和 ItemType 分开是有意的：
///   ItemType 描述【配置里这件东西是什么】（ItemData 上的字段）
///   BagType  描述【哪个容器】（BagData 上的字段）
///
/// 每个背包的容量 / 收哪种物品 / 显示名，统一登记在 BagManager 的 BagDef 表里 ——
/// 加一个新背包 = 这里加一个枚举值 + BagManager 表里加一行。
///
/// ⚠ 本枚举会被 BagPanel 资产序列化成 int —— 新增类型一律**追加在末尾**。
/// </summary>
public enum BagType
{
    /// <summary>道具背包</summary>
    Item = 0,

    /// <summary>武器背包</summary>
    Weapon = 1,
}

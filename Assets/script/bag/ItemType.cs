/// <summary>
/// 道具类型：这件东西是"什么"，决定它归哪个背包收。
///
/// 和 BagType 分开是有意的：
///   ItemType 描述【配置里这件东西是什么】（ItemData 上的字段）
///   BagType  描述【哪个容器】（BagData 上的字段）
/// 以后出现"材料背包也装普通道具""武器背包也能放饰品"这类情况时，两者不会打架。
///
/// ⚠ 本枚举会被 ItemData 资产序列化成 int —— 新增类型一律**追加在末尾**，
///   插到中间或改动顺序会让已配好的道具类型错位。
/// </summary>
public enum ItemType
{
    /// <summary>普通道具（可堆叠，默认归道具背包）</summary>
    Item = 0,

    /// <summary>武器（不可堆叠，默认归武器背包，可带属性加成）</summary>
    Weapon = 1,
}

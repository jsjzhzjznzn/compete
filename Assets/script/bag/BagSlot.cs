/// <summary>
/// 背包里的一个槽位（运行时数据）。
///
/// 用 class 而不是 struct：BagData 的槽位列表实例只创建一次、之后只改元素，
/// 各处（UI、以后的存档）拿到的都是同一个对象的引用，改了就都看得见。
///
/// 空槽的表示方式就是 itemId == 0（或 count &lt;= 0），没有 null 槽位。
/// </summary>
public class BagSlot
{
    /// <summary>放的什么东西 —— 存 ItemData.itemIdHash，0 表示空</summary>
    public int itemId;

    /// <summary>放了多少个 —— &lt;= 0 表示空</summary>
    public int count;

    /// <summary>是否为空槽</summary>
    public bool IsEmpty => itemId == 0 || count <= 0;

    /// <summary>清空（不触发通知，通知由 BagData 统一发）</summary>
    public void Clear()
    {
        itemId = 0;
        count = 0;
    }

    public override string ToString() => IsEmpty ? "Empty" : $"{itemId} x{count}";
}

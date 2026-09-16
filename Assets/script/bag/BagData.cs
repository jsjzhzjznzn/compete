using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一个背包的运行时数据：定长槽位 + 增删改查 + 变更通知。
///
/// 【为什么空槽也占位】
///   _slots 的长度恒等于 capacity，空槽用 itemId == 0 表示，列表永远按槽位号 0..capacity-1 展示。
///   这样"槽位下标"就是 UI 里的格子上标，不需要任何索引换算；
///   而且容量不变 → UI 的滚动位置天然稳定，不需要"保持滚动位置"的补丁。
///
/// 【实例只创建一次】
///   _slots 这个 List 实例只创建一次、之后只改元素、永不重新赋值 —— 外部（UI/存档）
///   拿到的引用始终有效。
///
/// 【不落盘】
///   纯内存，退出即清。要存档时在这里加 Serialize/Deserialize（SO 不能直接序列化，得转 DTO）。
/// </summary>
public class BagData
{
    /// <summary>哪个背包</summary>
    public BagType bagType { get; }

    /// <summary>槽位总数</summary>
    public int capacity { get; }

    /// <summary>
    /// 这个背包收哪种物品（由 BagManager 的登记表在创建时注入）。
    /// 放在自己身上而不是去问 BagManager —— 否则 BagData 和 BagManager 会互相依赖。
    /// </summary>
    public ItemType acceptItemType { get; }

    /// <summary>槽位列表（定长，只读视图；外部只读，改数据走 Add/RemoveAt/Swap）</summary>
    public IReadOnlyList<BagSlot> Slots => _slots;

    /// <summary>槽位变化通知。参数是槽位号；-1 表示整体变化（Clear / 批量操作）</summary>
    public event Action<int> OnSlotChanged;

    private readonly List<BagSlot> _slots;

    public BagData(BagType bagType, int capacity, ItemType acceptItemType)
    {
        this.bagType = bagType;
        this.capacity = Math.Max(1, capacity);
        this.acceptItemType = acceptItemType;

        _slots = new List<BagSlot>(this.capacity);
        for (int i = 0; i < this.capacity; i++)
        {
            _slots.Add(new BagSlot());
        }
    }

    /// <summary>取槽位（越界返回 null，调用方不必自己判范围）</summary>
    public BagSlot GetSlot(int index)
    {
        return (index >= 0 && index < _slots.Count) ? _slots[index] : null;
    }

    /// <summary>这个背包收不收这件东西（看 ItemData.itemType；查不到配置的不收）</summary>
    public bool Accepts(int itemId)
    {
        var config = ItemDatabase.Resolve(itemId);
        return config != null && config.itemType == acceptItemType;
    }

    /// <summary>
    /// 放入物品：先叠到已有同类未满的槽，再填空槽。
    /// 返回**没放进去的余量**（0 = 全放进去了；&gt;0 = 背包满了）。
    /// </summary>
    public int Add(int itemId, int count = 1)
    {
        if (itemId == 0 || count <= 0) return 0;

        var config = ItemDatabase.Resolve(itemId);
        if (config == null)
        {
            Debug.LogWarning($"[BagData] 找不到 itemId={itemId} 的配置，未放入 {count} 个");
            return count;
        }
        if (config.itemType != acceptItemType)
        {
            Debug.LogWarning($"[BagData] {bagType} 包不收 {config.itemType} 类型的 \"{config.itemName}\"");
            return count;
        }

        int maxStack = config.maxStack;
        int remain = count;

        // 1) 先叠到已有的同类槽（maxStack == 1 的武器跳过这步）
        if (maxStack > 1)
        {
            for (int i = 0; i < _slots.Count && remain > 0; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty || slot.itemId != itemId || slot.count >= maxStack) continue;

                int put = Math.Min(remain, maxStack - slot.count);
                slot.count += put;
                remain -= put;
                Notify(i);
            }
        }

        // 2) 再填空槽
        for (int i = 0; i < _slots.Count && remain > 0; i++)
        {
            var slot = _slots[i];
            if (!slot.IsEmpty) continue;

            int put = Math.Min(remain, maxStack);
            slot.itemId = itemId;
            slot.count = put;
            remain -= put;
            Notify(i);
        }

        return remain;
    }

    /// <summary>
    /// 从指定槽移除若干个（不够就只移除现有的）。
    /// 返回是否真的产生了变化。
    /// </summary>
    public bool RemoveAt(int index, int count = 1)
    {
        if (count <= 0) return false;

        var slot = GetSlot(index);
        if (slot == null || slot.IsEmpty) return false;

        slot.count -= Math.Min(count, slot.count);
        if (slot.count <= 0) slot.Clear();
        Notify(index);
        return true;
    }

    /// <summary>
    /// 交换两个槽（拖拽换位用）。
    /// 若两边是**同一种且可堆叠**的东西，按"尽量合并到一个槽"处理 —— 与 Add 的行为保持一致，
    /// 否则两个都没满的同种物品换一下位置反而变成不合并，看着像 bug。
    /// 返回是否发生了变化。
    /// </summary>
    public bool Swap(int a, int b)
    {
        if (a == b) return false;

        var slotA = GetSlot(a);
        var slotB = GetSlot(b);
        if (slotA == null || slotB == null) return false;
        if (slotA.IsEmpty && slotB.IsEmpty) return false;

        // 同种且可叠 → 合并
        if (!slotA.IsEmpty && !slotB.IsEmpty && slotA.itemId == slotB.itemId)
        {
            var config = ItemDatabase.Resolve(slotA.itemId);
            int maxStack = config != null ? config.maxStack : 1;
            if (maxStack > 1)
            {
                int total = slotA.count + slotB.count;
                slotA.count = Math.Min(total, maxStack);

                int remain = total - slotA.count;
                if (remain > 0)
                {
                    slotB.itemId = slotA.itemId;
                    slotB.count = remain;
                }
                else
                {
                    slotB.Clear();
                }

                Notify(a);
                Notify(b);
                return true;
            }
        }

        // 其余情况直接对调
        int tempId = slotA.itemId;
        int tempCount = slotA.count;
        slotA.itemId = slotB.itemId;
        slotA.count = slotB.count;
        slotB.itemId = tempId;
        slotB.count = tempCount;

        Notify(a);
        Notify(b);
        return true;
    }

    /// <summary>清空整个背包（发一次整体变化通知）</summary>
    public void Clear()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            _slots[i].Clear();
        }
        Notify(-1);
    }

    private void Notify(int slotIndex) => OnSlotChanged?.Invoke(slotIndex);
}

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

    /// <summary>
    /// 有东西没放进去 —— 背包满了，或者只放下了一部分。
    /// 参数：(itemId, 没能放进去的数量)。
    ///
    /// ⚠ 只有"空间不够"才发这个事件。
    ///   配置查不到 / 类型收不对 属于**配置或调用错误**，只打 LogError、**不发这个事件** ——
    ///   否则 UI 会把配置 bug 提示成"背包已满"，真问题就被伪装成正常状态了（这是最容易埋雷的地方）。
    /// </summary>
    public event Action<int, int> OnAddOverflow;

    private readonly List<BagSlot> _slots;

    /// <summary>排序用的快照缓冲区（只增不减，反复复用，避免每次排序产生垃圾）</summary>
    private readonly List<BagSlot> _sortBuffer = new List<BagSlot>();

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
    /// 返回**没放进去的余量**（0 = 全部放进去了）。
    ///
    /// 放不下时会额外发 <see cref="OnAddOverflow"/>，UI 订阅它做提示即可 ——
    /// 调用方不需要自己算空间（UI 看到的只是镜像，多背包 / 以后联机时自己算必然算错）。
    /// </summary>
    public int Add(int itemId, int count = 1)
    {
        if (itemId == 0 || count <= 0) return 0;      // 空物品 / 数量非法：不算错误也不算满

        var config = ItemDatabase.Resolve(itemId);
        if (config == null)
        {
            // 配置错误，不是"背包满" → 用 LogError 让它显眼，并且刻意不发 OnAddOverflow
            Debug.LogError($"[BagData] 找不到 itemId={itemId} 的配置，未放入 {count} 个（这是配置错误，不是背包满）");
            return count;
        }
        if (config.itemType != acceptItemType)
        {
            Debug.LogError($"[BagData] {bagType} 包不收 {config.itemType} 类型的 \"{config.itemName}\"，未放入（这是配置/调用错误，不是背包满）");
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

        // 3) 还有剩下的 → 通知"没放下"（部分放下也会走到这，所以事件名叫 Overflow 不叫 BagFull）
        if (remain > 0) OnAddOverflow?.Invoke(itemId, remain);

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

    /// <summary>
    /// 合并散堆：把同种且可叠的多个堆，合并到占用格数最少。
    ///
    /// 例：3 格分别放 40 / 30 / 20 个（上限 99）→ 合并后 90 个占 1 格，另 2 格空出来。
    ///
    /// 规则：
    ///   - 只处理"同 itemId 且 maxStack > 1"的堆（武器这类不可叠的整格跳过）
    ///   - 只往**下标更小的同类堆**里塞，**不会**把整堆搬进空槽
    ///     （所以它叫"合并"不叫"排序"：整理后物品不会自动挤到最前面。
    ///       要做到那一步是另一个操作 ItemActionSort，需要吗再说）
    ///   - 全部改完只发**一次**整体变化通知（-1），不逐格通知
    ///
    /// 放数据层而不是放在 ItemActionMerge 里：它是纯粹的容器操作，
    /// 跟"按钮叫什么、什么时候能点"无关 —— 策略只负责触发。
    /// </summary>
    /// <returns>是否真的发生了变化</returns>
    public bool MergeAll()
    {
        bool changed = false;

        // 从前往后扫：把每一堆往它前面那些"同类未满"的堆里塞
        for (int i = 0; i < _slots.Count; i++)
        {
            var from = _slots[i];
            if (from.IsEmpty) continue;

            var config = ItemDatabase.Resolve(from.itemId);
            int maxStack = config != null ? config.maxStack : 1;
            if (maxStack <= 1) continue;          // 不可叠的（武器）没有可合并的余地

            for (int j = 0; j < i && from.count > 0; j++)
            {
                var to = _slots[j];
                if (to.IsEmpty || to.itemId != from.itemId || to.count >= maxStack) continue;

                int move = Mathf.Min(from.count, maxStack - to.count);
                if (move <= 0) continue;

                to.count += move;
                from.count -= move;
                changed = true;
            }

            // 这一堆被搬空了，把槽位还原成空格
            if (from.count <= 0)
            {
                from.Clear();
                changed = true;
            }
        }

        if (changed) Notify(-1);
        return changed;
    }

    /// <summary>
    /// 按比较器重排整个背包：非空槽按序排在前，空槽全部沉底。返回是否发生了变化。
    ///
    /// 【为什么整批重排，而不是逐个 Swap】
    ///   逐个 Swap 每一步都会发一次 OnSlotChanged，UI 要重刷几十遍（还会疯狂重绑格子）。
    ///   这里把非空堆快照到缓冲区 → 排序 → 一次写回 → 只发**一次** Notify(-1)。
    ///
    /// 【排的是"堆"不是"物品"】
    ///   同一种物品占两格（99 + 51）是两个独立的堆，排序后相邻但不会自动合并。
    ///   想先聚拢再排，调用方先调 MergeAll()（ItemActionTidy 就是"合并 + 排序"）。
    ///
    /// ⚠ 排序会改变"第 N 格是什么"。按槽位号记的东西（比如 UI 的选中态）排完就指偏了，
    ///   调用方必须自己处理 —— ItemAction 上有 invalidatesSelection 标记干这件事。
    /// </summary>
    public bool Sort(Comparison<BagSlot> compare)
    {
        if (compare == null) return false;

        // ① 非空堆快照进缓冲区（BagSlot 对象复用；缓冲区长度调到正好等于这次的堆数）
        int count = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot.IsEmpty) continue;

            if (_sortBuffer.Count <= count) _sortBuffer.Add(new BagSlot());
            var buffer = _sortBuffer[count];
            buffer.itemId = slot.itemId;
            buffer.count = slot.count;
            count++;
        }

        if (count <= 1) return false;      // 0 或 1 堆，排序没有意义

        // 上次排得更多时缓冲区会长出来，截掉尾巴，保证只排有效部分
        if (_sortBuffer.Count > count) _sortBuffer.RemoveRange(count, _sortBuffer.Count - count);

        // ② 排序（只排快照，不动 _slots）
        _sortBuffer.Sort(compare);

        // ③ 写回：前 count 个槽按序填，剩下的槽全部清空（空槽自然沉底）
        bool changed = false;
        for (int i = 0; i < _slots.Count; i++)
        {
            int newItemId = 0;
            int newCount = 0;
            if (i < count)
            {
                newItemId = _sortBuffer[i].itemId;
                newCount = _sortBuffer[i].count;
            }

            var slot = _slots[i];
            if (slot.itemId != newItemId || slot.count != newCount) changed = true;
            slot.itemId = newItemId;
            slot.count = newCount;
        }

        if (changed) Notify(-1);
        return changed;
    }

    private void Notify(int slotIndex) => OnSlotChanged?.Invoke(slotIndex);
}

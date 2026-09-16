using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一个背包的登记信息：它是谁、多大、收什么、叫什么。
/// 集中放在 BagManager 的 Defs 表里，一处定义所有背包。
/// </summary>
public struct BagDef
{
    /// <summary>哪个背包</summary>
    public BagType bagType;

    /// <summary>槽位总数（建议为背包预制体 VirtualGridList._columns 的整数倍，否则最后一行残缺）</summary>
    public int capacity;

    /// <summary>收哪种类型的物品（对应 ItemData.itemType）</summary>
    public ItemType acceptItemType;

    /// <summary>显示名（UI 标题用）</summary>
    public string displayName;

    public BagDef(BagType bagType, int capacity, ItemType acceptItemType, string displayName)
    {
        this.bagType = bagType;
        this.capacity = capacity;
        this.acceptItemType = acceptItemType;
        this.displayName = displayName;
    }
}

/// <summary>
/// 背包管理器（单例，跨场景）：登记"游戏里有哪些背包"，按需创建，并提供统一增删入口。
///
/// 【单例与跨场景】
///   继承工程里的 SingletonMono（Assets/script/基类/SingletonMono.cs）：
///     - 手动挂到某个场景物体上也行，不挂也行（第一次访问 Instance 时自动创建）
///     - Awake 里自动 DontDestroyOnLoad —— 换场景背包数据不丢
///     - 场景里出现重复实例会被销毁，永远只剩一个
///   退出 Play 模式时随 DDOL 物体一起销毁 → 下次进 Play 是干净状态
///  （这也顺带修了编辑器"关 domain reload 时 static 跨会话残留"的隐患。）
///
/// 【加一个新背包只要两步】
///   1) BagType 枚举末尾加一个值
///   2) 下面 Defs 表里加一行（容量 / 收哪种 ItemType / 显示名）
/// 不用动 BagData，也不用动任何 UI —— BagPanel 只认 BagType，尺寸/标题都从这张表读。
///
/// 【懒创建】没被访问过的背包不会创建，白占的背包不花内存。
/// 【纯内存】换场景不丢，但退出游戏即清，不落盘（存档是另一个话题）。
/// </summary>
public class BagManager : SingletonMono<BagManager>
{
    /// <summary>
    /// 所有背包的登记表。
    /// 数组顺序 = Add(itemId) 自动路由的优先级（排前面的先收）。
    ///
    /// ⚠ 容量必须是背包预制体上 VirtualGridList._columns 的整数倍（现在是 6），
    ///   否则最后一行会残缺。这条是纯约定，没有编译期保障。
    ///
    /// 注：故意不做成 [SerializeField] —— 挂在场景里的实例会把序列化值"冻结"，
    /// 以后改代码里的默认值场景不跟着变，是经典坑。要调容量就改这里。
    /// </summary>
    private readonly BagDef[] Defs =
    {
        new BagDef(BagType.Item,   120, ItemType.Item,   "道具背包"),   // 6 列 x 20 行
        new BagDef(BagType.Weapon,  12, ItemType.Weapon, "武器背包"),   // 6 列 x 2 行
    };

    /// <summary>已创建的背包（懒创建后缓存）</summary>
    private readonly Dictionary<BagType, BagData> Bags = new Dictionary<BagType, BagData>();

    /// <summary>取背包（第一次访问时按登记表创建；没登记过返回 null）</summary>
    public BagData Get(BagType bagType)
    {
        if (Bags.TryGetValue(bagType, out var bag)) return bag;

        if (!TryGetDef(bagType, out var def))
        {
            Debug.LogError($"[BagManager] BagType.{bagType} 没有登记在 Defs 表里");
            return null;
        }

        bag = new BagData(def.bagType, def.capacity, def.acceptItemType);
        Bags[bagType] = bag;
        return bag;
    }

    /// <summary>取登记信息（没登记过返回 false）</summary>
    public bool TryGetDef(BagType bagType, out BagDef def)
    {
        for (int i = 0; i < Defs.Length; i++)
        {
            if (Defs[i].bagType == bagType)
            {
                def = Defs[i];
                return true;
            }
        }
        def = default;
        return false;
    }

    /// <summary>找出哪个背包收这件东西（配置查不到、或没有任何包收这种类型，返回 null）</summary>
    public BagData GetBagFor(int itemId)
    {
        var config = ItemDatabase.Resolve(itemId);
        if (config == null) return null;

        for (int i = 0; i < Defs.Length; i++)
        {
            if (Defs[i].acceptItemType == config.itemType) return Get(Defs[i].bagType);
        }
        return null;
    }

    /// <summary>
    /// 统一入口：按物品类型自动路由到对应的背包。
    /// 返回**没放进去的余量**（0 = 全部放入，&gt;0 = 那个包满了）。
    /// </summary>
    public int Add(int itemId, int count = 1)
    {
        var config = ItemDatabase.Resolve(itemId);
        if (config == null)
        {
            Debug.LogWarning($"[BagManager] 找不到 itemId={itemId} 的配置，未放入 {count} 个");
            return count;
        }

        var bag = GetBagFor(itemId);
        if (bag == null)
        {
            Debug.LogWarning($"[BagManager] 没有任何背包收 {config.itemType} 类型的 \"{config.itemName}\"，未放入");
            return count;
        }

        return bag.Add(itemId, count);
    }

    /// <summary>指定放进哪个背包（不预检类型，收不收由 BagData.Add 自己拒绝）</summary>
    public int AddTo(BagType bagType, int itemId, int count = 1)
    {
        var bag = Get(bagType);
        return bag != null ? bag.Add(itemId, count) : count;
    }
}

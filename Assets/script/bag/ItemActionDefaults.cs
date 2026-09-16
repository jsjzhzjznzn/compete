using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 物品操作的"类型默认表"（ScriptableObject，放 Resources/ItemActionDefaults.asset）。
///
/// 【为什么需要它】
///   ItemData 上可以直接列表挂策略，但那意味着每配一件物品都要拖一遍策略 —— 100 件道具拖 100 次。
///   所以加这一层默认：**按类型给一套默认操作**，物品想要特例再自己挂（见 ItemData.GetActions）。
///
///     道具（ItemType.Item）   → 使用、丢弃、合并
///     武器（ItemType.Weapon） → 丢弃、合并（穿戴要等 instanceId，见 ItemActionEquip 的计划）
///
///   某把"绑定武器"想只有穿戴没有丢弃？在那个 ItemData 的 _actions 上单独挂一份即可，
///   这条默认表完全不用动。
///
/// 【和 ItemDatabase 一样是资产】
///   表里存的是**策略资产引用**，代码里没法直接 new 一个引用出来，所以必须做成 SO 在 Inspector 里拖。
///   懒加载单例 + 线性查找（条目只有几个）。
/// </summary>
[CreateAssetMenu(fileName = "ItemActionDefaults", menuName = "Create/Bag/ItemActionDefaults")]
public class ItemActionDefaults : ScriptableObject
{
    /// <summary>一个类型 → 一套默认操作</summary>
    [Serializable]
    public class Entry
    {
        public ItemType itemType;
        public List<ItemAction> actions = new List<ItemAction>();
    }

    [SerializeField, Header("按类型配默认操作集（物品自己挂了 _actions 的话以物品为准）")]
    private List<Entry> _entries = new List<Entry>();

    private const string ResourcePath = "ItemActionDefaults";

    private static ItemActionDefaults _instance;

    /// <summary>查不到时返回的空表（避免调用方到处判 null）</summary>
    private static readonly List<ItemAction> Empty = new List<ItemAction>();

    /// <summary>单例（从 Resources 加载；未配置返回 null）</summary>
    public static ItemActionDefaults Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<ItemActionDefaults>(ResourcePath);
            return _instance;
        }
    }

    /// <summary>取某个类型的默认操作集（未配置总表 / 该类型没配 → 空表）</summary>
    public static IReadOnlyList<ItemAction> Get(ItemType itemType)
    {
        var db = Instance;
        if (db == null) return Empty;

        for (int i = 0; i < db._entries.Count; i++)
        {
            var entry = db._entries[i];
            if (entry != null && entry.itemType == itemType) return entry.actions ?? Empty;
        }
        return Empty;
    }
}

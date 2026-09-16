using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道具 / 武器配置总表（ScriptableObject，放 Resources/ItemDatabase.asset）
///
/// 用途：按 itemIdHash 反查 ItemData。
/// 背包槽位里只存 int（不存 SO 引用），显示、使用、以后存档/联网都靠本表把 id 还原成配置。
/// 道具背包和武器背包共用这一张表，各自按 ItemData.itemType 过滤。
///
/// 写法与 BuffDatabase 一致：懒加载单例 + 懒建 Dictionary 映射 + OnValidate 清映射。
/// 未配置总表时 Resolve 返回 null（不会抛异常），只是格子显示为空。
/// </summary>
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Create/Bag/ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField, Header("全部道具/武器配置（道具背包与武器背包共用此表，按 ItemType 区分）")]
    private List<ItemData> _items = new List<ItemData>();

    private const string ResourcePath = "ItemDatabase";

    private static ItemDatabase _instance;

    private Dictionary<int, ItemData> _map;

    /// <summary>表里全部配置（遍历/调试用；正常按 id 显示走 Resolve）</summary>
    public IReadOnlyList<ItemData> AllItems => _items;

    /// <summary>单例（从 Resources 加载；未配置返回 null）</summary>
    public static ItemDatabase Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<ItemDatabase>(ResourcePath);
            return _instance;
        }
    }

    /// <summary>按 itemIdHash 反查配置（未配置总表或查不到返回 null）</summary>
    public static ItemData Resolve(int itemIdHash)
    {
        var db = Instance;
        if (db == null) return null;
        db.EnsureMap();
        return db._map.TryGetValue(itemIdHash, out var data) ? data : null;
    }

    private void EnsureMap()
    {
        if (_map != null) return;
        _map = new Dictionary<int, ItemData>();
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item == null) continue;
            _map[item.itemIdHash] = item;
        }
    }

    /// <summary>编辑器改表后重建映射（静态缓存，运行时构建一次）</summary>
    private void OnValidate() => _map = null;
}

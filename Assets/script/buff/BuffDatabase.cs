using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buff 配置总表（ScriptableObject，放在 Resources/BuffDatabase.asset）
/// 用途：按 buffIdHash 反查 BuffData —— 联网时 RPC 只能传 id（不能传 SO 引用），
/// 服务端/客户端拿到 id 后用本表还原配置。
///
/// buffIdHash 走 Animator.StringToHash（确定性哈希，跨端/跨进程稳定），可安全用于网络。
/// </summary>
[CreateAssetMenu(fileName = "BuffDatabase", menuName = "Create/Buff/BuffDatabase")]
public class BuffDatabase : ScriptableObject
{
    [SerializeField, Header("全部 Buff 配置（服务端/客户端按 id 反查用）")]
    private List<BuffData> _buffs = new List<BuffData>();

    private Dictionary<int, BuffData> _map;

    private const string ResourcePath = "BuffDatabase";

    private static BuffDatabase _instance;

    /// <summary>单例（从 Resources 加载；未配置返回 null）</summary>
    public static BuffDatabase Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<BuffDatabase>(ResourcePath);
            return _instance;
        }
    }

    /// <summary>按 buffIdHash 反查配置（未配置总表或查不到返回 null）</summary>
    public static BuffData Resolve(int buffIdHash)
    {
        var db = Instance;
        if (db == null) return null;
        db.EnsureMap();
        return db._map.TryGetValue(buffIdHash, out var data) ? data : null;
    }

    private void EnsureMap()
    {
        if (_map != null) return;
        _map = new Dictionary<int, BuffData>();
        for (int i = 0; i < _buffs.Count; i++)
        {
            var b = _buffs[i];
            if (b == null) continue;
            _map[b.buffIdHash] = b;
        }
    }

    /// <summary>编辑器改表后重建映射（静态缓存，运行时构建一次）</summary>
    private void OnValidate() => _map = null;
}

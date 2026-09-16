using System;
using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using UnityEngine;

/// <summary>
/// 物品图标加载器：按 <see cref="ItemData.iconPath"/> 里配的路径（png 全路径），
/// 用 YooAsset 异步加载 Sprite 并缓存在内存。
///
/// 【为什么要有这一层】
///   1) 图标是异步加载的，而格子会被复用（同一个格子一会儿显示药水、一会儿显示零件）。
///      加载回来的那一刻，格子可能早就换成别的物品了 —— 所以调用方拿到结果后必须自己校验，
///      本类不做这件事（它不认识格子），只保证"同一路径只加载一次 + 结果缓存住"。
///   2) 缓存：滚动时同一张图会被反复请求，命中缓存直接同步返回，不产生任何异步开销。
///   3) 合并请求：同一帧内有多个格子要同一张图，只会真正发起一次加载，回调分发给所有等待者。
///
/// 【路径形式】
///   散图全路径，例：Assets/Resource/ui/mingchao/道具/T_IconA80_02_UI.png
///   带不带 .png 都行 —— ResourceManager 的 NormalizeToLocation 会去掉扩展名做归一化。
///   前提：该目录必须被 YooAsset 收集器收集（mingchao 目录已经在收集器里）。
///
/// 【YooAsset 初始化兜底】
///   工程的 YooAsset 初始化目前只有 UIManager 会触发，而背包 UI 不走 UIManager。
///   所以这里发现还没初始化时，用 BagManager（常驻单例 MonoBehaviour）跑一次初始化，
///   完成后再把排队中的请求统一发起。YooAssetService.InitializeAsync 自身有防重入，
///   跟 UIManager 那条路撞上也是安全的。
/// </summary>
public static class ItemIconLoader
{
    /// <summary>路径 → 已加载好的 Sprite</summary>
    private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    /// <summary>路径 → 还在等这张图的回调（同一张图被多个格子同时要时只真正加载一次）</summary>
    private static readonly Dictionary<string, List<Action<Sprite>>> _pending =
        new Dictionary<string, List<Action<Sprite>>>();

    /// <summary>加载失败过的路径：记住，免得每次滚动都重试 + 刷屏</summary>
    private static readonly HashSet<string> _failed = new HashSet<string>();

    /// <summary>初始化完成后遍历待加载列表用的临时表（避免边遍历边改集合）</summary>
    private static readonly List<string> _flushPaths = new List<string>();

    private static bool _initRequested;

    /// <summary>
    /// 同步取图标：缓存里有就返回，没有则返回 null **并顺手发起一次异步加载**。
    /// 滚动复用格子时绝大多数命中缓存，所以这条路径是主力。
    /// </summary>
    public static Sprite Get(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (_cache.TryGetValue(path, out var sprite)) return sprite;

        Request(path, null);
        return null;
    }

    /// <summary>
    /// 异步取图标。同一张图的重复请求会合并，加载完成后每个回调都会收到同一个 Sprite。
    /// ⚠ 命中缓存时 <paramref name="onLoaded"/> 是**同步**调用的，调用方不要假设它下一帧才来
    ///   （所以防串味的 token 必须在调用 Request **之前**就取好）。
    /// </summary>
    public static void Request(string path, Action<Sprite> onLoaded)
    {
        if (string.IsNullOrEmpty(path))
        {
            onLoaded?.Invoke(null);
            return;
        }

        if (_cache.TryGetValue(path, out var cached))
        {
            onLoaded?.Invoke(cached);
            return;
        }

        if (_failed.Contains(path))
        {
            onLoaded?.Invoke(null);   // 之前就失败过，不再重试
            return;
        }

        if (!_pending.TryGetValue(path, out var waiters))
        {
            waiters = new List<Action<Sprite>>();
            _pending[path] = waiters;
        }
        if (onLoaded != null) waiters.Add(onLoaded);

        // YooAsset 还没就绪：先排队，初始化完成后统一发起
        if (!EnsureYooAssetReady()) return;

        StartLoad(path);
    }

    /// <summary>清空缓存（换账号 / 主动释放资源时用；正常不需要调）</summary>
    public static void Clear()
    {
        _cache.Clear();
        _pending.Clear();
        _failed.Clear();
    }

    // ==================== 内部实现 ====================

    private static void StartLoad(string path)
    {
        if (!_pending.TryGetValue(path, out var waiters)) return;
        _pending.Remove(path);

        ResourceManager.Instance.LoadAssetAsync<Sprite>(path, sprite =>
        {
            if (sprite != null)
            {
                _cache[path] = sprite;
            }
            else
            {
                _failed.Add(path);
                Debug.LogWarning($"[ItemIconLoader] 图标加载失败：{path}" +
                                 "（检查 SO 里配的路径对不对、以及该目录有没有被 YooAsset 收集器收集）");
            }

            for (int i = 0; i < waiters.Count; i++)
            {
                waiters[i]?.Invoke(sprite);
            }
        });
    }

    /// <summary>确保 YooAsset 已就绪。返回 false 表示"还没好，请求先排队"</summary>
    private static bool EnsureYooAssetReady()
    {
        var service = YooAssetService.Instance;
        if (service != null && service.IsInitialized) return true;

        if (!_initRequested)
        {
            _initRequested = true;

            // BagManager 是常驻单例 MonoBehaviour，正好拿来跑协程
            var host = BagManager.Instance;
            if (host != null)
            {
                host.StartCoroutine(CoInitialize());
            }
            else
            {
                Debug.LogError("[ItemIconLoader] 拿不到 BagManager，没法兜底初始化 YooAsset，图标会加载不出来");
            }
        }
        return false;
    }

    private static IEnumerator CoInitialize()
    {
        yield return ResourceManager.Instance.InitializeAsync();

        if (YooAssetService.Instance == null || !YooAssetService.Instance.IsInitialized)
        {
            Debug.LogError("[ItemIconLoader] YooAsset 初始化失败，物品图标会加载不出来");
            yield break;
        }

        // 初始化好了：把排队中的请求全部发起
        _flushPaths.Clear();
        foreach (var path in _pending.Keys) _flushPaths.Add(path);
        for (int i = 0; i < _flushPaths.Count; i++) StartLoad(_flushPaths[i]);
        _flushPaths.Clear();
    }
}

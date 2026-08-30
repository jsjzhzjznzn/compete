using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;

namespace SkierFramework
{
    /// <summary>
    /// YooAsset 运行模式
    /// </summary>
    public enum YooAssetRunMode
    {
        /// <summary>编辑器模拟（走 AssetDatabase，无需打 AB 包）</summary>
        EditorSimulate,
        /// <summary>离线包（bundle 内置在 StreamingAssets）</summary>
        OfflinePlay,
        /// <summary>联机模式（热更，暂未接入下载流程）</summary>
        HostPlay,
    }

    /// <summary>
    /// YooAsset 初始化解耦层：UI 框架只依赖本类，切换运行模式/增减资源包只需改这里的配置
    /// 使用方式：先初始化（UIManager 初始化协程第一步会自动调用），再通过 LoadAssetAsync 等接口加载
    /// </summary>
    public class YooAssetService : Singleton<YooAssetService>
    {
        /// <summary>默认包名（未配置路由前缀的资源都归它管，需与 BundleCollectorSetting 包名一致）</summary>
        public const string DefaultPackageName = "test";

        /// <summary>
        /// 需要初始化的包列表（多包时把包名加进来，需与 BundleCollectorSetting 中的包名一致）
        /// </summary>
        public static readonly string[] PackageNames =
        {
            DefaultPackageName,
        };

        /// <summary>
        /// 路径前缀 → 包名 路由表（最长前缀优先；未命中回退默认包）。
        /// 前缀需与 BundleCollectorSetting 中各包的收集路径保持一致。
        /// 例：("Assets/Game/Battle", "battle") 表示 Assets/Game/Battle 下的资源都从 battle 包加载
        /// </summary>
        public static readonly (string prefix, string package)[] LocationRoutes =
        {
            ("Assets/HOTS/UI", "test"),
        };

        /// <summary>已创建的包实例缓存：包名 → 包（初始化时填充）</summary>
        private readonly Dictionary<string, ResourcePackage> _packages = new Dictionary<string, ResourcePackage>();

        /// <summary>默认包（未配置路由的资源都走它），初始化完成后才非空</summary>
        public ResourcePackage Package => GetPackage(DefaultPackageName);
        /// <summary>全部包的只读视图（遍历用）</summary>
        public IReadOnlyDictionary<string, ResourcePackage> Packages => _packages;
        /// <summary>当前运行模式（EditorSimulate / OfflinePlay / HostPlay）</summary>
        public YooAssetRunMode RunMode { get; private set; }
        /// <summary>所有包是否初始化完成（加载接口的可用性标志）</summary>
        public bool IsInitialized { get; private set; }
        /// <summary>最近一次初始化失败原因（成功时为 null）</summary>
        public string LastError { get; private set; }

        /// <summary>外部在初始化前赋值即可强制指定运行模式（默认：编辑器=模拟，真机=离线）</summary>
        public static YooAssetRunMode? ForceRunMode;

        /// <summary>初始化进行中标记（防重入：多个调用方并发进入时只跑一次流程）</summary>
        private bool _initializing;
        /// <summary>本次初始化是否失败（失败时等待方轮询退出，避免死等）</summary>
        private bool _initFailed;

        /// <summary>
        /// 默认运行模式：编辑器下走模拟模式（直接读 AssetDatabase，不用打 AB 包），真机走离线模式（bundle 随包内置）
        /// </summary>
        private static YooAssetRunMode GetDefaultRunMode()
        {
#if UNITY_EDITOR
            return YooAssetRunMode.EditorSimulate;
#else
            return YooAssetRunMode.OfflinePlay;
#endif
        }

        /// <summary>
        /// 按包名取包实例（未初始化或包名不存在返回 null）
        /// </summary>
        public ResourcePackage GetPackage(string packageName)
        {
            _packages.TryGetValue(packageName, out var pkg);
            return pkg;
        }

        /// <summary>
        /// 按 location 前缀路由到所属资源包（最长前缀优先，未命中回退默认包）。
        /// 注意：返回的包可能为 null（尚未初始化），调用方需先确认 IsInitialized
        /// </summary>
        public ResourcePackage GetPackageByLocation(string location)
        {
            ResourcePackage best = Package;
            int bestLen = -1;
            foreach (var (prefix, packageName) in LocationRoutes)
            {
                // 前缀匹配 + 比当前候选更长 + 该包确实已创建，三者同时满足才更新候选
                if (location.StartsWith(prefix) && prefix.Length > bestLen && GetPackage(packageName) != null)
                {
                    best = GetPackage(packageName);
                    bestLen = prefix.Length;
                }
            }
            return best;
        }

        /// <summary>
        /// 初始化全部包（防重入：并发调用方等待同一流程结束）。
        /// 每个包依次走三步：初始化文件系统 → 请求包版本 → 加载激活清单。
        /// 注意：3.0.5 中激活清单必须单独加载，否则后续 LoadAssetAsync 会报 "Active package manifest not found"
        /// </summary>
        public IEnumerator InitializeAsync(YooAssetRunMode? mode = null)
        {
            // 已初始化完成：校验底层 YooAssets 是否还活着（编辑器关闭域重载时静态状态残留，
            // 但 YooAssets 会被 SubsystemRegistration 重置，此时需自愈重置再走完整初始化）
            if (IsInitialized)
            {
                if (!YooAssets.IsInitialized || !YooAssets.TryGetPackage(DefaultPackageName, out _))
                {
                    IsInitialized = false;
                    _packages.Clear();
                }
                else
                {
                    yield break;
                }
            }
            // 已有初始化流程在进行：等待其结束（失败也退出，避免死等）
            if (_initializing)
            {
                while (!IsInitialized && !_initFailed) yield return null;
                yield break;
            }

            _initializing = true;
            _initFailed = false;
            RunMode = mode ?? ForceRunMode ?? GetDefaultRunMode();
            LastError = null;

            // 第一步：全局初始化 + 创建所有包（同步操作，单独包 try/catch 是因为协程不能在带 catch 的 try 里 yield）
            bool setupOk = false;
            try
            {
                if (!YooAssets.IsInitialized)
                {
                    YooAssets.Initialize();
                }

                foreach (var packageName in PackageNames)
                {
                    if (!YooAssets.TryGetPackage(packageName, out var pkg))
                    {
                        pkg = YooAssets.CreatePackage(packageName);
                    }
                    _packages[packageName] = pkg;
                }
                setupOk = true;
            }
            catch (System.Exception e)
            {
                LastError = e.Message;
                Debug.LogError($"[YooAssetService] 初始化异常：{e}");
            }

            // 第二步：逐包异步初始化（任一包失败则整体标记失败）
            if (setupOk)
            {
                bool allOk = true;
                foreach (var kv in _packages)
                {
                    // 2.1 初始化文件系统（编辑器模拟模式会在此触发一次 simulate build，耗时秒级属正常）
                    var initOp = kv.Value.InitializePackageAsync(CreateOptions(RunMode, kv.Key));
                    yield return initOp;
                    if (initOp.Status != EOperationStatus.Succeeded)
                    {
                        LastError = $"{kv.Key}: {initOp.Error}";
                        Debug.LogError($"[YooAssetService] 包 {kv.Key} 初始化失败：{initOp.Error}");
                        allOk = false;
                        break;
                    }

                    // 2.2 请求当前包版本号（离线模式读 StreamingAssets 内的版本文件）
                    var versionOp = kv.Value.RequestPackageVersionAsync();
                    yield return versionOp;
                    if (versionOp.Status != EOperationStatus.Succeeded)
                    {
                        LastError = $"{kv.Key}: {versionOp.Error}";
                        Debug.LogError($"[YooAssetService] 包 {kv.Key} 请求版本失败：{versionOp.Error}");
                        allOk = false;
                        break;
                    }

                    // 2.3 按版本加载清单并设为激活（30 = 超时秒数）
                    var manifestOp = kv.Value.LoadPackageManifestAsync(new LoadPackageManifestOptions(versionOp.PackageVersion, 30));
                    yield return manifestOp;
                    if (manifestOp.Status != EOperationStatus.Succeeded)
                    {
                        LastError = $"{kv.Key}: {manifestOp.Error}";
                        Debug.LogError($"[YooAssetService] 包 {kv.Key} 加载清单失败：{manifestOp.Error}");
                        allOk = false;
                        break;
                    }
                }

                if (allOk)
                {
                    IsInitialized = true;
                    Debug.Log($"[YooAssetService] 初始化成功，模式={RunMode}，包数={_packages.Count}");
                }
            }

            _initializing = false;
            _initFailed = !IsInitialized;
        }

        /// <summary>
        /// 按运行模式构造包初始化选项（决定资源从哪读：编辑器模拟读 AssetDatabase，离线读 StreamingAssets）
        /// </summary>
        private static InitializePackageOptions CreateOptions(YooAssetRunMode mode, string packageName)
        {
            switch (mode)
            {
                case YooAssetRunMode.EditorSimulate:
                {
#if UNITY_EDITOR
                    // 自动执行编辑器模拟构建（读取 BundleCollectorSetting，产物进 Library/YooAsset 缓存，不产生 AB 包）
                    var result = EditorSimulateBuildInvoker.Build(packageName, (int)EBundleType.VirtualAssetBundle);
                    return new EditorSimulateModeOptions
                    {
                        EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(result.PackageRootDirectory),
                    };
#else
                    // 非编辑器下 EditorSimulate 不合法，回退离线模式
                    goto case YooAssetRunMode.OfflinePlay;
#endif
                }
                case YooAssetRunMode.OfflinePlay:
                {
                    return new OfflinePlayModeOptions
                    {
                        BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters(),
                    };
                }
                case YooAssetRunMode.HostPlay:
                {
                    // 预留：接入热更时实现 IRemoteService（远程版本/下载）并替换此选项
                    Debug.LogWarning("[YooAssetService] HostPlay 尚未接入热更下载，本次按 OfflinePlay 初始化");
                    goto case YooAssetRunMode.OfflinePlay;
                }
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(mode));
            }
        }

        /// <summary>卸载全部包的无用资产（清理时调用）</summary>
        public void UnloadUnusedAssets()
        {
            if (!IsInitialized) return;
            foreach (var pkg in _packages.Values)
            {
                pkg.UnloadUnusedAssetsAsync();
            }
        }

        /// <summary>按 location 路由到所属包并卸载其无用资产</summary>
        public void TryUnloadUnusedAsset(string location)
        {
            var pkg = GetPackageByLocation(location);
            if (pkg != null)
            {
                pkg.TryUnloadUnusedAsset(location);
            }
        }

        /// <summary>异步加载资源（按 location 自动路由到所属包）</summary>
        public AssetHandle LoadAssetAsync<TObject>(string location) where TObject : UnityEngine.Object
        {
            return GetPackageByLocation(location).LoadAssetAsync<TObject>(location);
        }

        /// <summary>同步加载资源（按 location 自动路由到所属包；编辑器模拟模式可用，真机大资源慎用）</summary>
        public AssetHandle LoadAssetSync<TObject>(string location) where TObject : UnityEngine.Object
        {
            return GetPackageByLocation(location).LoadAssetSync<TObject>(location);
        }

        /// <summary>异步加载场景（按 location 自动路由到所属包）</summary>
        public YooAsset.SceneHandle LoadSceneAsync(string location, LoadSceneMode mode)
        {
            return GetPackageByLocation(location).LoadSceneAsync(location, mode);
        }

        /// <summary>校验 location 是否存在于所属包（预热/判空用）</summary>
        public bool IsLocationValid(string location)
        {
            var pkg = GetPackageByLocation(location);
            return pkg != null && pkg.IsLocationValid(location);
        }
    }
}

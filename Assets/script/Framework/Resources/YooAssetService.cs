using System.Collections;
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
    /// YooAsset 初始化解耦层：UI 框架只依赖本类，切换运行模式只需改这里
    /// </summary>
    public class YooAssetService : Singleton<YooAssetService>
    {
        public const string DefaultPackageName = "test";

        public ResourcePackage Package { get; private set; }
        public YooAssetRunMode RunMode { get; private set; }
        public bool IsInitialized { get; private set; }
        public string LastError { get; private set; }

        /// <summary>外部在初始化前赋值即可强制指定运行模式（默认：编辑器=模拟，真机=离线）</summary>
        public static YooAssetRunMode? ForceRunMode;

        private bool _initializing;
        private bool _initFailed;

        private static YooAssetRunMode GetDefaultRunMode()
        {
#if UNITY_EDITOR
            return YooAssetRunMode.EditorSimulate;
#else
            return YooAssetRunMode.OfflinePlay;
#endif
        }

        /// <summary>
        /// 初始化（防重入：并发调用方等待同一流程结束）
        /// </summary>
        public IEnumerator InitializeAsync(YooAssetRunMode? mode = null)
        {
            if (IsInitialized)
            {
                // 编辑器关闭域重载时静态状态残留，但 YooAssets 会被 SubsystemRegistration 重置，需要自愈
                if (!YooAssets.IsInitialized || !YooAssets.TryGetPackage(DefaultPackageName, out _))
                {
                    IsInitialized = false;
                    Package = null;
                }
                else
                {
                    yield break;
                }
            }
            if (_initializing)
            {
                while (!IsInitialized && !_initFailed) yield return null;
                yield break;
            }

            _initializing = true;
            _initFailed = false;
            RunMode = mode ?? ForceRunMode ?? GetDefaultRunMode();
            LastError = null;

            InitializePackageOperation op = null;
            try
            {
                if (!YooAssets.IsInitialized)
                {
                    YooAssets.Initialize();
                }

                if (!YooAssets.TryGetPackage(DefaultPackageName, out var pkg))
                {
                    pkg = YooAssets.CreatePackage(DefaultPackageName);
                }
                Package = pkg;

                op = Package.InitializePackageAsync(CreateOptions(RunMode));
            }
            catch (System.Exception e)
            {
                LastError = e.Message;
                Debug.LogError($"[YooAssetService] 初始化异常：{e}");
            }

            if (op != null)
            {
                yield return op;

                if (op.Status == EOperationStatus.Succeeded)
                {
                    // 3.0.5 中 InitializePackageAsync 只建立文件系统，激活清单需单独加载
                    var versionOp = Package.RequestPackageVersionAsync();
                    yield return versionOp;

                    if (versionOp.Status == EOperationStatus.Succeeded)
                    {
                        var manifestOp = Package.LoadPackageManifestAsync(new LoadPackageManifestOptions(versionOp.PackageVersion, 30));
                        yield return manifestOp;

                        if (manifestOp.Status == EOperationStatus.Succeeded)
                        {
                            IsInitialized = true;
                            Debug.Log($"[YooAssetService] 初始化成功，模式={RunMode}，版本={versionOp.PackageVersion}");
                        }
                        else
                        {
                            LastError = manifestOp.Error;
                            Debug.LogError($"[YooAssetService] 加载资源清单失败：{manifestOp.Error}");
                        }
                    }
                    else
                    {
                        LastError = versionOp.Error;
                        Debug.LogError($"[YooAssetService] 请求包版本失败：{versionOp.Error}");
                    }
                }
                else
                {
                    LastError = op.Error;
                    Debug.LogError($"[YooAssetService] 初始化失败：{op.Error}");
                }
            }

            _initializing = false;
            _initFailed = !IsInitialized;
        }

        private static InitializePackageOptions CreateOptions(YooAssetRunMode mode)
        {
            switch (mode)
            {
                case YooAssetRunMode.EditorSimulate:
                {
#if UNITY_EDITOR
                    // 自动执行编辑器模拟构建（读取 BundleCollectorSetting，产物进 Library/YooAsset 缓存）
                    var result = EditorSimulateBuildInvoker.Build(DefaultPackageName, (int)EBundleType.VirtualAssetBundle);
                    return new EditorSimulateModeOptions
                    {
                        EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(result.PackageRootDirectory),
                    };
#else
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
                    // 预留：接入热更时实现 IRemoteService 并替换此选项
                    Debug.LogWarning("[YooAssetService] HostPlay 尚未接入热更下载，本次按 OfflinePlay 初始化");
                    goto case YooAssetRunMode.OfflinePlay;
                }
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(mode));
            }
        }

        /// <summary>卸载无用资产（清理时调用）</summary>
        public void UnloadUnusedAssets()
        {
            if (IsInitialized && Package != null)
            {
                Package.UnloadUnusedAssetsAsync();
            }
        }

        public AssetHandle LoadAssetAsync<TObject>(string location) where TObject : UnityEngine.Object
        {
            return Package.LoadAssetAsync<TObject>(location);
        }

        public AssetHandle LoadAssetSync<TObject>(string location) where TObject : UnityEngine.Object
        {
            return Package.LoadAssetSync<TObject>(location);
        }

        public YooAsset.SceneHandle LoadSceneAsync(string location, LoadSceneMode mode)
        {
            return Package.LoadSceneAsync(location, mode);
        }

        public bool IsLocationValid(string location)
        {
            return Package != null && Package.IsLocationValid(location);
        }
    }
}

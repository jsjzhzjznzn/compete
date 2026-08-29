using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;

namespace SkierFramework
{
    /// <summary>
    /// 资源管理器（基于 YooAsset）
    /// - path 统一为 YooAsset location：完整资源路径（可带扩展名），如 "Assets/HOTS/UI/Prefabs/Mainman.prefab"
    /// - 保留引用计数 / 对象池 / 常驻资源 / 缓存语义
    /// - 缓存句柄（AssetHandle）与缓存资产同生共死，卸载时统一 Release
    /// </summary>
    public class ResourceManager : Singleton<ResourceManager>
    {
        /// <summary>
        /// 已加载资源路径对应的资源缓存
        /// </summary>
        private Dictionary<string, UnityEngine.Object> _assetCaches = new Dictionary<string, UnityEngine.Object>();
        /// <summary>
        /// 已加载资源路径对应的缓存句柄（与 _assetCaches 严格同步）
        /// </summary>
        private Dictionary<string, AssetHandle> _assetHandles = new Dictionary<string, AssetHandle>();
        /// <summary>
        /// 常驻内存中的资源路径哈希集
        /// </summary>
        private HashSet<string> _residentAssetsHashSet = new HashSet<string>();
        /// <summary>
        /// 调用清除时使用
        /// </summary>
        private HashSet<string> _clearAssetsSet = new HashSet<string>();
        /// <summary>
        /// 资源的引用个数
        /// </summary>
        private Dictionary<string, int> _loadedAssetInstanceCountDic = new Dictionary<string, int>();
        /// <summary>
        /// 已实例化对象对应的Key
        /// key: entityId
        /// value: path
        /// </summary>
        private Dictionary<EntityId, string> _objectInstanceIdKeyDic = new Dictionary<EntityId, string>();
        /// <summary>
        /// instancePool
        /// </summary>
        private InstancePool _instancePool;
        /// <summary>
        /// 真实加载中的资源数量
        /// </summary>
        private int _processingLoadCount = 0;

        public bool IsProcessLoading
        {
            get => _processingLoadCount > 0;
        }

        public override void OnInitialize()
        {
            base.OnInitialize();
            _instancePool = new InstancePool();
        }

        #region 初始化/清除
        /// <summary>
        /// 初始化 YooAsset（防重入）
        /// </summary>
        public IEnumerator InitializeAsync()
        {
            yield return YooAssetService.Instance.InitializeAsync();
        }

        /// <summary>
        /// 清除所有常驻资源之外的资源
        /// </summary>
        public IEnumerable CleanupAsync()
        {
            yield return new WaitUntil(() => {
                return !IsProcessLoading;
            });

            Cleanup();
        }

        /// <summary>
        /// 清除所有常驻资源之外的资源
        /// </summary>
        public void Cleanup()
        {
            foreach (var item in _assetCaches)
            {
                if (!_residentAssetsHashSet.Contains(item.Key))
                {
                    _clearAssetsSet.Add(item.Key);
                }
            }
            foreach (var key in _clearAssetsSet)
            {
                if (_spriteCache.TryGetValue(key, out SpriteAtlas spriteAtlas))
                {
                    spriteAtlas.Cleanup();
                    _spriteCache.Remove(key);
                }

                ReleaseHandle(key);
                _assetCaches.Remove(key);
                _loadedAssetInstanceCountDic.Remove(key);
                _instancePool.Clear(key);
            }
            _clearAssetsSet.Clear();
            YooAssetService.Instance.UnloadUnusedAssets();
        }

        /// <summary>
        /// 增加常驻资源
        /// </summary>
        public void AddResidentAsset(string key)
        {
            _residentAssetsHashSet.Add(NormalizeToLocation(key));
        }
        #endregion

        #region 实例化和回收对象
        public void InstantiateAsync(string path, Action<UnityEngine.GameObject> callback, bool active = true)
        {
            if (string.IsNullOrEmpty(path))
            {
                callback?.Invoke(null);
                return;
            }

            string location = NormalizeToLocation(path);
            if (!_assetCaches.ContainsKey(location))
            {
                //未加载过此资源
                LoadAssetAsyncInternal<GameObject>(location, (asset) =>
                {
                    if (asset == null)
                    {
                        Debug.LogErrorFormat("[InstantiateAsync] 加载失败：{0}！", path);
                        callback?.Invoke(null);
                        return;
                    }
                    InternalInstantiate(location, callback, active);
                });
                return;
            }
            InternalInstantiate(location, callback, active);
        }

        public IEnumerator CoInstantiateAsync(string path, Action<UnityEngine.GameObject> callback, bool active = true)
        {
            if (string.IsNullOrEmpty(path))
            {
                callback?.Invoke(null);
                yield break;
            }

            string location = NormalizeToLocation(path);
            bool done = _assetCaches.ContainsKey(location);
            if (!done)
            {
                LoadAssetAsyncInternal<GameObject>(location, (asset) => done = true);
            }
            while (!done) yield return null;

            if (_assetCaches.ContainsKey(location))
            {
                InternalInstantiate(location, callback, active);
            }
            else
            {
                Debug.LogErrorFormat("[CoInstantiateAsync] 加载失败：{0}！", path);
                callback?.Invoke(null);
            }
        }

        public void Recycle(UnityEngine.GameObject instanceObject, bool forceDestroy = false)
        {
            if (instanceObject == null)
            {
                return;
            }

            EntityId id = instanceObject.GetEntityId();
            if (_objectInstanceIdKeyDic.TryGetValue(id, out string path))
            {
                _instancePool.Recycle(path, instanceObject, forceDestroy);
                if (_loadedAssetInstanceCountDic.TryGetValue(path, out int count))
                {
                    _loadedAssetInstanceCountDic[path] = count - 1;
                }
                _objectInstanceIdKeyDic.Remove(id);
            }
            else
            {
                Debug.LogErrorFormat("此模块不回收不是从这个模块实例化出去的对象：{0}", instanceObject.name);
                GameObject.Destroy(instanceObject);
            }
        }

        /// <summary>
        /// 实例化
        /// </summary>
        private void InternalInstantiate(string location, Action<UnityEngine.GameObject> callback, bool active = true)
        {
            GameObject result = _instancePool.Get(location);
            GameObject invokeResult = null;

            if (result == null)
            {
                if (_assetCaches.TryGetValue(location, out UnityEngine.Object asset) && asset != null)
                {
                    invokeResult = GameObject.Instantiate(asset as GameObject);
                }
            }
            else
            {
                invokeResult = result;
            }

            if (invokeResult != null)
            {
                _instancePool.InitInst(invokeResult, active);
                _objectInstanceIdKeyDic[invokeResult.GetEntityId()] = location;
                if (_loadedAssetInstanceCountDic.TryGetValue(location, out int count))
                {
                    _loadedAssetInstanceCountDic[location] = count + 1;
                }
                else
                {
                    _loadedAssetInstanceCountDic[location] = 1;
                }
            }
            callback?.Invoke(invokeResult);
        }
        #endregion

        #region 资源加载/卸载   
        /// <summary>
        /// 加载资源（异步；已加载则走缓存直接回调）
        /// </summary>
        public void LoadAssetAsync<T>(string path, Action<T> onComplete, bool autoUnload = false) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path))
            {
                onComplete?.Invoke(null);
                return;
            }

            string location = NormalizeToLocation(path);
            LoadAssetAsyncInternal<T>(location, (asset) =>
            {
                if (asset == null)
                {
                    Debug.LogErrorFormat("[LoadAssetAsync] {0} 加载失败！", path);
                }
                onComplete?.Invoke(asset);
                if (autoUnload)
                {
                    UnLoadAsset(location);
                }
            });
        }

        public void LoadAssetAsync<T, T1>(string path, Action<T, T1> onComplete, T1 data1, bool autoUnload = false) where T : UnityEngine.Object
        {
            LoadAssetAsync<T>(path, (asset) =>
            {
                onComplete?.Invoke(asset, data1);
            }, autoUnload);
        }

        /// <summary>
        /// 加载并缓存资源（缓存命中直接回调；句柄与缓存同生共死）
        /// </summary>
        private void LoadAssetAsyncInternal<T>(string location, Action<T> onComplete) where T : UnityEngine.Object
        {
            if (_assetCaches.TryGetValue(location, out UnityEngine.Object cached))
            {
                onComplete?.Invoke(cached as T);
                return;
            }

            if (YooAssetService.Instance.Package == null)
            {
                Debug.LogErrorFormat("[LoadAssetAsync] {0} 加载失败：YooAsset 尚未初始化！", location);
                onComplete?.Invoke(null);
                return;
            }

            _processingLoadCount++;
            var handle = YooAssetService.Instance.LoadAssetAsync<T>(location);
            handle.Completed += (h) =>
            {
                _processingLoadCount--;
                if (h.Status == EOperationStatus.Succeeded && h.AssetObject != null)
                {
                    if (_assetCaches.TryGetValue(location, out UnityEngine.Object other))
                    {
                        // 并发加载：另一请求先完成并已缓存，本句柄立即自释放
                        if (h.IsValid) h.Release();
                        onComplete?.Invoke(other as T);
                        return;
                    }
                    _assetCaches[location] = h.AssetObject;
                    _assetHandles[location] = h;
                    if (!_loadedAssetInstanceCountDic.ContainsKey(location))
                    {
                        _loadedAssetInstanceCountDic.Add(location, 1);
                    }
                    onComplete?.Invoke(h.AssetObject as T);
                }
                else
                {
                    Debug.LogErrorFormat("[LoadAssetAsync] {0} 加载失败！{1}", location, h.Error);
                    if (h.IsValid) h.Release();
                    onComplete?.Invoke(null);
                }
            };
        }

        /// <summary>
        /// 释放缓存句柄（与 _assetCaches 同步移除）
        /// </summary>
        private void ReleaseHandle(string location)
        {
            if (_assetHandles.TryGetValue(location, out AssetHandle handle))
            {
                if (handle.IsValid)
                {
                    handle.Release();
                }
                _assetHandles.Remove(location);
            }
        }

        /// <summary>
        /// 直接卸载资源
        /// </summary>
        public void UnLoadAsset(string path)
        {
            string location = NormalizeToLocation(path);

            //判断卸载是否是一个常驻资源
            if (_residentAssetsHashSet.Contains(location))
            {
                Debug.LogErrorFormat("[UnLoadAsset] 禁止卸载常驻资源：{0} ！", path);
                return;
            }

            if (_assetCaches.ContainsKey(location))
            {
                Debug.Log(string.Format("[UnLoadAsset] 卸载资源：{0} ！", path));

                if (_spriteCache.TryGetValue(location, out SpriteAtlas spriteAtlas))
                {
                    spriteAtlas.Cleanup();
                    _spriteCache.Remove(location);
                }
                ReleaseHandle(location);
                _assetCaches.Remove(location);
                _loadedAssetInstanceCountDic.Remove(location);
                if (YooAssetService.Instance.Package != null)
                {
                    YooAssetService.Instance.Package.TryUnloadUnusedAsset(location);
                }
            }
            else
            {
                Debug.LogErrorFormat("[UnLoadAsset] 卸载未加载资源：{0} ！", path);
            }
        }

        /// <summary>
        /// 释放资源引用，当引用数为0时 自动卸载
        /// </summary>
        public void ReleaseRef(string path)
        {
            string location = NormalizeToLocation(path);
            if (_loadedAssetInstanceCountDic.TryGetValue(location, out int count))
            {
                _loadedAssetInstanceCountDic[location] = --count;
                if (count <= 0)
                {
                    UnLoadAsset(location);
                }
            }
        }
        #endregion

        #region 预加载/缓存
        public void PreLoadAssetAsync<T>(string path, Action<T> callback = null) where T : UnityEngine.Object
        {
            LoadAssetAsync<T>(path, (obj) =>
            {
                callback?.Invoke(obj);
            });
        }

        public IEnumerator CoPreLoadAsset<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path)) yield break;

            string location = NormalizeToLocation(path);
            if (_assetCaches.ContainsKey(location)) yield break;

            bool done = false;
            LoadAssetAsyncInternal<T>(location, (asset) => done = true);
            while (!done) yield return null;
        }

        public bool TryGetAsset<T>(string path, out T target) where T : UnityEngine.Object
        {
            target = null;
            string location = NormalizeToLocation(path);
            if (_assetCaches.TryGetValue(location, out UnityEngine.Object cached))
            {
                target = cached as T;
                return target != null;
            }

            if (YooAssetService.Instance.Package == null)
            {
                return false;
            }

            var handle = YooAssetService.Instance.LoadAssetSync<T>(location);
            if (handle.Status == EOperationStatus.Succeeded && handle.AssetObject != null)
            {
                _assetCaches[location] = handle.AssetObject;
                _assetHandles[location] = handle;
                if (!_loadedAssetInstanceCountDic.ContainsKey(location))
                {
                    _loadedAssetInstanceCountDic.Add(location, 1);
                }
                target = handle.AssetObject as T;
                return target != null;
            }

            if (handle.IsValid) handle.Release();
            return false;
        }
        #endregion

        #region 图片加载
        /// <summary>
        /// SpriteAtlas.GetSprite()会clone一份，不会重复使用 因此需要缓存
        /// </summary>
        private class SpriteAtlas
        {
            public UnityEngine.U2D.SpriteAtlas spriteAtlas;
            private Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

            public Sprite Get(string name)
            {
                if (!_spriteCache.TryGetValue(name, out Sprite sprite))
                {
                    sprite = spriteAtlas.GetSprite(name);
                    _spriteCache.Add(name, sprite);
                }
                return sprite;
            }

            public void Cleanup()
            {
                foreach (var sprite in _spriteCache.Values)
                {
                    GameObject.Destroy(sprite);
                }
                _spriteCache.Clear();
            }
        }
        private Dictionary<string, SpriteAtlas> _spriteCache = new Dictionary<string, SpriteAtlas>();
        public void LoadSpriteAsync(string atlasPath, string spriteName, Action<UnityEngine.Sprite> callback)
        {
            if (string.IsNullOrEmpty(atlasPath) || string.IsNullOrEmpty(spriteName))
            {
                Debug.LogErrorFormat("[LoadSpriteAsync] error：atlasPath = {0}, spriteName = {1}！", atlasPath, spriteName);
                callback.Invoke(null);
                return;
            }

            string location = NormalizeToLocation(atlasPath);
            SpriteAtlas atlas = null;
            if (_spriteCache.TryGetValue(location, out atlas))
            {
                callback?.Invoke(atlas.Get(spriteName));
            }
            else
            {
                LoadAssetAsync<UnityEngine.U2D.SpriteAtlas>(location, (obj) =>
                {
                    if (obj == null)
                    {
                        Debug.LogErrorFormat("[LoadSpriteAsync] load failed：atlasPath = {0}！", atlasPath);
                        return;
                    }
                    if (_spriteCache.TryGetValue(location, out atlas))
                    {
                        callback?.Invoke(atlas.Get(spriteName));
                        return;
                    }
                    atlas = new SpriteAtlas { spriteAtlas = obj };
                    _spriteCache.Add(location, atlas);
                    callback?.Invoke(atlas.Get(spriteName));
                });
            }
        }
        #endregion

        #region 场景加载
        public void LoadSceneAsync(string name, LoadSceneMode loadMode = LoadSceneMode.Single, Action<AsyncOperation> callback = null)
        {
            if (YooAssetService.Instance.Package == null)
            {
                callback?.Invoke(null);
                return;
            }

            string location = NormalizeToLocation(name);
            var handle = YooAssetService.Instance.LoadSceneAsync(location, loadMode);
            // SceneHandle 不是 UnityEngine.AsyncOperation，回调仅作完成通知
            handle.Completed += (h) => callback?.Invoke(null);
        }

        public void UnloadSceneAsync(Scene scene, Action callback)
        {
            AsyncOperation op = SceneManager.UnloadSceneAsync(scene);
            if (op != null)
            {
                op.completed += (asyncOp) => callback?.Invoke();
            }
            else
            {
                callback?.Invoke();
            }
        }

        public IEnumerator CoUnloadSceneAsync(Scene scene, Action callback)
        {
            AsyncOperation op = SceneManager.UnloadSceneAsync(scene);
            if (op != null)
            {
                yield return op;
            }
            callback?.Invoke();
        }
        #endregion

        #region Text读取
        public IEnumerator CoReadTextStringAsync(string path, Action<string> callback)
        {
            LoadAssetAsync<UnityEngine.TextAsset>(path, (obj) =>
            {
                if (obj == null)
                {
                    Debug.LogErrorFormat("[ReadTextStreamAsync] load failed：path = {0}！", path);
                    callback?.Invoke(string.Empty);
                    return;
                }

                callback?.Invoke(obj.text);
            });
            yield break;
        }

        public void ReadTextStringAsync(string path, Action<string> callback)
        {
            LoadAssetAsync<UnityEngine.TextAsset>(path, (obj) =>
            {
                if (obj == null)
                {
                    Debug.LogErrorFormat("[ReadTextStreamAsync] load failed：path = {0}！", path);
                    callback?.Invoke(string.Empty);
                    return;
                }

                callback?.Invoke(obj.text);
            });
        }

        public void ReadTextBytesAsync(string path, Action<byte[], object[]> callback, params object[] userData)
        {
            LoadAssetAsync<UnityEngine.TextAsset>(path, (obj) =>
            {
                if (obj == null)
                {
                    Debug.LogErrorFormat("[ReadTextStreamAsync] load failed：path = {0}！", path);
                    callback?.Invoke(null, userData);
                    return;
                }

                callback?.Invoke(obj.bytes, userData);
            }, true);
        }

        public IEnumerator CoReadTextBytesAsync(string path, Action<byte[]> callback)
        {
            LoadAssetAsync<UnityEngine.TextAsset>(path, (obj) =>
            {
                if (obj == null)
                {
                    Debug.LogErrorFormat("[ReadTextStreamAsync] load failed：path = {0}！", path);
                    callback?.Invoke(null);
                    return;
                }

                callback?.Invoke(obj.bytes);
            }, true);
            yield break;
        }

        public byte[] ReadTextBytes(string path)
        {
            if (YooAssetService.Instance.Package == null)
            {
                return null;
            }

            string location = NormalizeToLocation(path);
            var handle = YooAssetService.Instance.LoadAssetSync<TextAsset>(location);
            if (handle.Status == EOperationStatus.Succeeded && handle.AssetObject != null)
            {
                var text = handle.AssetObject as TextAsset;
                byte[] result = text.bytes;
                handle.Release();
                return result;
            }

            if (handle.IsValid) handle.Release();
            Debug.LogErrorFormat("[ReadTextStreamAsync] load failed：path = {0}！", path);
            return null;
        }
        #endregion

        #region Debug
        public void PrintState()
        {
            foreach (var item in _loadedAssetInstanceCountDic)
            {
                Debug.LogFormat("Asset Key: {0}, Count: {1}", item.Key, item.Value);
            }
        }
        #endregion

        #region 路径归一化
        /// <summary>
        /// 统一转成 YooAsset location：
        /// "Assets/HOTS/UI/Prefabs/Mainman.prefab" → "Assets/HOTS/UI/Prefabs/Mainman"
        /// 兼容旧适配版的 "Assets/Resources/xxx" 前缀；其余相对路径原样保留（Addressable 模式下可用）
        /// </summary>
        private static string NormalizeToLocation(string path)
        {
            path = path.Trim();
            const string resPrefix = "Assets/Resources/";
            if (path.StartsWith(resPrefix))
            {
                path = path.Substring(resPrefix.Length);
            }
            int extIndex = path.LastIndexOf('.');
            int slashIndex = path.LastIndexOf('/');
            if (extIndex > slashIndex)
            {
                path = path.Substring(0, extIndex);
            }
            return path;
        }
        #endregion
    }
}

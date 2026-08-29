using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkierFramework
{
    /// <summary>
    /// 资源管理器（适配版：基于 Resources.Load）
    /// 原版基于 Addressables，本项目没有 Addressables，改为 Resources 加载：
    /// - path 一律为 Resources 目录下的相对路径（不含扩展名），如 "UI/Prefabs/LoginView"
    /// - Resources.Load 是同步的，所有回调立即执行（API 形状保持不变）
    /// - 保留引用计数 / 对象池 / 常驻资源 / 缓存语义
    /// - 移除了热更新相关 API（CheckUpdateCor / DownLoadCor / 按 Label 预加载）
    /// </summary>
    public class ResourceManager : Singleton<ResourceManager>
    {
        /// <summary>
        /// 已加载资源路径对应的资源缓存
        /// </summary>
        private Dictionary<string, UnityEngine.Object> _assetCaches = new Dictionary<string, UnityEngine.Object>();
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
        /// 是否正在加载资源（Resources 加载是同步的，恒为 false，保留该字段仅为 API 兼容）
        /// </summary>
        public bool IsProcessLoading
        {
            get => false;
        }

        public override void OnInitialize()
        {
            base.OnInitialize();
            _instancePool = new InstancePool();
        }

        #region 初始化/清除
        /// <summary>
        /// 初始化（Resources 无需初始化，保留该协程仅为 API 兼容）
        /// </summary>
        public IEnumerator InitializeAsync()
        {
            yield break;
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

                _assetCaches.Remove(key);
                _loadedAssetInstanceCountDic.Remove(key);
                _instancePool.Clear(key);
            }
            _clearAssetsSet.Clear();
        }

        /// <summary>
        /// 增加常驻资源
        /// </summary>
        public void AddResidentAsset(string key)
        {
            _residentAssetsHashSet.Add(key);
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

            if (!_assetCaches.ContainsKey(path))
            {
                //未加载过此资源
                if (LoadAsset<GameObject>(path) == null)
                {
                    Debug.LogErrorFormat("[InstantiateAsync] 加载失败：{0}！", path);
                    callback?.Invoke(null);
                    return;
                }
            }
            InternalInstantiate(path, callback, active);
        }

        public IEnumerable CoInstantiateAsync(string path, Action<UnityEngine.GameObject> callback, bool active = true)
        {
            if (!_assetCaches.ContainsKey(path))
            {
                LoadAsset<GameObject>(path);
            }
            InternalInstantiate(path, callback, active);
            yield break;
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
        private void InternalInstantiate(string path, Action<UnityEngine.GameObject> callback, bool active = true)
        {
            GameObject result = _instancePool.Get(path);
            GameObject invokeResult = null;

            if (result == null)
            {
                if (_assetCaches.TryGetValue(path, out UnityEngine.Object asset) && asset != null)
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
                _objectInstanceIdKeyDic[invokeResult.GetEntityId()] = path;
                if (_loadedAssetInstanceCountDic.TryGetValue(path, out int count))
                {
                    _loadedAssetInstanceCountDic[path] = count + 1;
                }
                else
                {
                    _loadedAssetInstanceCountDic[path] = 1;
                }
            }
            callback?.Invoke(invokeResult);
        }
        #endregion

        #region 资源加载/卸载   
        /// <summary>
        /// 加载资源（同步，回调立即执行；已加载则走缓存）
        /// </summary>
        public void LoadAssetAsync<T>(string path, Action<T> onComplete, bool autoUnload = false) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path))
            {
                onComplete?.Invoke(null);
                return;
            }

            T asset = LoadAsset<T>(path);
            if (asset == null)
            {
                Debug.LogErrorFormat("[LoadAssetAsync] {0} 加载失败！", path);
            }
            onComplete?.Invoke(asset);
            if (autoUnload)
            {
                UnLoadAsset(path);
            }
        }

        public void LoadAssetAsync<T, T1>(string path, Action<T, T1> onComplete, T1 data1, bool autoUnload = false) where T : UnityEngine.Object
        {
            LoadAssetAsync<T>(path, (asset) =>
            {
                onComplete?.Invoke(asset, data1);
            }, autoUnload);
        }

        /// <summary>
        /// 同步加载并缓存：Resources.Load
        /// </summary>
        private T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            if (_assetCaches.TryGetValue(path, out UnityEngine.Object cached))
            {
                return cached as T;
            }

            T asset = Resources.Load<T>(NormalizePath(path));
            if (asset != null)
            {
                _assetCaches[path] = asset as UnityEngine.Object;
                if (!_loadedAssetInstanceCountDic.ContainsKey(path))
                {
                    _loadedAssetInstanceCountDic.Add(path, 1);
                }
            }
            return asset;
        }

        /// <summary>
        /// 兼容编辑器写入的完整资源路径："Assets/Resources/UI/Prefabs/Mainman.prefab" -> "UI/Prefabs/Mainman"
        /// </summary>
        private static string NormalizePath(string path)
        {
            const string prefix = "Assets/Resources/";
            if (path.StartsWith(prefix))
            {
                path = path.Substring(prefix.Length);
            }
            int extIndex = path.LastIndexOf('.');
            if (extIndex >= 0)
            {
                path = path.Substring(0, extIndex);
            }
            return path;
        }

        /// <summary>
        /// 直接卸载资源
        /// </summary>
        public void UnLoadAsset(string path)
        {
            //判断卸载是否是一个常驻资源
            if (_residentAssetsHashSet.Contains(path))
            {
                Debug.LogErrorFormat("[UnLoadAsset] 禁止卸载常驻资源：{0} ！", path);
                return;
            }

            if (_assetCaches.ContainsKey(path))
            {
                Debug.Log(string.Format("[UnLoadAsset] 卸载资源：{0} ！", path));

                if (_spriteCache.TryGetValue(path, out SpriteAtlas spriteAtlas))
                {
                    spriteAtlas.Cleanup();
                    _spriteCache.Remove(path);
                }
                _assetCaches.Remove(path);
                _loadedAssetInstanceCountDic.Remove(path);
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
            if (_loadedAssetInstanceCountDic.TryGetValue(path, out int count))
            {
                _loadedAssetInstanceCountDic[path] = --count;
                if (count <= 0)
                {
                    UnLoadAsset(path);
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
            LoadAsset<T>(path);
            yield break;
        }

        public bool TryGetAsset<T>(string path, out T target) where T : UnityEngine.Object
        {
            target = null;
            if (_assetCaches.TryGetValue(path, out UnityEngine.Object cached))
            {
                target = cached as T;
                return target != null;
            }
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

            SpriteAtlas atlas = null;
            if (_spriteCache.TryGetValue(atlasPath, out atlas))
            {
                callback?.Invoke(atlas.Get(spriteName));
            }
            else
            {
                LoadAssetAsync<UnityEngine.U2D.SpriteAtlas>(atlasPath, (obj) =>
                {
                    if (obj == null)
                    {
                        Debug.LogErrorFormat("[LoadSpriteAsync] load failed：atlasPath = {0}！", atlasPath);
                        return;
                    }
                    if (_spriteCache.TryGetValue(atlasPath, out atlas))
                    {
                        callback?.Invoke(atlas.Get(spriteName));
                        return;
                    }
                    atlas = new SpriteAtlas { spriteAtlas = obj };
                    _spriteCache.Add(atlasPath, atlas);
                    callback?.Invoke(atlas.Get(spriteName));
                });
            }
        }
        #endregion

        #region 场景加载
        public void LoadSceneAsync(string name, LoadSceneMode loadMode = LoadSceneMode.Single, Action<AsyncOperation> callback = null)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(name, loadMode);
            if (op != null)
            {
                op.completed += (asyncOp) => callback?.Invoke(asyncOp);
            }
            else
            {
                callback?.Invoke(null);
            }
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
            byte[] result = null;
            LoadAssetAsync<TextAsset>(path,
                (text) => {
                    if (text != null)
                    {
                        result = text.bytes;
                    }
                },
                true);
            return result;
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
    }
}

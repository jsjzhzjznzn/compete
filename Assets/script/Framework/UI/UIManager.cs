using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SkierFramework
{
    public enum UIBlackType
    {
        None,       // 无黑边，全适应
        Height,     // 保持高度填满，两边黑边
        Width,      // 保持宽度填满, 上下黑边
        AutoBlack,  // 自动黑边(选中左右或上下黑边最少的一方)
    }

    public struct UIJumpData
    {
        public UIType curUIType;
        public object curUserData;
        public UIType nextUIType;
        public object nextUserData;
    }

    public class UIManager : SingletonMono<UIManager>
    {
        /// <summary>
        /// 需要修改分辨率 根据实际情况
        /// </summary>
        public int width = 1920;
        public int height = 1080;
        public UIBlackType uiBlackType = UIBlackType.None;

        private Transform _root;
        private Camera _worldCamera;
        private Camera _uiCamera;
        /// <summary>
        /// 屏幕渐变遮罩
        /// </summary>
        private CanvasGroup _blackMask;
        private CanvasGroup _backgroundMask;
        private Tweener _fadeTweener;
        /// <summary>
        /// 黑边
        /// </summary>
        private RectTransform[] _blacks = new RectTransform[2];

        private Dictionary<UIType, UIViewController> _viewControllers;
        private Dictionary<UILayer, UILayerLogic> _layers;
        private HashSet<UIType> _openViews;
        private HashSet<UIType> _residentViews;
        private List<UIJumpData> _uiJumpDatas;
        private bool _isConfigInit;
        /// <summary>
        /// 初始化协程进行中（YooAsset 初始化 + 配置加载是异步的）
        /// </summary>
        private bool _isInitializing;
        /// <summary>
        /// UI 配置是否已真正解析完成（区别于 _isConfigInit 发起即置 true）
        /// </summary>
        private bool _isConfigLoaded;
        /// <summary>
        /// 初始化完成前发起的操作，就绪后按序执行
        /// </summary>
        private readonly List<Action> _pendingActions = new List<Action>();

        public EventSystem EventSystem { get; private set; }
        public EventController<UIEvent> Event { get; private set; }
        public Camera UICamera => _uiCamera;

        // Debug用
        public List<UIJumpData> UIJumpDatas => _uiJumpDatas;

        public void Initialize()
        {
            if (_viewControllers != null) return;

            _layers = new Dictionary<UILayer, UILayerLogic>();
            _viewControllers = new Dictionary<UIType, UIViewController>();
            _openViews = new HashSet<UIType>();
            _residentViews = new HashSet<UIType>();
            _uiJumpDatas = new List<UIJumpData>();
            Event = new EventController<UIEvent>();

            _worldCamera = Camera.main;
            _worldCamera.cullingMask &= int.MaxValue ^ (1 << Layer.UI);

            var root = GameObject.Find("UIRoot");
            if (root == null)
            {
                root = new GameObject("UIRoot");
            }
            root.layer = Layer.UI;
            GameObject.DontDestroyOnLoad(root);
            _root = root.transform;

            var camera = GameObject.Find("UICamera");
            if (camera == null)
            {
                camera = new GameObject("UICamera");
            }
            _uiCamera = camera.GetOrAddComponent<Camera>();
            _uiCamera.cullingMask = 1 << Layer.UI;
            _uiCamera.transform.SetParent(_root);
            _uiCamera.orthographic = true;
            _uiCamera.clearFlags = CameraClearFlags.Depth;
            // URP管线下 需要通过UniversalAdditionalCameraData设置renderType = Overlay，并将该相机加到主相机的Camera Stack中
            var cameraData = _uiCamera.GetOrAddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cameraData.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Overlay;
            if (_worldCamera != null)
            {
                var worldCameraData = _worldCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (worldCameraData != null)
                {
                    worldCameraData.cameraStack.Add(_uiCamera);
                }
            }

            EventSystem = EventSystem.current;

            var layers = Enum.GetValues(typeof(UILayer));
            foreach (UILayer layer in layers)
            {
                bool is3d = layer == UILayer.SceneLayer;
                Canvas layerCanvas = UIExtension.CreateLayerCanvas(layer, is3d, _root, is3d ? _worldCamera : _uiCamera, width, height);
                UILayerLogic uILayerLogic = new UILayerLogic(layer, layerCanvas);
                _layers.Add(layer, uILayerLogic);
            }
            _blackMask = UIExtension.CreateBlackMask(_layers[UILayer.BlackMaskLayer].canvas.transform);
            _backgroundMask = UIExtension.CreateBlackMask(_layers[UILayer.BackgroundLayer].canvas.transform);
        }

        /// <summary>
        /// 创建或者调整黑边，需间隔触发，由于有些设备屏幕是可以转动，是动态的
        /// </summary>
        private void ChangeOrCreateBlack()
        {
            if (_layers == null) return;
            var parent = _layers[UILayer.BackgroundLayer].canvas.transform as RectTransform;
            var uIBlackType = GetUIBlackType();
            switch (uIBlackType)
            {
                case UIBlackType.Height:
                    // 高度适配时的左右黑边
                    var rect = _blacks[0];
                    if (rect == null)
                    {
                        _blacks[0] = rect = UIExtension.CreateBlackMask(parent, 1, "right").transform as RectTransform;
                    }
                    else if (Mathf.Abs(rect.anchoredPosition.x * 2 + parent.rect.width - width) < 1)
                    {
                        return;
                    }
                    rect.pivot = new Vector2(0, 0.5f);
                    rect.anchorMin = new Vector2(1, 0);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.sizeDelta = new Vector2(Mathf.Abs(width - parent.rect.width), 0);
                    rect.anchoredPosition = new Vector2((width - parent.rect.width) / 2, 0);

                    rect = _blacks[1];
                    if (rect == null)
                    {
                        _blacks[1] = rect = UIExtension.CreateBlackMask(parent, 1, "left").transform as RectTransform;
                    }
                    rect.pivot = new Vector2(1, 0.5f);
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.sizeDelta = new Vector2(Mathf.Abs(width - parent.rect.width), 0);
                    rect.anchoredPosition = new Vector2(-(width - parent.rect.width) / 2, 0);
                    break;
                case UIBlackType.Width:
                    // 宽度适配时的上下黑边
                    rect = _blacks[0];
                    if (rect == null)
                    {
                        _blacks[0] = rect = UIExtension.CreateBlackMask(parent, 1, "top").transform as RectTransform;
                    }
                    else if (Mathf.Abs(rect.anchoredPosition.y * 2 + parent.rect.height - height) < 1)
                    {
                        return;
                    }
                    rect.pivot = new Vector2(0.5f, 0);
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.sizeDelta = new Vector2(0, Mathf.Abs(height - parent.rect.height));
                    rect.anchoredPosition = new Vector2(0, (height - parent.rect.height) / 2);

                    rect = _blacks[1];
                    if (rect == null)
                    {
                        _blacks[1] = rect = UIExtension.CreateBlackMask(parent, 1, "bottom").transform as RectTransform;
                    }
                    rect.pivot = new Vector2(0.5f, 1);
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(1, 0);
                    rect.sizeDelta = new Vector2(0, Mathf.Abs(height - parent.rect.height));
                    rect.anchoredPosition = new Vector2(0, -(height - parent.rect.height) / 2);
                    break;
                default:
                    break;
            }
        }

        public UIBlackType GetUIBlackType()
        {
            var uIBlackType = uiBlackType;
            if (uIBlackType == UIBlackType.AutoBlack)
            {
                var parent = _layers[UILayer.BackgroundLayer].canvas.transform as RectTransform;
                float widthDis = Mathf.Abs(width - parent.rect.width);
                float heightDis = Mathf.Abs(height - parent.rect.height);

                if (widthDis < 1 && heightDis < 1)
                    uIBlackType = UIBlackType.None;
                else if (widthDis > heightDis)
                    uIBlackType = UIBlackType.Height;
                else
                    uIBlackType = UIBlackType.Width;
            }
            return uIBlackType;
        }

        public Rect GetSafeArea()
        {
            Rect rect = Screen.safeArea;
            if (uiBlackType == UIBlackType.Width)
            {
                var parent = _layers[UILayer.BackgroundLayer].canvas.transform as RectTransform;
                float blackArea = Mathf.Abs(height - parent.rect.height) / 2;
                rect.yMin = Mathf.Max(0, rect.yMin - blackArea);
                rect.yMax = Mathf.Min(rect.yMax + blackArea, Screen.height);
            }
            else if (uiBlackType == UIBlackType.Height)
            {
                var parent = _layers[UILayer.BackgroundLayer].canvas.transform as RectTransform;
                float blackArea = Mathf.Abs(width - parent.rect.width) / 2;
                rect.xMin = Mathf.Max(0, rect.xMin - blackArea);
                rect.xMax = Mathf.Min(rect.xMax + blackArea, Screen.width);
            }
            return rect;
        }

        public void EnableBackgroundMask(bool enable)
        {
            _backgroundMask.alpha = enable ? 1 : 0;
        }

        public void InitUIConfig(Action onCompleted = null)
        {
            // 重复调用自动跳过
            if (_isConfigInit)
            {
                onCompleted?.Invoke();
                return;
            }
            _isConfigInit = true;

            // 初始化需要加载所有UI的配置
            UIConfig.GetAllConfigs((list) =>
            {
                foreach (var cfg in list)
                {
                    if (_viewControllers.ContainsKey(cfg.uiType))
                    {
                        Debug.LogErrorFormat("存在相同的uiType:{0}， 请检查UIConfig是否重复！", cfg.uiType.ToString());
                        continue;
                    }

                    _viewControllers.Add(cfg.uiType, new UIViewController
                    {
                        uiPath = cfg.path,
                        uiType = cfg.uiType,
                        uiLayer = _layers[cfg.uiLayer],
                        uiViewType = cfg.viewType,
                        isWindow = cfg.isWindow,
                    });
                }
                onCompleted?.Invoke();
            });
        }

        /// <summary>
        /// 注册常驻UI
        /// </summary>
        public void AddResidentUI(UIType type)
        {
            _residentViews.Add(type);
        }

        /// <summary>
        /// 是否初始化完成（YooAsset + UI 配置均就绪）
        /// </summary>
        private bool IsReady => _viewControllers != null && _isConfigLoaded;

        /// <summary>
        /// 就绪则返回 true；否则将操作入队并（首次）启动初始化协程，返回 false
        /// </summary>
        private bool TryEnsureInitialized(Action pendingAction)
        {
            if (IsReady)
            {
                return true;
            }

            if (pendingAction != null)
            {
                _pendingActions.Add(pendingAction);
            }
            if (_isInitializing)
            {
                return false;
            }

            _isInitializing = true;
            StartCoroutine(InitializeCoroutine());
            return false;
        }

        private IEnumerator InitializeCoroutine()
        {
            // 1) YooAsset 初始化（编辑器模拟模式内含 simulate build，可能耗时数秒）
            yield return ResourceManager.Instance.InitializeAsync();
            if (!YooAssetService.Instance.IsInitialized)
            {
                Debug.LogError("[UIManager] YooAsset 初始化失败，UI 系统初始化中止！");
                _isInitializing = false;
                _pendingActions.Clear();
                yield break;
            }

            // 2) UI 根节点/分层
            Initialize();

            // 3) 异步加载 UI 配置
            bool configDone = false;
            InitUIConfig(() => configDone = true);
            while (!configDone)
            {
                yield return null;
            }
            _isConfigLoaded = true;
            _isInitializing = false;

            // 4) 按序执行排队操作
            var actions = _pendingActions.ToArray();
            _pendingActions.Clear();
            foreach (var action in actions)
            {
                action();
            }
        }

        /// <summary>
        /// 开启UI
        /// </summary>
        public void Open(UIType type, object userData = null, Action callback = null)
        {
            if (!TryEnsureInitialized(() => Open(type, userData, callback))) return;

            if (!_viewControllers.ContainsKey(type))
            {
                Debug.LogErrorFormat("未配置uiType:{0}， 请检查UIConfig.cs！", type.ToString());
                return;
            }

            _openViews.Add(type);
            _viewControllers[type].Open(userData, callback);
        }

        /// <summary>
        /// 关闭UI
        /// </summary>
        public void Close(UIType type, Action callback = null, bool isJump = false)
        {
            if (!TryEnsureInitialized(() => Close(type, callback, isJump))) return;

            if (!_viewControllers.ContainsKey(type))
            {
                Debug.LogErrorFormat("未配置uiType:{0}， 请检查UIConfig.cs！", type.ToString());
                return;
            }

            _openViews.Remove(type);
            _viewControllers[type].Close(callback, isJump);
        }

        /// <summary>
        /// UI跳转 
        /// 解决想要有依次打开1->2->3->2，并在关闭2时依次是2->3->2->1的恢复情况
        /// 跳转问题不该由底层的UI遮挡问题来实现，属于两套逻辑
        /// UI遮挡问题的目的：解决底下看不见的UI的重复渲染，不管理其他业务。
        /// 
        /// 逻辑：从 curUI 跳转到 nextUI，如果nextUI被关闭则重新打开curUI
        /// </summary>
        public void JumpUI(UIType curUIType, object curUserData, UIType nextUIType, object nextUserData)
        {
            if (!TryEnsureInitialized(() => JumpUI(curUIType, curUserData, nextUIType, nextUserData))) return;

            if (IsOpen(curUIType))
            {
                int order = _viewControllers[curUIType].order;
                // 由于存在异步，所以必须等他先开启完毕后，再关闭
                Open(nextUIType, nextUserData, () => {
                    if (order == _viewControllers[curUIType].order)
                    {
                        Close(curUIType, null, true);
                    }
                });
            }
            else
            {
                Debug.LogError($"跳转UI从 {curUIType} 跳转到 {nextUIType}时，{curUIType}并没有被开启！");
            }
            _uiJumpDatas.Add(new UIJumpData
            {
                curUIType = curUIType,
                curUserData = curUserData,
                nextUIType = nextUIType,
                nextUserData = nextUserData
            });
        }

        /// <summary>
        /// UI关闭时回调，把跳转之前的UI重新还原。
        /// </summary>
        public void OnUIClose(UIType type)
        {
            if (_uiJumpDatas.Count == 0) return;

            for (int i = _uiJumpDatas.Count - 1; i >= 0; i--)
            {
                if (_uiJumpDatas[i].nextUIType == type)
                {
                    Open(_uiJumpDatas[i].curUIType, _uiJumpDatas[i].curUserData);
                    _uiJumpDatas.RemoveAt(i);
                    break;
                }
            }
        }

        public void Preload(UIType type)
        {
            if (!TryEnsureInitialized(() => Preload(type))) return;

            if (!_viewControllers.TryGetValue(type, out var controller))
            {
                Debug.LogErrorFormat("未配置uiType:{0}， 请检查UIConfig.cs！", type.ToString());
                return;
            }
            controller.Load();
        }

        public void PreloadAll()
        {
            if (!TryEnsureInitialized(PreloadAll)) return;

            foreach (var controller in _viewControllers.Values)
            {
                ResourceManager.Instance.LoadAssetAsync<GameObject>(controller.uiPath, null);
            }
        }

        public bool IsOpen(UIType type)
        {
            if (!IsReady)
            {
                Debug.LogError("[UIManager] 尚未初始化完成，无法查询！");
                return false;
            }
            return _openViews.Contains(type);
        }

        /// <summary>
        /// UI建议都用事件进行交互，最好不使用该接口
        /// </summary>
        public T GetView<T>(UIType type) where T : UIView
        {
            if (!IsReady)
            {
                Debug.LogError("[UIManager] 尚未初始化完成，无法获取视图！");
                return null;
            }

            if (!_viewControllers.ContainsKey(type))
            {
                Debug.LogErrorFormat("未配置uiType:{0}， 请检查UIConfig.cs！", type.ToString());
                return null;
            }

            return _viewControllers[type].uiView as T;
        }

        /// <summary>
        /// 获得已经打开的UI，没开返回空
        /// </summary>
        public UIView GetOpenedView(UIType type)
        {
            if (!IsReady)
            {
                Debug.LogError("[UIManager] 尚未初始化完成，无法获取视图！");
                return null;
            }

            if (_viewControllers.TryGetValue(type, out var viewController))
            {
                if (viewController.uiView != null && viewController.isOpen)
                {
                    return viewController.uiView;
                }
            }
            return null;
        }

        public void CloseAll(UIType ignoreType = UIType.Max, bool closeResidentView = false)
        {
            if (!TryEnsureInitialized(() => CloseAll(ignoreType, closeResidentView))) return;

            _uiJumpDatas.Clear();
            var list = ListPool<UIType>.Get();

            foreach (var uiType in _openViews)
            {
                if (ignoreType == uiType) continue;

                if (closeResidentView || !_residentViews.Contains(uiType))
                {
                    _viewControllers[uiType].Close();
                    list.Add(uiType);
                }
            }
            foreach (var uiType in list)
            {
                _openViews.Remove(uiType);
            }
            ListPool<UIType>.Release(list);
        }

        public void ReleaseAll()
        {
            if (!TryEnsureInitialized(ReleaseAll)) return;

            _uiJumpDatas.Clear();
            foreach (var controller in _viewControllers.Values)
            {
                if (!_residentViews.Contains(controller.uiType))
                {
                    _openViews.Remove(controller.uiType);
                    controller.FullRelease();
                }
            }
        }

        public void FadeIn(float duration = 0.5f, TweenCallback callback = null)
        {
            if (_fadeTweener != null && _fadeTweener.IsPlaying())
                _fadeTweener.Complete();
            _fadeTweener = _blackMask.DOFade(1.0f, duration);
            _fadeTweener.onComplete = callback;
        }

        public void FadeOut(float duration = 0.5f, TweenCallback callback = null)
        {
            if (_fadeTweener != null && _fadeTweener.IsPlaying())
                _fadeTweener.Complete();
            _fadeTweener = _blackMask.DOFade(0.0f, duration);
            _fadeTweener.onComplete = callback;
        }

        public void FadeInOut(float duration = 1.0f, TweenCallback callback = null)
        {
            if (_fadeTweener != null && _fadeTweener.IsPlaying())
                _fadeTweener.Complete();
            _fadeTweener = _blackMask.DOFade(1.0f, duration * 0.5f);
            _fadeTweener.onComplete += () =>
            {
                _fadeTweener = _blackMask.DOFade(0.0f, duration * 0.5f);
                _fadeTweener.onComplete = callback;
            };
        }

        public void Cancel()
        {
            if (!IsReady)
            {
                return;
            }

            if (_layers.TryGetValue(UILayer.NormalLayer, out var layer) && layer.openedViews.Count > 0)
            {
                var viewController = layer.openedViews[layer.openedViews.Count - 1];
                if (viewController.uiView != null)
                {
                    viewController.uiView.OnCancel();
                }
            }
        }

        public Canvas GetLayerCanvas(UILayer layer)
        {
            if (_layers.TryGetValue(layer, out var layerLogic))
                return layerLogic.canvas;
            return null;
        }
    }
}

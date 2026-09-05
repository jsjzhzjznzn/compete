using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkierFramework
{
    public class UIViewController
    {
        // 配置
        /// <summary>
        /// UI 标识（字符串）：C# UI 为 UIType 枚举名，纯 Lua UI 为 json 里的任意字符串
        /// </summary>
        public string uiId;
        public string uiPath;
        public Type uiViewType;
        public UILayerLogic uiLayer;
        public bool isWindow;
        /// <summary>
        /// 纯 Lua UI（LuaOnlyView）的 Lua 模块 require 名，加载实例前注入
        /// </summary>
        public string luaModuleName;

        public UIView uiView;
        public UIViewAnim uiViewAnim;
        public bool isLoading = false;
        public bool isOpen = false;
        public bool isPause = false;
        public int order;
        /// <summary>
        /// 在我上面的界面(非窗口界面)的数量
        /// </summary>
        public int topViewNum;

        public void Load(object userData = null, Action callback = null)
        {
            isLoading = true;

            if (isOpen)
            {
                order = uiLayer.PopOrder(this);
            }
            ResourceManager.Instance.InstantiateAsync(uiPath, (go) =>
            {
                if (go == null)
                {
                    // 资源加载失败，避免后续空引用
                    isLoading = false;
                    Release();
                    callback?.Invoke();
                    return;
                }

                if (!isLoading)
                {
                    // 加载期间面板已关闭：回收实例 + 卸载底层 AssetHandle，防止句柄泄漏
                    ResourceManager.Instance.Recycle(go);
                    ResourceManager.Instance.UnLoadAsset(uiPath);
                    callback?.Invoke();
                    Release();
                    return;
                }

                isLoading = false;
                uiView = (UIView)go.GetOrAddComponent(uiViewType);
                uiViewAnim = go.GetComponent<UIViewAnim>();
                if (uiView is LuaOnlyView luaOnlyView && !string.IsNullOrEmpty(luaModuleName))
                {
                    // 通用壳实例共享同一类型，模块名必须按配置注入后再走 OnInit（内部按模块名 require）
                    luaOnlyView.luaModuleName = luaModuleName;
                }
                uiView.transform.SetParentEx(uiLayer.canvas.transform);
                RectTransform rectTransform = uiView.transform as RectTransform;

                switch (UIManager.Instance.GetUIBlackType())
                {
                    case UIBlackType.None:
                        // 全适配
                        rectTransform.SetAnchor(AnchorPresets.StretchAll);
                        rectTransform.anchoredPosition = Vector2.zero;
                        rectTransform.sizeDelta = Vector2.zero;
                        break;
                    case UIBlackType.Height:
                        // 保持高度填满，两边留黑边
                        rectTransform.SetAnchor(AnchorPresets.VertStretchCenter);
                        rectTransform.anchoredPosition = Vector2.zero;
                        rectTransform.sizeDelta = new Vector2(UIManager.Instance.width, 0);
                        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UIManager.Instance.width);
                        break;
                    case UIBlackType.Width:
                        // 保持宽度填满，上下留黑边
                        rectTransform.SetAnchor(AnchorPresets.HorStretchMiddle);
                        rectTransform.anchoredPosition = Vector2.zero;
                        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, UIManager.Instance.height);
                        rectTransform.sizeDelta = new Vector2(0, UIManager.Instance.height);
                        break;
                }

                uiView.OnInit(go.GetComponent<UIControlData>(), this);
                uiView.transform.SetAsLastSibling();

                if (isOpen)
                {
                    Open(userData, callback, true);
                }
                else
                {
                    Close(callback);
                }
            });
        }

        public void Open(object userData = null, Action callback = null, bool isFirstOpen = false)
        {
            isOpen = true;
            if (isLoading) return;

            if (uiView == null)
            {
                Load(userData, callback);
            }
            else
            {
                // 如果之前打开过，再次掉OpenUI，需要把层级给他拉上来
                // 设计上关于跳转相关的，不应该通过底层的遮挡关系来实现，只允许一个UI同时只能被开启1次。
                if (!isFirstOpen && isOpen && order > 0)
                {
                    TrueClose();
                }

                TrueOpen(userData, callback, isFirstOpen);
                if (uiViewAnim != null)
                {
                    uiViewAnim.Open();
                }
            }
        }

        public void Close(Action callback = null, bool isJump = false)
        {
            isOpen = false;
            if (!isJump)
            {
                UIManager.Instance.OnUIClose(uiId);
            }
            if (isLoading) return;

            if (uiView != null)
            {
                if (uiViewAnim != null)
                {
                    uiViewAnim.Close(() => { TrueClose(callback); });
                }
                else
                {
                    TrueClose(callback);
                }
            }
        }

        public void Release()
        {
            if (uiView != null)
            {
                if (isOpen)
                    TrueClose();
                uiView.OnRelease();
                ResourceManager.Instance.Recycle(uiView.gameObject);
            }
            uiView = null;
            uiViewAnim = null;
            isLoading = false;
            isOpen = false;
            order = 0;
        }

        /// <summary>
        /// 彻底释放：销毁实例 + 卸载底层 AssetHandle（句柄泄漏兜底）
        /// 调用时机：UIManager.ReleaseAll / 非常驻面板 CloseAll 后统一清理
        /// </summary>
        public void FullRelease()
        {
            Release();
            ResourceManager.Instance.UnLoadAsset(uiPath);
        }

        private void TrueOpen(object userData = null, Action callback = null, bool isFirstOpen = false)
        {
            uiLayer.OpenUI(this, isFirstOpen);
            SetVisible(true);
            // 刷新一下显示
            AddTopViewNum(0);
            uiView.OnOpen(userData);
            uiView.OnResume();
            callback?.Invoke();
        }

        private void TrueClose(Action callback = null)
        {
            uiLayer.CloseUI(this);
            // 刷新一下显示
            AddTopViewNum(-100000);
            SetVisible(false);
            uiView.OnPause();
            uiView.OnClose();
            callback?.Invoke();
        }

        public void SetVisible(bool visible)
        {
            if (uiView != null)
            {
                uiView.gameObject.SetActive(visible);
            }
        }

        public void AddTopViewNum(int num)
        {
            topViewNum += num;
            topViewNum = Mathf.Max(0, topViewNum);
            SetVisible(topViewNum <= 0);
        }
    }

}

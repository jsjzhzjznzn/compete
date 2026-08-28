using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkierFramework
{
    public enum UILayer
    {
        SceneLayer = 1000,
        BackgroundLayer = 2000,
        NormalLayer = 3000,
        InfoLayer = 4000,
        TopLayer = 5000,
        TipLayer = 6000,
        BlackMaskLayer = 7000,
    }

    public class UILayerLogic
    {
        public UILayer layer;
        public Canvas canvas;
        private int maxOrder;
        private HashSet<int> orders;
        public List<UIViewController> openedViews;

        public UILayerLogic(UILayer layer, Canvas canvas)
        {
            this.layer = layer;
            this.canvas = canvas;
            maxOrder = (int)layer;
            orders = new HashSet<int>();
            openedViews = new List<UIViewController>();
        }

        public void CloseUI(UIViewController closedUI)
        {
            int order = closedUI.order;
            PushOrder(closedUI);
            closedUI.order = 0;

            if (openedViews.Count > 0)
            {
                var topViewController = openedViews[openedViews.Count - 1];
                // 拿到最上层UI，如果被暂停的话，则恢复，
                // 暂停和恢复不影响其是否被覆盖隐藏，只要不是最上层UI都应该标记暂停状态
                if (topViewController != null && topViewController.isPause)
                {
                    topViewController.isPause = false;
                    if (topViewController.uiView != null)
                    {
                        topViewController.uiView.OnResume();
                    }
                }

                if (!closedUI.isWindow)
                {
                    foreach (var viewController in openedViews)
                    {
                        if (viewController != closedUI
                            && viewController.isOpen
                            && viewController.order < order)
                        {
                            viewController.AddTopViewNum(-1);
                        }
                    }
                }
            }
        }

        public void OpenUI(UIViewController openedUI, bool isFirstOpen)
        {
            if (isFirstOpen)
            {
                // 第一次开启时，由于异步加载原因，在Go没被加载时就先申请了order，因此这时候order不为0，也不需要重新加入栈中
            }
            else
            {
                if (openedUI.order != 0)
                {
                    // 没出其他问题的情况下，说明这个UI之前并没有被关闭过
                    Debug.LogError($"{openedUI.uiType} 已经是打开的了，又被再次打开了，异常情况！！");
                    PushOrder(openedUI);
                }
                openedUI.order = PopOrder(openedUI);
            }

            foreach (var viewController in openedViews)
            {
                if (viewController != openedUI
                    && viewController.isOpen
                    && viewController.order < openedUI.order
                    && viewController.uiView != null)
                {
                    if (!viewController.isPause)
                    {
                        viewController.isPause = true;
                        viewController.uiView.OnPause();
                    }
                    if (!openedUI.isWindow)
                    {
                        viewController.AddTopViewNum(1);
                    }
                }
            }
        }

        /// <summary>
        /// 关闭界面时，归还已分配的层级
        /// </summary>
        public bool PushOrder(UIViewController closedUI)
        {
            int order = closedUI.order;
            if (orders.Remove(order))
            {
                // 重新计算最大值
                maxOrder = (int)layer;
                foreach (var item in orders)
                {
                    maxOrder = Mathf.Max(maxOrder, item);
                }
                openedViews.Remove(closedUI);
                return true;
            }
            return false;
        }

        public int PopOrder(UIViewController openedUI = null)
        {
            maxOrder += 30;
            orders.Add(maxOrder);
            openedViews.Add(openedUI);
            return maxOrder;
        }
    }

}

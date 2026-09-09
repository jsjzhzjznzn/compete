using System;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using TMPro;

namespace SkierFramework
{
    public class Xuanze : UIView
    {
        #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
		[ControlBinding]
		public Button createrroom;
		[ControlBinding]

		public Button exit;
		[ControlBinding]
		public Button jionroom;

		#pragma warning restore 0649
#endregion





        /// <summary>点击“创建房间”生成的联机物体（room 界面 exit 时销毁）</summary>
        public static GameObject OnlineRoomObj;

        public override void OnInit(UIControlData uIControlData, UIViewController controller)
        {
            base.OnInit(uIControlData, controller);
        }

        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            exit.onClick.AddListener(OnExitClicked);
            createrroom.onClick.AddListener(OnCreateRoomClicked);
            jionroom.onClick.AddListener(OnJoinRoomClicked);
        }

        private void OnExitClicked()
        {
            UIManager.Instance.Close("Xuanze");
        }

        private void OnJoinRoomClicked()
        {
            // 加入流程：先打开 jiaru 输入主机 IP，回车后再实例化联机物体并 StartClient
            UIManager.Instance.Open(UIType.jiaru);
        }

        private void OnCreateRoomClicked()
        {
            // 用 Unity 自带 Resources 加载生成联机物体（不走 UI 框架）
            if (OnlineRoomObj == null)
            {
                var prefab = Resources.Load<GameObject>("联机");
                if (prefab != null)
                {
                    OnlineRoomObj = UnityEngine.Object.Instantiate(prefab);
                    UnityEngine.Object.DontDestroyOnLoad(OnlineRoomObj);
                    var networkManager = OnlineRoomObj.GetComponent<Unity.Netcode.NetworkManager>();
                    if (networkManager != null)
                    {
                        networkManager.StartHost();
                    }
                }
            }
            UIManager.Instance.Open(UIType.room);
        }

        public override void OnAddListener()
        {
            base.OnAddListener();
        }

        public override void OnRemoveListener()
        {
            base.OnRemoveListener();
        }

        public override void OnClose()
        {
            exit.onClick.RemoveListener(OnExitClicked);
            createrroom.onClick.RemoveListener(OnCreateRoomClicked);
            jionroom.onClick.RemoveListener(OnJoinRoomClicked);
            base.OnClose();
        }
    }
}

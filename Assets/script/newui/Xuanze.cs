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
		private Button createrroom;
		[ControlBinding]
		private Button exit;
		[ControlBinding]
		private Button jionroom;

		#pragma warning restore 0649
#endregion



        public override void OnInit(UIControlData uIControlData, UIViewController controller)
        {
            base.OnInit(uIControlData, controller);
        }

        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            exit.onClick.AddListener(OnExitClicked);
            createrroom.onClick.AddListener(OnCreateRoomClicked);
        }

        private void OnExitClicked()
        {
            UIManager.Instance.Close("Xuanze");
        }

        private void OnCreateRoomClicked()
        {
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
            base.OnClose();
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using TMPro;

namespace SkierFramework
{
    public class Dating : UIView
    {
        #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
		[ControlBinding]
		public Button compete;
		[ControlBinding]
		public TextMeshProUGUI Textp;
		[ControlBinding]
		public Button activity;
		[ControlBinding]
		public Button friend;
		[ControlBinding]
		public Button setting;
		[ControlBinding]
		public Button role;
		[ControlBinding]
		public Button chalenge;

		#pragma warning restore 0649
#endregion





        public override void OnInit(UIControlData uIControlData, UIViewController controller)
        {
            base.OnInit(uIControlData, controller);
        }

        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            compete.onClick.AddListener(OnCompeteClicked);
        }

        private void OnCompeteClicked()
        {
            UIManager.Instance.Open("Xuanze");
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
            compete.onClick.RemoveListener(OnCompeteClicked);
            base.OnClose();
        }
    }
}

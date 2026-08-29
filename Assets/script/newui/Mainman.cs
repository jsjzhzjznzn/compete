using System;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using TMPro;

namespace SkierFramework
{
    public class Mainman : UIView
    {
        #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
		[ControlBinding]
		private Button kaishi;
		[ControlBinding]
		private Button jieshu;

		#pragma warning restore 0649
#endregion



        public override void OnInit(UIControlData uIControlData, UIViewController controller)
        {
            base.OnInit(uIControlData, controller);
        }

        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
        }

        public override void OnAddListener()
        {
            base.OnAddListener();
            kaishi.onClick.AddListener(OnClickKaishi);
        }

        public override void OnRemoveListener()
        {
            base.OnRemoveListener();
            kaishi.onClick.RemoveListener(OnClickKaishi);
        }

        private void OnClickKaishi()
        {
            UIManager.Instance.Open(UIType.Test1);
        }

        public override void OnClose()
        {
            base.OnClose();
        }
    }
}

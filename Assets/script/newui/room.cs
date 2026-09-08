using System;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using TMPro;

namespace SkierFramework
{
    public class room : UIView
    {
        #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
		[ControlBinding]
		private RawImage role;
		[ControlBinding]
		private Button secect;
		[ControlBinding]
		private Button select;
		[ControlBinding]
		private Button exit;
		[ControlBinding]
		private Button ready;

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
            secect.onClick.AddListener(OnClickSecect);
            select.onClick.AddListener(OnClickSelect);
        }

        public override void OnRemoveListener()
        {
            base.OnRemoveListener();
            secect.onClick.RemoveListener(OnClickSecect);
            select.onClick.RemoveListener(OnClickSelect);
        }

        public override void OnClose()
        {
            base.OnClose();
        }

        private void OnClickSecect()
        {
            UIModelManager.Instance.LoadModelToRawImage(
                "Assets/Resource/人物/Real/le_Size02_Ellen_Ani_Idle (1).prefab", 
                role,
                canDrag: true,
                offset: new Vector3(0, -1f, 0),
                rot: Quaternion.identity,
                scale: Vector3.one,
                isOrth: true,
                orthSizeOrFOV: 1f
            );
        }

        private void OnClickSelect()
        {
            UIModelManager.Instance.LoadModelToRawImage(
                "Assets/Resource/人物/Real/安比test.prefab", 
                role,
                canDrag: true,
                offset: new Vector3(0, -1f, 0),
                rot: Quaternion.identity,
                scale: Vector3.one,
                isOrth: true,
                orthSizeOrFOV: 1f
            );
        }
    }
}

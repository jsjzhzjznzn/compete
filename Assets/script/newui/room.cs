using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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

        private TextMeshProUGUI ipText;

        public override void OnInit(UIControlData uIControlData, UIViewController controller)
        {
            base.OnInit(uIControlData, controller);
            ipText = transform.Find("password").GetComponent<TextMeshProUGUI>();
        }

        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            if (ipText.font == null)
            {
                ipText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
            }
            ipText.text = GetLocalIPv4();
            Debug.Log("[room] 本机IP = " + ipText.text);

            secect.onClick.AddListener(OnClickSecect);
            select.onClick.AddListener(OnClickSelect);
            exit.onClick.AddListener(OnClickExit);
        }

        private static string GetLocalIPv4()
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect("8.8.8.8", 80);
                    var endPoint = socket.LocalEndPoint as IPEndPoint;
                    if (endPoint != null && endPoint.Address != null)
                    {
                        string ip = endPoint.Address.ToString();
                        if (!string.IsNullOrEmpty(ip)) return ip;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[room] 默认路由探测IP失败: " + e.Message);
            }

            string fallback = null;
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                        byte[] b = ua.Address.GetAddressBytes();
                        bool isPrivate = b[0] == 10
                            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                            || (b[0] == 192 && b[1] == 168);
                        if (isPrivate) return ua.Address.ToString();
                        if (fallback == null) fallback = ua.Address.ToString();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[room] 遍历网卡取IP失败: " + e.Message);
            }
            return fallback ?? "";
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
            secect.onClick.RemoveListener(OnClickSecect);
            select.onClick.RemoveListener(OnClickSelect);
            exit.onClick.RemoveListener(OnClickExit);
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

        private void OnClickExit()
        {
            if (Xuanze.OnlineRoomObj != null)
            {
                Destroy(Xuanze.OnlineRoomObj);
                Xuanze.OnlineRoomObj = null;
            }
            UIManager.Instance.Close(UIType.room);
        }
    }
}

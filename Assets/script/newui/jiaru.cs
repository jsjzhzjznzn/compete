using System.Net;
using UnityEngine.UI;
using UnityEngine;
using TMPro;
using Unity.Netcode;
using Netcode.Transports.KCP;

namespace SkierFramework
{
    public class jiaru : UIView
    {
        #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
		[ControlBinding]
		private Button exit;
		[ControlBinding]
		private TMP_InputField shuru; 

		#pragma warning restore 0649
#endregion

        private bool _connecting;

        public override void OnInit(UIControlData uIControlData, UIViewController controller)
        {
            base.OnInit(uIControlData, controller);
        }

        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            exit.onClick.AddListener(OnExitClicked);
            // 输入框按回车（或移动端键盘"完成"）才触发，单纯失焦不触发
            shuru.onSubmit.AddListener(OnIpSubmitted);
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
            shuru.onSubmit.RemoveListener(OnIpSubmitted);
            base.OnClose();
        }

        private void OnExitClicked()
        {
            if (Xuanze.OnlineRoomObj != null)
            {
                Destroy(Xuanze.OnlineRoomObj);
                Xuanze.OnlineRoomObj = null;
            }
            UIManager.Instance.Close(UIType.jiaru);
        }

        private void OnIpSubmitted(string text)
        {
            if (_connecting) return;
            string ip = string.IsNullOrWhiteSpace(text) ? "127.0.0.1" : text.Trim();
            if (!IPAddress.TryParse(ip, out _))
            {
                Debug.LogWarning("[jiaru] 无效的主机 IP: " + ip);
                return;
            }
            _connecting = true;

            if (Xuanze.OnlineRoomObj == null)
            {
                var prefab = Resources.Load<GameObject>("联机");
                if (prefab == null)
                {
                    Debug.LogError("[jiaru] 找不到联机预制体");
                    _connecting = false;
                    return;
                }
                Xuanze.OnlineRoomObj = Instantiate(prefab);
                DontDestroyOnLoad(Xuanze.OnlineRoomObj);
            }

            var networkManager = Xuanze.OnlineRoomObj.GetComponent<NetworkManager>();
            var kcp = Xuanze.OnlineRoomObj.GetComponent<Kcp2KTransport>();
            if (networkManager == null || kcp == null)
            {
                Debug.LogError("[jiaru] 联机物体缺少 NetworkManager / Kcp2KTransport 组件");
                _connecting = false;
                return;
            }

            kcp.host = ip; // 先改连接地址，再 StartClient
            if (!networkManager.StartClient())
            {
                Debug.LogError("[jiaru] StartClient 失败: " + ip);
                Destroy(Xuanze.OnlineRoomObj);
                Xuanze.OnlineRoomObj = null;
                _connecting = false;
                return;
            }

            UIManager.Instance.Close(UIType.jiaru);
            UIManager.Instance.Open(UIType.room);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine.UI;
using UnityEngine;
using TMPro;
using Unity.Collections;
using Unity.Netcode;

namespace SkierFramework
{
    /// <summary>
    /// 房间（大厅）界面：选英雄 -> 准备 -> 双方就绪后服务端自动切战斗场景。
    ///
    /// 本机只操作自己的选择/准备：房主直接调 RoomState 的服务端接口，
    /// 纯客户端把状态用命名消息发给服务端；服务端不把任何人的状态广播给其他人，
    /// 所以双方互相看不到对方的英雄和准备状态（"盲选 + 盲准备"）。
    ///
    /// 数据链路：
    ///   room.cs(本机选择) -> RoomState(服务端记录 座位/CharId/Ready)
    ///   -> 两人都 Ready -> RoomState.LoadScene(战斗场景)
    ///   -> BattleSpawnManager 按座位生成双方英雄
    /// </summary>
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

        // 绑定字段用途（自动生成区勿手改，说明写在这里）：
        //   role   = 展示所选英雄 3D 模型的 RawImage（由 UIModelManager 驱动）
        //   secect = 选艾莲的按钮； select = 选安比的按钮
        //   ready  = 准备 / 取消准备按钮； exit = 退出房间按钮

        // ================= 房间对战逻辑（本机自管，服务端不广播他人状态） =================
        // CharId 必须与战斗场景 BattleSpawnManager.HeroPrefabList 的下标一致：
        // 安比 = 0，艾莲 = 1。若你英雄列表顺序不同，改这两行即可。
        private const int CharIdAnbi = 0;   // select 按钮对应的英雄编号
        private const int CharIdEllen = 1;  // secect 按钮对应的英雄编号

        private TextMeshProUGUI ipText;     // "password"那行文字：显示房主本机 IP，方便加入方输入连接
        private TextMeshProUGUI readyLabel; // ready 按钮上的文字：本机在"准备/取消准备"间切换（服务端不同步，自己反馈用）
        private int _selectedCharId = -1;    // 当前选中的英雄；-1 表示还没选（服务端也会拒绝 -1）
        private bool _isReady;               // 本机是否已准备（纯客户端本地记忆，不代表服务端一定接受了）

        /// <summary>UI 初始化：缓存两个"绑定区没有声明"的文字引用，后续直接用，不用每次 Find</summary>
        public override void OnInit(UIControlData uIControlData, UIViewController controller)
        {
            base.OnInit(uIControlData, controller);
            ipText = transform.Find("password").GetComponent<TextMeshProUGUI>();
            readyLabel = FindButtonLabel(ready);
        }

        /// <summary>每次打开房间界面：显示本机 IP、给四个按钮挂监听、刷新准备按钮初始状态</summary>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            // 该 UI 有自己的字体加载兜底（老工程字体资源可能没进包，运行时手动补一次）
            if (ipText.font == null)
            {
                ipText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
            }
            // 房主把自己的局域网 IP 显示出来，加入方照着填到 jiaru 的输入框
            ipText.text = GetLocalIPv4();
            Debug.Log("[room] 本机IP = " + ipText.text);

            secect.onClick.AddListener(OnClickSecect);   // 选艾莲
            select.onClick.AddListener(OnClickSelect);   // 选安比
            exit.onClick.AddListener(OnClickExit);
            ready.onClick.AddListener(OnClickReady);
            RefreshReadyUI();
        }

        /// <summary>探测本机局域网 IP：优先走"默认路由"（能出去的那张网卡），拿不到再遍历所有内网 IPv4</summary>
        private static string GetLocalIPv4()
        {
            // 办法一：UDP 连一个公网地址但不发包，让系统选默认路由，再从 LocalEndPoint 取本机 IP
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

            // 办法二（兜底）：遍历所有网卡，优先返回第一个内网地址(10/172.16-31/192.168)
            string fallback = null;
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue; // 只取启用中的网卡
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue; // 跳过回环

                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue; // 只要 IPv4

                        byte[] b = ua.Address.GetAddressBytes();
                        // 判断是否为私网地址（10.x / 172.16~31.x / 192.168.x）
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

        /// <summary>界面关闭时摘掉所有监听，避免下次打开重复 Add 造成"点一次触发多次"</summary>
        public override void OnClose()
        {
            secect.onClick.RemoveListener(OnClickSecect);
            select.onClick.RemoveListener(OnClickSelect);
            exit.onClick.RemoveListener(OnClickExit);
            ready.onClick.RemoveListener(OnClickReady);
            base.OnClose();
        }

        // ================= 选英雄（预览 + 记录 CharId） =================

        /// <summary>选艾莲：记录 CharId=1 并重置本地准备状态，再加载 3D 预览</summary>
        private void OnClickSecect()
        {
            _selectedCharId = CharIdEllen;
            _isReady = false;   // 换人后之前的准备作废，需重新点准备
            RefreshReadyUI();
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

        /// <summary>选安比：记录 CharId=0 并重置本地准备状态，再加载 3D 预览</summary>
        private void OnClickSelect()
        {
            _selectedCharId = CharIdAnbi;
            _isReady = false;
            RefreshReadyUI();
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

        // ================= 准备 / 取消准备 =================

        /// <summary>
        /// 准备按钮总入口：已准备 -> 取消准备；未准备 -> 校验"选过英雄 + 已连网"后提交准备。
        /// 只上报给服务端，不读取任何对手信息。
        /// </summary>
        private void OnClickReady()
        {
            if (_isReady)
            {
                SubmitCancelReady();
                return;
            }

            if (_selectedCharId < 0)
            {
                Debug.LogWarning("[room] 请先选英雄再准备");
                return;
            }

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsConnectedClient)
            {
                Debug.LogWarning("[room] 还没连上网络，无法准备");
                return;
            }

            if (nm.IsServer)
            {
                // 房主就是服务端：直接写本机状态，避免命名消息"发给自己"的回环问题
                if (RoomState.Instance == null)
                {
                    Debug.LogError("[room] 联机预制体上没挂 RoomState 组件");
                    return;
                }
                RoomState.Instance.ServerSetReady(nm.LocalClientId, _selectedCharId);
            }
            else
            {
                // 纯客户端：命名消息发给服务端
                SendSetReadyToServer(_selectedCharId);
            }

            _isReady = true;    // 本地先记为已准备（服务端不确认不广播，只能本地反馈）
            RefreshReadyUI();
        }

        /// <summary>取消准备：房主直调服务端 / 客户端发命名消息；无论有没有网都把本地状态复位</summary>
        private void SubmitCancelReady()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsConnectedClient)
            {
                if (nm.IsServer)
                {
                    if (RoomState.Instance != null)
                        RoomState.Instance.ServerCancelReady(nm.LocalClientId);
                }
                else
                {
                    SendCancelReadyToServer();
                }
            }

            _isReady = false;
            RefreshReadyUI();
        }

        /// <summary>
        /// 客户端上报"准备 + 所选英雄"。payload 只带一个 int(CharId)，
        /// 命名消息只发给 ServerClientId（服务端），别的客户端收不到 -> 天然满足"互不可见"。
        /// </summary>
        private static void SendSetReadyToServer(int charId)
        {
            var nm = NetworkManager.Singleton;
            using var writer = new FastBufferWriter(4, Allocator.Temp); // int 预留 4 字节
            writer.WriteValueSafe(charId);
            nm.CustomMessagingManager.SendNamedMessage(RoomState.MsgSetReady, NetworkManager.ServerClientId, writer);
        }

        /// <summary>客户端上报"取消准备"，无 payload</summary>
        private static void SendCancelReadyToServer()
        {
            var nm = NetworkManager.Singleton;
            using var writer = new FastBufferWriter(0, Allocator.Temp);
            nm.CustomMessagingManager.SendNamedMessage(RoomState.MsgCancelReady, NetworkManager.ServerClientId, writer);
        }

        /// <summary>根据本机状态刷新准备按钮：文字在"准备/取消准备"间切换；没选英雄时置灰不可点</summary>
        private void RefreshReadyUI()
        {
            if (readyLabel != null)
                readyLabel.text = _isReady ? "取消准备" : "准备";
            if (ready != null)
                ready.interactable = _selectedCharId >= 0; // 没选英雄不能点准备
        }

        /// <summary>从按钮下找那行文字：优先取直接子节点 "Text (TMP)"，找不到再全子树找第一个 TMP 文本</summary>
        private static TextMeshProUGUI FindButtonLabel(Button button)
        {
            var child = button.transform.Find("Text (TMP)");
            if (child != null)
            {
                var label = child.GetComponent<TextMeshProUGUI>();
                if (label != null) return label;
            }
            return button.GetComponentInChildren<TextMeshProUGUI>();
        }

        /// <summary>退出房间：销毁联机物体（断开网络/清掉 DDOL 的 NetworkManager），再关界面</summary>
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

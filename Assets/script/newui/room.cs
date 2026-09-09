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
using DG.Tweening;

namespace SkierFramework
{
    /// <summary>
    /// 房间（大厅）界面：选英雄 -> 准备 -> 双方就绪后服务端自动切战斗场景。
    ///
    /// rolezhu   = 本机所选英雄的 3D 预览（点选瞬间本地加载）
    /// roleenemy = 对手所选英雄的 3D 预览（实时同步：对手换人，这里跟着换）
    ///
    /// 数据链路：
    ///   本机点选英雄 -> rolezhu 本地预览 + 上报服务端(SelectChar/房主直调)
    ///   -> 服务端记录并转发对手(PeerSelectChar) / 房主本机走事件
    ///   -> 双方 roleenemy 实时刷新对手英雄
    ///   准备流程不变：Ready 上报 -> 两人都 Ready -> 服务端 LoadScene -> BattleSpawnManager 生成
    /// </summary>
    public class room : UIView
    {
        #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
		[ControlBinding]
		public RawImage rolezhu;
		[ControlBinding]
		public RawImage roleenemy;
		[ControlBinding]
		public Button shayu;
		[ControlBinding]
		public Button anbi;
		[ControlBinding]
		public Button exit;
		[ControlBinding]
		public Button ready;

		#pragma warning restore 0649
#endregion

        // 绑定字段用途（自动生成区勿手改，说明写在这里）：
        //   rolezhu   = 我方所选英雄预览； roleenemy = 对手所选英雄预览（实时同步）
        //   shayu  = 选艾莲的按钮； anbi = 选安比的按钮
        //   ready  = 准备 / 取消准备按钮； exit = 退出房间按钮

        // ================= 英雄编号 & 模型路径（两处必须对应同一英雄） =================
        // CharId 必须与战斗场景 BattleSpawnManager.HeroPrefabList 的下标一致：
        // 安比 = 0，艾莲 = 1。若你英雄列表顺序不同，改这几行即可。
        private const int CharIdAnbi = 0;   // anbi 按钮对应的英雄编号
        private const int CharIdEllen = 1;  // shayu 按钮对应的英雄编号
        private const string ModelPathAnbi = "Assets/Resource/人物/Real/安比test.prefab";
        private const string ModelPathEllen = "Assets/Resource/人物/Real/le_Size02_Ellen_Ani_Idle (1).prefab";

        private TextMeshProUGUI ipText;     // "password"那行文字：显示房主本机 IP，方便加入方输入连接
        private TextMeshProUGUI readyLabel; // ready 按钮上的文字：本机在"准备/取消准备"间切换（服务端不同步，自己反馈用）
        private int _selectedCharId = -1;    // 当前选中的英雄；-1 表示还没选（服务端也会拒绝 -1）
        private bool _isReady;               // 本机是否已准备（纯客户端本地记忆，不代表服务端一定接受了）

        // ================= 飘字消息模板（Roommessage） =================
        // Roommessage（TMP）只当样式模板用，常驻隐藏、不直接显示；
        // 每次要提示时克隆一条挂到它的父节点下，向上飘 2 秒后直接销毁。
        private const string MessageTemplateName = "Roommessage";
        private RectTransform _messageTemplate; // 模板的 RectTransform（首次用时查找并缓存）

        /// <summary>UI 初始化：缓存文字引用；隐藏飘字模板（它只当样式用）</summary>
        public override void OnInit(UIControlData uIControlData, UIViewController controller)
        {
            base.OnInit(uIControlData, controller);
            ipText = transform.Find("password").GetComponent<TextMeshProUGUI>();
            readyLabel = FindButtonLabel(ready);

            // 找到 Roommessage 模板并隐藏（找不到不报死，等第一次 ShowMessage 时再提示）
            var templateTf = FindChildByName(transform, MessageTemplateName);
            if (templateTf != null)
            {
                _messageTemplate = templateTf.GetComponent<RectTransform>();
                _messageTemplate.gameObject.SetActive(false); // 模板只当样式，不直接显示
            }
        }

        /// <summary>每次打开房间界面：复位本局状态、显示本机 IP、挂监听、拉一次对手当前选择、刷新准备按钮</summary>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            // ===== 复位本局状态（界面是缓存复用的，不复位会残留上一次的选择/准备） =====
            // 退出 room 会销毁联机预制体（网络会话已重建），服务端状态本来就是新的，本地必须同步归零
            _selectedCharId = -1;
            _isReady = false;
            if (rolezhu != null) UIModelManager.Instance.UnLoadModelByRawImage(rolezhu);     // 清掉上次的英雄预览
            if (roleenemy != null) UIModelManager.Instance.UnLoadModelByRawImage(roleenemy); // 清掉上次的对手预览

            // 该 UI 有自己的字体加载兜底（老工程字体资源可能没进包，运行时手动补一次）
            if (ipText.font == null)
            {
                ipText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
            }
            // 房主把自己的局域网 IP 显示出来，加入方照着填到 jiaru 的输入框
            ipText.text = GetLocalIPv4();
            Debug.Log("[room] 本机IP = " + ipText.text);

            shayu.onClick.AddListener(OnClickShayu);   // 选艾莲
            anbi.onClick.AddListener(OnClickAnbi);     // 选安比
            exit.onClick.AddListener(OnClickExit);
            ready.onClick.AddListener(OnClickReady);

            // 订阅"对手选择变化"事件（房主：服务端记录时触发；客户端：收到转发消息时触发）
            if (RoomState.Instance != null)
                RoomState.Instance.OnPeerCharIdChanged += OnPeerCharIdChanged;
            else
                Debug.LogError("[room] 联机预制体上没挂 RoomState 组件，对手英雄无法同步");

            // 监听掉线：房主退出/掉线时，客户端要自动关闭房间界面
            var nmOpen = NetworkManager.Singleton;
            if (nmOpen != null)
                nmOpen.OnClientDisconnectCallback += OnClientDisconnected;

            // 打开界面时拉一次对手当前选择（防止"对方先选、我后开界面"漏消息）
            RequestPeerChar();

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

        /// <summary>
        /// 界面关闭时摘掉所有监听，并统一处理联机物体的销毁：
        /// 只要退出 room 界面就销毁联机预制体（断开网络），唯一例外 = 正在跳转战斗场景（保留连接）。
        /// </summary>
        public override void OnClose()
        {
            shayu.onClick.RemoveListener(OnClickShayu);
            anbi.onClick.RemoveListener(OnClickAnbi);
            exit.onClick.RemoveListener(OnClickExit);
            ready.onClick.RemoveListener(OnClickReady);
            if (RoomState.Instance != null)
                RoomState.Instance.OnPeerCharIdChanged -= OnPeerCharIdChanged;
            var nmClose = NetworkManager.Singleton;
            if (nmClose != null)
                nmClose.OnClientDisconnectCallback -= OnClientDisconnected;

            // ===== 关界面即销毁联机预制体（跳转战斗场景除外） =====
            bool battleTransition = RoomState.Instance != null && RoomState.Instance.IsLoadingBattleScene;
            if (!battleTransition && Xuanze.OnlineRoomObj != null)
            {
                Destroy(Xuanze.OnlineRoomObj);
                Xuanze.OnlineRoomObj = null;
            }

            base.OnClose();
        }

        /// <summary>
        /// 掉线回调（仅客户端关心）：房主退出/掉线 = 服务端断开 -> 自动关闭房间界面，
        /// 并清掉本机的联机物体（NetworkManager 会随断线自动关停，物体留着会污染下一局）。
        /// </summary>
        private void OnClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer) return;                       // 房主自己退出走 OnClickExit，不在这处理
            if (clientId != NetworkManager.ServerClientId) return;       // 只关心"服务端(房主)断开"

            Debug.Log("[room] 房主已退出，自动关闭房间界面");
            if (Xuanze.OnlineRoomObj != null)
            {
                Destroy(Xuanze.OnlineRoomObj);
                Xuanze.OnlineRoomObj = null;
            }
            UIManager.Instance.Close(UIType.room);
        }

        // ================= 选英雄（预览 + 记录 CharId + 实时上报对手） =================

        /// <summary>选艾莲：rolezhu 本地预览 + 记录 CharId + 上报服务端（服务端转发给对手）</summary>
        private void OnClickShayu()
        {
            _selectedCharId = CharIdEllen;
            _isReady = false;   // 换人后之前的准备作废，需重新点准备
            RefreshReadyUI();
            LoadHeroPreview(rolezhu, _selectedCharId);
            SubmitSelectChar(_selectedCharId);
        }

        /// <summary>选安比：rolezhu 本地预览 + 记录 CharId + 上报服务端（服务端转发给对手）</summary>
        private void OnClickAnbi()
        {
            _selectedCharId = CharIdAnbi;
            _isReady = false;
            RefreshReadyUI();
            LoadHeroPreview(rolezhu, _selectedCharId);
            SubmitSelectChar(_selectedCharId);
        }

        /// <summary>charId -> 模型路径（选人预览和对手预览共用）</summary>
        private static string GetModelPath(int charId)
        {
            return charId == CharIdAnbi ? ModelPathAnbi : ModelPathEllen;
        }

        /// <summary>把英雄模型加载到指定 RawImage 上做 3D 预览（LoadModelToRawImage 会自动卸掉该图上的旧模型）</summary>
        private void LoadHeroPreview(RawImage target, int charId)
        {
            UIModelManager.Instance.LoadModelToRawImage(
                GetModelPath(charId),
                target,
                canDrag: true,
                offset: new Vector3(0, -1f, 0),
                rot: Quaternion.identity,
                scale: Vector3.one,
                isOrth: true,
                orthSizeOrFOV: 1f
            );
        }

        // ================= 飘字消息（克隆 Roommessage 模板 + DOTween） =================

        /// <summary>
        /// 弹一条飘字通知：克隆隐藏的 Roommessage 模板 -> 挂到模板父节点下 -> 向上飘 2 秒 -> 直接销毁。
        /// 多条消息可同时存在，互不影响。
        /// </summary>
        private void ShowMessage(string text)
        {
            // 模板没找到就再找一次（初始化时可能还没实例化好）
            if (_messageTemplate == null)
            {
                var templateTf = FindChildByName(transform, MessageTemplateName);
                if (templateTf != null)
                    _messageTemplate = templateTf.GetComponent<RectTransform>();
            }
            if (_messageTemplate == null)
            {
                Debug.LogError($"[room] 找不到飘字模板 {MessageTemplateName}，提示未显示: {text}");
                return;
            }

            // 1. 克隆模板，挂到模板的父节点下（与模板同级，继承布局层级）
            var toast = Instantiate(_messageTemplate, _messageTemplate.parent);
            toast.name = "Roommessage_" + text;
            toast.gameObject.SetActive(true); // 克隆自隐藏模板，需手动激活
            toast.SetAsLastSibling();         // 置顶显示，避免被背景盖住

            // 2. 填文案 + 字体兜底（工程有过 TMP 字体丢失问题，没字体会显示成"看不见的空白"）
            //    TMP 可能挂在 Roommessage 自己身上，也可能在它子级，两级都找
            var tmp = toast.GetComponent<TextMeshProUGUI>()
                      ?? toast.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp == null)
            {
                Debug.LogError("[room] 飘字模板上没有 TextMeshProUGUI 组件，提示未显示: " + text);
                Destroy(toast.gameObject);
                return;
            }
            tmp.text = text;
            if (tmp.font == null)
                tmp.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");

            // 3. 从模板的原始位置开始飘
            Vector2 startPos = _messageTemplate.anchoredPosition;
            toast.anchoredPosition = startPos;

            // 4. DOTween：向上飘 2 秒后直接销毁（SetLink 保证界面中途销毁时 tween 一起停）
            toast.DOAnchorPosY(startPos.y + 120f, 2f)
                 .SetEase(Ease.OutQuad)
                 .SetLink(toast.gameObject)
                 .OnComplete(() => Destroy(toast.gameObject));

            Debug.Log($"[room] 飘字: {text} pos={startPos} parent={toast.parent?.name} " +
                      $"size={toast.rect.size} active={toast.gameObject.activeInHierarchy}");
        }

        /// <summary>在层级树里按名字递归查找子物体（直接子节点找不到就继续往下找）</summary>
        private static Transform FindChildByName(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name) return child;
                var hit = FindChildByName(child, name);
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>上报英雄选择：房主直调服务端 / 客户端发命名消息（点选瞬间就发，不等准备）</summary>
        private void SubmitSelectChar(int charId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsConnectedClient)
            {
                ShowMessage("网络未连接");   // 选择没同步出去时飘字提示
                return;
            }

            if (nm.IsServer)
            {
                if (RoomState.Instance != null)
                    RoomState.Instance.ServerSetCharId(nm.LocalClientId, charId);
            }
            else
            {
                using var writer = new FastBufferWriter(4, Allocator.Temp);
                writer.WriteValueSafe(charId);
                nm.CustomMessagingManager.SendNamedMessage(RoomState.MsgSelectChar, NetworkManager.ServerClientId, writer);
            }
        }

        // ================= 对手英雄实时同步（roleenemy） =================

        /// <summary>
        /// 对手英雄变化回调（RoomState 事件）：
        /// clientId 是自己 -> 忽略（rolezhu 已在点击时本地处理）；
        /// 否则刷新 roleenemy（charId=-1 表示对手未选/清空）。
        /// </summary>
        private void OnPeerCharIdChanged(ulong clientId, int charId)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && clientId == nm.LocalClientId) return;
            UpdateEnemyPreview(charId);
        }

        /// <summary>roleenemy 显示对手英雄；-1 时清空</summary>
        private void UpdateEnemyPreview(int charId)
        {
            if (roleenemy == null) return;
            if (charId < 0)
            {
                UIModelManager.Instance.UnLoadModelByRawImage(roleenemy);
                return;
            }
            LoadHeroPreview(roleenemy, charId);
        }

        /// <summary>拉取对手当前选择：房主直接查服务端状态；客户端发 QueryPeerChar 消息让服务端回推</summary>
        private void RequestPeerChar()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsConnectedClient) return;

            if (nm.IsServer)
            {
                if (RoomState.Instance != null)
                    UpdateEnemyPreview(RoomState.Instance.GetPeerCharId(nm.LocalClientId));
            }
            else
            {
                using var writer = new FastBufferWriter(0, Allocator.Temp);
                nm.CustomMessagingManager.SendNamedMessage(RoomState.MsgQueryPeerChar, NetworkManager.ServerClientId, writer);
            }
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
                ShowMessage("请先选择角色");   // 未选英雄不能准备，飘字提示
                return;
            }

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsConnectedClient)
            {
                ShowMessage("网络未连接，无法准备");
                return;
            }

            if (nm.IsServer)
            {
                // 房主就是服务端：直接写本机状态，避免消息回环
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
        /// 命名消息只发给 ServerClientId（服务端），别的客户端收不到。
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

        /// <summary>根据本机状态刷新准备按钮：文字在"准备/取消准备"间切换。
        /// 按钮保持始终可点：未选人时点了会飘字提示原因（如果禁用按钮，点击事件不触发，飘字就永远出不来了）</summary>
        private void RefreshReadyUI()
        {
            if (readyLabel != null)
                readyLabel.text = _isReady ? "取消准备" : "准备";
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

        /// <summary>退出房间：只关界面；联机预制体的销毁统一由 OnClose 处理（跳转战斗场景除外）</summary>
        private void OnClickExit()
        {
            UIManager.Instance.Close(UIType.room);
        }
    }
}

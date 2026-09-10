using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 服务端房间裁判 / 房间状态（手动挂载：拖到"联机"预制体根节点上，与 NetworkManager 同物体，
/// 必须在 StartHost 之前就存在 —— 因为 Awake 里就要订阅 NetworkManager 事件）。
///
/// 为什么需要它：
///   1. 客户端之间互不可见 -> 只有服务端能汇总"谁选了谁、谁准备好了"
///   2. NGO 的 PlayerObject 跨场景存活不可靠 -> 把状态放在这个 DDOL(联机物体) 上，
///      切战斗场景时它随 NetworkManager 一起活下来，数据不丢
///
/// 数据流：
///   客户端 room.cs 准备 -> 命名消息(或房主直调) -> ServerSetReady 改字典
///   -> CheckStartGame 发现 2 人都 Ready -> LoadScene(战斗场景)
///   -> 战斗场景 BattleSpawnManager 调 GetBattlePlayers() 拿快照生成英雄
///
/// 注意：不要用会自动创建空物体的单例基类 —— 本组件依赖同物体上的 NetworkManager，
/// 忘挂时宁可报错提示，也不要悄悄生成一个无 NetworkManager 的空单例。
/// </summary>
[DisallowMultipleComponent]
public class RoomState : MonoBehaviour
{
    /// <summary>客户端"准备"命名消息名；payload = int charId</summary>
    public const string MsgSetReady = "RoomState.SetReady";
    /// <summary>客户端"取消准备"命名消息名；无 payload</summary>
    public const string MsgCancelReady = "RoomState.CancelReady";
    /// <summary>客户端上报"我选了英雄"命名消息名；payload = int charId（点选瞬间就发，不等准备）</summary>
    public const string MsgSelectChar = "RoomState.SelectChar";
    /// <summary>服务端转发"对手选了英雄"命名消息名；payload = int charId（-1 = 清空）</summary>
    public const string MsgPeerSelectChar = "RoomState.PeerSelectChar";
    /// <summary>客户端打开房间界面时拉取对手当前选择；无 payload</summary>
    public const string MsgQueryPeerChar = "RoomState.QueryPeerChar";
    /// <summary>服务端推送"对手的准备状态"命名消息；payload = byte（1=已准备 0=未准备）</summary>
    public const string MsgPeerReady = "RoomState.PeerReady";

    /// <summary>唯一实例：由 Awake 在挂载的物体上赋值；没挂载时为 null（调用处需判空）</summary>
    public static RoomState Instance { get; private set; }

    /// <summary>
    /// 是否正在跳转战斗场景（LoadScene 已发起）。
    /// room.cs 关界面时据此决定要不要销毁联机预制体：跳转战斗时保留，其余关闭一律销毁。
    /// </summary>
    public bool IsLoadingBattleScene => _sceneLoading;

    /// <summary>
    /// 某玩家的英雄选择变化（clientId=选择归属者，charId=-1 表示未选/清空）。
    /// 房主端：服务端记录选择时直接触发；纯客户端：收到 PeerSelectChar 消息时触发。
    /// room.cs 用它刷新 roleenemy（clientId 等于本机时忽略，rolezhu 由点击本地处理）。
    /// </summary>
    public event Action<ulong, int> OnPeerCharIdChanged;

    /// <summary>
    /// 某玩家的准备状态变化（clientId=状态归属者，isReady=是否已准备）。
    /// 触发时机与服务端广播 PeerReady 相同（准备/取消/换人清准备）。
    /// room.cs 用它刷新 prepareenemy（clientId 等于本机时忽略，preparezhu 由本地点准备时本地处理）。
    /// </summary>
    public event Action<ulong, bool> OnPeerReadyChanged;

    [Header("服务端配置")]
    [Tooltip("对战人数（双方=2），全部准备才开局")]
    public int RequiredPlayerCount = 2;
    [Tooltip("英雄总数，服务端用它校验客户端上报的 CharId")]
    public int HeroCount = 2;
    [Tooltip("开始战斗后加载的场景名，需加入 Build Settings")]
    public string BattleSceneName = "multiplecompete";

    /// <summary>某玩家在房间里的状态（只存服务端字典里，不发给任何人）</summary>
    public struct PlayerState
    {
        public bool IsReady; // 是否点了准备
        public int CharId;   // 选中的英雄（-1 表示还没选）
    }

    /// <summary>战斗场景生成用：按座位排序的玩家快照（仅服务端调用）</summary>
    public struct BattlePlayerInfo
    {
        public ulong ClientId; // 玩家网络 ID
        public int Seat;       // 座位：0=先入座的(通常是房主)，1=后入座的；决定出生点
        public int CharId;     // 英雄编号：决定生成哪个英雄预制体
    }

    /// <summary>房间成员状态表：clientId -> (是否准备, 英雄编号)</summary>
    private readonly Dictionary<ulong, PlayerState> _players = new Dictionary<ulong, PlayerState>();
    /// <summary>座位表：clientId -> 座位号（0/1），先来先坐，用于战斗出生点分配</summary>
    private readonly Dictionary<ulong, int> _seats = new Dictionary<ulong, int>();
    /// <summary>场景是否已在加载中，防止两人同时触发/重复触发 LoadScene</summary>
    private bool _sceneLoading;
    /// <summary>命名消息是否已注册（RegisterNamedMessageHandler 会覆盖同名旧handler，所以只注册一次即可）</summary>
    private bool _handlersRegistered;
    /// <summary>纯客户端是否已注册"对手选择"接收（连接成功后注册一次）</summary>
    private bool _clientHandlersRegistered;
    /// <summary>缓存本物体上的 NetworkManager 引用，避免到处 Singleton 查找</summary>
    private NetworkManager _nm;

    private void Awake()
    {
        // 单例去重：只移除重复组件不销毁整个物体，防止把同物体上的 NetworkManager 一起删掉
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // 本组件必须和 NetworkManager 同物体（联机预制体根节点）
        _nm = GetComponent<NetworkManager>();
        if (_nm == null) _nm = NetworkManager.Singleton;
        if (_nm == null)
        {
            Debug.LogError("[RoomState] 必须挂在带 NetworkManager 的物体上（联机预制体根节点）");
            return;
        }

        // Awake 就订阅，保证 Xuanze 调 StartHost() 之前就已经在监听，
        // 这样房主自己(host)的连接回调也不会漏掉
        _nm.OnClientConnectedCallback += OnClientConnected;
        _nm.OnClientDisconnectCallback += OnClientDisconnected;
        _nm.OnServerStarted += OnServerStarted;
    }

    private void Start()
    {
        // 兜底：万一房间是在"服务端已启动"之后才创建的，也能立刻注册消息处理
        if (_nm == null) _nm = NetworkManager.Singleton;
        if (_nm != null && _nm.IsServer)
        {
            RegisterMessageHandlers();
            RegisterExistingPlayers();   // 自愈：把已在房间里的玩家(含房主自己)补登记，防止漏登记导致只生成一人
        }
        else if (_nm != null && _nm.IsClient && _nm.IsConnectedClient)
        {
            RegisterClientHandlers();    // 客户端兜底：已连上才走到 Start 的情况
        }
    }

    private void OnDestroy()
    {
        // 与 Awake 成对退订，防止对象销毁后事件还指向已释放的实例
        if (_nm != null)
        {
            _nm.OnClientConnectedCallback -= OnClientConnected;
            _nm.OnClientDisconnectCallback -= OnClientDisconnected;
            _nm.OnServerStarted -= OnServerStarted;
        }
        if (Instance == this) Instance = null;
    }

    /// <summary>判断当前进程是不是服务端（host 也满足）；_nm 可能为空所以要兜底</summary>
    private bool IsServerNow()
    {
        return _nm != null && _nm.IsServer;
    }

    // ---------------- 玩家进出 ----------------

    /// <summary>NetworkManager 进入服务端状态时触发（StartHost/StartServer 成功时），此刻才能注册命名消息</summary>
    private void OnServerStarted()
    {
        RegisterMessageHandlers();
        RegisterExistingPlayers(); // 服务端刚起来时也补登记一次存量玩家
    }

    /// <summary>把已在房间里的所有客户端补登记一遍（防连接回调比 Awake 订阅更早/漏触发）</summary>
    private void RegisterExistingPlayers()
    {
        if (!IsServerNow()) return;
        var ids = new List<ulong>(_nm.ConnectedClients.Keys);
        foreach (var clientId in ids)
            TryRegisterPlayer(clientId);
    }

    /// <summary>玩家进入房间（连接成功）：服务端登记 + 发座位；客户端则注册"对手选择"接收</summary>
    private void OnClientConnected(ulong clientId)
    {
        if (IsServerNow())
        {
            TryRegisterPlayer(clientId);
            return;
        }
        // 纯客户端：自己连上后注册 PeerSelectChar 接收（服务端随时可能转发对手的选择）
        RegisterClientHandlers();
    }

    private void TryRegisterPlayer(ulong clientId)
    {
        if (_players.ContainsKey(clientId)) return; // 防止重复登记

        // 超过对战人数后进来的按旁观处理：不登记，它的准备消息会被 ServerSetReady 拒掉
        if (_players.Count >= RequiredPlayerCount)
        {
            Debug.LogWarning($"[RoomState] 房间已满({RequiredPlayerCount}人)，{clientId} 按旁观处理");
            return;
        }

        _players[clientId] = new PlayerState { IsReady = false, CharId = -1 }; // 初始未准备、未选
        _seats[clientId] = GetNextFreeSeat();
        Debug.Log($"[RoomState] 玩家 {clientId} 进入房间，座位 {_seats[clientId]}");

        // 新人进房时，把对手已选的英雄和准备状态推给它（对手可能早已选好，否则新人 roleenemy/prepareenemy 会漏显示）
        foreach (var kv in _players)
        {
            if (kv.Key == clientId || kv.Value.CharId < 0) continue;
            if (kv.Key == NetworkManager.ServerClientId) continue; // 对手是房主自己：房主不在"另一个客户端"上，无需推
            using (var writer = new FastBufferWriter(4, Allocator.Temp))
            {
                writer.WriteValueSafe(kv.Value.CharId);
                _nm.CustomMessagingManager.SendNamedMessage(MsgPeerSelectChar, clientId, writer);
            }
            using (var readyWriter = new FastBufferWriter(1, Allocator.Temp))
            {
                readyWriter.WriteValueSafe(kv.Value.IsReady ? (byte)1 : (byte)0);
                _nm.CustomMessagingManager.SendNamedMessage(MsgPeerReady, clientId, readyWriter);
            }
            break; // 双人局只有一个对手
        }
    }

    /// <summary>注册服务端要处理的消息；CustomMessagingManager 只在会话开始后才可用</summary>
    private void RegisterMessageHandlers()
    {
        if (_handlersRegistered || _nm == null || _nm.CustomMessagingManager == null) return;
        _handlersRegistered = true;
        _nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgSetReady, OnMsgSetReady);
        _nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgCancelReady, OnMsgCancelReady);
        _nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgSelectChar, OnMsgSelectChar);
        _nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgQueryPeerChar, OnMsgQueryPeerChar);
        _nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgPeerSelectChar, OnMsgPeerSelectChar); // host 收不到自己发的，注册无害
        _nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgPeerReady, OnMsgPeerReady);           // 同上，注册无害
    }

    /// <summary>纯客户端注册"对手选择"接收（连接成功后调用一次）</summary>
    private void RegisterClientHandlers()
    {
        if (_clientHandlersRegistered || _nm == null || _nm.CustomMessagingManager == null) return;
        _clientHandlersRegistered = true;
        _nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgPeerSelectChar, OnMsgPeerSelectChar);
        _nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgPeerReady, OnMsgPeerReady);
    }

    /// <summary>玩家离开：清掉它的记录，座位释放给后来的人</summary>
    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServerNow()) return;
        _players.Remove(clientId);
        _seats.Remove(clientId);
        Debug.Log($"[RoomState] 玩家 {clientId} 离开房间");
    }

    /// <summary>找当前最小的空闲座位号（0 号先给先入座的人），保持战斗出生点稳定</summary>
    private int GetNextFreeSeat()
    {
        for (int s = 0; s < RequiredPlayerCount; s++)
        {
            if (!_seats.ContainsValue(s)) return s;
        }
        return _seats.Count; // 正常到不了（人满后不再登记新人）
    }

    // ---------------- 命名消息入口（客户端上报） ----------------

    /// <summary>收到"准备"：读出 payload 里的 CharId 后交给 ServerSetReady 统一处理</summary>
    private void OnMsgSetReady(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsServerNow()) return;
        int charId = -1;
        try
        {
            reader.ReadValueSafe(out charId);
        }
        catch (System.Exception)
        {
            // 边界输入不可信：消息格式不对就直接忽略，别让异常把服务端打崩
            Debug.LogWarning($"[RoomState] {senderClientId} 的消息格式不对，忽略");
            return;
        }
        ServerSetReady(senderClientId, charId);
    }

    /// <summary>收到"取消准备"</summary>
    private void OnMsgCancelReady(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsServerNow()) return;
        ServerCancelReady(senderClientId);
    }

    /// <summary>收到"我选了英雄"（点选瞬间上报）：交给 ServerSetCharId 记录并转发给对手</summary>
    private void OnMsgSelectChar(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsServerNow()) return;
        if (!TryReadCharId(senderClientId, reader, out int charId)) return;
        ServerSetCharId(senderClientId, charId);
    }

    /// <summary>收到"拉取对手当前选择"（打开房间界面时）：把对手的 charId 和准备状态单发给请求者</summary>
    private void OnMsgQueryPeerChar(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsServerNow()) return;
        int peerChar = GetPeerCharId(senderClientId);
        using (var writer = new FastBufferWriter(4, Allocator.Temp))
        {
            writer.WriteValueSafe(peerChar);
            _nm.CustomMessagingManager.SendNamedMessage(MsgPeerSelectChar, senderClientId, writer);
        }
        using (var readyWriter = new FastBufferWriter(1, Allocator.Temp))
        {
            readyWriter.WriteValueSafe(GetPeerReady(senderClientId) ? (byte)1 : (byte)0);
            _nm.CustomMessagingManager.SendNamedMessage(MsgPeerReady, senderClientId, readyWriter);
        }
    }

    /// <summary>收到服务端转发的"对手选择"（仅纯客户端会收到）：触发本地事件给 UI 刷新 roleenemy</summary>
    private void OnMsgPeerSelectChar(ulong senderClientId, FastBufferReader reader)
    {
        if (IsServerNow()) return; // host 收不到自己发的消息，这里防御一下
        if (!TryReadCharId(senderClientId, reader, out int charId)) return;
        // 客户端不知道对手的 clientId，用 ulong.MaxValue 占位（room.cs 只用来排除"是自己"）
        OnPeerCharIdChanged?.Invoke(ulong.MaxValue, charId);
    }

    /// <summary>收到服务端推送的"对手准备状态"（仅纯客户端会收到）：触发本地事件给 UI 刷新 prepareenemy</summary>
    private void OnMsgPeerReady(ulong senderClientId, FastBufferReader reader)
    {
        if (IsServerNow()) return;
        byte flag = 0;
        try
        {
            reader.ReadValueSafe(out flag);
        }
        catch (Exception)
        {
            Debug.LogWarning($"[RoomState] {senderClientId} 的 PeerReady 消息格式不对，忽略");
            return;
        }
        OnPeerReadyChanged?.Invoke(ulong.MaxValue, flag != 0);
    }

    /// <summary>统一读 int payload，格式错误只警告不抛异常（客户端输入不可信）</summary>
    private bool TryReadCharId(ulong senderClientId, FastBufferReader reader, out int charId)
    {
        charId = -1;
        try
        {
            reader.ReadValueSafe(out charId);
            return true;
        }
        catch (Exception)
        {
            Debug.LogWarning($"[RoomState] {senderClientId} 的消息格式不对，忽略");
            return false;
        }
    }

    // ---------------- 服务端状态修改（客户端消息 & 房主 UI 都走这里） ----------------

    /// <summary>
    /// 服务端记录某玩家的英雄选择（点选瞬间，不等准备），并实时转发给对手：
    /// 1) 校验成员 + CharId 范围；
    /// 2) 转发给房间里另一个"纯客户端"；
    /// 3) 触发 OnPeerCharIdChanged（房主本机 UI 靠它更新 roleenemy）。
    /// </summary>
    public void ServerSetCharId(ulong clientId, int charId)
    {
        if (!IsServerNow()) return;
        if (!_players.ContainsKey(clientId))
        {
            Debug.LogWarning($"[RoomState] {clientId} 不在房间状态里（可能是旁观者），忽略选英雄");
            return;
        }
        if (charId < 0 || charId >= HeroCount)
        {
            Debug.LogWarning($"[RoomState] {clientId} 上报非法英雄 CharId={charId}");
            return;
        }

        var state = _players[clientId];
        if (state.CharId == charId) return; // 没变化不重复广播

        // 换人后之前的准备作废（与 room.cs 本地逻辑一致），服务端状态也同步复位
        bool wasReady = state.IsReady;
        _players[clientId] = new PlayerState { IsReady = false, CharId = charId };
        Debug.Log($"[RoomState] 玩家 {clientId} 选择英雄 {charId}");
        if (wasReady)
        {
            Debug.Log($"[RoomState] 玩家 {clientId} 换人，准备状态已复位");
            ServerBroadcastPeerReady(clientId, false);
        }

        // 转发给另一个纯客户端（房主在本进程，走下面的事件拿）
        foreach (var kv in _players)
        {
            if (kv.Key == clientId) continue;
            if (kv.Key == NetworkManager.ServerClientId) continue; // 对手是房主自己：不走网络
            using var writer = new FastBufferWriter(4, Allocator.Temp);
            writer.WriteValueSafe(charId);
            _nm.CustomMessagingManager.SendNamedMessage(MsgPeerSelectChar, kv.Key, writer);
        }

        // 本机（房主）UI 事件：room.cs 收到后如果 clientId 不是自己就刷 roleenemy
        OnPeerCharIdChanged?.Invoke(clientId, charId);
    }

    /// <summary>取某玩家对手当前选的英雄（-1 = 对手没选/没有对手）；仅服务端</summary>
    public int GetPeerCharId(ulong clientId)
    {
        if (!IsServerNow()) return -1;
        foreach (var kv in _players)
        {
            if (kv.Key != clientId) return kv.Value.CharId;
        }
        return -1;
    }

    /// <summary>取某玩家对手当前是否已准备（没对手/没选人时为 false）；仅服务端</summary>
    public bool GetPeerReady(ulong clientId)
    {
        if (!IsServerNow()) return false;
        foreach (var kv in _players)
        {
            if (kv.Key != clientId) return kv.Value.CharId >= 0 && kv.Value.IsReady;
        }
        return false;
    }

    /// <summary>
    /// 服务端广播某玩家的准备状态：转发给房间里另一个"纯客户端"（payload = byte），
    /// 并触发 OnPeerReadyChanged 让房主本机 UI 刷新 prepareenemy。
    /// </summary>
    private void ServerBroadcastPeerReady(ulong clientId, bool isReady)
    {
        foreach (var kv in _players)
        {
            if (kv.Key == clientId) continue;
            if (kv.Key == NetworkManager.ServerClientId) continue; // 对手是房主自己：走下面的事件
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            writer.WriteValueSafe(isReady ? (byte)1 : (byte)0);
            _nm.CustomMessagingManager.SendNamedMessage(MsgPeerReady, kv.Key, writer);
        }
        OnPeerReadyChanged?.Invoke(clientId, isReady);
    }

    /// <summary>
    /// 服务端把某玩家标记为"已准备 + 选了英雄 CharId"。
    /// 校验两点：1) 是房间成员（旁观者/陌生人拒绝）；2) CharId 在合法范围内（防越界崩服务器）。
    /// 每次变更后都检查能否开局。
    /// </summary>
    public void ServerSetReady(ulong clientId, int charId)
    {
        if (!IsServerNow()) return;
        if (!_players.ContainsKey(clientId))
        {
            Debug.LogWarning($"[RoomState] {clientId} 不在房间状态里（可能是旁观者），忽略准备");
            return;
        }
        if (charId < 0 || charId >= HeroCount)
        {
            Debug.LogWarning($"[RoomState] {clientId} 上报非法英雄 CharId={charId}");
            return;
        }

        _players[clientId] = new PlayerState { IsReady = true, CharId = charId };
        Debug.Log($"[RoomState] 玩家 {clientId} 已准备，英雄 {charId}");
        ServerBroadcastPeerReady(clientId, true);
        CheckStartGame(); // 每次有人准备完都看一眼能不能开局
    }

    /// <summary>服务端取消某玩家准备（保留它已选的英雄，方便再点准备）</summary>
    public void ServerCancelReady(ulong clientId)
    {
        if (!IsServerNow()) return;
        if (!_players.TryGetValue(clientId, out var state)) return;
        if (!state.IsReady) return; // 本来就没准备，无需处理

        _players[clientId] = new PlayerState { IsReady = false, CharId = state.CharId };
        Debug.Log($"[RoomState] 玩家 {clientId} 取消准备");
        ServerBroadcastPeerReady(clientId, false);
    }

    /// <summary>
    /// 开局判定：人数刚好够(2) 且所有人都已准备 -> 服务端加载战斗场景（客户端由 NGO 场景管理同步切换）。
    /// _sceneLoading 防重入：LoadScene 进行中再有 ready 进来也不会二次触发。
    /// </summary>
    private void CheckStartGame()
    {
        if (_sceneLoading) return;          // 已在加载，忽略
        if (_players.Count != RequiredPlayerCount) return; // 人没齐（防止 1 人也能开）

        // 只要还有一个人没准备就继续等
        foreach (var kv in _players)
        {
            if (!kv.Value.IsReady) return;
        }

        _sceneLoading = true;
        var status = _nm.SceneManager.LoadScene(BattleSceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            // Started 以外的返回值都是失败（场景名错/没进 Build Settings/场景管理没开等）
            Debug.LogError($"[RoomState] 加载战斗场景失败({status})：确认场景已加进 Build Settings 且名字正确");
            _sceneLoading = false; // 失败要复位，允许下次再触发
            return;
        }

        // 开局成功后清掉准备标记（CharId/座位保留）：以后若做"返回大厅再战"，双方需要重新点准备
        var keys = new List<ulong>(_players.Keys);
        foreach (var k in keys)
        {
            var p = _players[k];
            _players[k] = new PlayerState { IsReady = false, CharId = p.CharId };
        }
        Debug.Log("[RoomState] 双方准备完成，已开始加载战斗场景");
    }

    // ---------------- 战斗场景读取（仅服务端） ----------------

    /// <summary>
    /// 供战斗场景的 BattleSpawnManager 取快照：返回按座位升序的玩家列表。
    /// 座位 0 排前面 -> 出生点数组下标直接对应座位号。
    /// </summary>
    public List<BattlePlayerInfo> GetBattlePlayers()
    {
        var list = new List<BattlePlayerInfo>(_players.Count);
        foreach (var kv in _players)
        {
            int seat = _seats.TryGetValue(kv.Key, out var s) ? s : 0; // 有座位用座位，异常兜底 0
            list.Add(new BattlePlayerInfo
            {
                ClientId = kv.Key,
                Seat = seat,
                CharId = kv.Value.CharId
            });
        }
        list.Sort((a, b) => a.Seat.CompareTo(b.Seat)); // 保证生成顺序确定：座位 0 先
        return list;
    }
}

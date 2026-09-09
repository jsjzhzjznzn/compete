using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 战斗场景生成角色（独立组件，放战斗场景）。
///
/// 挂载要求：
///   1. 本脚本所在物体要带 NetworkObject，作为"场景内放置的 NetworkObject"，
///      这样服务端加载完战斗场景时 OnNetworkSpawn 才会被触发
///   2. 英雄预制体必须带 NetworkObject，并注册进 NetworkManager 的 Network Prefabs List
///      （否则服务端 Spawn 会报 "prefab not registered"）
///   3. 需要位移同步的给英雄挂 NetworkTransform；角色控制脚本里用 IsOwner 判断是否本机操作
///
/// 触发流程（只在服务端执行）：
///   RoomState 判定双方就绪 -> LoadScene(战斗场景)
///   -> 本组件 OnNetworkSpawn（服务端）-> GetBattlePlayers() 拿房间快照
///   -> 按 (座位 -> 出生点, CharId -> 英雄预制体) 生成双方英雄
/// </summary>
public class BattleSpawnManager : NetworkBehaviour
{
    [Header("英雄预制体：数组下标 = CharId（安比=0，艾莲=1，与房间 room.cs 一致）")]
    public List<GameObject> HeroPrefabList;
    [Header("出生点：数组下标 = 座位（0 先入座=房主，1 后入座）")]
    public List<Transform> SpawnPoints;

    /// <summary>防止重复生成（例如 OnNetworkSpawn 在异常情况下被再次调用）</summary>
    private bool _spawned;

    /// <summary>本次生成统计：成功生成数（排查"只生成一个人物"用）</summary>
    private int _spawnedCount;
    /// <summary>本次生成统计：被跳过数（CharId/座位/预制体空等）</summary>
    private int _skippedCount;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer || _spawned) return; // 生成逻辑只有服务端跑一次
        _spawned = true;

        // ---- 配置校验：缺任何一样都直接报错，别带着坏配置继续跑 ----
        if (HeroPrefabList == null || HeroPrefabList.Count == 0 || SpawnPoints == null || SpawnPoints.Count == 0)
        {
            Debug.LogError("[BattleSpawn] 没配置英雄预制体或出生点");
            return;
        }
        // RoomState 在联机预制体上(DDOL)，切场景后仍然活着；拿不到说明没挂
        if (RoomState.Instance == null)
        {
            Debug.LogError("[BattleSpawn] 找不到 RoomState（联机预制体根节点上没挂？）");
            return;
        }

        // 从服务端房间状态拿玩家快照（含每人选的英雄 CharId 和座位）
        var players = RoomState.Instance.GetBattlePlayers();
        if (players.Count == 0)
        {
            Debug.LogError("[BattleSpawn] RoomState 里没有玩家数据");
            return;
        }

        _spawnedCount = 0;
        _skippedCount = 0;
        foreach (var p in players)
            SpawnHeroFor(p);

        // 汇总（排查"只生成一个人物"：看 总数 vs 成功 vs 跳过）
        Debug.Log($"[BattleSpawn] 生成汇总: 房间玩家={players.Count} 成功={_spawnedCount} 跳过={_skippedCount}，" +
                  $"出生点数={SpawnPoints.Count} 英雄预制体数={HeroPrefabList.Count}");
    }

    /// <summary>为单个玩家生成英雄：先做下标/空引用校验，再实例化并分配网络归属</summary>
    private void SpawnHeroFor(RoomState.BattlePlayerInfo info)
    {
        // CharId 越界保护：房间数据理论上经过 RoomState 校验，这里再兜底一次（英雄列表可能配错）
        if (info.CharId < 0 || info.CharId >= HeroPrefabList.Count)
        {
            _skippedCount++;
            Debug.LogError($"[BattleSpawn] 玩家 {info.ClientId} 的 CharId={info.CharId} 越界（预制体数={HeroPrefabList.Count}），跳过");
            return;
        }
        // 座位越界保护：出生点没配够就直接跳过，别在空 Transform 上生成
        if (info.Seat < 0 || info.Seat >= SpawnPoints.Count)
        {
            _skippedCount++;
            Debug.LogError($"[BattleSpawn] 玩家 {info.ClientId} 的座位 {info.Seat} 没有对应出生点（只有 {SpawnPoints.Count} 个），跳过");
            return;
        }

        var prefab = HeroPrefabList[info.CharId];
        if (prefab == null)
        {
            _skippedCount++;
            Debug.LogError($"[BattleSpawn] 英雄预制体 CharId={info.CharId} 为空");
            return;
        }

        var spawnPoint = SpawnPoints[info.Seat];
        var hero = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        var netObj = hero.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            _skippedCount++;
            Debug.LogError($"[BattleSpawn] 英雄预制体 {prefab.name} 上没有 NetworkObject");
            Destroy(hero); // 没 NetworkObject 的对象无法入网，本地销毁避免留下空壳
            return;
        }

        // ===== 关键修复（2026-09）：不要用 destroyWithScene=true！ =====
        // 之前用 netObj.SpawnWithOwnership(clientId, true) 时，英雄是在"战斗场景加载事件还在进行中"
        // 生成的，NGO 场景事件收尾时会把这类对象一并反序列化销毁 → 表现为"刚进对战就有人物消失"。
        // 现在改成默认 destroyWithScene=false：英雄被 Instantiate 进当前(战斗)场景，
        // 场景卸载时 Unity 会把它们随场景一起销毁（无需 destroyWithScene 标记），且不会在加载事件里被误清。
        // SpawnWithOwnership：把归属权给对应玩家（IsOwner 输入/血量逻辑），【不是】PlayerObject。
        netObj.SpawnWithOwnership(info.ClientId);
        _spawnedCount++;
        Debug.Log($"[BattleSpawn] 玩家 {info.ClientId} 座位 {info.Seat} 生成英雄 CharId={info.CharId}, " +
                  $"OwnerClientId={netObj.OwnerClientId}（客户端应看到 Owner==自己的 LocalClientId 才能操作）");
    }
}

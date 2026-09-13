using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Buff 管理组件（MonoBehaviour，挂角色身上，与 HealthModel / AttributeComponent 并列）
///
/// 职责：
///   1. 添加/叠加/移除 Buff（按 BuffData.buffIdHash 判定同一 Buff，层数累加 + 时长刷新）
///   2. Update 统一计时：剩余时间倒计时、按策略 tickInterval 周期性触发 OnTick
///   3. 到期/主动移除/死亡清空，并调用策略生命周期钩子（OnApply/OnStackChanged/OnTick/OnRemove/OnExpire）
///   4. 属性加成经 AttributeComponent（Modifier 账本）生效，本组件不再自己求和系数
///
/// 【服务端权威】：Buff 状态只在服务端变更（单机未 spawn 时本地模拟）。
///   变更后走 ClientRpc 广播给各端派发 E_BuffAdd/E_BuffRemove（UI 按 target 过滤）。
///   跨端挂 Buff 走 RequestAddBuff → ServerRpc（RPC 传 buffIdHash，服务端用 BuffDatabase 还原配置）。
///
/// 计时用 Time.deltaTime（受 timeScale 影响）：顿帧/慢动作时 Buff 计时同步暂停。
/// </summary>
public class BuffComponent : NetworkBehaviour
{
    /// <summary>当前角色身上的全部 Buff 实例（只读遍历用，修改必须走 AddBuff/RemoveBuff）</summary>
    public IReadOnlyList<BuffInstance> Buffs => _buffs;

    private readonly List<BuffInstance> _buffs = new List<BuffInstance>();

    /// <summary>
    /// 网络 Buff 列表（服务端写、全员可读）：服务端把 _buffs 的摘要同步到各端，
    /// 客户端据此重建只读镜像——解决迟到加入/断线重连看不到已有 Buff、客户端查询不到的问题。
    /// 单机（!IsSpawned）全程不使用（NetworkList 未 spawn 时不可写）。
    /// </summary>
    private readonly NetworkList<BuffNetState> _netBuffs = new NetworkList<BuffNetState>();

    /// <summary>血量组件缓存（同物体上必有 HealthModel，首次访问懒获取）</summary>
    private HealthModel _health;

    private HealthModel Health => _health != null ? _health : _health = GetComponent<HealthModel>();

    /// <summary>行为状态组件缓存（可能未挂载，未挂时行为类效果空转）</summary>
    private CharacterState _characterState;

    private CharacterState CharacterState => _characterState != null ? _characterState : _characterState = GetComponent<CharacterState>();

    public override void OnNetworkSpawn()
    {
        _netBuffs.OnListChanged += OnNetBuffsChanged;

        if (IsServer)
            SyncNetBuffs();               // 初次全量推送（含 spawn 前已挂的 Buff）
        else
            RebuildLocalBuffsFromNet();   // 迟到加入：NetworkList 首次同步不走 OnListChanged，必须主动重建
    }

    public override void OnNetworkDespawn()
    {
        _netBuffs.OnListChanged -= OnNetBuffsChanged;
    }

    private void Update()
    {
        // 服务端权威：非服务端不结算 Buff（DoT/回血/到期只由服务端模拟），
        // 只递减镜像剩余时间供展示（不跑效果、不做到期移除，移除由服务端列表增量驱动）
        if (IsSpawned && !IsServer)
        {
            TickClientMirror();
            return;
        }

        // 死亡清空：血量归零后移除所有 Buff
        var health = Health;
        if (health != null && !health.IsAlive)
        {
            if (_buffs.Count > 0) RemoveAllBuffs();
            return;
        }

        // 倒序遍历：移除时安全 RemoveAt
        float dt = Time.deltaTime;
        bool netDirty = false;
        for (int i = _buffs.Count - 1; i >= 0; i--)
        {
            var buff = _buffs[i];
            var effect = buff.data.effect;
            if (effect == null) continue;   // 未配置策略：挂上但不产生效果

            // 永久 Buff（duration <= 0）不倒计时，仅走 tick
            if (buff.data.duration > 0f)
                buff.remainTime -= dt;

            // 策略声明需要周期结算时：累计 tick 计时，到间隔触发一次
            if (effect.tickInterval > 0f)
            {
                buff.tickTimer += dt;
                if (buff.tickTimer >= effect.tickInterval)
                {
                    buff.tickTimer = 0f;
                    effect.OnTick(buff, Health);
                }
            }

            if (buff.remainTime <= 0f)
            {
                effect.OnExpire(buff);
                _buffs.RemoveAt(i);
                NotifyChange(buff.data, 0, 0f, false);
                netDirty = true;
            }
        }

        // 到期移出后统一同步一次网络列表（循环内只置脏，避免每帧多次写）
        if (netDirty) SyncNetBuffs();
    }

    // ==================== 添加 / 移除 ====================

    /// <summary>
    /// 添加一个 Buff。同 buffId 已存在时：层数累加（不超过 maxStack）+ 时长刷新，并通知策略 OnStackChanged。
    /// 新建实例时调用策略 OnApply。变更后通知各端（单机本地派发 / 联网 ClientRpc）。
    /// </summary>
    /// <param name="data">静态配置（必须非空）</param>
    /// <param name="source">施加来源（DoT 扣血时作为伤害来源）</param>
    /// <param name="stacks">本次添加的层数（默认 1）</param>
    public void AddBuff(BuffData data, GameObject source = null, int stacks = 1)
    {
        if (data == null) return;
        if (IsSpawned && !IsServer) return;   // 服务端权威

        var existing = _buffs.Find(b => b.data.buffIdHash == data.buffIdHash);
        if (existing != null)
        {
            // 同一 buffId 视为同一 Buff：以【已挂实例的配置】为准（时长/层数上限/效果）。
            // 若换了一份"同 buffId 的不同资产"，提醒配置冲突（否则新配置会被静默忽略）
            if (existing.data != data)
                Debug.LogWarning($"[Buff] 叠加同 buffId 的 Buff 时传入了不同资产（现有 {existing.data.name} / 传入 {data.name}），将沿用已挂实例的配置", this);

            existing.stacks = Mathf.Min(existing.stacks + Mathf.Max(1, stacks), existing.data.maxStack);
            if (existing.data.duration > 0f) existing.remainTime = existing.data.duration;  // 叠加刷新时长
            existing.data.effect?.OnStackChanged(existing);   // 层数变化 → 属性类效果重算 Modifier
            SyncNetBuffs();                                   // 层数/时长变化同步到网络列表
            NotifyChange(existing.data, existing.stacks, existing.remainTime, true);
            return;
        }

        var buff = new BuffInstance(data, gameObject, source, stacks);
        _buffs.Add(buff);
        data.effect?.OnApply(buff);
        SyncNetBuffs();                                       // 新增一条同步到网络列表
        NotifyChange(data, buff.stacks, buff.remainTime, true);
    }

    /// <summary>
    /// 请求挂 Buff（跨端安全）：未联网直接本地挂；联网走服务端，RPC 传 buffIdHash，
    /// 服务端用 BuffDatabase 还原配置后挂上（附带攻击者的网络 id 作为 source）。
    /// </summary>
    public void RequestAddBuff(BuffData data, GameObject source)
    {
        if (data == null) return;

        if (!IsSpawned)
        {
            AddBuff(data, source);
            return;
        }

        AddBuffServerRpc(data.buffIdHash, ResolveSourceId(source));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void AddBuffServerRpc(int buffIdHash, ulong sourceId)
    {
        if (!IsServer) return;

        var data = BuffDatabase.Resolve(buffIdHash);
        if (data == null) return;   // 未配置总表/查不到：丢弃（防伪造 id）

        AddBuff(data, ResolveSourceObject(sourceId));
    }

    /// <summary>移除指定 Buff（按 buffIdHash 匹配，与叠加判定口径一致），调用策略 OnRemove 并通知各端</summary>
    public void RemoveBuff(BuffData data)
    {
        if (data == null) return;
        if (IsSpawned && !IsServer) return;

        int hash = data.buffIdHash;
        for (int i = _buffs.Count - 1; i >= 0; i--)
        {
            if (_buffs[i].data.buffIdHash == hash)
            {
                var removed = _buffs[i];
                removed.data.effect?.OnRemove(removed);
                _buffs.RemoveAt(i);
                SyncNetBuffs();                               // 移除后同步到网络列表
                NotifyChange(removed.data, 0, 0f, false);
                return;
            }
        }
    }

    /// <summary>清空全部 Buff（死亡/场景切换用），逐个调用策略 OnRemove</summary>
    public void RemoveAllBuffs()
    {
        if (IsSpawned && !IsServer) return;

        for (int i = _buffs.Count - 1; i >= 0; i--)
        {
            var buff = _buffs[i];
            buff.data.effect?.OnRemove(buff);
            var data = buff.data;
            _buffs.RemoveAt(i);
            NotifyChange(data, 0, 0f, false);
        }

        SyncNetBuffs();   // 清空后同步（_netBuffs 一并清空）
    }

    /// <summary>是否带有指定 Buff（按 buffIdHash 匹配，与叠加判定口径一致）</summary>
    public bool HasBuff(BuffData data)
    {
        if (data == null) return false;
        int hash = data.buffIdHash;
        for (int i = 0; i < _buffs.Count; i++)
            if (_buffs[i].data.buffIdHash == hash) return true;
        return false;
    }

    // ==================== 网络同步 ====================

    /// <summary>
    /// 服务端把 _buffs 的摘要按 buffIdHash 增量同步到 _netBuffs：
    ///   新出现 → Add；已存在但状态变化（层数/到期时刻/控制标志）→ Set；已消失 → RemoveAt。
    /// 单机（!IsSpawned）与客户端不可写，直接 return。
    /// </summary>
    private void SyncNetBuffs()
    {
        if (!IsSpawned || !IsServer) return;

        // 反向：_netBuffs 中已不在 _buffs 的移除（从后往前 RemoveAt 安全）
        for (int i = _netBuffs.Count - 1; i >= 0; i--)
        {
            if (FindBuff(_netBuffs[i].buffIdHash) == null)
                _netBuffs.RemoveAt(i);
        }

        // 正向：_buffs 每条写入 / 更新（Set 内部有相等门禁：值没变不发事件）
        for (int i = 0; i < _buffs.Count; i++)
        {
            var state = ToNetState(_buffs[i]);
            int index = IndexOfNetBuff(state.buffIdHash);
            if (index < 0) _netBuffs.Add(state);
            else _netBuffs[index] = state;
        }
    }

    /// <summary>
    /// 客户端订阅 _netBuffs 变化：任何结构性变更都整体重建镜像（不按事件 Index 增量，避免索引错位）。
    /// 服务端本地写也会触发本事件（host），必须 return 防重入、防与服务端本地派发重复。
    /// </summary>
    private void OnNetBuffsChanged(NetworkListEvent<BuffNetState> changeEvent)
    {
        if (IsServer) return;
        RebuildLocalBuffsFromNet();
    }

    /// <summary>
    /// 客户端从 _netBuffs 重建只读镜像 _buffs，并同步 CharacterState 的控制计数。
    /// 【绝不调用效果钩子】：属性 Modifier 与控制效果只在服务端生效，客户端调钩子会污染本地账本。
    /// </summary>
    private void RebuildLocalBuffsFromNet()
    {
        if (!IsSpawned || IsServer) return;

        _buffs.Clear();

        int stun = 0, silence = 0, invincible = 0;
        for (int i = 0; i < _netBuffs.Count; i++)
        {
            var state = _netBuffs[i];
            var data = BuffDatabase.Resolve(state.buffIdHash);
            if (data == null)
            {
                Debug.LogWarning($"[Buff] 客户端无法解析 buffIdHash={state.buffIdHash}，跳过（BuffDatabase 两端不一致？）", this);
                continue;
            }

            var buff = new BuffInstance(data, gameObject, null, state.stacks);
            buff.remainTime = state.endServerTime > 0.0
                ? Mathf.Max(0f, (float)(state.endServerTime - NetworkManager.ServerTime.Time))
                : float.MaxValue;
            _buffs.Add(buff);

            if ((state.controlFlags & (byte)BuffControlFlags.Stun) != 0) stun++;
            if ((state.controlFlags & (byte)BuffControlFlags.Silence) != 0) silence++;
            if ((state.controlFlags & (byte)BuffControlFlags.Invincible) != 0) invincible++;
        }

        CharacterState?.SetControlStates(stun, silence, invincible);
    }

    /// <summary>
    /// 客户端镜像计时：仅按 deltaTime 递减 remainTime 供 UI 展示，
    /// 不做任何结算、不做到期移除、不派发事件——到期移除完全由服务端 _netBuffs 增量驱动。
    /// 与服务端一致用 Time.deltaTime，顿帧/慢动作时两端同步暂停。
    /// </summary>
    private void TickClientMirror()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < _buffs.Count; i++)
        {
            var buff = _buffs[i];
            if (buff.data.duration > 0f && buff.remainTime < float.MaxValue)
                buff.remainTime = Mathf.Max(0f, buff.remainTime - dt);
        }
    }

    /// <summary>把一条运行时 Buff 摘要成可同步状态（永久 Buff 的 endServerTime 用 0 哨兵，避免超大数）</summary>
    private BuffNetState ToNetState(BuffInstance buff)
    {
        double endServerTime = buff.data.duration > 0f
            ? NetworkManager.ServerTime.Time + buff.remainTime
            : 0.0;
        byte flags = (byte)(buff.data.effect != null ? buff.data.effect.controlFlags : BuffControlFlags.None);
        return new BuffNetState(buff.data.buffIdHash, buff.stacks, endServerTime, flags);
    }

    /// <summary>按 buffIdHash 找运行时 Buff（找不到返回 null）</summary>
    private BuffInstance FindBuff(int buffIdHash)
    {
        for (int i = 0; i < _buffs.Count; i++)
            if (_buffs[i].data.buffIdHash == buffIdHash) return _buffs[i];
        return null;
    }

    /// <summary>按 buffIdHash 找 _netBuffs 下标（找不到返回 -1）</summary>
    private int IndexOfNetBuff(int buffIdHash)
    {
        for (int i = 0; i < _netBuffs.Count; i++)
            if (_netBuffs[i].buffIdHash == buffIdHash) return i;
        return -1;
    }

    // ==================== 内部 ====================

    /// <summary>变更通知：单机本地派发；联网走 ClientRpc 由各端派发（UI 按 target 过滤）</summary>
    private void NotifyChange(BuffData data, int stacks, float remainTime, bool isAdd)
    {
        if (data == null) return;

        // 永久 Buff 的 remainTime 是 float.MaxValue：归一成 0 再发，避免 UI 收到天文数字
        if (remainTime >= float.MaxValue) remainTime = 0f;

        if (!IsSpawned)
        {
            DispatchLocal(isAdd, data, stacks, remainTime);
            return;
        }

        NotifyBuffChangeClientRpc(data.buffIdHash, stacks, remainTime, isAdd);
    }

    [ClientRpc]
    private void NotifyBuffChangeClientRpc(int buffIdHash, int stacks, float remainTime, bool isAdd)
    {
        var data = BuffDatabase.Resolve(buffIdHash);
        DispatchLocal(isAdd, data, stacks, remainTime);
    }

    /// <summary>派发 buff 状态变化事件（统一封装，避免调用处漏发 target）</summary>
    private void DispatchLocal(bool isAdd, BuffData data, int stacks, float remainTime)
    {
        EventCenter.MainInstance.Dispatch(
            isAdd ? E_EventType.E_BuffAdd : E_EventType.E_BuffRemove,
            new BuffChangeData
            {
                target = gameObject,
                buffData = data,
                stacks = stacks,
                remainTime = remainTime
            });
    }

    /// <summary>伤害来源 GameObject → NetworkObjectId（0 表示无来源）</summary>
    private static ulong ResolveSourceId(GameObject source)
    {
        if (source == null) return 0;
        var netObj = source.GetComponentInParent<NetworkObject>();
        return netObj != null ? netObj.NetworkObjectId : 0;
    }

    /// <summary>按 NetworkObjectId 查伤害来源 GameObject（查不到返回 null）</summary>
    private static GameObject ResolveSourceObject(ulong sourceId)
    {
        if (sourceId == 0 || NetworkManager.Singleton == null) return null;
        return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(sourceId, out var no)
            ? no.gameObject
            : null;
    }
}

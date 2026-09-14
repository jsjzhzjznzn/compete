using System;
using Unity.Netcode;

/// <summary>
/// Buff 行为控制标志（位组合）：服务端在同步 buff 状态时按效果类型计算，
/// 客户端据此更新本地 CharacterState（眩晕/沉默/无敌的引用计数）。
///
/// 用位标志而非直接同步 CharacterState 的计数，是为了让"某个 buff 贡献了哪些控制状态"
/// 跟着 buff 走——同一条 buff 数据既能驱动服务端效果，也能驱动客户端表现，不额外占字段。
/// </summary>
[Flags]
public enum BuffControlFlags : byte
{
    None = 0,
    Stun = 1 << 0,
    Silence = 1 << 1,
    Invincible = 1 << 2,
}

/// <summary>
/// Buff 网络同步状态（NGO NetworkList 的元素类型）
///
/// 用途：服务端把身上每条 Buff 的"可同步摘要"写进 NetworkList，客户端据此重建只读镜像，
/// 解决迟到加入/断线重连看不到已有 Buff、以及客户端查询不到 Buff 状态的问题。
///
/// 约束：NGO 2.x 的 NetworkList&lt;T&gt; 要求 T 是 【unmanaged + IEquatable&lt;T&gt;】（不是 INetworkSerializable），
/// 所以本结构体只能含非托管字段（int/double/byte），且相等性必须比较【全部字段】——
/// 否则叠层/刷新时长时 NetworkList.Set 的相等门禁会把变更吞掉。
///
/// 序列化：显式实现 INetworkSerializeByMemcpy —— 本结构体全是 blittable 字段，直接按内存拷贝最省事，
/// 且**不依赖 NGO 的 ILPP 自动生成**（否则会报 "Serialization has not been generated for type BuffNetState"）。
///
/// 只带"跨端展示与查询够用"的字段：
///   - 不含 source（只有服务端 DoT 结算用，客户端镜像传 null）
///   - 不含 tickTimer（tick 只在服务端结算）
/// </summary>
public struct BuffNetState : IEquatable<BuffNetState>, INetworkSerializeByMemcpy
{
    /// <summary>Buff 标识哈希（身份键 + 客户端 BuffDatabase.Resolve 反查配置用）</summary>
    public int buffIdHash;

    /// <summary>当前层数</summary>
    public int stacks;

    /// <summary>
    /// 服务端绝对到期时刻（NetworkManager.ServerTime.Time 时间轴）；
    /// &lt;= 0 表示永久 Buff。用绝对时刻而非剩余秒快照，迟到加入者也能算出准确剩余时间。
    /// </summary>
    public double endServerTime;

    /// <summary>该 Buff 贡献的行为控制标志（BuffControlFlags 位组合）</summary>
    public byte controlFlags;

    public BuffNetState(int buffIdHash, int stacks, double endServerTime, byte controlFlags)
    {
        this.buffIdHash = buffIdHash;
        this.stacks = stacks;
        this.endServerTime = endServerTime;
        this.controlFlags = controlFlags;
    }

    public bool Equals(BuffNetState other)
        => buffIdHash == other.buffIdHash
        && stacks == other.stacks
        && endServerTime == other.endServerTime
        && controlFlags == other.controlFlags;

    public override bool Equals(object obj) => obj is BuffNetState other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(buffIdHash, stacks, endServerTime, controlFlags);
}

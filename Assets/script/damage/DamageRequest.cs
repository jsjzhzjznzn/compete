using Unity.Netcode;

/// <summary>
/// 一次伤害请求（由攻击方发起）。
///
/// 【传的是数值，不是倍率】出手伤害由攻方在自己出手那一刻用
/// DamageCalculator.CalculateOutgoing 算定（攻击力 × 招式倍率 × 增伤 × 暴击），
/// 只把结果数值带过来；受击方拿到后只做自己这一侧的防御/减伤结算
/// （DamageCalculator.CalculateIncoming）。
///
/// 这样做的语义收益：出手瞬间数值固化，之后攻方属性增减不会回溯影响这一击（出手快照）。
/// 代价：联网时失去"服务端按权威攻击力重算"的防作弊能力 —— 按设计取舍，服务端不做数值上限校验。
///
/// 单机（未 spawn）时同一套请求走本地结算，路径一致。
///
/// 联网时整个结构体作为 RequestDamageServerRpc 的唯一参数发送：
/// 以后新增字段（元素/伤害类型等）只需在此追加 + 在 NetworkSerialize 追加一行，
/// RPC 签名与全部调用点都不用动。
/// ⚠ NetworkSerialize 里的字段顺序即线上格式，只能追加，不能插队或改序
///   （与 AttrType 枚举同一条规矩：改序会让新旧构建解析错位）。
/// </summary>
public struct DamageRequest : INetworkSerializable
{
    /// <summary>攻方算好的出手伤害（含攻击力/装备/Buff/招式倍率/增伤/暴击；尚未减受方防御）</summary>
    public float damage;

    /// <summary>本次是否暴击（攻方掷骰决定，随请求带给受方做飘字样式）</summary>
    public bool isCritical;

    /// <summary>攻击者 NetworkObjectId（受方据此解析伤害来源，供飘字/受击/DoT 使用）</summary>
    public ulong sourceId;

    /// <summary>是否持续伤害（DoT：不触发受击硬直、无视无敌窗口）</summary>
    public bool isDoT;

    /// <summary>网络序列化（顺序 = 线上字节格式，新增字段只能追加在末尾）</summary>
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref damage);
        serializer.SerializeValue(ref isCritical);
        serializer.SerializeValue(ref sourceId);
        serializer.SerializeValue(ref isDoT);
    }
}

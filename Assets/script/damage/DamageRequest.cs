/// <summary>
/// 一次伤害请求（由攻击方发起，最终伤害由服务端用服务端属性重算）。
/// 只携带"配置侧的原始数值"，不携带最终伤害 —— 客户端无法伪造最终值，
/// 受击方减伤等属性加成也一律在服务端按服务端数据计算。
///
/// 单机（未 spawn）时同一套请求走本地计算，保持原行为。
/// </summary>
public struct DamageRequest
{
    /// <summary>攻击段配置的基础伤害</summary>
    public float baseDamage;

    /// <summary>暴击率（0~1）</summary>
    public float critRate;

    /// <summary>暴击倍率（暴击时伤害 × 该值）</summary>
    public float critMultiplier;

    /// <summary>攻击者 NetworkObjectId（联网时服务端据此解析攻方，读其增伤属性）</summary>
    public ulong sourceId;

    /// <summary>是否持续伤害（DoT：不触发受击硬直、无视无敌窗口）</summary>
    public bool isDoT;
}

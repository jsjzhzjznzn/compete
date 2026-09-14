/// <summary>
/// 一次伤害请求（由攻击方发起，最终伤害由服务端用服务端属性重算）。
/// 只携带"招式倍率 + 暴击参数"，**不携带伤害数值** ——
/// 最终基础伤害 = 【攻击者攻击力】× 倍率，攻击力由服务端从攻击者的 AttributeComponent 读取，
/// 因此客户端既伪造不了伤害数值，也伪造不了攻击力（修复了旧版直接传 baseDamage 的漏洞）。
///
/// 单机（未 spawn）时同一套请求走本地计算，保持原行为。
/// </summary>
public struct DamageRequest
{
    /// <summary>招式伤害倍率（小数：0.08 = 攻击力的 8%）。最终基础伤害 = 攻击者攻击力 × 该值</summary>
    public float multiplier;

    /// <summary>暴击率（0~1）</summary>
    public float critRate;

    /// <summary>暴击倍率（暴击时伤害 × 该值）</summary>
    public float critMultiplier;

    /// <summary>攻击者 NetworkObjectId（联网时服务端据此解析攻方，读其攻击力/增伤属性）</summary>
    public ulong sourceId;

    /// <summary>是否持续伤害（DoT：不触发受击硬直、无视无敌窗口）</summary>
    public bool isDoT;
}

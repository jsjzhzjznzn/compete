/// <summary>
/// Buff 效果类型枚举
/// 决定 Buff 到期前"持续做什么"：是改属性系数（增伤/减伤/移速），还是周期性结算（流血/回血）。
/// 新增效果类型时：在此加枚举值，并在 BuffComponent 的 Update/tick 结算处补对应分支。
/// </summary>
public enum BuffEffectType
{
    /// <summary>增伤：每层按系数提升造成的伤害（乘区，DamageUp 0.3 = +30%）</summary>
    DamageUp,

    /// <summary>减伤：每层按系数降低受到的伤害（乘区，DamageDown 0.2 = -20%，上限 90%）</summary>
    DamageDown,

    /// <summary>持续伤害（DoT，流血/灼烧）：每隔 tickInterval 秒扣一次血</summary>
    DoT,

    /// <summary>持续回血（HoT，治疗/再生）：每隔 tickInterval 秒回一次血</summary>
    HoT,

    /// <summary>加速：每层按系数提升移动速度（乘区，暂只提供查询接口，移动状态机接入见二期）</summary>
    SpeedUp,

    /// <summary>减速：每层按系数降低移动速度（乘区，暂只提供查询接口，移动状态机接入见二期）</summary>
    SpeedDown,
}

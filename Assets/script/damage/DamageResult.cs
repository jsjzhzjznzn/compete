using System.Collections.Generic;

/// <summary>
/// 伤害计算结果（struct，值类型）
/// 管道跑完后的一次性输出：最终伤害、是否暴击、是否被完全格挡、各阶段数值明细。
/// 调用方拿 finalDamage + isCritical 去调 HealthModel.TakeDamage / 飘字即可。
///
/// stageResults：按阶段名记录每阶段结束时的伤害值，调平衡时打日志看伤害从哪一步变的。
/// </summary>
public struct DamageResult
{
    /// <summary>最终伤害（管道末尾已保底：>0 时至少为 1）</summary>
    public float finalDamage;

    /// <summary>本次是否暴击（传给 TakeDamage 的 isCritical，飘字据此用暴击样式）</summary>
    public bool isCritical;

    /// <summary>是否被完全拦下（减伤后伤害归零；预留，后续格挡系统可复用）</summary>
    public bool isBlocked;

    /// <summary>各阶段结束时的伤害值（键=阶段名，如 "Base"/"Critical"/"Final"；调试用）</summary>
    public Dictionary<string, float> stageResults;
}

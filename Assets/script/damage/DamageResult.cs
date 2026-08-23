/// <summary>
/// 伤害计算结果（struct，值类型，无引用字段 → 拷贝零分配）
/// 管道跑完后的一次性输出：最终伤害、是否暴击、是否被完全格挡、各阶段数值明细。
/// 调用方拿 finalDamage + isCritical 去调 HealthModel.TakeDamage / 飘字即可。
///
/// stage* 字段：每阶段结束时的伤害值，调平衡时打断点/打日志看伤害从哪一步变的。
/// 原实现用 Dictionary 存阶段明细，每次命中分配一次堆内存且无人读取；
/// 改为纯字段后零分配，阶段名即字段名，调试器同样直观。
/// </summary>
public struct DamageResult
{
    /// <summary>最终伤害（管道末尾已保底：>0 时至少为 1）</summary>
    public float finalDamage;

    /// <summary>本次是否暴击（传给 TakeDamage 的 isCritical，飘字据此用暴击样式）</summary>
    public bool isCritical;

    /// <summary>是否被完全拦下（减伤后伤害归零；预留，后续格挡系统可复用）</summary>
    public bool isBlocked;

    // ============ 各阶段明细（调试用；纯字段替代原字典，零分配） ============
    /// <summary>阶段1 基础伤害后</summary>
    public float stageBase;

    /// <summary>阶段2 攻击方增伤后</summary>
    public float stageAttackerBonus;

    /// <summary>阶段3 暴击掷骰后</summary>
    public float stageCritical;

    /// <summary>阶段4 防御方减伤后</summary>
    public float stageDefenderReduction;

    /// <summary>阶段5 保底后</summary>
    public float stageFinal;
}

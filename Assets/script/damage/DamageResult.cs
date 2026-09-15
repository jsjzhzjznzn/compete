/// <summary>
/// 伤害计算结果（struct，值类型，无引用字段 → 拷贝零分配）
///
/// 管道拆成攻守两段后，本结构是两个函数的共用输出，**只填自己那半的字段**：
///   CalculateOutgoing（攻方段）填：finalDamage(=出手伤害)、isCritical、
///                                  stageBase / stageAttackerBonus / stageCritical
///   CalculateIncoming（受方段）填：finalDamage(=实际扣血)、
///                                  stageDefense / stageDefenderReduction / stageFinal
/// 对手方那半的字段保持默认 0，不要读。
/// 调用方拿 finalDamage + isCritical 去调 HealthModel.TakeDamage / 飘字即可。
///
/// stage* 字段：每阶段结束时的伤害值，调平衡时打断点/打日志看伤害从哪一步变的。
/// 原实现用 Dictionary 存阶段明细，每次命中分配一次堆内存且无人读取；
/// 改为纯字段后零分配，阶段名即字段名，调试器同样直观。
/// </summary>
public struct DamageResult
{
    /// <summary>最终伤害：攻方段 = 出手伤害（未减防御）；受方段 = 实际扣血（已保底：>0 时至少为 1）</summary>
    public float finalDamage;

    /// <summary>本次是否暴击（仅攻方段填写；随 DamageRequest 带给受方，飘字据此用暴击样式）</summary>
    public bool isCritical;

    // ============ 各阶段明细（调试用；纯字段替代原字典，零分配） ============
    /// <summary>【攻方段】阶段1 攻击力 × 招式倍率 后</summary>
    public float stageBase;

    /// <summary>【攻方段】阶段2 攻击方增伤系数后</summary>
    public float stageAttackerBonus;

    /// <summary>【攻方段】阶段3 暴击掷骰后（= 最终的出手伤害）</summary>
    public float stageCritical;

    /// <summary>【受方段】阶段4 防御方防御力减免后</summary>
    public float stageDefense;

    /// <summary>【受方段】阶段5 防御方减伤系数后</summary>
    public float stageDefenderReduction;

    /// <summary>【受方段】阶段6 保底后（= 实际扣血）</summary>
    public float stageFinal;
}

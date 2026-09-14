using UnityEngine;

/// <summary>
/// 伤害计算管道（静态类，无状态纯函数）
/// 全项目唯一的伤害数值计算入口，固定顺序：
///
///   攻击力 × 招式倍率 → AttackerBonus(攻击方增伤) → Critical(暴击)
///                     → Defense(防御力减免) → DefenderReduction(减伤系数) → 保底
///
/// 为什么固定顺序写死而不是可插拔修饰器注册表：
///   本项目修饰点少（增伤/暴击/防御/减伤），顺序即文档，一屏看完；
///   注册制要管理优先级排序 + 生命周期，属于 RPG 几十种修饰才需要的复杂度。
///
/// 为什么是静态类而不是 MonoBehaviour 单例：
///   纯计算无状态、无 Update，不需要场景对象，直接 DamageCalculator.Calculate() 调用。
///
/// 数值来源约定：
///   攻击力/防御力/增伤/减伤**全部**从攻击方/受击方的 AttributeComponent 属性账本读取
///   （没挂组件/没配则走默认值，见 DefaultAttack）；招式只提供倍率与暴击参数。
/// </summary>
public static class DamageCalculator
{
    /// <summary>减伤系数上限（0.9 = 最多减免 90%，防止叠满减伤后伤害变负/归零失去意义）</summary>
    private const float MaxDamageReduction = 0.9f;

    /// <summary>防御力减免公式常数：减免比例 = DEF / (DEF + K)。K 越大，同样防御力的收益越低</summary>
    private const float DefenseK = 100f;

    /// <summary>攻击力兜底：角色没配 AttributeComponent.Attack（或漏配 CharacterStatsData）时用它，避免伤害塌成保底 1</summary>
    private const float DefaultAttack = 100f;

    /// <summary>
    /// 计算一次伤害的最终数值。按固定阶段顺序执行，每阶段结果记入 stage* 字段供调试。
    /// </summary>
    /// <param name="ctx">伤害上下文（招式倍率、暴击参数、攻防双方）</param>
    /// <returns>计算结果（最终伤害 + 标记 + 阶段明细）</returns>
    public static DamageResult Calculate(DamageContext ctx)
    {
        var result = new DamageResult();

        // 阶段 1：攻击力 × 招式倍率（攻击力是白值，装备/Buff 加的就是它）
        float attack = GetAttrValue(ctx.attacker, AttrType.Attack);
        if (attack <= 0f) attack = DefaultAttack;   // 兜底：没配攻击力时不让伤害塌成 1
        float damage = attack * ctx.multiplier;
        result.stageBase = damage;

        // 阶段 2：攻击方增伤系数（读 AttributeComponent.DamageUp，无组件返回 0 即无加成）
        float attackerBonus = GetAttrValue(ctx.attacker, AttrType.DamageUp);
        damage *= 1f + attackerBonus;
        result.stageAttackerBonus = damage;

        // 阶段 3：暴击掷骰（每次命中独立判定，暴击时伤害 × 倍率）
        result.isCritical = Random.value < ctx.critRate;
        if (result.isCritical)
        {
            damage *= Mathf.Max(1f, ctx.critMultiplier);
        }
        result.stageCritical = damage;

        // 阶段 4：防御方防御力减免（乘式：1 - DEF/(DEF+K)；DEF=0 时无影响，且永不归零）
        float defense = GetAttrValue(ctx.defender, AttrType.Defense);
        if (defense > 0f)
            damage *= 1f - defense / (defense + DefenseK);
        result.stageDefense = damage;

        // 阶段 5：防御方减伤系数（读 AttributeComponent.DamageDown，系数 clamp 到 [0, 0.9]）
        float defenderReduction = Mathf.Clamp(GetAttrValue(ctx.defender, AttrType.DamageDown), 0f, MaxDamageReduction);
        damage *= 1f - defenderReduction;
        result.stageDefenderReduction = damage;

        // 阶段 6：保底（伤害归零标记格挡，否则至少 1 点，保证攻击永远有反馈）
        if (damage <= 0f)
        {
            result.isBlocked = true;
            damage = 0f;
        }
        else
        {
            damage = Mathf.Max(1f, damage);
        }
        result.stageFinal = damage;

        result.finalDamage = damage;
        return result;
    }

    /// <summary>
    /// 查询某角色指定属性的最终值（读 AttributeComponent 账本；白值型与系数型通用）。
    /// 角色身上没有 AttributeComponent（或其加成 Buff 不在本端）时返回 0（无修饰）。
    /// </summary>
    private static float GetAttrValue(GameObject target, AttrType type)
    {
        if (target == null) return 0f;

        // GetComponentInParent：命中 Collider 挂在子物体时也能找到角色根上的属性组件
        var attr = target.GetComponentInParent<AttributeComponent>();
        if (attr == null) return 0f;

        return attr.GetValue(type);
    }
}

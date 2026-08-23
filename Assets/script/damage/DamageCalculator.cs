using UnityEngine;

/// <summary>
/// 伤害计算管道（静态类，无状态纯函数）
/// 全项目唯一的伤害数值计算入口，固定顺序：
///
///   Base(基础伤害) → AttackerBonus(攻击方增伤) → Critical(暴击) → DefenderReduction(防御方减伤) → 保底
///
/// 为什么固定顺序写死而不是可插拔修饰器注册表：
///   本项目修饰点少（增伤/暴击/减伤），顺序即文档，一屏看完；
///   注册制要管理优先级排序 + 生命周期，属于 RPG 几十种修饰才需要的复杂度。
///
/// 为什么是静态类而不是 MonoBehaviour 单例：
///   纯计算无状态、无 Update，不需要场景对象，直接 DamageCalculator.Calculate() 调用。
///
/// 数值来源约定：
///   增伤/减伤系数从攻击方/受击方的 BuffComponent 查询（没挂组件返回 0）；
///   暴击参数由调用方传入（ComboData 每段独立配置）。
/// </summary>
public static class DamageCalculator
{
    /// <summary>减伤系数上限（0.9 = 最多减免 90%，防止叠满减伤后伤害变负/归零失去意义）</summary>
    private const float MaxDamageReduction = 0.9f;

    /// <summary>
    /// 计算一次伤害的最终数值。按固定阶段顺序执行，每阶段结果记入 stage* 字段供调试。
    /// </summary>
    /// <param name="ctx">伤害上下文（基础伤害、暴击参数、攻防双方）</param>
    /// <returns>计算结果（最终伤害 + 标记 + 阶段明细）</returns>
    public static DamageResult Calculate(DamageContext ctx)
    {
        var result = new DamageResult();

        // 阶段 1：基础伤害（攻击段配置的原始值）
        float damage = ctx.baseDamage;
        result.stageBase = damage;

        // 阶段 2：攻击方增伤（BuffComponent 查询，无组件返回 0 即无加成）
        float attackerBonus = GetModifier(ctx.attacker, BuffEffectType.DamageUp);
        damage *= 1f + attackerBonus;
        result.stageAttackerBonus = damage;

        // 阶段 3：暴击掷骰（每次命中独立判定，暴击时伤害 × 倍率）
        result.isCritical = Random.value < ctx.critRate;
        if (result.isCritical)
        {
            damage *= Mathf.Max(1f, ctx.critMultiplier);
        }
        result.stageCritical = damage;

        // 阶段 4：防御方减伤（系数 clamp 到 [0, 0.9]）
        float defenderReduction = Mathf.Clamp(GetModifier(ctx.defender, BuffEffectType.DamageDown), 0f, MaxDamageReduction);
        damage *= 1f - defenderReduction;
        result.stageDefenderReduction = damage;

        // 阶段 5：保底（伤害归零标记格挡，否则至少 1 点，保证攻击永远有反馈）
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
    /// 查询某角色的指定类型 Buff 修饰系数（各层数值求和，向上取整层数倍）。
    /// 角色身上没有 BuffComponent 或没有对应 Buff 时返回 0（无修饰）。
    /// </summary>
    private static float GetModifier(GameObject target, BuffEffectType type)
    {
        if (target == null) return 0f;

        // GetComponentInParent：命中 Collider 挂在子物体时也能找到角色根上的 BuffComponent
        var buffComponent = target.GetComponentInParent<BuffComponent>();
        if (buffComponent == null) return 0f;

        return type switch
        {
            BuffEffectType.DamageUp => buffComponent.GetDamageUpModifier(),
            BuffEffectType.DamageDown => buffComponent.GetDamageDownModifier(),
            _ => 0f,
        };
    }
}

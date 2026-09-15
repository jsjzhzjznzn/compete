using UnityEngine;

/// <summary>
/// 伤害计算管道（静态类，无状态纯函数）—— 分成攻守两段：
///
///   【攻方段】CalculateOutgoing：攻击力 × 招式倍率 → ×(1+增伤) → 暴击
///             只依赖【攻方自己】的属性，产出"出手伤害"这一个数值。
///   【受方段】CalculateIncoming：×防御力减免 → ×减伤 → 保底
///             只依赖【受方自己】的属性，产出实际扣血。
///
/// 为什么拆成两段：
///   出手伤害在攻方出手那一刻算定，之后攻方属性再变也不回溯影响这一击（出手快照）；
///   两段各自只读自己一侧的账本，互不跨读对方属性。
///
/// 数值来源约定：
///   攻方段读 AttributeComponent.GetOwnValue
///     （服务端/单机 = 本地账本；拥有者客户端 = 服务端推来的出手属性镜像）
///   受方段读 AttributeComponent.GetValue
///     （只有服务端/单机持有受方的完整账本，客户端不写 Modifier）
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
    /// 【攻方段】算出"出手伤害"（阶段 1-3）。
    /// 攻击力是白值，装备/Buff 加的就是它；招式只提供倍率与暴击参数。
    /// 结果填 finalDamage(出手伤害) + isCritical + stageBase/stageAttackerBonus/stageCritical。
    /// </summary>
    /// <param name="attacker">攻方 GameObject（读它的 Attack / DamageUp；可为 null）</param>
    /// <param name="multiplier">招式伤害倍率（小数：0.08 = 攻击力的 8%）</param>
    /// <param name="critRate">暴击率（0~1）</param>
    /// <param name="critMultiplier">暴击倍率</param>
    public static DamageResult CalculateOutgoing(GameObject attacker, float multiplier,
                                                 float critRate, float critMultiplier)
    {
        var result = new DamageResult();

        // 阶段 1：攻击力 × 招式倍率
        float attack = GetOwnAttrValue(attacker, AttrType.Attack);
        if (attack <= 0f) attack = DefaultAttack;   // 兜底：没配攻击力时不让伤害塌成 1
        float damage = attack * multiplier;
        result.stageBase = damage;

        // 阶段 2：攻击方增伤系数（读 AttributeComponent.DamageUp，无组件返回 0 即无加成）
        damage *= 1f + GetOwnAttrValue(attacker, AttrType.DamageUp);
        result.stageAttackerBonus = damage;

        // 阶段 3：暴击掷骰（每次结算独立判定，暴击时伤害 × 倍率）
        result.isCritical = Random.value < critRate;
        if (result.isCritical)
            damage *= Mathf.Max(1f, critMultiplier);
        result.stageCritical = damage;

        result.finalDamage = damage;
        return result;
    }

    /// <summary>
    /// 【受方段】对收到的出手伤害做防御/减伤/保底结算（阶段 4-6），返回实际扣血。
    /// 结果填 finalDamage(实际扣血) + stageDefense/stageDefenderReduction/stageFinal。
    ///
    /// incoming 非法（NaN/Infinity）时直接返回 0 伤害。
    /// 这**不是**数值合理性校验（出手伤害由攻方算定，不做上限校验），
    /// 而是防止 NaN 进血量后角色僵死：血量变 NaN → IsAlive=false，但死亡事件不派发，
    /// 之后 TakeDamage / Heal 都被闸门挡住 —— 既死不了也不能再被处理。
    /// </summary>
    /// <param name="defender">受方 GameObject（读它的 Defense / DamageDown）</param>
    /// <param name="incoming">攻方算好的出手伤害</param>
    public static DamageResult CalculateIncoming(GameObject defender, float incoming)
    {
        var result = new DamageResult();

        if (!float.IsFinite(incoming)) return result;   // finalDamage 保持 0

        float damage = incoming;

        // 阶段 4：防御方防御力减免（乘式：1 - DEF/(DEF+K)；DEF=0 时无影响，且永不归零）
        float defense = GetAttrValue(defender, AttrType.Defense);
        if (defense > 0f)
            damage *= 1f - defense / (defense + DefenseK);
        result.stageDefense = damage;

        // 阶段 5：防御方减伤系数（读 AttributeComponent.DamageDown，系数 clamp 到 [0, 0.9]）
        float defenderReduction = Mathf.Clamp(GetAttrValue(defender, AttrType.DamageDown), 0f, MaxDamageReduction);
        damage *= 1f - defenderReduction;
        result.stageDefenderReduction = damage;

        // 阶段 6：保底（退化输入 → 0；其余至少 1 点，保证攻击永远有反馈）
        damage = damage <= 0f ? 0f : Mathf.Max(1f, damage);
        result.stageFinal = damage;

        result.finalDamage = damage;
        return result;
    }

    /// <summary>
    /// 【攻方段】读攻方自己的属性：走 GetOwnValue ——
    /// 服务端/单机读本地账本；拥有者客户端读服务端推来的出手属性镜像
    /// （客户端本地账本只有基础白值，没有 Buff 加成）。
    /// </summary>
    private static float GetOwnAttrValue(GameObject target, AttrType type)
    {
        if (target == null) return 0f;

        var attr = target.GetComponentInParent<AttributeComponent>();
        if (attr == null) return 0f;

        return attr.GetOwnValue(type);
    }

    /// <summary>
    /// 【受方段】读受方属性：受方的账本只有服务端/单机是完整的
    /// （客户端不写 Modifier），所以这里读本地最终值即可。
    /// 角色身上没有 AttributeComponent 时返回 0（无修饰）。
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

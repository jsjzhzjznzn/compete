using Unity.Netcode;
using UnityEngine;

// ============================================================================
// 技能效果节点：最小集实现
// 全部走项目现有系统（HealthModel / BuffComponent），不引入额外实体抽象。
// 注：这是框架，尚未接入任何现有战斗流程。
// ============================================================================

/// <summary>
/// 造成伤害节点：对 target 发起一次伤害请求（服务端权威，最终伤害由服务端按攻击力×倍率重算）。
/// 最终倍率 = 配置伤害倍率 × 本次释放系数(context.skillCoef)。
/// </summary>
public class DamageEffectNode : ISkillEffect
{
    private readonly float _damageMultiplier;    // 配置伤害倍率（相对攻击力；构建后只读）
    private readonly float _critRate;
    private readonly float _critMultiplier;
    private readonly bool _isDoT;

    public DamageEffectNode(float damageMultiplier, float critRate = 0f, float critMultiplier = 1f, bool isDoT = false)
    {
        _damageMultiplier = damageMultiplier;
        _critRate = critRate;
        _critMultiplier = critMultiplier;
        _isDoT = isDoT;
    }

    public void Execute(GameObject source, GameObject target, SkillCastContext context)
    {
        if (target == null) return;

        var health = target.GetComponentInParent<HealthModel>();
        if (health == null || !health.IsAlive) return;

        health.RequestDamage(new DamageRequest
        {
            multiplier = _damageMultiplier * context.skillCoef,
            critRate = _critRate,
            critMultiplier = _critMultiplier,
            sourceId = ResolveSourceId(source),
            isDoT = _isDoT,
        }, source);
    }

    private static ulong ResolveSourceId(GameObject source)
    {
        if (source == null) return 0UL;
        var netObj = source.GetComponentInParent<NetworkObject>();
        return netObj != null && netObj.IsSpawned ? netObj.NetworkObjectId : 0UL;
    }
}

/// <summary>治疗节点：按系数对目标回血</summary>
public class HealEffectNode : ISkillEffect
{
    private readonly float _baseHeal;

    public HealEffectNode(float baseHeal) => _baseHeal = baseHeal;

    public void Execute(GameObject source, GameObject target, SkillCastContext context)
    {
        var health = target != null ? target.GetComponentInParent<HealthModel>() : null;
        health?.Heal(_baseHeal * context.skillCoef);
    }
}

/// <summary>挂 Buff 节点：把配置的 Buff 施加到目标（走 RequestAddBuff，联网转发服务端）</summary>
public class AddBuffEffectNode : ISkillEffect
{
    private readonly BuffData _buff;

    public AddBuffEffectNode(BuffData buff) => _buff = buff;

    public void Execute(GameObject source, GameObject target, SkillCastContext context)
    {
        if (_buff == null || target == null) return;
        target.GetComponentInParent<BuffComponent>()?.RequestAddBuff(_buff, source);
    }
}

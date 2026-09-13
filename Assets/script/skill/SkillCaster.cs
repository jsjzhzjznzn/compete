using UnityEngine;

/// <summary>
/// 技能释放入口（表达式树驱动）。
/// 前置校验（CD / 蓝量 / 沉默 / 距离）应在调用本类之前完成，本类只负责"建上下文 → 遍历执行节点"。
///
/// 用法示例（尚未接入现有战斗，需自行构建 SkillAsset）：
///   var asset = new SkillAsset(1001, new List&lt;ISkillEffect&gt;
///   {
///       new DamageEffectNode(2.0f),                  // 2 倍基础伤害
///       new AddBuffEffectNode(someBuffData),         // 命中后挂 Buff
///   });
///   SkillCaster.Cast(caster, target, asset);
/// </summary>
public static class SkillCaster
{
    /// <summary>释放技能：构建本次上下文并驱动表达式树执行</summary>
    /// <param name="caster">释放者</param>
    /// <param name="target">作用目标</param>
    /// <param name="asset">技能资源（启动阶段构建、全局复用）</param>
    /// <param name="skillCoef">本次释放的动态系数（默认 1）</param>
    public static void Cast(GameObject caster, GameObject target, SkillAsset asset, float skillCoef = 1f)
    {
        if (asset == null) return;

        // TODO: 生产环境从对象池 Alloc/Free，避免每次释放 new（当前框架先直接 new）
        var context = new SkillCastContext
        {
            skillId = asset.skillId,
            skillCoef = skillCoef,
            executeTime = Time.time,
        };

        asset.Execute(caster, target, context);
    }
}

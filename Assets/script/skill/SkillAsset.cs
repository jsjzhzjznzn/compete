using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能效果节点（表达式树基础接口）。
/// 每个实现类 = 一个最小原子战斗行为（造成伤害 / 治疗 / 挂 Buff / 生成弹体 …）。
///
/// 约定：
///   - 节点在【启动阶段】构建并全局复用，运行释放只做遍历执行，Execute 内不要 new 集合/实体；
///   - 节点只依赖入参 source / target / context，不写死攻击者与目标；
///   - 一个节点只做一件事（不要把"伤害+眩晕+挂buff"塞进同一个 Execute）。
/// </summary>
public interface ISkillEffect
{
    /// <summary>执行本效果节点</summary>
    /// <param name="source">释放者（伤害来源 / Buff 施加者）</param>
    /// <param name="target">作用目标</param>
    /// <param name="context">本次释放上下文</param>
    void Execute(GameObject source, GameObject target, SkillCastContext context);
}

/// <summary>
/// 技能资源（表达式树载体）：一串可执行的效果节点，列表顺序即执行顺序。
/// 简单技能 = 线性列表；复杂技能 = 树（复合节点内部持有子节点）。
///
/// 由配置表在【启动阶段】解析构建（工厂按节点类型 + 参数 new 出节点），运行释放只遍历执行、不再 new 节点。
/// </summary>
public class SkillAsset
{
    /// <summary>技能 ID（配置表主键）</summary>
    public readonly int skillId;

    /// <summary>效果节点（表达式树）；只读复用</summary>
    public readonly List<ISkillEffect> effectNodes;

    public SkillAsset(int skillId, List<ISkillEffect> effectNodes)
    {
        this.skillId = skillId;
        this.effectNodes = effectNodes ?? new List<ISkillEffect>();
    }

    /// <summary>依次执行全部节点（多态分发，运行时只遍历不 new）</summary>
    public void Execute(GameObject source, GameObject target, SkillCastContext context)
    {
        for (int i = 0; i < effectNodes.Count; i++)
            effectNodes[i]?.Execute(source, target, context);
    }
}

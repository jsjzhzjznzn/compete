using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// 复合效果节点：内部持有子节点，把多个原子效果组合成树。
// 列表只是树的特例：SequenceEffect 的子节点列表 ≈ 简单技能的 effectNodes。
// ============================================================================

/// <summary>顺序执行全部子节点</summary>
public class SequenceEffect : ISkillEffect
{
    private readonly List<ISkillEffect> _children;

    public SequenceEffect(List<ISkillEffect> children)
        => _children = children ?? new List<ISkillEffect>();

    public void Execute(GameObject source, GameObject target, SkillCastContext context)
    {
        for (int i = 0; i < _children.Count; i++)
            _children[i]?.Execute(source, target, context);
    }
}

/// <summary>延时执行子节点（"释放 → 等 N 秒 → 结算"，用 TimerManager 调度）</summary>
public class DelayEffect : ISkillEffect
{
    private readonly float _delay;
    private readonly ISkillEffect _child;

    public DelayEffect(float delay, ISkillEffect child)
    {
        _delay = delay;
        _child = child;
    }

    public void Execute(GameObject source, GameObject target, SkillCastContext context)
    {
        if (_child == null) return;

        if (_delay <= 0f)
        {
            _child.Execute(source, target, context);
            return;
        }

        TimerManager.MainInstance.GetOneTimer(_delay, () => _child.Execute(source, target, context));
    }
}

/// <summary>条件分支：predicate 为真执行 trueNode，否则执行 falseNode（可空）</summary>
public class ConditionEffect : ISkillEffect
{
    private readonly Func<GameObject, GameObject, SkillCastContext, bool> _predicate;
    private readonly ISkillEffect _trueNode;
    private readonly ISkillEffect _falseNode;

    public ConditionEffect(Func<GameObject, GameObject, SkillCastContext, bool> predicate,
                           ISkillEffect trueNode,
                           ISkillEffect falseNode = null)
    {
        _predicate = predicate;
        _trueNode = trueNode;
        _falseNode = falseNode;
    }

    public void Execute(GameObject source, GameObject target, SkillCastContext context)
    {
        if (_predicate == null) return;
        var node = _predicate(source, target, context) ? _trueNode : _falseNode;
        node?.Execute(source, target, context);
    }
}

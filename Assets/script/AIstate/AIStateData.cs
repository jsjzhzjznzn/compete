using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI 状态数据基类（模仿 PlayerStateData）
/// 每个状态一份数据：动画片段 + 播放参数
/// </summary>
[System.Serializable]
public abstract class AIStateData
{
    /// <summary>该状态播放的动画片段</summary>
    [field: SerializeField] public AnimationClip animationClip { get; private set; }

    /// <summary>播放速度倍率（对应 animancer.States.Current.Speed）</summary>
    [field: SerializeField, Range(0f, 4f)] public float playSpeed { get; private set; } = 1f;

    /// <summary>Animancer 淡入时长（秒）</summary>
    [field: SerializeField] public float fadeDuration { get; private set; } = 0.15f;

    /// <summary>转向平滑时间（秒，预留：接根运动转向时用）</summary>
    [field: SerializeField] public float rotationTime { get; private set; } = 0.04f;
}

/// <summary>待机数据</summary>
[System.Serializable]
public class AIIdleData : AIStateData { }

/// <summary>行走数据</summary>
[System.Serializable]
public class AIWalkData : AIStateData
{
    /// <summary>巡逻随机点半径（米）</summary>
    [field: SerializeField] public float wanderRadius { get; private set; } = 5f;

    /// <summary>到点/超时换下一个巡逻点（秒）</summary>
    [field: SerializeField, Min(0.5f)] public float wanderRetargetTime { get; private set; } = 3f;
}

/// <summary>死亡数据</summary>
[System.Serializable]
public class AIDieData : AIStateData
{
    /// <summary>死亡动画时长（秒，播完后停留/销毁）</summary>
    [field: SerializeField, Min(0.1f)] public float dieAnimDuration { get; private set; } = 2f;
}

/// <summary>受击硬直数据</summary>
[System.Serializable]
public class AIHurtData : AIStateData
{
    /// <summary>硬直时长（秒，定身 + 可被再次受击刷新）</summary>
    [field: SerializeField, Min(0.05f)] public float stunDuration { get; private set; } = 0.4f;
}

/// <summary>
/// 攻击数据（Attack1/2/3 各一份）
/// 原来写死在 AttackState 里的时长/伤害/范围全部搬到这里配置
/// </summary>
[System.Serializable]
public class AIAttackData : AIStateData
{
    /// <summary>整个攻击动作的总时长（秒）</summary>
    [field: SerializeField] public float duration { get; private set; } = 0.8f;

    /// <summary>命中判定时刻（动画开始后第几秒出伤害）</summary>
    [field: SerializeField] public float hitTime { get; private set; } = 0.35f;

    /// <summary>第二段命中时刻（仅 Attack2 连击用）</summary>
    [field: SerializeField] public float secondHitTime { get; private set; } = 0.75f;

    /// <summary>攻击范围（米）</summary>
    [field: SerializeField] public float range { get; private set; } = 2f;

    /// <summary>伤害</summary>
    [field: SerializeField] public float damage { get; private set; } = 10f;

    /// <summary>收招后额外冷却（秒）</summary>
    [field: SerializeField] public float cooldown { get; private set; } = 0.2f;

    /// <summary>AOE 半径（仅 Attack3 大招用）</summary>
    [field: SerializeField] public float aoeRadius { get; private set; } = 4f;
}

/// <summary>
/// AI 连招配置（模仿 Player 的连击索引）
/// 按序列循环出招：每次实际出招索引 ++，resetTime 内没出下一招就归零从头开始
/// </summary>
[System.Serializable]
public class AIComboData
{
    /// <summary>连招序列（循环）：如 Attack1 → Attack2 → Attack1。只填 Attack1/2/3</summary>
    [field: SerializeField] public List<AIStateType> comboSequence { get; private set; }
        = new List<AIStateType> { AIStateType.Attack1, AIStateType.Attack2 };

    /// <summary>多久没出下一次攻击就重置回序列开头（秒）</summary>
    [field: SerializeField, Min(0.1f)] public float resetTime { get; private set; } = 2f;
}

/// <summary>
/// AI 移动/行为参数集合（模仿 PlayerMovementData）
/// 作为 AIPlayerSO 的子字段在 Inspector 中配置
/// </summary>
[System.Serializable]
public class AIMovementData
{
    [Header("行为参数")]
    /// <summary>移动速度</summary>
    [field: SerializeField] public float walkSpeed { get; private set; } = 3f;

    /// <summary>发现目标的索敌半径（米）</summary>
    [field: SerializeField] public float detectRange { get; private set; } = 8f;

    /// <summary>脱离追击半径（米）</summary>
    [field: SerializeField] public float chaseRange { get; private set; } = 10f;

    [Header("连招")]
    [field: SerializeField] public AIComboData comboData { get; private set; }

    [Header("各状态数据")]
    [field: SerializeField] public AIIdleData idleData { get; private set; }
    [field: SerializeField] public AIWalkData walkData { get; private set; }
    [field: SerializeField] public AIHurtData hurtData { get; private set; }
    [field: SerializeField] public AIDieData dieData { get; private set; }
    [field: SerializeField] public AIAttackData attack1Data { get; private set; }
    [field: SerializeField] public AIAttackData attack2Data { get; private set; }
    [field: SerializeField] public AIAttackData attack3Data { get; private set; }
}

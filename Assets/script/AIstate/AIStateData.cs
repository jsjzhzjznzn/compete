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

/// <summary>受击硬直数据（僵直时长 = 受击动画播放时长,这里只配动画和播放参数）</summary>
[System.Serializable]
public class AIHurtData : AIStateData { }

/// <summary>
/// 攻击段数据（一段攻击一份）
/// 单段/多段/AOE 统一由本类描述：命中帧可多段，isAoe 打开时走范围伤害。
/// 连招顺序由 AIComboData.attacks 列表顺序决定（像 player 的 ComboContainerData）
/// </summary>
[System.Serializable]
public class AIAttackData : AIStateData
{
    /// <summary>整个攻击动作的总时长（秒，秒表兜底模式用）</summary>
    [field: SerializeField] public float duration { get; private set; } = 0.8f;

    /// <summary>命中判定时刻列表（动画开始后第几秒出伤害，可多段；空=本段无伤害判定）</summary>
    [field: SerializeField] public List<float> hitTimes { get; private set; } = new List<float> { 0.35f };

    /// <summary>攻击范围（米）</summary>
    [field: SerializeField] public float range { get; private set; } = 2f;

    /// <summary>伤害（每次命中结算一次）</summary>
    [field: SerializeField] public float damage { get; private set; } = 10f;

    /// <summary>收招后额外冷却（秒）</summary>
    [field: SerializeField] public float cooldown { get; private set; } = 0.2f;

    /// <summary>是否范围(AOE)伤害：开启则命中帧对 aoeRadius 内所有目标结算</summary>
    [field: SerializeField] public bool isAoe { get; private set; } = false;

    /// <summary>AOE 半径（米，仅 isAoe 时生效）</summary>
    [field: SerializeField] public float aoeRadius { get; private set; } = 4f;
}

/// <summary>
/// AI 连招配置（模仿 player 的 ComboContainerData）
/// attacks 列表顺序 = 连招顺序：每次实际出招索引 ++，resetTime 内没出下一招就归零从头开始
/// </summary>
[System.Serializable]
public class AIComboData
{
    /// <summary>攻击段列表（顺序即连招顺序）：如 [轻攻击, 连击, AOE大招]</summary>
    [field: SerializeField] public List<AIAttackData> attacks { get; private set; } = new List<AIAttackData>();

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

    [Header("各状态数据")]
    [field: SerializeField] public AIIdleData idleData { get; private set; }
    [field: SerializeField] public AIWalkData walkData { get; private set; }
    [field: SerializeField] public AIHurtData hurtData { get; private set; }
    [field: SerializeField] public AIDieData dieData { get; private set; }
}

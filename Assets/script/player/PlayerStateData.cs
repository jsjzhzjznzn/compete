using UnityEngine;

/// <summary>
/// 移动状态数据基类
/// 每个移动状态一份数据：动画片段 + 播放参数
/// 适配 Animancer 根运动方案：
/// - 速度由动画片段本身烘焙，需要调速时用 playSpeed（对应 animancer.States.Current.Speed）
/// - 淡入时长即原模板里的 bufferToIdleTime / fadeToWalkStartTime
/// - rotationTime 用于状态内手动转向（根运动不管转向）
/// </summary>
/// 
[System.Serializable]
public abstract class PlayerStateData
{
    /// <summary>该状态播放的动画片段</summary>
    [field: SerializeField] public AnimationClip animationClip { get; private set; }

    /// <summary>播放速度倍率</summary>
    [field: SerializeField, Range(0f, 4f)] public float playSpeed { get; private set; } = 1f;

    /// <summary>Animancer 淡入时长（秒）</summary>
    [field: SerializeField] public float fadeDuration { get; private set; } = 0.3f;

    /// <summary>转向平滑时间（秒）</summary>
    [field: SerializeField] public float rotationTime { get; private set; } = 0.04f;
}

/// <summary>待机数据</summary>
[System.Serializable]
public class PlayerIdleData : PlayerStateData { }

/// <summary>行走数据</summary>
[System.Serializable]
public class PlayerWalkData : PlayerStateData
{
    /// <summary>松开摇杆后的收尾动画（播完自动回待机）</summary>
    [field: SerializeField] public AnimationClip walkStopClip { get; private set; }
}

/// <summary>奔跑数据</summary>
[System.Serializable]
public class PlayerRunData : PlayerStateData { }

/// <summary>冲刺数据</summary>
[System.Serializable]
public class PlayerSprintData : PlayerStateData { }

/// <summary>闪避/突进数据</summary>
[System.Serializable]
public class PlayerDashData : PlayerStateData { }

/// <summary>回到奔跑过渡数据</summary>
[System.Serializable]
public class PlayerReturnRunData : PlayerStateData { }

/// <summary>切换角色（进入）数据</summary>
[System.Serializable]
public class PlayerOnSwitchData : PlayerStateData { }

/// <summary>切换角色（离开）数据</summary>
[System.Serializable]
public class PlayerOnSwitchOutData : PlayerStateData { }

/// <summary>空状态数据（禁止移动，无需动画）</summary>
[System.Serializable]
public class PlayerMovementNullData : PlayerStateData { }

/// <summary>受击数据（受击硬直动画）</summary>
[System.Serializable]
public class PlayerHurtData : PlayerStateData { }

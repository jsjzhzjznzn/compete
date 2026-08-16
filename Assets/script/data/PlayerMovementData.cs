using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 移动状态数据集合
/// 持有各状态数据 + 状态机共享参数，作为 PlayerSO 的子字段在 Inspector 中配置
/// </summary>
[System.Serializable]
public class PlayerMovementData
{
    // ============ 状态机共享参数 ============

    /// <summary>后退/转身判定角度（大于该角度视为转身）</summary>
    [field: SerializeField] public float turnBackAngle { get; private set; } = 135f;

    // ============ 相机回正配置 ============

    /// <summary>侧移(A/D)时的相机回正配置（按相机俯仰角区间匹配）</summary>
    [field: SerializeField] public List<PlayerCameraRecenteringData> SidewaysCameraRecenteringData { get; private set; }

    /// <summary>后退(S)时的相机回正配置（按相机俯仰角区间匹配）</summary>
    [field: SerializeField] public List<PlayerCameraRecenteringData> BackWardsCameraRecenteringData { get; private set; }

    // ============ 各状态数据 ============

    [field: SerializeField] public PlayerIdleData idleData { get; private set; }
    [field: SerializeField] public PlayerWalkData walkData { get; private set; }
    [field: SerializeField] public PlayerDashData dashData { get; private set; } //闪避
    //[field: SerializeField] public PlayerReturnRunData returnRunData { get; private set; }
   // [field: SerializeField] public PlayerOnSwitchData onSwitchData { get; private set; }
//    [field: SerializeField] public PlayerOnSwitchOutData onSwitchOutData { get; private set; }
    [field: SerializeField] public PlayerMovementNullData movementNullData { get; private set; }
}

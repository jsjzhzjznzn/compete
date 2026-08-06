using UnityEngine;

/// <summary>
/// 状态间共享的数据容器
/// 存放需要在多个移动状态间传递的字段（如速度倍率、转身时间等）
/// 后续按需补充字段
/// </summary>
  public class PlayerStateReusableData
  {
      public bool canDash { get; set; } = true;      // 闪避冷却（接入 dash 时用）
      public Vector2 inputDirection { get; set; }     // 原始输入方向（未转换，判断是否在移动用）
      public Vector3 worldMovement { get; set; }      // 相机相对转换后的世界移动向量（转向用）
      public Vector3 localMovement { get; set; }      // 世界→角色局部的移动向量（区分前进/侧移/后退动画用）
      public float targetAngle { get; set; }          // 目标朝向角（平滑转向用）
      public float rotationTime { get; set; }         // 当前状态转向平滑时间（Enter 里各状态设）
  }
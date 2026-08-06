using UnityEngine;

/// <summary>
/// 状态间共享的数据容器
/// 存放需要在多个移动状态间传递的字段（如速度倍率、转身时间等）
/// 后续按需补充字段
/// </summary>
public class PlayerStateReusableData
{
    // 示例：移动速度倍率（根运动缩放）
    public float MoveSpeedModifier { get; set; } = 1f;
}

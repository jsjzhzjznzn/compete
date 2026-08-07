using UnityEngine;

/// <summary>
/// 玩家移动状态基类
/// 子类构造时传入状态机，通过 stateMachine 访问 player / reusableData
///
/// 轮询控制说明：
/// 不做任何输入事件订阅（AddInputActionCallBacks 已废弃），
/// 所有状态在 OnUpdate 中调用 PollInput() 每帧轮询 CharacterInputSystem，
/// 基类统一处理"读输入方向 → 写共享数据 → 平滑转向"，
/// 子类只负责基于轮询结果做状态切换。
/// </summary>
public abstract class PlayerMovementState : IState
{
    protected readonly PlayerMovementStateMachine stateMachine;
    protected readonly Player player;
    protected readonly PlayerStateReusableData reusableData;

    /// <summary>移动级共享数据（未配置 PlayerSO 时为 null）</summary>
    protected PlayerMovementData playerMovementData => stateMachine.movementData;

    protected PlayerMovementState(PlayerMovementStateMachine stateMachine)
    {
        this.stateMachine = stateMachine;
        player = stateMachine.player;
        reusableData = stateMachine.reusableData;
    }

    public virtual void OnEnter() { }
    public virtual void OnUpdate() { }
    public virtual void OnExit() { }

    // ==================== 轮询输入（基类统一实现） ====================

    /// <summary>
    /// 每帧轮询输入并应用：
    /// 读取移动方向 → 相机相对转世界向量 → 转角色局部向量 → 平滑转向。
    /// 子类在 OnUpdate 中最先调用，然后再做状态切换判断。
    /// </summary>
    protected void PollInput()
    {
        reusableData.inputDirection = GetInputDirection();
        reusableData.worldMovement = GetWorldMovement(reusableData.inputDirection);
        reusableData.localMovement = GetLocalMovement(reusableData.worldMovement);
        RotateTowardsInput();
    }

    /// <summary>
    /// 读取移动输入（Vector2），斜向输入归一化，防止对角线移动速度更快
    /// </summary>
    private Vector2 GetInputDirection()
    {
        Vector2 input = CharacterInputSystem.MainInstance.Movement;
        return input.magnitude > 1f ? input.normalized : input;
    }

    /// <summary>
    /// ① ② 相机相对转换：输入 Vector2 → 世界空间移动向量。
    /// ① 相机正向投影到 XZ 水平面，消除俯仰影响（低头抬头不会让玩家上下移动）
    /// ② 前后用相机投影正向、左右用相机右向，实现"相机朝向 = 玩家移动朝向"
    /// 相机引用来自 player.CameraTransform（Inspector 显式拖入，回退 Camera.main）。
    /// 注意：这里只需要相机的旋转（forward/right），位置不参与计算。
    /// </summary>
    private Vector3 GetWorldMovement(Vector2 moveInput)
    {
        if (moveInput == Vector2.zero) return Vector3.zero;

        Transform cam = player.CameraTransform;
        if (cam == null)
        {
            return new Vector3(moveInput.x, 0f, moveInput.y);
        }

        Vector3 forward = new Vector3(cam.forward.x, 0f, cam.forward.z).normalized;
        Vector3 right = new Vector3(cam.right.x, 0f, cam.right.z).normalized;

        Vector3 world = forward * moveInput.y + right * moveInput.x;
        // 斜向输入归一化，避免对角移动更快
        return world.magnitude > 1f ? world.normalized : world;
    }

    /// <summary>
    /// ③ 世界移动向量 → 角色局部移动向量。
    /// 供动画混合/状态机做局部方向判定（区分前进/侧移/后退动画）。
    /// </summary>
    private Vector3 GetLocalMovement(Vector3 worldMovement)
    {
        if (worldMovement == Vector3.zero) return Vector3.zero;
        return player.transform.InverseTransformVector(worldMovement);
    }

    /// <summary>
    /// 平滑转向世界移动方向（水平面）。
    /// 转向平滑时间取 reusableData.rotationTime（由 ApplyStateData 在 Enter 时写入），
    /// 时间越小转向越快；根运动不管转向，所以这里手动转。
    /// </summary>
    private void RotateTowardsInput()
    {
        Vector3 dir = reusableData.worldMovement;
        if (dir.sqrMagnitude <= 0f) return;

        float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        reusableData.targetAngle = targetAngle;

        float rotationTime = reusableData.rotationTime;
        if (rotationTime <= 0f)
        {
            player.transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);
            return;
        }

        // 指数平滑：经过 rotationTime 秒后逼近目标角度
        float dampFactor = 1f - Mathf.Exp(-Time.deltaTime / rotationTime);
        float currentAngle = player.transform.eulerAngles.y;
        float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, dampFactor);
        player.transform.rotation = Quaternion.Euler(0f, newAngle, 0f);
    }

    // ==================== 状态数据应用 ====================

    /// <summary>
    /// 进入状态时统一应用状态数据：
    /// 写转向平滑时间 → 播放动画（带淡入）→ 设置 Animancer 播放速度
    /// </summary>
    protected void ApplyStateData(PlayerStateData data)
    {
        reusableData.rotationTime = data != null ? data.rotationTime : 0.04f;

        if (data?.animationClip != null)
            player.PlayAnimation(data.animationClip, data.fadeDuration);
            
    }

}

/// <summary>
/// 空状态（禁止移动时的占位状态，不播放动画）
/// </summary>
public class PlayerMovementNullState : PlayerMovementState
{
    public PlayerMovementNullState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        // 空状态不播放动画，只停止移动
        // TODO: 停止角色移动
    }

    public override void Update()
    {
        base.Update();
        // TODO: 保持静止，等待恢复移动
    }

    public override void Exit()
    {
        base.Exit();
        // TODO: 恢复移动
    }
}

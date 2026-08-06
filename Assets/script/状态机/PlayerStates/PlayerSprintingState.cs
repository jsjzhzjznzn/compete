/// <summary>
/// 冲刺状态
/// </summary>
public class PlayerSprintingState : PlayerMovementState
{
    private PlayerSprintData Data => playerMovementData?.sprintData;

    public PlayerSprintingState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        if (Data?.animationClip != null)
            player.PlayAnimation(Data.animationClip, Data.fadeDuration);
        // TODO: 其他进入初始化
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
        // TODO: 根据输入切换到奔跑/待机等状态
    }

    public override void OnExit()
    {
        base.OnExit();
        // TODO: 退出时清理
    }
}

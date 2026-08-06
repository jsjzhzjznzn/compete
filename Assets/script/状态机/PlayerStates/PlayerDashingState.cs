/// <summary>
/// 闪避/突进状态
/// </summary>
public class PlayerDashingState : PlayerMovementState
{
    private PlayerDashData Data => playerMovementData?.dashData;

    public PlayerDashingState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

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
        // TODO: 动画结束后切换到奔跑/待机等状态
    }

    public override void OnExit()
    {
        base.OnExit();
        // TODO: 退出时清理
    }
}

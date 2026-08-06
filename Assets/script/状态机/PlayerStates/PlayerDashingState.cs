/// <summary>
/// 闪避/突进状态
/// </summary>
public class PlayerDashingState : PlayerMovementState
{
    private readonly PlayerDashData data;

    public PlayerDashingState(PlayerMovementStateMachine stateMachine, PlayerDashData data) : base(stateMachine)
    {
        this.data = data;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        if (data?.animationClip != null)
            player.PlayAnimation(data.animationClip, data.fadeDuration);
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

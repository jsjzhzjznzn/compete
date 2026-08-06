/// <summary>
/// 回到奔跑状态（过渡状态）
/// </summary>
public class PlayerReturnRunState : PlayerMovementState
{
    private readonly PlayerReturnRunData data;

    public PlayerReturnRunState(PlayerMovementStateMachine stateMachine, PlayerReturnRunData data) : base(stateMachine)
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
        // TODO: 过渡结束后切换到奔跑状态
    }

    public override void OnExit()
    {
        base.OnExit();
        // TODO: 退出时清理
    }
}

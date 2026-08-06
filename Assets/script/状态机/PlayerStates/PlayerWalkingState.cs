/// <summary>
/// 行走状态
/// </summary>
public class PlayerWalkingState : PlayerMovementState
{
    private readonly PlayerWalkData data;

    public PlayerWalkingState(PlayerMovementStateMachine stateMachine, PlayerWalkData data) : base(stateMachine)
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
        // TODO: 根据输入切换到奔跑/待机等状态
    }

    public override void OnExit()
    {
        base.OnExit();
        // TODO: 退出时清理
    }
}

/// <summary>
/// 切换角色（离开）状态
/// </summary>
public class PlayerOnSwitchOutState : PlayerMovementState
{
    private readonly PlayerOnSwitchOutData data;

    public PlayerOnSwitchOutState(PlayerMovementStateMachine stateMachine, PlayerOnSwitchOutData data) : base(stateMachine)
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
        // TODO: 退场完成后交给下一个角色
    }

    public override void OnExit()
    {
        base.OnExit();
        // TODO: 退出时清理
    }
}

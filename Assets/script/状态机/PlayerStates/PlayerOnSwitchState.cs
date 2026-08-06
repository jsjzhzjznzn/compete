/// <summary>
/// 切换角色（进入）状态
/// </summary>
public class PlayerOnSwitchState : PlayerMovementState
{
    private readonly PlayerOnSwitchData data;

    public PlayerOnSwitchState(PlayerMovementStateMachine stateMachine, PlayerOnSwitchData data) : base(stateMachine)
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
        // TODO: 切换完成后进入待机/奔跑等状态
    }

    public override void OnExit()
    {
        base.OnExit();
        // TODO: 退出时清理
    }
}

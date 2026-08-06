/// <summary>
/// 奔跑状态
/// </summary>
public class PlayerRunningState : PlayerMovementState
{
    private readonly PlayerRunData data;

    public PlayerRunningState(PlayerMovementStateMachine stateMachine, PlayerRunData data) : base(stateMachine)
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
        // TODO: 根据输入切换到冲刺/待机等状态
    }

    public override void OnExit()
    {
        base.OnExit();
        // TODO: 退出时清理
    }
}

/// <summary>
/// 后冲状态
/// 向移动输入反方向快速后撤一段距离
/// 不接受输入轮询：入场后锁定位移，动画播放完成自动回到待机
/// </summary>
public class PlayerDashBackingState : PlayerMovementState
{
    private PlayerDashBackData Data => playerMovementData?.dashBackData;

    public PlayerDashBackingState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        ApplyStateData(Data);
        // TODO: 锁定后撤方向、启动后撤位移等

        // 动画播放完成 → 回到待机
        var state = player.characterAnimancer.States.Current;
        if (state != null)
            state.Events.OnEnd = OnDashBackAnimationEnd;
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
        // 后冲不接受输入轮询：不调用 PollInput()
    }

    public override void OnExit()
    {
        base.OnExit();
        // TODO: 复位后冲状态等

        // 清理动画结束回调，避免切换状态后误触发
        var state = player.characterAnimancer.States.Current;
        if (state != null)
            state.Events.OnEnd = null;
    }

    private void OnDashBackAnimationEnd()
    {
        stateMachine.SwitchState(stateMachine.idlingState);
    }
}

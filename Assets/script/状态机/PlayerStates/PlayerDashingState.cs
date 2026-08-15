/// <summary>
/// 前冲状态
/// 向移动输入方向快速突进一段距离
/// 不接受输入轮询：入场后锁定位移，动画播放完成自动回到待机
/// </summary>
public class PlayerDashingState : PlayerMovementState
{
    private PlayerDashData Data => playerMovementData?.dashData;

    public PlayerDashingState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        ApplyStateData(Data);
        // TODO: 锁定冲刺方向、初始化速度/位移等

        // 动画播放完成 → 回到待机
        var state = player.characterAnimancer.States.Current;
        if (state != null)
            state.Events.OnEnd = OnDashAnimationEnd;
    }

    public override void Update()
    {
        base.Update();
        // 前冲不接受输入轮询：不调用 PollInput()
    }

    public override void Exit()
    {
        base.Exit();
        // TODO: 复位冲刺状态等

        // 清理动画结束回调，避免切换状态后误触发
        var state = player.characterAnimancer.States.Current;
        if (state != null)
            state.Events.OnEnd = null;
    }

    private void OnDashAnimationEnd()
    {
        stateMachine.SwitchState(stateMachine.walkingState);
    }
}

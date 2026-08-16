/// <summary>
/// 走路收尾状态
/// 松开摇杆后进入：播放走路收尾动画，动画播放完成自动回到待机
/// 收尾动画播放期间仍接受输入轮询：重新移动回走路、按 Shift 进后冲
/// </summary>
public class PlayerWalkStopState : PlayerMovementState
{
    private PlayerWalkData WalkData => playerMovementData?.walkData;

    public PlayerWalkStopState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        var stopClip = WalkData?.walkStopClip;
        if (stopClip == null)
        {
            // 未配置收尾动画 → 直接回待机
            stateMachine.SwitchState(stateMachine.idlingState);
            return;
        }

        player.PlayAnimation(stopClip, WalkData.fadeDuration);

        // 动画播放完成 → 回到待机
        var state = player.characterAnimancer.States.Current;
        if (state != null)
            state.Events.OnEnd = OnWalkStopAnimationEnd;
    }

    public override void Update()
    {
        base.Update();
        PollInput();                          // 收尾动画期间也轮询输入

        // 重新有移动输入 → 回走路
        if (player.IsMoving)
        {
            stateMachine.SwitchState(stateMachine.walkingState);
            return;
        }
        // 无输入：等待收尾动画播完（OnEnd）自动回待机
    }

    public override void Exit()
    {
        base.Exit();

        // 清理动画结束回调，避免切换状态后误触发
        var state = player.characterAnimancer.States.Current;
        if (state != null)
            state.Events.OnEnd = null;
    }

    private void OnWalkStopAnimationEnd()
    {
        stateMachine.SwitchState(stateMachine.idlingState);
    }
}

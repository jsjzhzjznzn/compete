/// <summary>
/// 待机状态
/// 轮询控制：每帧读取输入，一旦有移动输入就进入 行走/奔跑
/// </summary>
public class PlayerIdlingState : PlayerMovementState
{
    private PlayerIdleData Data => playerMovementData?.idleData;

    public PlayerIdlingState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        ApplyStateData(Data);
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
        PollInput();                          // 每帧轮询输入（无输入时自然不转向）

        // Shift 按下 → 进入后冲状态
        if (player.IsSprintPressed)
        {
            stateMachine.SwitchState(stateMachine.dashBackingState);
            return;
        }

        if (!player.IsMoving) return;         // 没有移动输入，保持待机

        // 有移动输入：按住冲刺键进奔跑，否则进行走
        stateMachine.SwitchState(stateMachine.walkingState);
    }

    public override void OnExit()
    {
        base.OnExit();
    }
}

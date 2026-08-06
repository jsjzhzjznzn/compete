/// <summary>
/// 奔跑状态
/// 轮询控制：每帧读取输入并转向，无输入回待机，松开冲刺键回行走
/// </summary>
public class PlayerRunningState : PlayerMovementState
{
    private PlayerRunData Data => playerMovementData?.runData;

    public PlayerRunningState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        ApplyStateData(Data);
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
        PollInput();                          // 每帧轮询输入并平滑转向

        if (!player.IsMoving)
        {
            stateMachine.SwitchState(stateMachine.idlingState);   // 松开摇杆 → 待机
            return;
        }

        if (!player.IsSprintHeld)
        {
            stateMachine.SwitchState(stateMachine.walkingState);  // 松开冲刺 → 行走
        }
    }

    public override void OnExit()
    {
        base.OnExit();
    }
}

/// <summary>
/// 行走状态
/// 轮询控制：每帧读取输入并转向，无输入回待机，按住冲刺键进奔跑
/// </summary>
public class PlayerWalkingState : PlayerMovementState
{
    private PlayerWalkData Data => playerMovementData?.walkData;

    public PlayerWalkingState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

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

        
    }

    public override void OnExit()
    {
        base.OnExit();
    }
}

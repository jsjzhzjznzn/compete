/// <summary>
/// 前冲状态
/// 向移动输入方向快速突进一段距离
/// </summary>
public class PlayerDashingState : PlayerMovementState
{
    private PlayerDashData Data => playerMovementData?.dashData;

    public PlayerDashingState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        ApplyStateData(Data);
        // TODO: 前冲入场逻辑（锁定冲刺方向、初始化速度/位移等）
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
        // TODO: 前冲更新逻辑（每帧位移、计时、结束后切换状态）
    }

    public override void OnExit()
    {
        base.OnExit();
        // TODO: 前冲退出逻辑（复位冲刺状态等）
    }
}

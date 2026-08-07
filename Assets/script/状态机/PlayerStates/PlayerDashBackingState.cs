/// <summary>
/// 后冲状态
/// 向移动输入反方向快速后撤一段距离
/// </summary>
public class PlayerDashBackingState : PlayerMovementState
{
    private PlayerDashBackData Data => playerMovementData?.dashBackData;

    public PlayerDashBackingState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        ApplyStateData(Data);
        // TODO: 后冲入场逻辑（锁定后撤方向、初始化速度/位移等）
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
        // TODO: 后冲更新逻辑（每帧位移、计时、结束后切换状态）
    }

    public override void OnExit()
    {
        base.OnExit();
        // TODO: 后冲退出逻辑（复位后冲状态等）
    }
}

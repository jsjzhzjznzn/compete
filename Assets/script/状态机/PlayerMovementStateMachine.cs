/// <summary>
/// 玩家移动状态机
/// 创建并持有所有移动状态，供 Player 调用
/// </summary>
public class PlayerMovementStateMachine : StateMachine
{
    // 状态间共享数据
    public PlayerStateReusableData reusableData { get; }
    public Player player { get; }
    public PlayerMovementData movementData { get; }

    public PlayerIdlingState idlingState { get; }
    public PlayerWalkingState walkingState { get; }
    public PlayerWalkStopState walkStopState { get; }
    public PlayerDashingState dashingState { get; }
    public PlayerDodgeState dodgeState { get; }
    public PlayerHurtState hurtState { get; }
  
    public PlayerMovementNullState playerMovementNullState { get; }

    public PlayerMovementStateMachine(Player P, PlayerSO playerSO)
    {
        player = P;

        reusableData = new PlayerStateReusableData();

        // 从角色数据资产中取出移动数据（未配置时为 null，状态内部自行判空）
        movementData = playerSO != null ? playerSO.movementData : null;

        // 创建所有状态
        idlingState = new PlayerIdlingState(this);
        walkingState = new PlayerWalkingState(this);
        walkStopState = new PlayerWalkStopState(this);
        dashingState = new PlayerDashingState(this);
        dodgeState = new PlayerDodgeState(this);
        hurtState = new PlayerHurtState(this);
       // sprintingState = new PlayerSprintingState(this);
       // returnRunState = new PlayerReturnRunState(this);
      //  onSwitchState = new PlayerOnSwitchState(this);
      //  onSwitchOutState = new PlayerOnSwitchOutState(this);
        playerMovementNullState = new PlayerMovementNullState(this);
    }

    protected override void OnStateSwitched()
    {
        base.OnStateSwitched();
        // 移动状态变化 → 同步给网络（拥有者写入；远程端据此回放动画）
        if (CurrentState is PlayerMovementState moveState)
            player.SyncMoveState(moveState.StateType);
    }
}

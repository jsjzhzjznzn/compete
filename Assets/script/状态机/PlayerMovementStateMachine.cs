/// <summary>
/// 玩家移动状态机
/// 创建并持有所有移动状态，供 Player 调用
/// </summary>
public class PlayerMovementStateMachine : StateMachine
{
    // 状态间共享数据
    public PlayerStateReusableData reusableData { get; }
    public Player player { get; }

    public PlayerIdlingState idlingState { get; }
    public PlayerWalkingState walkingState { get; }
    public PlayerRunningState runningState { get; }
   // public PlayerSprintingState sprintingState { get; }
   // public PlayerDashingState dashingState { get; }
   // public PlayerDashBackingState dashBackingState { get; }
    public PlayerReturnRunState returnRunState { get; }
  //  public PlayerOnSwitchState onSwitchState { get; }
 //   public PlayerOnSwitchOutState onSwitchOutState { get; }
    public PlayerMovementNullState playerMovementNullState { get; }

    public PlayerMovementStateMachine(Player P, PlayerSO playerSO)
    {
        player = P;

        reusableData = new PlayerStateReusableData();

        // 从角色数据资产中取出各状态数据（未配置时为 null，状态内部自行判空）
        var movementData = playerSO != null ? playerSO.movementData : null;

        // 创建所有状态
        idlingState = new PlayerIdlingState(this, movementData?.idleData);
        walkingState = new PlayerWalkingState(this, movementData?.walkData);
        runningState = new PlayerRunningState(this, movementData?.runData);
       // sprintingState = new PlayerSprintingState(this, movementData?.sprintData);
       // dashingState = new PlayerDashingState(this, movementData?.dashData);
     //   dashBackingState = new PlayerDashBackingState(this, movementData?.dashBackData);
        returnRunState = new PlayerReturnRunState(this, movementData?.returnRunData);
      //  onSwitchState = new PlayerOnSwitchState(this, movementData?.onSwitchData);
      //  onSwitchOutState = new PlayerOnSwitchOutState(this, movementData?.onSwitchOutData);
        playerMovementNullState = new PlayerMovementNullState(this, movementData?.movementNullData);
    }
}

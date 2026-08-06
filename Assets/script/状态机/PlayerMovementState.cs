/// <summary>
/// 玩家移动状态基类
/// 子类构造时传入状态机，通过 stateMachine 访问 player / reusableData
/// </summary>
public abstract class PlayerMovementState : IState
{
    protected readonly PlayerMovementStateMachine stateMachine;
    protected readonly Player player;
    protected readonly PlayerStateReusableData reusableData;

    protected PlayerMovementState(PlayerMovementStateMachine stateMachine)
    {
        this.stateMachine = stateMachine;
        player = stateMachine.player;
        reusableData = stateMachine.reusableData;
    }

    public virtual void OnEnter() { }
    public virtual void OnUpdate() { }
    public virtual void OnExit() { }
}

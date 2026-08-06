

/// <summary>
/// 简易状态机——管理当前状态与切换
/// </summary>
public class StateMachine
{
    public IState CurrentState { get; private set; }

    public void SwitchState(IState newState)
    {
        CurrentState?.OnExit();
        CurrentState = newState;
        CurrentState?.OnEnter();
    }

    public void Update()
    {
        CurrentState?.OnUpdate();
    }
}

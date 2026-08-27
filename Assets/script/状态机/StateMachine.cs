

/// <summary>
/// 简易状态机——管理当前状态与切换
/// </summary>
public class StateMachine
{
    public IState CurrentState { get; private set; }

    public void SwitchState(IState newState)
    {
        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState?.Enter();
        OnStateSwitched();
    }

    /// <summary>状态切换完成钩子（子类覆写，用于网络状态同步等）</summary>
    protected virtual void OnStateSwitched() { }

    public void Update()
    {
        CurrentState?.Update();
    }
}

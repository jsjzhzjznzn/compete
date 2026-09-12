using UnityEngine;

/// <summary>
/// AI 状态统一枚举：Root 层 + Attack 容器内的子状态
/// 行为树自定义任务切状态时也传这个枚举
/// </summary>
public enum AIStateType
{
    Root,      // 顶层容器（不可切换，仅内部使用）
    Idle,      // 待机
    Walk,      // 移动
    Attack,    // 攻击容器
    Die,       // 死亡
    Attack1,   // 轻攻击
    Attack2,   // 连击
    Attack3,   // AOE 大招
}

public abstract class AIState
{
    protected AIStateMachine stateMachine;
    protected GameObject owner;

    public AIStateType StateType { get; protected set; }
    public string StateName => StateType.ToString();

    public AIState(AIStateMachine machine, GameObject owner, AIStateType type)
    {
        this.stateMachine = machine;
        this.owner = owner;
        this.StateType = type;
    }

    public virtual void OnEnter() { }
    public virtual void OnUpdate() { }
    public virtual void OnExit() { }

    // 返回下一个状态,null 表示不切换(给父层看)
    public virtual AIStateType? CheckTransitions() { return null; }
}

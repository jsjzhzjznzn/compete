using UnityEngine;

/// <summary>
/// AI 状态统一枚举：Root 层 + Attack 叶子
/// 行为树自定义任务切状态时也传这个枚举
/// </summary>
public enum AIStateType
{
    Root,      // 顶层容器（不可切换，仅内部使用）
    Idle,      // 待机
    Walk,      // 移动
    Hurt,      // 受击硬直
    Attack,    // 攻击（单状态，连招段由 AIComboData.attacks 索引决定）
    Die,       // 死亡
}

/// <summary>
/// AI 状态基类（分层状态机的叶子/容器都继承它）
/// 生命周期：OnEnter（进入时一次）→ OnUpdate（每帧）→ OnExit（离开时一次）
/// CheckTransitions 返回要切换到的兄弟状态,返回 null 表示不切换（由父层容器消费）
/// </summary>
public abstract class AIState
{
    protected AIStateMachine stateMachine;   // 所属状态机（读配置/切状态/播动画都走它）
    protected GameObject owner;              // 挂载状态机的角色物体（位移/朝向操作用）

    /// <summary>状态类型枚举（行为树切状态/查数据统一用它,不写字符串）</summary>
    public AIStateType StateType { get; protected set; }

    /// <summary>状态名（= 枚举转字符串,仅日志显示用）</summary>
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

    /// <summary>该状态是否已完成（攻击收招后为 true,行为树据此决定切去哪;默认 false）</summary>
    public virtual bool IsFinished => false;

    // 返回下一个状态,null 表示不切换(给父层看)
    public virtual AIStateType? CheckTransitions() { return null; }
}

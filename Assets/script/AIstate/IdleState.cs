using UnityEngine;

/// <summary>
/// 待机状态:只负责播放待机动画。
/// 何时离开(巡逻/攻击/死亡)由行为树决定,这里不再自己找目标/切状态。
/// 死亡是反射转移,保留在状态机内部(必须无条件立即响应)。
/// </summary>
public class IdleState : AIState
{
    public IdleState(AIStateMachine m, GameObject o) : base(m, o, AIStateType.Idle) { }

    public override void OnEnter()
    {
        stateMachine.PlayAnim(StateType);
    }

    public override AIStateType? CheckTransitions()
    {
        return stateMachine.isDead ? AIStateType.Die : null;
    }
}

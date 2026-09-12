using UnityEngine;

/// <summary>
/// 死亡状态:播放死亡动画,播完停留。
/// 由 isDead 反射转移进入(AIPlayer 收到 E_OnDeath 后 TriggerDeath),行为树不需要管。
/// </summary>
public class DieState : AIState
{
    private float timer;
    private const float dieAnimDuration = 2f;

    public DieState(AIStateMachine m, GameObject o) : base(m, o, AIStateType.Die) { }

    public override void OnEnter()
    {
        timer = 0f;
        stateMachine.PlayAnim(StateType);
        Debug.Log("[State] Die OnEnter");
    }

    public override void OnUpdate()
    {
        timer += Time.deltaTime;
        if (timer >= dieAnimDuration)
        {
            // Destroy(owner);
        }
    }

    public override AIStateType? CheckTransitions() => null;
}

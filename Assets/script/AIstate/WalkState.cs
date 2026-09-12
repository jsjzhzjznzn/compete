using UnityEngine;

/// <summary>
/// 移动状态:有目标追目标,没目标随机巡逻。
/// 何时停止/进攻击由行为树决定,这里只执行移动,不再自己切状态。
/// 死亡是反射转移,保留在状态机内部。
/// </summary>
public class WalkState : AIState
{
    private Vector3 wanderTarget;
    private float wanderTimer;

    public WalkState(AIStateMachine m, GameObject o) : base(m, o, AIStateType.Walk) { }

    public override void OnEnter()
    {
        stateMachine.PlayAnim(StateType);
        wanderTimer = 0f;
        PickRandomWanderPoint();
    }

    private void PickRandomWanderPoint()
    {
        Vector3 rnd = Random.insideUnitSphere * 5f;
        rnd.y = 0;
        wanderTarget = owner.transform.position + rnd;
    }

    public override void OnUpdate()
    {
        if (stateMachine.target != null)
        {
            MoveTowards(stateMachine.target.position);
        }
        else
        {
            wanderTimer += Time.deltaTime;
            MoveTowards(wanderTarget);

            // 到点/超时换一个巡逻点(只换目标点,不切状态)
            if (Vector3.Distance(owner.transform.position, wanderTarget) < 0.5f || wanderTimer > 3f)
            {
                wanderTimer = 0f;
                PickRandomWanderPoint();
            }
        }
    }

    private void MoveTowards(Vector3 dest)
    {
        Vector3 dir = dest - owner.transform.position;
        dir.y = 0;
        stateMachine.MoveHorizontal(dir, stateMachine.walkSpeed);

        if (dir.sqrMagnitude < 0.01f) return;

        owner.transform.rotation = Quaternion.Slerp(
            owner.transform.rotation,
            Quaternion.LookRotation(dir),
            Time.deltaTime * 10f);
    }

    public override AIStateType? CheckTransitions()
    {
        return stateMachine.isDead ? AIStateType.Die : null;
    }
}

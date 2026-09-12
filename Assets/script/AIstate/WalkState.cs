using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 移动状态:有目标用 NavMesh 寻路追击,没目标 NavMesh 随机巡逻。
/// 何时停止/进攻击由行为树决定,这里只执行移动,不再自己切状态。
/// 死亡是反射转移,保留在状态机内部。
/// 未挂 NavMeshAgent(或不在 NavMesh 上)时退回直线移动。
/// </summary>
public class WalkState : AIState
{
    private Vector3 wanderTarget;
    private float wanderTimer;

    private AIWalkData Data => stateMachine.GetStateData(AIStateType.Walk) as AIWalkData;
    private float WanderRadius => Data?.wanderRadius ?? 5f;
    private float RetargetTime => Data?.wanderRetargetTime ?? 3f;

    public WalkState(AIStateMachine m, GameObject o) : base(m, o, AIStateType.Walk) { }

    public override void OnEnter()
    {
        stateMachine.PlayAnim(StateType);
        wanderTimer = 0f;
        PickRandomWanderPoint();
    }

    public override void OnExit()
    {
        stateMachine.ResetNavPath();   // 清掉残留路径,防止下次进 Walk 直奔旧目标点
    }

    /// <summary>随机巡逻点:优先采样到 NavMesh 表面上(寻路才走得过去),失败则用原始随机点</summary>
    private void PickRandomWanderPoint()
    {
        Vector3 rnd = Random.insideUnitSphere * WanderRadius;
        rnd.y = 0;
        Vector3 raw = owner.transform.position + rnd;

        if (NavMesh.SamplePosition(raw, out NavMeshHit hit, WanderRadius, NavMesh.AllAreas))
            wanderTarget = hit.position;
        else
            wanderTarget = raw;
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
            if (Vector3.Distance(owner.transform.position, wanderTarget) < 0.5f || wanderTimer > RetargetTime)
            {
                wanderTimer = 0f;
                PickRandomWanderPoint();
            }
        }
    }

    /// <summary>
    /// 朝目标点移动(根运动方案):
    /// - 有 NavMeshAgent:只让它算路径(SetDestination),按路径方向转身;
    ///   位移完全交给动画根运动(基类 OnAnimatorMove → CharacterController),
    ///   agent.nextPosition 每帧回写保持代理与角色同步
    /// - 没挂 NavMeshAgent:退回直线移动(由代码驱动位移)
    /// </summary>
    private void MoveTowards(Vector3 dest)
    {
        Vector3 dir;
        var agent = stateMachine.NavAgent;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.SetDestination(dest);
            dir = agent.desiredVelocity;                    // 沿寻路路径的期望方向(只用来转身)
            if (dir.sqrMagnitude > 0.01f) dir.Normalize();
            agent.nextPosition = owner.transform.position;  // 同步代理位置,防止路径计算偏移
        }
        else
        {
            // 无寻路时的退路:直线方向 + 代码位移
            dir = dest - owner.transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f) dir.Normalize();
            stateMachine.MoveHorizontal(dir, stateMachine.walkSpeed);
        }

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

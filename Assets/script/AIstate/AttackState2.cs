using UnityEngine;

/// <summary>
/// 连击(两段):两段命中判定,收招后自动决定连击/追击/回待机。
/// 参数(动画/时长/伤害/范围)由 AIPlayerSO 的 attack2Data 配置,secondHitTime 为第二段命中时刻。
/// 进入 Attack 由行为树触发(EnterAttack);收招后的连击与离开属于执行层,保留在状态机内部。
/// </summary>
public class AttackState2 : AIState
{
    private AIHierarchicalState parent;
    private float timer;
    private int hitCount;           // 第几段命中
    private bool[] hitFlags = new bool[2];

    private AIAttackData Data => stateMachine.GetAttackData(StateType);
    private float Duration => Data?.duration ?? 1.2f;
    private float HitTime1 => Data?.hitTime ?? 0.35f;
    private float HitTime2 => Data?.secondHitTime ?? 0.75f;
    private float Range => Data?.range ?? 2.5f;
    private float Damage => Data?.damage ?? 12f;
    private float Cooldown => Data?.cooldown ?? 0.3f;

    public AttackState2(AIStateMachine m, GameObject o, AIHierarchicalState p)
        : base(m, o, AIStateType.Attack2) { parent = p; }

    public override void OnEnter()
    {
        timer = 0f;
        hitCount = 0;
        hitFlags[0] = hitFlags[1] = false;
        stateMachine.PlayAnim(StateType);
        Debug.Log("[Attack2] 连击开始");
    }

    public override void OnUpdate()
    {
        timer += Time.deltaTime;
        FaceTarget();

        // 两段命中判定
        if (!hitFlags[0] && timer >= HitTime1)
        {
            hitFlags[0] = true;
            DoHit(1);
        }
        if (!hitFlags[1] && timer >= HitTime2)
        {
            hitFlags[1] = true;
            DoHit(2);
        }

        if (timer >= Duration + Cooldown)
            DecideNext();
    }

    private void DoHit(int index)
    {
        if (stateMachine.TargetInRange(Range))
        {
            hitCount++;
            Debug.Log($"[Attack2] 第{index}段命中 {Damage} 伤害 (共 {hitCount} 段)");
        }
    }

    private void FaceTarget()
    {
        if (stateMachine.target == null) return;
        Vector3 dir = stateMachine.target.position - owner.transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
            owner.transform.rotation = Quaternion.Slerp(
                owner.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);
    }

    private void DecideNext()
    {
        if (stateMachine.target == null) { stateMachine.SwitchState(AIStateType.Idle); return; }
        float dist = Vector3.Distance(owner.transform.position, stateMachine.target.position);

        if (dist <= Range)
            parent.SwitchSubState(stateMachine.ChooseAttack());
        else if (dist <= stateMachine.chaseRange)
            stateMachine.SwitchState(AIStateType.Walk);
        else
            stateMachine.SwitchState(AIStateType.Idle);
    }

    public override AIStateType? CheckTransitions() => null;
}

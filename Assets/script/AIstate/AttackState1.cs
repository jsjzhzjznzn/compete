using UnityEngine;

/// <summary>
/// 轻攻击:单段攻击,收招后自动决定连击/追击/回待机。
/// 参数(动画/时长/伤害/范围)由 AIPlayerSO 的 attack1Data 配置。
/// 进入 Attack 由行为树触发(EnterAttack);收招后的连击与离开属于执行层,保留在状态机内部。
/// </summary>
public class AttackState1 : AIState
{
    private AIHierarchicalState parent;
    private float timer;
    private bool hasHit;

    private AIAttackData Data => stateMachine.GetAttackData(StateType);
    private float Duration => Data?.duration ?? 0.8f;
    private float HitTime => Data?.hitTime ?? 0.35f;
    private float Range => Data?.range ?? 2f;
    private float Damage => Data?.damage ?? 10f;
    private float Cooldown => Data?.cooldown ?? 0.2f;

    public AttackState1(AIStateMachine m, GameObject o, AIHierarchicalState p)
        : base(m, o, AIStateType.Attack1) { parent = p; }

    public override void OnEnter()
    {
        timer = 0f; hasHit = false;
        stateMachine.PlayAnim(StateType);
        Debug.Log("[Attack1] 轻攻击");
    }

    public override void OnUpdate()
    {
        timer += Time.deltaTime;
        FaceTarget();

        if (!hasHit && timer >= HitTime)
        {
            hasHit = true;
            if (stateMachine.TargetInRange(Range))
                Debug.Log($"[Attack1] 命中 {Damage} 伤害");
        }

        if (timer >= Duration + Cooldown)
            DecideNext();
    }

    private void FaceTarget()
    {
        if (stateMachine.target == null) return;
        Vector3 dir = stateMachine.target.position - owner.transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
            owner.transform.rotation = Quaternion.Slerp(
                owner.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
    }

    private void DecideNext()
    {
        // 目标没了 → 离开 Attack
        if (stateMachine.target == null)
        {
            stateMachine.SwitchState(AIStateType.Idle);
            return;
        }

        float dist = Vector3.Distance(owner.transform.position, stateMachine.target.position);

        if (dist <= Range)
        {
            // 还在范围 → 换一个攻击子状态(连击)
            parent.SwitchSubState(stateMachine.ChooseAttack());
        }
        else if (dist <= stateMachine.chaseRange)
        {
            stateMachine.SwitchState(AIStateType.Walk);
        }
        else
        {
            stateMachine.SwitchState(AIStateType.Idle);
        }
    }

    public override AIStateType? CheckTransitions()
    {
        // 死亡交给顶层;子状态之间切换由 DecideNext 处理
        return null;
    }
}

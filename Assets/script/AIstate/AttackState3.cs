using UnityEngine;

/// <summary>
/// AOE 大招:前摇蓄力后范围伤害,收招后自动决定连击/追击/回待机。
/// 参数(动画/时长/伤害/AOE半径)由 AIPlayerSO 的 attack3Data 配置。
/// 进入 Attack 由行为树触发(EnterAttack);收招后的连击与离开属于执行层,保留在状态机内部。
/// </summary>
public class AttackState3 : AIState
{
    private AIHierarchicalState parent;
    private float timer;
    private bool hasHit;

    private AIAttackData Data => stateMachine.GetAttackData(StateType);
    private float Duration => Data?.duration ?? 1.6f;
    private float HitTime => Data?.hitTime ?? 0.8f;    // 前摇后释放
    private float AoeRadius => Data?.aoeRadius ?? 4f;
    private float Damage => Data?.damage ?? 35f;
    private float Cooldown => Data?.cooldown ?? 0.8f;

    public AttackState3(AIStateMachine m, GameObject o, AIHierarchicalState p)
        : base(m, o, AIStateType.Attack3) { parent = p; }

    public override void OnEnter()
    {
        timer = 0f; hasHit = false;
        stateMachine.PlayAnim(StateType);
        Debug.Log("[Attack3] 大招蓄力...");
    }

    public override void OnUpdate()
    {
        timer += Time.deltaTime;

        // 前摇阶段跟踪目标转向
        if (timer < HitTime && stateMachine.target != null)
        {
            Vector3 dir = stateMachine.target.position - owner.transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                owner.transform.rotation = Quaternion.Slerp(
                    owner.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
        }

        // 释放 AOE
        if (!hasHit && timer >= HitTime)
        {
            hasHit = true;
            DoAoeHit();
        }

        if (timer >= Duration + Cooldown)
            DecideNext();
    }

    private void DoAoeHit()
    {
        Debug.Log($"[Attack3] 释放!范围 {AoeRadius} 内造成 {Damage} 伤害");
        Collider[] hits = Physics.OverlapSphere(owner.transform.position, AoeRadius);
        foreach (var h in hits)
        {
            if (h.gameObject == owner) continue;
            if (h.CompareTag(stateMachine.targetTag))
            {
                Debug.Log($"[Attack3] 命中 {h.name}");
                // h.GetComponent<HealthModel>()?.TakeDamage(Damage, owner);
            }
        }
    }

    private void DecideNext()
    {
        if (stateMachine.target == null) { stateMachine.SwitchState(AIStateType.Idle); return; }
        float dist = Vector3.Distance(owner.transform.position, stateMachine.target.position);

        if (dist <= 2.5f)
            parent.SwitchSubState(stateMachine.ChooseAttack());
        else if (dist <= stateMachine.chaseRange)
            stateMachine.SwitchState(AIStateType.Walk);
        else
            stateMachine.SwitchState(AIStateType.Idle);
    }

    public override AIStateType? CheckTransitions() => null;
}

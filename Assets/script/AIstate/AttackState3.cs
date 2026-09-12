using UnityEngine;

/// <summary>
/// AOE 大招:前摇蓄力后范围伤害,收招后停在本状态并标记 IsFinished,由行为树决定去留。
/// 参数(动画/时长/伤害/AOE半径)由 AIPlayerSO 的 attack3Data 配置。
/// 进入/连招/离开全部由行为树触发(EnterAttack / SwitchState),本状态不主动切任何状态。
/// </summary>
public class AttackState3 : AIState
{
    // ============ 运行时状态 ============
    private float timer;    // 本段攻击已进行时长
    private bool hasHit;    // AOE 是否已释放(一次蓄力只放一次)
    private bool finished;  // 收招完成标记(置位后停在这里,等行为树切走)

    // ============ 攻击参数（全部读 AIPlayerSO.attack3Data,未配置用代码默认值） ============
    private AIAttackData Data => stateMachine.GetAttackData(StateType);
    private float Duration => Data?.duration ?? 1.6f;      // 整个攻击动作总时长(含前后摇)
    private float HitTime => Data?.hitTime ?? 0.8f;        // 前摇结束/释放时刻
    private float AoeRadius => Data?.aoeRadius ?? 4f;      // AOE 半径(米)
    private float Damage => Data?.damage ?? 35f;           // 伤害
    private float Cooldown => Data?.cooldown ?? 0.8f;      // 收招后的额外冷却

    public override bool IsFinished => finished;

    public AttackState3(AIStateMachine m, GameObject o)
        : base(m, o, AIStateType.Attack3) { }

    /// <summary>进入大招:复位计时/释放标记/完成标记,播放蓄力动画</summary>
    public override void OnEnter()
    {
        timer = 0f; hasHit = false; finished = false;
        stateMachine.PlayAnim(StateType);
        Debug.Log("[Attack3] 大招蓄力...");
    }

    /// <summary>
    /// 每帧流程:计时 → 到释放时刻放 AOE → 动作+冷却播完收招(只标记,不切状态)。
    /// 朝向由行为树的 FaceTarget 节点负责(前摇阶段想锁敌就让 FaceTarget 在此期间持续调用),状态不管转向
    /// </summary>
    public override void OnUpdate()
    {
        if (finished) return;   // 已收招:停在原地,去留由行为树决定

        timer += Time.deltaTime;

        // 释放 AOE
        // TODO: 接入真实伤害(h.GetComponent<HealthModel>().TakeDamage(Damage, owner))
        if (!hasHit && timer >= HitTime)
        {
            hasHit = true;
            DoAoeHit();
        }

        // 动作时长 + 冷却都走完 → 收招
        if (timer >= Duration + Cooldown)
            FinishAttack();
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
            }
        }
    }

    /// <summary>
    /// 收招:标记完成 + 通知状态机(连招超时计时从这里起算),不切任何状态。
    /// 行为树下一帧读到 IsFinished=true:范围内 → EnterAttack(ChooseAttack())接下一招;
    /// 范围外 → Chase/Idle 分支接管
    /// </summary>
    private void FinishAttack()
    {
        finished = true;
        stateMachine.NotifyAttackEnded();
    }

    public override AIStateType? CheckTransitions()
    {
        // 死亡反射转移(isDead→Die)由 Root 层统一消费;其余切换全部交给行为树
        return null;
    }
}

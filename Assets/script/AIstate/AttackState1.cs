using UnityEngine;

/// <summary>
/// 轻攻击:单段攻击,收招后停在本状态并标记 IsFinished,由行为树决定去留。
/// 参数(动画/时长/伤害/范围)由 AIPlayerSO 的 attack1Data 配置。
/// 进入/连招/离开全部由行为树触发(EnterAttack / SwitchState),本状态不主动切任何状态。
/// </summary>
public class AttackState1 : AIState
{
    // ============ 运行时状态 ============
    private float timer;                  // 本段攻击已进行时长
    private bool hasHit;                  // 本次攻击是否已判定过命中(一段只判一次)
    private bool finished;                // 收招完成标记(置位后停在这里,等行为树切走)

    // ============ 攻击参数（全部读 AIPlayerSO.attack1Data,未配置用代码默认值） ============
    private AIAttackData Data => stateMachine.GetAttackData(StateType);
    private float Duration => Data?.duration ?? 0.8f;      // 整个攻击动作总时长
    private float HitTime => Data?.hitTime ?? 0.35f;       // 命中判定时刻(动画播到这秒出伤害)
    private float Range => Data?.range ?? 2f;              // 攻击范围(米)
    private float Damage => Data?.damage ?? 10f;           // 伤害
    private float Cooldown => Data?.cooldown ?? 0.2f;      // 收招后的额外冷却

    public override bool IsFinished => finished;

    public AttackState1(AIStateMachine m, GameObject o)
        : base(m, o, AIStateType.Attack1) { }

    /// <summary>进入轻攻击:复位计时/命中标记/完成标记,播放攻击动画</summary>
    public override void OnEnter()
    {
        timer = 0f; hasHit = false; finished = false;
        stateMachine.PlayAnim(StateType);
        Debug.Log("[Attack1] 轻攻击");
    }

    /// <summary>
    /// 每帧流程:计时 → 到命中帧判定伤害 → 动作+冷却播完收招(只标记,不切状态)。
    /// 朝向由行为树的 FaceTarget 节点负责,状态不管转向
    /// </summary>
    public override void OnUpdate()
    {
        if (finished) return;   // 已收招:停在原地,去留由行为树决定

        timer += Time.deltaTime;

        // 命中帧:到 HitTime 时刻判定一次,目标在范围内则造成伤害
        // TODO: 接入真实伤害(target.GetComponent<HealthModel>().TakeDamage(Damage, owner))
        if (!hasHit && timer >= HitTime)
        {
            hasHit = true;
            if (stateMachine.TargetInRange(Range))
                Debug.Log($"[Attack1] 命中 {Damage} 伤害");
        }

        // 动作时长 + 冷却都走完 → 收招
        if (timer >= Duration + Cooldown)
            FinishAttack();
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

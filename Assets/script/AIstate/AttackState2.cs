using UnityEngine;

/// <summary>
/// 连击(两段):两段命中判定,收招后停在本状态并标记 IsFinished,由行为树决定去留。
/// 参数(动画/时长/伤害/范围)由 AIPlayerSO 的 attack2Data 配置,secondHitTime 为第二段命中时刻。
/// 进入/连招/离开全部由行为树触发(EnterAttack / SwitchState),本状态不主动切任何状态。
/// </summary>
public class AttackState2 : AIState
{
    // ============ 运行时状态 ============
    private float timer;                   // 本段攻击已进行时长
    private int hitCount;                  // 实际命中的段数
    private bool[] hitFlags = new bool[2]; // 两段各自的命中判定标记(每段只判一次)
    private bool finished;                 // 收招完成标记(置位后停在这里,等行为树切走)

    // ============ 攻击参数（全部读 AIPlayerSO.attack2Data,未配置用代码默认值） ============
    private AIAttackData Data => stateMachine.GetAttackData(StateType);
    private float Duration => Data?.duration ?? 1.2f;      // 整个攻击动作总时长
    private float HitTime1 => Data?.hitTime ?? 0.35f;      // 第一段命中时刻
    private float HitTime2 => Data?.secondHitTime ?? 0.75f;// 第二段命中时刻
    private float Range => Data?.range ?? 2.5f;            // 攻击范围(米)
    private float Damage => Data?.damage ?? 12f;           // 每段伤害
    private float Cooldown => Data?.cooldown ?? 0.3f;      // 收招后的额外冷却

    public override bool IsFinished => finished;

    public AttackState2(AIStateMachine m, GameObject o)
        : base(m, o, AIStateType.Attack2) { }

    /// <summary>进入连击:复位计时/命中标记/完成标记,播放攻击动画</summary>
    public override void OnEnter()
    {
        timer = 0f;
        hitCount = 0;
        hitFlags[0] = hitFlags[1] = false;
        finished = false;
        stateMachine.PlayAnim(StateType);
        Debug.Log("[Attack2] 连击开始");
    }

    /// <summary>
    /// 每帧流程:计时 → 两段命中帧各判定一次 → 动作+冷却播完收招(只标记,不切状态)。
    /// 朝向由行为树的 FaceTarget 节点负责,状态不管转向
    /// </summary>
    public override void OnUpdate()
    {
        if (finished) return;   // 已收招:停在原地,去留由行为树决定

        timer += Time.deltaTime;

        // 两段命中判定
        // TODO: 接入真实伤害(target.GetComponent<HealthModel>().TakeDamage(Damage, owner))
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

        // 动作时长 + 冷却都走完 → 收招
        if (timer >= Duration + Cooldown)
            FinishAttack();
    }

    private void DoHit(int index)
    {
        if (stateMachine.TargetInRange(Range))
        {
            hitCount++;
            Debug.Log($"[Attack2] 第{index}段命中 {Damage} 伤害 (共 {hitCount} 段)");
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

    /// <summary>攻击全程平滑转向目标(只在水平面转,不影响重力)</summary>
    private void FaceTarget()
    {
        if (stateMachine.target == null) return;
        Vector3 dir = stateMachine.target.position - owner.transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
            owner.transform.rotation = Quaternion.Slerp(
                owner.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);
    }

    public override AIStateType? CheckTransitions()
    {
        // 死亡反射转移(isDead→Die)由 Root 层统一消费;其余切换全部交给行为树
        return null;
    }
}

using UnityEngine;

/// <summary>
/// 连击(两段):两个命中帧各自结算一次伤害,收招后停在本状态并标记 IsFinished,由行为树决定去留。
/// 命中判定/收招时机由 Animancer 动画事件驱动(hitTime/secondHitTime 秒 → 归一化命中帧,OnEnd 收招),
/// 未配动画时退回秒表模式。
/// 参数由 AIPlayerSO 的 attack2Data 配置;进入/连招/离开全部由行为树触发。
/// </summary>
public class AttackState2 : AIState
{
    // ============ 运行时状态 ============
    private float timer;          // 已进行时长(冷却计时用)
    private int hitCount;         // 实际触发的命中段数
    private bool[] hitFlags;      // 秒表兜底模式的分段命中标记
    private bool animEnded;       // 动画播完标记(OnEnd 触发,之后进入冷却)
    private bool finished;        // 收招完成标记(置位后停在这里,等行为树切走)
    private float duration;       // 动画实际时长(= 片段长度/播放速度)
    private bool hasAnim;         // 是否成功走了 Animancer 事件模式

    // ============ 攻击参数（全部读 AIPlayerSO.attack2Data,未配置用代码默认值） ============
    private AIAttackData Data => stateMachine.GetAttackData(StateType);
    private float Duration => Data?.duration ?? 1.2f;       // 秒表兜底用:动作总时长
    private float HitTime1 => Data?.hitTime ?? 0.35f;       // 第一段命中时刻(秒)
    private float HitTime2 => Data?.secondHitTime ?? 0.75f; // 第二段命中时刻(秒)
    private float Range => Data?.range ?? 2.5f;             // 攻击范围(米)
    private float Damage => Data?.damage ?? 12f;            // 每段伤害
    private float Cooldown => Data?.cooldown ?? 0.3f;       // 动画播完后的额外冷却

    public override bool IsFinished => finished;

    public AttackState2(AIStateMachine m, GameObject o)
        : base(m, o, AIStateType.Attack2) { }

    /// <summary>进入连击:播动画并注册两个命中帧/收招事件;没配动画则退回秒表模式</summary>
    public override void OnEnter()
    {
        timer = 0f;
        hitCount = 0;
        hitFlags = new[] { false, false };
        animEnded = false;
        finished = false;

        var animState = stateMachine.PlayAttackAnim(StateType, new[] { HitTime1, HitTime2 }, OnHitFrame, OnAnimEnd);
        hasAnim = animState != null;
        if (hasAnim)
            duration = animState.Length / Mathf.Max(animState.Speed, 0.01f);
        else
            Debug.LogWarning("[Attack2] 未配置攻击动画,退回秒表模式");
    }

    /// <summary>
    /// 事件模式:两段命中/收招全由动画事件触发,这里只做"播完后冷却到点收招";
    /// 秒表兜底:计时 → 两段各自到点判定 → 收招
    /// </summary>
    public override void OnUpdate()
    {
        if (finished) return;   // 已收招:停在原地,去留由行为树决定

        timer += Time.deltaTime;

        if (hasAnim)
        {
            if (animEnded && timer >= duration + Cooldown)
                FinishAttack();
        }
        else
        {
            // 兜底:两段各自到点判定一次
            if (!hitFlags[0] && timer >= HitTime1)
            {
                hitFlags[0] = true;
                DoHit();
            }
            if (!hitFlags[1] && timer >= HitTime2)
            {
                hitFlags[1] = true;
                DoHit();
            }
            if (timer >= Duration + Cooldown)
                FinishAttack();
        }
    }

    /// <summary>Animancer 命中帧事件:走伤害管道结算本段伤害</summary>
    private void OnHitFrame()
    {
        if (finished) return;
        DoHit();
    }

    private void DoHit()
    {
        stateMachine.DealDamage(Range, Damage);
        hitCount++;
        Debug.Log($"[Attack2] 第{hitCount}段命中");
    }

    /// <summary>Animancer 播完事件:进入冷却阶段</summary>
    private void OnAnimEnd()
    {
        animEnded = true;
    }

    /// <summary>收招:标记完成 + 通知状态机(连招超时计时从这里起算),不切任何状态</summary>
    private void FinishAttack()
    {
        finished = true;
        stateMachine.NotifyAttackEnded();
    }

    public override AIStateType? CheckTransitions()
    {
        return null;   // 死亡反射转移由 Root 层消费;其余切换交给行为树
    }
}

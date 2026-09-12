using UnityEngine;

/// <summary>
/// 轻攻击:单段攻击,收招后停在本状态并标记 IsFinished,由行为树决定去留。
/// 命中判定/收招时机由 Animancer 动画事件驱动(hitTime 秒 → 归一化命中帧,OnEnd 收招),
/// 未配动画时退回秒表模式(hitTime/duration 计时)。
/// 参数由 AIPlayerSO 的 attack1Data 配置;进入/连招/离开全部由行为树触发。
/// </summary>
public class AttackState1 : AIState
{
    // ============ 运行时状态 ============
    private float timer;        // 已进行时长(冷却计时用)
    private bool hasHit;        // 秒表兜底模式的命中标记(一段只判一次)
    private bool animEnded;     // 动画播完标记(OnEnd 触发,之后进入冷却)
    private bool finished;      // 收招完成标记(置位后停在这里,等行为树切走)
    private float duration;     // 动画实际时长(= 片段长度/播放速度)
    private bool hasAnim;       // 是否成功走了 Animancer 事件模式

    // ============ 攻击参数（全部读 AIPlayerSO.attack1Data,未配置用代码默认值） ============
    private AIAttackData Data => stateMachine.GetAttackData(StateType);
    private float Duration => Data?.duration ?? 0.8f;      // 秒表兜底用:动作总时长
    private float HitTime => Data?.hitTime ?? 0.35f;       // 命中判定时刻(秒,换算成动画归一化命中帧)
    private float Range => Data?.range ?? 2f;              // 攻击范围(米)
    private float Damage => Data?.damage ?? 10f;           // 伤害
    private float Cooldown => Data?.cooldown ?? 0.2f;      // 动画播完后的额外冷却

    public override bool IsFinished => finished;

    public AttackState1(AIStateMachine m, GameObject o)
        : base(m, o, AIStateType.Attack1) { }

    /// <summary>进入轻攻击:播动画并注册命中帧/收招事件;没配动画则退回秒表模式</summary>
    public override void OnEnter()
    {
        timer = 0f; hasHit = false; finished = false; animEnded = false;

        var animState = stateMachine.PlayAttackAnim(StateType, new[] { HitTime }, OnHitFrame, OnAnimEnd);
        hasAnim = animState != null;
        if (hasAnim)
            duration = animState.Length / Mathf.Max(animState.Speed, 0.01f);
        else
            Debug.LogWarning("[Attack1] 未配置攻击动画,退回秒表模式");
    }

    /// <summary>
    /// 事件模式:命中/收招全由动画事件触发,这里只做"播完后冷却到点收招";
    /// 秒表兜底:计时 → 到点判定 → 收招。朝向由行为树的 FaceTarget 节点负责
    /// </summary>
    public override void OnUpdate()
    {
        if (finished) return;   // 已收招:停在原地,去留由行为树决定

        timer += Time.deltaTime;

        if (hasAnim)
        {
            // OnEnd(动画播完)之后走 Cooldown 冷却,到点收招
            if (animEnded && timer >= duration + Cooldown)
                FinishAttack();
        }
        else
        {
            // 兜底:到命中时刻判定一次
            if (!hasHit && timer >= HitTime)
            {
                hasHit = true;
                stateMachine.DealDamage(Range, Damage);
            }
            if (timer >= Duration + Cooldown)
                FinishAttack();
        }
    }

    /// <summary>Animancer 命中帧事件:走伤害管道结算一次</summary>
    private void OnHitFrame()
    {
        if (finished) return;   // 极端情况:收招后残留事件不再结算
        stateMachine.DealDamage(Range, Damage);
    }

    /// <summary>Animancer 播完事件:进入冷却阶段</summary>
    private void OnAnimEnd()
    {
        animEnded = true;
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

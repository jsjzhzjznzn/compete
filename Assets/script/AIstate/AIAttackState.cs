using UnityEngine;

/// <summary>
/// 攻击状态（单状态，替代原 Attack1/2/3）：
/// 打哪一段由 AIStateMachine 的连招索引决定（行为树每段触发 EnterAttack(ChooseAttack())）。
/// 一段的形态完全由 AIAttackData 描述：
/// - hitTimes 列表 → 单段/多段命中通用
/// - isAoe → 命中帧走范围伤害
/// 命中/收招时机由 Animancer 动画事件驱动（hitTimes 秒 → 归一化命中帧，OnEnd 收招），
/// 未配动画时退回秒表模式。收招后停在本状态并标记 IsFinished，由行为树决定去留。
/// </summary>
public class AIAttackState : AIState
{
    // ============ 运行时状态 ============
    private float timer;        // 已进行时长(冷却计时用)
    private bool[] hitFlags;    // 秒表兜底模式的分段命中标记
    private bool animEnded;     // 动画播完标记(OnEnd 触发,之后进入冷却)
    private bool finished;      // 收招完成标记(置位后停在这里,等行为树切走)
    private float duration;     // 动画实际时长(= 片段长度/播放速度)
    private bool hasAnim;       // 是否成功走了 Animancer 事件模式

    // ============ 当前段数据（由状态机连招索引决定） ============
    private AIAttackData Data => stateMachine.CurrentAttackSegment;
    private float Duration => Data?.duration ?? 0.8f;      // 秒表兜底用:动作总时长
    private float Cooldown => Data?.cooldown ?? 0.2f;      // 动画播完后的额外冷却

    public override bool IsFinished => finished;

    public AIAttackState(AIStateMachine m, GameObject o)
        : base(m, o, AIStateType.Attack) { }

    /// <summary>进入本段攻击:播动画并注册命中帧/收招事件;没配动画则退回秒表模式</summary>
    public override void OnEnter()
    {
        timer = 0f; animEnded = false; finished = false; hitFlags = null;

        var data = Data;
        if (data == null)
        {
            // 没配攻击段(comboData.attacks 为空/索引越界):直接判完成,避免卡死在攻击态
            Debug.LogWarning("[AI] 攻击段数据为空(comboData.attacks 未配置)");
            FinishAttack();
            return;
        }

        var animState = stateMachine.PlayAttackAnim(data, OnHitFrame, OnAnimEnd);
        hasAnim = animState != null;
        if (hasAnim)
        {
            duration = animState.Length / Mathf.Max(animState.Speed, 0.01f);
        }
        else
        {
            Debug.LogWarning("[AI] 未配置攻击动画,退回秒表模式");
            hitFlags = new bool[data.hitTimes != null ? data.hitTimes.Count : 0];
        }
    }

    /// <summary>
    /// 事件模式:命中/收招全由动画事件触发,这里只做"播完后冷却到点收招";
    /// 秒表兜底:按 hitTimes 逐个到点判定 → 收招。朝向由行为树的 FaceTarget 节点负责
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
            var hitTimes = Data?.hitTimes;
            if (hitTimes != null && hitFlags != null)
            {
                for (int i = 0; i < hitTimes.Count && i < hitFlags.Length; i++)
                {
                    if (!hitFlags[i] && timer >= hitTimes[i])
                    {
                        hitFlags[i] = true;
                        DoHit();
                    }
                }
            }
            if (timer >= Duration + Cooldown)
                FinishAttack();
        }
    }

    /// <summary>Animancer 命中帧事件:按本段配置走单体/AOE 伤害结算</summary>
    private void OnHitFrame()
    {
        if (finished) return;   // 极端情况:收招后残留事件不再结算
        DoHit();
    }

    private void DoHit()
    {
        var data = Data;
        if (data == null) return;

        if (data.isAoe)
            stateMachine.DealAoeDamage(data.aoeRadius, data.damage);
        else
            stateMachine.DealDamage(data.range, data.damage);
    }

    /// <summary>Animancer 播完事件:进入冷却阶段</summary>
    private void OnAnimEnd()
    {
        animEnded = true;
    }

    /// <summary>
    /// 收招:标记完成 + 通知状态机(连招超时计时从这里起算),不切任何状态。
    /// 行为树下一帧读到 IsFinished=true:范围内 → EnterAttack(ChooseAttack())接下一段;
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

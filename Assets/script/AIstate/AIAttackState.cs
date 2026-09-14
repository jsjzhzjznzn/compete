using UnityEngine;

/// <summary>
/// 攻击状态（单状态，替代原 Attack1/2/3）：
/// 打哪一段由 AIStateMachine 的连招索引决定（行为树每段触发 EnterAttack(ChooseAttack())）。
/// 一段的形态完全由 AIAttackData 描述：
/// - hitTimes 列表 → 单段/多段命中通用
/// - isAoe → 命中帧走范围伤害
/// 命中/收招时机由 Animancer 动画事件驱动（hitTimes 归一化比例(0~1) → 命中帧，OnEnd 收招），
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

        WarnIfUnconfigured(data);

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
    /// 秒表兜底:按 hitTimes(归一化比例 × duration) 逐个到点判定 → 收招。朝向由行为树的 FaceTarget 节点负责
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
                    if (!hitFlags[i] && timer >= hitTimes[i] * Duration)   // hitTimes 是归一化比例(0~1)，× 总时长换成到点时间
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
            stateMachine.DealAoeDamage(data.aoeRadius, data.damageMultiplier, data.hitAngle);
        else
            stateMachine.DealDamage(data.range, data.damageMultiplier, data.hitAngle);
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

    /// <summary>
    /// 兜底校验：攻击段关键参数为 0/空时告警。避免"AI 有攻击动作却打不出伤害/进不了攻击范围"这类**静默失效**
    /// （Unity 新建攻击段时字段常是 0，不会自动带 C# 默认值，极易漏配）。
    /// </summary>
    private static void WarnIfUnconfigured(AIAttackData data)
    {
        string issues = "";
        if (data.range <= 0f) issues += "\n  - range=0（单体命中判定 distance<=0，打不中；单体攻击请填 range）";
        if (data.hitTimes == null || data.hitTimes.Count == 0) issues += "\n  - hitTimes 为空（没有命中帧，不会结算伤害）";
        if (data.damageMultiplier <= 0f) issues += "\n  - damageMultiplier=0（命中倍率为 0，伤害会保底 1）";
        if (data.animationClip != null && data.playSpeed <= 0f) issues += "\n  - playSpeed=0（动画会冻住，攻击状态出不来）";
        if (data.animationClip == null && data.duration <= 0f) issues += "\n  - 没配动画且 duration=0（秒表兜底模式下立刻结束）";

        if (issues.Length > 0)
            Debug.LogWarning($"[AI] 攻击段参数没配好，请到 AIPlayerSO 的对应攻击段里填：{issues}");
    }

    public override AIStateType? CheckTransitions()
    {
        // 死亡反射转移(isDead→Die)由 Root 层统一消费;其余切换全部交给行为树
        return null;
    }
}

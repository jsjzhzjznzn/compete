using UnityEngine;

/// <summary>
/// 受击硬直:被打时打断当前动作,播受击动画并定身。
/// 硬直时长 = 受击动画本身的播放时长(动画播完即硬直结束),未配动画时退回 SO 里的 stunDuration。
/// 挂在 Root 层(与 Idle/Walk/Die 平级):任何状态下被打都要能立即切进来。
/// 硬直期间再次被打 → 通过 HurtHitCount 计数感知 → 重播受击动画并刷新硬直计时(重新执行自己,不是切状态)。
/// 硬直结束后只标记 IsFinished 停在原地,去留由行为树决定;死亡反射转移保留(isDead→Die)。
/// </summary>
public class HurtState : AIState
{
    // ============ 运行时状态 ============
    private float timer;          // 硬直已持续时长
    private float duration;       // 硬直总时长(= 受击动画长度/播放速度)
    private bool finished;        // 硬直结束标记(行为树据此接管)
    private int seenHitCount;     // 本状态已处理到的受击次数(对照 HurtHitCount 检测新受击)

    // ============ 兜底参数（SO 里未配受击动画时用） ============
    private AIHurtData Data => stateMachine.GetStateData(AIStateType.Hurt) as AIHurtData;
    private float FallbackDuration => Data?.stunDuration ?? 0.4f;

    public override bool IsFinished => finished;

    public HurtState(AIStateMachine m, GameObject o) : base(m, o, AIStateType.Hurt) { }

    /// <summary>进入硬直:同步受击计数,播受击动画(硬直时长 = 动画长度)</summary>
    public override void OnEnter()
    {
        seenHitCount = stateMachine.HurtHitCount;
        PlayHurt();
    }

    /// <summary>
    /// 硬直期间定身(状态不产生任何位移)。
    /// 检测到新受击 → 重播受击动画刷新硬直;
    /// 动画播完 → 清受击标记 + 标记完成,不切状态
    /// </summary>
    public override void OnUpdate()
    {
        // 硬直中再次被打:重播受击动画,重置计时(未播完被打=从头播;播完了被打=继续留在硬直里重新打)
        if (seenHitCount != stateMachine.HurtHitCount)
        {
            seenHitCount = stateMachine.HurtHitCount;
            PlayHurt();
        }

        if (finished) return;   // 硬直已结束:停在原地,去留由行为树决定

        timer += Time.deltaTime;
        if (timer >= duration)
        {
            finished = true;
            stateMachine.IsHurt = false;   // 受击结束:清标记,行为树的硬直分支随之退出
        }
    }

    /// <summary>播放/重播受击动画,硬直时长取动画长度(重置计时与完成标记)</summary>
    private void PlayHurt()
    {
        timer = 0f;
        finished = false;

        var animState = stateMachine.PlayAnim(StateType);
        if (animState != null)
        {
            // 墙钟时长 = 片段长度 / 播放速度(playSpeed 调速时硬直跟着动画走)
            float speed = Mathf.Max(animState.Speed, 0.01f);
            duration = animState.Length / speed;
        }
        else
        {
            duration = FallbackDuration;
            Debug.LogWarning("[Hurt] 未配置受击动画,硬直时长退回 stunDuration 配置值");
        }

        Debug.Log($"[Hurt] 受击硬直 {duration:F2}s");
    }

    public override AIStateType? CheckTransitions()
    {
        // 死亡反射转移(isDead→Die)由 Root 层统一消费;其余切换全部交给行为树
        return null;
    }
}

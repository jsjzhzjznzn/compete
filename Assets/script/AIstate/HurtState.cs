using UnityEngine;

/// <summary>
/// 受击硬直:被打时打断当前动作,播受击动画并定身。
/// 没有独立僵直时长——受击动画播完(Animancer OnEnd)即硬直结束。
/// 挂在 Root 层(与 Idle/Walk/Die 平级):任何状态下被打都要能立即切进来。
/// 硬直期间再次被打 → 行为树 AIStunRecover 检测到新受击 → forceRestart 重入本节点(动画重播)。
/// 硬直结束后只标记 IsFinished 停在原地,去留由行为树决定;死亡反射转移保留(isDead→Die)。
/// </summary>
public class HurtState : AIState
{
    // ============ 运行时状态 ============
    private bool finished;   // 受击动画播完标记(行为树据此接管)

    public override bool IsFinished => finished;

    public HurtState(AIStateMachine m, GameObject o) : base(m, o, AIStateType.Hurt) { }

    /// <summary>进入硬直:播受击动画,Animancer OnEnd 触发结束(每次进入都重播,含 forceRestart 重入)</summary>
    public override void OnEnter()
    {
        finished = false;
        Debug.Log($"[AI][诊断] Hurt.OnEnter (受击计数={stateMachine.HurtHitCount})");

        var animState = stateMachine.ReplayAnim(StateType);
        if (animState == null)
        {
            // 没配受击动画:无法定义"播完",硬直立即结束(仅警告提示去配 AIPlayerSO)
            Debug.LogWarning("[Hurt] 未配置受击动画,硬直立即结束");
            finished = true;
            stateMachine.IsHurt = false;
            return;
        }

        // 同一片段复用同一 AnimancerState,清掉残留事件再挂本次的 OnEnd
        animState.Events.Clear();
        animState.Events.OnEnd = OnAnimEnd;
    }

    /// <summary>硬直期间定身(状态不产生任何位移),结束全由 Animancer OnEnd 事件驱动,无计时逻辑</summary>
    public override void OnUpdate() { }

    /// <summary>Animancer 播完事件:硬直结束,清受击标记,行为树接管去留</summary>
    private void OnAnimEnd()
    {
        finished = true;
        stateMachine.IsHurt = false;   // 受击结束:清标记,行为树的硬直分支随之退出
    }

    public override AIStateType? CheckTransitions()
    {
        // 死亡反射转移(isDead→Die)由 Root 层统一消费;其余切换全部交给行为树
        return null;
    }
}

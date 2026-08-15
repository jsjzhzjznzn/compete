using UnityEngine.InputSystem;

/// <summary>
/// 技能执行状态
/// 播放当前技能（currentSkill）的攻击动画，播完回待机。
///
/// 触发方式：PlayerNullState.OnSkillInput 按下技能键后，
/// 会把 reusableData.currentSkill 填上配置里的技能数据，再切到本状态。
///
/// 职责：
/// - 进入时校验技能数据，非法则兜底回待机（防止空引用/黑屏卡死）
/// - 用 Animancer 播放技能的攻击动画，并在播完回调里回待机
/// </summary>
public class PlayerSkillState : PlayerComboState
{
    public PlayerSkillState(PlayerComboStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        // 订阅本状态要用的输入事件（attack / heavyattk / skill）
        base.Enter();

        // 取出共享数据里的当前技能（PlayerNullState 在切状态前填好的）
        var skill = reusableData.currentSkill;

        // 防御性校验：技能数据为空，或没配攻击动画 → 直接回待机，不播任何东西
        if (skill == null || skill.attackClip == null)
        {
            stateMachine.SwitchState(stateMachine.NullState);
            return;
        }

        // 用 Animancer 播放技能的攻击动画（ClipTransition），返回 AnimancerState
        var state = player.characterAnimancer.Play(skill.attackClip);

        // 挂播完回调：动画走到最后一帧时触发 OnSkillAnimationEnd
        state.Events.OnEnd = OnSkillAnimationEnd;
    }

    /// <summary>技能动画播完 → 回待机</summary>
    private void OnSkillAnimationEnd()
    {
        // 技能播完回到连击空状态，等待下一次输入
        stateMachine.SwitchState(stateMachine.NullState);
    }
}

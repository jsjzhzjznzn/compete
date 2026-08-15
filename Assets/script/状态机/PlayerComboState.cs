using Animancer;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家连击状态基类
/// 子类构造时传入连击状态机，通过 stateMachine 访问 player / reusableData
///
/// 连击流程：
/// - OnEnter 订阅攻击输入事件，OnExit 退订
/// - 攻击键按下 → hasATKCommand 输入缓冲（子类在动画播完时消费）
/// - PlayAttackClip 播放段动画并挂 OnEnd，播完进 OnAnimationEndEvent，由子类决定连下一段还是收招
/// - PlayAttackEndClip 播放收尾动画，播完自动回待机
/// </summary>
public abstract class PlayerComboState : IState
{
    protected readonly PlayerComboStateMachine stateMachine;
    protected readonly Player player;
    protected readonly PlayerComboReusableData reusableData;

    protected PlayerComboState(PlayerComboStateMachine stateMachine)
    {
        this.stateMachine = stateMachine;
        player = stateMachine.Player;
        reusableData = stateMachine.ReusableData;
    }

    // ==================== IState ====================

    public virtual void Enter()
    {
        AddInputActionCallBacks();
    }

    public virtual void Update()
    {
        HandInput();
    }

    public virtual void Exit()
    {
        RemoveInputActionCallBacks();
        ClearAnimationEnd();
    }

    // ==================== 输入事件订阅 ====================

    protected virtual void AddInputActionCallBacks()
    {
        CharacterInputSystem.MainInstance.inputActions.player.attack.started += OnAttackInput;
        CharacterInputSystem.MainInstance.inputActions.player.heavyattk.started += OnHeavyAttackInput;
        CharacterInputSystem.MainInstance.inputActions.player.skill.started += OnSkillInput;
    }

    protected virtual void RemoveInputActionCallBacks()
    {
        CharacterInputSystem.MainInstance.inputActions.player.attack.started -= OnAttackInput;
        CharacterInputSystem.MainInstance.inputActions.player.heavyattk.started -= OnHeavyAttackInput;
        CharacterInputSystem.MainInstance.inputActions.player.skill.started -= OnSkillInput;
    }

    // ==================== 输入回调（子类按需覆写） ====================

    /// <summary>轻攻击按下：处于可输入窗口则写入攻击指令（输入缓冲）</summary>
    protected virtual void OnAttackInput(InputAction.CallbackContext context)
    {
        if (!reusableData.canInput) return;
        reusableData.hasATKCommand = true;
    }

    /// <summary>重攻击按下</summary>
    protected virtual void OnHeavyAttackInput(InputAction.CallbackContext context) { }

    /// <summary>技能按下</summary>
    protected virtual void OnSkillInput(InputAction.CallbackContext context) { }

    /// <summary>每帧输入轮询，子类覆写做状态切换判断</summary>
    protected virtual void HandInput() { }

    // ==================== 动画播放辅助 ====================

    /// <summary>播放当前段的攻击动画，播完触发 OnAnimationEndEvent</summary>
    protected void PlayAttackClip()
    {
        var transition = reusableData.currentCombo?.GetAttackClip(reusableData.ATKIndex);
        if (transition == null) return;

        var state = player.characterAnimancer.Play(transition);
        state.Events.OnEnd = OnAnimationEndEvent;
    }

    /// <summary>播放当前段的收尾动画，播完自动回待机</summary>
    protected void PlayAttackEndClip()
    {
        var transition = reusableData.currentCombo?.GetAttackEndClip(reusableData.ATKIndex);
        if (transition == null) return;

        var state = player.characterAnimancer.Play(transition);
        state.Events.OnEnd = OnRecoveryEnd;
    }

    /// <summary>设置当前段数，并同步通知动画/UI 下标（currentIndex）</summary>
    protected void SetATKIndex(int index)
    {
        reusableData.ATKIndex = index;
        reusableData.currentIndex.Value = index;
    }

    /// <summary>清除动画结束回调，避免切换状态后误触发</summary>
    protected void ClearAnimationEnd()
    {
        var state = player.characterAnimancer.States.Current;
        if (state != null) state.Events.OnEnd = null;
    }

    // ==================== 动画事件（子类覆写） ====================

    /// <summary>本段攻击动画播完：子类在此判断连下一段 or 进收招</summary>
    protected virtual void OnAnimationEndEvent() { }

    /// <summary>收尾动画播完：回到待机（子类可覆写为其它行为）</summary>
    protected virtual void OnRecoveryEnd()
    {
        stateMachine.SwitchState(stateMachine.NullState);
    }

    /// <summary>动画事件触发的强制状态切换（保留原接口）</summary>
    public virtual void OnAnimationTranslateEvent(IState state)
    {
        stateMachine.SwitchState(state);
    }
}

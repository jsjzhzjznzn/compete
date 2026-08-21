using Animancer;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家连击状态基类
/// 子类构造时传入连击状态机，通过 stateMachine 访问 player / reusableData
///
/// 连击流程：
/// - OnEnter 订阅攻击输入事件，OnExit 退订
/// - PlayAttackClip 播放段动画：前段（linkCancelTime 归一化时间，默认 60%）打开输入窗口，
///   窗口内按键 → hasATKCommand 缓冲；到检查点 OnLinkCheckpoint 由子类统一消费（切下一段）
/// - 动画播完进 OnAnimationEndEvent，由子类决定连下一段还是收招
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
        CharacterInputSystem.MainInstance.inputActions.player.defense.started += OnHeavyAttackInput;
        CharacterInputSystem.MainInstance.inputActions.player.skill.started += OnSkillInput;
    }

    protected virtual void RemoveInputActionCallBacks()
    {
        CharacterInputSystem.MainInstance.inputActions.player.attack.started -= OnAttackInput;
        CharacterInputSystem.MainInstance.inputActions.player.defense.started -= OnHeavyAttackInput;
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

    /// <summary>
    /// 播放当前段的攻击动画，播完触发 OnAnimationEndEvent
    /// 连击缓冲：本段配置了 linkCancelTime（0~1 归一化时间，默认 0.6），
    /// 动画前段打开输入窗口，窗口内按键只写 hasATKCommand（不立即出手），
    /// 到检查点 OnLinkCheckpoint 时由子类统一决定是否切下一段
    /// </summary>
    protected void PlayAttackClip()
    {
        var combo = reusableData.currentCombo;
        if (combo == null) return;

        var transition = combo.GetAttackClip(reusableData.currentIndex.Value);
        if (transition == null) return;

        var state = player.characterAnimancer.Play(transition);

        // 清除该状态上可能残留的动画事件：同一 clip 被重复播放时会复用同一个 state，
        // 不清除会导致检查点/打击帧回调重复注册、同一帧被多次触发
        state.Events.Clear();
        state.Events.OnEnd = OnAnimationEndEvent;

        // 触发本段的角色语音与武器挥砍音效
        var data = combo.GetComboData(reusableData.currentIndex.Value);
        stateMachine.characterCombo.PlayCharacterVoice(data);
        stateMachine.characterCombo.PlayWeaponSound(data);
        // 触发本段的攻击刀光特效（电光蓝弧线，挂到武器骨骼跟随挥砍）
        //stateMachine.characterCombo.PlayAttackVFX(data);

        // 连击缓冲检查点：linkTime 之前接受攻击输入，到点统一出手（子类覆写 OnLinkCheckpoint）
        float linkTime = combo.GetLinkCancelTime(reusableData.currentIndex.Value);
        if (linkTime > 0f)
        {
            reusableData.hasATKCommand = false;   // 清掉上一起手/上一段的残留指令，只统计本段窗口内新按键
            reusableData.canInput = true;         // 打开输入窗口（窗口内按键只缓冲，不立即出手）
            state.Events.Add(linkTime, OnLinkCheckpoint);
        }

        // 打击帧事件：动画播到本段 hitFrameTime 处触发伤害判定（CharacterCombo.ATK 入口）
        // 与 OnLinkCheckpoint 同一套 Animancer 事件机制，判定与动画严格同步
        float hitFrameTime = combo.GetComboHitFrameTime(reusableData.currentIndex.Value);
        if (hitFrameTime > 0f)
        {
            state.Events.Add(hitFrameTime, () => stateMachine.characterCombo.ATK());
        }
    }

    /// <summary>
    /// 连击检查点回调：动画播放到 linkCancelTime（默认 60%）处触发。
    /// 子类在此消费 hasATKCommand 缓冲的输入指令（ATKIngState 用于切下一段）
    /// </summary>
    protected virtual void OnLinkCheckpoint() { }

    /// <summary>播放当前段的收尾动画，播完自动回待机</summary>
    protected void PlayAttackEndClip()
    {
        var transition = reusableData.currentCombo?.GetAttackEndClip(reusableData.currentIndex.Value);
        if (transition == null) return;

        var state = player.characterAnimancer.Play(transition);
        state.Events.OnEnd = OnRecoveryEnd;
    }

    /// <summary>设置当前段数（变化时自动通知动画/UI 订阅方）</summary>
    protected void SetATKIndex(int index)
    {
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

    /// <summary>收尾动画播完：恢复移动并回到待机（子类可覆写为其它行为）</summary>
    protected virtual void OnRecoveryEnd()
    {
        RestoreMovement();
        stateMachine.SwitchState(stateMachine.NullState);
    }

    /// <summary>恢复移动：移动状态机从空状态切回待机/行走（攻击流程结束时调用）</summary>
    protected void RestoreMovement()
    {
        var moveMachine = player.MovementStateMachine;
        if (moveMachine == null) return;

        moveMachine.SwitchState(
            player.IsMoving ? moveMachine.walkingState : moveMachine.idlingState);
    }

    /// <summary>动画事件触发的强制状态切换（保留原接口）</summary>
    public virtual void OnAnimationTranslateEvent(IState state)
    {
        stateMachine.SwitchState(state);
    }
}

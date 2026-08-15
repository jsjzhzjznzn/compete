using UnityEngine.InputSystem;

/// <summary>
/// 攻击执行状态
/// 播放当前段的攻击动画，接段通过"提前取消窗口"触发：
/// 本段配置了 linkCancelTime，到时间点打开 canInput 窗口，窗口内按键立即切下一段。
///
/// 收尾阶段（EnterRecovery）：
/// 攻击动画播完/断连/末尾段 → 播放收尾动画并打开输入（isRecovery=true）。
/// 此阶段移动状态机保持在空状态，但每帧检测输入：
///   - 按移动 → 恢复移动并回待机（收尾动画被打断）
///   - 按攻击 → 回待机重新起手第一段
///   - 无输入 → 收尾动画播完（OnRecoveryEnd）自动回待机
/// </summary>
public class PlayerATKIngState : PlayerComboState
{
    /// <summary>是否处于收尾动画阶段（此阶段允许移动 / 重新起手）</summary>
    private bool isRecovery;

    public PlayerATKIngState(PlayerComboStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        isRecovery = false;
        PlayAttackClip();
    }

    public override void Update()
    {
        base.Update();

        // 收尾阶段：检测到移动输入 → 打断收尾，恢复移动并回待机
        if (isRecovery && player.IsMoving)
        {
            isRecovery = false;
            RestoreMovement();
            stateMachine.SwitchState(stateMachine.NullState);
        }
    }

    /// <summary>攻击键按下</summary>
    protected override void OnAttackInput(InputAction.CallbackContext context)
    {
        if (!reusableData.canInput) return;

        // 收尾阶段按攻击 → 重新起手第一段（回待机后由 NullState 重新进入攻击）
        if (isRecovery)
        {
            isRecovery = false;
            reusableData.hasATKCommand = true;
            stateMachine.SwitchState(stateMachine.NullState);
            return;
        }

        // 攻击段取消窗口内按攻击 → 立即切下一段
        reusableData.hasATKCommand = true;
        AdvanceToNextSegment();
    }

    /// <summary>进入下一段攻击（段数+1 并重播新段动画）</summary>
    private void AdvanceToNextSegment()
    {
        var combo = reusableData.currentCombo;
        if (combo == null)
        {
            RestoreMovement();
            stateMachine.SwitchState(stateMachine.NullState);
            return;
        }

        // 已被断连（如格挡触发 DisConnectCombo）→ 不允许再连段，进入收尾
        if (!reusableData.canLink)
        {
            reusableData.hasATKCommand = false;
            EnterRecovery();
            return;
        }

        // 已到末尾段则不再连击，进入收尾
        if (reusableData.ATKIndex >= combo.GetComboMaxCount() - 1)
        {
            reusableData.hasATKCommand = false;
            EnterRecovery();
            return;
        }

        reusableData.hasATKCommand = false;
        reusableData.canInput = false;   // 切段瞬间关窗，等新段自己的窗口时间再开
        SetATKIndex(reusableData.ATKIndex + 1);
        PlayAttackClip();
    }

    /// <summary>本段攻击动画播完（窗口未被消费时兜底）：进入收尾</summary>
    protected override void OnAnimationEndEvent()
    {
        EnterRecovery();
    }

    /// <summary>
    /// 进入收尾阶段：打开输入窗口，收尾动画期间可移动、可按攻击重新起手
    /// </summary>
    private void EnterRecovery()
    {
        reusableData.canInput = true;
        isRecovery = true;

        var combo = reusableData.currentCombo;
        if (combo == null || combo.GetAttackEndClip(reusableData.ATKIndex) == null)
        {
            // 无收尾动画 → 直接恢复移动并回待机，避免状态卡死在攻击态
            RestoreMovement();
            stateMachine.SwitchState(stateMachine.NullState);
            return;
        }

        // 播放收尾动画（播完 OnRecoveryEnd 自动恢复移动并回待机）
        PlayAttackEndClip();
    }
}

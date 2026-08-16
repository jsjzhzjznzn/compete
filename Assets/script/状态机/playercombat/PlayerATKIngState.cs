using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 攻击执行状态
/// 接段通过"连击缓冲 + 检查点"触发：
/// 本段配置了 linkCancelTime（0~1 归一化时间，默认 0.6），动画前段打开输入窗口，
/// 窗口内按攻击只写 hasATKCommand（缓冲），动画播到 linkCancelTime 处触发
/// OnLinkCheckpoint：有缓冲指令 → 立即切下一段；无 → 关闭窗口等动画播完。
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
      //  FaceAttackDirection();
        PlayAttackClip();
    }

    /// <summary>
    /// 攻击朝向修正：起手时让角色面向"相机前方"（水平面）。
    /// 因为攻击期间移动状态机处于空状态、角色不会转向，
    /// 不修正的话会保持旧朝向，挥击弧线（动画本身朝角色右侧）看起来就"偏"。
    /// </summary>
   /* private void FaceAttackDirection()
    {
        Vector3 camFwd = player.CameraTransform != null
            ? player.CameraTransform.forward
            : player.transform.forward;
        camFwd.y = 0f;
        camFwd.Normalize();
        if (camFwd.sqrMagnitude <= 0.001f)
        {
            camFwd = player.transform.forward;
            camFwd.y = 0f;
            camFwd.Normalize();
        }
        if (camFwd.sqrMagnitude > 0.001f)
        {
            player.transform.rotation = Quaternion.LookRotation(camFwd);
        }
    }*/

    public override void Update()
    {
        base.Update();

        // 收尾阶段：检测到移动输入 → 打断收尾，恢复移动并回待机
        if (isRecovery && player.IsMoving)
        {
            isRecovery = false;
            RestoreMovement();
            stateMachine.SwitchState(stateMachine.NullState);
            return;
        }

        // 收尾阶段：Shift 按下 → 打断收尾直接进入闪避（有移动输入=前冲，无=后冲）
        // 闪避只在 Idle/Walk 的 Update 里检测，而收尾时移动状态机停在空状态，
        // 所以这里必须自己拦截；且 triggered 是按下帧一次性信号，不能等下一帧，直接切。
        // 顺序很关键：先让连击回空状态（此时清的是"收尾动画"的 OnEnd），
        // 再切闪避（此时闪避动画的 OnEnd 刚挂上，不会被 NullState.ClearAnimationEnd 误清）。
        if (isRecovery && player.IsSprintPressed)
        {
            isRecovery = false;
            stateMachine.SwitchState(stateMachine.NullState);

            var moveMachine = player.MovementStateMachine;
            if (moveMachine != null)
            {
                moveMachine.SwitchState(
                    player.IsMoving
                        ? (IState)moveMachine.dashingState
                        : moveMachine.dashBackingState);
            }
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

        // 攻击段：窗口内按键只写入缓冲指令，到检查点（OnLinkCheckpoint）统一切下一段
        reusableData.hasATKCommand = true;
    }

    /// <summary>
    /// 到达本段连击检查点（动画播到 linkCancelTime，默认 60% 处）：
    /// 缓冲过攻击指令 → 立即切下一段；否则关闭输入窗口，等动画播完进收尾
    /// </summary>
    protected override void OnLinkCheckpoint()
    {
        if (reusableData.hasATKCommand)
        {
            AdvanceToNextSegment();
        }
        else
        {
            reusableData.canInput = false;
        }
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
        if (reusableData.currentIndex.Value >= combo.GetComboMaxCount() - 1)
        {
            reusableData.hasATKCommand = false;
            EnterRecovery();
            return;
        }

        reusableData.hasATKCommand = false;
        reusableData.canInput = false;   // 切段瞬间关窗，等新段自己的窗口时间再开
        SetATKIndex(reusableData.currentIndex.Value + 1);
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
        if (combo == null || combo.GetAttackEndClip(reusableData.currentIndex.Value) == null)
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

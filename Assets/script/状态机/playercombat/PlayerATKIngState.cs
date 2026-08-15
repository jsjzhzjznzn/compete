using UnityEngine.InputSystem;

/// <summary>
/// 攻击执行状态
/// 播放当前段的攻击动画；段播完判断连击：有指令且允许连击 → 进下一段，否则收招回待机
/// </summary>
public class PlayerATKIngState : PlayerComboState
{
    public PlayerATKIngState(PlayerComboStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        PlayAttackClip();
    }

    public override void Update()
    {
        base.Update();

        // 攻击中转向敌人（敌人系统接入前为空实现）
        stateMachine.characterCombo.UpdateAttackLookAtEnemy();
    }

    /// <summary>本段攻击动画播完</summary>
    protected override void OnAnimationEndEvent()
    {
        var combo = reusableData.currentCombo;
        if (combo == null)
        {
            stateMachine.SwitchState(stateMachine.NullState);
            return;
        }

        // 有攻击指令、允许连击、且还有下一段 → 连下一段
        if (reusableData.hasATKCommand && reusableData.canLink
            && reusableData.ATKIndex < combo.GetComboMaxCount() - 1)
        {
            reusableData.hasATKCommand = false;
            SetATKIndex(reusableData.ATKIndex + 1);
            PlayAttackClip();
        }
        else
        {
            // 否则播放收尾动画（播完 OnRecoveryEnd 自动回待机）
            PlayAttackEndClip();
        }
    }
}

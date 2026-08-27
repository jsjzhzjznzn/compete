using UnityEngine;

/// <summary>
/// 受击状态（硬直）
/// 由 Player.TakeHit 切入（连击状态机已先回空状态）：
/// - 不订阅任何移动输入（移动/轻点/相机回正全部禁用）
/// - 锁定连击输入（canInput=false，攻击/技能事件即使触发也被忽略）
/// - 播放受击动画，动画时长即僵直时长，播完自动恢复：
///   移动状态机 → idle，连击状态机 → 空状态（重新打开输入）
/// </summary>
public class PlayerHurtState : PlayerMovementState
{
    public PlayerHurtState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

    public override MovementStateType StateType => MovementStateType.Hurt;

    public override void Enter()
    {
        // 不调用 base.Enter()：硬直期间不订阅任何移动输入
        var data = playerMovementData?.hurtData;
        reusableData.rotationTime = data != null ? data.rotationTime : 0.03f;

        // 受击音效（从 SoundData 查 HIT 类型取 clip）
        player.ActorAudio?.PlayByStyle(SoundStyle.HIT);

        // 锁定连击输入：攻击/技能事件仍会回调，但 canInput=false 让它们全部忽略
        player.ComboStateMachine.ReusableData.canInput = false;

        if (data?.animationClip != null)
        {
            // 受击动画时长即僵直时长，播完 OnHurtEnd 自动恢复
            var state = player.characterAnimancer.Play(data.animationClip, data.fadeDuration);
            state.Events.Clear();
            state.Events.OnEnd = OnHurtEnd;
        }
        else
        {
            Debug.LogWarning($"[{player.name}] 未配置受击动画（PlayerSO.movementData.hurtData），受击状态无法自动结束");
        }
    }

    public override void Update() { }

    public override void Exit()
    {
        // 不调用 base.Exit()：硬直期间本来就没订阅输入

        // 清除动画结束回调，避免被打断（再次受击）后残留触发
        var current = player.characterAnimancer.States.Current;
        if (current != null) current.Events.OnEnd = null;
    }

    /// <summary>受击动画播完（僵直结束）：恢复输入并回到待机</summary>
    private void OnHurtEnd()
    {
        // 先回连击空状态：复位连招信息 + 重新打开输入（canInput=true）
        player.ComboStateMachine.SwitchState(player.ComboStateMachine.NullState);
        // 再强制回移动待机（NullState.Enter 里恢复移动可能切到行走，这里统一回 idle）
        player.MovementStateMachine.SwitchState(player.MovementStateMachine.idlingState);
    }
}

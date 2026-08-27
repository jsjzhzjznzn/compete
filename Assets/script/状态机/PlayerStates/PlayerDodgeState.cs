using UnityEngine;

/// <summary>
/// 闪避状态（由无敌窗口内受到伤害触发，独立于前冲 PlayerDashingState）
/// 由 Player.OnDamageBlocked 切入（连击状态机已先回空状态）：
/// - 不订阅任何移动输入（闪避期间锁定移动）
/// - 锁定连击输入（canInput=false，攻击/技能事件即使触发也被忽略）
/// - 播放闪避动画，动画时长即闪避时长，播完自动恢复：
///   移动状态机 → 待机/行走，连击状态机 → 空状态（重新打开输入）
/// - 闪避全程无敌（无敌时长 = 闪避动画长度，HealthModel.SetInvincible 拦截伤害）
/// </summary>
public class PlayerDodgeState : PlayerMovementState
{
    public PlayerDodgeState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

    public override MovementStateType StateType => MovementStateType.Dodge;

    public override void Enter()
    {
        // 不调用 base.Enter()：闪避期间不订阅任何移动输入
        var data = playerMovementData?.dodgeData;
        reusableData.rotationTime = data != null ? data.rotationTime : 0.03f;

        // 锁定连击输入：闪避期间攻击/技能事件仍会回调，但 canInput=false 让它们全部忽略
        player.ComboStateMachine.ReusableData.canInput = false;

        var clip = data?.animationClip;
        // 兜底：未配置闪避动画时回退用冲刺动画（旧 asset 只配了 dashData）
       /* if (clip == null && playerMovementData?.dashData?.animationClip != null)
        {
            clip = playerMovementData.dashData.animationClip;
            Debug.LogWarning($"[{player.name}] 未配置闪避动画（PlayerSO.movementData.dodgeData），回退使用冲刺动画");
        }*/

        if (clip != null)
        {
            // 闪避动画时长即闪避时长，播完 OnDodgeEnd 自动恢复
            var state = player.characterAnimancer.Play(clip, data != null ? data.fadeDuration : 0.3f);
            player.ApplyRemotePhaseOffset(state);   // 远程端：按拥有者已播放时长定位起点
            state.Events.Clear();
            state.Events.OnEnd = OnDodgeEnd;

            // 闪避全程无敌：无敌时长 = 动画长度（拦下闪避期间受到的伤害）
            var health = player.GetComponent<HealthModel>();
            if (health != null)
                health.SetInvincible(clip.length);
            else
                Debug.LogWarning($"[{player.name}] 未挂载 HealthModel，闪避无敌无效");
        }
        
    }

    public override void Update() { }

    public override void Exit()
    {
        // 不调用 base.Exit()：闪避期间本来就没订阅输入

        // 清除动画结束回调，避免被打断（再次受击等）后残留触发
        var current = player.characterAnimancer.States.Current;
        if (current != null) current.Events.OnEnd = null;
    }

    /// <summary>闪避动画播完（闪避结束）：恢复输入并回到待机/行走</summary>
    private void OnDodgeEnd()
    {
        // 先回连击空状态：复位连招信息 + 重新打开输入（canInput=true）
        player.ComboStateMachine.SwitchState(player.ComboStateMachine.NullState);
        // 再回移动状态：有移动输入回行走，否则回待机
        player.MovementStateMachine.SwitchState(
            player.IsMoving ? player.MovementStateMachine.walkingState
                            : player.MovementStateMachine.idlingState);
    }
}

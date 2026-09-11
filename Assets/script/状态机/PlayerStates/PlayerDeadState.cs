using UnityEngine;

/// <summary>
/// 死亡状态
/// 由 E_OnDeath 事件触发切入：
/// - 不订阅任何输入（死亡后无法操作）
/// - 锁定连击输入（canInput=false）
/// - 播放死亡动画，动画播完后销毁自身
/// - 死亡后不接受任何反应（受击/闪避/Buff 等全部忽略）
/// </summary>
public class PlayerDeadState : PlayerMovementState
{
    public PlayerDeadState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

    public override MovementStateType StateType => MovementStateType.Dead;

    public override void Enter()
    {
        // 不调用 base.Enter()：死亡期间不订阅任何输入

        var data = playerMovementData?.deadData;
        reusableData.rotationTime = 0f;  // 死亡后不转向

        // 锁定连击输入（防止残留事件触发攻击）
        player.ComboStateMachine.ReusableData.canInput = false;

        // 播放死亡动画
        if (data?.animationClip != null)
        {
            var state = player.characterAnimancer.Play(data.animationClip, data.fadeDuration);
            player.ApplyRemotePhaseOffset(state);  // 远程端同步动画相位
            
            // 动画播完后销毁自身
            state.Events.Clear();
            state.Events.OnEnd = OnDeathAnimationEnd;
        }
        else
        {
            // 没有死亡动画，直接销毁
            Debug.LogWarning($"[{player.name}] 未配置死亡动画（PlayerSO.movementData.deadData），直接销毁");
            DestroyPlayer();
        }
    }

    public override void Update() { }

    public override void Exit()
    {
        // 不调用 base.Exit()：死亡期间本来就没订阅输入
    }

    /// <summary>死亡动画播完：销毁自身</summary>
    private void OnDeathAnimationEnd()
    {
        DestroyPlayer();
    }

    /// <summary>
    /// 销毁玩家实例
    /// 网络模式：Despawn（服务端回收 NetworkObject）
    /// 单机模式：Destroy（本地销毁）
    /// </summary>
    private void DestroyPlayer()
    {
        if (player == null) return;

        // 清除动画回调，防止销毁后残留触发
        var current = player.characterAnimancer?.States.Current;
        if (current != null) current.Events.OnEnd = null;

        if (player.IsSpawned)
        {
            // 联网模式：Despawn 销毁（服务端回收 NetworkObject）
            // 如果是拥有者端，调用 Despawn 会通知所有端销毁
            // 如果是远程端，不要调用 Despawn（只有拥有者能销毁）
            if (player.IsOwner)
            {
                player.NetworkObject.Despawn();
            }
            else
            {
                // 远程端不销毁，只禁用表现
                player.gameObject.SetActive(false);
            }
        }
        else
        {
            // 单机模式：直接 Destroy
            Object.Destroy(player.gameObject);
        }
    }
}

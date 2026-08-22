using Animancer;
using UnityEngine;

/// <summary>
/// 测试用角色脚本（继承 Player 但不响应任何玩家输入）
/// 用途：场景中多个角色并存时，非操控角色保留待机动画，但键盘/鼠标不会驱动它
/// 原理：不创建移动/连击状态机（状态机是订阅输入的唯二入口），直接播放待机动画
/// 注意：playerSO 仍需在 Inspector 中拖入，否则没有待机动画可播
/// </summary>
public class TestPlayer : Player
{
    private AnimancerComponent animancer;

    protected override void Awake()
    {
        // 不调用 base.Awake()：不创建状态机 → 不订阅任何输入事件
        characterController = GetComponent<CharacterController>();
        animancer = GetComponent<AnimancerComponent>();
    }

    protected override void Start()
    {
        // 不调用 base.Start()：不进入状态机，改为直接播放待机动画（保留动画功能）
        fallOutdeltaTimer = fallOutTimer;

        var idle = PlayerSO?.movementData?.idleData;
        if (idle?.animationClip != null)
        {
            animancer?.Play(idle.animationClip, idle.fadeDuration);
        }
    }

    protected override void Update()
    {
        // Player.Update 中 stateMachine / comboStateMachine 为 null（Awake 未创建），
        // 调用 base 只会执行地面检测与重力，不会读取任何输入
        base.Update();
    }

    // ================================================================
    // 受击处理（无状态机版本）
    // ================================================================

    /// <summary>
    /// 受击：直接播放受击动画（动画时长即僵直时长），播完自动回待机。
    /// 本脚本本就不响应输入，无需锁定移动/连击。
    /// </summary>
    public override void TakeHit()
    {
        var hurt = PlayerSO?.movementData?.hurtData;
        if (hurt?.animationClip == null)
        {
            Debug.LogWarning($"[{name}] 未配置受击动画（PlayerSO.movementData.hurtData）", this);
            return;
        }

        var state = animancer.Play(hurt.animationClip, hurt.fadeDuration);
        state.Events.Clear();
        state.Events.OnEnd = OnHurtEnd;
    }

    /// <summary>受击动画播完：清除回调并回待机</summary>
    private void OnHurtEnd()
    {
        // 先清掉当前状态上的结束回调，防止回待机时误触发
        var current = animancer.States.Current;
        if (current != null) current.Events.OnEnd = null;

        var idle = PlayerSO?.movementData?.idleData;
        if (idle?.animationClip != null)
        {
            animancer.Play(idle.animationClip, idle.fadeDuration);
        }
    }
}

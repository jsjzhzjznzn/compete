using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 待机状态
/// 事件控制：订阅移动输入 started 事件，启动轻点计时器；
/// 按住超过阈值判定为真正移动 → 行走；
/// 不足阈值就松手判定为轻点 → 进入走路收尾（播放 walkStopClip）
/// </summary>
public class PlayerIdlingState : PlayerMovementState
{
    private PlayerIdleData Data => playerMovementData?.idleData;

    // ===== 轻点检测 =====
    private const float TapThreshold = 0.13f;   // 判定"真正移动"的按住时长（秒）
    private GameTimer gameTimer;                // 持有引用，退出状态时取消

    public PlayerIdlingState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        ApplyStateData(Data);
    }

    protected override void AddInputActionCallBacks()
    {
        base.AddInputActionCallBacks();
        CharacterInputSystem.MainInstance.inputActions.player.move.started += BufferToMove;
    }

    protected override void RemoveInputActionCallBacks()
    {
        base.RemoveInputActionCallBacks();
        CharacterInputSystem.MainInstance.inputActions.player.move.started -= BufferToMove;

        // 离开 Idle 时若计时器还在跑，必须取消，避免在错误时机回调
        if (gameTimer != null)
        {
            TimerManager.MainInstance.UnregisterTimer(gameTimer);
            gameTimer = null;
        }
    }

    /// <summary>移动输入按下瞬间（started 事件）</summary>
    private void BufferToMove(InputAction.CallbackContext context)
    {
        gameTimer = TimerManager.MainInstance.GetOneTimer(TapThreshold, CheckMoveInput);
    }

    /// <summary>0.11s 计时器到点回调：此时再看输入还在不在</summary>
    private void CheckMoveInput()
    {
        gameTimer = null;

        if (player.IsMoving)
        {
            // 还在按 → 判定为真移动，切 Walk
            stateMachine.SwitchState(stateMachine.walkingState);
        }
        else
        {
            // 已经松手 → 轻点，直接进走路收尾（播放 walkStopClip）
            stateMachine.SwitchState(stateMachine.walkStopState);
        }
    }

    public override void Update()
    {
        base.Update();
        PollInput();                          // 每帧轮询输入（无输入时自然不转向）
    }
}

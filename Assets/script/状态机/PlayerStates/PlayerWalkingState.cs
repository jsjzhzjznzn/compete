/// <summary>
/// 行走状态
/// 轮询控制：每帧读取输入并转向，无输入回待机，按住冲刺键进奔跑
/// </summary>
using System.Collections;
using UnityEngine;
public class PlayerWalkingState : PlayerMovementState
{
    private PlayerWalkData Data => playerMovementData?.walkData;
    

    public PlayerWalkingState(PlayerMovementStateMachine stateMachine) : base(stateMachine) { }

    public override MovementStateType StateType => MovementStateType.Walking;

    public override void Enter()
    {
        base.Enter();
        ApplyStateData(Data);
        // 进入行走：开启脚步循环音效（从 SoundData 查 FOOT 类型取 clip）
        player.ActorAudio?.PlayLoopSound(SoundStyle.FOOT);
    }

    public override void Update()
    {
        base.Update();
        PollInput();                          // 每帧轮询输入并平滑转向

        // Shift 按下 → 进入前冲状态
        if (player.IsSprintPressed)
        {
            stateMachine.SwitchState(stateMachine.dashingState);
            return;
        }

        // 松开摇杆 → 走路收尾
        if (!player.IsMoving)
        {
            stateMachine.SwitchState(stateMachine.walkStopState);
            return;
        }
    }

    public override void Exit()
    {
        base.Exit();
        // 离开行走：停止脚步循环（停下/切闪避/进攻击时脚步不再响）
        player.ActorAudio?.StopLoopSound();
    }
}

using BehaviorDesigner.Runtime.Tasks;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>停止移动(对应 [攻击] 分支的 StopMove):切换到 Idle 状态即可,
    /// Idle 不产生位移,走路动画和根运动位移自然停止</summary>
    [TaskCategory("AI")]
    [TaskDescription("停止移动:切回待机")]
    public class AIStopMove : Action
    {
        private AIStateMachine sm;
        public override void OnAwake() { sm = AITaskUtil.GetStateMachine(this); }
        public override TaskStatus OnUpdate()
        {
            sm?.SwitchState(AIStateType.Idle);
            return TaskStatus.Success;   // 一次性动作,完成即过
        }
    }
}

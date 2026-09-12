using BehaviorDesigner.Runtime.Tasks;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>追击(对应 [追击] 分支的 Chase):切 Walk(WalkState 有目标时自动追),常驻 Running</summary>
    [TaskCategory("AI")]
    [TaskDescription("切换追击(Walk 状态追向目标)")]
    public class AIChase : Action
    {
        private AIStateMachine sm;
        public override void OnAwake() { sm = AITaskUtil.GetStateMachine(this); }
        public override TaskStatus OnUpdate()
        {
            sm?.SwitchState(AIStateType.Walk);
            return TaskStatus.Running;
        }
    }
}

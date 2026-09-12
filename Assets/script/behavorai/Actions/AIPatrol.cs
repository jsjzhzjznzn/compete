using BehaviorDesigner.Runtime.Tasks;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>巡逻(对应 [巡逻] 分支的 Patrol):切 Walk(无目标时 WalkState 随机巡逻),常驻 Running</summary>
    [TaskCategory("AI")]
    [TaskDescription("巡逻(无目标时随机走动)")]
    public class AIPatrol : Action
    {
        private AIStateMachine sm;
        public override void OnAwake() { sm = GetComponent<AIStateMachine>(); }
        public override TaskStatus OnUpdate()
        {
            sm?.SwitchState(AIStateType.Walk);
            return TaskStatus.Running;
        }
    }
}

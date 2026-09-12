using BehaviorDesigner.Runtime.Tasks;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>死亡(对应 [死亡] 分支的 Action):切 Die,常驻 Running</summary>
    [TaskCategory("AI")]
    [TaskDescription("切到死亡状态")]
    public class AIDie : Action
    {
        private AIStateMachine sm;
        public override void OnAwake() { sm = GetComponent<AIStateMachine>(); }
        public override TaskStatus OnUpdate()
        {
            sm?.SwitchState(AIStateType.Die);
            return TaskStatus.Running;   // 死亡是终态,常驻
        }
    }
}

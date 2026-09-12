using BehaviorDesigner.Runtime.Tasks;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>已死亡?(对应 [死亡] 分支)</summary>
    [TaskCategory("AI")]
    [TaskDescription("AI 已死亡")]
    public class AIIsDead : Conditional
    {
        private AIStateMachine sm;
        public override void OnAwake() { sm = AITaskUtil.GetStateMachine(this); }
        public override TaskStatus OnUpdate()
        {
            return sm != null && sm.isDead ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}

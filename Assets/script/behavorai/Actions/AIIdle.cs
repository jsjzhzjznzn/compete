using BehaviorDesigner.Runtime.Tasks;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>兜底待机(对应根节点最后的 Action: Idle):切 Idle,常驻 Running。
    /// 目标丢失后选择器一路落空会走到这里,由本节点完成 Idle 切换</summary>
    [TaskCategory("AI")]
    [TaskDescription("兜底待机")]
    public class AIIdle : Action
    {
        private AIStateMachine sm;
        public override void OnAwake() { sm = AITaskUtil.GetStateMachine(this); }
        public override TaskStatus OnUpdate()
        {
            sm?.SwitchState(AIStateType.Idle);
            return TaskStatus.Running;
        }
    }
}

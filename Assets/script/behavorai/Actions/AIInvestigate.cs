using BehaviorDesigner.Runtime.Tasks;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>调查动静(对应 [警戒] 分支的 Action)
    /// TODO: 未接入声响/调查点系统,先 Failure 让选择器落空</summary>
    [TaskCategory("AI")]
    [TaskDescription("调查动静(未接入)")]
    public class AIInvestigate : Action
    {
        public override TaskStatus OnUpdate() => TaskStatus.Failure;
    }
}

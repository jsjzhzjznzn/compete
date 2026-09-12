using BehaviorDesigner.Runtime.Tasks;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>听到动静?(对应 [警戒] 分支)
    /// TODO: 未接入声响系统,先恒 false</summary>
    [TaskCategory("AI")]
    [TaskDescription("听到动静(未接入,恒 false)")]
    public class AIHeardNoise : Conditional
    {
        public override TaskStatus OnUpdate() => TaskStatus.Failure;
    }
}

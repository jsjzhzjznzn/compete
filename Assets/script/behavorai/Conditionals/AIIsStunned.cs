using BehaviorDesigner.Runtime.Tasks;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>受击硬直中?(对应 [硬直] 分支)
    /// 检测受击标记:收到攻击时 AIPlayer 将 IsHurt 置 true,受击动画播完由 HurtState 置 false</summary>
    [TaskCategory("AI")]
    [TaskDescription("受击标记为 true")]
    public class AIIsStunned : Conditional
    {
        private AIStateMachine sm;
        public override void OnAwake() { sm = GetComponent<AIStateMachine>(); }
        public override TaskStatus OnUpdate()
        {
            return sm != null && sm.IsHurt ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}

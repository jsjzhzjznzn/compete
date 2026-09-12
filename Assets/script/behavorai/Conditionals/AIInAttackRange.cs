using UnityEngine;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>目标在攻击范围内?(取连招里最大的 range,保证任一段够得着)</summary>
    [TaskCategory("AI")]
    [TaskDescription("目标在攻击范围内")]
    public class AIInAttackRange : Conditional
    {
        private AIStateMachine sm;
        public override void OnAwake() { sm = AITaskUtil.GetStateMachine(this); }
        public override TaskStatus OnUpdate()
        {
            if (sm == null || sm.target == null) return TaskStatus.Failure;

            float dist = Vector3.Distance(sm.transform.position, sm.target.position);
            return dist <= sm.MaxAttackRange ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}

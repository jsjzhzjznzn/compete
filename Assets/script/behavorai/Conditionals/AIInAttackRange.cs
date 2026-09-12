using UnityEngine;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>目标在攻击范围内?(取三段攻击里最大的 range,保证任一招够得着)</summary>
    [TaskCategory("AI")]
    [TaskDescription("目标在攻击范围内")]
    public class AIInAttackRange : Conditional
    {
        private AIStateMachine sm;
        public override void OnAwake() { sm = GetComponent<AIStateMachine>(); }
        public override TaskStatus OnUpdate()
        {
            if (sm == null || sm.target == null) return TaskStatus.Failure;

            float dist = Vector3.Distance(sm.transform.position, sm.target.position);
            float r1 = sm.GetAttackData(AIStateType.Attack1)?.range ?? 2f;
            float r2 = sm.GetAttackData(AIStateType.Attack2)?.range ?? 2.5f;
            float r3 = sm.GetAttackData(AIStateType.Attack3)?.range ?? 3f;
            return dist <= Mathf.Max(r1, r2, r3) ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}

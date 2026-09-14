using UnityEngine;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>
    /// 目标在攻击范围内?(范围取自 AIStateMachine.attackRange,是固定值,与招式里的 range 无关)
    /// 范围配在 AIPlayerSO 的 movementData.attackRange 上；不要在本节点上加新字段——
    /// 这个项目里行为树任务新加的序列化字段不会带 C# 默认值(会变 0)，会让判定永远失败。
    /// </summary>
    [TaskCategory("AI")]
    [TaskDescription("目标在攻击范围内(固定范围)")]
    public class AIInAttackRange : Conditional
    {
        private AIStateMachine sm;
        public override void OnAwake() { sm = AITaskUtil.GetStateMachine(this); }
        public override TaskStatus OnUpdate()
        {
            if (sm == null || sm.target == null) return TaskStatus.Failure;

            float dist = Vector3.Distance(sm.transform.position, sm.target.position);
            return dist <= sm.attackRange ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}

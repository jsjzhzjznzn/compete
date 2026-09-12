using UnityEngine;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>面向目标(对应 [攻击] 分支的 FaceTarget):平滑转向,
    /// 朝向误差小于阈值才 Success,让后面的 Attack 在转身完成后才出手</summary>
    [TaskCategory("AI")]
    [TaskDescription("平滑转向目标,转到位才成功")]
    public class AIFaceTarget : Action
    {
        public float turnSpeed = 8f;       // 转向速度
        public float angleThreshold = 10f; // 认为已面向的误差角(度)

        private AIStateMachine sm;
        public override void OnAwake() { sm = AITaskUtil.GetStateMachine(this); }
        public override TaskStatus OnUpdate()
        {
            if (sm == null || sm.target == null) return TaskStatus.Failure;

            sm.FaceTarget(turnSpeed);

            Vector3 dir = sm.target.position - sm.transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.0001f) return TaskStatus.Success;

            float angle = Vector3.Angle(sm.transform.forward, dir.normalized);
            return angle <= angleThreshold ? TaskStatus.Success : TaskStatus.Running;
        }
    }
}

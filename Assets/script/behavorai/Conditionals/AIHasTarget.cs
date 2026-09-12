using UnityEngine;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>
    /// 发现目标(扇形视野索敌,对应 [追击]/[攻击] 分支的前置条件):
    /// - 未锁定:在 targetLayer 物理层、detectRange 范围内、且在前方 viewAngle 扇形内的最近目标 → 设为 sm.target
    /// - 已锁定:一直以这个人为目标,不要求仍在扇形内(追击转身时目标可能在背后),
    ///   脱离追击范围(超出 chaseRange,可追出视野范围一段)或目标死亡 → 清除目标
    /// 物理过滤走层(player 层),不再用 Tag。本节点只做检测和写 sm.target,不切状态
    /// </summary>
    [TaskCategory("AI")]
    [TaskDescription("扇形视野索敌:物理层+角度检测,锁定后直到脱离追击范围")]
    public class AIHasTarget : Conditional
    {
        public float viewAngle = 120f;   // 视野扇形角度(度),以角色正面朝向为中心

        private AIStateMachine sm;
        public override void OnAwake() { sm = AITaskUtil.GetStateMachine(this); }

        public override TaskStatus OnUpdate()
        {
            if (sm == null) return TaskStatus.Failure;

            Transform t = sm.target;

            // 已锁定的目标:脱离追击范围或死亡 → 丢失
            if (t != null && IsLost(t))
            {
                sm.target = null;
                t = null;
            }

            // 没有目标:扇形内搜索最近的一个
            if (t == null)
            {
                t = FindInFan();
                sm.target = t;
            }

            return sm.target != null ? TaskStatus.Success : TaskStatus.Failure;
        }

        /// <summary>锁定维持判定:脱离追击范围或已死亡即丢失(不检查扇形角度,防追击转身死锁)。
        /// 追击范围(chaseRange) ≥ 索敌半径(detectRange),敌人跑了先追一段,彻底脱离才丢</summary>
        private bool IsLost(Transform t)
        {
            if (Vector3.Distance(sm.transform.position, t.position) > sm.chaseRange)
                return true;

            var health = t.GetComponentInParent<HealthModel>();
            return health != null && !health.IsAlive;
        }

        /// <summary>扇形搜索:targetLayer 物理层 + detectRange 内 + 前方 viewAngle 扇形内,取最近者</summary>
        private Transform FindInFan()
        {
            Transform best = null;
            float bestDist = float.MaxValue;
            float halfAngle = viewAngle * 0.5f;

            // 物理层过滤:一次查询只拿目标层内的 Collider(不再全场景扫 Tag)
            Collider[] hits = Physics.OverlapSphere(sm.transform.position, sm.detectRange, sm.targetLayer);
            foreach (var col in hits)
            {
                Transform t = col.transform;
                if (t.IsChildOf(sm.transform)) continue;   // 排除自己及子物体

                Vector3 dir = t.position - sm.transform.position;
                dir.y = 0;
                float dist = dir.magnitude;
                if (dist < 0.0001f) continue;
                if (Vector3.Angle(sm.transform.forward, dir.normalized) > halfAngle) continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = t;
                }
            }
            return best;
        }
    }
}

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
        public float closeRange = 2.5f;  // 近距离圆形检测半径(米),AI周围一圈全向检测

        private AIStateMachine sm;

        // 血量组件缓存:按目标 Transform 键控,目标不变就复用,避免每帧 GetComponentInParent 向上遍历
        private Transform cachedTarget;
        private HealthModel cachedHealth;

        // 索敌命中 buffer:复用同一数组,避免每帧 new Collider[](GC 主要来源)
        private readonly Collider[] hitBuffer = new Collider[32];

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

            var health = GetTargetHealth(t);
            return health != null && !health.IsAlive;
        }

        /// <summary>取目标血量组件(带缓存):目标 Transform 没变就复用上次查到的引用,
        /// 只有换目标时才向上遍历一次 GetComponentInParent</summary>
        private HealthModel GetTargetHealth(Transform t)
        {
            if (t != cachedTarget)
            {
                cachedTarget = t;
                cachedHealth = t != null ? t.GetComponentInParent<HealthModel>() : null;
            }
            return cachedHealth;
        }

        /// <summary>搜索:近距离圆形(全向) + 远距离扇形(前方viewAngle),取最近者</summary>
        private Transform FindInFan()
        {
            Transform best = null;
            float bestDist = float.MaxValue;
            float halfAngle = viewAngle * 0.5f;

            // 物理层过滤:一次查询只拿目标层内的 Collider(不再全场景扫 Tag)。
            // NonAlloc 写入复用 buffer,不 new 数组 → 每帧零 GC;物理计算本身不变
            int count = Physics.OverlapSphereNonAlloc(
                sm.transform.position, sm.detectRange, hitBuffer, sm.targetLayer);
            if (count == hitBuffer.Length)
                Debug.LogWarning($"[AI] {sm.name} 索敌命中数达 buffer 上限({count}),可能漏检,建议调大 hitBuffer", sm);

            for (int i = 0; i < count; i++)
            {
                Transform t = hitBuffer[i].transform;
                if (t.IsChildOf(sm.transform)) continue;   // 排除自己及子物体

                Vector3 dir = t.position - sm.transform.position;
                dir.y = 0;
                float dist = dir.magnitude;
                if (dist < 0.0001f) continue;

                // 近距离圆形检测:在 closeRange 范围内直接通过,不检查角度
                bool inCloseRange = dist <= closeRange;
                
                // 扇形检测:在 viewAngle 角度内
                bool inFan = Vector3.Angle(sm.transform.forward, dir.normalized) <= halfAngle;
                
                // 满足任一条件即视为检测到
                if (!inCloseRange && !inFan) continue;

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

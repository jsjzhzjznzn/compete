using UnityEngine;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>
    /// 追击(对应 [追击] 分支的 Chase):切 Walk(WalkState 有目标时自动追)。
    /// 已在攻击范围内时不再一直 Running——停住并返回 Success，让整棵行为树重跑，
    /// 由更高优先级的 [攻击] 分支接管。
    /// 这样不依赖 Behavior Designer 的条件中断(abort)：否则 AI 会一直卡在追击里，
    /// 顶着玩家走却不切攻击。
    /// </summary>
    [TaskCategory("AI")]
    [TaskDescription("切换追击(Walk 状态追向目标)；到攻击范围即交回行为树")]
    public class AIChase : Action
    {
        private AIStateMachine sm;
        public override void OnAwake() { sm = AITaskUtil.GetStateMachine(this); }

        public override TaskStatus OnUpdate()
        {
            if (sm == null) return TaskStatus.Failure;

            // 已进入攻击范围：停住(Idle)并返回 Success → 本轮分支结束，行为树重跑 → 下一 tick 先进 [攻击] 分支
            if (sm.target != null &&
                Vector3.Distance(sm.transform.position, sm.target.position) <= sm.attackRange)
            {
                sm.SwitchState(AIStateType.Idle);
                return TaskStatus.Success;
            }

            sm.SwitchState(AIStateType.Walk);
            return TaskStatus.Running;
        }
    }
}

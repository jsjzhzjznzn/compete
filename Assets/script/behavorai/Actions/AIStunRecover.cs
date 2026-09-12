using BehaviorDesigner.Runtime.Tasks;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>受击硬直恢复(对应 [硬直] 分支的 Action):
    /// 受击标记为 true 时切入 Hurt 状态并保持 Running;
    /// 受击动画播完 HurtState 会把标记置 false,本节点随之返回 Success,
    /// 选择器下一轮按优先级决定去留(攻击/追击/巡逻等)。
    /// 标记的置位在 AIPlayer(收到攻击),置位结束在 HurtState(动画播完),这里只负责切状态</summary>
    [TaskCategory("AI")]
    [TaskDescription("切入受击硬直,播完即成功")]
    public class AIStunRecover : Action
    {
        private AIStateMachine sm;
        public override void OnAwake() { sm = AITaskUtil.GetStateMachine(this); }
        public override TaskStatus OnUpdate()
        {
            if (sm == null || !sm.IsHurt) return TaskStatus.Success;

            // 首次被打时切进 Hurt(幂等,已在 Hurt 中则无操作)
            if (sm.CurrentRootState != AIStateType.Hurt)
                sm.SwitchState(AIStateType.Hurt);

            return TaskStatus.Running;
        }
    }
}

using BehaviorDesigner.Runtime.Tasks;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>受击硬直恢复(对应 [硬直] 分支的 Action):
    /// - 首次受击:切入 Hurt 状态
    /// - 硬直中再次受击(受击计数变化):forceRestart 重入 Hurt 节点,受击动画重播、僵直刷新
    /// - 受击动画播完(HurtState 置 IsHurt=false)返回 Success,选择器接管去留
    /// 标记置位在 AIPlayer(收到攻击),结束置位在 HurtState(动画播完),这里负责切换与重入</summary>
    [TaskCategory("AI")]
    [TaskDescription("切入/重入受击硬直,播完即成功")]
    public class AIStunRecover : Action
    {
        private AIStateMachine sm;
        private int seenHitCount;   // 本任务已处理到的受击次数(检测硬直中的新受击)

        public override void OnAwake() { sm = AITaskUtil.GetStateMachine(this); }

        public override TaskStatus OnUpdate()
        {
            if (sm == null || !sm.IsHurt) return TaskStatus.Success;

            if (sm.CurrentRootState != AIStateType.Hurt)
            {
                // 首次被打:切入 Hurt
                sm.SwitchState(AIStateType.Hurt);
                seenHitCount = sm.HurtHitCount;
            }
            else if (seenHitCount != sm.HurtHitCount)
            {
                // 硬直中再次被打:重入 Hurt 节点(forceRestart → OnExit→OnEnter → 受击动画重播)
                sm.SwitchState(AIStateType.Hurt, forceRestart: true);
                seenHitCount = sm.HurtHitCount;
            }

            return TaskStatus.Running;
        }
    }
}

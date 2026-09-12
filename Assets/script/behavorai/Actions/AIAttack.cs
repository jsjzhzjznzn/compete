using BehaviorDesigner.Runtime.Tasks;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>攻击(对应 [攻击] 分支的 Attack):按连招序列出招,打完返回 Success;
    /// 只要还满足攻击分支条件,选择器下一轮会再次进来接下一招</summary>
    [TaskCategory("AI")]
    [TaskDescription("按连招出招,收招即成功")]
    public class AIAttack : Action
    {
        private AIStateMachine sm;
        public override void OnAwake() { sm = GetComponent<AIStateMachine>(); }
        public override TaskStatus OnUpdate()
        {
            if (sm == null) return TaskStatus.Failure;

            // 没在打才出手(避免 Running 期间每帧重复 EnterAttack 把连招索引推着走)
            if (!sm.IsAttacking)
                sm.EnterAttack(sm.ChooseAttack());

            return sm.IsAttacking ? TaskStatus.Running : TaskStatus.Success;
        }
    }
}

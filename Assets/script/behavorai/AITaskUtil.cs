using UnityEngine;

namespace BehaviorDesigner.Runtime.Tasks
{
    /// <summary>
    /// AI 行为树任务工具:统一解析 AIStateMachine 的入口。
    /// 状态机组件由 AIPlayer 在 Awake 里动态补挂(预制体上可能没有),
    /// 所以任务一律先找 AIPlayer 再拿它持有的状态机,找不到再退回直接 GetComponent。
    /// 注意:BD 的 Task 不是 Component,通过 task.Owner(Behavior 组件)拿 GameObject
    /// </summary>
    public static class AITaskUtil
    {
        public static AIStateMachine GetStateMachine(Task task)
        {
            GameObject go = task != null && task.Owner != null ? task.Owner.gameObject : null;
            if (go == null) return null;

            var player = go.GetComponent<AIPlayer>();
            if (player != null && player.AIStateMachine != null)
                return player.AIStateMachine;

            return go.GetComponent<AIStateMachine>();
        }
    }
}

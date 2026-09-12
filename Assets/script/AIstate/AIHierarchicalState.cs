using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可以包含子状态的状态节点
/// </summary>
public class AIHierarchicalState : AIState
{
    // 子状态表:按枚举索引,AddSubState 注册
    protected Dictionary<AIStateType, AIState> subStates = new Dictionary<AIStateType, AIState>();

    protected AIState currentSubState;        // 当前激活的子状态
    protected AIStateType entrySubState;      // 每次进入本容器时默认进入的子状态（SetEntry 可改）

    public AIHierarchicalState(AIStateMachine machine, GameObject owner, AIStateType type, AIStateType entrySubState)
        : base(machine, owner, type)
    {
        this.entrySubState = entrySubState;
    }

    public void AddSubState(AIState state)
    {
        subStates[state.StateType] = state;
    }

    public AIState GetSubState(AIStateType type)
    {
        subStates.TryGetValue(type, out var s);
        return s;
    }

    public override void OnEnter()
    {
        Debug.Log($"[HSM] Enter: {StateName}");
        if (subStates.ContainsKey(entrySubState))
        {
            SwitchSubState(entrySubState);
        }
    }

    public override void OnUpdate()
    {
        if (currentSubState == null) return;

        // 子状态自己检查是否要切换到兄弟状态
        AIStateType? next = currentSubState.CheckTransitions();
        if (next != null)
        {
            SwitchSubState(next.Value);
        }

        currentSubState.OnUpdate();
    }

    public override void OnExit()
    {
        currentSubState?.OnExit();   // 级联退出
        currentSubState = null;
        Debug.Log($"[HSM] Exit: {StateName}");
    }

    /// <summary>
    /// 切换子状态
    /// </summary>
    /// <param name="type">目标子状态</param>
    /// <param name="forceRestart">目标就是当前子状态时是否重入(Exit→Enter,重播动画);行为树重复触发同一招时传 true</param>
    public void SwitchSubState(AIStateType type, bool forceRestart = false)
    {
        if (!subStates.TryGetValue(type, out AIState next))
        {
            Debug.LogError($"[HSM] 子状态不存在: {StateName}.{type}");
            return;
        }

        if (next == currentSubState)
        {
            // 已在该状态:默认幂等返回(行为树每帧调用安全);forceRestart 时重入重播
            if (forceRestart)
            {
                currentSubState.OnExit();
                currentSubState.OnEnter();
                Debug.Log($"[HSM] {StateName} -> {type}(重入)");
            }
            return;
        }

        currentSubState?.OnExit();
        currentSubState = next;
        currentSubState.OnEnter();
        Debug.Log($"[HSM] {StateName} -> {type}");
    }

    /// <summary>当前子状态运行时对象(行为树读取 IsFinished 等用);无子状态返回 null</summary>
    public AIState CurrentSubState => currentSubState;

    public string CurrentSubStateName => currentSubState?.StateName;

    /// <summary>当前子状态类型(行为树 Conditional 节点读取用);无子状态返回 null</summary>
    public AIStateType? CurrentSubStateType => currentSubState?.StateType;

    // 允许外部指定进入哪个子状态
    public void SetEntry(AIStateType type) => entrySubState = type;
}

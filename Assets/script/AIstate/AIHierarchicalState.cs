using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可以包含子状态的状态节点
/// </summary>
public class AIHierarchicalState : AIState
{
    protected Dictionary<AIStateType, AIState> subStates = new Dictionary<AIStateType, AIState>();
    protected AIState currentSubState;
    protected AIStateType entrySubState;

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

    public void SwitchSubState(AIStateType type)
    {
        if (!subStates.TryGetValue(type, out AIState next))
        {
            Debug.LogError($"[HSM] 子状态不存在: {StateName}.{type}");
            return;
        }
        if (next == currentSubState) return;   // 已在该状态则幂等返回,行为树每帧调用也安全

        currentSubState?.OnExit();
        currentSubState = next;
        currentSubState.OnEnter();
        Debug.Log($"[HSM] {StateName} -> {type}");
    }

    public string CurrentSubStateName => currentSubState?.StateName;

    /// <summary>当前子状态类型(行为树 Conditional 节点读取用);无子状态返回 null</summary>
    public AIStateType? CurrentSubStateType => currentSubState?.StateType;

    // 允许外部指定进入哪个子状态
    public void SetEntry(AIStateType type) => entrySubState = type;
}

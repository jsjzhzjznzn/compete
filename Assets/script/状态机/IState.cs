using UnityEngine;

public interface IState
{
    void OnEnter();      // 进入状态时调用一次
    void OnUpdate();     // 每帧调用
    void OnExit();       // 离开状态时调用一次
}
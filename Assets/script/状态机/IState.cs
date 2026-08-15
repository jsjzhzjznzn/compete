using UnityEngine;

public interface IState
{
    void Enter();      // 进入状态时调用一次
    void Update();     // 每帧调用
    void Exit();       // 离开状态时调用一次
}
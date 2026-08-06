using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 计时器管理器（单例）
/// 对象池管理 GameTimer，提供受/不受 Time.timeScale 影响的延时回调
/// </summary>
public class TimerManager : Singleton<TimerManager>
{
    [SerializeField, Header("计时器预创建数量")] private int timerCount = 10;

    private Queue<GameTimer> notWorkTimers = new Queue<GameTimer>();   // 空闲计时器池
    private List<GameTimer> isWorkingTimers = new List<GameTimer>();   // 工作中的计时器

    protected void Start()
    {
        for (int i = 0; i < timerCount; i++)
            CreateTimer();
    }

    private void Update()
    {
        UpdateTime();
    }

    private void CreateTimer()
    {
        notWorkTimers.Enqueue(new GameTimer());
    }

    /// <summary>
    /// 获取一个受 Time.timeScale 影响的计时器
    /// </summary>
    /// <param name="timer">计时时长（秒）</param>
    /// <param name="action">计时完成后的回调</param>
    /// <returns>可返回该计时器用于手动取消（UnregisterTimer）</returns>
    public GameTimer GetOneTimer(float timer, Action action)
    {
        GameTimer gameTimer = GetFreeTimer();
        gameTimer.StartTimer(false, timer, action);
        return gameTimer;
    }

    /// <summary>
    /// 获取一个不受 Time.timeScale 影响的计时器（实时计时，暂停时仍走）
    /// </summary>
    public GameTimer GetRealTimer(float time, Action action)
    {
        GameTimer gameTimer = GetFreeTimer();
        gameTimer.StartTimer(true, time, action);
        return gameTimer;
    }

    /// <summary>
    /// 从池中取出一个空闲计时器，没有则新建
    /// </summary>
    private GameTimer GetFreeTimer()
    {
        if (notWorkTimers.Count == 0)
            CreateTimer();
        GameTimer timer = notWorkTimers.Dequeue();
        isWorkingTimers.Add(timer);
        return timer;
    }

    /// <summary>
    /// 手动结束并回收一个计时器（不会触发回调）
    /// </summary>
    public void UnregisterTimer(GameTimer gameTimer)
    {
        if (gameTimer == null) return;
        // 非工作中的计时器不需要回收
        if (gameTimer.TimerStation != TimerStation.DoWorking) return;
        gameTimer.InitTimer();
        isWorkingTimers.Remove(gameTimer);
        notWorkTimers.Enqueue(gameTimer);
    }

    /// <summary>
    /// 更新所有工作中的计时器，回收已完成的可继续使用
    /// </summary>
    private void UpdateTime()
    {
        if (isWorkingTimers.Count == 0) return;

        // 倒序遍历：完成/回收时安全 RemoveAt，避免正序遍历删除导致跳项
        for (int i = isWorkingTimers.Count - 1; i >= 0; i--)
        {
            GameTimer timer = isWorkingTimers[i];
            if (timer.TimerStation == TimerStation.DoWorking)
            {
                if (timer.IsRealTime)
                    timer.UpdateRealTimer();
                else
                    timer.UpdateTimer();
            }
            else if (timer.TimerStation == TimerStation.DoneWorked)
            {
                timer.InitTimer();
                notWorkTimers.Enqueue(timer);
                isWorkingTimers.RemoveAt(i);
            }
        }
    }
}

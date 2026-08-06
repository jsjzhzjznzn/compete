using System;
using UnityEngine;

/// <summary>
/// 计时器状态
/// </summary>
public enum TimerStation
{
    NotWorking,   // 空闲（在池中，可复用）
    DoWorking,    // 工作中
    DoneWorked    // 已完成（等待回收）
}

/// <summary>
/// 单个计时器
/// 由 TimerManager 通过对象池管理，不要直接 new 使用
/// </summary>
public class GameTimer
{
    public TimerStation TimerStation { get; private set; } = TimerStation.NotWorking;
    public bool IsRealTime { get; private set; }

    private float duration;
    private float currentTime;
    private Action onComplete;

    /// <summary>
    /// 开始计时
    /// </summary>
    /// <param name="isRealTime">true = 不受 Time.timeScale 影响；false = 受 timeScale 影响</param>
    /// <param name="time">计时时长（秒）</param>
    /// <param name="action">计时完成后的回调</param>
    public void StartTimer(bool isRealTime, float time, Action action)
    {
        IsRealTime = isRealTime;
        duration = time;
        currentTime = 0f;
        onComplete = action;
        TimerStation = TimerStation.DoWorking;
    }

    /// <summary>受 Time.timeScale 影响的计时</summary>
    public void UpdateTimer()
    {
        if (TimerStation != TimerStation.DoWorking) return;
        currentTime += Time.deltaTime;
        if (currentTime >= duration)
            Finish();
    }

    /// <summary>不受 Time.timeScale 影响的计时（实时计时，暂停时仍走）</summary>
    public void UpdateRealTimer()
    {
        if (TimerStation != TimerStation.DoWorking) return;
        currentTime += Time.unscaledDeltaTime;
        if (currentTime >= duration)
            Finish();
    }

    private void Finish()
    {
        TimerStation = TimerStation.DoneWorked;
        onComplete?.Invoke();
    }

    /// <summary>重置并回收（由 TimerManager 调用）</summary>
    public void InitTimer()
    {
        TimerStation = TimerStation.NotWorking;
        duration = 0f;
        currentTime = 0f;
        onComplete = null;
    }
}

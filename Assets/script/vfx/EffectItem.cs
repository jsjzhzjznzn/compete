using UnityEngine;

/// <summary>
/// 特效池对象（挂到特效预制体上）
/// 激活时播放全部粒子，播放 playTime 秒后自动禁用回池（禁用即回池，无需外部回收）。
/// 复用时自动重启：每次 OnEnable 先取消上一次的回收计时器，再重新播放，避免旧计时器误杀重播。
/// </summary>
public class EffectItem : MonoBehaviour
{
    [SerializeField, Header("特效持续播放时间（秒），到点自动回池")]
    private float playTime = 1f;

    [SerializeField, Header("特效播放速度（1=正常，>1 加速，<1 慢放）")]
    private float playSpeed = 1f;

    private ParticleSystem[] particles;      // 预制体上全部粒子系统（含子物体）
    private GameTimer recycleTimer;          // 回收计时器：保存引用以便重播时取消旧的

    private void Awake()
    {
        particles = GetComponentsInChildren<ParticleSystem>();

        // 注册到 VFXManager，统一做时停/慢放控制（每个对象只注册一次）
        for (int i = 0; i < particles.Length; i++)
        {
            VFXManager.MainInstance.AddVFX(particles[i], playSpeed);
        }
    }

    /// <summary>对象销毁时从 VFXManager 注销，防止列表/字典残留已销毁的粒子引用</summary>
    private void OnDestroy()
    {
        if (particles == null) return;
        for (int i = 0; i < particles.Length; i++)
        {
            VFXManager.MainInstance.RemoveVFX(particles[i]);
        }
    }

    /// <summary>从池中取出激活时：取消旧计时器 → 播放 → 开新回收计时器</summary>
    private void OnEnable()
    {
        StartPlay();
    }

    private void StartPlay()
    {
        // 关键：取消上一次的回收计时器，防止旧计时器把这次重播的特效提前回收
        CancelRecycleTimer();

        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Play();
        }

        if (playTime > 0f)
        {
            recycleTimer = TimerManager.MainInstance.GetOneTimer(playTime, StartReCycle);
        }
    }

    /// <summary>播完定时回调：禁用自身回池</summary>
    private void StartReCycle()
    {
        recycleTimer = null;
        this.gameObject.SetActive(false);
    }

    /// <summary>禁用回池时：取消计时器 + 停止所有粒子</summary>
    private void OnDisable()
    {
        CancelRecycleTimer();

        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Stop();
        }
    }

    /// <summary>取消回收计时器（重播或回池时调用，防止旧计时器误触发）</summary>
    private void CancelRecycleTimer()
    {
        if (recycleTimer != null)
        {
            TimerManager.MainInstance.UnregisterTimer(recycleTimer);
            recycleTimer = null;
        }
    }
}

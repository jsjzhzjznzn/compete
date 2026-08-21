using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 特效粒子时间控制器（单例）
/// 收集场景中所有注册的特效粒子系统，统一控制它们的播放速度 / 暂停。
/// 用途：动作游戏常见的"时停 / 慢放 / 变速"特效表现（打击帧暂停、受击慢放等）。
///
/// 设计要点：
/// - 每个粒子系统记录自己的【基准速度】（EffectItem 传入的 baseSpeed），
///   全局倍率 = 基准速度 × currentTimeScale，互不干扰、恢复时不丢失各自速度差异。
/// - 慢放期间新注册的特效会自动乘上当前全局倍率，不会出现"新特效全速跑"的脱节。
/// - 自动清理已销毁的粒子引用，避免跨场景累积内存泄漏。
/// </summary>
public class VFXManager : Singleton<VFXManager>
{
    /// <summary>已注册的粒子系统列表</summary>
    private readonly List<ParticleSystem> allParticles = new List<ParticleSystem>();

    /// <summary>每个粒子系统的基准速度（EffectItem 传入的 baseSpeed，恢复/变速时用）</summary>
    private readonly Dictionary<ParticleSystem, float> baseSpeeds = new Dictionary<ParticleSystem, float>();

    /// <summary>当前全局倍率（1 = 正常，0 = 冻结，<1 慢放，>1 加速）</summary>
    private float currentTimeScale = 1f;

    /// <summary>
    /// 注册一个粒子系统（特效对象 Awake 时调用）。
    /// 基准速度 = baseSpeed；实际速度 = 基准 × 当前全局倍率（新特效自动跟随慢放/时停）。
    /// </summary>
    /// <param name="particleSystem">要注册的粒子系统</param>
    /// <param name="baseSpeed">该特效的基准速度（1=正常，>1 加速，<1 慢放）</param>
    public void AddVFX(ParticleSystem particleSystem, float baseSpeed = 1f)
    {
        if (particleSystem == null) return;
        if (allParticles.Contains(particleSystem)) return;   // 防重复注册

        allParticles.Add(particleSystem);
        baseSpeeds[particleSystem] = baseSpeed;

        // ✅ 修复：使用main模块设置simulationSpeed
        var main = particleSystem.main;
        main.simulationSpeed = baseSpeed * currentTimeScale;
    }

    /// <summary>
    /// 注销一个粒子系统（特效对象 OnDestroy 时调用）。
    /// 注意是 OnDestroy 不是 OnDisable：特效回池（SetActive(false)）时仍需保留注册，方便下次取出继续受控。
    /// </summary>
    public void RemoveVFX(ParticleSystem particleSystem)
    {
        if (particleSystem == null) return;
        allParticles.Remove(particleSystem);
        baseSpeeds.Remove(particleSystem);
    }

    /// <summary>
    /// 设置全局特效播放速度倍率（作用于所有已注册粒子）。
    /// 0 = 冻结所有特效（时停），>1 = 加速，<1 = 慢放，1 = 恢复正常。
    /// 每个粒子按自己的基准速度 × 倍率，不会互相覆盖速度差异。
    /// </summary>
    /// <param name="scale">速度倍率</param>
    public void SetGlobalTimeScale(float scale)
    {
        currentTimeScale = Mathf.Max(0f, scale);
        ApplyToAll(ps =>
        {
            var main = ps.main;
            main.simulationSpeed = baseSpeeds[ps] * currentTimeScale;
        });
    }

    /// <summary>暂停所有特效（时停表现）</summary>
    public void PauseAll()
    {
        ApplyToAll(ps => ps.Pause());
    }

    /// <summary>恢复所有特效播放（时停结束）</summary>
    public void ResumeAll()
    {
        ApplyToAll(ps => ps.Play());
    }

    /// <summary>
    /// 对全部已注册粒子执行操作，并顺手清理已销毁的粒子。
    /// 倒序遍历：清理时 RemoveAt 安全，不会跳项。
    /// </summary>
    /// <param name="action">对每个有效粒子执行的操作</param>
    private void ApplyToAll(Action<ParticleSystem> action)
    {
        for (int i = allParticles.Count - 1; i >= 0; i--)
        {
            ParticleSystem ps = allParticles[i];
            if (ps == null)
            {
                // ✅ 修复：ps是列表取出的有效引用，作为字典key删除
                allParticles.RemoveAt(i);
                baseSpeeds.Remove(ps);
                continue;
            }
            action(ps);
        }
    }
}

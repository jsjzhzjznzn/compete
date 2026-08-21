using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 全局 2D 音效管理器（单例，DontDestroyOnLoad）
/// 只负责 2D 音效 / BGM：UI 点击、弹窗提示、全局 BGM、全屏呐喊、全屏爆炸、剧情音效。
/// 所有音源 spatialBlend = 0（纯 2D，无距离衰减、无左右方位），固定在世界原点。
/// 注意：本项目单例统一用 MainInstance（见 Singleton<T>），不是 Instance。
/// </summary>
public class AudioManager : Singleton<AudioManager>
{
    [Header("Mixer 分组（Inspector 拖入）")]
    [SerializeField] private AudioMixerGroup uiGroup;        // Master/UI
    [SerializeField] private AudioMixerGroup bgmGroup;       // Master/BGM
    [SerializeField] private AudioMixerGroup voiceGroup;     // Master/Voice（全屏呐喊/剧情语音）

    [Header("2D 一次性音源池")]
    [SerializeField, Min(1)] private int oneShotPoolSize = 16;   // 一次性 2D 音源池大小
    [SerializeField, Range(0f, 1f)] private float minPlayInterval = 0.05f;   // 同一 clip 防叠播冷却（秒）

    [Header("数据驱动查表（全屏呐喊等按类型取随机 clip）")]
    [SerializeField] private SoundData soundData;   // 2D 音效配置表

    // 一次性 2D 音源池（都是本管理器物体的子物体，固定在世界原点）
    private readonly List<AudioSource> oneShotPool = new List<AudioSource>();
    private int oneShotCursor;                                     // 轮转指针：全部在播时按顺序抢最旧的那个

    // BGM 专属音源（不走一次性池）
    private AudioSource bgmSource;

    // 防叠播冷却记录：clip → 上次播放时间
    private readonly Dictionary<AudioClip, float> lastPlayTime = new Dictionary<AudioClip, float>();

    protected override void Awake()
    {
        base.Awake();
        transform.position = Vector3.zero;   // 音源固定世界原点
        EnsureBGMSource();
    }

    // ==================== 对外接口 ====================

    /// <summary>播放 UI 点击 / 弹窗提示音（走 UI 分组）</summary>
    public void PlayUISound(AudioClip clip, float volume = 1f)
    {
        Play2D(clip, uiGroup, volume);
    }

    /// <summary>播放战斗 2D 音效（全屏呐喊 / 全局爆炸 / 剧情音效）</summary>
    public void PlayBattle2DSound(AudioClip clip, float volume = 1f)
    {
        Play2D(clip, voiceGroup, volume);
    }

    /// <summary>按音效类型查表播放 2D 音效（随机抽 clip）</summary>
    public void Play2DByStyle(SoundStyle style, CharacterNameList character = CharacterNameList.Null, float volume = 1f)
    {
        if (soundData == null) { return; }
        AudioClip clip = soundData.GetAudioClip(style, character);
        Play2D(clip, voiceGroup, volume);
    }

    /// <summary>播放 BGM（循环，自动停掉上一首）</summary>
    public void PlayBGM(AudioClip clip, float volume = 1f)
    {
        if (clip == null || bgmSource == null) { return; }
        bgmSource.clip = clip;
        bgmSource.volume = Mathf.Clamp01(volume);
        bgmSource.loop = true;
        bgmSource.Play();
    }

    /// <summary>停止 BGM</summary>
    public void StopBGM()
    {
        if (bgmSource != null) { bgmSource.Stop(); }
    }

    /// <summary>全局暂停 / 恢复所有 2D 音效（含 BGM）</summary>
    public void SetAll2DPlaying(bool playing)
    {
        for (int i = 0; i < oneShotPool.Count; i++)
        {
            if (oneShotPool[i] != null) { if (playing) oneShotPool[i].UnPause(); else oneShotPool[i].Pause(); }
        }
        if (bgmSource != null) { if (playing) bgmSource.UnPause(); else bgmSource.Pause(); }
    }

    // ==================== 内部实现 ====================

    /// <summary>通用 2D 播放入口：防叠播 → 取音源 → 播放</summary>
    private void Play2D(AudioClip clip, AudioMixerGroup group, float volume)
    {
        if (clip == null) { return; }

        // 防叠播：同一 clip 冷却期内直接丢弃，避免高频触发音量爆增
        if (lastPlayTime.TryGetValue(clip, out float last))
        {
            if (Time.time - last < minPlayInterval) { return; }
        }
        lastPlayTime[clip] = Time.time;

        AudioSource src = GetOneShotSource();
        src.outputAudioMixerGroup = group;
        src.clip = clip;
        src.volume = Mathf.Clamp01(volume);
        src.loop = false;
        src.Play();
    }

    /// <summary>
    /// 取一个一次性 2D 音源：
    /// 优先找空闲（未在播放）的；全部在播则轮转复用最旧那个（2D 短音效可接受掐断）。
    /// </summary>
    private AudioSource GetOneShotSource()
    {
        for (int i = 0; i < oneShotPool.Count; i++)
        {
            if (!oneShotPool[i].isPlaying) { return oneShotPool[i]; }
        }

        // 池没满就扩容一个，满了轮转抢最旧的
        if (oneShotPool.Count < oneShotPoolSize)
        {
            AudioSource src = Create2DSource();
            oneShotPool.Add(src);
            return src;
        }

        oneShotCursor = (oneShotCursor + 1) % oneShotPool.Count;
        return oneShotPool[oneShotCursor];
    }

    /// <summary>创建一个 2D 音源：子物体 + 原点 + spatialBlend = 0</summary>
    private AudioSource Create2DSource()
    {
        GameObject go = new GameObject("2D_AudioSource");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;                 // 纯 2D：无距离衰减、无方位
        src.outputAudioMixerGroup = uiGroup;   // 默认 UI 组，播放时按需覆盖
        return src;
    }

    /// <summary>确保 BGM 专属音源存在（BGM 不走一次性池，避免被短音效抢占）</summary>
    private void EnsureBGMSource()
    {
        if (bgmSource != null) { return; }
        GameObject go = new GameObject("BGM_AudioSource");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        bgmSource = go.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.spatialBlend = 0f;
        bgmSource.outputAudioMixerGroup = bgmGroup;
    }
}

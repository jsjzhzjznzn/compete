using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 全局 AudioMixer 控制器（单例）
/// 统一管理 Mixer 的音量/静音，供设置界面、系统全局调用。
/// 运行时通过暴露参数（Expose Parameters）控制组音量，参数名见 [SerializeField] 配置。
/// 使用前需在 Inspector 拖入 AudioMixer，并用菜单 HuHuTools/AudioMixer/Auto Expose Volumes 自动暴露参数。
/// </summary>
public class AudioMixerController : Singleton<AudioMixerController>
{
    [Header("AudioMixer 资产")]
    /// <summary>要控制的 AudioMixer（在 Inspector 中拖入）</summary>
    [SerializeField] private AudioMixer audioMixer;

    [Header("暴露参数名（与 AudioMixer 中 Expose 的参数名一致）")]
    /// <summary>主音量参数名</summary>
    [SerializeField] private string masterVolumeParam = "MasterVolume";
    /// <summary>音效组音量参数名</summary>
    [SerializeField] private string sfxVolumeParam = "SFXVolume";
    /// <summary>BGM 组音量参数名</summary>
    [SerializeField] private string bgmVolumeParam = "BGMVolume";
    /// <summary>语音组音量参数名</summary>
    [SerializeField] private string voiceVolumeParam = "VoiceVolume";

    // 内部缓存（0~1 线性值）
    private float _masterVolume = 1f;
    private float _sfxVolume = 1f;
    private float _bgmVolume = 1f;
    private float _voiceVolume = 1f;
    private bool _muted = false;

    protected override void Awake()
    {
        base.Awake();
        // 启动时按缓存值同步一次，保证静音/音量设置跨场景生效
        ApplyAllVolumes();
    }

    // ==================== 主音量 ====================

    /// <summary>设置主音量（0~1，线性）</summary>
    public void SetMasterVolume(float volume)
    {
        _masterVolume = Mathf.Clamp01(volume);
        ApplyMasterVolume();
    }

    /// <summary>获取主音量（0~1）</summary>
    public float MasterVolume => _masterVolume;

    /// <summary>静音/取消静音（保留音量值，恢复时沿用）</summary>
    public void SetMute(bool mute)
    {
        _muted = mute;
        ApplyAllVolumes();
    }

    /// <summary>当前是否静音</summary>
    public bool Muted => _muted;

    // ==================== 分组音量 ====================

    /// <summary>设置音效组音量（0~1）</summary>
    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        ApplySFXVolume();
    }

    /// <summary>获取音效组音量（0~1）</summary>
    public float SFXVolume => _sfxVolume;

    /// <summary>设置 BGM 组音量（0~1）</summary>
    public void SetBGMVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp01(volume);
        ApplyBGMVolume();
    }

    /// <summary>获取 BGM 组音量（0~1）</summary>
    public float BGMVolume => _bgmVolume;

    /// <summary>设置语音组音量（0~1）</summary>
    public void SetVoiceVolume(float volume)
    {
        _voiceVolume = Mathf.Clamp01(volume);
        ApplyVoiceVolume();
    }

    /// <summary>获取语音组音量（0~1）</summary>
    public float VoiceVolume => _voiceVolume;

    /// <summary>按参数名直接设置任意已暴露的 mixer 参数（分贝值），找不到该参数返回 false</summary>
    public bool SetExposedParam(string paramName, float dbValue)
    {
        if (audioMixer == null) return false;
        return audioMixer.SetFloat(paramName, dbValue);
    }

    /// <summary>按参数名读取任意已暴露的 mixer 参数（分贝值），失败返回 false</summary>
    public bool GetExposedParam(string paramName, out float dbValue)
    {
        if (audioMixer == null) { dbValue = 0f; return false; }
        return audioMixer.GetFloat(paramName, out dbValue);
    }

    // ==================== 内部应用 ====================

    /// <summary>应用全部音量（静音时全部压到静音）</summary>
    private void ApplyAllVolumes()
    {
        ApplyMasterVolume();
        ApplySFXVolume();
        ApplyBGMVolume();
        ApplyVoiceVolume();
    }

    private void ApplyMasterVolume()
    {
        if (audioMixer == null) return;
        float db = VolumeToDb(_masterVolume);
        audioMixer.SetFloat(masterVolumeParam, _muted ? -80f : db);
    }

    private void ApplySFXVolume()
    {
        if (audioMixer == null) return;
        float db = VolumeToDb(_sfxVolume);
        audioMixer.SetFloat(sfxVolumeParam, _muted ? -80f : db);
    }

    private void ApplyBGMVolume()
    {
        if (audioMixer == null) return;
        float db = VolumeToDb(_bgmVolume);
        audioMixer.SetFloat(bgmVolumeParam, _muted ? -80f : db);
    }

    private void ApplyVoiceVolume()
    {
        if (audioMixer == null) return;
        float db = VolumeToDb(_voiceVolume);
        audioMixer.SetFloat(voiceVolumeParam, _muted ? -80f : db);
    }

    /// <summary>线性音量（0~1）转分贝（-80~0dB）</summary>
    private float VolumeToDb(float volume)
    {
        if (volume <= 0.0001f) return -80f;
        return Mathf.Log10(volume) * 20f;
    }
}
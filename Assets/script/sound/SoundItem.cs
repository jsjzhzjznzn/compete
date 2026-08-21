using UnityEngine;

/// <summary>
/// 通用音效池对象（挂到音效预制体上）
/// 从 SoundData 配置表中按 "音效类型 + 角色" 随机取一段音频播放，播完自动回池。
/// </summary>
public class SoundItem : PoolItemBase
{
    /// <summary>当前音效类型</summary>
    [SerializeField] private SoundStyle soundStyle;
    /// <summary>音效配置表（ScriptableObject）</summary>
    [SerializeField] private SoundData soundData;
    /// <summary>角色名（Null 表示通用音效）</summary>
    [SerializeField] private CharacterNameList CharacterNameList = CharacterNameList.Null;
    private AudioSource audioSource;
    private AudioClip clip;

    /// <summary>设置音效配置表（池管理器取出时调用）</summary>
    public void GetSoundData(SoundData soundData)
    {
        this.soundData = soundData;
    }

    /// <summary>设置角色名</summary>
    public void SetCharacterName(CharacterNameList characterNameList)
    {
        CharacterNameList = characterNameList;
    }

    /// <summary>设置音效类型</summary>
    public void SetSoundStyle(SoundStyle soundStyle)
    {
        this.soundStyle = soundStyle;
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>从池中激活时开始播放</summary>
    protected override void Spawn()
    {
        base.Spawn();
        ReadyPlay();
    }

    /// <summary>从配置表随机取音频</summary>
    private void ReadyPlay()
    {
        if (soundData == null) { return; }
        clip = soundData.GetAudioClip(soundStyle, CharacterNameList);
        if (clip == null) { return; }
        ToPlay();
    }

    /// <summary>播放音频</summary>
    private void ToPlay()
    {
        audioSource.clip = clip;
        audioSource.Play();
    }

    /// <summary>播完后自动回池</summary>
    private void Update()
    {
        if (!audioSource.isPlaying)
        {
            StopPlay();
        }
    }

    /// <summary>禁用自身以回池</summary>
    private void StopPlay()
    {
        this.gameObject.SetActive(false);
    }
}

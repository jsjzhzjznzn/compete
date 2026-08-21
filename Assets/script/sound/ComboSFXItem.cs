using UnityEngine;

/// <summary>
/// 连击音效池对象（挂到连击音效预制体上）
/// 播放指定 ComboData 段的角色语音或武器挥砍音效，播完自动回池。
/// </summary>
public class ComboSFXItem : PoolItemBase
{
    /// <summary>连击段数据（从中读取音效数组）</summary>
    [SerializeField] private ComboData comboData;
    private AudioSource audioSource;
    /// <summary>要播放的音效类型</summary>
    [SerializeField] private SoundStyle soundStyle;

    /// <summary>设置音效类型</summary>
    public void SetSoundStyle(SoundStyle soundStyle)
    {
        this.soundStyle = soundStyle;
    }

    /// <summary>设置连击段数据</summary>
    public void GetComboData(ComboData comboData)
    {
        this.comboData = comboData;
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>从池中激活时按类型随机播放一段音效</summary>
    protected override void Spawn()
    {
        base.Spawn();

        // 角色语音
        if (soundStyle == SoundStyle.ComboVoice)
        {
            audioSource.clip = comboData.characterVoice[Random.Range(0, comboData.characterVoice.Length)];
        }
        // 武器挥砍
        else if (soundStyle == SoundStyle.WeaponSound)
        {
            audioSource.clip = comboData.weaponSound[Random.Range(0, comboData.weaponSound.Length)];
        }

        if (audioSource.clip == null) { return; }
        audioSource.Play();
    }

    /// <summary>播完后自动回池</summary>
    private void Update()
    {
        if (!audioSource.isPlaying)
        {
            StopAudioPlay();
        }
    }

    /// <summary>禁用自身以回池</summary>
    private void StopAudioPlay()
    {
        this.gameObject.SetActive(false);
    }
}

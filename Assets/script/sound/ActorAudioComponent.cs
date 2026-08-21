using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 角色私有 3D 音效组件（挂到每个角色 / 怪物 GameObject 上）
///
/// 【职责】
/// 只负责"该角色相关"的 3D 空间音效：挥砍、普攻打击、跳跃落地、闪避、受击、脚步循环、技能命中。
/// 与全局 2D 的 AudioManager 分工：这里是"跟着角色走的声音"，AudioManager 是"不分区位的全局声"。
///
/// 【核心设计】
/// 1. 音源是角色的【子物体】——角色移动/旋转，音源同步跟随，声音从角色身上发出。
/// 2. 所有音源 spatialBlend = 1（纯 3D）：具备距离衰减（越远越小声）+ 方位立体声（左右声道）。
/// 3. 音源池是【角色私有】的——玩家和怪物各自用各自的池，互不抢占，不会你掐我我掐你。
/// 4. 生命周期绑定角色：角色销毁，组件和内部所有音源（都是子物体）一起销毁，无游离残留、无内存泄漏。
///
/// 【两类音源通道】
/// - 一次性音源池（oneShotPool）：短促音效，播完即弃，可被复用。
/// - 循环音源（loopSource）：脚步/持续施法等需要"一直响"的声音走专属音源，
///   不走一次性池，避免被别的短音效抢占而中途打断。
///
/// 【数据驱动】
/// 不直接存音频，而是持有一份 SoundData 配置表 + 角色名，播放时按"音效类型 + 角色"查表随机抽 clip，
/// 调用方只传枚举，不关心具体是哪段音频。
/// </summary>
public class ActorAudioComponent : MonoBehaviour
{
    // ==================== Inspector 配置区 ====================

    [Header("Mixer 分组（Inspector 拖入）")]
    [SerializeField] private AudioMixerGroup sfx3DGroup;   // Master/SFX3D：所有 3D 音效统一输出到这个 mixer 组，方便单独调音量/静音

    [Header("3D 空间参数")]
    // 最小距离：音源离听者在这个距离以内时音量不再变大（防止贴脸爆音）
    [SerializeField, Min(0.1f)] private float minDistance = 1f;
    // 最大距离：音源离听者超过这个距离后音量衰减到 0（听不见）
    [SerializeField, Min(0.5f)] private float maxDistance = 30f;
    // 距离衰减曲线类型：Linear 线性衰减 / Logarithmic 对数衰减（更真实的"近响远轻"）/ Custom 自定义曲线
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;

    [Header("一次性 3D 音源池")]
    // 池大小：这个角色最多同时能响几个一次性音效（建议 2-3 个，够用即可，多了浪费内存）
    [SerializeField, Min(1)] private int oneShotPoolSize = 4;
    // 防叠播冷却：同一类型音效在 minPlayInterval 秒内重复触发会被丢弃，防止高频触发音量爆增
    [SerializeField, Range(0f, 1f)] private float minPlayInterval = 0.05f;

    [Header("数据驱动配置（Inspector 拖入）")]
    // 角色音效配置表：一份总表，按"音效类型 + 角色名"查随机 clip；所有角色可共用同一份表（表内用角色名区分）
    [SerializeField] private SoundData soundData;
    // 本角色名：查表时的角色键；Null 表示通用音效（所有角色共用同一批音频）
    [SerializeField] private CharacterNameList characterName = CharacterNameList.Null;

    // ==================== 运行时内部状态 ====================

    // 一次性 3D 音源池：所有音源都是本角色的子物体，跟随移动。
    // 播放时优先取"空闲（没在播）"的，全部在播时轮转抢最旧的（短音效被掐断可接受）。
    private readonly List<AudioSource> oneShotPool = new List<AudioSource>();

    // 轮转指针：池子全满且全在播时，按顺序轮流牺牲哪个音源。
    // 用 (cursor + 1) % Count 循环，保证"掐断"这件事公平分配，而不是每次都牺牲同一个。
    private int oneShotCursor;

    // 循环音效专属音源：脚步、持续施法等"一直响"的声音走这里。
    // 每个角色只保留【一个】循环音源（当前仅脚步这类通用循环），不走一次性池，永不被短音效抢占。
    // 简化设计：不做多通道，循环即"当前角色唯一的持续声"。将来若需要多循环（脚步+施法同时），
    // 再把它改回 Dictionary<string, AudioSource> 按 key 区分即可。
    private AudioSource loopSource;

    // 防叠播冷却记录：SoundStyle → 上次播放的 Time.time，配合 minPlayInterval 做节流
    private readonly Dictionary<SoundStyle, float> lastStylePlayTime = new Dictionary<SoundStyle, float>();

    // ==================== 一次性 3D 音效（短促、播完即弃） ====================

    /// <summary>
    /// 直接播一段 3D 音效（调用方已持有具体 clip，不走查表）。
    /// 从一次性池取一个音源，播完自动变空闲，下次自动被复用。
    /// </summary>
    /// <param name="clip">要播放的音频</param>
    /// <param name="volume">音量 0~1，默认最大</param>
    public void PlayOneShot3D(AudioClip clip, float volume = 1f)
    {
        if (clip == null) { return; }                       // 空 clip 直接忽略，避免报错

        AudioSource src = GetOneShotSource();               // 从池里取一个可用音源
        src.clip = clip;                                    // 指定音频
        src.volume = Mathf.Clamp01(volume);                 // 音量限制在 0~1
        src.loop = false;                                   // 一次性，不循环
        src.Play();                                         // 开始播放
    }

    /// <summary>
    /// 按音效类型查表播放（推荐入口）。
    /// 内部从 soundData 按"类型 + 本角色名"随机抽一段 clip 再播，调用方只传枚举。
    /// 自带防叠播：同一类型冷却期内重复触发会被丢弃。
    /// </summary>
    /// <param name="style">音效类型（受击/脚步/挥砍等，见 SoundStyle 枚举）</param>
    /// <param name="volume">音量 0~1，默认最大</param>
    public void PlayByStyle(SoundStyle style, float volume = 1f)
    {
        if (soundData == null) { return; }                  // 没配表就不播，静默跳过

        // 防叠播：同类型在冷却期内直接丢弃（比如受击连击时连续触发，防止音量叠爆）
        if (lastStylePlayTime.TryGetValue(style, out float last))
        {
            if (Time.time - last < minPlayInterval) { return; }
        }
        lastStylePlayTime[style] = Time.time;               // 记录本次播放时间

        AudioClip clip = soundData.GetAudioClip(style, characterName);   // 查表随机抽一段
        if (clip == null) { return; }                       // 表里没配这个类型就不播
        PlayOneShot3D(clip, volume);                        // 复用上面的直接播放逻辑
    }

    /// <summary>
    /// 播放连击段角色语音（呐喊、技能台词等）。
    /// 从 ComboData.characterVoice 数组里随机抽一段，实现"同一招式多次播放不单调"。
    /// </summary>
    /// <param name="comboData">单段攻击配置，内含语音数组；null 或空数组则忽略</param>
    public void PlayComboVoice(ComboData comboData)
    {
        if (comboData == null || comboData.characterVoice == null || comboData.characterVoice.Length == 0) { return; }
        PlayOneShot3D(comboData.characterVoice[Random.Range(0, comboData.characterVoice.Length)]);
    }

    /// <summary>
    /// 播放连击段武器挥砍/打击音效。
    /// 从 ComboData.weaponSound 数组里随机抽一段，原理同 PlayComboVoice。
    /// </summary>
    /// <param name="comboData">单段攻击配置，内含挥砍音数组；null 或空数组则忽略</param>
    public void PlayWeaponSound(ComboData comboData)
    {
        if (comboData == null || comboData.weaponSound == null || comboData.weaponSound.Length == 0) { return; }
        PlayOneShot3D(comboData.weaponSound[Random.Range(0, comboData.weaponSound.Length)]);
    }

    // ==================== 循环音效（专属音源，稳定启停） ====================

    /// <summary>
    /// 开启循环音效（脚步、持续施法等通用循环）。
    /// clip 从 soundData 按"类型 + 角色"自动取，调用方不用碰配置表，只传类型。
    /// 首次调用创建专属循环音源；再次调用直接复用同一音源（不会重复创建）。
    /// </summary>
    /// <param name="style">音效类型，用于查表取 clip</param>
    /// <param name="volume">音量 0~1</param>
    public void PlayLoopSound(SoundStyle style, float volume = 1f)
    {
        if (soundData == null) { return; }                              // 没配表就不播
        AudioClip clip = soundData.GetAudioClip(style, characterName);  // 查表取 clip
        if (clip == null) { return; }                                   // 没配这个类型就不播

        // 首次调用：循环音源还没创建 → 现场创建一个专属音源
        if (loopSource == null)
        {
            loopSource = Create3DSource();
        }
        loopSource.clip = clip;                         // 指定循环音频
        loopSource.volume = Mathf.Clamp01(volume);      // 音量限制 0~1
        loopSource.loop = true;                         // 标记循环播放
        loopSource.Play();                              // 开始（或继续）循环
    }

    /// <summary>
    /// 停止循环音效（角色停下脚步时调用）。
    /// 只 Stop 不停用音源本身，下次 PlayLoopSound 会复用这个音源重新播。
    /// </summary>
    public void StopLoopSound()
    {
        if (loopSource != null) { loopSource.Stop(); }
    }

    // ==================== 内部实现 ====================

    /// <summary>
    /// 从一次性 3D 音源池取一个可用音源。
    /// 分配策略（优先级从高到低）：
    /// 1. 优先找空闲（未在播放）的音源 → 直接用它，互不干扰；
    /// 2. 池没满 → 现场扩容新建一个；
    /// 3. 池满且全在播 → 轮转抢最旧的（oneShotCursor 依次后移），短音效被掐断可接受。
    /// </summary>
    private AudioSource GetOneShotSource()
    {
        // 策略 1：找空闲音源
        for (int i = 0; i < oneShotPool.Count; i++)
        {
            if (!oneShotPool[i].isPlaying) { return oneShotPool[i]; }
        }

        // 策略 2：池没满就扩容一个
        if (oneShotPool.Count < oneShotPoolSize)
        {
            AudioSource src = Create3DSource();
            oneShotPool.Add(src);
            return src;
        }

        // 策略 3：满了轮转抢最旧的（取模保证指针 0→1→2→3→0 循环，不越界）
        oneShotCursor = (oneShotCursor + 1) % oneShotPool.Count;
        return oneShotPool[oneShotCursor];
    }

    /// <summary>
    /// 创建一个 3D 音源：
    /// - 新建一个空 GameObject 并挂到角色下（SetParent）→ 音源随角色移动旋转；
    /// - localPosition 归零 → 声音正好从角色身体位置发出；
    /// - spatialBlend = 1 → 纯 3D，距离衰减 + 方位立体声；
    /// - 应用 Inspector 配置的衰减参数和 mixer 分组。
    /// </summary>
    private AudioSource Create3DSource()
    {
        GameObject go = new GameObject("3D_AudioSource");
        go.transform.SetParent(transform, false);           // 挂到角色下，跟随移动
        go.transform.localPosition = Vector3.zero;          // 位置对齐角色身体
        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;                            // 不让 Awake 自动播，全由代码控制
        src.spatialBlend = 1f;                              // 纯 3D：距离衰减 + 方位立体声
        src.rolloffMode = rolloffMode;                      // 距离衰减曲线类型
        src.minDistance = minDistance;                      // 最小距离
        src.maxDistance = maxDistance;                      // 最大距离
        src.outputAudioMixerGroup = sfx3DGroup;             // 输出到 SFX3D 分组，统一控制音量
        return src;
    }

    // 角色销毁时，子物体 AudioSource 会随角色一起销毁，无需手动清理，不会内存泄漏。
}

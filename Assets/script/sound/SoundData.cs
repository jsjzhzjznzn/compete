using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色音效配置总表（ScriptableObject 资源）
/// 每个角色 + 每种音效类型（SoundStyle）可配置多个 AudioClip，播放时随机抽一个。
/// 通过菜单 Create/Asset/SoundData 创建一份配置资源。
/// </summary>
[CreateAssetMenu(fileName = "SoundData", menuName = "Create/Asset/SoundData")]
public class SoundData : ScriptableObject
{
    /// <summary>
    /// 单条音效配置：一种音效类型 + 一个角色 + 该类型下的音频数组
    /// </summary>
    [System.Serializable]
    public class SoundInfo
    {
        /// <summary>音效类型（挥砍/语音/闪避/受击等，见 SoundStyle 枚举）</summary>
        public SoundStyle soundStyle;
        /// <summary>所属角色（Null 表示通用音效，所有角色共用）</summary>
        public CharacterNameList characterName;
        /// <summary>该类型下的音频片段数组，播放时随机抽取</summary>
        public AudioClip[] clips;
    }

    /// <summary>所有音效配置项</summary>
    [SerializeField] public List<SoundInfo> soundInfoList = new List<SoundInfo>();

    /// <summary>
    /// 按音效类型 + 角色查询音频，随机返回一个 AudioClip
    /// </summary>
    /// <param name="soundStyle">音效类型</param>
    /// <param name="characterName">角色名（Null 时只按类型匹配通用音效）</param>
    /// <returns>随机选中的音频，未找到返回 null</returns>
    public AudioClip GetAudioClip(SoundStyle soundStyle, CharacterNameList characterName)
    {
        // 通用音效：不区分角色，只按类型在列表中查找第一条匹配项
        if (characterName == CharacterNameList.Null)
        {
            for (int i = 0; i < soundInfoList.Count; i++)
            {
                if (soundStyle == soundInfoList[i].soundStyle)
                {
                    // clips 未配置时跳过，避免越界
                    if (soundInfoList[i].clips == null || soundInfoList[i].clips.Length == 0)
                        continue;
                    return soundInfoList[i].clips[Random.Range(0, soundInfoList[i].clips.Length)];
                }
            }
            return null;
        }

        // 角色专属音效：类型 + 角色同时匹配
        SoundInfo targetSound = soundInfoList.Find(i => i.soundStyle == soundStyle && i.characterName == characterName);
        if (targetSound == null || targetSound.clips == null || targetSound.clips.Length == 0)
            return null;
        return targetSound.clips[Random.Range(0, targetSound.clips.Length)];
    }

}
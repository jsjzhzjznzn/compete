using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 音效对象池管理器（单例）
/// 预生成所有音效预制体放进池子，需要时取出一个播放，播完自动回池。
/// 支持两种池：
/// 1. bigSoundCenter：按 "音效名 + SoundStyle" 双重细分的池（同一种音效多个实例）
/// 2. soundCenter：只按 SoundStyle 区分的普通池
/// 持有 AudioMixer 引用，统一控制全局音效音量/静音。
/// </summary>
public class SFX_PoolManager : Singleton<SFX_PoolManager>
{


    /// <summary>
    /// 配置项：一种音效对应一个预制体，预生成 soundCount 个实例
    /// </summary>
    [System.Serializable]
    public class SoundItem
    {
        /// <summary>音效类型</summary>
        public SoundStyle soundStyle;
        /// <summary>音效名称（ApplyBigCenter 为 true 时用作大池的细分键）</summary>
        public string soundName;
        /// <summary>音效预制体（带 AudioSource 和池对象脚本）</summary>
        public GameObject soundPrefab;
        /// <summary>池中预生成的数量</summary>
        public int soundCount;
        /// <summary>是否放入大池（按 soundName + soundStyle 细分）</summary>
        public bool ApplyBigCenter;
    }

    /// <summary>Inspector 中配置的音效池列表</summary>
    [SerializeField] private List<SoundItem> soundPools = new List<SoundItem>();
    /// <summary>普通池：SoundStyle → 可用音效队列</summary>
    private Dictionary<SoundStyle, Queue<GameObject>> soundCenter = new Dictionary<SoundStyle, Queue<GameObject>>();
    /// <summary>大池：音效名 → SoundStyle → 可用音效队列</summary>
    private Dictionary<string, Dictionary<SoundStyle, Queue<GameObject>>> bigSoundCenter = new Dictionary<string, Dictionary<SoundStyle, Queue<GameObject>>>();

    protected override void Awake()
    {
        base.Awake();
        InitSoundPool();
    }

    /// <summary>根据配置预生成所有音效实例并放入对应池中</summary>
    private void InitSoundPool()
    {
        if (soundPools.Count == 0) { return; }

        for (int i = 0; i < soundPools.Count; i++)
        {
            // 大池：以 soundName + SoundStyle 细分
            if (soundPools[i].ApplyBigCenter)
            {
                for (int j = 0; j < soundPools[i].soundCount; j++)
                {
                    // 实例化
                    var go = Instantiate(soundPools[i].soundPrefab);
                    // 设置父物体
                    go.transform.parent = this.transform;
            
                    // 禁用（入池状态）
                    go.SetActive(false);

                    // 不存在该音效名时创建大池键
                    if (!bigSoundCenter.ContainsKey(soundPools[i].soundName))
                    {
                        Debug.Log(soundPools[i].soundName + "大池创建");
                        bigSoundCenter.Add(soundPools[i].soundName, new Dictionary<SoundStyle, Queue<GameObject>>());
                    }
                    // 不存在该 SoundStyle 时创建二级队列
                    if (!bigSoundCenter[soundPools[i].soundName].ContainsKey(soundPools[i].soundStyle))
                    {
                        bigSoundCenter[soundPools[i].soundName].Add(soundPools[i].soundStyle, new Queue<GameObject>());
                    }
                    bigSoundCenter[soundPools[i].soundName][soundPools[i].soundStyle].Enqueue(go);
                }
            }
            // 普通池：只按 SoundStyle 区分
            else
            {
                for (int j = 0; j < soundPools[i].soundCount; j++)
                {
                    // 实例化
                    var go = Instantiate(soundPools[i].soundPrefab);
                    // 设置父物体
                    go.transform.parent = this.transform;
                    // 禁用（入池状态）
                    go.SetActive(false);

                    // 存入字典
                    if (!soundCenter.ContainsKey(soundPools[i].soundStyle))
                    {
                        // 没有 Key 时先创建队列，再放入实例
                        soundCenter.Add(soundPools[i].soundStyle, new Queue<GameObject>());
                        soundCenter[soundPools[i].soundStyle].Enqueue(go);
                    }
                    else
                    {
                        // 已有队列时直接入队
                        soundCenter[soundPools[i].soundStyle].Enqueue(go);
                    }
                }
            }
        }
    }

    /// <summary>从大池取音效（按音效名 + 类型），移动到指定位置播放</summary>
    public void TryGetSoundPool(SoundStyle soundStyle, string soundName, Vector3 position)
    {
        if (bigSoundCenter.ContainsKey(soundName))
        {
            if (bigSoundCenter[soundName].TryGetValue(soundStyle, out var Q))
            {
                GameObject go = Q.Dequeue();
                go.transform.position = position;
                go.gameObject.SetActive(true);
                Q.Enqueue(go);
                // Debug.Log("取出" + soundName + "类型" + soundStyle);
            }
            else
            {
                // Debug.LogWarning(soundStyle + "找不到");
            }
        }
        else
        {
            // Debug.LogWarning(soundName + "找不到");
        }
    }

    /// <summary>从普通池取音效（按类型），移动到指定位置和角度播放</summary>
    public void TryGetSoundPool(SoundStyle soundStyle, Vector3 position, Quaternion quaternion)
    {
        if (soundCenter.TryGetValue(soundStyle, out var sound))
        {
            // Debug.Log(soundStyle + "存在");
            GameObject go = sound.Dequeue();
            go.transform.position = position;
            go.gameObject.SetActive(true);
            soundCenter[soundStyle].Enqueue(go);
        }
        else
        {
            // Debug.Log(soundStyle + "不存在");
        }
    }


}

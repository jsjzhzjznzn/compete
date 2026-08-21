using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 特效对象池管理器（单例）
/// 预生成所有特效预制体放进池子，需要时取一个播放，播完自动回池。
/// 结构：角色 → 特效名 → 可用队列，三层索引（VFXItemData 按角色配置）。
///
/// 取用策略（与音频池一致，修掉了"取出即放回"环形池的 bug）：
/// 优先取"空闲（未激活）"的实例，全部在播时才按队列顺序抢占最旧的一个。
/// 这样同一特效连续多次请求时，各自拿到独立实例，不会互相打断。
/// </summary>
public class VFX_PoolManager : Singleton<VFX_PoolManager>
{
    /// <summary>配置项：角色 → 该角色的一套特效数据</summary>
    [System.Serializable]
    public class effectData
    {
        [Header("角色名（Null 表示通用特效，所有角色共用）")]
        public CharacterNameList style;

        [Header("该角色的特效配置资产")]
        public VFXItemData effectItemData;
    }

    [Header("Inspector 配置：角色 → 特效资产 列表")]
    [SerializeField] public List<effectData> effectDates = new List<effectData>();

    /// <summary>大池：角色 → 特效名 → 可用特效队列</summary>
    private Dictionary<CharacterNameList, Dictionary<string, Queue<GameObject>>> effectPool =
        new Dictionary<CharacterNameList, Dictionary<string, Queue<GameObject>>>();

    protected override void Awake()
    {
        base.Awake();
        InitEffectPools();
    }

    /// <summary>根据配置预生成所有特效实例并放入对应池中</summary>
    private void InitEffectPools()
    {
        if (effectDates.Count == 0) { return; }

        for (int i = 0; i < effectDates.Count; i++)
        {
            // 不存在该角色时创建一级键（角色）
            if (!effectPool.ContainsKey(effectDates[i].style))
            {
                effectPool.Add(effectDates[i].style, new Dictionary<string, Queue<GameObject>>());
            }

            for (int j = 0; j < effectDates[i].effectItemData.effectItems.Count; j++)
            {
                var item = effectDates[i].effectItemData.effectItems[j];

                // 欧拉角转四元数缓存，取出时直接赋值
                item.effectRotation = Quaternion.Euler(item.effectEulerAngle);

                for (int k = 0; k < item.count; k++)
                {
                    // 实例化
                    GameObject go = Instantiate(item.VFXPrefab);

                    // 挂点：配置了 applyParentPos 则挂到指定父物体，否则挂在管理器下
                    if (item.applyParentPos && item.parentPos != null)
                    {
                        go.transform.parent = item.parentPos;
                    }
                    else
                    {
                        go.transform.parent = this.transform;
                    }

                    // 位置归零（相对父物体）
                    go.transform.localPosition = Vector3.zero;
                    // 旋转用缓存的四元数
                    go.transform.localRotation = item.effectRotation;
                    // 禁用（入池状态）
                    go.SetActive(false);

                    // 不存在该特效名时创建二级键（特效名）
                    if (!effectPool[effectDates[i].style].ContainsKey(item.VFXName))
                    {
                        effectPool[effectDates[i].style].Add(item.VFXName, new Queue<GameObject>());
                    }
                    effectPool[effectDates[i].style][item.VFXName].Enqueue(go);
                }
            }
        }
    }

    /// <summary>
    /// 只在角色挂点处播放特效（不指定世界位置，用预制体自身的挂点/本地位置）。
    /// 适合"跟角色走"的特效，如武器挥砍光效、受击附身效果。
    /// </summary>
    /// <param name="characterName">角色名</param>
    /// <param name="effectName">特效标识名</param>
    public void TryGetVFX(CharacterNameList characterName, string effectName)
    {
        GameObject go = GetFreeVFX(characterName, effectName);
        if (go == null)
        {
            Debug.LogWarning($"特效池找不到：{characterName} / {effectName}，请检查 VFXItemData 配置");
            return;
        }
        go.SetActive(true);
    }

    /// <summary>
    /// 在世界坐标指定位置播放特效。
    /// 适合"跟场景走"的特效，如爆炸、命中火花、掉落特效。
    /// </summary>
    /// <param name="characterName">角色名</param>
    /// <param name="effectName">特效标识名</param>
    /// <param name="worldPos">世界坐标（默认 Vector3.zero）</param>
    /// <param name="quaternion">世界旋转（默认无旋转）</param>
    public void GetVFX(CharacterNameList characterName, string effectName,
        Vector3 worldPos = default(Vector3), Quaternion quaternion = default(Quaternion))
    {
        GameObject go = GetFreeVFX(characterName, effectName);
        if (go == null)
        {
            Debug.LogWarning($"特效池找不到：{characterName} / {effectName}，请检查 VFXItemData 配置");
            return;
        }

        go.transform.position = worldPos;
        go.transform.rotation = quaternion;
        go.SetActive(true);
    }

    /// <summary>
    /// 在指定父物体（如武器骨骼）下播放特效，作为子物体跟随父物体移动。
    /// 适合"跟武器走"的攻击刀光/挥砍特效：挂到骨骼下后自动跟随动画移动，无需每帧更新。
    /// </summary>
    /// <param name="characterName">角色名</param>
    /// <param name="effectName">特效标识名</param>
    /// <param name="attachPoint">挂点（通常为武器骨骼）</param>
    /// <returns>激活的特效 GameObject，未取到返回 null</returns>
    public GameObject GetAttachedVFX(CharacterNameList characterName, string effectName, Transform attachPoint)
    {
        if (attachPoint == null)
        {
            Debug.LogWarning("GetAttachedVFX: 挂点为空，特效无法挂载到骨骼");
            return null;
        }

        GameObject go = GetFreeVFX(characterName, effectName);
        if (go == null)
        {
            Debug.LogWarning($"特效池找不到：{characterName} / {effectName}，请检查 VFXItemData 配置");
            return null;
        }

        // 重新挂到目标骨骼下，局部位置/旋转归零
        go.transform.SetParent(attachPoint, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.SetActive(true);
        return go;
    }

    /// <summary>
    /// 从池中取一个空闲特效：
    /// 优先找"未激活"的实例（空闲）；全部在播则按队列顺序取队头（抢占最旧的）。
    /// 注意：取出的实例不会立刻放回队列——由 EffectItem 播完自动 SetActive(false) 回池，
    /// 下次再取时它会作为"空闲实例"被优先选中。
    /// </summary>
    private GameObject GetFreeVFX(CharacterNameList characterName, string effectName)
    {
        if (!effectPool.ContainsKey(characterName)) return null;
        if (!effectPool[characterName].ContainsKey(effectName)) return null;
        if (effectPool[characterName][effectName].Count == 0) return null;

        Queue<GameObject> queue = effectPool[characterName][effectName];

        // 优先找空闲（未激活）实例
        foreach (GameObject go in queue)
        {
            if (go != null && !go.activeSelf)
            {
                return go;
            }
        }

        // 全部在播：取队头抢占最旧的一个（特效被掐断重播，可接受）
        GameObject oldest = queue.Dequeue();
        queue.Enqueue(oldest);   // 保持队列长度不变（环形）
        return oldest;
    }
}

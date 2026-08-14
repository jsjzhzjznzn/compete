using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

/// <summary>
/// 连招容器配置资源
/// 统一管理一套角色完整普攻连招、闪避攻击、后闪攻击，提供对外统一读取接口
/// 可在编辑器创建资源，存储该角色全部ComboData数据
/// </summary>
[CreateAssetMenu(fileName = "ComboContainerData", menuName = "Create/Asset/CoomboContainerData")]
public class ComboContainerData : ScriptableObject
{
    [Header("基础连招列表（常规平A连击序列）")]
    [SerializeField] public List<ComboData> comboDates = new List<ComboData>();

    [SerializeField, Header("闪避攻击连招数据")] 
    public ComboData DodgeATKData;

    [SerializeField, Header("后闪/后撤攻击连招数据")] 
    public ComboData BackDodgeATKData;

    /// <summary>缓存初始第一套普攻连招，用于闪避攻击后恢复原连招</summary>
    private ComboData firstComboData;

    /// <summary>
    /// 初始化缓存原始首段连招
    /// 游戏加载/角色初始化时调用，保存默认普攻第一段
    /// </summary>
    public void Init()
    {
        // 无连招配置直接返回
        if (comboDates.Count == 0) 
            return;
        
        // 缓存列表第一个基础连招
        firstComboData = comboDates[0];
        Debug.Log("连招容器初始化完成");
    }

    /// <summary>
    /// 根据索引获取对应连招的动画/事件标识名称
    /// </summary>
    /// <param name="index">连招列表下标</param>
    /// <returns>连招名称字符串</returns>
    public string GetComboName(int index)
    {
        if (comboDates.Count == 0) 
            return null;

        // 空名称弹出警告提示配置缺失
        if (comboDates[index].comboName == null)
            Debug.LogWarning($"下标{index}连招未配置连招名称");
        
        return comboDates[index].comboName;
    }

    /// <summary>
    /// 切换当前第一段连招为闪避攻击
    /// 闪避触发时调用，替换普攻第一段逻辑
    /// </summary>
    public void SwitchDodgeATK()
    {
        if (DodgeATKData == null) 
            return;
        
        comboDates[0] = DodgeATKData;
    }

    /// <summary>
    /// 切换当前第一段连招为后闪攻击
    /// 后撤闪避触发时调用
    /// </summary>
    public void SwitchBackDodgeATK()
    {
        if (BackDodgeATKData == null) 
            return;
        
        comboDates[0] = BackDodgeATKData;
    }

    /// <summary>
    /// 重置第一段连招为初始化缓存的原始普攻
    /// 闪避攻击结束后恢复正常平A连招
    /// </summary>
    public void ResetComboDates()
    {
        if (comboDates == null)
        {
            Debug.Log("连招列表为空");
            return;
        }

        // 当前第一段不等于初始连招时执行还原
        if (comboDates[0] != firstComboData)
        {
            comboDates[0] = firstComboData;
            Debug.Log($"连招已还原为默认：{comboDates[0].name}");
        }
    }

    /// <summary>
    /// 获取指定连招的冷却时间
    /// </summary>
    /// <param name="index">连招下标</param>
    /// <returns>连招冷却时长</returns>
    public float GetComboColdTime(int index)
    {
        if (comboDates.Count == 0) 
            return 0;

        if (comboDates[index].comboColdTime == 0)
            Debug.LogWarning($"下标{index}连招未配置冷却时间");
        
        return comboDates[index].comboColdTime;
    }

    /// <summary>
    /// 获取指定连招攻击判定距离
    /// </summary>
    /// <param name="index">连招下标</param>
    /// <returns>攻击有效距离</returns>
    public float GetComboDistance(int index)
    {
        if (comboDates.Count == 0) 
            return 0;

        if (comboDates[index].attackDistance == 0)
            Debug.LogWarning($"下标{index}连招未配置攻击距离");
        
        return comboDates[index].attackDistance;
    }

    /// <summary>
    /// 获取连招攻击碰撞盒前后偏移值
    /// </summary>
    /// <param name="index">连招下标</param>
    /// <returns>攻击盒偏移量</returns>
    public float GetComboOffset(int index)
    {
        if (comboDates.Count == 0) 
            return 0;

        if (comboDates[index].comboOffset == 0)
            Debug.LogWarning($"下标{index}连招未配置攻击盒偏移");
        
        return comboDates[index].comboOffset;
    }

    // 以下为废弃注释音效接口，保留不作删除
    //public AudioClip  GetComboSound(int index)
    //{
    //    if (comboDates.Count == 0) { return null; }
    //    if (comboDates[index].weaponSound == null) { Debug.LogWarning(index + "该连招没有配置音效"); }
    //    return comboDates[index].weaponSound;
    //}
    //public AudioClip GetCharacterVoice(int index)
    //{
    //    if (comboDates.Count == 0) { return null; }
    //    if (comboDates[index].characterVoice == null) { Debug.LogWarning(index + "该连招没有配置语音"); }
    //    return comboDates[index].characterVoice;
    //}
    //public GameObject GetCharacterVoicePrefab(int index)
    //{
    //    if (comboDates.Count == 0) { return null; }
    //    if (comboDates[index].characterVoicePrefab == null) { Debug.LogWarning(index + "该连招没有配置音效预制体"); }
    //    return comboDates[index].characterVoicePrefab;
    //}

    //public GameObject GetComboSoundPrefab(int index)
    //{
    //    if (comboDates.Count == 0) { return null; }
    //    if (comboDates[index].weaponSoundPrefab == null) { Debug.LogWarning(index + "该连招没有配置音效预制体"); }
    //    return comboDates[index].weaponSoundPrefab;
    //}

    /// <summary>
    /// 随机获取连招受击特效名称
    /// </summary>
    /// <param name="index">连招下标</param>
    /// <returns>受击特效标识名</returns>
    public string GetComboHitName(int index)
    {
        if (comboDates.Count == 0) 
            return null;

        if (comboDates[index].hitName == null)
            Debug.LogWarning($"下标{index}连招未配置受击特效");
        
        return comboDates[index].hitName;
    }

    /// <summary>
    /// 随机获取连招格挡/弹反特效名称
    /// </summary>
    /// <param name="index">连招下标</param>
    /// <returns>弹反特效标识名</returns>
    public string GetComboParryName(int index)
    {
        if (comboDates.Count == 0) 
            return null;

        if (comboDates[index].parryName == null)
            Debug.LogWarning($"下标{index}连招未配置弹反特效");
        
        return comboDates[index].parryName;
    }

    /// <summary>
    /// 获取连招列表总数量（最大连招下标边界）
    /// </summary>
    /// <returns>连招总数</returns>
    public int GetComboMaxCount()
    {
        if (comboDates.Count == 0) 
            return 0;
        
        return comboDates.Count;
    }

    /// <summary>
    /// 获取整套连招总伤害数值
    /// </summary>
    /// <param name="index">连招下标</param>
    /// <returns>连招总伤害</returns>
    public float GetComboDamage(int index)
    {
        if (comboDates.Count == 0) 
            return 0f;

        if (comboDates[index].comboDamage == 0)
            Debug.LogWarning($"下标{index}连招未配置伤害数值");
        
        return comboDates[index].comboDamage;
    }

    /// <summary>
    /// 获取连招全局通用音效规则类型
    /// </summary>
    /// <param name="index">连招下标</param>
    /// <returns>音效样式枚举</returns>
    public SoundStyle GetComboSoundStyle(int index)
    {
        if (comboDates[index].comboDamage == 0)
            Debug.LogWarning($"下标{index}连招未配置通用音效Style");
        
        return comboDates[index].universalSound;
    }

    /// <summary>
    /// 获取指定连击段数对应的相机震动力度
    /// </summary>
    /// <param name="index">连招列表下标</param>
    /// <param name="ATKIndex">当前第几段攻击（从1开始）</param>
    /// <returns>对应段震动强度，无配置返回0</returns>
    public float GetComboShakeForce(int index, int ATKIndex)
    {
        // 震动数组为空 或 当前攻击段数超过数组长度，返回0无震动
        if (comboDates[index].shakeForce == null || ATKIndex > comboDates[index].shakeForce.Length)
        {
            return 0;
        }
        // ATKIndex从1计数，数组下标从0，需要-1偏移
        return comboDates[index].shakeForce[ATKIndex - 1];
    }

    /// <summary>
    /// 获取指定连击段数的动作动画
    /// </summary>
    /// <param name="index">连招列表下标</param>
    /// <param name="ATKIndex">当前第几段攻击（从1开始）</param>
    /// <returns>对应段的 ClipTransition，未配置返回 null</returns>
    public ClipTransition GetAttackClip(int index, int ATKIndex)
    {
        // 动画数组为空 或 当前攻击段数超过数组长度，返回 null 无动画
        if (comboDates[index].attackClips == null || ATKIndex > comboDates[index].attackClips.Length)
        {
            return null;
        }
        // ATKIndex从1计数，数组下标从0，需要-1偏移
        return comboDates[index].attackClips[ATKIndex - 1];
    }

    /// <summary>
    /// 获取连招结束收尾动画
    /// </summary>
    /// <param name="index">连招列表下标</param>
    /// <returns>收尾动画，未配置返回 null</returns>
    public ClipTransition GetAttackEndClip(int index)
    {
        return comboDates[index].attackEndClip;
    }

    /// <summary>
    /// 获取连招总攻击段数（几连击）
    /// </summary>
    /// <param name="index">连招下标</param>
    /// <returns>连招段数</returns>
    public int GetComboATKCount(int index)
    {
        return comboDates[index].ATKCount;
    }

    /// <summary>
    /// 获取当前段攻击帧暂停时长
    /// 优先读取分段自定义暂停时间，无配置则使用全局兜底暂停时间
    /// </summary>
    /// <param name="index">连招下标</param>
    /// <param name="ATKIndex">当前攻击段数（从1开始）</param>
    /// <returns>帧暂停持续时间</returns>
    public float GetPauseFrameTime(int index, int ATKIndex)
    {
        // 分段时间数组不存在/段数超出范围，读取全局默认暂停时间
        if (comboDates[index].pauseFrameTimeList == null || ATKIndex > comboDates[index].pauseFrameTimeList.Length)
        {
            return GetComboPauseFrameTime(index);
        }
        return comboDates[index].pauseFrameTimeList[ATKIndex - 1];
    }

    /// <summary>
    /// 私有兜底方法：获取连招全局统一帧暂停时间
    /// </summary>
    private float GetComboPauseFrameTime(int index)
    {
        return comboDates[index].pauseFrameTime;
    }
}

using System.Collections.Generic;
using Animancer;
using UnityEngine;

/// <summary>
/// 连招容器配置资源
/// 统一管理一套角色完整普攻连招、闪避攻击、后闪攻击，提供对外统一读取接口
/// comboDates 列表的下标即攻击段数：comboDates[0]=第一段攻击，comboDates[1]=第二段攻击，以此类推
/// 每段攻击在容器里占一个 ComboData 元素，存放该段的动画、收尾、伤害、震动、暂停等数据
/// </summary>
[CreateAssetMenu(fileName = "ComboContainerData", menuName = "Create/Asset/CoomboContainerData")]
public class ComboContainerData : ScriptableObject
{
    [Header("连招段序列（下标0=第一段攻击，下标1=第二段攻击...）")]
    [SerializeField] public List<ComboData> comboDates = new List<ComboData>();

    [SerializeField, Header("闪避攻击连招数据")] 
    public ComboData DodgeATKData;

    [SerializeField, Header("后闪/后撤攻击连招数据")] 
    public ComboData BackDodgeATKData;

    /// <summary>缓存初始第一段连招，用于闪避攻击后恢复原连招</summary>
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
    /// 按段数下标安全获取该段的 ComboData，越界返回 null
    /// </summary>
    /// <param name="index">攻击段数（0=第一段，1=第二段...）</param>
    /// <returns>该段的连招数据，越界返回 null</returns>
    public ComboData GetComboData(int index)
    {
        if (comboDates.Count == 0 || index < 0 || index >= comboDates.Count) 
            return null;
        
        return comboDates[index];
    }

    /// <summary>
    /// 获取连招总段数
    /// </summary>
    public int GetComboMaxCount()
    {
        return comboDates.Count;
    }

    /// <summary>
    /// 获取该段的动画/事件标识名称
    /// </summary>
    /// <param name="index">攻击段数（0=第一段）</param>
    /// <returns>连招名称字符串</returns>
    public string GetComboName(int index)
    {
        var data = GetComboData(index);
        if (data == null) 
            return null;

        // 空名称弹出警告提示配置缺失
        if (data.comboName == null)
            Debug.LogWarning($"下标{index}连招未配置连招名称");
        
        return data.comboName;
    }

    /// <summary>
    /// 获取该段的冷却时间
    /// </summary>
    public float GetComboColdTime(int index)
    {
        var data = GetComboData(index);
        if (data == null) 
            return 0;

        if (data.comboColdTime == 0)
            Debug.LogWarning($"下标{index}连招未配置冷却时间");
        
        return data.comboColdTime;
    }

    /// <summary>
    /// 获取该段的攻击判定距离
    /// </summary>
    public float GetComboDistance(int index)
    {
        var data = GetComboData(index);
        if (data == null) 
            return 0;

        if (data.attackDistance == 0)
            Debug.LogWarning($"下标{index}连招未配置攻击距离");
        
        return data.attackDistance;
    }

    /// <summary>
    /// 获取该段的攻击碰撞盒前后偏移值
    /// </summary>
    public float GetComboOffset(int index)
    {
        var data = GetComboData(index);
        if (data == null) 
            return 0;

        if (data.comboOffset == 0)
            Debug.LogWarning($"下标{index}连招未配置攻击盒偏移");
        
        return data.comboOffset;
    }

    /// <summary>
    /// 随机获取该段的受击特效名称
    /// </summary>
    public string GetComboHitName(int index)
    {
        var data = GetComboData(index);
        if (data == null) 
            return null;

        if (data.hitName == null)
            Debug.LogWarning($"下标{index}连招未配置受击特效");
        
        return data.hitName;
    }

    /// <summary>
    /// 随机获取该段的格挡/弹反特效名称
    /// </summary>
    public string GetComboParryName(int index)
    {
        var data = GetComboData(index);
        if (data == null) 
            return null;

        if (data.parryName == null)
            Debug.LogWarning($"下标{index}连招未配置弹反特效");
        
        return data.parryName;
    }

    /// <summary>
    /// 获取该段的伤害数值
    /// </summary>
    public float GetComboDamage(int index)
    {
        var data = GetComboData(index);
        if (data == null) 
            return 0f;

        if (data.comboDamage == 0)
            Debug.LogWarning($"下标{index}连招未配置伤害数值");
        
        return data.comboDamage;
    }

    /// <summary>
    /// 获取该段的通用音效规则类型
    /// </summary>
    public SoundStyle GetComboSoundStyle(int index)
    {
        var data = GetComboData(index);
        if (data == null) 
            return default;

        return data.universalSound;
    }

    /// <summary>
    /// 获取该段的攻击动作动画
    /// </summary>
    public ClipTransition GetAttackClip(int index)
    {
        var data = GetComboData(index);
        return data?.attackClip;
    }

    /// <summary>
    /// 获取该段的收尾动画（播完自动回待机）
    /// </summary>
    public ClipTransition GetAttackEndClip(int index)
    {
        var data = GetComboData(index);
        return data?.attackEndClip;
    }

    /// <summary>
    /// 获取该段的相机震动力度
    /// </summary>
    public float GetComboShakeForce(int index)
    {
        var data = GetComboData(index);
        if (data == null) 
            return 0;

        return data.shakeForce;
    }

    /// <summary>
    /// 获取该段的帧暂停时长
    /// </summary>
    public float GetPauseFrameTime(int index)
    {
        var data = GetComboData(index);
        if (data == null) 
            return 0;

        return data.pauseFrameTime;
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
        if (comboDates == null || comboDates.Count == 0)
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
}

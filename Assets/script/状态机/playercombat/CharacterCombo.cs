using Animancer;
using UnityEngine;

/// <summary>
/// 连击战斗逻辑辅助类
/// 参考旧版 CharacterComboBase 重构，适配本项目 Animancer + ComboContainerData + PlayerComboReusableData 架构
///
/// 职责：选招（轻/重/闪避）→ 连击推进（换段动画）→ 动画事件回调（可输入/冷却/锁连击）
///       → 伤害触发入口（距离角度判定、帧暂停、QTE 检查）
///
/// 说明：音效池、伤害事件、敌人系统、相机震动在本项目尚未接入，相关位置以 TODO 占位，接入后补实现
/// </summary>
public class CharacterCombo
{
    private readonly Player player;
    private readonly PlayerComboReusableData reusableData;
    private readonly PlayerComboSOData comboData;

    public CharacterCombo(Player player, PlayerComboReusableData reusableData, PlayerComboSOData comboData)
    {
        this.player = player;
        this.reusableData = reusableData;
        this.comboData = comboData;

        reusableData.cameraTransform = player.CameraTransform;
    }

    // ==================== 初始化 ====================

    /// <summary>初始化所有连招容器（缓存首段用于闪避后还原），角色初始化时调用一次</summary>
    public void Init()
    {
        comboData?.lightCombo?.Init();
        // comboData?.heavyCombo?.Init();      // heavyCombo 未配置（PlayerComboSOData 中注释保留）
        // comboData?.executeCombo?.Init();    // executeCombo 未配置（PlayerComboSOData 中注释保留）
    }

    /// <summary>技能招式数据（技能状态用）</summary>
    public ComboData SkillCombo => comboData?.skillCombo;

    // ==================== 输入门控 ====================

    /// <summary>是否允许发动基础连招：处于可输入窗口且当前未在执行其他招式</summary>
    public virtual bool CanBaseComboInput()
    {
        if (!reusableData.canInput) return false;
        return true;
    }

    // ==================== 选招 / 进入连击 ====================

    /// <summary>轻攻击连招</summary>
    public virtual void LightComboInput()
    {
        if (comboData?.lightCombo == null) return;

        // 换到轻击容器时重置连击信息
        if (reusableData.currentCombo != comboData.lightCombo || reusableData.currentCombo == null)
        {
            reusableData.currentCombo = comboData.lightCombo;
            ReSetComboInfo();
        }

        // 确保前进攻击后首段恢复正常普攻
        reusableData.currentCombo.ResetComboDates();
        ExecuteBaseCombo();
    }

    /// <summary>重攻击连招（heavyCombo 未配置，PlayerComboSOData 中注释保留）</summary>
    /*public virtual void HeavyComboInput()
    {
        if (comboData?.heavyCombo == null) return;

        if (reusableData.currentCombo != comboData.heavyCombo || reusableData.currentCombo == null)
        {
            reusableData.currentCombo = comboData.heavyCombo;
            ReSetComboInfo();
        }

        ExecuteBaseCombo();
    }*/

    /// <summary>前进攻击：临时把首段替换为前进攻击段再执行</summary>
    public virtual void ForwardCombo()
    {
        if (comboData?.lightCombo == null) return;

        if (reusableData.currentCombo != comboData.lightCombo || reusableData.currentCombo == null)
        {
            reusableData.currentCombo = comboData.lightCombo;
        }

        reusableData.currentCombo.SwitchForwardATK();
        ReSetComboInfo();
        ExecuteBaseCombo();
    }

    /// <summary>进入攻击流程：写入攻击指令，关闭输入窗口（等攻击段播完再开）</summary>
    protected virtual void ExecuteBaseCombo()
    {
        if (reusableData.currentCombo == null) return;

        reusableData.hasATKCommand = true;
        reusableData.canInput = false;
    }

    // ==================== 重置连击信息 ====================

    /// <summary>重置连击信息：回到第一段、打开输入、允许连击、冷却就绪、清空输入缓冲</summary>
    public virtual void ReSetComboInfo()
    {
        reusableData.currentIndex.Value = 0;
        reusableData.canInput = true;
        reusableData.canLink = true;
        reusableData.canMoveInterrupt = false;
        reusableData.canATK = true;
        reusableData.hasATKCommand = false;   // 清空输入缓冲，防止上一轮攻击指令残留导致自动重新起手
    }

    // ==================== 动画事件回调（动画时间轴触发） ====================

    /// <summary>事件：禁止继续连击（例如被格挡/断连）</summary>
    public void DisConnectCombo()
    {
        reusableData.canLink = false;
    }

    /// <summary>事件：允许被移动打断</summary>
    public void CanMoveInterrupt()
    {
        reusableData.canMoveInterrupt = true;
    }

    /// <summary>事件：打开可输入窗口（预备/收招阶段）</summary>
    public void CanInput()
    {
        reusableData.canInput = true;
    }

    /// <summary>事件：冷却结束，允许发动下一段攻击</summary>
    public void CanATK()
    {
        reusableData.canATK = true;
    }

    /// <summary>事件：播放连击通用音效</summary>
    public void PlayComboFX()
    {
        // TODO: 接入音效池 SFX_PoolManager
        // SFX_PoolManager.MainInstance.TryGetSoundPool(
        //     reusableData.currentCombo.GetComboSoundStyle(reusableData.currentIndex.Value),
        //     player.transform.position, Quaternion.identity);
    }

    // ==================== 伤害触发（打击帧回调） ====================

    /// <summary>打击帧动画事件入口：根据当前招式类型走不同伤害逻辑</summary>
    public void ATK()
    {
        AttackTrigger();
    }

    protected virtual void AttackTrigger()
    {
        var combo = reusableData.currentCombo;
        if (combo == null) return;

        // TODO: 接入相机震动
        // CameraHitFeel.MainInstance.CameraShake(combo.GetComboShakeForce(reusableData.currentIndex.Value));

        if (!AttackDetection(combo)) return;

        // TODO: 接入伤害事件系统
        // GameEventsManager.MainInstance.CallEvent("造成伤害",
        //     combo.GetComboDamage(reusableData.currentIndex.Value),
        //     combo.GetComboHitName(reusableData.currentIndex.Value),
        //     combo.GetComboParryName(reusableData.currentIndex.Value),
        //     player.transform, ...);
    }

    /// <summary>攻击判定：距离 + 角度（敌人系统接入后补实现）</summary>
    protected bool AttackDetection(ComboContainerData comboContainerData)
    {
        // TODO: 接入敌人系统（GameBlackboard.GetEnemy()），当前直接放行
        return true;
    }

    // ==================== 辅助 ====================

    /// <summary>攻击时转向敌人（敌人系统接入后补实现）</summary>
    public void UpdateAttackLookAtEnemy()
    {
        // TODO: 接入敌人系统后实现 Look 转向
    }

    /// <summary>可被打断且玩家在移动时，打断攻击（切回移动状态）</summary>
    public void CheckMoveInterrupt()
    {
        if (!reusableData.canMoveInterrupt) return;
        if (!player.IsMoving) return;

        // TODO: 通知状态机切回移动状态（如 idlingState / walkingState）
        reusableData.canMoveInterrupt = false;
    }

    /// <summary>不满足连击条件或角色在冲刺时，重置连击回到第一段</summary>
    public void CheckCanLinkCombo()
    {
        if (!reusableData.canLink || player.IsSprintHeld)
        {
            ReSetComboInfo();
        }
    }

    // ==================== 音效（音效池接入后补实现） ====================

    /// <summary>播放角色语音（攻击段开始时调用）</summary>
    public void PlayCharacterVoice(ComboData data)
    {
        if (data == null) return;
        // TODO: 接入角色语音音效池
    }

    /// <summary>播放武器挥砍音效（攻击段开始时调用）</summary>
    public void PlayWeaponSound(ComboData data)
    {
        if (data == null) return;
        // TODO: 接入武器挥砍音效池
    }
}

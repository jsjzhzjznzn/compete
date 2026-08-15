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
        comboData?.heavyCombo?.Init();
        comboData?.executeCombo?.Init();
    }

    // ==================== 事件订阅 ====================

    /// <summary>订阅下标变化（用于重置 ATKIndex 等），状态 OnEnter 时调用</summary>
    public void AddEventAction()
    {
        reusableData.currentIndex.OnValueChanged += ReSetATKIndex;
    }

    /// <summary>退订，状态 OnExit 时调用</summary>
    public void RemoveEventAction()
    {
        reusableData.currentIndex.OnValueChanged -= ReSetATKIndex;
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

        // 确保闪避攻击后首段恢复正常普攻
        reusableData.currentCombo.ResetComboDates();
        ExecuteBaseCombo();
    }

    /// <summary>重攻击连招</summary>
    public virtual void HeavyComboInput()
    {
        if (comboData?.heavyCombo == null) return;

        if (reusableData.currentCombo != comboData.heavyCombo || reusableData.currentCombo == null)
        {
            reusableData.currentCombo = comboData.heavyCombo;
            ReSetComboInfo();
        }

        ExecuteBaseCombo();
    }

    /// <summary>闪避攻击：临时把首段替换为闪避攻击段再执行</summary>
    public virtual void NormalDodgeCombo()
    {
        if (comboData?.lightCombo == null) return;

        if (reusableData.currentCombo != comboData.lightCombo || reusableData.currentCombo == null)
        {
            reusableData.currentCombo = comboData.lightCombo;
        }

        reusableData.currentCombo.SwitchDodgeATK();
        ReSetComboInfo();
        ReSetATKIndex(0);
        ExecuteBaseCombo();
    }

    /// <summary>进入攻击流程：写入攻击指令，关闭输入窗口（等攻击段播完再开）</summary>
    protected virtual void ExecuteBaseCombo()
    {
        if (reusableData.currentCombo == null) return;

        reusableData.hasATKCommand = true;
        reusableData.canInput = false;
    }

    // ==================== 连击推进（每帧） ====================

    /// <summary>
    /// 冷却结束(canATK) 且 有攻击指令(hasATKCommand) 时，播放当前段的攻击动画
    /// 由状态机的 OnUpdate 调用
    /// </summary>
    public virtual void UpdateComboAnimation()
    {
        if (!reusableData.canATK) return;
        if (!reusableData.hasATKCommand) return;

        var combo = reusableData.currentCombo;
        if (combo == null) return;

        reusableData.currentIndex.Value = reusableData.comboIndex;

        var transition = combo.GetAttackClip(reusableData.currentIndex.Value);
        if (transition != null)
        {
            var state = player.characterAnimancer.Play(transition);
            state.Events.OnEnd = OnAttackClipEnd;
        }

        var data = combo.GetComboData(reusableData.currentIndex.Value);
        PlayCharacterVoice(data);
        PlayWeaponSound(data);

        UpdateComboInfo();

        reusableData.hasATKCommand = false;
        reusableData.canATK = false;
    }

    /// <summary>本段攻击动画播完：子类/外部决定连下一段还是收招</summary>
    protected virtual void OnAttackClipEnd()
    {
        // TODO: 连击逻辑由具体状态处理（连下一段/进收招），此处留空
    }

    /// <summary>推进连击段数下标（到尾段归零，形成循环）</summary>
    protected virtual void UpdateComboInfo()
    {
        reusableData.comboIndex++;
        if (reusableData.comboIndex > reusableData.currentCombo.GetComboMaxCount() - 1)
        {
            reusableData.comboIndex = 0;
        }
    }

    /// <summary>重置连击信息：回到第一段、打开输入、允许连击、冷却就绪</summary>
    public virtual void ReSetComboInfo()
    {
        reusableData.comboIndex = 0;
        reusableData.canInput = true;
        reusableData.canLink = true;
        reusableData.canMoveInterrupt = false;
        reusableData.canATK = true;
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

        UpdateATKIndex();

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

    // ==================== 下标管理 ====================

    /// <summary>重置 ATKIndex 为 0（进入新连招/闪避攻击时调用）</summary>
    public void ReSetATKIndex(int index)
    {
        reusableData.ATKIndex = 0;
    }

    /// <summary>ATKIndex 自增（每次打击帧触发）</summary>
    public void UpdateATKIndex()
    {
        reusableData.ATKIndex++;
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

    private void PlayCharacterVoice(ComboData data)
    {
        if (data == null) return;
        // TODO: 接入角色语音音效池
    }

    private void PlayWeaponSound(ComboData data)
    {
        if (data == null) return;
        // TODO: 接入武器挥砍音效池
    }
}

using System.Collections.Generic;
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
        var data = reusableData.currentCombo?.GetComboData(reusableData.currentIndex.Value);
        if (data == null) return;
        // 通用音效类型：查 SoundData 表播放（未配置则为 Null，PlayByStyle 内部会忽略）
        player.ActorAudio?.PlayByStyle(data.universalSound);
    }

    // ==================== 伤害触发（打击帧回调） ====================

    /// <summary>本段已结算过的受击单位（同一段/同一次判定内防重复扣血，按受击单位 GameObject 去重）</summary>
    private readonly HashSet<GameObject> _hitTargets = new();

    /// <summary>预分配的命中检测缓冲（OverlapSphereNonAlloc 用，避免每次打击帧 GC 分配；敌人多时调大）</summary>
    private readonly Collider[] _detectBuffer = new Collider[16];

    /// <summary>打击帧动画事件入口：根据当前招式类型走不同伤害逻辑</summary>
    public void ATK()
    {
        AttackTrigger();
    }

    protected virtual void AttackTrigger()
    {
        var combo = reusableData.currentCombo;
        if (combo == null) return;

        int index = reusableData.currentIndex.Value;
        var data = combo.GetComboData(index);
        if (data == null) return;

        // TODO: 接入相机震动（相机震动系统尚未接入，CameraHitFeel 类不存在）
        // if (data.shakeForce > 0f && CameraHitFeel.MainInstance != null)
        //     CameraHitFeel.MainInstance.CameraShake(data.shakeForce);

        // 命中判定：收集本段打击范围内的目标（结果写入 _detectBuffer，返回有效数量）
        int hitCount = AttackDetection(combo, data);
        if (hitCount == 0) return;

        // 本次打击帧独立结算（同一目标多 Collider / 多次命中只扣一次血）
        _hitTargets.Clear();

        bool anyHit = false;
        for (int i = 0; i < hitCount; i++)
        {
            var target = _detectBuffer[i].GetComponentInParent<HealthModel>();
            if (target == null) continue;                       // 没有受击组件（地形/装饰）跳过

            if (!_hitTargets.Add(target.gameObject)) continue;  // 同一受击单位只结算一次

            target.TakeDamage(data.comboDamage, player.gameObject, false);
            anyHit = true;
        }

        // 命中顿帧（打中敌人时 timeScale 压低，打击感关键；realTime 后由 Player.HitStop 恢复）
        if (anyHit && data.pauseFrameTime > 0f)
        {
            player.HitStop(data.pauseFrameTime);
        }
    }

    /// <summary>
    /// 攻击判定：以角色前方偏移为球心做 OverlapSphere 收集命中目标。
    /// 命中点 = 角色位置 + 面朝方向 * 本段前移量(comboOffset)，半径 = 本段判定距离(attackDistance)，
    /// 按本段敌人层(ComboData.enemyLayer)过滤（未配置回退全层），并排除玩家自身及子物体（武器碰撞等）。
    /// 结果写入 _detectBuffer（原地压缩掉无效项），返回有效命中数量。
    /// </summary>
    protected virtual int AttackDetection(ComboContainerData comboContainerData, ComboData data)
    {
        var playerTransform = player.transform;
        Vector3 origin = playerTransform.position + playerTransform.forward * data.comboOffset;

        // 记录判定信息（供外部/调试读取）
        reusableData.detectionOrigin = origin;
        reusableData.detectionDir = playerTransform.forward;

        // 敌人层：本段 ComboData 里配置的 LayerMask；未配置(0)回退全层，靠下方 IsChildOf 排除玩家自身
        int layerMask = data.enemyLayer.value != 0 ? data.enemyLayer : Physics.DefaultRaycastLayers;

        int count = Physics.OverlapSphereNonAlloc(origin, data.attackDistance, _detectBuffer, layerMask);

        // 原地压缩：跳过玩家自身及子物体（全层过滤时可能把玩家自己打中）
        int valid = 0;
        for (int i = 0; i < count; i++)
        {
            if (_detectBuffer[i] == null) continue;
            if (_detectBuffer[i].transform.IsChildOf(playerTransform)) continue;
            if (valid != i) _detectBuffer[valid] = _detectBuffer[i];
            valid++;
        }
        return valid;
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

    // ==================== 音效（接入 ActorAudioComponent） ====================

    /// <summary>播放角色语音（攻击段开始时调用）</summary>
    public void PlayCharacterVoice(ComboData data)
    {
        if (data == null) return;
        player.ActorAudio?.PlayComboVoice(data);
    }

    /// <summary>播放武器挥砍音效（攻击段开始时调用）</summary>
    public void PlayWeaponSound(ComboData data)
    {
        if (data == null) return;
        player.ActorAudio?.PlayWeaponSound(data);
    }

   /* /// <summary>
    /// 播放攻击段挥砍特效（电光蓝刀光）。挂到角色武器骨骼下，随武器挥动移动。
    /// 攻击段开始时由 PlayerComboState.PlayAttackClip 调用。
    /// </summary>
    public void PlayAttackVFX(ComboData data)
    {
        if (data == null) return;

        Transform bone = player.WeaponBone;
        if (bone == null)
        {
            Debug.LogWarning("PlayAttackVFX: 武器骨骼为空，特效无法挂载");
            return;
        }

        VFX_PoolManager.MainInstance.GetAttachedVFX(data.characterName, "ATK_Slash", bone);
    }*/
}

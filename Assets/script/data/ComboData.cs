using Animancer;
using UnityEngine;

/// <summary>
/// 攻击招式类型枚举
/// 区分普通攻击、技能、终结技、切换技能四种招式逻辑
/// </summary>
public enum AttackStyle
{ 
    /// <summary>普通平A攻击</summary>
    Attack,
    /// <summary>常规技能</summary>
    Skill,
    /// <summary>终结必杀技</summary>
    FinishSkill,
    /// <summary>切换形态/切换武器专用技能</summary>
    SwitchSkill,
}

/// <summary>
/// 单段攻击数据配置表 ScriptableObject
/// 每一段攻击单独创建一份配置资源，统一管理本段动作动画、收尾动画、伤害、音效、震动、帧暂停、打击特效等参数
/// 多段连招由 ComboContainerData.comboDates 按下标顺序组织（下标0=第一段攻击）

public class ComboData : ScriptableObject
{
    [SerializeField, Header("所属角色（用于区分不同角色共用资源）")] 
    public CharacterNameList characterName;

    [SerializeField, Header("招式类型")] 
    private AttackStyle _attackStyle;

    [SerializeField, Header("本段攻击标识名（动画/事件标识名）")] 
    private string _comboName;

    [SerializeField, Header("本段攻击的动作动画（Animancer ClipTransition）")] 
    private ClipTransition _attackClip;

    [SerializeField, Header("本段收尾动画（播完自动回待机），可不配置")] 
    private ClipTransition _attackEndClip;

    [SerializeField, Header("本段攻击后的冷却时间")]
    private float _comboColdTime;

    // ============ 伤害判定参数（已接入系统，Inspector 按段配置） ============
    [SerializeField, Header("本段攻击伤害")]
    private float _comboDamage = 8f;

    [SerializeField, Header("本段暴击率（0~1，每次命中独立掷骰）")]
    private float _critRate = 0.2f;

    [SerializeField, Header("本段暴击倍率（暴击时伤害 = 伤害 × 倍率）")]
    private float _critMultiplier = 2f;

    [SerializeField, Header("本段攻击命中半径（OverlapSphere 半径）")]
    private float _attackDistance = 1f;

    [SerializeField, Header("本段攻击命中点前移量（角色前方偏移）")]
    private float _comboOffset = 1f;

    [SerializeField, Header("本段攻击敌人层（OverlapSphere 过滤用；不配=全层，仍排除玩家自身）")]
    private LayerMask _enemyLayer;

    [SerializeField, Header("本段打击帧时间（0~1 动画归一化时间，到点触发伤害判定）\n建议早于接段窗口 linkCancelTime，否则窗口内提前接段会跳过本段打击帧")]
    private float _hitFrameTime = 0.35f;

    [SerializeField, Header("本段命中顿帧时长（打到敌人时 timeScale 压低，秒）")]
    private float _pauseFrameTime;

    [SerializeField, Header("本段攻击相机震动力度（相机震动系统接入后启用）")]
    private float _shakeForce;

    // [SerializeField, Header("受击特效名称数组，随机抽取播放")]
    // private string[] _hitName;
    //
    // [SerializeField, Header("弹反/格挡特效名称数组，随机抽取播放")]
    // private string[] _parryName;
    //
     [SerializeField, Header("是否启用独立音效预制体播放")] 
     bool appAudioPrefab = false;
    //
    [SerializeField, Header("武器挥砍/打击音效数组")] 
     private AudioClip[] _weaponSound;
    //
    [SerializeField, Header("角色语音音效数组（呐喊、技能台词等）")] 
    private AudioClip[] _characterVoice;
    //
    [SerializeField, Header("本段通用音效类型")]
    private SoundStyle _universalSound;
    // ============ 上面这些参数暂停使用，待对应系统接入后恢复 ============

    [SerializeField, Header("接下一段的取消窗口时间（0~1 动画归一化时间，到点后可按键立即切段；0 关闭窗口）")]
    private float _linkCancelTime = 0.6f;

    #region 只读属性封装（外部仅读取，禁止修改配置数据）
    /// <summary>招式分类</summary>
    public AttackStyle attackStyle => _attackStyle;

    /// <summary>本段攻击标识名</summary>
    public string comboName => _comboName;

    /// <summary>本段攻击的动作动画（Animancer）</summary>
    public ClipTransition attackClip => _attackClip;

    /// <summary>本段收尾动画（播完回待机），未配置返回 null</summary>
    public ClipTransition attackEndClip => _attackEndClip;

    /// <summary>本段攻击后的冷却时长</summary>
    public float comboColdTime => _comboColdTime;

    /// <summary>接下一段的取消窗口时间（动画归一化时间，0=关闭提前取消，靠动画播完接段）</summary>
    public float linkCancelTime => _linkCancelTime;

    // ============ 伤害判定属性（对应上面已接入的字段） ============
    /// <summary>本段攻击伤害</summary>
    public float comboDamage => _comboDamage;

    /// <summary>本段暴击率（0~1，每次命中独立掷骰）</summary>
    public float critRate => _critRate;

    /// <summary>本段暴击倍率（暴击时伤害 = 伤害 × 倍率）</summary>
    public float critMultiplier => _critMultiplier;

    /// <summary>本段攻击命中半径</summary>
    public float attackDistance => _attackDistance;

    /// <summary>本段攻击命中点前移量</summary>
    public float comboOffset => _comboOffset;

    /// <summary>本段攻击敌人层掩码（未配置=全层，靠 IsChildOf 排除玩家自身）</summary>
    public LayerMask enemyLayer => _enemyLayer;

    /// <summary>本段打击帧时间（动画归一化时间，0=本段无伤害判定）</summary>
    public float hitFrameTime => _hitFrameTime;

    /// <summary>本段命中顿帧时长</summary>
    public float pauseFrameTime => _pauseFrameTime;

    /// <summary>本段攻击相机震动力度</summary>
    public float shakeForce => _shakeForce;

    /// <summary>本段武器音效数组</summary>
    public AudioClip[] weaponSound => _weaponSound;

    /// <summary>本段角色语音数组</summary>
    public AudioClip[] characterVoice => _characterVoice;

    // /// <summary>随机返回一个受击特效名称</summary>
    // public string hitName => _hitName[Random.Range(0, _hitName.Length)];
    //
    // /// <summary>随机返回一个格挡弹反特效名称</summary>
    // public string parryName => _parryName[Random.Range(0, _parryName.Length)];
    //
    /// <summary>本段通用音效规则</summary>
    public SoundStyle universalSound => _universalSound;

    /// <summary>是否使用独立音效预制体播放音效</summary>
    public bool AppAudioPrefab => appAudioPrefab;
    // ============ 上面这些属性暂停使用 ============
    #endregion
}

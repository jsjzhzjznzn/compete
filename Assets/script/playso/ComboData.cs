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
/// 连招数据配置表 ScriptableObject
/// 每个连招/技能单独创建一份配置资源，统一管理动作动画、伤害、音效、震动、帧暂停、打击特效等参数
/// </summary>
[CreateAssetMenu(fileName = "ComboData", menuName = "Create/Asset/ComboData")]
public class ComboData : ScriptableObject
{
    [SerializeField, Header("所属角色（用于区分不同角色共用资源）")] 
    public CharacterNameList characterName;

    [SerializeField, Header("招式类型")] 
    private AttackStyle _attackStyle;

    [SerializeField, Header("连招名称（动画/事件标识名）")] 
    private string _comboName;

    [SerializeField, Header("每段攻击的动作动画（Animancer ClipTransition），下标对应连击段数")] 
    private ClipTransition[] _attackClipList;

    [SerializeField, Header("连招结束收尾动画（播完自动回待机），可不配置")] 
    private ClipTransition _attackEndClip;

    [SerializeField, Header("连招冷却时间")] 
    private float _comboColdTime;

    [SerializeField, Header("单次连招总伤害")] 
    private float _comboDamage;

    [SerializeField, Header("攻击判定距离")] 
    private float _attackDistance;

    [SerializeField, Header("攻击碰撞盒前后偏移值")] 
    private float _comboOffset;

    [SerializeField, Header("受击特效名称数组，随机抽取播放")] 
    private string[] _hitName;

    [SerializeField, Header("弹反/格挡特效名称数组，随机抽取播放")] 
    private string[] _parryName;


    [SerializeField, Header("是否启用独立音效预制体播放")] 
    bool appAudioPrefab = false;

    [SerializeField, Header("武器挥砍/打击音效数组")] 
    private AudioClip[] _weaponSound;

    [SerializeField, Header("角色语音音效数组（呐喊、技能台词等）")] 
    private AudioClip[] _characterVoice;

    [SerializeField, Header("全局通用音效类型，整套连招共用一套音效逻辑")] 
    private SoundStyle _universalSound;


    [SerializeField, Header("整套连招攻击段数（几连击）")]
    private int _ATKCount;

    [SerializeField, Header("全局统一帧暂停时长（兜底用）")] 
    private float _pauseFrameTime;

    [SerializeField, Header("每一段攻击对应的相机震动力度数组，下标对应连击段数")] 
    private float[] _shakeForceList;

    [SerializeField, Header("每一段攻击单独帧暂停时长数组，下标对应连击段数")] 
    private float[] _pauseFrameTimeList;


    #region 只读属性封装（外部仅读取，禁止修改配置数据）
    /// <summary>招式分类</summary>
    public AttackStyle attackStyle => _attackStyle;

    /// <summary>连招标识名</summary>
    public string comboName => _comboName;

    /// <summary>每段攻击的动作动画数组（Animancer），下标对应连击段数</summary>
    public ClipTransition[] attackClips => _attackClipList;

    /// <summary>连招结束收尾动画（播完回待机），未配置返回 null</summary>
    public ClipTransition attackEndClip => _attackEndClip;

    /// <summary>整套连招总伤害</summary>
    public float comboDamage => _comboDamage;

    /// <summary>连招冷却时长</summary>
    public float comboColdTime => _comboColdTime;

    /// <summary>攻击判定距离</summary>
    public float attackDistance => _attackDistance;

    /// <summary>攻击盒偏移</summary>
    public float comboOffset => _comboOffset;

    /// <summary>全部武器音效数组</summary>
    public AudioClip[] weaponSound => _weaponSound;

    /// <summary>全部角色语音数组</summary>
    public AudioClip[] characterVoice => _characterVoice;

    /// <summary>随机返回一个受击特效名称</summary>
    public string hitName => _hitName[Random.Range(0, _hitName.Length)];

    /// <summary>随机返回一个格挡弹反特效名称</summary>
    public string parryName => _parryName[Random.Range(0, _parryName.Length)];

    /// <summary>各段攻击相机震动力度数组</summary>
    public float[] shakeForce => _shakeForceList;

    /// <summary>全局通用音效规则</summary>
    public SoundStyle universalSound => _universalSound;

    /// <summary>兜底全局帧暂停时间</summary>
    public float pauseFrameTime => _pauseFrameTime;

    /// <summary>分段自定义帧暂停时长数组</summary>
    public float[] pauseFrameTimeList => _pauseFrameTimeList;

    /// <summary>连招总段数</summary>
    public int ATKCount => _ATKCount;

    /// <summary>是否使用独立音效预制体播放音效</summary>
    public bool AppAudioPrefab => appAudioPrefab;
    #endregion
}

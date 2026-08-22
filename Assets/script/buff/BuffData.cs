using UnityEngine;

/// <summary>
/// Buff 静态配置表（ScriptableObject）
/// Inspector 右键 Create/Buff 创建，一个效果一张配置，运行时可被多个角色共享。
///
/// 职责：只描述"这个 Buff 是什么"（标识/时长/层数 + 挂什么效果策略），不持有任何运行时状态。
/// 运行时状态（剩余时间、当前层数、tick 计时）放在 BuffInstance，由 BuffComponent 统一管理。
///
/// 行为（数值/结算逻辑）在 effect 策略资产里配置（灼烧扣多少血、增伤多少），
/// 本类只管通用壳：时长、层数上限、叠加判定 ID。
///
/// 叠加规则：按 buffId 判定同一 Buff，重复添加时层数累加（不超过 maxStack），时长刷新为完整时长。
/// </summary>
[CreateAssetMenu(fileName = "Buff", menuName = "Create/Buff")]
public class BuffData : ScriptableObject
{
    [SerializeField, Header("Buff 标识（叠加判定按此 ID，同一 Buff 多份配置时共用 ID）")]
    private string _buffId;

    [SerializeField, Header("Buff 显示名称（UI/调试用）")]
    private string _buffName;

    [SerializeField, Header("效果策略资产（拖入 Create/Buff/... 创建的策略；决定做什么、数值多少）")]
    private BuffEffect _effect;

    [SerializeField, Header("持续时间（秒，<=0 视为永久，直到主动移除）")]
    private float _duration = 5f;

    [SerializeField, Header("最大层数（叠加上限，默认 1 = 不可叠加）")]
    private int _maxStack = 1;

    #region 只读属性封装（外部仅读取，禁止修改配置数据）
    /// <summary>Buff 标识（叠加判定用）</summary>
    public string buffId => _buffId;

    /// <summary>
    /// Buff 标识的预计算哈希（叠加判定用，替代字符串比较）
    /// Animator.StringToHash 内部有全局字符串→哈希缓存表，同一字符串只算一次；
    /// 后续所有比较都走 int，比 string == 快一个量级。哈希冲突概率极低，可忽略。
    /// </summary>
    public int buffIdHash => Animator.StringToHash(_buffId ?? string.Empty);

    /// <summary>Buff 显示名称</summary>
    public string buffName => _buffName;

    /// <summary>效果策略资产（未配置时 Buff 挂上但不产生任何效果）</summary>
    public BuffEffect effect => _effect;

    /// <summary>持续时间（秒，<=0 永久）</summary>
    public float duration => _duration;

    /// <summary>最大层数</summary>
    public int maxStack => _maxStack;
    #endregion
}

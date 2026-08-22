using UnityEngine;

/// <summary>
/// Buff 运行时实例（非 MonoBehaviour，纯数据载体）
/// 由 BuffComponent 在添加 Buff 时创建，持有该角色身上"这一条 Buff"的动态状态：
/// 剩余时间、当前层数、来源、tick 计时。
///
/// 静态配置（时长/层数/效果策略引用）在 BuffData 里，实例只引用它，不复制。
/// </summary>
public class BuffInstance
{
    /// <summary>静态配置（哪张表，决定了效果类型/数值含义）</summary>
    public BuffData data;

    /// <summary>当前层数（叠加后累加，不超过 data.maxStack）</summary>
    public int stacks;

    /// <summary>剩余时间（秒，<=0 时被 BuffComponent 移除）</summary>
    public float remainTime;

    /// <summary>施加来源（谁挂的 Buff；DoT 结算时作为扣血来源传给 HealthModel）</summary>
    public GameObject source;

    /// <summary>tick 计时（累计经过的秒数，达到策略 tickInterval 触发一次结算并归零）</summary>
    public float tickTimer;

    /// <summary>创建一个新实例（duration <=0 视为永久，remainTime 置为极大值避免计时误移除）</summary>
    public BuffInstance(BuffData data, GameObject source = null, int stacks = 1)
    {
        this.data = data;
        this.source = source;
        this.stacks = Mathf.Max(1, stacks);
        remainTime = data != null && data.duration > 0f ? data.duration : float.MaxValue;
    }
}

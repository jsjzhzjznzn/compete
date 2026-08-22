using UnityEngine;

/// <summary>
/// Buff 效果策略基类（ScriptableObject）
/// 策略模式：每种效果类型一个策略资产，把"这个 Buff 具体做什么"从 BuffComponent 中拆出来。
///
/// 职责分工：
///   - BuffData（通用壳）：buffId/duration/maxStack + 拖入一个策略资产
///   - BuffEffect（行为）：怎么 tick、怎么修饰数值
///   - BuffComponent（容器）：统一计时 + 调用策略钩子，不关心具体效果类型
///
/// 收益：新增效果类型（护盾/反伤/冻结）只需新建一个策略类，BuffComponent 一行不改（开闭原则）。
/// 参数放哪：通用参数留在 BuffData；类型特有参数（灼烧扣血量、增伤百分比）放各自策略资产里，Inspector 直观可配。
///
/// 生命周期钩子（BuffComponent 调用）：
///   - OnApply   添加 Buff 时
///   - OnTick    到达 tickInterval 时（DoT/HoT 实现结算）
///   - OnRemove  主动移除时（默认转调 OnExpire，需要区分可覆写）
///   - OnExpire  自然到期时
/// </summary>
public abstract class BuffEffect : ScriptableObject
{
    /// <summary>效果类型标识（BuffComponent 过滤修饰查询用，子类必须返回）</summary>
    public abstract BuffEffectType effectType { get; }

    /// <summary>tick 间隔（秒）；0 = 不需要周期结算（BuffComponent 据此决定是否计时）</summary>
    public virtual float tickInterval => 0f;

    /// <summary>Buff 添加时调用（收尾初始化用，默认无操作）</summary>
    public virtual void OnApply(BuffInstance buff) { }

    /// <summary>到达 tick 间隔时调用（灼烧扣血/持续回血在这里实现）</summary>
    public virtual void OnTick(BuffInstance buff, HealthModel health) { }

    /// <summary>Buff 被主动移除时调用（RemoveBuff/清空）；默认与到期同逻辑</summary>
    public virtual void OnRemove(BuffInstance buff) => OnExpire(buff);

    /// <summary>Buff 自然到期时调用（收尾清理用，默认无操作）</summary>
    public virtual void OnExpire(BuffInstance buff) { }

    /// <summary>修饰系数查询（伤害管道/移动状态机读取；默认无修饰返回 0）</summary>
    public virtual float GetModifier(BuffInstance buff) => 0f;
}

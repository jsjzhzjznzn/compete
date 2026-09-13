using UnityEngine;

/// <summary>
/// Buff 效果策略基类（ScriptableObject）
/// 策略模式：每种效果一个策略资产，把"这个 Buff 具体做什么"从 BuffComponent 拆出来。
/// 新增效果类型：新建一个继承本类的策略资产即可，BuffComponent 一行不改。
///
/// 注意：策略资产是【静态配置 + 全局共享】，禁止在这里存任何运行时状态
/// （计时器/临时缓存等），否则同一 Buff 在多角色/多层之间会串数据——
/// 运行时状态一律放 BuffInstance。
///
/// 生命周期钩子（BuffComponent 调用）：
///   - OnApply        首次挂上
///   - OnStackChanged 层数变化（叠层/减层），属性类效果需在这里重算 Modifier
///   - OnTick         到达 tickInterval
///   - OnRemove       主动移除（默认转 OnExpire）
///   - OnExpire       自然到期
/// </summary>
public abstract class BuffEffect : ScriptableObject
{
    /// <summary>tick 间隔（秒）；0 = 不需要周期结算</summary>
    public virtual float tickInterval => 0f;

    /// <summary>
    /// 该策略贡献的行为控制标志（行为类 Buff 用）。
    /// 服务端同步 buff 状态时写入 NetworkList，客户端据此同步本地 CharacterState；
    /// 普通效果（属性/DoT/HoT）保持 None。
    /// </summary>
    public virtual BuffControlFlags controlFlags => BuffControlFlags.None;

    /// <summary>首次挂上时调用</summary>
    public virtual void OnApply(BuffInstance buff) { }

    /// <summary>层数变化时调用（属性类效果在这里重算自己的 Modifier）</summary>
    public virtual void OnStackChanged(BuffInstance buff) { }

    /// <summary>到达 tick 间隔时调用（DoT/HoT 结算）</summary>
    public virtual void OnTick(BuffInstance buff, HealthModel health) { }

    /// <summary>被主动移除时调用；默认与到期同逻辑</summary>
    public virtual void OnRemove(BuffInstance buff) => OnExpire(buff);

    /// <summary>自然到期时调用</summary>
    public virtual void OnExpire(BuffInstance buff) { }
}

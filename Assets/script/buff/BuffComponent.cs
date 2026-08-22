using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buff 管理组件（MonoBehaviour，挂角色身上，与 HealthModel 并列）
///
/// 职责：
///   1. 添加/叠加/移除 Buff（按 BuffData.buffIdHash 判定同一 Buff，层数累加 + 时长刷新）
///   2. Update 统一计时：剩余时间倒计时、按策略 tickInterval 周期性触发 OnTick
///   3. 到期/主动移除/死亡清空，并调用策略生命周期钩子（OnApply/OnTick/OnRemove/OnExpire）
///   4. 修饰系数查询（供 DamageCalculator 读取增伤/减伤，供移动状态机读移速）
///
/// 策略模式：效果逻辑全部委托给 BuffData.effect（BuffEffect 子类），
/// 本组件不感知具体效果类型——新增效果类型无需改这里（开闭原则）。
///
/// 事件结合（状态变化走事件中心，数值修改走查询，互不混淆）：
///   - 添加/移除/层数变化 → E_BuffAdd / E_BuffRemove（BuffChangeData，UI buff 栏订阅）
///   - tick 结算 → 策略内调 HealthModel.TakeDamage / Heal，复用 E_OnDamage 链路（飘字/受击自动生效）
///
/// 计时用 Time.deltaTime（受 timeScale 影响）：顿帧/慢动作时 Buff 计时同步暂停，符合直觉。
/// </summary>
public class BuffComponent : MonoBehaviour
{
    /// <summary>当前角色身上的全部 Buff 实例（只读遍历用，修改必须走 AddBuff/RemoveBuff）</summary>
    public IReadOnlyList<BuffInstance> Buffs => _buffs;

    private readonly List<BuffInstance> _buffs = new List<BuffInstance>();

    /// <summary>血量组件缓存（同物体上必有 HealthModel，首次访问懒获取）</summary>
    private HealthModel _health;

    private HealthModel Health => _health != null ? _health : _health = GetComponent<HealthModel>();

    private void Update()
    {
        // 死亡清空：角色血量归零后把所有 Buff 移除（尸体上挂持续效果没意义，也防泄漏）
        var health = Health;
        if (health != null && !health.IsAlive)
        {
            if (_buffs.Count > 0) RemoveAllBuffs();
            return;
        }

        // 倒序遍历：移除时安全 RemoveAt，避免正序删除导致跳项
        // 局部缓存 deltaTime：一帧内多个 buff 共用同一个增量，避免重复读属性
        float dt = Time.deltaTime;
        for (int i = _buffs.Count - 1; i >= 0; i--)
        {
            var buff = _buffs[i];
            var effect = buff.data.effect;
            if (effect == null) continue;   // 未配置策略的防御：挂上但不产生效果

            // 永久 Buff（duration <= 0）不倒计时，仅走 tick
            if (buff.data.duration > 0f)
            {
                buff.remainTime -= dt;
            }

            // 策略声明需要周期结算时：累计 tick 计时，到间隔触发一次 OnTick
            if (effect.tickInterval > 0f)
            {
                buff.tickTimer += dt;
                if (buff.tickTimer >= effect.tickInterval)
                {
                    buff.tickTimer = 0f;
                    effect.OnTick(buff, Health);
                }
            }

            if (buff.remainTime <= 0f)
            {
                effect.OnExpire(buff);
                _buffs.RemoveAt(i);
                DispatchChange(E_EventType.E_BuffRemove, buff.data, 0, 0f);
            }
        }
    }

    // ==================== 添加 / 移除 ====================

    /// <summary>
    /// 添加一个 Buff。同 buffId 已存在时：层数累加（不超过 maxStack）+ 时长刷新为完整时长。
    /// 新建实例时调用策略 OnApply；变更后派发 E_BuffAdd（UI/音效订阅）。
    /// </summary>
    /// <param name="data">静态配置（必须非空）</param>
    /// <param name="source">施加来源（DoT 扣血时作为伤害来源）</param>
    /// <param name="stacks">本次添加的层数（默认 1）</param>
    public void AddBuff(BuffData data, GameObject source = null, int stacks = 1)
    {
        if (data == null) return;

        var existing = _buffs.Find(b => b.data.buffIdHash == data.buffIdHash);
        if (existing != null)
        {
            existing.stacks = Mathf.Min(existing.stacks + Mathf.Max(1, stacks), existing.data.maxStack);
            if (existing.data.duration > 0f) existing.remainTime = existing.data.duration;  // 叠加刷新时长
            DispatchChange(E_EventType.E_BuffAdd, existing.data, existing.stacks, existing.remainTime);
            return;
        }

        var buff = new BuffInstance(data, source, stacks);
        _buffs.Add(buff);
        data.effect?.OnApply(buff);
        DispatchChange(E_EventType.E_BuffAdd, data, buff.stacks, buff.remainTime);
    }

    /// <summary>移除指定 Buff（按配置引用匹配），调用策略 OnRemove 并派发 E_BuffRemove</summary>
    public void RemoveBuff(BuffData data)
    {
        if (data == null) return;

        for (int i = _buffs.Count - 1; i >= 0; i--)
        {
            if (_buffs[i].data == data)
            {
                _buffs[i].data.effect?.OnRemove(_buffs[i]);
                _buffs.RemoveAt(i);
                DispatchChange(E_EventType.E_BuffRemove, data, 0, 0f);
                return;
            }
        }
    }

    /// <summary>清空全部 Buff（死亡/场景切换用），逐个调用策略 OnRemove 并派发 E_BuffRemove</summary>
    public void RemoveAllBuffs()
    {
        for (int i = _buffs.Count - 1; i >= 0; i--)
        {
            var buff = _buffs[i];
            buff.data.effect?.OnRemove(buff);
            var data = buff.data;
            _buffs.RemoveAt(i);
            DispatchChange(E_EventType.E_BuffRemove, data, 0, 0f);
        }
    }

    /// <summary>是否带有指定 Buff（按配置引用匹配）</summary>
    public bool HasBuff(BuffData data)
    {
        if (data == null) return false;
        return _buffs.Find(b => b.data == data) != null;
    }

    // ==================== 修饰系数查询（供伤害管道/移动状态机读取） ====================

    /// <summary>增伤系数（所有增伤策略的修饰值求和；无 Buff 返回 0）</summary>
    public float GetDamageUpModifier()
    {
        return SumModifiers(BuffEffectType.DamageUp);
    }

    /// <summary>减伤系数（所有减伤策略的修饰值求和；无 Buff 返回 0）</summary>
    public float GetDamageDownModifier()
    {
        return SumModifiers(BuffEffectType.DamageDown);
    }

    /// <summary>移速系数（增/减速策略净和，减速策略返回负值；无 Buff 返回 0。移动状态机接入见二期）</summary>
    public float GetSpeedModifier()
    {
        return SumModifiers(BuffEffectType.SpeedUp) + SumModifiers(BuffEffectType.SpeedDown);
    }

    // ==================== 内部 ====================

    /// <summary>累加指定类型策略的修饰值（每个 buff 委托给它的 effect.GetModifier）</summary>
    private float SumModifiers(BuffEffectType type)
    {
        float sum = 0f;
        for (int i = 0; i < _buffs.Count; i++)
        {
            var buff = _buffs[i];
            var effect = buff.data.effect;
            if (effect != null && effect.effectType == type)
                sum += effect.GetModifier(buff);
        }
        return sum;
    }

    /// <summary>派发 buff 状态变化事件（统一封装，避免调用处漏发 target）</summary>
    private void DispatchChange(E_EventType evt, BuffData data, int stacks, float remainTime)
    {
        EventCenter.MainInstance.Dispatch(evt, new BuffChangeData
        {
            target = gameObject,
            buffData = data,
            stacks = stacks,
            remainTime = remainTime
        });
    }
}

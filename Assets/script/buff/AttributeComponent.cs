using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 属性组件：持有所有属性（AttrType → MutableAttribute），挂在角色根上，与 BuffComponent 并列。
/// Buff 的属性加成效果通过它 AddModifier / RemoveAllFromSource。
///
/// 【权威】只有服务端（或单机未 spawn）会写 Modifier；客户端不参与伤害结算，
/// 因此属性账本本身**不做全量同步**（客户端上只有 SetBase 写入的基础白值）。
///
/// 【唯一例外：出手属性】改成"攻方自己算出伤害数值再发给受击方"之后，
/// 拥有者端需要读到自己角色的 Attack / DamageUp 才能算出手伤害（Buff 加成只在服务端）。
/// 所以只把这两个值用 NetworkVariable(Owner 可读 / Server 写) 推给拥有者，其余属性不同步。
/// </summary>
public class AttributeComponent : NetworkBehaviour
{
    private readonly Dictionary<AttrType, MutableAttribute> _attrs = new Dictionary<AttrType, MutableAttribute>();

    /// <summary>出手属性镜像（服务端写、仅拥有者可读）：Attack 的最终值</summary>
    private readonly NetworkVariable<float> netOwnAttack =
        new(0f, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    /// <summary>出手属性镜像（服务端写、仅拥有者可读）：DamageUp 的最终值</summary>
    private readonly NetworkVariable<float> netOwnDamageUp =
        new(0f, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        // 预建所有属性容器，保证 Get 不会因缺失返回 null
        foreach (AttrType type in System.Enum.GetValues(typeof(AttrType)))
            _attrs[type] = new MutableAttribute(AttrTypeUtil.IsRatio(type));
    }

    public override void OnNetworkSpawn()
    {
        // 服务端：spawn 时先推一次，拥有者不必等第一帧 Update 才有值
        PublishOwnValues();
    }

    private void Update()
    {
        PublishOwnValues();
    }

    /// <summary>
    /// 服务端把出手属性推给拥有者（客户端在方法开头直接返回）。
    /// 用每帧比对而不是挂在 BuffEffects 的写入点上：后者要在每个加/减 Modifier 处回调，
    /// 耦合更重且容易漏加；两个 float 比对的开销可以忽略。
    /// </summary>
    private void PublishOwnValues()
    {
        if (!IsSpawned || !IsServer) return;   // NetworkVariable 只有服务端能写

        Publish(AttrType.Attack, netOwnAttack);
        Publish(AttrType.DamageUp, netOwnDamageUp);
    }

    private void Publish(AttrType type, NetworkVariable<float> netVar)
    {
        float value = GetValue(type);
        if (!Mathf.Approximately(netVar.Value, value))
            netVar.Value = value;
    }

    /// <summary>取指定属性的容器（缺失时兜底新建）</summary>
    public MutableAttribute Get(AttrType type)
    {
        if (!_attrs.TryGetValue(type, out var attr))
        {
            attr = new MutableAttribute(AttrTypeUtil.IsRatio(type));
            _attrs[type] = attr;
        }
        return attr;
    }

    /// <summary>读取指定属性的最终值（无任何加成时通常为 0）</summary>
    public float GetValue(AttrType type) => Get(type).FinalValue;

    /// <summary>
    /// 读取"出手计算"用的属性值（攻方算自己的伤害时用）：
    ///   服务端 / 单机 → 本地账本（权威值）
    ///   拥有者客户端 → 服务端推来的镜像（本地账本只有基础白值，没有 Buff 加成）
    /// 目前只镜像 Attack / DamageUp，其它属性回落本地账本。
    /// </summary>
    public float GetOwnValue(AttrType type)
    {
        if (!IsSpawned || IsServer) return GetValue(type);

        return type switch
        {
            AttrType.Attack => netOwnAttack.Value,
            AttrType.DamageUp => netOwnDamageUp.Value,
            _ => GetValue(type),
        };
    }

    /// <summary>
    /// 设置指定属性的基础值（仅白值型有意义）。
    /// 系数型中性值恒为 1（0 = 无加成），基础值不参与计算，误用会静默无效 —— 故直接报错忽略。
    /// </summary>
    public void SetBase(AttrType type, float value)
    {
        if (AttrTypeUtil.IsRatio(type))
        {
            Debug.LogError($"[AttributeComponent] 系数型属性 {type} 无基础值概念（中性值为 1），SetBase({value}) 已忽略");
            return;
        }
        Get(type).BaseValue = value;
    }

    /// <summary>清空全部属性的修改项（死亡/复活重置用）</summary>
    public void ClearAllModifiers()
    {
        foreach (var kv in _attrs) kv.Value.ClearAllModifiers();
    }
}

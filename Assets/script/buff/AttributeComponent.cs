using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 属性组件：持有所有系数型属性（AttrType → MutableAttribute），挂在角色根上，与 BuffComponent 并列。
/// Buff 的属性加成效果通过它 AddModifier / RemoveAllFromSource。
///
/// 权威与服务端一致（见 HealthModel）：只有服务端（或单机未 spawn）会写 Modifier；
/// 客户端不参与属性计算，因此本组件不需要网络同步。
/// </summary>
public class AttributeComponent : MonoBehaviour
{
    private readonly Dictionary<AttrType, MutableAttribute> _attrs = new Dictionary<AttrType, MutableAttribute>();

    private void Awake()
    {
        // 预建所有属性容器，保证 Get 不会因缺失返回 null
        foreach (AttrType type in System.Enum.GetValues(typeof(AttrType)))
            _attrs[type] = new MutableAttribute();
    }

    /// <summary>取指定属性的容器（缺失时兜底新建）</summary>
    public MutableAttribute Get(AttrType type)
    {
        if (!_attrs.TryGetValue(type, out var attr))
        {
            attr = new MutableAttribute();
            _attrs[type] = attr;
        }
        return attr;
    }

    /// <summary>读取指定属性的最终值（无任何加成时通常为 0）</summary>
    public float GetValue(AttrType type) => Get(type).FinalValue;

    /// <summary>设置指定属性的基础值</summary>
    public void SetBase(AttrType type, float value) => Get(type).BaseValue = value;

    /// <summary>清空全部属性的修改项（死亡/复活重置用）</summary>
    public void ClearAllModifiers()
    {
        foreach (var kv in _attrs) kv.Value.ClearAllModifiers();
    }
}

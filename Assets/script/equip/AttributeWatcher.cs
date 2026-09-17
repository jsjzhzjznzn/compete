using System;
using UnityEngine;

/// <summary>
/// 属性显示刷新器：挂在任何"要显示角色属性"的 UI 上，属性变了就喊 <see cref="OnChanged"/>。
/// 面板自己订阅它、自己去改文本 —— 本组件不认识任何 UI 控件。
///
/// 【为什么用"每帧比对"而不是事件订阅】
///   1) 属性变化的来源不止一个：装备穿脱、Buff 加/减、等级、天赋。
///      如果只订阅 EquipmentComponent.OnSlotChanged，Buff 改属性时面板不会刷，
///      数字就会停在旧值上 —— 这是最典型的坑。
///   2) MutableAttribute 本身没有变更事件，只有脏标记 + 延迟计算。
///   3) 工程里已有同款先例：AttributeComponent.PublishOwnValues 就是每帧比对
///      （AttributeComponent.cs:46-50 的注释写明了为什么不用写入点回调：
///       写入点太多、耦合重、容易漏加；两个 float 比对的开销可以忽略）。
///
///   属性总共只有几个，每帧比对全部的开销可以忽略。
///
/// 【什么时候不跑】只在自己 active && enabled 时才 Update，
///   所以挂在面板根上、面板一关就自然停了，不需要手动开关。
///
/// 【摆在哪个目录】放 equip/ 是因为它是跟着装备系统一起进来的，
///   其实它跟装备无关，以后要挪到别处直接搬文件即可。
/// </summary>
public class AttributeWatcher : MonoBehaviour
{
    [SerializeField, Header("属性容器（留空则从父级自动找）")]
    private AttributeComponent _attributes;

    [SerializeField, Header("要监听的属性（留空 = 监听全部）")]
    private AttrType[] _watch;

    /// <summary>被监听的属性有任何变化时触发（首次也会触发一次，用于初始化显示）</summary>
    public event Action OnChanged;

    /// <summary>上次看到的值，下标和 watch 一一对应</summary>
    private float[] _lastValues;

    /// <summary>是否已经取过初值。false 时第一次 Poll 必定报一次变化</summary>
    private bool _primed;

    /// <summary>全部属性类型（_watch 留空时用）</summary>
    private static readonly AttrType[] AllAttrs = (AttrType[])Enum.GetValues(typeof(AttrType));

    private AttributeComponent Attributes
    {
        get
        {
            if (_attributes == null) _attributes = GetComponentInParent<AttributeComponent>();
            return _attributes;
        }
    }

    private void OnEnable()
    {
        // 重新打开时当作"还没取过初值"，让面板一上来就能刷到当前值
        _primed = false;
    }

    private void Update() => Poll();

    /// <summary>取某个属性当前的最终值（面板写文本用）</summary>
    public float Get(AttrType type) => Attributes != null ? Attributes.GetValue(type) : 0f;

    private void Poll()
    {
        var attrs = Attributes;
        if (attrs == null) return;

        var watch = (_watch != null && _watch.Length > 0) ? _watch : AllAttrs;

        if (_lastValues == null || _lastValues.Length != watch.Length)
            _lastValues = new float[watch.Length];

        // _primed == false 时无条件算作"变了"：保证首次（和重新打开）必定刷一次
        bool changed = !_primed;

        for (int i = 0; i < watch.Length; i++)
        {
            float value = attrs.GetValue(watch[i]);
            if (!_primed || !Mathf.Approximately(value, _lastValues[i]))
            {
                _lastValues[i] = value;
                changed = true;
            }
        }

        if (!changed) return;

        _primed = true;
        OnChanged?.Invoke();
    }
}

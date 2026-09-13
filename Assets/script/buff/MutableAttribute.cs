using System.Collections.Generic;

/// <summary>
/// 可变属性容器（系数型）：BaseValue + Modifier 账本 + Dirty 脏标记延迟计算。
///
/// 写阶段（加/删 Modifier、改 BaseValue）：只打脏标记，不遍历、不计算；
/// 读阶段（FinalValue）：脏才 Recalculate()（遍历账本算一次并缓存），否则直接返回缓存。
/// 战斗高频读属性时，绝大多数帧 Buff 没变 → 零遍历开销。
///
/// 计算顺序固定：Final = (Base + 所有加法之和) × 所有乘法之积
/// </summary>
public class MutableAttribute
{
    private float _baseValue;

    /// <summary>基础值。做成属性而非 public 字段：修改时打脏标记，保证缓存失效</summary>
    public float BaseValue
    {
        get => _baseValue;
        set
        {
            if (_baseValue == value) return;
            _baseValue = value;
            _isDirty = true;
        }
    }

    /// <summary>全部属性修改项账本</summary>
    private readonly List<AttributeModifier> _modifiers = new List<AttributeModifier>();

    private float _cachedFinalValue;

    /// <summary>脏标记。初值 true：BaseValue 可能被外部设置，首读必须算一次</summary>
    private bool _isDirty = true;

    /// <summary>最终值（对外只读）：脏才重算，否则直出缓存</summary>
    public float FinalValue
    {
        get
        {
            if (_isDirty) Recalculate();
            return _cachedFinalValue;
        }
    }

    /// <summary>追加一条修改项（打脏标记，不立即计算）</summary>
    public void AddModifier(AttributeModifier mod)
    {
        _modifiers.Add(mod);
        _isDirty = true;
    }

    /// <summary>按来源移除属于该来源的全部修改项（精准删除，不动其他来源）</summary>
    public void RemoveAllFromSource(object source)
    {
        // 倒序遍历：删除时避免正序索引错乱漏项
        bool removed = false;
        for (int i = _modifiers.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_modifiers[i].source, source))
            {
                _modifiers.RemoveAt(i);
                removed = true;
            }
        }
        if (removed) _isDirty = true;
    }

    /// <summary>清空全部修改项（角色死亡/复活重置属性用）</summary>
    public void ClearAllModifiers()
    {
        if (_modifiers.Count == 0) return;
        _modifiers.Clear();
        _isDirty = true;
    }

    /// <summary>核心重算：先全部加法，后统一乘法</summary>
    private void Recalculate()
    {
        float addSum = 0f;
        float mulProduct = 1f;

        for (int i = 0; i < _modifiers.Count; i++)
        {
            var mod = _modifiers[i];
            if (mod.type == ModType.Add) addSum += mod.value;
            else mulProduct *= mod.value;
        }

        _cachedFinalValue = (_baseValue + addSum) * mulProduct;
        _isDirty = false;
    }
}

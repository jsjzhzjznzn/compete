/// <summary>
/// 一条属性修改项，代表一条加成（来自 Buff / 装备 / 天赋）。
/// 用 source 标识归属，移除时按来源精准删除——
/// 禁止按 value 删除：两个 Buff 都 +30 时，按数值删会误删别的来源。
/// </summary>
public struct AttributeModifier
{
    /// <summary>修改类型（加/乘）</summary>
    public ModType type;

    /// <summary>修改数值</summary>
    public float value;

    /// <summary>来源对象（一般传 BuffInstance）；按引用相等匹配删除</summary>
    public object source;

    public AttributeModifier(ModType type, float value, object source)
    {
        this.type = type;
        this.value = value;
        this.source = source;
    }
}

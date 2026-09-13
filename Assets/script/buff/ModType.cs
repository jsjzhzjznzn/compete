/// <summary>
/// 属性修改类型：加法 / 乘法
/// 计算顺序固定"先全部加法，后统一乘法"：Final = (Base + ΣAdd) × ΠMultiply
/// </summary>
public enum ModType
{
    /// <summary>加法：进入加法总和</summary>
    Add,

    /// <summary>乘法：进入乘法累乘（不填则等价乘 1）</summary>
    Multiply,
}

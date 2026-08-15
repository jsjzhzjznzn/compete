using System;
using System.Collections.Generic;

/// <summary>
/// 可观察属性：Value 被赋新值（且与旧值不同）时触发 OnValueChanged 回调
/// 用于连击下标等数据的响应式通知（如驱动动画/UI 跟随下标变化）
/// </summary>
public class BindableProperty<T>
{
    private T mValue = default(T);

    /// <summary>值变化时触发的回调，参数为最新值</summary>
    public Action<T> OnValueChanged;

    public T Value
    {
        get { return mValue; }
        set
        {
            if (!EqualityComparer<T>.Default.Equals(value, mValue))
            {
                mValue = value;
                OnValueChanged?.Invoke(mValue);
            }
        }
    }
}

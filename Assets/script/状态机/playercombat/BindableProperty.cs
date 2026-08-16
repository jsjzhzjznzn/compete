using System;
using System.Collections.Generic;

/// <summary>
/// 可观察属性（响应式数据包装器）
///
/// 【作用】
/// 把普通字段升级成"有变化通知"的响应式数据：外部订阅 OnValueChanged，
/// 当 Value 被赋一个【与当前值不同】的新值时，自动通知所有订阅者。
/// 相当于一个极简版的 MVVM 绑定 / ObservableProperty（不依赖任何第三方框架）。
///
/// 【为什么要用它】
/// 在状态机/战斗系统里，经常出现"数据变化需要驱动另一处逻辑"的场景，
/// 例如：连击段数下标 currentIndex 一变，UI 招式图标、动画层、音效都要跟着切换。
/// 如果用普通字段，就得在每一处修改点手动去调用刷新方法，容易漏；
/// 用 BindableProperty，只需在"数据源头"赋值，所有关心它的地方自动收到通知，
/// 解耦了"谁改数据"和"谁响应数据"。
///
/// 【用法】
///   var index = new BindableProperty<int>();
///   index.OnValueChanged += v => Debug.Log("下标变成：" + v);   // 订阅
///   index.Value = 1;                                           // 赋值即通知（1 != 0 旧值，触发）
///   index.Value = 1;                                           // 再赋相同值，不重复触发
///
/// 【本项目中的使用】
/// PlayerComboReusableData.currentIndex 用它包装连击段数下标，
/// 下标变化时通知外部（动画/UI）同步更新。
/// </summary>
public class BindableProperty<T>
{
    /// <summary>内部真正存储的值</summary>
    private T mValue = default(T);

    /// <summary>
    /// 值变化时触发的回调（参数为最新值）。
    /// 在 Value 被设为不同值时自动调用；可在外部 += / -= 订阅或退订。
    /// </summary>
    public Action<T> OnValueChanged;

    /// <summary>
    /// 读取/写入包装的值。
    /// 写入时会先用 EqualityComparer 比较新旧值：
    ///  - 不同 → 更新内部值并触发 OnValueChanged 通知
    ///  - 相同 → 直接忽略，避免无意义的重复通知
    /// </summary>
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

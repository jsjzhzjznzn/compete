using System;
using UnityEngine;

/// <summary>
/// 血条 ViewModel，纯C#不继承Mono，可单元测试
/// 接收Model数据投影为View可用字段，命令封装，管理事件订阅生命周期
/// </summary>
public class HealthBarViewModel : IDisposable
{
    private readonly HealthModel _healthModel;
    private bool _disposed;

    /// <summary>血量填充比例 0~1</summary>
    public BindableProperty<float> HPRatio { get; } = new();
    /// <summary>是否存活，控制血条显隐</summary>
    public BindableProperty<bool> IsAlive { get; } = new();

    public HealthBarViewModel(HealthModel healthModel)
    {
        _healthModel = healthModel ?? throw new ArgumentNullException(nameof(healthModel));

        _healthModel.CurrentHP.OnValueChanged += OnModelValueChanged;
        _healthModel.MaxHP.OnValueChanged += OnModelValueChanged;
        RefreshProjection();
    }

    /// <summary>根据Model原始数据刷新投影属性</summary>
    private void RefreshProjection()
    {
        float ratio = _healthModel.MaxHP.Value > 0f
            ? Mathf.Clamp01(_healthModel.CurrentHP.Value / _healthModel.MaxHP.Value)
            : 0f;

        HPRatio.Value = ratio;
        IsAlive.Value = _healthModel.IsAlive;
    }

    private void OnModelValueChanged(float oldVal, float newVal)
    {
        RefreshProjection();
    }

    /// <summary>命令：整体设置血量（读档/复活初始化）</summary>
    public void SetHealth(float current, float max)
    {
        _healthModel.ResetHealth(current, max);
    }

    #region 标准 .NET IDisposable 模式
    ~HealthBarViewModel()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // 解绑托管事件
            _healthModel.CurrentHP.OnValueChanged -= OnModelValueChanged;
            _healthModel.MaxHP.OnValueChanged -= OnModelValueChanged;
        }
        _disposed = true;
    }
    #endregion
}

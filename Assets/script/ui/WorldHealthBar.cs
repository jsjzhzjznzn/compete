using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 世界空间血条 View 层，只负责渲染UI，无业务逻辑
/// 绑定ViewModel，自动响应HPRatio、IsAlive变化
/// </summary>
[RequireComponent(typeof(Canvas))]
[DisallowMultipleComponent]
public class WorldHealthBar : MonoBehaviour
{
    [Header("UI引用")]
    [SerializeField] private Image fillImage;

    [Header("Billboard设置")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool faceCamera = true;

    private HealthBarViewModel _viewModel;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (fillImage == null)
            fillImage = GetComponentInChildren<Image>(includeInactive: true);

        // 便利：未显式 BindViewModel 时，自动在父级找 Model 并绑定（显式绑定优先）
        if (_viewModel == null)
        {
            var model = GetComponentInParent<HealthModel>();
            if (model != null)
                BindViewModel(new HealthBarViewModel(model));
        }
    }

    /// <summary>绑定ViewModel，启动自动数据驱动（外部调用一次绑定）</summary>
    public void BindViewModel(HealthBarViewModel viewModel)
    {
        UnbindViewModel();

        _viewModel = viewModel;
        if (_viewModel == null) return;

        // 订阅VM投影属性，自动刷新UI
        _viewModel.HPRatio.OnValueChanged += OnHpRatioChanged;
        _viewModel.IsAlive.OnValueChanged += OnAliveStateChanged;

        // 初始化刷新一次界面
        OnHpRatioChanged(0, _viewModel.HPRatio.Value);
        OnAliveStateChanged(false, _viewModel.IsAlive.Value);
    }

    /// <summary>解除绑定，释放订阅</summary>
    public void UnbindViewModel()
    {
        if (_viewModel == null) return;

        _viewModel.HPRatio.OnValueChanged -= OnHpRatioChanged;
        _viewModel.IsAlive.OnValueChanged -= OnAliveStateChanged;
        _viewModel.Dispose();
        _viewModel = null;
    }

    private void OnHpRatioChanged(float oldVal, float newVal)
    {
        if (fillImage != null)
            fillImage.fillAmount = newVal;
    }

    private void OnAliveStateChanged(bool oldVal, bool newVal)
    {
        gameObject.SetActive(newVal);
    }

    private void LateUpdate()
    {
        if (!faceCamera) return;
        if (targetCamera == null)
            targetCamera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (targetCamera != null)
        {
            transform.rotation = targetCamera.transform.rotation;
        }
    }

    private void OnDestroy()
    {
        UnbindViewModel();
    }
}

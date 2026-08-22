using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 飘字显示样式（普通/暴击各配一个，在 DamageTextManager 的 Inspector 里配置）
/// </summary>
[System.Serializable]
public class DamageTextStyle
{
    public Color color = Color.white;   // 文字颜色
    public int fontSize = 60;           // 文字字号（像素）
    public float scaleFrom;             // 入场缩放起点（0.6 = 从 0.6 倍弹到 1 倍；<=0 关闭）
    public bool punchOnShow;            // 是否弹跳放大（暴击用：出场先放大再回弹，更有冲击力）
}

/// <summary>
/// 单条伤害飘字（对象池复用单元）
///
/// 【职责】
///   只负责"一条飘字的一生"：设置文本/颜色/字号 → 播放动画 → 动画结束通知管理器回收。
///   不关心字从哪来、飘完去哪——取用/回收都由 DamageTextManager 的对象池调度。
///
/// 【动画（DOTween Sequence）】
///   1. 上飘：anchoredPosition.y 从出生点向上移动 floatHeight（OutQuad 先快后慢）
///   2. 淡出：文字 alpha 从 1 渐隐到 0（延迟 40% 时长后才开始淡，保证先看清数字）
///   3. 暴击附加：PunchScale 放大回弹
///   动画播完 → OnAnimEnd → 回调 Manager 的回收方法
///
/// 【为什么 Show 开头要 SetActive(true)】
///   池里的实例回收时被 SetActive(false) 关闭。而 Unity 规定：
///   inactive 物体上 AddComponent 不会执行 Awake——所以 Manager 预创建池时
///   （物体还没激活）本类的 Awake 不跑，text 序列化字段（运行时创建，无 Inspector 赋值）
///   就是 null。必须在首次激活后由 Awake 里 GetComponent&lt;TextMeshProUGUI&gt;() 补齐。
///   因此在 Show 开头先激活物体，保证 text 引用一定可用，否则 text.text 会空引用崩溃。
/// </summary>
public class DamageText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;   // 文本组件（运行时由 CreateText 创建，Awake 自动补齐）

    private Action _onComplete;    // 动画结束回调（Manager 注入的回收方法）
    private Sequence _sequence;    // 当前动画序列（复用前 Kill 旧的，防止残留）

    private void Awake()
    {
        // 运行时创建的组件没有 Inspector 赋值，这里兜底取同物体上的 TextMeshProUGUI
        if (text == null) text = GetComponent<TextMeshProUGUI>();
    }

    /// <summary>
    /// 开始播放一条飘字（screenPos 为屏幕像素坐标，Overlay Canvas 下即 anchoredPosition）。
    /// 每次播放前先复位文本/位置/缩放，避免复用上次残留的状态。
    /// </summary>
    public void Show(int amount, Vector2 screenPos, DamageTextStyle style, float floatHeight, float duration, Action onComplete)
    {
        // 池里取出的实例是 inactive（回收时关闭）。先激活：首次激活才会触发 Awake 补齐 text 引用
        gameObject.SetActive(true);
        _onComplete = onComplete;

        // 复位显示状态：文本、颜色、字号（复用旧实例时这些都是上次残留值）
        text.text = amount.ToString();
        text.color = style.color;
        text.fontSize = style.fontSize;
        // 复位位置与缩放：锚点左下 + pivot 居中，anchoredPosition 即文本中心屏幕坐标
        RectTransform rt = (RectTransform)transform;
        rt.anchoredPosition = screenPos;
        rt.localScale = Vector3.one;

        // 组装动画序列（复用前先杀掉旧序列，防止上一条飘字的 tween 还在跑）
        _sequence?.Kill();
        _sequence = DOTween.Sequence()
            .Append(rt.DOAnchorPosY(screenPos.y + floatHeight, duration).SetEase(Ease.OutQuad));
        if (style.scaleFrom > 0f)
            _sequence.Join(rt.DOScale(1f, 0.2f).From(style.scaleFrom).SetEase(Ease.OutBack));
        if (style.punchOnShow)
            _sequence.Join(rt.DOPunchScale(Vector3.one * 0.4f, 0.25f, 6, 0.5f));
        _sequence.Join(text.DOFade(0f, duration * 0.6f).SetDelay(duration * 0.4f))
            .OnComplete(OnAnimEnd);
    }

    /// <summary>动画播完：取回调后立刻置空（防止重复回收），再通知 Manager 回收自己</summary>
    private void OnAnimEnd()
    {
        var cb = _onComplete;
        _onComplete = null;
        cb?.Invoke();
    }

    private void OnDestroy()
    {
        // 物体销毁时顺带杀掉 tween，防止 DOTween 持有已销毁目标的引用报错
        _sequence?.Kill();
    }
}

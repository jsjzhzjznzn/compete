using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 伤害飘字管理器（单例，DontDestroyOnLoad）
///
/// 【职责】
///   全局唯一的飘字调度中心：订阅 E_OnDamage 事件，在受击者头顶飘出伤害数字。
///   负责对象池的取/还、Canvas/EventSystem 的自动创建、飘字样式与动画参数的配置。
///
/// 【触发链路】
///   攻击命中 → 目标 HealthModel.TakeDamage → 派发 E_OnDamage(DamageData)
///   → 本类 OnDamage 收到 → 受击者头顶坐标转屏幕坐标 → 池里取飘字播放。
///
/// 【为什么用 E_OnDamage 而不是在攻击代码里直接飘字】
///   - 零侵入：HealthModel / 攻击逻辑一行不改，飘字是纯 UI 表现层
///   - 自动过滤：闪避无敌被拦下的伤害走 E_DamageBlocked、不派发 E_OnDamage，
///     所以"攻击被躲掉"天然不会飘字，符合"攻击成功才飘"
///   - 任何人造成伤害（玩家连击、AI、后续技能）都自动有飘字，无需逐个接入
///
/// 【对象池】
///   飘字频率高（每刀一次），频繁 Instantiate/Destroy 会产生 GC 和性能抖动，
///   所以用 Stack 池复用。池满时新建（不淘汰），池空闲时回收复用。
///
/// 【为什么 Overlay Canvas 且不挂 CanvasScaler】
///   - Overlay 下 RectTransform.anchoredPosition 就是屏幕像素坐标，
///     生成时一次 WorldToScreenPoint 转换即可，无需每帧跟随/面向相机
///   - 不挂 CanvasScaler 保持 1:1 像素，坐标计算最简单；飘字字号固定，
///     距离远近字都一样清晰（动作游戏 HUD 飘字的常见做法）
/// </summary>
public class DamageTextManager : Singleton<DamageTextManager>
{
    [Header("字体（不填则用 TMP 默认字体 LiberationSans SDF 兜底）")]
    [SerializeField] private TMP_FontAsset fontAsset;

    [Header("样式（普通 / 暴击各配一套颜色字号）")]
    [SerializeField] private DamageTextStyle normalStyle = new DamageTextStyle { color = new Color(0.9f, 0.15f, 0.15f), fontSize = 60, scaleFrom = 0.6f };
    [SerializeField] private DamageTextStyle criticalStyle = new DamageTextStyle { color = new Color(1f, 0.55f, 0.1f), fontSize = 78, punchOnShow = true };

    [Header("动画参数")]
    [SerializeField, Min(0f)] private float floatHeight = 80f;    // 上飘距离（屏幕像素）
    [SerializeField, Min(0f)] private float duration = 1f;        // 动画总时长（秒）
    [SerializeField, Min(0f)] private float headOffsetY = 1.4f;   // 受击者头顶偏移（世界单位，取头顶而非胸口）
    [SerializeField, Min(0f)] private float randomOffsetRange = 20f;  // 每次飘字随机偏移半径（屏幕像素）：连续命中不重叠

    [Header("对象池")]
    [SerializeField, Min(1)] private int poolSize = 10;           // 预创建数量，不够时自动扩

    private Canvas canvas;
    private Camera mainCamera;   // 缓存的场景相机（不依赖 MainCamera tag）
    private readonly Stack<DamageText> pool = new Stack<DamageText>();

    protected override void Awake()
    {
        base.Awake();
        EnsureReady();
    }

    /// <summary>惰性初始化（幂等）：正常流程 Awake 已做，异常时序下首次使用时补齐</summary>
    private void EnsureReady()
    {
        if (canvas != null) return;
        EnsureCanvas();
        for (int i = 0; i < poolSize; i++) pool.Push(CreateText());
    }

    private void OnEnable()
    {
        // 订阅伤害事件（带 this 目标，OnDisable 时 UnregisterTarget 一键注销防泄漏）
        EventCenter.MainInstance.AddListener<DamageData>(E_EventType.E_OnDamage, this, OnDamage);
    }

    private void OnDisable()
    {
        EventCenter.MainInstance.UnregisterTarget(this);
    }

    /// <summary>E_OnDamage 回调：在受击者头顶飘一条伤害数字</summary>
    private void OnDamage(DamageData data)
    {
        // 目标可能已被销毁（死亡清除等），Unity 的 == 会正确判断
        if (data.target == null) return;

        // 受击者头顶（世界坐标）→ 屏幕坐标
        Vector3 worldPos = data.target.transform.position + Vector3.up * headOffsetY;

        // 相机：优先 Camera.main，没有 MainCamera tag 时兜底找场景任意相机并缓存
        if (mainCamera == null)
        {
            mainCamera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            if (mainCamera == null) return;
        }
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        // z < 0 说明目标在相机背后，屏幕坐标是镜像位置，直接跳过
        if (screenPos.z < 0f) return;

        // 随机偏移：连续砍几刀数字不叠在同一个点，更自然
        Vector2 offset = Random.insideUnitCircle * randomOffsetRange;
        screenPos.x += offset.x;
        screenPos.y += offset.y;

        // 伤害显示为整数；暴击用暴击样式
        Show(Mathf.RoundToInt(data.amount), screenPos, data.isCritical ? criticalStyle : normalStyle);
    }

    /// <summary>
    /// 对外接口：手动飘一条字（screenPos 为屏幕像素坐标）。
    /// 除了伤害飘字，回血/格挡/经验等任何需要飘字的系统都能复用这个入口。
    /// </summary>
    public void Show(int amount, Vector2 screenPos, DamageTextStyle style)
    {
        if (style == null) return;

        DamageText text = GetText();
        // 动画播完回调里回收自己，形成"取用 → 播放 → 归还"闭环
        text.Show(amount, screenPos, style, floatHeight, duration, () => Recycle(text));
    }

    // ==================== 对象池 ====================

    private DamageText GetText()
    {
        EnsureReady();
        return pool.Count > 0 ? pool.Pop() : CreateText();
    }

    private void Recycle(DamageText text)
    {
        text.gameObject.SetActive(false);
        pool.Push(text);
    }

    /// <summary>创建一条飘字：RectTransform 锚左下（anchoredPosition 即屏幕坐标）+ TextMeshProUGUI + 描边</summary>
    private DamageText CreateText()
    {
        GameObject go = new GameObject("DamageText");
        go.transform.SetParent(canvas.transform, false);

        // 注意：全程保持 active 完成组件配置！TMP 的 outlineWidth 等 setter 内部会访问字体材质，
        // 若在 inactive 物体上 AddComponent（组件从未 OnEnable，材质未初始化）会抛空引用。
        // 配置全部完成后最后 SetActive(false) 隐藏入池。
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = fontAsset != null ? fontAsset : TMPro.TMP_Settings.defaultFontAsset;
        text.font = font;
        text.alignment = TextAlignmentOptions.Center;
        // 溢出模式：单行不换行不裁剪，飘字字号比默认 rect 大也能完整显示
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;       // 飘字不挡 UI 点击
        text.richText = false;            // 伤害数字纯文本，不解析富文本

        // TMP 内置描边：黑描边让红/白数字在亮背景、粒子特效前也能看清（比 legacy Outline 组件效果好）。
        // 描边依赖字体材质，字体都没拿到时跳过（宁可不描边也不崩）
        if (font != null)
        {
            text.outlineWidth = 0.18f;
            text.outlineColor = new Color32(0, 0, 0, 204);
        }

        DamageText damageText = go.AddComponent<DamageText>();
        go.SetActive(false);
        return damageText;
    }

    // ==================== 环境自动创建 ====================

    /// <summary>确保 Overlay Canvas 存在（挂在管理器下，随单例 DontDestroyOnLoad）</summary>
    private void EnsureCanvas()
    {
        if (canvas != null) return;
        GameObject go = new GameObject("DamageTextCanvas");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;   // 盖在普通 UI 上面，飘字永远可见
    }

    
}

/* ▪ UGUI/TMP 的合批（Batching）有 4 个核心条件，全部满足才能合成一个 draw call：

   1. 相同材质（Material）
   最核心的一条。shader 相同、材质参数相同、必须是同一个材质实例。
   - 陷阱：给某个 UI 元素单独改 text.material（实例化材质）就会断批

   2. 相同纹理（Texture）
   材质里的贴图要一样。TMP 场景中"同字体 = 同一张 SDF 图集"就是这条——不同字体的文字必然不同图集，不能合批。

   3. 渲染顺序连续（相邻）
   合批只对绘制顺序相邻的元素有效。中间如果插了一个不同材质的元素（比如一条飘字中间夹了个 Image），批次就在这里断开：
    [飘字A][飘字B][飘字C][Image][飘字D]  →  A/B/C 一批，D 单独一批
   4. 不互相重叠（不遮挡）
   同一批内元素不能覆盖彼此。如果 B 的 rect 压住了 A，批处理算法会把 A 拆出去（或者把 B 拆开）——因为同批次渲染无法保证正确的遮挡关系。
   - 这就是我们飘字加了随机偏移的意义之一：数字叠在一起不止难看，还会拆批*/

   /*对，完全正确。 这正是 TMP 比 legacy Text 干净的地方：

   - text.color（含 alpha）是顶点颜色——写进顶点数据（每条飘字 4 个顶点各带一份颜色），渲染时 GPU 直接按顶点色插值。改颜色不碰材质、不实例化材质、不触发断批
   - 我们 Show() 里每次 text.color = style.color、DOFade 淡出都是改顶点色 → 不会影响合批
   - 所以不同颜色的飘字（红字、橙字）混在一起照样是同一批*/
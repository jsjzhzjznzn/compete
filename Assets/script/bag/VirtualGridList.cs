using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 虚拟无限网格列表：只依赖 <see cref="ScrollRect"/>，不依赖任何 UI 框架。
///
/// 【它解决什么】
///   列表有几百上千格时，不要真的 Instantiate 那么多格。这里只维护"屏幕能看见的那点格子"
///   （可见行 + 上下缓冲行），滚动时把同一批格子重新赋坐标 + 重新绑数据。
///   撑起滚动条的是 Content 的高度（按总格数算出来的），不是格子数量。
///
/// 【为什么不需要"按索引回收格子"的簿记】
///   格子数量固定为 maxCells，第 index 个数据永远用第 (index % maxCells) 个格子。
///   只要"窗口跨度 <= maxCells"，取模在窗口内就是唯一的，不需要字典记录谁绑了谁。
///   坐标由 index 直接算出来（无状态），所以滚动跳多远都不会错位。
///
/// 【对外只认识"数量"和"一个绑定回调"】
///   它完全不知道背包/道具的存在，谁用谁自己在 bindCell 里把第 index 条数据填进格子。
///   所以同一套东西可以拿去做背包、商店、排行榜、任务列表。
///
/// 【预制体契约】
///   - _viewport：带 RectMask2D 的可见区域
///   - _content ：锚点 TopStretch、pivot (0,1)，**不要挂任何 LayoutGroup / ContentSizeFitter**
///                （格子位置由本脚本手算，挂了布局组件会和手算打架）
///   - _itemTemplate：本预制体内的一个 **inactive**、**固定尺寸**（不要拉伸锚点）的子物体
///   - 格子的锚点/轴心由本脚本统一设成左上 (0,1)，尺寸统一用 _cellSize
/// </summary>
public class VirtualGridList : MonoBehaviour
{
    [SerializeField] private ScrollRect _scrollRect;

    /// <summary>可见区域（带 RectMask2D）。用来算"一屏能放几行"</summary>
    [SerializeField] private RectTransform _viewport;

    /// <summary>承载格子的容器（锚点 TopStretch、pivot (0,1)）</summary>
    [SerializeField] private RectTransform _content;

    /// <summary>格子模板（本预制体内的 inactive 子物体，固定尺寸）</summary>
    [SerializeField] private RectTransform _itemTemplate;

    [SerializeField] private Vector2 _cellSize = new Vector2(120f, 120f);

    /// <summary>格子之间的间距（x = 列间距，y = 行间距）</summary>
    [SerializeField] private Vector2 _spacing = new Vector2(8f, 8f);

    /// <summary>每行几个</summary>
    [SerializeField, Min(1)] private int _columns = 6;

    /// <summary>上下各多渲染几行做缓冲，滚动时不容易看到空白</summary>
    [SerializeField, Min(0)] private int _extraRows = 1;

    /// <summary>数据条数</summary>
    public int Count => _count;

    /// <summary>全部格子（调试/特殊需求用，正常不需要碰）</summary>
    public IReadOnlyList<RectTransform> Cells => _cells;

    private readonly List<RectTransform> _cells = new List<RectTransform>();

    /// <summary>和 _cells 等长，每轮标记哪些格子用到了（用来收掉没用到的）</summary>
    private bool[] _cellInUse;

    private Action<int, RectTransform> _bindCell;

    private int _count;
    private int _maxCells;                 // 当前需要的格子数（随视口大小变）
    private int _firstIndex = -1;          // 当前窗口的首个数据下标（-1 = 还没算过）
    private int _lastIndex = -1;
    private bool _hasSource;               // 是否已经 SetSource 过
    private Vector2 _lastViewportSize;

    private float RowStride => Mathf.Max(1f, _cellSize.y + _spacing.y);
    private float ColStride => Mathf.Max(0f, _cellSize.x + _spacing.x);

    private int RowCount => _columns > 0 ? Mathf.CeilToInt(_count / (float)_columns) : 0;

    private void Awake()
    {
        if (_columns < 1) _columns = 1;

        if (_scrollRect != null)
            _scrollRect.onValueChanged.AddListener(OnScrollChanged);

        if (_scrollRect == null || _viewport == null || _content == null || _itemTemplate == null)
            Debug.LogError($"[VirtualGridList] \"{name}\" 有必填字段没连（需要 ScrollRect / Viewport / Content / ItemTemplate）", this);

        if (_itemTemplate != null && _itemTemplate.gameObject.activeSelf)
            Debug.LogWarning($"[VirtualGridList] \"{name}\" 的 ItemTemplate 是激活状态，会多出一个格子在界面上；请在预制体里把它取消勾选", this);
    }

    private void OnDestroy()
    {
        if (_scrollRect != null)
            _scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
    }

    // ==================== 对外接口 ====================

    /// <summary>
    /// 设置数据条数 + 绑定回调。
    /// 会重算 Content 高度并立刻重绑可见格；**不会**改变当前滚动位置。
    /// </summary>
    /// <param name="count">数据条数</param>
    /// <param name="bindCell">把第 index 条数据填进第 cell 个格子（列表不认识你的数据类型）</param>
    public void SetSource(int count, Action<int, RectTransform> bindCell)
    {
        _bindCell = bindCell;
        _count = Mathf.Max(0, count);
        _hasSource = true;

        Rebuild();

        _firstIndex = -1;
        _lastIndex = -1;
        UpdateVisible(true);
    }

    /// <summary>
    /// 数据内容变了但条数没变：只重绑当前可见的那批格子。
    /// 不碰 Content 高度、不碰滚动位置、不重建格子 —— 所以"用掉一个道具"这种刷新不会让列表跳动。
    /// </summary>
    public void Refresh()
    {
        if (!_hasSource) return;
        UpdateVisible(true);
    }

    /// <summary>滚动到某一格（center = true 时尽量把它居中）</summary>
    public void ScrollToIndex(int index, bool center = true)
    {
        if (_content == null || _viewport == null || _columns <= 0) return;
        if (index < 0 || index >= _count) return;

        float viewHeight = _viewport.rect.height;
        float rowTop = (index / _columns) * RowStride;
        float target = center ? rowTop - (viewHeight - _cellSize.y) * 0.5f : rowTop;

        float maxScroll = Mathf.Max(0f, _content.rect.height - viewHeight);
        _content.anchoredPosition = new Vector2(
            _content.anchoredPosition.x,
            Mathf.Clamp(target, 0f, maxScroll));

        UpdateVisible(true);
    }

    /// <summary>回到顶部</summary>
    public void ScrollToTop()
    {
        if (_content == null) return;
        _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, 0f);
        UpdateVisible(true);
    }

    /// <summary>
    /// 把一个屏幕坐标换算成格子下标 —— 拖拽落点判定用。
    /// 返回 -1 表示落在列表外/越界（调用方当作"没放到格子上"处理）。
    /// 注意：目标格子可能当前没被渲染（在窗口外），但下标照样算得出来，这是无状态坐标计算的好处。
    /// </summary>
    public int GetIndexAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
    {
        if (_content == null) return -1;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, screenPosition, eventCamera, out var local))
            return -1;

        // Content 是 pivot(0,1)：local.y 从顶部往下是负的，local.x 从左边缘往右是正的
        int col = Mathf.FloorToInt(local.x / ColStride);
        int row = Mathf.FloorToInt(-local.y / RowStride);

        int index = GetIndex(col, row);
        return (index >= 0 && index < _count) ? index : -1;
    }

    /// <summary>行列 → 下标（越界返回 -1）</summary>
    public int GetIndex(int col, int row)
    {
        if (col < 0 || col >= _columns || row < 0 || row >= RowCount) return -1;
        return row * _columns + col;
    }

    // ==================== 内部实现 ====================

    /// <summary>按总格数重算 Content 高度（撑滚动条用），并保证格子数量够</summary>
    private void Rebuild()
    {
        int rows = RowCount;
        float height = rows > 0 ? rows * _cellSize.y + (rows - 1) * _spacing.y : 0f;

        if (_content != null)
        {
            // Content 是 TopStretch 锚点：只改高度，宽度 delta 保持预制体里的值
            _content.sizeDelta = new Vector2(_content.sizeDelta.x, Mathf.Max(0f, height));
        }

        _maxCells = CalcMaxCells();
        EnsureCells(_maxCells);
    }

    /// <summary>当前需要多少个格子：一屏的行数 + 上下缓冲，再乘每行个数</summary>
    private int CalcMaxCells()
    {
        float viewHeight = _viewport != null ? _viewport.rect.height : 0f;
        int visibleRows = Mathf.Max(1, Mathf.CeilToInt(viewHeight / RowStride));
        int windowRows = visibleRows + _extraRows * 2;
        return Mathf.Max(_columns, windowRows * _columns);
    }

    /// <summary>格子只增不减地从模板复制出来（复用，不销毁）</summary>
    private void EnsureCells(int needed)
    {
        if (_itemTemplate == null || _content == null) return;

        while (_cells.Count < needed)
        {
            // instantiateInWorldSpace: false —— 保留模板的局部变换，否则会继承模板在世界里的位置
            var cell = Instantiate(_itemTemplate, _content, false);
            cell.name = $"{_itemTemplate.name}_{_cells.Count}";

            // 格子的锚点/轴心/尺寸统一由本脚本说了算，保证坐标能直接用 index 算出来。
            // localScale 也归零成 1：_cellSize 才是权威尺寸，模板上的缩放不去乘它。
            cell.localScale = Vector3.one;
            cell.anchorMin = new Vector2(0f, 1f);
            cell.anchorMax = new Vector2(0f, 1f);
            cell.pivot = new Vector2(0f, 1f);
            cell.sizeDelta = _cellSize;

            cell.gameObject.SetActive(true);
            _cells.Add(cell);
        }

        if (_cellInUse == null || _cellInUse.Length < _cells.Count)
            _cellInUse = new bool[_cells.Count];
    }

    private void OnScrollChanged(Vector2 _) => UpdateVisible(false);

    /// <summary>
    /// 重绑可见窗口。
    /// force = false 时，若窗口范围没变就直接返回（滚动一点点不做无谓的重绑）。
    /// </summary>
    private void UpdateVisible(bool force)
    {
        if (!_hasSource || _content == null || _viewport == null) return;

        int rows = RowCount;
        if (rows <= 0)
        {
            HideAllCells();
            _firstIndex = -1;
            _lastIndex = -1;
            return;
        }

        // 视口大小变了要重算格子预算（比如分辨率/窗口尺寸切换）
        _maxCells = CalcMaxCells();
        EnsureCells(_maxCells);

        // 模板没连/字段没配好时 EnsureCells 会提前返回，这里必须挡住 ——
        // 否则下面的绑定循环会去索引空列表直接抛异常（Awake 里已经报过一次错了）
        if (_cellInUse == null || _cells.Count == 0) return;

        int visibleRows = Mathf.Max(1, Mathf.CeilToInt(_viewport.rect.height / RowStride));
        int windowRows = visibleRows + _extraRows * 2;

        // Content 的 y 就是"已经滚过去的高度"，除以行步长得到第一可见行
        int firstRow = Mathf.FloorToInt(_content.anchoredPosition.y / RowStride) - _extraRows;
        firstRow = Mathf.Clamp(firstRow, 0, Mathf.Max(0, rows - 1));
        int lastRow = Mathf.Min(firstRow + windowRows - 1, rows - 1);

        int firstIndex = firstRow * _columns;
        int lastIndex = Mathf.Min((lastRow + 1) * _columns - 1, _count - 1);

        // 防御：窗口跨度一旦超过格子数，index % _maxCells 就会撞车，
        // 表现为"该显示的格子被别的数据覆盖 / 有些格子干脆空着"。正常算不出来这种情况，
        // 但把这条守住，这个失败类别就彻底不存在了。
        int span = lastIndex - firstIndex + 1;
        if (span > _maxCells)
        {
            _maxCells = span;
            EnsureCells(_maxCells);
            if (_cellInUse == null || _cellInUse.Length < _cells.Count) _cellInUse = new bool[_cells.Count];
            if (_cellInUse == null || _cells.Count == 0) return;
        }

        if (!force && firstIndex == _firstIndex && lastIndex == _lastIndex) return;

        _firstIndex = firstIndex;
        _lastIndex = lastIndex;

        for (int i = 0; i < _cells.Count; i++) _cellInUse[i] = false;

        for (int index = firstIndex; index <= lastIndex; index++)
        {
            int pos = index % _maxCells;
            var cell = _cells[pos];

            if (!cell.gameObject.activeSelf) cell.gameObject.SetActive(true);
            _cellInUse[pos] = true;

            PositionCell(cell, index);
            _bindCell?.Invoke(index, cell);
        }

        // 这一轮没用到的格子收起来（数据变少、视口变大后会出现）
        for (int i = 0; i < _cells.Count; i++)
        {
            if (!_cellInUse[i] && _cells[i].gameObject.activeSelf) _cells[i].gameObject.SetActive(false);
        }
    }

    /// <summary>格子坐标直接由数据下标算出（无状态，所以任意跳转都不会错位）</summary>
    private void PositionCell(RectTransform cell, int index)
    {
        int col = index % _columns;
        int row = index / _columns;
        cell.anchoredPosition = new Vector2(col * ColStride, -row * RowStride);
    }

    private void HideAllCells()
    {
        for (int i = 0; i < _cells.Count; i++)
        {
            if (_cells[i].gameObject.activeSelf) _cells[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 首帧布局（以及之后窗口尺寸变化）后视口尺寸才准，这里补一次重绑。
    /// Content 高度只跟格数有关，所以不会影响滚动位置。
    /// </summary>
    private void LateUpdate()
    {
        if (!_hasSource || _viewport == null) return;
        if (_viewport.rect.size == _lastViewportSize) return;

        _lastViewportSize = _viewport.rect.size;
        UpdateVisible(true);
    }
}

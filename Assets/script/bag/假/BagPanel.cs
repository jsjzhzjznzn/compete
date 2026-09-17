using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 背包面板：数据(模型) ↔ 界面(视图) 的中间层。
///
/// 【两个背包共用一个类】
///   道具背包和武器背包的差别只有"显示哪个 BagData"，所以不写两个子类，
///   用序列化的 _bagType 区分。容量和标题都从 BagManager 的登记表读。
///
/// 【数据 → 界面 是自动的】
///   BagData 任何改动都会发 OnSlotChanged，本面板订阅了它：
///       bag.Add / RemoveAt / Swap   →   Notify(槽位号)   →   OnSlotChanged
///                                       →   _list.Refresh()（重绑可见格）
///                                       →   RefreshDetail()
///   所以外面只要调 AddItem/RemoveItem/SwapItems，界面自己就会变，不用手动刷。
///   用 Refresh() 而不是重新 SetSource：不碰 Content 高度、不碰滚动位置，列表不会跳。
///
/// 【生命周期】
///   标准 Unity：OnEnable + Start 都调一次幂等的 EnsureReady（依赖没就绪时能自愈），
///   OnDisable 退订。开关走 Open/Close/Toggle。
/// </summary>
public class BagPanel : MonoBehaviour
{
    [SerializeField] private BagType _bagType = BagType.Item;

    [Header("顶部")]
    [SerializeField] private TextMeshProUGUI _capacityText;
    [SerializeField] private Button _closeButton;

    [Header("格子列表")]
    [SerializeField] private VirtualGridList _list;

    [Header("操作")]
    [SerializeField] private Button[] _actionButtons;  // 操作按钮池：按选中物品的 ItemAction 动态显示
    [SerializeField] private Button _addButton;       // 打开"加物品"选择器
    [SerializeField] private Image _dragGhost;        // 拖拽跟手的幽灵图（默认 inactive）

    [Header("提示")]
    [SerializeField] private TextMeshProUGUI _noticeText;   // 一行提示（背包满等），默认 inactive

    [Header("详情")]
    [SerializeField] private GameObject _detailRoot;
    [SerializeField] private Image _detailIcon;
    [SerializeField] private TextMeshProUGUI _detailName;
    [SerializeField] private TextMeshProUGUI _detailDesc;
    [SerializeField] private TextMeshProUGUI _detailStat;

    [Header("加物品选择器")]
    [SerializeField] private GameObject _pickerRoot;      // 选择器整块（默认 inactive）
    [SerializeField] private VirtualGridList _pickerList; // 复用同一套虚拟列表
    [SerializeField] private Button _pickerCloseButton;

    /// <summary>拖拽幽灵图的尺寸（跟格子不一样大，小一点更像"拿在手上"）</summary>
    private const float DragGhostSize = 90f;

    /// <summary>提示文字显示多久后自动隐藏（秒）</summary>
    private const float NoticeSeconds = 2.5f;

    private readonly List<int> _pickerIds = new List<int>();

    /// <summary>画选择器格子用的临时槽位（只用来借 BagCell 的画法，不进背包）</summary>
    private readonly BagSlot _previewSlot = new BagSlot();

    private BagData _bag;
    private int _selectedSlot = -1;
    private int _draggingSlot = -1;
    private int _detailIconToken;
    private bool _subscribed;
    private bool _warnedMissingCell;
    private float _noticeHideAt;

    /// <summary>详情名的原始颜色（Awake 时从预制体记下，配置缺失时还原用）</summary>
    private Color _detailNameDefaultColor = Color.white;

    /// <summary>这个面板显示哪个背包</summary>
    public BagType bagType => _bagType;

    /// <summary>当前选中的槽位号（-1 = 没选）</summary>
    public int selectedSlot => _selectedSlot;

    /// <summary>当前显示的背包数据（外面想直接操作数据也可以拿）</summary>
    public BagData bag => Bag;

    private BagData Bag
    {
        get
        {
            if (_bag == null) _bag = BagManager.Instance.Get(_bagType);
            return _bag;
        }
    }

    // ==================== 开关（外部接按键/按钮） ====================

    public void Open() => gameObject.SetActive(true);

    public void Close() => gameObject.SetActive(false);

    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);

    // ==================== 生命周期 ====================

    private void Awake()
    {
        if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        if (_addButton != null) _addButton.onClick.AddListener(OpenPicker);
        if (_pickerCloseButton != null) _pickerCloseButton.onClick.AddListener(ClosePicker);

        SetGhostActive(false);
        if (_pickerRoot != null) _pickerRoot.SetActive(false);

        // 记下详情名在预制体里配的原始颜色 —— 稀有度着色后要能还原（免得重复定义颜色常量）
        if (_detailName != null) _detailNameDefaultColor = _detailName.color;
    }

    private void OnEnable()
    {
        EnsureReady();
        RefreshDetail();
    }

    /// <summary>OnEnable 时依赖万一还没就绪，Start 再补一次（幂等，不会重复铺列表）</summary>
    private void Start() => EnsureReady();

    private void OnDisable()
    {
        if (_bag != null && _subscribed)
        {
            _bag.OnSlotChanged -= OnSlotChanged;
            _bag.OnAddOverflow -= OnAddOverflow;
            _subscribed = false;
        }
        _draggingSlot = -1;
        SetGhostActive(false);
    }

    private void OnDestroy()
    {
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
        if (_addButton != null) _addButton.onClick.RemoveListener(OpenPicker);
        if (_pickerCloseButton != null) _pickerCloseButton.onClick.RemoveListener(ClosePicker);
        if (_bag != null)
        {
            _bag.OnSlotChanged -= OnSlotChanged;
            _bag.OnAddOverflow -= OnAddOverflow;
        }
    }

    /// <summary>
    /// 幂等初始化：拿数据、写标题、铺列表、订阅变化。OnEnable 和 Start 都会调。
    /// 列表按"容量"铺（不是按现有道具数），并且用 Count != capacity 做自愈判断 ——
    /// 这样即使第一次进来时依赖没就绪，后一次也能补上，不会出现"一个格子都没有"的状态。
    /// </summary>
    private void EnsureReady()
    {
        var bag = Bag;
        if (bag == null) return;

        if (_selectedSlot >= bag.capacity) _selectedSlot = -1;

        // 标题不在这里设 —— 它是 UI 文案，写在预制体的 BagTitle 上
        if (_capacityText != null)
            _capacityText.text = $"{bag.capacity} 格";

        if (_list != null && _list.Count != bag.capacity)
            _list.SetSource(bag.capacity, BindCell);

        if (!_subscribed)
        {
            bag.OnSlotChanged += OnSlotChanged;
            bag.OnAddOverflow += OnAddOverflow;
            _subscribed = true;
        }
    }

    // ==================== 增 / 删 / 换（对外功能入口） ====================

    /// <summary>往本背包加物品，返回没放进去的余量（0 = 全放进去了）</summary>
    public int AddItem(ItemData config, int count = 1) => config != null ? AddItem(config.itemIdHash, count) : count;

    /// <summary>往本背包加物品（按 itemIdHash），返回没放进去的余量</summary>
    public int AddItem(int itemIdHash, int count = 1)
    {
        var bag = Bag;
        return bag != null ? bag.Add(itemIdHash, count) : count;
    }

    /// <summary>删掉指定格子的若干个（count 超过现有数量就删光），返回是否发生了变化</summary>
    public bool RemoveItem(int slotIndex, int count = 1)
    {
        var bag = Bag;
        return bag != null && bag.RemoveAt(slotIndex, count);
    }

    /// <summary>交换两格（同种且可叠会自动合并），返回是否发生了变化</summary>
    public bool SwapItems(int fromSlot, int toSlot)
    {
        var bag = Bag;
        return bag != null && bag.Swap(fromSlot, toSlot);
    }

    /// <summary>清空本背包</summary>
    public void ClearBag() => Bag?.Clear();

    /// <summary>
    /// 执行当前选中物品的某个操作（外部想程序化触发也能调）。
    /// 数据修改由策略内部走 BagData 完成，这里只负责"跑完刷新界面"。
    /// </summary>
    public bool RunAction(ItemAction action, int slotIndex)
    {
        var bag = Bag;
        if (action == null || bag == null) return false;

        bool changed = action.Run(bag, slotIndex);

        // 排序/整理这类会重排槽位的操作：选中是按槽位号记的，排完就指到别的物品上了 → 清掉
        // （以后有了 instanceId 才能做"排序后选中跟着物品走"）
        if (action.invalidatesSelection) _selectedSlot = -1;

        // 改了数据的话 BagData 已经发过通知（OnSlotChanged 刷过一次）；
        // 没改数据的操作（比如只回血）这里补一次，保证按钮的可点状态是最新的
        RefreshDetail();
        return changed;
    }

    // ==================== 列表绑定 ====================

    /// <summary>VirtualGridList 的回调：把第 index 个槽画到第 cellRect 个格子上</summary>
    private void BindCell(int index, RectTransform cellRect)
    {
        var cell = GetCell(cellRect);
        var bag = Bag;
        if (cell == null || bag == null) return;

        var slot = bag.GetSlot(index);
        var config = (slot != null && !slot.IsEmpty) ? ItemDatabase.Resolve(slot.itemId) : null;

        cell.OnClicked = OnCellClicked;
        cell.OnBeginDragCell = OnCellBeginDrag;
        cell.OnDragCell = OnCellDrag;
        cell.OnEndDragCell = OnCellEndDrag;

        cell.Bind(index, slot, config, index == _selectedSlot);
    }

    /// <summary>取格子组件（没挂就报一次错，不要每帧刷屏）</summary>
    private BagCell GetCell(RectTransform cellRect)
    {
        if (cellRect == null) return null;

        var cell = cellRect.GetComponent<BagCell>();
        if (cell == null && !_warnedMissingCell)
        {
            _warnedMissingCell = true;
            Debug.LogError($"[BagPanel] 格子模板上没有 BagCell 组件，格子不会显示内容（面板 \"{name}\"）", this);
        }
        return cell;
    }

    // ==================== 点击 / 拖拽 ====================

    private void OnCellClicked(int slotIndex)
    {
        var bag = Bag;
        var slot = bag?.GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty) return;

        _selectedSlot = slotIndex;
        _list?.Refresh();       // 重绑可见格，高亮才会跟着变
        RefreshDetail();
    }

    private void OnCellBeginDrag(int fromSlot)
    {
        var bag = Bag;
        var slot = bag?.GetSlot(fromSlot);
        if (slot == null || slot.IsEmpty) return;   // 空格子不给拖

        _draggingSlot = fromSlot;

        if (_dragGhost != null)
        {
            var config = ItemDatabase.Resolve(slot.itemId);

            // 这一格已经显示着图标了，所以缓存里基本一定有；拿不到就只是幽灵图没图，不影响拖拽功能
            var sprite = config != null ? ItemIconLoader.Get(config.iconPath) : null;

            _dragGhost.sprite = sprite;
            _dragGhost.enabled = sprite != null;
            _dragGhost.rectTransform.sizeDelta = new Vector2(DragGhostSize, DragGhostSize);
            SetGhostActive(true);
        }
    }

    private void OnCellDrag(int fromSlot, PointerEventData eventData)
    {
        if (_draggingSlot != fromSlot || _dragGhost == null) return;

        var parent = _dragGhost.rectTransform.parent as RectTransform;
        if (parent == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out var local))
            _dragGhost.rectTransform.anchoredPosition = local;
    }

    private void OnCellEndDrag(int fromSlot, PointerEventData eventData)
    {
        SetGhostActive(false);

        if (_draggingSlot != fromSlot) { _draggingSlot = -1; return; }
        _draggingSlot = -1;

        // 松手位置换算成格子下标；拖到列表外（-1）就当作取消
        int toSlot = _list != null ? _list.GetIndexAtScreenPosition(eventData.position, eventData.pressEventCamera) : -1;
        if (toSlot < 0 || toSlot == fromSlot) return;

        SwapItems(fromSlot, toSlot);
    }

    private void SetGhostActive(bool active)
    {
        if (_dragGhost != null && _dragGhost.gameObject.activeSelf != active)
            _dragGhost.gameObject.SetActive(active);
    }

    // ==================== 加物品选择器 ====================

    /// <summary>打开"加物品"选择器：列出 ItemDatabase 里本背包收得下的全部配置</summary>
    public void OpenPicker()
    {
        var database = ItemDatabase.Instance;
        if (database == null)
        {
            Debug.LogError("[BagPanel] 没有找到 ItemDatabase（Assets/Resources/ItemDatabase.asset），列不出可添加的物品", this);
            return;
        }

        _pickerIds.Clear();
        var bag = Bag;
        if (bag != null)
        {
            for (int i = 0; i < database.AllItems.Count; i++)
            {
                var config = database.AllItems[i];
                if (config != null && config.itemType == bag.acceptItemType) _pickerIds.Add(config.itemIdHash);
            }
        }

        if (_pickerRoot != null) _pickerRoot.SetActive(true);
        if (_pickerList != null) _pickerList.SetSource(_pickerIds.Count, BindPickerCell);
    }

    public void ClosePicker()
    {
        if (_pickerRoot != null) _pickerRoot.SetActive(false);
    }

    /// <summary>选择器的格子：点一下 = 往背包加 1 个（复用 BagCell 的画法，不写进背包数据）</summary>
    private void BindPickerCell(int index, RectTransform cellRect)
    {
        var cell = GetCell(cellRect);
        if (cell == null) return;

        bool valid = index >= 0 && index < _pickerIds.Count;
        var config = valid ? ItemDatabase.Resolve(_pickerIds[index]) : null;

        // 借一个临时槽位把"有东西"的状态喂给 BagCell（数量传 1 就不显示数字）
        _previewSlot.itemId = valid ? _pickerIds[index] : 0;
        _previewSlot.count = valid ? 1 : 0;

        cell.OnClicked = OnPickerCellClicked;
        cell.OnBeginDragCell = null;    // 选择器里不给拖
        cell.OnDragCell = null;
        cell.OnEndDragCell = null;

        cell.Bind(index, _previewSlot, config, false);
    }

    private void OnPickerCellClicked(int pickerIndex)
    {
        if (pickerIndex < 0 || pickerIndex >= _pickerIds.Count) return;

        // 加不进去时不用在这里判断 —— BagData 会发 OnAddOverflow，由 OnAddOverflow 统一提示
        AddItem(_pickerIds[pickerIndex], 1);
    }

    // ==================== 数据变化 → 刷新 ====================

    /// <summary>槽位数据变了：重绑可见格 + 刷详情（不重建列表，滚动位置不变）</summary>
    private void OnSlotChanged(int slotIndex)
    {
        var bag = Bag;

        // 选中的那个格子被拿空了就取消选中（先纠正再刷新，否则高亮会残留一帧）
        if (bag != null && _selectedSlot >= 0)
        {
            var selected = bag.GetSlot(_selectedSlot);
            if (selected == null || selected.IsEmpty) _selectedSlot = -1;
        }

        _list?.Refresh();
        RefreshDetail();
    }

    /// <summary>
    /// 有东西没放进去（背包满 / 只放下了一部分）。
    /// 只负责提示 —— 数据层已经自己处理好"能放多少放多少"，UI 不做任何判断。
    /// </summary>
    private void OnAddOverflow(int itemId, int remain)
    {
        var config = ItemDatabase.Resolve(itemId);
        string name = config != null ? config.itemName : $"#{itemId}";

        ShowNotice($"{name} ×{remain} 放不下，背包已满");
        Debug.Log($"[BagPanel] 「{_bagType}」放不下 {name} ×{remain}");
    }

    /// <summary>
    /// 显示一条提示，<see cref="NoticeSeconds"/> 秒后自动隐藏。
    ///
    /// 【为什么不需要节流】
    ///   整个面板只复用这**一个** TextMeshProUGUI，不是每次提示都 new 一个飘字对象。
    ///   所以连点 100 次也不会叠出一屏字 —— 只是把隐藏时间不断往后推。
    ///   这是选"单文本 + 自动隐藏"而不是"飘字池"的关键理由。
    /// </summary>
    public void ShowNotice(string message)
    {
        if (_noticeText == null)
        {
            // 没接提示控件时至少别让信息丢了
            Debug.LogWarning($"[BagPanel] 提示（未接提示控件）：{message}");
            return;
        }

        _noticeText.text = message;
        if (!_noticeText.gameObject.activeSelf) _noticeText.gameObject.SetActive(true);
        _noticeHideAt = Time.unscaledTime + NoticeSeconds;
    }

    private void Update()
    {
        if (_noticeText == null || !_noticeText.gameObject.activeSelf) return;
        if (Time.unscaledTime < _noticeHideAt) return;

        _noticeText.gameObject.SetActive(false);
    }

    // ==================== 详情面板 ====================

    private void RefreshDetail()
    {
        var bag = Bag;
        var slot = bag?.GetSlot(_selectedSlot);
        bool hasItem = slot != null && !slot.IsEmpty;
        var config = hasItem ? ItemDatabase.Resolve(slot.itemId) : null;

        // 按钮池必须在 hasItem 早退之前刷 —— 否则"取消选中 / 物品被拿空"时旧按钮会留在界面上
        RefreshActionButtons(hasItem ? config : null, _selectedSlot);

        if (_detailRoot != null && _detailRoot.activeSelf != hasItem)
            _detailRoot.SetActive(hasItem);

        if (!hasItem) return;

        ApplyDetailIcon(config);
        if (_detailName != null)
        {
            _detailName.text = config != null ? config.itemName : $"未知物品({slot.itemId})";

            // 名字颜色也**无条件写**：配置缺失（config == null）时还原成预制体的原始色，
            // 否则会留着上一次选中物品的稀有度颜色
            _detailName.color = config != null ? config.rarity.ToTextColor() : _detailNameDefaultColor;
        }

        if (_detailDesc != null)
            _detailDesc.text = config != null ? config.desc : "找不到这条配置，检查 ItemDatabase 里有没有它";

        if (_detailStat != null)
            _detailStat.text = BuildStatText(config);
    }

    /// <summary>
    /// 按选中物品的 ItemAction 列表，驱动按钮池：有几个操作就显示几个按钮。
    ///
    /// 这里完全不判断"这件物品是什么类型、该显示什么按钮" ——
    /// 那些信息全在配置里（ItemData.GetActions()），所以加/减操作只改配置，本方法一行不用动。
    /// </summary>
    private void RefreshActionButtons(ItemData config, int slotIndex)
    {
        if (_actionButtons == null) return;

        var actions = config != null ? config.GetActions() : null;
        int actionCount = actions != null ? actions.Count : 0;

        for (int i = 0; i < _actionButtons.Length; i++)
        {
            var button = _actionButtons[i];
            if (button == null) continue;

            bool show = i < actionCount;
            if (button.gameObject.activeSelf != show) button.gameObject.SetActive(show);
            if (!show) continue;

            var action = actions[i];
            if (action == null)
            {
                button.interactable = false;
                continue;
            }

            // 可点状态由策略自己判断（材料够不够、等级够不够…），面板不掺和
            button.interactable = action.CanRun(Bag, slotIndex);

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = action.actionName;

            button.onClick.RemoveAllListeners();

            // 闭包要捕获"当前这一轮"的值，不能直接用循环变量和会变的字段
            var capturedAction = action;
            int capturedSlot = slotIndex;
            button.onClick.AddListener(() => RunAction(capturedAction, capturedSlot));
        }
    }

    /// <summary>详情图标也走异步加载，同样用 token 防串味（连续点不同格子时会把旧请求丢掉）</summary>
    private void ApplyDetailIcon(ItemData config)
    {
        int token = ++_detailIconToken;   // ★ 必须在发起请求之前取

        if (_detailIcon == null) return;

        if (config == null || !config.hasIcon)
        {
            _detailIcon.enabled = false;
            return;
        }

        var cached = ItemIconLoader.Get(config.iconPath);
        if (cached != null)
        {
            _detailIcon.sprite = cached;
            _detailIcon.enabled = true;
            return;
        }

        _detailIcon.sprite = null;
        _detailIcon.enabled = false;

        ItemIconLoader.Request(config.iconPath, sprite =>
        {
            if (token != _detailIconToken) return;
            if (_detailIcon == null) return;

            _detailIcon.sprite = sprite;
            _detailIcon.enabled = sprite != null;
        });
    }

    /// <summary>把 ItemData.stats 拼成几行属性文本（武器加成在本轮的可见出口，穿戴逻辑还没做）</summary>
    private static string BuildStatText(ItemData config)
    {
        if (config == null || config.stats == null || config.stats.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < config.stats.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(FormatStat(config.stats[i]));
        }
        return sb.ToString();
    }

    private static string FormatStat(ItemStatEntry entry)
    {
        string attrName = AttrDisplayName(entry.attr);

        // Multiply 的 value 是倍率（见 MutableAttribute.Recalculate：mulProduct *= mod.value）
        if (entry.modType == ModType.Multiply)
            return $"{attrName}  ×{entry.value:0.###}";

        // Add 在系数型属性上是小数（0.15 = +15%），在白值型上就是点数（20 = +20）
        return AttrTypeUtil.IsRatio(entry.attr)
            ? $"{attrName}  +{entry.value * 100f:0.#}%"
            : $"{attrName}  +{entry.value:0.##}";
    }

    private static string AttrDisplayName(AttrType attr) => attr switch
    {
        AttrType.DamageUp => "增伤",
        AttrType.DamageDown => "减伤",
        AttrType.MoveSpeed => "移速",
        AttrType.Attack => "攻击力",
        AttrType.Defense => "防御力",
        _ => attr.ToString(),
    };

    // ==================== 调试 ====================

    /// <summary>调试：每个配置塞一份（可堆叠的塞两倍上限，正好占两格看叠层）</summary>
    [ContextMenu("调试：塞几件测试物品")]
    private void DebugAddFewItems() => DebugAddItems(false);

    /// <summary>调试：一直塞到背包满（用来看虚拟列表滚动；120 格 = 20 行）</summary>
    [ContextMenu("调试：塞满本背包（看虚拟列表滚动）")]
    private void DebugFillBag() => DebugAddItems(true);

    private void DebugAddItems(bool fillToFull)
    {
        var bag = Bag;
        if (bag == null) return;

        var database = ItemDatabase.Instance;
        if (database == null)
        {
            Debug.LogError("[BagPanel] 找不到 Assets/Resources/ItemDatabase.asset，没法塞测试物品", this);
            return;
        }

        int guard = 0;
        bool addedAny;
        do
        {
            addedAny = false;
            for (int i = 0; i < database.AllItems.Count; i++)
            {
                var config = database.AllItems[i];
                if (config == null || config.itemType != bag.acceptItemType) continue;

                // 塞满模式一次只塞一个满叠，才能铺得开；塞几件模式塞两叠方便看叠层
                int batch = config.maxStack > 1 ? config.maxStack * (fillToFull ? 1 : 2) : 1;
                if (bag.Add(config.itemIdHash, batch) == 0) addedAny = true;
            }
            guard++;
        }
        while (fillToFull && addedAny && guard < 300);

        int used = 0;
        for (int i = 0; i < bag.capacity; i++)
        {
            if (!bag.GetSlot(i).IsEmpty) used++;
        }

        Debug.Log($"[BagPanel] 调试填充「{_bagType}」：{used}/{bag.capacity} 格已用");
    }
}

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
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _capacityText;
    [SerializeField] private Button _closeButton;

    [Header("格子列表")]
    [SerializeField] private VirtualGridList _list;

    [Header("操作")]
    [SerializeField] private Button _dropButton;      // 丢弃当前选中的（整叠）
    [SerializeField] private Button _addButton;       // 打开"加物品"选择器
    [SerializeField] private Image _dragGhost;        // 拖拽跟手的幽灵图（默认 inactive）

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

    private readonly List<int> _pickerIds = new List<int>();

    /// <summary>画选择器格子用的临时槽位（只用来借 BagCell 的画法，不进背包）</summary>
    private readonly BagSlot _previewSlot = new BagSlot();

    private BagData _bag;
    private int _selectedSlot = -1;
    private int _draggingSlot = -1;
    private int _detailIconToken;
    private bool _subscribed;
    private bool _warnedMissingCell;

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
        if (_dropButton != null) _dropButton.onClick.AddListener(DropSelected);
        if (_addButton != null) _addButton.onClick.AddListener(OpenPicker);
        if (_pickerCloseButton != null) _pickerCloseButton.onClick.AddListener(ClosePicker);

        SetGhostActive(false);
        if (_pickerRoot != null) _pickerRoot.SetActive(false);
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
            _subscribed = false;
        }
        _draggingSlot = -1;
        SetGhostActive(false);
    }

    private void OnDestroy()
    {
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
        if (_dropButton != null) _dropButton.onClick.RemoveListener(DropSelected);
        if (_addButton != null) _addButton.onClick.RemoveListener(OpenPicker);
        if (_pickerCloseButton != null) _pickerCloseButton.onClick.RemoveListener(ClosePicker);
        if (_bag != null) _bag.OnSlotChanged -= OnSlotChanged;
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

        if (_title != null)
            _title.text = BagManager.Instance.TryGetDef(_bagType, out var def) ? def.displayName : _bagType.ToString();

        if (_capacityText != null)
            _capacityText.text = $"{bag.capacity} 格";

        if (_list != null && _list.Count != bag.capacity)
            _list.SetSource(bag.capacity, BindCell);

        if (!_subscribed)
        {
            bag.OnSlotChanged += OnSlotChanged;
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

    /// <summary>丢弃当前选中的一整叠（没有选中就什么都不做）</summary>
    public void DropSelected()
    {
        var bag = Bag;
        var slot = bag?.GetSlot(_selectedSlot);
        if (slot == null || slot.IsEmpty) return;

        // 删掉后 BagData 会发通知 → OnSlotChanged 会把选中清掉并刷新界面
        bag.RemoveAt(_selectedSlot, slot.count);
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

        int remain = AddItem(_pickerIds[pickerIndex], 1);
        if (remain > 0)
            Debug.LogWarning($"[BagPanel] 「{_bagType}」背包满了，再加不进去了");
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

    // ==================== 详情面板 ====================

    private void RefreshDetail()
    {
        var bag = Bag;
        var slot = bag?.GetSlot(_selectedSlot);
        bool hasItem = slot != null && !slot.IsEmpty;
        var config = hasItem ? ItemDatabase.Resolve(slot.itemId) : null;

        if (_detailRoot != null && _detailRoot.activeSelf != hasItem)
            _detailRoot.SetActive(hasItem);

        if (!hasItem) return;

        ApplyDetailIcon(config);
        if (_detailName != null)
            _detailName.text = config != null ? config.itemName : $"未知物品({slot.itemId})";

        if (_detailDesc != null)
            _detailDesc.text = config != null ? config.desc : "找不到这条配置，检查 ItemDatabase 里有没有它";

        if (_detailStat != null)
            _detailStat.text = BuildStatText(config);
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

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 背包里的一个格子（挂在格子模板根上，会被 VirtualGridList 复制出来复用）。
///
/// 【职责】
///   1) 把一条槽位数据画出来（图标 / 数量 / 高亮 / 空槽表现）
///   2) 把用户的动作转成"第几格发生了什么"喊给外面听：点击、开始拖、拖动中、松手
///   它不认识背包容器，也不知道外面拿这些回调干什么 —— 换/合并/丢弃都由 BagPanel 决定。
///
/// 【为什么回调里都带 slotIndex】
///   格子是被复用的，同一个格子对象一会儿是第 3 格、一会儿是第 40 格。
///   所以不能靠"我是谁"来判断，每次都得把**当前**绑的槽位号一起报出去。
/// </summary>
public class BagCell : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _countText;
    [SerializeField] private Button _button;

    /// <summary>选中高亮框（默认应为 inactive）</summary>
    [SerializeField] private GameObject _selectFrame;

    /// <summary>空槽遮罩 / 空格占位图（可以不接，格子底框已经能表示"空"）</summary>
    [SerializeField] private GameObject _emptyMask;

    /// <summary>点击，参数 = 槽位号</summary>
    public Action<int> OnClicked;

    /// <summary>开始拖拽，参数 = 起始槽位号</summary>
    public Action<int> OnBeginDragCell;

    /// <summary>拖拽中（每帧），参数 = 起始槽位号 + 指针事件（用来跟手画幽灵图）</summary>
    public Action<int, PointerEventData> OnDragCell;

    /// <summary>松手，参数 = 起始槽位号 + 指针事件（外面用指针位置算落点）</summary>
    public Action<int, PointerEventData> OnEndDragCell;

    /// <summary>当前绑的槽位号（-1 = 还没绑过）</summary>
    public int SlotIndex => _slotIndex;

    /// <summary>当前绑的配置有没有图标（拖拽幽灵图要用）</summary>
    public Sprite CurrentIcon => _icon != null && _icon.enabled ? _icon.sprite : null;

    private int _slotIndex = -1;

    private void Awake()
    {
        if (_button != null) _button.onClick.AddListener(OnButtonClick);
    }

    private void OnDestroy()
    {
        if (_button != null) _button.onClick.RemoveListener(OnButtonClick);
    }

    /// <summary>把第 slotIndex 个槽的数据填进这个格子</summary>
    /// <param name="slotIndex">槽位号（= 回调里回传的值）</param>
    /// <param name="slot">槽位数据，可为 null</param>
    /// <param name="config">配置，查不到时传 null（只会少个图标，数量照显示，便于发现配置漏了）</param>
    /// <param name="selected">是否处于选中状态</param>
    public void Bind(int slotIndex, BagSlot slot, ItemData config, bool selected)
    {
        _slotIndex = slotIndex;

        bool hasItem = slot != null && !slot.IsEmpty;

        if (_icon != null)
        {
            var sprite = config != null ? config.icon : null;
            _icon.enabled = sprite != null;
            if (sprite != null) _icon.sprite = sprite;
        }

        if (_countText != null)
        {
            // 只有 1 个时不显示数字，界面干净
            _countText.text = (hasItem && slot.count > 1) ? slot.count.ToString() : string.Empty;
        }

        if (_emptyMask != null && _emptyMask.activeSelf != !hasItem)
            _emptyMask.SetActive(!hasItem);

        bool highlight = hasItem && selected;
        if (_selectFrame != null && _selectFrame.activeSelf != highlight)
            _selectFrame.SetActive(highlight);

        // 空格子不可点、也不可拖（拖了没意义）
        if (_button != null) _button.interactable = hasItem;
    }

    private void OnButtonClick() => OnClicked?.Invoke(_slotIndex);

    // ==================== 拖拽 ====================

    public void OnBeginDrag(PointerEventData eventData) => OnBeginDragCell?.Invoke(_slotIndex);

    public void OnDrag(PointerEventData eventData) => OnDragCell?.Invoke(_slotIndex, eventData);

    public void OnEndDrag(PointerEventData eventData) => OnEndDragCell?.Invoke(_slotIndex, eventData);
}

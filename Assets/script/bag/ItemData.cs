using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道具 / 武器的静态配置（ScriptableObject）
/// Inspector 右键 Create/Bag/Item 创建，一条配置代表一种东西，运行时可被多处共享。
///
/// 职责：只描述"这件东西是什么"（标识/类型/图标/文案/堆叠上限/属性加成），不持有任何运行时状态。
/// 运行时状态（在哪个格子、剩几个）放在 BagSlot 里，由 BagData 统一管理。
///
/// 道具背包和武器背包共用这一张表，靠 itemType 区分。
/// </summary>
[CreateAssetMenu(fileName = "Item", menuName = "Create/Bag/Item")]
public class ItemData : ScriptableObject
{
    [SerializeField, Header("唯一标识（留空则用资产名兜底；同一 id 视为同一件东西）")]
    private string _itemId;

    [SerializeField, Header("显示名称（留空则用资产名）")]
    private string _itemName;

    [SerializeField, Header("类型：决定归道具背包还是武器背包")]
    private ItemType _itemType = ItemType.Item;

    [SerializeField, Header("图标路径（散图全路径，例：Assets/Resource/ui/mingchao/道具/T_IconA80_02_UI.png）")]
    private string _iconPath;

    [SerializeField, TextArea(2, 4), Header("描述")]
    private string _desc;

    [SerializeField, Header("单格堆叠上限（1 = 不可堆叠，武器应填 1）")]
    private int _maxStack = 1;

    [SerializeField, Header("属性加成（本轮只用于详情展示，穿戴逻辑后续实现）")]
    private List<ItemStatEntry> _stats = new List<ItemStatEntry>();

    #region 只读属性封装（外部仅读取，禁止修改配置数据）
    /// <summary>配置里填的原始标识（可能为空字符串）</summary>
    public string itemId => _itemId;

    /// <summary>
    /// 标识的预计算哈希，槽位里存的就是它。
    /// Animator.StringToHash 内部有全局字符串→哈希缓存表，同一字符串只算一次，后续比较都走 int。
    ///
    /// 空 itemId 兜底用【资产名】：否则所有没填 itemId 的配置都会哈希到同一个值，
    /// 被当成同一件东西叠到一起（配置串味）。见 BuffData.buffIdHash 的同款处理。
    /// </summary>
    public int itemIdHash => Animator.StringToHash(string.IsNullOrEmpty(_itemId) ? name : _itemId);

    /// <summary>显示名称（没填就退回资产名，避免 UI 出现空白）</summary>
    public string itemName => string.IsNullOrEmpty(_itemName) ? name : _itemName;

    /// <summary>类型（道具 / 武器）</summary>
    public ItemType itemType => _itemType;

    /// <summary>图标路径（散图全路径，走 YooAsset 加载，对应 ItemIconLoader）</summary>
    public string iconPath => _iconPath;

    /// <summary>配了图标路径没有</summary>
    public bool hasIcon => !string.IsNullOrEmpty(_iconPath);

    /// <summary>描述文案</summary>
    public string desc => _desc;

    /// <summary>单格堆叠上限（至少 1）</summary>
    public int maxStack => Mathf.Max(1, _maxStack);

    /// <summary>是否可堆叠</summary>
    public bool stackable => maxStack > 1;

    /// <summary>属性加成列表（不穿戴时只是展示数据）</summary>
    public IReadOnlyList<ItemStatEntry> stats => _stats;
    #endregion

#if UNITY_EDITOR
    /// <summary>编辑器校验：只提醒，不阻断（配错只会表现为叠层异常/武器能叠）</summary>
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(_itemId))
            Debug.LogWarning($"[ItemData] \"{name}\" 未填 itemId，将临时用资产名当标识；建议显式填写", this);

        if (_itemType == ItemType.Weapon && _maxStack > 1)
            Debug.LogWarning($"[ItemData] \"{name}\" 是武器但 maxStack={_maxStack}，武器应当不可堆叠", this);

        // 图标路径提醒：填了就得是工程内的资产路径，写错了运行时才报（格子会没图）
        if (!string.IsNullOrEmpty(_iconPath) && !_iconPath.StartsWith("Assets/"))
            Debug.LogWarning($"[ItemData] \"{name}\" 的图标路径不是 Assets/ 开头的工程内路径：{_iconPath}", this);
    }
#endif
}

/// <summary>
/// 一条属性加成。复用现有的 AttrType / ModType，所以以后实现穿戴时可以直接：
///   attrComp.Get(entry.attr).AddModifier(new AttributeModifier(entry.modType, entry.value, source))
/// 卸下用 MutableAttribute.RemoveAllFromSource(source) —— 不需要新的属性体系。
/// </summary>
[Serializable]
public struct ItemStatEntry
{
    [Tooltip("作用在哪条属性上（白值型如 Attack/Defense，系数型如 DamageUp）")]
    public AttrType attr;

    [Tooltip("Add = 加法进入加和，Multiply = 乘法进入累乘")]
    public ModType modType;

    [Tooltip("数值：白值型 Add 填点数（20 = +20）；系数型 Add 填小数（0.15 = +15%）；Multiply 一律填倍率（1.15 = ×1.15）")]
    public float value;
}

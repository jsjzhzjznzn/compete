using UnityEngine;

/// <summary>
/// 物品操作策略基类（ScriptableObject 资产）。
///
/// 【它解决什么】
///   "点击这件物品能干什么"不是写在背包代码里的，而是**配置出来的**：
///   一件物品能有哪些操作，取决于它的配置上挂了哪些策略资产。
///   于是新增一种行为 = 新建一个策略资产 + 挂上去，背包和面板代码一行都不用改。
///
/// 【写法对齐工程里的 Buff】
///   Buff 是 BuffData（配置）→ _effect（BuffEffect 抽象 SO 策略），
///   这里是 ItemData（配置）→ _actions（ItemAction 抽象 SO 策略），同一套路数。
///   策略自己的参数配在资产上（例：ItemActionUse._healAmount），不用给 ItemData 加一堆用不上的字段。
///
/// 【铁律：不要直接改 BagSlot】
///   策略里**不要写 slot.count--**，必须调 BagData 的方法 —— 它负责发变更通知，
///   UI 刷新 / 以后的存档和联网同步都挂在那条通知上。直接改字段会静默跳过它们。
/// </summary>
public abstract class ItemAction : ScriptableObject
{
    [SerializeField, Header("按钮文字（留空则用资产名）")]
    private string _actionName;

    /// <summary>按钮文字，例：【使用】【穿戴】【分解】</summary>
    public string actionName => string.IsNullOrEmpty(_actionName) ? name : _actionName;

    /// <summary>
    /// 能不能点。不满足时 UI 把按钮置灰（例：材料不够、装备等级不符）。
    /// 默认 true，需要校验的子类重写。
    /// </summary>
    public virtual bool CanRun(BagData bag, int slotIndex) => true;

    /// <summary>
    /// 执行。返回**是否真的改了数据**（UI 据此决定要不要刷新）。
    /// 注意：数据修改一律走 BagData，不要直接动 BagSlot。
    /// </summary>
    public abstract bool Run(BagData bag, int slotIndex);

    /// <summary>给子类复用：取这一格的配置（空槽返回 null）</summary>
    protected static ItemData ResolveConfig(BagData bag, int slotIndex)
    {
        var slot = bag != null ? bag.GetSlot(slotIndex) : null;
        return (slot != null && !slot.IsEmpty) ? ItemDatabase.Resolve(slot.itemId) : null;
    }
}

/// <summary>
/// 物品操作的"作用对象"（本机玩家）。
///
/// 【为什么需要这个】
///   背包是一个纯数据模块，它**不知道谁是玩家**（工程里也没有 LocalPlayer 访问点）。
///   而"使用药水"这类操作需要作用在某个角色身上。
///   所以约定：由玩家的 spawn 流程在判定 IsOwner 之后写一次 LocalOwner，之后所有操作都能拿到。
///
/// 【还没接入时的行为】
///   LocalOwner 为 null → ItemActionUse.CanRun 返回 false → 按钮置灰，点了也不会出错。
/// </summary>
public static class ItemActionTarget
{
    /// <summary>当前操作的作用对象。由玩家 spawn（IsOwner 分支）时赋值。</summary>
    public static GameObject LocalOwner;
}

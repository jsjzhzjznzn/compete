using UnityEngine;

/// <summary>
/// 【使用】：消耗 N 个，对作用对象产生效果。
///
/// 【参数配在策略资产上】
///   _healAmount / _cost 这些是策略自己的字段，不给 ItemData 加 ——
///   否则 100 件物品的配置里都会多出几个只有消耗品用得上的字段。
///   想共享同一效果的物品，共用同一份这个资产即可（跟 Buff 的 BuffEffect 一个道理）。
///
/// 【作用对象从哪来】
///   背包不知道谁是玩家，所以走 ItemActionTarget.LocalOwner（由玩家 spawn 时赋值）。
///   还没接入时 CanRun 返回 false → 按钮置灰，不会出错。
///
/// ⚠ 联机注意：HealthModel.Heal 里有一句 `if (IsSpawned && !IsServer) return;` ——
///   血量是服务端权威的。所以联机时这里必须改成发请求给服务端，而不是本地直接调。
///   单机（对象没 Spawn）时不受影响，直接生效。
/// </summary>
[CreateAssetMenu(fileName = "ItemAction_Use", menuName = "Create/Bag/ItemAction/Use")]
public class ItemActionUse : ItemAction
{
    [SerializeField, Header("回多少血（<=0 表示这个物品不回血）")]
    private float _healAmount = 30f;

    [SerializeField, Min(1), Header("用一次消耗几个")]
    private int _cost = 1;

    public override bool CanRun(BagData bag, int slotIndex)
    {
        if (ItemActionTarget.LocalOwner == null) return false;   // 还不知道给谁用

        var slot = bag != null ? bag.GetSlot(slotIndex) : null;
        return slot != null && !slot.IsEmpty && slot.count >= _cost;
    }

    public override bool Run(BagData bag, int slotIndex)
    {
        var owner = ItemActionTarget.LocalOwner;
        if (owner == null)
        {
            Debug.LogWarning("[ItemActionUse] 还没设置作用对象（ItemActionTarget.LocalOwner），用不了。" +
                             "在玩家 spawn 判定 IsOwner 之后写一行 ItemActionTarget.LocalOwner = gameObject; 即可。");
            return false;
        }

        var slot = bag != null ? bag.GetSlot(slotIndex) : null;
        if (slot == null || slot.IsEmpty || slot.count < _cost) return false;

        // ① 先产生效果
        if (_healAmount > 0f)
        {
            var health = owner.GetComponent<HealthModel>();
            if (health != null)
            {
                health.Heal(_healAmount);
            }
            else
            {
                Debug.LogWarning($"[ItemActionUse] 作用对象 \"{owner.name}\" 上没有 HealthModel，回血没生效");
            }
        }

        // ② 再扣物品。扣失败说明状态变了，效果已经发出去了 —— 单机下这个窗口极小，先不处理
        return bag.RemoveAt(slotIndex, _cost);
    }
}

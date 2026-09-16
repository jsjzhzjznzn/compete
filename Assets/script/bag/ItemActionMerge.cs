using UnityEngine;

/// <summary>
/// 【合并】：一键整理 —— 把背包里同种且可叠的散堆，合并到占用格数最少。
///
/// 例：3 个格子分别放 40 / 30 / 20 个药水（上限 99）
///     整理后变成 90 个占 1 格、其余 2 格空出来。
///
/// 算法本身在 BagData.MergeAll()（纯数据操作，不属于策略），
/// 策略只负责"这是个能点的操作、按钮叫合并" —— 职责边界就在这。
/// </summary>
[CreateAssetMenu(fileName = "ItemAction_Merge", menuName = "Create/Bag/ItemAction/Merge")]
public class ItemActionMerge : ItemAction
{
    public override bool CanRun(BagData bag, int slotIndex) => bag != null;

    public override bool Run(BagData bag, int slotIndex)
    {
        return bag != null && bag.MergeAll();
    }
}

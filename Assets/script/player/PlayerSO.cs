using UnityEngine;

/// <summary>
/// 角色数据资产（Inspector 右键 Create/Character/Player 创建）
/// 一个角色一份配置，集中管理所有移动状态数据
/// </summary>
[CreateAssetMenu(fileName = "Player", menuName = "Create/Character/Player")]
public class PlayerSO : ScriptableObject
{
    [field: SerializeField] public PlayerMovementData movementData { get; private set; }

    // 预留：连击系统接入后使用
    [field: SerializeField] public PlayerComboData comboData { get; private set; }
}

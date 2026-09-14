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

    /// <summary>基础属性（攻击力 / 防御力 / 最大血量）：角色生成时写入 AttributeComponent / HealthModel</summary>
    [field: SerializeField, Header("基础属性（攻击力 / 防御力 / 最大血量）")]
    public CharacterStatsData statsData { get; private set; }
}

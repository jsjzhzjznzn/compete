using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI 角色数据资产（Inspector 右键 Create/Character/AIPlayer 创建）
/// 模仿 PlayerSO：一个 AI 角色一份配置，集中管理所有状态数据与行为参数
/// </summary>
[CreateAssetMenu(fileName = "AIPlayer", menuName = "Create/Character/AIPlayer")]
public class AIPlayerSO : ScriptableObject
{
    [field: SerializeField] public AIMovementData movementData { get; private set; }

    [field: SerializeField, Header("Boss 阶段表（顺序即阶段：0=起始；每阶段自带连招与属性）")]
    public List<AIPhaseData> phases { get; private set; } = new List<AIPhaseData>();
}

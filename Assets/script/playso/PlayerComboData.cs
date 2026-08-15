using UnityEngine;

/// <summary>
/// 连击数据（预留）
/// 连击系统接入后补充：连击动画列表、连击窗口、朝向锁定等
/// </summary>
[System.Serializable]
 public class PlayerComboData
    {
        [field: SerializeField, Header("��ʽ����")] public PlayerComboSOData comboData { get; private set; }

        [field: SerializeField, Header("���˼��")] public PlayerEnemyDetectionData playerEnemyDetectionData { get; private set;}

    

    }
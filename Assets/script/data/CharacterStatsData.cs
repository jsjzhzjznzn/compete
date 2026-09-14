using UnityEngine;

/// <summary>
/// 角色基础属性配置（白值）：一个角色一份，挂在 PlayerSO / AIPlayerSO 上。
///
/// 这些是"基础值"：装备 / 等级 / Buff 通过 AttributeComponent 的 Modifier 在此基础上加减乘，
/// 招式只提供"攻击力×多少"的倍率——所以装备加攻，全部招式一起变强。
///
/// 注意：这里只放"基础/上限"这类配置，**不放运行时状态**（当前血量等运行时数据在 HealthModel）。
/// </summary>
[System.Serializable]
public class CharacterStatsData
{
    /// <summary>基础攻击力（伤害 = 攻击力 × 招式倍率；装备加攻就是加这个）</summary>
    [field: SerializeField] public float baseAttack { get; private set; } = 100f;

    /// <summary>基础防御力（减伤用；伤害公式接入见第二步）</summary>
    [field: SerializeField] public float baseDefense { get; private set; } = 0f;

    /// <summary>基础最大血量（角色生成时写给 HealthModel）</summary>
    [field: SerializeField] public float baseMaxHP { get; private set; } = 100f;
}

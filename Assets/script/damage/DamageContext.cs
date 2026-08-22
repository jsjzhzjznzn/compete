using UnityEngine;

/// <summary>
/// 伤害计算上下文（class，可变）
/// 一次伤害结算的全部输入：基础伤害、暴击参数、攻击方/受击方。
/// 由调用方（CharacterCombo 等）在每次攻击时组装，只存在于管道计算期间。
///
/// 为什么是 class 而不是 struct：
///   管道各阶段要原地修改 currentDamage 等中间值，class 引用传递避免复制；
///   且它只是"计算期临时对象"，不参与事件中心派发（事件中心仍用 struct 通知），
///   与 EventCenter.DamageData 职责明确区分：Context 是输入，DamageData 是结算通知。
/// </summary>
public class DamageContext
{
    /// <summary>基础伤害（攻击段 ComboData.comboDamage）</summary>
    public float baseDamage;

    /// <summary>暴击率（0~1，调用方从 ComboData.critRate 传入）</summary>
    public float critRate;

    /// <summary>暴击倍率（暴击时伤害 = 当前伤害 × 倍率，来自 ComboData.critMultiplier）</summary>
    public float critMultiplier;

    /// <summary>攻击方 GameObject（查增伤系数用；可为 null 表示无来源）</summary>
    public GameObject attacker;

    /// <summary>受击方 GameObject（查减伤系数用）</summary>
    public GameObject defender;
}

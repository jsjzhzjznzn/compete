using UnityEngine;

/// <summary>
/// 角色行为状态（行为类 Buff 的唯一操作对象）：眩晕 / 沉默 / 无敌。
///
/// 全部用【引用计数】，禁止直接赋值 bool：
///   多个眩晕 Buff 同时生效 → 计数累加；其中一个到期 → 计数 -1；
///   计数 > 0 才生效。否则会出现"一个 Buff 结束把另一个还在生效的控制状态取消"。
///
/// 说明：本组件已提供状态与计数，但【尚未接入】移动/攻击/闪避的拦截逻辑，
/// 后续需要时在对应业务处读 IsStun / IsSilence / IsInvincible 做拦截。
/// </summary>
public class CharacterState : MonoBehaviour
{
    private int _stunCount;
    private int _silenceCount;
    private int _invincibleCount;

    /// <summary>是否被眩晕（禁止行动）</summary>
    public bool IsStun => _stunCount > 0;

    /// <summary>是否被沉默（禁止释放技能）</summary>
    public bool IsSilence => _silenceCount > 0;

    /// <summary>是否无敌</summary>
    public bool IsInvincible => _invincibleCount > 0;

    public void AddStun() => _stunCount++;
    public void RemoveStun() { if (_stunCount > 0) _stunCount--; }

    public void AddSilence() => _silenceCount++;
    public void RemoveSilence() { if (_silenceCount > 0) _silenceCount--; }

    public void AddInvincible() => _invincibleCount++;
    public void RemoveInvincible() { if (_invincibleCount > 0) _invincibleCount--; }

    /// <summary>清空全部状态计数（死亡/复活重置）</summary>
    public void ClearAll()
    {
        _stunCount = 0;
        _silenceCount = 0;
        _invincibleCount = 0;
    }
}

using UnityEngine;

// ============================================================================
// 行为类 Buff 策略：操作 CharacterState（引用计数），不碰 MutableAttribute / Modifier。
// 【尚未接入拦截】：状态与计数已就绪，移动/攻击/闪避是否被禁止，需后续在业务处读取。
// ============================================================================

/// <summary>眩晕策略：挂上时眩晕计数 +1，结束 -1（计数 &gt; 0 才真正眩晕）</summary>
[CreateAssetMenu(fileName = "BuffStun", menuName = "Create/Buff/眩晕")]
public class BuffStunEffect : BuffEffect
{
    public override BuffControlFlags controlFlags => BuffControlFlags.Stun;

    public override void OnApply(BuffInstance buff) => GetState(buff)?.AddStun();

    public override void OnExpire(BuffInstance buff) => GetState(buff)?.RemoveStun();

    private static CharacterState GetState(BuffInstance buff)
        => buff.owner != null ? buff.owner.GetComponent<CharacterState>() : null;
}

/// <summary>沉默策略：挂上时沉默计数 +1，结束 -1（禁止释放技能）</summary>
[CreateAssetMenu(fileName = "BuffSilence", menuName = "Create/Buff/沉默")]
public class BuffSilenceEffect : BuffEffect
{
    public override BuffControlFlags controlFlags => BuffControlFlags.Silence;

    public override void OnApply(BuffInstance buff) => GetState(buff)?.AddSilence();

    public override void OnExpire(BuffInstance buff) => GetState(buff)?.RemoveSilence();

    private static CharacterState GetState(BuffInstance buff)
        => buff.owner != null ? buff.owner.GetComponent<CharacterState>() : null;
}

/// <summary>无敌策略：挂上时无敌计数 +1，结束 -1（计数 &gt; 0 才真正无敌）</summary>
[CreateAssetMenu(fileName = "BuffInvincible", menuName = "Create/Buff/无敌")]
public class BuffInvincibleEffect : BuffEffect
{
    public override BuffControlFlags controlFlags => BuffControlFlags.Invincible;

    public override void OnApply(BuffInstance buff) => GetState(buff)?.AddInvincible();

    public override void OnExpire(BuffInstance buff) => GetState(buff)?.RemoveInvincible();

    private static CharacterState GetState(BuffInstance buff)
        => buff.owner != null ? buff.owner.GetComponent<CharacterState>() : null;
}

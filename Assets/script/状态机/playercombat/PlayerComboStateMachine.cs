using UnityEngine;

/// <summary>
/// 玩家连击状态机
/// 创建并持有所有连击状态与连击战斗辅助类，供连击状态访问
/// </summary>
public class PlayerComboStateMachine : StateMachine
{
    public Player Player { get; }//只能在构造函数里修改

    public PlayerComboReusableData ReusableData { get; }

    public CharacterCombo characterCombo { get; }

    public PlayerATKIngState ATKIngState { get; }
    public PlayerNullState NullState { get; }
    public PlayerSkillState SkillState { get; }

    public PlayerComboStateMachine(Player player)
    {
        Player = player;

        ReusableData = new PlayerComboReusableData();

        // 连击战斗辅助类（选招/连击推进/伤害触发等）
        characterCombo = new CharacterCombo(player, ReusableData, player.PlayerSO?.comboData?.comboData);
        characterCombo.Init();

        ATKIngState = new PlayerATKIngState(this);
        NullState = new PlayerNullState(this);
        SkillState = new PlayerSkillState(this);
    }

    protected override void OnStateSwitched()
    {
        base.OnStateSwitched();
        // 连击状态变化 → 同步给网络（拥有者写入；远程端据此播放攻击/技能动画）
        if (CurrentState is PlayerComboState comboState)
            Player.SyncComboState(comboState.StateType);
    }
}

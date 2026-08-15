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
}

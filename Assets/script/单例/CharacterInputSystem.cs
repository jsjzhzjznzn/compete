using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 角色输入系统（单例）
/// 封装 playerinputaction.inputactions 生成的强类型类 Playerinputaction，
/// 统一对角色脚本/状态机暴露输入状态
/// </summary>
public class CharacterInputSystem : Singleton<CharacterInputSystem>
{
    public Playerinputaction inputActions;

    protected override void Awake()
    {
        base.Awake();
        if (inputActions == null)
            inputActions = new Playerinputaction();
    }

    private void OnEnable()
    {
        inputActions?.Enable();
    }

    private void OnDisable()
    {
        inputActions?.Disable();
    }

    // ==================== 输入封装 ====================

    // 移动
    public Vector2 PlayerMove => inputActions.player.move.ReadValue<Vector2>();

    // 冲刺（Shift）
    public bool Sprint          => inputActions.player.sprint.triggered;
    public bool Sprint_Continue => inputActions.player.sprint.phase == InputActionPhase.Performed;

    // 轻攻击（鼠标左键）
    public bool Attack          => inputActions.player.attack.triggered;
    public bool Attack_Continue => inputActions.player.attack.phase == InputActionPhase.Performed;

    // 重攻击（鼠标右键）
    public bool HeavyAttack          => inputActions.player.heavyattk.triggered;
    public bool HeavyAttack_Continue => inputActions.player.heavyattk.phase == InputActionPhase.Performed;

    // 技能（E）
    public bool Skill          => inputActions.player.skill.triggered;
    public bool Skill_Continue => inputActions.player.skill.phase == InputActionPhase.Performed;
}

using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterInputSystem : Singleton<CharacterInputSystem>
{
    private PlayerInput playerInput;

    protected override void Awake()
    {
        base.Awake();
        /* FindAnyObjectByType<T>()
作用：随机返回任意一个符合类型的对象，不保证顺序
Unity 官方设计目的：性能更快，内部遍历可以提前结束，不保证返回哪一个；
风险：如果场景有多个PlayerInput，每次运行拿到的实例可能不一样，极易出诡异 Bug；
不推荐用于玩家输入这种必须唯一的组件。*/
        playerInput = Object.FindAnyObjectByType<PlayerInput>();
    }

    // 移动
    public Vector2 PlayerMove => playerInput.actions["move"].ReadValue<Vector2>();

    // 冲刺
    public bool Sprint          => playerInput.actions["sprint"].triggered;
    public bool Sprint_Continue => playerInput.actions["sprint"].phase == InputActionPhase.Performed;

    // 轻攻击（attack）
    public bool Attack          => playerInput.actions["attack"].triggered;
    //public bool Attack_Continue => playerInput.actions["attack"].phase == InputActionPhase.Performed;

    // 重攻击（heavyattk）
    public bool HeavyAttack          => playerInput.actions["heavyattk"].triggered;
   // public bool HeavyAttack_Continue => playerInput.actions["heavyattk"].phase == InputActionPhase.Performed;

    // 技能
    public bool Skill          => playerInput.actions["skill"].triggered;
    public bool Skill_Continue => playerInput.actions["skill"].phase == InputActionPhase.Performed;
}

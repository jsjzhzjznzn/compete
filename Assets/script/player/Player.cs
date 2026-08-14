using Animancer;
using UnityEngine;

/// <summary>
/// 角色主控脚本，继承移动基类
/// 负责：输入驱动、动画播放、连接状态机
/// </summary>
public class Player : CharacterMoveControllerBase
{
    [SerializeField] public string currentMovementState;   // 调试用：当前移动状态名
    //  [SerializeField] public string currentComboState;

    [SerializeField] private PlayerSO playerSO;            // 角色数据资产（Inspector 拖入）

    [Header("视角参考")]
    [SerializeField] private Camera viewCamera;            // 主相机（带CinemachineBrain），Inspector拖入

    /// <summary>相机 Transform（相机相对移动转换用）；未配置时回退 Camera.main</summary>
    public Transform CameraTransform =>
        viewCamera != null ? viewCamera.transform
        : Camera.main != null ? Camera.main.transform : null;

    private PlayerMovementStateMachine stateMachine;

    protected override void Awake()
    {
        base.Awake();
        stateMachine = new PlayerMovementStateMachine(this, playerSO);
        // 初始状态在 Start 切换：确保所有单例（CharacterInputSystem 等）Awake 已完成，
        // 事件订阅能拿到已初始化的 inputActions
    }

    protected override void Start()
    {
        base.Start();
        stateMachine.SwitchState(stateMachine.idlingState);   // 初始进入待机

        // 游戏运行时锁定并隐藏鼠标光标（用鼠标视角）
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    protected override void Update()
    {
        base.Update();                        // 地面检测 + 重力 + 竖直速度
        stateMachine?.Update();               // 状态机Tick
        //currentMovementState = stateMachine.CurrentState?.GetType().Name;   // 同步调试显示
    }

    // ================================================================
    // 动画播放（统一入口，方便以后改Transition、加Fade）
    // ================================================================

    public void PlayAnimation(AnimationClip clip)
    {
        characterAnimancer.Play(clip);
    }

    public void PlayAnimation(AnimationClip clip, float fadeDuration)
    {
        characterAnimancer.Play(clip, fadeDuration);
    }

    // ================================================================
    // 输入响应（状态机会调用这些，暂时暴露）
    // ================================================================

    public float CurrentMoveSpeed => CharacterInputSystem.MainInstance.Movement.magnitude;
    public bool IsMoving => CurrentMoveSpeed > 0.1f;
   public bool IsSprintHeld => CharacterInputSystem.MainInstance.dashHeld;
    public bool IsSprintPressed => CharacterInputSystem.MainInstance.dashPressed;
    public bool PressedAttack => CharacterInputSystem.MainInstance.Attack;
    public bool PressedSkill => CharacterInputSystem.MainInstance.Skill;
}

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

    /// <summary>角色数据资产（连击状态机取连招配置用）</summary>
    public PlayerSO PlayerSO => playerSO;

    // 角色音效组件缓存（懒获取：首次访问时 GetComponent 并缓存，避免状态机频繁调用重复查找）
    private ActorAudioComponent actorAudio;
    /// <summary>角色音效组件（播放 3D 空间音效用；未挂载返回 null）</summary>
    public ActorAudioComponent ActorAudio
    {
        get
        {
            if (actorAudio == null)
            {
                actorAudio = GetComponent<ActorAudioComponent>();
            }
            return actorAudio;
        }
    }
    [SerializeField] public PlayerCameraUtility playerCameraUtility;

    [Header("视角参考")]
    [SerializeField] private Camera viewCamera;            // 主相机（带CinemachineBrain），Inspector拖入

    /// <summary>相机 Transform（相机相对移动转换用）；未配置时回退 Camera.main</summary>
    public Transform CameraTransform =>
        viewCamera != null ? viewCamera.transform
        : Camera.main != null ? Camera.main.transform : null;

    [Header("武器骨骼（攻击特效挂点）")]
    [SerializeField] private Transform weaponBone;

    /// <summary>
    /// 武器骨骼 Transform（VFX 挂点）。优先用 Inspector 配置，未配置则在首次访问时
    /// 沿 transform.Find 查找 "Bip001/Anbi_Weapon_02"，结果缓存复用。
    /// </summary>
    public Transform WeaponBone
    {
        get
        {
            if (weaponBone == null) weaponBone = transform.Find("Bip001/Anbi_Weapon_02");
            return weaponBone;
        }
    }

    private PlayerMovementStateMachine stateMachine;
    private PlayerComboStateMachine comboStateMachine;

    /// <summary>移动状态机（连击状态等需要联动移动状态时访问）</summary>
    public PlayerMovementStateMachine MovementStateMachine => stateMachine;

    protected override void Awake()
    {
        base.Awake();
        stateMachine = new PlayerMovementStateMachine(this, playerSO);
        comboStateMachine = new PlayerComboStateMachine(this);
        playerCameraUtility?.Init();                    // 初始化相机辅助（缓存 Virtual Camera 的 CinemachinePOV）
        // 初始状态在 Start 切换：确保所有单例（CharacterInputSystem 等）Awake 已完成，
        // 事件订阅能拿到已初始化的 inputActions
    }

    protected override void Start()
    {
        base.Start();
        stateMachine.SwitchState(stateMachine.idlingState);   // 初始进入待机
        comboStateMachine.SwitchState(comboStateMachine.NullState);   // 连击初始进入空状态

        // 游戏运行时锁定并隐藏鼠标光标（用鼠标视角）
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    protected override void Update()
    {
        base.Update();                        // 地面检测 + 重力 + 竖直速度
        stateMachine?.Update();               // 状态机Tick
        comboStateMachine?.Update();          // 连击状态机Tick
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

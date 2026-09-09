using Animancer;
using Cinemachine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Unity.Netcode;
using UnityEngine;


/// <summary>
/// 角色主控脚本，继承移动基类
/// 负责：输入驱动、动画播放、连接状态机
/// </summary>
public class Player : CharacterMoveControllerBase
{
    [SerializeField] public string currentMovementState;   // 调试用：当前移动状态名
     [SerializeField] public string currentComboState;

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
            // 本对象可能已被销毁（切场景/退出战斗后的残留状态机回调会访问到这里）：
            // Unity 的 == 能判"已销毁"，先挡掉，避免走进 GetComponent 抛 MissingReferenceException
            if (this == null) return null;
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

    // [Header("武器骨骼（攻击特效挂点）")]
    // [SerializeField] private Transform weaponBone;
    //
    // /// <summary>
    // /// 武器骨骼 Transform（VFX 挂点）。优先用 Inspector 配置，未配置则在首次访问时
    // /// 沿 transform.Find 查找 "Bip001/Anbi_Weapon_02"，结果缓存复用。
    // /// </summary>
    // public Transform WeaponBone
    // {
    //     get
    //     {
    //         if (weaponBone == null) weaponBone = transform.Find("Bip001/Anbi_Weapon_02");
    //         return weaponBone;
    //     }
    // }

    private PlayerMovementStateMachine stateMachine;
    private PlayerComboStateMachine comboStateMachine;

    /// <summary>移动状态机（连击状态等需要联动移动状态时访问）</summary>
    public PlayerMovementStateMachine MovementStateMachine => stateMachine;

    /// <summary>连击状态机（调试组件读取当前连招/判定数据用）</summary>
    public PlayerComboStateMachine ComboStateMachine => comboStateMachine;

    // ================================================================
    // 网络动画状态同步（拥有者写入，远程端回放动画）
    // ================================================================

    /// <summary>状态起始的服务端时间戳（秒）：远程端据此计算"状态已播放时长"做相位对齐。
    /// 注意：必须声明在所有状态变量之前（NetworkVariable 按声明顺序应用，保证状态回调里读到的已是最新值）</summary>
    private readonly NetworkVariable<float> netStateStartTime =
        new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    /// <summary>移动状态同步变量（拥有者写入；远程端 OnValueChanged 回放对应状态动画）</summary>
    private readonly NetworkVariable<MovementStateType> netMoveState =
        new(MovementStateType.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    /// <summary>连击状态同步变量（拥有者写入；远程端据此播放攻击/技能动画）</summary>
    private readonly NetworkVariable<ComboStateType> netComboState =
        new(ComboStateType.Null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    /// <summary>连击段数同步变量（拥有者写入；远程端段数前进时重播新段攻击动画）</summary>
    private readonly NetworkVariable<int> netComboIndex =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    /// <summary>前进攻击标识（拥有者写入；远程端把轻击容器首段切换/还原为前进攻击段）</summary>
    private readonly NetworkVariable<bool> netForwardATK =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    /// <summary>
    /// 本角色座位号（服务端生成时写入：0=第一人，1=第二人）。
    /// 客户端据此选场景里对应的那套相机（Camera/thirdcamera 或 Camera (1)/thirdcamera (1)）。
    /// </summary>
    public readonly NetworkVariable<int> NetSeatId =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>是否作为远程镜像端（联网且非拥有者）；单机永远为 false</summary>
    public bool IsRemote => IsSpawned && !IsOwner;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // ===== 联机排障日志：确认每个角色归属到谁（定位"客户端无法操作角色"，定位后可删） =====
        ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
        Debug.Log($"[Player] name={name} Owner={OwnerClientId} Local={localId} IsOwner={IsOwner} IsSpawned={IsSpawned}");

        netMoveState.OnValueChanged += OnMoveStateChanged;
        netComboState.OnValueChanged += OnComboStateChanged;
        netComboIndex.OnValueChanged += OnComboIndexChanged;
        netForwardATK.OnValueChanged += OnForwardATKChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        netMoveState.OnValueChanged -= OnMoveStateChanged;
        netComboState.OnValueChanged -= OnComboStateChanged;
        netComboIndex.OnValueChanged -= OnComboIndexChanged;
        netForwardATK.OnValueChanged -= OnForwardATKChanged;

        // 角色离场（战斗结束/断线清理等）时取消状态机挂起的计时器，防止到点回调访问已销毁组件
        stateMachine?.CancelPendingTimers();
    }

    protected override void Awake()
    {
        base.Awake();
        stateMachine = new PlayerMovementStateMachine(this, playerSO);
        comboStateMachine = new PlayerComboStateMachine(this);
        playerCameraUtility?.Init();                    // 初始化相机辅助（缓存 Virtual Camera 的 CinemachinePOV）
        // 初始状态在 Start 切换：确保所有单例（CharacterInputSystem 等）Awake 已完成，
        // 事件订阅能拿到已初始化的 inputActions

        // Buff 组件兜底挂载：不强制在 Inspector 手动挂，漏挂时自动补（增伤/减伤/持续伤害都依赖它）
        if (GetComponent<BuffComponent>() == null)
            gameObject.AddComponent<BuffComponent>();
    }

    protected override void Start()
    {
        base.Start();
        // 联网时状态由 NetworkVariable 同步驱动：拥有者端在此做初始切换（写入网络），
        // 远程端跳过（OnNetworkSpawn 时已收到拥有者当前状态并回放）；单机走本地初始切换
        if (!IsSpawned || IsOwner)
        {
            stateMachine.SwitchState(stateMachine.idlingState);   // 初始进入待机
            comboStateMachine.SwitchState(comboStateMachine.NullState);   // 连击初始进入空状态
        }

        // 游戏运行时锁定并隐藏鼠标光标（用鼠标视角）；非拥有者端不抢光标
        if (!IsSpawned || IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // ============ 联机排障日志：拥有者端每秒采样一次输入（定位"客户端无法操作角色"，定位后可删） ============
    private float _inputProbeNextLogTime;

    /// <summary>
    /// 只有 IsOwner 的 Update 会跑到这里：每秒打一次输入采样。
    /// 判断方法：客户端若收不到这行 = 客户端不是 Owner(Update 提前 return)；收到了但输入恒为 0 = 输入系统没生效。
    /// </summary>
    private void LogInputProbeOncePerSecond()
    {
        if (Time.unscaledTime < _inputProbeNextLogTime) return;
        _inputProbeNextLogTime = Time.unscaledTime + 1f;

    }

    protected override void Update()
    {
        // 非拥有者端不做本地模拟：状态机/输入只驱动自己的角色
        if (IsSpawned && !IsOwner) return;

        LogInputProbeOncePerSecond();         // 联机排障：客户端跑不到这里 = IsOwner=false
        TryBindSceneCamera();                 // 场景 vcam 就绪前每帧重试绑定（时序兜底）

        base.Update();                        // 地面检测 + 重力 + 竖直速度
        stateMachine?.Update();               // 状态机Tick
        comboStateMachine?.Update();          // 连击状态机Tick
        HandleDodgeInput();                   // 右键开无敌窗口（带冷却）
        //currentMovementState = stateMachine.CurrentState?.GetType().Name;   // 同步调试显示
    }

    // ================================================================
    // 相机绑定（场景预置两套相机：第一人 Camera/thirdcamera，第二人 Camera (1)/thirdcamera (1)）
    // ================================================================

    private const string SeatCameraBaseName = "Camera";      // 主相机基名（渲染，带 CinemachineBrain）
    private const string SeatVCamBaseName = "thirdcamera";   // vcam 基名（跟随）

    private bool _cameraAssigned; // 本机相机关联完成（成功后停止每帧重试）
    private bool _warnedNoCamera; // 找不到相机警告只打一次

    /// <summary>
    /// 按本角色座位号选场景里预置的那套相机（不生成相机）：
    ///   座位 0 → Camera / thirdcamera
    ///   座位 1 → Camera (1) / thirdcamera (1)
    /// 把 vcam 的 Follow/LookAt 绑到本角色，主相机赋给 viewCamera，
    /// 并停用本机其它 Camera/vcam，避免一屏叠多个画面。
    /// 场景相机可能比角色晚就绪，找不到就每帧重试，绑上即停。
    /// </summary>
    private void TryBindSceneCamera()
    {
        if (_cameraAssigned) return;

        int seat = NetSeatId.Value;
        string vcamName = seat == 0 ? SeatVCamBaseName : $"{SeatVCamBaseName} ({seat})";
        string camName = seat == 0 ? SeatCameraBaseName : $"{SeatCameraBaseName} ({seat})";

        var vcamGo = GameObject.Find(vcamName);
        var camGo = GameObject.Find(camName);
        var vcam = vcamGo != null ? vcamGo.GetComponent<CinemachineVirtualCamera>() : null;
        var mainCam = camGo != null ? camGo.GetComponent<Camera>() : null;
        if (vcam == null || mainCam == null)
        {
            if (!_warnedNoCamera)
            {
                _warnedNoCamera = true;
                Debug.LogWarning($"[{name}] 没找到座位{seat}的相机({camName}/{vcamName})，将每帧重试；请在战斗场景确认这两个相机存在且命名一致", this);
            }
            return; // 下帧再试
        }

        // 确保本座位那套相机是启用状态（防止场景里初始是关的）
        vcamGo.SetActive(true);
        camGo.SetActive(true);

        vcam.Follow = transform;   // vcam 跟随本角色
        vcam.LookAt = transform;   // vcam 看向本角色
        viewCamera = mainCam;      // Player.viewCamera(主相机) = 本座位的 Camera

        // 一屏只留自己那套相机：停用本机其它主相机和 vcam
        var allVCams = FindObjectsByType<CinemachineVirtualCamera>();
        foreach (var v in allVCams)
            if (v != vcam) v.gameObject.SetActive(false);
        var allCams = FindObjectsByType<Camera>();
        foreach (var c in allCams)
            if (c != mainCam) c.gameObject.SetActive(false);

        _cameraAssigned = true;
        Debug.Log($"[{name}] 座位{seat}: 主相机={camName} vcam={vcamName} 已关联本角色", this);
    }

    // ================================================================
    // 受击处理
    // ================================================================

    private void OnEnable()
    {
        // 受到伤害 → 进受击硬直（HealthModel.TakeDamage 派发 E_OnDamage）
        EventCenter.MainInstance.AddListener<DamageData>(E_EventType.E_OnDamage, this, OnDamageTaken);
        // 无敌窗口内被打中 → 触发闪避（HealthModel.TakeDamage 派发 E_DamageBlocked）
        EventCenter.MainInstance.AddListener<DamageData>(E_EventType.E_DamageBlocked, this, OnDamageBlocked);
    }

    private void OnDisable()
    {
        EventCenter.MainInstance.UnregisterTarget(this);

        // 角色被禁用/销毁（切场景、退出战斗、反序列化等）时状态机不会走正常 Exit，
        // 主动取消挂起的计时器（与 OnNetworkDespawn 互为兜底，幂等可重复调用）
        stateMachine?.CancelPendingTimers();
    }

    private void OnDamageTaken(DamageData data)
    {
        if (IsSpawned && !IsOwner) return;   // 血量只在拥有者端结算，事件也只在拥有者端派发
        if (data.target == gameObject && !data.isDoT)   // DoT（灼烧类）不进受击硬直
        {
            TakeHit();
        }
    }

    // ================================================================
    // 状态同步写入（状态机 SwitchState 钩子 / 连击辅助类调用；只允许拥有者写）
    // ================================================================

    /// <summary>同步移动状态（拥有者写入；单机/远程端为空操作）</summary>
    public void SyncMoveState(MovementStateType type)
    {
        if (!IsSpawned || !IsOwner) return;
        netMoveState.Value = type;
        netStateStartTime.Value = (float)NetworkManager.ServerTime.Time;   // 状态起始时间戳（相位对齐用）
    }

    /// <summary>同步连击状态（拥有者写入；单机/远程端为空操作）</summary>
    public void SyncComboState(ComboStateType type)
    {
        if (!IsSpawned || !IsOwner) return;
        netComboState.Value = type;
        netStateStartTime.Value = (float)NetworkManager.ServerTime.Time;   // 状态起始时间戳（相位对齐用）
    }

    /// <summary>同步连击段数（拥有者写入；单机/远程端为空操作）</summary>
    public void SyncComboIndex(int index)
    {
        if (!IsSpawned || !IsOwner) return;
        netComboIndex.Value = index;
        netStateStartTime.Value = (float)NetworkManager.ServerTime.Time;   // 状态起始时间戳（相位对齐用）
    }

    /// <summary>同步前进攻击标识（拥有者写入；单机/远程端为空操作）</summary>
    public void SyncForwardATK(bool isForward)
    {
        if (!IsSpawned || !IsOwner) return;
        netForwardATK.Value = isForward;
    }

    // ================================================================
    // 状态同步回放（远程端：收到状态变化 → 切换对应状态播动画）
    // ================================================================

    private void OnMoveStateChanged(MovementStateType previous, MovementStateType current)
    {
        if (IsOwner) return;   // 拥有者端状态本来就是本地切换的，不需要回放
        if (stateMachine == null) return;

        MarkPhaseOffsetPending();
        switch (current)
        {
            case MovementStateType.Idle: stateMachine.SwitchState(stateMachine.idlingState); break;
            case MovementStateType.Walking: stateMachine.SwitchState(stateMachine.walkingState); break;
            case MovementStateType.WalkStop: stateMachine.SwitchState(stateMachine.walkStopState); break;
            case MovementStateType.Dashing: stateMachine.SwitchState(stateMachine.dashingState); break;
            case MovementStateType.Dodge: stateMachine.SwitchState(stateMachine.dodgeState); break;
            case MovementStateType.Hurt: stateMachine.SwitchState(stateMachine.hurtState); break;
            case MovementStateType.Null: stateMachine.SwitchState(stateMachine.playerMovementNullState); break;
        }
        ClearPhaseOffsetPending();
    }

    private void OnComboStateChanged(ComboStateType previous, ComboStateType current)
    {
        if (IsOwner) return;
        if (comboStateMachine == null) return;

        MarkPhaseOffsetPending();
        switch (current)
        {
            case ComboStateType.Null: comboStateMachine.SwitchState(comboStateMachine.NullState); break;
            case ComboStateType.Attacking: comboStateMachine.SwitchState(comboStateMachine.ATKIngState); break;
            case ComboStateType.Skill: comboStateMachine.SwitchState(comboStateMachine.SkillState); break;
        }
        ClearPhaseOffsetPending();
    }

    private void OnComboIndexChanged(int previous, int current)
    {
        if (IsOwner) return;
        if (comboStateMachine == null) return;

        comboStateMachine.ReusableData.currentIndex.Value = current;

        // 段数前进 = 拥有者切到下一段：重进攻击状态播放新段动画（当前正处于攻击段且非收尾时）
        if (current > previous && comboStateMachine.CurrentState is PlayerATKIngState { IsRecovery: false })
        {
            MarkPhaseOffsetPending();
            comboStateMachine.SwitchState(comboStateMachine.ATKIngState);
            ClearPhaseOffsetPending();
        }
    }

    private void OnForwardATKChanged(bool previous, bool current)
    {
        if (IsOwner) return;

        // 把本端轻击容器首段切换/还原为前进攻击段（与拥有者同一份配置资源，各自本地修改）
        var lightCombo = playerSO?.comboData?.comboData?.lightCombo;
        if (lightCombo == null) return;
        if (current) lightCombo.SwitchForwardATK();
        else lightCombo.ResetComboDates();
    }

    // ================================================================
    // 远程端动画相位对齐（状态变化 → 按已播放时长定位起点，而非从头重放）
    // ================================================================

    /// <summary>相位缓冲：防网络抖动导致远程端超前于拥有者，预留一点"慢半拍"的余量</summary>
    private const float RemotePhaseBuffer = 0.05f;

    /// <summary>本次网络状态切换是否带相位偏移（进入状态播放第一个动画时消费）</summary>
    private bool phaseOffsetPending;

    /// <summary>状态回调触发时标记：下一次播放动画按时间戳偏移起播</summary>
    private void MarkPhaseOffsetPending()
    {
        phaseOffsetPending = IsRemote;
    }

    private void ClearPhaseOffsetPending()
    {
        phaseOffsetPending = false;
    }

    /// <summary>起播时按时间戳对齐：已播放时长 - 缓冲，钳制到片段长度内；消费后自动清除</summary>
    public void ApplyRemotePhaseOffset(Animancer.AnimancerState state)
    {
        if (!phaseOffsetPending || state == null) return;
        phaseOffsetPending = false;

        float elapsed = Mathf.Max(0f, (float)NetworkManager.ServerTime.Time - netStateStartTime.Value);
        if (elapsed <= RemotePhaseBuffer) return;
        state.Time = Mathf.Min(elapsed - RemotePhaseBuffer, Mathf.Max(0f, state.Length));
    }

    /// <summary>
    /// 受击入口：打断当前动作进入受击硬直。
    /// 顺序敏感：先切连击→空状态（复位连招 + 清除攻击动画残留回调），
    /// 再切移动→受击（覆盖空状态 Enter 恢复的移动，硬直期间锁定输入）。
    /// </summary>
    public virtual void TakeHit()
    {
        if (comboStateMachine == null || stateMachine == null) return;

        comboStateMachine.SwitchState(comboStateMachine.NullState);
        stateMachine.SwitchState(stateMachine.hurtState);
    }

    // ================================================================
    // 闪避（右键：开无敌窗口，窗口内受伤才触发闪避）
    // ================================================================

    private float dodgeCooldownRemain;

    /// <summary>右键开启的无敌窗口时长（秒，读 PlayerSO 配置，未配置用默认值）</summary>
    private float DodgeInvincibleWindow => PlayerSO?.movementData?.dodgeData?.invincibleWindow ?? 0.3f;

    /// <summary>闪避冷却时长（秒，读 PlayerSO 配置，未配置用默认值）</summary>
    private float DodgeCooldown => PlayerSO?.movementData?.dodgeData?.cooldown ?? 1.5f;

    /// <summary>
    /// 右键闪避：按下右键不切状态，只开启一段无敌窗口（联网时在服务器开启，带服务器冷却限流）；
    /// 窗口内受到伤害（E_DamageBlocked）才触发闪避动画。
    /// 限制：冷却已好 + 非受击/非闪避中 + 存活。
    /// </summary>
    private void HandleDodgeInput()
    {
        if (dodgeCooldownRemain > 0f)
            dodgeCooldownRemain -= Time.deltaTime;

        if (!CharacterInputSystem.MainInstance.HeavyAttack) return;
        if (dodgeCooldownRemain > 0f) return;
        if (stateMachine == null) return;
        if (stateMachine.CurrentState == stateMachine.hurtState) return;
        if (stateMachine.CurrentState == stateMachine.dodgeState) return;

        var health = GetComponent<HealthModel>();
        if (health == null || !health.IsAlive) return;

        health.RequestInvincible(DodgeInvincibleWindow, DodgeCooldown);   // 无敌窗口在服务器开（服务器另有冷却限流）
        dodgeCooldownRemain = DodgeCooldown;   // 本地冷却只做按键手感反馈
    }

    /// <summary>无敌窗口内被打中：伤害已拦下，切入闪避状态（DoT 无视无敌直接扣血，不会走到这里）</summary>
    private void OnDamageBlocked(DamageData data)
    {
        if (IsSpawned && !IsOwner) return;   // 非拥有者端不参与本地状态切换
        if (data.target != gameObject) return;
        EnterDodgeState();
    }

    /// <summary>
    /// 闪避入口：打断当前动作进入闪避。
    /// 顺序敏感：先切连击→空状态（复位连招 + 清除攻击动画残留回调），
    /// 再切移动→闪避（覆盖空状态 Enter 恢复的移动，闪避期间锁定输入 + 无敌）。
    /// </summary>
    private void EnterDodgeState()
    {
        if (stateMachine == null || comboStateMachine == null) return;
        if (stateMachine.CurrentState == stateMachine.hurtState) return;
        if (stateMachine.CurrentState == stateMachine.dodgeState) return;

        comboStateMachine.SwitchState(comboStateMachine.NullState);
        stateMachine.SwitchState(stateMachine.dodgeState);
    }

    // ================================================================
    // 动画播放（统一入口，方便以后改Transition、加Fade）
    // ================================================================

    public void PlayAnimation(AnimationClip clip)
    {
        // 角色已销毁（切场景/退出战斗/反序列化）时 Animancer 组件已释放，Unity 的 == 判空能捕获，直接跳过
        if (characterAnimancer == null) return;
        ApplyRemotePhaseOffset(characterAnimancer.Play(clip));
    }

    public void PlayAnimation(AnimationClip clip, float fadeDuration)
    {
        if (characterAnimancer == null) return;
        ApplyRemotePhaseOffset(characterAnimancer.Play(clip, fadeDuration));
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

    // ================================================================
    // 打击感辅助（命中顿帧）
    // ================================================================

    private CancellationTokenSource hitStopCts;

    /// <summary>
    /// 命中顿帧：把 timeScale 压到接近 0 制造打击停顿感，realTime 后恢复。
    /// 用实时等待（ignoreTimeScale），顿帧期间动画/Update 全部停住也不会影响恢复计时。
    /// 注意：后续若接入游戏暂停/慢动作，这里恢复成 1f 需要改为恢复"暂停前的 timeScale"。
    /// </summary>
    public void HitStop(float realSeconds)
    {
        // 新顿帧接管：取消旧顿帧且不恢复 timeScale（保持 0.03），由新顿帧负责恢复
        if (hitStopCts != null) { hitStopCts.Cancel(); hitStopCts.Dispose(); }
        hitStopCts = new CancellationTokenSource();
        Time.timeScale = 0.03f;
        _ = HitStopAsync(realSeconds, hitStopCts.Token);
    }

    private async UniTaskVoid HitStopAsync(float realSeconds, CancellationToken token)
    {
        await UniTask.WaitForSeconds(realSeconds, ignoreTimeScale: true, cancellationToken: token);
        Time.timeScale = 1f;
    }
}

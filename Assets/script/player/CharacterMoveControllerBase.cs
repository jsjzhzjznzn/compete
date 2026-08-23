using Animancer;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 角色移动控制器基类（Animancer版）
/// 负责：根运动位移、重力、地面检测、斜坡修正
/// 子类负责：动画播放控制、状态切换逻辑
/// </summary>
public class CharacterMoveControllerBase : NetworkBehaviour
{
    // ==================== 组件引用 ====================

    [Header("动画与控制器")]
    public AnimancerComponent characterAnimancer { get; private set; }   // Animancer动画播放组件
    protected CharacterController characterController;                    // Unity物理移动组件

    // ==================== 重力系统 ====================

    [Header("重力设置")]
    [SerializeField] private float characterGravity = -9f;               // 重力加速度（负值=下落）
    [SerializeField] protected float maxVerticalSpeed = 20f;             // 最大上升速度
    [SerializeField] protected float minVerticalSpeed = -30f;            // 最大下落速度
    [SerializeField] protected float verticalSpeed;                      // 当前竖直速度（运行时变化）
    protected Vector3 verticalVelocity;                                   // 竖直速度向量，每帧传给CharacterController

    // 离地缓冲：离开地面后延迟 fallOutTimer 秒再施加重力
    // 解决走下台阶时的抖动问题（短暂离地被当成下落）
    protected float fallOutdeltaTimer;                                    // 离地缓冲计时器
    [SerializeField] protected float fallOutTimer = 0.2f;               // 缓冲时长

    // ==================== 地面检测 ====================

    [Header("地面球形检测")]
    [SerializeField] private float GroundDetectionRadius;                // 检测球半径
    [SerializeField] private float GroundDetectionOffset;                // 检测原点向下偏移量
    [SerializeField] private LayerMask whatIsGround;                     // 地面层
    [SerializeField] protected bool isOnGround;                          // 是否在地面（运行时更新）
    private Vector3 groundDetectionOrigin;                                // 检测球世界坐标

    // ==================== 斜坡处理 ====================

    [Header("斜坡射线检测")]
    [SerializeField] private float SlopDetectionLenth = 1f;             // 斜坡检测射线长度

    // ==================== 速度倍率 ====================

    [Header("移动速度倍率（根运动缩放）")]
    [Range(0.2f, 100), SerializeField] private float moveMult = 1f;     // 根运动位移缩放系数

    // ================================================================
    // 生命周期
    // ================================================================

    protected virtual void Awake()
    {
        characterAnimancer = GetComponent<AnimancerComponent>();
        characterController = GetComponent<CharacterController>();
    }

    protected virtual void Start()
    {
        // 初始化缓冲计时器
        fallOutdeltaTimer = fallOutTimer;
    }

    protected virtual void Update()
    {
        GroundDetection();           // 每帧检测是否着地
        UpdateCharacterGravity();    // 更新竖直速度（重力/贴地）
        UpdateVerticalVelocity();    // 应用竖直方向位移
    }

    // ================================================================
    // 根运动
    // ================================================================

    /// <summary>
    /// Animator回调：每帧动画更新后调用，将动画根运动位移应用到CharacterController
    /// AnimancerComponent底层通过Unity Animator产生Root Motion，所以OnAnimatorMove仍然有效
    /// 注意：只应用位移，不应用旋转（旋转由状态机 RotateTowardsInput 手动控制）
    /// </summary>
    protected virtual void OnAnimatorMove()
    {
        UpdateCharacterVelocity(characterAnimancer.Animator.deltaPosition);   // 取出本帧位移量，传给CharacterController
    }

    // ================================================================
    // 地面检测
    // ================================================================

    /// <summary>
    /// 球形检测脚下是否有地面，结果写入 isOnGround
    /// </summary>
    protected void GroundDetection()
    {
        groundDetectionOrigin = new Vector3(
            transform.position.x,
            transform.position.y - GroundDetectionOffset,         // 从角色脚底向下偏移
            transform.position.z
        );
        isOnGround = Physics.CheckSphere(groundDetectionOrigin, GroundDetectionRadius, whatIsGround, QueryTriggerInteraction.Ignore);
    }
    
    // ================================================================
    // 重力系统
    // ================================================================

    /// <summary>
    /// 更新竖直速度：
    /// - 着地时：重置缓冲，保持-2的微小向下速度（防止CharacterController弹跳）
    /// - 离地时：先走缓冲计时器，再施加重力，限制最大下落速度
    /// </summary>
    protected void UpdateCharacterGravity()
    {
        if (isOnGround)
        {
            fallOutdeltaTimer = fallOutTimer;                    // 着地重置缓冲
            verticalSpeed = -2f;                                  // 微小向下贴地，避免弹跳
        }
        else
        {
            if (fallOutdeltaTimer > 0)
            {
                fallOutdeltaTimer -= Time.deltaTime;             // 缓冲期内不施加重力
            }
            else
            {
                // 重力加速，并限制最小速度（防止无限下落）
                verticalSpeed = Mathf.Max(verticalSpeed + characterGravity * Time.deltaTime, minVerticalSpeed);
            }
        }
    }

    /// <summary>
    /// 将竖直速度用CharacterController.Move应用到角色
    /// </summary>
    protected void UpdateVerticalVelocity()
    {
        verticalVelocity.Set(0, verticalSpeed, 0);               // 只改Y轴
        characterController.Move(verticalVelocity * Time.deltaTime);
    }

    // ================================================================
    // 斜坡处理
    // ================================================================

    /// <summary>
    /// 斜坡速度修正：将水平位移投影到地面法线平面
    /// 防止上坡时角色被"吞进"地面、下坡时打滑
    /// </summary>
    /// <param name="characterVelocity">原始根运动位移</param>
    /// <returns>修正后的位移向量</returns>
    protected Vector3 ResetVelocityOnSlop(Vector3 characterVelocity)
    {
        // 从角色脚底稍上方向下打射线
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit groundHit, SlopDetectionLenth, whatIsGround))
        {
            // 地面法线不能太陡（如垂直墙壁），且角色不能在上浮时才修正
            if (Vector3.Dot(Vector3.up, groundHit.normal) > 0 && verticalSpeed <= 0)
            {
                // 将位移投影到地面法线平面上，沿地面方向移动
                return Vector3.ProjectOnPlane(characterVelocity, groundHit.normal);
            }
        }
        return characterVelocity;
    }

    // ================================================================
    // 位移应用
    // ================================================================

    /// <summary>
    /// 应用根运动位移：
    /// 1. 先经斜坡修正
    /// 2. 乘以倍率
    /// 3. 通过CharacterController.Move位移
    /// 注意：movement 是 Animator.deltaPosition，本身已是"本帧位移"，不能再乘 Time.deltaTime
    /// 子类可重写以区分不同移动状态（行走/闪避/攻击位移等）
    /// </summary>
    protected virtual void UpdateCharacterVelocity(Vector3 movement)
    {
        Vector3 dir = ResetVelocityOnSlop(movement);
        characterController.Move(dir * moveMult);
    }

    // ================================================================
    // 编辑器辅助
    // ================================================================

    /// <summary>
    /// Scene窗口绘制地面检测球：绿色=着地，红色=悬空
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = isOnGround ? Color.green : Color.red;
        Vector3 origin = new Vector3(transform.position.x, transform.position.y - GroundDetectionOffset, transform.position.z);
        Gizmos.DrawWireSphere(origin, GroundDetectionRadius);
    }
}

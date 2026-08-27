using Cinemachine;
using UnityEngine;

/// <summary>
/// 玩家水平回正辅助类（相机转向角度同步到 Player 的 Inspector 配置），
/// 封装 CinemachinePOV 的 m_HorizontalRecentering 功能，
/// 让角色朝后或静止时能自动转回角色正前方的速度。
/// 在 PlayerMovementState 里移动状态切换时调用。
/// </summary>
[System.Serializable]
public class PlayerCameraUtility
{
    /// <summary>
    /// 关联的 Cinemachine 相机（Inspector 拖入配置）。
    /// </summary>
    [field: SerializeField] public CinemachineVirtualCamera virtualCamera { get; private set; }

    /// <summary>
    /// 默认回正等待时间（秒）：停止操作后等稳定后才开始回正。
    /// </summary>
    [field: SerializeField] public float DefaultHorizontalWaitTime { get; private set; } = 0f;

    /// <summary>
    /// 默认回正时间（秒）：从当前角度转回正前方用时。
    /// </summary>
    [field: SerializeField] public float DefaultHorizontalRecenteringTime { get; private set; } = 0.5f;

    /// <summary>
    /// 缓存的 CinemachinePOV 组件引用（Virtual Camera 的 Aim 组件里）。
    /// </summary>
    private CinemachinePOV cinemachinePOV;

    /// <summary>
    /// 初始化：缓存获取 CinemachinePOV 组件引用。
    /// 在 Player.Awake() 中调用。未配置 virtualCamera 时自动查找场景中的虚拟相机。
    /// </summary>
    public void Init()
    {
        if (virtualCamera == null)
            virtualCamera = Object.FindFirstObjectByType<CinemachineVirtualCamera>();
        cinemachinePOV = virtualCamera != null
            ? virtualCamera.GetCinemachineComponent<CinemachinePOV>()
            : null;
    }

    /// <summary>
    /// 开启水平回正：角色朝后或静止时，能自动转回角色正前方。
    /// 在 PlayerMovementState 里玩家按 A/D/S 移动时调用。
    /// </summary>
    /// <param name="waitTime">回正等待时间，为 -1 使用默认值</param>
    /// <param name="recenteringTime">回正时间，为 -1 使用默认值</param>
    public void EnableRecentering(float waitTime = -1f, float recenteringTime = -1f)
    {
        if (cinemachinePOV == null) return;
        cinemachinePOV.m_HorizontalRecentering.m_enabled = true;

        if (waitTime != -1f)
            cinemachinePOV.m_HorizontalRecentering.m_WaitTime = waitTime;
        if (recenteringTime != -1f)
            cinemachinePOV.m_HorizontalRecentering.m_RecenteringTime = recenteringTime;
    }

    /// <summary>
    /// 关闭水平回正：玩家按 W 前进或停止移动时调用。
    /// </summary>
    public void DisableRecentering()
    {
        if (cinemachinePOV == null) return;
        cinemachinePOV.m_HorizontalRecentering.m_enabled = false;
    }
}

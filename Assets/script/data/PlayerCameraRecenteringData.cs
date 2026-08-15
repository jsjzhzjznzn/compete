using UnityEngine;

/// <summary>
/// 相机回正配置：按相机俯仰角的区间匹配要使用的回正等待/回正时间。
/// 参考 ZZZ 项目：侧移(A/D)与后退(S)各自一组配置。
/// </summary>
[System.Serializable]
public class PlayerCameraRecenteringData
{
    [field: SerializeField, Range(0, 360)] public float minAngleRange { get; private set; }
    [field: SerializeField, Range(0, 360)] public float maxAngleRange { get; private set; }
    [field: SerializeField, Range(-1, 10)] public float waitingTime { get; private set; }
    [field: SerializeField, Range(-1, 10)] public float recenteringTime { get; private set; }

    public bool IsWithInAngle(float angle)
    {
        return angle > minAngleRange && angle < maxAngleRange;
    }
}

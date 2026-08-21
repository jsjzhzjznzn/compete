using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 特效配置资产（ScriptableObject）
/// 一份配置 = 某角色的一套特效资源。一个角色一份 VFXItemData，
/// 内含多个特效条目（VFXItem），每个条目 = 特效名 + 预制体 + 池数量 + 挂点 + 旋转。
/// 通过菜单 Create/Asset/VFX/VFXItemData 创建配置资源。
/// </summary>
[CreateAssetMenu(fileName = "VFXItemData", menuName = "Asset/VFX/VFXItemData")]
public class VFXItemData : ScriptableObject
{
    /// <summary>单个特效条目配置</summary>
    [System.Serializable]
    public class VFXItem
    {
        [Header("特效标识名（取出时用这个名字匹配）")]
        public string VFXName;

        [Header("特效预制体（带 ParticleSystem 和 EffectItem 脚本）")]
        public GameObject VFXPrefab;

        [Header("池中预生成的数量")]
        public int count = 3;

        [Header("是否挂到指定父物体（如武器骨骼）")]
        public bool applyParentPos;

        [Header("挂点：applyParentPos 为 true 时生效")]
        public Transform parentPos;

        [Header("本地旋转欧拉角（运行时转成 Quaternion）")]
        public Vector3 effectEulerAngle;

        /// <summary>运行时缓存：由 InitEffectPools 从 effectEulerAngle 转换，避免每次取用都转一次</summary>
        [HideInInspector] public Quaternion effectRotation;
    }

    /// <summary>该角色下的全部特效条目</summary>
    [SerializeField] public List<VFXItem> effectItems = new List<VFXItem>();
}

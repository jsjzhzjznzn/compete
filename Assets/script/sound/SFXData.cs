using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用特效/音效资源包（ScriptableObject 资源）
/// 存放不区分角色、不分类别的音效片段（如 UI 音效、环境音等），供音效池等系统直接取用。
/// 通过菜单 Asset/SFX/SFXData 创建一份配置资源。
/// </summary>
[CreateAssetMenu(fileName = "SFXData", menuName = "Asset/SFX/SFXData")]
public class SFXData : ScriptableObject
{
    /// <summary>通用音效片段列表</summary>
    [SerializeField] public List<AudioClip> SFXList = new List<AudioClip>();
}
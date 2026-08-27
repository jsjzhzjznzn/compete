using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 安比选择按钮：普通 MonoBehaviour，不依赖 NetworkObject（UI 隐藏/销毁不影响）。
/// 点击后通过 CustomMessagingManager 发命名消息给服务端，服务端生成安比并分配给点击的客户端。
/// </summary>
public class Selectperson : MonoBehaviour
{
    private const string SpawnRequestName = "RequestSpawnAnBi";

    [SerializeField] private GameObject anbiPrefab; //安比预制体，已注册进 NetworkConfig 预制体列表
    public Button anbiButton; //安比按钮

    private void Awake()
    {
        if (anbiButton == null) anbiButton = GetComponent<Button>();
        if (anbiButton != null)
        {
            anbiButton.onClick.RemoveAllListeners();
            anbiButton.onClick.AddListener(OnClickSelectAnBi);
        }
    }

    //客户端UI按钮调用
    public void OnClickSelectAnBi()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            Debug.LogWarning("[Selectperson] 还没连接网络，无法选择角色");
            return;
        }

        //发命名消息给服务端（同步入队，隐藏UI不影响发送）
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            SpawnRequestName, NetworkManager.ServerClientId, new FastBufferWriter(0, Allocator.Temp));

        //隐藏选择UI而不是销毁，方便调试
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvas.gameObject.SetActive(false);
        else gameObject.SetActive(false);
    }

    /// <summary>服务端注册生成请求处理（NetworkPlayerSpawner 在服务端启动后调用），生成逻辑仍在本类</summary>
    public static void RegisterServerHandler(GameObject prefab)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.CustomMessagingManager == null)
        {
            Debug.LogWarning("[Selectperson] 网络会话尚未启动，跳过生成请求注册");
            return;
        }
        nm.CustomMessagingManager.RegisterNamedMessageHandler(
            SpawnRequestName, (senderClientId, _) => SpawnRole(prefab, senderClientId));
    }

    private static void SpawnRole(GameObject prefab, ulong clientId)
    {
        if (prefab == null)
        {
            Debug.LogError("[Selectperson] anbiPrefab 为空，无法生成");
            return;
        }

        //服务端实例化，同一个预制体可以多次实例，多人选安比没问题
        GameObject roleObj = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        NetworkObject netObj = roleObj.GetComponent<NetworkObject>();

        //分配给请求的客户端作为玩家对象
        netObj.SpawnAsPlayerObject(clientId, true);
    }
}

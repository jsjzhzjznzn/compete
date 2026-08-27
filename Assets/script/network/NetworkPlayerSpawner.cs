using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 挂在 NetworkManager 物体上（不会被销毁）：
/// 服务端会话启动后（CustomMessagingManager 可用时）注册所有角色生成请求处理。
/// </summary>
public class NetworkPlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject anbiPrefab; //安比预制体，已注册进 NetworkConfig 预制体列表
    [SerializeField] private GameObject ellenPrefab; //艾莲预制体，已注册进 NetworkConfig 预制体列表

    private const string RequestSpawnAnBi = "RequestSpawnAnBi";
    private const string RequestSpawnEllen = "RequestSpawnEllen";

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }
        //Start 时网络会话还没启动，CustomMessagingManager 不可用，等 OnServerStarted 再注册
        NetworkManager.Singleton.OnServerStarted += RegisterHandlers;
        if (NetworkManager.Singleton.IsServer)
        {
            RegisterHandlers();
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= RegisterHandlers;
        }
    }

    private void RegisterHandlers()
    {
        Selectperson.RegisterServerHandler(RequestSpawnAnBi, anbiPrefab);
        Selectperson.RegisterServerHandler(RequestSpawnEllen, ellenPrefab);
    }
}

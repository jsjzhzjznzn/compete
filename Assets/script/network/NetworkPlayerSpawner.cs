using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 挂在 NetworkManager 物体上（不会被销毁）：
/// 服务端会话启动后（CustomMessagingManager 可用时）注册安比生成请求处理。
/// </summary>
public class NetworkPlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject anbiPrefab; //安比预制体，与 Selectperson 引用同一个

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }
        //Start 时网络会话还没启动，CustomMessagingManager 不可用，等 OnServerStarted 再注册
        NetworkManager.Singleton.OnServerStarted += RegisterHandler;
        if (NetworkManager.Singleton.IsServer)
        {
            RegisterHandler();
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= RegisterHandler;
        }
    }

    private void RegisterHandler()
    {
        Selectperson.RegisterServerHandler(anbiPrefab);
    }
}

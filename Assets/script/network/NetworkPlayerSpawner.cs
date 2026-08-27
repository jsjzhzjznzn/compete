using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerSpawner : MonoBehaviour
{
    private void Start()
    {
        // 放到Start，NetworkManager.Awake已经跑完，Singleton一定初始化完成
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }
        // 列表顺序约定: [0] 给 Host 自己, [1] 给连接的客户端
        int prefabIndex = clientId == NetworkManager.Singleton.LocalClientId ? 0 : 1;
        var prefabs = NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs;
        if (prefabIndex >= prefabs.Count)
        {
            return;
        }
        var prefab = prefabs[prefabIndex].Prefab;
        var spawnPosition = NetworkManager.Singleton.transform.position;
        var instance = Instantiate(prefab, spawnPosition, Quaternion.identity);
        instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }
}

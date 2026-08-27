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
        var instance = Instantiate(prefab, GetSpawnPosition(clientId), Quaternion.identity);
        instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }

    /// <summary>
    /// 生成点：Plane 平台中心上方（y+1 让角色站在表面）。
    /// 按 clientId 在 X 向错开间距，避免 Host 与客户端角色出生时叠在一起。
    /// 场景里没有名为 Plane 的对象时回退到 NetworkManager 位置。
    /// </summary>
    private Vector3 GetSpawnPosition(ulong clientId)
    {
        var plane = GameObject.Find("Plane");
        Vector3 basePos = plane != null
            ? plane.transform.position + Vector3.up
            : NetworkManager.Singleton.transform.position;
        return basePos + new Vector3(clientId * 1.5f, 0f, 0f);
    }
}

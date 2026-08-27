using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using Unity.Netcode;

public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private TextMeshProUGUI statusText;

    private void Start()
    {
        EnsureEventSystem();

        if (hostButton == null)
        {
            var go = GameObject.Find("HostButton");
            if (go != null) hostButton = go.GetComponent<Button>();
        }
        if (clientButton == null)
        {
            var go = GameObject.Find("ClientButton");
            if (go != null) clientButton = go.GetComponent<Button>();
        }
        if (statusText == null)
        {
            var go = GameObject.Find("StatusText");
            if (go != null) statusText = go.GetComponent<TextMeshProUGUI>();
        }

        if (hostButton != null) hostButton.onClick.AddListener(StartHost);
        if (clientButton != null) clientButton.onClick.AddListener(StartClient);

        SetStatus("点击 Host 或 Client 开始");
    }

    private void Update()
    {
        if (statusText == null || NetworkManager.Singleton == null)
        {
            return;
        }

        var nm = NetworkManager.Singleton;
        if (nm.IsHost)
        {
            statusText.text = "Host 运行中（已生成角色）";
        }
        else if (nm.IsServer)
        {
            statusText.text = "Server 运行中";
        }
        else if (nm.IsClient && nm.IsConnectedClient)
        {
            statusText.text = "Client 已连接（已生成角色）";
        }
        else if (nm.IsClient)
        {
            statusText.text = "Client 连接中...";
        }
    }

    private void StartHost()
    {
        if (NetworkManager.Singleton.StartHost())
        {
            SetStatus("Host 启动成功");
        }
        else
        {
            SetStatus("Host 启动失败");
        }
    }

    private void StartClient()
    {
        if (NetworkManager.Singleton.StartClient())
        {
            SetStatus("Client 连接中...");
        }
        else
        {
            SetStatus("Client 启动失败");
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}

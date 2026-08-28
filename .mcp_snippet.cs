var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/大厅.unity");
var es = UnityEngine.EventSystems.EventSystem.current;
if (es == null)
{
    var esGo = new GameObject("EventSystem");
    esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
    var inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
    if (inputModuleType != null)
    {
        esGo.AddComponent(inputModuleType);
    }
    else
    {
        esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }
}
var bootGo = GameObject.Find("UILobbyDemo");
if (bootGo == null) bootGo = new GameObject("UILobbyDemo");
if (bootGo.GetComponent<UILobbyDemoBootstrap>() == null) bootGo.AddComponent<UILobbyDemoBootstrap>();
var cam = Camera.main;
if (cam != null)
{
    var camData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
    if (camData == null) camData = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "done. scene=" + scene.name + " objects=" + UnityEngine.Object.FindObjectsOfType<Transform>(true).Length + " es=" + (UnityEngine.EventSystems.EventSystem.current != null);

using UnityEngine;

/// <summary>
/// 饿汉版单例 —— 类型首次被引用时立即创建实例。
/// 适合：初始化顺序敏感的管理器，不希望在首次访问时才创建。
///
/// 原理：
///   任何代码首次触及 T（如 T.Instance、typeof(T)）→ 静态构造函数触发 → 立即创建实例。
///   静态构造在 Instance getter 之前执行，保证首次访问时 instance 已就绪。
///
/// 使用方式：
///   1. 创建脚本继承此类
///   2. 在启动场景的某个 GameObject 上挂一个脚本，在 Awake 中引用所有饿汉单例，例如：
///      public class EagerInit : MonoBehaviour {
///          void Awake() { _ = AudioManager.Instance; _ = NetManager.Instance; }
///      }
///   3. 或直接在项目的任意位置自然使用 T.Instance（首次访问即创建）
/// </summary>
[DisallowMultipleComponent]
public class SingletonEagerMono<T> : MonoBehaviour where T : SingletonEagerMono<T>
{
    private static T instance;

    static SingletonEagerMono()
    {
        // 静态构造在类型首次被引用时自动触发，早于任何 Instance 访问
        GameObject obj = new GameObject(typeof(T).Name);
        instance = obj.AddComponent<T>();
        Object.DontDestroyOnLoad(obj);
    }

    public static T Instance
    {
        get
        {
            // 静态构造函数保证此处 instance 非 null
            if (instance == null)
                instance = FindAnyObjectByType<T>();
            return instance;
        }
    }

    protected virtual void Awake()
    {
        if (instance == null)
        {
            instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
}

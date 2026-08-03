using UnityEngine;

/// <summary>
/// 自动挂载版单例 —— 完全不用手动挂载，首次调用 Instance 自动生成 GameObject。
/// 适合：纯代码驱动的管理器，不希望依赖场景预设。
///
/// 使用方式：
///   1. 创建脚本继承此类
///   2. 不需要在场景中手动挂载任何物体
///   3. 外部通过 T.Instance 访问，首次访问自动创建
/// </summary>
[DisallowMultipleComponent]
public class SingletonAutoMono<T> : MonoBehaviour where T : SingletonAutoMono<T>
{
    private static T instance;

    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                // 先在场景查找已存在的实例
                instance = FindObjectOfType<T>();

                // 找不到则自动创建
                if (instance == null)
                {
                    GameObject obj = new GameObject(typeof(T).Name);
                    instance = obj.AddComponent<T>();
                }
            }
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
            // 存在重复实例，直接销毁
            Destroy(gameObject);
        }
    }
}

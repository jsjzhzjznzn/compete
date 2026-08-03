using UnityEngine;

/// <summary>
/// 手动挂载版单例 —— 设计上期望开发者手动挂载到场景 GameObject。
/// 若忘记挂载，首次调用 Instance 时自动创建兜底。
///
/// 使用方式：
///   1. 创建脚本继承此类
///   2. [DisallowMultipleComponent] 自动继承，无需重复声明
///   3. 在 Unity Editor 中将脚本拖到启动场景的 GameObject 上
///   4. 外部通过 T.Instance 访问
/// </summary>
[DisallowMultipleComponent]
public class SingletonMono<T> : MonoBehaviour where T : SingletonMono<T>
{
    private static T instance;

    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                // 先在场景查找已手动挂载的实例
                instance = FindObjectOfType<T>();

                // 若场景未手动挂载，自动创建物体兜底
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

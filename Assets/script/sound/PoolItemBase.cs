using UnityEngine;

/// <summary>
/// 对象池基础类
/// 挂到所有使用对象池的预制体上：激活时调用 Spawn（播放），禁用时调用 ReSycle（回收）。
/// 子类通过重写 Spawn / ReSycle 实现各自的激活与回收逻辑。
/// </summary>
public class PoolItemBase : MonoBehaviour
{
    /// <summary>物体被池取出并激活时调用</summary>
    private void OnEnable()
    {
        Spawn();
    }

    /// <summary>物体播放完被放回池中禁用时调用</summary>
    private void OnDisable()
    {
        ReSycle();
    }

    /// <summary>激活时的初始化逻辑（子类覆写）</summary>
    protected virtual void Spawn()
    {

    }

    /// <summary>回收时的清理逻辑（子类覆写）</summary>
    protected virtual void ReSycle()
    {

    }

}

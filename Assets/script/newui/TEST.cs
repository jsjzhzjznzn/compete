using UnityEngine;
using SkierFramework;

public class TEST : MonoBehaviour
{
    void Awake()
    {
        // UIManager 首次使用时自动完成 Initialize + InitUIConfig，无需手动调用
        UIManager.Instance.Open(UIType.Mainman);
    }
    
}

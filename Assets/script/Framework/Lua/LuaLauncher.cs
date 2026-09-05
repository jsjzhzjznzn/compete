using System;
using System.IO;
using UnityEngine;
using XLua;
using SkierFramework;

/// <summary>
/// Lua 虚拟机入口：全局唯一 LuaEnv，业务侧直接访问 LuaLauncher.LuaEnv。
/// 必须等 YooAssets 初始化完成之后再调用 StartLuaVM()，不要放在 Start 里。
/// </summary>
public class LuaLauncher : MonoBehaviour
{
    /// <summary>
    /// Lua 脚本根目录（相对 Assets），编辑器模式直接读该目录下的文件
    /// </summary>
    public const string LuaRoot = "Assets/LuaScripts";

    public static LuaEnv LuaEnv { get; private set; }

    private void Awake()
    {
        // 防止场景挂载多个 LuaLauncher
        if (LuaEnv != null)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 启动 Lua 虚拟机（外部在 YooAssets 初始化完成后调用）
    /// </summary>
    public void StartLuaVM()
    {
        if (LuaEnv != null)
        {
            Debug.LogWarning("Lua虚拟机已经启动，请勿重复调用");
            return;
        }

        LuaEnv = new LuaEnv();
        LuaEnv.AddLoader(CustomLoader);
        try
        {
            LuaEnv.DoString("require('Main')", "LuaMain");
            Debug.Log("Lua虚拟机启动成功");
        }
        catch (Exception ex)
        {
            Debug.LogError("Lua启动失败: " + ex);
        }
    }

    /// <summary>
    /// xLua 自定义加载器（require 回调）。
    /// 模块名统一用 '/' 分段，例如 require('Main')、require('UI/UILoginView')；
    /// 若误用 '.' 分隔会自动转成 '/'。
    /// 编辑器优先读本地磁盘；打包/真机环境走 YooAsset（经 ResourceManager）加载 TextAsset。
    /// 注意：Assets/LuaScripts 必须被 BundleCollector 收集进资源包，真机才加载得到。
    /// </summary>
    public byte[] CustomLoader(ref string filepath)
    {
        // .lua.txt 双后缀：Unity 按 TextAsset(.txt) 导入，require 时又能用模块名定位
        string assetPath = LuaRoot + "/" + filepath.Replace('.', '/') + ".lua.txt";

#if UNITY_EDITOR
        // 编辑器直接读磁盘：改 lua 即生效，不用打 Yoo 包
        string editorPath = Path.Combine(Application.dataPath, "LuaScripts", filepath.Replace('.', '/') + ".lua.txt");
        if (File.Exists(editorPath))
        {
            return File.ReadAllBytes(editorPath);
        }
#endif

        // 打包环境：ResourceManager 归一化 location 后走 YooAsset 同步加载。
        // "Assets/LuaScripts/UI/xxx.lua.txt" → "Assets/LuaScripts/UI/xxx.lua"，
        // 与 SupportExtensionless 注册的无后缀映射一致，命中同一资源
        return ResourceManager.Instance.ReadTextBytes(assetPath);
    }

    private void Update()
    {
        // xlua 增量 GC：每帧驱动一次，避免卡顿
        LuaEnv?.Tick();
    }

    private void OnDestroy()
    {
        if (LuaEnv != null)
        {
            LuaEnv.Dispose();
            LuaEnv = null;
        }
    }
}

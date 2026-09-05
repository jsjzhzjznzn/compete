using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkierFramework
{
    [System.Serializable]
    public class UIConfigJson
    {
        public string uiType;
        public string path;
        public bool isWindow;
        public string uiLayer;
        public bool isAutoNavigation;
        /// <summary>
        /// 纯 Lua 热更 UI：不生成 C# View 类、不进 UIType 枚举，运行期挂 LuaOnlyView 通用壳
        /// </summary>
        public bool isLuaUI;
        /// <summary>
        /// isLuaUI=true 时 Lua 模块的 require 名，留空默认 "UI/" + uiType
        /// </summary>
        public string luaModuleName;
    }

    // JsonUtility 不支持根级数组，需要包装一层
    [System.Serializable]
    public class UIConfigJsonList
    {
        public List<UIConfigJson> items;
    }

    public class UIConfig
    {
        public string path;
        /// <summary>
        /// UI 标识：C# UI 为 UIType 枚举名，纯 Lua UI 为任意不重复字符串（不要求出现在枚举里）
        /// </summary>
        public string uiType;
        public UILayer uiLayer;
        public Type viewType;
        public bool isWindow;
        public bool isLuaUI;
        public string luaModuleName;

        private const string UIConfigPath = "Assets/HOTS/UI/UIConfig";

        public static void GetAllConfigs(Action<List<UIConfig>> callback) 
        {
            ResourceManager.Instance.LoadAssetAsync<UnityEngine.TextAsset>(UIConfigPath, (textAsset) =>
            {
                if (textAsset != null)
                {
                    var list = new List<UIConfig>();
                    var wrapper = JsonUtility.FromJson<UIConfigJsonList>(textAsset.text);
                    if (wrapper == null || wrapper.items == null)
                    {
                        Debug.LogError("UIConfig.json 解析失败：" + UIConfigPath);
                        callback?.Invoke(list);
                        return;
                    }
                    var uiConfigs = wrapper.items;
                    foreach (var config in uiConfigs)
                    {
                        if (string.IsNullOrEmpty(config.uiType))
                        {
                            Debug.LogErrorFormat("UIConfig.json 中存在缺少 uiType 的条目：{0}", config.path);
                            continue;
                        }
                        if (!Enum.TryParse<UILayer>(config.uiLayer, out UILayer layer))
                        {
                            layer = UILayer.NormalLayer;
                            Debug.LogErrorFormat("UIConfig.json 中的：{0}  uiLayer解析异常 {1}", config.path, config.uiLayer);
                        }

                        // 纯 Lua 热更 UI：uiType 允许不在 UIType 枚举里，也不要求存在 C# View 类，统一挂 LuaOnlyView
                        if (config.isLuaUI)
                        {
                            list.Add(new UIConfig
                            {
                                path = config.path,
                                uiType = config.uiType,
                                uiLayer = layer,
                                viewType = typeof(LuaOnlyView),
                                isWindow = config.isWindow,
                                isLuaUI = true,
                                luaModuleName = string.IsNullOrEmpty(config.luaModuleName) ? "UI/" + config.uiType : config.luaModuleName,
                            });
                            continue;
                        }

                        // C# UI：uiType 必须是 UIType 枚举成员（新增 C# UI 需发包，与 Lua UI 无关）
                        if (!Enum.TryParse<UIType>(config.uiType, out _))
                        {
                            Debug.LogErrorFormat("UIConfig.json 中的：{0}  uiType解析异常 {1}（若是纯 Lua UI 请设置 isLuaUI=true）", config.path, config.uiType);
                        }
                        Type viewType = GetType(config.uiType);
                        if (viewType == null)
                        {
                            viewType = GetType($"{typeof(UIConfig).Namespace}.{config.uiType}");
                        }
                        list.Add(new UIConfig
                        {
                            path = config.path,
                            uiType = config.uiType,
                            uiLayer = layer,
                            viewType = viewType,
                            isWindow = config.isWindow
                        });
                    }
                    callback?.Invoke(list);
                }
                else
                {
                    Debug.LogError("未找到配置：" + UIConfigPath);
                    callback?.Invoke(new List<UIConfig>());
                }
            }, true);
        }

        public static Type GetType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (System.Reflection.Assembly assembly in assemblies)
            {
                type = Type.GetType(string.Format("{0}, {1}", typeName, assembly.FullName));
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
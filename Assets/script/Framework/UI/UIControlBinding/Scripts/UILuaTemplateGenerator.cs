#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SkierFramework
{
    /// <summary>
    /// 根据 UIControlData 绑定数据生成 Lua 视图模板（模块表 + 生命周期桩 + 控件事件桩）。
    /// 规则与运行时 UILuaView/LuaUICtrlRules 保持一致：
    ///   - 文件不存在：全量生成
    ///   - 文件已存在：只追加缺失的桩函数，绝不覆盖开发者已填写的逻辑
    /// 默认输出目录 Assets/LuaScripts/UI（require 名 = "UI/{类名}"，对应 LuaLauncher 加载器根目录）
    /// </summary>
    public static class UILuaTemplateGenerator
    {
        public const string LuaRoot = "Assets/LuaScripts";
        public const string DefaultLuaDir = LuaRoot + "/UI";

        private const string PrefKey = "UILuaTemplateDir";

        /// <summary>
        /// 从 UI 对象（场景实例 / Prefab 编辑态 / Prefab 资产）解析出资产上的 UIControlData 并生成
        /// </summary>
        public static string GenerateForInstance(GameObject uiGo, string luaDir = null)
        {
            if (uiGo == null)
                return null;

            UIControlData ctrlData = ResolvePrefabCtrlData(uiGo);
            if (ctrlData == null)
            {
                Debug.LogErrorFormat("[UILua生成] {0} 上没有 UIControlData，无法生成", uiGo.name);
                return null;
            }

            string uiName = uiGo.name;
            GameObject assetGo = PrefabUtility.GetCorrespondingObjectFromSource(uiGo) ?? uiGo;
            string assetPath = AssetDatabase.GetAssetPath(assetGo);
            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab"))
                uiName = Path.GetFileNameWithoutExtension(assetPath);

            return GenerateCore(ctrlData, uiName, luaDir ?? GetLuaDir());
        }

        /// <summary>
        /// 从 Prefab 资产生成（UICreateWindow 创建流程用）
        /// </summary>
        public static string GenerateForPrefab(GameObject uiPrefabAsset, string uiName, string luaDir = null)
        {
            if (uiPrefabAsset == null)
                return null;

            var ctrlData = uiPrefabAsset.GetComponent<UIControlData>();
            if (ctrlData == null)
            {
                Debug.LogErrorFormat("[UILua生成] {0} 上没有 UIControlData", uiPrefabAsset.name);
                return null;
            }
            if (string.IsNullOrEmpty(uiName))
                uiName = uiPrefabAsset.name;

            return GenerateCore(ctrlData, uiName, luaDir ?? GetLuaDir());
        }

        /// <summary>
        /// 把场景/Prefab编辑态对象解析到 Prefab 资产上的组件（保证以落盘数据为准）
        /// </summary>
        private static UIControlData ResolvePrefabCtrlData(GameObject uiGo)
        {
            GameObject assetGo = PrefabUtility.GetCorrespondingObjectFromSource(uiGo) ?? uiGo;
            string assetPath = AssetDatabase.GetAssetPath(assetGo);
            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab"))
            {
                var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefabRoot != null)
                    return prefabRoot.GetComponent<UIControlData>();
            }
            return uiGo.GetComponent<UIControlData>();
        }

        public static string GetLuaDir()
        {
            return PlayerPrefs.GetString(PrefKey, DefaultLuaDir);
        }

        public static void SetLuaDir(string luaDir)
        {
            PlayerPrefs.SetString(PrefKey, luaDir);
        }

        /// <summary>
        /// 由 lua 目录推导 require 前缀：Assets/LuaScripts/UI -> "UI/"，根目录 -> ""；不在 LuaRoot 下返回 null
        /// </summary>
        public static string GetRequirePrefix(string luaDir)
        {
            string dir = (luaDir ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
            if (dir.Equals(LuaRoot, StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            if (dir.StartsWith(LuaRoot + "/", StringComparison.OrdinalIgnoreCase))
                return dir.Substring(LuaRoot.Length + 1) + "/";
            return null;
        }

        private static string GenerateCore(UIControlData ctrlData, string uiName, string luaDir)
        {
            luaDir = (luaDir ?? string.Empty).Trim().Replace('\\', '/');
            if (string.IsNullOrEmpty(luaDir))
                luaDir = DefaultLuaDir;

            string requirePrefix = GetRequirePrefix(luaDir);
            if (requirePrefix == null)
            {
                Debug.LogErrorFormat("[UILua生成] Lua 目录必须在 {0} 之下，当前: {1}，已回退默认目录", LuaRoot, luaDir);
                luaDir = DefaultLuaDir;
                requirePrefix = "UI/";
            }

            string moduleName = uiName;
            string relPath = luaDir + "/" + moduleName + ".lua.txt";
            string content = BuildTemplate(ctrlData, moduleName, requirePrefix + moduleName);
            if (content == null)
                return null;

            try
            {
                if (File.Exists(relPath))
                {
                    string merged = AppendMissingStubs(File.ReadAllText(relPath), moduleName, content);
                    if (merged == null)
                    {
                        Debug.Log("[UILua生成] 无新增桩函数，已跳过: " + relPath);
                        return null;
                    }
                    File.WriteAllText(relPath, merged);
                    Debug.Log("[UILua生成] 已追加缺失桩: " + relPath);
                }
                else
                {
                    Directory.CreateDirectory(luaDir);
                    File.WriteAllText(relPath, content);
                    AssetDatabase.ImportAsset(relPath);
                    Debug.Log("[UILua生成] 已生成: " + relPath);
                }
                AssetDatabase.SaveAssets();
                return relPath;
            }
            catch (Exception e)
            {
                Debug.LogError("[UILua生成] 写入失败: " + relPath + "\n" + e);
                return null;
            }
        }

        /// <summary>
        /// 用旧文件 + 新模板比对，返回追加了缺失桩的内容；没有缺失返回 null
        /// </summary>
        private static string AppendMissingStubs(string oldContent, string moduleName, string newContent)
        {
            List<string> missing = new List<string>();
            foreach (var pair in ExtractFuncBlocks(newContent))
            {
                string marker = "function " + moduleName + ":" + pair.Key + "(";
                if (oldContent.IndexOf(marker, StringComparison.Ordinal) < 0)
                    missing.Add(pair.Value);
            }
            if (missing.Count == 0)
                return null;

            StringBuilder sb = new StringBuilder(oldContent);
            if (!oldContent.EndsWith("\n"))
                sb.Append('\n');

            string tail = "\n-- ===== 自动追加的缺失桩（由 UIControlData 生成，勿改函数名）=====\n"
                        + string.Join("\n", missing) + "\n";

            int retIdx = oldContent.LastIndexOf("\nreturn " + moduleName, StringComparison.Ordinal);
            if (retIdx >= 0)
                sb.Insert(retIdx + 1, tail.Substring(1));
            else
                sb.Append(tail).Append("return ").Append(moduleName).Append('\n');

            return sb.ToString();
        }

        /// <summary>
        /// 从新模板内容里解析所有桩函数：函数名 -> 完整块文本
        /// </summary>
        private static Dictionary<string, string> ExtractFuncBlocks(string content)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            string currentName = null;
            StringBuilder block = new StringBuilder();

            void Flush()
            {
                if (currentName != null)
                {
                    map[currentName] = block.ToString().TrimEnd('\r', '\n');
                    currentName = null;
                    block.Clear();
                }
            }

            foreach (string rawLine in content.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                int funcIdx = line.IndexOf("function ", StringComparison.Ordinal);
                if (funcIdx >= 0)
                {
                    Flush();
                    string after = line.Substring(funcIdx + "function ".Length);
                    int colonIdx = after.IndexOf(':');
                    int parenIdx = after.IndexOf('(');
                    if (colonIdx > 0 && parenIdx > colonIdx)
                    {
                        currentName = after.Substring(colonIdx + 1, parenIdx - colonIdx - 1).Trim();
                        block.Append(rawLine).Append('\n');
                        continue;
                    }
                }

                if (currentName != null)
                {
                    block.Append(rawLine).Append('\n');
                    if (line.Trim() == "end")
                        Flush();
                }
            }
            Flush();
            return map;
        }

        /// <summary>
        /// 生成完整模板文本（header + 生命周期桩 + 控件事件桩 + return）
        /// </summary>
        private static string BuildTemplate(UIControlData ctrlData, string moduleName, string requireName)
        {
            StringBuilder sb = new StringBuilder(1024);
            sb.Append("--[[\r\n");
            sb.Append("  自动生成：由 UIControlData 绑定数据生成，桩函数名请勿手改\r\n");
            sb.Append("  require 路径: ").Append(requireName).Append("（C# 侧 UILuaView 按此加载）\r\n");
            sb.Append("  重复生成只追加缺失桩，不会覆盖本文件已填写的业务逻辑\r\n");
            sb.Append("  控件清单（运行时会按绑定名注入到本模块表）:\r\n");

            if (ctrlData.ctrlItemDatas != null)
            {
                foreach (var ctrl in ctrlData.ctrlItemDatas)
                {
                    if (ctrl == null || string.IsNullOrEmpty(ctrl.name))
                        continue;

                    bool isArray = ctrl.targets != null && ctrl.targets.Length > 1;
                    string typeName = GetCtrlTypeName(ctrl);
                    sb.Append("    ").Append(ctrl.name);
                    if (isArray)
                        sb.Append(" (").Append(typeName).Append("[] 数组控件无事件桩)\r\n");
                    else
                        sb.Append(" (").Append(typeName).Append(")\r\n");
                }
            }

            if (ctrlData.subUIItemDatas != null && ctrlData.subUIItemDatas.Count > 0)
            {
                sb.Append("  SubUI:\r\n");
                foreach (var subUI in ctrlData.subUIItemDatas)
                {
                    if (subUI != null && !string.IsNullOrEmpty(subUI.name))
                        sb.Append("    ").Append(subUI.name).Append(" (UIControlData)\r\n");
                }
            }
            sb.Append("]]\r\n");

            sb.Append("local ").Append(moduleName).Append(" = {}\r\n\r\n");

            sb.Append("-- ===== 生命周期桩（C# 在对应时机自动调用，函数未定义则跳过）=====\r\n");
            AppendStub(sb, moduleName, "OnInit", "");
            AppendStub(sb, moduleName, "OnOpen", "userData");
            AppendStub(sb, moduleName, "OnAddListener", "");
            AppendStub(sb, moduleName, "OnRemoveListener", "");
            AppendStub(sb, moduleName, "OnClose", "");

            sb.Append("-- ===== 控件事件桩（由 UIControlData 绑定数据生成，请勿改名）=====\r\n");
            bool anyStub = false;
            if (ctrlData.ctrlItemDatas != null)
            {
                foreach (var ctrl in ctrlData.ctrlItemDatas)
                {
                    if (ctrl == null || string.IsNullOrEmpty(ctrl.name))
                        continue;
                    if (ctrl.targets == null || ctrl.targets.Length != 1 || ctrl.targets[0] == null)
                        continue;

                    LuaCtrlEventKind kind = LuaUICtrlRules.GetEventKind(ctrl.targets[0] as Component);
                    string stubName = LuaUICtrlRules.GetStubName(kind, ctrl.name);
                    string paramsDesc = LuaUICtrlRules.GetParamsDesc(kind);
                    if (string.IsNullOrEmpty(stubName))
                        continue;

                    anyStub = true;
                    AppendStub(sb, moduleName, stubName, paramsDesc);
                }
            }
            if (!anyStub)
                sb.Append("-- 当前没有可绑定事件的控件（Button/Toggle/Slider/输入框/下拉框）\r\n\r\n");

            sb.Append("return ").Append(moduleName).Append("\r\n");
            return sb.ToString();
        }

        private static void AppendStub(StringBuilder sb, string moduleName, string funcName, string paramsDesc)
        {
            sb.Append("function ").Append(moduleName).Append(':').Append(funcName).Append('(').Append(paramsDesc).Append(")\r\n");
            if (funcName.StartsWith("On", StringComparison.Ordinal))
                sb.Append("\t-- TODO: 填写业务逻辑\r\n");
            sb.Append("end\r\n\r\n");
        }

        private static string GetCtrlTypeName(CtrlItemData ctrl)
        {
            if (!string.IsNullOrEmpty(ctrl.type))
                return ctrl.type;

            if (ctrl.targets != null && ctrl.targets.Length > 0 && ctrl.targets[0] != null)
                return ctrl.targets[0].GetType().Name;

            return "未知";
        }
    }
}
#endif

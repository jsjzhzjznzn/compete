using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

#if XLUA
using XLua;
#endif

namespace SkierFramework
{
    /// <summary>
    /// 控件事件桩分类。生成器与运行时桥接共用同一套规则，保证生成的桩和实际挂的事件一一对应
    /// </summary>
    public enum LuaCtrlEventKind
    {
        None,
        Click,              // Button -> OnClick_xxx()
        ValueChangedBool,   // Toggle -> OnValueChanged_xxx(isOn)
        ValueChangedFloat,  // Slider -> OnValueChanged_xxx(value)
        EndEdit,            // InputField/TMP_InputField -> OnEndEdit_xxx(text)
        ValueChangedInt,    // Dropdown/TMP_Dropdown -> OnValueChanged_xxx(index)
    }

    public static class LuaUICtrlRules
    {
        public static LuaCtrlEventKind GetEventKind(Component ctrl)
        {
            if (ctrl == null)
                return LuaCtrlEventKind.None;

            if (ctrl is TMP_Dropdown || ctrl is Dropdown)
                return LuaCtrlEventKind.ValueChangedInt;
            if (ctrl is TMP_InputField || ctrl is InputField)
                return LuaCtrlEventKind.EndEdit;
            if (ctrl is Slider)
                return LuaCtrlEventKind.ValueChangedFloat;
            if (ctrl is Toggle)
                return LuaCtrlEventKind.ValueChangedBool;
            if (ctrl is Button)
                return LuaCtrlEventKind.Click;

            return LuaCtrlEventKind.None;
        }

        public static string GetStubName(LuaCtrlEventKind kind, string ctrlName)
        {
            switch (kind)
            {
                case LuaCtrlEventKind.Click:       return "OnClick_" + ctrlName;
                case LuaCtrlEventKind.ValueChangedBool:   return "OnValueChanged_" + ctrlName;
                case LuaCtrlEventKind.ValueChangedFloat:  return "OnValueChanged_" + ctrlName;
                case LuaCtrlEventKind.EndEdit:     return "OnEndEdit_" + ctrlName;
                case LuaCtrlEventKind.ValueChangedInt:    return "OnValueChanged_" + ctrlName;
                default: return null;
            }
        }

        /// <summary>
        /// 桩函数的 Lua 形参（用于生成模板），无参返回空串
        /// </summary>
        public static string GetParamsDesc(LuaCtrlEventKind kind)
        {
            switch (kind)
            {
                case LuaCtrlEventKind.Click:           return "";
                case LuaCtrlEventKind.ValueChangedBool: return "isOn";
                case LuaCtrlEventKind.ValueChangedFloat: return "value";
                case LuaCtrlEventKind.EndEdit:         return "text";
                case LuaCtrlEventKind.ValueChangedInt: return "index";
                default: return null;
            }
        }
    }

    /// <summary>
    /// C# 壳 -> Lua 模块 的通用桥接基类。
    /// 生命周期（OnInit/OnOpen/OnClose 等）与控件事件按约定调用 Lua 模块同名函数，函数不存在则跳过。
    /// 由 UI 生成工具生成的 View 继承本类；Lua 模块默认路径 Assets/LuaScripts/UI/{类名}.lua.txt，require 名 "UI/{类名}"。
    /// </summary>
    public class UILuaView : UIView
    {
        /// <summary>
        /// Lua 模块 require 名。若在生成器里改了 Lua 目录，需同步重写本属性（目录必须位于 Assets/LuaScripts 下）
        /// </summary>
        protected virtual string LuaModuleName => "UI/" + GetType().Name;

#if XLUA
        private UIControlData _ctrlData;
        private bool _eventsWired;
        private readonly List<Action> _unsubscribeActions = new List<Action>();
#endif

#if XLUA
        private LuaTable _module;
        private LuaEnv _env;
        private static LuaEnv s_lastEnv;
        private static bool s_envWarned;
        private static readonly Dictionary<Type, LuaTable> s_moduleCache = new Dictionary<Type, LuaTable>();
#endif

        public override void OnInit(UIControlData uIControlData, UIViewController controller)
        {
            base.OnInit(uIControlData, controller);
#if XLUA
            _ctrlData = uIControlData;
            if (EnsureModule())
            {
                InjectControls();
                InvokeLua("OnInit");
            }
#endif
        }

        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
#if XLUA
            if (_module != null)
                InvokeLua("OnOpen", userData);
#endif
        }

        public override void OnAddListener()
        {
            base.OnAddListener();
#if XLUA
            if (_module == null)
                return;
            WireEvents();
            InvokeLua("OnAddListener");
#endif
        }

        public override void OnRemoveListener()
        {
#if XLUA
            if (_module != null)
            {
                UnwireEvents();
                InvokeLua("OnRemoveListener");
            }
#endif
            base.OnRemoveListener();
        }

        public override void OnClose()
        {
#if XLUA
            if (_module != null)
            {
                InvokeLua("OnClose");
                ClearInjectedControls(); // 释放模块表里对控件的引用，避免静态缓存持有已销毁对象
            }
#endif
            base.OnClose();
        }

#if XLUA
        /// <summary>
        /// 获取（或加载）本 View 对应的 Lua 模块表。模块按类型缓存，LuaEnv 未启动时降级为纯 C# 模式
        /// </summary>
        private bool EnsureModule()
        {
            if (_module != null)
                return true;

            _env = LuaLauncher.LuaEnv;
            if (_env == null)
            {
                if (!s_envWarned)
                {
                    s_envWarned = true;
                    Debug.LogWarning("[UILuaView] LuaEnv 未启动，Lua UI 以纯 C# 模式运行（请确认已调用 LuaLauncher.StartLuaVM()）");
                }
                return false;
            }

            // LuaEnv 重建（如编辑器重进 Play）后清掉旧缓存，避免拿到死环境的表
            if (s_lastEnv != _env)
            {
                s_lastEnv = _env;
                s_moduleCache.Clear();
            }

            Type type = GetType();
            if (!s_moduleCache.TryGetValue(type, out _module))
            {
                try
                {
                    object[] ret = _env.DoString("return require('" + LuaModuleName + "')", "UILuaView:" + LuaModuleName);
                    _module = (ret != null && ret.Length > 0) ? ret[0] as LuaTable : null;
                    if (_module == null)
                    {
                        Debug.LogError("[UILuaView] Lua 模块加载失败，检查 .lua.txt 文件与 require 路径: " + LuaModuleName);
                    }
                    else
                    {
                        s_moduleCache[type] = _module;
                    }
                }
                catch (Exception e)
                {
                    _module = null;
                    Debug.LogError("[UILuaView] Lua 模块加载异常: " + LuaModuleName + "\n" + e);
                }
            }
            return _module != null;
        }

        /// <summary>
        /// 调用模块函数（约定首个参数 self = 模块表，与 Lua 里 function M:xxx() 定义对应）
        /// </summary>
        protected void InvokeLua(string funcName, params object[] args)
        {
            if (_module == null)
                return;

            LuaFunction func = _module.Get<LuaFunction>(funcName);
            if (func == null)
                return;

            try
            {
                object[] callArgs = new object[args.Length + 1];
                callArgs[0] = _module;
                Array.Copy(args, 0, callArgs, 1, args.Length);
                func.Call(callArgs);
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("[UILuaView] 调用 {0}.{1} 出错: {2}", LuaModuleName, funcName, e);
            }
            finally
            {
                func.Dispose();
            }
        }

        /// <summary>
        /// 把 UIControlData 绑定的控件引用注入模块表（字段名 = 绑定名，与 LuaUICtrlRules 生成规则一致）
        /// </summary>
        private void InjectControls()
        {
            if (_ctrlData == null || _module == null)
                return;

            foreach (var item in _ctrlData.ctrlItemDatas)
            {
                var targets = item.targets;
                if (targets == null || targets.Length == 0)
                    continue;

                if (targets.Length == 1)
                {
                    _module.Set(item.name, targets[0]);
                }
                else
                {
                    LuaTable arr = _env.NewTable();
                    for (int i = 0; i < targets.Length; i++)
                        arr.Set(i + 1, targets[i]);
                    _module.Set(item.name, arr);
                }
            }

            foreach (var subUI in _ctrlData.subUIItemDatas)
            {
                if (subUI != null && subUI.subUIData != null)
                    _module.Set(subUI.name, subUI.subUIData);
            }
        }

        /// <summary>
        /// 清空注入的控件引用（置 nil），防止模块表在界面关闭后仍持有控件
        /// </summary>
        private void ClearInjectedControls()
        {
            if (_ctrlData == null || _module == null)
                return;

            if (_ctrlData.ctrlItemDatas != null)
            {
                foreach (var item in _ctrlData.ctrlItemDatas)
                {
                    if (item != null && !string.IsNullOrEmpty(item.name))
                        _module.Set<string, object>(item.name, null);
                }
            }

            if (_ctrlData.subUIItemDatas != null)
            {
                foreach (var subUI in _ctrlData.subUIItemDatas)
                {
                    if (subUI != null && !string.IsNullOrEmpty(subUI.name))
                        _module.Set<string, object>(subUI.name, null);
                }
            }
        }

        /// <summary>
        /// 按控件实际类型挂事件：Button/Toggle/Slider/输入框/下拉框 -> Lua 桩函数
        /// </summary>
        private void WireEvents()
        {
            if (_eventsWired || _ctrlData == null || _module == null)
                return;

            try
            {
                foreach (var item in _ctrlData.ctrlItemDatas)
                {
                    var targets = item.targets;
                    if (targets == null || targets.Length != 1 || targets[0] == null)
                        continue; // 数组控件不做自动事件绑定

                    Component ctrl = targets[0] as Component;
                    LuaCtrlEventKind kind = LuaUICtrlRules.GetEventKind(ctrl);
                    string stubName = LuaUICtrlRules.GetStubName(kind, item.name);
                    if (string.IsNullOrEmpty(stubName))
                        continue;

                    if (ctrl is TMP_Dropdown tmpDropdown)
                    {
                        UnityAction<int> cb = v => InvokeLua(stubName, v);
                        tmpDropdown.onValueChanged.AddListener(cb);
                        _unsubscribeActions.Add(() => tmpDropdown.onValueChanged.RemoveListener(cb));
                    }
                    else if (ctrl is Dropdown dropdown)
                    {
                        UnityAction<int> cb = v => InvokeLua(stubName, v);
                        dropdown.onValueChanged.AddListener(cb);
                        _unsubscribeActions.Add(() => dropdown.onValueChanged.RemoveListener(cb));
                    }
                    else if (ctrl is TMP_InputField tmpInput)
                    {
                        UnityAction<string> cb = s => InvokeLua(stubName, s);
                        tmpInput.onEndEdit.AddListener(cb);
                        _unsubscribeActions.Add(() => tmpInput.onEndEdit.RemoveListener(cb));
                    }
                    else if (ctrl is InputField input)
                    {
                        UnityAction<string> cb = s => InvokeLua(stubName, s);
                        input.onEndEdit.AddListener(cb);
                        _unsubscribeActions.Add(() => input.onEndEdit.RemoveListener(cb));
                    }
                    else if (ctrl is Slider slider)
                    {
                        UnityAction<float> cb = v => InvokeLua(stubName, v);
                        slider.onValueChanged.AddListener(cb);
                        _unsubscribeActions.Add(() => slider.onValueChanged.RemoveListener(cb));
                    }
                    else if (ctrl is Toggle toggle)
                    {
                        UnityAction<bool> cb = v => InvokeLua(stubName, v);
                        toggle.onValueChanged.AddListener(cb);
                        _unsubscribeActions.Add(() => toggle.onValueChanged.RemoveListener(cb));
                    }
                    else if (ctrl is Button button)
                    {
                        UnityAction cb = () => InvokeLua(stubName);
                        button.onClick.AddListener(cb);
                        _unsubscribeActions.Add(() => button.onClick.RemoveListener(cb));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[UILuaView] 控件事件绑定失败: " + GetType().Name + "\n" + e);
            }
            _eventsWired = true;
        }

        private void UnwireEvents()
        {
            for (int i = 0; i < _unsubscribeActions.Count; i++)
            {
                try
                {
                    _unsubscribeActions[i]();
                }
                catch (Exception e)
                {
                    Debug.LogError("[UILuaView] 控件事件解绑失败: " + GetType().Name + "\n" + e);
                }
            }
            _unsubscribeActions.Clear();
            _eventsWired = false;
        }

        [ContextMenu("重载Lua模块(调试)")]
        public void ReloadLuaModule()
        {
            s_moduleCache.Remove(GetType());
            _module = null;
            UnwireEvents();
            if (_ctrlData != null && EnsureModule())
            {
                InjectControls();
                InvokeLua("OnInit");
                WireEvents();
                InvokeLua("OnAddListener");
            }
        }
#endif
    }
}

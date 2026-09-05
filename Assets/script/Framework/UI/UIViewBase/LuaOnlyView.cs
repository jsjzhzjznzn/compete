using UnityEngine;

namespace SkierFramework
{
    /// <summary>
    /// 纯 Lua 热更 UI 的通用壳：不需要为每个界面写 C# 类、也不需要往 UIType 枚举加值。
    /// 一个 LuaOnlyView 实例 = UIConfig.json 里的一条 isLuaUI=true 记录，模块名由 UIViewController
    /// 在加载实例前按配置注入，所有逻辑写在 Lua 模块里。因此新增界面只发 Lua + 预制体 + json 即可。
    /// </summary>
    public class LuaOnlyView : UILuaView
    {
        /// <summary>
        /// Lua 模块 require 名（如 "UI/UIShopView"），加载实例前由 UIViewController 按 UIConfig.json 注入；
        /// 为空时按类型名兜底
        /// </summary>
        [SerializeField]
        public string luaModuleName;

        protected override string LuaModuleName
        {
            get
            {
                if (string.IsNullOrEmpty(luaModuleName))
                    return "UI/" + GetType().Name;
                return luaModuleName;
            }
        }
    }
}

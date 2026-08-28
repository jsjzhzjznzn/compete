# UI 框架使用文档（SkierFramework 适配版）

> 本框架由 UISystem 迁移并适配到本项目：Addressables → Resources.Load，Newtonsoft.Json → JsonUtility，XLua 剥离（`#if XLUA` 保护），Unity 6 API 兼容。命名空间统一为 `SkierFramework`。

---

## 一、整体架构

```
UIManager（总调度，单例）
 ├─ UIConfig.json ──配置表──> 一组 UIViewController
 ├─ UILayerLogic × 7（每层一个 Canvas）
 ├─ UIViewController（每个 UI 一个，控制加载/开/关）
 │    └─ UIView（挂在预制体上的 MonoBehaviour，业务逻辑）
 └─ UIControlData（预制体上的控件绑定数据）
```

核心设计：**UIType 枚举 ↔ 预制体路径 ↔ 视图类** 三者通过 `UIConfig.json` 配置表映射，代码中只用枚举打开 UI。

### 七层 Canvas（UILayer.cs）

| 层级 | 值 | 用途 |
|---|---|---|
| SceneLayer | 1000 | 3D 场景 UI（WorldSpace Canvas） |
| BackgroundLayer | 2000 | 背景层 |
| NormalLayer | 3000 | 普通界面（默认） |
| InfoLayer | 4000 | 信息面板 |
| TopLayer | 5000 | 顶层界面 |
| TipLayer | 6000 | 飘字、提示 |
| BlackMaskLayer | 7000 | 渐隐黑幕（转场用） |

每层由 `UILayerLogic` 管理一个独立 Canvas（UIExtension.cs `CreateLayerCanvas`），层内 UI 排序号从 `(int)layer` 起步、每次 +30 递增（`PopOrder`）。

---

## 二、初始化

```csharp
var uiManager = UIManager.Instance;
uiManager.Initialize();   // 创建 UIRoot、UICamera、7 层 Canvas、黑幕
uiManager.InitUIConfig(); // 读 Resources/UI/UIConfig.json，注册所有 UI
```

- `UIManager` 为 `SingletonMono`，场景中没有时运行时会自动创建
- **URP 注意**：`Initialize()` 中 UI 相机叠加代码默认注释（UIManager.cs:95-108），需打开注释，将 UI 相机设为 Overlay 并加入主相机 Camera Stack，否则 UI 不显示

---

## 三、创建新 UI（编辑器工具流程）

### 1. 制作预制体
- 搭建 UI（普通 UGUI 做法），**根节点挂 `UIControlData` 组件**
- 预制体放在 `Assets/Resources/UI/` 下（Resources.Load 只能加载该目录，路径不含扩展名）

### 2. 绑定控件
- UIControlData 上添加绑定项，填名字（如 `loginBtn`），控件拖入 targets
- **绑定名不能重名**，与程序字段名严格一致
- 右键 UIControlData → **"复制代码到剪贴板"** → 粘贴字段声明到 UIView 脚本，反射自动绑定

### 3. 生成脚本
`Tools/UI管理` 窗口 → 选预制体 → 点"创建UI"：
- 生成 `xxx.cs`（继承 UIView 模板，默认到 `Assets/script/ui/`）
- 自动注册 `UIType.cs` 枚举
- 自动写入 `Assets/Resources/UI/UIConfig.json`

### 4. 配置表格式

```json
{
  "items": [
    { "uiType": "UILoginView", "path": "UI/Prefabs/UILoginView", "isWindow": false, "uiLayer": "NormalLayer", "isAutoNavigation": false }
  ]
}
```

> 注意：JsonUtility 不支持根级数组，格式必须是 `{"items":[...]}`，请用"UI管理"窗口操作，勿手写旧数组格式。

---

## 四、界面逻辑（UIView）

```csharp
public class UILoginView : UIView
{
    // —— 从 UIControlData "复制代码到剪贴板" 粘贴 ——
    public TMP_InputField accountInput;
    public Button loginBtn;
    // —— ——

    public override void OnAddListener()
    {
        loginBtn.onClick.AddListener(OnLogin);
    }

    public override void OnRemoveListener()
    {
        loginBtn.onClick.RemoveListener(OnLogin);
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        accountInput.text = "";
    }

    private void OnLogin()
    {
        UIManager.Instance.Close(UIType.UILoginView);
    }
}
```

### 生命周期钩子

| 钩子 | 触发时机 | 典型用途 |
|---|---|---|
| `OnInit` | 首次加载 | 只做一次的事 |
| `OnAddListener` | 每次打开 | 挂事件监听 |
| `OnRemoveListener` | 每次关闭 | 移除监听 |
| `OnOpen(userData)` | 打开时 | 刷新显示、读传参 |
| `OnResume` | 恢复焦点 | 默认焦点回 `DefaultSelect` |
| `OnPause` | 被其他界面覆盖 | 暂停计时/动画 |
| `OnClose` | 关闭 | 清理 |
| `OnCancel` | 取消键 | 默认关自己 |
| `OnRelease` | 卸载 | 释放资源 |

> **视图关闭不销毁**（仅隐藏），数据要在 `OnOpen` 里重置。

---

## 五、开关与调度 API

```csharp
// 打开（可传任意参数、带完成回调）
UIManager.Instance.Open(UIType.UILoginView, userData: 12345, () => Debug.Log("完成"));

// 关闭
UIManager.Instance.Close(UIType.UILoginView);

// 跳转（A→B，自动管理返回栈：关 B 时还原 A）
UIManager.Instance.JumpUI(UIType.UIMainView, null, UIType.UIShopView, null);

// 获取已打开视图
var view = UIManager.Instance.GetView<UILoginView>(UIType.UILoginView);

// 预加载（打开时秒开）
UIManager.Instance.Preload(UIType.UIShopView);

// 常驻 UI（CloseAll/ReleaseAll 跳过）
UIManager.Instance.AddResidentUI(UIType.UIMainView);

// 批量关 / 全释放
UIManager.Instance.CloseAll();
UIManager.Instance.ReleaseAll();

// 转场黑幕
UIManager.Instance.FadeIn(0.5f);
UIManager.Instance.FadeOut(0.5f);
```

**isWindow（窗口型 UI）**：打开时不遮挡、不隐藏下层 UI（如弹窗）；非窗口 UI 打开时下层被暂停并隐藏（`topViewNum` 机制），避免不可见 UI 重复渲染。

---

## 六、进阶模块

### 1. 开闭动画（UIViewAnim）
预制体上挂 UIViewAnim，Inspector 配 `openType/closeType`（None / Alpha / Scale / Animation），打开关闭动画播完才执行回调。

### 2. 子视图（UISubView）
预制体内小组件挂 UISubView（自动绑定自身 UIControlData），由 Unity 生命周期驱动：`Awake→OnInit/OnAddListener`、`OnEnable→OnOpen`、`OnDisable→OnClose`、`OnDestroy→OnRelease`。不走 UIManager。

### 3. 虚拟滚动列表（UIScrollView + UILoopItem）

```csharp
// Item 脚本：继承 UILoopItem
public class ItemHero : UILoopItem
{
    public Text nameText;
    protected override void OnUpdateData(IList dataList, int index, object userData)
    {
        var hero = (HeroData)dataList[index];
        nameText.text = hero.name;
    }
}

// 使用（挂在 ScrollRect 同物体上，Inspector 配轴/间距/分页）
scrollView.UpdateList<ItemHero>(heroList, itemPrefab);
scrollView.OnSelectChanged += idx => { };
```

### 4. 3D 模型展示（UIModelManager）

```csharp
UIModelManager.Instance.LoadModelToRawImage("UI/Models/hero", rawImage); // 模型显示到 RawImage
UIModelManager.Instance.UnLoadModelByRawImage(rawImage, true);          // 卸载
```

### 5. 全局 UI 事件（跨界面通信）

```csharp
UIManager.Instance.Event.AddListener(UIEvent.None, (Action)(() => { }));
UIManager.Instance.Event.Dispatch(UIEvent.None);
// UIEvent 枚举在 Assets/script/Framework/UI/UIViewBase/UIEvent.cs，可按需扩展
```

### 6. 组件式补间（UITweener）
UI 上挂 `TweenPosition`/`TweenAlpha` 等组件，Inspector 配置即可，与 DOTween 并存。

---

## 七、常见问题

1. **预制体加载失败**：确认在 `Assets/Resources/` 下，路径无扩展名
2. **控件绑定不上**：UIControlData 绑定名与字段名不一致；绑定项重名
3. **UI 不显示（URP）**：UI 相机未加入主相机 Camera Stack
4. **UI 打开黑屏**：`FadeIn` 后必须 `FadeOut`
5. **"UI管理"窗口报目录不存在**：点"选择创建路径"选一个存在的目录（默认 `Assets/script/ui`）

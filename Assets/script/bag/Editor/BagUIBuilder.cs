using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键在**当前场景**里搭出背包 UI（UGUI，纯手摆，不依赖工程里的 UI 框架）。
///
/// 菜单：Tools/背包/在场景里搭背包 UI
///
/// 【这是什么】
///   搭场景的一次性工具，只在编辑器里跑，不参与运行时逻辑。搭完就可以把这个文件删掉。
///   重复执行会先删掉上一次建的 BagCanvas 再重建（幂等）；Items 配置资产会复用不删。
///
/// 【产出的结构】
///   BagCanvas
///   ├─ BagBackground
///   ├─ BagWindow ─┬─ BagTitle / BagCapacity / BagClose
///   │             ├─ BagScrollArea ── BagViewport ── BagContent ── BagCellTemplate
///   │             ├─ BagScrollbar
///   │             ├─ BagAddButton          ← 打开加物品选择器
///   │             └─ BagDetail ─┬─ BagDetailIcon / Name / Desc / Stat
///   │                           └─ BagDropButton   ← 丢弃选中的整叠
///   ├─ BagPicker（默认关）      ← 加物品选择器，复用同一套虚拟列表
///   └─ BagDragGhost（默认关）   ← 拖拽时跟手的图标
/// </summary>
public static class BagUIBuilder
{
    // ==================== 布局常量 ====================
    private const string RootName = "BagCanvas";

    private const float WindowW = 1100f;
    private const float WindowH = 700f;

    private const float CellSize = 120f;
    private const float CellSpacing = 8f;
    private const int Columns = 6;
    private const int VisibleRows = 4;

    /// <summary>整片格子的宽度：6×120 + 5×8 = 760</summary>
    private const float GridW = Columns * CellSize + (Columns - 1) * CellSpacing;

    /// <summary>背包视口高度：4×120 + 3×8 = 504</summary>
    private const float ViewportH = VisibleRows * CellSize + (VisibleRows - 1) * CellSpacing;

    /// <summary>选择器视口高度：3×120 + 2×8 = 376</summary>
    private const float PickerViewportH = 3 * CellSize + 2 * CellSpacing;

    private const float ScrollbarW = 14f;

    // ==================== 资产路径 ====================
    private const string FontPath = "Assets/Resource/字体/晴圆 (Windows 8.1)_爱给网_aigei_com SDF.asset";
    private const string DbPath = "Assets/Resources/ItemDatabase.asset";
    private const string ItemFolder = "Assets/Resources/BagItems";
    /// <summary>
    /// 图标目录（散图全路径写进 SO，走 YooAsset 加载）。
    /// 这两个目录已在 YooAsset 收集器里，所以不用额外配置。
    /// </summary>
    private const string IconItemFolder = "Assets/Resource/ui/mingchao/道具";
    private const string IconWeaponFolder = "Assets/Resource/ui/mingchao/武器";

    // ==================== 配色 ====================
    private static readonly Color ColWindow = new Color(0.12f, 0.13f, 0.16f, 0.98f);
    private static readonly Color ColCell = new Color(0.21f, 0.23f, 0.28f, 1f);
    private static readonly Color ColDetail = new Color(0.17f, 0.18f, 0.22f, 1f);
    private static readonly Color ColSelect = new Color(1f, 0.78f, 0.20f, 1f);
    private static readonly Color ColText = new Color(0.93f, 0.94f, 0.96f, 1f);
    private static readonly Color ColDim = new Color(0.70f, 0.72f, 0.76f, 1f);
    private static readonly Color ColBtn = new Color(0.26f, 0.34f, 0.46f, 1f);
    private static readonly Color ColBtnDanger = new Color(0.55f, 0.26f, 0.26f, 1f);

    /// <summary>一块"虚拟网格滚动区"的全套引用</summary>
    private struct GridArea
    {
        public ScrollRect scrollRect;
        public RectTransform viewport;
        public RectTransform content;
        public RectTransform template;
        public VirtualGridList list;
    }

    [MenuItem("Tools/背包/在场景里搭背包 UI")]
    public static void BuildBagUI()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            Debug.LogWarning($"[BagUIBuilder] 没找到中文字体 {FontPath}，文字可能显示不出来");

        // 幂等：先把上次搭的删掉
        var previous = GameObject.Find(RootName);
        if (previous != null) Object.DestroyImmediate(previous);

        // ==================== 根 Canvas ====================
        var root = NewUI(RootName, null);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();
        var rootRt = root.GetComponent<RectTransform>();
        Stretch(rootRt);

        var panel = root.AddComponent<BagPanel>();

        // ==================== 半透明背景 ====================
        var background = NewUI("BagBackground", root.transform);
        Stretch(background.GetComponent<RectTransform>());
        background.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // ==================== 面板窗口 ====================
        var window = NewUI("BagWindow", root.transform);
        CenterRect(window.GetComponent<RectTransform>(), new Vector2(WindowW, WindowH), Vector2.zero);
        window.AddComponent<Image>().color = ColWindow;

        var title = NewTMP("BagTitle", window.transform, "道具背包", font, 34f, TextAlignmentOptions.Left);
        CenterRect(title.rectTransform, new Vector2(500f, 56f), new Vector2(-250f, 300f));

        var capacity = NewTMP("BagCapacity", window.transform, "0 格", font, 22f, TextAlignmentOptions.Right);
        CenterRect(capacity.rectTransform, new Vector2(200f, 40f), new Vector2(300f, 296f));

        var closeButton = NewButton("BagClose", window.transform, "X", font, 28f,
            new Vector2(64f, 64f), new Vector2(490f, 300f), new Color(0.72f, 0.26f, 0.26f, 1f));

        // 身体区中线
        const float bodyCenterY = -36f;

        // ==================== 背包滚动区 ====================
        var bagArea = BuildGridArea("Bag", window.transform, font,
            new Vector2(GridW, ViewportH), new Vector2(-(WindowW - GridW) * 0.5f + 10f, bodyCenterY), true, ScrollbarW);

        // ==================== 加物品按钮 ====================
        var addButton = NewButton("BagAddButton", window.transform, "加物品", font, 24f,
            new Vector2(140f, 46f), new Vector2(-450f, -316f), ColBtn);

        // ==================== 详情面板 ====================
        var detail = NewUI("BagDetail", window.transform);
        CenterRect(detail.GetComponent<RectTransform>(), new Vector2(280f, ViewportH), new Vector2(398f, bodyCenterY));
        detail.AddComponent<Image>().color = ColDetail;

        var detailIcon = NewUI("BagDetailIcon", detail.transform);
        CenterRect(detailIcon.GetComponent<RectTransform>(), new Vector2(150f, 150f), new Vector2(0f, 160f));
        var detailIconImage = detailIcon.AddComponent<Image>();
        detailIconImage.raycastTarget = false;
        detailIconImage.preserveAspect = true;

        var detailName = NewTMP("BagDetailName", detail.transform, "", font, 26f, TextAlignmentOptions.Center);
        CenterRect(detailName.rectTransform, new Vector2(270f, 50f), new Vector2(0f, 40f));

        var detailDesc = NewTMP("BagDetailDesc", detail.transform, "", font, 20f, TextAlignmentOptions.TopLeft);
        detailDesc.color = ColDim;
        CenterRect(detailDesc.rectTransform, new Vector2(270f, 150f), new Vector2(0f, -70f));

        var detailStat = NewTMP("BagDetailStat", detail.transform, "", font, 22f, TextAlignmentOptions.TopLeft);
        detailStat.color = ColSelect;
        CenterRect(detailStat.rectTransform, new Vector2(270f, 60f), new Vector2(0f, -180f));

        var dropButton = NewButton("BagDropButton", detail.transform, "丢弃", font, 22f,
            new Vector2(120f, 40f), new Vector2(0f, -230f), ColBtnDanger);

        // ==================== 加物品选择器（默认关） ====================
        var picker = NewUI("BagPicker", root.transform);
        Stretch(picker.GetComponent<RectTransform>());

        var pickerBg = NewUI("BagPickerBg", picker.transform);
        Stretch(pickerBg.GetComponent<RectTransform>());
        var pickerBgImage = pickerBg.AddComponent<Image>();
        pickerBgImage.color = new Color(0f, 0f, 0f, 0.6f);

        var pickerWindow = NewUI("BagPickerWindow", picker.transform);
        CenterRect(pickerWindow.GetComponent<RectTransform>(), new Vector2(820f, 500f), Vector2.zero);
        pickerWindow.AddComponent<Image>().color = ColWindow;

        var pickerTitle = NewTMP("BagPickerTitle", pickerWindow.transform, "加物品", font, 30f, TextAlignmentOptions.Left);
        CenterRect(pickerTitle.rectTransform, new Vector2(400f, 50f), new Vector2(-190f, 200f));

        var pickerClose = NewButton("BagPickerClose", pickerWindow.transform, "X", font, 26f,
            new Vector2(56f, 56f), new Vector2(368f, 200f), new Color(0.72f, 0.26f, 0.26f, 1f));

        var pickerArea = BuildGridArea("Picker", pickerWindow.transform, font,
            new Vector2(GridW, PickerViewportH), new Vector2(-10f, -30f), true, ScrollbarW);

        picker.SetActive(false);

        // ==================== 拖拽幽灵图（默认关，放最后保证在最上层） ====================
        var ghost = NewUI("BagDragGhost", root.transform);
        CenterRect(ghost.GetComponent<RectTransform>(), new Vector2(90f, 90f), Vector2.zero);
        var ghostImage = ghost.AddComponent<Image>();
        ghostImage.raycastTarget = false;      // 幽灵图不能挡住底下的格子，否则算不出落点
        ghostImage.preserveAspect = true;
        ghost.SetActive(false);

        // ==================== 面板连线 ====================
        var panelSo = new SerializedObject(panel);
        panelSo.FindProperty("_bagType").enumValueIndex = (int)BagType.Item;
        panelSo.FindProperty("_title").objectReferenceValue = title;
        panelSo.FindProperty("_capacityText").objectReferenceValue = capacity;
        panelSo.FindProperty("_closeButton").objectReferenceValue = closeButton;
        panelSo.FindProperty("_list").objectReferenceValue = bagArea.list;
        panelSo.FindProperty("_dropButton").objectReferenceValue = dropButton;
        panelSo.FindProperty("_addButton").objectReferenceValue = addButton;
        panelSo.FindProperty("_dragGhost").objectReferenceValue = ghostImage;
        panelSo.FindProperty("_detailRoot").objectReferenceValue = detail;
        panelSo.FindProperty("_detailIcon").objectReferenceValue = detailIconImage;
        panelSo.FindProperty("_detailName").objectReferenceValue = detailName;
        panelSo.FindProperty("_detailDesc").objectReferenceValue = detailDesc;
        panelSo.FindProperty("_detailStat").objectReferenceValue = detailStat;
        panelSo.FindProperty("_pickerRoot").objectReferenceValue = picker;
        panelSo.FindProperty("_pickerList").objectReferenceValue = pickerArea.list;
        panelSo.FindProperty("_pickerCloseButton").objectReferenceValue = pickerClose;
        panelSo.ApplyModifiedPropertiesWithoutUndo();

        // ==================== 配置资产 ====================
        CreateItemAssets();

        // ==================== 收尾 ====================
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        Debug.Log($"[BagUIBuilder] 背包 UI 搭好了：{RootName}（场景未保存，确认没问题自己 Ctrl+S）。" +
                  $"格子 {Columns} 列、{CellSize}×{CellSize}、间距 {CellSpacing}；背包视口 {GridW}×{ViewportH}、选择器视口 {GridW}×{PickerViewportH}。" +
                  $"进 Play 模式后格子才会生成（按容量铺）。");
    }

    // ==================== 滚动区（背包和选择器共用这一套） ====================

    /// <summary>
    /// 生成一块虚拟网格滚动区：ScrollRect + Viewport(RectMask2D) + Content + 格子模板（+ 可选滑动条），
    /// 并把 VirtualGridList 的字段全部接好。
    /// </summary>
    private static GridArea BuildGridArea(string prefix, Transform parent, TMP_FontAsset font,
        Vector2 size, Vector2 position, bool withScrollbar, float scrollbarWidth)
    {
        var area = NewUI($"{prefix}ScrollArea", parent);
        var areaRt = area.GetComponent<RectTransform>();
        CenterRect(areaRt, size, position);

        // Viewport：铺满滚动区，带 RectMask2D 负责裁切
        var viewport = NewUI($"{prefix}Viewport", area.transform);
        var viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.pivot = new Vector2(0f, 1f);
        viewportRt.anchoredPosition = Vector2.zero;
        viewportRt.sizeDelta = Vector2.zero;

        // 视口要有一张（透明的）Image 才能接住在空白处按下拖动的事件，只用 RectMask2D 不够
        var viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewport.AddComponent<RectMask2D>();

        // Content：TopStretch，宽度跟视口一样，高度由 VirtualGridList 运行时算
        var content = NewUI($"{prefix}Content", viewport.transform);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, size.y);

        var scrollRect = area.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.1f;
        scrollRect.scrollSensitivity = 40f;

        // 格子模板
        var template = NewUI($"{prefix}CellTemplate", content.transform);
        var templateRt = template.GetComponent<RectTransform>();
        templateRt.anchorMin = new Vector2(0f, 1f);
        templateRt.anchorMax = new Vector2(0f, 1f);
        templateRt.pivot = new Vector2(0f, 1f);
        templateRt.sizeDelta = new Vector2(CellSize, CellSize);
        templateRt.anchoredPosition = Vector2.zero;

        // 格子的底框：空槽也要看得见"这里有个格子"，所以底框挂在根节点上
        var templateImage = template.AddComponent<Image>();
        templateImage.color = ColCell;
        templateImage.raycastTarget = true;

        var cellButton = template.AddComponent<Button>();
        cellButton.targetGraphic = templateImage;
        cellButton.transition = Selectable.Transition.None;   // 高亮交给 SelectFrame

        var cell = template.AddComponent<BagCell>();

        // 选中高亮：铺满整格、放在最底层 —— 图标比它小一圈，露出来的就是一圈亮边
        var select = NewUI($"{prefix}CellSelect", template.transform);
        var selectRt = select.GetComponent<RectTransform>();
        Stretch(selectRt);
        var selectImage = select.AddComponent<Image>();
        selectImage.color = ColSelect;
        selectImage.raycastTarget = false;
        selectRt.SetAsFirstSibling();
        select.SetActive(false);

        // 图标：铺满并留 8px 内边距
        var icon = NewUI($"{prefix}CellIcon", template.transform);
        var iconRt = icon.GetComponent<RectTransform>();
        Stretch(iconRt);
        iconRt.sizeDelta = new Vector2(-16f, -16f);
        var iconImage = icon.AddComponent<Image>();
        iconImage.raycastTarget = false;
        iconImage.preserveAspect = true;

        // 数量：右下角，只有 >1 时才显示
        var count = NewTMP($"{prefix}CellCount", template.transform, "", font, 22f, TextAlignmentOptions.BottomRight);
        var countRt = count.rectTransform;
        countRt.anchorMin = new Vector2(1f, 0f);
        countRt.anchorMax = new Vector2(1f, 0f);
        countRt.pivot = new Vector2(1f, 0f);
        countRt.sizeDelta = new Vector2(70f, 28f);
        countRt.anchoredPosition = new Vector2(-6f, 6f);

        // 模板必须关掉，否则界面上会多出一个格子
        template.SetActive(false);

        var cellSo = new SerializedObject(cell);
        cellSo.FindProperty("_icon").objectReferenceValue = iconImage;
        cellSo.FindProperty("_countText").objectReferenceValue = count;
        cellSo.FindProperty("_button").objectReferenceValue = cellButton;
        cellSo.FindProperty("_selectFrame").objectReferenceValue = select;
        cellSo.FindProperty("_emptyMask").objectReferenceValue = null;   // 有底框就够了
        cellSo.ApplyModifiedPropertiesWithoutUndo();

        // 滑动条：放在滚动区右边（不是它的子物体），这样 760 宽的格子区不用为它让位
        if (withScrollbar)
        {
            var scrollbar = NewUI($"{prefix}Scrollbar", parent);
            CenterRect(scrollbar.GetComponent<RectTransform>(), new Vector2(scrollbarWidth, size.y),
                new Vector2(position.x + size.x * 0.5f + 8f + scrollbarWidth * 0.5f, position.y));
            scrollbar.AddComponent<Image>().color = new Color(0.10f, 0.11f, 0.13f, 1f);

            var scrollbarComp = scrollbar.AddComponent<Scrollbar>();
            scrollbarComp.direction = Scrollbar.Direction.BottomToTop;

            var slidingArea = NewUI($"{prefix}ScrollbarArea", scrollbar.transform);
            Stretch(slidingArea.GetComponent<RectTransform>());

            var handle = NewUI($"{prefix}ScrollbarHandle", slidingArea.transform);
            Stretch(handle.GetComponent<RectTransform>());
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.55f, 0.58f, 0.64f, 1f);

            scrollbarComp.handleRect = handle.GetComponent<RectTransform>();
            scrollbarComp.targetGraphic = handleImage;

            scrollRect.verticalScrollbar = scrollbarComp;
            // Permanent：不做"自动隐藏/自动扩视口"那套（那依赖 LayoutRebuilder，
            // 我们这棵树是手摆锚点的，走它反而不确定）。内容不够高时 Handle 撑满整条，看着也正常。
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scrollRect.verticalScrollbarSpacing = 0f;
        }

        var list = area.AddComponent<VirtualGridList>();
        var listSo = new SerializedObject(list);
        listSo.FindProperty("_scrollRect").objectReferenceValue = scrollRect;
        listSo.FindProperty("_viewport").objectReferenceValue = viewportRt;
        listSo.FindProperty("_content").objectReferenceValue = contentRt;
        listSo.FindProperty("_itemTemplate").objectReferenceValue = templateRt;
        listSo.FindProperty("_cellSize").vector2Value = new Vector2(CellSize, CellSize);
        listSo.FindProperty("_spacing").vector2Value = new Vector2(CellSpacing, CellSpacing);
        listSo.FindProperty("_columns").intValue = Columns;
        listSo.FindProperty("_extraRows").intValue = 1;
        listSo.ApplyModifiedPropertiesWithoutUndo();

        return new GridArea
        {
            scrollRect = scrollRect,
            viewport = viewportRt,
            content = contentRt,
            template = templateRt,
            list = list,
        };
    }

    // ==================== 配置资产 ====================

    private static void CreateItemAssets()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(ItemFolder);

        var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DbPath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<ItemDatabase>();
            AssetDatabase.CreateAsset(database, DbPath);
        }

        var items = new List<ItemData>
        {
            CreateItem("Item_HpPotion", "item.hp_potion", "回复药水", ItemType.Item, 99,
                $"{IconItemFolder}/T_IconA80_02_UI.png", "喝下去回一点体力。（示例配置）"),

            CreateItem("Item_ManaPotion", "item.mana_potion", "能量饮料", ItemType.Item, 99,
                $"{IconItemFolder}/T_IconA80_03_UI.png", "灌一口提神。（示例配置）"),

            CreateItem("Item_Gear", "item.gear", "齿轮零件", ItemType.Item, 999,
                $"{IconItemFolder}/T_IconA80_04_UI.png", "随处可见的小零件。（示例配置，用来试叠很多个）"),

            CreateItem("Weapon_Blade01", "weapon.blade_01", "试作长刃", ItemType.Weapon, 1,
                $"{IconWeaponFolder}/T_Luckdraw21030015_UI.png", "还没开刃的刀。（示例武器，走武器背包）",
                (AttrType.Attack, ModType.Add, 20f)),

            CreateItem("Weapon_Blade02", "weapon.blade_02", "试作双刃", ItemType.Weapon, 1,
                $"{IconWeaponFolder}/T_Luckdraw21030023_UI.png", "双持用的刀。（示例武器）",
                (AttrType.Attack, ModType.Multiply, 1.15f),
                (AttrType.DamageUp, ModType.Add, 0.1f)),
        };

        var dbSo = new SerializedObject(database);
        var listProp = dbSo.FindProperty("_items");
        listProp.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
        {
            listProp.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
        dbSo.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
    }

    /// <summary>建（或更新）一条配置。已存在就复用不删 —— 避免换 guid 把引用弄断</summary>
    private static ItemData CreateItem(string fileName, string id, string displayName, ItemType type,
        int maxStack, string iconPath, string desc,
        params (AttrType attr, ModType modType, float value)[] stats)
    {
        string path = $"{ItemFolder}/{fileName}.asset";

        var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, path);
        }

        // 搭建时顺手校验图标路径 —— 路径写错时编辑器里立刻能看见，不用等运行时
        if (AssetDatabase.LoadAssetAtPath<Sprite>(iconPath) == null)
            Debug.LogWarning($"[BagUIBuilder] 图标路径找不到 Sprite：{iconPath}（\"{displayName}\" 的格子会没有图）");

        var so = new SerializedObject(item);
        so.FindProperty("_itemId").stringValue = id;
        so.FindProperty("_itemName").stringValue = displayName;
        so.FindProperty("_itemType").enumValueIndex = (int)type;
        so.FindProperty("_maxStack").intValue = maxStack;
        so.FindProperty("_desc").stringValue = desc;
        so.FindProperty("_iconPath").stringValue = iconPath;

        var statsProp = so.FindProperty("_stats");
        statsProp.arraySize = stats.Length;
        for (int i = 0; i < stats.Length; i++)
        {
            var entry = statsProp.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("attr").enumValueIndex = (int)stats[i].attr;
            entry.FindPropertyRelative("modType").enumValueIndex = (int)stats[i].modType;
            entry.FindPropertyRelative("value").floatValue = stats[i].value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        return item;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    // ==================== 小工具 ====================

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) go.layer = uiLayer;
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI NewTMP(string name, Transform parent, string text,
        TMP_FontAsset font, float fontSize, TextAlignmentOptions alignment)
    {
        var go = NewUI(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        if (font != null) tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.color = ColText;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>图片按钮 + 一行居中文字</summary>
    private static Button NewButton(string name, Transform parent, string label, TMP_FontAsset font,
        float fontSize, Vector2 size, Vector2 position, Color color)
    {
        var go = NewUI(name, parent);
        CenterRect(go.GetComponent<RectTransform>(), size, position);

        var image = go.AddComponent<Image>();
        image.color = color;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        var text = NewTMP($"{name}Text", go.transform, label, font, fontSize, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);

        return button;
    }

    /// <summary>铺满父节点</summary>
    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    /// <summary>固定尺寸 + 相对父节点中心定位</summary>
    private static void CenterRect(RectTransform rt, Vector2 size, Vector2 position)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }
}

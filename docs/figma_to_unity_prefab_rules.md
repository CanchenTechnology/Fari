# Figma 到 Unity Prefab 生成规则

本文档用于约束从 Figma 设计稿生成 Unity UGUI prefab 的流程。目标是让生成结果可以直接进入项目工程，而不是只做视觉截图还原。

参考 Figma 文件：

```text
https://www.figma.com/design/giPUObWIasLznx7cUHuZV3/Fari?node-id=213-319&t=fjFNIQvo5zcmCzCm-0
```

## 核心原则

1. 一个 Figma 界面 Frame 对应一个 Unity prefab。
2. 不允许把完整界面截图作为 prefab 主体。
3. 所有可见 UI 都应拆成 Unity 组件：`Image`、`TextMeshProUGUI`、`Button`、`Toggle`、`ScrollRect`、布局容器等。
4. prefab 必须可适配安全区和不同屏幕比例。
5. prefab 必须能在 Unity 中重新生成，生成逻辑要可重复、可检查。

## Prefab 拆分规则

### 必须拆分的情况

- Figma 中的不同页面、不同主界面、不同导航 Tab 页面，必须生成不同 prefab。
- 例如 `Friend` 和 `Mine` 是两个界面，应分别生成：
  - `Assets/GameData/AIUI/Friend/ExampleFriendUI.prefab`
  - `Assets/GameData/AIUI/My/ExampleMineUI.prefab`
- 不要把多个完整页面塞进一个 prefab 再用脚本切换，除非需求明确要求“一个复合容器 prefab”。

### 可以放在同一个 prefab 的情况

- 同一界面内的弹出层、空状态、展开态，如果属于同一业务界面的内部状态，可以放在同一个 prefab 内。
- 同一组件的多个视觉状态可以放在一个 prefab 内，但应通过清晰命名区分，例如 `EmptyState`、`LoadingState`、`ContentState`。

## 目录与命名规则

### Prefab 路径

Figma 生成的 prefab 统一放在：

```text
Assets/GameData/AIUI/
```

`AIUI` 是 Figma 自动生成 UI 的临时加工区，只用于 Editor 内预览、检查和二次加工。生成到这里的 prefab、脚本和图片资源默认不视为正式运行资源，不能因为生成成功就自动接入正式 UI 配置或打包链路。

按界面或业务模块继续分子目录：

```text
Assets/GameData/AIUI/Friend/ExampleFriendUI.prefab
Assets/GameData/AIUI/My/ExampleMineUI.prefab
```

命名格式：

```text
<Feature><ScreenName>UI.prefab
```

示例：

```text
FriendRequestUI.prefab
ExampleFriendUI.prefab
ExampleMineUI.prefab
```

### 资源路径

从 Figma 导出的独立图片资源放入：

```text
Assets/GameData/AIUI/UI/<Feature>/Generated/
```

所有从 Figma 导出的独立图片资源都必须放在这个目录结构下。

如果只是本次示例或跨界面共享资源，可以放入：

```text
Assets/GameData/AIUI/UI/ExampleUi/Generated/
```

不要保留整页截图作为正式 prefab 依赖。截图只允许作为临时参考图。

## Figma 读取规则

1. 优先读取具体 Frame 节点，而不是只读当前选中小组件。
2. 如果用户给的是 Figma 文件 URL，先通过 metadata 找到页面中的顶层 Frame。
3. 对每个要生成的界面单独读取设计上下文。
4. 截图只能用于视觉对照，不作为最终 prefab 背景。
5. Figma 中的图片、头像、图标，应导出为单独 sprite 资源。

推荐读取顺序：

```text
get_metadata(file root)
get_design_context(frame node)
get_screenshot(frame node, for visual reference only)
download required image assets
```

## 组件还原规则

### 必须组件化

以下内容必须生成独立 GameObject：

- 页面背景
- 状态栏文本和图标
- 顶部头像、用户名、状态文案
- 列表行
- 卡片
- 设置项
- 底部 Tab
- 按钮
- 文本
- 分割线
- Home indicator
- 图标和头像

### 不允许的做法

不要使用：

```text
ScreenShot
FullPageImage
完整页面 PNG + 透明热区按钮
```

不要用透明按钮热区代替真实组件。按钮可以有透明背景，但按钮内部的文字、图标、卡片仍必须独立存在。

## UGUI 技术规范

### UI 模板来源

生成 prefab 时必须参考项目 UI 模板：

```text
Assets/GamerFrameWork/UIFrameWork/TempPrefabs/UITemp.prefab
```

不要从空 GameObject 手写一套 Canvas 根节点。生成流程应先克隆或加载 `UITemp.prefab`，再把根节点重命名为目标 prefab 名称，例如 `ExampleFriendUI`、`ExampleMineUI`。

模板相关适配脚本：

```text
Assets/GamerFrameWork/UIFrameWork/Scripts/Runtime/Adaptation/AdaptationBangs.cs
Assets/GamerFrameWork/UIFrameWork/Scripts/Runtime/Adaptation/AdapterIOSTouchBar.cs
```

`AdaptationBangs` 负责根据 `Screen.safeArea` 调整安全区锚点；`AdapterIOSTouchBar` 负责 iOS 底部触摸条偏移。生成的界面内容必须放到模板的 `UIContent` 节点下，让这两个适配脚本统一生效。

### 根节点结构

每个 prefab 使用以下结构。根节点来自 `UITemp.prefab`，生成后只重命名并追加业务脚本：

```text
ExampleFriendUI                       // root, cloned from UITemp
  RectTransform
  Canvas
  CanvasScaler
  GraphicRaycaster
  CanvasGroup
  ExampleFriendUI                     // runtime window script
  ExampleFriendUIComponent            // generated component references
  UIMask                              // keep template background / mask node
  UIContent                           // generated Figma UI must be placed here
    AdaptationBangs
    AdapterIOSTouchBar
    Screen
      FriendPageDesign
        ScreenBackground
        ...
```

`Mine` 同理：

```text
ExampleMineUI
  ExampleMineUI
  ExampleMineUIComponent
  UIMask
  UIContent
    Screen
      MinePageDesign
        ScreenBackground
        ...
```

### Canvas 设置

```text
Canvas.renderMode = ScreenSpaceOverlay
CanvasScaler.uiScaleMode = ScaleWithScreenSize
CanvasScaler.referenceResolution = 1080 x 1920
CanvasScaler.screenMatchMode = MatchWidthOrHeight
CanvasScaler.matchWidthOrHeight = 0
```

以上值以 `UITemp.prefab` 当前配置为准。除非明确改模板，否则生成器不要私自改 Canvas、CanvasScaler、GraphicRaycaster、CanvasGroup 的基础配置。

### 安全区和适配

每个 prefab 都必须支持：

- iPhone 刘海屏安全区
- Android 异形屏安全区
- 不同高宽比
- 平板或宽屏等比缩放

推荐实现：

- 复用 `UITemp.prefab` 的 `UIContent` 节点作为安全区内容容器，不再额外创建 `SafeAreaContent`。
- `<ScreenName>UI.cs` 负责 Safe Area、界面表现、交互响应和适配刷新。
- `<ScreenName>UIComponent.cs` 负责保存组件引用和事件绑定。
- 所有从 Figma 生成的界面统一按 `1080 x 1920` 设计基准还原。
- Figma 原始 Frame 尺寸只作为比例参考，生成时需要等比映射到 `1080 x 1920` 基准容器。
- Figma 子节点在 `1080 x 1920` 固定设计尺寸容器内用 top-left 坐标还原。

统一设计尺寸：

```text
1080 x 1920
```

## UI 脚本拆分规则

每个由 Figma 生成的界面必须配套两个运行时脚本，参考项目中的：

```text
Assets/Scripts/UI/NavigationUI.cs
Assets/Scripts/UI/NavigationUIComponent.cs
```

生成脚本路径统一放在对应的 `AIUI` 界面目录下：

```text
Assets/GameData/AIUI/Friend/ExampleFriendUI.cs
Assets/GameData/AIUI/Friend/ExampleFriendUIComponent.cs
Assets/GameData/AIUI/My/ExampleMineUI.cs
Assets/GameData/AIUI/My/ExampleMineUIComponent.cs
```

这些脚本属于临时生成脚本，用于让 prefab 在 Editor 阶段可检查、可绑定、可二次加工。只有当 prefab 被人工整理并迁移到正式目录后，才把对应脚本迁移或合并到正式脚本目录。

### `<ScreenName>UI.cs`

职责：

- 继承项目 UI 框架的 `WindowBase`。
- 持有 `public <ScreenName>UIComponent uiComponent;`。
- 在 `OnAwake()` 中获取 Component 并调用 `uiComponent.InitComponent(this)`。
- 负责界面的表现层逻辑：安全区适配、状态刷新、按钮响应、显示隐藏动画、视觉更新。
- 不写业务数据逻辑，不直接访问远端服务，不承担数据持久化。

结构参考：

```csharp
public class ExampleFriendUI : WindowBase
{
    public ExampleFriendUIComponent uiComponent;

    public override void OnAwake()
    {
        uiComponent = gameObject.GetComponent<ExampleFriendUIComponent>();
        uiComponent.InitComponent(this);
        this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
        base.OnAwake();
    }
}
```

### `<ScreenName>UIComponent.cs`

职责：

- 继承 `MonoBehaviour`。
- 保存 prefab 中所有需要脚本访问的组件引用。
- 暴露 `public WindowLayer windowLayer = WindowLayer.Top;`。
- 提供 `InitComponent(WindowBase target)`。
- 在 `InitComponent` 中完成按钮、Toggle、Input 等 UI 事件绑定。
- 可以由自动化工具覆盖生成，手写业务表现逻辑不要放在这里。

结构参考：

```csharp
public class ExampleFriendUIComponent : MonoBehaviour
{
    public WindowLayer windowLayer = WindowLayer.Top;
    public Button addFriendButton;
    public Button searchFriendButton;

    public void InitComponent(WindowBase target)
    {
        target.Canvas.sortingOrder = (int)windowLayer;
        target.Layer = windowLayer;
        ExampleFriendUI window = (ExampleFriendUI)target;
        target.AddButtonClickListener(addFriendButton, window.OnAddFriendClick);
        target.AddButtonClickListener(searchFriendButton, window.OnSearchFriendClick);
    }
}
```

### 命名要求

- UI 逻辑脚本：`<ScreenName>UI.cs`
- UI 组件脚本：`<ScreenName>UIComponent.cs`
- prefab 根节点挂载这两个脚本。
- 组件字段名称必须稳定、语义化，例如 `friendRequestButton`、`avatarImage`、`userNameText`。
- prefab 层级中的关键节点仍使用 `[Button]`、`[Text]`、`[Icon]` 这类前缀，方便自动生成 Component 字段。

## 坐标与布局规则

Figma 坐标采用左上角原点。Unity UGUI 中建议：

```text
anchorMin = (0, 1)
anchorMax = (0, 1)
pivot = (0, 1)
anchoredPosition = (figmaX, -figmaY)
sizeDelta = (figmaWidth, figmaHeight)
```

所有 Figma 绝对坐标先映射到 `1080 x 1920` 固定设计稿容器中，再由外层 fitter 缩放到实际屏幕。不要直接把每个子节点拉伸到屏幕比例，否则容易变形和错位。

## 文本规则

1. 所有文字使用 `TextMeshProUGUI`。
2. 字体优先使用项目已有 `EditorTMPTextFactory.GetUIFont()`。
3. 字号、颜色、粗细尽量还原 Figma。
4. 文本 GameObject 命名使用：

```text
[Text]UserName
[Text]StatusTime
[Text]GeneralSettings
```

5. 文本默认不接收 raycast。

## 图片与图标规则

1. 图标和头像使用 `Image`。
2. 导入设置必须是 Sprite：

```text
TextureImporterType.Sprite
SpriteImportMode.Single
alphaIsTransparency = true
mipmapEnabled = false
```

3. 圆角图片和圆角面板优先使用项目内：

```text
Nobi.UiRoundedCorners.ImageWithRoundedCorners
```

4. 已存在于项目内的图标应优先复用，不重复下载。

## Button 规则

按钮 GameObject 命名使用：

```text
[Button]AddFriend
[Button]SearchFriend
[Button]FriendTab
[Button]MineTab
[Button]LogOut
```

按钮必须满足：

- 有 `Button` 组件。
- 有可 raycast 的 `targetGraphic`。
- 内部图标和文字是独立子节点。
- 不使用整图透明热区代替组件。

底部 Tab 可以只生成按钮组件，不强行绑定业务导航逻辑。导航逻辑由项目现有 UI 框架接入。

## Builder 规则

生成 prefab 应通过一次性 Editor Builder 完成。生成脚本也统一放在：

```text
Assets/GameData/AIUI/
```

如果该脚本需要作为 Unity Editor 脚本编译，应放在 `AIUI` 下的 `Editor` 子目录：

```text
Assets/GameData/AIUI/Editor/ExampleUiPrefabBuilder.cs
```

Builder 必须做到：

- 可重复运行。
- 输出路径固定。
- 自动配置 sprite import settings。
- 自动创建目录。
- 自动删除废弃的旧 prefab。
- 生成后 `AssetDatabase.SaveAssets()` 和 `AssetDatabase.Refresh()`。
- 如果 Builder 只是本次 Figma 转 prefab 的一次性生成脚本，生成和验证完成后必须删除，不应作为正式工程代码长期保留。

Builder 不允许修改正式运行配置，例如：

```text
Assets/Resources/WindowConfig.asset
Assets/GamerFrameWork/UIFrameWork/Config/UISetting.asset
Assets/AssetBundleCollectorSetting.asset
```

`AIUI` 下的临时 prefab 不自动注册窗口名、不加入正式 UI 路径、不加入 AssetBundle/YooAsset 收集规则。等用户二次加工完成并移动到正式目录后，再由人工接入正式配置。

菜单入口示例：

```csharp
[MenuItem("Tools/UI/Build ExampleUi")]
```

## 生成校验规则

### 质量基线

生成结果不能只满足“有 prefab、有组件、有文字”就算完成。必须至少达到同项目中已有高质量生成样例的标准，例如：

```text
Assets/GameData/UI/Main/Friend/ExampleFriendUI.prefab
Assets/GameData/UI/Main/My/ExampleMineUI.prefab
```

如果当前样例文件已被清理，则以同批次截图或上一次验收通过的 prefab 结构为质量基线。

必须检查：

- 是否沿用 `UITemp.prefab` 的根节点、`UIContent`、安全区适配和缩放策略。
- 是否有清晰的页面容器、内容容器、字段组、按钮组，而不是所有节点平铺在根下。
- 是否使用真实 UI 组件还原 Figma，而不是手机壳、整页截图、临时白块或占位图。
- 是否保留足够的视觉细节：圆角、间距、状态栏、顶部标题、头像、输入框、下拉箭头、底部 Home indicator。
- 是否在 Unity 中实际预览过，文本、图片、圆角、按钮位置都正常显示。
- 如果新生成结果明显弱于已验收样例，必须回退重做，不能交付。

生成后必须检查：

```bash
rg -n "Screenshot|FullPage|Friend\\.png|Mine\\.png" Assets/GameData/AIUI
rg -n "m_Script: \\{fileID: 0\\}" Assets/GameData/AIUI
rg -n "FriendRequestRow|MyInvitationRow" Assets/GameData/AIUI/Friend/ExampleFriendUI.prefab
rg -n "RecentFeatures|PrimarySettingsGroup" Assets/GameData/AIUI/My/ExampleMineUI.prefab
```

必须满足：

- prefab 中没有 `Screenshot` 层。
- prefab 中没有完整页面 PNG 引用。
- prefab 中没有 `m_Script: {fileID: 0}`。
- Friend prefab 不包含 Mine 页面专属组件。
- Mine prefab 不包含 Friend 页面专属组件。
- 所有新脚本都有 `.meta`，且 prefab 引用的 GUID 正确。
- 正式配置中没有 `Assets/GameData/AIUI` 引用。

## Unity 被占用时的处理规则

如果当前 Unity Editor 正在打开同一项目，batchmode 可能失败：

```text
Multiple Unity instances cannot open the same project.
```

这时不要强行关闭用户当前 Editor。可以复制临时工程到 `/tmp`，带上：

```text
Assets
Packages
ProjectSettings
Library/PackageCache
Library/PackageManager
```

在临时工程中 batchmode 生成 prefab，验证成功后再把 prefab 拷回原项目。

## 缓存清理规则

生成并验证完成后，必须清理本次生成产生的临时缓存、中间文件和一次性生成代码。

必须清理：

```text
/tmp/<temporary-unity-project>
/tmp/<temporary-build-log>.log
Temp/<one-shot-build-request>
Assets/GameData/AIUI/Editor/<TemporaryPrefabBuilder>.cs
Assets/GameData/AIUI/Editor/<TemporaryPrefabBuilder>.cs.meta
```

如果使用临时 Unity 工程生成 prefab，拷回正式工程并完成验证后，应删除整个临时工程目录。不要把临时工程、临时日志、旧 request 文件留在工作区或 `/tmp` 中。

如果生成时创建了类似下面的一次性 Builder：

```text
Assets/GameData/AIUI/Editor/ExampleUiPrefabBuilder.cs
```

在 prefab 已生成、验证通过、并确认不需要继续复跑后，必须删除这个 Builder 脚本和对应 `.meta`。只有当它被明确设计为长期维护的正式生成器时，才允许保留，并且需要改成稳定业务命名，避免留下 `Example`、`Temporary`、`OneShot` 这类临时命名。

## 最终交付清单

每次从 Figma 生成 prefab，最终至少交付：

- 一个或多个独立 prefab。
- prefab 必须基于 `Assets/GamerFrameWork/UIFrameWork/TempPrefabs/UITemp.prefab` 生成。
- 对应导出的独立 sprite 资源。
- 每个 prefab 对应的两个运行时脚本：`<ScreenName>UI.cs` 和 `<ScreenName>UIComponent.cs`。
- 简短验证结果。

示例：

```text
Assets/GameData/AIUI/Friend/ExampleFriendUI.prefab
Assets/GameData/AIUI/Friend/ExampleFriendUI.cs
Assets/GameData/AIUI/Friend/ExampleFriendUIComponent.cs
Assets/GameData/AIUI/My/ExampleMineUI.prefab
Assets/GameData/AIUI/My/ExampleMineUI.cs
Assets/GameData/AIUI/My/ExampleMineUIComponent.cs
Assets/GameData/AIUI/UI/ExampleUi/Generated/
```

## 禁止事项总结

1. 禁止整页截图当 UI。
2. 禁止多个主界面塞进一个 prefab。
3. 禁止透明热区代替真实组件。
4. 禁止丢失脚本引用。
5. 禁止没有安全区适配。
6. 禁止生成无法复跑的手工 prefab。
7. 禁止脱离 `UITemp.prefab` 重新创建 UI 根节点。
8. 禁止把临时 Figma asset URL 写进 prefab 或代码。
9. 禁止生成完成后遗留临时缓存、临时工程或旧构建 request。
10. 禁止把一次性 prefab 生成脚本长期留在工程中，例如 `Assets/GameData/AIUI/Editor/ExampleUiPrefabBuilder.cs`。

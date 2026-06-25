# Unity UI 双端屏幕适配制作方案

## 目标

本方案用于 MoonlyApp 的 Unity UGUI 界面制作，目标是让同一套 UI prefab 可以稳定适配 iOS 和 Android 的大多数手机机型。

重点不是给每个界面单独写适配代码，而是在 prefab 制作阶段通过 CanvasScaler、RectTransform 锚点、Layout 组件、ScrollRect 和安全区预留，把界面做成天然可伸缩的结构。

## 适配范围

优先覆盖竖屏手机：

- 常规比例：16:9、18:9、19.5:9、20:9、21:9。
- 常见分辨率：720x1280、720x1600、1080x1920、1080x2340、1080x2400、1170x2532、1242x2688、1440x3200。
- iOS 特殊区域：刘海屏、Dynamic Island、底部 Home Indicator。
- Android 特殊区域：挖孔屏、水滴屏、顶部状态栏、底部虚拟导航栏或手势导航栏。

暂不把平板作为主目标。如果后续要支持 iPad 或 Android 平板，需要额外做横向留白和最大内容宽度策略。

## 基础原则

1. UI 以 1080x1920 作为竖屏设计基准。
2. Canvas 使用 `Scale With Screen Size`。
3. 大部分竖屏界面推荐 `Reference Resolution = 1080x1920`，`Match = 0`，优先按宽度缩放，避免窄屏横向挤压。
4. 背景、遮罩、滚动区域、底部导航必须使用拉伸锚点。
5. 头部按钮、标题、底部按钮必须留出安全区距离。
6. 长内容必须进入 ScrollRect，不能依赖固定高度硬塞。
7. 卡片内部可以使用固定尺寸，但卡片所在容器要能随屏幕高度滚动。
8. 装饰元素可以固定位置，但不能承担主要布局职责。

## 推荐层级

每个全屏 UI prefab 推荐使用下面的层级结构：

```text
UIName
├── UIMask
├── UIContent
│   ├── Background
│   ├── TopShade
│   ├── TopBar
│   │   ├── [Button]Back
│   │   └── Title
│   ├── MainScroll
│   │   └── Viewport
│   │       └── Content
│   ├── BottomBar
│   └── FloatingLayer
└── PopupLayer
```

说明：

- `UIMask`：全屏遮罩，锚点拉满。
- `UIContent`：主内容根节点，锚点拉满。
- `Background`：背景图或背景色，锚点拉满。
- `TopShade`：顶部渐隐或遮罩，横向拉伸，顶部锚点。
- `TopBar`：返回按钮、标题、分享按钮等顶部控件。
- `MainScroll`：页面主体内容，锚点上下左右拉伸。
- `BottomBar`：底部导航或固定主按钮，底部锚点，横向拉伸。
- `FloatingLayer`：悬浮按钮、提示、角标。
- `PopupLayer`：弹窗、滚动选择器、上传头像面板。

## CanvasScaler 规范

根 Canvas 推荐配置：

```text
UI Scale Mode: Scale With Screen Size
Reference Resolution: 1080 x 1920
Screen Match Mode: Match Width Or Height
Match: 0
Reference Pixels Per Unit: 100
```

为什么推荐 `Match = 0`：

- 竖屏 App 的横向空间更紧张，按宽度缩放可以保证卡片、按钮、中文文本不被压窄。
- 高屏设备会多出垂直空间，正好交给 ScrollRect、顶部留白、底部留白吸收。
- 如果按高度或 0.5 缩放，部分高屏设备上横向内容容易超出屏幕。

例外情况：

- 如果是纯弹窗、小工具面板、横向居中的轻量界面，可以使用独立 Canvas 或局部容器控制最大宽度。
- 如果未来支持平板，需要给主内容加最大宽度容器，而不是直接放大到全屏宽度。

## RectTransform 锚点规范

### 全屏根节点

`UIContent`、`UIMask`、背景图、遮罩层：

```text
Anchor Min: 0, 0
Anchor Max: 1, 1
Left: 0
Right: 0
Top: 0
Bottom: 0
Pivot: 0.5, 0.5
```

这些节点必须跟随屏幕完整拉伸。

### 顶部栏

顶部栏容器：

```text
Anchor Min: 0, 1
Anchor Max: 1, 1
Height: 180 到 240
Top: 0
Pivot: 0.5, 1
```

顶部控件建议位置：

- 返回按钮：左上锚点，`x = 72`，`y = -110 到 -130`。
- 标题：顶部居中锚点，`y = -110 到 -130`。
- 分享/通知按钮：右上锚点，`x = -72`，`y = -110 到 -130`。

这个范围可以覆盖大多数刘海屏和状态栏。视觉上如果标题太低，可以把顶部背景装饰往下延展，而不是把按钮贴到屏幕最上方。

### 主滚动区域

`MainScroll` 推荐拉伸：

```text
Anchor Min: 0, 0
Anchor Max: 1, 1
Left: 0
Right: 0
Top: 220 到 280
Bottom: 40 到 220
Pivot: 0.5, 0.5
```

取值原则：

- 没有底部导航的详情页：`Bottom = 40 到 80`。
- 有底部导航的主界面：`Bottom = 180 到 240`。
- 顶部有大标题或装饰头图：`Top = 240 到 360`。
- 顶部只有小标题：`Top = 160 到 220`。

`Viewport` 必须拉满 `MainScroll`。

`Content` 推荐：

```text
Anchor Min: 0, 1
Anchor Max: 1, 1
Left: 60 到 90
Right: 60 到 90
Top: 0
Pivot: 0.5, 1
```

并添加：

- `VerticalLayoutGroup`
- `ContentSizeFitter`
- 必要时给子节点添加 `LayoutElement`

### 底部导航

底部导航容器：

```text
Anchor Min: 0, 0
Anchor Max: 1, 0
Height: 150 到 190
Bottom: 30 到 60
Pivot: 0.5, 0
```

如果底部导航带大圆角背景，背景图横向拉伸，内容图标均匀分布。底部不要贴边，至少留出 30 到 60 的手势条空间。

### 固定底部主按钮

如果界面底部有固定主按钮：

```text
Anchor Min: 0.5, 0
Anchor Max: 0.5, 0
Width: 760 到 900
Height: 80 到 110
Y: 70 到 120
```

如果同时有底部导航，主按钮不要固定在底部，应该放进 `MainScroll/Content` 或者放在导航上方单独预留区域。

## 安全区策略

优先级从高到低：

1. prefab 锚点和偏移预留安全距离。
2. 共用 SafeArea 根节点统一处理刘海和底部手势区。
3. 单个特殊界面再做局部适配。

制作 prefab 时，即使后续会有 SafeArea 组件，也不能把按钮贴到屏幕边缘。安全区组件只能兜底，不能替代正确的锚点。

推荐安全距离，以 1080x1920 设计基准计算：

```text
顶部可交互控件中心 Y: -110 到 -130
顶部装饰遮罩高度: 170 到 240
底部导航底边: 30 到 60
底部主按钮中心 Y: 70 到 120
滚动内容底部留白: 40 到 80
```

## 卡片和列表制作规范

卡片容器：

- 宽度建议 860 到 920。
- 圆角、边框、底图可以固定尺寸。
- 卡片放进 `VerticalLayoutGroup` 时，用 `LayoutElement` 控制高度。
- 卡片内文本区域用顶部或左侧锚点，不要全靠中心点堆坐标。

列表和长内容：

- 内容超过一屏时必须使用 ScrollRect。
- ScrollRect 的 `Content` 必须使用顶部锚点。
- 最后一项下面至少留 40 到 80 的 padding，避免被手势条或底部按钮遮住。
- 不要让 ScrollRect 和固定底部按钮互相覆盖。

## 文本适配规范

中文 UI 最容易在小屏上出问题，制作时按下面规则处理：

- 标题类文本给足宽度，不要用过窄的 `SizeDelta`。
- 按钮文字最多 6 到 8 个中文，超过要降字号或换文案。
- 正文必须开启自动换行。
- 多语言文本或动态文本区域要预留 20% 到 30% 的高度余量。
- 文本不要使用负字距。
- 关键按钮文字不能依赖 Best Fit 硬压，优先调整按钮宽度和字号。

推荐字号，以 1080x1920 设计基准计算：

```text
大标题: 42 到 56
页面标题: 36 到 46
卡片标题: 28 到 36
正文: 24 到 30
辅助说明: 20 到 26
底部导航文字: 22 到 28
```

## 图片和装饰适配

背景图：

- 必须锚点拉满。
- 建议使用可裁切背景，不要把重要人物、文字、按钮画进背景边缘。
- 竖屏背景图建议准备 1080x2400 或更高比例，让高屏有裁切空间。

头像、卡牌、图标：

- 头像使用固定正方形容器，内部 Image 开启 Preserve Aspect。
- 卡牌使用固定比例容器，必要时加 `AspectRatioFitter`。
- 图标点击区域不能只等于图标大小，按钮热区建议不小于 72x72。

装饰线、星星、边角：

- 可在卡片内部固定坐标。
- 不要作为布局基准。
- 横向分割线可以用左右拉伸锚点。

## 弹窗和滚动选择器

弹窗遮罩：

```text
Anchor Min: 0, 0
Anchor Max: 1, 1
Offset: 0
```

居中弹窗：

```text
Anchor Min: 0.5, 0.5
Anchor Max: 0.5, 0.5
Width: 760 到 920
Height: 根据内容设置
Pivot: 0.5, 0.5
```

底部弹窗：

```text
Anchor Min: 0, 0
Anchor Max: 1, 0
Bottom: 0 到 40
Height: 600 到 900
Pivot: 0.5, 0
```

滚动选择器注意：

- 年月日、时分、国家地区这类选择器不要直接固定到屏幕边缘。
- 弹窗宽度不要超过 920。
- 列之间用 LayoutGroup 分配空间。
- 确认按钮底部留出 40 到 80。

## iOS 和 Android 差异注意

iOS：

- 顶部刘海和 Dynamic Island 会占用标题区域。
- 底部 Home Indicator 会遮挡贴边按钮。
- iPhone SE 这类短屏高度少，内容必须可滚动。

Android：

- 不同品牌状态栏高度差异明显。
- 有些机型底部是三键导航，有些是手势导航。
- 挖孔位置可能在中间或左上，顶部栏不要把关键按钮放得太靠上。
- 部分 Android 机型比例接近 20:9 或 21:9，高度多，背景必须能延展。

共同策略：

- 顶部交互控件不要贴边。
- 底部交互控件不要贴边。
- 主内容用 ScrollRect 吸收高度差。
- 横向宽度以 1080 基准锁住，不让内容在窄屏被挤坏。

## 测试分辨率清单

每个新 UI prefab 做完后，至少在 Game View 或 Device Simulator 验下面几组：

```text
720x1280   16:9      小 Android
1080x1920  16:9      标准设计基准
750x1334   16:9      小 iPhone
1080x2340  19.5:9    常见 Android
1170x2532  19.5:9    常见 iPhone 刘海屏
1080x2400  20:9      常见 Android 长屏
1440x3200  20:9      高分 Android
```

必须检查：

- 顶部返回按钮不被状态栏遮挡。
- 标题不碰刘海区域。
- 底部导航不被手势条遮挡。
- 主按钮可见且可点击。
- ScrollRect 能滚到最后一项。
- 背景没有露边。
- 中文文本没有截断、重叠或溢出。
- 弹窗在小屏上不会超出屏幕。

## Prefab 制作流程

1. 先定界面类型：主界面、详情页、弹窗、滚动选择器。
2. 创建标准层级：`UIMask`、`UIContent`、`TopBar`、`MainScroll`、`BottomBar`。
3. 设置 CanvasScaler，确认使用 1080x1920 基准。
4. 先设置所有大容器锚点，再摆放具体按钮和文字。
5. 背景、遮罩、ScrollRect、底部导航全部使用拉伸锚点。
6. 头部和底部控件按安全距离摆放。
7. 长内容加入 ScrollRect、VerticalLayoutGroup、ContentSizeFitter。
8. 在 7 组测试分辨率下检查。
9. 截图记录异常，回 prefab 调锚点，不优先写代码补丁。
10. 最后再接业务脚本和按钮事件。

## MoonlyApp 当前推荐标准

结合当前项目的视觉风格，建议后续全屏 UI 统一使用：

```text
设计基准: 1080x1920
Canvas Match: 0
顶部按钮中心 Y: -116
顶部标题中心 Y: -116
顶部遮罩: 横向拉伸，高度 170 到 220
MainScroll Top: 220 到 280
MainScroll Bottom: 无底部导航 40 到 80，有底部导航 180 到 240
Content 左右边距: 60 到 90
卡片宽度: 890 到 900
底部导航高度: 160 到 180
底部导航 Bottom: 40 到 60
```

例如 `DivinationInfoUI` 这类详情页：

- `UIContent` 拉满全屏。
- `TopShade` 横向拉伸。
- `Title` 和 `[Button]Back` 顶部锚点，中心 Y 约为 -116。
- `MainScroll` 四向拉伸，上方避开标题，下方留 40 到 80。
- 详情内容放进 `MainScroll/Viewport/Content`，通过 LayoutGroup 自然撑高。

例如 `CreateFriendUI` 这类表单页：

- 顶部返回和标题固定在安全区内。
- 头像区可以固定在顶部内容区域。
- 表单项放进 ScrollRect。
- 创建按钮如果固定底部，需要给 ScrollRect 留出对应 Bottom。
- 如果有底部导航，创建按钮建议放进 ScrollRect 内容里，避免和导航争空间。

例如 `SpinPickerUI` 这类选择器：

- 遮罩拉满全屏。
- Picker 面板居中或底部弹出。
- 面板最大宽度控制在 760 到 920。
- 选择列使用 LayoutGroup 横向分布。
- 确认按钮底部留安全距离。

## 验收标准

一个 UI prefab 可以认为完成适配，需要满足：

- 在 16:9 到 21:9 的竖屏手机比例下没有关键控件被遮挡。
- 顶部和底部交互区都避开系统安全区。
- 小屏设备上所有内容可以通过滚动访问。
- 大屏设备上背景和遮罩能铺满，不露边。
- 横向内容不超屏，按钮文字不截断。
- 弹窗和选择器不会因为屏幕高度变化而不可操作。
- 不依赖每个界面单独写适配代码。

## 常见错误

- 把整页 UI 固定成 1080x1920，不使用拉伸锚点。
- 背景图固定尺寸，长屏设备上下露边。
- 返回按钮和标题贴近屏幕最上方。
- 底部按钮贴边，被 iPhone Home Indicator 或 Android 手势条挡住。
- 详情内容不用 ScrollRect，小屏设备下按钮被挤出屏幕。
- ScrollRect 的 Content 用中心锚点，导致内容高度变化时位置漂移。
- 卡片内部所有元素都用屏幕级坐标，换分辨率后整体错位。
- 为每个界面单独写位置适配，后续维护成本变高。

## 后续执行建议

后面每次新做或重构 UI prefab，都按这个顺序处理：

1. 先按标准层级搭 prefab。
2. 只调 RectTransform 和 Layout 组件完成第一版适配。
3. 再接业务脚本。
4. 最后用测试分辨率清单跑一遍。

如果某个界面确实因为系统安全区需要动态处理，应该做一个项目级共用 SafeArea 组件挂在统一根节点，而不是在每个 UI 脚本里分别写一套适配逻辑。

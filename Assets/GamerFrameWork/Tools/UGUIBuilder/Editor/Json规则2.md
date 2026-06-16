UGUI Builder JSON Schema 参考本文档描述 UGUI Builder 工具接受的 JSON 布局格式。
给 AI 的提示：请严格遵循本文档的字段名、枚举值和嵌套规则生成 JSON。字段名大小写敏感。
特别注意：所有涉及文本的组件（Text、Button、InputField 等），其字体颜色（textColor）必须严格观察并跟随用户提供的贴图/UI设计稿中的实际颜色进行提取，切勿直接盲目使用默认的纯黑或纯白。  JSON 根结构JSON{
  "screenName": "string（必填，Prefab名）",
  "resolution": { "x": 1080, "y": 1920 },
  "matchWidthOrHeight": 0.1,
  "includeMask": true,
  "maskColor": { "r": 0, "g": 0, "b": 0, "a": 0.67 },
  "defaultFont": "Assets/...（可选字体路径）",
  "elements": [ /* LayoutElement 数组 */ ]
}
字段类型必填默认值说明screenNamestring是-Prefab 名称  resolutionVector2Data否{x:750,y:1334}设计分辨率  matchWidthOrHeightfloat否0.5CanvasScaler 适配权重  includeMaskbool否true是否生成 UIMask 遮罩层  maskColorColorData否{r:0,g:0,b:0,a:0.67}遮罩颜色  defaultFontstring否""Legacy 字体路径（窗口字体可覆盖）  elementsarray是-根级元素列表  通用字段（所有元素支持）JSON{
  "name": "MyElement",
  "type": "Panel",
  "anchorMin": { "x": 0, "y": 0 },
  "anchorMax": { "x": 1, "y": 1 },
  "position": { "x": 0, "y": 0 },
  "size": { "x": 100, "y": 100 },
  "pivot": { "x": 0.5, "y": 0.5 },
  "color": { "r": 1, "g": 1, "b": 1, "a": 1 },
  "raycastTarget": true,
  "interactable": true,
  "children": []
}
字段类型默认值说明namestring-元素名称（同一父级下不可重复）。需代码操作的对象请遵循 [组件名字]名字 的格式typestring-元素类型，见下方类型表  anchorMinVector2Data{x:0.5,y:0.5}锚点最小值  anchorMaxVector2Data{x:0.5,y:0.5}锚点最大值  positionVector2Data{x:0,y:0}锚点偏移位置  sizeVector2Data{x:100,y:100}宽高  pivotVector2Data{x:0.5,y:0.5}轴心点  colorColorData{r:1,g:1,b:1,a:1}RGBA (0~1)  raycastTargetbooltrue是否响应射线  interactablebooltrue是否可交互  childrenarraynull子元素列表  辅助类型定义JSON// ColorData - RGBA 分量，范围 0.0 ~ 1.0
// 默认值：纯黑 { "r": 0, "g": 0, "b": 0, "a": 1 }
{ "r": 0.5, "g": 0.2, "b": 0.8, "a": 1.0 }

// Vector2Data
{ "x": 100, "y": 200 }
元素类型一览（8种）type 值用途子元素Panel纯容器 / 色块可含子元素  Image图片显示无  Button按钮无  Text文本显示无  Toggle开关无（内部自建 Background/Checkmark/Label）  ToggleGroup互斥 Toggle 组放 Toggle 子元素  ScrollRect滚动区域放可滚动内容  InputField输入框无（内部自建 Text/Placeholder）  各类型专属字段1. Panel继承自 Image，没有额外字段。仅通过 color 设置背景色。  JSON{
  "name": "HeaderPanel",
  "type": "Panel",
  "color": { "r": 0.1, "g": 0.1, "b": 0.1, "a": 1 },
  "anchorMin": { "x": 0, "y": 1 },
  "anchorMax": { "x": 1, "y": 1 },
  "size": { "x": 0, "y": 80 },
  "children": [{ "name": "Title", "type": "Text", "text": "标题" }]
}
2. Image字段类型默认值说明spritestring""Sprite 文件名（不含扩展名）  imageTypestring"Simple"Simple / Sliced / Tiled / Filled  fillMethodstring"Horizontal"仅 imageType=Filled 时生效  fillAmountfloat1.0填充量 (0~1)，仅 Filled 时生效  fillOriginint0填充起始位置，仅 Filled 时生效  imageType 枚举值： "Simple" / "Sliced" / "Tiled" / "Filled"  fillMethod 枚举值（仅 imageType=Filled 时使用）： "Horizontal" / "Vertical" / "Radial90" / "Radial180" / "Radial360"  3. Button无额外字段。sprite 设为按钮底色，text 设为按钮文字（自动创建 Text 子对象）。  JSON{
  "name": "[Button]Confirm",
  "type": "Button",
  "sprite": "btn_bg",
  "text": "确认",
  "textColor": { "r": 1, "g": 1, "b": 1, "a": 1 },
  "fontSize": 24,
  "size": { "x": 200, "y": 60 }
}
4. Text字段类型默认值说明textstring""显示文本  fontSizeint14字号  fontStylestring"Normal"Normal / Bold / Italic / BoldItalic  textAlignmentstring"Center"文本对齐  textColorColorData-文字颜色。必须严格根据贴图/设计稿中实际的字体颜色提取（转换为 0~1 的 RGBA 值）outlineColorColorDatanull描边颜色。同样需观察贴图是否有描边并取色（有值才添加 Outline 组件）outlineDistanceVector2Data{x:1,y:-1}描边偏移  useTMPboolfalse是否使用 TextMeshPro 渲染  textAlignment 枚举值： "Left" / "Center" / "Right" / "TopLeft" / "Top" / "TopRight" / "BottomLeft" / "Bottom" / "BottomRight"  fontStyle 枚举值： "Normal" / "Bold" / "Italic" / "BoldItalic"  5. Toggle字段类型默认值说明isOnboolfalse初始选中状态  backgroundSpritestring""背景 Sprite 名  checkmarkSpritestring""选中标记 Sprite 名  textstring""标签文字  fontSizeint14标签字号  outlineColorColorDatanull文字描边颜色  outlineDistanceVector2Data{x:1,y:-1}描边偏移  6. ToggleGroup字段类型默认值说明allowSwitchOffboolfalse是否允许全部取消选中  spacingfloat0Toggle 间距  paddingLeftint0左内边距  paddingRightint0右内边距  paddingTopint0上内边距  paddingBottomint0下内边距  必须将 Toggle 放在 ToggleGroup 的 children 中。 ToggleGroup 自动生成 HorizontalLayoutGroup。  7. ScrollRect字段类型默认值说明horizontalScrollbooltrue水平滚动  verticalScrollbooltrue垂直滚动  scrollMovementTypestring"Clamped"Clamped / Unrestricted / Elastic  scrollInertiabooltrue滚动惯性  scrollSensitivityfloat20滚动灵敏度  layoutTypestring"None"Content 子元素的布局方式  spacingfloat0子元素间距  paddingLeft / Right / Top / Bottomint0Content 内边距  ScrollRect 的 children 会自动放置在 Viewport/Content 下，而不是直接在 ScrollRect 下。  
如果设置了 layoutType，Content 还会自动添加 ContentSizeFitter。  8. InputField字段类型默认值说明textstring""默认文本  fontSizeint14字号  textColorColorData-输入文字颜色。必须严格根据贴图/设计稿中实际的字体颜色提取placeholderTextstring"Enter text..."占位提示文字  characterLimitint0字数限制（0=无限制）  contentTypestring"Standard"输入内容类型  lineTypestring"SingleLine"行类型  useTMPboolfalse是否使用 TMP_InputField  spritestring""输入框背景 Sprite（会自动设为 Sliced）  contentType 枚举值： "Standard" / "Integer" / "Decimal" / "AlphaNumeric" / "Name" / "EmailAddress" / "Password" / "Pin" / "Custom"  lineType 枚举值： "SingleLine" / "MultiLineSubmit" / "MultiLineNewline"  LayoutGroup 通用字段任何元素都可以附加 LayoutGroup，通过以下字段控制：  字段类型默认值说明layoutTypestringnullNone / Horizontal / Vertical / Grid  childAlignmentstring"MiddleCenter"子元素对齐方式  spacingfloat0子元素间距  paddingLeftint0左内边距  paddingRightint0右内边距  paddingTopint0上内边距  paddingBottomint0下内边距  childControlWidthboolfalse控制子元素宽度  childControlHeightboolfalse控制子元素高度  childForceExpandWidthbooltrue强制扩展子宽度  childForceExpandHeightbooltrue强制扩展子高度  嵌套规则Canvas
├── UIMask（可选，由 includeMask 控制）
└── UIContent
    └── root elements[] ← 您的元素从这里开始
        ├── Panel / ToggleGroup / ScrollRect → 可以有 children
        ├── Image / Button / Text / Toggle / InputField → 不能有 children
        └── children 可以无限嵌套
重要限制：Button、Text、Toggle、Image、InputField 类型不支持 children  Panel、ToggleGroup、ScrollRect 类型的 children 才会被递归构建  ScrollRect 的 children 放在 Content/Viewport 结构内，不要设置它们的 anchor  命名规范 (Naming Conventions)为了方便代码自动绑定和逻辑操作，UI 元素的 name 字段需要遵循以下规范：需要代码交互/修改的节点：必须使用 [组件名字]具体名字 的格式。示例：离开按钮：[Button]Exit账号文本：[Text]Account密码输入框：[InputField]Password主容器：[Panel]MainView纯静态展示节点：不需要代码操作的节点（如背景图、静态标题文字、修饰性元素等），可直接使用常规命名，无需加中括号前缀（如 Background, TitleText, LogoImage）。颜色值与贴图还原注意数值范围：ColorData 的 r / g / b / a 范围应为 0.0 ~ 1.0（不是 0~255）。  严格跟随贴图：字体颜色（textColor）：千万不要默认写死黑色 { "r": 0, "g": 0, "b": 0, "a": 1 } 或白色。必须观察参考贴图，提取准确的 RGB 颜色并转换为 0~1 的格式。背景与色块颜色（color）：Panel 背景、Image 染色等同理，需根据设计图实际颜色还原。JSON// 正确（取自贴图的实际深灰色字体）
"textColor": { "r": 0.2, "g": 0.2, "b": 0.2, "a": 1.0 }

// 错误（RGB超出范围，或未按贴图取色）
"textColor": { "r": 255, "g": 255, "b": 255, "a": 255 }
完整示例：一个经典登录页（包含代码绑定节点）JSON{
  "screenName": "LoginScreen",
  "resolution": { "x": 750, "y": 1334 },
  "matchWidthOrHeight": 0.5,
  "includeMask": false,
  "elements": [
    {
      "name": "Background",
      "type": "Panel",
      "color": { "r": 0.08, "g": 0.08, "b": 0.12, "a": 1 },
      "anchorMin": { "x": 0, "y": 0 },
      "anchorMax": { "x": 1, "y": 1 },
      "size": { "x": 0, "y": 0 },
      "children": [
        {
          "name": "Logo",
          "type": "Image",
          "sprite": "app_logo",
          "size": { "x": 120, "y": 120 },
          "position": { "x": 0, "y": 100 }
        },
        {
          "name": "TitleText",
          "type": "Text",
          "text": "欢迎登录",
          "fontSize": 36,
          "fontStyle": "Bold",
          "textAlignment": "Center",
          "textColor": { "r": 0.8, "g": 0.8, "b": 0.8, "a": 1 },
          "position": { "x": 0, "y": 0 }
        },
        {
          "name": "[InputField]Account",
          "type": "InputField",
          "placeholderText": "请输入手机号",
          "contentType": "Integer",
          "characterLimit": 11,
          "fontSize": 22,
          "sprite": "input_bg",
          "size": { "x": 600, "y": 56 },
          "position": { "x": 0, "y": -80 }
        },
        {
          "name": "[InputField]Password",
          "type": "InputField",
          "placeholderText": "请输入密码",
          "contentType": "Password",
          "fontSize": 22,
          "sprite": "input_bg",
          "size": { "x": 600, "y": 56 },
          "position": { "x": 0, "y": -160 }
        },
        {
          "name": "[Button]Login",
          "type": "Button",
          "text": "登 录",
          "fontSize": 24,
          "textColor": { "r": 1, "g": 1, "b": 1, "a": 1 },
          "color": { "r": 0.27, "g": 0.55, "b": 0.91, "a": 1 },
          "size": { "x": 600, "y": 56 },
          "position": { "x": 0, "y": -260 }
        }
      ]
    }
  ]
}
常见错误错误正确写法"type": "Pannel""type": "Panel"  "type": "Label""type": "Text"  "imageType": "filled""imageType": "Filled"  "textAlignment": "MiddleLeft""textAlignment": "Left"  "contentType": "Number""contentType": "Integer"  color.r=255color.r=1.0  Button 下放 childrenButton 不支持 children，用 Panel + children  ScrollRect 不设置 layoutType列表内容推荐设 Vertical + spacing  文本节点使用默认颜色需严格从参考图/贴图获取并转为 0~1 的色值交互节点命名未使用前缀使用类似 [Button]Exit 包含组件名字的格式

---

## UI 交互层代码规范（NavigationUI 及窗口管理）

以下规则适用于 UGUI Builder 生成 JSON 布局之后，编写交互层 C# 代码时必须遵守的规范。

### 1. 导航栏防重复点击

当使用 Toggle 实现导航栏切换时，同一导航项被反复点击会触发重复 PopUpWindow 调用。
必须通过 `mCurrentActiveWindow` 字段进行防重复保护，模式如下：

```csharp
private string mCurrentActiveWindow = "";

public void OnTabToggleChange(bool state, Toggle toggle)
{
    if (state)
    {
        // 当前已激活的窗口与要弹出的窗口相同 → 直接跳过，防止重复弹出
        if (mCurrentActiveWindow == nameof(TargetUI)) return;
        mCurrentActiveWindow = nameof(TargetUI);
        UIModule.Instance.PopUpWindow<TargetUI>();
    }
    else
    {
        // 关闭时清空记录（仅当记录的就是该窗口时才清空）
        if (mCurrentActiveWindow == nameof(TargetUI)) mCurrentActiveWindow = "";
        UIModule.Instance.HideWindow<TargetUI>();
    }
}
```

**要点**：
- `mCurrentActiveWindow` 必须在 OnAwake 初始化时同步设置默认值（与默认选中 Toggle 对应）
- 切换导航时，Unity ToggleGroup 的回调顺序：新 Toggle(true) → 旧 Toggle(false)，mCurrentActiveWindow 会在 true 分支中被更新，false 分支不会误清空
- 每个导航 Toggle 回调都必须包含此 guard 逻辑，不可遗漏

### 2. UIModule 窗口查询 API

| 方法 | 返回值 | 说明 |
|------|--------|------|
| `GetTopWindow()` | `WindowBase` | 获取当前最顶层打开的界面（mVisibleWindowList 最后一个） |
| `GetPreviousVisibleWindow()` | `WindowBase` | 获取上一个当前打开的界面（mVisibleWindowList 倒数第二个） |
| `GetWindow<T>()` | `T` | 在可见窗口中按类型查找（已存在） |

**使用场景**：
- 关闭当前窗口后需要刷新上一个窗口：`UIModule.Instance.GetPreviousVisibleWindow()`
- 需要对当前最顶层窗口做操作：`UIModule.Instance.GetTopWindow()`
- 不足 2 个可见窗口时 `GetPreviousVisibleWindow()` 返回 null，调用方需做 null 检查

### 3. 禁止在 Toggle 回调中直接调用 GetTopWindow().HideWindow()

```csharp
// ❌ 错误：GetTopWindow() 返回的是当前最顶层窗口，可能与预期不符
UIModule.Instance.GetTopWindow().HideWindow();

// ✅ 正确：明确指定要隐藏的窗口类型
UIModule.Instance.HideWindow<MyUI>();
```

`GetTopWindow()` 仅用于查询/操作当前最顶层窗口，不应作为隐藏窗口的替代手段。隐藏窗口应使用 `HideWindow<T>()` 或 `HideWindow(string)` 明确指定目标。

文档版本：1.2 — 新增 UI 交互层代码规范（导航栏防重复点击、窗口查询 API、GetTopWindow 使用限制）
# UGUI Builder JSON Schema 参考

> 本文档描述 UGUI Builder 工具接受的 JSON 布局格式。
> **给 AI 的提示：请严格遵循本文档的字段名、枚举值和嵌套规则生成 JSON。字段名大小写敏感。**

---

## JSON 根结构

```json
{
  "screenName": "string（必填，Prefab名）",
  "resolution": { "x": 750, "y": 1334 },
  "matchWidthOrHeight": 0.5,
  "includeMask": true,
  "maskColor": { "r": 0, "g": 0, "b": 0, "a": 0.67 },
  "defaultFont": "Assets/...（可选字体路径）",
  "elements": [ /* LayoutElement 数组 */ ]
}
```

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `screenName` | string | 是 | - | Prefab 名称 |
| `resolution` | Vector2Data | 否 | `{x:750,y:1334}` | 设计分辨率 |
| `matchWidthOrHeight` | float | 否 | 0.5 | CanvasScaler 适配权重 |
| `includeMask` | bool | 否 | true | 是否生成 UIMask 遮罩层 |
| `maskColor` | ColorData | 否 | `{r:0,g:0,b:0,a:0.67}` | 遮罩颜色 |
| `defaultFont` | string | 否 | `""` | Legacy 字体路径（窗口字体可覆盖） |
| `elements` | array | 是 | - | 根级元素列表 |

---

## 通用字段（所有元素支持）

```json
{
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
```

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `name` | string | - | 元素名称（同一父级下不可重复） |
| `type` | string | - | 元素类型，见下方类型表 |
| `anchorMin` | Vector2Data | `{x:0.5,y:0.5}` | 锚点最小值 |
| `anchorMax` | Vector2Data | `{x:0.5,y:0.5}` | 锚点最大值 |
| `position` | Vector2Data | `{x:0,y:0}` | 锚点偏移位置 |
| `size` | Vector2Data | `{x:100,y:100}` | 宽高 |
| `pivot` | Vector2Data | `{x:0.5,y:0.5}` | 轴心点 |
| `color` | ColorData | `{r:1,g:1,b:1,a:1}` | RGBA (0~1) |
| `raycastTarget` | bool | true | 是否响应射线 |
| `interactable` | bool | true | 是否可交互 |
| `children` | array | null | 子元素列表 |

### 辅助类型定义

```json
// ColorData - RGBA 分量，范围 0.0 ~ 1.0
{ "r": 0.5, "g": 0.2, "b": 0.8, "a": 1.0 }

// Vector2Data
{ "x": 100, "y": 200 }
```

---

## 元素类型一览（8种）

| type 值 | 用途 | 子元素 |
|---------|------|--------|
| `Panel` | 纯容器 / 色块 | 可含子元素 |
| `Image` | 图片显示 | 无 |
| `Button` | 按钮 | 无 |
| `Text` | 文本显示 | 无 |
| `Toggle` | 开关 | 无（内部自建 Background/Checkmark/Label） |
| `ToggleGroup` | 互斥 Toggle 组 | 放 Toggle 子元素 |
| `ScrollRect` | 滚动区域 | 放可滚动内容 |
| `InputField` | 输入框 | 无（内部自建 Text/Placeholder） |

---

## 各类型专属字段

### 1. Panel

> 继承自 Image，没有额外字段。仅通过 `color` 设置背景色。

```json
{
  "name": "HeaderPanel",
  "type": "Panel",
  "color": { "r": 0.1, "g": 0.1, "b": 0.1, "a": 1 },
  "anchorMin": { "x": 0, "y": 1 },
  "anchorMax": { "x": 1, "y": 1 },
  "size": { "x": 0, "y": 80 },
  "children": [{ "name": "Title", "type": "Text", "text": "标题" }]
}
```

### 2. Image

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `sprite` | string | `""` | Sprite 文件名（不含扩展名） |
| `imageType` | string | `"Simple"` | Simple / Sliced / Tiled / Filled |
| `fillMethod` | string | `"Horizontal"` | 仅 imageType=Filled 时生效 |
| `fillAmount` | float | 1.0 | 填充量 (0~1)，仅 Filled 时生效 |
| `fillOrigin` | int | 0 | 填充起始位置，仅 Filled 时生效 |

**imageType 枚举值：**

| 值 | 对应 Unity Image.Type |
|----|----------------------|
| `"Simple"` | Simple |
| `"Sliced"` | Sliced |
| `"Tiled"` | Tiled |
| `"Filled"` | Filled |

**fillMethod 枚举值（仅 imageType=Filled 时使用）：**

| 值 | 对应 Unity Image.FillMethod |
|----|---------------------------|
| `"Horizontal"` | Horizontal |
| `"Vertical"` | Vertical |
| `"Radial90"` | Radial90 |
| `"Radial180"` | Radial180 |
| `"Radial360"` | Radial360 |

```json
{
  "name": "ProgressBar",
  "type": "Image",
  "imageType": "Filled",
  "fillMethod": "Horizontal",
  "fillAmount": 0.75,
  "sprite": "progress_fill",
  "color": { "r": 0.27, "g": 0.85, "b": 0.39, "a": 1 }
}
```

### 3. Button

> 无额外字段。`sprite` 设为按钮底色，`text` 设为按钮文字（自动创建 Text 子对象）。

```json
{
  "name": "ConfirmBtn",
  "type": "Button",
  "sprite": "btn_bg",
  "text": "确认",
  "textColor": { "r": 1, "g": 1, "b": 1, "a": 1 },
  "fontSize": 24,
  "size": { "x": 200, "y": 60 }
}
```

### 4. Text

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `text` | string | `""` | 显示文本 |
| `fontSize` | int | 14 | 字号 |
| `fontStyle` | string | `"Normal"` | Normal / Bold / Italic / BoldItalic |
| `textAlignment` | string | `"Center"` | 文本对齐 |
| `textColor` | ColorData | 黑色 | 文字颜色 |
| `outlineColor` | ColorData | null | 描边颜色（有值才添加 Outline 组件） |
| `outlineDistance` | Vector2Data | `{x:1,y:-1}` | 描边偏移 |
| `useTMP` | bool | false | 是否使用 TextMeshPro 渲染 |

**textAlignment 枚举值：**

| 值 | Legacy TextAnchor | TMP TextAlignmentOptions |
|----|-------------------|-------------------------|
| `"Left"` | MiddleLeft | Left |
| `"Center"` | MiddleCenter | Center |
| `"Right"` | MiddleRight | Right |
| `"TopLeft"` | UpperLeft | TopLeft |
| `"Top"` | UpperCenter | Top |
| `"TopRight"` | UpperRight | TopRight |
| `"BottomLeft"` | LowerLeft | BottomLeft |
| `"Bottom"` | LowerCenter | Bottom |
| `"BottomRight"` | LowerRight | BottomRight |

**fontStyle 枚举值：** `"Normal"` / `"Bold"` / `"Italic"` / `"BoldItalic"`

```json
{
  "name": "Title",
  "type": "Text",
  "text": "欢迎回来",
  "fontSize": 32,
  "fontStyle": "Bold",
  "textAlignment": "Center",
  "textColor": { "r": 0.9, "g": 0.9, "b": 0.9, "a": 1 },
  "outlineColor": { "r": 0, "g": 0, "b": 0, "a": 0.5 },
  "outlineDistance": { "x": 1, "y": -1 }
}
```

### 5. Toggle

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `isOn` | bool | false | 初始选中状态 |
| `backgroundSprite` | string | `""` | 背景 Sprite 名 |
| `checkmarkSprite` | string | `""` | 选中标记 Sprite 名 |
| `text` | string | `""` | 标签文字 |
| `fontSize` | int | 14 | 标签字号 |
| `outlineColor` | ColorData | null | 文字描边颜色 |
| `outlineDistance` | Vector2Data | `{x:1,y:-1}` | 描边偏移 |

```json
{
  "name": "AgreeToggle",
  "type": "Toggle",
  "isOn": true,
  "backgroundSprite": "checkbox_bg",
  "checkmarkSprite": "checkbox_check",
  "text": "同意用户协议",
  "fontSize": 18
}
```

### 6. ToggleGroup

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `allowSwitchOff` | bool | false | 是否允许全部取消选中 |
| `spacing` | float | 0 | Toggle 间距 |
| `paddingLeft` | int | 0 | 左内边距 |
| `paddingRight` | int | 0 | 右内边距 |
| `paddingTop` | int | 0 | 上内边距 |
| `paddingBottom` | int | 0 | 下内边距 |

> **必须将 Toggle 放在 ToggleGroup 的 children 中。** ToggleGroup 自动生成 HorizontalLayoutGroup。

```json
{
  "name": "TabGroup",
  "type": "ToggleGroup",
  "allowSwitchOff": false,
  "spacing": 10,
  "paddingLeft": 20,
  "children": [
    { "name": "Tab1", "type": "Toggle", "text": "首页", "isOn": true },
    { "name": "Tab2", "type": "Toggle", "text": "发现", "isOn": false },
    { "name": "Tab3", "type": "Toggle", "text": "我的", "isOn": false }
  ]
}
```

### 7. ScrollRect

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `horizontalScroll` | bool | true | 水平滚动 |
| `verticalScroll` | bool | true | 垂直滚动 |
| `scrollMovementType` | string | `"Clamped"` | Clamped / Unrestricted / Elastic |
| `scrollInertia` | bool | true | 滚动惯性 |
| `scrollSensitivity` | float | 20 | 滚动灵敏度 |
| `layoutType` | string | `"None"` | Content 子元素的布局方式 |
| `spacing` | float | 0 | 子元素间距 |
| `paddingLeft` / `Right` / `Top` / `Bottom` | int | 0 | Content 内边距 |

> **ScrollRect 的 children 会自动放置在 Viewport/Content 下，而不是直接在 ScrollRect 下。**
> 如果设置了 `layoutType`，Content 还会自动添加 ContentSizeFitter。

```json
{
  "name": "CardScroll",
  "type": "ScrollRect",
  "verticalScroll": true,
  "horizontalScroll": false,
  "scrollMovementType": "Clamped",
  "layoutType": "Vertical",
  "spacing": 12,
  "paddingTop": 16,
  "paddingBottom": 16,
  "color": { "r": 0.06, "g": 0.06, "b": 0.06, "a": 1 },
  "size": { "x": 700, "y": 1000 },
  "children": [
    { "name": "Card1", "type": "Panel", "size": { "x": 680, "y": 120 }, "color": { "r": 0.15, "g": 0.15, "b": 0.15, "a": 1 } },
    { "name": "Card2", "type": "Panel", "size": { "x": 680, "y": 120 }, "color": { "r": 0.15, "g": 0.15, "b": 0.15, "a": 1 } },
    { "name": "Card3", "type": "Panel", "size": { "x": 680, "y": 120 }, "color": { "r": 0.15, "g": 0.15, "b": 0.15, "a": 1 } }
  ]
}
```

### 8. InputField

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `text` | string | `""` | 默认文本 |
| `fontSize` | int | 14 | 字号 |
| `textColor` | ColorData | 黑色 | 文字颜色 |
| `placeholderText` | string | `"Enter text..."` | 占位提示文字 |
| `characterLimit` | int | 0 | 字数限制（0=无限制） |
| `contentType` | string | `"Standard"` | 输入内容类型 |
| `lineType` | string | `"SingleLine"` | 行类型 |
| `useTMP` | bool | false | 是否使用 TMP_InputField |
| `sprite` | string | `""` | 输入框背景 Sprite（会自动设为 Sliced） |

**contentType 枚举值：** `"Standard"` / `"Integer"` / `"Decimal"` / `"AlphaNumeric"` / `"Name"` / `"EmailAddress"` / `"Password"` / `"Pin"` / `"Custom"`

**lineType 枚举值：** `"SingleLine"` / `"MultiLineSubmit"` / `"MultiLineNewline"`

```json
{
  "name": "EmailInput",
  "type": "InputField",
  "placeholderText": "请输入邮箱",
  "contentType": "EmailAddress",
  "characterLimit": 50,
  "fontSize": 20,
  "sprite": "input_bg",
  "size": { "x": 500, "y": 60 }
}
```

---

## LayoutGroup 通用字段

> 任何元素都可以附加 LayoutGroup，通过以下字段控制：

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `layoutType` | string | null | None / Horizontal / Vertical / Grid |
| `childAlignment` | string | `"MiddleCenter"` | 子元素对齐方式 |
| `spacing` | float | 0 | 子元素间距 |
| `paddingLeft` | int | 0 | 左内边距 |
| `paddingRight` | int | 0 | 右内边距 |
| `paddingTop` | int | 0 | 上内边距 |
| `paddingBottom` | int | 0 | 下内边距 |
| `childControlWidth` | bool | true | 控制子元素宽度 |
| `childControlHeight` | bool | true | 控制子元素高度 |
| `childForceExpandWidth` | bool | true | 强制扩展子宽度 |
| `childForceExpandHeight` | bool | true | 强制扩展子高度 |

**childAlignment 枚举值：** `"UpperLeft"` / `"UpperCenter"` / `"UpperRight"` / `"MiddleLeft"` / `"MiddleCenter"` / `"MiddleRight"` / `"LowerLeft"` / `"LowerCenter"` / `"LowerRight"`

**Grid 额外字段：**

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `gridCellSize` | Vector2Data | `{x:100,y:100}` | 单元格大小 |
| `gridSpacing` | Vector2Data | `{x:0,y:0}` | 单元格间距 |
| `gridConstraint` | string | `"Flexible"` | Flexible / FixedColumnCount / FixedRowCount |
| `gridConstraintCount` | int | 1 | 约束数量 |

---

## 嵌套规则

```
Canvas
├── UIMask（可选，由 includeMask 控制）
└── UIContent
    └── root elements[] ← 您的元素从这里开始
        ├── Panel / ToggleGroup / ScrollRect → 可以有 children
        ├── Image / Button / Text / Toggle / InputField → 不能有 children
        └── children 可以无限嵌套
```

**重要限制：**
- `Button`、`Text`、`Toggle`、`Image`、`InputField` 类型**不支持 children**
- `Panel`、`ToggleGroup`、`ScrollRect` 类型的 children 才会被递归构建
- `ScrollRect` 的 children 放在 Content/Viewport 结构内，不要设置它们的 anchor

---

## 颜色值注意

ColorData 的 r / g / b / a 范围应为 **0.0 ~ 1.0**（不是 0~255）。

```json
// 正确
"color": { "r": 1.0, "g": 0.5, "b": 0.0, "a": 1.0 }

// 错误
"color": { "r": 255, "g": 128, "b": 0, "a": 255 }
```

---

## 完整示例：一个经典登录页

```json
{
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
          "textColor": { "r": 1, "g": 1, "b": 1, "a": 1 },
          "position": { "x": 0, "y": 0 }
        },
        {
          "name": "PhoneInput",
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
          "name": "PasswordInput",
          "type": "InputField",
          "placeholderText": "请输入密码",
          "contentType": "Password",
          "fontSize": 22,
          "sprite": "input_bg",
          "size": { "x": 600, "y": 56 },
          "position": { "x": 0, "y": -160 }
        },
        {
          "name": "LoginBtn",
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
```

---

## 常见错误

| 错误 | 正确写法 |
|------|----------|
| `"type": "Pannel"` | `"type": "Panel"` |
| `"type": "Label"` | `"type": "Text"` |
| `"imageType": "filled"` | `"imageType": "Filled"` |
| `"textAlignment": "MiddleLeft"` | `"textAlignment": "Left"` |
| `"contentType": "Number"` | `"contentType": "Integer"` |
| color.r=255 | color.r=1.0 |
| Button 下放 children | Button 不支持 children，用 Panel + children |
| ScrollRect 不设置 layoutType | 列表内容推荐设 Vertical + spacing |

---

_文档版本：1.0 — 最后更新 2026-06-10_

# UGUI Builder JSON 生成规则

## 一、顶层结构 (LayoutRoot)

```json
{
  "screenName": "页面名称（即 Canvas GameObject 名称）",
  "resolution": { "x": 750, "y": 1334 },
  "matchWidthOrHeight": 0.5,
  "includeMask": true,
  "maskColor": { "r": 0, "g": 0, "b": 0, "a": 0.67 },
  "defaultFont": "Assets/路径/字体.asset",
  "elements": [ ... ]
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `screenName` | string | ✅ | Canvas 名称，也是 Prefab 根节点名 |
| `resolution` | Vector2 | ✅ | CanvasScaler 参考分辨率，常用 `750x1334`（手机）或 `1920x1080`（PC） |
| `matchWidthOrHeight` | float | 否 | 0=按宽度缩放，1=按高度，0.5=均衡。默认 0.5 |
| `includeMask` | bool | 否 | 是否生成 UIMask 遮罩层，默认 true |
| `maskColor` | Color | 否 | 遮罩颜色，默认 `(0,0,0,0.67)` 半透明黑 |
| `defaultFont` | string | 否 | TMP 字体路径，留空自动查找项目第一个 TMP 字体 |
| `elements` | Element[] | 否 | 元素数组，每个元素是一个 UI 节点 |

---

## 二、元素通用属性 (LayoutElement)

每个元素都拥有以下 RectTransform 属性（不写则使用默认值）：

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `name` | string | — | **必填**。GameObject 名称，同层级不可重复 |
| `type` | string | — | **必填**。元素类型（见下方类型表） |
| `anchorMin` | Vector2 | `(0.5, 0.5)` | 锚点最小值（左下角） |
| `anchorMax` | Vector2 | `(0.5, 0.5)` | 锚点最大值（右上角） |
| `position` | Vector2 | `(0, 0)` | 锚点偏移（anchoredPosition） |
| `size` | Vector2 | `(100, 100)` | 尺寸（sizeDelta） |
| `pivot` | Vector2 | `(0.5, 0.5)` | 轴心点 |
| `raycastTarget` | bool | `true` | 是否响应射线检测 |
| `interactable` | bool | `true` | 是否可交互（Button/Toggle 专用） |
| `children` | Element[] | `null` | **递归子元素数组**，支持无限嵌套 |

### 锚点速查表

| 布局意图 | anchorMin | anchorMax | size | 说明 |
|----------|-----------|-----------|------|------|
| 全屏拉伸 | (0,0) | (1,1) | (0,0) | UI 根容器常用 |
| 顶部停靠 | (0,1) | (1,1) | (0, H) | 顶栏 |
| 底部停靠 | (0,0) | (1,0) | (0, H) | 底栏 |
| 居中固定 | (0.5,0.5) | (0.5,0.5) | (W, H) | 弹窗/按钮 |
| 左上角 | (0,1) | (0,1) | (W, H) | 返回按钮 |
| 右上角 | (1,1) | (1,1) | (W, H) | 关闭按钮 |

---

## 三、元素类型详解

### 1. Image（图片）

```json
{
  "name": "MyImage",
  "type": "Image",
  "sprite": "icon_xxx",
  "color": { "r": 1, "g": 1, "b": 1, "a": 1 },
  "raycastTarget": true
}
```

| 额外属性 | 说明 |
|----------|------|
| `sprite` | Sprite 文件名（不带扩展名），从 Sprite 文件夹加载 |
| `color` | 叠加颜色 |

---

### 2. Panel（面板/容器）

```json
{
  "name": "MyPanel",
  "type": "Panel",
  "color": { "r": 0, "g": 0, "b": 0, "a": 1 },
  "children": [ ... ]
}
```

> Panel = Image + 默认白色背景。适合做子元素的父容器。不写 `color` 时默认白色。

---

### 3. Button（按钮）

```json
{
  "name": "MyButton",
  "type": "Button",
  "sprite": "btn_start",
  "text": "开始游戏",
  "fontSize": 24,
  "color": { "r": 1, "g": 1, "b": 1, "a": 1 },
  "textColor": { "r": 1, "g": 1, "b": 1, "a": 1 },
  "interactable": true
}
```

| 额外属性 | 说明 |
|----------|------|
| `sprite` | 按钮背景图 |
| `text` | 按钮文字 |
| `fontSize` | 文字大小 |
| `textColor` | 文字颜色 |
| `interactable` | 是否可点击 |

> Button 会自动创建一个名为 "Text" 的子 TextMeshProUGUI 作为标签。

---

### 4. Text（纯文本）

```json
{
  "name": "MyText",
  "type": "Text",
  "text": "这是一段文本",
  "fontSize": 20,
  "fontStyle": "Bold",
  "textAlignment": "Center",
  "textColor": { "r": 1, "g": 1, "b": 1, "a": 1 },
  "outlineColor": { "r": 0.9, "g": 0, "b": 1, "a": 0.5 },
  "outlineDistance": { "x": 1, "y": -1 }
}
```

| 额外属性 | 可选值 | 说明 |
|----------|--------|------|
| `text` | 任意字符串 | 显示文本 |
| `fontSize` | 数字 | 字号，默认 14 |
| `fontStyle` | `Normal` / `Bold` / `Italic` / `BoldItalic` | 字体样式 |
| `textAlignment` | 见下方表 | 对齐方式，默认 `Center` |
| `textColor` | Color | 文本颜色 |
| `outlineColor` | Color | 描边颜色（不写则不添加 Outline 组件） |
| `outlineDistance` | Vector2 | 描边偏移，默认 `(1, -1)`（右下阴影效果） |

#### textAlignment 可选值

| 值 | 效果 | 值 | 效果 |
|----|------|----|------|
| `TopLeft` | 左上 | `Top` | 顶部居中 |
| `TopRight` | 右上 | `Left` | 左中 |
| `Center` | 居中 | `Right` | 右中 |
| `BottomLeft` | 左下 | `Bottom` | 底部居中 |
| `BottomRight` | 右下 | | |

---

### 5. Toggle（开关/选项卡）

```json
{
  "name": "tab1",
  "type": "Toggle",
  "size": { "x": 75, "y": 75 },
  "backgroundSprite": "tab_bg",
  "checkmarkSprite": "checkmark",
  "text": "今日神谕",
  "fontSize": 20,
  "isOn": true,
  "outlineColor": { "r": 0.903, "g": 0.005, "b": 1, "a": 0.5 },
  "outlineDistance": { "x": 1, "y": -1 }
}
```

| 额外属性 | 说明 |
|----------|------|
| `backgroundSprite` | 背景切图 |
| `checkmarkSprite` | 选中标记切图（默认绿色） |
| `text` | 标签文字 |
| `fontSize` | 标签字号 |
| `isOn` | 是否默认选中 |
| `outlineColor` | 标签描边色 |
| `outlineDistance` | 标签描边偏移 |

> Toggle 放在 ToggleGroup 的 children 中时，**自动绑定到父级 ToggleGroup**。

#### Toggle 结构

```
Toggle (name)
├── Background     ← targetGraphic（背景 Image）
│   └── Checkmark  ← graphic（选中标记 Image，默认绿色）
└── Label          ← TextMeshProUGUI（可选）
```

---

### 6. ToggleGroup（开关组）

```json
{
  "name": "MyToggleGroup",
  "type": "ToggleGroup",
  "spacing": 10,
  "paddingLeft": 20,
  "paddingRight": 20,
  "paddingTop": 10,
  "paddingBottom": 10,
  "allowSwitchOff": false,
  "children": [
    { "name": "tab1", "type": "Toggle", ... },
    { "name": "tab2", "type": "Toggle", ... }
  ]
}
```

| 额外属性 | 说明 |
|----------|------|
| `spacing` | Toggle 间距，默认 0 |
| `paddingLeft/Right/Top/Bottom` | 内边距，默认 0 |
| `allowSwitchOff` | 是否允许全部取消选中，默认 false |

> ToggleGroup 使用 `HorizontalLayoutGroup`（水平排列），子 Toggle 自动绑定。

---

## 四、数据类型速查

### Vector2

```json
{ "x": 100, "y": 50 }
```

### Color

```json
{ "r": 1.0, "g": 0.5, "b": 0.0, "a": 1.0 }
```

> r/g/b/a 范围 0.0 ~ 1.0

---

## 五、Sprite 加载规则

1. Sprite 文件放在 `Sprite 文件夹` 下
2. JSON 中的 `sprite`/`backgroundSprite`/`checkmarkSprite` 写**不带扩展名**的文件名
3. 工具自动按 `.png` → `.jpg` → `.jpeg` → `.psd` 顺序查找
4. 示例：JSON 写 `"sprite": "btn_start"` → 加载 `Sprite文件夹/btn_start.png`

---

## 六、完整生成流程

```
JSON 布局文件
    │
    ▼
UGUIBuilderTool.BuildUGUIFromJSON()
    │
    ├── 1. 解析 JSON → LayoutRoot
    ├── 2. 创建 / 复用 Canvas + CanvasScaler + EventSystem
    ├── 3. 创建 UIContent（全屏拉伸容器）
    ├── 4. 创建 UIMask（遮罩，默认隐藏）
    ├── 5. 递归构建 elements[] 树
    │       ├── Image / Panel → Image 组件
    │       ├── Button → Image + Button + Text(TMP)
    │       ├── Text → TextMeshProUGUI + Outline(可选)
    │       ├── Toggle → Toggle + Background + Checkmark + Label
    │       └── ToggleGroup → HorizontalLayoutGroup + ToggleGroup
    └── 6. SaveAsPrefabAsset + 清理场景临时对象
```

---

## 七、完整示例

```json
{
  "screenName": "MainMenu",
  "resolution": { "x": 750, "y": 1334 },
  "matchWidthOrHeight": 0.5,
  "includeMask": true,
  "maskColor": { "r": 0, "g": 0, "b": 0, "a": 0.67 },
  "defaultFont": "",
  "elements": [
    {
      "name": "TopBar",
      "type": "Panel",
      "color": { "r": 0.1, "g": 0.1, "b": 0.1, "a": 1 },
      "anchorMin": { "x": 0, "y": 1 },
      "anchorMax": { "x": 1, "y": 1 },
      "size": { "x": 0, "y": 88 },
      "position": { "x": 0, "y": 0 },
      "children": [
        {
          "name": "Title",
          "type": "Text",
          "text": "月灵",
          "fontSize": 32,
          "fontStyle": "Bold",
          "textAlignment": "Center",
          "textColor": { "r": 1, "g": 1, "b": 1, "a": 1 },
          "anchorMin": { "x": 0, "y": 0 },
          "anchorMax": { "x": 1, "y": 1 },
          "size": { "x": 0, "y": 0 }
        }
      ]
    },
    {
      "name": "CenterContent",
      "type": "Panel",
      "color": { "r": 0, "g": 0, "b": 0, "a": 0 },
      "anchorMin": { "x": 0, "y": 0 },
      "anchorMax": { "x": 1, "y": 1 },
      "size": { "x": 0, "y": 0 },
      "children": [
        {
          "name": "StartButton",
          "type": "Button",
          "sprite": "btn_start",
          "text": "开始",
          "fontSize": 28,
          "anchorMin": { "x": 0.5, "y": 0.5 },
          "anchorMax": { "x": 0.5, "y": 0.5 },
          "size": { "x": 200, "y": 64 }
        }
      ]
    },
    {
      "name": "BottomNav",
      "type": "Panel",
      "color": { "r": 0, "g": 0, "b": 0, "a": 1 },
      "raycastTarget": false,
      "anchorMin": { "x": 0, "y": 0 },
      "anchorMax": { "x": 1, "y": 0 },
      "size": { "x": 0, "y": 208 },
      "position": { "x": 0, "y": 104 },
      "children": [
        {
          "name": "NavGroup",
          "type": "ToggleGroup",
          "spacing": 0,
          "paddingLeft": 69,
          "paddingTop": 30,
          "allowSwitchOff": false,
          "anchorMin": { "x": 0, "y": 0 },
          "anchorMax": { "x": 1, "y": 1 },
          "size": { "x": -16, "y": -29 },
          "position": { "x": 8, "y": 14 },
          "children": [
            {
              "name": "TabOracle",
              "type": "Toggle",
              "size": { "x": 75, "y": 75 },
              "backgroundSprite": "icon_oracle",
              "checkmarkSprite": "checkmark",
              "text": "今日神谕",
              "fontSize": 20,
              "isOn": true,
              "outlineColor": { "r": 0.9, "g": 0, "b": 1, "a": 0.5 },
              "outlineDistance": { "x": 1, "y": -1 }
            },
            {
              "name": "TabChat",
              "type": "Toggle",
              "size": { "x": 75, "y": 75 },
              "backgroundSprite": "icon_chat",
              "checkmarkSprite": "checkmark",
              "text": "对话",
              "fontSize": 20,
              "isOn": false,
              "outlineColor": { "r": 0.9, "g": 0, "b": 1, "a": 0.5 },
              "outlineDistance": { "x": 1, "y": -1 }
            }
          ]
        }
      ]
    }
  ]
}
```

---

## 八、注意事项

1. **`name` 必须唯一**：同一层级下子元素的 `name` 不可重复（Unity GameObject 要求）
2. **Sprite 先于 JSON**：确保切图已导入 Assets 后再生成 Prefab，否则 Sprite 引用为空
3. **锚点互斥**：`anchorMin` 和 `anchorMax` 相同时 = 固定定位；不同时 = 拉伸，`sizeDelta` 为偏移量
4. **Toggle 必须放 ToggleGroup 内**：放在 ToggleGroup 的 `children` 中才能自动绑定组
5. **文本颜色**：Text 元素不写 `textColor` 时默认白色；`outlineColor` 不写则不添加描边

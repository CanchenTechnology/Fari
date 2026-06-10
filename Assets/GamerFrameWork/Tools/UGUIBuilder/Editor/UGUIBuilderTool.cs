using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using System.Linq;

namespace UGUIBuilder
{
    /// <summary>
    /// UGUI Builder Tool - 读取 JSON 布局文件，生成 Canvas → UIMask + UIContent → 用户元素的 Prefab。
    /// 与 NavigationUI.prefab 结构保持一致：Canvas 下包含 UIMask 和 UIContent，UIContent 里放置具体功能。
    /// </summary>
    public static class UGUIBuilderTool
    {
        #region JSON Data Classes

        [Serializable]
        public class ColorData
        {
            public float r = 1, g = 1, b = 1, a = 1;
            public Color ToColor() => new Color(r, g, b, a);
        }

        [Serializable]
        public class Vector2Data
        {
            public float x, y;
            public Vector2 ToVector2() => new Vector2(x, y);
        }

        [Serializable]
        public class LayoutElement
        {
            public string name;
            public string type;
            public Vector2Data anchorMin = new Vector2Data { x = 0.5f, y = 0.5f };
            public Vector2Data anchorMax = new Vector2Data { x = 0.5f, y = 0.5f };
            public Vector2Data position = new Vector2Data { x = 0, y = 0 };
            public Vector2Data size = new Vector2Data { x = 100, y = 100 };
            public Vector2Data pivot = new Vector2Data { x = 0.5f, y = 0.5f };

            public string sprite;
            public ColorData color;
            public bool raycastTarget = true;
            public bool interactable = true;

            public string text;
            public int fontSize = 14;
            public string fontStyle = "Normal";
            public string textAlignment = "Center";
            public ColorData textColor;
            public ColorData outlineColor;
            public Vector2Data outlineDistance;

            public string backgroundSprite;
            public string checkmarkSprite;
            public bool isOn;

            public bool allowSwitchOff;

            public float spacing;
            public int paddingLeft, paddingRight, paddingTop, paddingBottom;

            // Image 类型 & Filled
            public string imageType = "Simple";
            public string fillMethod = "Horizontal";
            public float fillAmount = 1f;
            public int fillOrigin;

            // TextMeshPro
            public bool useTMP;

            // InputField
            public string placeholderText;
            public int characterLimit;
            public string contentType = "Standard";
            public string lineType = "SingleLine";

            // ScrollRect
            public bool horizontalScroll = true;
            public bool verticalScroll = true;
            public string scrollMovementType = "Clamped";
            public bool scrollInertia = true;
            public float scrollSensitivity = 20f;

            // Layout 组件
            public string layoutType;
            public string childAlignment = "MiddleCenter";
            public bool childControlWidth = true;
            public bool childControlHeight = true;
            public bool childForceExpandWidth = true;
            public bool childForceExpandHeight = true;
            public Vector2Data gridCellSize;
            public Vector2Data gridSpacing;
            public string gridConstraint = "Flexible";
            public int gridConstraintCount = 1;

            public List<LayoutElement> children;
        }

        [Serializable]
        public class LayoutRoot
        {
            public string screenName = "NewScreen";
            public Vector2Data resolution = new Vector2Data { x = 750, y = 1334 };
            public float matchWidthOrHeight = 0.5f;
            public bool includeMask = true;
            public ColorData maskColor;
            public string defaultFont;
            public List<LayoutElement> elements;
        }

        #endregion

        private static string spriteFolder;
        private static Font defaultFont;
        private static TMP_FontAsset tmpFontAsset;

        /// <summary>
        /// 入口：从 JSON 构建并保存为 UGUI Prefab
        /// </summary>
        /// <param name="overrideFont">可选：覆盖 JSON 中指定的字体。传入 null 则使用 JSON 中的 defaultFont 路径</param>
        public static void BuildUGUIFromJSON(string jsonPath, string spriteFolderPath, string prefabOutputPath, Font overrideFont = null, TMP_FontAsset tmpFont = null)
        {
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[UGUIBuilder] JSON file not found: {jsonPath}");
                return;
            }

            try
            {
                GameObject canvasGO = BuildCanvasFromJSON(jsonPath, spriteFolderPath, overrideFont, tmpFont);
                if (canvasGO == null) return;

                SavePrefab(canvasGO, prefabOutputPath);
                Debug.Log($"[UGUIBuilder] Build complete: {prefabOutputPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[UGUIBuilder] Build failed: {e.Message}\n{e.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// 场景预览：从 JSON 构建并直接放入当前场景（不保存 Prefab）
        /// 返回 Canvas GameObject，可用于后续清除预览。
        /// </summary>
        public static GameObject BuildPreviewFromJSON(string jsonPath, string spriteFolderPath, Font overrideFont = null, TMP_FontAsset tmpFont = null)
        {
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[UGUIBuilder] JSON file not found: {jsonPath}");
                return null;
            }

            var canvasGO = BuildCanvasFromJSON(jsonPath, spriteFolderPath, overrideFont, tmpFont);
            if (canvasGO != null)
            {
                canvasGO.name = "[Preview] " + canvasGO.name;
                Debug.Log($"[UGUIBuilder] Preview created in scene: {canvasGO.name}");
            }
            return canvasGO;
        }

        /// <summary>
        /// 清除场景预览
        /// </summary>
        public static void ClearPreview(GameObject previewRoot)
        {
            if (previewRoot != null)
            {
                GameObject.DestroyImmediate(previewRoot);
                Debug.Log("[UGUIBuilder] Preview cleared.");
            }
        }

        /// <summary>
        /// 核心构建方法：解析 JSON、创建 Canvas 和所有元素，返回 Canvas GameObject。
        /// 不负责保存——由调用方决定是保存为 Prefab 还是留在场景中预览。
        /// </summary>
        private static GameObject BuildCanvasFromJSON(string jsonPath, string spriteFolderPath, Font overrideFont, TMP_FontAsset tmpFont)
        {
            spriteFolder = spriteFolderPath;
            defaultFont = null;
            tmpFontAsset = tmpFont;

            string jsonText = File.ReadAllText(jsonPath);
            LayoutRoot layout;

            try
            {
                layout = JsonUtility.FromJson<LayoutRoot>(jsonText);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UGUIBuilder] JSON parse failed: {e.Message}");
                return null;
            }

            if (layout == null)
            {
                Debug.LogError("[UGUIBuilder] Layout is null. Check JSON structure.");
                return null;
            }

            // 构建前自动校验，有 ERROR 则阻止生成
            var (hasErrors, validationMsgs) = ValidateLayout(layout);
            if (hasErrors)
            {
                foreach (var m in validationMsgs.Where(m => m.StartsWith("[ERROR]")))
                    Debug.LogError($"[UGUIBuilder] {m}");
                Debug.LogError("[UGUIBuilder] 校验发现错误，已中止构建。请先修正 JSON。");
                return null;
            }

            LoadDefaultFont(layout, overrideFont);

            GameObject canvasGO = CreateOrGetCanvas(layout);
            GameObject uiContent = CreateUIContent(canvasGO.transform);

            if (layout.includeMask)
            {
                CreateUIMask(canvasGO.transform, layout);
            }

            if (layout.elements != null)
            {
                foreach (var elem in layout.elements)
                {
                    BuildElement(elem, uiContent.transform, null);
                }
            }

            return canvasGO;
        }

        #region Canvas / UIMask / UIContent

        private static void LoadDefaultFont(LayoutRoot layout, Font overrideFont = null)
        {
            // 优先使用窗口传入的字体覆盖
            if (overrideFont != null)
            {
                defaultFont = overrideFont;
                return;
            }

            if (!string.IsNullOrEmpty(layout.defaultFont))
            {
                defaultFont = AssetDatabase.LoadAssetAtPath<Font>(layout.defaultFont);
            }

            if (defaultFont == null)
            {
                defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        /// <summary>
        /// 创建临时 Canvas（不复用场景中已有的 Canvas，避免副作用）。
        /// 生成的 Prefab 保存后该 Canvas 会在 SavePrefab 中被 DestroyImmediate。
        /// </summary>
        private static GameObject CreateOrGetCanvas(LayoutRoot layout)
        {
            // 始终创建新的临时 Canvas，避免修改场景中已有 Canvas
            GameObject canvasGO = new GameObject(layout.screenName);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = layout.resolution.ToVector2();
            scaler.matchWidthOrHeight = layout.matchWidthOrHeight;

            if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            return canvasGO;
        }

        private static GameObject CreateUIContent(Transform parent)
        {
            GameObject uiContent = new GameObject("UIContent",
                typeof(RectTransform), typeof(CanvasRenderer));
            uiContent.layer = LayerMask.NameToLayer("UI");

            RectTransform rt = uiContent.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            return uiContent;
        }

        private static void CreateUIMask(Transform parent, LayoutRoot layout)
        {
            GameObject uiMask = new GameObject("UIMask",
                typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            uiMask.layer = LayerMask.NameToLayer("UI");
            uiMask.SetActive(false);

            // 根据画布分辨率动态计算遮罩尺寸（4x 确保覆盖任意屏幕）
            Vector2 res = layout.resolution.ToVector2();
            Vector2 maskSize = new Vector2(res.x * 4f, res.y * 4f);

            RectTransform rt = uiMask.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = maskSize;
            rt.anchoredPosition = Vector2.zero;

            Image img = uiMask.GetComponent<Image>();
            img.color = layout.maskColor != null
                ? layout.maskColor.ToColor()
                : new Color(0, 0, 0, 0.67f);
            img.raycastTarget = false;
        }

        #endregion

        #region Element Builders

        private static GameObject BuildElement(LayoutElement elem, Transform parent, ToggleGroup parentToggleGroup)
        {
            GameObject go = null;

            switch (elem.type)
            {
                case "Image":       go = BuildImage(elem);       break;
                case "Panel":       go = BuildPanel(elem);       break;
                case "Button":      go = BuildButton(elem);      break;
                case "Text":        go = BuildText(elem);        break;
                case "Toggle":      go = BuildToggle(elem);      break;
                case "ToggleGroup": go = BuildToggleGroup(elem); break;
                case "ScrollRect":  go = BuildScrollRect(elem);  break;
                case "InputField":  go = BuildInputField(elem);  break;
                default:
                    Debug.LogWarning($"[UGUIBuilder] Unknown element type '{elem.type}', creating empty GameObject.");
                    go = new GameObject(elem.name, typeof(RectTransform));
                    break;
            }

            if (go == null) return null;

            go.name = elem.name;
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");

            SetupRectTransform(go, elem);

            ToggleGroup currentToggleGroup = parentToggleGroup;
            if (elem.type == "ToggleGroup")
            {
                currentToggleGroup = go.GetComponent<ToggleGroup>();
            }

            // 应用 LayoutGroup（在构建子元素之前）
            ApplyLayoutGroup(go, elem);

            // ScrollRect 自行处理子元素，InputField 不需要子元素
            if (elem.children != null && elem.children.Count > 0
                && elem.type != "ScrollRect" && elem.type != "InputField")
            {
                foreach (var child in elem.children)
                {
                    BuildElement(child, go.transform, currentToggleGroup);
                }
            }

            if (elem.type == "Toggle" && parentToggleGroup != null)
            {
                Toggle toggle = go.GetComponent<Toggle>();
                if (toggle != null) toggle.group = parentToggleGroup;
            }

            return go;
        }

        private static void SetupRectTransform(GameObject go, LayoutElement elem)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) return;

            rt.anchorMin = elem.anchorMin.ToVector2();
            rt.anchorMax = elem.anchorMax.ToVector2();
            rt.sizeDelta = elem.size.ToVector2();
            rt.anchoredPosition = elem.position.ToVector2();
            rt.pivot = elem.pivot.ToVector2();
        }

        private static GameObject BuildImage(LayoutElement elem)
        {
            GameObject go = new GameObject(elem.name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

            Image img = go.GetComponent<Image>();
            img.raycastTarget = elem.raycastTarget;

            if (elem.color != null) img.color = elem.color.ToColor();
            if (!string.IsNullOrEmpty(elem.sprite))
            {
                Sprite s = LoadSprite(elem.sprite);
                if (s != null) img.sprite = s;
            }

            // Image 类型: Simple / Sliced / Tiled / Filled
            img.type = elem.imageType switch
            {
                "Sliced" => Image.Type.Sliced,
                "Tiled"  => Image.Type.Tiled,
                "Filled" => Image.Type.Filled,
                _        => Image.Type.Simple
            };

            if (img.type == Image.Type.Filled)
            {
                img.fillMethod = elem.fillMethod switch
                {
                    "Vertical"   => Image.FillMethod.Vertical,
                    "Radial90"   => Image.FillMethod.Radial90,
                    "Radial180"  => Image.FillMethod.Radial180,
                    "Radial360"  => Image.FillMethod.Radial360,
                    _            => Image.FillMethod.Horizontal
                };
                img.fillAmount = Mathf.Clamp01(elem.fillAmount);
                img.fillOrigin = elem.fillOrigin;
                img.fillClockwise = true;
            }

            return go;
        }

        private static GameObject BuildPanel(LayoutElement elem)
        {
            GameObject go = BuildImage(elem);

            Image img = go.GetComponent<Image>();
            if (elem.color == null && string.IsNullOrEmpty(elem.sprite))
                img.color = Color.white;

            return go;
        }

        private static GameObject BuildButton(LayoutElement elem)
        {
            GameObject go = new GameObject(elem.name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));

            Image img = go.GetComponent<Image>();
            img.raycastTarget = elem.raycastTarget;
            if (elem.color != null) img.color = elem.color.ToColor();
            if (!string.IsNullOrEmpty(elem.sprite))
            {
                Sprite s = LoadSprite(elem.sprite);
                if (s != null) img.sprite = s;
            }

            Button btn = go.GetComponent<Button>();
            btn.interactable = elem.interactable;
            btn.targetGraphic = img;

            if (!string.IsNullOrEmpty(elem.text))
            {
                GameObject label = BuildPureText("Text", elem);
                label.transform.SetParent(go.transform, false);
                SetFullStretch(label.GetComponent<RectTransform>());
            }

            return go;
        }

        private static GameObject BuildText(LayoutElement elem)
        {
            return BuildPureText(elem.name, elem);
        }

        private static GameObject BuildPureText(string goName, LayoutElement elem)
        {
            if (elem.useTMP)
                return BuildTMProText(goName, elem);
            return BuildLegacyText(goName, elem);
        }

        private static GameObject BuildLegacyText(string goName, LayoutElement elem)
        {
            GameObject go = new GameObject(goName,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));

            Text txt = go.GetComponent<Text>();
            txt.text = elem.text ?? "";
            txt.fontSize = elem.fontSize > 0 ? elem.fontSize : 14;
            txt.raycastTarget = elem.raycastTarget;
            txt.color = elem.textColor != null ? elem.textColor.ToColor() : Color.black;
            txt.font = defaultFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            txt.fontStyle = elem.fontStyle switch
            {
                "Bold"       => FontStyle.Bold,
                "Italic"     => FontStyle.Italic,
                "BoldItalic" => FontStyle.BoldAndItalic,
                _            => FontStyle.Normal
            };

            txt.alignment = elem.textAlignment switch
            {
                "Left"        => TextAnchor.MiddleLeft,
                "Right"       => TextAnchor.MiddleRight,
                "Center"      => TextAnchor.MiddleCenter,
                "TopLeft"     => TextAnchor.UpperLeft,
                "Top"         => TextAnchor.UpperCenter,
                "TopRight"    => TextAnchor.UpperRight,
                "BottomLeft"  => TextAnchor.LowerLeft,
                "Bottom"      => TextAnchor.LowerCenter,
                "BottomRight" => TextAnchor.LowerRight,
                _             => TextAnchor.MiddleCenter
            };

            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;

            if (elem.outlineColor != null)
            {
                Outline outline = go.AddComponent<Outline>();
                outline.effectColor = elem.outlineColor.ToColor();
                outline.effectDistance = elem.outlineDistance != null
                    ? elem.outlineDistance.ToVector2()
                    : new Vector2(1, -1);
                outline.useGraphicAlpha = true;
            }

            return go;
        }

        private static GameObject BuildTMProText(string goName, LayoutElement elem)
        {
            GameObject go = new GameObject(goName,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));

            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = elem.text ?? "";
            tmp.fontSize = elem.fontSize > 0 ? elem.fontSize : 14;
            tmp.raycastTarget = elem.raycastTarget;
            tmp.color = elem.textColor != null ? elem.textColor.ToColor() : Color.black;
            tmp.enableWordWrapping = true;

            if (tmpFontAsset != null) tmp.font = tmpFontAsset;

            // TMP FontStyle
            tmp.fontStyle = elem.fontStyle switch
            {
                "Bold"       => FontStyles.Bold,
                "Italic"     => FontStyles.Italic,
                "BoldItalic" => FontStyles.Bold | FontStyles.Italic,
                _            => FontStyles.Normal
            };

            // TMP Alignment 映射
            tmp.alignment = elem.textAlignment switch
            {
                "Left"        => TextAlignmentOptions.Left,
                "Right"       => TextAlignmentOptions.Right,
                "Center"      => TextAlignmentOptions.Center,
                "TopLeft"     => TextAlignmentOptions.TopLeft,
                "Top"         => TextAlignmentOptions.Top,
                "TopRight"    => TextAlignmentOptions.TopRight,
                "BottomLeft"  => TextAlignmentOptions.BottomLeft,
                "Bottom"      => TextAlignmentOptions.Bottom,
                "BottomRight" => TextAlignmentOptions.BottomRight,
                _             => TextAlignmentOptions.Center
            };

            // TMP Outline：使用标准 Outline（作用于 Graphic，对 TMP 同样有效）
            if (elem.outlineColor != null)
            {
                var outlineComp = go.AddComponent<Outline>();
                outlineComp.effectColor = elem.outlineColor.ToColor();
                outlineComp.effectDistance = elem.outlineDistance != null
                    ? elem.outlineDistance.ToVector2()
                    : new Vector2(1, -1);
                outlineComp.useGraphicAlpha = true;
            }

            return go;
        }

        private static GameObject BuildToggle(LayoutElement elem)
        {
            GameObject go = new GameObject(elem.name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Toggle));

            Toggle toggle = go.GetComponent<Toggle>();
            toggle.isOn = elem.isOn;
            toggle.interactable = elem.interactable;
            toggle.toggleTransition = Toggle.ToggleTransition.Fade;

            GameObject bgGO = new GameObject("Background",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGO.transform.SetParent(go.transform, false);
            Image bgImg = bgGO.GetComponent<Image>();
            bgImg.color = Color.white;
            if (!string.IsNullOrEmpty(elem.backgroundSprite))
            {
                Sprite s = LoadSprite(elem.backgroundSprite);
                if (s != null) bgImg.sprite = s;
            }
            SetFullStretch(bgGO.GetComponent<RectTransform>());

            GameObject checkGO = new GameObject("Checkmark",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            checkGO.transform.SetParent(bgGO.transform, false);
            Image checkImg = checkGO.GetComponent<Image>();
            checkImg.color = new Color(0.107f, 1f, 0f, 1f);
            if (!string.IsNullOrEmpty(elem.checkmarkSprite))
            {
                Sprite s = LoadSprite(elem.checkmarkSprite);
                if (s != null) checkImg.sprite = s;
            }
            SetFullStretch(checkGO.GetComponent<RectTransform>());

            if (!string.IsNullOrEmpty(elem.text))
            {
                GameObject labelGO = BuildPureText("Label", elem);
                labelGO.transform.SetParent(go.transform, false);
                SetFullStretch(labelGO.GetComponent<RectTransform>());
            }

            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;

            return go;
        }

        private static GameObject BuildToggleGroup(LayoutElement elem)
        {
            GameObject go = new GameObject(elem.name,
                typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(HorizontalLayoutGroup), typeof(ToggleGroup));

            Image img = go.GetComponent<Image>();
            img.enabled = false;
            img.raycastTarget = false;

            HorizontalLayoutGroup hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = elem.spacing;
            hlg.padding = new RectOffset(
                elem.paddingLeft, elem.paddingRight,
                elem.paddingTop, elem.paddingBottom);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            ToggleGroup tg = go.GetComponent<ToggleGroup>();
            tg.allowSwitchOff = elem.allowSwitchOff;

            return go;
        }

        private static GameObject BuildScrollRect(LayoutElement elem)
        {
            GameObject go = new GameObject(elem.name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect), typeof(Mask));
            go.layer = LayerMask.NameToLayer("UI");

            // 配置 ScrollRect 背景
            Image bgImg = go.GetComponent<Image>();
            bgImg.raycastTarget = true;
            if (elem.color != null) bgImg.color = elem.color.ToColor();
            if (!string.IsNullOrEmpty(elem.sprite))
            {
                Sprite s = LoadSprite(elem.sprite);
                if (s != null) bgImg.sprite = s;
            }

            // Viewport
            GameObject viewport = new GameObject("Viewport",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(go.transform, false);
            viewport.layer = go.layer;
            Image vpImg = viewport.GetComponent<Image>();
            vpImg.color = Color.white;
            vpImg.raycastTarget = true;
            SetFullStretch(viewport.GetComponent<RectTransform>());

            // Content
            GameObject content = new GameObject("Content",
                typeof(RectTransform), typeof(CanvasRenderer));
            content.transform.SetParent(viewport.transform, false);
            content.layer = go.layer;
            RectTransform contentRT = content.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.sizeDelta = new Vector2(0, 0);
            contentRT.anchoredPosition = Vector2.zero;

            // 配置 ScrollRect
            ScrollRect sr = go.GetComponent<ScrollRect>();
            sr.content = contentRT;
            sr.viewport = viewport.GetComponent<RectTransform>();
            sr.horizontal = elem.horizontalScroll;
            sr.vertical = elem.verticalScroll;
            sr.movementType = elem.scrollMovementType switch
            {
                "Unrestricted" => ScrollRect.MovementType.Unrestricted,
                "Elastic"      => ScrollRect.MovementType.Elastic,
                _              => ScrollRect.MovementType.Clamped
            };
            sr.inertia = elem.scrollInertia;
            sr.scrollSensitivity = elem.scrollSensitivity;

            // Content 上应用 LayoutGroup
            ApplyLayoutGroupTo(content, elem);

            // 构建子元素 -> Content
            if (elem.children != null)
            {
                foreach (var child in elem.children)
                {
                    BuildElement(child, content.transform, null);
                }
            }

            // 如果 content 有布局，自动添加 ContentSizeFitter
            if (!string.IsNullOrEmpty(elem.layoutType) && elem.layoutType != "None")
            {
                var fitter = content.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            // 强制刷新布局，确保 Content 尺寸正确
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);

            return go;
        }

        private static GameObject BuildInputField(LayoutElement elem)
        {
            bool useTMP = elem.useTMP || Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro") != null;
            if (!useTMP)
            {
                // 回退 Legacy InputField
                return BuildLegacyInputField(elem);
            }

            return BuildInputText(elem);
        }

        private static GameObject BuildInputText(LayoutElement elem)
        {
            GameObject go = new GameObject(elem.name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
            go.layer = LayerMask.NameToLayer("UI");

            // 背景图
            Image bgImg = go.GetComponent<Image>();
            if (!string.IsNullOrEmpty(elem.sprite))
            {
                Sprite s = LoadSprite(elem.sprite);
                if (s != null) bgImg.sprite = s;
            }
            bgImg.type = Image.Type.Sliced;
            bgImg.color = elem.color != null ? elem.color.ToColor() : Color.white;

            // Placeholder
            GameObject placeholder = new GameObject("Placeholder",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            placeholder.transform.SetParent(go.transform, false);
            placeholder.layer = go.layer;
            Text phTxt = placeholder.GetComponent<Text>();
            phTxt.text = elem.placeholderText ?? "Enter text...";
            phTxt.fontSize = elem.fontSize > 0 ? elem.fontSize : 14;
            phTxt.fontStyle = FontStyle.Italic;
            phTxt.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            phTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            phTxt.font = defaultFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            SetFullStretch(placeholder.GetComponent<RectTransform>());

            // Text
            GameObject textGO = new GameObject("Text",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            textGO.layer = go.layer;
            Text txt = textGO.GetComponent<Text>();
            txt.fontSize = elem.fontSize > 0 ? elem.fontSize : 14;
            txt.color = elem.textColor != null ? elem.textColor.ToColor() : Color.black;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.font = defaultFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            SetFullStretch(textGO.GetComponent<RectTransform>());

            // 配置 InputField
            InputField input = go.GetComponent<InputField>();
            input.textComponent = txt;
            input.placeholder = phTxt;
            input.characterLimit = elem.characterLimit > 0 ? elem.characterLimit : 0;
            input.interactable = elem.interactable;
            input.targetGraphic = bgImg;

            input.contentType = elem.contentType switch
            {
                "Integer"       => InputField.ContentType.IntegerNumber,
                "Decimal"       => InputField.ContentType.DecimalNumber,
                "AlphaNumeric"  => InputField.ContentType.Alphanumeric,
                "Name"          => InputField.ContentType.Name,
                "EmailAddress"  => InputField.ContentType.EmailAddress,
                "Password"      => InputField.ContentType.Password,
                "Pin"           => InputField.ContentType.Pin,
                "Custom"        => InputField.ContentType.Custom,
                _               => InputField.ContentType.Standard
            };

            input.lineType = elem.lineType switch
            {
                "MultiLineSubmit" => InputField.LineType.MultiLineSubmit,
                "MultiLineNewline"=> InputField.LineType.MultiLineNewline,
                _                 => InputField.LineType.SingleLine
            };

            return go;
        }

        private static GameObject BuildLegacyInputField(LayoutElement elem)
        {
            GameObject go = new GameObject(elem.name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
            go.layer = LayerMask.NameToLayer("UI");

            Image bgImg = go.GetComponent<Image>();
            if (!string.IsNullOrEmpty(elem.sprite))
            {
                Sprite s = LoadSprite(elem.sprite);
                if (s != null) bgImg.sprite = s;
            }
            bgImg.color = elem.color != null ? elem.color.ToColor() : Color.white;

            // Placeholder + Text 子对象
            GameObject placeholder = new GameObject("Placeholder",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            placeholder.transform.SetParent(go.transform, false);
            Text phTxt = placeholder.GetComponent<Text>();
            phTxt.text = elem.placeholderText ?? "Enter text...";
            phTxt.fontSize = elem.fontSize > 0 ? elem.fontSize : 14;
            phTxt.fontStyle = FontStyle.Italic;
            phTxt.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            phTxt.font = defaultFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            SetFullStretch(placeholder.GetComponent<RectTransform>());

            GameObject textGO = new GameObject("Text",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            Text txt = textGO.GetComponent<Text>();
            txt.fontSize = elem.fontSize > 0 ? elem.fontSize : 14;
            txt.color = elem.textColor != null ? elem.textColor.ToColor() : Color.black;
            txt.font = defaultFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.alignment = TextAnchor.MiddleLeft;
            SetFullStretch(textGO.GetComponent<RectTransform>());

            InputField input = go.GetComponent<InputField>();
            input.textComponent = txt;
            input.placeholder = phTxt;
            input.characterLimit = elem.characterLimit > 0 ? elem.characterLimit : 0;
            input.interactable = elem.interactable;
            input.targetGraphic = bgImg;

            return go;
        }

        #endregion

        #region Helpers

        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

        private static Sprite LoadSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteFolder) || string.IsNullOrEmpty(spriteName))
                return null;

            // 构建缓存 key：spriteFolder + spriteName
            string cacheKey = spriteFolder + "|" + spriteName;
            if (spriteCache.TryGetValue(cacheKey, out Sprite cached))
                return cached;

            string[] extensions = { ".png", ".jpg", ".jpeg", ".psd" };
            foreach (string ext in extensions)
            {
                string path = Path.Combine(spriteFolder, spriteName + ext).Replace("\\", "/");
                Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null)
                {
                    spriteCache[cacheKey] = s;
                    return s;
                }
            }

            string noExtPath = Path.Combine(spriteFolder, spriteName).Replace("\\", "/");
            Sprite fallback = AssetDatabase.LoadAssetAtPath<Sprite>(noExtPath);
            if (fallback != null)
            {
                spriteCache[cacheKey] = fallback;
                return fallback;
            }

            Debug.LogWarning($"[UGUIBuilder] Sprite not found: {spriteName} in {spriteFolder}");
            return null;
        }

        /// <summary>
        /// 清除 Sprite 查找缓存（切换 spriteFolder 后调用）
        /// </summary>
        public static void ClearSpriteCache() => spriteCache.Clear();

        /// <summary>
        /// 根据 layoutType 添加 LayoutGroup 组件到 GameObject
        /// </summary>
        private static void ApplyLayoutGroup(GameObject go, LayoutElement elem)
        {
            ApplyLayoutGroupTo(go, elem);
        }

        private static void ApplyLayoutGroupTo(GameObject go, LayoutElement elem)
        {
            if (string.IsNullOrEmpty(elem.layoutType) || elem.layoutType == "None")
                return;

            TextAnchor alignment = elem.childAlignment switch
            {
                "UpperLeft"     => TextAnchor.UpperLeft,
                "UpperCenter"   => TextAnchor.UpperCenter,
                "UpperRight"    => TextAnchor.UpperRight,
                "MiddleLeft"    => TextAnchor.MiddleLeft,
                "MiddleCenter"  => TextAnchor.MiddleCenter,
                "MiddleRight"   => TextAnchor.MiddleRight,
                "LowerLeft"     => TextAnchor.LowerLeft,
                "LowerCenter"   => TextAnchor.LowerCenter,
                "LowerRight"    => TextAnchor.LowerRight,
                _               => TextAnchor.MiddleCenter
            };

            if (elem.layoutType == "Grid")
            {
                GridLayoutGroup glg = go.AddComponent<GridLayoutGroup>();
                glg.padding = new RectOffset(
                    elem.paddingLeft, elem.paddingRight,
                    elem.paddingTop, elem.paddingBottom);
                glg.spacing = elem.gridSpacing != null
                    ? elem.gridSpacing.ToVector2()
                    : Vector2.zero;
                glg.cellSize = elem.gridCellSize != null
                    ? elem.gridCellSize.ToVector2()
                    : new Vector2(100, 100);
                glg.childAlignment = alignment;
                glg.constraint = elem.gridConstraint switch
                {
                    "FixedColumnCount" => GridLayoutGroup.Constraint.FixedColumnCount,
                    "FixedRowCount"    => GridLayoutGroup.Constraint.FixedRowCount,
                    _                  => GridLayoutGroup.Constraint.Flexible
                };
                glg.constraintCount = Mathf.Max(1, elem.gridConstraintCount);
            }
            else
            {
                HorizontalOrVerticalLayoutGroup lg;
                if (elem.layoutType == "Horizontal")
                    lg = go.AddComponent<HorizontalLayoutGroup>();
                else
                    lg = go.AddComponent<VerticalLayoutGroup>();

                lg.padding = new RectOffset(
                    elem.paddingLeft, elem.paddingRight,
                    elem.paddingTop, elem.paddingBottom);
                lg.spacing = elem.spacing;
                lg.childAlignment = alignment;
                lg.childControlWidth = elem.childControlWidth;
                lg.childControlHeight = elem.childControlHeight;
                lg.childForceExpandWidth = elem.childForceExpandWidth;
                lg.childForceExpandHeight = elem.childForceExpandHeight;
            }
        }

        private static void SetFullStretch(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private static void SavePrefab(GameObject root, string prefabPath)
        {
            string dir = Path.GetDirectoryName(prefabPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (!prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                prefabPath += ".prefab";

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            GameObject.DestroyImmediate(root);
            spriteCache.Clear();
        }

        #endregion

        #region Validation

        /// <summary>
        /// 校验 JSON 布局的合法性，返回 (有错误?, 消息列表)。
        /// 递归检查所有元素的类型、枚举值、颜色范围、必填字段等。
        /// </summary>
        public static (bool hasErrors, List<string> messages) ValidateLayout(LayoutRoot layout)
        {
            var messages = new List<string>();

            if (layout == null)
            {
                messages.Add("[ERROR] 根布局为 null，JSON 解析失败。");
                return (true, messages);
            }

            if (string.IsNullOrEmpty(layout.screenName))
                messages.Add("[WARN] screenName 为空，将使用默认名称。");

            if (layout.elements == null || layout.elements.Count == 0)
            {
                messages.Add("[WARN] elements 数组为空，不会生成任何 UI 元素。");
                return (false, messages);
            }

            var siblingNames = new HashSet<string>();
            foreach (var elem in layout.elements)
            {
                ValidateElementRecursive(elem, "", siblingNames, messages);
            }

            int errorCount = messages.Count(m => m.StartsWith("[ERROR]"));
            int warnCount  = messages.Count(m => m.StartsWith("[WARN]"));
            messages.Add($"[INFO] 校验完成：{errorCount} 个错误，{warnCount} 个警告，共 {CountElements(layout)} 个元素。");

            return (errorCount > 0, messages);
        }

        private static int CountElements(LayoutRoot layout)
        {
            int count = 0;
            if (layout.elements != null)
                foreach (var e in layout.elements)
                    CountRecursive(e, ref count);
            return count;
        }

        private static void CountRecursive(LayoutElement elem, ref int count)
        {
            count++;
            if (elem.children != null)
                foreach (var c in elem.children)
                    CountRecursive(c, ref count);
        }

        private static void ValidateElementRecursive(LayoutElement elem, string parentPath,
            HashSet<string> siblingNames, List<string> messages)
        {
            string fullPath = string.IsNullOrEmpty(parentPath) ? elem.name : $"{parentPath}/{elem.name}";

            // Name
            if (string.IsNullOrEmpty(elem.name))
            {
                messages.Add($"[ERROR] 元素在 '{parentPath}' 下缺少 name 字段。");
                return;
            }

            if (siblingNames.Contains(elem.name))
                messages.Add($"[WARN] {fullPath}: 同级存在重名元素。");
            else
                siblingNames.Add(elem.name);

            // Type
            var validTypes = new HashSet<string>
                { "Image", "Panel", "Button", "Text", "Toggle", "ToggleGroup", "ScrollRect", "InputField" };
            if (string.IsNullOrEmpty(elem.type))
                messages.Add($"[ERROR] {fullPath}.type: 未指定元素类型。");
            else if (!validTypes.Contains(elem.type))
                messages.Add($"[ERROR] {fullPath}.type: '{elem.type}' 无效。有效值: {string.Join(", ", validTypes)}。");

            // Color range
            ValidateColor(fullPath + ".color",     elem.color,      messages);
            ValidateColor(fullPath + ".textColor", elem.textColor,  messages);
            ValidateColor(fullPath + ".outlineColor", elem.outlineColor, messages);

            // Type-specific
            switch (elem.type)
            {
                case "Image":       ValidateImageType(elem, fullPath, messages);    break;
                case "Text":        ValidateTextFields(elem, fullPath, messages);   break;
                case "ScrollRect":  ValidateScrollRect(elem, fullPath, messages);   break;
                case "InputField":  ValidateInputField(elem, fullPath, messages);   break;
                case "ToggleGroup":
                    if (elem.children == null || elem.children.Count == 0)
                        messages.Add($"[WARN] {fullPath}: ToggleGroup 没有任何子元素。");
                    break;
            }

            // Layout
            if (!string.IsNullOrEmpty(elem.layoutType) && elem.layoutType != "None")
                ValidateLayoutGroup(elem, fullPath, messages);

            // Children recursion
            if (elem.children != null && elem.children.Count > 0)
            {
                var childNames = new HashSet<string>();
                foreach (var child in elem.children)
                    ValidateElementRecursive(child, fullPath, childNames, messages);
            }
        }

        private static void ValidateColor(string path, ColorData c, List<string> messages)
        {
            if (c == null) return;
            if (c.r < 0 || c.r > 1 || c.g < 0 || c.g > 1 || c.b < 0 || c.b > 1 || c.a < 0 || c.a > 1)
                messages.Add($"[WARN] {path}: RGBA 值 ({c.r},{c.g},{c.b},{c.a}) 应在 0~1 范围。");
        }

        private static void ValidateImageType(LayoutElement elem, string fp, List<string> messages)
        {
            var valid = new HashSet<string> { "Simple", "Sliced", "Tiled", "Filled" };
            if (!string.IsNullOrEmpty(elem.imageType) && !valid.Contains(elem.imageType))
                messages.Add($"[ERROR] {fp}.imageType: '{elem.imageType}' 无效。有效值: {string.Join(", ", valid)}。");

            if (elem.imageType == "Filled")
            {
                var fm = new HashSet<string> { "Horizontal", "Vertical", "Radial90", "Radial180", "Radial360" };
                if (!string.IsNullOrEmpty(elem.fillMethod) && !fm.Contains(elem.fillMethod))
                    messages.Add($"[ERROR] {fp}.fillMethod: '{elem.fillMethod}' 无效。有效值: {string.Join(", ", fm)}。");
                if (elem.fillAmount < 0 || elem.fillAmount > 1)
                    messages.Add($"[WARN] {fp}.fillAmount: {elem.fillAmount} 不在 [0,1] 范围。");
            }
        }

        private static void ValidateTextFields(LayoutElement elem, string fp, List<string> messages)
        {
            var fonts = new HashSet<string> { "Normal", "Bold", "Italic", "BoldItalic" };
            if (!string.IsNullOrEmpty(elem.fontStyle) && !fonts.Contains(elem.fontStyle))
                messages.Add($"[ERROR] {fp}.fontStyle: '{elem.fontStyle}' 无效。有效值: {string.Join(", ", fonts)}。");

            var aligns = new HashSet<string>
                { "Left", "Center", "Right", "TopLeft", "Top", "TopRight", "BottomLeft", "Bottom", "BottomRight" };
            if (!string.IsNullOrEmpty(elem.textAlignment) && !aligns.Contains(elem.textAlignment))
                messages.Add($"[ERROR] {fp}.textAlignment: '{elem.textAlignment}' 无效。有效值: {string.Join(", ", aligns)}。");

            if (elem.fontSize <= 0)
                messages.Add($"[WARN] {fp}.fontSize: {elem.fontSize} 无效，将使用默认值 14。");
        }

        private static void ValidateScrollRect(LayoutElement elem, string fp, List<string> messages)
        {
            var vm = new HashSet<string> { "Clamped", "Unrestricted", "Elastic" };
            if (!string.IsNullOrEmpty(elem.scrollMovementType) && !vm.Contains(elem.scrollMovementType))
                messages.Add($"[ERROR] {fp}.scrollMovementType: '{elem.scrollMovementType}' 无效。有效值: {string.Join(", ", vm)}。");
            if (elem.scrollSensitivity <= 0)
                messages.Add($"[WARN] {fp}.scrollSensitivity: {elem.scrollSensitivity} 非正数。");
        }

        private static void ValidateInputField(LayoutElement elem, string fp, List<string> messages)
        {
            var ct = new HashSet<string> { "Standard","Integer","Decimal","AlphaNumeric","Name","EmailAddress","Password","Pin","Custom" };
            if (!string.IsNullOrEmpty(elem.contentType) && !ct.Contains(elem.contentType))
                messages.Add($"[ERROR] {fp}.contentType: '{elem.contentType}' 无效。有效值: {string.Join(", ", ct)}。");

            var lt = new HashSet<string> { "SingleLine", "MultiLineSubmit", "MultiLineNewline" };
            if (!string.IsNullOrEmpty(elem.lineType) && !lt.Contains(elem.lineType))
                messages.Add($"[ERROR] {fp}.lineType: '{elem.lineType}' 无效。有效值: {string.Join(", ", lt)}。");

            if (elem.characterLimit < 0)
                messages.Add($"[WARN] {fp}.characterLimit: {elem.characterLimit} 为负数。");
        }

        private static void ValidateLayoutGroup(LayoutElement elem, string fp, List<string> messages)
        {
            var vl = new HashSet<string> { "Horizontal", "Vertical", "Grid" };
            if (!vl.Contains(elem.layoutType))
                messages.Add($"[ERROR] {fp}.layoutType: '{elem.layoutType}' 无效。有效值: {string.Join(", ", vl)}。");

            var va = new HashSet<string>
                { "UpperLeft","UpperCenter","UpperRight","MiddleLeft","MiddleCenter","MiddleRight","LowerLeft","LowerCenter","LowerRight" };
            if (!string.IsNullOrEmpty(elem.childAlignment) && !va.Contains(elem.childAlignment))
                messages.Add($"[ERROR] {fp}.childAlignment: '{elem.childAlignment}' 无效。有效值: {string.Join(", ", va)}。");

            if (elem.layoutType == "Grid")
            {
                var vg = new HashSet<string> { "Flexible", "FixedColumnCount", "FixedRowCount" };
                if (!string.IsNullOrEmpty(elem.gridConstraint) && !vg.Contains(elem.gridConstraint))
                    messages.Add($"[ERROR] {fp}.gridConstraint: '{elem.gridConstraint}' 无效。有效值: {string.Join(", ", vg)}。");
                if (elem.gridConstraintCount <= 0)
                    messages.Add($"[WARN] {fp}.gridConstraintCount: {elem.gridConstraintCount} 无效。");
            }
        }

        #endregion
    }
}
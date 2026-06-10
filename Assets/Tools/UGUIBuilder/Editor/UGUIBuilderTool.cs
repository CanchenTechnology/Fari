using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

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

        /// <summary>
        /// 入口：从 JSON 构建 UGUI Prefab
        /// </summary>
        /// <param name="overrideFont">可选：覆盖 JSON 中指定的字体。传入 null 则使用 JSON 中的 defaultFont 路径</param>
        public static void BuildUGUIFromJSON(string jsonPath, string spriteFolderPath, string prefabOutputPath, Font overrideFont = null)
        {
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[UGUIBuilder] JSON file not found: {jsonPath}");
                return;
            }

            spriteFolder = spriteFolderPath;
            defaultFont = null;

            string jsonText = File.ReadAllText(jsonPath);
            LayoutRoot layout;

            try
            {
                layout = JsonUtility.FromJson<LayoutRoot>(jsonText);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UGUIBuilder] JSON parse failed: {e.Message}");
                return;
            }

            if (layout == null)
            {
                Debug.LogError("[UGUIBuilder] Layout is null. Check JSON structure.");
                return;
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

            SavePrefab(canvasGO, prefabOutputPath);
            Debug.Log($"[UGUIBuilder] Build complete: {prefabOutputPath}");
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

        private static GameObject CreateOrGetCanvas(LayoutRoot layout)
        {
            Canvas existingCanvas = GameObject.FindObjectOfType<Canvas>();
            GameObject canvasGO;

            if (existingCanvas != null)
            {
                canvasGO = existingCanvas.gameObject;
            }
            else
            {
                canvasGO = new GameObject("Canvas");
                Canvas canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = layout.resolution.ToVector2();
                scaler.matchWidthOrHeight = layout.matchWidthOrHeight;
            }

            if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            canvasGO.name = layout.screenName;
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
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            uiMask.layer = LayerMask.NameToLayer("UI");
            uiMask.SetActive(false);

            RectTransform rt = uiMask.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(3433, 1958);
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
                case "Image": go = BuildImage(elem); break;
                case "Panel": go = BuildPanel(elem); break;
                case "Button": go = BuildButton(elem); break;
                case "Text": go = BuildText(elem); break;
                case "Toggle": go = BuildToggle(elem); break;
                case "ToggleGroup": go = BuildToggleGroup(elem); break;
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

            if (elem.children != null && elem.children.Count > 0)
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
            img.raycastTarget = true;
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
                "Bold" => FontStyle.Bold,
                "Italic" => FontStyle.Italic,
                "BoldItalic" => FontStyle.BoldAndItalic,
                _ => FontStyle.Normal
            };

            txt.alignment = elem.textAlignment switch
            {
                "Left" => TextAnchor.MiddleLeft,
                "Right" => TextAnchor.MiddleRight,
                "Center" => TextAnchor.MiddleCenter,
                "TopLeft" => TextAnchor.UpperLeft,
                "Top" => TextAnchor.UpperCenter,
                "TopRight" => TextAnchor.UpperRight,
                "BottomLeft" => TextAnchor.LowerLeft,
                "Bottom" => TextAnchor.LowerCenter,
                "BottomRight" => TextAnchor.LowerRight,
                _ => TextAnchor.MiddleCenter
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
            checkImg.color = new Color(0.107f, 1, 0, 1);
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
                RectTransform labelRT = labelGO.GetComponent<RectTransform>();
                labelRT.anchorMin = Vector2.zero;
                labelRT.anchorMax = Vector2.one;
                labelRT.anchoredPosition = Vector2.zero;
                labelRT.sizeDelta = Vector2.zero;
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

        #endregion

        #region Helpers

        private static Sprite LoadSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteFolder) || string.IsNullOrEmpty(spriteName))
                return null;

            string[] extensions = { ".png", ".jpg", ".jpeg", ".psd" };
            foreach (string ext in extensions)
            {
                string path = Path.Combine(spriteFolder, spriteName + ext).Replace("\\", "/");
                Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) return s;
            }

            string noExtPath = Path.Combine(spriteFolder, spriteName).Replace("\\", "/");
            Sprite fallback = AssetDatabase.LoadAssetAtPath<Sprite>(noExtPath);
            if (fallback != null) return fallback;

            Debug.LogWarning($"[UGUIBuilder] Sprite not found: {spriteName} in {spriteFolder}");
            return null;
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

            AssetDatabase.Refresh();
        }

        #endregion
    }
}
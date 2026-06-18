using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GamerFrameWork.UIFrameWork
{
    public static class ComponentNamePrefixTool
    {
        private const string AddPrefixMenuPath = "GameObject/GamerFrameWork/UIFrame/添加组件名前缀";
        private const string RemovePrefixMenuPath = "GameObject/GamerFrameWork/UIFrame/移除组件名前缀";

        private static readonly string[] PriorityComponentNames =
        {
            "Button",
            "Image",
            "RawImage",
            "Toggle",
            "Slider",
            "Scrollbar",
            "ScrollRect",
            "Dropdown",
            "TMP_Dropdown",
            "InputField",
            "TMP_InputField",
            "Text",
            "TextMeshProUGUI",
            "TMP_Text",
            "Canvas",
            "CanvasGroup",
            "Animator",
            "RectTransform",
            "Transform"
        };

        [MenuItem(AddPrefixMenuPath, false, 20)]
        private static void AddComponentNamePrefix()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先在 Hierarchy 中选择一个或多个对象。", "确定");
                return;
            }

            int renameCount = 0;
            foreach (GameObject selectedObject in selectedObjects)
            {
                if (selectedObject == null)
                {
                    continue;
                }

                string componentName = GetPreferredComponentName(selectedObject);
                string rawName = RemoveExistingComponentPrefix(selectedObject.name);
                string newName = $"[{componentName}]{rawName}";
                if (selectedObject.name == newName)
                {
                    continue;
                }

                Undo.RecordObject(selectedObject, "Add Component Name Prefix");
                selectedObject.name = newName;
                EditorUtility.SetDirty(selectedObject);
                renameCount++;
            }

            Debug.Log($"组件名前缀添加完成，重命名对象数量：{renameCount}");
        }

        [MenuItem(AddPrefixMenuPath, true)]
        private static bool ValidateAddComponentNamePrefix()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        [MenuItem(RemovePrefixMenuPath, false, 21)]
        private static void RemoveComponentNamePrefix()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先在 Hierarchy 中选择一个或多个对象。", "确定");
                return;
            }

            int renameCount = 0;
            foreach (GameObject selectedObject in selectedObjects)
            {
                if (selectedObject == null)
                {
                    continue;
                }

                string newName = RemoveExistingComponentPrefix(selectedObject.name);
                if (selectedObject.name == newName)
                {
                    continue;
                }

                Undo.RecordObject(selectedObject, "Remove Component Name Prefix");
                selectedObject.name = newName;
                EditorUtility.SetDirty(selectedObject);
                renameCount++;
            }

            Debug.Log($"组件名前缀移除完成，重命名对象数量：{renameCount}");
        }

        [MenuItem(RemovePrefixMenuPath, true)]
        private static bool ValidateRemoveComponentNamePrefix()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        private static string GetPreferredComponentName(GameObject target)
        {
            Component[] components = target.GetComponents<Component>();
            string customScriptName = GetFirstCustomScriptName(components);
            if (!string.IsNullOrEmpty(customScriptName))
            {
                return customScriptName;
            }

            foreach (string componentName in PriorityComponentNames)
            {
                if (components.Any(component => component != null && component.GetType().Name == componentName))
                {
                    return componentName;
                }
            }

            return "Transform";
        }

        private static string GetFirstCustomScriptName(IEnumerable<Component> components)
        {
            foreach (Component component in components)
            {
                if (!(component is MonoBehaviour monoBehaviour) || monoBehaviour == null)
                {
                    continue;
                }

                MonoScript monoScript = MonoScript.FromMonoBehaviour(monoBehaviour);
                if (monoScript == null)
                {
                    continue;
                }

                string scriptPath = AssetDatabase.GetAssetPath(monoScript);
                if (string.IsNullOrEmpty(scriptPath) || scriptPath.StartsWith("Packages/"))
                {
                    continue;
                }

                return component.GetType().Name;
            }

            return null;
        }

        private static string RemoveExistingComponentPrefix(string objectName)
        {
            if (string.IsNullOrEmpty(objectName) || objectName[0] != '[')
            {
                return objectName;
            }

            int endIndex = objectName.IndexOf(']');
            if (endIndex <= 0)
            {
                return objectName;
            }

            return objectName.Substring(endIndex + 1);
        }
    }
}

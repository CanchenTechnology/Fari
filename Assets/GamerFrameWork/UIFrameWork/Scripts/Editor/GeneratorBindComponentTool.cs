using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GamerFrameWork.UIFrameWork
{
    public class EditorObjectData
    {
        public int insID;
        public string fieldName;
        public string fieldType;
        public List<EditorObjectData> dataList;
    }
    public class GeneratorBindComponentTool : Editor
    {
        private const string GeneratorClassPathKey = "GeneratorClassPath";
        private const string GeneratorTargetObjectKey = "GeneratorTargetObject";

        public static List<EditorObjectData> objDataList;//查找对象的数据
        public static WindowLayer selectedLayer = WindowLayer.MainUI; // 默认选择MainUI

        // 原来的菜单项改为弹出下拉框
        [MenuItem("GameObject/GamerFrameWork/UIFrame/生成组件拖动赋值脚本(Shift+B) #B", false, 0)]
        static void ShowSelectLayerWindow()
        {
            GameObject obj = Selection.activeGameObject;//获取到当前选择的物体
            if (obj == null)
            {
                // 弹出警告对话框
                EditorUtility.DisplayDialog("警告", "请选择一个对象!", "确定");
                return;
            }

            // 打开窗口
            GeneratorSelectLayerWindow.Init(obj);
        }
        public static string CreateCS(string name)
        {
            StringBuilder sb = new StringBuilder();
            string nameSpaceName = "GamerFrameWork.UIFrameWork";
            //添加引用
            sb.AppendLine("/*---------------------------------");
            sb.AppendLine(" *Title:UI自动化组件生成代码生成工具");
            sb.AppendLine(" *Author:GamerFrameWork-UIFrameWork");
            sb.AppendLine(" *Date:" + System.DateTime.Now);
            sb.AppendLine(" *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可");
            sb.AppendLine(" *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成");
            sb.AppendLine("---------------------------------*/");
            foreach (string nameSpace in UISetting.Instance.UsingNameSpaceArr)
            {
                sb.AppendLine($"using {nameSpace};");
            }
            sb.AppendLine();
            sb.AppendLine($"public class {name}{UISetting.Instance.GenerateCSharpSuffix}:MonoBehaviour");
            sb.AppendLine("{");
            sb.AppendLine($"\tpublic WindowLayer windowLayer = WindowLayer.{selectedLayer};");
            //根据字段数据列表 声明字段
            foreach (var item in objDataList)
            {
                if (item.dataList != null)
                {
                    sb.AppendLine($"\tpublic {item.fieldType}[] {item.fieldName}{item.fieldType}Array;\n");
                }
                else
                {
                    sb.AppendLine("\tpublic " + item.fieldType + " " + item.fieldName + item.fieldType + ";"); //添加一行
                }
            }

            //声明初始化组件接口
            sb.AppendLine("\tpublic void InitComponent(WindowBase target)");
            sb.AppendLine("\t{");
            sb.AppendLine("\t    //组件事件绑定");
            sb.AppendLine("\t    target.Canvas.sortingOrder = (int)windowLayer;");
            sb.AppendLine("\t    target.Layer = windowLayer;");
            //得到逻辑类 WindowBase => LoginWindow
            sb.AppendLine($"\t    {name} mWindow=({name})target;");

            //生成UI事件绑定代码
            foreach (var item in objDataList)
            {
                string type = item.fieldType;
                string methodName = item.fieldName;
                string suffix = "";
                if (type.Contains("Button"))
                {
                    suffix = "Click";
                    sb.AppendLine($"\t    target.AddButtonClickListener({methodName}{type},mWindow.On{methodName}Button{suffix});");
                }
                if (type.Contains("InputField"))
                {
                    sb.AppendLine($"\t    target.AddInputFieldListener({methodName}{type},mWindow.On{methodName}InputChange,mWindow.On{methodName}InputEnd);");
                }
                if (type.Contains("Toggle"))
                {
                    suffix = "Change";
                    sb.AppendLine($"\t    target.AddToggleClickListener({methodName}{type},mWindow.On{methodName}Toggle{suffix});");
                }
            }
            sb.AppendLine("\t}");
            if (!string.IsNullOrEmpty(nameSpaceName))
            {
                sb.AppendLine("}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 当脚本编译完成后系统自动调用:自动添加组件到对象上，并且自动赋值
        /// </summary>
        [UnityEditor.Callbacks.DidReloadScripts]
        public static void AddComponent2Window()
        {
            // 如果当前不是生成数据脚本的回调,就不处理
            string scriptPath = EditorPrefs.GetString(GeneratorClassPathKey);
            if (string.IsNullOrEmpty(scriptPath))
            {
                return;
            }
            scriptPath = ToAssetPath(scriptPath);

            //通过反射的方式,从程序集中找到这个脚本，把它挂载到当前的物体上
            System.Type targetScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath)?.GetClass();//MonoScript文件就是.cs
            if (targetScript == null)
            {
                Debug.LogError($"Failed to load generated component script: {scriptPath}");
                return;
            }
            //获取要挂载的那个物体
            GameObject selectedObject = EditorUtility.InstanceIDToObject(EditorPrefs.GetInt(GeneratorTargetObjectKey, 0)) as GameObject;
            if (selectedObject == null)
            {
                selectedObject = Selection.activeGameObject;
            }
            if (selectedObject == null)
            {
                // 弹出警告对话框
                EditorUtility.DisplayDialog("警告", "请选择一个对象!", "确定");
                ClearGeneratorPrefs();
                return;
            }
            //先获取现窗口上有没有挂载该数据组件，如果没挂载在进行挂载
            Component compt = selectedObject.GetComponent(targetScript);
            if (compt == null)
            {
                compt = selectedObject.AddComponent(targetScript);
            }
            // 2.通过反射的方式,遍历数据列表,找到对应的字段，进行赋值
            // 获取对象数据列表
            string datalistJson = PlayerPrefs.GetString(UISetting.OBJDATALIST_KEY);
            if (string.IsNullOrEmpty(datalistJson))
            {
                Debug.LogError("没有找到对象数据列表 PlayerPrefs！");
                ClearGeneratorPrefs();
                return;
            }
            List<EditorObjectData> editorObjDataList =
                JsonConvert.DeserializeObject<List<EditorObjectData>>(datalistJson);
            if (editorObjDataList == null)
            {
                Debug.LogError("对象数据列表反序列化失败！");
                ClearGeneratorPrefs();
                return;
            }

            //获取脚本所有字段
            FieldInfo[] fieldInfoList = targetScript.GetFields();

            foreach (var item in fieldInfoList) //脚本里的字段,该字段自动生成,Name+Type
            {
                foreach (var objData in editorObjDataList) //对象里的字段
                {
                    if (string.Equals(item.Name, $"{objData.fieldName}{objData.fieldType}") || string.Equals(item.Name,$"{objData.fieldName}{objData.fieldType}Array"))
                    { 
                        //根据insid找到对应的对象
                        GameObject uiObject = EditorUtility.InstanceIDToObject(objData.insID) as GameObject;
                        if (uiObject == null)
                        {
                            Debug.LogWarning($"字段 {item.Name} 对应的对象不存在，已跳过。");
                            break;
                        }
                        if (objData.dataList == null) //说明不是数组类型
                        {
                            //设置该字段所对应的对象
                            if (string.Equals(objData.fieldType, "GameObject"))
                            {
                                item.SetValue(compt, uiObject);
                            }
                            else
                            {
                                Component component = uiObject.GetComponent(objData.fieldType);
                                if (component == null)
                                {
                                    Debug.LogWarning($"对象 {uiObject.name} 上没有找到组件 {objData.fieldType}，字段 {item.Name} 未赋值。");
                                }
                                item.SetValue(compt, component);
                            }
                        }
                        else
                        {
                            if (objData.fieldType.Contains("GameObject"))
                            {
                                GameObject[] newArray = new GameObject[objData.dataList.Count];
                                for (int i = 0; i < objData.dataList.Count; i++)
                                {
                                    newArray[i] = EditorUtility.InstanceIDToObject(objData.dataList[i].insID) as GameObject;
                                }
                                item.SetValue(compt, newArray);
                            }
                            else
                            {
                                // 获取数组类型
                                Type arrayType = item.FieldType;
                                // 获取数组元素类型
                                Type elementType = arrayType.GetElementType();
                                //获取该节点下的所有的物体
                                Component[] components = uiObject.GetComponentsInChildren(elementType, true);
                                // 创建目标数组
                                Array targetArray = Array.CreateInstance(elementType, components.Length);

                                // 将组件赋值给目标数组
                                for (int i = 0; i < components.Length; i++)
                                {
                                    if (components[i] != null && elementType.IsAssignableFrom(components[i].GetType()))
                                    {
                                        targetArray.SetValue(components[i], i);
                                    }
                                    else
                                    {
                                        Debug.LogError($"Element at index {i} is not of type {elementType.Name}!");
                                    }
                                }
                                // 设置字段的值
                                item.SetValue(compt, targetArray);
                            }
                        }
                        break;
                    }
                    
                }
            }

            // 清理标记
            ClearGeneratorPrefs();
            PlayerPrefs.DeleteKey(UISetting.OBJDATALIST_KEY);

            // 自动保存 prefab（仅当对象是 Prefab 实例时）
            EditorUtility.SetDirty(compt);
            PrefabUtility.RecordPrefabInstancePropertyModifications(compt);
            var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(selectedObject);
            if (prefabRoot != null && PrefabUtility.IsPartOfPrefabInstance(selectedObject))
            {
                PrefabUtility.ApplyPrefabInstance(prefabRoot, InteractionMode.AutomatedAction);
            }
            else
            {
                Debug.LogWarning("当前选中的对象不是 Prefab 实例，自动 Apply 已跳过。");
            }
        }

        /// <summary>
        /// 真正执行生成脚本逻辑
        /// </summary>
        public static void CreateFindComponentScripts(GameObject obj, WindowLayer layer)
        {
            objDataList = new List<EditorObjectData>();

            //设置脚本生成路径
            if (!Directory.Exists(UISetting.Instance.BindComponentGeneratorPath))
            {
                Directory.CreateDirectory(UISetting.Instance.BindComponentGeneratorPath);
            }  
            //解析窗口组件数据
            AnalysisComponentDataTool.AnalysisWindowNodeData(ref objDataList, obj.transform, obj.name);

            //储存字段名称
            string datalistJson = JsonConvert.SerializeObject(objDataList);
            PlayerPrefs.SetString(UISetting.OBJDATALIST_KEY, datalistJson);

            //生成CS脚本
            string csContent = CreateCS(obj.name);
            Debug.Log("CsConent:\n" + csContent);
            string cspath = UISetting.Instance.BindComponentGeneratorPath + "/" + obj.name + $"{UISetting.Instance.GenerateCSharpSuffix}.cs";
            UIWindowEditor.ShowWindow(csContent, cspath, generatorPathPrefsKey: GeneratorClassPathKey);
            EditorPrefs.SetString(GeneratorClassPathKey, cspath);
            EditorPrefs.SetInt(GeneratorTargetObjectKey, obj.GetInstanceID());

            Debug.Log($"已为 {obj.name} 在层级 {layer} 生成脚本");
        }

        private static string ToAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            path = path.Replace("\\", "/");
            string dataPath = Application.dataPath.Replace("\\", "/");
            if (path.StartsWith(dataPath, StringComparison.Ordinal))
            {
                return "Assets" + path.Substring(dataPath.Length);
            }

            return path;
        }

        private static void ClearGeneratorPrefs()
        {
            EditorPrefs.DeleteKey(GeneratorClassPathKey);
            EditorPrefs.DeleteKey(GeneratorTargetObjectKey);
            EditorPrefs.DeleteKey("WindowLayer");
        }
    }

    /// <summary>
    /// 弹出窗口：选择 WindowLayer
    /// </summary>
    public class GeneratorSelectLayerWindow : EditorWindow
    {
        private static GameObject selectedObj;

        // 这两组用于下拉显示
        private static WindowLayer[] layerValues;
        private static string[] layerDisplayNames;
        private static int selectedIndex;

        public static void Init(GameObject obj)
        {
            selectedObj = obj;

            // 构建显示数组（名字+枚举值）
            Array values = Enum.GetValues(typeof(WindowLayer));
            layerValues = new WindowLayer[values.Length];
            layerDisplayNames = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                WindowLayer layer = (WindowLayer)values.GetValue(i);
                layerValues[i] = layer;
                int intValue = (int)layer; //拿到枚举的数字
                layerDisplayNames[i] = $"{layer} ({intValue})"; //组合显示
            }

            // 默认选中MainUI
            selectedIndex = Array.IndexOf(layerValues, WindowLayer.MainUI);
            if (selectedIndex < 0) selectedIndex = 0;

            var window = GetWindow<GeneratorSelectLayerWindow>("选择层级");
            window.position = new Rect(Screen.width / 2, Screen.height / 2, 400, 150);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("选择 UI 窗口层级：", EditorStyles.boldLabel);

            // 下拉框显示成 “名字 (值)”
            selectedIndex = EditorGUILayout.Popup("窗口层级", selectedIndex, layerDisplayNames);

            GUILayout.Space(10);

            if (GUILayout.Button("确定并生成脚本"))
            {
                // 得到真实枚举值
                WindowLayer selectedLayer = layerValues[selectedIndex];
                GeneratorBindComponentTool.selectedLayer = selectedLayer;
                EditorPrefs.SetInt("WindowLayer", (int)selectedLayer);
                GeneratorBindComponentTool.CreateFindComponentScripts(selectedObj, selectedLayer);
                Close();
            }
        }
    }

}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using System.Text;
namespace GamerFrameWork.UIFrameWork
{
#if UNITY_EDITOR
    public class GeneratorWindowTool : Editor
    {
        public static List<EditorObjectData> objDataList;//查找对象的数据
        static Dictionary<string, string> methodDic = new Dictionary<string, string>();  //Key:函数名,Value:函数内容
        [MenuItem("GameObject/GamerFrameWork/UIFrame/生成Window脚本(Shift+V) #V", false, 1)]
        static void CreateFindComponentScripts()
        {
            GameObject obj = Selection.activeGameObject; // 获取当前选中的物体
            if (obj == null)
            {
                // 弹出警告对话框
                EditorUtility.DisplayDialog("警告", "请选择一个对象!", "确定");
                return;
            }
            objDataList = new List<EditorObjectData>();
            //设置脚本生成路径
            if (!Directory.Exists(UISetting.Instance.WindowGeneratorPath))
            {
                Directory.CreateDirectory(UISetting.Instance.WindowGeneratorPath);
            }
            //解析窗口组件数据
            AnalysisComponentDataTool.AnalysisWindowNodeData(ref objDataList,obj.transform,obj.name);
            //生成CS脚本
            string csContnet = CreateWindoCs(obj.name);

            Debug.Log("CsConent:\n" + csContnet);
            string cspath = UISetting.Instance.WindowGeneratorPath + "/" + obj.name + ".cs";
            UIWindowEditor.ShowWindow(csContnet, cspath, methodDic);

        }
        /// <summary>
        /// 生成Window脚本
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string CreateWindoCs(string name)
        {
            methodDic.Clear();
            StringBuilder sb = new StringBuilder();

            // 添加引用
            sb.AppendLine("/*---------------------------------");
            sb.AppendLine(" * Title: UI表现层脚本自动化生成工具-不会被覆盖");
            sb.AppendLine(" * Author: GamerFrameWork");
            sb.AppendLine(" * Date: " + System.DateTime.Now);
            sb.AppendLine(" * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码");
            sb.AppendLine(" * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用");
            sb.AppendLine("---------------------------------*/");
            sb.AppendLine("using UnityEngine.UI;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using GamerFrameWork.UIFrameWork;");
            sb.AppendLine();

            // 生成类名
            sb.AppendLine($"public class {name} : WindowBase");
            sb.AppendLine("{");

            sb.AppendLine($"\tpublic {name}{UISetting.Instance.GenerateCSharpSuffix} uiComponent;");

            // 生成生命周期函数
            sb.AppendLine();
            sb.AppendLine("\t#region 生命周期函数");
            sb.AppendLine("\t// 调用机制与 Mono Awake 一致");
            sb.AppendLine("\tpublic override void OnAwake()");
            sb.AppendLine("\t{");

            sb.AppendLine($"\t\tuiComponent = gameObject.GetComponent<{name}{UISetting.Instance.GenerateCSharpSuffix}>();");
            sb.AppendLine("\t\tuiComponent.InitComponent(this);");

            sb.AppendLine("\t\tthis.Canvas.sortingOrder = (int)uiComponent.windowLayer;");
            sb.AppendLine("\t\tbase.OnAwake();");
            sb.AppendLine("\t}");

            // OnShow
            sb.AppendLine("\t// 物体显示时执行");
            sb.AppendLine("\tpublic override void OnShow()");
            sb.AppendLine("\t{");
            sb.AppendLine("\t\tbase.OnShow();");
            sb.AppendLine("\t}");

            // OnHide
            sb.AppendLine("\t// 物体隐藏时执行");
            sb.AppendLine("\tpublic override void OnHide()");
            sb.AppendLine("\t{");
            sb.AppendLine("\t\tbase.OnHide();");
            sb.AppendLine("\t}");

            // OnDestroy
            sb.AppendLine("\t// 物体销毁时执行");
            sb.AppendLine("\tpublic override void OnDestroy()");
            sb.AppendLine("\t{");
            sb.AppendLine("\t\tbase.OnDestroy();");
            sb.AppendLine("\t}");

            sb.AppendLine("\t#endregion");

            // API Function
            sb.AppendLine();
            sb.AppendLine("\t#region API Function");
            sb.AppendLine();
            sb.AppendLine("\t#endregion");

            // UI 组件事件生成
            sb.AppendLine();
            sb.AppendLine("\t#region UI组件事件");
            foreach (var item in objDataList)
            {
                string type = item.fieldType;
                string methodName = "On" + item.fieldName;
                string suffix = "";
                if (type.Contains("Button"))
                {
                    suffix = "ButtonClick";
                    CreateMethod(sb, ref methodDic, methodName + suffix);
                }
                else if (type.Contains("InputField"))
                {
                    suffix = "InputChange";
                    CreateMethod(sb, ref methodDic, methodName + suffix, "string text");
                    suffix = "InputEnd";
                    CreateMethod(sb, ref methodDic, methodName + suffix, "string text");
                }
                else if (type.Contains("Toggle"))
                {
                    suffix = "ToggleChange";
                    CreateMethod(sb, ref methodDic, methodName + suffix, "bool state, Toggle toggle");
                }
            }
            sb.AppendLine("\t#endregion");

            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// 生成UI事件方法
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="methodDic"></param>
        /// <param name="modthName"></param>
        /// <param name="param"></param>
        public static void CreateMethod(StringBuilder sb, ref Dictionary<string, string> methodDic, string methodName, string param = "")
        {
            //声明UI组件事件
            sb.AppendLine($"\tpublic void {methodName}({param})");
            sb.AppendLine("\t{");
            if (methodName == "OnCloseButtonClick")
            {
                sb.AppendLine("\t    HideWindow();");
            }
            sb.AppendLine("\t}");

            //存储UI组件事件 提供给后续新增代码使用
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"\t\t public void {methodName}({param})");
            builder.AppendLine("\t\t {");
            builder.AppendLine("\t\t");
            builder.AppendLine("\t\t }");
            methodDic.Add(methodName, builder.ToString());
        }

    }
#endif
}


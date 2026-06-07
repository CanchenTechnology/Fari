using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
namespace GamerFrameWork.UIFrameWork
{
    public class UIWindowEditor : EditorWindow
    {
        private string scriptContent;//代码内容
        private string filePath; //代码路径
        private Vector2 scroll = new Vector2();
        private string mFileName;
        /// <summary>
        /// 显示代码窗口
        /// </summary>
        /// <param name="content">代码内容</param>
        /// <param name="filePath">代码路径</param>
        /// <param name="insterDic">新增的代码</param>
        public static void ShowWindow(string content, string filePath, Dictionary<string, string> insterDic = null, List<EditorObjectData> fieldList = null)
        {
            //创建代码展示窗口
            UIWindowEditor window = (UIWindowEditor)GetWindowWithRect(typeof(UIWindowEditor), new Rect(100, 50, 800, 700), true, "Window生成界面");
            window.scriptContent = content;
            window.filePath = filePath;
            window.mFileName = Path.GetFileName(filePath);
            //处理新增的代码
            string originScript = string.Empty;
            bool isInsterSuccess = false;
            if (File.Exists(filePath) && (insterDic != null || fieldList != null))
            {
                //获取原始代码
                originScript = File.ReadAllText(filePath);
                if (!string.IsNullOrEmpty(originScript))
                {
                    if (fieldList != null)
                    {
                        //插入字段(生成item脚本时使用)
                        foreach (var item in fieldList)
                        {
                            if (!originScript.Contains($"{item.fieldName}{item.fieldType}"))
                            {
                                string insterArrayType = item.dataList != null ? "[]" : "";
                                string insterArray = item.dataList != null ? "Array" : "";
                                //插入新增的数据
                                originScript = window.scriptContent = originScript.Insert(window.GetInsertFieldIndex(originScript)
                                    , $"public {item.fieldType}{insterArrayType} {item.fieldName}{item.fieldType}{insterArray};\n\t\t");
                                isInsterSuccess = true;

                            }

                        }
                    }
                    if (insterDic != null)
                    {
                        foreach (var item in insterDic)
                        {
                            //如果老代码中没有这个代码就进行插入操作
                            if (!originScript.Contains(item.Key))
                            {
                                int index = window.GetInsertMethodIndex(originScript);
                                originScript = window.scriptContent = originScript.Insert(index, item.Value + "\t\t");
                            }
                        }
                    }
                    if (fieldList != null)
                    {

                        //插入事件(生成item脚本时使用)
                        foreach (var item in fieldList)
                        {
                            string field = $"{item.fieldName}{item.fieldType}";
                            string type = item.fieldType;
                            string methodName = "On" + item.fieldName;
                            string suffix = "";
                            StringBuilder sb = new StringBuilder();
                            if (type.Contains("Button"))
                            {
                                suffix = "ButtonClick";
                                sb.AppendLine($"\t\t\t{field}.onClick.AddListener({methodName}{suffix});");
                            }
                            else if (type.Contains("InputField"))
                            {
                                suffix = "InputChange";
                                sb.AppendLine($"\t\t\t{field}.onValueChanged.AddListener({methodName}{suffix});");
                                suffix = "InputEnd";
                                sb.AppendLine($"\t\t\t{field}.onEndEdit.AddListener({methodName}{suffix});");
                            }
                            else if (type.Contains("Toggle"))
                            {
                                suffix = "ToggleChange";
                                sb.AppendLine($"\t\t\t{field}.onValueChanged.AddListener({methodName}{suffix});");
                            }
                            else
                            {
                                continue;
                            }
                            if (!originScript.Contains($"AddListener({methodName}{suffix})"))
                            {
                                sb.Insert(0, "//按钮事件自动注册绑定\n");
                                originScript = window.scriptContent = originScript.Replace("//按钮事件自动注册绑定", $"{sb.ToString()}");
                                isInsterSuccess = true;
                            }
                        }
                    }

                }
                if (isInsterSuccess == false)
                {
                    window.scriptContent = originScript;
                }

            }
            originScript = null;
            insterDic = null;
            window.Show();
        }
        private void OnGUI()
        {
            //绘制ScrollView
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(600), GUILayout.Width(800));
            EditorGUILayout.TextArea(scriptContent);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("脚本生成路径:");
            //绘制脚本生成路径
            EditorGUILayout.BeginHorizontal();
            filePath = EditorGUILayout.TextField(filePath);
            if (GUILayout.Button("选择路径", GUILayout.Width(80)))
            {
                string folder = EditorUtility.OpenFolderPanel("选择生成路径", filePath, "");
                if (!string.IsNullOrEmpty(folder))
                {
                    filePath = folder;   // 用户选的文件夹路径
                    filePath = Path.Combine(folder, mFileName);
                }
            }
            EditorPrefs.SetString("GeneratorClassPath", filePath);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            //绘制按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("生成脚本", GUILayout.Height(30)))
            {
                //按钮事件
                ButtonClick();
            }
            EditorGUILayout.EndHorizontal();

        }
        public void ButtonClick()
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            StreamWriter writer = File.CreateText(filePath);
            writer.Write(scriptContent);
            writer.Close();
            AssetDatabase.Refresh();
            if (EditorUtility.DisplayDialog("自动化生成工具", "生成脚本成功!", "确定"))
            {
                Close();
            }
        }
        /// <summary>
        /// 获取插入代码的下标
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public int GetInserIndex(string content)
        {
            //找到UI事件组件下面的第一个public所在的位置进行插入
            Regex regex = new Regex("UI组件事件");
            Match match = regex.Match(content);

            Regex regex1 = new Regex("public");
            MatchCollection matchCollection = regex1.Matches(content);

            for (int i = 0; i < matchCollection.Count; i++)
            {
                if (matchCollection[i].Index > match.Index)
                {
                    return matchCollection[i].Index;
                }
            }
            return -1;
        }
        /// <summary>
        /// 获取插入代码的下标
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public int GetInsertMethodIndex(string content)
        {
            //找到UI事件组件下面的第一个public 所在的位置 进行插入
            Regex regex = new Regex("UI组件事件");
            Match match = regex.Match(content);
            return match.Index + 6;
        }
        public int GetInsertFieldIndex(string content)
        {
            //找到UI事件组件下面的第一个public 所在的位置 进行插入
            Regex regex = new Regex("自定义字段");
            Match match = regex.Match(content);
            Regex regex1 = new Regex("public");
            MatchCollection matchColltion = regex1.Matches(content);

            for (int i = 0; i < matchColltion.Count; i++)
            {
                if (matchColltion[i].Index > match.Index)
                {
                    return matchColltion[i].Index;
                }
            }
            return -1;
        }
    }
}


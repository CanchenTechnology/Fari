using System;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
/*
 * 作用:为了方便更好的加载Resources(AssetBundle)中的预制体(不同路径下的预制体)，保存预制体的名称和在Resources(AssetBundle)下的路径
*/


namespace GamerFrameWork.UIFrameWork
{
    [CreateAssetMenu(fileName = "WindowConfig", menuName = "GamerFrameWork/UIFrameWork/WindowConfig", order = 0)]
    public class WindowConfig : ScriptableObject
    {
        public List<WindowData> windowDataList = new List<WindowData>();
#if UNITY_EDITOR
        /// <summary>
        /// 生成窗口预制体加载路径
        /// </summary>
        public void GeneratorWindowConfig()
        {
            string[] windowRootArr = UISetting.Instance.WindowPrefabFolderPathArr;
            //检测预制体路径或名称没有改变，如果没有就不需要生成配置
            bool needUpdate = false;
            foreach (var windowRootName in windowRootArr)
            {
                string[] filePathArr = Directory.GetFiles(Application.dataPath.Replace("Assets", "") + windowRootName, "*.prefab", SearchOption.AllDirectories);
                foreach (var path in filePathArr)
                {
                    if (path.EndsWith(".meta")) continue;
                    WindowData windowData = GetWindowData(Path.GetFileNameWithoutExtension(path), false);

                    string windowPath = windowData == null ? string.Empty : windowData.path;
                    //路径不存在或路径不一致
                    if (string.IsNullOrEmpty(windowPath) || (!string.IsNullOrEmpty(windowPath) && windowPath.GetHashCode() != path.GetHashCode()))
                    {
                        needUpdate = true;
                        break;
                    }
                }
            }
            if (!needUpdate)
            {
                Debug.Log("预制体个数没有发生改变，不生成窗口配置");
                return;
            }

            windowDataList.Clear();
            foreach (var windowRootName in windowRootArr)
            {
                //获取预制体文件夹读取路径
                string folder = Application.dataPath.Replace("Assets", "") + windowRootName;
                //获取文件夹下的所有Prefab文件
                string[] filePathArr = Directory.GetFiles(folder, "*.prefab", SearchOption.AllDirectories);
                foreach (var path in filePathArr)
                {
                    if (path.EndsWith(".meta"))
                    {
                        continue;
                    }
                    //获取预制体名字
                    string fileName = Path.GetFileNameWithoutExtension(path);

                    //计算文件读取路径 
                    string tempPath = windowRootName + "/" + fileName;

                    // 如果包含 Resources，则去掉 Resources 及之前的部分
                    int index = tempPath.IndexOf("Resources", StringComparison.OrdinalIgnoreCase);
                    if (index >= 0)
                    {
                        tempPath = tempPath.Substring(index + "Resources".Length);
                        // 去掉开头的 / 
                        if (tempPath.StartsWith("/"))
                        {
                            tempPath = tempPath.Substring(1);
                        }
                    }

                    WindowData data = new WindowData { name = fileName, path = tempPath };
                    windowDataList.Add(data);
                }
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
#endif
        }
#endif
        /// <summary>
        /// 获取窗口数据
        /// </summary>
        /// <param name="wndName">窗口名称</param>
        /// <param name="log">是否打印窗口不存在日志.</param>
        /// <returns></returns>
        public WindowData GetWindowData(string wndName, bool log = true)
        {
            foreach (var item in windowDataList)
            {
                if (string.Equals(item.name, wndName))
                {
                    return item;
                }
            }
            if (log)
                Debug.LogError(wndName + "不存在在配置文件中，请检查预制体存放位置，或配置文件");
            return null;
        }
    }
    [System.Serializable]
    public class WindowData
    {
        public string name;
        public string path;
    }
}


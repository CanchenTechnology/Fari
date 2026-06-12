/*----------------------------------------------------------------------------
* Title:  一款Mono分离式UI管理框架
* UI配置文件（最终安全版本）
----------------------------------------------------------------------------*/
#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;



#if ODIN_INSPECTOR && UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

using UnityEngine;

namespace GamerFrameWork.UIFrameWork
{
    [CreateAssetMenu(fileName = "UISetting",menuName = "GamerFrameWork/UIFrameWork/UISetting(Ui配置路径)",order = 0)]
    public class UISetting : ScriptableObject
    {
        private const string uiSettingPath = "Assets/GamerFrameWork/UIFrameWork/Config/UISetting.asset";
        private static UISetting _instance;
        public static UISetting Instance
        {
            get
            {
                if (_instance == null)
                    _instance = AssetDatabase.LoadAssetAtPath<UISetting>(uiSettingPath);
                return _instance;
            }
        }

        public static string OBJDATALIST_KEY = "objDataList";

        //——————————————————————————————————————————
        //  所有字段参与序列化，绝不被条件编译裁掉
        //——————————————————————————————————————————


        //——— 自动生成路径配置 ————————————————
#if ODIN_INSPECTOR && UNITY_EDITOR
        [TitleGroup("脚本自动化生成路径", "自定义生成脚本后缀")]
        [LabelText("自定义生成脚本后缀名")]
#endif
        public string GenerateCSharpSuffix = "UIComponent";

#if ODIN_INSPECTOR && UNITY_EDITOR
        [TitleGroup("脚本自动化生成路径配置", "自定义生成路径")]
        [LabelText("组件绑定脚本生成路径")]
        [FolderPath]
#endif
        public string BindComponentGeneratorPath = "";

#if ODIN_INSPECTOR && UNITY_EDITOR
        [TitleGroup("脚本自动化生成路径配置")]
        [LabelText("窗口交互脚本生成路径")]
        [FolderPath]
#endif
        public string WindowGeneratorPath = "";

#if ODIN_INSPECTOR && UNITY_EDITOR
        [TitleGroup("脚本自动化生成路径配置")]
        [LabelText("Item脚本生成路径")]
        [FolderPath]
#endif
        public string ItemScriptsGeneratorPath = "";

        //——— Namespace 配置 ————————————————

#if ODIN_INSPECTOR && UNITY_EDITOR
        [TitleGroup("自动生成脚本命名空间引入配置",
            "生成脚本时框架自动 using 以下命名空间")]
        [LabelText("命名空间配置")]
#endif
        public string[] UsingNameSpaceArr = new string[]
            {
                "GamerFrameWork.UIFrameWork",
                "UnityEngine.UI",
                "UnityEngine",
            };


        //——— Prefab 路径配置 ————————————————

#if ODIN_INSPECTOR && UNITY_EDITOR
        [TitleGroup("Prefabs资源加载路径")]
        [FolderPath]
#endif
        public string[] WindowPrefabFolderPathArr;



        //——— WindowConfig 路径 ————————————————

#if ODIN_INSPECTOR && UNITY_EDITOR
        [TitleGroup("加载WindowConfig配置文件的路径")]
        [FolderPath]
#endif
        public string windowConfigPath;


        //——— 生成 WindowConfig ————————————————

#if ODIN_INSPECTOR && UNITY_EDITOR
        [Button("生成 WindowConfig资源加载路径配置", ButtonSizes.Large)]
#endif
        public void GenerateWindowConfig()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(windowConfigPath))
            {
                string allPath = $"{windowConfigPath}/WindowConfig.asset";
                WindowConfig config = AssetDatabase.LoadAssetAtPath<WindowConfig>(allPath);

                if (config == null)
                {
                    Debug.LogError($"无法从路径加载 WindowConfig: {allPath}");
                    return;
                }

                config.GeneratorWindowConfig();
                Debug.Log("WindowConfig资源加载路径生成完成！");
            }
#endif
        }


        //——— 保存 ————————————————
        public void Save()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
#endif
        }
    }
}
#endif
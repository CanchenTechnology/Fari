using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
namespace GamerFrameWork.UIFrameWork
{
    public class UISettingMenu : OdinMenuEditorWindow
    {
        [SerializeField]
        public UISettingMenu uiSettingWindow;

        [MenuItem("GamerFrameWork/UIFrameWork/UI Setting", false, 2)]
        public static void ShowAssetBundleWindow()
        {
            UISettingMenu window = GetWindow<UISettingMenu>();
            window.position = GUIHelper.GetEditorWindowRect().AlignCenter(985, 612);
            window.ForceMenuTreeRebuild();
        }

        protected override OdinMenuTree BuildMenuTree()
        {

            OdinMenuTree menuTree = new OdinMenuTree(supportsMultiSelect: false)
        {
            { "UIFrameWork Setting",UISetting.Instance,EditorIcons.SettingsCog},
        };
            return menuTree;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            UISetting.Instance.Save();
        }
    }


}
#endif


using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace SuperScrollView
{
    public class QuickGame : EditorWindow
    {
        private static string s_StartSceneName = @"Assets\SuperScrollView\Demo\Scenes\Menu\Menu.unity";

        [MenuItem("GamerFrameWork/QuickGame/SuperScrollView",false,100)]
        public static void Start()
        {
            if (!EditorApplication.isPlaying)
            {
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorUtility.DisplayDialog("!!Warning!!", "Please save current scene to continue.", "OK");
                    return;
                }
            }

            Time.timeScale = 1.0f;
            EditorSceneManager.OpenScene(s_StartSceneName);
            EditorApplication.isPlaying = true;
        }
    }
}


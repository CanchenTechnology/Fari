using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace GamerFrameWork.UIFrameWork
{
    public class SystemUIEditor : Editor
    {
        [InitializeOnLoadMethod]
        private static void InitEditor()
        {
            //监听Hierarchy发生改变的委托
            EditorApplication.hierarchyChanged -= HanderTextOrImageRaycast;
            EditorApplication.hierarchyChanged -= LoadWindowCamera;
            EditorApplication.hierarchyChanged += HanderTextOrImageRaycast;
            EditorApplication.hierarchyChanged += LoadWindowCamera;
        }

        private static void HanderTextOrImageRaycast()
        {
            GameObject obj = Selection.activeGameObject;
            if (obj != null)
            {
                if (obj.name.Contains("Text"))
                {
                    Text text = obj.GetComponent<Text>();
                    if (text != null && text.raycastTarget)
                    {
                        Undo.RecordObject(text, "Disable Text Raycast Target");
                        text.raycastTarget = false;
                        EditorUtility.SetDirty(text);
                    }
                }
                else if (obj.name.Contains("Image"))
                {
                    Image image = obj.GetComponent<Image>();
                    if (image != null && image.raycastTarget)
                    {
                        Undo.RecordObject(image, "Disable Image Raycast Target");
                        image.raycastTarget = false;
                        EditorUtility.SetDirty(image);
                    }
                    else
                    {
                        RawImage rawImage = obj.GetComponent<RawImage>();
                        if (rawImage != null && rawImage.raycastTarget)
                        {
                            Undo.RecordObject(rawImage, "Disable RawImage Raycast Target");
                            rawImage.raycastTarget = false;
                            EditorUtility.SetDirty(rawImage);
                        }
                    }
                }
            }
        }
        private static void LoadWindowCamera()
        {
            if (Selection.activeGameObject != null)
            {
                GameObject uiCameraObj = GameObject.Find("UICamera");
                if (uiCameraObj != null)
                {
                    Camera camera = uiCameraObj.GetComponent<Camera>();
                    if (Selection.activeGameObject.name.Contains("Window"))
                    {
                        Canvas canvas = Selection.activeGameObject.GetComponent<Canvas>();
                        if (canvas != null && canvas.worldCamera != camera)
                        {
                            Undo.RecordObject(canvas, "Assign UI Camera");
                            canvas.worldCamera = camera;
                            EditorUtility.SetDirty(canvas);
                        }
                    }
                }
            }
        }
    }
}

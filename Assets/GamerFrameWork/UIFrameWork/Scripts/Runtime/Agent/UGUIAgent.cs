using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace GamerFrameWork.UIFrameWork
{
    public static class UGUIAgent
    {
        public static void SetVisable(this GameObject obj, bool visible)
        {
            obj.transform.localScale = visible ? Vector3.one : Vector3.zero;
        }
        public static void SetVisable(this Transform trans, bool visible)
        {
            trans.transform.localScale = visible ? Vector3.one : Vector3.zero;
        }
        public static void SetVisable(this Button btn, bool visible)
        {
            btn.transform.localScale = visible ? Vector3.one : Vector3.zero;
        }
        public static void SetVisable(this Image image, bool visible)
        {
            image.transform.localScale = visible ? Vector3.one : Vector3.zero;
        }
        public static void SetVisable(this Text text, bool visible)
        {
            text.transform.localScale = visible ? Vector3.one : Vector3.zero;
        }
        public static void SetVisable(this Slider slider, bool visible)
        {
            slider.transform.localScale = visible ? Vector3.one : Vector3.zero;
        }
        public static void SetVisable(this Toggle toggle, bool visible)
        {
            toggle.transform.localScale = visible ? Vector3.one : Vector3.zero;
        }
        public static void SetVisable(this InputField input, bool visible)
        {
            input.transform.localScale = visible ? Vector3.one : Vector3.zero;
        }
        public static void SetVisable(this RawImage rawImage, bool visible)
        {
            rawImage.transform.localScale = visible ? Vector3.one : Vector3.zero;
        }
    }
}


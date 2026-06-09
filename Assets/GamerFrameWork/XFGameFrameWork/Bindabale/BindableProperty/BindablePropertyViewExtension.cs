using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//**********************************************
//创建人：玖一
//功能说明：
//**********************************************
namespace XFGameFrameWork
{
    public static class BindablePropertyViewExtension
    {
        /// <summary>
        /// 将BindProperty绑定到Text
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="property"></param>
        /// <param name="go"></param>
        public static void BindToText<T>(this BindableProperty<T> property, GameObject go)
        {
            if (go == null) return;
            var uiText = go.GetComponent<Text>();
            if (uiText != null)
            {
                property.RegisterWithInitValue(value =>
                {
                    uiText.text = property.Value.ToString();

                }).UnRegisterWhenGameObjectDestroyed(go);
                return;
            }
            var uiTmp = go.GetComponent<TMP_Text>();
            if (uiTmp != null)
            {
                property.RegisterWithInitValue(value =>
                {
                    uiTmp.text = property.Value.ToString();

                }).UnRegisterWhenGameObjectDestroyed(go);
                return;
            }
            Debug.LogError($"Can Find Text Info,The gameObject is {go.name}");

        }
        /// <summary>
        /// 将BindableProperty绑定到Image的FillAmount
        /// </summary>
        public static void BindToImageFillAmout(this BindableProperty<float> property, Image image)
        {
            if (image == null) return;
            property.RegisterWithInitValue(value =>
            {
                image.fillAmount = value;
            }).UnRegisterWhenGameObjectDestroyed(image.gameObject);
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//**********************************************
//创建人：玖一
//功能说明：
//**********************************************
namespace XFGameFrameWork
{
    public static class UIXF
    {
        public static T ColorAlpha<T>(this T selfGraphic, float alpha) where T : Graphic
        {
            var color = selfGraphic.color;
            color.a = alpha;
            selfGraphic.color = color;
            return selfGraphic;
        }
        public static Image FillAmount(this Image selfImage, float fillAmount)
        {
            selfImage.fillAmount = fillAmount;
            return selfImage;
        }


    }
}



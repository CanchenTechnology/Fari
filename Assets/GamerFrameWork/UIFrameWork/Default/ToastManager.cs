using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace GamerFrameWork.UIFrameWork
{
    /// <summary>
    /// Toast属于层级最高
    /// </summary>
    public class ToastManager : MonoBehaviour
    {
        private static Toast mToast;
        public static void ShowToast(string key,int scale = 1)
        {
            if (mToast == null)
            {
                mToast = UIModule.Instance.LoadWindow("Toast", UIPrefabLoadType.Resources).GetComponent<Toast>();
            }
            mToast.ShowToast(key,scale);
        }
        /// <summary>
        /// 服务端显示提示
        /// </summary>
        /// <param name="code"></param>
        public static void ShowToast(int code,int scale = 1)
        {
            if (mToast == null)
            {
                mToast = UIModule.Instance.LoadWindow("Toast", UIPrefabLoadType.Resources).GetComponent<Toast>();
            }
            mToast.ShowToast(code,scale);
        }
        /// <summary>
        /// 显示正在开发中
        /// </summary>
        public static void ShowDebug()
        {
            ShowToast("功能待开发。。。");
        }
        
    }
}


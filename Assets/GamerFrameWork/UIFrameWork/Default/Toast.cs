using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GamerFrameWork.UIFrameWork
{
    public class Toast : MonoBehaviour
    {
        public CanvasGroup canvasGroup;
        public Transform ContentRootTrans;
        public Canvas canvas;
        public Text ContentTextPro;

        private float mShowTime = 2.0f;

        // ===== 新增：服务端 Code→提示文本 查表 =====
        private static readonly Dictionary<int, string> CodeMsgDict = new Dictionary<int, string>
        {
            { 0, "操作成功" },
            { 1, "未知错误" },
            { 1001, "参数错误" },
            { 1002, "签名不正确" },
            { 1003, "请求过于频繁" },
            { 2001, "用户未登录，请重新登录" },
            { 2002, "用户不存在" },
            { 2003, "权限不足" },
            { 3001, "数据不存在" },
            { 3002, "数据格式错误" },
            { 4001, "服务器异常，请稍后再试" },
            { 5001, "网络不稳定，请检查连接" },
        };

        /// <summary>
        /// 显示字符串提示
        /// </summary>
        public void ShowToast(string key, int scale = 1)
        {
            Debug.Log($"ShowToast: {key}");

            if (canvas.worldCamera == null)
            {
                canvas.worldCamera = UIModule.Instance.Camera;
            }

            ContentTextPro.text = key;

            ContentRootTrans.localScale = new Vector3(0.8f, 0.8f, 0.8f)*scale;
            ContentRootTrans.DOScale(Vector3.one*scale, 0.3f).SetEase(Ease.OutBack);

            canvasGroup.alpha = 1;

            CancelInvoke();
            Invoke(nameof(HideToast), mShowTime);
        }

        /// <summary>
        /// 通过 code 自动显示对应的错误提示
        /// </summary>
        public void ShowToast(int code,int scale = 1)
        {
            Debug.Log($"ShowSocket Toast: {code}");

            string msg;

            if (!CodeMsgDict.TryGetValue(code, out msg))
                msg = $"未知错误（{code}）";

            ShowToast(msg);
        }

        public void HideToast()
        {
            canvasGroup.alpha = 0;
        }
    }
}

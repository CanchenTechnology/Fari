using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamerFrameWork.UIFrameWork
{
    public class Toast : MonoBehaviour
    {
        public CanvasGroup canvasGroup;
        public Transform ContentRootTrans;
        public Canvas canvas;
        public TMP_Text ContentTextPro;

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

            // 限制Toast宽度，防止内容超出屏幕
            ConstrainToastWidth();

            ContentRootTrans.localScale = new Vector3(0.8f, 0.8f, 0.8f)*scale;
            ContentRootTrans.DOScale(Vector3.one*scale, 0.3f).SetEase(Ease.OutBack);

            canvasGroup.alpha = 1;

            CancelInvoke();
            Invoke(nameof(HideToast), mShowTime);
        }

        /// <summary>
        /// 限制Toast最大宽度，防止长文本超出屏幕
        /// </summary>
        private void ConstrainToastWidth()
        {
            // 参考分辨率 750，留给Toast的最大文字宽度（减去左右padding 20+20 后约480）
            float maxTextWidth = 480f;

            // 让文本在超出宽度时自动换行
            ContentTextPro.enableWordWrapping = true;
            ContentTextPro.overflowMode = TextOverflowModes.Overflow;

            // 通过 LayoutElement 给文本一个固定首选宽度，这样 HorizontalLayoutGroup +
            // ContentSizeFitter 才能正确计算出 ContentRoot 应有的宽高
            var layoutElement = ContentTextPro.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = ContentTextPro.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = maxTextWidth;

            // 确保 ContentSizeFitter 垂直也自适应，让背景在文本多行时能拉高
            var rootFitter = ContentRootTrans.GetComponent<ContentSizeFitter>();
            if (rootFitter != null)
                rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
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

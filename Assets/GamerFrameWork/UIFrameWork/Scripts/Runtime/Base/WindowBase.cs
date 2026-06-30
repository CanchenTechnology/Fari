using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
namespace GamerFrameWork.UIFrameWork
{
    /// <summary>
    /// WindowBase:UI窗口的基类,负责部分共用功能的统一化处理，例如弹出、关闭动画、以及解耦合方法的声明等其他公用接口的处理
    /// </summary>
    public class WindowBase : WindowBehaviour
    {
        public static bool EnableWindowAnimation = false;

        private CanvasGroup mUIMaskCanvasGroup;
        private CanvasGroup mCanvasGroup;
        protected Transform mUIContent;
        protected bool mDisableAnim = false;//禁用动画

        private List<Button> mAllButtonList = new List<Button>();//所有Button列表
        private List<Toggle> mToggleList = new List<Toggle>();//所有的Toggle列表
        private List<InputField> mInputList = new List<InputField>();//所有的输入框列表
        private List<TMP_InputField> mTmpInputList = new List<TMP_InputField>();//所有TMP输入框列表
        private void InitializeBaseComponent()
        {
            mCanvasGroup = transform.GetComponent<CanvasGroup>();
            mUIMaskCanvasGroup = transform.Find("UIMask").GetComponent<CanvasGroup>();
            mUIContent = transform.Find("UIContent").transform;
        }
        #region 声明周期
        public override void OnAwake()
        {
            base.OnAwake();
            InitializeBaseComponent();
        }
        public override void OnShow()
        {
            base.OnShow();
            ShowAnimation();
        }
        public override void OnUpdate()
        {
            base.OnUpdate();
        }
        public override void OnHide()
        {
            base.OnHide();
        }
        public override void OnDestroy()
        {
            base.OnDestroy();
            RemoveAllButtonListener();
            RemoveAllToggleListener();
            RemoveAllInputListener();
            mAllButtonList.Clear();
            mToggleList.Clear();
            mInputList.Clear();
            mTmpInputList.Clear();
        }
        #endregion
        #region 动画管理
        public void ShowAnimation()
        {
            KillWindowTweens();

            //基础弹窗不需要动画
            if (ShouldPlayWindowAnimation())
            {
                //Mask动画
                mUIMaskCanvasGroup.alpha = 0;
                mUIMaskCanvasGroup.DOFade(1, 0.2f);
                //缩放动画
                mUIContent.localScale = Vector3.one * 0.8f;
                mUIContent.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            }
            else if (ShouldUseWindowAnimationSlot())
            {
                ResetWindowAnimationState();
            }

        }
        public void HideAnimation()
        {
            KillWindowTweens();

            if (ShouldPlayWindowAnimation())
            {
                mUIContent.DOScale(Vector3.one * 1.1f, 0.2f).SetEase(Ease.OutBack).OnComplete(() =>
                {
                    UIModule.Instance.HideWindow(Name);
                });
            }
            else
            {
                if (ShouldUseWindowAnimationSlot())
                {
                    ResetWindowAnimationState();
                }

                UIModule.Instance.HideWindow(Name);
            }
        }

        private bool ShouldPlayWindowAnimation()
        {
            return EnableWindowAnimation && ShouldUseWindowAnimationSlot();
        }

        private bool ShouldUseWindowAnimationSlot()
        {
            return Canvas != null && Canvas.sortingOrder > 90 && !mDisableAnim;
        }

        private void ResetWindowAnimationState()
        {
            if (mUIMaskCanvasGroup != null)
            {
                mUIMaskCanvasGroup.alpha = 1f;
            }

            if (mUIContent != null)
            {
                mUIContent.localScale = Vector3.one;
            }
        }

        private void KillWindowTweens()
        {
            if (mUIContent != null)
            {
                DOTween.Kill(mUIContent);
            }

            if (mUIMaskCanvasGroup != null)
            {
                DOTween.Kill(mUIMaskCanvasGroup);
            }
        }
        #endregion
        public void HideWindow()
        {
            HideAnimation();
            //UIModule.Instance.HideWindow(Name);
        }
        public void DestroyWindow()
        {
            UIModule.Instance.DestroyWindow(Name);
        }
        public override void SetVisible(bool isVisible)
        {
            if (mCanvasGroup == null)
            {
                Debug.LogError("CanvasGroup is Null!" + Name);
                return;
            }
            Visible = mCanvasGroup.interactable = mCanvasGroup.blocksRaycasts = isVisible;
            mCanvasGroup.alpha = isVisible ? 1 : 0;

            if (!isVisible && transform != null)
            {
                transform.SetAsFirstSibling();
            }
            if (isVisible && PopStack)//特殊情况,需要重绘,以免未刷新
            {
                gameObject.SetActive(false);
                gameObject.SetActive(true);
            }

        }
        public void SetMaskVisible(bool isVisible)
        {
            mUIMaskCanvasGroup.alpha = isVisible ? 1 : 0;
            mUIMaskCanvasGroup.blocksRaycasts = isVisible;
            //特殊情况下进行窗口同层级重绘渲染
            if (isVisible && PopStack)
            {
                mUIMaskCanvasGroup.gameObject.SetActive(false);
                mUIMaskCanvasGroup.gameObject.SetActive(true);
            }
        }
        #region 事件管理
        public void AddButtonClickListener(Button btn, UnityAction unityAction)
        {
            if (btn != null)
            {
                if (!mAllButtonList.Contains(btn))
                {
                    mAllButtonList.Add(btn);
                }
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(unityAction);
            }
        }
        public void AddToggleClickListener(Toggle toggle, UnityAction<bool, Toggle> action)
        {
            if (toggle != null)
            {
                if (!mToggleList.Contains(toggle))
                {
                    mToggleList.Add(toggle);
                }
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener((isOn) =>
                {
                    action?.Invoke(isOn, toggle);
                });
            }
        }
        public void AddInputFieldListener(InputField input, UnityAction<string> onChangeAction, UnityAction<string> endAction)
        {
            if (input != null)
            {
                if (!mInputList.Contains(input))
                {
                    mInputList.Add(input);
                }
                input.onValueChanged.RemoveAllListeners();
                input.onEndEdit.RemoveAllListeners();
                input.onValueChanged.AddListener(onChangeAction);
                input.onEndEdit.AddListener(endAction);
            }

        }
        public void AddInputFieldListener(TMP_InputField input, UnityAction<string> onChangeAction, UnityAction<string> endAction)
        {
            if (input != null)
            {
                if (!mTmpInputList.Contains(input))
                {
                    mTmpInputList.Add(input);
                }
                input.onValueChanged.RemoveAllListeners();
                input.onEndEdit.RemoveAllListeners();
                input.onValueChanged.AddListener(onChangeAction);
                input.onEndEdit.AddListener(endAction);
            }
        }
        public void RemoveAllButtonListener()
        {
            foreach (var item in mAllButtonList)
            {
                item.onClick.RemoveAllListeners();
            }
        }
        public void RemoveAllToggleListener()
        {
            foreach (var item in mToggleList)
            {
                item.onValueChanged.RemoveAllListeners();
            }
        }
        public void RemoveAllInputListener()
        {
            foreach (var item in mInputList)
            {
                item.onValueChanged.RemoveAllListeners();
                item.onEndEdit.RemoveAllListeners();
            }
            foreach (var item in mTmpInputList)
            {
                item.onValueChanged.RemoveAllListeners();
                item.onEndEdit.RemoveAllListeners();
            }
        }
        #endregion

    }

}

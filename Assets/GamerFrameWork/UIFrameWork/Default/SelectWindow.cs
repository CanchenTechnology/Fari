/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2025/11/26 10:23:17
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using System;
namespace GamerFrameWork.UIFrameWork
{
    public enum SelectType
    {
        Normal,
        Only_OK,
    }
    public class SelectWindow : WindowBase
    {
        public SelectWindowUIComponent uiComponent;

        private Action mOnSureCallBack;
        private Action mOnCancelCallBack;

        private SelectType mSelectType;
        #region 生命周期函数
        // 调用机制与 Mono Awake 一致
        public override void OnAwake()
        {
            uiComponent = gameObject.GetComponent<SelectWindowUIComponent>();
            uiComponent.InitComponent(this);
            this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
            base.OnAwake();
        }
        // 物体显示时执行
        public override void OnShow()
        {
            base.OnShow();

            mOnCancelCallBack = null;
            mOnSureCallBack = null;
        }
        // 物体隐藏时执行
        public override void OnHide()
        {
            base.OnHide();
        }
        // 物体销毁时执行
        public override void OnDestroy()
        {
            base.OnDestroy();
        }
        #endregion

        #region API Function
        public void InitViewState(SelectType type,string content,Action sureCallBack = null,Action cancelCallBack = null,string sureText = "确认",string cancelText ="取消",string title = "提示",TextAnchor aligment = TextAnchor.MiddleCenter)
        {
            mSelectType = type;
            mOnSureCallBack = sureCallBack;
            mOnCancelCallBack = cancelCallBack; 

            uiComponent.ContentText.text = content;
            uiComponent.ContentText.alignment = aligment;
            uiComponent.CancelText.text = cancelText;
            uiComponent.OKText.text = sureText;
            uiComponent.OKButton.gameObject.SetActive(type==SelectType.Normal||type==SelectType.Only_OK);
            uiComponent.CancelButton.gameObject.SetActive(type==SelectType.Normal);
        }
        #endregion

        #region UI组件事件
        public void OnCancelButtonClick()
        {
            HideWindow();
            mOnCancelCallBack?.Invoke();

        }
        public void OnOKButtonClick()
        {
            HideWindow();
            mOnSureCallBack?.Invoke();
        }
        #endregion
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using GamerFrameWork.UIFrameWork;
using SuperScrollView;
using System.Diagnostics.Tracing;
using GamerFrameWork;
using System;
using TMPro;

public class SwitchRoleItem : MonoBehaviour, IGamerUIViewListItem, IGamerUIListViewItemContext
{
    [Header("基础信息")]
    public Image roleImage;
    public TMP_Text roleNameText;
    public TMP_Text roleTypeText;
    public TMP_Text roleInfoText;
    public Button detailBtn;
    public Button switchBtn;
    public GameObject usingTagGo;

    public ExtraInfo extraInfo;

    [Header("展开区域")]
    public RectTransform contentRoot;
    public CanvasGroup contentCanvasGroup;

    [Header("动画参数")]
    public float minHeight = 307f;
    public float mContentMaxHeight;
    public float expandDuration = 0.3f;

    private bool mIsExpand;
    private float mMaxHeight;


    private RectTransform mRectTransform;
    private Tween mTween;
    private float mAnimProgress;

    // === LoopListView2 上下文 ===
    private LoopListView2 mLoopListView;
    private int mItemIndex = -1;

    private int roleIndex;
    private RoleDataSO roleData;

    #region IGamerUIViewListItem 实现

    public void InitListItem()
    {
        mRectTransform = GetComponent<RectTransform>();

        if (detailBtn != null)
        {
            detailBtn.onClick.AddListener(OnDetailBtnClick);
        }

        if (switchBtn != null)
        {
            switchBtn.onClick.AddListener(OnSwitchBtnClick);
        }
        EventSystem.AddEvent(GameDataStr.UpdateRoleInfo, UpdateRoleInfo);
    }

    private void UpdateRoleInfo(object data)
    {
        int index = (int)data;
        Debug.Log($"{index},{roleIndex}");
        if (index != roleIndex)
        {
            usingTagGo.SetActive(false);
        }
        else
        {
            usingTagGo.SetActive(true);
        }
    }


    public void SetItemListData(int index, params object[] data)
    {
        mItemIndex = index;

        if (data != null && data.Length > 0 && data[0] is RoleDataSO roleData)
        {
            roleIndex = roleData.roleId;
            BindRoleData(roleData);
            var extraData = roleData.extraData;
            extraInfo.SetData(extraData.roleName,
            extraData.roleDes, extraData.roleContent, extraData.tags[0], extraData.tags[1],
            extraData.tags[2]);
        }

        // 必须先刷新高度（基于文本内容计算），再设置展开状态
        RefreshHeight();
        SetExpand(false, true);

    }

    public void OnRelease()
    {
        mTween?.Kill();
        mTween = null;
        EventSystem.RemoveEvent(GameDataStr.UpdateRoleInfo, UpdateRoleInfo);
    }

    #endregion

    #region IGamerUIListViewItemContext 实现

    public void SetListViewContext(LoopListView2 listView, int index)
    {
        mLoopListView = listView;
        mItemIndex = index;
    }

    #endregion

    #region 数据绑定

    private void BindRoleData(RoleDataSO roleData)
    {
        if (roleData == null) return;
        this.roleData = roleData;
        // 基础信息
        if (roleNameText != null)
            roleNameText.text = roleData.roleName;
        if (roleTypeText != null)
            roleTypeText.text = roleData.roleType;
        if (roleInfoText != null)
            roleInfoText.text = roleData.roleDescription;

        // 头像
        if (roleImage != null && roleData.roleImage != null)
        {
            roleImage.sprite = roleData.roleImage;
        }

        // 是否正在使用中
        bool isCurrent = RoleManager.Instance != null
            && RoleManager.Instance.GetCurrentRoleData() == roleData;
        if (usingTagGo != null)
        {
            usingTagGo.SetActive(isCurrent);
        }
    }

    #endregion

    #region 按钮事件

    private void OnDetailBtnClick()
    {
        SetExpand(!mIsExpand);
    }

    private void OnSwitchBtnClick()
    {
        if (mItemIndex >= 0)
        {
            RoleManager.Instance.ChangeRole(mItemIndex);
            ToastManager.ShowToast($"已切换为:{roleData.roleName}");
        }
    }

    #endregion

    #region 展开/收起动画

    /// <summary>
    /// 刷新内容高度（基于 roleInfoText 实际文字高度）
    /// 类似 SuperScrollView 中 ContentSizeFitter + rect.size 的做法
    /// </summary>
    private void RefreshHeight()
    {
        if (roleInfoText == null) return;

        // 强制刷新 Layout，获取真实文本高度
        LayoutRebuilder.ForceRebuildLayoutImmediate(roleInfoText.rectTransform);

        float descHeight = 232;
        mContentMaxHeight = descHeight;
        mMaxHeight = minHeight + descHeight;
    }

    /// <summary>
    /// 展开/收起
    /// </summary>
    /// <param name="expand">true=展开, false=收起</param>
    /// <param name="instant">true=立即切换（无动画）</param>
    public void SetExpand(bool expand, bool instant = false)
    {
        mIsExpand = expand;
        mTween?.Kill();

        float targetHeight = expand ? mMaxHeight : minHeight; //当前对象的宽度
        float targetContentHeight = expand ? mContentMaxHeight : 0f;
        float targetAlpha = expand ? 1f : 0f;

        // 记录动画起始值（当前实际值）
        float startHeight = mRectTransform != null ? mRectTransform.rect.height : minHeight;
        float startContentHeight = contentRoot != null ? contentRoot.rect.height : 0f;
        float startAlpha = contentCanvasGroup != null ? contentCanvasGroup.alpha : 0f;

        if (instant)
        {
            ApplyHeightAndAlpha(targetHeight, targetContentHeight, targetAlpha);
            NotifySizeChanged();
            return;
        }

        // DOTween 动画：驱动 mAnimProgress 从 0 → 1
        mAnimProgress = 0f;
        mTween = DOTween.To(
            () => mAnimProgress,
            x =>
            {
                mAnimProgress = x;
                float curHeight = Mathf.Lerp(startHeight, targetHeight, mAnimProgress);
                float curContentHeight = Mathf.Lerp(startContentHeight, targetContentHeight, mAnimProgress);
                float curAlpha = Mathf.Lerp(startAlpha, targetAlpha, mAnimProgress);

                ApplyHeightAndAlpha(curHeight, curContentHeight, curAlpha);

                // ★ 关键：每帧通知 LoopListView2 当前 Item 高度变化
                //    这样其他 Item 才会被正确重新排版（与 SuperScrollView Demo 中 Update() 的做法一致）
                NotifySizeChanged();
            },
            1f,
            expandDuration
        ).SetEase(Ease.OutQuad);
    }

    /// <summary>
    /// 通知 LoopListView2 当前 Item 尺寸已变更，触发列表重新排版
    /// </summary>
    private void NotifySizeChanged()
    {
        if (mLoopListView != null && mItemIndex >= 0)
        {
            mLoopListView.OnItemSizeChanged(mItemIndex);
        }
    }

    private void ApplyHeightAndAlpha(float height, float contentHeight, float alpha)
    {
        if (mRectTransform != null)
            SetHeight(mRectTransform, height);
        if (contentRoot != null)
            SetHeight(contentRoot, contentHeight);
        if (contentCanvasGroup != null)
            contentCanvasGroup.alpha = alpha;
    }

    private void SetHeight(RectTransform rect, float height)
    {
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    #endregion

    private void OnDestroy()
    {
        mTween?.Kill();
        mTween = null;
    }
}

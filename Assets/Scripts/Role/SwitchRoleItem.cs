using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using GamerFrameWork.UIFrameWork;

public class SwitchRoleItem : MonoBehaviour, IGamerUIViewListItem
{
    [Header("基础信息")]
    public Image roleImage;

    public Text roleNameText;
    public Text roleTypeText;
    public Text roleInfoText;

    public Button detailBtn;
    public Button switchBtn;

    public GameObject usingTagGo;

    [Header("展开区域")]
    public RectTransform contentRoot;
    public CanvasGroup contentCanvasGroup;

    [Header("动画参数")]
    public float minHeight = 307f;
    public float itemGap = 30f;
    public float expandDuration = 0.3f;

    private bool mIsExpand;

    private float mMaxHeight;
    private float mContentMaxHeight;

    private RectTransform mRectTransform;
    private Tween mTween;

    public void InitListItem()
    {
        mRectTransform = GetComponent<RectTransform>();

        if (detailBtn != null)
        {
            detailBtn.onClick.AddListener(OnDetailBtnClick);
        }
    }

    public void SetItemListData(int index, params object[] data)
    {
        // 示例
        /*
        RoleData roleData = data[0] as RoleData;

        roleNameText.text = roleData.RoleName;
        roleTypeText.text = roleData.RoleType;
        roleInfoText.text = roleData.RoleDesc;
        */
        RefreshHeight();

        // 默认收起
        SetExpand(false, true);
    }

    public void OnRelease()
    {
        mTween?.Kill();
    }

    private void OnDetailBtnClick()
    {
        SetExpand(!mIsExpand);
    }

    /// <summary>
    /// 刷新内容高度
    /// </summary>
    private void RefreshHeight()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(roleInfoText.rectTransform);

        float descHeight = roleInfoText.preferredHeight;

        mContentMaxHeight = descHeight + itemGap;
        mMaxHeight = minHeight + descHeight + itemGap;
    }

    /// <summary>
    /// 展开收起
    /// </summary>
    public void SetExpand(bool expand, bool instant = false)
    {
        mIsExpand = expand;

        float targetHeight = expand ? mMaxHeight : minHeight;
        float targetAlpha = expand ? 1f : 0f;
        float targetContentHeight = expand ? mContentMaxHeight : 0f;

        mTween?.Kill();

        if (instant)
        {
            SetHeight(mRectTransform, targetHeight);
            SetHeight(contentRoot, targetContentHeight);

            if (contentCanvasGroup != null)
            {
                contentCanvasGroup.alpha = targetAlpha;
            }

            return;
        }

        Sequence seq = DOTween.Sequence();

        seq.Join(
            DOTween.To(
                () => mRectTransform.rect.height,
                x => SetHeight(mRectTransform, x),
                targetHeight,
                expandDuration
            )
        );

        seq.Join(
            DOTween.To(
                () => contentRoot.rect.height,
                x => SetHeight(contentRoot, x),
                targetContentHeight,
                expandDuration
            )
        );

        if (contentCanvasGroup != null)
        {
            seq.Join(
                contentCanvasGroup
                    .DOFade(targetAlpha, expandDuration)
            );
        }

        mTween = seq;
    }

    private void SetHeight(RectTransform rect, float height)
    {
        rect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            height);
    }
}
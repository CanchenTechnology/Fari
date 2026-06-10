using System;
using DG.Tweening;
using GamerFrameWork.UIFrameWork;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 好友分组头部 Item（已有好友 / 创建的好友）
/// 支持展开/折叠动画
/// </summary>
public class FriendsGroupItem : MonoBehaviour, IGamerUIViewListItem
{
    [Header("UI组件")]
    public Text groupNameText;
    public Text countText;
    public Transform arrowTransform;
    public Button headerButton;

    [Header("动画参数")]
    public float arrowRotateDuration = 0.25f;

    private int mGroupIndex = -1;
    private bool mIsExpanded = true;
    private Action<int> mClickCallback;
    private Tween mArrowTween;

    #region IGamerUIViewListItem 实现

    public void InitListItem()
    {
        if (headerButton != null)
        {
            headerButton.onClick.AddListener(OnHeaderClick);
        }
    }

    public void SetItemListData(int index, params object[] data)
    {
        // data[0] = groupIndex (int)
        // data[1] = groupName (string)
        // data[2] = childCount (int)
        // data[3] = isExpanded (bool)
        if (data.Length >= 4)
        {
            mGroupIndex = (int)data[0];

            if (groupNameText != null)
                groupNameText.text = (string)data[1];

            if (countText != null)
                countText.text = ((int)data[2]).ToString();

            SetExpand((bool)data[3], true);
        }
    }

    public void OnRelease()
    {
        mArrowTween?.Kill();
        mArrowTween = null;
    }

    #endregion

    public void SetClickCallback(Action<int> callback)
    {
        mClickCallback = callback;
    }

    /// <summary>
    /// 设置展开/折叠状态
    /// </summary>
    /// <param name="expand">是否展开</param>
    /// <param name="instant">是否立即切换（无动画）</param>
    public void SetExpand(bool expand, bool instant = false)
    {
        if (mIsExpanded == expand)
            return;

        mIsExpanded = expand;

        if (arrowTransform == null)
            return;

        float targetZ = expand ? -90f : 90f;

        if (instant)
        {
            arrowTransform.localEulerAngles = new Vector3(0, 0, targetZ);
            return;
        }

        mArrowTween?.Kill();
        mArrowTween = arrowTransform.DOLocalRotate(new Vector3(0, 0, targetZ), arrowRotateDuration)
            .SetEase(Ease.OutQuad);
    }

    private void OnHeaderClick()
    {
        mClickCallback?.Invoke(mGroupIndex);
    }

    private void OnDestroy()
    {
        mArrowTween?.Kill();
        mArrowTween = null;
    }
}

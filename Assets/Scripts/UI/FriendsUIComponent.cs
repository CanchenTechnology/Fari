using GamerFrameWork.UIFrameWork;
using SuperScrollView;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FriendsUI 组件绑定脚本
/// 负责绑定所有 UI 组件引用和事件注册
/// </summary>
public class FriendsUIComponent : MonoBehaviour
{
    [Header("窗口层级")]
    public WindowLayer windowLayer = WindowLayer.MainUI;

    [Header("背景")]
    public Image chatBGImage;

    [Header("标题区域")]
    public Text titleText;
    public Button notificationButton;
    public Text notificationBadgeText;

    [Header("占卜邀请通知区域")]
    public GameObject inviteNotificationGo;
    public Button viewInviteButton;

    [Header("Friends关系上下文区域")]
    public GameObject friendsContextGo;
    public Button contextAddButton;

    [Header("好友树形列表")]
    public LoopListView2 loopListView;

    public void InitComponent(WindowBase target)
    {
        target.Canvas.sortingOrder = (int)windowLayer;
        target.Layer = windowLayer;

        FriendsUI mWindow = (FriendsUI)target;

        // 绑定按钮点击事件
        target.AddButtonClickListener(notificationButton, mWindow.OnNotificationButtonClick);
        target.AddButtonClickListener(viewInviteButton, mWindow.OnViewInviteButtonClick);
        target.AddButtonClickListener(contextAddButton, mWindow.OnContextAddButtonClick);
    }
}

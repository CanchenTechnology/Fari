/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/19/2026 12:34:10 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class CreateFriendSuccessUI : WindowBase
{
	public CreateFriendSuccessUIComponent uiComponent;

	private static FriendDataManager.FriendData sPendingFriend;
	private FriendDataManager.FriendData currentFriend;

	public static void Show(FriendDataManager.FriendData friend)
	{
		sPendingFriend = friend;
		UIModule.Instance.PopUpWindow<CreateFriendSuccessUI>();
	}

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<CreateFriendSuccessUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("CreateFriendSuccessUI 缺少 UI 组件绑定脚本：CreateFriendSuccessUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
		NotificationUnreadBadge.Attach(uiComponent.NotificationButton);
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		currentFriend = sPendingFriend;
		RefreshFriendView();
		NotificationUnreadBadge.Attach(uiComponent.NotificationButton);
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		currentFriend = null;
		sPendingFriend = null;
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region API Function
	private void RefreshFriendView()
	{
		if (currentFriend == null)
		{
			SetText(uiComponent.FriendNameText, "好友");
			return;
		}

		SetText(uiComponent.FriendNameText, string.IsNullOrWhiteSpace(currentFriend.name) ? "好友" : currentFriend.name.Trim());

		if (uiComponent.FriendAvatarImage != null && currentFriend.headSprite != null)
		{
			uiComponent.FriendAvatarImage.sprite = currentFriend.headSprite;
			uiComponent.FriendAvatarImage.preserveAspect = true;
		}
	}

	private void SetText(Text text, string value)
	{
		if (text != null)
		{
			text.text = value;
		}
	}

	private void OpenFriendList()
	{
		HideWindow();
		NavigationUI navigation = UIModule.Instance.PopUpWindow<NavigationUI>();
		if (navigation != null)
		{
			navigation.OpenFriendUI();
			return;
		}

		UIModule.Instance.HideWindow<NoFriendUI>();
		UIModule.Instance.PopUpWindow<FriendUI>();
	}

	private void OpenConversation()
	{
		FriendDataManager.FriendData friend = currentFriend;
		HideWindow();

		NavigationUI navigation = UIModule.Instance.PopUpWindow<NavigationUI>();
		if (navigation != null)
		{
			navigation.OpenDialogUI();
		}

		DialogUI dialog = UIModule.Instance.PopUpWindow<DialogUI>();
		if (dialog != null)
		{
			dialog.SendAtFriendsMessage(friend);
		}
	}

	#endregion

	#region UI组件事件
	public void OnNotificationButtonClick()
	{
		UIModule.Instance.PopUpWindow<NotionUI>();
	}
	public void OnBackButtonClick()
	{
		OpenFriendList();
	}
	public void OnEnterConversationButtonClick()
	{
		OpenConversation();
	}
	public void OnBackToFriendsButtonClick()
	{
		OpenFriendList();
	}
	#endregion
}

/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/19/2026 1:01:01 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using TMPro;

public class JumpToDialogUI : WindowBase
{
	public JumpToDialogUIComponent uiComponent;

	private static FriendDataManager.FriendData sPendingFriend;

	private FriendDataManager.FriendData currentFriend;
	private string currentQuestion = string.Empty;
	private TMP_Text friendInfoText;
	private TMP_Text autoHintText;

	public static void Show(FriendDataManager.FriendData friend)
	{
		sPendingFriend = friend;
		UIModule.Instance.PopUpWindow<JumpToDialogUI>();
	}

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<JumpToDialogUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("JumpToDialogUI 缺少 UI 组件绑定脚本：JumpToDialogUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		CacheOptionalTextRefs();
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		currentFriend = sPendingFriend;
		currentQuestion = string.Empty;
		if (uiComponent.QuestionInputInputField != null)
		{
			uiComponent.QuestionInputInputField.text = string.Empty;
		}
		RefreshFriendView();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		currentFriend = null;
		currentQuestion = string.Empty;
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
	private void CacheOptionalTextRefs()
	{
		TMP_Text[] texts = gameObject.GetComponentsInChildren<TMP_Text>(true);
		foreach (TMP_Text text in texts)
		{
			if (text == null) continue;

			if (friendInfoText == null && text.text == "双鱼座 · 月亮")
			{
				friendInfoText = text;
			}

			if (autoHintText == null && text.text.Contains("塔罗师将自动结合"))
			{
				autoHintText = text;
			}
		}
	}

	private void RefreshFriendView()
	{
		string friendName = GetFriendName();
		SetText(uiComponent.FriendNameText, friendName);
		SetText(uiComponent.SelectedFriendNameText, $"@{friendName}");
		SetText(friendInfoText, BuildFriendSubtitle());
		SetText(autoHintText, $"✦  塔罗师将自动结合 @{friendName} 的相关信息与你们的连接能量进行解读");

		ApplyAvatar(FriendAvatarImageUtility.ResolveFriendAvatar(currentFriend, uiComponent.FriendAvatarImage));
		LoadRemoteAvatarIfNeeded();

		SetActionButtonsInteractable(currentFriend != null);
	}

	private void ApplyAvatar(Sprite avatar)
	{
		FriendAvatarImageUtility.ApplyAvatar(uiComponent?.FriendAvatarImage, avatar);
	}

	private void LoadRemoteAvatarIfNeeded()
	{
		if (currentFriend == null
			|| currentFriend.headSprite != null
			|| string.IsNullOrWhiteSpace(currentFriend.photoUrl)
			|| uiComponent == null)
			return;

		FriendDataManager.FriendData friend = currentFriend;
		uiComponent.StartCoroutine(FriendAvatarImageUtility.LoadUserSpriteFromUrlCoroutine(currentFriend.name, currentFriend.photoUrl, sprite =>
		{
			if (currentFriend != friend || sprite == null) return;
			currentFriend.headSprite = sprite;
			ApplyAvatar(sprite);
		}));
	}

	private string GetFriendName()
	{
		if (currentFriend == null || string.IsNullOrWhiteSpace(currentFriend.name))
		{
			return "好友";
		}

		return currentFriend.name.Trim();
	}

	private string BuildFriendSubtitle()
	{
		if (currentFriend == null) return "未选择好友";
		if (!string.IsNullOrWhiteSpace(currentFriend.info)) return currentFriend.info.Trim();

		System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
		if (!string.IsNullOrWhiteSpace(currentFriend.relationship)) parts.Add(currentFriend.relationship.Trim());
		if (!string.IsNullOrWhiteSpace(currentFriend.birthday)) parts.Add(currentFriend.birthday.Trim());
		if (!string.IsNullOrWhiteSpace(currentFriend.city)) parts.Add(currentFriend.city.Trim());
		return parts.Count > 0 ? string.Join(" · ", parts) : (currentFriend.isVirtual ? "创建的好友档案" : "好友档案");
	}

	private void SetText(TMP_Text text, string value)
	{
		if (text != null)
		{
			text.text = value;
		}
	}

	private void SetActionButtonsInteractable(bool interactable)
	{
		if (uiComponent.SendQuestionButton != null) uiComponent.SendQuestionButton.interactable = interactable;
		if (uiComponent.EnterDialogButton != null) uiComponent.EnterDialogButton.interactable = interactable;
		if (uiComponent.PromptRecentStatusButton != null) uiComponent.PromptRecentStatusButton.interactable = interactable;
		if (uiComponent.PromptRelationshipButton != null) uiComponent.PromptRelationshipButton.interactable = interactable;
		if (uiComponent.PromptContactButton != null) uiComponent.PromptContactButton.interactable = interactable;
		if (uiComponent.PromptViewMeButton != null) uiComponent.PromptViewMeButton.interactable = interactable;
	}

	private void SetQuestion(string question)
	{
		currentQuestion = question ?? string.Empty;
		if (uiComponent.QuestionInputInputField != null)
		{
			uiComponent.QuestionInputInputField.text = currentQuestion;
			uiComponent.QuestionInputInputField.Select();
			uiComponent.QuestionInputInputField.ActivateInputField();
		}
	}

	private void ReturnToFriendList()
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

	private void OpenDialogWithFriend(string question)
	{
		if (currentFriend == null)
		{
			ToastManager.ShowToast("请先选择好友");
			ReturnToFriendList();
			return;
		}

		FriendDataManager.FriendData friend = currentFriend;
		string message = string.IsNullOrWhiteSpace(question) ? string.Empty : question.Trim();
		HideWindow();

		NavigationUI navigation = UIModule.Instance.PopUpWindow<NavigationUI>();
		if (navigation != null)
		{
			navigation.OpenDialogUI();
		}

		DialogUI dialog = UIModule.Instance.PopUpWindow<DialogUI>();
		if (dialog == null)
		{
			return;
		}

		dialog.SendAtFriendsMessage(friend);
		if (!string.IsNullOrWhiteSpace(message))
		{
			dialog.SendMessageFromExternal(message);
		}
	}

	#endregion

	#region UI组件事件
	public void OnRemoveSelectedFriendButtonClick()
	{
		ToastManager.ShowToast("已取消选择好友");
		ReturnToFriendList();
	}
	public void OnQuestionInputInputChange(string text)
	{
		currentQuestion = text ?? string.Empty;
	}
	public void OnQuestionInputInputEnd(string text)
	{
		currentQuestion = text ?? string.Empty;
	}
	public void OnSendQuestionButtonClick()
	{
		if (string.IsNullOrWhiteSpace(currentQuestion))
		{
			ToastManager.ShowToast("请输入问题或选择一个灵感问题");
			if (uiComponent.QuestionInputInputField != null)
			{
				uiComponent.QuestionInputInputField.Select();
				uiComponent.QuestionInputInputField.ActivateInputField();
			}
			return;
		}

		OpenDialogWithFriend(currentQuestion);
	}
	public void OnEnterDialogButtonClick()
	{
		OpenDialogWithFriend(currentQuestion);
	}
	public void OnPromptRecentStatusButtonClick()
	{
		SetQuestion($"帮我看看 {GetFriendName()} 最近的状态");
	}
	public void OnPromptRelationshipButtonClick()
	{
		SetQuestion($"我和 {GetFriendName()} 的关系会如何发展？");
	}
	public void OnPromptContactButtonClick()
	{
		SetQuestion($"我适合主动联系 {GetFriendName()} 吗？");
	}
	public void OnPromptViewMeButtonClick()
	{
		SetQuestion($"{GetFriendName()} 怎么看我？");
	}
	#endregion
}

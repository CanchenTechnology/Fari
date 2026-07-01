/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/30/2026 1:26:55 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using System.Collections.Generic;
using TMPro;

public class DivinationDirectionUI : WindowBase
{
	private static readonly string[] DefaultDirections =
	{
		"关系趋势",
		"相处建议",
		"心意确认",
		"冲突化解",
		"复合可能",
		"未来发展"
	};

	private static FriendDataManager.FriendData sPendingFriend;

	public DivinationDirectionUIComponent uiComponent;
	private FriendDataManager.FriendData currentFriend;
	private RelationshipDivinationRecord currentRecord;
	private bool isSubmitting;
	private int avatarRequestVersion;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<DivinationDirectionUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("DivinationDirectionUI 缺少 UI 组件绑定脚本：DivinationDirectionUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		currentFriend = sPendingFriend ?? RelationshipDivinationFlow.CurrentFriend;
		currentRecord = null;
		isSubmitting = false;
		avatarRequestVersion++;
		EnsureDirectionOptions();
		ResetInputIfNeeded();
		Render();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		avatarRequestVersion++;
		isSubmitting = false;
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region API Function
	public static DivinationDirectionUI Show(FriendDataManager.FriendData friend)
	{
		if (friend == null)
		{
			ToastManager.ShowToast("好友资料不完整");
			return null;
		}

		sPendingFriend = friend;
		DivinationDirectionUI window = UIModule.Instance.PopUpWindow<DivinationDirectionUI>();
		window?.SetFriend(friend);
		return window;
	}

	public void SetFriend(FriendDataManager.FriendData friend)
	{
		if (friend == null)
			return;

		sPendingFriend = friend;
		currentFriend = friend;
		currentRecord = null;
		isSubmitting = false;
		if (gameObject != null && gameObject.activeInHierarchy)
			Render();
	}

	#endregion

	#region UI组件事件
	public void OnExitButtonClick()
	{
		HideWindow();
	}

	public void OnSubmitButtonClick()
	{
		if (isSubmitting)
			return;

		if (currentFriend == null)
		{
			ToastManager.ShowToast("好友资料不完整");
			UpdateSubmitState();
			return;
		}

		string userQuestion = GetUserQuestion();
		if (string.IsNullOrWhiteSpace(userQuestion))
		{
			ToastManager.ShowToast("请输入想一起占卜的问题");
			uiComponent?.userInput?.ActivateInputField();
			UpdateSubmitState();
			return;
		}

		if (!RelationshipDivinationFlow.CanUseTwoPersonDivination(currentFriend, true))
		{
			UpdateSubmitState();
			return;
		}

		if (currentFriend.isVirtual)
		{
			StartLocalRelationshipReading();
			return;
		}

		RelationshipDivinationFlow.TryOpenActiveOrCreate(currentFriend, CreateRemoteInvite);
	}

	public void OnNextStepButtonClick()
	{
		if (currentRecord == null)
		{
			ToastManager.ShowToast("邀请记录不存在");
			return;
		}

		HideWindow();
		TwoPersonDivinationUI.Show(currentRecord, currentFriend);
	}

	public void OnDirectionDropDownChanged(int value)
	{
		UpdateSubmitState();
	}

	public void OnUserInputValueChanged(string text)
	{
		UpdateSubmitState();
	}
	#endregion

	private void Render()
	{
		if (uiComponent == null)
			return;

		SetInviteSuccessPanelVisible(false);
		RenderFriendInfo();
		RenderInputState();
		UpdateSubmitState();
	}

	private void RenderFriendInfo()
	{
		if (currentFriend == null)
			return;

		if (uiComponent.friendName != null)
			uiComponent.friendName.text = RelationshipDivinationFlow.GetFriendName(currentFriend);

		if (uiComponent.infoText != null)
		{
			if (currentFriend.isVirtual)
				uiComponent.infoText.text = "创建好友 · 本地关系占卜";
			else
				uiComponent.infoText.text = currentFriend.isOnline ? "真实好友 · 在线" : "真实好友 · 离线";
		}

		ApplyAvatar(FriendAvatarImageUtility.ResolveFriendAvatar(currentFriend, uiComponent.friendHead));
		LoadRemoteAvatarIfNeeded();
	}

	private void RenderInputState()
	{
		if (uiComponent.userInput != null)
		{
			TMP_Text placeholder = uiComponent.userInput.placeholder as TMP_Text;
			if (placeholder != null)
				placeholder.text = "写下你想和 TA 一起看的问题";
		}
	}

	private void EnsureDirectionOptions()
	{
		if (uiComponent?.directionDropDown == null)
			return;

		TMP_Dropdown dropdown = uiComponent.directionDropDown;
		if (dropdown.options != null && dropdown.options.Count >= DefaultDirections.Length)
			return;

		int previousValue = Mathf.Clamp(dropdown.value, 0, DefaultDirections.Length - 1);
		List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
		foreach (string direction in DefaultDirections)
			options.Add(new TMP_Dropdown.OptionData(direction));

		dropdown.ClearOptions();
		dropdown.AddOptions(options);
		dropdown.value = previousValue;
		dropdown.RefreshShownValue();
	}

	private void ResetInputIfNeeded()
	{
		if (uiComponent?.userInput == null)
			return;

		uiComponent.userInput.text = string.Empty;
	}

	private void UpdateSubmitState()
	{
		if (uiComponent?.btn == null)
			return;

		bool hasFriend = currentFriend != null;
		bool hasQuestion = !string.IsNullOrWhiteSpace(GetUserQuestion());
		bool canUse = hasFriend && RelationshipDivinationFlow.CanUseTwoPersonDivination(currentFriend, false);
		bool successVisible = uiComponent.inviteSuccessPanel != null && uiComponent.inviteSuccessPanel.activeSelf;

		uiComponent.btn.interactable = !isSubmitting && !successVisible && hasQuestion && canUse;
		RelationshipDivinationFlow.SetButtonText(
			uiComponent.btn,
			isSubmitting
				? currentFriend != null && currentFriend.isVirtual ? "生成中..." : "邀请中..."
				: currentFriend != null && currentFriend.isVirtual ? "开始" : "邀请");
	}

	private void StartLocalRelationshipReading()
	{
		if (currentFriend == null)
			return;

		isSubmitting = true;
		UpdateSubmitState();
		RelationshipDivinationRecord record = CreatedFriendRelationshipDivinationLocalFlow.CreateRecord(currentFriend, BuildQuestion());
		isSubmitting = false;
		if (record == null)
		{
			ToastManager.ShowToast("本地关系占卜创建失败");
			UpdateSubmitState();
			return;
		}

		HideWindow();
		RelationshipDivinationFlow.ShowRecord(record, currentFriend);
	}

	private void CreateRemoteInvite()
	{
		RelationshipDivinationFirestore service = RelationshipDivinationFlow.GetOrCreateService();
		if (service == null)
		{
			ToastManager.ShowToast("关系占卜服务初始化中，请稍后再试");
			UpdateSubmitState();
			return;
		}

		isSubmitting = true;
		UpdateSubmitState();
		service.CreateInvite(currentFriend, BuildQuestion(), record =>
		{
			isSubmitting = false;
			if (record == null)
			{
				UpdateSubmitState();
				return;
			}

			currentRecord = record;
			SetInviteSuccessPanelVisible(true);
			UpdateSubmitState();
		}, false);
	}

	private string BuildQuestion()
	{
		string friendName = currentFriend != null ? RelationshipDivinationFlow.GetFriendName(currentFriend) : "好友";
		string direction = GetSelectedDirection();
		string userQuestion = GetUserQuestion();
		return $"我想和 {friendName} 一起看「{direction}」：{userQuestion}";
	}

	private string GetSelectedDirection()
	{
		TMP_Dropdown dropdown = uiComponent != null ? uiComponent.directionDropDown : null;
		if (dropdown != null && dropdown.options != null && dropdown.options.Count > 0)
		{
			int index = Mathf.Clamp(dropdown.value, 0, dropdown.options.Count - 1);
			string text = dropdown.options[index]?.text;
			if (!string.IsNullOrWhiteSpace(text))
				return text.Trim();
		}

		return DefaultDirections[0];
	}

	private string GetUserQuestion()
	{
		return uiComponent?.userInput != null ? (uiComponent.userInput.text ?? string.Empty).Trim() : string.Empty;
	}

	private void SetInviteSuccessPanelVisible(bool visible)
	{
		if (uiComponent?.inviteSuccessPanel != null)
			uiComponent.inviteSuccessPanel.SetActive(visible && currentRecord != null && currentFriend != null && !currentFriend.isVirtual);
		if (uiComponent?.nextStepBtn != null)
			uiComponent.nextStepBtn.interactable = visible && currentRecord != null;
	}

	private void ApplyAvatar(Sprite sprite)
	{
		FriendAvatarImageUtility.ApplyAvatar(uiComponent != null ? uiComponent.friendHead : null, sprite);
	}

	private void LoadRemoteAvatarIfNeeded()
	{
		if (currentFriend == null
			|| currentFriend.headSprite != null
			|| string.IsNullOrWhiteSpace(currentFriend.photoUrl)
			|| uiComponent == null)
			return;

		int requestId = ++avatarRequestVersion;
		FriendDataManager.FriendData friend = currentFriend;
		uiComponent.StartCoroutine(FriendAvatarImageUtility.LoadUserSpriteFromUrlCoroutine(currentFriend.name, currentFriend.photoUrl, sprite =>
		{
			if (requestId != avatarRequestVersion || currentFriend != friend || sprite == null)
				return;

			currentFriend.headSprite = sprite;
			ApplyAvatar(sprite);
		}));
	}
}

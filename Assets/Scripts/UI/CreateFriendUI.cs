/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 12:25:22
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using System.Collections;
using System.IO;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using TMPro;

public class CreateFriendUI : WindowBase
{
	public CreateFriendUIComponent uiComponent;

	private const int MaxNameLength = 20;
	private const string BirthdayPlaceholder = "选择生日";
	private const string BirthTimePlaceholder = "选择出生时间";
	private const string CityPlaceholder = "选择出生城市";
	private const float SuccessHideDelaySeconds = 1f;
	private static readonly Color CreateSuccessColor = new Color32(74, 214, 124, 255);
	private static readonly Color CreateFailColor = new Color32(255, 92, 92, 255);
	private static FriendDataManager.FriendData sPendingEditFriend;
	private static Action<FriendDataManager.FriendData> sPendingEditSaved;

	private int selectedAvatarIndex = 0;
	private Sprite selectedAvatarSprite;
	private Sprite prefabFallbackAvatarSprite;
	private Sprite accountAvatarSprite;
	private string selectedAvatarImagePath = string.Empty;
	private string accountAvatarImagePath = string.Empty;
	private bool hasUserSelectedAvatar;
	private bool formInitialized;
	private int avatarLoadVersion;
	private TMP_InputField birthdayDateInputField;
	private TMP_InputField birthdayTimeInputField;
	private TMP_InputField birthdayCountryInputField;
	private Coroutine hideAfterSuccessCoroutine;
	private string defaultPrivacyText;
	private Color defaultPrivacyTextColor = new Color(1f, 1f, 1f, 0.8f);
	private string username = string.Empty;
	private string birthday = string.Empty;
	private string birthTime = string.Empty;
	private string city = string.Empty;
	private string defaultTitleText;
	private string defaultSubmitButtonText;
	private FriendDataManager.FriendData editingFriend;
	private Action<FriendDataManager.FriendData> onEditSaved;
	private bool isEditMode;

	#region 生命周期函数
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<CreateFriendUIComponent>();
		uiComponent.InitComponent(this);
		BindSelectionInputFields();
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}

	public override void OnShow()
	{
		base.OnShow();
		CapturePrefabFallbackAvatar();
		CapturePrivacyTextDefaults();
		CaptureDefaultModeTexts();
		CancelHideAfterSuccess();
		SetSubmitInteractable(true);
		RestorePrivacyText();
		HideChooseHeadPanel();
		ConsumePendingEditMode();
		RefreshModeTexts();

		if (isEditMode)
		{
			if (editingFriend == null || !editingFriend.isVirtual)
			{
				ToastManager.ShowToast("只有自己创建的好友可以编辑");
				formInitialized = false;
				HideWindow();
				return;
			}

			formInitialized = true;
			accountAvatarSprite = null;
			accountAvatarImagePath = string.Empty;
			PopulateEditForm();
			RefreshAvatarPreview();
			RefreshNameCounter();
			LoadEditAvatarIfNeeded();
			return;
		}

		if (!formInitialized)
		{
			formInitialized = true;
			accountAvatarSprite = null;
			accountAvatarImagePath = string.Empty;
			hasUserSelectedAvatar = false;
			ResetForm();
			LoadDefaultAccountAvatar();
		}
		else
		{
			ReadFormValues();
			RefreshSelectionTexts();
		}

		RefreshAvatarPreview();
		RefreshNameCounter();
	}


	public override void OnHide()
	{
		CancelHideAfterSuccess();
		avatarLoadVersion++;
		HideChooseHeadPanel();
		if (isEditMode)
			formInitialized = false;
		editingFriend = null;
		onEditSaved = null;
		isEditMode = false;
		base.OnHide();
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region API Function
	public static void ShowEdit(FriendDataManager.FriendData friend, Action<FriendDataManager.FriendData> onSaved = null)
	{
		if (friend == null || !friend.isVirtual)
		{
			ToastManager.ShowToast("只有自己创建的好友可以编辑");
			return;
		}

		sPendingEditFriend = friend;
		sPendingEditSaved = onSaved;
		UIModule.Instance.PopUpWindow<CreateFriendUI>();
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		if (IsChooseHeadPanelVisible())
		{
			HideChooseHeadPanel();
			return;
		}

		formInitialized = false;
		HideWindow();
	}

	public void OnUploadAvatarButtonClick()
	{
		if (uiComponent.chooseHeadPanel != null)
		{
			ShowChooseHeadPanel();
			return;
		}

		SelectFriendAvatarUI.Show(selectedAvatarSprite, selectedAvatarIndex, selectedAvatarImagePath, ApplySelectedAvatar);
	}

	public void OnHideChooseHeadButtonClick()
	{
		HideChooseHeadPanel();
	}

	public void OnAIGenerateButtonClick()
	{
		ReadFormValues();
		HideChooseHeadPanel();

		string seed = $"{username}|{birthday}|{birthTime}|{city}|{DateTime.UtcNow.Ticks}";
		FriendAvatarImageUtility.PickedAvatar avatar = FriendAvatarImageUtility.GenerateAiAvatar(seed);
		if (avatar == null || avatar.sprite == null)
		{
			ToastManager.ShowToast("AI 头像生成失败，请稍后重试");
			return;
		}

		ApplySelectedAvatar(avatar.sprite, 0, avatar.persistentPath);
		ToastManager.ShowToast("AI 头像已生成");
	}

	public void OnFromPhotoAlbumButtonClick()
	{
		HideChooseHeadPanel();
		FriendAvatarImageUtility.PickAvatar(
			avatar =>
			{
				if (avatar == null || avatar.sprite == null) return;
				ApplySelectedAvatar(avatar.sprite, 0, avatar.persistentPath);
				ToastManager.ShowToast("头像已选择");
			},
			error =>
			{
				if (!string.IsNullOrEmpty(error) && error != "已取消选择头像")
				{
					Debug.LogWarning("[CreateFriendUI] 选择好友头像失败: " + error);
					ToastManager.ShowToast(error);
				}
			});
	}

	public void OnInputInputChange(string text)
	{
		username = TrimName(text);
		if (uiComponent.InputInputField != null && uiComponent.InputInputField.text != username)
		{
			uiComponent.InputInputField.text = username;
		}
		RestorePrivacyText();
		RefreshNameCounter();
	}

	public void OnInputInputEnd(string text)
	{
		username = TrimName(text);
		RestorePrivacyText();
		RefreshNameCounter();
	}

	public void OnField_birthdayDateButtonClick()
	{
		SpinPickerUI.ShowDate(birthday, ApplyBirthday);
	}

	public void OnField_birthdayTimeButtonClick()
	{
		SpinPickerUI.ShowTime(birthTime, ApplyBirthTime);
	}

	public void OnField_birthdayCountryButtonClick()
	{
		SpinPickerUI.ShowRegion(city, ApplyCity);
	}

	public void OnSubmitButtonClick()
	{
		HideChooseHeadPanel();
		ReadFormValues();
		username = TrimName(username);
		if (string.IsNullOrWhiteSpace(username))
		{
			if (isEditMode)
				ShowEditFailure("请先填写好友名字");
			else
				ShowCreateFailure("请先填写好友名字");
			FocusInput(uiComponent.InputInputField);
			return;
		}

		SetSubmitInteractable(false);
		if (isEditMode)
		{
			ConfirmEditFriend(BuildFriendDraft());
			return;
		}

		ConfirmCreateFriend(BuildFriendDraft());
	}

	private ConfirmFriendInfoUI.FriendDraft BuildFriendDraft()
	{
		return new ConfirmFriendInfoUI.FriendDraft
		{
			name = username.Trim(),
			birthday = birthday.Trim(),
			birthTime = birthTime.Trim(),
			city = city.Trim(),
			notes = BuildFriendNotes(),
			avatarSprite = selectedAvatarSprite,
			avatarImagePath = selectedAvatarImagePath
		};
	}

	private void ConfirmCreateFriend(ConfirmFriendInfoUI.FriendDraft draft)
	{
		if (draft == null || string.IsNullOrWhiteSpace(draft.name))
		{
			ShowCreateFailure("好友信息缺失，请返回重试");
			return;
		}

		try
		{
			var createdFriend = FriendDataManager.Instance.AddVirtualFriend(
				draft.name.Trim(),
				"好友",
				draft.birthday.Trim(),
				draft.birthTime.Trim(),
				draft.city.Trim(),
				draft.notes,
				draft.avatarSprite,
				draft.avatarImagePath);

			if (createdFriend == null)
			{
				ShowCreateFailure("好友资料保存失败");
				return;
			}

			SaveCreatedFriendToCloud(createdFriend);
			formInitialized = false;
			ShowCreateSuccess();
		}
		catch (Exception ex)
		{
			Debug.LogError("[CreateFriendUI] 创建好友失败: " + ex);
			ShowCreateFailure(string.IsNullOrWhiteSpace(ex.Message) ? "保存好友资料时出错" : ex.Message);
		}
	}

	private void ConfirmEditFriend(ConfirmFriendInfoUI.FriendDraft draft)
	{
		if (editingFriend == null || !editingFriend.isVirtual)
		{
			ShowEditFailure("好友资料不完整");
			return;
		}

		if (draft == null || string.IsNullOrWhiteSpace(draft.name))
		{
			ShowEditFailure("好友信息缺失，请返回重试");
			return;
		}

		try
		{
			string previousAvatarPath = editingFriend.avatarImagePath ?? string.Empty;
			bool updated = FriendDataManager.Instance.UpdateVirtualFriend(
				editingFriend,
				draft.name.Trim(),
				string.IsNullOrWhiteSpace(editingFriend.relationship) ? "好友" : editingFriend.relationship,
				draft.birthday.Trim(),
				draft.birthTime.Trim(),
				draft.city.Trim(),
				draft.notes,
				draft.avatarSprite,
				draft.avatarImagePath);

			if (!updated)
			{
				ShowEditFailure("好友资料保存失败");
				return;
			}

			SyncEditedFriendToCloud(previousAvatarPath);
			ToastManager.ShowToast("好友资料已保存");
			onEditSaved?.Invoke(editingFriend);
			formInitialized = false;
			HideWindow();
		}
		catch (Exception ex)
		{
			Debug.LogError("[CreateFriendUI] 编辑好友失败: " + ex);
			ShowEditFailure(string.IsNullOrWhiteSpace(ex.Message) ? "保存好友资料时出错" : ex.Message);
		}
	}

	private void SaveCreatedFriendToCloud(FriendDataManager.FriendData createdFriend)
	{
		if (createdFriend == null)
		{
			return;
		}

		if (FirestoreManager.Instance == null)
		{
			FirestoreManager.QueueVirtualFriendSaveLocal(createdFriend);
			return;
		}

		if (ShouldUploadVirtualFriendAvatar(createdFriend))
		{
			AvatarUploadManager.Instance.UploadVirtualFriendAvatarFromFile(
				createdFriend.virtualFriendId,
				createdFriend.avatarImagePath,
				result =>
				{
					FriendDataManager.Instance.SetVirtualFriendCloudAvatar(
						createdFriend,
						result.photoUrl,
						result.storagePath,
						result.previewSprite);
					SaveVirtualFriendDocument(createdFriend);
				},
				error =>
				{
					Debug.LogWarning("[CreateFriendUI] 创建好友头像上传失败: " + error);
					SaveVirtualFriendDocument(createdFriend);
				});
			return;
		}

		SaveVirtualFriendDocument(createdFriend);
	}

	private void SyncEditedFriendToCloud(string previousAvatarPath)
	{
		FriendDataManager.FriendData friend = editingFriend;
		if (friend == null)
			return;

		if (FirestoreManager.Instance == null)
		{
			FirestoreManager.QueueVirtualFriendSaveLocal(friend);
			return;
		}

		if (ShouldUploadEditedVirtualFriendAvatar(friend, previousAvatarPath))
		{
			AvatarUploadManager.Instance.UploadVirtualFriendAvatarFromFile(
				friend.virtualFriendId,
				friend.avatarImagePath,
				result =>
				{
					FriendDataManager.Instance.SetVirtualFriendCloudAvatar(
						friend,
						result.photoUrl,
						result.storagePath,
						result.previewSprite);
					SaveVirtualFriendDocument(friend);
				},
				error =>
				{
					Debug.LogWarning("[CreateFriendUI] 编辑好友头像上传失败: " + error);
					SaveVirtualFriendDocument(friend);
				});
			return;
		}

		SaveVirtualFriendDocument(friend);
	}

	private bool ShouldUploadVirtualFriendAvatar(FriendDataManager.FriendData friend)
	{
		return friend != null
			&& !string.IsNullOrWhiteSpace(friend.avatarImagePath)
			&& File.Exists(friend.avatarImagePath)
			&& AvatarUploadManager.Instance != null;
	}

	private bool ShouldUploadEditedVirtualFriendAvatar(FriendDataManager.FriendData friend, string previousAvatarPath)
	{
		return ShouldUploadVirtualFriendAvatar(friend)
			&& (string.IsNullOrWhiteSpace(friend.photoUrl)
				|| !string.Equals(previousAvatarPath, friend.avatarImagePath, StringComparison.Ordinal));
	}

	private void SaveVirtualFriendDocument(FriendDataManager.FriendData friend)
	{
		if (friend == null)
		{
			return;
		}

		if (FirestoreManager.Instance == null)
		{
			FirestoreManager.QueueVirtualFriendSaveLocal(friend);
			ToastManager.ShowToast("已保存到本地，稍后会同步云端");
			return;
		}

		FirestoreManager.Instance.SaveVirtualFriend(friend, success =>
		{
			if (!success)
			{
				ToastManager.ShowToast("已保存到本地，稍后会同步云端");
			}
		});
	}

	private void HandleConfirmEditRequested(ConfirmFriendInfoUI.EditTarget target)
	{
		switch (target)
		{
			case ConfirmFriendInfoUI.EditTarget.Name:
				FocusInput(uiComponent.InputInputField);
				break;
			case ConfirmFriendInfoUI.EditTarget.Birthday:
				OnField_birthdayDateButtonClick();
				break;
			case ConfirmFriendInfoUI.EditTarget.BirthTime:
				OnField_birthdayTimeButtonClick();
				break;
			case ConfirmFriendInfoUI.EditTarget.City:
				OnField_birthdayCountryButtonClick();
				break;
		}
	}

	public void OnAvatar1ButtonClick()
	{
		SelectAvatar(1);
	}

	public void OnAvatar2ButtonClick()
	{
		SelectAvatar(2);
	}

	public void OnAvatar3ButtonClick()
	{
		SelectAvatar(3);
	}

	public void OnAvatar4ButtonClick()
	{
		SelectAvatar(4);
	}

	public void OnNotificationsButtonClick()
	{
		UIModule.Instance.PopUpWindow<NotionUI>();
	}

	public void OnAvatarSelectionToggleChange(bool state, Toggle toggle)
	{
	}

	public void OnAvatar1ToggleChange(bool state, Toggle toggle)
	{
	}

	public void OnAvatar2ToggleChange(bool state, Toggle toggle)
	{
	}

	public void OnAvatar3ToggleChange(bool state, Toggle toggle)
	{
	}

	public void OnAvatar4ToggleChange(bool state, Toggle toggle)
	{
	}

	public void OnBottomNavToggleChange(bool state, Toggle toggle)
	{
	}

	public void OnTabOracleToggleChange(bool state, Toggle toggle)
	{
		if (!state) return;
		HideWindow();
		UIModule.Instance.PopUpWindow<TodayOracleUI>();
	}

	public void OnTabChatToggleChange(bool state, Toggle toggle)
	{
		if (!state) return;
		HideWindow();
		UIModule.Instance.PopUpWindow<DialogUI>();
	}

	public void OnTabFriendsToggleChange(bool state, Toggle toggle)
	{
		if (!state) return;
		HideWindow();
		NavigationUI navigation = UIModule.Instance.PopUpWindow<NavigationUI>();
		if (navigation != null)
		{
			navigation.OpenFriendUI();
			return;
		}
		UIModule.Instance.PopUpWindow<FriendUI>();
	}

	public void OnTabProfileToggleChange(bool state, Toggle toggle)
	{
		if (!state) return;
		HideWindow();
		UIModule.Instance.PopUpWindow<MyUI>();
	}

	private void ReadFormValues()
	{
		username = uiComponent.InputInputField != null ? TrimName(uiComponent.InputInputField.text) : username;
		birthday = ReadSelectedInput(birthdayDateInputField, uiComponent.birthdayDateText, BirthdayPlaceholder, birthday);
		birthTime = ReadSelectedInput(birthdayTimeInputField, uiComponent.birthdayTimeText, BirthTimePlaceholder, birthTime);
		city = ReadSelectedInput(birthdayCountryInputField, uiComponent.birthdayCountryText, CityPlaceholder, city);
	}

	private void ConsumePendingEditMode()
	{
		editingFriend = sPendingEditFriend;
		onEditSaved = sPendingEditSaved;
		isEditMode = editingFriend != null && editingFriend.isVirtual;
		sPendingEditFriend = null;
		sPendingEditSaved = null;
	}

	private void CaptureDefaultModeTexts()
	{
		if (uiComponent?.titleText != null && defaultTitleText == null)
			defaultTitleText = uiComponent.titleText.text;
		if (uiComponent?.SubmitButtonText != null && defaultSubmitButtonText == null)
			defaultSubmitButtonText = uiComponent.SubmitButtonText.text;
	}

	private void RefreshModeTexts()
	{
		SetText(uiComponent?.titleText, isEditMode ? "编辑好友" : FirstNonEmpty(defaultTitleText, "创建好友"));
		SetText(uiComponent?.SubmitButtonText, isEditMode ? "保存修改" : FirstNonEmpty(defaultSubmitButtonText, "创建好友"));
	}

	private void PopulateEditForm()
	{
		if (editingFriend == null)
			return;

		hasUserSelectedAvatar = true;
		selectedAvatarIndex = 0;
		selectedAvatarSprite = FriendAvatarImageUtility.ResolveFriendAvatar(editingFriend, uiComponent.AvatarPreviewImage, prefabFallbackAvatarSprite);
		selectedAvatarImagePath = editingFriend.avatarImagePath ?? string.Empty;
		username = TrimName(editingFriend.name);
		birthday = editingFriend.birthday ?? string.Empty;
		birthTime = editingFriend.birthTime ?? string.Empty;
		city = editingFriend.city ?? string.Empty;

		if (uiComponent.InputInputField != null)
		{
			uiComponent.InputInputField.characterLimit = MaxNameLength;
			uiComponent.InputInputField.text = username;
		}
		RefreshSelectionTexts();
	}

	private void ResetForm()
	{
		selectedAvatarIndex = accountAvatarSprite != null ? 0 : 1;
		selectedAvatarSprite = accountAvatarSprite != null ? accountAvatarSprite : prefabFallbackAvatarSprite;
		selectedAvatarImagePath = accountAvatarSprite != null ? accountAvatarImagePath : string.Empty;
		username = string.Empty;
		birthday = string.Empty;
		birthTime = string.Empty;
		city = string.Empty;

		if (uiComponent.InputInputField != null)
		{
			uiComponent.InputInputField.characterLimit = MaxNameLength;
			uiComponent.InputInputField.text = string.Empty;
		}
		SetSelectionValue(birthdayDateInputField, uiComponent.birthdayDateText, string.Empty, BirthdayPlaceholder);
		SetSelectionValue(birthdayTimeInputField, uiComponent.birthdayTimeText, string.Empty, BirthTimePlaceholder);
		SetSelectionValue(birthdayCountryInputField, uiComponent.birthdayCountryText, string.Empty, CityPlaceholder);
	}

	private void RefreshSelectionTexts()
	{
		SetSelectionValue(birthdayDateInputField, uiComponent.birthdayDateText, birthday, BirthdayPlaceholder);
		SetSelectionValue(birthdayTimeInputField, uiComponent.birthdayTimeText, birthTime, BirthTimePlaceholder);
		SetSelectionValue(birthdayCountryInputField, uiComponent.birthdayCountryText, city, CityPlaceholder);
	}

	private void LoadEditAvatarIfNeeded()
	{
		if (editingFriend == null
			|| editingFriend.headSprite != null
			|| string.IsNullOrWhiteSpace(editingFriend.photoUrl)
			|| uiComponent == null)
		{
			return;
		}

		int loadVersion = ++avatarLoadVersion;
		uiComponent.StartCoroutine(FriendAvatarImageUtility.LoadUserSpriteFromUrlCoroutine(editingFriend.name, editingFriend.photoUrl, sprite =>
		{
			if (loadVersion != avatarLoadVersion || editingFriend == null || sprite == null)
				return;

			editingFriend.headSprite = sprite;
			selectedAvatarSprite = sprite;
			RefreshAvatarPreview();
		}));
	}

	private void SelectAvatar(int index)
	{
		hasUserSelectedAvatar = true;
		selectedAvatarIndex = Mathf.Max(1, index);
		selectedAvatarImagePath = string.Empty;
		RefreshAvatarPreview();
	}

	private void ApplySelectedAvatar(Sprite avatarSprite, int avatarIndex)
	{
		ApplySelectedAvatar(avatarSprite, avatarIndex, string.Empty);
	}

	private void ApplySelectedAvatar(Sprite avatarSprite, int avatarIndex, string avatarImagePath)
	{
		hasUserSelectedAvatar = true;
		selectedAvatarIndex = avatarIndex > 0 ? avatarIndex : 0;
		selectedAvatarSprite = avatarSprite;
		selectedAvatarImagePath = avatarImagePath ?? string.Empty;
		RefreshAvatarPreview();
	}

	private void ApplyBirthday(string value)
	{
		birthday = value ?? string.Empty;
		SetSelectionValue(birthdayDateInputField, uiComponent.birthdayDateText, birthday, BirthdayPlaceholder);
	}

	private void ApplyBirthTime(string value)
	{
		birthTime = value ?? string.Empty;
		SetSelectionValue(birthdayTimeInputField, uiComponent.birthdayTimeText, birthTime, BirthTimePlaceholder);
	}

	private void ApplyCity(string value)
	{
		city = value ?? string.Empty;
		SetSelectionValue(birthdayCountryInputField, uiComponent.birthdayCountryText, city, CityPlaceholder);
	}

	private void RefreshAvatarPreview()
	{
		if (selectedAvatarSprite == null)
		{
			selectedAvatarSprite = accountAvatarSprite != null ? accountAvatarSprite : prefabFallbackAvatarSprite;
		}

		SetAvatarImage(uiComponent.AvatarPreviewImage, selectedAvatarSprite);
		SetAvatarImage(uiComponent.headImage, selectedAvatarSprite);
	}

	private void SetAvatarImage(Image image, Sprite sprite)
	{
		if (image == null || sprite == null)
			return;

		image.sprite = sprite;
	}

	private void ShowChooseHeadPanel()
	{
		if (uiComponent.chooseHeadPanel != null)
		{
			uiComponent.chooseHeadPanel.SetActive(true);
		}
	}

	private void HideChooseHeadPanel()
	{
		if (uiComponent != null && uiComponent.chooseHeadPanel != null)
		{
			uiComponent.chooseHeadPanel.SetActive(false);
		}
	}

	private bool IsChooseHeadPanelVisible()
	{
		return uiComponent != null
			&& uiComponent.chooseHeadPanel != null
			&& uiComponent.chooseHeadPanel.activeSelf;
	}

	private void CapturePrefabFallbackAvatar()
	{
		if (prefabFallbackAvatarSprite == null && uiComponent.AvatarPreviewImage != null)
		{
			prefabFallbackAvatarSprite = uiComponent.AvatarPreviewImage.sprite;
		}
	}

	private void LoadDefaultAccountAvatar()
	{
		if (GameManager.Instance == null)
		{
			return;
		}

		int loadVersion = ++avatarLoadVersion;
		GameManager.Instance.StartCoroutine(FriendAvatarImageUtility.LoadCurrentUserAvatarCoroutine((sprite, path) =>
		{
			if (loadVersion != avatarLoadVersion || sprite == null)
			{
				return;
			}

			accountAvatarSprite = sprite;
			accountAvatarImagePath = path ?? string.Empty;
			if (hasUserSelectedAvatar)
			{
				return;
			}

			selectedAvatarIndex = 0;
			selectedAvatarSprite = accountAvatarSprite;
			selectedAvatarImagePath = accountAvatarImagePath;
			RefreshAvatarPreview();
		}));
	}

	private void RefreshNameCounter()
	{
		if (uiComponent.UsernameCountText != null)
		{
			uiComponent.UsernameCountText.text = $"{username.Length}/{MaxNameLength}";
		}
	}

	private void CapturePrivacyTextDefaults()
	{
		if (uiComponent?.privacyText == null || defaultPrivacyText != null)
		{
			return;
		}

		defaultPrivacyText = uiComponent.privacyText.text;
		defaultPrivacyTextColor = uiComponent.privacyText.color;
	}

	private void RestorePrivacyText()
	{
		if (uiComponent?.privacyText == null)
		{
			return;
		}

		uiComponent.privacyText.gameObject.SetActive(true);
		uiComponent.privacyText.text = string.IsNullOrEmpty(defaultPrivacyText)
			? "虚拟占卜伙伴，只有你可见。"
			: defaultPrivacyText;
		uiComponent.privacyText.color = defaultPrivacyTextColor;
	}

	private void ShowCreateSuccess()
	{
		SetSubmitInteractable(false);
		ShowCreateStatus("创建成功", CreateSuccessColor);
		StartHideAfterSuccess();
	}

	private void ShowCreateFailure(string reason)
	{
		CancelHideAfterSuccess();
		SetSubmitInteractable(true);
		string message = string.IsNullOrWhiteSpace(reason)
			? "创建失败"
			: $"创建失败：{reason}";
		ShowCreateStatus(message, CreateFailColor);
	}

	private void ShowEditFailure(string reason)
	{
		CancelHideAfterSuccess();
		SetSubmitInteractable(true);
		string message = string.IsNullOrWhiteSpace(reason)
			? "保存失败"
			: $"保存失败：{reason}";
		ShowCreateStatus(message, CreateFailColor);
	}

	private void ShowCreateStatus(string message, Color color)
	{
		if (uiComponent?.privacyText == null)
		{
			ToastManager.ShowToast(message);
			return;
		}

		uiComponent.privacyText.gameObject.SetActive(true);
		uiComponent.privacyText.text = message;
		uiComponent.privacyText.color = color;
	}

	private void SetSubmitInteractable(bool interactable)
	{
		if (uiComponent?.SubmitButton != null)
		{
			uiComponent.SubmitButton.interactable = interactable;
		}
	}

	private void StartHideAfterSuccess()
	{
		CancelHideAfterSuccess();
		if (uiComponent != null)
		{
			hideAfterSuccessCoroutine = uiComponent.StartCoroutine(HideAfterSuccessDelay());
		}
	}

	private void CancelHideAfterSuccess()
	{
		if (hideAfterSuccessCoroutine == null)
		{
			return;
		}

		if (uiComponent != null)
		{
			uiComponent.StopCoroutine(hideAfterSuccessCoroutine);
		}
		hideAfterSuccessCoroutine = null;
	}

	private IEnumerator HideAfterSuccessDelay()
	{
		yield return new WaitForSeconds(SuccessHideDelaySeconds);
		hideAfterSuccessCoroutine = null;
		HideWindow();
		OpenFriendListAfterCreateSuccess();
	}

	private void OpenFriendListAfterCreateSuccess()
	{
		UIModule.Instance.HideWindow<AddFriendUI>();
		UIModule.Instance.HideWindow<NoFriendUI>();
		UIModule.Instance.HideWindow<FriendRequestUI>();

		NavigationUI navigation = UIModule.Instance.PopUpWindow<NavigationUI>();
		if (navigation != null)
		{
			navigation.OpenFriendUI();
			return;
		}

		UIModule.Instance.PopUpWindow<FriendUI>();
	}

	private string TrimName(string value)
	{
		if (string.IsNullOrEmpty(value)) return string.Empty;
		value = value.Trim();
		return value.Length <= MaxNameLength ? value : value.Substring(0, MaxNameLength);
	}

	private string FirstNonEmpty(params string[] values)
	{
		if (values == null) return string.Empty;
		foreach (string value in values)
		{
			if (!string.IsNullOrWhiteSpace(value))
				return value.Trim();
		}
		return string.Empty;
	}

	private void BindSelectionInputFields()
	{
		birthdayDateInputField = FindSelectionInputField(uiComponent.Field_birthdayDateButton, uiComponent.birthdayDateText);
		birthdayTimeInputField = FindSelectionInputField(uiComponent.Field_birthdayTimeButton, uiComponent.birthdayTimeText);
		birthdayCountryInputField = FindSelectionInputField(uiComponent.Field_birthdayCountryButton, uiComponent.birthdayCountryText);
	}

	private TMP_InputField FindSelectionInputField(Button button, TMP_Text text)
	{
		if (text != null)
		{
			TMP_InputField input = text.GetComponentInParent<TMP_InputField>();
			if (input != null) return input;
		}

		return button != null ? button.GetComponentInChildren<TMP_InputField>(true) : null;
	}

	private string ReadSelectedInput(TMP_InputField input, TMP_Text text, string placeholder, string fallback)
	{
		string value = input != null ? input.text : text != null ? text.text : fallback;
		return string.IsNullOrWhiteSpace(value) || value == placeholder ? string.Empty : value;
	}

	private void SetSelectionValue(TMP_InputField input, TMP_Text text, string value, string placeholder)
	{
		if (input != null)
		{
			input.text = string.IsNullOrWhiteSpace(value) ? string.Empty : value;
			return;
		}

		SetText(text, string.IsNullOrWhiteSpace(value) ? placeholder : value);
	}

	private void SetText(TMP_Text text, string value)
	{
		if (text != null)
		{
			text.text = value;
		}
	}

	private void FocusInput(TMP_InputField input)
	{
		if (input == null) return;
		input.Select();
		input.ActivateInputField();
	}

	private string BuildFriendNotes()
	{
		string avatarLabel = selectedAvatarIndex > 0 ? $"头像 {selectedAvatarIndex}" : "登录/自定义头像";
		var parts = new System.Collections.Generic.List<string> { $"虚拟好友档案 · {avatarLabel}" };
		parts.Add(string.IsNullOrWhiteSpace(birthday) ? "生日未知" : $"生日 {birthday.Trim()}");
		parts.Add(string.IsNullOrWhiteSpace(birthTime) ? "出生时间未知" : $"出生时间 {birthTime.Trim()}");
		parts.Add(string.IsNullOrWhiteSpace(city) ? "出生地区未知" : $"出生地区 {city.Trim()}");
		return string.Join(" · ", parts);
	}
	#endregion
}

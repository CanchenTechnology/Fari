/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/18
 * Description: 好友头像选择界面
---------------------------------*/
using System;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class SelectFriendAvatarUI : WindowBase
{
	public SelectFriendAvatarUIComponent uiComponent;

	private static Action<Sprite, int> sOnAvatarSelected;
	private static Action<Sprite, int, string> sOnAvatarSelectedWithPath;
	private static Sprite sInitialSprite;
	private static int sInitialIndex;
	private static string sInitialAvatarImagePath;

	private int selectedAvatarIndex;
	private Sprite selectedAvatarSprite;
	private string selectedAvatarImagePath = string.Empty;

	public static void Show(Sprite initialSprite, int initialIndex, Action<Sprite, int> onAvatarSelected)
	{
		Show(initialSprite, initialIndex, string.Empty, (sprite, index, _) => onAvatarSelected?.Invoke(sprite, index));
	}

	public static void Show(Sprite initialSprite, int initialIndex, string initialAvatarImagePath, Action<Sprite, int, string> onAvatarSelected)
	{
		sInitialSprite = initialSprite;
		sInitialIndex = initialIndex > 0 ? initialIndex - 1 : -1;
		sInitialAvatarImagePath = initialAvatarImagePath ?? string.Empty;
		sOnAvatarSelected = null;
		sOnAvatarSelectedWithPath = onAvatarSelected;
		UIModule.Instance.PopUpWindow<SelectFriendAvatarUI>();
	}

	#region 生命周期函数
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<SelectFriendAvatarUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}

	public override void OnShow()
	{
		base.OnShow();
		SelectAvatarStyle(GetValidInitialIndex());
		if (sInitialSprite != null)
		{
			selectedAvatarSprite = sInitialSprite;
			selectedAvatarImagePath = sInitialAvatarImagePath;
			selectedAvatarIndex = sInitialIndex >= 0 && string.IsNullOrEmpty(selectedAvatarImagePath) ? selectedAvatarIndex : -1;
			RefreshPreview();
			RefreshSelectedMarks();
		}
	}

	public override void OnHide()
	{
		base.OnHide();
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}

	public void OnPhotoUploadButtonClick()
	{
		PickUploadedAvatar();
	}

	public void OnAlbumSelectButtonClick()
	{
		PickUploadedAvatar();
	}

	public void OnAIAvatarButtonClick()
	{
		FriendAvatarImageUtility.PickedAvatar avatar = FriendAvatarImageUtility.GenerateAiAvatar(DateTime.UtcNow.Ticks.ToString());
		if (avatar == null || avatar.sprite == null)
		{
			ToastManager.ShowToast("AI 头像生成失败，请稍后重试");
			return;
		}

		selectedAvatarIndex = -1;
		selectedAvatarSprite = avatar.sprite;
		selectedAvatarImagePath = avatar.persistentPath ?? string.Empty;
		RefreshPreview();
		RefreshSelectedMarks();
		ToastManager.ShowToast("AI 头像已生成");
	}

	public void OnAvatarStyleButtonClick(int index)
	{
		SelectAvatarStyle(index);
	}

	public void OnConfirmButtonClick()
	{
		int avatarIndex = selectedAvatarIndex >= 0 ? selectedAvatarIndex + 1 : 0;
		sOnAvatarSelected?.Invoke(selectedAvatarSprite, avatarIndex);
		sOnAvatarSelectedWithPath?.Invoke(selectedAvatarSprite, avatarIndex, selectedAvatarImagePath);
		HideWindow();
	}
	#endregion

	private void PickUploadedAvatar()
	{
		FriendAvatarImageUtility.PickAvatar(
			avatar =>
			{
				if (avatar == null || avatar.sprite == null) return;

				selectedAvatarIndex = -1;
				selectedAvatarSprite = avatar.sprite;
				selectedAvatarImagePath = avatar.persistentPath ?? string.Empty;
				RefreshPreview();
				RefreshSelectedMarks();
				ToastManager.ShowToast("头像已选择");
			},
			error =>
			{
				if (!string.IsNullOrEmpty(error) && error != "已取消选择头像")
				{
					Debug.LogWarning("[SelectFriendAvatarUI] 选择好友头像失败: " + error);
					ToastManager.ShowToast(error);
				}
			});
	}

	private int GetValidInitialIndex()
	{
		int count = uiComponent.AvatarStyleButtons == null ? 0 : uiComponent.AvatarStyleButtons.Length;
		if (count <= 0) return 0;
		return Mathf.Clamp(sInitialIndex, 0, count - 1);
	}

	private void SelectAvatarStyle(int index)
	{
		if (uiComponent.AvatarStyleImages == null || uiComponent.AvatarStyleImages.Length == 0)
			return;

		selectedAvatarIndex = Mathf.Clamp(index, 0, uiComponent.AvatarStyleImages.Length - 1);
		Image selectedImage = uiComponent.AvatarStyleImages[selectedAvatarIndex];
		selectedAvatarSprite = selectedImage != null ? selectedImage.sprite : selectedAvatarSprite;
		selectedAvatarImagePath = string.Empty;

		RefreshPreview();
		RefreshSelectedMarks();
	}

	private void RefreshPreview()
	{
		if (uiComponent.PreviewAvatarImage != null && selectedAvatarSprite != null)
		{
			uiComponent.PreviewAvatarImage.sprite = selectedAvatarSprite;
			uiComponent.PreviewAvatarImage.preserveAspect = true;
		}
	}

	private void RefreshSelectedMarks()
	{
		if (uiComponent.SelectedMarks == null) return;
		for (int i = 0; i < uiComponent.SelectedMarks.Length; i++)
		{
			if (uiComponent.SelectedMarks[i] != null)
			{
				uiComponent.SelectedMarks[i].SetActive(i == selectedAvatarIndex && selectedAvatarIndex >= 0);
			}
		}
	}
}

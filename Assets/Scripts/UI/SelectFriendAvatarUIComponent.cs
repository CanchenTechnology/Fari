/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:2026/6/18
 *Description:好友头像选择界面组件绑定
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class SelectFriendAvatarUIComponent : MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button BackButton;
	public Image PreviewAvatarImage;
	public Button PhotoUploadButton;
	public Button AlbumSelectButton;
	public Button AIAvatarButton;
	public Button[] AvatarStyleButtons;
	public Image[] AvatarStyleImages;
	public GameObject[] SelectedMarks;
	public Button ConfirmButton;

	public void InitComponent(WindowBase target)
	{
		target.Canvas.sortingOrder = (int)windowLayer;
		target.Layer = windowLayer;
		SelectFriendAvatarUI mWindow = (SelectFriendAvatarUI)target;
		target.AddButtonClickListener(BackButton, mWindow.OnBackButtonClick);
		target.AddButtonClickListener(PhotoUploadButton, mWindow.OnPhotoUploadButtonClick);
		target.AddButtonClickListener(AlbumSelectButton, mWindow.OnAlbumSelectButtonClick);
		target.AddButtonClickListener(AIAvatarButton, mWindow.OnAIAvatarButtonClick);
		target.AddButtonClickListener(ConfirmButton, mWindow.OnConfirmButtonClick);

		if (AvatarStyleButtons == null) return;
		for (int i = 0; i < AvatarStyleButtons.Length; i++)
		{
			int index = i;
			target.AddButtonClickListener(AvatarStyleButtons[i], () => mWindow.OnAvatarStyleButtonClick(index));
		}
	}
}

/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/18/2026
 *Description:好友空状态界面组件绑定
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class NoFriendUIComponent : MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button AddFriendButton;
	public Button CreateFriendButton;

	public void InitComponent(WindowBase target)
	{
		target.Canvas.sortingOrder = (int)windowLayer;
		target.Layer = windowLayer;
		NoFriendUI mWindow = (NoFriendUI)target;
		target.AddButtonClickListener(AddFriendButton, mWindow.OnAddFriendButtonClick);
		target.AddButtonClickListener(CreateFriendButton, mWindow.OnCreateFriendButtonClick);
	}
}

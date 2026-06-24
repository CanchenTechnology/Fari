/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/11/2026 11:54:12 AM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using SuperScrollView;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class FriendUIComponent : MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;

	[Header("用户信息")]
	public Image headImage;
	public TMP_Text nameText;
	public TMP_Text stateText;

	[Header("交互")]
	public Button friendRequestBtn;
	public Button alreadyReceiveInviteBtn;

	public LoopListView2 friendListView;
	public Button searchBtn;
	public Button addExpandBtn;
	public GameObject addPanelGO;

	[Header("扩展添加好友")]
	public Button AddFriendButton;
	public Button CreateFriendButton;
	public Button settingBtn;
	public Button exitAddPanelBtn;

	[Header("删除好友")]
	public RectTransform deleteFriendRect;
	public TMP_Text deleteContent;
	public Button cancelBtn;
	public Button sureBtn;


	public void InitComponent(WindowBase target)
	{
		ResolveReferences();

		target.Canvas.sortingOrder = (int)windowLayer;
		target.Layer = windowLayer;
		FriendUI mWindow = (FriendUI)target;

		AddClick(target, friendRequestBtn, mWindow.OnFriendRequestButtonClick);
		AddClick(target, alreadyReceiveInviteBtn, mWindow.OnAlreadyReceiveInviteButtonClick);
		AddClick(target, searchBtn, mWindow.OnSearchButtonClick);
		AddClick(target, addExpandBtn, mWindow.OnAddExpandButtonClick);
		AddClick(target, AddFriendButton, mWindow.OnAddFriendButtonClick);
		AddClick(target, CreateFriendButton, mWindow.OnCreateFriendButtonClick);
		AddClick(target, settingBtn, mWindow.OnSettingButtonClick);
		AddClick(target, exitAddPanelBtn, mWindow.OnExitAddPanelButtonClick);
		AddClick(target, cancelBtn, mWindow.OnCancelDeleteFriendButtonClick);
		AddClick(target, sureBtn, mWindow.OnConfirmDeleteFriendButtonClick);
	}

	public void ResolveReferences()
	{
		if (headImage == null) headImage = FindImageByName("MeAvatar", "headImage", "HeadImage");
		if (nameText == null) nameText = FindTextByName("userName", "nameText", "UserName");
		if (stateText == null) stateText = FindTextByName("userState", "stateText", "StateText");

		if (friendRequestBtn == null) friendRequestBtn = FindButtonByName("FriendRequestBtn");
		if (alreadyReceiveInviteBtn == null) alreadyReceiveInviteBtn = FindButtonByName("InvitationReceivedBtn");
		if (searchBtn == null) searchBtn = FindButtonByName("[Button]search", "searchBtn", "SearchBtn");
		if (addExpandBtn == null) addExpandBtn = FindButtonByName("[Button]Add", "addExpandBtn", "AddExpandBtn");
		if (AddFriendButton == null) AddFriendButton = FindButtonByName("AddFriendBtn", "AddFriendButton");
		if (CreateFriendButton == null) CreateFriendButton = FindButtonByName("CreateVirtualFriendBtn", "CreateFriendButton");
		if (settingBtn == null) settingBtn = FindButtonByName("SettingBtn", "settingBtn");
		if (exitAddPanelBtn == null) exitAddPanelBtn = FindButtonByName("exitAddPanelBtn", "ExitAddPanelBtn");
		if (deleteFriendRect == null)
		{
			Transform deletePanel = FindTransformByName(transform, "DeleteFriendUI", "deleteFriendRect", "DeleteFriendPanel");
			if (deletePanel != null) deleteFriendRect = deletePanel as RectTransform;
		}
		if (deleteContent == null) deleteContent = FindTextByName("deleteInfo", "DeleteInfo", "deleteContent", "DeleteContent");
		if (cancelBtn == null) cancelBtn = FindButtonByName("cancelBtn", "CancelBtn", "[Button]Cancel");
		if (sureBtn == null) sureBtn = FindButtonByName("sureBtn", "SureBtn", "confirmBtn", "ConfirmBtn", "[Button]Sure", "[Button]Confirm");
		
		if (addPanelGO == null)
		{
			Transform panel = FindTransformByName(transform, "AddPanel");
			if (panel != null) addPanelGO = panel.gameObject;
		}

		if (friendListView == null)
		{
			friendListView = GetComponentInChildren<LoopListView2>(true);
		}

	}

	private void AddClick(WindowBase target, Button button, UnityEngine.Events.UnityAction action)
	{
		if (button == null || action == null) return;
		target.AddButtonClickListener(button, action);
	}

	private Button FindButtonByName(params string[] names)
	{
		foreach (Transform child in GetComponentsInChildren<Transform>(true))
		{
			if (NameMatches(child.name, names))
				return child.GetComponent<Button>();
		}
		return null;
	}

	private TMP_Text FindTextByName(params string[] names)
	{
		foreach (Transform child in GetComponentsInChildren<Transform>(true))
		{
			if (NameMatches(child.name, names))
				return child.GetComponent<TMP_Text>();
		}
		return null;
	}

	private Image FindImageByName(params string[] names)
	{
		foreach (Transform child in GetComponentsInChildren<Transform>(true))
		{
			if (NameMatches(child.name, names))
				return child.GetComponent<Image>();
		}
		return null;
	}

	private Transform FindTransformByName(Transform root, params string[] names)
	{
		if (root == null) return null;
		if (NameMatches(root.name, names)) return root;

		for (int i = 0; i < root.childCount; i++)
		{
			Transform result = FindTransformByName(root.GetChild(i), names);
			if (result != null) return result;
		}
		return null;
	}

	private bool NameMatches(string objectName, params string[] names)
	{
		if (string.IsNullOrEmpty(objectName) || names == null) return false;
		foreach (string name in names)
		{
			if (objectName == name) return true;
		}
		return false;
	}
}

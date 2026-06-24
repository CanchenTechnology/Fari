/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/24/2026 4:23:34 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using SuperScrollView;

public class FriendRequestUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button exitBtnButton;
	public TMP_Text emptyStateText;
	public RectTransform noFriendContentRoot;
	public RectTransform haveFriendContentRoot;

	public LoopListView2 friendLoopListView; 



	public void InitComponent(WindowBase target)
	{
	    ResolveReferences();
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    FriendRequestUI mWindow=(FriendRequestUI)target;
	    if (exitBtnButton != null)
	        target.AddButtonClickListener(exitBtnButton,mWindow.OnexitBtnButtonClick);
	}

	public void ResolveReferences()
	{
		if (exitBtnButton == null)
			exitBtnButton = FindButtonByName("[Button]exitBtn", "exitBtn", "ExitButton", "BackButton");
		if (emptyStateText == null)
			emptyStateText = FindTextByName("EmptyStateText", "[Text]EmptyState", "NoFriendRequestText");
		if (noFriendContentRoot == null)
		{
			Transform root = FindTransformByName(transform, "NoFriendContent", "NoRequestContent", "EmptyContent");
			if (root != null)
				noFriendContentRoot = root as RectTransform;
		}
		if (haveFriendContentRoot == null)
		{
			Transform root = FindTransformByName(transform, "HaveFriendContent", "FriendRequestScrollView", "FriendRequestList", "RequestListContent", "InviteListContent");
			if (root != null)
				haveFriendContentRoot = root as RectTransform;
		}
		if (friendLoopListView == null)
			friendLoopListView = FindLoopListViewByName("FriendRequestScrollView", "friendScrollView", "FriendScrollView", "friendLoopListView");
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

	private LoopListView2 FindLoopListViewByName(params string[] names)
	{
		foreach (Transform child in GetComponentsInChildren<Transform>(true))
		{
			if (NameMatches(child.name, names))
			{
				LoopListView2 listView = child.GetComponent<LoopListView2>();
				if (listView != null) return listView;
			}
		}

		return GetComponentInChildren<LoopListView2>(true);
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

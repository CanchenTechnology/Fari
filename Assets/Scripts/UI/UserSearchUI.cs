/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 11:59:17
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class UserSearchUI : WindowBase
{
	public UserSearchUIComponent uiComponent;
	private string currentSearchText = string.Empty;
	private readonly List<FirestoreManager.UserSearchResult> currentResults = new List<FirestoreManager.UserSearchResult>();
	private Text[] resultNameTexts;
	private Text[] resultHandleTexts;

	private readonly MockSearchUser[] mockUsers =
	{
		new MockSearchUser("Morgan", "@morgan", "真实好友 · 塔罗同好"),
		new MockSearchUser("Luna", "@luna", "真实好友 · 月亮能量"),
		new MockSearchUser("Iris Moon", "@iris", "真实好友 · 关系占卜")
	};

	private struct MockSearchUser
	{
		public string name;
		public string handle;
		public string info;

		public MockSearchUser(string name, string handle, string info)
		{
			this.name = name;
			this.handle = handle;
			this.info = info;
		}
	}

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<UserSearchUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		CacheResultTextRefs();
		UpdateResultViews();
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		currentSearchText = uiComponent.SearchInputInputField != null ? uiComponent.SearchInputInputField.text : string.Empty;
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region API Function

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnNotificationsButtonClick()
	{
		ToastManager.ShowDebug();
	}
	public void OnSearchInputInputChange(string text)
	{
		currentSearchText = text;
	}
	public void OnSearchInputInputEnd(string text)
	{
		currentSearchText = text;
	}
	public void OnSearchFriendsButtonClick()
	{
		string keyword = string.IsNullOrWhiteSpace(currentSearchText) ? "推荐好友" : currentSearchText.Trim();
		if (string.IsNullOrWhiteSpace(currentSearchText))
		{
			ToastManager.ShowToast("请输入用户名再搜索");
			return;
		}

		ToastManager.ShowToast($"正在搜索：{keyword}");
		FirestoreManager.Instance.SearchUsersByName(keyword, results =>
		{
			currentResults.Clear();
			bool hasSelfMatch = false;
			foreach (var result in results)
			{
				if (result.isSelf)
				{
					hasSelfMatch = true;
					continue;
				}

				currentResults.Add(result);
			}
			UpdateResultViews();

			if (currentResults.Count == 0)
			{
				ToastManager.ShowToast(hasSelfMatch ? "这是你自己，不能添加自己" : "没有找到匹配用户");
			}
			else
			{
				ToastManager.ShowToast($"找到 {currentResults.Count} 个用户");
			}
		});
	}
	public void OnInvite1ButtonClick()
	{
		InviteMockUser(0);
	}
	public void OnInvite2ButtonClick()
	{
		InviteMockUser(1);
	}
	public void OnInvite3ButtonClick()
	{
		InviteMockUser(2);
	}

	private void InviteMockUser(int index)
	{
		if (index < 0) return;

		if (index < currentResults.Count)
		{
			var user = currentResults[index];
			ToastManager.ShowToast($"正在发送给 {user.displayName}");
			FirestoreManager.Instance.SendFriendRequest(user, success =>
			{
				ToastManager.ShowToast(success ? $"已发送给 {user.displayName}" : "发送失败，请稍后再试");
			});
			return;
		}

		if (Application.isEditor && index < mockUsers.Length)
		{
			var user = mockUsers[index];
			FriendDataManager.Instance.AddRealFriend(user.name, user.handle, user.info, null, "用户名搜索");
			ToastManager.ShowToast($"Editor 模拟添加 {user.name}");
		}
	}

	private void CacheResultTextRefs()
	{
		resultNameTexts = new[]
		{
			FindTextByName("NameText1"),
			FindTextByName("NameText2"),
			FindTextByName("NameText3")
		};
		resultHandleTexts = new[]
		{
			FindTextByName("HandleText1"),
			FindTextByName("HandleText2"),
			FindTextByName("HandleText3")
		};
	}

	private Text FindTextByName(string targetName)
	{
		Text[] texts = gameObject.GetComponentsInChildren<Text>(true);
		foreach (Text text in texts)
		{
			if (text.transform.name == targetName)
			{
				return text;
			}
		}

		return null;
	}

	private void UpdateResultViews()
	{
		Button[] buttons =
		{
			uiComponent.Invite1Button,
			uiComponent.Invite2Button,
			uiComponent.Invite3Button
		};

		for (int i = 0; i < buttons.Length; i++)
		{
			bool hasResult = i < currentResults.Count;
			if (buttons[i] != null) buttons[i].interactable = hasResult;

			if (resultNameTexts != null && i < resultNameTexts.Length && resultNameTexts[i] != null)
			{
				resultNameTexts[i].text = hasResult ? currentResults[i].displayName : string.Empty;
			}

			if (resultHandleTexts != null && i < resultHandleTexts.Length && resultHandleTexts[i] != null)
			{
				resultHandleTexts[i].text = hasResult ? currentResults[i].Handle : string.Empty;
			}
		}
	}
	#endregion
}

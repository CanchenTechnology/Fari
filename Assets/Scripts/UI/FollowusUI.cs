/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 15:37:34
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class FollowusUI : WindowBase
{
	public FollowusUIComponent uiComponent;
	private SocialLinksConfig socialLinks = SocialLinksConfig.Default;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<FollowusUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		LoadSocialLinks();
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
	private void LoadSocialLinks()
	{
		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized) return;

			firestore.LoadPublicAppConfig(config =>
			{
				if (config?.socialLinks != null)
					socialLinks = MergeWithDefaults(config.socialLinks);
			});
		}

		private void OpenLink(string url, string label)
		{
			url = string.IsNullOrWhiteSpace(url) ? GetDefaultLink(label) : url.Trim();
			if (string.IsNullOrWhiteSpace(url))
			{
				ToastManager.ShowToast("链接不可用，请稍后再试");
				return;
			}

			Application.OpenURL(url);
			ToastManager.ShowToast($"正在打开 {label}");
		}

		private static SocialLinksConfig MergeWithDefaults(SocialLinksConfig config)
		{
			SocialLinksConfig defaults = SocialLinksConfig.Default;
			return new SocialLinksConfig
			{
				instagram = string.IsNullOrWhiteSpace(config.instagram) ? defaults.instagram : config.instagram,
				facebook = string.IsNullOrWhiteSpace(config.facebook) ? defaults.facebook : config.facebook,
				x = string.IsNullOrWhiteSpace(config.x) ? defaults.x : config.x,
				tiktok = string.IsNullOrWhiteSpace(config.tiktok) ? defaults.tiktok : config.tiktok,
				pinterest = string.IsNullOrWhiteSpace(config.pinterest) ? defaults.pinterest : config.pinterest,
			};
		}

		private static string GetDefaultLink(string label)
		{
			SocialLinksConfig defaults = SocialLinksConfig.Default;
			return label switch
			{
				"Instagram" => defaults.instagram,
				"Facebook" => defaults.facebook,
				"X" => defaults.x,
				"TikTok" => defaults.tiktok,
				"Pinterest" => defaults.pinterest,
				_ => string.Empty,
			};
		}

		#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnOpenLink_InstaButtonClick()
	{
		OpenLink(socialLinks.instagram, "Instagram");
	}
	public void OnOpenLink_FBButtonClick()
	{
		OpenLink(socialLinks.facebook, "Facebook");
	}
	public void OnOpenLink_TwitterButtonClick()
	{
		OpenLink(socialLinks.x, "X");
	}
	public void OnOpenLink_TikTokButtonClick()
	{
		OpenLink(socialLinks.tiktok, "TikTok");
	}
	public void OnOpenLink_PinterestButtonClick()
	{
		OpenLink(socialLinks.pinterest, "Pinterest");
	}
	#endregion
}

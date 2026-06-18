/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/11/2026 2:21:05 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class UnlockProUI : WindowBase
{
	public UnlockProUIComponent uiComponent;
	private IapProductsConfig iapProducts = IapProductsConfig.Default;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<UnlockProUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		LoadIapProducts();
		RefreshMembershipStatus();
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
	public void OnBackButtonButtonClick()
	{
		HideWindow();
		UIModule.Instance.PopUpWindow<MyUI>();
	}
	public void OnManageSubscriptionBtnButtonClick()
	{
		Debug.Log($"[UnlockProUI] 打开订阅管理。IAP: {iapProducts.proMonthly?.productId}, {iapProducts.proYearly?.productId}");
		GetIapManager().OpenSubscriptionManagement();
		ToastManager.ShowToast("已打开订阅管理");
	}
	public void OnRestorePurchaseBtnButtonClick()
	{
		RefreshMembershipStatus(true);
	}

	private void RefreshMembershipStatus(bool showToast = false)
	{
		var client = BackendMembershipClient.Instance;
		if (client == null)
		{
			var go = new GameObject("BackendMembershipClient");
			client = go.AddComponent<BackendMembershipClient>();
		}

		if (uiComponent.ValidityValueText != null)
			uiComponent.ValidityValueText.text = "检查中...";

		client.GetMembershipStatus(
			(status) =>
			{
				if (status == null)
				{
					if (uiComponent.ValidityValueText != null)
						uiComponent.ValidityValueText.text = "Free";
					return;
				}

				if (uiComponent.ValidityLabelText != null)
					uiComponent.ValidityLabelText.text = status.isPro ? "Pro 有效期" : "当前状态";

				if (uiComponent.ValidityValueText != null)
					uiComponent.ValidityValueText.text = status.isPro
						? (string.IsNullOrEmpty(status.proExpiresAt) ? "Pro" : status.proExpiresAt)
						: "Free";

				if (showToast)
					ToastManager.ShowToast(status.isPro ? "已恢复 Pro 状态" : "当前没有有效订阅");
			},
			(error) =>
			{
				if (uiComponent.ValidityValueText != null)
					uiComponent.ValidityValueText.text = "检查失败";
				if (showToast)
					ToastManager.ShowToast("恢复失败：" + error);
				Debug.LogWarning("[UnlockProUI] 会员状态检查失败: " + error);
			},
			forceRefresh: showToast);
	}

	private void LoadIapProducts()
	{
		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized) return;

		firestore.LoadPublicAppConfig(config =>
		{
			if (config?.iapProducts != null)
				iapProducts = config.iapProducts;
		});
	}

	public void OnPurchaseMonthlyButtonClick()
	{
		StartPurchase(iapProducts.proMonthly);
	}

	public void OnPurchaseYearlyButtonClick()
	{
		StartPurchase(iapProducts.proYearly);
	}

	private void StartPurchase(IapProductConfig product)
	{
		GetIapManager().PurchaseSubscription(product, (success, message) =>
		{
			ToastManager.ShowToast(success ? "购买处理中" : message);
		});
	}

	private IapPurchaseManager GetIapManager()
	{
		var manager = IapPurchaseManager.Instance;
		if (manager != null) return manager;

		var go = new GameObject("IapPurchaseManager");
		return go.AddComponent<IapPurchaseManager>();
	}
	#endregion
}

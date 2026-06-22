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
	private Text monthlyPurchaseText;
	private Text yearlyPurchaseText;
	private Text purchaseHintText;
	private Text pageTitleText;
	private Text proStatusTitleText;
	private Text proStatusDescText;
	private Text currentPlanLabelText;
	private Text currentPlanValueText;
	private readonly Text[] featureTitleTexts = new Text[4];
	private readonly Text[] featureDescTexts = new Text[4];
	private bool isCurrentPro;
	private bool isRefreshingStatus;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<UnlockProUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
		BindPrefabTexts();
	}

	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		BindPrefabTexts();
		LoadIapProducts();
		RefreshMembershipStatus();
		RefreshStaticCopy();
		RefreshPurchaseControls();
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
	private void LoadIapProducts()
	{
		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			GetIapManager().ConfigureProducts(iapProducts);
			RefreshPurchaseControls();
			return;
		}

		firestore.LoadPublicAppConfig(config =>
		{
			if (config?.iapProducts != null)
				iapProducts = config.iapProducts;

			GetIapManager().ConfigureProducts(iapProducts);
			RefreshPurchaseControls();
		});
	}

	private void RefreshMembershipStatus(bool showToast = false)
	{
		var client = BackendMembershipClient.Instance;
		if (client == null)
		{
			var go = new GameObject("BackendMembershipClient");
			client = go.AddComponent<BackendMembershipClient>();
		}

		isRefreshingStatus = true;
		SetText(currentPlanValueText, "检查中...");
		RefreshPurchaseControls();

		client.GetMembershipStatus(
			status =>
			{
				isRefreshingStatus = false;
				isCurrentPro = status != null && status.isPro;

				RefreshStaticCopy(status);
				RefreshPurchaseControls();

				if (showToast)
					ToastManager.ShowToast(isCurrentPro ? "已恢复 Pro 状态" : "当前没有有效订阅");
			},
			error =>
			{
				isRefreshingStatus = false;
				isCurrentPro = false;

				RefreshStaticCopy();
				RefreshPurchaseControls();
				SetText(currentPlanValueText, "检查失败");

				if (showToast)
					ToastManager.ShowToast("恢复失败：" + error);
				Debug.LogWarning("[UnlockProUI] 会员状态检查失败: " + error);
			},
			forceRefresh: showToast);
	}

	private void StartPurchase(IapProductConfig product)
	{
		if (isCurrentPro)
		{
			ToastManager.ShowToast("你已经是 Pro，可在订阅管理中调整方案。");
			return;
		}

		if (product == null || string.IsNullOrEmpty(product.productId))
		{
			ToastManager.ShowToast("IAP 商品未配置");
			return;
		}

		var manager = GetIapManager();
		manager.ConfigureProducts(iapProducts);
		SetPurchaseButtonsInteractable(false);
		SetPurchaseHint("正在连接商店...");

		manager.PurchaseSubscription(product, (success, message) =>
		{
			SetPurchaseButtonsInteractable(!isCurrentPro);

			if (success)
			{
				ToastManager.ShowToast(string.IsNullOrEmpty(message) ? "购买处理中，请稍后查看 Pro 状态" : message);
				RefreshMembershipStatus(true);
				return;
			}

			ToastManager.ShowToast(string.IsNullOrEmpty(message) ? "购买失败，请稍后重试" : message);
			if (!manager.IsUnityIapAvailable)
				SetPurchaseHint("当前未安装 Unity IAP 包，暂不能发起真实购买。", true);
			else
				RefreshPurchaseControls();
		});
	}

	private IapPurchaseManager GetIapManager()
	{
		var manager = IapPurchaseManager.Instance;
		if (manager != null)
		{
			manager.ConfigureProducts(iapProducts);
			return manager;
		}

		var go = new GameObject("IapPurchaseManager");
		manager = go.AddComponent<IapPurchaseManager>();
		manager.ConfigureProducts(iapProducts);
		return manager;
	}

	private void BindPrefabTexts()
	{
		monthlyPurchaseText = GetButtonLabel(uiComponent?.RuntimeMonthlyPurchaseBtnButton, monthlyPurchaseText);
		yearlyPurchaseText = GetButtonLabel(uiComponent?.RuntimeYearlyPurchaseBtnButton, yearlyPurchaseText);
		purchaseHintText = purchaseHintText != null ? purchaseHintText : FindTextByObjectName("RuntimePurchaseHint");
		pageTitleText = pageTitleText != null ? pageTitleText : FindTextByObjectName("TitleText");
		proStatusTitleText = proStatusTitleText != null ? proStatusTitleText : FindTextByObjectName("ProStatusTitle");
		proStatusDescText = proStatusDescText != null ? proStatusDescText : FindTextByObjectName("ProStatusDesc");
		currentPlanLabelText = currentPlanLabelText != null ? currentPlanLabelText : FindTextByObjectName("CurrentPlanLabel");
		currentPlanValueText = uiComponent?.CurrentPlanValueText != null
			? uiComponent.CurrentPlanValueText
			: currentPlanValueText != null
			? currentPlanValueText
			: FindTextByObjectName("CurrentPlanValue");

		for (int i = 0; i < featureTitleTexts.Length; i++)
		{
			int index = i + 1;
			featureTitleTexts[i] = featureTitleTexts[i] != null ? featureTitleTexts[i] : FindTextByObjectName($"Title{index}");
			featureDescTexts[i] = featureDescTexts[i] != null ? featureDescTexts[i] : FindTextByObjectName($"Desc{index}");
		}
	}

	private Text GetButtonLabel(Button button, Text cached)
	{
		if (cached != null) return cached;
		return button != null ? button.GetComponentInChildren<Text>(true) : null;
	}

	private Text FindTextByObjectName(string objectName)
	{
		if (string.IsNullOrEmpty(objectName))
			return null;

		Text[] texts = gameObject.GetComponentsInChildren<Text>(true);
		foreach (Text text in texts)
		{
			if (text == null) continue;
			string name = text.gameObject.name;
			if (name == objectName || name == "[Text]" + objectName)
				return text;
		}

		return null;
	}

	private void RefreshStaticCopy(MembershipStatusResponse status = null)
	{
		BindPrefabTexts();

		SetText(pageTitleText, isCurrentPro ? "Pro 已开通" : "解锁所有功能");
		SetText(proStatusTitleText, isCurrentPro ? "PRO 已开通" : "升级到 PRO");
		SetText(proStatusDescText, isCurrentPro
			? "你已解锁完整占卜体验。"
			: "解锁更多占卜额度、深度记录和神谕师体验。");
		SetText(currentPlanLabelText, "当前方案");

		if (!isRefreshingStatus)
			SetText(currentPlanValueText, BuildCurrentPlanText(status));

		SetFeatureText(0, "占卜额度", isCurrentPro
			? "每日牌与牌阵可持续使用"
			: $"免费：每日牌 {UsageStatsManager.FreeDailyOracleLimit}/日，牌阵 {UsageStatsManager.FreeReadingLimit}/日");
		SetFeatureText(1, "今日对话", isCurrentPro
			? "Pro 对话额度 300 句/日"
			: $"免费：{UsageStatsManager.FreeDialogLimit} 句/日");
		SetFeatureText(2, "神谕师角色", "切换塔罗师、冥想师和占星师");
		SetFeatureText(3, "记忆与历史", "完整查看历史、记忆与回访线索");
	}

	private string BuildCurrentPlanText(MembershipStatusResponse status)
	{
		if (!isCurrentPro)
			return "Free";

		if (status != null && !string.IsNullOrEmpty(status.proExpiresAt))
			return "Pro";

		return "Pro";
	}

	private void RefreshPurchaseControls()
	{
		RefreshStaticCopy();

		if (monthlyPurchaseText != null)
			monthlyPurchaseText.text = isCurrentPro ? "已开通 Pro" : BuildProductButtonText(iapProducts.proMonthly, "Moonly Pro Monthly");
		if (yearlyPurchaseText != null)
			yearlyPurchaseText.text = isCurrentPro ? "可在订阅管理中调整方案" : BuildProductButtonText(iapProducts.proYearly, "Moonly Pro Yearly");

		SetPurchaseButtonsInteractable(!isCurrentPro && !isRefreshingStatus);

		if (uiComponent?.RestorePurchaseBtnButton != null)
			uiComponent.RestorePurchaseBtnButton.interactable = !isRefreshingStatus;
		if (uiComponent?.ManageSubscriptionBtnButton != null)
			uiComponent.ManageSubscriptionBtnButton.interactable = true;

		var manager = GetIapManager();
		SetPurchaseHint(isCurrentPro
			? "当前账号已是 Pro。续订、取消或更换方案请使用订阅管理。"
			: manager.IsUnityIapAvailable
			? "购买完成后会自动刷新 Pro 状态，也可以手动恢复购买。"
			: "当前未安装 Unity IAP 包，购买按钮会显示降级提示。");
	}

	private void SetPurchaseButtonsInteractable(bool interactable)
	{
		if (uiComponent?.RuntimeMonthlyPurchaseBtnButton != null)
			uiComponent.RuntimeMonthlyPurchaseBtnButton.interactable = interactable;
		if (uiComponent?.RuntimeYearlyPurchaseBtnButton != null)
			uiComponent.RuntimeYearlyPurchaseBtnButton.interactable = interactable;
	}

	private static string BuildProductButtonText(IapProductConfig product, string fallbackName)
	{
		string name = string.IsNullOrWhiteSpace(product?.displayName) ? fallbackName : product.displayName;
		string price = product?.priceLabel;
		return string.IsNullOrWhiteSpace(price) ? name : $"{name} · {price}";
	}

	private void SetPurchaseHint(string text, bool warning = false)
	{
		if (purchaseHintText == null) return;
		purchaseHintText.text = text;
		purchaseHintText.color = warning
			? new Color(1f, 0.73f, 0.36f, 1f)
			: new Color(0.86f, 0.82f, 0.92f, 1f);
	}

	private void SetFeatureText(int index, string title, string desc)
	{
		if (index < 0 || index >= featureTitleTexts.Length)
			return;

		SetText(featureTitleTexts[index], title);
		SetText(featureDescTexts[index], desc);
	}

	private static void SetText(Text target, string value)
	{
		if (target == null)
			return;

		target.text = value ?? "";
		target.resizeTextForBestFit = true;
		target.resizeTextMinSize = Mathf.Clamp(target.resizeTextMinSize <= 0 ? 12 : target.resizeTextMinSize, 10, 18);
		target.resizeTextMaxSize = Mathf.Max(target.fontSize, target.resizeTextMaxSize);
	}
	#endregion

	#region UI组件事件
	public void OnRuntimeYearlyPurchaseBtnButtonClick()
	{
		StartPurchase(iapProducts.proYearly);
	}

	public void OnRuntimeMonthlyPurchaseBtnButtonClick()
	{
		StartPurchase(iapProducts.proMonthly);
	}

	public void OnBackButtonButtonClick()
	{
		HideWindow();
	}

	public void OnManageSubscriptionBtnButtonClick()
	{
		Debug.Log($"[UnlockProUI] 打开订阅管理。IAP: {iapProducts.proMonthly?.productId}, {iapProducts.proYearly?.productId}");
		GetIapManager().OpenSubscriptionManagement();
		ToastManager.ShowToast("已打开订阅管理");
	}

	public void OnRestorePurchaseBtnButtonClick()
	{
		var manager = GetIapManager();
		SetPurchaseButtonsInteractable(false);
		if (uiComponent?.RestorePurchaseBtnButton != null)
			uiComponent.RestorePurchaseBtnButton.interactable = false;
		SetPurchaseHint("正在恢复购买...");

		manager.RestorePurchases(iapProducts, (success, message) =>
		{
			if (!string.IsNullOrEmpty(message))
				ToastManager.ShowToast(message);

			if (!success && !manager.IsUnityIapAvailable)
				SetPurchaseHint("当前未安装 Unity IAP 包，暂不能从商店恢复购买。", true);

			RefreshMembershipStatus(true);
		});
	}
	#endregion
}

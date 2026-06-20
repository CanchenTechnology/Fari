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
	private Button monthlyPurchaseButton;
	private Button yearlyPurchaseButton;
	private Text monthlyPurchaseText;
	private Text yearlyPurchaseText;
	private Text purchaseHintText;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<UnlockProUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
		EnsureRuntimePurchaseControls();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		EnsureRuntimePurchaseControls();
		LoadIapProducts();
		RefreshMembershipStatus();
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
		var manager = GetIapManager();
		manager.RestorePurchases(iapProducts, (success, message) =>
		{
			if (!string.IsNullOrEmpty(message))
				ToastManager.ShowToast(message);
			RefreshMembershipStatus(true);
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
		if (firestore == null || !firestore.IsInitialized)
		{
			RefreshPurchaseControls();
			return;
		}

		firestore.LoadPublicAppConfig(config =>
		{
			if (config?.iapProducts != null)
				iapProducts = config.iapProducts;
			RefreshPurchaseControls();
			GetIapManager().ConfigureProducts(iapProducts);
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
		var manager = GetIapManager();
		manager.ConfigureProducts(iapProducts);
		manager.PurchaseSubscription(product, (success, message) =>
		{
			if (success)
			{
				ToastManager.ShowToast("购买处理中，请稍后查看 Pro 状态");
				RefreshMembershipStatus(true);
				return;
			}

			ToastManager.ShowToast(message);
			if (!manager.IsUnityIapAvailable)
				SetPurchaseHint("当前未安装 Unity IAP 包，暂不能发起真实购买。你仍可以恢复或管理已有订阅。", true);
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

	private void EnsureRuntimePurchaseControls()
	{
		if (monthlyPurchaseButton != null && yearlyPurchaseButton != null)
			return;

		Transform parent = uiComponent.RestorePurchaseBtnButton != null
			? uiComponent.RestorePurchaseBtnButton.transform.parent
			: transform;
		RectTransform restoreRect = uiComponent.RestorePurchaseBtnButton != null
			? uiComponent.RestorePurchaseBtnButton.GetComponent<RectTransform>()
			: null;
		RectTransform manageRect = uiComponent.ManageSubscriptionBtnButton != null
			? uiComponent.ManageSubscriptionBtnButton.GetComponent<RectTransform>()
			: restoreRect;

		monthlyPurchaseButton = CreateRuntimeButton(parent, "[Button]RuntimeMonthlyPurchaseBtn", new Color(0.96f, 0.72f, 0.18f, 1f), out monthlyPurchaseText);
		yearlyPurchaseButton = CreateRuntimeButton(parent, "[Button]RuntimeYearlyPurchaseBtn", new Color(0.44f, 0.12f, 0.66f, 1f), out yearlyPurchaseText);
		monthlyPurchaseButton.onClick.AddListener(OnPurchaseMonthlyButtonClick);
		yearlyPurchaseButton.onClick.AddListener(OnPurchaseYearlyButtonClick);

		Vector2 baseSize = restoreRect != null ? restoreRect.sizeDelta : new Vector2(864f, 86f);
		float rowGap = Mathf.Max(18f, baseSize.y * 0.24f);
		float startY = manageRect != null ? manageRect.anchoredPosition.y : 250f;
		PlaceRuntimeRect(monthlyPurchaseButton.GetComponent<RectTransform>(), baseSize, new Vector2(0f, startY + (baseSize.y + rowGap) * 2f));
		PlaceRuntimeRect(yearlyPurchaseButton.GetComponent<RectTransform>(), baseSize, new Vector2(0f, startY + baseSize.y + rowGap));

		purchaseHintText = CreateRuntimeText(parent, "RuntimePurchaseHint", 20, FontStyle.Normal, new Color(0.86f, 0.82f, 0.92f, 1f));
		RectTransform hintRect = purchaseHintText.GetComponent<RectTransform>();
		Vector2 hintBase = restoreRect != null ? restoreRect.anchoredPosition : new Vector2(0f, 70f);
		PlaceRuntimeRect(hintRect, new Vector2(baseSize.x, 44f), new Vector2(0f, hintBase.y - baseSize.y * 0.85f));
		purchaseHintText.alignment = TextAnchor.MiddleCenter;

		RefreshPurchaseControls();
	}

	private Button CreateRuntimeButton(Transform parent, string name, Color color, out Text label)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
		go.layer = parent.gameObject.layer;
		go.transform.SetParent(parent, false);

		var image = go.GetComponent<Image>();
		image.color = color;

		var button = go.GetComponent<Button>();
		button.targetGraphic = image;

		label = CreateRuntimeText(go.transform, "Text", 26, FontStyle.Bold, Color.white);
		RectTransform labelRect = label.GetComponent<RectTransform>();
		labelRect.anchorMin = Vector2.zero;
		labelRect.anchorMax = Vector2.one;
		labelRect.offsetMin = new Vector2(24f, 0f);
		labelRect.offsetMax = new Vector2(-24f, 0f);
		label.alignment = TextAnchor.MiddleCenter;
		label.resizeTextForBestFit = true;
		label.resizeTextMinSize = 18;
		label.resizeTextMaxSize = 28;

		return button;
	}

	private Text CreateRuntimeText(Transform parent, string name, int fontSize, FontStyle fontStyle, Color color)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
		go.layer = parent.gameObject.layer;
		go.transform.SetParent(parent, false);

		var text = go.GetComponent<Text>();
		text.font = GetRuntimeFont();
		text.fontSize = fontSize;
		text.fontStyle = fontStyle;
		text.color = color;
		text.raycastTarget = false;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Truncate;
		return text;
	}

	private Font GetRuntimeFont()
	{
		if (uiComponent != null)
		{
			if (uiComponent.ValidityValueText != null && uiComponent.ValidityValueText.font != null)
				return uiComponent.ValidityValueText.font;
			if (uiComponent.ValidityLabelText != null && uiComponent.ValidityLabelText.font != null)
				return uiComponent.ValidityLabelText.font;
		}

		return Resources.GetBuiltinResource<Font>("Arial.ttf");
	}

	private static void PlaceRuntimeRect(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
	{
		if (rect == null) return;
		rect.anchorMin = new Vector2(0.5f, 0f);
		rect.anchorMax = new Vector2(0.5f, 0f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = size;
		rect.anchoredPosition = anchoredPosition;
	}

	private void RefreshPurchaseControls()
	{
		if (monthlyPurchaseText != null)
			monthlyPurchaseText.text = BuildProductButtonText(iapProducts.proMonthly, "月度 Pro");
		if (yearlyPurchaseText != null)
			yearlyPurchaseText.text = BuildProductButtonText(iapProducts.proYearly, "年度 Pro");

		var manager = GetIapManager();
		SetPurchaseHint(manager.IsUnityIapAvailable
			? "购买完成后会自动刷新 Pro 状态，也可以手动恢复购买。"
			: "当前未安装 Unity IAP 包，购买按钮会显示降级提示。");
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
	#endregion
}

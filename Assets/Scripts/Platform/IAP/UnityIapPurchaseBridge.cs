#if UNITY_PURCHASING
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using Unity.Services.Core;

/// <summary>
/// Unity IAP 购买桥。仅在安装 Unity Purchasing 包后参与编译。
/// </summary>
public class UnityIapPurchaseBridge : MonoBehaviour, IDetailedStoreListener
{
    private IStoreController storeController;
    private IExtensionProvider extensionProvider;
    private IapPurchaseManager purchaseManager;
    private Action<bool, string> purchaseCallback;
    private IapProductConfig pendingProduct;
    private bool isInitializing;
    private bool pendingRestore;
    private bool isRestoringTransactions;
    private int restoreReceiptSubmissionsPending;
    private int restoreReceiptSubmissionsSucceeded;
    private string restoreReceiptSubmissionError;
    private readonly Dictionary<string, IapProductConfig> productsById = new Dictionary<string, IapProductConfig>();

    public bool IsReady => storeController != null && extensionProvider != null;

    public void Purchase(
        IapProductConfig product,
        IapProductsConfig productConfig,
        IapPurchaseManager manager,
        Action<bool, string> onComplete)
    {
        if (product == null || string.IsNullOrEmpty(product.productId))
        {
            onComplete?.Invoke(false, "IAP 商品未配置");
            return;
        }

        purchaseManager = manager;
        purchaseCallback = onComplete;
        pendingProduct = product;
        RegisterProducts(product, productConfig);

        if (!IsReady)
        {
            InitializeStore();
            return;
        }

        InitiatePurchase(product);
    }

    public void RestorePurchases(
        IapProductsConfig productConfig,
        IapPurchaseManager manager,
        Action<bool, string> onComplete)
    {
        purchaseManager = manager;
        purchaseCallback = onComplete;
        pendingRestore = true;
        RegisterProducts(null, productConfig);

        if (!IsReady)
        {
            InitializeStore();
            return;
        }

        pendingRestore = false;
        BeginStoreRestoreTransactions();
    }

    private async void InitializeStore()
    {
        if (isInitializing)
            return;

        isInitializing = true;

        bool servicesReady = await EnsureUnityServicesInitialized();
        if (!servicesReady)
        {
            isInitializing = false;
            CompletePurchase(false, "Unity Gaming Services 初始化失败，请确认 Unity Project ID 已关联并重试。");
            return;
        }

        try
        {
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            foreach (var entry in productsById)
            {
                builder.AddProduct(entry.Key, ProductType.Subscription);
            }

            UnityPurchasing.Initialize(this, builder);
        }
        catch (Exception e)
        {
            isInitializing = false;
            Debug.LogError("[UnityIapPurchaseBridge] IAP 初始化启动失败: " + e.Message);
            CompletePurchase(false, "IAP 初始化启动失败：" + e.Message);
        }
    }

    private static async System.Threading.Tasks.Task<bool> EnsureUnityServicesInitialized()
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Initialized)
                return true;

            await UnityServices.InitializeAsync();
            return UnityServices.State == ServicesInitializationState.Initialized;
        }
        catch (Exception e)
        {
            Debug.LogError("[UnityIapPurchaseBridge] Unity Gaming Services 初始化失败: " + e.Message);
            return false;
        }
    }

    private void RegisterProducts(IapProductConfig requestedProduct, IapProductsConfig productConfig)
    {
        AddProduct(requestedProduct);
        AddProduct(productConfig?.proMonthly);
        AddProduct(productConfig?.proYearly);
        AddProduct(IapProductConfig.MonthlyDefault);
        AddProduct(IapProductConfig.YearlyDefault);
    }

    private void AddProduct(IapProductConfig product)
    {
        if (product == null || string.IsNullOrEmpty(product.productId))
            return;

        productsById[product.productId] = product;
    }

    private void InitiatePurchase(IapProductConfig product)
    {
        Product storeProduct = storeController.products.WithID(product.productId);
        if (storeProduct == null)
        {
            LogStoreProductDiagnostics(product.productId, "商店未返回商品");
            CompletePurchase(false, $"商店没有返回商品：{product.productId}。请检查商店后台商品 ID、包名和测试账号。");
            return;
        }

        if (!storeProduct.availableToPurchase)
        {
            LogStoreProductDiagnostics(product.productId, "商品暂不可购买");
            CompletePurchase(false, $"商品暂不可购买：{product.productId}。请确认商品已创建、可销售，并使用沙盒/测试账号。");
            return;
        }

        storeController.InitiatePurchase(storeProduct);
    }

    private void BeginStoreRestoreTransactions()
    {
        if (!IsReady)
        {
            CompletePurchase(false, "IAP 商品列表未就绪");
            return;
        }

        if (IsAppleRestorePlatform())
        {
            StartAppleRestoreTransactions();
            return;
        }

        if (IsGooglePlayRestorePlatform())
        {
            StartGooglePlayRestoreTransactions();
            return;
        }

        SubmitOwnedReceipts();
    }

    private void StartAppleRestoreTransactions()
    {
        try
        {
            isRestoringTransactions = true;
            extensionProvider.GetExtension<IAppleExtensions>()
                .RestoreTransactions((success, message) => OnStoreRestoreTransactionsFinished("Apple", success, message));
        }
        catch (Exception e)
        {
            isRestoringTransactions = false;
            CompletePurchase(false, $"Apple 恢复购买启动失败：{e.Message}");
        }
    }

    private void StartGooglePlayRestoreTransactions()
    {
        try
        {
            isRestoringTransactions = true;
            extensionProvider.GetExtension<IGooglePlayStoreExtensions>()
                .RestoreTransactions((success, message) => OnStoreRestoreTransactionsFinished("Google Play", success, message));
        }
        catch (Exception e)
        {
            isRestoringTransactions = false;
            CompletePurchase(false, $"Google Play 恢复购买启动失败：{e.Message}");
        }
    }

    private void OnStoreRestoreTransactionsFinished(string storeName, bool success, string message)
    {
        isRestoringTransactions = false;
        if (!success)
        {
            CompletePurchase(false, string.IsNullOrWhiteSpace(message) ? $"{storeName} 没有返回可恢复的购买" : message);
            return;
        }

        SubmitOwnedReceipts();
    }

    private static bool IsAppleRestorePlatform()
    {
        return Application.platform == RuntimePlatform.IPhonePlayer
            || Application.platform == RuntimePlatform.OSXPlayer
            || Application.platform == RuntimePlatform.tvOS
#if UNITY_VISIONOS
            || Application.platform == RuntimePlatform.VisionOS
#endif
            ;
    }

    private static bool IsGooglePlayRestorePlatform()
    {
        return Application.platform == RuntimePlatform.Android
            && StandardPurchasingModule.Instance().appStore == AppStore.GooglePlay;
    }

    private void SubmitOwnedReceipts()
    {
        if (storeController?.products?.all == null)
        {
            CompletePurchase(false, "IAP 商品列表未就绪");
            return;
        }

        int submitted = 0;
        restoreReceiptSubmissionsPending = 0;
        restoreReceiptSubmissionsSucceeded = 0;
        restoreReceiptSubmissionError = "";
        foreach (Product product in storeController.products.all)
        {
            if (product == null || !product.hasReceipt || string.IsNullOrEmpty(product.receipt))
                continue;

            submitted++;
            restoreReceiptSubmissionsPending++;
            SubmitReceipt(product, OnRestoreReceiptSubmitted);
        }

        if (submitted == 0)
            CompletePurchase(false, "没有找到可恢复的订阅");
    }

    private void OnRestoreReceiptSubmitted(bool success, string message)
    {
        if (success)
        {
            restoreReceiptSubmissionsSucceeded++;
        }
        else if (string.IsNullOrWhiteSpace(restoreReceiptSubmissionError))
        {
            restoreReceiptSubmissionError = string.IsNullOrWhiteSpace(message)
                ? "可恢复订阅凭证校验失败"
                : message;
        }

        restoreReceiptSubmissionsPending--;
        if (restoreReceiptSubmissionsPending > 0)
            return;

        if (restoreReceiptSubmissionsSucceeded > 0)
        {
            CompletePurchase(true, "已提交可恢复订阅，等待会员状态刷新");
            return;
        }

        CompletePurchase(false, string.IsNullOrWhiteSpace(restoreReceiptSubmissionError)
            ? "恢复购买失败，未能校验订阅凭证"
            : restoreReceiptSubmissionError);
    }

    private void SubmitReceipt(Product product, Action<bool, string> callback)
    {
        IapProductConfig config = productsById.TryGetValue(product.definition.id, out var existing)
            ? existing
            : new IapProductConfig
            {
                productId = product.definition.id,
                type = "subscription",
                store = Application.platform.ToString(),
                displayName = product.metadata?.localizedTitle ?? product.definition.id,
                priceLabel = product.metadata?.localizedPriceString ?? string.Empty,
            };

        purchaseManager.SubmitPurchaseReceipt(
            config,
            product.receipt,
            product.transactionID,
            Application.identifier,
            callback);
    }

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        isInitializing = false;
        storeController = controller;
        extensionProvider = extensions;

        if (pendingRestore)
        {
            pendingRestore = false;
            BeginStoreRestoreTransactions();
            return;
        }

        if (pendingProduct != null)
        {
            var product = pendingProduct;
            pendingProduct = null;
            InitiatePurchase(product);
        }
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        isInitializing = false;
        LogStoreProductDiagnostics(pendingProduct?.productId, $"IAP 初始化失败：{error}");
        CompletePurchase(false, BuildInitializeFailureMessage(error, string.Empty));
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        isInitializing = false;
        LogStoreProductDiagnostics(pendingProduct?.productId, $"IAP 初始化失败：{error} {message}");
        CompletePurchase(false, BuildInitializeFailureMessage(error, message));
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        Product product = args.purchasedProduct;
        if (product == null || string.IsNullOrEmpty(product.receipt))
        {
            CompletePurchase(false, "购买完成但没有收到商店凭证");
            return PurchaseProcessingResult.Complete;
        }

        if (isRestoringTransactions)
            return PurchaseProcessingResult.Complete;

        SubmitReceipt(product, (success, message) =>
        {
            CompletePurchase(success, message);
        });
        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        string productId = product?.definition?.id ?? "";
        CompletePurchase(false, $"购买失败：{productId} {failureReason}");
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
        string productId = product?.definition?.id ?? failureDescription?.productId ?? "";
        string reason = failureDescription != null
            ? $"{failureDescription.reason} {failureDescription.message}"
            : "Unknown";
        CompletePurchase(false, $"购买失败：{productId} {reason}");
    }

    private void CompletePurchase(bool success, string message)
    {
        var callback = purchaseCallback;
        purchaseCallback = null;
        pendingProduct = null;
        pendingRestore = false;
        isRestoringTransactions = false;
        restoreReceiptSubmissionsPending = 0;
        restoreReceiptSubmissionsSucceeded = 0;
        restoreReceiptSubmissionError = "";
        callback?.Invoke(success, message);
    }

    private static string BuildInitializeFailureMessage(InitializationFailureReason error, string detail)
    {
        string baseMessage = error == InitializationFailureReason.NoProductsAvailable
            ? "商店没有返回任何商品。请检查商品 ID、Bundle ID/包名、商品可销售状态和沙盒/测试账号。"
            : $"IAP 初始化失败：{error}";

        return string.IsNullOrWhiteSpace(detail)
            ? baseMessage
            : $"{baseMessage} {detail}";
    }

    private void LogStoreProductDiagnostics(string requestedProductId, string reason)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("[UnityIapPurchaseBridge] ").Append(reason);
        builder.Append(" | requested=").Append(string.IsNullOrEmpty(requestedProductId) ? "(none)" : requestedProductId);
        builder.Append(" | appId=").Append(Application.identifier);
        builder.Append(" | platform=").Append(Application.platform);
        builder.Append(" | registered=").Append(BuildRegisteredProductsSummary());
        builder.Append(" | storeProducts=").Append(BuildStoreProductsSummary());
        Debug.LogWarning(builder.ToString());
    }

    private string BuildRegisteredProductsSummary()
    {
        if (productsById.Count == 0)
            return "(none)";

        return string.Join(",", productsById.Keys);
    }

    private string BuildStoreProductsSummary()
    {
        if (storeController?.products?.all == null || storeController.products.all.Length == 0)
            return "(none)";

        List<string> summaries = new List<string>();
        foreach (Product product in storeController.products.all)
        {
            if (product == null)
                continue;

            string id = product.definition?.id ?? "(null)";
            string storeId = product.definition?.storeSpecificId ?? string.Empty;
            string available = product.availableToPurchase ? "available" : "unavailable";
            string price = product.metadata?.localizedPriceString ?? string.Empty;
            summaries.Add(string.IsNullOrEmpty(storeId) || storeId == id
                ? $"{id}:{available}:{price}"
                : $"{id}/{storeId}:{available}:{price}");
        }

        return summaries.Count == 0 ? "(none)" : string.Join(",", summaries);
    }
}
#endif

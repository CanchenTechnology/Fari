using System;
using System.Collections;
using System.Text;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;
using XFGameFrameWork;

/// <summary>
/// IAP 购买入口。
/// 当前工程尚未安装 Unity IAP 包，因此这里提供安全降级和统一入口。
/// </summary>
public class IapPurchaseManager : MonoSingleton<IapPurchaseManager>
{
    public const string SubmitReceiptFunctionUrl = "https://us-central1-fari-app-b2fd2.cloudfunctions.net/submitIapReceipt";
    private IapProductsConfig configuredProducts = IapProductsConfig.Default;

    public bool IsUnityIapAvailable => HasType("UnityEngine.Purchasing.StandardPurchasingModule, UnityEngine.Purchasing")
        || HasType("UnityEngine.Purchasing.StandardPurchasingModule, Unity.Purchasing");

    public void ConfigureProducts(IapProductsConfig products)
    {
        if (products != null)
            configuredProducts = products;
    }

    public void PurchaseSubscription(IapProductConfig product, Action<bool, string> onComplete = null)
    {
        if (product == null || string.IsNullOrEmpty(product.productId))
        {
            onComplete?.Invoke(false, "IAP 商品未配置");
            return;
        }

#if UNITY_PURCHASING
        GetUnityIapBridge().Purchase(product, configuredProducts, this, onComplete);
        return;
#else
        if (!IsUnityIapAvailable)
        {
            string message = $"Unity IAP 包未安装，暂不能发起购买：{product.productId}";
            Debug.LogWarning("[IapPurchaseManager] " + message);
            onComplete?.Invoke(false, message);
            return;
        }

        string pendingMessage = $"Unity IAP 已安装但购买桥未启用，请等待 Unity 重新解析 Package 后重试：{product.productId}";
        Debug.LogWarning("[IapPurchaseManager] " + pendingMessage);
        onComplete?.Invoke(false, pendingMessage);
#endif
    }

    public void RestorePurchases(IapProductsConfig products, Action<bool, string> onComplete = null)
    {
        ConfigureProducts(products);
#if UNITY_PURCHASING
        GetUnityIapBridge().RestorePurchases(configuredProducts, this, onComplete);
#else
        onComplete?.Invoke(false, "Unity IAP 包未安装，暂不能从商店恢复购买");
#endif
    }

    public void SubmitPurchaseReceipt(
        IapProductConfig product,
        string receipt,
        string transactionId = "",
        string packageName = "",
        Action<bool, string> onComplete = null)
    {
        if (product == null || string.IsNullOrEmpty(product.productId))
        {
            onComplete?.Invoke(false, "IAP 商品未配置");
            return;
        }

        if (string.IsNullOrWhiteSpace(receipt))
        {
            onComplete?.Invoke(false, "购买凭证为空，无法校验");
            return;
        }

        StartCoroutine(SubmitPurchaseReceiptRoutine(product, receipt, transactionId, packageName, onComplete));
    }

    public void OpenSubscriptionManagement()
    {
        Application.OpenURL(GetSubscriptionManagementUrl());
    }

#if UNITY_PURCHASING
    private UnityIapPurchaseBridge GetUnityIapBridge()
    {
        var bridge = GetComponent<UnityIapPurchaseBridge>();
        if (bridge != null) return bridge;
        return gameObject.AddComponent<UnityIapPurchaseBridge>();
    }
#endif

    private IEnumerator SubmitPurchaseReceiptRoutine(
        IapProductConfig product,
        string receipt,
        string transactionId,
        string packageName,
        Action<bool, string> onComplete)
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance?.CurrentUser;
        if (user == null)
        {
            onComplete?.Invoke(false, "请先登录后再恢复购买");
            yield break;
        }

        var tokenTask = user.TokenAsync(false);
        yield return new WaitUntil(() => tokenTask.IsCompleted);
        if (tokenTask.IsFaulted || tokenTask.IsCanceled || string.IsNullOrEmpty(tokenTask.Result))
        {
            onComplete?.Invoke(false, "获取登录凭证失败，无法提交购买凭证");
            yield break;
        }

        string json = BuildReceiptJson(product, receipt, transactionId, packageName);
        using (UnityWebRequest request = new UnityWebRequest(SubmitReceiptFunctionUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + tokenTask.Result);

            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool failed = request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError;
#else
            bool failed = request.isNetworkError || request.isHttpError;
#endif
            if (failed)
            {
                string error = string.IsNullOrEmpty(request.downloadHandler.text)
                    ? request.error
                    : request.downloadHandler.text;
                Debug.LogWarning("[IapPurchaseManager] 提交购买凭证失败: " + error);
                onComplete?.Invoke(false, "提交购买凭证失败：" + error);
                yield break;
            }

            var response = JsonUtility.FromJson<IapReceiptSubmitResponse>(request.downloadHandler.text);
            string message = response != null && (response.status == "pending_verification" || response.status == "pending_configuration")
                ? "购买凭证已提交，等待商店校验"
                : response != null && response.status == "verified"
                ? "购买校验成功，Pro 已生效"
                : response != null && response.status == "expired"
                ? "订阅已过期，请重新购买"
                : response != null && response.status == "invalid"
                ? "购买凭证无效，请检查商店账号"
                : string.IsNullOrEmpty(response?.message)
                ? "购买凭证已提交，等待商店校验"
                : response.message;
            onComplete?.Invoke(response == null || response.ok, message);
        }
    }

    private static string BuildReceiptJson(IapProductConfig product, string receipt, string transactionId, string packageName)
    {
        return "{"
            + $"\"productId\":\"{EscapeJson(product.productId)}\","
            + $"\"store\":\"{EscapeJson(product.store)}\","
            + $"\"transactionId\":\"{EscapeJson(transactionId)}\","
            + $"\"receipt\":\"{EscapeJson(receipt)}\","
            + $"\"packageName\":\"{EscapeJson(packageName)}\","
            + $"\"platform\":\"{EscapeJson(Application.platform.ToString())}\","
            + $"\"appVersion\":\"{EscapeJson(Application.version)}\""
            + "}";
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private static bool HasType(string typeName)
    {
        return Type.GetType(typeName, false) != null;
    }

    private static string GetSubscriptionManagementUrl()
    {
#if UNITY_IOS
        return "https://apps.apple.com/account/subscriptions";
#elif UNITY_ANDROID
        return "https://play.google.com/store/account/subscriptions";
#else
        return "https://play.google.com/store/account/subscriptions";
#endif
    }

#pragma warning disable 0649
    [Serializable]
    private class IapReceiptSubmitResponse
    {
        public bool ok;
        public string status;
        public string membershipStatus;
        public bool isPro;
        public string proExpiresAt;
        public string message;
    }
#pragma warning restore 0649
}

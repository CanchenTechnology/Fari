using System;
using UnityEngine;
using XFGameFrameWork;

/// <summary>
/// IAP 购买入口。
/// 当前工程尚未安装 Unity IAP 包，因此这里提供安全降级和统一入口。
/// </summary>
public class IapPurchaseManager : MonoSingleton<IapPurchaseManager>
{
    public bool IsUnityIapAvailable => HasType("UnityEngine.Purchasing.StandardPurchasingModule, UnityEngine.Purchasing")
        || HasType("UnityEngine.Purchasing.StandardPurchasingModule, Unity.Purchasing");

    public void PurchaseSubscription(IapProductConfig product, Action<bool, string> onComplete = null)
    {
        if (product == null || string.IsNullOrEmpty(product.productId))
        {
            onComplete?.Invoke(false, "IAP 商品未配置");
            return;
        }

        if (!IsUnityIapAvailable)
        {
            string message = $"Unity IAP 包未安装，暂不能发起购买：{product.productId}";
            Debug.LogWarning("[IapPurchaseManager] " + message);
            onComplete?.Invoke(false, message);
            return;
        }

        string pendingMessage = $"已检测到 Unity IAP 包，购买流程待接入：{product.productId}";
        Debug.LogWarning("[IapPurchaseManager] " + pendingMessage);
        onComplete?.Invoke(false, pendingMessage);
    }

    public void OpenSubscriptionManagement()
    {
        Application.OpenURL(GetSubscriptionManagementUrl());
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
}

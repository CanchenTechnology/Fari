using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Firebase;
using Firebase.Auth;
using UnityEngine;

/// <summary>
/// Runtime diagnostics for services that depend on external setup.
/// Use this in Editor/device logs to separate code issues from package, auth, deploy, or store setup issues.
/// </summary>
public static class AppReadinessDiagnostics
{
    private const string IapPackageName = "com.unity.purchasing";
    private const string NotificationsPackageName = "com.unity.mobile.notifications";
    private const string AndroidPostNotificationsPermission = "android.permission.POST_NOTIFICATIONS";

    public static void LogCurrentState(string reason = "manual")
    {
        Debug.Log(BuildReport(reason));
    }

    public static string BuildReport(string reason = "manual")
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"[AppReadinessDiagnostics] Moonly readiness report ({reason})");
        foreach (string line in BuildReportLines())
            builder.AppendLine("  - " + line);
        return builder.ToString().TrimEnd();
    }

    public static List<string> BuildReportLines()
    {
        List<string> lines = new List<string>
        {
            $"Platform: {Application.platform}, Unity {Application.unityVersion}, App {Application.version}",
            $"Firebase: {BuildFirebaseLine()}",
            $"Auth providers: gameCenter={FormatBool(IsGameCenterAuthProviderResolved())}",
            $"Functions: membershipStatus={BackendMembershipClient.MembershipStatusFunctionUrl}, submitIapReceipt={IapPurchaseManager.SubmitReceiptFunctionUrl}",
            $"Functions readiness endpoint: {BackendMembershipClient.ReadinessStatusFunctionUrl}",
            $"Unity IAP: manifest={FormatBool(ManifestHasPackage(IapPackageName))}, packageResolved={FormatBool(IsUnityIapPackageResolved())}, bridgeCompiled={FormatBool(IsUnityPurchasingSymbolCompiled())}",
            $"Mobile Notifications: manifest={FormatBool(ManifestHasPackage(NotificationsPackageName))}, apiResolved={FormatBool(IsMobileNotificationsApiResolved())}, androidPostPermission={FormatBool(AndroidManifestHasPermission(AndroidPostNotificationsPermission))}",
            $"Notification settings: {BuildNotificationSettingsLine()}",
            $"Scheduled notifications: {BuildScheduledNotificationsLine()}",
            $"Backend blockers: {BuildBackendBlockersLine()}",
        };

        return lines;
    }

    public static bool IsUnityIapPackageResolved()
    {
        return HasAnyType(
            "UnityEngine.Purchasing.StandardPurchasingModule, UnityEngine.Purchasing",
            "UnityEngine.Purchasing.StandardPurchasingModule, Unity.Purchasing");
    }

    public static bool IsMobileNotificationsApiResolved()
    {
        return HasAnyType(
            "Unity.Notifications.NotificationCenter, Unity.Notifications",
            "Unity.Notifications.NotificationCenter, Unity.Notifications.Unified",
            "Unity.Notifications.Android.AndroidNotificationCenter, Unity.Notifications.Android",
            "Unity.Notifications.iOS.iOSNotificationCenter, Unity.Notifications.iOS");
    }

    public static bool IsGameCenterAuthProviderResolved()
    {
        return HasAnyType("Firebase.Auth.GameCenterAuthProvider, Firebase.Auth");
    }

    public static bool ManifestHasPackage(string packageName)
    {
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
            if (!File.Exists(manifestPath)) return false;

            string manifest = File.ReadAllText(manifestPath);
            return manifest.Contains($"\"{packageName}\"", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static bool AndroidManifestHasPermission(string permissionName)
    {
        return ProjectFileContains(
            Path.Combine("Assets", "Plugins", "Android", "AndroidManifest.xml"),
            permissionName);
    }

    private static string BuildFirebaseLine()
    {
        try
        {
            FirebaseApp app = FirebaseApp.DefaultInstance;
            FirebaseUser user = FirebaseAuth.DefaultInstance?.CurrentUser;
            string uid = string.IsNullOrEmpty(user?.UserId) ? "none" : user.UserId;
            string provider = user == null
                ? "none"
                : user.IsAnonymous ? "anonymous" : user.ProviderId ?? "firebase";
            return $"app={FormatBool(app != null)}, uid={uid}, provider={provider}";
        }
        catch (Exception ex)
        {
            return "not ready (" + ex.GetType().Name + ": " + ex.Message + ")";
        }
    }

    private static string BuildNotificationSettingsLine()
    {
        try
        {
            NotificationSettingsManager settings = NotificationSettingsManager.Instance;
            if (settings == null) return "manager missing";

            string schedulerSummary = AppNotificationScheduler.Instance != null
                ? AppNotificationScheduler.Instance.LastSyncSummary
                : string.Empty;
            if (string.IsNullOrWhiteSpace(schedulerSummary))
                schedulerSummary = "no scheduled summary yet";

            return $"daily={settings.DailyOracleEnabled}, return={settings.DivinationReturnEnabled}, friends={settings.FriendInteractionEnabled}, system={settings.ActivitySystemEnabled}, time={settings.ReminderTime}, schedule={schedulerSummary}";
        }
        catch (Exception ex)
        {
            return "unavailable (" + ex.GetType().Name + ": " + ex.Message + ")";
        }
    }

    private static string BuildScheduledNotificationsLine()
    {
        try
        {
            AppNotificationScheduler scheduler = AppNotificationScheduler.Instance;
            return scheduler != null
                ? scheduler.BuildScheduledDebugSummary()
                : "scheduler missing";
        }
        catch (Exception ex)
        {
            return "unavailable (" + ex.GetType().Name + ": " + ex.Message + ")";
        }
    }

    private static string BuildBackendBlockersLine()
    {
        List<string> blockers = new List<string>();

        if (!IsUnityPurchasingSymbolCompiled())
            blockers.Add("UNITY_PURCHASING symbol not compiled");
        if (!IsUnityIapPackageResolved())
            blockers.Add("Unity IAP package not resolved in Editor");
        if (!IsMobileNotificationsApiResolved())
            blockers.Add("Mobile Notifications API not resolved in Editor");
        if (!AndroidManifestHasPermission(AndroidPostNotificationsPermission))
            blockers.Add("Android POST_NOTIFICATIONS permission missing");

        FirebaseUser user = null;
        try
        {
            user = FirebaseAuth.DefaultInstance?.CurrentUser;
        }
        catch
        {
            // Firebase not initialized yet.
        }

        if (user == null)
            blockers.Add("Firebase user not signed in");

        blockers.Add("Firebase deploy/secrets still require CLI/account verification");
        return blockers.Count == 0 ? "none detected locally" : string.Join("; ", blockers);
    }

    private static bool HasAnyType(params string[] typeNames)
    {
        foreach (string typeName in typeNames)
        {
            if (Type.GetType(typeName, false) != null)
                return true;
        }

        return false;
    }

    private static bool ProjectFileContains(string relativePath, string needle)
    {
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(projectRoot, relativePath);
            return File.Exists(path) && File.ReadAllText(path).Contains(needle, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string FormatBool(bool value)
    {
        return value ? "yes" : "no";
    }

    private static bool IsUnityPurchasingSymbolCompiled()
    {
#if UNITY_PURCHASING
        return true;
#else
        return false;
#endif
    }
}

using UnityEditor;
using UnityEngine;

public static class AppReadinessDiagnosticsMenu
{
    private const string DeployCommand = "MOONLY_PROXY='http://[::1]:7897' ./scripts/deploy-firebase.sh";
    private const string FullReadinessCommand = "MOONLY_PROXY='http://[::1]:7897' MOONLY_ALL_PROXY=socks5://127.0.0.1:10808 CURL_MAX_TIME=180 CHECK_REGISTRY=1 CHECK_SECRETS_ENV=1 CHECK_FIREBASE=1 CHECK_FIREBASE_NETWORK=1 CHECK_FIREBASE_FUNCTIONS=1 CHECK_FUNCTIONS_SMOKE=1 CHECK_FUNCTIONS_SMOKE_STRICT=1 CHECK_IAP_SMOKE=1 CHECK_BUILD=1 ./scripts/check-local-readiness.sh";
    private const string ReleaseBlockersCommand = "MOONLY_PROXY='http://[::1]:7897' MOONLY_ALL_PROXY=socks5://127.0.0.1:10808 CURL_MAX_TIME=180 CHECK_FIREBASE_SECRETS=1 CHECK_FUNCTIONS_SMOKE=1 CHECK_IAP_FAKE_SMOKE=1 ./scripts/check-release-blockers.sh";
    private const string ReleaseBlockersEnvCommand = "RELEASE_ENV_FILE=scripts/release.env ./scripts/check-release-blockers.sh";
    private const string PrepareReleaseCommand = "MOONLY_PROXY='http://[::1]:7897' MOONLY_ALL_PROXY=socks5://127.0.0.1:10808 CURL_MAX_TIME=180 REPORT_ONLY=1 ./scripts/prepare-release.sh";
    private const string PrepareReleaseEnvCommand = "RELEASE_ENV_FILE=scripts/release.env REPORT_ONLY=1 ./scripts/prepare-release.sh";
    private const string CheckReleaseEnvCommand = "RELEASE_ENV_FILE=scripts/release.env ./scripts/check-release-env.sh";
    private const string FinishReleaseEnvCommand = "RELEASE_ENV_FILE=scripts/release.env ./scripts/finish-release.sh";
    private const string IosExportCommand = "CLEAN_IOS_EXPORT=1 ./scripts/build-ios-xcode.sh";
    private const string AndroidKeystoreCheckCommand = "RELEASE_ENV_FILE=scripts/release.env ./scripts/check-android-keystore.sh";
    private const string AndroidBuildCommand = "CLEAN_ANDROID_BUILD=1 ./scripts/build-android-apk.sh";

    [MenuItem("Tools/Moonly/Log Readiness Report")]
    public static void LogReadinessReport()
    {
        AppReadinessDiagnostics.LogCurrentState("Editor menu");
    }

    [MenuItem("Tools/Moonly/Copy Firebase Deploy Command")]
    public static void CopyFirebaseDeployCommand()
    {
        GUIUtility.systemCopyBuffer = DeployCommand;
        Debug.Log("[Moonly] Firebase deploy command copied:\n" + DeployCommand);
    }

    [MenuItem("Tools/Moonly/Copy Full Readiness Command")]
    public static void CopyFullReadinessCommand()
    {
        GUIUtility.systemCopyBuffer = FullReadinessCommand;
        Debug.Log("[Moonly] Full readiness command copied:\n" + FullReadinessCommand);
    }

    [MenuItem("Tools/Moonly/Copy Release Blockers Command")]
    public static void CopyReleaseBlockersCommand()
    {
        GUIUtility.systemCopyBuffer = ReleaseBlockersCommand;
        Debug.Log("[Moonly] Release blockers command copied:\n" + ReleaseBlockersCommand);
    }

    [MenuItem("Tools/Moonly/Copy Release Blockers Env Command")]
    public static void CopyReleaseBlockersEnvCommand()
    {
        GUIUtility.systemCopyBuffer = ReleaseBlockersEnvCommand;
        Debug.Log("[Moonly] Release blockers env command copied:\n" + ReleaseBlockersEnvCommand);
    }

    [MenuItem("Tools/Moonly/Copy Prepare Release Command")]
    public static void CopyPrepareReleaseCommand()
    {
        GUIUtility.systemCopyBuffer = PrepareReleaseCommand;
        Debug.Log("[Moonly] Prepare release command copied:\n" + PrepareReleaseCommand);
    }

    [MenuItem("Tools/Moonly/Copy Prepare Release Env Command")]
    public static void CopyPrepareReleaseEnvCommand()
    {
        GUIUtility.systemCopyBuffer = PrepareReleaseEnvCommand;
        Debug.Log("[Moonly] Prepare release env command copied:\n" + PrepareReleaseEnvCommand);
    }

    [MenuItem("Tools/Moonly/Copy Check Release Env Command")]
    public static void CopyCheckReleaseEnvCommand()
    {
        GUIUtility.systemCopyBuffer = CheckReleaseEnvCommand;
        Debug.Log("[Moonly] Check release env command copied:\n" + CheckReleaseEnvCommand);
    }

    [MenuItem("Tools/Moonly/Copy Finish Release Env Command")]
    public static void CopyFinishReleaseEnvCommand()
    {
        GUIUtility.systemCopyBuffer = FinishReleaseEnvCommand;
        Debug.Log("[Moonly] Finish release env command copied:\n" + FinishReleaseEnvCommand);
    }

    [MenuItem("Tools/Moonly/Copy iOS Xcode Export Command")]
    public static void CopyIOSXcodeExportCommand()
    {
        GUIUtility.systemCopyBuffer = IosExportCommand;
        Debug.Log("[Moonly] iOS Xcode export command copied:\n" + IosExportCommand);
    }

    [MenuItem("Tools/Moonly/Copy Android APK Build Command")]
    public static void CopyAndroidApkBuildCommand()
    {
        GUIUtility.systemCopyBuffer = AndroidBuildCommand;
        Debug.Log("[Moonly] Android APK build command copied:\n" + AndroidBuildCommand);
    }

    [MenuItem("Tools/Moonly/Copy Android Keystore Check Command")]
    public static void CopyAndroidKeystoreCheckCommand()
    {
        GUIUtility.systemCopyBuffer = AndroidKeystoreCheckCommand;
        Debug.Log("[Moonly] Android keystore check command copied:\n" + AndroidKeystoreCheckCommand);
    }

    [MenuItem("Tools/Moonly/Open Functions Readiness URL")]
    public static void OpenFunctionsReadinessUrl()
    {
        Application.OpenURL(BackendMembershipClient.ReadinessStatusFunctionUrl);
    }

    [MenuItem("Tools/Moonly/Schedule Test Notification (10s)")]
    public static void ScheduleTestNotification()
    {
        bool nativeScheduled = AppNotificationScheduler.Instance.ScheduleDiagnosticNotification(10);
        string mode = nativeScheduled ? "native" : "fallback";
        Debug.Log($"[Moonly] Test notification scheduled in {mode} mode. If this is a device build, wait 10 seconds and confirm the system notification appears.");
        Debug.Log("[Moonly] Scheduled notifications:\n" + AppNotificationScheduler.Instance.BuildScheduledDebugSummary());
    }

    [MenuItem("Tools/Moonly/Log Scheduled Notifications")]
    public static void LogScheduledNotifications()
    {
        Debug.Log("[Moonly] Scheduled notifications:\n" + AppNotificationScheduler.Instance.BuildScheduledDebugSummary());
    }
}

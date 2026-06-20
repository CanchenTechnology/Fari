using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

public static class AppPackageResolverMenu
{
    private const double ResolveTimeoutSeconds = 300.0;
    private const string UnityPurchasingDefine = "UNITY_PURCHASING";

    private static readonly string[] RequiredPackages =
    {
        "com.unity.purchasing@4.12.2",
        "com.unity.mobile.notifications@2.3.2",
    };

    private static Queue<string> pendingPackages;
    private static AddRequest currentRequest;
    private static string currentPackage;
    private static bool isResolving;
    private static bool quitWhenFinished;
    private static bool hadFailure;
    private static double resolveStartedAt;

    [MenuItem("Tools/Moonly/Resolve Required Packages")]
    public static void ResolveRequiredPackages()
    {
        if (isResolving)
        {
            Debug.LogWarning("[Moonly] Package resolve is already running.");
            return;
        }

        pendingPackages = new Queue<string>(RequiredPackages);
        isResolving = true;
        quitWhenFinished = false;
        hadFailure = false;
        resolveStartedAt = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        Debug.Log("[Moonly] Resolving required packages: " + string.Join(", ", RequiredPackages));
        EnsureUnityPurchasingDefine();
        StartNextPackage();
    }

    [MenuItem("Tools/Moonly/Ensure IAP Scripting Define")]
    public static void EnsureUnityPurchasingDefine()
    {
        EnsureDefineForGroup(BuildTargetGroup.Android, UnityPurchasingDefine);
        EnsureDefineForGroup(BuildTargetGroup.iOS, UnityPurchasingDefine);
        EnsureDefineForGroup(BuildTargetGroup.Standalone, UnityPurchasingDefine);
    }

    public static void ResolveRequiredPackagesBatchMode()
    {
        ResolveRequiredPackages();
        quitWhenFinished = true;
    }

    private static void Tick()
    {
        if (!isResolving)
            return;

        if (EditorApplication.timeSinceStartup - resolveStartedAt > ResolveTimeoutSeconds)
        {
            hadFailure = true;
            Debug.LogError("[Moonly] Package resolve timed out.");
            Finish();
            return;
        }

        if (currentRequest == null || !currentRequest.IsCompleted)
            return;

        if (currentRequest.Status == StatusCode.Success)
        {
            Debug.Log($"[Moonly] Package ready: {currentRequest.Result.packageId}");
        }
        else if (currentRequest.Status >= StatusCode.Failure)
        {
            hadFailure = true;
            string error = currentRequest.Error != null ? currentRequest.Error.message : "Unknown package manager error";
            Debug.LogWarning($"[Moonly] Package resolve failed for {currentPackage}: {error}");
        }

        currentRequest = null;
        currentPackage = string.Empty;
        StartNextPackage();
    }

    private static void StartNextPackage()
    {
        if (pendingPackages == null || pendingPackages.Count == 0)
        {
            Finish();
            return;
        }

        currentPackage = pendingPackages.Dequeue();
        Debug.Log("[Moonly] Requesting package: " + currentPackage);
        currentRequest = Client.Add(currentPackage);
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        currentRequest = null;
        currentPackage = string.Empty;
        pendingPackages = null;
        isResolving = false;
        Debug.Log("[Moonly] Package resolve requests finished. Run Tools/Moonly/Log Readiness Report or scripts/check-local-readiness.sh to verify packages-lock.");

        if (quitWhenFinished)
            EditorApplication.Exit(hadFailure ? 1 : 0);
    }

    private static void EnsureDefineForGroup(BuildTargetGroup group, string define)
    {
        string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
        HashSet<string> symbols = new HashSet<string>(
            current.Split(';').Where(symbol => !string.IsNullOrWhiteSpace(symbol)));

        if (!symbols.Add(define))
            return;

        string updated = string.Join(";", symbols);
        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, updated);
        Debug.Log($"[Moonly] Added {define} scripting define for {group}.");
    }
}

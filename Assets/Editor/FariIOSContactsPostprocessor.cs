using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class FariIOSContactsPostprocessor
{
    private const string EntitlementsFileName = "fari.entitlements";

    [PostProcessBuild(46)]
    public static void ConfigureIOSProject(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        AddGoogleSignInPod(pathToBuiltProject);
        AddInfoPlistUsageDescriptions(pathToBuiltProject);
        AddRequiredCapabilities(pathToBuiltProject);
    }

    private static void AddGoogleSignInPod(string pathToBuiltProject)
    {
        string podfilePath = Path.Combine(pathToBuiltProject, "Podfile");
        if (!File.Exists(podfilePath)) return;

        string podfile = File.ReadAllText(podfilePath);
        if (podfile.Contains("pod 'GoogleSignIn'") || podfile.Contains("pod \"GoogleSignIn\"")) return;

        const string unityFrameworkTarget = "target 'UnityFramework' do";
        int targetIndex = podfile.IndexOf(unityFrameworkTarget, System.StringComparison.Ordinal);
        if (targetIndex < 0) return;

        int insertIndex = podfile.IndexOf('\n', targetIndex);
        if (insertIndex < 0) return;

        podfile = podfile.Insert(insertIndex + 1, "  pod 'GoogleSignIn', '~> 7.0'\n");
        File.WriteAllText(podfilePath, podfile);
    }

    private static void AddInfoPlistUsageDescriptions(string pathToBuiltProject)
    {
        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        if (!File.Exists(plistPath)) return;

        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetString(
            "NSContactsUsageDescription",
            "用于选择你想邀请加入 Moonly 的联系人。我们不会自动发送消息。");
        plist.WriteToFile(plistPath);
    }

    private static void AddRequiredCapabilities(string pathToBuiltProject)
    {
        string pbxProjectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        if (!File.Exists(pbxProjectPath)) return;

        PBXProject project = new PBXProject();
        project.ReadFromFile(pbxProjectPath);
        string mainTargetGuid = project.GetUnityMainTargetGuid();
        if (string.IsNullOrEmpty(mainTargetGuid)) return;

        ProjectCapabilityManager capabilities = new ProjectCapabilityManager(
            pbxProjectPath,
            EntitlementsFileName,
            null,
            mainTargetGuid);

        capabilities.AddGameCenter();
        capabilities.AddSignInWithApple();
        capabilities.AddInAppPurchase();
        capabilities.AddPushNotifications(EditorUserBuildSettings.development);
        capabilities.WriteToFile();
    }
}

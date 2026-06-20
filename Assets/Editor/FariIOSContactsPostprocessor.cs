#if UNITY_IOS
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

        AddInfoPlistUsageDescriptions(pathToBuiltProject);
        AddRequiredCapabilities(pathToBuiltProject);
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
        capabilities.WriteToFile();
    }
}
#endif

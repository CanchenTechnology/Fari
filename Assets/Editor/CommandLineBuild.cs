using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;
using UnityBuildResult = UnityEditor.Build.Reporting.BuildResult;
using YooBuildResult = YooAsset.Editor.BuildResult;

public static class CommandLineBuild
{
    private const string DefaultYooPackageName = "DefaultPackage";
    private const string DefaultYooPipelineName = nameof(EBuildPipeline.ScriptableBuildPipeline);

    public static void BuildIOSProject()
    {
        var outputPath = GetArg("-outputPath", "Builds/iOS");
        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(outputPath);

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
        ValidateRelationshipDivinationLocalFlow();

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = outputPath,
            target = BuildTarget.iOS,
            options = BuildOptions.None
        });

        var summary = report.summary;
        Debug.Log($"iOS build result: {summary.result}, output: {outputPath}, size: {summary.totalSize} bytes");

        if (summary.result != UnityBuildResult.Succeeded)
        {
            throw new InvalidOperationException($"iOS build failed: {summary.result}");
        }
    }

    public static void BuildAndroidApk()
    {
        var outputPath = GetArg("-outputPath", "Builds/Android/MoonlyApp.apk");
        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        ValidateRelationshipDivinationLocalFlow();
        var oldBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
        var oldExportAndroidProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;

        try
        {
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

            SetGameStartPlayMode(EPlayMode.OfflinePlayMode);
            BuildYooAssetPackage(BuildTarget.Android);
            GenerateHybridClrFiles();
            ApplySigningPasswords();
            ClearBuildOutputPath(outputPath);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            var summary = report.summary;
            Debug.Log($"Android build result: {summary.result}, output: {outputPath}, size: {summary.totalSize} bytes");

            if (summary.result != UnityBuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Android build failed: {summary.result}");
            }
        }
        finally
        {
            EditorUserBuildSettings.buildAppBundle = oldBuildAppBundle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = oldExportAndroidProject;
        }
    }

    private static void ClearBuildOutputPath(string outputPath)
    {
        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, true);
        }
        else if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }

    private static void BuildYooAssetPackage(BuildTarget buildTarget)
    {
        var packageName = GetArg("-yooPackage", DefaultYooPackageName);
        var pipelineName = GetArg("-yooPipeline", AssetBundleBuilderSetting.GetPackageBuildPipeline(packageName));
        if (string.IsNullOrEmpty(pipelineName))
        {
            pipelineName = DefaultYooPipelineName;
        }

        var packageVersion = GetArg("-yooPackageVersion", GetDefaultYooPackageVersion());
        var copyOption = GetYooBuildinCopyOption(packageName, pipelineName);
        var buildParameters = CreateYooBuildParameters(packageName, pipelineName, packageVersion, buildTarget, copyOption);
        var pipeline = CreateYooBuildPipeline(pipelineName);

        Debug.Log($"YooAsset build start. package={packageName}, pipeline={pipelineName}, version={packageVersion}, copyOption={copyOption}");
        YooBuildResult result = pipeline.Run(buildParameters, true);
        if (!result.Success)
        {
            throw new InvalidOperationException($"YooAsset build failed. task={result.FailedTask}, error={result.ErrorInfo}");
        }

        Debug.Log($"YooAsset build succeeded. output={result.OutputPackageDirectory}");
        AssetDatabase.Refresh();
    }

    private static BuildParameters CreateYooBuildParameters(
        string packageName,
        string pipelineName,
        string packageVersion,
        BuildTarget buildTarget,
        EBuildinFileCopyOption copyOption)
    {
        var fileNameStyle = AssetBundleBuilderSetting.GetPackageFileNameStyle(packageName, pipelineName);
        var buildinFileCopyParams = AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(packageName, pipelineName);
        var clearBuildCache = AssetBundleBuilderSetting.GetPackageClearBuildCache(packageName, pipelineName);
        var useAssetDependencyDB = AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipelineName);

        if (pipelineName == nameof(EBuildPipeline.RawFileBuildPipeline))
        {
            return ApplyCommonYooParameters(new RawFileBuildParameters(), packageName, pipelineName, packageVersion,
                buildTarget, (int)EBuildBundleType.RawBundle, fileNameStyle, copyOption, buildinFileCopyParams,
                clearBuildCache, useAssetDependencyDB);
        }

        var compressOption = AssetBundleBuilderSetting.GetPackageCompressOption(packageName, pipelineName);
        if (pipelineName == nameof(EBuildPipeline.BuiltinBuildPipeline))
        {
            var parameters = ApplyCommonYooParameters(new BuiltinBuildParameters(), packageName, pipelineName,
                packageVersion, buildTarget, (int)EBuildBundleType.AssetBundle, fileNameStyle, copyOption,
                buildinFileCopyParams, clearBuildCache, useAssetDependencyDB);
            parameters.CompressOption = compressOption;
            return parameters;
        }

        var scriptableParameters = ApplyCommonYooParameters(new ScriptableBuildParameters(), packageName, pipelineName,
            packageVersion, buildTarget, (int)EBuildBundleType.AssetBundle, fileNameStyle, copyOption,
            buildinFileCopyParams, clearBuildCache, useAssetDependencyDB);
        scriptableParameters.CompressOption = compressOption;
        scriptableParameters.BuiltinShadersBundleName = GetBuiltinShaderBundleName(packageName);
        return scriptableParameters;
    }

    private static T ApplyCommonYooParameters<T>(
        T parameters,
        string packageName,
        string pipelineName,
        string packageVersion,
        BuildTarget buildTarget,
        int buildBundleType,
        EFileNameStyle fileNameStyle,
        EBuildinFileCopyOption copyOption,
        string buildinFileCopyParams,
        bool clearBuildCache,
        bool useAssetDependencyDB)
        where T : BuildParameters
    {
        parameters.BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
        parameters.BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
        parameters.BuildPipeline = pipelineName;
        parameters.BuildBundleType = buildBundleType;
        parameters.BuildTarget = buildTarget;
        parameters.PackageName = packageName;
        parameters.PackageVersion = packageVersion;
        parameters.EnableSharePackRule = true;
        parameters.VerifyBuildingResult = true;
        parameters.FileNameStyle = fileNameStyle;
        parameters.BuildinFileCopyOption = copyOption;
        parameters.BuildinFileCopyParams = buildinFileCopyParams;
        parameters.ClearBuildCacheFiles = clearBuildCache;
        parameters.UseAssetDependencyDB = useAssetDependencyDB;
        parameters.EncryptionServices = CreateYooService<IEncryptionServices>(
            AssetBundleBuilderSetting.GetPackageEncyptionServicesClassName(packageName, pipelineName));
        parameters.ManifestProcessServices = CreateYooService<IManifestProcessServices>(
            AssetBundleBuilderSetting.GetPackageManifestProcessServicesClassName(packageName, pipelineName));
        parameters.ManifestRestoreServices = CreateYooService<IManifestRestoreServices>(
            AssetBundleBuilderSetting.GetPackageManifestRestoreServicesClassName(packageName, pipelineName));
        return parameters;
    }

    private static IBuildPipeline CreateYooBuildPipeline(string pipelineName)
    {
        if (pipelineName == nameof(EBuildPipeline.RawFileBuildPipeline))
        {
            return new RawFileBuildPipeline();
        }

        if (pipelineName == nameof(EBuildPipeline.BuiltinBuildPipeline))
        {
            return new BuiltinBuildPipeline();
        }

        return new ScriptableBuildPipeline();
    }

    private static EBuildinFileCopyOption GetYooBuildinCopyOption(string packageName, string pipelineName)
    {
        var argValue = GetArg("-yooCopyOption", null);
        if (!string.IsNullOrEmpty(argValue) && Enum.TryParse(argValue, true, out EBuildinFileCopyOption argOption))
        {
            return argOption;
        }

        var option = AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(packageName, pipelineName);
        return option == EBuildinFileCopyOption.None ? EBuildinFileCopyOption.ClearAndCopyAll : option;
    }

    private static string GetBuiltinShaderBundleName(string packageName)
    {
        var uniqueBundleName = AssetBundleCollectorSettingData.Setting.UniqueBundleName;
        var packRuleResult = DefaultPackRule.CreateShadersPackRuleResult();
        return packRuleResult.GetBundleName(packageName, uniqueBundleName);
    }

    private static T CreateYooService<T>(string className) where T : class
    {
        var serviceType = EditorTools.GetAssignableTypes(typeof(T)).FirstOrDefault(type => type.FullName == className);
        return serviceType == null ? null : Activator.CreateInstance(serviceType) as T;
    }

    private static string GetDefaultYooPackageVersion()
    {
        int totalMinutes = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
        return DateTime.Now.ToString("yyyy-MM-dd") + "-" + totalMinutes;
    }

    private static void SetGameStartPlayMode(EPlayMode playMode)
    {
        foreach (var scenePath in GetEnabledScenes())
        {
            var scene = EditorSceneManager.OpenScene(scenePath);
            bool changed = false;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var gameStart in root.GetComponentsInChildren<GameStart>(true))
                {
                    if (gameStart.PlayMode == playMode)
                    {
                        continue;
                    }

                    gameStart.PlayMode = playMode;
                    EditorUtility.SetDirty(gameStart);
                    changed = true;
                    Debug.Log($"Set {scenePath} GameStart.PlayMode to {playMode}");
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
    }

    private static void GenerateHybridClrFiles()
    {
        HybridCLR.Editor.Commands.PrebuildCommand.GenerateAll();
    }

    private static void ApplySigningPasswords()
    {
        var keystorePass = GetArg("-keystorePass", Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS"));
        var keyaliasPass = GetArg("-keyaliasPass", Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASS"));

        if (!string.IsNullOrEmpty(keystorePass))
        {
            PlayerSettings.Android.keystorePass = keystorePass;
        }

        if (!string.IsNullOrEmpty(keyaliasPass))
        {
            PlayerSettings.Android.keyaliasPass = keyaliasPass;
        }
    }

    private static string[] GetEnabledScenes()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled scenes found in EditorBuildSettings.");
        }

        return scenes;
    }

    private static void ValidateRelationshipDivinationLocalFlow()
    {
        var root = Directory.GetCurrentDirectory();
        var helper = ReadProjectText(root, "Assets/Scripts/Friend/CreatedFriendRelationshipDivinationLocalFlow.cs");
        var createInfo = ReadProjectText(root, "Assets/Scripts/UI/CreateFriendInfoUI.cs");
        var friendMove = ReadProjectText(root, "Assets/Scripts/UI/FriendMoveUI.cs");
        var friendRuntime = ReadProjectText(root, "Assets/Scripts/Friend/FriendRuntimeUI.cs");
        var inviteConfirm = ReadProjectText(root, "Assets/Scripts/UI/TwoPersonDivinationInviteConfirmFlowUI.cs");

        bool helperCreatesLocalRecord = helper.Contains("RelationshipDivinationFlow.ShowRecord(record, friend)") &&
                                        helper.Contains("status = RelationshipDivinationStatus.Completed") &&
                                        helper.Contains("isLocalOnly = true") &&
                                        helper.Contains("TarotDeck.DrawMultiple(3)");
        bool createInfoEntry = createInfo.Contains("RelationshipDivinationButtonName = \"RelationshipDivinationButton\"") &&
                               createInfo.Contains("RefreshRelationshipDivinationButton()") &&
                               createInfo.Contains("CreatedFriendRelationshipDivinationLocalFlow.TryStart(currentFriend)");
        bool friendMoveLocalEntry = friendMove.Contains("CreatedFriendRelationshipDivinationLocalFlow.CanHandle(currentFriend)") &&
                                    friendMove.Contains("CreatedFriendRelationshipDivinationLocalFlow.TryStart(capturedLocal)");
        bool overlayLocalEntry = friendRuntime.Contains("CreatedFriendRelationshipDivinationLocalFlow.TryStart(friend)");
        bool confirmLocalEntry = inviteConfirm.Contains("CreatedFriendRelationshipDivinationLocalFlow.TryStart(currentFriend)");

        if (helperCreatesLocalRecord && createInfoEntry && friendMoveLocalEntry && overlayLocalEntry && confirmLocalEntry)
            return;

        throw new InvalidOperationException(
            "Relationship divination local created-friend flow is missing before build. " +
            $"helperCreatesLocalRecord={helperCreatesLocalRecord}, createInfoEntry={createInfoEntry}, " +
            $"friendMoveLocalEntry={friendMoveLocalEntry}, overlayLocalEntry={overlayLocalEntry}, " +
            $"confirmLocalEntry={confirmLocalEntry}");
    }

    private static string ReadProjectText(string root, string relativePath)
    {
        var fullPath = Path.Combine(root, relativePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Required project source file is missing.", fullPath);

        return File.ReadAllText(fullPath);
    }

    private static string SliceBetween(string text, string start, string end)
    {
        int startIndex = text.IndexOf(start, StringComparison.Ordinal);
        if (startIndex < 0) return string.Empty;

        int endIndex = text.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        if (endIndex < 0) return text.Substring(startIndex);

        return text.Substring(startIndex, endIndex - startIndex);
    }

    private static string GetArg(string name, string defaultValue = null)
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return defaultValue;
    }
}

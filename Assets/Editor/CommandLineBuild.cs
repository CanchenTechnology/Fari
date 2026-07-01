using System;
using System.IO;
using System.Linq;
using HybridCLR.Editor.Commands;
using UnityEditor;
using UnityEditor.Android;
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

    public static void BuildAndroidApk()
    {
        string outputPath = Path.GetFullPath(GetArg("-outputPath", "Builds/Android/FariApp.apk"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        bool oldBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
        bool oldExportAndroidProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
        bool oldUseCustomKeystore = PlayerSettings.Android.useCustomKeystore;
        AndroidArchitecture oldTargetArchitectures = PlayerSettings.Android.targetArchitectures;

        GameStartPlayModeSnapshot playModeSnapshot = null;
        try
        {
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

            if (GetBoolArg("-useDebugKeystore"))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.Log("Android APK build will use the default debug keystore for this batchmode run.");
            }

            GenerateHybridClrFiles();
            BuildYooAssetPackage(BuildTarget.Android);
            playModeSnapshot = SetGameStartPlayMode(GetBuildYooPlayMode());
            ApplySigningPasswords();
            ClearBuildOutputPath(outputPath);

            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            BuildSummary summary = report.summary;
            Debug.Log($"Android build result: {summary.result}, output: {outputPath}, size: {summary.totalSize} bytes");
            if (summary.result != UnityBuildResult.Succeeded)
                throw new InvalidOperationException($"Android build failed: {summary.result}");
        }
        finally
        {
            playModeSnapshot?.Restore();
            EditorUserBuildSettings.buildAppBundle = oldBuildAppBundle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = oldExportAndroidProject;
            PlayerSettings.Android.useCustomKeystore = oldUseCustomKeystore;
            PlayerSettings.Android.targetArchitectures = oldTargetArchitectures;
        }
    }

    public static void BuildAndroidAppBundle()
    {
        string outputPath = Path.GetFullPath(GetArg("-outputPath", "Builds/Android/FariApp.aab"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        bool oldBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
        bool oldExportAndroidProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
        AndroidArchitecture oldTargetArchitectures = PlayerSettings.Android.targetArchitectures;

        GameStartPlayModeSnapshot playModeSnapshot = null;
        try
        {
            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

            GenerateHybridClrFiles();
            BuildYooAssetPackage(BuildTarget.Android);
            playModeSnapshot = SetGameStartPlayMode(GetBuildYooPlayMode());
            ApplySigningPasswords();
            ClearBuildOutputPath(outputPath);

            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            BuildSummary summary = report.summary;
            Debug.Log($"Android app bundle build result: {summary.result}, output: {outputPath}, size: {summary.totalSize} bytes");
            if (summary.result != UnityBuildResult.Succeeded)
                throw new InvalidOperationException($"Android app bundle build failed: {summary.result}");
        }
        finally
        {
            playModeSnapshot?.Restore();
            EditorUserBuildSettings.buildAppBundle = oldBuildAppBundle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = oldExportAndroidProject;
            PlayerSettings.Android.targetArchitectures = oldTargetArchitectures;
        }
    }

    private static void GenerateHybridClrFiles()
    {
        Debug.Log("HybridCLR GenerateAll start.");
        PrebuildCommand.GenerateAll();
        Debug.Log("HybridCLR GenerateAll finished.");
    }

    private static void BuildYooAssetPackage(BuildTarget buildTarget)
    {
        string packageName = GetArg("-yooPackage", DefaultYooPackageName);
        string pipelineName = GetArg("-yooPipeline", AssetBundleBuilderSetting.GetPackageBuildPipeline(packageName));
        if (string.IsNullOrEmpty(pipelineName))
            pipelineName = DefaultYooPipelineName;

        string packageVersion = GetArg("-yooPackageVersion", GetDefaultYooPackageVersion());
        EBuildinFileCopyOption copyOption = GetYooBuildinCopyOption(packageName, pipelineName);
        BuildParameters buildParameters = CreateYooBuildParameters(packageName, pipelineName, packageVersion, buildTarget, copyOption);
        IBuildPipeline pipeline = CreateYooBuildPipeline(pipelineName);

        Debug.Log($"YooAsset build start. package={packageName}, pipeline={pipelineName}, version={packageVersion}, copyOption={copyOption}");
        YooBuildResult result = pipeline.Run(buildParameters, true);
        if (!result.Success)
            throw new InvalidOperationException($"YooAsset build failed. task={result.FailedTask}, error={result.ErrorInfo}");

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
        EFileNameStyle fileNameStyle = AssetBundleBuilderSetting.GetPackageFileNameStyle(packageName, pipelineName);
        string buildinFileCopyParams = AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(packageName, pipelineName);
        bool clearBuildCache = AssetBundleBuilderSetting.GetPackageClearBuildCache(packageName, pipelineName);
        bool useAssetDependencyDB = AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipelineName);

        if (pipelineName == nameof(EBuildPipeline.RawFileBuildPipeline))
        {
            return ApplyCommonYooParameters(new RawFileBuildParameters(), packageName, pipelineName, packageVersion,
                buildTarget, (int)EBuildBundleType.RawBundle, fileNameStyle, copyOption, buildinFileCopyParams,
                clearBuildCache, useAssetDependencyDB);
        }

        ECompressOption compressOption = AssetBundleBuilderSetting.GetPackageCompressOption(packageName, pipelineName);
        if (pipelineName == nameof(EBuildPipeline.BuiltinBuildPipeline))
        {
            BuiltinBuildParameters parameters = ApplyCommonYooParameters(new BuiltinBuildParameters(), packageName,
                pipelineName, packageVersion, buildTarget, (int)EBuildBundleType.AssetBundle, fileNameStyle, copyOption,
                buildinFileCopyParams, clearBuildCache, useAssetDependencyDB);
            parameters.CompressOption = compressOption;
            return parameters;
        }

        ScriptableBuildParameters scriptableParameters = ApplyCommonYooParameters(new ScriptableBuildParameters(),
            packageName, pipelineName, packageVersion, buildTarget, (int)EBuildBundleType.AssetBundle, fileNameStyle,
            copyOption, buildinFileCopyParams, clearBuildCache, useAssetDependencyDB);
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
            return new RawFileBuildPipeline();
        if (pipelineName == nameof(EBuildPipeline.BuiltinBuildPipeline))
            return new BuiltinBuildPipeline();
        return new ScriptableBuildPipeline();
    }

    private static EBuildinFileCopyOption GetYooBuildinCopyOption(string packageName, string pipelineName)
    {
        string argValue = GetArg("-yooCopyOption", null);
        if (!string.IsNullOrEmpty(argValue) && Enum.TryParse(argValue, true, out EBuildinFileCopyOption argOption))
            return argOption;

        EBuildinFileCopyOption option = AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(packageName, pipelineName);
        return option == EBuildinFileCopyOption.None ? EBuildinFileCopyOption.ClearAndCopyAll : option;
    }

    private static string GetBuiltinShaderBundleName(string packageName)
    {
        bool uniqueBundleName = AssetBundleCollectorSettingData.Setting.UniqueBundleName;
        PackRuleResult packRuleResult = DefaultPackRule.CreateShadersPackRuleResult();
        return packRuleResult.GetBundleName(packageName, uniqueBundleName);
    }

    private static T CreateYooService<T>(string className) where T : class
    {
        Type serviceType = EditorTools.GetAssignableTypes(typeof(T)).FirstOrDefault(type => type.FullName == className);
        return serviceType == null ? null : Activator.CreateInstance(serviceType) as T;
    }

    private static EPlayMode GetBuildYooPlayMode()
    {
        string argValue = GetArg("-yooPlayMode", EPlayMode.OfflinePlayMode.ToString());
        if (Enum.TryParse(argValue, true, out EPlayMode playMode))
            return playMode;

        throw new ArgumentException($"Invalid -yooPlayMode value: {argValue}");
    }

    private static GameStartPlayModeSnapshot SetGameStartPlayMode(EPlayMode playMode)
    {
        var snapshot = new GameStartPlayModeSnapshot();
        foreach (string scenePath in GetEnabledScenes())
        {
            var scene = EditorSceneManager.OpenScene(scenePath);
            bool changed = false;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (GameStart gameStart in root.GetComponentsInChildren<GameStart>(true))
                {
                    snapshot.Record(scenePath, gameStart, gameStart.PlayMode);
                    if (gameStart.PlayMode == playMode)
                        continue;

                    gameStart.PlayMode = playMode;
                    EditorUtility.SetDirty(gameStart);
                    changed = true;
                    Debug.Log($"Set GameStart PlayMode={playMode} in scene {scenePath}");
                }
            }

            if (changed)
                EditorSceneManager.SaveScene(scene);
        }

        return snapshot;
    }

    private sealed class GameStartPlayModeSnapshot
    {
        private readonly System.Collections.Generic.List<Entry> _entries = new System.Collections.Generic.List<Entry>();

        public void Record(string scenePath, GameStart gameStart, EPlayMode playMode)
        {
            _entries.Add(new Entry(scenePath, GetHierarchyPath(gameStart.transform), playMode));
        }

        public void Restore()
        {
            foreach (IGrouping<string, Entry> sceneGroup in _entries.GroupBy(entry => entry.ScenePath))
            {
                var scene = EditorSceneManager.OpenScene(sceneGroup.Key);
                bool changed = false;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (GameStart gameStart in root.GetComponentsInChildren<GameStart>(true))
                    {
                        string hierarchyPath = GetHierarchyPath(gameStart.transform);
                        Entry entry = sceneGroup.FirstOrDefault(item => item.HierarchyPath == hierarchyPath);
                        if (entry == null || gameStart.PlayMode == entry.PlayMode)
                            continue;

                        gameStart.PlayMode = entry.PlayMode;
                        EditorUtility.SetDirty(gameStart);
                        changed = true;
                    }
                }

                if (changed)
                {
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"Restored GameStart PlayMode in scene {sceneGroup.Key}");
                }
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var parts = new System.Collections.Generic.List<string>();
            while (transform != null)
            {
                parts.Add($"{transform.GetSiblingIndex()}:{transform.name}");
                transform = transform.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        private sealed class Entry
        {
            public readonly string ScenePath;
            public readonly string HierarchyPath;
            public readonly EPlayMode PlayMode;

            public Entry(string scenePath, string hierarchyPath, EPlayMode playMode)
            {
                ScenePath = scenePath;
                HierarchyPath = hierarchyPath;
                PlayMode = playMode;
            }
        }
    }

    private static void ApplySigningPasswords()
    {
        string keystorePass = GetArg("-keystorePass", Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS"));
        string keyaliasPass = GetArg("-keyaliasPass", Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASS"));

        if (!string.IsNullOrEmpty(keystorePass))
            PlayerSettings.Android.keystorePass = keystorePass;

        if (!string.IsNullOrEmpty(keyaliasPass))
            PlayerSettings.Android.keyaliasPass = keyaliasPass;
    }

    private static string[] GetEnabledScenes()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled scenes are configured in Build Settings.");

        return scenes;
    }

    private static void ClearBuildOutputPath(string outputPath)
    {
        if (Directory.Exists(outputPath))
            Directory.Delete(outputPath, true);
        else if (File.Exists(outputPath))
            File.Delete(outputPath);
    }

    private static string GetDefaultYooPackageVersion()
    {
        int totalMinutes = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
        return DateTime.Now.ToString("yyyy-MM-dd") + "-" + totalMinutes;
    }

    private static string GetArg(string name, string defaultValue)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return args[i + 1];
        }
        return defaultValue;
    }

    private static bool GetBoolArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        return args.Any(arg => arg == name);
    }
}

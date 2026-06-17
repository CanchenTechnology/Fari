using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CommandLineBuild
{
    public static void BuildIOSProject()
    {
        var outputPath = GetArg("-outputPath", "Builds/iOS");
        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(outputPath);

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = outputPath,
            target = BuildTarget.iOS,
            options = BuildOptions.None
        });

        var summary = report.summary;
        Debug.Log($"iOS build result: {summary.result}, output: {outputPath}, size: {summary.totalSize} bytes");

        if (summary.result != BuildResult.Succeeded)
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
        EditorUserBuildSettings.buildAppBundle = false;

        ApplySigningPasswords();

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        });

        var summary = report.summary;
        Debug.Log($"Android build result: {summary.result}, output: {outputPath}, size: {summary.totalSize} bytes");

        if (summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"Android build failed: {summary.result}");
        }
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

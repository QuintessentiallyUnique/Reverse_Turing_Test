#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class QuestBuildAndRun
{
    private const string MenuRoot = "Tools/Reverse Turing/";
    private const string OutputDir = "Builds/Quest";
    private const string ApkName = "ReverseTuringTest.apk";

    [MenuItem(MenuRoot + "Deploy to Connected Quest")]
    public static void DeployToQuest()
    {
        ApplyQuestPlayerSettings();
        EnsureAndroidBuildTarget();

        Directory.CreateDirectory(OutputDir);
        string outputPath = Path.Combine(OutputDir, ApkName);

        string[] scenePaths = GetScenePathsForBuild();
        if (scenePaths.Length == 0)
        {
            Debug.LogError("Quest Build & Run: no scenes in Build Settings and the active scene is unsaved. Save the scene or add it to File > Build Profiles.");
            return;
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenePaths,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            locationPathName = outputPath,
            options = BuildOptions.AutoRunPlayer | BuildOptions.Development | BuildOptions.AllowDebugging,
        };

        Debug.Log($"Quest Build & Run: building {scenePaths.Length} scene(s) -> {outputPath}");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            long sizeMb = (long)(summary.totalSize / (1024 * 1024));
            Debug.Log($"Quest Build & Run: deployed in {summary.totalTime.TotalSeconds:0.0}s ({sizeMb} MB).");
        }
        else
        {
            Debug.LogError($"Quest Build & Run failed with result {summary.result} after {summary.totalTime.TotalSeconds:0.0}s. Check the Build Report and ensure a Quest is connected via USB with developer mode enabled.");
        }
    }

    [MenuItem(MenuRoot + "Configure Android Player Settings for Quest")]
    public static void ApplyQuestPlayerSettings()
    {
        NamedBuildTarget android = NamedBuildTarget.Android;

        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetApiCompatibilityLevel(android, ApiCompatibilityLevel.NET_Standard);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

        Debug.Log("Quest Build & Run: applied IL2CPP, ARM64, minSdk 29, linear color, landscape-left orientation.");
    }

    private static void EnsureAndroidBuildTarget()
    {
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
        {
            return;
        }

        Debug.Log("Quest Build & Run: switching active build target to Android...");
        bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        if (!switched)
        {
            throw new BuildFailedException("Could not switch to Android build target. Install Android Build Support and the Android NDK via Unity Hub.");
        }
    }

    private static string[] GetScenePathsForBuild()
    {
        List<string> paths = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && !string.IsNullOrEmpty(scene.path))
            {
                paths.Add(scene.path);
            }
        }

        if (paths.Count > 0)
        {
            return paths.ToArray();
        }

        string activeScenePath = EditorSceneManager.GetActiveScene().path;
        return string.IsNullOrEmpty(activeScenePath) ? new string[0] : new[] { activeScenePath };
    }
}
#endif

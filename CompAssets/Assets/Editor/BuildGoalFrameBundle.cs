// Assets/Editor/BuildGoalFrameBundle.cs
// Run via menu: Assets > Build GoalFrame Bundle
// Output: <project>/BundleOutput/goalframe
// Deploy to: <Puck>/Plugins/CompetitiveAdjustments/assets/goalframe

using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildGoalFrameBundle
{
    private const string OutputDir    = "BundleOutput";
    private const string BundleName   = "CompAssets";
    private static readonly string[] BundleAssets =
    {
        "Assets/frame.prefab",
        "Assets/ArenaAndColliders.prefab",
        "Assets/TorsoThin.prefab",
        "Assets/TorsoThin.fbx",  // Include FBX directly so LoadAllAssets<Mesh>() finds the mesh
    };

    [MenuItem("Assets/Build GoalFrame Bundle")]
    public static void Build()
    {
        Directory.CreateDirectory(OutputDir);

        // Stamp the bundle label on each asset so the Inspector reflects the assignment.
        string bundleLower = BundleName.ToLowerInvariant();
        foreach (var assetPath in BundleAssets)
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer != null && importer.assetBundleName != bundleLower)
            {
                importer.assetBundleName = bundleLower;
                importer.SaveAndReimport();
            }
        }

        var builds = new[]
        {
            new AssetBundleBuild
            {
                assetBundleName = BundleName,
                assetNames      = BundleAssets,
            }
        };

        BuildPipeline.BuildAssetBundles(
            OutputDir,
            builds,
            BuildAssetBundleOptions.ForceRebuildAssetBundle,
            BuildTarget.StandaloneWindows64);

        Debug.Log($"[GoalFrame] Bundle written to: {Path.GetFullPath(OutputDir)}/{BundleName}");
    }
}

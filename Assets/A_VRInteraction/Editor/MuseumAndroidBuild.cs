using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Android;

namespace Game4026.Editor
{
    public static class MuseumAndroidBuild
    {
        private const string ScenePath = "Assets/Scenes/Museum.unity";
        private const string DefaultOutput = "Builds/Android/Museum.apk";
        private const string PackageName = "com.game4026.museum";
        private const string EmbeddedSdkPreference = "SdkUseEmbedded";

        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Select Android before building Museum.");

            string[] args = Environment.GetCommandLineArgs();
            bool optimized = Array.IndexOf(args, "-museumQuestOptimized") >= 0;
            int index = Array.IndexOf(args, "-buildOutput");
            string output = Path.GetFullPath(index >= 0 && index + 1 < args.Length ? args[index + 1] : DefaultOutput);
            if (!string.Equals(Path.GetExtension(output), ".apk", StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Museum requires an .apk output path.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            string previousIdentifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            bool previousBundle = EditorUserBuildSettings.buildAppBundle;
            bool previousExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            int previousVersionCode = PlayerSettings.Android.bundleVersionCode;
            string embeddedSdk = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "SDK");
            bool useEmbeddedSdk = optimized && !EditorPrefs.GetBool(EmbeddedSdkPreference, true) &&
                Directory.Exists(Path.Combine(embeddedSdk, "platform-tools"));

            var xrSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (xrSettings == null)
                throw new BuildFailedException("Museum requires Android OpenXR settings.");

            // The Android XR package also registers a hidden native OpenXR lifecycle hook.
            var androidXrAssembly = typeof(AndroidXRSupportFeature).Assembly;
            var enabledAndroidXrFeatures = Array.FindAll(xrSettings.GetFeatures(), feature =>
                feature != null && feature.enabled && feature.GetType().Assembly == androidXrAssembly);

            try
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                SetFeatureState(enabledAndroidXrFeatures, false);

                if (optimized)
                {
                    if (useEmbeddedSdk)
                        EditorPrefs.SetBool(EmbeddedSdkPreference, true);
                    PlayerSettings.Android.bundleVersionCode = Math.Max(2, previousVersionCode);
                    VerifyQuestTextures();
                }

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                });

                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException($"Museum APK build ended with {report.summary.result}.");

                Debug.Log($"Museum APK built from {ScenePath}: {output} ({new FileInfo(output).Length} bytes).");
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, previousIdentifier);
                EditorUserBuildSettings.buildAppBundle = previousBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExport;
                PlayerSettings.Android.bundleVersionCode = previousVersionCode;
                SetFeatureState(enabledAndroidXrFeatures, true);
                if (useEmbeddedSdk)
                    EditorPrefs.SetBool(EmbeddedSdkPreference, false);
            }
        }

        private static void SetFeatureState(OpenXRFeature[] features, bool enabled)
        {
            foreach (var feature in features)
                feature.enabled = enabled;
        }

        private static void VerifyQuestTextures()
        {
            int count = 0;
            var models = AssetDatabase.GetDependencies(ScenePath).Where(path =>
                path.StartsWith("Assets/Models/", StringComparison.Ordinal) &&
                path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase));

            foreach (string path in models)
            {
                foreach (var texture in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>())
                {
                    if (texture.format is TextureFormat.RGB24 or TextureFormat.RGBA32 or TextureFormat.ARGB32)
                        throw new BuildFailedException($"Uncompressed embedded texture {texture.name} in {path}. Run Build-MuseumQuest.ps1.");
                    if (texture.format is TextureFormat.ASTC_4x4 or TextureFormat.ASTC_6x6)
                        count++;
                }
            }

            if (count == 0)
                throw new BuildFailedException("No ASTC exhibit textures were imported.");
            Debug.Log($"Museum preflight found {count} ASTC textures in referenced GLB models.");
        }
    }
}

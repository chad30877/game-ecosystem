using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ecosystem.Editor
{
    public static class BuildWorld
    {
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Resources");
            const string materialPath = "Assets/Resources/WorldMaterial.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) == null)
            {
                var material = new Material(Shader.Find("Sprites/Default"));
                material.color = new Color(0.35f, 0.9f, 0.65f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/World.unity");
            PlayerSettings.companyName = "Ecosystem";
            PlayerSettings.productName = "Ecosystem";
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/World.unity" },
                locationPathName = "Builds/World.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("世界建置失敗：" + report.summary.result);
        }
    }
}

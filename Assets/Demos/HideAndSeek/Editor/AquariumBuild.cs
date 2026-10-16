using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumBuild
    {
        [MenuItem("Aquarium/Build Windows Demo")]
        public static void Windows()
        {
            AquariumCameraChecks.Run();
            string path = Environment.GetEnvironmentVariable("AQUARIUM_BUILD_PATH");
            if (string.IsNullOrEmpty(path)) path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/Aquarium-v0.1/AquariumDemo.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PlayerSettings.companyName = "PrototypeLab"; PlayerSettings.productName = "Aquarium Hide and Seek";
            PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            // Procedural art has no serialized material references for the shader collector.
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var included = graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach (string shaderName in new[] { "Standard", "Sprites/Default" })
            {
                var shader = Shader.Find(shaderName);
                if (shader == null) throw new Exception("Missing shader: " + shaderName);
                bool exists = false;
                for (int i = 0; i < included.arraySize; i++) if (included.GetArrayElementAtIndex(i).objectReferenceValue == shader) exists = true;
                if (!exists) { int index = included.arraySize; included.InsertArrayElementAtIndex(index); included.GetArrayElementAtIndex(index).objectReferenceValue = shader; }
            }
            graphics.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Demos/HideAndSeek/30_HideAndSeek.unity" },
                locationPathName = path, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (result.summary.result != BuildResult.Succeeded) throw new Exception("Aquarium build failed: " + result.summary.result);
            Debug.Log("AQUARIUM_BUILD_OK " + path);
        }
    }
}

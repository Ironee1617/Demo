using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PrototypeLab.Roguelike.Editor
{
    /// <summary>Builds just the tower demo, keeping the host project's settings intact.</summary>
    public static class TowerBuild
    {
        public const string ScenePath = "Assets/Demos/Roguelike/20_Roguelike.unity";

        [MenuItem("Roguelike/Validate Demo")]
        public static void Validate()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Missing tower scene: " + ScenePath);
            if (Resources.Load<Font>("Tower/Font") == null)
                throw new InvalidOperationException("Missing Korean font at Resources/Tower/Font.");
            if (Resources.Load<TextAsset>("Tower/Font-License") == null)
                throw new InvalidOperationException("Missing redistributable font license at Resources/Tower/Font-License.");
            if (Resources.LoadAll<Texture2D>("Tower").Length == 0)
                throw new InvalidOperationException("Missing tower art in Resources/Tower.");
            if (Resources.Load<TextAsset>("Tower/ArtData") == null)
                throw new InvalidOperationException("Missing original weighted-mesh and animation data in Resources/Tower/ArtData.");
            var actorIndices = new HashSet<int> { 0 };
            foreach (CompanionDefinition definition in TowerRun.Definitions)
                actorIndices.Add(definition.ArtIndex);
            foreach (int actor in actorIndices)
            {
                if(TowerMythArt.IsMythIndex(actor))continue;
                string folder = "Tower/Actors/cha_" + actor.ToString("000") + "/";
                if (Resources.Load<Texture2D>(folder + "body") == null || Resources.Load<Texture2D>(folder + "head") == null)
                    throw new InvalidOperationException("Missing original character textures for actor " + actor + ".");
            }
            TowerMythArt.ValidateLibrary();
            foreach (string effect in new[] { "slash_0", "slash_1", "slash_2", "slash_3", "slash_4", "slash_5", "slash_6", "swirl", "arrow" })
                if (Resources.Load<Texture2D>("Tower/Effects/" + effect) == null)
                    throw new InvalidOperationException("Missing original effect texture: " + effect);
            for (int monster = 0; monster < 5; monster++)
                RequireSprite("Tower/Monsters/monster_" + monster);
            // The story campaign's final boss and village props (Tools/TowerArt/StoryPainter).
            foreach (string story in new[] { "nihilos", "shrine", "stele", "signpost", "dummy" })
                if (Resources.Load<Texture2D>("Tower/Story/" + story) == null)
                    throw new InvalidOperationException("Missing story campaign art at Resources/Tower/Story/" + story + " (" +
                        AssetDatabase.GetMainAssetTypeAtPath("Assets/Demos/Roguelike/Resources/Tower/Story/" + story + ".png") + ").");
            if (Resources.Load<Shader>("Tower/Effects/TowerEffectAdditive") == null)
                throw new InvalidOperationException("Missing additive light shader at Resources/Tower/Effects/TowerEffectAdditive.");
            RequireShader("Sprites/Default");
            RequireShader("UI/Default");
            Debug.Log("TOWER_VALIDATE_OK scene, Korean font, art and sprite/UI shaders available.");
        }

        [MenuItem("Roguelike/Build Windows Demo")]
        public static void Windows()
        {
            Validate();
            string path = Environment.GetEnvironmentVariable("TOWER_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(path))
                path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/TowerDemo/TowerDemo.exe"));
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var previous = new PlayerOptions();
            var graphicsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphicsAsset.Length == 0)
                throw new InvalidOperationException("GraphicsSettings.asset is unavailable.");
            var graphics = new SerializedObject(graphicsAsset[0]);
            var included = graphics.FindProperty("m_AlwaysIncludedShaders");
            if (included == null)
                throw new InvalidOperationException("Always Included Shaders setting is unavailable.");
            var previousShaders = new List<UnityEngine.Object>();
            for (int i = 0; i < included.arraySize; i++)
                previousShaders.Add(included.GetArrayElementAtIndex(i).objectReferenceValue);

            try
            {
                PlayerSettings.companyName = "PrototypeLab";
                PlayerSettings.productName = "Towerbound - Companion Roguelike";
                PlayerSettings.defaultScreenWidth = 1600;
                PlayerSettings.defaultScreenHeight = 900;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.runInBackground = true;

                // The renderer creates its materials at runtime, so include their shaders explicitly.
                foreach (string shaderName in new[] { "Sprites/Default", "UI/Default" })
                {
                    Shader shader = RequireShader(shaderName);
                    bool exists = false;
                    for (int i = 0; i < included.arraySize; i++)
                        if (included.GetArrayElementAtIndex(i).objectReferenceValue == shader) exists = true;
                    if (exists) continue;
                    int index = included.arraySize;
                    included.InsertArrayElementAtIndex(index);
                    included.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                }
                graphics.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(graphics.targetObject);

                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = path,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Tower build failed: " + report.summary.result);
                File.Copy(Path.Combine(Application.dataPath, "Demos/Roguelike/Resources/Tower/Font-License.txt"),
                    Path.Combine(Path.GetDirectoryName(path), "Font-License.txt"), true);
                Debug.Log("TOWER_BUILD_OK " + path);
            }
            finally
            {
                previous.Restore();
                graphics.Update();
                included = graphics.FindProperty("m_AlwaysIncludedShaders");
                included.arraySize = previousShaders.Count;
                for (int i = 0; i < previousShaders.Count; i++)
                    included.GetArrayElementAtIndex(i).objectReferenceValue = previousShaders[i];
                graphics.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(graphics.targetObject);
            }
        }

        private static Shader RequireShader(string name)
        {
            Shader shader = Shader.Find(name);
            if (shader == null) throw new InvalidOperationException("Missing shader: " + name);
            return shader;
        }

        private static void RequireSprite(string resource)
        {
            if (Resources.Load<Sprite>(resource) == null)
                throw new InvalidOperationException("Missing sprite or incorrect sprite import: " + resource);
        }

        private sealed class PlayerOptions
        {
            private readonly string company = PlayerSettings.companyName;
            private readonly string product = PlayerSettings.productName;
            private readonly int width = PlayerSettings.defaultScreenWidth;
            private readonly int height = PlayerSettings.defaultScreenHeight;
            private readonly FullScreenMode mode = PlayerSettings.fullScreenMode;
            private readonly bool resizable = PlayerSettings.resizableWindow;
            private readonly bool background = PlayerSettings.runInBackground;

            public void Restore()
            {
                PlayerSettings.companyName = company;
                PlayerSettings.productName = product;
                PlayerSettings.defaultScreenWidth = width;
                PlayerSettings.defaultScreenHeight = height;
                PlayerSettings.fullScreenMode = mode;
                PlayerSettings.resizableWindow = resizable;
                PlayerSettings.runInBackground = background;
            }
        }
    }
}

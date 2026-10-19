using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrototypeLab.Aquarium.Editor
{
    // R17: wider fish maw + meal that fills the opening while it is drawn in.
    // Rebuilds only Fish0 from ArtSources/Cartoon/Export once (the open scene is
    // not touched: work happens in a temporary additive scene). Waits for Edit mode.
    // Menu: Aquarium > Characters > Apply fish0 wide maw (R17)
    [InitializeOnLoad]
    public static class Fish0MawR17
    {
        const string Characters = "Assets/Demos/HideAndSeek/Characters/Resources/AquariumCharacters";
        public const float MouthRadius = .17f;
        static bool waiting;
        static string Export => Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtSources/Cartoon/Export"));
        static string Marker => Path.Combine(Export, "Fish0.r17.applied");

        static Fish0MawR17() { EditorApplication.delayCall += TryAuto; }

        static string Stamp()
        {
            string path = Path.Combine(Export, "Fish0.cartoon");
            if (!File.Exists(path)) return null;
            using (var md5 = System.Security.Cryptography.MD5.Create())
                return BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        static void TryAuto()
        {
            if (Application.isBatchMode) return;
            string stamp = Stamp();
            if (stamp == null) return;
            if (File.Exists(Marker) && File.ReadAllText(Marker).Trim() == stamp) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!waiting) { waiting = true; EditorApplication.playModeStateChanged += OnPlayMode; }
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += TryAuto; return; }
            Apply();
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayMode; waiting = false;
            EditorApplication.delayCall += TryAuto;
        }

        [MenuItem("Aquarium/Characters/Apply fish0 wide maw (R17)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Fish0 R17: exit Play mode first."); return; }
            var active = SceneManager.GetActiveScene();
            var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temp);
            try
            {
                var import = typeof(AquariumCartoonImport);
                var flags = BindingFlags.NonPublic | BindingFlags.Static;
                object data = import.GetMethod("Load", flags).Invoke(null, new object[] { "Fish0" });
                var rig = (Component)import.GetMethod("Rig", flags).Invoke(null, new object[] { data, Characters });
                UnityEngine.Object.DestroyImmediate(rig.gameObject);

                var fish = AssetDatabase.LoadAssetAtPath<GameObject>(Characters + "/Fish0.prefab");
                foreach (var species in Resources.LoadAll<AquariumSpecies>("AquariumCharacters"))
                {
                    species.normal = fish; species.feeding = fish; species.closedFeeding = fish;
                    species.feedingMouthRadius = MouthRadius;
                    EditorUtility.SetDirty(species);
                }

                var root = PrefabUtility.LoadPrefabContents(Characters + "/Fish0.prefab");
                try
                {
                    var morph = root.GetComponent<AquariumMorphMotion>();
                    if (morph == null) morph = root.AddComponent<AquariumMorphMotion>();
                    morph.leave = AssetDatabase.LoadAssetAtPath<AnimationClip>(Characters + "/AlienOctopus/FishOcto_MorphLeave.anim");
                    morph.arrive = AssetDatabase.LoadAssetAtPath<AnimationClip>(Characters + "/AlienOctopus/FishOcto_MorphArrive.anim");
                    PrefabUtility.SaveAsPrefabAsset(root, Characters + "/Fish0.prefab");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }

                AssetDatabase.SaveAssets();
                File.WriteAllText(Marker, Stamp());
                Debug.Log("FISH0_R17_APPLIED wide maw; meal radius " + MouthRadius);
            }
            catch (Exception e) { Debug.LogError("Fish0 R17 failed: " + (e.InnerException ?? e)); }
            finally
            {
                if (active.IsValid()) SceneManager.SetActiveScene(active);
                EditorSceneManager.CloseScene(temp, true);
            }
        }
    }
}

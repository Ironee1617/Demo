using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrototypeLab.Aquarium.Editor
{
    // R22: the six painted fish (blue tang, clownfish, yellow tang, shark, manta ray, flatfish) join Fish0
    // as ordinary fish species 1-6. Each one is built by AquariumCartoonImport from
    // ArtSources/Cartoon/Export exactly like Fish0 (Root/Body rig, Idle/Swim/Bite/... clips, the wide maw,
    // swallow bulge and the shared morph egg), so an alien can disguise as any of them and they are eaten,
    // harpooned and spat like Fish0. Runs once per export revision (marker file), waits for Edit mode and
    // never replaces the open scene. The cute monster alien and Fish0 are not rebuilt.
    // Menu: Aquarium > Characters > Add painted fish species (R22)
    [InitializeOnLoad]
    public static class FishSpeciesR22
    {
        const string Characters = "Assets/Demos/HideAndSeek/Characters/Resources/AquariumCharacters";
        const string Folder = Characters + "/PaintedFish";
        const string Revision = "r25-traits-3";   // R23: + size and habit; R24: + speed, food, shark cruise; R25: small anemone clownfish
        // Species id = index + 1 (species 0 stays Fish0).
        public static readonly string[] Models = { "FishBlueTang", "FishClownfish", "FishYellowTang", "FishShark", "FishMantaRay", "FishFlatfish" };
        // R23: size (Fish0 = 1), collision hull across/along at that size, and habit, per model above.
        static readonly float[] Sizes = { .78f, .6f, .78f, 1.8f, 1.6f, 1f };
        static readonly float[] HullWidths = { 1, 1, 1, .72f, 1, 1 };
        static readonly float[] HullLengths = { 1, 1, 1, 2.2f, 1, 1 };
        static readonly FishHabit[] Habits = { FishHabit.Wander, FishHabit.Anemone, FishHabit.Wander, FishHabit.Cruise, FishHabit.Glide, FishHabit.Bottom };
        // R24: swim speed relative to Fish0 (4 m/s; a disguised alien follows it) and food interest.
        static readonly float[] Speeds = { 1.15f, .75f, 1.1f, .5f, .7f, .45f };
        static readonly bool[] IgnoreFeed = { false, false, false, true, true, false };
        static bool waiting;
        static string Export => Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtSources/Cartoon/Export"));
        static string Marker => Path.Combine(Export, "PaintedFish.r22.applied");
        [Serializable] class Extra { public float mouthRadius; }

        static FishSpeciesR22() { EditorApplication.delayCall += TryAuto; }

        static string Stamp()
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var all = new MemoryStream())
            {
                foreach (var model in Models)
                    foreach (var suffix in new[] { ".cartoon", ".json", "_Atlas.png" })
                    {
                        string path = Path.Combine(Export, model + suffix);
                        if (!File.Exists(path)) return null;
                        var bytes = md5.ComputeHash(File.ReadAllBytes(path)); all.Write(bytes, 0, bytes.Length);
                    }
                return Revision + ":" + BitConverter.ToString(md5.ComputeHash(all.ToArray())).Replace("-", "").ToLowerInvariant();
            }
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

        [MenuItem("Aquarium/Characters/Add painted fish species (R22)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Painted fish R22: exit Play mode first."); return; }
            string stamp = Stamp();
            if (stamp == null) { Debug.LogError("Painted fish R22: exports missing in " + Export); return; }
            var active = SceneManager.GetActiveScene();
            // An unsaved untitled scene cannot take an additive scene. Then the temporary rigs are
            // built in place and removed again (script-made objects do not dirty it).
            bool additive = !string.IsNullOrEmpty(active.path);
            var temp = additive ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive) : active;
            var existing = new System.Collections.Generic.HashSet<GameObject>(active.GetRootGameObjects());
            if (additive) SceneManager.SetActiveScene(temp);
            try
            {
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Characters, "PaintedFish");
                var import = typeof(AquariumCartoonImport);
                var flags = BindingFlags.NonPublic | BindingFlags.Static;
                var leave = AssetDatabase.LoadAssetAtPath<AnimationClip>(Characters + "/AlienOctopus/FishOcto_MorphLeave.anim");
                var arrive = AssetDatabase.LoadAssetAtPath<AnimationClip>(Characters + "/AlienOctopus/FishOcto_MorphArrive.anim");
                var catalog = Resources.LoadAll<AquariumSpecies>("AquariumCharacters");
                for (int i = 0; i < Models.Length; i++)
                {
                    string model = Models[i]; int id = i + 1;
                    EditorUtility.DisplayProgressBar("Painted fish (R22)", model, (float)i / Models.Length);
                    object data = import.GetMethod("Load", flags).Invoke(null, new object[] { model });
                    var rig = (Component)import.GetMethod("Rig", flags).Invoke(null, new object[] { data, Folder });
                    UnityEngine.Object.DestroyImmediate(rig.gameObject);

                    string prefabPath = Folder + "/" + model + ".prefab";
                    var root = PrefabUtility.LoadPrefabContents(prefabPath);
                    try
                    {
                        // The same disguise morph overlay as Fish0.
                        var morph = root.GetComponent<AquariumMorphMotion>();
                        if (morph == null) morph = root.AddComponent<AquariumMorphMotion>();
                        morph.leave = leave; morph.arrive = arrive;
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }

                    var fish = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    AquariumSpecies species = null;
                    foreach (var s in catalog) if (s.speciesId == id) species = s;
                    if (species == null)
                    {
                        species = ScriptableObject.CreateInstance<AquariumSpecies>();
                        species.speciesId = id;
                        AssetDatabase.CreateAsset(species, Characters + "/Species" + id + ".asset");
                    }
                    var extra = JsonUtility.FromJson<Extra>(File.ReadAllText(Path.Combine(Export, model + ".json")));
                    species.normal = fish; species.feeding = fish; species.closedFeeding = fish;
                    species.tint = Color.white;                 // painted: no species tint over the texture
                    species.feedingMouthRadius = extra.mouthRadius > 0 ? extra.mouthRadius : Fish0FeastR18.MouthRadius;
                    species.bodyScale = Sizes[i]; species.hullWidth = HullWidths[i]; species.hullLength = HullLengths[i]; species.habit = Habits[i];
                    species.swimSpeed = Speeds[i]; species.ignoresFeed = IgnoreFeed[i];
                    EditorUtility.SetDirty(species);
                }
                AssetDatabase.SaveAssets();
                File.WriteAllText(Marker, stamp);
                Debug.Log("PAINTED_FISH_R23_APPLIED species 1-" + Models.Length + ": " + string.Join(", ", Models) + " (sizes " + string.Join("/", Array.ConvertAll(Sizes, x => x.ToString("0.##"))) + "); species count " + AquariumSpecies.Count);
            }
            catch (Exception e) { Debug.LogError("Painted fish R22 failed: " + (e.InnerException ?? e)); }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (additive)
                {
                    if (active.IsValid()) SceneManager.SetActiveScene(active);
                    EditorSceneManager.CloseScene(temp, true);
                }
                else foreach (var go in active.GetRootGameObjects()) if (!existing.Contains(go)) UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}

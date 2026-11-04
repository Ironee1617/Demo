using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrototypeLab.Aquarium.Editor
{
    // R57: bakes the "쌍둥이 수조" gallery (AquariumEnvironment.CreateTwin) into Resources/AquariumTwin/TwinGallery.prefab
    // without touching the open scene, renders it, and runs the twin map's host model with bots.
    public static class AquariumTwinGalleryAuthoring
    {
        private const string Root = "Assets/Demos/HideAndSeek/Environment/Resources/AquariumTwin";

        [MenuItem("Aquarium/Twin Tanks/Rebuild twin gallery prefab")]
        public static void RebuildMenu() { Debug.Log(Rebuild()); }
        [MenuItem("Aquarium/Twin Tanks/Rebuild grotto gallery prefab")]
        public static void RebuildGrottoMenu() { Debug.Log(Rebuild(AquariumMaps.Grotto)); }

        public static string Rebuild() { return Rebuild(AquariumMaps.Twin); }
        // R73: the grotto bakes into its own folders and GrottoGallery.prefab.
        public static string Rebuild(int map)
        {
            bool grotto = map == AquariumMaps.Grotto;
            string meshes = Root + (grotto ? "/GrottoMeshes" : "/Meshes"), mats = Root + (grotto ? "/GrottoMaterials" : "/Materials");
            Directory.CreateDirectory(meshes); Directory.CreateDirectory(mats);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewPreviewScene();
            var report = new StringBuilder();
            try
            {
                var holder = new GameObject("Twin bake holder"); SceneManager.MoveGameObjectToScene(holder, scene);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var environment = AquariumEnvironment.CreateTwin(holder.transform, grotto ? AquariumMaps.Grotto : AquariumMaps.Twin);
                report.Append("built in ").Append(watch.ElapsedMilliseconds).Append(" ms\n");
                environment.transform.SetParent(null, false); SceneManager.MoveGameObjectToScene(environment.gameObject, scene);
                int meshIndex = 0, materialIndex = 0, triangles = 0;
                var materials = new Dictionary<Material, Material>();
                foreach (var filter in environment.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = filter.sharedMesh; if (mesh == null) continue;
                    triangles += mesh.triangles.Length / 3;
                    filter.sharedMesh = StoreMesh(mesh, meshes + "/" + (++meshIndex).ToString("000") + ".asset");
                }
                foreach (var renderer in environment.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var source = renderer.sharedMaterial;
                    if (source == null || AssetDatabase.Contains(source)) continue;
                    Material saved;
                    if (!materials.TryGetValue(source, out saved))
                    {
                        string path = mats + "/" + (++materialIndex).ToString("00") + ".mat";
                        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (existing != null) { existing.shader = source.shader; existing.CopyPropertiesFromMaterial(source); existing.name = source.name; EditorUtility.SetDirty(existing); saved = existing; }
                        else { AssetDatabase.CreateAsset(source, path); saved = source; }
                        materials.Add(source, saved);
                    }
                    renderer.sharedMaterial = saved;
                }
                PrefabUtility.SaveAsPrefabAsset(environment.gameObject, Root + (grotto ? "/GrottoGallery.prefab" : "/TwinGallery.prefab"));
                AssetDatabase.SaveAssets();
                report.Append("meshes ").Append(meshIndex).Append(", materials ").Append(materialIndex).Append(", triangles ").Append(triangles)
                    .Append(", bays ").Append(environment.visitorGalleryBays).Append(", bubbles ").Append(environment.bubbleCount).Append('\n');
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return report.ToString();
        }

        private static Mesh StoreMesh(Mesh source, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(source, path); return source; }
            existing.Clear(); existing.indexFormat = source.indexFormat;
            existing.vertices = source.vertices; existing.triangles = source.triangles; existing.normals = source.normals; existing.tangents = source.tangents;
            existing.colors = source.colors; existing.uv = source.uv; existing.uv2 = source.uv2; existing.bounds = source.bounds; existing.name = source.name;
            EditorUtility.SetDirty(existing); return existing;
        }

        // ------------------------------------------------------------------ renders
        public static string RenderViews(string output, string which)
        {
            var names = new List<string>(); var from = new List<Vector3>(); var at = new List<Vector3>(); var fov = new List<float>();
            Action<string, Vector3, Vector3, float> add = (n, f, t, v) => { names.Add(n); from.Add(f); at.Add(t); fov.Add(v); };
            if (which.Contains("overview")) add("overview", new Vector3(0, 100, -66), new Vector3(0, 4, 1), 43);
            if (which.Contains("tankA")) add("tankA", new Vector3(-40, 4.2f, -10), new Vector3(-14, 4, 2), 70);
            if (which.Contains("tunnel")) add("tunnel", new Vector3(-17, 3.2f, .4f), new Vector3(2, 3.6f, 0), 70);
            if (which.Contains("inside")) add("inside", new Vector3(-6, 2.6f, -1.5f), new Vector3(6, 4.2f, 1.2f), 72);
            if (which.Contains("tankB")) add("tankB", new Vector3(42, 5, 9), new Vector3(14, 3.5f, -2), 70);
            if (which.Contains("hall")) add("hall", new Vector3(-4, 1.7f, 8.0f), new Vector3(5, 3.6f, 0), 72);
            if (which.Contains("portal")) add("portal", new Vector3(-2, 6.5f, 7.6f), new Vector3(-11, 6.5f, 0), 70);
            if (which.Contains("dive")) add("dive", new Vector3(-36, 20, 33), new Vector3(-25, 11.5f, 14), 55);
            if (which.Contains("gallery")) add("gallery", new Vector3(-24, 1.7f, -21.5f), new Vector3(-34, 2.8f, -12), 70);
            if (which.Contains("deck")) add("deck", new Vector3(8, 14, -14), new Vector3(-6, 12.5f, 0), 62);
            return AquariumMapQA.Render(AquariumEnvironment.TwinResourcePath, output, names.ToArray(), from.ToArray(), at.ToArray(), fov.ToArray());
        }

        // ------------------------------------------------------------------ host model with bots
        public static AquariumModel Sim;
        private static Dictionary<string, int> side = new Dictionary<string, int>();
        private static int fishCrossings, playerCrossings, violations, ticks, tunnelVisits; private static string firstViolation = "";
        private static double tickMs;
        public static string SimStart(int map, int seed)
        {
            Sim = new AquariumModel(seed, AquariumSpecies.Count, seed * 7 + 1) { DiveEntry = true };
            Sim.SetMap(map);
            Sim.AddPlayer(1, "AI 잠수부", Team.Seeker, true);
            for (int i = 2; i <= 5; i++) Sim.AddPlayer(i, "AI 외계인 " + i, Team.Hider, true);
            side.Clear(); fishCrossings = playerCrossings = violations = ticks = tunnelVisits = 0; firstViolation = ""; tickMs = 0;
            bool started = Sim.StartRound();
            var sb = new StringBuilder("map " + AquariumMaps.Current + " started=" + started + " drains:");
            foreach (var d in Sim.Snapshot().drains) sb.Append(string.Format(" ({0:0.0},{1:0.0},{2:0.00})", d.position.x, d.position.z, d.position.y));
            return sb.ToString();
        }
        private static void Check(string key, Point p, float pad)
        {
            bool bad = !AquariumModel.InsideTank(p, Math.Max(0, pad - .05f)) || p.y > AquariumModel.WaterHeight + .01f || p.y < AquariumSeabed.Height(p) - .05f
                || (AquariumMaps.IsTwin && AquariumTwinMap.AboveRoof(p, Math.Max(0, pad - .05f)));
            if (bad) { violations++; if (firstViolation.Length == 0) firstViolation = key + " at " + p.x.ToString("0.00") + "," + p.y.ToString("0.00") + "," + p.z.ToString("0.00") + " t=" + Sim.Time.ToString("0.0"); }
            if (!AquariumMaps.IsTwin) return;
            int s = AquariumTwinMap.TankOf(p); if (s < 0) { if (key[0] == 'f' && ticks % 30 == 0) tunnelVisits++; return; }
            int old; if (side.TryGetValue(key, out old) && old != s) { if (key[0] == 'f') fishCrossings++; else playerCrossings++; }
            side[key] = s;
        }
        public static string SimRun(float seconds)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew(); int n = Mathf.RoundToInt(seconds * 30);
            for (int i = 0; i < n && Sim.Phase == RoundPhase.Playing; i++)
            {
                Sim.Tick(1f / 30); ticks++;
                foreach (var f in Sim.Fish) if (!f.eaten && !f.dead) Check("f" + f.id, f.position, .1f);
                foreach (var p in Sim.Players) if (p.alive && !p.escaped && (int)p.drainTask == 0 && p.swallowedBy == 0 && (p.entryAt <= 0 || Sim.Time >= p.entryAt)) Check("p" + p.id, p.position, .2f);
            }
            tickMs += watch.Elapsed.TotalMilliseconds;
            return string.Format("t={0:0.0} phase={1} winner={2} reason={3} ticks={4} ms/tick={5:0.00} violations={6} first=[{7}] fishCross={8} playerCross={9} tunnelFishSamples={10} alive={11}",
                Sim.Time, Sim.Phase, Sim.Winner, Sim.WinReason, ticks, tickMs / Math.Max(1, ticks), violations, firstViolation, fishCrossings, playerCrossings, tunnelVisits,
                Sim.Players.Count(p => p.alive));
        }
    }
}

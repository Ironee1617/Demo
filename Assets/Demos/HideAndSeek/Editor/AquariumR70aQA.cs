using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrototypeLab.Aquarium.Editor
{
    // R70a: finds rock tops that show the sand through them (a hole in the drawn rock where the rock field says
    // there is stone). Preview scene only: the twin gallery with a seed's scenery, colliders on the drawn rocks,
    // the joins and the sand bed, and rays straight down over every rock's footprint.
    public static class AquariumR70aQA
    {
        public sealed class Hole { public int seed, rock, kind, samples; public Vector3 at; public float drop; public string hit; }
        public static readonly List<Hole> LastHoles = new List<Hole>();

        public static string ScanTwin(int from, int to, float step = .3f)
        {
            var log = new StringBuilder(); LastHoles.Clear();
            int oldMap = AquariumMaps.Current, oldSeed = AquariumSceneryLayout.Current.Seed;
            var scene = EditorSceneManager.NewPreviewScene();
            AquariumSceneryPresentation presentation = null;
            int total = 0, totalRocks = 0;
            try
            {
                var root = new GameObject("R70a QA"); SceneManager.MoveGameObjectToScene(root, scene);
                var prefab = Resources.Load<GameObject>(AquariumEnvironment.TwinResourcePath);
                var environment = UnityEngine.Object.Instantiate(prefab, root.transform).GetComponent<AquariumEnvironmentRoot>();
                AquariumMaps.Select(AquariumMaps.Twin);
                presentation = new AquariumSceneryPresentation(environment);
                var physics = scene.GetPhysicsScene();
                for (int seed = from; seed <= to; seed++)
                {
                    presentation.Sync(seed);
                    var colliders = new List<MeshCollider>();
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>(false))
                    {
                        var n = r.name; if (!(n.StartsWith("R58 • Twin rocks") || n.StartsWith("R64") || n.StartsWith("01 • "))) continue;
                        var c = r.gameObject.AddComponent<MeshCollider>(); c.sharedMesh = r.GetComponent<MeshFilter>().sharedMesh; colliders.Add(c);
                    }
                    var set = AquariumSceneryLayout.Current.TwinRocks; int found = 0;
                    for (int i = 0; i < set.Count; i++)
                    {
                        var centre = set.Centre(i); float reach = set.Reach(i); Hole hole = null;
                        for (float x = centre.x - reach; x <= centre.x + reach; x += step)
                            for (float z = centre.z - reach; z <= centre.z + reach; z += step)
                            {
                                if (set.RockDistance(i, new Point(x, centre.y, z)) > reach) continue;
                                float ground = AquariumSeabed.TwinHeight(x, z), top = float.NaN, y = centre.y + reach + .3f;
                                for (int it = 0; it < 60 && y > ground; it++)
                                {
                                    float d = set.RockDistance(i, new Point(x, y, z));
                                    if (d < .02f) { top = y; break; }
                                    y -= Mathf.Max(d, .03f);
                                }
                                if (float.IsNaN(top) || top < ground + .3f) continue;
                                // The union's surface above (another rock may sit on this one).
                                float union = set.Distance(new Point(x, top, z));
                                if (union < -.05f) continue;
                                // A top, not the edge of a wall: facing up, solid stone under it.
                                var normal = set.Normal(new Point(x, top, z));
                                if (normal.y < .55f || set.RockDistance(i, new Point(x, top - .45f, z)) > -.2f) continue;
                                RaycastHit hit;
                                bool any = physics.Raycast(new Vector3(x, top + 2.5f, z), Vector3.down, out hit, 20f);
                                float drop = any ? top - hit.point.y : 99;
                                if (drop < .3f) continue;
                                if (hole == null) { hole = new Hole { seed = seed, rock = i, kind = set.Rocks[i].kind, at = new Vector3(x, top, z), hit = any ? hit.collider.name : "nothing" }; }
                                hole.samples++;
                                if (drop > hole.drop) { hole.drop = drop; hole.at = new Vector3(x, top, z); hole.hit = any ? hit.collider.name : "nothing"; }
                            }
                        if (hole != null && hole.samples >= 2) { LastHoles.Add(hole); found++; log.AppendLine("  seed " + seed + " rock " + i + " kind " + hole.kind + " samples " + hole.samples + " drop " + hole.drop.ToString("0.00") + " at " + hole.at.ToString("F1") + " hit " + Short(hole.hit)); }
                    }
                    total += found; totalRocks += set.Count;
                    log.AppendLine("seed " + seed + " rocks " + set.Count + " holes " + found);
                    foreach (var c in colliders) UnityEngine.Object.DestroyImmediate(c);
                }
            }
            finally
            {
                if (presentation != null) presentation.Dispose();
                EditorSceneManager.ClosePreviewScene(scene);
                AquariumMaps.Select(oldMap); AquariumSceneryLayout.Apply(oldSeed);
            }
            log.AppendLine("TOTAL holes " + total + " over " + totalRocks + " rocks");
            Directory.CreateDirectory("D:/Demo/Temp/ClaudeQA");
            File.AppendAllText("D:/Demo/Temp/ClaudeQA/R70a_scan.txt", "== twin " + from + ".." + to + " " + DateTime.Now + "\n" + log);
            return log.ToString();
        }
        private static string Short(string s) { return s.Length > 28 ? s.Substring(0, 28) : s; }

        // Renders views (AquariumR58QA spec) of a map and seed.
        public static string Render(int map, int seed, string spec, int width = 1280, int height = 800)
        {
            return AquariumR58QA.RenderGallery(map, seed, "D:/Demo/Temp/ClaudeQA", spec, width, height);
        }
        private sealed class Undo : IDisposable
        {
            public AquariumSceneryPresentation presentation; public int map, seed;
            public void Dispose() { if (presentation != null) presentation.Dispose(); AquariumMaps.Select(map); AquariumSceneryLayout.Apply(seed); }
        }
        // As Render, with renderers whose name starts with one of hide (comma separated) switched off.
        public static string RenderHiding(int map, int seed, string spec, string hide, int width = 1280, int height = 800)
        {
            var prefixes = string.IsNullOrEmpty(hide) ? new string[0] : hide.Split(',');
            Func<Transform, IDisposable> build = root =>
            {
                var undo = new Undo { map = AquariumMaps.Current, seed = AquariumSceneryLayout.Current.Seed };
                var prefab = Resources.Load<GameObject>(map == AquariumMaps.Twin ? AquariumEnvironment.TwinResourcePath : AquariumEnvironment.ResourcePath);
                var environment = UnityEngine.Object.Instantiate(prefab, root).GetComponent<AquariumEnvironmentRoot>();
                AquariumMaps.Select(map);
                undo.presentation = new AquariumSceneryPresentation(environment); undo.presentation.Sync(seed);
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    foreach (var p in prefixes) if (p.Length > 0 && r.name.StartsWith(p)) { r.enabled = false; break; }
                return undo;
            };
            return AquariumR58QA.Render("D:/Demo/Temp/ClaudeQA", build, AquariumR58QA.Views(spec), width, height);
        }
    }
}

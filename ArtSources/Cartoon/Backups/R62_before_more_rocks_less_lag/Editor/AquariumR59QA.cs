using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    // R59: checks and renders for the twin tanks' giant formations (preview scenes only): a lineup of the six
    // giants, their swim-throughs tested against the collision field, layout statistics over many seeds, and
    // the twin map's lobby thumbnail framed with a margin round the building.
    public static class AquariumR59QA
    {
        // The six giants (both variants) on a sand slab.
        public static string RenderGiantLineup(string output, string spec, int width = 1800, int height = 1000)
        {
            Func<Transform, IDisposable> build = root =>
            {
                var rockMaterial = AquariumEnvironment.TwinRockMaterial();
                var sand = GameObject.CreatePrimitive(PrimitiveType.Plane); sand.transform.SetParent(root, false);
                sand.transform.localScale = new Vector3(14, 1, 7); sand.transform.position = new Vector3(0, 0, 10);
                var sandMat = new Material(Shader.Find("Aquarium/Natural Granular Sand")) { hideFlags = HideFlags.HideAndDontSave };
                sandMat.SetTexture("_MainTex", Resources.Load<Texture2D>("Aquarium/Textures/SandGrainAlbedo")); sandMat.SetTexture("_NormalTex", Resources.Load<Texture2D>("Aquarium/Textures/SandGrainNormal"));
                sandMat.SetTexture("_ARM", Resources.Load<Texture2D>("Aquarium/Textures/SandGrainARM")); sandMat.SetFloat("_WorldScale", .48f); sandMat.SetColor("_Color", AquariumEnvironment.C(0xe8d6b6));
                sand.GetComponent<Renderer>().sharedMaterial = sandMat;
                for (int variant = 0; variant < 2; variant++)
                    for (int g = 0; g < 6; g++)
                    {
                        int kind = AquariumRockShapes.FirstGiant + g;
                        var go = new GameObject(AquariumRockShapes.Names[kind]); go.transform.SetParent(root, false);
                        go.transform.position = new Vector3(-50 + g * 20f, -.35f, variant * 22f); go.transform.rotation = Quaternion.Euler(0, 15 + 30 * variant, 0);
                        go.AddComponent<MeshFilter>().sharedMesh = AquariumEnvironment.TwinRockMesh(kind, variant); go.AddComponent<MeshRenderer>().sharedMaterial = rockMaterial;
                    }
                return null;
            };
            return AquariumR58QA.Render(output, build, AquariumR58QA.Views(spec), width, height, new Color(.06f, .3f, .36f));
        }

        // Every swim-through of every giant (both variants, a few yaws) against the collision field with the
        // diver's radius: through-passages from one side to the other, the niche from outside to its back.
        public static string CheckPassages()
        {
            var log = new StringBuilder(); int fails = 0;
            var centres = new List<Point>(); var dirs = new List<Point>(); var through = new List<bool>();
            for (int kind = AquariumRockShapes.FirstGiant; kind < AquariumRockShapes.Count; kind++)
                for (int v = 0; v < 2; v++)
                {
                    AquariumRockShapes.Passages(kind, v, centres, dirs, through);
                    for (int i = 0; i < centres.Count; i++)
                    {
                        float bestClear = 0;
                        // The widest clearance across the passage at its centre plane (how much room is left).
                        for (float y = .8f; y < 4.5f; y += .2f)
                        {
                            var c = centres[i]; c.y = y;
                            bestClear = Math.Max(bestClear, AquariumRockShapes.Local(kind, v, c));
                        }
                        var start = through[i] ? centres[i] - dirs[i] * 5 : centres[i] + dirs[i] * 6; var end = through[i] ? centres[i] + dirs[i] * 5 : centres[i];
                        bool open = false; float bestY = 0;
                        for (float y = 1.0f; y < 4.6f && !open; y += .1f)
                        {
                            var a = start; a.y = y; var b = end; b.y = y;
                            if (SegmentClear(kind, v, a, b, .55f)) { open = true; bestY = y; }
                        }
                        if (!open) fails++;
                        log.Append(AquariumRockShapes.Names[kind]).Append('/').Append(v).Append(" passage ").Append(i).Append(open ? " open at y " + bestY.ToString("0.0") : " BLOCKED")
                            .Append(" clearance ").Append(bestClear.ToString("0.00")).Append('\n');
                    }
                }
            log.Insert(0, "passage fails " + fails + "\n");
            return log.ToString();
        }
        private static bool SegmentClear(int kind, int v, Point a, Point b, float radius)
        {
            var d = b - a; float length = d.Length; int steps = (int)Math.Ceiling(length / .1f);
            for (int s = 0; s <= steps; s++) if (AquariumRockShapes.Local(kind, v, a + d * (s / (float)steps)) < radius) return false;
            return true;
        }

        // Giant counts, kinds, heights and the other rocks over a run of seeds (twin map selected meanwhile).
        public static string LayoutStats(int firstSeed, int count)
        {
            var log = new StringBuilder(); int oldMap = AquariumMaps.Current, oldSeed = AquariumSceneryLayout.Current.Seed;
            try
            {
                AquariumMaps.Select(AquariumMaps.Twin);
                for (int seed = firstSeed; seed < firstSeed + count; seed++)
                {
                    AquariumSceneryLayout.Apply(seed);
                    var layout = AquariumSceneryLayout.Current; var rocks = layout.TwinRocks.Rocks;
                    int giants = 0, a = 0, b = 0; float top = 0; var kinds = new StringBuilder();
                    foreach (var r in rocks)
                    {
                        if (!AquariumRockShapes.IsGiant(r.kind)) continue;
                        giants++; if (r.position.x < 0) a++; else b++;
                        top = Math.Max(top, r.position.y + AquariumRockShapes.LocalMax[r.kind].y * r.scale);
                        kinds.Append(AquariumRockShapes.Names[r.kind].Replace("Giant ", "")).Append(r.position.x < 0 ? "(A " : "(B ").Append(r.position.x.ToString("0")).Append(',').Append(r.position.z.ToString("0")).Append(") ");
                    }
                    log.Append("seed ").Append(seed).Append(": giants ").Append(giants).Append(" A").Append(a).Append(" B").Append(b).Append(" top ").Append(top.ToString("0.0"))
                        .Append(" rocks ").Append(rocks.Length).Append(" corals ").Append(layout.Corals.Length).Append(" anemones ").Append(layout.Anemones.Length)
                        .Append(" plants ").Append(layout.Plants.Length).Append(" details ").Append(layout.Details.Length).Append(" drains ").Append(layout.DrainSites == null ? 0 : layout.DrainSites.Length)
                        .Append(" | ").Append(kinds).Append('\n');
                }
            }
            finally { AquariumMaps.Select(oldMap); AquariumSceneryLayout.Apply(oldSeed); }
            return log.ToString();
        }

        // The twin map's lobby picture: the whole building with a margin (the R58 one cut the tanks' ends).
        public static string RenderTwinThumbnail(int twinSeed, string spec)
        {
            string temp = "D:/Demo/Temp/ClaudeQA/R59_thumbs";
            var log = AquariumR58QA.RenderGallery(AquariumMaps.Twin, twinSeed, temp, spec, 960, 600);
            string path = AquariumR58QA.ThumbnailFolder + "/Map1.png";
            File.Copy(temp + "/Map1.png", path, true);
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false; importer.sRGBTexture = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.maxTextureSize = 1024; importer.alphaSource = TextureImporterAlphaSource.None;
                importer.SaveAndReimport();
            }
            return log;
        }

        // A whole twin game in the editor (no Play mode): a bot diver and four bot aliens on this drain seed's
        // scenery, ticked at 30 Hz. Reports host tick time, anybody out of the water or inside rock, bots that
        // stay put, and how often bots and fish swim through the giants' passages; plus bot paths through them.
        public static string SimulateTwin(int drainSeed, float seconds)
        {
            var log = new StringBuilder(); int oldMap = AquariumMaps.Current, oldSeed = AquariumSceneryLayout.Current.Seed;
            try
            {
                var model = new AquariumModel(drainSeed * 7 + 1, 3, drainSeed);
                model.SetMap(AquariumMaps.Twin);
                model.AddPlayer(1, "A", Team.Hider, true); model.AddPlayer(2, "D", Team.Seeker, true);
                if (!model.StartRound()) return "start failed: " + model.StartProblem();
                // Wait for the bots' grid (worked out on a worker thread).
                var navType = typeof(AquariumBotNav); var linked = navType.GetField("linkedFor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var waitWatch = System.Diagnostics.Stopwatch.StartNew();
                while ((int)linked.GetValue(null) != AquariumSceneryLayout.Version && waitWatch.ElapsedMilliseconds < 9000) System.Threading.Thread.Sleep(20);
                log.Append("seed ").Append(drainSeed).Append(" scenery ").Append(model.ScenerySeed).Append(" nav ").Append(waitWatch.ElapsedMilliseconds).Append(" ms");
                var rocks = AquariumSceneryLayout.Current.TwinRocks;
                var lanes = new List<Point>(); var giants = new StringBuilder();
                foreach (var r in rocks.Rocks) if (AquariumRockShapes.IsGiant(r.kind))
                {
                    giants.Append(AquariumRockShapes.Names[r.kind].Replace("Giant ", "")).Append(' ');
                    var c = new List<Point>(); var d = new List<Point>(); var t = new List<bool>();
                    AquariumRockShapes.Passages(r.kind, r.variant, c, d, t);
                    for (int i = 0; i < c.Count; i++) lanes.Add(AquariumRockShapes.ToWorld(r, c[i]));
                }
                log.Append(" giants: ").Append(giants).Append('\n');
                // Bot paths through each passage: from 6 m before to 6 m after (or into the niche).
                int pathThrough = 0, pathTotal = 0; var path = new List<Point>();
                foreach (var r in rocks.Rocks) if (AquariumRockShapes.IsGiant(r.kind))
                {
                    var c = new List<Point>(); var d = new List<Point>(); var t = new List<bool>();
                    AquariumRockShapes.Passages(r.kind, r.variant, c, d, t);
                    for (int i = 0; i < c.Count; i++)
                    {
                        var centre = AquariumRockShapes.ToWorld(r, c[i]); var dir = AquariumRockShapes.DirectionToWorld(r, d[i]);
                        var a = centre - dir * 6; var b = centre + dir * 6; if (!t[i]) a = centre;
                        a.y = b.y = 2.2f;
                        if (!AquariumModel.InsideTank(a, 1) || !AquariumModel.InsideTank(b, 1)) continue;
                        pathTotal++;
                        if (AquariumBotNav.Find(a, b, path))
                        {
                            float best = 99; var prev = a;
                            foreach (var q in path) { best = Math.Min(best, SegmentDistance(centre, prev, q)); prev = q; }
                            if (best < 2.2f) pathThrough++;
                        }
                    }
                }
                log.Append("nav paths through passages ").Append(pathThrough).Append('/').Append(pathTotal).Append('\n');
                int ticks = (int)(seconds * 30); double total = 0, worst = 0; int outOfWater = 0, inRock = 0, fishInRock = 0, laneVisitsBots = 0, laneVisitsFish = 0, exceptions = 0;
                var last = new Dictionary<int, Point>(); var still = new Dictionary<int, float>(); float maxStill = 0; string stillWho = "";
                var watch = new System.Diagnostics.Stopwatch();
                string firstError = null;
                for (int k = 0; k < ticks; k++)
                {
                    watch.Reset(); watch.Start();
                    try { model.Tick(1f / 30); } catch (Exception e) { exceptions++; if (firstError == null) firstError = e.ToString(); }
                    watch.Stop(); double ms = watch.Elapsed.TotalMilliseconds; total += ms; worst = Math.Max(worst, ms);
                    if (k % 6 != 0) continue;
                    foreach (var p in model.Players)
                    {
                        if (!p.alive || p.swallowedBy != 0 || p.entryAt > model.Time) continue;
                        var q = p.position;
                        if (q.y > AquariumModel.WaterHeight + .05f || q.y < AquariumSeabed.Height(q) - .05f || !AquariumModel.InsideTank(q, -.05f)) outOfWater++;
                        if (AquariumMonolithLayout.Distance(q) < -.05f) inRock++;
                        foreach (var l in lanes) if (Flat(q, l) < 1.6f && q.y < 6) { laneVisitsBots++; break; }
                        Point before; if (last.TryGetValue(p.id, out before) && (before - q).Length < .25f) still[p.id] = (still.ContainsKey(p.id) ? still[p.id] : 0) + .2f; else still[p.id] = 0;
                        last[p.id] = q;
                        if (still[p.id] > maxStill) { maxStill = still[p.id]; stillWho = (p.team == Team.Seeker ? "diver" : "alien") + " " + p.id + " at " + q.x.ToString("0.0") + "," + q.y.ToString("0.0") + "," + q.z.ToString("0.0") + " rock " + AquariumMonolithLayout.Distance(q).ToString("0.0") + " m"; }
                    }
                    foreach (var f in model.Fish)
                    {
                        if (AquariumMonolithLayout.Distance(f.position) < -.05f) fishInRock++;
                        foreach (var l in lanes) if (Flat(f.position, l) < 1.6f && f.position.y < 6) { laneVisitsFish++; break; }
                    }
                }
                log.Append("ticks ").Append(ticks).Append(" avg ").Append((total / ticks).ToString("0.000")).Append(" ms worst ").Append(worst.ToString("0.0"))
                    .Append(" | out of water ").Append(outOfWater).Append(" in rock ").Append(inRock).Append(" fish in rock ").Append(fishInRock)
                    .Append(" | longest still ").Append(maxStill.ToString("0.0")).Append(" s (").Append(stillWho).Append(") | passage visits bots ").Append(laneVisitsBots).Append(" fish ").Append(laneVisitsFish)
                    .Append(" | phase ").Append(model.Phase).Append(" exceptions ").Append(exceptions).Append('\n');
                if (firstError != null) log.Append(firstError).Append('\n');
            }
            finally { AquariumMaps.Select(oldMap); AquariumSceneryLayout.Apply(oldSeed); }
            return log.ToString();
        }
        private static float Flat(Point a, Point b) { float x = a.x - b.x, z = a.z - b.z; return (float)Math.Sqrt(x * x + z * z); }
        private static float SegmentDistance(Point p, Point a, Point b)
        {
            var d = b - a; float l2 = Point.Dot(d, d); float t = l2 < 1e-6f ? 0 : Math.Max(0, Math.Min(1, Point.Dot(p - a, d) / l2));
            return (p - (a + d * t)).Length;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // R58: the twin tanks' dressing prototypes: the eight rock shapes (meshed from their own collision field),
    // coral colonies, grass tufts, pebbles, shells and sea stars, and the shared materials they are drawn with.
    // The presentation (AquariumSceneryPresentation) merges them per area for the game's layout.
    public static partial class AquariumEnvironment
    {
        internal sealed class TwinDressingMaterials { public Material rock, stone, coral, kelp, anemone; }
        private static TwinDressingMaterials twinDressing;
        internal static TwinDressingMaterials TwinMaterials()
        {
            if (twinDressing != null && twinDressing.rock != null) return twinDressing;
            var caustics = Resources.Load<Texture2D>("Aquarium/Caustics");
            if (caustics == null) caustics = MakeCaustics();
            var m = new TwinDressingMaterials();
            m.stone = ScannedSurface("Reef stone / 4K mineral detail", "Rock", caustics, .55f, false);
            m.rock = ScannedSurface("Twin rocks / painted mineral, moss and sand dust", "Rock", caustics, .42f, false);
            var painted = Shader.Find("Aquarium/Painted Geology");
            if (painted != null)
            {
                m.rock.shader = painted;
                m.rock.SetColor("_MossColor", C(0x6c7d48)); m.rock.SetColor("_DustColor", C(0xdacba8)); m.rock.SetFloat("_Dust", .55f);
                m.rock.SetFloat("_NormalStrength", .8f);
            }
            m.coral = LivingMaterial("Living coral / six growth forms", caustics, 0, 0, .65f);
            m.kelp = LivingMaterial("Mixed seaweed / anchored continuous current", caustics, 1, 0, .05f);
            m.anemone = LivingMaterial("Anemones / gently moving soft tentacles", caustics, .42f, 1, .1f);
            foreach (var material in new[] { m.rock, m.stone, m.coral, m.kelp, m.anemone }) material.hideFlags = HideFlags.HideAndDontSave;
            return twinDressing = m;
        }

        // ---------------------------------------------------------------- rocks
        // Raw mesh arrays of one rock prototype (no Unity objects, so they are worked out on worker threads).
        internal sealed class RockMeshData { public Vector3[] vertices, normals; public Color[] colors; public int[] triangles; }
        private const float RockStep = .085f;
        private static readonly int[,] RockTets = { { 0, 5, 1, 6 }, { 0, 1, 2, 6 }, { 0, 2, 3, 6 }, { 0, 3, 7, 6 }, { 0, 7, 4, 6 }, { 0, 4, 5, 6 } };
        internal static RockMeshData RockMesh(int kind, int variant)
        {
            var lo = AquariumRockShapes.LocalMin[kind]; var hi = AquariumRockShapes.LocalMax[kind];
            var origin = new Point(lo.x - RockStep, Math.Max(lo.y, -.32f), lo.z - RockStep);
            int nx = (int)Math.Ceiling((hi.x - origin.x) / RockStep) + 2, ny = (int)Math.Ceiling((hi.y - origin.y) / RockStep) + 2, nz = (int)Math.Ceiling((hi.z - origin.z) / RockStep) + 2;
            var field = new float[nx * ny * nz];
            Func<int, Point> at = i => { int x = i % nx, y = (i / nx) % ny, z = i / (nx * ny); return new Point(origin.x + x * RockStep, origin.y + y * RockStep, origin.z + z * RockStep); };
            for (int i = 0; i < field.Length; i++)
            {
                var p = at(i); float d = AquariumRockShapes.Local(kind, variant, p);
                // Closed underneath, well below the sand, so no hole shows on a slope.
                field[i] = Math.Max(d, origin.y + .02f - p.y);
            }
            var positions = new List<Vector3>(); var triangles = new List<int>();
            var edges = new Dictionary<long, int>();
            Func<int, int, int> vertex = (a, b) =>
            {
                if (a > b) { int s = a; a = b; b = s; }
                long key = ((long)a << 32) | (uint)b; int index;
                if (edges.TryGetValue(key, out index)) return index;
                float t = Math.Max(.0002f, Math.Min(.9998f, field[a] / (field[a] - field[b])));
                var p = at(a) + (at(b) - at(a)) * t;
                index = positions.Count; positions.Add(new Vector3(p.x, p.y, p.z));
                edges[key] = index; return index;
            };
            var ids = new int[8]; var inside = new int[4]; var outside = new int[4];
            for (int z = 0; z < nz - 1; z++) for (int y = 0; y < ny - 1; y++) for (int x = 0; x < nx - 1; x++)
            {
                int a = (z * ny + y) * nx + x;
                ids[0] = a; ids[1] = a + 1; ids[2] = a + 1 + nx * ny; ids[3] = a + nx * ny;
                ids[4] = a + nx; ids[5] = a + nx + 1; ids[6] = a + nx + 1 + nx * ny; ids[7] = a + nx + nx * ny;
                bool positive = false, negative = false;
                for (int i = 0; i < 8; i++) { positive |= field[ids[i]] >= 0; negative |= field[ids[i]] < 0; }
                if (!positive || !negative) continue;
                for (int t = 0; t < 6; t++)
                {
                    int ni = 0, no = 0;
                    for (int c = 0; c < 4; c++) { int id = ids[RockTets[t, c]]; if (field[id] < 0) inside[ni++] = id; else outside[no++] = id; }
                    if (ni == 0 || no == 0) continue;
                    Point inner = new Point(), outer = new Point();
                    for (int c = 0; c < ni; c++) inner += at(inside[c]);
                    for (int c = 0; c < no; c++) outer += at(outside[c]);
                    var direction = outer / no - inner / ni; var outward = new Vector3(direction.x, direction.y, direction.z);
                    Action<int, int, int> tri = (i0, i1, i2) =>
                    {
                        if (Vector3.Dot(Vector3.Cross(positions[i1] - positions[i0], positions[i2] - positions[i0]), outward) < 0) { int s = i1; i1 = i2; i2 = s; }
                        triangles.Add(i0); triangles.Add(i1); triangles.Add(i2);
                    };
                    if (ni == 1) tri(vertex(inside[0], outside[0]), vertex(inside[0], outside[1]), vertex(inside[0], outside[2]));
                    else if (ni == 3) tri(vertex(outside[0], inside[0]), vertex(outside[0], inside[1]), vertex(outside[0], inside[2]));
                    else
                    {
                        int v0 = vertex(inside[0], outside[0]), v1 = vertex(inside[0], outside[1]), v2 = vertex(inside[1], outside[1]), v3 = vertex(inside[1], outside[0]);
                        tri(v0, v1, v2); tri(v0, v2, v3);
                    }
                }
            }
            return Cluster(kind, variant, origin.y + .02f, positions, triangles);
        }
        // Vertex clustering: the fine lattice surface is welded onto a coarser grid (each cluster's mean settled
        // back onto the field), which keeps the silhouette and cuts the triangle count about three times.
        private const float RockCluster = .16f;
        private static float Capped(int kind, int variant, float floor, Point p) { return Math.Max(AquariumRockShapes.Local(kind, variant, p), floor - p.y); }
        private static Point CappedNormal(int kind, int variant, float floor, Point p)
        {
            const float e = .012f;
            return new Point(Capped(kind, variant, floor, new Point(p.x + e, p.y, p.z)) - Capped(kind, variant, floor, new Point(p.x - e, p.y, p.z)),
                Capped(kind, variant, floor, new Point(p.x, p.y + e, p.z)) - Capped(kind, variant, floor, new Point(p.x, p.y - e, p.z)),
                Capped(kind, variant, floor, new Point(p.x, p.y, p.z + e)) - Capped(kind, variant, floor, new Point(p.x, p.y, p.z - e))).Normalized;
        }
        private static RockMeshData Cluster(int kind, int variant, float floor, List<Vector3> positions, List<int> triangles)
        {
            var lookup = new Dictionary<long, int>(); var sum = new List<Vector3>(); var count = new List<int>(); var remap = new int[positions.Count];
            for (int i = 0; i < positions.Count; i++)
            {
                var p = positions[i];
                long key = ((long)Mathf.FloorToInt(p.x / RockCluster + 512) << 40) | ((long)Mathf.FloorToInt(p.y / RockCluster + 512) << 20) | (long)Mathf.FloorToInt(p.z / RockCluster + 512);
                int index; if (!lookup.TryGetValue(key, out index)) { index = sum.Count; lookup[key] = index; sum.Add(Vector3.zero); count.Add(0); }
                sum[index] += p; count[index]++; remap[i] = index;
            }
            var vertices = new Vector3[sum.Count]; var normals = new Vector3[sum.Count]; var colors = new Color[sum.Count];
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = sum[i] / count[i]; var p = new Point(v.x, v.y, v.z);
                for (int k = 0; k < 3; k++) { float d = Capped(kind, variant, floor, p); var n = CappedNormal(kind, variant, floor, p); p = p - n * d; }
                var normal = CappedNormal(kind, variant, floor, p);
                vertices[i] = new Vector3(p.x, p.y, p.z); normals[i] = new Vector3(normal.x, normal.y, normal.z); colors[i] = RockColour(kind, variant, p, normal);
            }
            var kept = new List<int>(triangles.Count / 2); var seen = new HashSet<long>();
            for (int t = 0; t < triangles.Count; t += 3)
            {
                int a = remap[triangles[t]], b = remap[triangles[t + 1]], c = remap[triangles[t + 2]];
                if (a == b || b == c || a == c) continue;
                int lo = Math.Min(a, Math.Min(b, c)), hi = Math.Max(a, Math.Max(b, c)), mid = a + b + c - lo - hi;
                long key = ((long)lo << 42) | ((long)mid << 21) | (long)hi;
                if (!seen.Add(key)) continue;
                // Wound outward (the welded surface can fold a sliver over; it then faces the field's normal again).
                var face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (Vector3.Dot(face, normals[a] + normals[b] + normals[c]) < 0) { int s = b; b = c; c = s; }
                kept.Add(a); kept.Add(b); kept.Add(c);
            }
            return new RockMeshData { vertices = vertices, normals = normals, colors = colors, triangles = kept.ToArray() };
        }
        // Painted variation: soft occlusion in the creases, broad warm and cool patches, moss (alpha) on the
        // upper faces and in the hollows.
        private static Color RockColour(int kind, int variant, Point p, Point n)
        {
            float probe = AquariumRockShapes.Local(kind, variant, p + n * .32f);
            float ao = Math.Max(0, Math.Min(1, probe / .32f));
            float shade = .64f + .36f * ao;
            float patch = AquariumRockShapes.Noise(p.x * .7f + kind * 5.3f, p.y * .7f, p.z * .7f + variant * 3.1f);
            float speck = AquariumRockShapes.Noise(p.x * 3.3f, p.y * 3.3f + 7, p.z * 3.3f);
            float tone = (.9f + .2f * patch) * (.95f + .1f * speck);
            float warm = AquariumRockShapes.Noise(p.x * .45f + 13, p.y * .45f, p.z * .45f - 9);
            var c = new Color(shade * tone * (1.0f + .07f * warm), shade * tone * (.99f + .02f * warm), shade * tone * (.97f - .05f * warm), 0);
            float up = Mathf.Clamp01((n.y - .3f) / .5f);
            float mossNoise = Mathf.Clamp01((AquariumRockShapes.Noise(p.x * 1.7f + 11, p.y * 1.7f, p.z * 1.7f + 4) - .42f) / .25f);
            c.a = Mathf.Clamp01(up * mossNoise * .55f + (1 - ao) * .25f * mossNoise);
            return c;
        }

        private static readonly Dictionary<int, RockMeshData> rockData = new Dictionary<int, RockMeshData>();
        private static readonly object rockLock = new object();
        private static volatile bool rocksWarming;
        // Works the rock meshes out on worker threads ahead of the first twin game (about a second in all).
        public static void PrewarmRocks()
        {
            if (rocksWarming) return; rocksWarming = true;
            for (int k = 0; k < AquariumRockShapes.Count * AquariumRockShapes.Variants; k++)
            {
                int key = k;
                System.Threading.ThreadPool.QueueUserWorkItem(_ => { try { RockData(key / AquariumRockShapes.Variants, key % AquariumRockShapes.Variants); } catch (Exception) { } });
            }
        }
        private static readonly Dictionary<int, object> rockKeyLocks = new Dictionary<int, object>();
        private static RockMeshData RockData(int kind, int variant)
        {
            int key = kind * 10 + variant; object keyLock;
            lock (rockLock)
            {
                RockMeshData ready; if (rockData.TryGetValue(key, out ready)) return ready;
                if (!rockKeyLocks.TryGetValue(key, out keyLock)) rockKeyLocks[key] = keyLock = new object();
            }
            lock (keyLock)
            {
                lock (rockLock) { RockMeshData ready; if (rockData.TryGetValue(key, out ready)) return ready; }
                var data = RockMesh(kind, variant);
                lock (rockLock) rockData[key] = data;
                return data;
            }
        }
        private static readonly Dictionary<int, SceneryPrototype> rockPrototypes = new Dictionary<int, SceneryPrototype>();
        internal static SceneryPrototype RockPrototype(int kind, int variant)
        {
            int key = kind * 10 + variant; SceneryPrototype p;
            if (rockPrototypes.TryGetValue(key, out p) && p.mesh != null) return p;
            var data = RockData(kind, variant);
            var mesh = new Mesh { name = "Twin rock / " + AquariumRockShapes.Names[kind] + " " + variant, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = data.vertices; mesh.normals = data.normals; mesh.colors = data.colors; mesh.triangles = data.triangles;
            SurfaceBasis(mesh); mesh.RecalculateBounds();
            rockPrototypes[key] = p = new SceneryPrototype { mesh = mesh, size = 1, vertices = mesh.vertexCount };
            return p;
        }

        // For the editor's renders and checks.
        public static Material TwinRockMaterial() { return TwinMaterials().rock; }
        public static Mesh TwinRockMesh(int kind, int variant) { return RockPrototype(kind, variant).mesh; }

        // ---------------------------------------------------------------- small life and sand dressing
        internal static SceneryPrototype CoralPrototype(int kind, int variant)
        {
            var g = new Geo(); int seed = kind + 6 * (variant + 1);
            CoralColony(g, Vector3.zero, .4f, seed, new System.Random(4100 + seed * 13), null);
            return Capture(g, "Scenery coral", .4f);
        }
        // A low tuft of fine grass blades (height 1, scaled per tuft).
        internal static SceneryPrototype GrassPrototype(int variant)
        {
            var g = new Geo(); var r = new System.Random(7300 + variant * 31);
            Color a = variant == 0 ? C(0x4f8f55) : C(0x6d9a4a), b = variant == 0 ? C(0x93bb6a) : C(0xa9b65c);
            for (int n = 0; n < 15; n++)
            {
                float angle = n * 2.399f + variant, reach = .03f + .2f * (float)Math.Sqrt(Range(r, 0, 1));
                var root = new Vector3(Mathf.Cos(angle) * reach, 0, Mathf.Sin(angle) * reach);
                float h = Range(r, .55f, 1f); var lean = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * h * Range(r, .15f, .4f);
                var tip = root + lean + Vector3.up * h;
                OrganicBlade(g, Curve(root, root + Vector3.up * h * .6f + lean * .2f, tip, 6), Range(r, .016f, .03f), Color.Lerp(a, b, Range(r, 0, 1)), angle, 0, .55f, 25);
            }
            return Capture(g, "Scenery grass tuft", 1);
        }
        internal static SceneryPrototype PebblePrototype(int variant)
        {
            var g = new Geo();
            g.Ellipsoid(Vector3.up * .2f, new Vector3(1, .55f, .82f), Color.white, 9, 6, .22f + .08f * variant);
            return Capture(g, "Scenery pebble", 1);
        }
        internal static SceneryPrototype ShellPrototype(int variant)
        {
            var g = new Geo(); Color tint = variant == 0 ? C(0xe8c8a8) : C(0xd9a7a0);
            for (int rib = 0; rib < 7; rib++)
            {
                float a = (rib - 3) * .32f; var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                g.Ellipsoid(dir * .45f + Vector3.up * .08f, new Vector3(.2f, .14f, .55f), Color.Lerp(tint, Color.white, (rib % 2) * .25f), 8, 5, 0);
            }
            g.Ellipsoid(new Vector3(0, .06f, -.05f), new Vector3(.22f, .1f, .18f), tint * .85f, 8, 5, 0);
            return Capture(g, "Scenery shell", 1);
        }
        internal static SceneryPrototype StarPrototype(int variant)
        {
            var g = new Geo(); Color tint = variant == 0 ? C(0xe0824f) : C(0xc96a8a);
            Starfish(g, Vector3.zero, 1, tint, variant * .7f);
            for (int arm = 0; arm < 5; arm++)
            {
                float a = variant * .7f + arm * Mathf.PI * 2 / 5; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                g.Ellipsoid(dir * .45f + Vector3.up * .05f, new Vector3(.2f, .09f, .2f), Color.Lerp(tint, Color.white, .15f), 7, 4, 0);
            }
            g.Ellipsoid(Vector3.up * .06f, new Vector3(.3f, .12f, .3f), tint, 8, 5, 0);
            return Capture(g, "Scenery sea star", 1);
        }
    }
}

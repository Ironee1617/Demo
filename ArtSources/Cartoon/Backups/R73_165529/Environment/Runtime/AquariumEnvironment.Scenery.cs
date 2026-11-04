using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    public static partial class AquariumEnvironment
    {
        // R37: run-time scenery prototypes, built once at the origin with the environment's own generators.
        internal sealed class SceneryPrototype { public Mesh mesh; public float size; public int vertices; }
        private static SceneryPrototype Capture(Geo g, string name, float size)
        {
            var mesh = g.Build(name); mesh.hideFlags = HideFlags.HideAndDontSave;
            return new SceneryPrototype { mesh = mesh, size = size, vertices = mesh.vertexCount };
        }
        internal static SceneryPrototype SeaweedPrototype(int form, float height, int variant)
        {
            var g = new Geo(); var r = new System.Random(9100 + form * 131 + variant * 17 + Mathf.RoundToInt(height * 10));
            Seaweed(g, Vector3.zero, height, variant * 2.1f + form * .7f, r, null, form);
            return Capture(g, "Scenery seaweed", height);
        }
        internal static SceneryPrototype AnemonePrototype(int kind, float size, int variant)
        {
            var g = new Geo(); LivingAnemone(g, Vector3.zero, size, kind, 17 + variant * 5 + kind, null);
            return Capture(g, "Scenery anemone", size);
        }
        // The anemone garden's three crevice stones round a foot (BuildAnemoneGardens).
        internal static SceneryPrototype HoldfastPrototype(float size)
        {
            var g = new Geo(); var field = new StoneField(); // R65: the three stones joined into one
            for (int stone = 0; stone < 3; stone++)
            {
                float angle = stone * 2.3f; var foot = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * size * .27f; foot.y -= .025f;
                field.Add(foot, new Vector3(size * .31f, size * .14f, size * .26f), C(0x7f8b7c), .24f, size * .22f);
            }
            field.MeshInto(g, size * .085f);
            return Capture(g, "Scenery holdfast", size);
        }
    }

    // R37: shows the current AquariumSceneryLayout on the gallery's baked environment:
    //  * the cliffs (31 rock, 32 ledge corals) turn about their pivots as whole objects;
    //  * each reef rock's cover and corals (12, 13) are re-made turned about the rock and settled on the sand
    //    under their new place (the far planting pockets are left out);
    //  * the baked seaweed and anemones (14, 16, 33, 34, 35, 36) are hidden and the layout's plants and anemones
    //    are drawn instead, merged into a few meshes per area.
    // The baked layout restores everything as authored.
    public sealed class AquariumSceneryPresentation : IDisposable
    {
        private sealed class Part
        {
            public Transform transform; public MeshFilter filter; public MeshRenderer renderer; public Mesh original;
            public Vector3 position; public Quaternion rotation; public int group = -1, rock = -1; public bool plant;
            public ReefSource source;
        }
        private readonly List<ReefTurn> turns = new List<ReefTurn>(); private readonly List<Part> turned = new List<Part>();
        private readonly Component root;
        private readonly List<Part> parts = new List<Part>();
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Mesh> made = new List<Mesh>();
        private Material seaweedMaterial, anemoneMaterial, stoneMaterial; private int plantLayer;
        private AquariumSceneryLayout shown;
        public AquariumSceneryLayout Shown { get { return shown; } }

        private static readonly float[] Heights = { .9f, 1.6f, 2.6f, 3.8f };
        private const float TallKelp = 6.4f;
        private static readonly float[] AnemoneSizes = { .32f, .42f };
        private static Dictionary<int, AquariumEnvironment.SceneryPrototype> seaweed, anemones;
        private static AquariumEnvironment.SceneryPrototype holdfast;

        public AquariumSceneryPresentation(Component root)
        {
            this.root = root; if (root == null) return;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>(); if (filter == null) continue;
                string name = renderer.name; var part = new Part { transform = renderer.transform, filter = filter, renderer = renderer, original = filter.sharedMesh,
                    position = renderer.transform.localPosition, rotation = renderer.transform.localRotation };
                var bits = name.Split(new[] { " / " }, StringSplitOptions.None);
                if (name.StartsWith("31 • ") && bits.Length > 1) part.group = Array.IndexOf(AquariumMonolithLayout.Names, bits[1]);
                else if (name.StartsWith("32 • ")) part.group = TrailingNumber(bits[1]);
                else if (name.StartsWith("12 • ") || name.StartsWith("13 • ")) part.rock = TrailingNumber(bits[0]) - 1;
                else if (name.StartsWith("14 • ") || name.StartsWith("16 • ") || name.StartsWith("33 • ") || name.StartsWith("35 • ")) { part.plant = true; if (seaweedMaterial == null) { seaweedMaterial = renderer.sharedMaterial; plantLayer = renderer.gameObject.layer; } }
                else if (name.StartsWith("34 • ")) { part.plant = true; if (anemoneMaterial == null) anemoneMaterial = renderer.sharedMaterial; }
                else if (name.StartsWith("36 • ")) { part.plant = true; if (stoneMaterial == null) stoneMaterial = renderer.sharedMaterial; }
                else continue;
                if (part.group >= AquariumMonolithLayout.Groups.Length || part.rock >= AquariumModel.Rocks.Length) continue;
                if (part.group < 0 && part.rock < 0 && !part.plant) continue;
                parts.Add(part);
            }
            if (stoneMaterial == null) foreach (var p in parts) if (p.rock >= 0 && p.renderer.name.StartsWith("12 • ")) { stoneMaterial = p.renderer.sharedMaterial; break; }
        }
        private static int TrailingNumber(string text)
        {
            int end = text.Length; while (end > 0 && !char.IsDigit(text[end - 1])) end--;
            int start = end; while (start > 0 && char.IsDigit(text[start - 1])) start--;
            int value; return start < end && int.TryParse(text.Substring(start, end - start), out value) ? value : -1;
        }

        // Builds the plant and anemone prototypes ahead of the first game (about a second, once per run).
        public static void Prewarm()
        {
            for (int form = 0; form < 5; form++) foreach (float h in Heights) for (int variant = 0; variant < 2; variant++) Seaweed(form, h, variant);
            for (int variant = 0; variant < 2; variant++) Seaweed(2, TallKelp, variant);
            for (int kind = 0; kind < 2; kind++) foreach (float size in AnemoneSizes) for (int variant = 0; variant < 2; variant++) Anemone(kind, size, variant);
            if (holdfast == null || holdfast.mesh == null) holdfast = AquariumEnvironment.HoldfastPrototype(.42f);
        }

        // Switch the tank to the layout of this seed (cheap when it is already shown).
        // R62: in play the twin tanks' dressing is laid out off the main thread and then built a few meshes per frame
        // (StepBudgetMs), the old dressing staying until the new one is complete; tools and renders build at once.
        public void Sync(int seed) { Sync(seed, !Application.isPlaying); }
        public void Sync(int seed, bool immediate)
        {
            if (AquariumMaps.IsTwin && !immediate && !AquariumSceneryLayout.TwinReady(seed)) { AquariumSceneryLayout.PrepareTwin(seed); return; }
            AquariumSceneryLayout.Apply(seed);
            var layout = AquariumSceneryLayout.Current;
            if (root == null) return;
            // R58: the twin tanks are dressed entirely at run time (rocks, corals, plants, anemones, sand details).
            if (layout.Map == AquariumMaps.Twin)
            {
                if (layout == shown) { CancelBuild(); return; }
                if (layout != building) BeginTwin(layout);
                RunSteps(immediate);
                return;
            }
            CancelBuild();
            if (layout == shown) { PollCliffJoins(); return; }
            Restore(); shown = layout;
            StartCliffJoins(layout, immediate);
            if (layout.Baked) return;
            foreach (var part in parts)
            {
                if (part.group >= 0)
                {
                    var turn = Quaternion.Euler(0, layout.MonolithYaw[part.group], 0); var pivot = AquariumSceneryLayout.MonolithPivot[part.group];
                    var p = new Vector3(pivot.x, 0, pivot.z);
                    part.transform.localRotation = turn * part.rotation; part.transform.localPosition = p + turn * (part.position - p);
                }
                else if (part.rock >= 0 && part.original != null && part.original.isReadable)
                {
                    if (part.source == null) part.source = new ReefSource(part.original);
                    turns.Add(new ReefTurn { source = part.source, rock = part.rock, yaw = layout.RockYaw[part.rock] }); turned.Add(part);
                }
                else if (part.plant) part.renderer.enabled = false;
            }
            System.Threading.Tasks.Parallel.For(0, turns.Count, i => turns[i].Run());
            for (int i = 0; i < turns.Count; i++) { var mesh = turns[i].Build(); made.Add(mesh); turned[i].filter.sharedMesh = mesh; }
            turns.Clear(); turned.Clear();
            Plant(layout);
            RunSteps(true);
        }

        // ---------------------------------------------------------------- R62: building in steps
        private readonly List<Action> steps = new List<Action>();
        private readonly List<GameObject> nextSpawned = new List<GameObject>();
        private readonly List<Mesh> nextMade = new List<Mesh>();
        private AquariumSceneryLayout building; private int stepAt;
        private const double StepBudgetMs = 4;
        // True while a new layout's dressing is still being built.
        public bool Building { get { return steps.Count > 0; } }
        private void BeginTwin(AquariumSceneryLayout layout)
        {
            CancelBuild(); building = layout;
            BuildTwin(layout);
        }
        private void CancelBuild()
        {
            foreach (var go in nextSpawned) Release(go); nextSpawned.Clear();
            foreach (var mesh in nextMade) Release(mesh); nextMade.Clear();
            steps.Clear(); stepAt = 0; building = null; gate = null;
        }
        // R64: the build waits (the old dressing still showing) until the layout's rock joins are worked out.
        private Func<bool> gate; private float gateSince;
        private void RunSteps(bool all)
        {
            if (gate != null)
            {
                if (!all && !gate() && Time.realtimeSinceStartup - gateSince < 12) return;
                gate = null;
            }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (stepAt < steps.Count && (all || watch.Elapsed.TotalMilliseconds < StepBudgetMs)) steps[stepAt++]();
            if (stepAt < steps.Count) return;
            // Done: swap the new dressing in (the twin layout's old dressing goes now, the default tank's was restored first).
            var next = building;
            if (next != null) { Restore(); shown = next; }
            foreach (var go in nextSpawned) go.SetActive(true);
            spawned.AddRange(nextSpawned); made.AddRange(nextMade); nextSpawned.Clear(); nextMade.Clear();
            steps.Clear(); stepAt = 0; building = null;
            if (next != null && Application.isPlaying)
            {
                // R61: in the twin tanks the scene's weak cool fill light is drawn per vertex (no second per-pixel pass
                // over the dense dressing; the look stays the same); the default tank gets it back as it was.
                var fill = GameObject.Find("Gallery cool fill");
                fillLight = fill != null ? fill.GetComponent<Light>() : null;
                if (fillLight != null && fillLight.type == LightType.Directional) { fillMode = fillLight.renderMode; fillLight.renderMode = LightRenderMode.ForceVertex; }
                else fillLight = null;
            }
        }

        // ---------------------------------------------------------------- R65: reef rocks meeting the cliffs
        // The default tank's cliffs and reef rocks turn per game, so where they meet changes: the join patches are
        // meshed for the shown layout on a worker thread (AquariumEnvironment.CliffJoins) and added when ready.
        private AquariumSceneryLayout joinFor; private List<AquariumEnvironment.RockMeshData> joinsReady; private readonly object joinLock = new object();
        private static Color cliffColour; private static bool cliffColourKnown;
        private void StartCliffJoins(AquariumSceneryLayout layout, bool immediate)
        {
            if (AquariumReefStoneTable.Stones == null || layout.Map == AquariumMaps.Twin || AquariumEnvironment.CliffJoinsOff) return;
            if (!cliffColourKnown)
            {
                // The cliffs' painted colour on average (the join's cliff side is painted with it).
                Color sum = Color.clear; int n = 0;
                foreach (var part in parts)
                    if (part.group >= 0 && part.original != null && part.original.isReadable && part.renderer.name.StartsWith("31 • "))
                    { var cs = part.original.colors; for (int i = 0; i < cs.Length; i += 7) { sum += cs[i]; n++; } }
                cliffColour = n > 0 ? sum / n : new Color(.55f, .58f, .55f, 1); cliffColourKnown = true;
            }
            lock (joinLock) { joinFor = layout; joinsReady = null; }
            var colour = cliffColour;
            if (immediate) { joinsReady = AquariumEnvironment.CliffJoins(layout, colour); PollCliffJoins(); return; }
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                List<AquariumEnvironment.RockMeshData> made = null;
                try { made = AquariumEnvironment.CliffJoins(layout, colour); } catch (Exception e) { Debug.LogWarning("R65 cliff joins: " + e); }
                lock (joinLock) if (joinFor == layout) joinsReady = made;
            });
        }
        private void PollCliffJoins()
        {
            List<AquariumEnvironment.RockMeshData> ready;
            lock (joinLock) { ready = joinsReady; if (ready == null || joinFor != shown) return; joinsReady = null; }
            Material material = null; int layer = plantLayer;
            foreach (var part in parts) if (part.rock >= 0 && part.renderer != null && part.renderer.name.StartsWith("12 • ")) { material = part.renderer.sharedMaterial; layer = part.renderer.gameObject.layer; break; }
            if (material == null || root == null) return;
            for (int i = 0; i < ready.Count; i++)
            {
                var mesh = AquariumEnvironment.SeamMesh(ready[i], "R65 • Reef and cliff join"); made.Add(mesh);
                var go = new GameObject("R65 • Reef and cliff join " + (i + 1).ToString("00")); go.layer = layer; go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                spawned.Add(go);
            }
        }

        private void Restore()
        {
            foreach (var part in parts)
            {
                if (part.transform == null) continue;
                part.transform.localPosition = part.position; part.transform.localRotation = part.rotation;
                if (part.filter != null && part.filter.sharedMesh != part.original) part.filter.sharedMesh = part.original;
                if (part.plant && part.renderer != null) part.renderer.enabled = true;
            }
            if (fillLight != null) { fillLight.renderMode = fillMode; fillLight = null; }
            foreach (var go in spawned) Release(go); spawned.Clear();
            foreach (var mesh in made) Release(mesh); made.Clear();
            lock (joinLock) { joinFor = null; joinsReady = null; }
        }
        public void Dispose() { CancelBuild(); Restore(); shown = null; }
        private static void Release(UnityEngine.Object value)
        { if (value == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }

        // A reef rock's cover or corals, read once from the baked mesh.
        private sealed class ReefSource
        {
            public Vector3[] vertices, normals; public Vector4[] tangents; public Color[] colors; public Vector2[] uv, data; public int[] triangles; public string name;
            public ReefSource(Mesh mesh)
            {
                name = mesh.name; vertices = mesh.vertices; normals = mesh.normals; tangents = mesh.tangents; colors = mesh.colors; triangles = mesh.triangles;
                var list = new List<Vector2>(); mesh.GetUVs(0, list); uv = list.Count == vertices.Length ? list.ToArray() : null;
                list.Clear(); mesh.GetUVs(1, list); data = list.Count == vertices.Length ? list.ToArray() : null;
                if (normals.Length != vertices.Length) normals = null; if (tangents.Length != vertices.Length) tangents = null; if (colors.Length != vertices.Length) colors = null;
            }
        }
        // The same turned about the rock (pure arithmetic, so the reefs are turned side by side on worker threads),
        // each point settled by the change of sand height under it; the planting pockets two metres out are left out.
        private sealed class ReefTurn
        {
            public ReefSource source; public int rock; public float yaw;
            public Vector3[] vertices, normals; public Vector4[] tangents; public Color[] colors; public Vector2[] uv, data; public int[] triangles; public int used, kept;
            public void Run()
            {
                var v = source.vertices; var tri = source.triangles; var centre = AquariumModel.Rocks[rock]; float keep = AquariumSceneryLayout.ReefKeep(rock);
                float a = yaw * Mathf.Deg2Rad, c = (float)Math.Cos(a), s = (float)Math.Sin(a);
                // Sand height change under every new place, on a fine grid over the reef (bilinear in between).
                const float step = .2f; int n = (int)Math.Ceiling(2 * (keep + .5f) / step) + 2; float x0 = centre.x - keep - .5f, z0 = centre.z - keep - .5f;
                var lift = new float[n * n];
                for (int iz = 0; iz < n; iz++) for (int ix = 0; ix < n; ix++)
                {
                    float x = x0 + ix * step, z = z0 + iz * step, dx = x - centre.x, dz = z - centre.z;
                    // Back to where this point was before the turn (inverse of the yaw).
                    lift[iz * n + ix] = AquariumSeabed.Height(x, z) - AquariumSeabed.Height(centre.x + c * dx - s * dz, centre.z + s * dx + c * dz);
                }
                int count = v.Length; var remap = new int[count]; for (int i = 0; i < count; i++) remap[i] = -1;
                vertices = new Vector3[count]; if (source.normals != null) normals = new Vector3[count]; if (source.tangents != null) tangents = new Vector4[count];
                if (source.colors != null) colors = new Color[count]; if (source.uv != null) uv = new Vector2[count]; if (source.data != null) data = new Vector2[count];
                triangles = new int[tri.Length]; used = kept = 0; float keep2 = keep * keep;
                for (int t = 0; t < tri.Length; t += 3)
                {
                    int i0 = tri[t], i1 = tri[t + 1], i2 = tri[t + 2];
                    float cx = (v[i0].x + v[i1].x + v[i2].x) / 3 - centre.x, cz = (v[i0].z + v[i1].z + v[i2].z) / 3 - centre.z;
                    if (cx * cx + cz * cz > keep2) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        int i = tri[t + k];
                        if (remap[i] < 0)
                        {
                            int o = remap[i] = used++; var p = v[i];
                            float dx = p.x - centre.x, dz = p.z - centre.z, x = centre.x + c * dx + s * dz, z = centre.z - s * dx + c * dz;
                            float gx = Math.Max(0, Math.Min(n - 1.001f, (x - x0) / step)), gz = Math.Max(0, Math.Min(n - 1.001f, (z - z0) / step));
                            int ix = (int)gx, iz = (int)gz; float fx = gx - ix, fz = gz - iz;
                            float l0 = lift[iz * n + ix] + (lift[iz * n + ix + 1] - lift[iz * n + ix]) * fx, l1 = lift[(iz + 1) * n + ix] + (lift[(iz + 1) * n + ix + 1] - lift[(iz + 1) * n + ix]) * fx;
                            vertices[o] = new Vector3(x, p.y + l0 + (l1 - l0) * fz, z);
                            if (normals != null) { var m = source.normals[i]; normals[o] = new Vector3(c * m.x + s * m.z, m.y, -s * m.x + c * m.z); }
                            if (tangents != null) { var m = source.tangents[i]; tangents[o] = new Vector4(c * m.x + s * m.z, m.y, -s * m.x + c * m.z, m.w); }
                            if (colors != null) colors[o] = source.colors[i];
                            if (uv != null) uv[o] = source.uv[i];
                            if (data != null) data[o] = source.data[i];
                        }
                        triangles[kept++] = remap[i];
                    }
                }
            }
            public Mesh Build()
            {
                var mesh = new Mesh { name = source.name + " (turned)", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
                mesh.SetVertices(vertices, 0, used); if (normals != null) mesh.SetNormals(normals, 0, used); if (tangents != null) mesh.SetTangents(tangents, 0, used);
                if (colors != null) mesh.SetColors(colors, 0, used); if (uv != null) mesh.SetUVs(0, uv, 0, used); if (data != null) mesh.SetUVs(1, data, 0, used);
                mesh.SetIndices(triangles, 0, kept, MeshTopology.Triangles, 0, false); mesh.RecalculateBounds();
                var bounds = mesh.bounds; bounds.Expand(.7f); mesh.bounds = bounds;
                return mesh;
            }
        }

        // ---------------------------------------------------------------- seaweed and anemones
        private struct Item { public AquariumEnvironment.SceneryPrototype proto; public Matrix4x4 matrix; public float phase; public int rock1; }
        private static AquariumEnvironment.SceneryPrototype Seaweed(int form, float height, int variant)
        {
            if (seaweed == null) seaweed = new Dictionary<int, AquariumEnvironment.SceneryPrototype>();
            if (form == AquariumSceneryLayout.GrassForm) return Cached(seaweed, 9000 + variant, () => AquariumEnvironment.GrassPrototype(variant));
            // R61: the twin tanks' kelp forests (5.2 m and up) get their own tall prototype.
            if (height >= 5.2f) { int f = form, v = variant; return Cached(seaweed, 9100 + form * 10 + variant, () => AquariumEnvironment.SeaweedPrototype(f, TallKelp, v)); }
            int bucket = 0; for (int i = 1; i < Heights.Length; i++) if (Mathf.Abs(Heights[i] - height) < Mathf.Abs(Heights[bucket] - height)) bucket = i;
            int key = form * 100 + bucket * 10 + variant;
            AquariumEnvironment.SceneryPrototype p;
            if (!seaweed.TryGetValue(key, out p) || p.mesh == null) seaweed[key] = p = AquariumEnvironment.SeaweedPrototype(form, Heights[bucket], variant);
            return p;
        }
        private static AquariumEnvironment.SceneryPrototype Anemone(int kind, float size, int variant)
        {
            if (anemones == null) anemones = new Dictionary<int, AquariumEnvironment.SceneryPrototype>();
            int bucket = Mathf.Abs(AnemoneSizes[1] - size) < Mathf.Abs(AnemoneSizes[0] - size) ? 1 : 0;
            int key = kind * 100 + bucket * 10 + variant;
            AquariumEnvironment.SceneryPrototype p;
            if (!anemones.TryGetValue(key, out p) || p.mesh == null) anemones[key] = p = AquariumEnvironment.AnemonePrototype(kind, AnemoneSizes[bucket], variant);
            return p;
        }

        // ---------------------------------------------------------------- R58: the twin tanks' dressing
        private static readonly Dictionary<int, AquariumEnvironment.SceneryPrototype> dressing = new Dictionary<int, AquariumEnvironment.SceneryPrototype>();
        private static AquariumEnvironment.SceneryPrototype Cached(Dictionary<int, AquariumEnvironment.SceneryPrototype> cache, int key, Func<AquariumEnvironment.SceneryPrototype> make)
        {
            AquariumEnvironment.SceneryPrototype p;
            if (!cache.TryGetValue(key, out p) || p.mesh == null) cache[key] = p = make();
            return p;
        }
        public int TwinRockRenderers { get; private set; }
        private Light fillLight; private LightRenderMode fillMode;
        private void BuildTwin(AquariumSceneryLayout layout)
        {
            var materials = AquariumEnvironment.TwinMaterials();
            seaweedMaterial = seaweedMaterial ?? materials.kelp; anemoneMaterial = anemoneMaterial ?? materials.anemone; stoneMaterial = stoneMaterial ?? materials.stone;
            if (root != null)
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) if (renderer.name.StartsWith("01 • ")) { plantLayer = renderer.gameObject.layer; break; }
            var rocks = new SortedDictionary<long, List<Item>>();
            // R64: rocks that touch are joined into one stone (AquariumEnvironment.RockSeams, worked out off the main thread).
            seamSet = AquariumEnvironment.SeamsOff ? null : layout.TwinRocks;
            if (seamSet != null && !AquariumEnvironment.SeamsReady(seamSet)) { AquariumEnvironment.PrepareSeams(seamSet); var waitFor = seamSet; gate = () => AquariumEnvironment.SeamsReady(waitFor); gateSince = Time.realtimeSinceStartup; }
            for (int ri = 0; ri < layout.TwinRocks.Rocks.Length; ri++)
            {
                var rock = layout.TwinRocks.Rocks[ri];
                Add(rocks, rock.position, AquariumEnvironment.RockPrototype(rock.kind, rock.variant), rock.scale, rock.yaw, rock.tint);
                long key = ((long)Mathf.FloorToInt(rock.position.x / Area) << 32) ^ (uint)Mathf.FloorToInt(rock.position.z / Area);
                var list = rocks[key]; var last = list[list.Count - 1]; last.rock1 = ri + 1; list[list.Count - 1] = last;
            }
            TwinRockRenderers = rocks.Count;
            EmitRocks(rocks, "R58 • Twin rocks", materials.rock);
            steps.Add(() => EmitSeams(materials.rock));
            var corals = new SortedDictionary<long, List<Item>>(); var stones = new SortedDictionary<long, List<Item>>(); var shells = new SortedDictionary<long, List<Item>>();
            var stars = new SortedDictionary<long, List<Item>>();
            if (layout.Corals != null)
                for (int i = 0; i < layout.Corals.Length; i++)
                {
                    // R63: every fifth colony is one of the three new forms (table, organ pipes, bubble coral); three
                    // painted colourways per form, and a hue and value nudge per colony (Item.phase, EmitArea).
                    var c = layout.Corals[i]; int h = Hash(i * 7 + 3);
                    int kind = i % 5 == 2 ? 6 + (h >> 4) % 3 : c.kind, variant = (h >> 8) % 3;
                    var proto = Cached(dressing, 100 + kind * 10 + variant, () => AquariumEnvironment.CoralPrototype(kind, variant));
                    Add(corals, c.position, proto, c.size / proto.size, c.yaw, ((h >> 12) % 1000) / 500f - 1);
                }
            if (layout.Details != null)
                for (int i = 0; i < layout.Details.Length; i++)
                {
                    var d = layout.Details[i]; int variant = i % 2;
                    if (d.kind == 0) Add(stones, d.position, Cached(dressing, 200 + variant, () => AquariumEnvironment.PebblePrototype(variant)), d.size, d.yaw, 0, SmallArea);
                    else if (d.kind == 1) Add(shells, d.position, Cached(dressing, 300 + variant, () => AquariumEnvironment.ShellPrototype(variant)), d.size, d.yaw, 0, SmallArea);
                    else
                    {
                        // R63: modelled sea stars, four species in six colourways; draped over the sand (phase 1)
                        // or tilted onto the rock face (kind 3).
                        int h = Hash(i * 13 + 5), species = AquariumEnvironment.StarSpecies(h), colour = (h >> 6) % AquariumEnvironment.StarColourways;
                        var proto = Cached(dressing, 400 + species * 10 + colour, () => AquariumEnvironment.StarPrototype(species, colour));
                        var turn = Quaternion.Euler(0, d.yaw, 0);
                        if (d.kind == 3) turn = Quaternion.FromToRotation(Vector3.up, new Vector3(d.normal.x, d.normal.y, d.normal.z).normalized) * turn;
                        Add(stars, d.position, proto, d.size, turn, d.kind == 3 ? 0 : 1, SmallArea);
                    }
                }
            Emit(corals, "R58 • Corals", materials.coral, true, false, true);
            // R61: the sand's small things cast no shadows (too small to show one, and twice as many now).
            Emit(shells, "R61 • Shells", materials.coral, false, true);
            foreach (var area in stars) { var items = area.Value; if (items.Count > 0) steps.Add(() => EmitStarArea(items, "R63 • Sea stars", materials.coral)); }
            Emit(stones, "R58 • Sand pebbles", materials.stone, false, true);
            Plant(layout);
        }
        // Rocks merged per area; each rock's painted colours get its own warm or cool tint.
        private void EmitRocks(SortedDictionary<long, List<Item>> areas, string name, Material material)
        {
            foreach (var area in areas)
            {
                var items = area.Value; if (items.Count == 0) continue;
                steps.Add(() => EmitRockArea(items, name, material));
            }
        }
        private AquariumRockSet seamSet;
        // R64: the joins' patches, one renderer each (they are few), with the rocks' material and shadows.
        private void EmitSeams(Material material)
        {
            if (seamSet == null) return;
            var seams = AquariumEnvironment.Seams(seamSet);
            for (int i = 0; i < seams.patches.Count; i++)
            {
                var mesh = AquariumEnvironment.SeamMesh(seams.patches[i], "R64 • Rock join");
                nextMade.Add(mesh);
                var go = new GameObject("R64 • Rock join " + (i + 1).ToString("00")); go.SetActive(false); go.layer = plantLayer; go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                nextSpawned.Add(go);
            }
        }
        private void EmitRockArea(List<Item> items, string name, Material material)
        {
            {
                var seams = seamSet != null ? AquariumEnvironment.Seams(seamSet) : null;
                var parts = new CombineInstance[items.Count]; var trimmed = new List<Mesh>();
                for (int i = 0; i < items.Count; i++)
                {
                    var source = items[i].proto.mesh; int[] kept;
                    // R64: a rock that joins another loses its triangles inside the join (the patch draws it there).
                    if (seams != null && items[i].rock1 > 0 && seams.kept.TryGetValue(items[i].rock1 - 1, out kept))
                    {
                        var r = seamSet.Rocks[items[i].rock1 - 1];
                        source = AquariumEnvironment.TrimmedRockMesh(r.kind, r.variant, kept); trimmed.Add(source);
                    }
                    parts[i] = new CombineInstance { mesh = source, transform = items[i].matrix };
                }
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
                mesh.CombineMeshes(parts, true, true, false);
                foreach (var t in trimmed) Release(t);
                var colours = mesh.colors;
                if (colours.Length == mesh.vertexCount)
                {
                    int at = 0;
                    foreach (var item in items)
                    {
                        float t = item.phase;
                        var tint = Color.Lerp(new Color(1.05f, 1.0f, .92f), new Color(.9f, .97f, 1.03f), t) * (.93f + .12f * Mathf.Abs(t - .5f) * 2);
                        int end = Math.Min(colours.Length, at + item.proto.vertices);
                        for (; at < end; at++) { var c = colours[at]; colours[at] = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a); }
                    }
                    mesh.colors = colours;
                }
                var bounds = mesh.bounds; bounds.Expand(.2f); mesh.bounds = bounds;
                nextMade.Add(mesh);
                var go = new GameObject(name + " " + (spawned.Count + nextSpawned.Count + 1).ToString("00")); go.SetActive(false); go.layer = plantLayer; go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                nextSpawned.Add(go);
            }
        }

        private const float Area = 12f;
        private void Plant(AquariumSceneryLayout layout)
        {
            if (layout.Plants == null || layout.Anemones == null) return;
            if (holdfast == null || holdfast.mesh == null) holdfast = AquariumEnvironment.HoldfastPrototype(.42f);
            var kelp = new SortedDictionary<long, List<Item>>(); var soft = new SortedDictionary<long, List<Item>>(); var stones = new SortedDictionary<long, List<Item>>();
            var grass = new SortedDictionary<long, List<Item>>(); bool twin = layout.Map == AquariumMaps.Twin;
            for (int i = 0; i < layout.Plants.Length; i++)
            {
                var plant = layout.Plants[i]; var proto = Seaweed(plant.form, plant.height, i % 2);
                // R61: the twin tanks' grass tufts are drawn on their own, without shadows.
                // R62: and the short seaweed (under 1.6 m) with them: no shadows, dropped in the distance.
                if (twin && (plant.form == AquariumSceneryLayout.GrassForm || plant.height < 1.6f)) Add(grass, plant.position, proto, plant.height / proto.size, plant.yaw, plant.phase, SmallArea);
                else Add(kelp, plant.position, proto, plant.height / proto.size, plant.yaw, plant.phase);
            }
            for (int i = 0; i < layout.Anemones.Length; i++)
            {
                var a = layout.Anemones[i]; var proto = Anemone(a.kind, a.size, i % 2);
                Add(soft, a.position, proto, a.size / proto.size, a.yaw, a.yaw * Mathf.Deg2Rad);
                if (!a.ledge) Add(stones, a.position, holdfast, a.size / holdfast.size, a.yaw + 40, 0);
            }
            Emit(kelp, "R37 • Seaweed", seaweedMaterial);
            Emit(grass, "R61 • Grass tufts and low seaweed", seaweedMaterial, false, true);
            Emit(soft, "R37 • Anemones", anemoneMaterial);
            Emit(stones, "R37 • Anemone holdfasts", stoneMaterial);
        }
        // R61: the sand's small things are merged per 6 m square and dropped beyond about 50 m (CullSmall).
        private const float SmallArea = 6f, SmallCullHeight = .1f;
        private static void Add(SortedDictionary<long, List<Item>> areas, Point at, AquariumEnvironment.SceneryPrototype proto, float scale, float yaw, float phase, float area = Area)
        {
            Add(areas, at, proto, scale, Quaternion.Euler(0, yaw, 0), phase, area);
        }
        private static void Add(SortedDictionary<long, List<Item>> areas, Point at, AquariumEnvironment.SceneryPrototype proto, float scale, Quaternion turn, float phase, float area)
        {
            long key = ((long)Mathf.FloorToInt(at.x / area) << 32) ^ (uint)Mathf.FloorToInt(at.z / area);
            List<Item> list; if (!areas.TryGetValue(key, out list)) areas[key] = list = new List<Item>();
            list.Add(new Item { proto = proto, phase = phase, matrix = Matrix4x4.TRS(new Vector3(at.x, at.y, at.z), turn, Vector3.one * scale) });
        }
        private static int Hash(int n) { unchecked { uint x = (uint)n * 2654435761u; x ^= x >> 15; x *= 2246822519u; x ^= x >> 13; return (int)(x & 0x7fffffff); } }
        private void Emit(SortedDictionary<long, List<Item>> areas, string name, Material material, bool shadows = true, bool cullSmall = false, bool hue = false)
        {
            if (material == null) return;
            foreach (var area in areas)
            {
                var items = area.Value; if (items.Count == 0) continue;
                steps.Add(() => EmitArea(items, name, material, shadows, cullSmall, hue));
            }
        }
        // R63: the hue of painted colours turned round the grey axis (degrees), and their value scaled.
        private static void Turn(Color[] colours, int from, int end, float degrees, float value)
        {
            float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a), t = (1 - c) / 3, q = Mathf.Sqrt(1 / 3f) * s;
            float m0 = c + t, m1 = t - q, m2 = t + q;
            for (int i = from; i < end; i++)
            {
                var k = colours[i];
                colours[i] = new Color((k.r * m0 + k.g * m1 + k.b * m2) * value, (k.r * m2 + k.g * m0 + k.b * m1) * value, (k.r * m1 + k.g * m2 + k.b * m0) * value, k.a);
            }
        }
        // R63: sea stars merged per 6 m square; the sand stars drape over the sand's banks and ripples.
        private void EmitStarArea(List<Item> items, string name, Material material)
        {
            var parts = new CombineInstance[items.Count];
            for (int i = 0; i < items.Count; i++) parts[i] = new CombineInstance { mesh = items[i].proto.mesh, transform = items[i].matrix };
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
            mesh.CombineMeshes(parts, true, true, false);
            var v = mesh.vertices; var colours = mesh.colors; int at = 0;
            for (int n = 0; n < items.Count; n++)
            {
                var item = items[n]; int end = Math.Min(v.Length, at + item.proto.vertices);
                if (colours.Length == v.Length) Turn(colours, at, end, 0, .9f + .2f * (Hash(n * 31 + Mathf.RoundToInt(item.matrix.m03 * 10)) % 100) / 100f);
                if (item.phase > .5f)
                {
                    float x0 = item.matrix.m03, z0 = item.matrix.m23, y0 = AquariumSeabed.TwinHeight(x0, z0);
                    for (int i = at; i < end; i++) v[i].y += AquariumSeabed.TwinHeight(v[i].x, v[i].z) - y0;
                }
                at = end;
            }
            mesh.vertices = v; if (colours.Length == v.Length) mesh.colors = colours;
            mesh.RecalculateBounds();
            var bounds = mesh.bounds; bounds.Expand(.3f); mesh.bounds = bounds;
            nextMade.Add(mesh);
            var go = new GameObject(name + " " + (spawned.Count + nextSpawned.Count + 1).ToString("00")); go.SetActive(false); go.layer = plantLayer; go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(SmallCullHeight, new Renderer[] { renderer }) }); lod.RecalculateBounds();
            nextSpawned.Add(go);
        }
        private void EmitArea(List<Item> items, string name, Material material, bool shadows, bool cullSmall, bool hue = false)
        {
            {
                var parts = new CombineInstance[items.Count];
                for (int i = 0; i < items.Count; i++) parts[i] = new CombineInstance { mesh = items[i].proto.mesh, transform = items[i].matrix };
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
                mesh.CombineMeshes(parts, true, true, false);
                if (hue)
                {
                    // R63: each colony's colourway turned a little (Item.phase -1..1 = -22..22 degrees) and lit or dimmed.
                    var colours = mesh.colors;
                    if (colours.Length == mesh.vertexCount)
                    {
                        int from = 0;
                        foreach (var item in items) { int end = Math.Min(colours.Length, from + item.proto.vertices); Turn(colours, from, end, item.phase * 22, .92f + .14f * Mathf.Abs(item.phase)); from = end; }
                        mesh.colors = colours;
                    }
                }
                // The current's phase differs per plant, so neighbours never sway in lockstep.
                var phase = mesh.uv2;
                if (!hue && phase.Length == mesh.vertexCount)
                {
                    int at = 0;
                    foreach (var item in items) { int end = Math.Min(phase.Length, at + item.proto.vertices); for (; at < end; at++) phase[at].y += item.phase; }
                    mesh.uv2 = phase;
                }
                var bounds = mesh.bounds; bounds.Expand(1f); mesh.bounds = bounds;
                nextMade.Add(mesh);
                var go = new GameObject(name + " " + (spawned.Count + nextSpawned.Count + 1).ToString("00")); go.SetActive(false); go.layer = plantLayer; go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; renderer.receiveShadows = true;
                if (cullSmall)
                {
                    // One LOD and then nothing: Unity drops the patch once it is small on screen (no script per frame).
                    var lod = go.AddComponent<LODGroup>();
                    lod.SetLODs(new[] { new LOD(SmallCullHeight, new Renderer[] { renderer }) }); lod.RecalculateBounds();
                }
                nextSpawned.Add(go);
            }
        }
    }
}

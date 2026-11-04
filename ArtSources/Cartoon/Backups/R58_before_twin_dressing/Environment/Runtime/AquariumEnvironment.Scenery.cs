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
            var g = new Geo();
            for (int stone = 0; stone < 3; stone++)
            {
                float angle = stone * 2.3f; var foot = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * size * .27f; foot.y -= .025f;
                g.Ellipsoid(foot, new Vector3(size * .31f, size * .14f, size * .26f), C(0x7f8b7c), 10, 7, .24f);
            }
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
            for (int kind = 0; kind < 2; kind++) foreach (float size in AnemoneSizes) for (int variant = 0; variant < 2; variant++) Anemone(kind, size, variant);
            if (holdfast == null || holdfast.mesh == null) holdfast = AquariumEnvironment.HoldfastPrototype(.42f);
        }

        // Switch the tank to the layout of this seed (cheap when it is already shown).
        public void Sync(int seed)
        {
            AquariumSceneryLayout.Apply(seed);
            var layout = AquariumSceneryLayout.Current;
            if (layout == shown || root == null) return;
            Restore(); shown = layout;
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
            foreach (var go in spawned) Release(go); spawned.Clear();
            foreach (var mesh in made) Release(mesh); made.Clear();
        }
        public void Dispose() { Restore(); shown = null; }
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
        private struct Item { public AquariumEnvironment.SceneryPrototype proto; public Matrix4x4 matrix; public float phase; }
        private static AquariumEnvironment.SceneryPrototype Seaweed(int form, float height, int variant)
        {
            if (seaweed == null) seaweed = new Dictionary<int, AquariumEnvironment.SceneryPrototype>();
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

        private const float Area = 12f;
        private void Plant(AquariumSceneryLayout layout)
        {
            if (layout.Plants == null || layout.Anemones == null) return;
            if (holdfast == null || holdfast.mesh == null) holdfast = AquariumEnvironment.HoldfastPrototype(.42f);
            var kelp = new SortedDictionary<long, List<Item>>(); var soft = new SortedDictionary<long, List<Item>>(); var stones = new SortedDictionary<long, List<Item>>();
            for (int i = 0; i < layout.Plants.Length; i++)
            {
                var plant = layout.Plants[i]; var proto = Seaweed(plant.form, plant.height, i % 2);
                Add(kelp, plant.position, proto, plant.height / proto.size, plant.yaw, plant.phase);
            }
            for (int i = 0; i < layout.Anemones.Length; i++)
            {
                var a = layout.Anemones[i]; var proto = Anemone(a.kind, a.size, i % 2);
                Add(soft, a.position, proto, a.size / proto.size, a.yaw, a.yaw * Mathf.Deg2Rad);
                if (!a.ledge) Add(stones, a.position, holdfast, a.size / holdfast.size, a.yaw + 40, 0);
            }
            Emit(kelp, "R37 • Seaweed", seaweedMaterial);
            Emit(soft, "R37 • Anemones", anemoneMaterial);
            Emit(stones, "R37 • Anemone holdfasts", stoneMaterial);
        }
        private static void Add(SortedDictionary<long, List<Item>> areas, Point at, AquariumEnvironment.SceneryPrototype proto, float scale, float yaw, float phase)
        {
            long key = ((long)Mathf.FloorToInt(at.x / Area) << 32) ^ (uint)Mathf.FloorToInt(at.z / Area);
            List<Item> list; if (!areas.TryGetValue(key, out list)) areas[key] = list = new List<Item>();
            list.Add(new Item { proto = proto, phase = phase, matrix = Matrix4x4.TRS(new Vector3(at.x, at.y, at.z), Quaternion.Euler(0, yaw, 0), Vector3.one * scale) });
        }
        private void Emit(SortedDictionary<long, List<Item>> areas, string name, Material material)
        {
            if (material == null) return;
            foreach (var area in areas)
            {
                var items = area.Value; if (items.Count == 0) continue;
                var parts = new CombineInstance[items.Count];
                for (int i = 0; i < items.Count; i++) parts[i] = new CombineInstance { mesh = items[i].proto.mesh, transform = items[i].matrix };
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
                mesh.CombineMeshes(parts, true, true, false);
                // The current's phase differs per plant, so neighbours never sway in lockstep.
                var phase = mesh.uv2;
                if (phase.Length == mesh.vertexCount)
                {
                    int at = 0;
                    foreach (var item in items) { int end = Math.Min(phase.Length, at + item.proto.vertices); for (; at < end; at++) phase[at].y += item.phase; }
                    mesh.uv2 = phase;
                }
                var bounds = mesh.bounds; bounds.Expand(1f); mesh.bounds = bounds;
                made.Add(mesh);
                var go = new GameObject(name + " " + (spawned.Count + 1).ToString("00")); go.layer = plantLayer; go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                spawned.Add(go);
            }
        }
    }
}

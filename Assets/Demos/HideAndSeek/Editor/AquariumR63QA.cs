using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    // R63: painted corals and modelled sea stars.
    //  * RefreshBasicReef regenerates the default tank (AquariumEnvironment.Create, same random stream, so the same
    //    layout) and copies only its coral meshes (13 reef colonies, 17 shallow corals and sea stars, 32 cliff
    //    corals) into the saved mesh assets, checking the coral positions are unchanged; sets the coral material's
    //    pattern. Nothing else of the saved tank changes.
    //  * Lineups of the sea stars (4 species x 6 colourways) and the corals (9 forms x 3 colourways).
    public static class AquariumR63QA
    {
        private const string MeshFolder = "Assets/Demos/HideAndSeek/Environment/Resources/Aquarium/Meshes/";
        public static string RefreshBasicReef(string backup)
        {
            var log = new StringBuilder();
            var prefab = Resources.Load<GameObject>(AquariumEnvironment.ResourcePath);
            var saved = new Dictionary<string, MeshFilter>();
            foreach (var f in prefab.GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh != null && !saved.ContainsKey(f.name)) saved[f.name] = f;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var env = AquariumEnvironment.Create();
            log.Append("create ").Append(watch.ElapsedMilliseconds).Append(" ms\n");
            Material coral = null; int copied = 0;
            try
            {
                foreach (var f in env.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!(f.name.StartsWith("13 • ") || f.name.StartsWith("17 • ") || f.name.StartsWith("32 • "))) continue;
                    MeshFilter target; if (!saved.TryGetValue(f.name, out target)) { log.Append("no saved ").Append(f.name).Append('\n'); continue; }
                    var renderer = target.GetComponent<MeshRenderer>(); if (renderer != null && coral == null) coral = renderer.sharedMaterial;
                    var to = target.sharedMesh; var from = f.sharedMesh; string path = AssetDatabase.GetAssetPath(to);
                    if (string.IsNullOrEmpty(path) || !path.StartsWith(MeshFolder)) { log.Append("skip (not a saved mesh) ").Append(f.name).Append('\n'); continue; }
                    if (!string.IsNullOrEmpty(backup))
                    {
                        string copy = Path.Combine(backup, path.Substring("Assets/Demos/HideAndSeek/".Length)); Directory.CreateDirectory(Path.GetDirectoryName(copy));
                        if (!File.Exists(copy)) File.Copy(path, copy);
                    }
                    float moved = 0;
                    if (from.vertexCount == to.vertexCount) { var a = from.vertices; var b = to.vertices; for (int i = 0; i < a.Length; i++) moved = Mathf.Max(moved, (a[i] - b[i]).sqrMagnitude); }
                    log.Append(f.name).Append(": ").Append(to.vertexCount).Append(" -> ").Append(from.vertexCount).Append(" vertices");
                    if (from.vertexCount == to.vertexCount) log.Append(", max move ").Append(Mathf.Sqrt(moved).ToString("0.00000"));
                    log.Append('\n');
                    to.Clear(); to.indexFormat = from.indexFormat;
                    to.SetVertices(from.vertices); to.SetNormals(from.normals); to.SetTangents(from.tangents); to.SetColors(from.colors);
                    to.SetUVs(0, new List<Vector2>(from.uv)); to.SetUVs(1, new List<Vector2>(from.uv2)); to.SetTriangles(from.triangles, 0);
                    to.bounds = from.bounds; EditorUtility.SetDirty(to); copied++;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(env.gameObject); }
            if (coral != null)
            {
                coral.SetFloat("_Pattern", 1); coral.SetFloat("_Fluoro", .55f); EditorUtility.SetDirty(coral);
                log.Append("material ").Append(AssetDatabase.GetAssetPath(coral)).Append(" pattern on\n");
            }
            AssetDatabase.SaveAssets();
            log.Append("copied ").Append(copied).Append(" meshes, total ").Append(watch.ElapsedMilliseconds).Append(" ms\n");
            return log.ToString();
        }

        private static Material Sand()
        {
            var sandMat = new Material(Shader.Find("Aquarium/Natural Granular Sand")) { hideFlags = HideFlags.HideAndDontSave };
            sandMat.SetTexture("_MainTex", Resources.Load<Texture2D>("Aquarium/Textures/SandGrainAlbedo")); sandMat.SetTexture("_NormalTex", Resources.Load<Texture2D>("Aquarium/Textures/SandGrainNormal"));
            sandMat.SetTexture("_ARM", Resources.Load<Texture2D>("Aquarium/Textures/SandGrainARM")); sandMat.SetFloat("_WorldScale", .48f); sandMat.SetColor("_Color", AquariumEnvironment.C(0xe8d6b6));
            return sandMat;
        }
        private static void Slab(Transform root, Vector3 at, Vector3 scale)
        {
            var sand = GameObject.CreatePrimitive(PrimitiveType.Plane); sand.transform.SetParent(root, false);
            sand.transform.localScale = scale; sand.transform.position = at;
            UnityEngine.Object.DestroyImmediate(sand.GetComponent<Collider>());
            sand.GetComponent<Renderer>().sharedMaterial = Sand();
        }
        private static void Put(Transform root, string name, Mesh mesh, Material material, Vector3 at, float yaw, float scale)
        {
            var go = new GameObject(name); go.transform.SetParent(root, false);
            go.transform.position = at; go.transform.rotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        // Sea stars: species in rows (classic, cushion, brittle, sunflower), colourways in columns, 0.6 m apart.
        public static string RenderStarLineup(string output, string spec, int width = 1800, int height = 1000)
        {
            Func<Transform, IDisposable> build = root =>
            {
                Slab(root, new Vector3(1.5f, 0, 1), new Vector3(1, 1, .6f));
                var material = AquariumEnvironment.TwinCoralMaterial();
                for (int s = 0; s < AquariumEnvironment.StarSpeciesCount; s++)
                    for (int c = 0; c < AquariumEnvironment.StarColourways; c++)
                        Put(root, "star " + s + "/" + c, AquariumEnvironment.TwinStarMesh(s, c), material, new Vector3(c * .62f, .002f, s * .62f), 20 * c + 9 * s, .24f);
                return null;
            };
            return AquariumR58QA.Render(output, build, AquariumR58QA.Views(spec), width, height, new Color(.06f, .3f, .36f));
        }
        // Corals: forms in columns (staghorn, scroll, fan, brain, finger, tube, table, organ pipes, bubble), colourways in rows.
        public static string RenderCoralLineup(string output, string spec, int width = 1800, int height = 1000)
        {
            Func<Transform, IDisposable> build = root =>
            {
                Slab(root, new Vector3(4, 0, 1.2f), new Vector3(1.4f, 1, .7f));
                var material = AquariumEnvironment.TwinCoralMaterial();
                for (int k = 0; k < AquariumEnvironment.CoralKinds; k++)
                    for (int v = 0; v < 3; v++)
                        Put(root, "coral " + k + "/" + v, AquariumEnvironment.TwinCoralMesh(k, v), material, new Vector3(k * 1.0f, 0, v * 1.1f), 30 * k + 50 * v, 1.2f);
                return null;
            };
            return AquariumR58QA.Render(output, build, AquariumR58QA.Views(spec), width, height, new Color(.06f, .3f, .36f));
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    // R65: the default tank's reef rocks and anemone holdfasts joined into single stones. RefreshBasicStones
    // regenerates the tank (AquariumEnvironment.Create: same random stream, same layout), copies only the reef rock
    // covers (12) and the holdfast stones (36) into the saved meshes (the rock positions are checked unchanged) and
    // writes AquariumReefStoneTable (the stones, for the run-time joins where a reef rock meets a cliff).
    public static class AquariumR65QA
    {
        private const string MeshFolder = "Assets/Demos/HideAndSeek/Environment/Resources/Aquarium/Meshes/";
        private const string TablePath = "Assets/Demos/HideAndSeek/Scripts/AquariumReefStoneTable.cs";
        private static string Key(string name) { int cut = name.IndexOf(" / "); return cut < 0 ? name : name.Substring(0, cut); }
        public static string RefreshBasicStones(string backup)
        {
            var log = new StringBuilder();
            var prefab = Resources.Load<GameObject>(AquariumEnvironment.ResourcePath);
            var saved = new Dictionary<string, MeshFilter>();
            foreach (var f in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null && (f.name.StartsWith("12 • ") || f.name.StartsWith("36 • ")) && !saved.ContainsKey(Key(f.name))) saved[Key(f.name)] = f;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var env = AquariumEnvironment.Create();
            log.Append("create ").Append(watch.ElapsedMilliseconds).Append(" ms\n");
            int copied = 0;
            try
            {
                foreach (var f in env.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!(f.name.StartsWith("12 • ") || f.name.StartsWith("36 • "))) continue;
                    MeshFilter target; if (!saved.TryGetValue(Key(f.name), out target)) { log.Append("no saved ").Append(f.name).Append('\n'); continue; }
                    var to = target.sharedMesh; var from = f.sharedMesh; string path = AssetDatabase.GetAssetPath(to);
                    if (string.IsNullOrEmpty(path) || !path.StartsWith(MeshFolder)) { log.Append("skip ").Append(f.name).Append('\n'); continue; }
                    if (!string.IsNullOrEmpty(backup))
                    {
                        string copy = Path.Combine(backup, path.Substring("Assets/Demos/HideAndSeek/".Length)); Directory.CreateDirectory(Path.GetDirectoryName(copy));
                        if (!File.Exists(copy)) File.Copy(path, copy);
                    }
                    log.Append(Key(f.name)).Append(": ").Append(to.vertexCount).Append(" -> ").Append(from.vertexCount).Append(" vertices, bounds ").Append(to.bounds.center.ToString("F2")).Append(" -> ").Append(from.bounds.center.ToString("F2")).Append('\n');
                    to.Clear(); to.indexFormat = from.indexFormat;
                    to.SetVertices(from.vertices); to.SetNormals(from.normals); to.SetTangents(from.tangents); to.SetColors(from.colors);
                    to.SetUVs(0, new List<Vector2>(from.uv)); to.SetUVs(1, new List<Vector2>(from.uv2)); to.SetTriangles(from.triangles, 0);
                    to.bounds = from.bounds; EditorUtility.SetDirty(to); copied++;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(env.gameObject); }
            AssetDatabase.SaveAssets();
            // The stones table.
            var stones = AquariumEnvironment.RecordedReefStones();
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var text = new StringBuilder();
            text.Append("namespace PrototypeLab.Aquarium\r\n{\r\n    // R65: the default tank's reef rock stones (x, y, z, size x, y, z, colour r, g, b, roughness, join width; the\r\n    // authored layout), written by AquariumR65QA.RefreshBasicStones from the baked environment.\r\n");
            text.Append("    internal static class AquariumReefStoneTable\r\n    {\r\n        internal static readonly float[][] Stones =\r\n        {\r\n");
            int count = 0;
            foreach (var rock in stones)
            {
                text.Append("            new float[] { ");
                for (int i = 0; i < rock.Length; i++) { text.Append(rock[i].ToString("0.####", ic)).Append("f"); if (i + 1 < rock.Length) text.Append(", "); if (i % 11 == 10 && i + 1 < rock.Length) text.Append("\r\n                "); }
                text.Append(" },\r\n"); count += rock.Length / 11;
            }
            text.Append("        };\r\n    }\r\n}\r\n");
            File.WriteAllText(TablePath, text.ToString(), new UTF8Encoding(true));
            log.Append("copied ").Append(copied).Append(" meshes; table ").Append(stones.Length).Append(" rocks, ").Append(count).Append(" stones; total ").Append(watch.ElapsedMilliseconds).Append(" ms\n");
            return log.ToString();
        }
    }
}

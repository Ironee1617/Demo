using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium.Editor
{
    // R58: the divers hold the regulator in the mouth. Their old regulator hung at the throat on a hose from the
    // earpiece; the patch (ArtSources/Cartoon/DiverRegulator/<mesh>.dreg, made outside Unity) deletes that disc and
    // hose, caps the holes it leaves (yellow: its shell was fused to the collar) and appends a new regulator in
    // front of the mouth (black body, grey purge button, exhaust tee, side port) with its hose from under the
    // earpiece along the cheek. Every new part rides the Head bone. The yellow diver's cream collar tube, which
    // ran into the old disc, now ends in a small inflator fitting.
    // The original vertices keep their order (new ones are appended, deleted parts only lose their triangles), so
    // anything that indexes the diver's vertices still lines up. A patch applies only to the exact mesh it was
    // made from (vertex count + position sum, triangle count + index sum) and is skipped once applied.
    // The builders call it again after they rebuild a mesh: DiverVariants.Build (blue, pink) and
    // AquariumPaintCut.CutInPlace (yellow, after the paint cut).
    public static class DiverRegulator
    {
        const string Equipment = "Assets/Demos/HideAndSeek/Equipment/Resources/AquariumEquipment";
        static string Source => Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtSources/Cartoon/DiverRegulator"));
        public static readonly string[] Meshes = { "Diver", "Diver_Tracker", "Diver_Drone" };

        [MenuItem("Aquarium/Characters/Regulator in the divers' mouths")]
        public static void ApplyAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("DiverRegulator: leave play mode first."); return; }
            Debug.Log("DIVER_REGULATOR " + ApplyAllReport());
        }

        public static string ApplyAllReport()
        {
            string report = "";
            foreach (string name in Meshes) report += name + ": " + ApplyTo(name) + "; ";
            AssetDatabase.SaveAssets();
            return report;
        }

        public static string ApplyTo(string name)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Equipment + "/" + name + ".asset");
            return mesh == null ? "no mesh" : ApplyTo(mesh, name);
        }

        // "applied", "already applied", or why the mesh was left as it is.
        public static string ApplyTo(Mesh mesh, string name)
        {
            string path = Path.Combine(Source, name + ".dreg");
            if (!File.Exists(path)) return "no patch at " + path;
            using (var br = new BinaryReader(File.OpenRead(path)))
            {
                var magic = br.ReadBytes(4);
                if (magic.Length != 4 || magic[0] != 'D' || magic[1] != 'R' || magic[2] != 'E' || magic[3] != 'G' || br.ReadInt32() != 2) return "unreadable patch";
                int origNv = br.ReadInt32(); double posSum = br.ReadDouble(); int origNt = br.ReadInt32(); long triSum = br.ReadInt64();
                var deleted = Ints(br, br.ReadInt32());
                int addNv = br.ReadInt32();
                var addP = Floats(br, addNv * 3); var addN = Floats(br, addNv * 3); var addUv = Floats(br, addNv * 2); var addUv2 = Floats(br, addNv * 2);
                var addBi = Ints(br, addNv * 4); var addBw = Floats(br, addNv * 4);
                var addT = Ints(br, br.ReadInt32() * 3);

                var v = mesh.vertices; var tris = mesh.triangles;
                if (v.Length == origNv + addNv && tris.Length / 3 == origNt - deleted.Length + addT.Length / 3 && Math.Abs(PositionSum(v, origNv) - posSum) < .01) return "already applied";
                if (v.Length != origNv) return "vertex count " + v.Length + " (patch made for " + origNv + ")";
                if (tris.Length / 3 != origNt || Math.Abs(PositionSum(v, origNv) - posSum) >= .01 || TriangleSum(tris) != triSum) return "mesh differs from the one the patch was made for";

                var n = mesh.normals; var uv = mesh.uv; var uv2 = new List<Vector2>(); mesh.GetUVs(1, uv2); var bw = mesh.boneWeights; var bind = mesh.bindposes;
                if (n.Length != origNv || uv.Length != origNv || uv2.Count != origNv || bw.Length != origNv) return "mesh lacks normals, uv, uv2 or bone weights";

                var ov = new List<Vector3>(v); var on = new List<Vector3>(n); var ouv = new List<Vector2>(uv); var ouv2 = uv2; var obw = new List<BoneWeight>(bw);
                for (int i = 0; i < addNv; i++)
                {
                    ov.Add(new Vector3(addP[3 * i], addP[3 * i + 1], addP[3 * i + 2]));
                    on.Add(new Vector3(addN[3 * i], addN[3 * i + 1], addN[3 * i + 2]));
                    ouv.Add(new Vector2(addUv[2 * i], addUv[2 * i + 1])); ouv2.Add(new Vector2(addUv2[2 * i], addUv2[2 * i + 1]));
                    obw.Add(new BoneWeight
                    {
                        boneIndex0 = addBi[4 * i], boneIndex1 = addBi[4 * i + 1], boneIndex2 = addBi[4 * i + 2], boneIndex3 = addBi[4 * i + 3],
                        weight0 = addBw[4 * i], weight1 = addBw[4 * i + 1], weight2 = addBw[4 * i + 2], weight3 = addBw[4 * i + 3]
                    });
                }
                var gone = new bool[origNt]; foreach (int t in deleted) gone[t] = true;
                var ot = new List<int>(tris.Length - deleted.Length * 3 + addT.Length);
                for (int t = 0; t < origNt; t++) if (!gone[t]) { ot.Add(tris[3 * t]); ot.Add(tris[3 * t + 1]); ot.Add(tris[3 * t + 2]); }
                ot.AddRange(addT);

                mesh.Clear(false);
                mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(ov); mesh.SetNormals(on); mesh.SetUVs(0, ouv); mesh.SetUVs(1, ouv2);
                mesh.boneWeights = obw.ToArray(); mesh.bindposes = bind;
                mesh.SetTriangles(ot, 0); mesh.RecalculateTangents(); mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);
                return "applied";
            }
        }

        static double PositionSum(Vector3[] v, int count)
        {
            double s = 0; for (int i = 0; i < count && i < v.Length; i++) s += (double)v[i].x + v[i].y + v[i].z; return Math.Round(s, 3);
        }
        static long TriangleSum(int[] t)
        {
            long s = 0; for (int k = 0; k < t.Length; k++) s += (long)t[k] * (k % 13 + 1); return s;
        }
        static float[] Floats(BinaryReader br, int count) { var a = new float[count]; for (int i = 0; i < count; i++) a[i] = br.ReadSingle(); return a; }
        static int[] Ints(BinaryReader br, int count) { var a = new int[count]; for (int i = 0; i < count; i++) a[i] = br.ReadInt32(); return a; }
    }
}

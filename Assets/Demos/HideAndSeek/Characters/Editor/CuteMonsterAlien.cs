using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium.Editor
{
    // Cute monster as the playable alien.
    //  * Builds the skinned surface from the Blender build (CuteMonster.bin): textured body + palette parts
    //    (ventral mouth, teeth, eyelids), 33 bones (soft head chain + bulges, eye, antenna, 5 arms x 4),
    //    shapes the runtime expects: Metamorphosis, To_Matched_0125..1000 (same midpoint as Fish0),
    //    BellyOpen, Blink_25/50/75/100 + BlinkStep_xx.
    //  * Generates every alien clip slot with the 12 principles (see each Build* comment).
    //  * Replaces Resources/AquariumCharacters/Alien.prefab (the octopus prefab is backed up first).
    public static class CuteMonsterAlien
    {
        const string Folder = "Assets/Demos/HideAndSeek/Characters/Resources/AquariumCharacters";
        const string PrefabPath = Folder + "/Alien.prefab", OutFolder = Folder + "/CuteMonster";
        const string BackupFolder = "Assets/Demos/HideAndSeek/Characters/Backup";
        const float Frame = 1f / 30f;
        static string Source => System.Environment.GetEnvironmentVariable("CUTE_MONSTER_SOURCE") ?? @"D:\수족관술래잡기\cute monster 3d model\Edited";
        static readonly string[] ArmCodes = { "FR", "SR", "B", "SL", "FL" };
        const int ArmCount = 5, Joints = 4;

        // ================================================================ IMPORT
        sealed class Data
        {
            public Vector3[] pos, nrm; public Vector2[] uv, uv2; public BoneWeight[] weights; public int[] tri0, tri1;
            public string[] boneNames; public int[] parents; public Vector3[] heads, tails;
            public List<(string name, Dictionary<int, (Vector3 dp, Vector3 dn)> delta)> shapes = new List<(string, Dictionary<int, (Vector3, Vector3)>)>();
            public Vector3 mouth, throat;
        }
        static Vector3 V3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        static Data Read()
        {
            var d = new Data();
            using (var r = new BinaryReader(File.OpenRead(Path.Combine(Source, "CuteMonster.bin"))))
            {
                if (new string(r.ReadChars(4)) != "CMON") throw new System.Exception("Not a CuteMonster.bin");
                r.ReadInt32(); int n = r.ReadInt32(), n0 = r.ReadInt32(), n1 = r.ReadInt32(), nb = r.ReadInt32(), ns = r.ReadInt32();
                d.pos = new Vector3[n]; d.nrm = new Vector3[n]; d.uv = new Vector2[n]; d.uv2 = new Vector2[n]; d.weights = new BoneWeight[n];
                for (int i = 0; i < n; i++)
                {
                    d.pos[i] = V3(r); d.nrm[i] = V3(r); d.uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle()); d.uv2[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    int a = r.ReadInt32(), b = r.ReadInt32(), c = r.ReadInt32(), e = r.ReadInt32();
                    d.weights[i] = new BoneWeight { boneIndex0 = a, boneIndex1 = b, boneIndex2 = c, boneIndex3 = e, weight0 = r.ReadSingle(), weight1 = r.ReadSingle(), weight2 = r.ReadSingle(), weight3 = r.ReadSingle() };
                }
                d.tri0 = new int[n0]; for (int i = 0; i < n0; i++) d.tri0[i] = r.ReadInt32();
                d.tri1 = new int[n1]; for (int i = 0; i < n1; i++) d.tri1[i] = r.ReadInt32();
                d.boneNames = new string[nb]; d.parents = new int[nb]; d.heads = new Vector3[nb]; d.tails = new Vector3[nb];
                for (int i = 0; i < nb; i++) { d.boneNames[i] = Rename(r.ReadString()); d.parents[i] = r.ReadInt32(); d.heads[i] = V3(r); d.tails[i] = V3(r); }
                for (int s = 0; s < ns; s++)
                {
                    string name = r.ReadString(); int count = r.ReadInt32(); var delta = new Dictionary<int, (Vector3, Vector3)>(count);
                    for (int k = 0; k < count; k++) { int i = r.ReadInt32(); delta[i] = (V3(r), V3(r)); }
                    d.shapes.Add((name, delta));
                }
                d.mouth = V3(r); d.throat = V3(r);
            }
            // Body pivots at the centre of mass (rig origin) so rolls/pitches turn the whole creature in place.
            for (int i = 0; i < d.boneNames.Length; i++) if (d.boneNames[i] == "Body") d.heads[i] = Vector3.zero;
            return d;
        }
        // Arm_FR_1 -> Arm0_0 (AlienArmDrag reads Arm{i}_{k})
        static string Rename(string n)
        {
            if (!n.StartsWith("Arm_")) return n;
            var p = n.Split('_'); return "Arm" + System.Array.IndexOf(ArmCodes, p[1]) + "_" + (int.Parse(p[2]) - 1);
        }
        static T SaveAsset<T>(T value, string path) where T : Object
        {
            var old = AssetDatabase.LoadAssetAtPath<T>(path);
            if (old == null) { AssetDatabase.CreateAsset(value, path); return value; }
            if (value is Mesh source && old is Mesh target)
            {
                target.Clear(false); target.ClearBlendShapes(); target.indexFormat = source.indexFormat; target.vertices = source.vertices; target.normals = source.normals; target.tangents = source.tangents;
                target.uv = source.uv; target.uv2 = source.uv2; target.subMeshCount = source.subMeshCount;
                for (int i = 0; i < source.subMeshCount; i++) target.SetTriangles(source.GetTriangles(i), i);
                target.bindposes = source.bindposes; target.boneWeights = source.boneWeights;
                for (int i = 0; i < source.blendShapeCount; i++) { var v = new Vector3[source.vertexCount]; var n = new Vector3[v.Length]; var t = new Vector3[v.Length]; source.GetBlendShapeFrameVertices(i, 0, v, n, t); target.AddBlendShapeFrame(source.GetBlendShapeName(i), 100, v, n, t); }
                target.bounds = source.bounds; EditorUtility.SetDirty(target); Object.DestroyImmediate(source); return old;
            }
            EditorUtility.CopySerialized(value, old); Object.DestroyImmediate(value); EditorUtility.SetDirty(old); return old;
        }
        static Texture2D ImportTexture(string file, string asset, bool palette)
        {
            string path = OutFolder + "/" + asset; File.Copy(Path.Combine(Source, file), path, true); AssetDatabase.ImportAsset(path);
            var im = (TextureImporter)AssetImporter.GetAtPath(path);
            im.sRGBTexture = true; im.wrapMode = TextureWrapMode.Clamp;
            if (palette) { im.mipmapEnabled = false; im.filterMode = FilterMode.Bilinear; im.textureCompression = TextureImporterCompression.Uncompressed; im.npotScale = TextureImporterNPOTScale.None; }
            else { im.maxTextureSize = 2048; im.mipmapEnabled = true; im.anisoLevel = 4; im.textureCompression = TextureImporterCompression.CompressedHQ; }
            im.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material MakeMaterial(string name, Texture2D tex)
        {
            var m = new Material(Shader.Find("Aquarium/Cartoon Character")) { name = name }; m.mainTexture = tex;
            return SaveAsset(m, OutFolder + "/" + name + ".mat");
        }
        static Mesh BuildMesh(Data d, Transform[] bones, Transform root)
        {
            var mesh = new Mesh { name = "CuteMonster cartoon surface", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = d.pos; mesh.normals = d.nrm; mesh.uv = d.uv; mesh.uv2 = d.uv2; mesh.subMeshCount = 2;
            mesh.SetTriangles(d.tri0, 0); mesh.SetTriangles(d.tri1, 1);
            mesh.boneWeights = d.weights; mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * root.localToWorldMatrix).ToArray();
            int n = d.pos.Length;
            foreach (var (name, delta) in d.shapes)
            {
                var dv = new Vector3[n]; var dn = new Vector3[n];
                foreach (var p in delta) { dv[p.Key] = p.Value.dp; dn[p.Key] = p.Value.dn; }
                mesh.AddBlendShapeFrame(name, 100, dv, dn, null);
            }
            // R19: the eyelid shell follows the eyeball it closes over (see EyelidFollowsEye).
            var blink = d.shapes.Find(s => s.name == "Blink_100").delta;
            if (blink != null)
            {
                var closed = new Vector3[n]; foreach (var p in blink) closed[p.Key] = p.Value.dp;
                mesh.boneWeights = EyelidFollowsEye(d.pos, closed, d.weights, d.tri0.Concat(d.tri1).ToArray(), d.boneNames);
                LowerLidColour(mesh);
            }
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return SaveAsset(mesh, OutFolder + "/CuteMonster.asset");
        }

        // ================================================================ EYELID SKIN (R19)
        // The eyelid shell rests inside the head and the Blink shapes slide it out over the
        // eyeball. It used to be skinned like the head interior it rests in (Head_Top/Base),
        // while the eyeball follows Eye under Head_Mid, so the jelly squash, the head sway and
        // the bulges pushed the eye through the closing lid. Every lid vertex is now skinned
        // like what it covers when closed: the eyeball (Head_Mid, plus a little gaze) and the
        // socket skin at its rim. Lid and eye then share one transform and keep their gap.
        public const float LidGaze = .35f;
        public static BoneWeight[] EyelidFollowsEye(Vector3[] pos, Vector3[] closedDelta, BoneWeight[] weights, int[] triangles, string[] boneNames)
        {
            int n = pos.Length, nb = boneNames.Length; var result = (BoneWeight[])weights.Clone();
            int eye = System.Array.IndexOf(boneNames, "Eye"), mid = System.Array.IndexOf(boneNames, "Head_Mid");
            var lidIndex = new int[n]; var lids = new List<int>();
            for (int i = 0; i < n; i++) { lidIndex[i] = -1; if (closedDelta[i].sqrMagnitude > 1e-10f) { lidIndex[i] = lids.Count; lids.Add(i); } }
            if (lids.Count == 0 || eye < 0 || mid < 0) return result;
            var bounds = new Bounds(pos[lids[0]] + closedDelta[lids[0]], Vector3.zero);
            foreach (int i in lids) bounds.Encapsulate(pos[i] + closedDelta[i]);
            bounds.Expand(.1f);
            var targets = new List<int>();
            for (int j = 0; j < n; j++) if (lidIndex[j] < 0 && bounds.Contains(pos[j])) targets.Add(j);
            if (targets.Count == 0) return result;
            // Inverse-distance blend of the 4 nearest surfaces under the closed lid.
            var dense = new float[lids.Count, nb];
            const int K = 4; var bestJ = new int[K]; var bestD = new float[K];
            for (int l = 0; l < lids.Count; l++)
            {
                Vector3 c = pos[lids[l]] + closedDelta[lids[l]];
                for (int k = 0; k < K; k++) { bestJ[k] = -1; bestD[k] = float.MaxValue; }
                foreach (int j in targets)
                {
                    float dist = (pos[j] - c).sqrMagnitude; if (dist >= bestD[K - 1]) continue;
                    int k = K - 1; while (k > 0 && bestD[k - 1] > dist) { bestD[k] = bestD[k - 1]; bestJ[k] = bestJ[k - 1]; k--; }
                    bestD[k] = dist; bestJ[k] = j;
                }
                float total = 0;
                for (int k = 0; k < K; k++)
                {
                    if (bestJ[k] < 0) continue; float w = 1 / (Mathf.Sqrt(bestD[k]) + .004f); w *= w; total += w;
                    var bw = weights[bestJ[k]];
                    dense[l, bw.boneIndex0] += bw.weight0 * w; dense[l, bw.boneIndex1] += bw.weight1 * w; dense[l, bw.boneIndex2] += bw.weight2 * w; dense[l, bw.boneIndex3] += bw.weight3 * w;
                }
                for (int b = 0; b < nb; b++) dense[l, b] /= total;
                // The eyeball rolls with gaze inside the lid; the lid follows only a little of it.
                float e = dense[l, eye]; dense[l, eye] = e * LidGaze; dense[l, mid] += e * (1 - LidGaze);
            }
            // Relax across the lid surface so the eyeball/skin hand-off has no seam.
            var neighbours = new List<int>[lids.Count]; for (int l = 0; l < lids.Count; l++) neighbours[l] = new List<int>();
            for (int t = 0; t + 2 < triangles.Length; t += 3)
                for (int a = 0; a < 3; a++)
                {
                    int p = lidIndex[triangles[t + a]], q = lidIndex[triangles[t + (a + 1) % 3]];
                    if (p >= 0 && q >= 0) { neighbours[p].Add(q); neighbours[q].Add(p); }
                }
            var next = new float[lids.Count, nb];
            for (int pass = 0; pass < 4; pass++)
            {
                for (int l = 0; l < lids.Count; l++)
                {
                    int m = neighbours[l].Count;
                    for (int b = 0; b < nb; b++) { float sum = 0; foreach (int q in neighbours[l]) sum += dense[q, b]; next[l, b] = m == 0 ? dense[l, b] : .5f * dense[l, b] + .5f * sum / m; }
                }
                var swap = dense; dense = next; next = swap;
            }
            // Split vertices (normal / UV seams) share one weight set, so the shell never cracks.
            var groups = new Dictionary<Vector3Int, List<int>>();
            for (int l = 0; l < lids.Count; l++)
            {
                var p = pos[lids[l]]; var key = new Vector3Int(Mathf.RoundToInt(p.x * 1e5f), Mathf.RoundToInt(p.y * 1e5f), Mathf.RoundToInt(p.z * 1e5f));
                if (!groups.TryGetValue(key, out var g)) groups[key] = g = new List<int>(); g.Add(l);
            }
            var shared = new float[nb];
            foreach (var g in groups.Values)
            {
                for (int b = 0; b < nb; b++) { float sum = 0; foreach (int l in g) sum += dense[l, b]; shared[b] = sum / g.Count; }
                var top = TopFour(shared);
                foreach (int l in g) result[lids[l]] = top;
            }
            return result;
        }
        static BoneWeight TopFour(float[] w)
        {
            var idx = new int[4]; var val = new float[4];
            for (int b = 0; b < w.Length; b++)
            {
                float x = w[b]; if (x <= val[3]) continue;
                int k = 3; while (k > 0 && val[k - 1] < x) { val[k] = val[k - 1]; idx[k] = idx[k - 1]; k--; }
                val[k] = x; idx[k] = b;
            }
            float s = val[0] + val[1] + val[2] + val[3]; if (s <= 0) s = 1;
            return new BoneWeight { boneIndex0 = idx[0], weight0 = val[0] / s, boneIndex1 = idx[1], weight1 = val[1] / s, boneIndex2 = idx[2], weight2 = val[2] / s, boneIndex3 = idx[3], weight3 = val[3] / s };
        }
        // ================================================================ LOWER LID COLOUR (R20)
        // The lower lid is a mirrored copy of the upper one, so its winding is reversed: the skin-coloured
        // outer layer faces into the eye (culled) and the pink inner layer faces out. A shut eye then read as
        // half skin, half pink. The two layers share their layout (1080 vertices each, same ring order), so
        // swapping their palette UVs puts the skin colour and lash line on the layer that is actually seen.
        // Returns 1 when fixed, 0 when already fixed, -1 when the mesh does not match the expected layout.
        public static int LowerLidColour(Mesh mesh)
        {
            int key = mesh.GetBlendShapeIndex("Blink_100"); if (key < 0) return -1;
            int n = mesh.vertexCount; var delta = new Vector3[n]; mesh.GetBlendShapeFrameVertices(key, mesh.GetBlendShapeFrameCount(key) - 1, delta, null, null);
            var lids = new List<int>(); for (int i = 0; i < n; i++) if (delta[i].sqrMagnitude > 1e-14f) lids.Add(i);
            if (lids.Count != 4320 || lids[lids.Count - 1] - lids[0] != lids.Count - 1) return -1;
            var pos = mesh.vertices; var uv = mesh.uv; const int lid = 2160, half = 1080;
            // The lower lid is the block whose closed position sits lower.
            float y0 = 0, y1 = 0; for (int i = 0; i < lid; i++) { y0 += pos[lids[i]].y + delta[lids[i]].y; y1 += pos[lids[lid + i]].y + delta[lids[lid + i]].y; }
            int start = lids[0] + (y1 < y0 ? lid : 0);
            bool Row(int from, float v) { for (int i = 0; i < half; i++) if (Mathf.Abs(uv[from + i].y - v) > .01f) return false; return true; }
            const float skinRow = 1 - 1.5f / 4, innerRow = 1 - 2.5f / 4;
            if (Row(start, innerRow) && Row(start + half, skinRow)) return 0;
            if (!Row(start, skinRow) || !Row(start + half, innerRow)) return -1;
            // Confirm the skin layer really faces inward before swapping.
            var tri = mesh.GetTriangles(1); int inward = 0, total = 0;
            for (int t = 0; t < tri.Length; t += 3)
            {
                int a = tri[t], b = tri[t + 1], c = tri[t + 2];
                if (a < start || a >= start + half || b < start || b >= start + half || c < start || c >= start + half) continue;
                var pa = pos[a] + delta[a]; var normal = Vector3.Cross(pos[b] + delta[b] - pa, pos[c] + delta[c] - pa);
                total++; if (normal.z < 0) inward++;
            }
            if (total == 0 || inward * 2 < total) return -1;
            for (int i = 0; i < half; i++) { var keep = uv[start + i]; uv[start + i] = uv[start + half + i]; uv[start + half + i] = keep; }
            mesh.uv = uv; return 1;
        }
        [MenuItem("Aquarium/Characters/Fix cute monster lower eyelid colour (R20)")]
        public static void FixLowerLid()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); var rig = prefab != null ? prefab.GetComponent<AquariumRig>() : null;
            if (rig == null || rig.skin == null || !rig.skin.sharedMesh.name.StartsWith("CuteMonster")) { Debug.LogWarning("Cute monster alien not found"); return; }
            var mesh = rig.skin.sharedMesh; int result = LowerLidColour(mesh);
            if (result == 1) { EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssets(); Debug.Log("CUTE_MONSTER_LOWER_LID_FIXED"); }
            else if (result == 0) Debug.Log("CUTE_MONSTER_LOWER_LID_OK (already skin coloured)");
            else Debug.LogWarning("CUTE_MONSTER_LOWER_LID_UNEXPECTED_LAYOUT");
        }

        // Re-skins the eyelid of the existing alien mesh in place (prefab, clips and springs untouched).
        [MenuItem("Aquarium/Characters/Fix cute monster eyelid skinning (R19)")]
        public static void FixEyelids()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); var rig = prefab != null ? prefab.GetComponent<AquariumRig>() : null;
            if (rig == null || rig.skin == null || !rig.skin.sharedMesh.name.StartsWith("CuteMonster")) { Debug.LogWarning("Cute monster alien not found"); return; }
            var mesh = rig.skin.sharedMesh; int key = mesh.GetBlendShapeIndex("Blink_100"); if (key < 0) { Debug.LogWarning("No Blink_100"); return; }
            var closed = new Vector3[mesh.vertexCount]; mesh.GetBlendShapeFrameVertices(key, 0, closed, null, null);
            var before = mesh.boneWeights; var after = EyelidFollowsEye(mesh.vertices, closed, before, mesh.triangles, rig.skin.bones.Select(b => b.name).ToArray());
            int changed = 0;
            for (int i = 0; i < before.Length; i++)
            {
                var a = before[i]; var b = after[i];
                if (a.boneIndex0 != b.boneIndex0 || a.boneIndex1 != b.boneIndex1 || a.boneIndex2 != b.boneIndex2 || a.boneIndex3 != b.boneIndex3
                    || Mathf.Abs(a.weight0 - b.weight0) > 1e-4f || Mathf.Abs(a.weight1 - b.weight1) > 1e-4f || Mathf.Abs(a.weight2 - b.weight2) > 1e-4f || Mathf.Abs(a.weight3 - b.weight3) > 1e-4f) changed++;
            }
            if (changed == 0) { Debug.Log("CUTE_MONSTER_EYELID_OK (already follows the eye)"); return; }
            mesh.boneWeights = after; EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssets();
            Debug.Log("CUTE_MONSTER_EYELID_FIXED " + changed + " vertices");
        }

        [MenuItem("Aquarium/Characters/Apply cute monster alien")]
        public static void Apply()
        {
            if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder(Folder, "CuteMonster");
            if (!AssetDatabase.IsValidFolder(BackupFolder)) AssetDatabase.CreateFolder("Assets/Demos/HideAndSeek/Characters", "Backup");
            string backup = BackupFolder + "/Alien_Octopus.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(backup) == null && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                var old = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<AquariumRig>();
                if (old == null || old.skin == null || !old.skin.sharedMesh.name.StartsWith("CuteMonster")) AssetDatabase.CopyAsset(PrefabPath, backup);
            }
            var d = Read();
            var atlas = ImportTexture("Monster_basecolor.png", "CuteMonster_Atlas.png", false);
            var palette = ImportTexture("CuteMonster_Palette.png", "CuteMonster_Palette.png", true);
            var materials = new[] { MakeMaterial("CuteMonster_Cartoon", atlas), MakeMaterial("CuteMonster_Parts", palette) };

            var go = new GameObject("Alien"); var rig = go.AddComponent<AquariumRig>(); var bones = new Transform[d.boneNames.Length];
            try
            {
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i] = new GameObject(d.boneNames[i]).transform; bones[i].SetParent(d.parents[i] < 0 ? go.transform : bones[d.parents[i]], false);
                    bones[i].position = d.heads[i]; bones[i].localRotation = Quaternion.identity;
                }
                var skinGo = new GameObject("Deforming surface"); skinGo.transform.SetParent(go.transform, false);
                var skin = skinGo.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = BuildMesh(d, bones, go.transform); skin.bones = bones; skin.rootBone = bones[0]; skin.quality = SkinQuality.Bone4;
                skin.localBounds = new Bounds(Vector3.zero, Vector3.one * 5); skin.sharedMaterials = materials; skin.receiveShadows = false;
                rig.skin = skin; rig.cartoon = true; rig.cartoonKind = "Alien";
                rig.restPositions = bones.Select(b => b.localPosition).ToArray(); rig.restRotations = bones.Select(b => b.localRotation).ToArray(); rig.restScales = bones.Select(b => b.localScale).ToArray();
                rig.mouthAnchor = d.mouth; rig.throatAnchor = d.throat;
                rig.clips = new AnimationClip[0];
                go.AddComponent<AquariumMorphMotion>(); go.AddComponent<AlienArmDrag>(); go.AddComponent<AquariumSpringBones>();
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            }
            finally { Object.DestroyImmediate(go); }
            AssetDatabase.SaveAssets();

            // ---- clips
            var poser = new Poser(); var clips = new List<AnimationClip>(); AnimationClip leave, arrive;
            try
            {
                clips.Add(Save(BuildIdle(poser))); clips.Add(Save(BuildSwim(poser)));
                clips.Add(Save(BuildEat(poser, "Monster_Bite"))); clips.Add(Save(BuildEat(poser, "Monster_TentacleEat")));
                clips.Add(Save(BuildEaten(poser))); clips.Add(Save(BuildHarpoon(poser)));
                leave = Save(BuildMorphLeave(poser)); arrive = Save(BuildMorphArrive(poser)); clips.Add(leave); clips.Add(arrive);
                clips.Add(Save(BuildSpitRecoil(poser))); clips.Add(Save(BuildSpitOut(poser)));
            }
            finally { Object.DestroyImmediate(poser.go); }
            var rootGo = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var r = rootGo.GetComponent<AquariumRig>(); r.clips = clips.ToArray();
                var morph = rootGo.GetComponent<AquariumMorphMotion>(); morph.leave = leave; morph.arrive = arrive;
                PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(rootGo); }
            AssetDatabase.SaveAssets();
            Debug.Log("CUTE_MONSTER_ALIEN_OK " + clips.Count + " clips");
        }
        static AnimationClip Save(AnimationClip clip)
        {
            string path = OutFolder + "/" + clip.name + ".anim"; var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (old == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
            EditorUtility.CopySerialized(clip, old); Object.DestroyImmediate(clip); EditorUtility.SetDirty(old); return old;
        }

        // ================================================================ POSING
        sealed class Recorder
        {
            readonly Dictionary<(string, System.Type, string), List<Keyframe>> curves = new Dictionary<(string, System.Type, string), List<Keyframe>>();
            readonly Dictionary<string, Quaternion> lastRotation = new Dictionary<string, Quaternion>();
            void Key(string path, System.Type type, string prop, float time, float value)
            {
                var id = (path, type, prop); if (!curves.TryGetValue(id, out var list)) curves[id] = list = new List<Keyframe>();
                list.Add(new Keyframe(time, value));
            }
            public void Shape(string name, float time, float value) => Key("Deforming surface", typeof(SkinnedMeshRenderer), "blendShape." + name, time, value);
            public void Transform(string path, Transform t, float time)
            {
                var p = t.localPosition; var s = t.localScale; var q = t.localRotation;
                if (lastRotation.TryGetValue(path, out var prev) && Quaternion.Dot(prev, q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                lastRotation[path] = q; var T = typeof(Transform);
                Key(path, T, "m_LocalPosition.x", time, p.x); Key(path, T, "m_LocalPosition.y", time, p.y); Key(path, T, "m_LocalPosition.z", time, p.z);
                Key(path, T, "m_LocalRotation.x", time, q.x); Key(path, T, "m_LocalRotation.y", time, q.y); Key(path, T, "m_LocalRotation.z", time, q.z); Key(path, T, "m_LocalRotation.w", time, q.w);
                Key(path, T, "m_LocalScale.x", time, s.x); Key(path, T, "m_LocalScale.y", time, s.y); Key(path, T, "m_LocalScale.z", time, s.z);
            }
            public AnimationClip Build(string name, bool loop)
            {
                var clip = new AnimationClip { name = name, legacy = true, frameRate = 30, wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever };
                foreach (var pair in curves)
                {
                    var curve = new AnimationCurve(pair.Value.ToArray());
                    for (int i = 0; i < curve.length; i++) { AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto); AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto); }
                    if (loop && curve.length > 2)
                    {   // seamless loop: first/last share the same slope (from the neighbours across the seam)
                        var a = curve[0]; var b = curve[curve.length - 1];
                        float slope = (curve[1].value - curve[curve.length - 2].value) / (curve[1].time + (b.time - curve[curve.length - 2].time));
                        a.inTangent = a.outTangent = b.inTangent = b.outTangent = slope; curve.MoveKey(0, a); curve.MoveKey(curve.length - 1, b);
                    }
                    clip.SetCurve(pair.Key.Item1, pair.Key.Item2, pair.Key.Item3, curve);
                }
                clip.EnsureQuaternionContinuity(); return clip;
            }
        }

        // One pose of the whole character. All values are in Body space; the poser spreads them over the chains.
        struct Pose
        {
            public Vector3 bodyPos, bodyRot, bodyScale, headLean, antenna, eye; public float stretch;
            public Vector4[] armAim; public float[] curl, wave; public float wavePhase;
        }
        sealed class Poser
        {
            public GameObject go; public AquariumRig rig; public Transform body, mouth, eye, a1, a2; public Transform[] head = new Transform[3]; public Transform bL, bR, bB, bC;
            public Transform[,] arm = new Transform[ArmCount, Joints];
            public Vector3[] dir = new Vector3[ArmCount], radial = new Vector3[ArmCount], bend = new Vector3[ArmCount]; public int[] group = new int[ArmCount];
            public float End = 3;
            Transform B(string n) => rig.skin.bones.First(b => b.name == n);
            public Poser()
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                rig = go.GetComponent<AquariumRig>(); body = B("Body"); mouth = B("Mouth"); eye = B("Eye"); a1 = B("Antenna_1"); a2 = B("Antenna_2");
                head[0] = B("Head_Base"); head[1] = B("Head_Mid"); head[2] = B("Head_Top");
                bL = B("Head_Bulge_L"); bR = B("Head_Bulge_R"); bB = B("Head_Bulge_Back"); bC = B("Head_Bulge_Crown");
                for (int i = 0; i < ArmCount; i++)
                {
                    for (int k = 0; k < Joints; k++) arm[i, k] = B("Arm" + i + "_" + k);
                    var root = arm[i, 0].localPosition; var tip = arm[i, Joints - 1].position - arm[i, 0].position;
                    dir[i] = body.InverseTransformDirection(tip).normalized; radial[i] = new Vector3(root.x, 0, root.z).normalized;
                    bend[i] = Vector3.Cross(dir[i], Vector3.down).normalized;
                    group[i] = radial[i].z > .3f ? 0 : radial[i].z > -.5f ? 1 : 2;
                }
            }
            public string Path(Transform t) => AnimationUtility.CalculateTransformPath(t, go.transform);
            public void Reset() { var b = rig.skin.bones; for (int i = 0; i < b.Length; i++) { b[i].localPosition = rig.restPositions[i]; b[i].localRotation = rig.restRotations[i]; b[i].localScale = rig.restScales[i]; } }
            int Index(Transform t) => System.Array.IndexOf(rig.skin.bones, t);
            static readonly float[] Share = { 0, .30f, .34f, .36f };
            const float MaxRoot = 30;
            // Apply the pose; `at(lag)` returns the pose `lag` seconds earlier so every link trails its parent.
            public void Apply(System.Func<float, Pose> at)
            {
                Reset();
                var p = at(0);
                body.localPosition = rig.restPositions[Index(body)] + p.bodyPos; body.localRotation = Quaternion.Euler(p.bodyRot); body.localScale = p.bodyScale;
                // soft head: each link a frame later than the one below it (overlapping action)
                for (int h = 0; h < 3; h++)
                {
                    var q = at((h + 1) * Frame); float share = h == 0 ? .30f : .35f;
                    head[h].localRotation = Quaternion.Euler(q.headLean * share);
                    float st = h == 0 ? q.stretch * .45f : h == 1 ? q.stretch : q.stretch * .35f, side = 1 / Mathf.Sqrt(1 + st);
                    head[h].localScale = new Vector3(side, 1 + st, side);
                }
                var m = at(2 * Frame);
                float bulge = -m.stretch;                               // squash pushes the sides out, stretch pulls them in
                bL.localScale = bR.localScale = Vector3.one * (1 + .55f * bulge); bB.localScale = Vector3.one * (1 + .45f * bulge); bC.localScale = Vector3.one * (1 + .5f * m.stretch);
                var e = at(0); eye.localRotation = Quaternion.Euler(e.eye);
                // antenna: big follow-through, two links of delay each
                var a = at(4 * Frame); var b = at(6 * Frame);
                a1.localRotation = Quaternion.Euler(a.antenna * .55f + a.headLean * .25f); a2.localRotation = Quaternion.Euler(b.antenna * .8f + b.headLean * .2f);
                // The root joint sits inside the skirt: it only turns up to MaxRoot degrees, the rest of the
                // aim is carried by the next two joints so the skin around the root never tears into a sheet.
                for (int i = 0; i < ArmCount; i++)
                    for (int k = 0; k < Joints; k++)
                    {
                        var q = at((group[i] + k) * Frame);
                        var w = q.armAim[i];
                        var aim = dir[i] * w.x + radial[i] * w.y + Vector3.up * w.z + Vector3.forward * w.w;
                        var full = aim.sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.FromToRotation(dir[i], aim.normalized);
                        float angle = Quaternion.Angle(Quaternion.identity, full);
                        var root = angle > MaxRoot ? Quaternion.Slerp(Quaternion.identity, full, MaxRoot / angle) : full;
                        var rest = Quaternion.Inverse(root) * full;
                        if (k == 0) { arm[i, 0].localRotation = root; continue; }
                        var carry = k == 1 ? Quaternion.Slerp(Quaternion.identity, rest, .6f) : k == 2 ? Quaternion.Slerp(Quaternion.identity, Quaternion.Inverse(Quaternion.Slerp(Quaternion.identity, rest, .6f)) * rest, 1) : Quaternion.identity;
                        arm[i, k].localRotation = Quaternion.AngleAxis(q.curl[i] * Share[k] + q.wave[i] * Mathf.Sin(q.wavePhase + i * 1.26f - k * .8f) * (.5f + .25f * k), bend[i]) * carry;
                    }
            }
            public void Record(Recorder rec, float time) { foreach (var b in rig.skin.bones) rec.Transform(Path(b), b, time); }
        }
        // ---------------------------------------------------------------- keyed tracks (slow in / slow out between keys)
        struct K { public float t; public Vector4 v; public K(float t, float x, float y = 0, float z = 0, float w = 0) { this.t = t; v = new Vector4(x, y, z, w); } }
        static Vector4 Ev(K[] keys, float t)
        {
            if (t <= keys[0].t) return keys[0].v; if (t >= keys[keys.Length - 1].t) return keys[keys.Length - 1].v;
            for (int i = 0; i < keys.Length - 1; i++)
                if (t <= keys[i + 1].t) return Vector4.Lerp(keys[i].v, keys[i + 1].v, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(keys[i].t, keys[i + 1].t, t)));
            return keys[keys.Length - 1].v;
        }
        static Vector3 E3(K[] k, float t) => Ev(k, t);
        static float E1(K[] k, float t) => Ev(k, t).x;
        sealed class Spec
        {
            public float end; public bool loop;
            public K[] bodyPos, bodyRot, bodyScale, headLean, stretch, antenna, eye, curl, wave, blink, belly;
            public K[][] armAim;                  // per arm group (front, side, back) or a single entry for all
            public System.Func<int, float, Vector4> armExtra;   // optional per-arm offset (flails etc.)
        }
        static Pose Sample(Spec s, Poser p, float t)
        {
            var pose = new Pose
            {
                bodyPos = E3(s.bodyPos, t), bodyRot = E3(s.bodyRot, t), bodyScale = s.bodyScale == null ? Vector3.one : E3(s.bodyScale, t),
                headLean = E3(s.headLean, t), stretch = E1(s.stretch, t), antenna = s.antenna == null ? Vector3.zero : E3(s.antenna, t), eye = s.eye == null ? Vector3.zero : E3(s.eye, t),
                armAim = new Vector4[ArmCount], curl = new float[ArmCount], wave = new float[ArmCount], wavePhase = t * 9
            };
            for (int i = 0; i < ArmCount; i++)
            {
                var track = s.armAim[s.armAim.Length == 1 ? 0 : p.group[i]];
                pose.armAim[i] = Ev(track, t) + (s.armExtra == null ? Vector4.zero : s.armExtra(i, t));
                pose.curl[i] = E1(s.curl, t); pose.wave[i] = s.wave == null ? 0 : E1(s.wave, t);
            }
            return pose;
        }
        static AnimationClip Render(Poser p, Spec s, string name)
        {
            var rec = new Recorder(); p.End = s.end; int frames = Mathf.RoundToInt(s.end * 30);
            for (int f = 0; f <= frames; f++)
            {
                float t = f * Frame;
                p.Apply(lag => Sample(s, p, s.loop ? Mathf.Repeat(t - lag, s.end) : Mathf.Clamp(t - lag, 0, s.end)));
                p.Record(rec, t);
                float blink = s.blink == null ? 0 : E1(s.blink, t), belly = s.belly == null ? 0 : E1(s.belly, t);
                for (int i = 1; i <= 12; i++)
                {
                    string key = i % 3 == 0 ? "Blink_" + (i * 100 / 12) : "BlinkStep_" + i.ToString("00");
                    rec.Shape(key, t, 100 * (i == 12 ? Mathf.Clamp01(blink * 12 - 11) : Mathf.Max(0, 1 - Mathf.Abs(blink * 12 - i))));
                }
                rec.Shape("BellyOpen", t, belly);
            }
            return rec.Build(name, s.loop);
        }
        static K[] C(float v) => new[] { new K(0, v) };
        static K[] C3(Vector3 v) => new[] { new K(0, v.x, v.y, v.z) };
        // rest aim: along the sculpted arm (x = rest direction weight, y = radial, z = up, w = forward)
        static readonly Vector4 Rest = new Vector4(1, 0, 0, 0);

        // ================================================================ IDLE  (2.4 s loop)
        // Secondary action only: breathing swells the soft head (2.5 %), the body bobs, every arm ripples
        // a fifth of a cycle after its neighbour, the antenna trails the head by 4-6 frames, one blink, eye drifts.
        static AnimationClip BuildIdle(Poser p)
        {
            var rec = new Recorder(); const float L = 2.4f; p.End = L;
            System.Func<float, Pose> at = t =>
            {
                float w = Mathf.Repeat(t, L) / L * Mathf.PI * 2;
                var pose = new Pose
                {
                    bodyPos = new Vector3(0, .016f * Mathf.Sin(w), 0), bodyRot = new Vector3(1.5f * Mathf.Sin(w + .6f), 0, 1.2f * Mathf.Sin(w + 2f)),
                    bodyScale = new Vector3(1 - .008f * Mathf.Sin(w), 1 + .012f * Mathf.Sin(w), 1 - .008f * Mathf.Sin(w)),
                    headLean = new Vector3(3f * Mathf.Sin(w - .9f), 2.5f * Mathf.Sin(w + 1.4f), 2.2f * Mathf.Sin(w + 1.2f)),
                    stretch = .025f * Mathf.Sin(w - .5f), antenna = new Vector3(7 * Mathf.Sin(w - 1.4f), 0, 5 * Mathf.Sin(w + .4f)),
                    eye = new Vector3(3 * Mathf.Sin(w + .4f), 7 * Mathf.Sin(w - 1f), 0),
                    armAim = new Vector4[ArmCount], curl = new float[ArmCount], wave = new float[ArmCount], wavePhase = w
                };
                for (int i = 0; i < ArmCount; i++)
                {
                    float ph = w + i * 1.26f;
                    pose.armAim[i] = new Vector4(1, .05f * Mathf.Sin(ph + 1.2f), .10f * Mathf.Sin(ph), 0);
                    pose.curl[i] = 6 + 8 * Mathf.Sin(ph - .6f); pose.wave[i] = 9;
                }
                return pose;
            };
            for (int f = 0; f <= 72; f++)
            {
                float t = f * Frame; p.Apply(lag => at(Mathf.Repeat(t - lag, L))); p.Record(rec, t);
                float tt = t; float blink = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.55f, 1.65f, tt)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.68f, 1.82f, tt)));
                for (int i = 1; i <= 12; i++) { string key = i % 3 == 0 ? "Blink_" + (i * 100 / 12) : "BlinkStep_" + i.ToString("00"); rec.Shape(key, t, 100 * (i == 12 ? Mathf.Clamp01(blink * 12 - 11) : Mathf.Max(0, 1 - Mathf.Abs(blink * 12 - i)))); }
                rec.Shape("BellyOpen", t, 0);
            }
            return rec.Build("Monster_Idle", true);
        }

        // ================================================================ SWIM  (2.4 s loop = 2 jet strokes)
        // Each 1.2 s stroke: gather (anticipation, arms open forward, head squashes) -> 0.18 s power stroke
        // (arms sweep back and close, head stretches 10 %, body surges) -> long glide with the arms trailing
        // (slow out), antenna whipping back 6 frames late (follow through). Arcs: arms swing on the radial/back plane.
        static AnimationClip BuildSwim(Poser p)
        {
            var rec = new Recorder(); const float L = 2.4f, S = 1.2f; p.End = L;
            var aims = new[]
            {   // front arms pull down-and-under, side arms down-and-back, the back arm straight back
                new[] { new K(0, .4f, .7f, .2f, .3f), new K(.14f, .3f, .85f, .28f, .25f), new K(.32f, 0, .35f, -.85f, -.25f), new K(.75f, 0, .45f, -.7f, -.35f), new K(1.05f, .3f, .6f, -.2f, 0), new K(1.2f, .4f, .7f, .2f, .3f) },
                new[] { new K(0, .3f, .9f, .15f, .2f), new K(.14f, .2f, 1, .25f, .15f), new K(.32f, 0, .35f, -.55f, -.75f), new K(.75f, 0, .5f, -.4f, -.6f), new K(1.05f, .3f, .7f, -.1f, -.1f), new K(1.2f, .3f, .9f, .15f, .2f) },
                new[] { new K(0, .3f, .8f, .1f, 0), new K(.14f, .2f, .9f, .2f, 0), new K(.32f, 0, .3f, -.4f, -.9f), new K(.75f, 0, .4f, -.3f, -.8f), new K(1.05f, .3f, .6f, -.05f, -.3f), new K(1.2f, .3f, .8f, .1f, 0) },
            };
            var curl = new[] { new K(0, -18), new K(.14f, -26), new K(.32f, 22), new K(.75f, 10), new K(1.05f, -4), new K(1.2f, -18) };
            var stretch = new[] { new K(0, -.05f), new K(.14f, -.08f), new K(.30f, .10f), new K(.50f, -.03f), new K(.80f, .01f), new K(1.2f, -.05f) };
            var pitch = new[] { new K(0, 9), new K(.14f, 6), new K(.32f, 17), new K(.75f, 12), new K(1.2f, 9) };
            var surge = new[] { new K(0, 0, -.006f, -.01f), new K(.14f, 0, -.012f, -.02f), new K(.32f, 0, .010f, .035f), new K(.75f, 0, .004f, .01f), new K(1.2f, 0, -.006f, -.01f) };
            var lean = new[] { new K(0, -4), new K(.14f, -7), new K(.32f, 10), new K(.6f, 3), new K(1.2f, -4) };
            var ant = new[] { new K(0, 6), new K(.2f, 10), new K(.40f, -28), new K(.7f, -10), new K(1.2f, 6) };
            System.Func<float, Pose> at = t =>
            {
                float s = Mathf.Repeat(t, S), side = Mathf.Repeat(t, L) < S ? 1 : -1;
                var pose = new Pose
                {
                    bodyPos = E3(surge, s), bodyRot = new Vector3(E1(pitch, s), 1.5f * side * Mathf.Sin(s / S * Mathf.PI), 2f * side * Mathf.Sin(s / S * Mathf.PI)),
                    bodyScale = Vector3.one, headLean = new Vector3(E1(lean, s), -1.5f * side * Mathf.Sin(s / S * Mathf.PI), -3f * side * Mathf.Sin(s / S * Mathf.PI)),
                    stretch = E1(stretch, s), antenna = new Vector3(E1(ant, s), 0, -4 * side * Mathf.Sin(s / S * Mathf.PI)), eye = new Vector3(6, 0, 0),
                    armAim = new Vector4[ArmCount], curl = new float[ArmCount], wave = new float[ArmCount], wavePhase = Mathf.Repeat(t, L) / L * Mathf.PI * 4
                };
                for (int i = 0; i < ArmCount; i++) { pose.armAim[i] = Ev(aims[p.group[i]], s); pose.curl[i] = E1(curl, s); pose.wave[i] = 7; }
                return pose;
            };
            for (int f = 0; f <= 72; f++)
            {
                float t = f * Frame; p.Apply(lag => at(Mathf.Repeat(t - lag, L))); p.Record(rec, t);
                for (int i = 1; i <= 12; i++) rec.Shape(i % 3 == 0 ? "Blink_" + (i * 100 / 12) : "BlinkStep_" + i.ToString("00"), t, 0);
                rec.Shape("BellyOpen", t, 0);
            }
            return rec.Build("Monster_Swim", true);
        }

        // ================================================================ BITE / TENTACLE EAT  (3 s, ventral mouth)
        // Timing locked to AquariumFeeding: aim .45 s, pull .72-1.2 s, suction 1.18-1.98 s, mouth open .45-.90, closes 1.98-2.65.
        // Anticipation: rears back and inhales (head stretch) -> lunge (squash on contact) -> arms wrap (curl 120-145)
        // -> two gulps (squash/stretch 10 %) -> relief stretch, arms flick open past rest (overshoot) -> settle.
        static AnimationClip BuildEat(Poser p, string name)
        {
            var s = new Spec
            {
                end = 3,
                bodyPos = new[] { new K(0, 0), new K(.22f, 0, .03f, -.02f), new K(.50f, 0, -.012f, .06f), new K(.72f, 0, -.015f, .04f), new K(1.10f, 0, 0, .01f), new K(1.35f, 0, -.012f, .01f), new K(1.55f, 0, .004f, .01f), new K(1.75f, 0, -.010f, .01f), new K(1.95f, 0, 0, .005f), new K(2.40f, 0, .012f, -.01f), new K(2.70f, 0, -.004f, 0), new K(3, 0) },
                bodyRot = new[] { new K(0, 0), new K(.22f, -10), new K(.50f, 18), new K(.72f, 14), new K(1.10f, 9), new K(1.35f, 8, 0, 2), new K(1.55f, 9), new K(1.75f, 8, 0, -2), new K(1.95f, 6), new K(2.40f, -4), new K(2.70f, 1.5f), new K(3, 0) },
                bodyScale = C3(Vector3.one),
                headLean = new[] { new K(0, 0), new K(.22f, -12), new K(.50f, 8), new K(.72f, 12), new K(1.10f, 6), new K(1.35f, 9, 0, -4), new K(1.55f, 3, 0, 2), new K(1.75f, 9, 0, 4), new K(1.95f, 2), new K(2.40f, -9), new K(2.70f, 3), new K(3, 0) },
                stretch = new[] { new K(0, 0), new K(.22f, .09f), new K(.50f, -.07f), new K(.72f, .04f), new K(1.10f, 0), new K(1.35f, -.11f), new K(1.55f, .06f), new K(1.75f, -.09f), new K(1.95f, .03f), new K(2.40f, .07f), new K(2.70f, -.025f), new K(3, 0) },
                antenna = new[] { new K(0, 0), new K(.30f, -20), new K(.60f, 22), new K(.9f, -8), new K(1.4f, 10), new K(1.8f, -8), new K(2.5f, -16), new K(2.85f, 6), new K(3, 0) },
                eye = new[] { new K(0, 0), new K(.4f, 14), new K(1.9f, 12), new K(2.4f, 0), new K(3, 0) },
                armAim = new[] { EatArms(.5f, -.45f, .85f, .55f, -.6f, .45f), EatArms(.45f, -.6f, .65f, .5f, -.72f, .35f), EatArms(.3f, -.8f, .5f, .4f, -.85f, .25f) },
                curl = new[] { new K(0, 0), new K(.22f, -28), new K(.50f, -10), new K(.72f, 70), new K(1.10f, 85), new K(1.35f, 100), new K(1.55f, 85), new K(1.75f, 100), new K(1.95f, 80), new K(2.40f, -40), new K(2.70f, 10), new K(3, 0) },
                wave = new[] { new K(0, 0), new K(.5f, 6), new K(1.1f, 0), new K(3, 0) },
                blink = new[] { new K(0, 0), new K(.40f, 0), new K(.55f, .38f), new K(1.10f, .38f), new K(1.32f, 0), new K(1.70f, 0), new K(1.78f, 1), new K(1.90f, 0), new K(3, 0) },
                belly = new[] { new K(0, 0), new K(.45f, 0), new K(.90f, 100), new K(1.98f, 100), new K(2.65f, 0), new K(3, 0) },
            };
            return Render(p, s, name);
        }

        // reach (r, up, fwd) at the lunge and wrap (r, up, fwd) while holding; chew pulses tighten the wrap
        static K[] EatArms(float r0, float u0, float f0, float r1, float u1, float f1) => new[]
        {
            new K(0, 1), new K(.22f, .2f, .8f, .45f, -.2f), new K(.50f, 0, r0, u0, f0), new K(.72f, 0, (r0 + r1) / 2, (u0 + u1) / 2, (f0 + f1) / 2),
            new K(1.10f, 0, r1, u1, f1), new K(1.35f, 0, r1 * .9f, u1 - .08f, f1 * .9f), new K(1.55f, 0, r1, u1, f1), new K(1.75f, 0, r1 * .9f, u1 - .08f, f1 * .9f),
            new K(1.95f, 0, r1, u1 + .05f, f1), new K(2.40f, 0, 1, .15f, 0), new K(2.70f, 1, 0, -.1f, 0), new K(3, 1)
        };

        // ================================================================ EATEN  (3 s, captured victim)
        // Startle (stretch 12 %, eye wide) -> flails with arms out of phase (twinning avoided) and a jelly head
        // that wobbles against them -> arms fold up and back as it is drawn in, head squeezed (-12 %), eye shut.
        static AnimationClip BuildEaten(Poser p)
        {
            var s = new Spec
            {
                end = 3,
                bodyPos = new[] { new K(0, 0), new K(.15f, 0, .03f, 0), new K(.55f, 0, .02f, 0), new K(1, 0), new K(3, 0) },
                bodyRot = new[] { new K(0, 0), new K(.15f, -6), new K(.35f, 4, 7, -9), new K(.55f, -3, -7, 9), new K(.75f, 5, 5, -7), new K(.95f, -2, -3, 4), new K(1.3f, -3), new K(3, 0) },
                bodyScale = C3(Vector3.one),
                headLean = new[] { new K(0, 0), new K(.15f, -12), new K(.35f, 7, -7, 10), new K(.55f, -6, 7, -10), new K(.75f, 8, -5, 8), new K(1.0f, -6), new K(1.4f, -3), new K(3, 0) },
                stretch = new[] { new K(0, 0), new K(.15f, .12f), new K(.35f, -.06f), new K(.55f, .05f), new K(.75f, -.07f), new K(1.0f, -.08f), new K(1.4f, -.12f), new K(2.1f, -.10f), new K(3, -.10f) },
                antenna = new[] { new K(0, 0), new K(.2f, -30), new K(.45f, 25, 0, 15), new K(.65f, -20, 0, -15), new K(.85f, 15), new K(1.3f, 30), new K(3, 30) },
                eye = new[] { new K(0, 0), new K(.15f, -10), new K(.5f, 0, 15), new K(.7f, 0, -15), new K(1.0f, 0), new K(3, 0) },
                armAim = new[] { new[] { new K(0, 1), new K(.15f, 0, 1, .35f, 0), new K(.35f, 0, .8f, .2f, 0), new K(.75f, 0, .8f, .2f, 0), new K(1.0f, 0, .35f, .75f, -.3f), new K(1.4f, 0, .2f, .85f, -.35f), new K(3, 0, .2f, .85f, -.35f) } },
                armExtra = (i, t) => new Vector4(0, 0, (i % 2 == 0 ? .35f : -.35f) * Mathf.Sin(t * 14) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, .3f, t)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.8f, 1.0f, t))), 0),
                curl = new[] { new K(0, 0), new K(.15f, -45), new K(.35f, 30), new K(.55f, -25), new K(.75f, 35), new K(1.0f, 60), new K(1.4f, 40), new K(2.1f, 25), new K(3, 20) },
                wave = new[] { new K(0, 0), new K(.3f, 25), new K(.8f, 20), new K(1.2f, 0), new K(3, 0) },
                blink = new[] { new K(0, 0), new K(.15f, 0), new K(.6f, .3f), new K(1.1f, .92f), new K(3, .92f) },
            };
            return Render(p, s, "Monster_Eaten");
        }

        // ================================================================ HARPOON  (3 s, death)
        // Impact: hard squash (-15 %) and recoil -> rebound stretch with flailing arms, decaying overshoot ->
        // slow roll belly-up (slow in), arms go limp and droop, eye closes.
        static AnimationClip BuildHarpoon(Poser p)
        {
            var s = new Spec
            {
                end = 3,
                bodyPos = new[] { new K(0, 0), new K(.08f, 0, .02f, -.04f), new K(.30f, 0, 0, -.02f), new K(.9f, 0), new K(3, 0) },
                bodyRot = new[] { new K(0, 0), new K(.08f, -12, 0, 15), new K(.30f, 6, 0, 25), new K(.55f, -2, 0, 45), new K(.90f, 0, 0, 90), new K(1.5f, 0, 0, 150), new K(2.1f, 0, 0, 180), new K(3, 0, 0, 180) },
                bodyScale = C3(Vector3.one),
                headLean = new[] { new K(0, 0), new K(.08f, -16, 0, -10), new K(.30f, 12, 0, 12), new K(.55f, -5, 0, -6), new K(.9f, 3, 0, 4), new K(1.5f, 1), new K(2.1f, 0), new K(3, 0) },
                stretch = new[] { new K(0, 0), new K(.08f, -.15f), new K(.30f, .10f), new K(.55f, -.04f), new K(.90f, .02f), new K(1.5f, -.01f), new K(3, 0) },
                antenna = new[] { new K(0, 0), new K(.12f, -35), new K(.35f, 28), new K(.6f, -14), new K(.95f, 8), new K(1.5f, 18), new K(3, 22) },
                eye = new[] { new K(0, 0), new K(.08f, -8), new K(.4f, 10), new K(3, 0) },
                armAim = new[] { new[] { new K(0, 1), new K(.08f, 0, 1, .3f, 0), new K(.30f, 0, .7f, -.6f, 0), new K(.55f, 0, 1, .1f, 0), new K(.90f, 0, .85f, -.35f, 0), new K(1.5f, 0, .9f, -.2f, 0), new K(2.1f, 0, .95f, -.12f, 0), new K(3, 0, .95f, -.12f, 0) } },
                curl = new[] { new K(0, 0), new K(.08f, -50), new K(.30f, 55), new K(.55f, -18), new K(.90f, 30), new K(1.5f, 18), new K(2.1f, 15), new K(3, 15) },
                wave = new[] { new K(0, 0), new K(.3f, 20), new K(.55f, 12), new K(.9f, 6), new K(1.4f, 0), new K(3, 0) },
                blink = new[] { new K(0, 0), new K(.08f, .5f), new K(.30f, .2f), new K(1.2f, 1), new K(3, 1) },
            };
            return Render(p, s, "Monster_Harpoon");
        }

        // ================================================================ SPIT RECOIL  (1.2 s, carrier spits the meal)
        // Anticipation: tips forward and inflates (squash -10 %) -> whips the mouth forward (-55 deg) as the head
        // stretches 14 % and the arms fan back from the jet -> head rebounds (overshoot) -> settle.
        static AnimationClip BuildSpitRecoil(Poser p)
        {
            var s = new Spec
            {
                end = 1.2f,
                bodyPos = new[] { new K(0, 0), new K(.06f, 0, -.01f, .01f), new K(.14f, 0, 0, -.04f), new K(.32f, 0, 0, -.02f), new K(.6f, 0, 0, -.005f), new K(1.2f, 0) },
                bodyRot = new[] { new K(0, 0), new K(.06f, 10), new K(.14f, -55), new K(.32f, -48), new K(.60f, -20), new K(.90f, -4), new K(1.2f, 0) },
                bodyScale = C3(Vector3.one),
                headLean = new[] { new K(0, 0), new K(.06f, 8), new K(.14f, -14), new K(.32f, 9), new K(.6f, -3), new K(.9f, 1), new K(1.2f, 0) },
                stretch = new[] { new K(0, 0), new K(.06f, -.10f), new K(.14f, .14f), new K(.32f, -.04f), new K(.6f, .02f), new K(1.2f, 0) },
                antenna = new[] { new K(0, 0), new K(.10f, 18), new K(.25f, -30), new K(.5f, 12), new K(.8f, -4), new K(1.2f, 0) },
                eye = C3(Vector3.zero),
                armAim = new[] { new[] { new K(0, 1), new K(.06f, .7f, .2f, -.25f, 0), new K(.14f, .4f, .6f, .15f, -.35f), new K(.32f, .45f, .6f, .05f, -.25f), new K(.6f, .6f, .5f, 0, -.1f), new K(.9f, 1), new K(1.2f, 1) } },
                curl = new[] { new K(0, 0), new K(.06f, 40), new K(.14f, -35), new K(.32f, -10), new K(.6f, 18), new K(.9f, -6), new K(1.2f, 0) },
                blink = new[] { new K(0, 0), new K(.06f, .7f), new K(.22f, .7f), new K(.40f, 0), new K(1.2f, 0) },
                belly = new[] { new K(0, 0), new K(.04f, 0), new K(.10f, 100), new K(.30f, 100), new K(.70f, 0), new K(1.2f, 0) },
            };
            return Render(p, s, "Monster_SpitRecoil");
        }

        // ================================================================ SPIT OUT  (0.85 s, expelled victim)
        // Tucked ball (squash -12 %) -> bursts open past rest (stretch, arms up: overshoot) -> swept back by the jet
        // -> settles into its swim silhouette. Eye pops open on the burst.
        static AnimationClip BuildSpitOut(Poser p)
        {
            var s = new Spec
            {
                end = .85f,
                bodyPos = C3(Vector3.zero),
                bodyRot = new[] { new K(0, 10), new K(.18f, -8, 0, 10), new K(.42f, -4, 0, -6), new K(.65f, -2, 0, 2), new K(.85f, 0) },
                bodyScale = C3(Vector3.one),
                headLean = new[] { new K(0, 10), new K(.18f, -12, 0, -8), new K(.42f, 6, 0, 5), new K(.65f, -2), new K(.85f, 0) },
                stretch = new[] { new K(0, -.12f), new K(.18f, .12f), new K(.42f, -.03f), new K(.65f, .01f), new K(.85f, 0) },
                antenna = new[] { new K(0, 20), new K(.2f, -30), new K(.45f, 15), new K(.7f, -5), new K(.85f, 0) },
                eye = C3(Vector3.zero),
                armAim = new[] { new[] { new K(0, .3f, .4f, -.7f, -.15f), new K(.18f, .3f, .9f, .25f, 0), new K(.42f, .4f, .5f, -.2f, -.35f), new K(.65f, .45f, .55f, -.2f, -.25f), new K(.85f, .5f, .5f, -.15f, -.2f) } },
                curl = new[] { new K(0, 80), new K(.18f, -40), new K(.42f, -5), new K(.65f, 8), new K(.85f, 5) },
                blink = new[] { new K(0, .9f), new K(.18f, 0), new K(.85f, 0) },
            };
            return Render(p, s, "Monster_SpitOut");
        }

        // ================================================================ MORPH halves (layered over the shape morph, 0.7 s)
        // Leave: squash, twist and spiral the arms in toward the midpoint. Arrive: burst out, stretch up, recoil, settle.
        static AnimationClip BuildMorphLeave(Poser p)
        {
            var s = new Spec
            {
                end = .7f,
                bodyPos = new[] { new K(0, 0), new K(.12f, 0, -.02f, 0), new K(.32f, 0, .01f, 0), new K(.7f, 0) },
                bodyRot = new[] { new K(0, 0), new K(.12f, 6), new K(.32f, -4, 25, 20), new K(.52f, 0, 40, 30), new K(.7f, 0) },
                bodyScale = C3(Vector3.one),
                headLean = new[] { new K(0, 0), new K(.12f, -10), new K(.32f, 6, -10, -10), new K(.52f, 3, -6, -6), new K(.7f, 0) },
                stretch = new[] { new K(0, 0), new K(.12f, -.12f), new K(.32f, .06f), new K(.52f, -.05f), new K(.7f, 0) },
                antenna = new[] { new K(0, 0), new K(.2f, -25), new K(.45f, 20), new K(.7f, 0) },
                armAim = new[] { new[] { new K(0, 1), new K(.12f, 0, .8f, .4f, 0), new K(.32f, 0, .5f, -.55f, -.3f), new K(.52f, 0, .4f, -.65f, -.35f), new K(.7f, 1) } },
                curl = new[] { new K(0, 0), new K(.12f, -30), new K(.32f, 65), new K(.52f, 90), new K(.7f, 0) },
                wave = new[] { new K(0, 0), new K(.32f, 15), new K(.7f, 0) },
            };
            return Render(p, s, "Monster_Morph");
        }
        static AnimationClip BuildMorphArrive(Poser p)
        {
            var s = new Spec
            {
                end = .7f,
                bodyPos = new[] { new K(0, 0), new K(.18f, 0, .03f, 0), new K(.38f, 0, -.01f, 0), new K(.7f, 0) },
                bodyRot = new[] { new K(0, 0), new K(.18f, -6, -15, -8), new K(.38f, 3, 6, 3), new K(.55f, -1, -2, -1), new K(.7f, 0) },
                bodyScale = C3(Vector3.one),
                headLean = new[] { new K(0, 0), new K(.18f, -12, 8, 6), new K(.38f, 8, -4, -3), new K(.55f, -3, 1, 1), new K(.7f, 0) },
                stretch = new[] { new K(0, 0), new K(.18f, .14f), new K(.38f, -.08f), new K(.55f, .025f), new K(.7f, 0) },
                antenna = new[] { new K(0, 0), new K(.22f, -30), new K(.42f, 20), new K(.6f, -6), new K(.7f, 0) },
                armAim = new[] { new[] { new K(0, 1), new K(.18f, 0, 1, .35f, 0), new K(.38f, 1, 0, -.15f, 0), new K(.55f, 0, 1, .05f, 0), new K(.7f, 1) } },
                curl = new[] { new K(0, 0), new K(.18f, -50), new K(.38f, 30), new K(.55f, -12), new K(.7f, 0) },
            };
            return Render(p, s, "Monster_Unmorph");
        }

        // ================================================================ PREVIEW (contact sheets for review)
        [MenuItem("Aquarium/Characters/Render cute monster clip previews")]
        public static void Previews()
        {
            string outDir = Path.Combine(Source, "unity_previews"); Directory.CreateDirectory(outDir);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var origin = new Vector3(0, -500, 0); go.transform.position = origin;
            var camGo = new GameObject("preview cam"); var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.86f, .9f, .94f);
            cam.orthographic = true; cam.orthographicSize = .95f; cam.nearClipPlane = .1f; cam.farClipPlane = 20; cam.cullingMask = ~0;
            var lightGo = new GameObject("preview light"); var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = .75f; lightGo.transform.rotation = Quaternion.Euler(40, -30, 0);
            var rt = new RenderTexture(360, 360, 24); cam.targetTexture = rt; var tex = new Texture2D(360, 360, TextureFormat.RGB24, false);
            try
            {
                var rig = go.GetComponent<AquariumRig>();
                // edit-mode skinning does not refresh between manual renders: draw a baked copy instead
                var bakedGo = new GameObject("preview baked"); bakedGo.transform.SetParent(go.transform, false);
                var mf = bakedGo.AddComponent<MeshFilter>(); var mr = bakedGo.AddComponent<MeshRenderer>(); mr.sharedMaterials = rig.skin.sharedMaterials;
                var baked = new Mesh(); mf.sharedMesh = baked; rig.skin.enabled = false;
                foreach (var clip in rig.clips)
                {
                    int n = 6; var sheet = new Texture2D(360 * n, 720, TextureFormat.RGB24, false);
                    for (int view = 0; view < 2; view++)
                        for (int i = 0; i < n; i++)
                        {
                            float t = clip.length * i / (n - 1); clip.SampleAnimation(go, t);
                            rig.skin.BakeMesh(baked); bakedGo.transform.position = rig.skin.transform.position; bakedGo.transform.rotation = rig.skin.transform.rotation;
                            var dirv = view == 0 ? new Vector3(.55f, .25f, 1) : new Vector3(1, .15f, -.15f);
                            cam.transform.position = origin + new Vector3(0, .05f, 0) + dirv.normalized * 6; cam.transform.LookAt(origin + new Vector3(0, .05f, 0));
                            cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 360, 360), 0, 0); tex.Apply();
                            sheet.SetPixels(i * 360, (1 - view) * 360, 360, 360, tex.GetPixels());
                        }
                    sheet.Apply(); File.WriteAllBytes(Path.Combine(outDir, clip.name + ".png"), sheet.EncodeToPNG()); Object.DestroyImmediate(sheet);
                }
            }
            finally { RenderTexture.active = null; cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go); Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo); }
            Debug.Log("CUTE_MONSTER_PREVIEWS " + outDir);
        }
    }
}

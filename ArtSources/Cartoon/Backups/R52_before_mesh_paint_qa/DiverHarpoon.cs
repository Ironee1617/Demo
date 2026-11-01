using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium.Editor
{
    // Painted cartoon diver + split harpoon gun (ArtSources/DiverHarpoon, generated outside Unity):
    //  * Diver       : 17-bone yellow cartoon diver (flat paint from the character sheet), gun hand curled around
    //                  the grip, face always
    //                  looking forward; Idle/Swim/Fire/Reload/FPSIdle/FPSFire/Eaten/Harpoon clips (12-principles pass)
    //  * DiverHands  : the diver's own gun arm posed in camera space (rigid FPS clips)
    //  * HarpoonGun  : body mesh + separate spear (loaded child and HarpoonHead projectile share it)
    public static class DiverHarpoon
    {
        const string Equipment = "Assets/Demos/HideAndSeek/Equipment/Resources/AquariumEquipment";
        const string Backup = "Assets/Demos/HideAndSeek/Equipment/OriginalDiverBackup";
        static string Source => Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtSources/DiverHarpoon"));

        [Serializable] class SocketData { public string bone; public float[] position, rotation; public float scale; }
        [Serializable] class ClipData { public string name; public float length; public bool loop; public int frames; public float[] data; }
        [Serializable] class RigData { public string name; public string[] bones; public int[] parents; public float[] world; public SocketData socket; public int vertices; public ClipData[] clips; }
        [Serializable] class GunData { public float[] muzzle, ropeExit; public float headLength, shaftLength; }

        static Vector3 V3(float[] a, int i = 0) => new Vector3(a[i], a[i + 1], a[i + 2]);

        [MenuItem("Aquarium/Characters/Apply painted diver + harpoon")]
        public static void Apply()
        {
            if (!Directory.Exists(Source)) throw new Exception("Missing " + Source);
            RemoveOld();
            var diverMain = Material("Diver_Atlas", "Diver_Cartoon");
            var gunMain = Material("HarpoonGun_Atlas", "HarpoonGun_Cartoon");
            BuildRig("Diver", new[] { diverMain }, false);
            BuildRig("DiverHands", new[] { diverMain }, true);
            BuildGun(gunMain);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("DIVER_HARPOON_APPLIED");
        }

        // Previous diver generations are removed outright (the project is under git): the old backup
        // folder, the glass lens material and every earlier diver / first-person clip.
        static void RemoveOld()
        {
            if (AssetDatabase.IsValidFolder(Backup)) AssetDatabase.DeleteAsset(Backup);
            foreach (string path in Directory.GetFiles(Equipment))
            {
                string n = Path.GetFileName(path); if (n.EndsWith(".meta")) continue;
                bool old = n == "Diver_Lens.mat" || n == "Diver_Glass.mat" || (n.EndsWith(".anim") && (n.StartsWith("Diver_") || n.StartsWith("DiverHands_")));
                if (old) AssetDatabase.DeleteAsset(path.Replace("\\", "/"));
            }
            AssetDatabase.Refresh();
        }

        static T Save<T>(T value, string path) where T : UnityEngine.Object
        {
            var old = AssetDatabase.LoadAssetAtPath<T>(path);
            if (old == null) { AssetDatabase.CreateAsset(value, path); return value; }
            if (value is Mesh source && old is Mesh target)
            {
                target.Clear(false); target.ClearBlendShapes(); target.indexFormat = source.indexFormat;
                target.vertices = source.vertices; target.normals = source.normals; target.tangents = source.tangents; target.uv = source.uv; target.uv2 = source.uv2;
                target.subMeshCount = source.subMeshCount; for (int i = 0; i < source.subMeshCount; i++) target.SetTriangles(source.GetTriangles(i), i);
                target.bindposes = source.bindposes; target.boneWeights = source.boneWeights; target.bounds = source.bounds; target.name = source.name;
                EditorUtility.SetDirty(target); UnityEngine.Object.DestroyImmediate(source); return old;
            }
            EditorUtility.CopySerialized(value, old); UnityEngine.Object.DestroyImmediate(value); EditorUtility.SetDirty(old); return old;
        }

        static Material Material(string atlas, string name)
        {
            string path = Equipment + "/" + atlas + ".png";
            File.Copy(Path.Combine(Source, atlas + ".png"), path, true); AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.maxTextureSize = 2048; importer.sRGBTexture = true; importer.mipmapEnabled = true; importer.anisoLevel = 4;
            importer.wrapMode = TextureWrapMode.Clamp; importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            var m = new Material(Shader.Find("Aquarium/Cartoon Character")) { name = name };
            m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return Save(m, Equipment + "/" + name + ".mat");
        }

        static Mesh ReadMesh(string file, string meshName, Transform[] bones, Transform root)
        {
            var mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            using (var r = new BinaryReader(File.OpenRead(Path.Combine(Source, file))))
            {
                int n = r.ReadInt32(), count = r.ReadInt32(), sub = r.ReadInt32();
                var v = new Vector3[n]; var nr = new Vector3[n]; var uv = new Vector2[n]; var uv2 = new Vector2[n]; var w = new float[n, 4];
                for (int i = 0; i < n; i++)
                {
                    v[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); nr[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle()); uv2[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    for (int k = 0; k < 4; k++) w[i, k] = r.ReadSingle();
                }
                var bw = new BoneWeight[n];
                for (int i = 0; i < n; i++)
                {
                    int a = r.ReadInt32(), b = r.ReadInt32(), c = r.ReadInt32(), d = r.ReadInt32();
                    float s = w[i, 0] + w[i, 1] + w[i, 2] + w[i, 3]; if (s <= 0) s = 1;
                    bw[i] = new BoneWeight { boneIndex0 = a, boneIndex1 = b, boneIndex2 = c, boneIndex3 = d, weight0 = w[i, 0] / s, weight1 = w[i, 1] / s, weight2 = w[i, 2] / s, weight3 = w[i, 3] / s };
                }
                var idx = new int[count]; for (int i = 0; i < count; i++) idx[i] = r.ReadInt32();
                var groups = new List<int>[sub]; for (int i = 0; i < sub; i++) groups[i] = new List<int>();
                for (int t = 0; t < count / 3; t++) { var g = groups[r.ReadInt32()]; g.Add(idx[3 * t]); g.Add(idx[3 * t + 1]); g.Add(idx[3 * t + 2]); }
                if (r.BaseStream.Position != r.BaseStream.Length) throw new Exception("Unread data in " + file);
                mesh.vertices = v; mesh.normals = nr; mesh.uv = uv; mesh.uv2 = uv2; mesh.subMeshCount = sub;
                for (int i = 0; i < sub; i++) mesh.SetTriangles(groups[i], i);
                if (bones != null) { mesh.boneWeights = bw; mesh.bindposes = bones.Select(t => t.worldToLocalMatrix * root.localToWorldMatrix).ToArray(); }
            }
            mesh.RecalculateBounds(); mesh.RecalculateTangents(); return mesh;
        }

        static Transform Child(string name, Transform parent, Vector3 p) { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = p; return t; }

        static void BuildRig(string name, Material[] materials, bool firstPerson)
        {
            var d = JsonUtility.FromJson<RigData>(File.ReadAllText(Path.Combine(Source, name + ".json")));
            var go = new GameObject(name); var rig = go.AddComponent<AquariumRig>();
            var bones = new Transform[d.bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                var world = V3(d.world, 3 * i); int p = d.parents[i];
                bones[i] = Child(d.bones[i], p < 0 ? go.transform : bones[p], p < 0 ? world : world - V3(d.world, 3 * p));
            }
            var skin = Child("Deforming surface", go.transform, Vector3.zero).gameObject.AddComponent<SkinnedMeshRenderer>();
            var mesh = ReadMesh(name + ".bin", name + " cartoon surface", bones, go.transform);
            if (mesh.vertexCount != d.vertices) throw new Exception("Vertex count mismatch " + name);
            CleanGloveUVs(mesh, bones);
            // R51: a clean, straight glove/sleeve edge with a rolled rubber lip cut into the surface itself.
            AquariumWristSeam.Seal(mesh, bones, skin.transform);
            skin.sharedMesh = Save(mesh, Equipment + "/" + name + ".asset");
            skin.bones = bones; skin.rootBone = bones[0]; skin.quality = SkinQuality.Bone4; skin.localBounds = new Bounds(Vector3.zero, Vector3.one * 5);
            skin.sharedMaterials = materials.Take(Mathf.Max(1, skin.sharedMesh.subMeshCount)).ToArray(); skin.receiveShadows = false; skin.updateWhenOffscreen = false;
            if (firstPerson) skin.shadowCastingMode = ShadowCastingMode.Off;
            rig.skin = skin; rig.cartoon = true; rig.cartoonKind = "Diver"; rig.firstPerson = firstPerson;
            rig.restPositions = bones.Select(b => b.localPosition).ToArray(); rig.restRotations = bones.Select(b => b.localRotation).ToArray(); rig.restScales = bones.Select(b => b.localScale).ToArray();
            var hand = bones.Single(b => b.name == d.socket.bone);
            var socket = Child("Harpoon grip socket", hand, V3(d.socket.position));
            socket.localRotation = new Quaternion(d.socket.rotation[0], d.socket.rotation[1], d.socket.rotation[2], d.socket.rotation[3]);
            socket.localScale = Vector3.one * d.socket.scale; rig.gunSocket = socket;
            rig.clips = d.clips.Select(c => Clip(rig, bones, name, c)).ToArray();
            for (int i = 0; i < bones.Length; i++) { bones[i].localPosition = rig.restPositions[i]; bones[i].localRotation = rig.restRotations[i]; bones[i].localScale = rig.restScales[i]; }
            PrefabUtility.SaveAsPrefabAsset(go, Equipment + "/" + name + ".prefab"); UnityEngine.Object.DestroyImmediate(go);
        }

        // R49: the painted atlas has a few glove triangles that land on the yellow suit paint or on the blended edge
        // of a UV island (around the cuffs and between the fingers); on the dark glove they read as torn holes.
        // Every glove vertex (hand weight > .5) of such a triangle is moved to the nearest clean glove texel.
        static readonly Color32 GloveColour = new Color32(66, 58, 62, 255);
        static bool IsGlove(Color32 c) { return Mathf.Abs(c.r - GloveColour.r) + Mathf.Abs(c.g - GloveColour.g) + Mathf.Abs(c.b - GloveColour.b) <= 36; }
        public static int CleanGloveUVs(Mesh mesh, Transform[] bones)
        {
            var image = new Texture2D(2, 2); image.LoadImage(File.ReadAllBytes(Path.Combine(Source, "Diver_Atlas.png")));
            int W = image.width, H = image.height; var px = image.GetPixels32(); UnityEngine.Object.DestroyImmediate(image);
            Func<int, int, bool> clean = (x, y) =>
            {
                for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                { int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= W || yy >= H || !IsGlove(px[yy * W + xx])) return false; }
                return true;
            };
            Func<Vector2, Color32> sample = q => px[Mathf.Clamp((int)(q.y * H), 0, H - 1) * W + Mathf.Clamp((int)(q.x * W), 0, W - 1)];
            var hands = new List<int>(); for (int i = 0; i < bones.Length; i++) if (bones[i].name == "HandL" || bones[i].name == "HandR") hands.Add(i);
            var bw = mesh.boneWeights; var uv = mesh.uv; var tris = mesh.triangles;
            var hand = new float[uv.Length];
            for (int i = 0; i < uv.Length; i++)
            {
                var b = bw[i]; float w = 0;
                foreach (int h in hands) w += (b.boneIndex0 == h ? b.weight0 : 0) + (b.boneIndex1 == h ? b.weight1 : 0) + (b.boneIndex2 == h ? b.weight2 : 0) + (b.boneIndex3 == h ? b.weight3 : 0);
                hand[i] = w;
            }
            var move = new HashSet<int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                if (hand[a] < .5f || hand[b] < .5f || hand[c] < .5f) continue;
                bool odd = false;
                for (int s = 0; s < 10 && !odd; s++)
                {
                    float u1 = (s * .37f + .05f) % 1, u2 = (s * .61f + .11f) % 1; if (u1 + u2 > 1) { u1 = 1 - u1; u2 = 1 - u2; }
                    if (!IsGlove(sample(uv[a] + (uv[b] - uv[a]) * u1 + (uv[c] - uv[a]) * u2))) odd = true;
                }
                if (!odd) continue;
                move.Add(a); move.Add(b); move.Add(c);
            }
            int moved = 0;
            foreach (int i in move)
            {
                int x0 = Mathf.Clamp((int)(uv[i].x * W), 0, W - 1), y0 = Mathf.Clamp((int)(uv[i].y * H), 0, H - 1);
                if (clean(x0, y0)) continue;
                bool found = false;
                for (int r = 1; r <= 64 && !found; r++)
                    for (int dy = -r; dy <= r && !found; dy++)
                        for (int dx = -r; dx <= r && !found; dx++)
                        {
                            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                            int x = x0 + dx, y = y0 + dy; if (x < 2 || y < 2 || x >= W - 2 || y >= H - 2 || !clean(x, y)) continue;
                            uv[i] = new Vector2((x + .5f) / W, (y + .5f) / H); found = true; moved++;
                        }
            }
            mesh.uv = uv;
            return moved;
        }
        // Applies CleanGloveUVs to the imported diver meshes in place (no full re-import).
        public static string CleanGloveUVsInPlace()
        {
            string report = "";
            foreach (string name in new[] { "Diver", "DiverHands" })
            {
                var rig = AssetDatabase.LoadAssetAtPath<GameObject>(Equipment + "/" + name + ".prefab").GetComponent<AquariumRig>();
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Equipment + "/" + name + ".asset");
                int moved = CleanGloveUVs(mesh, rig.skin.bones);
                EditorUtility.SetDirty(mesh); report += name + ": " + moved + " glove vertices moved; ";
            }
            AssetDatabase.SaveAssets();
            return report;
        }

        static AnimationClip Clip(AquariumRig rig, Transform[] bones, string owner, ClipData c)
        {
            var clip = new AnimationClip { name = owner + "_" + c.name, legacy = true, frameRate = 30, wrapMode = c.loop ? WrapMode.Loop : WrapMode.ClampForever };
            int nb = bones.Length; string[] props = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w", "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };
            for (int b = 0; b < nb; b++)
            {
                string path = AnimationUtility.CalculateTransformPath(bones[b], rig.transform);
                for (int f = 0; f < 10; f++)
                {
                    var keys = new Keyframe[c.frames];
                    for (int k = 0; k < c.frames; k++) keys[k] = new Keyframe(k / 30f, c.data[(k * nb + b) * 10 + f]);
                    var curve = new AnimationCurve(keys);
                    for (int k = 0; k < c.frames; k++) AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.ClampedAuto);
                    for (int k = 0; k < c.frames; k++) AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.ClampedAuto);
                    clip.SetCurve(path, typeof(Transform), props[f], curve);
                }
            }
            clip.EnsureQuaternionContinuity();
            return Save(clip, Equipment + "/" + clip.name + ".anim");
        }

        static void BuildGun(Material material)
        {
            var d = JsonUtility.FromJson<GunData>(File.ReadAllText(Path.Combine(Source, "Harpoon.json")));
            var body = Save(ReadMesh("HarpoonGun.bin", "HarpoonGun cartoon surface", null, null), Equipment + "/HarpoonGun.asset");
            var spear = Save(ReadMesh("HarpoonSpear.bin", "HarpoonHead cartoon surface", null, null), Equipment + "/HarpoonHead.asset");
            foreach (string name in new[] { "HarpoonGun", "HarpoonGunFPS" })
            {
                var g = new GameObject(name); var gun = g.AddComponent<AquariumHarpoonGun>();
                var barrel = Child("Cartoon barrel", g.transform, Vector3.zero);
                barrel.gameObject.AddComponent<MeshFilter>().sharedMesh = body; barrel.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                gun.muzzle = Child("Muzzle", g.transform, V3(d.muzzle)); gun.ropeExit = Child("Rope exit", g.transform, V3(d.ropeExit)); gun.headLength = d.headLength; gun.shaftLength = d.shaftLength > 0 ? d.shaftLength : gun.shaftLength;
                var loaded = Child("Loaded spear", g.transform, V3(d.muzzle));
                loaded.gameObject.AddComponent<MeshFilter>().sharedMesh = spear; loaded.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material; gun.loadedHarpoon = loaded;
                PrefabUtility.SaveAsPrefabAsset(g, Equipment + "/" + name + ".prefab"); UnityEngine.Object.DestroyImmediate(g);
            }
            var projectile = new GameObject("Cartoon tethered spear");
            projectile.AddComponent<MeshFilter>().sharedMesh = spear; projectile.AddComponent<MeshRenderer>().sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(projectile, Equipment + "/HarpoonHead.prefab"); UnityEngine.Object.DestroyImmediate(projectile);
        }
    }
}

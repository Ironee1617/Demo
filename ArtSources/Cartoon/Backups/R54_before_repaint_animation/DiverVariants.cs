using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium.Editor
{
    // R53: the tracker (blue) and drone (pink) divers. Their meshes are painted and skinned outside Unity
    // (ArtSources/Cartoon/DiverVariants/<Kind>: Diver.bin + Diver.json + Diver_Atlas.png, same binary layout as
    // ArtSources/DiverHarpoon) on the yellow diver's 17-bone skeleton, so every yellow diver clip is reused:
    // rotations as they are, bone offsets moved onto each variant's own rest pose. The first-person hands and
    // the harpoon gun keep the yellow meshes with a recoloured atlas (yellow paint -> the variant's colour).
    public static class DiverVariants
    {
        const string Equipment = "Assets/Demos/HideAndSeek/Equipment/Resources/AquariumEquipment";
        static string Source(string kind) => Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtSources/Cartoon/DiverVariants/" + kind));

        [Serializable] class SocketData { public string bone; public float[] position, rotation; public float scale; }
        [Serializable] class RigData { public string name; public string[] bones; public int[] parents; public float[] world; public SocketData socket; public int vertices; }

        public static readonly (string kind, Color32 colour)[] Kinds = { ("Tracker", new Color32(52, 118, 222, 255)), ("Drone", new Color32(246, 110, 142, 255)) };

        [MenuItem("Aquarium/Characters/Apply tracker + drone divers")]
        public static void Apply()
        {
            foreach (var k in Kinds) Build(k.kind, k.colour);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("DIVER_VARIANTS_APPLIED");
        }

        static Vector3 V3(float[] a, int i = 0) => new Vector3(a[i], a[i + 1], a[i + 2]);

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

        static Texture2D ImportTexture(string path, bool flat)
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = true; importer.wrapMode = TextureWrapMode.Clamp; importer.anisoLevel = 4;
            if (flat) { importer.maxTextureSize = 256; importer.mipmapEnabled = false; importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.Uncompressed; }
            else { importer.maxTextureSize = 2048; importer.mipmapEnabled = true; importer.textureCompression = TextureImporterCompression.CompressedHQ; }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material NewMaterial(string name, Texture2D texture, Material like)
        {
            var m = like != null ? new Material(like) : new Material(Shader.Find("Aquarium/Cartoon Character"));
            m.name = name; m.mainTexture = texture;
            return Save(m, Equipment + "/" + name + ".mat");
        }

        // Yellow suit paint (hue 25..58 degrees, saturated) takes the variant's hue and saturation; lightness keeps
        // its ratio to the flat yellow, so shading baked into the atlas survives.
        static Texture2D Recolour(string source, string target, Color32 colour, bool flat)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false); image.LoadImage(File.ReadAllBytes(Path.GetFullPath(source)));
            var px = image.GetPixels32();
            Color.RGBToHSV(colour, out float th, out float ts, out float tv);
            Color.RGBToHSV(new Color32(247, 184, 62, 255), out _, out _, out float yv);
            int changed = 0;
            for (int i = 0; i < px.Length; i++)
            {
                Color.RGBToHSV(px[i], out float h, out float s, out float v);
                if (h < 25f / 360 || h > 58f / 360 || s < .45f || v < .30f) continue;
                var c = Color.HSVToRGB(th, Mathf.Clamp01(ts * s / .75f), Mathf.Clamp01(tv * v / yv));
                px[i] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), px[i].a); changed++;
            }
            image.SetPixels32(px); File.WriteAllBytes(Path.GetFullPath(target), image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
            Debug.Log("Recoloured " + changed + " texels into " + target);
            return ImportTexture(target, flat);
        }

        static Mesh ReadMesh(string file, string meshName, Transform[] bones, Transform root)
        {
            var mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            using (var r = new BinaryReader(File.OpenRead(file)))
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
                mesh.boneWeights = bw; mesh.bindposes = bones.Select(t => t.worldToLocalMatrix * root.localToWorldMatrix).ToArray();
            }
            mesh.RecalculateBounds(); mesh.RecalculateTangents(); return mesh;
        }

        static Transform Child(string name, Transform parent, Vector3 p) { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = p; return t; }

        static void Build(string kind, Color32 colour)
        {
            string src = Source(kind);
            if (!Directory.Exists(src)) throw new Exception("Missing " + src);
            var yellow = AssetDatabase.LoadAssetAtPath<GameObject>(Equipment + "/Diver.prefab").GetComponent<AquariumRig>();
            var yellowMaterial = AssetDatabase.LoadAssetAtPath<Material>(Equipment + "/Diver_Cartoon.mat");

            // third-person body
            string atlasPath = Equipment + "/Diver_" + kind + "_Atlas.png";
            File.Copy(Path.Combine(src, "Diver_Atlas.png"), Path.GetFullPath(atlasPath), true);
            var bodyMaterial = NewMaterial("Diver_" + kind + "_Cartoon", ImportTexture(atlasPath, true), yellowMaterial);

            var d = JsonUtility.FromJson<RigData>(File.ReadAllText(Path.Combine(src, "Diver.json")));
            var go = new GameObject("Diver_" + kind); var rig = go.AddComponent<AquariumRig>();
            var bones = new Transform[d.bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                var world = V3(d.world, 3 * i); int p = d.parents[i];
                bones[i] = Child(d.bones[i], p < 0 ? go.transform : bones[p], p < 0 ? world : world - V3(d.world, 3 * p));
            }
            var skin = Child("Deforming surface", go.transform, Vector3.zero).gameObject.AddComponent<SkinnedMeshRenderer>();
            var mesh = ReadMesh(Path.Combine(src, "Diver.bin"), "Diver " + kind + " cartoon surface", bones, go.transform);
            if (mesh.vertexCount != d.vertices) throw new Exception("Vertex count mismatch " + kind);
            skin.sharedMesh = Save(mesh, Equipment + "/Diver_" + kind + ".asset");
            skin.bones = bones; skin.rootBone = bones[0]; skin.quality = SkinQuality.Bone4; skin.localBounds = new Bounds(Vector3.zero, Vector3.one * 5);
            skin.sharedMaterials = new[] { bodyMaterial }; skin.receiveShadows = false; skin.updateWhenOffscreen = false;
            rig.skin = skin; rig.cartoon = true; rig.cartoonKind = "Diver"; rig.firstPerson = false;
            rig.restPositions = bones.Select(b => b.localPosition).ToArray(); rig.restRotations = bones.Select(b => b.localRotation).ToArray(); rig.restScales = bones.Select(b => b.localScale).ToArray();
            var hand = bones.Single(b => b.name == d.socket.bone);
            var socket = Child("Harpoon grip socket", hand, V3(d.socket.position));
            socket.localRotation = new Quaternion(d.socket.rotation[0], d.socket.rotation[1], d.socket.rotation[2], d.socket.rotation[3]);
            socket.localScale = Vector3.one * d.socket.scale; rig.gunSocket = socket;

            // the yellow diver's clips on this rest pose
            var yellowBones = yellow.skin.bones;
            var yellowRest = new Dictionary<string, Vector3>(); for (int i = 0; i < yellowBones.Length; i++) yellowRest[yellowBones[i].name] = yellow.restPositions[i];
            var rest = new Dictionary<string, Vector3>(); for (int i = 0; i < bones.Length; i++) rest[bones[i].name] = rig.restPositions[i];
            var clips = new List<AnimationClip>();
            foreach (var source in yellow.clips)
            {
                var clip = new AnimationClip { name = source.name.Replace("Diver_", "Diver_" + kind + "_"), legacy = true, frameRate = source.frameRate, wrapMode = source.wrapMode };
                foreach (var binding in AnimationUtility.GetCurveBindings(source))
                {
                    var curve = AnimationUtility.GetEditorCurve(source, binding);
                    string bone = binding.path.Contains("/") ? binding.path.Substring(binding.path.LastIndexOf('/') + 1) : binding.path;
                    if (binding.type == typeof(Transform) && binding.propertyName.StartsWith("m_LocalPosition.") && rest.ContainsKey(bone) && yellowRest.ContainsKey(bone))
                    {
                        int c = "xyz".IndexOf(binding.propertyName[binding.propertyName.Length - 1]);
                        float delta = rest[bone][c] - yellowRest[bone][c];
                        var keys = curve.keys; for (int k = 0; k < keys.Length; k++) keys[k].value += delta; curve.keys = keys;
                    }
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                }
                clip.EnsureQuaternionContinuity();
                clips.Add(Save(clip, Equipment + "/" + clip.name + ".anim"));
            }
            rig.clips = clips.ToArray();
            PrefabUtility.SaveAsPrefabAsset(go, Equipment + "/Diver_" + kind + ".prefab"); UnityEngine.Object.DestroyImmediate(go);

            // first-person hands and the harpoon gun: the yellow meshes, recoloured
            var handsTexture = Recolour(Equipment + "/Diver_Atlas.png", Equipment + "/DiverHands_" + kind + "_Atlas.png", colour, false);
            NewMaterial("DiverHands_" + kind + "_Cartoon", handsTexture, yellowMaterial);
            var gunMaterial = AssetDatabase.LoadAssetAtPath<Material>(Equipment + "/HarpoonGun_Cartoon.mat");
            var gunTexture = Recolour(Equipment + "/HarpoonGun_Atlas.png", Equipment + "/HarpoonGun_" + kind + "_Atlas.png", colour, false);
            NewMaterial("HarpoonGun_" + kind + "_Cartoon", gunTexture, gunMaterial);
        }
    }
}

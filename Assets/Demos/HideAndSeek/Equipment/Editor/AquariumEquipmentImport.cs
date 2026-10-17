using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumEquipmentImport
    {
        const string Root = "Assets/Demos/HideAndSeek/Equipment/Resources/AquariumEquipment";
        [Serializable] class Bone { public string name; public int parent; public float[] position, rotation, scale; }
        [Serializable] class Clip { public string name; public float duration; public bool loop; public float[] values; }
        [Serializable] class Part { public string name; public float[] color; public float metallic, smoothness, detail; public bool atlas; }
        [Serializable] class DiverData { public Bone[] bones; public Clip[] clips; public Part[] materials; }
        [Serializable] class GunData { public float[] muzzle, ropeExit; public float headLength; public Part[] materials; }
        static Material[] diverMaterials, gunMaterials;
        static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);
        static Vector3 V(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        static void Save(UnityEngine.Object o, string name)
        {
            string path = Root + "/" + name; var old = AssetDatabase.LoadMainAssetAtPath(path);
            if (old == null) AssetDatabase.CreateAsset(o, path);
            else { EditorUtility.CopySerialized(o, old); UnityEngine.Object.DestroyImmediate(o); EditorUtility.SetDirty(old); }
        }
        [MenuItem("Aquarium/Equipment/Import diver and harpoon")]
        public static void Run()
        {
            string source = Environment.GetEnvironmentVariable("AQUARIUM_EQUIPMENT_SOURCE");
            if (string.IsNullOrEmpty(source)) throw new Exception("Set AQUARIUM_EQUIPMENT_SOURCE to the Blender Export folder.");
            Directory.CreateDirectory(Root); AssetDatabase.Refresh();
            var data = JsonUtility.FromJson<DiverData>(File.ReadAllText(Path.Combine(source, "Diver.json")));
            diverMaterials = ImportMaterials(source, "Diver", data.materials);
            ImportDiver(source, "Diver", data); ImportDiver(source, "DiverHands", data);
            var gunData = JsonUtility.FromJson<GunData>(File.ReadAllText(Path.Combine(source, "HarpoonGun.json")));
            gunMaterials = ImportMaterials(source, "HarpoonGun", gunData.materials);
            var gun = new GameObject("HarpoonGun"); var component = gun.AddComponent<AquariumHarpoonGun>();
            AddMesh(source, "HarpoonGun", gun.transform); component.headLength = gunData.headLength;
            component.muzzle = Child("Muzzle", gun.transform, V(gunData.muzzle));
            component.ropeExit = Child("Rope exit", gun.transform, V(gunData.ropeExit));
            component.loadedHarpoon = AddMesh(source, "HarpoonHead", gun.transform); component.loadedHarpoon.localPosition = component.muzzle.localPosition;
            PrefabUtility.SaveAsPrefabAsset(gun, Root + "/HarpoonGun.prefab"); UnityEngine.Object.DestroyImmediate(gun);
            var head = new GameObject("Tethered harpoon"); AddMesh(source, "HarpoonHead", head.transform);
            PrefabUtility.SaveAsPrefabAsset(head, Root + "/HarpoonHead.prefab"); UnityEngine.Object.DestroyImmediate(head);
            File.Copy(Path.Combine(source, "Rope.png"), Root + "/Rope.png", true); AssetDatabase.ImportAsset(Root + "/Rope.png");
            var rope = new Material(Shader.Find("Standard")); rope.color = new Color(.82f, .74f, .48f); rope.SetFloat("_Glossiness", .2f);
            rope.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Rope.png"));
            Save(rope, "Rope.mat");
            // Remove only the provisional generated maps replaced by named clean material slots.
            foreach (string model in new[] { "Diver", "HarpoonGun" })
                foreach (string suffix in new[] { ".mat", "_BaseColor.jpg", "_Normal.png", "_MetalSmooth.png" })
                    if (AssetDatabase.LoadMainAssetAtPath(Root + "/" + model + suffix) != null) AssetDatabase.DeleteAsset(Root + "/" + model + suffix);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Debug.Log("AQUARIUM_EQUIPMENT_IMPORT_OK");
        }
        static Transform Child(string name, Transform parent, Vector3 at)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = at; return t; }
        static Mesh ReadMesh(string source, string name, Transform[] bones = null, Transform root = null)
        {
            var mesh = new Mesh { name = name + " optimized mesh", indexFormat = IndexFormat.UInt32 };
            using (var r = new BinaryReader(File.OpenRead(Path.Combine(source, name + ".equipmesh"))))
            {
                int n = r.ReadInt32(), count = r.ReadInt32(); var v = new Vector3[n]; var normal = new Vector3[n]; var uv = new Vector2[n]; var weights = new BoneWeight[n];
                for (int i = 0; i < n; i++)
                {
                    v[i] = V(r); normal[i] = V(r); uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    weights[i] = new BoneWeight { weight0 = r.ReadSingle(), weight1 = r.ReadSingle(), weight2 = r.ReadSingle(), weight3 = r.ReadSingle(), boneIndex0 = r.ReadInt32(), boneIndex1 = r.ReadInt32(), boneIndex2 = r.ReadInt32(), boneIndex3 = r.ReadInt32() };
                }
                var indices = new int[count]; for (int i = 0; i < count; i++) indices[i] = r.ReadInt32();
                mesh.vertices = v; mesh.normals = normal; mesh.uv = uv; mesh.triangles = indices;
                int submeshes = r.ReadInt32(); var groups = new System.Collections.Generic.List<int>[submeshes];
                for (int i = 0; i < submeshes; i++) groups[i] = new System.Collections.Generic.List<int>();
                for (int i = 0; i < count; i += 3) { int group = r.ReadInt32(); groups[group].Add(indices[i]); groups[group].Add(indices[i + 1]); groups[group].Add(indices[i + 2]); }
                mesh.subMeshCount = submeshes; for (int i = 0; i < submeshes; i++) mesh.SetTriangles(groups[i], i);
                if (bones != null) { mesh.boneWeights = weights; mesh.bindposes = bones.Select(t => t.worldToLocalMatrix * root.localToWorldMatrix).ToArray(); }
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
            }
            Save(mesh, name + ".asset"); return AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/" + name + ".asset");
        }
        static Transform AddMesh(string source, string name, Transform parent)
        {
            var t = Child(name + " surface", parent, Vector3.zero); t.gameObject.AddComponent<MeshFilter>().sharedMesh = ReadMesh(source, name);
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterials = gunMaterials; return t;
        }
        static Material[] ImportMaterials(string source, string name, Part[] parts)
        {
            foreach (string role in new[] { "CleanBase", "CleanSurface" })
            {
                string path = Root + "/" + name + "_" + role + ".png"; File.Copy(Path.Combine(source, name + "_" + role + ".png"), path, true); AssetDatabase.ImportAsset(path);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path); ti.sRGBTexture = role == "CleanBase"; ti.maxTextureSize = 2048; ti.mipmapEnabled = true; ti.anisoLevel = 4;
                ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.SaveAndReimport();
            }
            var result = new Material[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var d = parts[i]; var m = new Material(Shader.Find("Aquarium/Clean Equipment")); m.SetColor("_Color", d.atlas ? Color.white : new Color(d.color[0], d.color[1], d.color[2]));
                m.SetFloat("_UseAtlas", d.atlas ? 1 : 0); m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/" + name + "_CleanBase.png"));
                m.SetTexture("_SurfaceTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/" + name + "_CleanSurface.png"));
                m.SetFloat("_Metallic", d.metallic); m.SetFloat("_Glossiness", d.smoothness); m.SetFloat("_Detail", d.detail);
                string file = name + "_" + d.name + ".mat"; Save(m, file); result[i] = AssetDatabase.LoadAssetAtPath<Material>(Root + "/" + file);
            }
            return result;
        }
        static void ImportDiver(string source, string name, DiverData data)
        {
            var root = new GameObject(name); var rig = root.AddComponent<AquariumRig>(); var bones = new Transform[data.bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                var b = data.bones[i]; var t = Child(b.name, b.parent < 0 ? root.transform : bones[b.parent], V(b.position)); bones[i] = t;
                t.localRotation = new Quaternion(b.rotation[0], b.rotation[1], b.rotation[2], b.rotation[3]); t.localScale = V(b.scale);
            }
            var skin = Child("Diver surface", root.transform, Vector3.zero).gameObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = ReadMesh(source, name, bones, root.transform); skin.bones = bones; skin.rootBone = bones[0]; skin.quality = SkinQuality.Bone4;
            skin.sharedMaterials = diverMaterials; skin.localBounds = new Bounds(Vector3.zero, Vector3.one * 4);
            rig.skin = skin; rig.restPositions = bones.Select(t => t.localPosition).ToArray(); rig.restRotations = bones.Select(t => t.localRotation).ToArray(); rig.restScales = bones.Select(t => t.localScale).ToArray();
            rig.clips = new AnimationClip[data.clips.Length];
            for (int c = 0; c < data.clips.Length; c++)
            {
                var input = data.clips[c]; string path = Root + "/Diver_" + input.name + ".anim";
                if (name == "Diver")
                {
                    var clip = new AnimationClip { name = input.name, legacy = true, frameRate = 30, wrapMode = WrapMode.Loop }; int frames = input.values.Length / bones.Length / 10;
                    for (int b = 0; b < bones.Length; b++) for (int field = 0; field < 10; field++)
                    {
                        string property = field < 3 ? "m_LocalPosition." + "xyz"[field] : field < 7 ? "m_LocalRotation." + "xyzw"[field - 3] : "m_LocalScale." + "xyz"[field - 7];
                        var keys = new Keyframe[frames]; for (int f = 0; f < frames; f++) keys[f] = new Keyframe(f / 30f, input.values[(f * bones.Length + b) * 10 + field]);
                        var curve = new AnimationCurve(keys); for (int k = 0; k < frames; k++) { AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Auto); AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Auto); }
                        clip.SetCurve(AnimationUtility.CalculateTransformPath(bones[b], root.transform), typeof(Transform), property, curve);
                    }
                    clip.EnsureQuaternionContinuity(); Save(clip, "Diver_" + input.name + ".anim");
                }
                rig.clips[c] = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            }
            PrefabUtility.SaveAsPrefabAsset(root, Root + "/" + name + ".prefab"); UnityEngine.Object.DestroyImmediate(root);
        }
    }
}

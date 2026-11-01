using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium.Editor
{
    // More aliens on the cute monster base (2026-10-28):
    //  * Green:  a stalk-and-ball antenna, the lower lid raised into a smiling eye.
    //  * Blue:   a crescent-moon antenna, a heavy sleepy upper lid with the gaze lowered.
    //  * Yellow: a lightning-bolt antenna on a short stalk, the lower lid raised flat to the middle (the pupil peeks over it).
    //  * Red:    a party-hat cone antenna, a flat tilted upper lid with the pupil tucked under it.
    // Only the head's antenna and the eye expression differ. The surface keeps the base rig, bone order,
    // bind pose and every shape name (Metamorphosis, To_Matched_*, BellyOpen, Blink_*), so the variant
    // prefabs reuse the base clips, springs, arm drag and morph overlays unchanged. The meshes and the
    // recoloured atlas/palette come from ArtSources/AlienVariants (built offline from CuteMonster.bin).
    public static partial class CuteMonsterAlien
    {
        public static readonly string[] Variants = { "Green", "Blue", "Yellow", "Red" };
        static string VariantSource => Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtSources/AlienVariants"));

        [MenuItem("Aquarium/Characters/Build alien variants (green, blue, yellow, red)")]
        public static void BuildVariants()
        {
            foreach (var v in Variants) BuildVariant(v);
            AssetDatabase.SaveAssets();
            Debug.Log("CUTE_MONSTER_VARIANTS_OK " + string.Join(", ", Variants.Select(v => Folder + "/Alien_" + v + ".prefab")));
        }

        public static string BuildVariant(string variant)
        {
            if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder(Folder, "CuteMonster");
            var d = Read(Path.Combine(VariantSource, "CuteMonster_" + variant + ".bin"));
            var atlas = ImportTexture("Monster_basecolor_" + variant + ".png", "CuteMonster_" + variant + "_Atlas.png", false, VariantSource);
            var palette = ImportTexture("CuteMonster_Palette_" + variant + ".png", "CuteMonster_" + variant + "_Palette.png", true, VariantSource);
            var materials = new[] { MakeMaterial("CuteMonster_" + variant + "_Cartoon", atlas), MakeMaterial("CuteMonster_" + variant + "_Parts", palette) };
            string prefabPath = Folder + "/Alien_" + variant + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var rig = root.GetComponent<AquariumRig>();
                var bones = rig.skin.bones;
                if (bones.Length != d.boneNames.Length) throw new System.Exception(variant + ": bone count differs from the base rig");
                for (int i = 0; i < bones.Length; i++)
                    if (bones[i].name != d.boneNames[i]) throw new System.Exception(variant + ": bone " + i + " is " + bones[i].name + ", the variant expects " + d.boneNames[i]);
                // The dead-eye X reads the gaze off the iris, but the blue alien's iris is rolled down: every variant
                // carries the base eye's gaze instead, so its X sits on the shut lids exactly like the base's.
                BaseGaze(rig.skin.sharedMesh, bones, out var gaze, out var gazeAxis);
                rig.eyePupils = new[] { gaze }; rig.eyeAxes = new[] { gazeAxis };
                // The base prefab is saved in its rest pose, so the bind poses match the base mesh exactly.
                rig.skin.sharedMesh = BuildMesh(d, bones, root.transform, OutFolder + "/CuteMonster_" + variant + ".asset", "CuteMonster " + variant + " cartoon surface");
                rig.skin.sharedMaterials = materials;
                // R38: green and yellow have no eyelid mesh; their lid is paint and the shader draws the blink.
                PaintedLids(variant, rig, materials[0]);
                root.name = "Alien_" + variant;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return prefabPath;
        }

        // ------------------------------------------------------------------ painted lids (R38)
        // PaintedLids_<kind>.json (written with the bins by lid_specs.py) describes each alien's lids: the upper and
        // lower resting edges, the line they shut onto, whether the shader draws the lower lid (green and yellow have
        // theirs painted in the atlas), widths and colours. The eyelid shell's faces are dropped (its points and Blink
        // shapes stay: vertex indices, shape names and the rig's blink keys are unchanged), the mesh gets the eye's
        // rest-pose coordinates in uv3 (x, y from the eye's centre seen from the front on the eye and the skin round
        // it, (10, 10) elsewhere so nothing else is painted), the cartoon material the lid settings, and the prefab an
        // AquariumPaintedLids that feeds the rig's blink to the shader. Without the file an alien keeps its 3D lids.
        [System.Serializable]
        class PaintedLidSpec
        {
            public float[] eye, upper, lower, shut, lash, skin;
            public float lashWidth, shadeWidth, rim, lashFadeStart, lashFadeEnd, reach;
            public bool drawLower;
        }
        static Vector4 V4(float[] a, float w = 0) => new Vector4(a[0], a[1], a[2], w);
        static void PaintedLids(string kind, AquariumRig rig, Material cartoon)
        {
            var file = Path.Combine(VariantSource, "PaintedLids_" + kind + ".json");
            var old = rig.GetComponent<AquariumPaintedLids>();
            if (!File.Exists(file))
            {
                if (old != null) Object.DestroyImmediate(old, true);
                cartoon.SetFloat("_LidOn", 0); EditorUtility.SetDirty(cartoon); return;
            }
            var spec = JsonUtility.FromJson<PaintedLidSpec>(File.ReadAllText(file));
            var mesh = rig.skin.sharedMesh; var centre = new Vector3(spec.eye[0], spec.eye[1], spec.eye[2]);
            // no lid shell: drop the parts' triangles whose three corners all move when the lids shut
            int shutKey = mesh.GetBlendShapeIndex("Blink_100");
            if (shutKey >= 0 && mesh.subMeshCount > 1)
            {
                var moves = new Vector3[mesh.vertexCount]; mesh.GetBlendShapeFrameVertices(shutKey, 0, moves, null, null);
                var kept = new List<int>(); var parts = mesh.GetTriangles(1);
                for (int t = 0; t < parts.Length; t += 3)
                    if (moves[parts[t]].sqrMagnitude < 1e-12f || moves[parts[t + 1]].sqrMagnitude < 1e-12f || moves[parts[t + 2]].sqrMagnitude < 1e-12f)
                    { kept.Add(parts[t]); kept.Add(parts[t + 1]); kept.Add(parts[t + 2]); }
                if (kept.Count != parts.Length) mesh.SetTriangles(kept, 1);
            }
            var coords = new List<Vector3>(mesh.vertexCount);
            foreach (var p in mesh.vertices)
            {
                var d = p - centre; bool onEye = d.magnitude < .34f && d.z > .02f && new Vector2(d.x, d.y).magnitude < spec.reach;
                coords.Add(onEye ? new Vector3(d.x, d.y, 1) : new Vector3(10, 10, 0));   // off the eye: far outside its rim
            }
            mesh.SetUVs(3, coords); EditorUtility.SetDirty(mesh);
            cartoon.SetFloat("_LidOn", 1);
            cartoon.SetVector("_LidUpper", V4(spec.upper)); cartoon.SetVector("_LidLower", V4(spec.lower, spec.drawLower ? 1 : 0)); cartoon.SetVector("_LidShut", V4(spec.shut));
            cartoon.SetVector("_LidStyle", new Vector4(spec.lashWidth, spec.shadeWidth, spec.rim, 0));
            cartoon.SetVector("_LidFade", new Vector4(spec.lashFadeStart, spec.lashFadeEnd, 0, 0));
            cartoon.SetColor("_LidSkin", new Color(spec.skin[0], spec.skin[1], spec.skin[2]));
            cartoon.SetColor("_LidLash", new Color(spec.lash[0], spec.lash[1], spec.lash[2]));
            EditorUtility.SetDirty(cartoon);
            var lids = old != null ? old : rig.gameObject.AddComponent<AquariumPaintedLids>();
            lids.skin = rig.skin; lids.restMesh = mesh;
        }

        // The base (purple) alien comes from the original import, not from a variant bin: paint its lids onto the saved
        // prefab, mesh and material in place (run again after re-importing the base).
        [MenuItem("Aquarium/Characters/Painted eyelids on the base alien")]
        public static string PaintBaseLids()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var rig = root.GetComponent<AquariumRig>();
                PaintedLids("Base", rig, rig.skin.sharedMaterials[0]);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            return PrefabPath;
        }

        // Same rule as AquariumDeadEyes on the base mesh: the front-most point of the eyeball and its axis from the ball's centre.
        static void BaseGaze(Mesh mesh, Transform[] bones, out Vector3 gaze, out Vector3 axis)
        {
            int eye = System.Array.FindIndex(bones, b => b != null && b.name == "Eye");
            if (eye < 0) throw new System.Exception("base rig has no Eye bone");
            var vertices = mesh.vertices; var weights = mesh.boneWeights; var centre = Vector3.zero; int count = 0, best = -1; float bestZ = float.MinValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = weights[i]; float share = (w.boneIndex0 == eye ? w.weight0 : 0) + (w.boneIndex1 == eye ? w.weight1 : 0) + (w.boneIndex2 == eye ? w.weight2 : 0) + (w.boneIndex3 == eye ? w.weight3 : 0);
                if (share < .6f) continue;
                centre += vertices[i]; count++;
                if (vertices[i].z > bestZ) { bestZ = vertices[i].z; best = i; }
            }
            if (count == 0) throw new System.Exception("base mesh has no eyeball");
            centre /= count; gaze = vertices[best]; axis = (gaze - centre).normalized;
        }

        // ------------------------------------------------------------------ review renders (Temp/ClaudeQA/variants-*.png)
        [MenuItem("Aquarium/QA/Claude 4 - Capture alien variants")]
        public static void CaptureVariants()
        {
            const string outDir = "Temp/ClaudeQA"; Directory.CreateDirectory(outDir);
            var report = new StringBuilder();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("QA camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.13f, .27f, .35f); cam.fieldOfView = 28; cam.nearClipPlane = .05f;
            var key = new GameObject("Key light").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.0f; key.transform.rotation = Quaternion.Euler(32, 152, 0);   // from the front-left, so front views show the true colours key.shadows = LightShadows.Soft;
            var fill = new GameObject("Fill light").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .35f; fill.color = new Color(.75f, .85f, 1f); fill.transform.rotation = Quaternion.Euler(15, -40, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.36f, .38f, .42f);   // kept below clipping so light spots keep their hue
            var rt = new RenderTexture(1600, 1000, 24) { antiAliasing = 4 };
            var names = new[] { "Alien" }.Concat(Variants.Select(v => "Alien_" + v)).ToArray();
            // seen from the front, left to right: green, yellow, the base in the middle, red, blue
            var slots = new[] { "Alien_Green", "Alien_Yellow", "Alien", "Alien_Red", "Alien_Blue" };
            var rigs = new AquariumRig[names.Length];
            // edit-mode skinning does not refresh between manual renders: every shot draws a freshly baked copy
            var proxies = new MeshFilter[names.Length];
            // the game's dead eyes (shut smooth lid + X), driven exactly as AquariumCreature does when a creature is dead
            var deadEyes = new AquariumDeadEyes[names.Length];
            try
            {
                for (int i = 0; i < names.Length; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + names[i] + ".prefab");
                    if (prefab == null) throw new System.Exception("missing " + names[i]);
                    var go = (GameObject)Object.Instantiate(prefab); go.name = names[i];
                    // +x is the viewer's left from the front camera
                    int slot = System.Array.IndexOf(slots, names[i]); if (slot < 0) slot = i;
                    go.transform.SetPositionAndRotation(new Vector3((2 - slot) * 2.3f, 0, 0), Quaternion.identity);
                    rigs[i] = go.GetComponent<AquariumRig>(); deadEyes[i] = new AquariumDeadEyes(go.transform);
                    var mesh = rigs[i].skin.sharedMesh;
                    report.AppendLine(names[i] + ": mesh " + mesh.name + " verts " + mesh.vertexCount + " shapes " + mesh.blendShapeCount + " bones " + rigs[i].skin.bones.Length + " clips " + rigs[i].clips.Length
                        + " materials " + string.Join("/", rigs[i].skin.sharedMaterials.Select(m => m.name)));
                    var skin = rigs[i].skin; var proxy = new GameObject("Pose capture"); proxy.transform.SetParent(skin.transform, false);
                    // BakeMesh includes the inherited skeleton scale; do not apply it again through the proxy's parents
                    var inherited = skin.transform.parent == null ? Vector3.one : skin.transform.parent.lossyScale;
                    proxy.transform.localScale = new Vector3(1 / inherited.x, 1 / inherited.y, 1 / inherited.z);
                    proxies[i] = proxy.AddComponent<MeshFilter>(); proxies[i].sharedMesh = new Mesh();
                    var pr = proxy.AddComponent<MeshRenderer>(); pr.sharedMaterials = skin.sharedMaterials; pr.receiveShadows = skin.receiveShadows; skin.enabled = false;
                }
                // Shape names and clips must match the base so every animation runs on the variants.
                var baseMesh = rigs[0].skin.sharedMesh;
                for (int i = 1; i < rigs.Length; i++)
                {
                    var m = rigs[i].skin.sharedMesh;
                    for (int k = 0; k < baseMesh.blendShapeCount; k++)
                        if (m.GetBlendShapeIndex(baseMesh.GetBlendShapeName(k)) < 0) throw new System.Exception(names[i] + " lacks shape " + baseMesh.GetBlendShapeName(k));
                    if (!rigs[i].clips.SequenceEqual(rigs[0].clips)) throw new System.Exception(names[i] + " does not share the base clips");
                }
                report.AppendLine("shared clips: " + string.Join(", ", rigs[0].clips.Select(c => c.name)));
                void Bake()
                {
                    for (int i = 0; i < rigs.Length; i++)
                    {
                        rigs[i].skin.BakeMesh(proxies[i].sharedMesh);
                        // painted lids (R38): the blink lives in the property block; edit mode runs no LateUpdate
                        var painted = rigs[i].GetComponent<AquariumPaintedLids>(); if (painted != null) painted.Apply();
                        // the dead eyes swap in a mesh with extra parts (eye white, X) and their materials: copy them every time
                        var pr = proxies[i].GetComponent<MeshRenderer>(); pr.sharedMaterials = rigs[i].skin.sharedMaterials;
                        var block = new MaterialPropertyBlock(); rigs[i].skin.GetPropertyBlock(block); pr.SetPropertyBlock(block);
                    }
                }
                void OpenEyes()
                {
                    foreach (var r in rigs) for (int k = 0; k < r.skin.sharedMesh.blendShapeCount; k++) if (r.skin.sharedMesh.GetBlendShapeName(k).StartsWith("Blink")) r.skin.SetBlendShapeWeight(k, 0);
                }
                float clock = 100;
                void Pose(string clip, float t, float blink, bool dead = false, string blinkShape = "Blink_100")
                {
                    for (int i = 0; i < rigs.Length; i++)
                    {
                        var r = rigs[i];
                        // a new clip starts a cross-fade from the previous pose: sample again past the fade so the shot shows the clip itself
                        r.SamplePose(clip, t, clock); r.SamplePose(clip, t, clock + 2);
                        if (blink >= 0) { int b = r.skin.sharedMesh.GetBlendShapeIndex(blinkShape); for (int k = 0; k < r.skin.sharedMesh.blendShapeCount; k++) if (r.skin.sharedMesh.GetBlendShapeName(k).StartsWith("Blink")) r.skin.SetBlendShapeWeight(k, 0); r.skin.SetBlendShapeWeight(b, blink); }
                        if (dead) deadEyes[i].Present(r); else deadEyes[i].Hide();
                    }
                    clock += 10; Bake();
                }
                // the dead X of rig i, to frame a close-up straight onto it whatever the death pose
                Transform DeadMark(int i) => rigs[i].transform.Cast<Transform>().FirstOrDefault(c => c.name == "Dead eye X" && c.gameObject.activeSelf);
                void Shot(string file, Vector3 from, Vector3 at, float fov = 28, Vector3? up = null)
                {
                    cam.transform.position = from; cam.transform.LookAt(at, up ?? Vector3.up); cam.fieldOfView = fov; cam.targetTexture = rt; cam.Render();
                    var old = RenderTexture.active; RenderTexture.active = rt;
                    var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(outDir, file + ".png"), image.EncodeToPNG()); RenderTexture.active = old; Object.DestroyImmediate(image);
                }
                Pose("Idle", .4f, -1);
                Shot("variants-group-front", new Vector3(0, .55f, 19.5f), new Vector3(0, .35f, 0));
                Shot("variants-group-34", new Vector3(-9.5f, 3.6f, 14.5f), new Vector3(0, .3f, 0));
                for (int i = 1; i < rigs.Length; i++)
                {
                    var p = rigs[i].transform.position; string n = names[i].Substring(6).ToLowerInvariant();
                    Shot("variants-" + n + "-front", p + new Vector3(0, .55f, 4.2f), p + new Vector3(0, .5f, 0), 30);
                    Shot("variants-" + n + "-head", p + new Vector3(0, .5f, 2.2f), p + new Vector3(0, .5f, .2f), 30);
                    Shot("variants-" + n + "-side", p + new Vector3(3.6f, .9f, 1.2f), p + new Vector3(0, .5f, 0), 30);
                    Shot("variants-" + n + "-back", p + new Vector3(-1.6f, 1.6f, -3.4f), p + new Vector3(0, .5f, 0), 30);
                }
                // Same clips on every alien: a blink, the swim, a tentacle meal and the harpoon death.
                Pose("Idle", .4f, 100);
                string Tag(int i) => names[i] == "Alien" ? "base" : names[i].Substring(6).ToLowerInvariant();
                for (int i = 0; i < rigs.Length; i++) { var p = rigs[i].transform.position; Shot("variants-" + Tag(i) + "-blink", p + new Vector3(0, .5f, 2.2f), p + new Vector3(0, .5f, .2f), 30); }
                // part-way through a blink (the shapes the rig crosses), for the painted lids sliding shut
                foreach (var (shape, tag) in new[] { ("Blink_25", "blink25"), ("Blink_50", "blink50"), ("Blink_75", "blink75") })
                {
                    Pose("Idle", .4f, 100, false, shape);
                    for (int i = 0; i < rigs.Length; i++) { var p = rigs[i].transform.position; Shot("variants-" + Tag(i) + "-" + tag, p + new Vector3(0, .5f, 2.2f), p + new Vector3(0, .5f, .2f), 30); }
                }
                // the open eyes up close, base included
                OpenEyes(); Pose("Idle", .4f, -1);
                { var p = rigs[0].transform.position; Shot("variants-base-head", p + new Vector3(0, .5f, 2.2f), p + new Vector3(0, .5f, .2f), 30); }
                foreach (var (clip, t) in new[] { ("Swim", .6f), ("TentacleEat", 1.1f), ("Harpoon", 2.6f) })
                {
                    if (!rigs[0].HasClip(clip)) { report.AppendLine("clip " + clip + " not on the base, skipped"); continue; }
                    // the harpoon death shows the game's dead eyes: one smooth shut lid with the X, no lash line
                    OpenEyes(); Pose(clip, t, -1, clip == "Harpoon");
                    Shot("variants-clip-" + clip.ToLowerInvariant(), new Vector3(-6.5f, 3.2f, 16.5f), new Vector3(0, .3f, 0));
                }
                // Dead-eye close-ups: during the harpoon death (camera square onto each X) and on the idle face.
                var tags = names.Select(n => n == "Alien" ? "base" : n.Substring(6).ToLowerInvariant()).ToArray();
                for (int i = 0; i < rigs.Length; i++)
                {
                    var mark = DeadMark(i);
                    if (mark == null) { report.AppendLine(names[i] + ": no dead-eye X shown"); continue; }
                    report.AppendLine(names[i] + ": dead X at " + mark.position.ToString("F3") + " facing " + mark.forward.ToString("F3"));
                    Shot("variants-" + tags[i] + "-dead-harpoon", mark.position + mark.forward * 1.5f + mark.up * .15f, mark.position, 30, mark.up);
                }
                OpenEyes(); Pose("Idle", .4f, -1, true);
                for (int i = 0; i < rigs.Length; i++) { var p = rigs[i].transform.position; Shot("variants-" + tags[i] + "-dead", p + new Vector3(0, .5f, 2.2f), p + new Vector3(0, .5f, .2f), 30); }
                File.WriteAllText(Path.Combine(outDir, "variants-report.txt"), report.ToString());
                Debug.Log("CLAUDE_QA_VARIANTS\n" + report);
            }
            finally
            {
                cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt);
                foreach (var e in deadEyes) e?.Dispose();
                foreach (var p in proxies) if (p != null) Object.DestroyImmediate(p.sharedMesh);
                foreach (var r in rigs) if (r != null) Object.DestroyImmediate(r.gameObject);
            }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumEquipmentChecks
    {
        public static void ImportAndCheck() { AquariumEquipmentImport.Run(); Run(); }
        public static void ImportCheckAndBuild() { AquariumEquipmentImport.Run(); Run(); AquariumBuild.Windows(); }
        public static void CheckAndBuild() { Run(); AquariumBuild.Windows(); }
        public static void ImportCartoonAndCheck() { AquariumCartoonImport.Run(); Run(); }
        public static void ImportCartoonCheckAndBuild() { ImportCartoonAndCheck(); AquariumBuild.Windows(); }
        static void Require(bool condition, string detail) { if (!condition) throw new Exception("Equipment check: " + detail); }
        public static void Run()
        {
            string output = Environment.GetEnvironmentVariable("AQUARIUM_EQUIPMENT_QA") ?? "Temp/EquipmentQA"; Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = Resources.Load<GameObject>("AquariumEquipment/Diver"); Require(prefab != null, "diver prefab");
            var rig = UnityEngine.Object.Instantiate(prefab).GetComponent<AquariumRig>();
            Require(rig.skin.bones.Length == 17 && rig.clips.Length >= 6, "diver rig with separate flexible fin tips and gameplay clips");
            Require(rig.skin.sharedMesh.triangles.Length / 3 < 55000, "diver topology budget");
            Require(rig.skin.sharedMaterials.Length == 2 && rig.skin.sharedMesh.subMeshCount == 2, "painted character atlas and clear lens");
            foreach (var material in rig.skin.sharedMaterials) Require(material != null && (material.shader.name == "Aquarium/Cartoon Character" || material.shader.name == "Standard"), "cartoon material shader available");
            foreach (var w in rig.skin.sharedMesh.boneWeights) Require(Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1) < .001f, "normalized skin weights");
            foreach (var clip in rig.clips)
            {
                Vector3[] first = null; float displacement = 0;
                for (int frame = 0; frame < 25; frame++)
                {
                    clip.SampleAnimation(rig.gameObject, frame * .1f); var mesh = new Mesh(); rig.skin.BakeMesh(mesh);
                    var v = mesh.vertices; if (first == null) first = v;
                    foreach (var p in v) Require(!float.IsNaN(p.x) && p.sqrMagnitude < 9, "stable skinned geometry");
                    for (int i = 0; i < v.Length; i += 13) displacement = Mathf.Max(displacement, Vector3.Distance(first[i], v[i]));
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
                Require(displacement > .001f, "animated pose " + clip.name);
            }
            UnityEngine.Object.DestroyImmediate(rig.gameObject);
            CheckFirstPersonHands();
            var parent = new GameObject("Equipment gameplay review"); var view = new AquariumView(parent.transform);
            var model = new AquariumModel(); model.AddPlayer(1, "Alien", Team.Hider); model.AddPlayer(2, "Diver", Team.Seeker); model.SetRole(2, Team.Seeker, true);
            Require(model.StartRound(), "round begins"); model.Players.RemoveAll(p => p.filled);
            while (model.Time < model.HuntAt + .1f) model.Tick(1f / 30);
            var diver = model.Player(2); diver.position = new Point(2.3f, 9, 4); diver.facing = new Point(0, 0, 1);
            model.Player(1).position = new Point(-20, 8, -5);
            foreach (var f in model.Fish) { f.position = new Point(-20, 8, -5); f.facing = new Point(1, 0); f.turnAt = float.MaxValue; }
            var rt = new RenderTexture(1440, 900, 24); view.Camera.targetTexture = rt;
            var camera = new AquariumThirdPersonCamera(view.Camera); var state = model.Snapshot();
            view.Draw(state, 2, .0333f); view.Camera.transform.position = new Vector3(4.8f, 9.8f, 7); view.Camera.transform.LookAt(new Vector3(2.3f, 9, 4)); view.Camera.fieldOfView = 42;
            view.PresentEquipment(state, 0, false); Capture(view.Camera, rt, Path.Combine(output, "Diver-in-aquarium.png"));
            view.Camera.transform.position = new Vector3(3.35f, 8.95f, 4.7f); view.Camera.transform.LookAt(new Vector3(2.65f, 8.82f, 4.65f)); view.Camera.fieldOfView = 40;
            Capture(view.Camera, rt, Path.Combine(output, "Trigger-grip.png"));
            camera.Prepare(state, 2, 0); camera.UpdatePose(state, view, 1); view.PresentEquipment(state, 2, true);
            Capture(view.Camera, rt, Path.Combine(output, "FPS-ready.png"));
            var fpsGun = view.Camera.GetComponentsInChildren<AquariumHarpoonGun>().Single();
            Require(fpsGun.transform.Find("Cartoon barrel").GetComponent<MeshFilter>().sharedMesh.name == "HarpoonGun cartoon surface", "extracted character hand uses its original prop");
            var screen = view.Camera.WorldToViewportPoint(fpsGun.muzzle.position);
            Require(screen.x > .5f && screen.x < .9f && screen.y > .08f && screen.y < .5f, "muzzle in lower right");
            Require(fpsGun.loadedHarpoon.gameObject.activeSelf, "loaded spear before firing");
            model.SetInput(2, new SwimInput { aimZ = 1, action = true });
            bool returned = false, outbound = false; int ropeFrames = 0; float fired = model.Time + 1f / 30;
            for (int frame = 0; frame < 98; frame++)
            {
                model.Tick(1f / 30); state = JsonUtility.FromJson<AquariumSnapshot>(JsonUtility.ToJson(model.Snapshot()));
                view.Draw(state, 2, 1f / 30); camera.Prepare(state, 2, 0); camera.UpdatePose(state, view, 1f / 30); view.PresentEquipment(state, 2, true);
                if (state.harpoons.Length > 0)
                {
                    var shot = state.harpoons[0]; returned |= shot.returning; outbound |= !shot.returning;
                    Require(!fpsGun.loadedHarpoon.gameObject.activeSelf, "no duplicate loaded spear");
                    var rope = parent.GetComponentsInChildren<LineRenderer>().Single(x => x.name == "Braided retrieval line");
                    Require(Vector3.Distance(rope.GetPosition(0), fpsGun.ropeExit.position) < .001f, "tether follows actual FPS muzzle");
                    Require(Vector3.Distance(rope.GetPosition(rope.positionCount - 1), rope.transform.parent.position) < .001f, "tether attached to spear tail"); ropeFrames++;
                }
                if (new[] { 5, 24, 53, 85, 95 }.Contains(frame)) Capture(view.Camera, rt, Path.Combine(output, "FPS-shot-" + frame.ToString("00") + ".png"));
            }
            Require(outbound && returned && ropeFrames > 80 && model.Harpoons.Count == 0 && fpsGun.loadedHarpoon.gameObject.activeSelf, "complete flight, reel and reload");
            view.PresentEquipment(state, 1, false); Require(!view.Camera.GetComponentsInChildren<AquariumHarpoonGun>().Any(), "equipment hidden for alien view");
            view.Camera.targetTexture = null; view.Dispose(); UnityEngine.Object.DestroyImmediate(parent); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            File.WriteAllText(Path.Combine(output, "validation.txt"), "Original character holding hand extracted into an 8-bone first-person rig; original HarpoonGun; all first-person clips retain edge lengths within 4 percent of the source shape; actual baked-skin camera review; lower-right muzzle; serialized host-authoritative flight/retrieval; " + ropeFrames + " tether frames; no duplicated loaded spear; 3-second reload; hider camera hides FPS weapon.");
            Debug.Log("AQUARIUM_EQUIPMENT_CHECKS_OK ropeFrames=" + ropeFrames);
        }
        static void CheckFirstPersonHands()
        {
            var hands = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("AquariumEquipment/DiverHands")).GetComponent<AquariumRig>();
            var mesh = hands.skin.sharedMesh; var rest = mesh.vertices; var triangles = mesh.triangles; float stretch = 0;
            Require(hands.Bones.Length == 8, "dedicated first-person arm rig");
            Require(rest.All(v=>v.x>.05f), "first-person view contains only the holding arm");
            Require(mesh.GetBlendShapeIndex("ProneSurfaceNormals") < 0, "no stretched torso corrective on first-person sleeves");
            foreach (var clip in hands.clips) for (int frame = 0; frame <= 24; frame++)
            {
                clip.SampleAnimation(hands.gameObject, clip.length * frame / 24); var baked = new Mesh(); hands.skin.BakeMesh(baked); var v = baked.vertices;
                for (int i = 0; i < triangles.Length; i += 3) for (int edge = 0; edge < 3; edge++)
                {
                    int a = triangles[i + edge], b = triangles[i + (edge + 1) % 3]; float length = Vector3.Distance(rest[a], rest[b]);
                    if (length > .001f) stretch = Mathf.Max(stretch, Vector3.Distance(v[a], v[b]) / length);
                }
                for (int i = 0; i < v.Length; i++) Require(float.IsFinite(v[i].x) && v[i].sqrMagnitude < 4, "finite first-person sleeves");
                UnityEngine.Object.DestroyImmediate(baked);
            }
            Require(stretch < 1.04f, "first-person arms retain rounded shape through animation: " + stretch);
            Debug.Log("FPS_HAND_SHAPE_OK maxEdgeStretch=" + stretch);
            UnityEngine.Object.DestroyImmediate(hands.gameObject);
        }
        static void Capture(Camera camera, RenderTexture rt, string path)
        {
            // A batch editor does not advance GPU skinning between sampled poses.
            // Render the exact CPU-baked mesh so the review shows actual deformation.
            var skins = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            var proxies = new System.Collections.Generic.List<GameObject>();
            foreach (var skin in skins)
            {
                var mesh = new Mesh(); skin.BakeMesh(mesh); var go = new GameObject("Sampled equipment surface"); go.layer = skin.gameObject.layer;
                go.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation); go.transform.localScale = skin.transform.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterials = skin.sharedMaterials;
                renderer.shadowCastingMode = skin.shadowCastingMode; renderer.receiveShadows = skin.receiveShadows; skin.enabled = false; proxies.Add(go);
            }
            camera.Render();
            foreach (var overlay in camera.GetComponentsInChildren<Camera>()) if (overlay != camera && overlay.enabled) overlay.Render();
            var old = RenderTexture.active; RenderTexture.active = rt; var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image);
            foreach (var proxy in proxies) { UnityEngine.Object.DestroyImmediate(proxy.GetComponent<MeshFilter>().sharedMesh); UnityEngine.Object.DestroyImmediate(proxy); }
            foreach (var skin in skins) skin.enabled = true;
        }
    }
}

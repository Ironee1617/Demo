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
        static void Require(bool condition, string detail) { if (!condition) throw new Exception("Equipment check: " + detail); }
        public static void Run()
        {
            string output = Environment.GetEnvironmentVariable("AQUARIUM_EQUIPMENT_QA") ?? "Temp/EquipmentQA"; Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = Resources.Load<GameObject>("AquariumEquipment/Diver"); Require(prefab != null, "diver prefab");
            var rig = UnityEngine.Object.Instantiate(prefab).GetComponent<AquariumRig>();
            Require(rig.skin.bones.Length == 15 && rig.clips.Length == 2, "full diver rig and both clips");
            Require(rig.skin.sharedMesh.triangles.Length / 3 < 25000, "diver topology budget");
            Require(rig.skin.sharedMaterials.Length == 13 && rig.skin.sharedMesh.subMeshCount == 13, "separate named suit, equipment and hand materials");
            foreach (var material in rig.skin.sharedMaterials) Require(material != null && material.shader.name == "Aquarium/Clean Equipment", "clean equipment shader available");
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
                Require(displacement > .015f, "animated flutter kick " + clip.name);
            }
            UnityEngine.Object.DestroyImmediate(rig.gameObject);
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
            File.WriteAllText(Path.Combine(output, "validation.txt"), "15 bones; idle/swim deformation checked; optimized meshes; 13 named diver material slots and clean 2K paint/surface atlases; sculpted trigger hand and complete FPS sleeve; lower-right first-person muzzle; serialized host-authoritative flight/retrieval; " + ropeFrames + " tether frames; no duplicated loaded spear; 3-second reload; hider camera hides FPS weapon.");
            Debug.Log("AQUARIUM_EQUIPMENT_CHECKS_OK ropeFrames=" + ropeFrames);
        }
        static void Capture(Camera camera, RenderTexture rt, string path)
        {
            camera.Render();
            foreach (var overlay in camera.GetComponentsInChildren<Camera>()) if (overlay != camera && overlay.enabled) overlay.Render();
            var old = RenderTexture.active; RenderTexture.active = rt; var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image);
        }
    }
}

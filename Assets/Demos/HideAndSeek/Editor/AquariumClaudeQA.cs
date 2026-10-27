using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    // Review helpers: apply the diver, run the existing checks and render the new gameplay features
    // (WASD diver strokes, 5 m harpoon with curved reel-in, biscuit food, harpoon inks, dead X eyes)
    // into Temp/ClaudeQA. Nothing here changes game assets except DiverHarpoon.Apply.
    public static class AquariumClaudeQA
    {
        const string Out = "Temp/ClaudeQA";

        [MenuItem("Aquarium/QA/Claude 1 - Apply diver and run checks")]
        public static void ApplyAndCheck()
        {
            Directory.CreateDirectory(Out);
            var report = new StringBuilder();
            Step(report, "DiverHarpoon.Apply", DiverHarpoon.Apply);
            Step(report, "Cute monster lower lid colour", () =>
            {
                var rig = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Demos/HideAndSeek/Characters/Resources/AquariumCharacters/Alien.prefab")?.GetComponent<AquariumRig>();
                if (rig == null) throw new Exception("alien prefab not found");
                int before = CuteMonsterAlien.LowerLidColour(rig.skin.sharedMesh);
                if (before == 1) { EditorUtility.SetDirty(rig.skin.sharedMesh); AssetDatabase.SaveAssets(); }
                report.AppendLine("     lower lid result " + before + " (1 fixed now, 0 already fixed)");
                if (before < 0) throw new Exception("unexpected lid layout");
            });
            Step(report, "AquariumEquipmentChecks.Run", AquariumEquipmentChecks.Run);
            Step(report, "AquariumCartoonChecks.Run", () => Call("AquariumCartoonChecks", "Run"));
            Step(report, "AquariumMovementChecks.Run", AquariumMovementChecks.Run);
            Step(report, "AquariumSwimmingChecks.Run", AquariumSwimmingChecks.Run);
            File.WriteAllText(Path.Combine(Out, "report.txt"), report.ToString());
            Debug.Log("CLAUDE_QA_REPORT\n" + report);
        }

        [MenuItem("Aquarium/QA/Claude 2 - Capture new features")]
        public static void Capture()
        {
            Directory.CreateDirectory(Out);
            var report = new StringBuilder();
            Step(report, "Feed biscuits", FeedScenario);
            Step(report, "Harpoon fish", () => HarpoonScenario(false));
            Step(report, "Harpoon alien", () => HarpoonScenario(true));
            Step(report, "Diver directions", DirectionScenario);
            File.WriteAllText(Path.Combine(Out, "capture-report.txt"), report.ToString());
            Debug.Log("CLAUDE_QA_CAPTURE\n" + report);
        }

        [MenuItem("Aquarium/QA/Claude 3 - Capture eyes, spear, egg, biscuit, dive")]
        public static void CaptureFixes()
        {
            Directory.CreateDirectory(Out);
            var report = new StringBuilder();
            Step(report, "Dead eyes", () => DeadEyes(report));
            Step(report, "Spear shaft", SpearScenario);
            Step(report, "Glossy egg", EggScenario);
            Step(report, "Biscuit crumble", () => CrumbleScenario(report));
            Step(report, "Dive entry", () => DiveScenario(report));
            File.WriteAllText(Path.Combine(Out, "fixes-report.txt"), report.ToString());
            Debug.Log("CLAUDE_QA_FIXES\n" + report);
        }

        static void Step(StringBuilder report, string name, Action action)
        {
            try { action(); report.AppendLine("OK   " + name); }
            catch (Exception e) { var x = e is System.Reflection.TargetInvocationException && e.InnerException != null ? e.InnerException : e; report.AppendLine("FAIL " + name + ": " + x.Message + "\n" + x.StackTrace); }
        }
        static void Call(string type, string method)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetTypes().FirstOrDefault(x => x.Name == type);
                if (t == null) continue;
                t.GetMethod(method, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
                return;
            }
            throw new Exception("Missing " + type);
        }

        // ------------------------------------------------------------------ shared stage
        sealed class Stage : IDisposable
        {
            public GameObject parent; public AquariumView view; public AquariumModel model; public RenderTexture rt; public AquariumSnapshot state;
            public Stage()
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                parent = new GameObject("Claude QA stage"); view = new AquariumView(parent.transform);
                // A second alien keeps the round running after the first one is speared.
                model = new AquariumModel(); model.AddPlayer(1, "Alien", Team.Hider); model.AddPlayer(2, "Diver", Team.Seeker); model.AddPlayer(3, "Alien 2", Team.Hider);
                model.SetRole(2, Team.Seeker, true); model.SetRole(3, Team.Hider, true);
                if (!model.StartRound()) throw new Exception("round did not start");
                model.Players.RemoveAll(p => p.filled);
                while (model.Time < model.HuntAt + .1f) model.Tick(1f / 30);
                var diver = model.Player(2); diver.position = new Point(2.3f, 9, 4); diver.facing = new Point(0, 0, 1);
                model.Player(1).position = new Point(-20, 8, -5); model.Player(1).lastMealAt = float.MaxValue;
                model.Player(3).position = new Point(-20, 8, 3); model.Player(3).lastMealAt = float.MaxValue;
                foreach (var f in model.Fish) { f.position = new Point(-20, 8, -5); f.facing = new Point(1, 0); f.turnAt = float.MaxValue; f.steering = new Point(); f.swimSpeed = 0; }
                rt = new RenderTexture(1280, 800, 24) { antiAliasing = 4 }; view.Camera.targetTexture = rt; view.Camera.fieldOfView = 45;
            }
            public void Step(int localId, float dt = 1f / 30, bool firstPerson = false)
            {
                model.Tick(dt); state = JsonUtility.FromJson<AquariumSnapshot>(JsonUtility.ToJson(model.Snapshot()));
                view.Draw(state, localId, dt); view.PresentEquipment(state, firstPerson ? 2 : 0, firstPerson);
            }
            public void Shoot(string file, Vector3 from, Vector3 at, float fov = 45)
            {
                view.Camera.transform.position = from; view.Camera.transform.LookAt(at); view.Camera.fieldOfView = fov;
                view.Camera.targetTexture = rt; view.Camera.Render();
                var old = RenderTexture.active; RenderTexture.active = rt;
                var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(Out, file + ".png"), image.EncodeToPNG()); RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image);
            }
            public Transform Find(string name) => parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            public void Dispose()
            {
                view.Camera.targetTexture = null; view.Dispose(); UnityEngine.Object.DestroyImmediate(parent); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
        }
        static Vector3 V(Point p) => new Vector3(p.x, p.y, p.z);

        // ------------------------------------------------------------------ biscuit food
        static void FeedScenario()
        {
            using (var s = new Stage())
            {
                s.Step(1);
                s.model.SetInput(2, new SwimInput { aimZ = 1, feed = true });
                for (int i = 0; i < 20; i++) s.Step(1);
                var food = s.model.Effects.First(e => e.kind == "feed");
                var at = V(food.position);
                s.Shoot("feed-close", at + new Vector3(.9f, .55f, -1.0f), at, 40);
                s.Shoot("feed-wide", at + new Vector3(2.2f, 1.0f, -2.6f), at, 45);
                for (int i = 0; i < 45; i++) s.Step(1);
                s.Shoot("feed-close-later", at + new Vector3(-.8f, .35f, -1.1f), at, 40);
                var pieces = s.parent.GetComponentsInChildren<MeshFilter>().Count(m => m.name == "Biscuit piece");
                if (pieces != 10) throw new Exception("biscuit pieces " + pieces);
                if (s.parent.GetComponentsInChildren<Transform>().Any(t => t.name == "Food pellet")) throw new Exception("old pellets still drawn");
            }
        }

        // ------------------------------------------------------------------ harpoon: 5 m range, curved reel, docking, inks, X eyes
        static void HarpoonScenario(bool alien)
        {
            using (var s = new Stage())
            {
                var diver = s.model.Player(2);
                var target = diver.position + diver.facing * 4;
                string tag = alien ? "alien" : "fish";
                int caughtId;
                if (alien) { var a = s.model.Player(1); a.position = target; a.facing = new Point(-1, 0, 0); caughtId = a.id; }
                else { var f = s.model.Fish[0]; f.position = target; f.facing = new Point(1, 0, 0); caughtId = f.id; }
                s.Step(1);
                s.model.SetInput(2, new SwimInput { aimZ = 1, action = true });
                var csv = new StringBuilder("frame,time,returning,model_x,model_y,model_z,view_x,view_y,view_z,muzzle_x,muzzle_y,muzzle_z\n");
                float maxReach = 0; bool hit = false, docked = false; Vector3 side = new Vector3(4.6f, 1.2f, 1.5f);
                for (int frame = 0; frame < 96; frame++)
                {
                    s.Step(1);
                    if (s.state.harpoons.Length > 0)
                    {
                        var h = s.state.harpoons[0];
                        maxReach = Mathf.Max(maxReach, (V(h.position) - V(diver.position)).magnitude);
                        hit |= h.caughtFish != 0 || h.caughtPlayer != 0;
                        var spear = s.Find("harpoon"); var gun = s.parent.GetComponentsInChildren<AquariumHarpoonGun>().FirstOrDefault();
                        var vp = spear != null ? spear.position : Vector3.zero; var mz = gun != null ? gun.muzzle.position : Vector3.zero;
                        csv.AppendLine(frame + "," + s.state.time.ToString("F3") + "," + h.returning + "," + h.position.x.ToString("F3") + "," + h.position.y.ToString("F3") + "," + h.position.z.ToString("F3") + "," + vp.x.ToString("F3") + "," + vp.y.ToString("F3") + "," + vp.z.ToString("F3") + "," + mz.x.ToString("F3") + "," + mz.y.ToString("F3") + "," + mz.z.ToString("F3"));
                    }
                    var heldGun = s.parent.GetComponentsInChildren<AquariumHarpoonGun>().FirstOrDefault();
                    if (heldGun != null && new[] { 62, 68, 72, 76, 80, 84, 88, 92 }.Contains(frame))
                    { var mz = heldGun.muzzle.position; s.Shoot("dock-" + tag + "-" + frame.ToString("00"), mz + new Vector3(1.6f, .5f, .6f), mz + new Vector3(0, 0, .6f), 40); }
                    var mid = V(diver.position) + new Vector3(0, 0, 2);
                    if (new[] { 4, 10, 30, 50, 66, 74, 80, 86, 90 }.Contains(frame)) s.Shoot("harpoon-" + tag + "-" + frame.ToString("00"), mid + side, mid, 50);
                    if (frame == 12)
                    {
                        // Close look at the speared body: X eyes and the ink.
                        var corpse = alien ? V(s.model.Player(1).position) : V(s.model.Fish.First(f => f.id == caughtId).position);
                        s.Shoot("dead-" + tag + "-front", corpse + new Vector3(0, .25f, -2.0f) + (alien ? Vector3.zero : new Vector3(0, 0, 0)), corpse, 32);
                        s.Shoot("dead-" + tag + "-side", corpse + new Vector3(1.9f, .4f, -.4f), corpse, 32);
                        s.Shoot("dead-" + tag + "-other", corpse + new Vector3(-1.9f, .4f, -.4f), corpse, 32);
                        if (alien) s.Shoot("dead-" + tag + "-top", corpse + new Vector3(0, 2.0f, -.6f), corpse, 32);
                    }
                    if (frame > 92 && s.model.Harpoons.Count == 0) docked = true;
                }
                File.WriteAllText(Path.Combine(Out, "harpoon-" + tag + ".csv"), csv.ToString());
                var inks = s.model.Effects.Select(e => e.kind).ToArray();
                File.AppendAllText(Path.Combine(Out, "harpoon-" + tag + ".csv"), "# reach=" + maxReach.ToString("F2") + " hit=" + hit + " docked=" + docked + " effects=" + string.Join("|", inks) + "\n");
                if (!hit) throw new Exception("harpoon missed the " + tag + " at 4 m");
                if (maxReach > 5.95f) throw new Exception("harpoon reach " + maxReach);
                if (!inks.Contains(alien ? "harpoonAlien" : "harpoonFish")) throw new Exception("missing harpoon ink");
                if (s.Find("Embedded harpoon") != null && s.Find("Embedded harpoon").gameObject.activeInHierarchy) throw new Exception("spear still drawn in the body");
            }
        }

        // ------------------------------------------------------------------ X eyes on a settled dead alien and fish
        static void DeadEyes(StringBuilder report)
        {
            using (var s = new Stage())
            {
                var alien = s.model.Player(1); alien.position = new Point(0, 8, 4); alien.facing = new Point(0, 0, -1);
                alien.alive = false; alien.deathReason = "harpoon"; alien.deathAt = alien.diedAt = s.model.Time; alien.deathSpecies = -1;
                var fish = s.model.Fish[0]; fish.position = new Point(3, 9, 4); fish.facing = new Point(-1, 0, 0); fish.dead = true;
                for (int i = 0; i < 6; i++) s.Step(1);
                ShootMarks(s, "eyes-early");
                for (int i = 0; i < 80; i++) s.Step(1);
                ShootMarks(s, "eyes-settled");
                var marks = s.parent.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Dead eye X" && t.gameObject.activeInHierarchy).ToArray();
                report.AppendLine("     active X marks " + marks.Length);
                if (marks.Length < 3) throw new Exception("expected X marks on the alien and both fish eyes, got " + marks.Length);
            }
        }
        static void ShootMarks(Stage s, string tag)
        {
            int k = 0;
            foreach (var mark in s.parent.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Dead eye X" && t.gameObject.activeInHierarchy))
            {
                float size = Mathf.Max(.05f, mark.lossyScale.x);
                s.Shoot(tag + "-" + k + "-front", mark.position + mark.forward * size * 7f + mark.up * size * .6f, mark.position, 30);
                s.Shoot(tag + "-" + k + "-oblique", mark.position + (mark.forward * 5f + mark.right * 4f + mark.up * 1.5f) * size, mark.position, 30);
                s.Shoot(tag + "-" + k + "-wide", mark.position + (mark.forward * 14f + mark.right * 6f + mark.up * 4f) * size, mark.position, 34);
                k++;
            }
        }

        // ------------------------------------------------------------------ the whole spear (rod + head) in flight, and hidden when loaded
        static void SpearScenario()
        {
            using (var s = new Stage())
            {
                var diver = s.model.Player(2);
                s.Step(1);
                var gun = s.parent.GetComponentsInChildren<AquariumHarpoonGun>().FirstOrDefault();
                if (gun == null) throw new Exception("no gun");
                var mz = gun.muzzle.position;
                s.Shoot("spear-loaded", mz + new Vector3(1.4f, .5f, -.3f), mz + new Vector3(0, -.05f, -.3f), 35);
                s.model.SetInput(2, new SwimInput { aimZ = 1, action = true });
                for (int frame = 1; frame <= 8; frame++)
                {
                    s.Step(1);
                    var spear = s.Find("harpoon");
                    if (spear != null && (frame == 3 || frame == 5 || frame == 7))
                    {
                        var tip = spear.position;
                        s.Shoot("spear-flight-" + frame, tip + new Vector3(2.2f, .5f, -.4f), tip + new Vector3(0, 0, -.2f), 40);
                    }
                }
                var tail = s.Find("Spear tail");
                if (tail == null) throw new Exception("no spear tail");
                if (Mathf.Abs(tail.localPosition.z + gun.shaftLength) > .001f) throw new Exception("tail not at the rod end: " + tail.localPosition.z);
            }
        }

        // ------------------------------------------------------------------ revival egg
        static void EggScenario()
        {
            using (var s = new Stage())
            {
                var at = new Point(0, 8, 4);
                var lost = s.model.Player(3); lost.alive = false; lost.deathReason = "harpoon"; lost.deathAt = lost.diedAt = s.model.Time; lost.position = new Point(-20, 8, 3);
                s.model.Eggs.Add(new RevivalEgg { id = 900, owner = 1, revivePlayer = 3, position = at, hatchAt = s.model.Time + 20 });
                for (int i = 0; i < 3; i++) s.Step(1);
                var p = V(at);
                s.Shoot("egg-close", p + new Vector3(.9f, .35f, -1.3f), p + new Vector3(0, .05f, 0), 32);
                s.Shoot("egg-side", p + new Vector3(-1.4f, .2f, .3f), p + new Vector3(0, .05f, 0), 32);
                if (s.Find("Shell seam") != null || s.Find("Amber egg core") != null) throw new Exception("old egg band/core still drawn");
                if (s.Find("Glossy egg") == null) throw new Exception("egg not drawn");
            }
        }

        // ------------------------------------------------------------------ biscuits crumbling away over the feed
        static void CrumbleScenario(StringBuilder report)
        {
            using (var s = new Stage())
            {
                s.Step(1);
                s.model.SetInput(2, new SwimInput { aimZ = 1, feed = true });
                s.Step(1);
                var food = s.model.Effects.First(e => e.kind == "feed");
                float born = food.born;
                foreach (float at in new[] { .6f, 1.6f, 2.4f, 3.1f, 3.8f, 4.5f, 4.95f })
                {
                    while (s.model.Time < born + at) s.Step(1);
                    var fx = V(food.position) + new Vector3(0, .3f * Mathf.Clamp01((s.model.Time - born) / food.duration), 0);
                    s.Shoot("crumble-" + at.ToString("0.0"), fx + new Vector3(1.1f, .45f, 1.2f), fx, 42);
                    var pieces = s.parent.GetComponentsInChildren<Transform>().Where(t => t.name == "Biscuit piece").ToArray();
                    float size = pieces.Length == 0 ? 0 : pieces.Average(t => t.localScale.x);
                    int crumbs = s.parent.GetComponentsInChildren<Transform>().Count(t => t.name == "Biscuit crumb");
                    report.AppendLine("     t=" + at.ToString("0.00") + " pieces " + pieces.Length + " mean scale " + size.ToString("F2") + " crumbs " + crumbs);
                }
            }
        }

        // ------------------------------------------------------------------ diver's round opening on the dive deck
        static void DiveScenario(StringBuilder report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var parent = new GameObject("Claude QA dive"); var view = new AquariumView(parent.transform);
            var rt = new RenderTexture(1280, 800, 24) { antiAliasing = 4 };
            try
            {
                var model = new AquariumModel { DiveEntry = true }; model.AddPlayer(1, "Diver", Team.Seeker); model.AddPlayer(2, "Alien", Team.Hider); model.SetRole(2, Team.Hider, true);
                if (!model.StartRound()) throw new Exception("round did not start");
                foreach (var p in model.Players) p.lastMealAt = float.MaxValue;
                var camera = new AquariumThirdPersonCamera(view.Camera);
                var diver = model.Player(1);
                float start = diver.entryAt - AquariumDiveEntry.Seconds;
                var shots = new[] { -2.0f, -.4f, .30f, .55f, .70f, .85f, .96f, 1.10f, 1.40f, 1.90f, 2.35f, 2.62f };
                int next = 0; bool splash = false; float maxY = float.MinValue, minY = float.MaxValue;
                var csv = new StringBuilder("t,x,y,z\n");
                AquariumSnapshot state = null;
                while (next < shots.Length && model.Time < diver.entryAt + 1)
                {
                    model.Tick(1f / 30); state = JsonUtility.FromJson<AquariumSnapshot>(JsonUtility.ToJson(model.Snapshot()));
                    view.Draw(state, 1, 1f / 30);
                    camera.Prepare(state, 1); camera.UpdatePose(state, view, 1f / 30);
                    view.PresentEquipment(state, camera.FollowId, camera.FirstPerson && !camera.Cinematic);
                    splash |= model.Effects.Any(e => e.kind == "splash");
                    float t = model.Time - start; maxY = Mathf.Max(maxY, diver.position.y); minY = Mathf.Min(minY, diver.position.y);
                    csv.AppendLine(t.ToString("F3") + "," + diver.position.x.ToString("F3") + "," + diver.position.y.ToString("F3") + "," + diver.position.z.ToString("F3"));
                    if (t >= shots[next])
                    {
                        string tag = shots[next].ToString("0.00");
                        Render(view, rt, "dive-cine-" + tag, null, null, 0);
                        var at = view.PlayerPosition(state.players.First(p => p.id == 1));
                        var right = new Vector3(AquariumEntryLayout.Right.x, 0, AquariumEntryLayout.Right.z); var inward = new Vector3(AquariumEntryLayout.Inward.x, 0, AquariumEntryLayout.Inward.z);
                        Render(view, rt, "dive-side-" + tag, at - right * 4.2f + inward * .6f + Vector3.up * .4f, at, 40);
                        next++;
                    }
                }
                File.WriteAllText(Path.Combine(Out, "dive-path.csv"), csv.ToString());
                report.AppendLine("     splash=" + splash + " y range " + minY.ToString("F2") + ".." + maxY.ToString("F2") + " end " + V(diver.position).ToString("F2") + " shots " + next);
                if (!splash) throw new Exception("no splash effect");
                if (maxY < AquariumModel.WaterHeight + .5f) throw new Exception("diver never stood on the deck");
                if (minY > AquariumModel.WaterHeight - .6f) throw new Exception("diver never went under");
            }
            finally { view.Camera.targetTexture = null; view.Dispose(); UnityEngine.Object.DestroyImmediate(parent); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
        }
        static void Render(AquariumView view, RenderTexture rt, string file, Vector3? from, Vector3? at, float fov)
        {
            var cam = view.Camera; var oldPos = cam.transform.position; var oldRot = cam.transform.rotation; float oldFov = cam.fieldOfView;
            if (from.HasValue) { cam.transform.position = from.Value; cam.transform.LookAt(at.Value); cam.fieldOfView = fov; }
            cam.targetTexture = rt; cam.Render();
            var old = RenderTexture.active; RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Out, file + ".png"), image.EncodeToPNG()); RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image);
            cam.targetTexture = null; cam.transform.SetPositionAndRotation(oldPos, oldRot); cam.fieldOfView = oldFov;
        }

        // ------------------------------------------------------------------ diver strokes seen by another player
        static void DirectionScenario()
        {
            var moves = new[] { ("forward", 0f, 1f), ("back", 0f, -1f), ("left", -1f, 0f), ("right", 1f, 0f), ("fwd-right", .7f, .7f) };
            foreach (var m in moves)
            {
                using (var s = new Stage())
                {
                    var diver = s.model.Player(2); diver.position = new Point(2.3f, 9, 4 - 2 * m.Item3); diver.facing = new Point(0, 0, 1);
                    for (int i = 0; i < 32; i++) { s.model.SetInput(2, new SwimInput { x = m.Item2, z = m.Item3, aimZ = 1 }); s.Step(1); }
                    var at = s.view.PlayerPosition(s.model.Player(2));
                    s.Shoot("swim-" + m.Item1 + "-34", at + new Vector3(2.4f, .9f, 2.4f), at, 40);
                    s.Shoot("swim-" + m.Item1 + "-side", at + new Vector3(3.4f, .3f, 0), at, 40);
                    s.Shoot("swim-" + m.Item1 + "-front", at + new Vector3(0, .4f, 3.4f), at, 40);
                    File.AppendAllText(Path.Combine(Out, "directions.txt"), m.Item1 + " velocity=" + V(s.model.Player(2).velocity).ToString("F2") + "\n");
                }
            }
        }
    }
}

using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    // R78b: plays a tutorial lesson from start to finish in Play mode. An autopilot steers the player through the same
    // SwimInput the keyboard and mouse fill (AquariumGame.R78QaInput), following the lesson's own marker and step;
    // every step change is timed, and a picture of each step is saved to Temp/ClaudeQA/R78b_*.png.
    public static class AquariumR78QA
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static AquariumGame game; private static AquariumSession session; private static object followCamera;
        private static int lesson, lastStep = -2, shotStep = -2; private static float began, stepBegan, nextUse, shotAt;
        private static readonly StringBuilder log = new StringBuilder();
        private static bool hooked, shots = true, cleared;
        // Seconds the autopilot waits at the start of every step (a person reading the objective) and its swimming effort.
        public static float Think = 0f, Effort = 1f;
        public static string Folder { get { return Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/ClaudeQA"); } }

        private static bool Find()
        {
            game = UnityEngine.Object.FindFirstObjectByType<AquariumGame>(); if (game == null) return false;
            session = (AquariumSession)typeof(AquariumGame).GetField("session", Any).GetValue(game);
            followCamera = typeof(AquariumGame).GetField("followCamera", Any).GetValue(game);
            return session != null;
        }
        public static string Begin(int which, bool pictures = true)
        {
            if (!EditorApplication.isPlaying) return "not playing";
            if (!Find()) return "no game";
            Directory.CreateDirectory(Folder);
            lesson = which; lastStep = -2; shotStep = -2; cleared = false; shots = pictures; log.Length = 0; nextUse = 0;
            typeof(AquariumGame).GetMethod("StartTutorial", Any).Invoke(game, new object[] { which });
            AquariumGame.R78QaInput = Drive;
            began = stepBegan = Time.realtimeSinceStartup;
            if (!hooked) { EditorApplication.update += Tick; hooked = true; }
            return "lesson " + which + " started, running=" + game.R78Lesson;
        }
        public static string End()
        {
            AquariumGame.R78QaInput = null;
            if (hooked) { EditorApplication.update -= Tick; hooked = false; }
            return Report();
        }
        public static string Report()
        { return "lesson=" + (game != null ? game.R78Lesson : -1) + " step=" + (game != null ? game.R78StepIndex : -1) + " cleared=" + cleared + " t=" + (Time.realtimeSinceStartup - began).ToString("0.0") + "\n" + log; }
        public static void Shot(string name) { Directory.CreateDirectory(Folder); ScreenCapture.CaptureScreenshot(Path.Combine(Folder, name + ".png")); }
        public static void Unpause() { if (game != null) typeof(AquariumGame).GetField("pause", Any).SetValue(game, false); }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || game == null) { End(); return; }
            Unpause();   // the editor is not focused while a check runs; the game would show its pause menu
            float now = Time.realtimeSinceStartup;
            int step = game.R78Lesson == 0 ? -1 : game.R78StepIndex;
            if (step != lastStep)
            {
                log.Append("  step ").Append(lastStep + 1).Append(" took ").Append((now - stepBegan).ToString("0.0")).Append("s (at ").Append((now - began).ToString("0.0")).Append("s)\n");
                lastStep = step; stepBegan = now; shotAt = now + Mathf.Max(.45f, Think * .8f);
            }
            if (shots && step >= 0 && shotStep != step && now >= shotAt && !game.R78Cleared)
            { shotStep = step; Shot("R78b_L" + lesson + "_step" + (step + 1).ToString("00")); }
            if (game.R78Cleared && !cleared)
            {
                cleared = true; log.Append("  CLEARED at ").Append((now - began).ToString("0.0")).Append("s\n"); shotAt = now + 1.6f; shotStep = 99;
            }
            if (cleared && shotStep == 99 && now >= shotAt) { shotStep = 100; if (shots) Shot("R78b_L" + lesson + "_clear"); }
        }

        private static Point Aim(Point from, Point to) { var d = to - from; return d.Length > .01f ? d.Normalized : new Point(1, 0, 0); }
        private static void Look(Point aim)
        {
            if (followCamera == null) return;
            var type = followCamera.GetType();
            float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg, pitch = -Mathf.Asin(Mathf.Clamp(aim.y, -1, 1)) * Mathf.Rad2Deg;
            var fy = type.GetField("targetYaw", Any); var fp = type.GetField("targetPitch", Any);
            if (fy != null) fy.SetValue(followCamera, yaw); if (fp != null) fp.SetValue(followCamera, Mathf.Clamp(pitch, -60, 60));
        }
        private static SwimInput Go(Swimmer p, Point to, float stop, bool sprint)
        {
            var d = to - p.position; float length = d.Length; var aim = Aim(p.position, to);
            var i = new SwimInput { aimX = aim.x, aimY = aim.y, aimZ = aim.z, sprint = sprint };
            if (length > stop) { i.x = aim.x * Effort; i.y = aim.y * Effort; i.z = aim.z * Effort; }
            Look(aim);
            return i;
        }
        private static Prey Nearest(AquariumModel model, Point from)
        {
            Prey best = null; float bestD = float.MaxValue;
            foreach (var f in model.Fish) { if (f.dead || f.eaten) continue; float d = (f.position - from).Length; if (d < bestD) { bestD = d; best = f; } }
            return best;
        }
        private static bool Ready(float gap) { float now = Time.realtimeSinceStartup; if (now < nextUse) return false; nextUse = now + gap; return true; }

        private static SwimInput Drive(Swimmer p, SwimInput given)
        {
            var model = session.Model; if (model == null || game.R78Lesson == 0 || game.R78Cleared) return new SwimInput { aimX = p.facing.x, aimY = p.facing.y, aimZ = p.facing.z };
            int step = game.R78StepIndex; var bot = model.Player(2); var drains = model.Drains;
            if (Time.realtimeSinceStartup - stepBegan < Think && p.drainTask == DrainTask.None) return new SwimInput { aimX = p.facing.x, aimY = p.facing.y, aimZ = p.facing.z };
            SwimInput i;
            if (game.R78Lesson == 1)
            {
                if (step <= 2) return Go(p, game.R78Target, .2f, step == 2);
                var fish = Nearest(model, p.position);
                if (step == 3 || step == 8)
                {
                    if (fish == null) return Go(p, p.position, 1, false);
                    i = Go(p, fish.position, 2.4f, false);
                    if ((fish.position - p.position).Length < 3.6f && Ready(.6f)) { i.morph = true; i.morphTargetId = fish.id; }
                    return i;
                }
                if (step == 4)
                {
                    if (fish == null) return Go(p, p.position, 1, false);
                    i = Go(p, fish.position, .7f, false);
                    if ((fish.position - p.position).Length < 1.5f && Ready(.3f)) i.action = true;
                    return i;
                }
                i = new SwimInput { aimX = p.facing.x, aimY = p.facing.y, aimZ = p.facing.z };
                if (step == 5 && Ready(.5f)) i.unmorph = true;
                if (step == 7 && Ready(.5f)) i.layEgg = true;
                if (step == 6 && Ready(.5f)) i.ability = true;
                if (step == 9) { i.x = p.facing.x * .3f; i.z = p.facing.z * .3f; if (bot != null) Look(Aim(p.position, bot.position)); }
                return i;
            }
            if (game.R78Lesson == 2)
            {
                var d = drains[0]; var above = d.position + new Point(-.5f, 1.0f, .4f);
                if (step == 0) return Go(p, above, .4f, false);
                if (p.drainTask == DrainTask.Enter) return new SwimInput { aimX = p.facing.x, aimY = p.facing.y, aimZ = p.facing.z };
                i = Go(p, above, .5f, false);
                var aim = Aim(p.position, d.position); i.aimX = aim.x; i.aimY = aim.y; i.aimZ = aim.z; Look(aim);
                if ((above - p.position).Length < 1.0f && p.drainTask == DrainTask.None && Ready(.4f))
                {
                    int part = AquariumModel.DrainHolePart;
                    if (!AquariumDrainLayout.AllScrewsOut(d)) { for (int s = 0; s < d.screws.Length; s++) if (d.screws[s] < 1) { part = s; break; } }
                    else if (!d.isOpen) part = AquariumModel.DrainLidPart;
                    i.interact = true; i.interactDrain = 0; i.interactPart = part;
                }
                return i;
            }
            {
                var d = drains[0];
                if (step == 0) return Go(p, game.R78Target, .2f, false);
                if (step == 1) { i = new SwimInput { aimX = 1, aimY = -.1f, aimZ = 0 }; if (Ready(.5f)) i.feed = true; return i; }
                if (step == 2 || step == 3)
                {
                    if (bot == null || !bot.alive) return new SwimInput { aimX = p.facing.x, aimY = p.facing.y, aimZ = p.facing.z };
                    var stand = bot.position + new Point(-3.2f, step == 3 ? 2.0f : 0, step == 3 ? 1.2f : 0);
                    float range = (bot.position - p.position).Length;
                    i = Go(p, stand, .5f, range > 9);
                    var lead = bot.position + bot.velocity * (range / 20f);
                    var aim = Aim(p.position, lead); i.aimX = aim.x; i.aimY = aim.y; i.aimZ = aim.z; Look(aim);
                    if (range < 6.5f && AquariumModel.ObstacleHit(p.position, bot.position) > 1 && Ready(.4f)) i.action = true;
                    return i;
                }
                var over = AquariumDrainLayout.LidCenter(d) + new Point(-.6f, 1.2f, .5f);
                bool close = (over - p.position).Length < .7f || p.drainTask == DrainTask.Cover;
                i = close ? new SwimInput() : Go(p, over, .3f, false);
                var down = Aim(p.position, AquariumDrainLayout.LidCenter(d)); i.aimX = down.x; i.aimY = down.y; i.aimZ = down.z; Look(down);
                if (step == 4 && close && p.drainTask == DrainTask.None && Ready(.5f)) { i.interact = true; i.interactDrain = 0; i.interactPart = AquariumModel.DrainLidPart; }
                if (step == 5 && close && Ready(.5f)) i.ability = true;
                return i;
            }
        }
    }
}

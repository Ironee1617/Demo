using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>
    /// Visual review of every skill effect in the real game scene. For each effect the hero winds up the cast and
    /// releases the learned skill against three fixture enemies on a level floor, while time advances in fixed steps.
    /// Twelve frames (four wind-up, eight after release) are rendered through the game camera into one contact sheet
    /// per effect, and the peak clip count and effect tick cost are written to a JSON report.
    /// Opt-in only: a player started with -towerVfxReview &lt;folder&gt; [-towerVfxEffects a,b] [-towerVfxTheme name]
    /// [-towerVfxLevel 1-3] [-towerVfxPrime slow|burn] runs it and quits; -towerVfxSuite &lt;name&gt; instead runs one of the
    /// editor-only smoke suites in the player. The level draws the skill at that level (awakening at three); priming leaves a
    /// status on the targets first so reactions show (fire or lightning on the slowed, frost or poison on the burning).
    /// </summary>
    public sealed class TowerVfxReviewDriver : MonoBehaviour
    {
        [Serializable] private sealed class EffectStats { public string effect; public int peakClips; public float tickMs, peakTickMs, castMs, peakRenderMs; public string sheet; }
        [Serializable] private sealed class Report
        {
            public string mode = "towerVfxReview", finishedUtc, theme; public bool success;
            // Full-view 1600x900 render of the scene without effects, GPU work included; peakRenderMs per effect is the same measure.
            public float idleRenderMs;
            public List<EffectStats> effects = new List<EffectStats>(); public List<string> errors = new List<string>();
        }
        public static readonly float[] WindupFrames = { .08f, .2f, .32f, .42f };
        public static readonly float[] ReleaseFrames = { .03f, .1f, .2f, .32f, .5f, .75f, 1f, 1.4f };
        // Lasting skills (orbs, phantoms, mines, banners, vortices, curses, seeds, quakes, discs) play out over a longer span.
        public static readonly float[] LongReleaseFrames = { .03f, .15f, .35f, .6f, 1f, 1.6f, 2.4f, 3.6f };
        private static float[] FramesFor(SkillEffect effect) { return (int)effect >= (int)SkillEffect.Boomerang ? LongReleaseFrames : ReleaseFrames; }
        // Skills that act around the caster face targets close by; the rest keep the usual spread.
        private static float[] TargetsFor(SkillEffect effect)
        {
            return effect == SkillEffect.Orbit || effect == SkillEffect.Decoy || effect == SkillEffect.Mine || effect == SkillEffect.WarBanner || effect == SkillEffect.BladeStorm
                ? new[] { 1.5f, 2.6f, 4.2f } : new[] { 3.6f, 5.6f, 7.6f };
        }
        private static int level = 1; private static string prime;
        private const float Windup = .46f, Step = 1 / 60f;
        private const int FrameWidth = 640, FrameHeight = 360, Columns = 4;
        private static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

        private readonly Report report = new Report();
        private TowerGame game; private TowerVfx vfx; private string folder; private bool quit; private List<SkillEffect> wanted;
        private TowerThemeId? theme;
        private readonly List<TowerGame.Fighter> targets = new List<TowerGame.Fighter>();
        public bool IsFinished { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Enter()
        {
            string[] args = Environment.GetCommandLineArgs();
            // Editor-only suites (Passive, Loot, Hazard, SkillTree) can also run in a player, attached the way the editor script attaches them.
            string suite = Arg(args, "-towerVfxSuite");
            if (suite != null) { new GameObject("Tower suite host").AddComponent<TowerVfxReviewDriver>().StartCoroutine(LaunchSuite(suite, Arg(args, "-towerVfxSuitePrefix"), Arg(args, "-towerVfxSuiteReport"))); return; }
            string output = Arg(args, "-towerVfxReview"); if (output == null) return;
            int parsed; if (int.TryParse(Arg(args, "-towerVfxLevel") ?? "1", out parsed)) level = Mathf.Clamp(parsed, 1, 3);
            prime = Arg(args, "-towerVfxPrime");
            new GameObject("Tower VFX review").AddComponent<TowerVfxReviewDriver>().StartCoroutine(Launch(output, Arg(args, "-towerVfxEffects"), Arg(args, "-towerVfxTheme")));
        }
        private static string Arg(string[] args, string key) { int at = Array.IndexOf(args, key); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
        private static IEnumerator Launch(string output, string effects, string themeName)
        {
            TowerGame tower = null;
            for (int frame = 0; frame < 300 && tower == null; frame++) { tower = FindFirstObjectByType<TowerGame>(); if (tower == null) yield return null; }
            if (tower == null) { Application.Quit(1); yield break; }
            yield return null;
            FindFirstObjectByType<TowerVfxReviewDriver>().Initialize(tower, Path.GetFullPath(output), effects, themeName, true);
        }
        private static IEnumerator LaunchSuite(string suite, string prefix, string reportPath)
        {
            TowerGame tower = null;
            for (int frame = 0; frame < 300 && tower == null; frame++) { tower = FindFirstObjectByType<TowerGame>(); if (tower == null) yield return null; }
            var type = Type.GetType("PrototypeLab.Roguelike.Tower" + suite + "SmokeDriver");
            if (tower == null || type == null || prefix == null || reportPath == null) { Application.Quit(1); yield break; }
            yield return null;
            var driver = tower.gameObject.AddComponent(type);
            type.GetMethod("Initialize").Invoke(driver, new object[] { tower, tower.GetComponent<TowerSmokeDriver>(), Path.GetFullPath(prefix), Path.GetFullPath(reportPath), true });
        }

        public void Initialize(TowerGame tower, string outputFolder, string effects = null, string themeName = null, bool quitOnComplete = false)
        {
            if (game != null) throw new InvalidOperationException("Create a new review driver for each run.");
            if (!Path.IsPathRooted(outputFolder)) throw new ArgumentException("The review folder must be absolute.");
            game = tower; folder = outputFolder; quit = quitOnComplete;
            wanted = new List<SkillEffect>();
            if (!string.IsNullOrEmpty(effects)) foreach (var name in effects.Split(',')) wanted.Add((SkillEffect)Enum.Parse(typeof(SkillEffect), name.Trim(), true));
            else wanted.AddRange((SkillEffect[])Enum.GetValues(typeof(SkillEffect)));
            if (!string.IsNullOrEmpty(themeName)) theme = (TowerThemeId)Enum.Parse(typeof(TowerThemeId), themeName, true);
            Application.logMessageReceived += RecordLog;
            StartCoroutine(Run());
        }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }
        private void RecordLog(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(condition + "\n" + trace); }

        private object Call(string method, params object[] args) { return typeof(TowerGame).GetMethod(method, Hidden).Invoke(game, args); }
        private IList Shots { get { return (IList)typeof(TowerGame).GetField("shots", Hidden).GetValue(game); } }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(folder);
            game.SmokeControlled = true; game.SuppressProgressPersistence = true; game.enabled = true;
            yield return null;
            // A climb walks and fights before its first skill, long enough for the effect code to finish compiling.
            for (float until = Time.realtimeSinceStartup + 10; !TowerVfx.CodeWarm && Time.realtimeSinceStartup < until;) yield return null;
            Exception failure = null;
            Setup(); yield return null;
            report.idleRenderMs = RenderCost();
            foreach (var effect in wanted)
            {
                IEnumerator body = Review(effect);
                while (true)
                {
                    object current = null;
                    try { if (!body.MoveNext()) break; current = body.Current; }
                    catch (Exception ex) { failure = ex; break; }
                    yield return current;
                }
                if (failure != null) { report.errors.Add(effect + ": " + failure); break; }
            }
            report.success = failure == null && report.errors.Count == 0;
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.theme = game.run != null ? game.run.Theme.Id.ToString() : "";
            File.WriteAllText(Path.Combine(folder, "vfx-review.json"), JsonUtility.ToJson(report, true));
            IsFinished = true; game.enabled = true;
            if (quit) Application.Quit();
        }

        private void Setup() { Setup(SkillEffect.Fireball); }
        private void Setup(SkillEffect effect)
        {
            game.enabled = true;
            if (game.hero != null && game.hero.root) game.hero.root.SetActive(false);
            foreach (var actor in game.allies) if (actor.root) actor.root.SetActive(false);
            foreach (var actor in game.enemies) if (actor.root) actor.root.SetActive(false);
            game.NewRun(74); game.StartClimb(); game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = false;
            foreach (var enemy in game.enemies) if (enemy.root) { enemy.root.SetActive(false); Destroy(enemy.root); }
            game.enemies.Clear();
            foreach (object shot in Shots) { var root = (GameObject)shot.GetType().GetField("root").GetValue(shot); if (root) Destroy(root); }
            Shots.Clear(); Call("ResetSkills");
            var world = (TowerWorld)typeof(TowerGame).GetField("world", Hidden).GetValue(game); var layout = world.CurrentLayout;
            var ground = new TowerSurface(new[] { TowerTerrainGenerator.TerrainLeft, TowerTerrainGenerator.TerrainRight }, new[] { TowerWorld.Ground, TowerWorld.Ground });
            world.SetTheme(theme ?? game.run.Theme.Id, game.run.Floor, game.seed, new TowerFloorLayout(layout.Id, layout.Archetype, layout.Floor, ground, new List<TowerPlatform>(), layout.ExitX));
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.hero.facing = 1; game.hero.hp = game.PlayerMaxHealth * .5f;
            targets.Clear();
            foreach (float x in TargetsFor(effect))
            {
                var enemy = (TowerGame.Fighter)Call("Spawn", "VFX review target", 0, true, new Vector2(x, TowerWorld.Ground));
                game.enemies.Add(enemy); Call("ConfigureEnemy", enemy, 0, 0);
                enemy.maxHp = enemy.hp = 1000000; enemy.cooldown = enemy.stunTime = 999; enemy.facing = -1; targets.Add(enemy);
            }
            game.view.transform.position = new Vector3(3.2f, TowerWorld.Ground + 3, -10);
            vfx = (TowerVfx)typeof(TowerGame).GetField("vfx", Hidden).GetValue(game);
            // The review steps the simulation itself, one fixed step per rendered frame (so destroyed shots are gone);
            // Update must not move anyone in between.
            game.enabled = false;
        }
        private static void Place(TowerGame.Fighter actor, Vector2 position)
        { actor.pos = position; actor.velocityY = 0; actor.grounded = true; actor.root.transform.position = position; }

        private IEnumerator Review(SkillEffect effect)
        {
            Setup(effect);
            if (prime == "slow") foreach (var t in targets) t.slowTime = 999;
            else if (prime == "burn") foreach (var t in targets) Call("ApplyBurn", t, game.run.Player, 1f, 999f);
            yield return null; // let the backdrop planes follow the camera
            AugmentDefinition definition = null;
            foreach (var candidate in TowerRun.Augments) if (candidate.Kind == RewardKind.Skill && candidate.Effect == effect) { definition = candidate; break; }
            if (definition == null) throw new InvalidOperationException("No skill card uses " + effect);
            var skill = new SkillState(definition, game.run.Player, level);
            var releaseFrames = FramesFor(effect);
            var color = (Color)Call("SkillColor", effect);
            var stats = new EffectStats { effect = effect.ToString() };
            var sheet = new Texture2D(FrameWidth * Columns, FrameHeight * 3, TextureFormat.RGBA32, false);
            var frame = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGBA32, false);
            int index = 0; float clock = 0; var watch = new Stopwatch();

            var hero = game.hero; var target = targets[0];
            hero.facing = 1; hero.art.PlayCast(effect, Windup);
            watch.Restart();
            vfx.Grade = level;
            vfx.Cast(effect, hero.pos + Vector2.up, Mathf.Min(1.3f, skill.Radius * .3f), color, Windup, hero.facing, hero.root.transform, target.pos + Vector2.up * .7f);
            vfx.Grade = 1;
            stats.castMs += (float)watch.Elapsed.TotalMilliseconds;
            foreach (float at in WindupFrames)
            {
                while (clock + Step <= at) { Advance(stats, watch, false); clock += Step; yield return null; }
                stats.peakRenderMs = Mathf.Max(stats.peakRenderMs, RenderCost());
                Render(sheet, frame, index++, at / (Windup + releaseFrames[releaseFrames.Length - 1]), new Color(1, .6f, .2f));
            }
            while (clock + Step <= Windup) { Advance(stats, watch, false); clock += Step; yield return null; }
            watch.Restart();
            Call("CastSkill", game.run.Player, skill, hero, target);
            stats.castMs += (float)watch.Elapsed.TotalMilliseconds;
            float released = clock;
            foreach (float at in releaseFrames)
            {
                while (clock + Step <= released + at) { Advance(stats, watch, true); clock += Step; yield return null; }
                stats.peakRenderMs = Mathf.Max(stats.peakRenderMs, RenderCost());
                Render(sheet, frame, index++, (Windup + at) / (Windup + releaseFrames[releaseFrames.Length - 1]), new Color(.3f, .9f, 1));
            }
            stats.sheet = Path.Combine(folder, effect + ".png");
            File.WriteAllBytes(stats.sheet, sheet.EncodeToPNG());
            Destroy(sheet); Destroy(frame);
            report.effects.Add(stats);
            vfx.Clear();
        }

        private void Advance(EffectStats stats, Stopwatch watch, bool released)
        {
            if (released) { Call("TickSkillAreas", Step); Call("TickSkillStatuses", Step); Call("TickSkillMoves", Step); Call("TickShots", Step); }
            game.hero.art.SetPose(false, false, false, game.hero.facing, Step);
            foreach (var enemy in targets) enemy.art.SetPose(false, false, false, enemy.facing, Step);
            watch.Restart(); vfx.Tick(Step); float ms = (float)watch.Elapsed.TotalMilliseconds;
            stats.tickMs += ms; stats.peakTickMs = Mathf.Max(stats.peakTickMs, ms); stats.peakClips = Mathf.Max(stats.peakClips, vfx.ActiveCount);
        }

        // Mean cost of one full-view render at the game's 1600x900, waiting for the GPU to finish.
        private float RenderCost()
        {
            const int Repeats = 8;
            var camera = game.view; var previousTarget = camera.targetTexture; float previousSize = camera.orthographicSize;
            var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
            var probe = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            camera.targetTexture = target; camera.orthographicSize = 5.8f; camera.aspect = 1600 / 900f;
            var previousActive = RenderTexture.active; RenderTexture.active = target;
            camera.Render(); probe.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < Repeats; i++) camera.Render();
            probe.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
            float ms = (float)watch.Elapsed.TotalMilliseconds / Repeats;
            RenderTexture.active = previousActive;
            camera.targetTexture = previousTarget; camera.orthographicSize = previousSize; camera.ResetAspect();
            RenderTexture.ReleaseTemporary(target); Destroy(probe);
            return ms;
        }

        private void Render(Texture2D sheet, Texture2D frame, int index, float progress, Color bar)
        {
            var camera = game.view; var previousTarget = camera.targetTexture; float previousSize = camera.orthographicSize;
            var target = RenderTexture.GetTemporary(FrameWidth, FrameHeight, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target; camera.orthographicSize = 5.8f; camera.aspect = FrameWidth / (float)FrameHeight;
            camera.Render();
            var previousActive = RenderTexture.active; RenderTexture.active = target;
            frame.ReadPixels(new Rect(0, 0, FrameWidth, FrameHeight), 0, 0); frame.Apply();
            RenderTexture.active = previousActive;
            camera.targetTexture = previousTarget; camera.orthographicSize = previousSize; camera.ResetAspect();
            RenderTexture.ReleaseTemporary(target);
            // A thin bar along the top edge shows where in the timeline the frame sits (orange: wind-up, cyan: after release).
            int length = Mathf.Clamp(Mathf.RoundToInt(progress * FrameWidth), 2, FrameWidth);
            for (int x = 0; x < length; x++) for (int y = FrameHeight - 4; y < FrameHeight; y++) frame.SetPixel(x, y, bar);
            for (int x = 0; x < FrameWidth; x++) { frame.SetPixel(x, 0, Color.black); frame.SetPixel(x, FrameHeight - 5, Color.black); }
            for (int y = 0; y < FrameHeight; y++) { frame.SetPixel(0, y, Color.black); frame.SetPixel(FrameWidth - 1, y, Color.black); }
            int column = index % Columns, row = 2 - index / Columns;
            sheet.SetPixels(column * FrameWidth, row * FrameHeight, FrameWidth, FrameHeight, frame.GetPixels());
            sheet.Apply(false);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>
    /// Visual review of mouse aim in the real game scene on a level floor, stepped at a fixed rate: the hero's guard toward
    /// eight aims with the reticle, three-swing combos aimed several ways (each frame drawn without and with the effects,
    /// one row pair per swing), and an aimed dash up and across. Opt-in only: a player started with
    /// -towerAimReview &lt;folder&gt; [-towerAimTheme name] writes the sheets and aim-review.json, then quits.
    /// </summary>
    public sealed class TowerAimReviewDriver : MonoBehaviour
    {
        [Serializable] private sealed class Report { public string mode = "towerAimReview", finishedUtc, theme; public bool success; public List<string> sheets = new List<string>(), checks = new List<string>(), errors = new List<string>(); }
        private const float Step = 1 / 60f, Zoom = 3f;
        private const int FrameWidth = 480, FrameHeight = 270;
        private static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        // Swing frames, as fractions of the swing: wind-up, late wind-up, just before contact, contact, follow-through, settled.
        private static readonly float[] SwingMarks = { .14f, .26f, .37f, .45f, .6f, .92f };

        private readonly Report report = new Report();
        private TowerGame game; private TowerVfx vfx; private string folder; private TowerThemeId? theme; private GameObject reticle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Enter()
        {
            string[] args = Environment.GetCommandLineArgs();
            string output = Arg(args, "-towerAimReview"); if (output == null) return;
            new GameObject("Tower aim review").AddComponent<TowerAimReviewDriver>().StartCoroutine(Launch(Path.GetFullPath(output), Arg(args, "-towerAimTheme")));
        }
        private static string Arg(string[] args, string key) { int at = Array.IndexOf(args, key); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
        private static IEnumerator Launch(string output, string themeName)
        {
            TowerGame tower = null;
            for (int frame = 0; frame < 300 && tower == null; frame++) { tower = FindFirstObjectByType<TowerGame>(); if (tower == null) yield return null; }
            if (tower == null) { Application.Quit(1); yield break; }
            yield return null;
            var driver = FindFirstObjectByType<TowerAimReviewDriver>();
            driver.game = tower; driver.folder = output;
            if (!string.IsNullOrEmpty(themeName)) driver.theme = (TowerThemeId)Enum.Parse(typeof(TowerThemeId), themeName, true);
            driver.StartCoroutine(driver.Run());
        }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }
        private void RecordLog(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(condition + "\n" + trace); }
        private object Call(string method, params object[] args) { return typeof(TowerGame).GetMethod(method, Hidden).Invoke(game, args); }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(folder);
            Application.logMessageReceived += RecordLog;
            game.SmokeControlled = true; game.SuppressProgressPersistence = true; game.enabled = true;
            yield return null;
            for (float until = Time.realtimeSinceStartup + 10; !TowerVfx.CodeWarm && Time.realtimeSinceStartup < until;) yield return null;
            var reviews = new List<KeyValuePair<string, IEnumerator>>
            {
                new KeyValuePair<string, IEnumerator>("hold", ReviewHold()),
                new KeyValuePair<string, IEnumerator>("swing-ahead", ReviewSwings("swing-ahead", 0, false)),
                new KeyValuePair<string, IEnumerator>("swing-up", ReviewSwings("swing-up", 62, false)),
                new KeyValuePair<string, IEnumerator>("swing-down", ReviewSwings("swing-down", -32, false)),
                new KeyValuePair<string, IEnumerator>("swing-back-up", ReviewSwings("swing-back-up", 150, false)),
                new KeyValuePair<string, IEnumerator>("swing-air-down", ReviewSwings("swing-air-down", -120, true)),
                new KeyValuePair<string, IEnumerator>("dash", ReviewDash()),
                new KeyValuePair<string, IEnumerator>("checks", Checks()),
                new KeyValuePair<string, IEnumerator>("film", Film()),
            };
            Exception failure = null;
            foreach (var review in reviews)
            {
                var body = review.Value;
                while (true)
                {
                    object current = null;
                    try { if (!body.MoveNext()) break; current = body.Current; }
                    catch (Exception ex) { failure = ex; break; }
                    yield return current;
                }
                if (failure != null) { report.errors.Add(review.Key + ": " + failure); break; }
            }
            game.AimOverride = null;
            report.success = failure == null && report.errors.Count == 0;
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.theme = game.run != null ? game.run.Theme.Id.ToString() : "";
            File.WriteAllText(Path.Combine(folder, "aim-review.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.success ? 0 : 1);
        }

        private void Setup()
        {
            game.enabled = true;
            if (game.hero != null && game.hero.root) game.hero.root.SetActive(false);
            foreach (var actor in game.allies) if (actor.root) actor.root.SetActive(false);
            game.NewRun(74); game.StartClimb(); game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = false;
            foreach (var enemy in game.enemies) if (enemy.root) { enemy.root.SetActive(false); Destroy(enemy.root); }
            game.enemies.Clear();
            foreach (var actor in game.allies) if (actor.root) actor.root.SetActive(false);
            var world = (TowerWorld)typeof(TowerGame).GetField("world", Hidden).GetValue(game); var layout = world.CurrentLayout;
            var ground = new TowerSurface(new[] { TowerTerrainGenerator.TerrainLeft, TowerTerrainGenerator.TerrainRight }, new[] { TowerWorld.Ground, TowerWorld.Ground });
            world.SetTheme(theme ?? game.run.Theme.Id, game.run.Floor, game.seed, new TowerFloorLayout(layout.Id, layout.Archetype, layout.Floor, ground, new List<TowerPlatform>(), layout.ExitX));
            var hero = game.hero; hero.pos = new Vector2(0, TowerWorld.Ground); hero.velocityY = 0; hero.grounded = true; hero.support = -1; hero.lastX = 0;
            hero.root.transform.position = hero.pos; hero.facing = 1;
            game.view.transform.position = new Vector3(0, TowerWorld.Ground + 1.2f, -10);
            vfx = (TowerVfx)typeof(TowerGame).GetField("vfx", Hidden).GetValue(game);
            if (!reticle)
            {
                reticle = new GameObject("Review reticle"); var renderer = reticle.AddComponent<SpriteRenderer>();
                renderer.sprite = TowerGame.BuildReticle(); renderer.sortingOrder = 900;
            }
            // The review steps the hero itself, one fixed step per call; Update must not move anyone in between.
            game.enabled = false;
        }
        private static Vector2 Direction(float degrees) { float r = degrees * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), Mathf.Sin(r)); }
        private Vector2 Chest { get { return game.hero.pos + Vector2.up * TowerGame.ChestHeight; } }
        private void Aim(float degrees, float distance)
        {
            game.AimOverride = Chest + Direction(degrees) * distance;
            reticle.transform.position = (Vector3)game.AimOverride.Value;
        }
        // One fixed step of the hero: aim, the swing in progress, the pose and the effects.
        private void Advance(bool faceAim)
        {
            var hero = game.hero;
            Call("TickAim"); if (faceAim && game.attackTimer <= 0) Call("FaceAim");
            Call("TickPlayerAttack", Step);
            hero.art.SetLocomotion(hero.grounded, hero.velocityY, game.dashTime > 0, 0, 0);
            hero.art.SetPose(false, game.attackTimer > 0, false, hero.facing, Step);
            vfx.Tick(Step);
        }

        // The guard toward eight aims with the reticle in place.
        private IEnumerator ReviewHold()
        {
            Setup(); yield return null;
            float[] angles = { 0, 35, 70, 105, -35, 180, 140, -150 };
            var sheet = new Texture2D(FrameWidth * 4, FrameHeight * 2, TextureFormat.RGBA32, false);
            var frame = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGBA32, false);
            for (int i = 0; i < angles.Length; i++)
            {
                Aim(angles[i], 2.4f);
                for (int s = 0; s < 40; s++) Advance(true);
                yield return null;
                Render(sheet, frame, 4, 2, i, true);
            }
            Save(sheet, frame, "hold");
        }

        // Three swings in a row toward one aim; each frame without the effects, then with them.
        private IEnumerator ReviewSwings(string name, float angle, bool airborne)
        {
            Setup();
            var hero = game.hero;
            if (airborne) { hero.pos.y += 2.4f; hero.grounded = false; hero.root.transform.position = hero.pos; game.view.transform.position += Vector3.up * 1.6f; }
            yield return null;
            Aim(angle, 2.6f);
            for (int s = 0; s < 30; s++) Advance(true);
            var sheet = new Texture2D(FrameWidth * SwingMarks.Length, FrameHeight * 6, TextureFormat.RGBA32, false);
            var frame = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGBA32, false);
            for (int swing = 0; swing < 3; swing++)
            {
                Call("StartPlayerAttack");
                float duration = game.LastPlayerAttackDuration, clock = 0;
                for (int m = 0; m < SwingMarks.Length; m++)
                {
                    while (clock + Step <= SwingMarks[m] * duration) { Advance(false); clock += Step; }
                    yield return null;
                    Render(sheet, frame, SwingMarks.Length, 6, swing * 2 * SwingMarks.Length + m, false);
                    Render(sheet, frame, SwingMarks.Length, 6, (swing * 2 + 1) * SwingMarks.Length + m, true);
                }
                while (game.attackTimer > 0) Advance(false);
                for (int s = 0; s < 4; s++) Advance(false);
            }
            Save(sheet, frame, name);
            vfx.Clear();
        }

        // A right-button dash up and to the right from the floor, then the fall.
        private IEnumerator ReviewDash()
        {
            Setup(); yield return null;
            var hero = game.hero;
            Aim(48, 3f);
            for (int s = 0; s < 20; s++) Advance(true);
            var sheet = new Texture2D(FrameWidth * 4, FrameHeight * 2, TextureFormat.RGBA32, false);
            var frame = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGBA32, false);
            game.view.transform.position = new Vector3(2, TowerWorld.Ground + 2.6f, -10);
            Call("StartDash", 0f, true);
            float[] marks = { .02f, .08f, .14f, .2f, .3f, .42f, .56f, .72f };
            float clock = 0;
            for (int m = 0; m < marks.Length; m++)
            {
                while (clock + Step <= marks[m])
                {
                    if (game.dashTime > 0) Call("TickDash", Step);
                    Call("StepGravity", hero, Step, false); hero.root.transform.position = hero.pos;
                    Advance(false); clock += Step;
                }
                yield return null;
                Render(sheet, frame, 4, 2, m, true);
            }
            Save(sheet, frame, "dash");
            vfx.Clear();
        }

        private void Check(bool passed, string name)
        { report.checks.Add((passed ? "pass  " : "FAIL  ") + name); if (!passed) report.errors.Add("check failed: " + name); }
        private TowerGame.Fighter Target(Vector2 chestOffset)
        {
            var at = Chest + chestOffset - Vector2.up * .85f;
            var enemy = (TowerGame.Fighter)Call("Spawn", "Aim check target", 0, true, at);
            game.enemies.Add(enemy); Call("ConfigureEnemy", enemy, 0, 0);
            enemy.pos = at; enemy.root.transform.position = at; enemy.maxHp = enemy.hp = 1000000; enemy.cooldown = enemy.stunTime = 999; enemy.facing = -1;
            return enemy;
        }
        private void Swing(float degrees)
        {
            Aim(degrees, 2.6f);
            for (int s = 0; s < 6; s++) Advance(true);
            Call("StartPlayerAttack");
            while (game.attackTimer > 0) Advance(false);
            for (int s = 0; s < 70; s++) Advance(true);
        }
        // Gameplay facts behind the sheets: an aimed cut reaches where the old flat cut could not and only where it points,
        // a dash aimed upward leaves the floor, and one aimed at the floor runs flat.
        private IEnumerator Checks()
        {
            Setup(); yield return null;
            var above = Target(new Vector2(.3f, 2.3f));
            Swing(0); Check(above.hp >= above.maxHp, "a cut aimed ahead misses an enemy 2.3 above the chest");
            Swing(80); Check(above.hp < above.maxHp, "a cut aimed up hits an enemy 2.3 above the chest");
            game.enemies.Remove(above); Destroy(above.root);
            var behind = Target(new Vector2(-1.4f, 0));
            Swing(0); Check(behind.hp >= behind.maxHp, "a cut aimed ahead misses an enemy behind");
            Swing(180); Check(behind.hp < behind.maxHp && game.hero.facing < 0, "a cut aimed behind turns the hero and hits it");
            game.enemies.Remove(behind); Destroy(behind.root);
            game.hero.pos.y += 2.6f; game.hero.grounded = false; game.hero.root.transform.position = game.hero.pos;
            var below = Target(new Vector2(1f, -2f));
            Swing(0); Check(below.hp >= below.maxHp, "a cut aimed ahead from the air misses an enemy 2 below the chest");
            Swing(-62); Check(below.hp < below.maxHp, "a cut aimed down from the air hits an enemy below");
            Check(Mathf.Approximately(game.hero.velocityY, TowerGame.PogoVelocities[0]) && game.PogoBounces == 1, "the down-cut that lands bounces the hero up at " + game.hero.velocityY.ToString("0.0") + " (9)");
            game.enemies.Remove(below); Destroy(below.root);
            yield return null;

            Setup(); yield return null;
            var hero = game.hero; float startY = hero.pos.y, startX = hero.pos.x, peak = startY;
            Aim(70, 3); Advance(true);
            Call("StartDash", 0f, true);
            for (int s = 0; s < 60; s++) { if (game.dashTime > 0) Call("TickDash", Step); Call("StepGravity", hero, Step, false); peak = Mathf.Max(peak, hero.pos.y); }
            Check(peak - startY > 2.4f && peak - startY < 4.5f, "a dash aimed up rises " + (peak - startY).ToString("0.00") + " (2.4 to 4.5)");
            Check(hero.grounded, "the hero lands again after the rising dash");
            Setup(); yield return null;
            hero = game.hero; startY = hero.pos.y; startX = hero.pos.x;
            Aim(-35, 3); Advance(true);
            Call("StartDash", 0f, true);
            bool stayed = true;
            for (int s = 0; s < 20; s++) { if (game.dashTime > 0) Call("TickDash", Step); Call("StepGravity", hero, Step, false); stayed &= hero.grounded && Mathf.Abs(hero.pos.y - startY) < .01f; }
            Check(stayed && hero.pos.x - startX > 3, "a dash aimed at the floor runs flat " + (hero.pos.x - startX).ToString("0.00") + " ahead");
            Setup(); yield return null;
            hero = game.hero; game.AimOverride = null; startX = hero.pos.x;
            Call("StartDash", 0f, false);
            for (int s = 0; s < 20; s++) { if (game.dashTime > 0) Call("TickDash", Step); Call("StepGravity", hero, Step, false); }
            Check(hero.grounded && Mathf.Abs(hero.pos.x - startX - 19 * .18f) < .2f, "the Shift dodge still runs " + (hero.pos.x - startX).ToString("0.00") + " along the facing");
        }

        // A short scripted bout at 30 frames a second (film/frame-###.png): the aim circles and the hero turns with it, a
        // combo ahead, two cuts behind and below, a dash up and across, and a cut down from the air.
        private IEnumerator Film()
        {
            Setup(); yield return null;
            var hero = game.hero; string films = Path.Combine(folder, "film"); Directory.CreateDirectory(films); reticle.SetActive(true);
            var shot = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGBA32, false);
            var attacks = new[] { .9f, 1.3f, 1.7f, 2.45f, 2.85f, 3.62f };
            int next = 0, step = 0, saved = 0; bool dashed = false;
            for (float clock = 0; clock < 4.7f; clock += Step, step++)
            {
                float angle = clock < .9f ? Mathf.Lerp(0, 160, Mathf.SmoothStep(0, 1, clock / .5f)) - Mathf.Lerp(0, 140, Mathf.SmoothStep(0, 1, (clock - .5f) / .4f))
                    : clock < 2.3f ? 20 : clock < 3.3f ? -160 : clock < 3.55f ? 50 : -45;
                Aim(angle, 2.6f);
                if (!dashed && clock >= 3.35f) { dashed = true; Call("TickAim"); Call("StartDash", 0f, true); }
                if (next < attacks.Length && clock >= attacks[next] && game.attackTimer <= 0 && game.dashTime <= 0) { next++; Call("TickAim"); if (game.attackTimer <= 0) Call("FaceAim"); Call("StartPlayerAttack"); }
                if (game.dashTime > 0) Call("TickDash", Step);
                Call("StepGravity", hero, Step, false); hero.root.transform.position = hero.pos;
                Advance(game.dashTime <= 0);
                game.view.transform.position = new Vector3(hero.pos.x * .6f, TowerWorld.Ground + 1.4f + Mathf.Max(0, hero.pos.y - TowerWorld.Ground - 1) * .6f, -10);
                reticle.transform.position = (Vector3)game.AimOverride.Value;
                if (step % 2 != 0) continue;
                yield return null;
                var camera = game.view; var target = RenderTexture.GetTemporary(FrameWidth, FrameHeight, 24, RenderTextureFormat.ARGB32);
                float size = camera.orthographicSize; camera.targetTexture = target; camera.orthographicSize = Zoom; camera.aspect = FrameWidth / (float)FrameHeight; camera.Render();
                var active = RenderTexture.active; RenderTexture.active = target; shot.ReadPixels(new Rect(0, 0, FrameWidth, FrameHeight), 0, 0); shot.Apply();
                RenderTexture.active = active; camera.targetTexture = null; camera.orthographicSize = size; camera.ResetAspect(); RenderTexture.ReleaseTemporary(target);
                File.WriteAllBytes(Path.Combine(films, "frame-" + (saved++).ToString("000") + ".png"), shot.EncodeToPNG());
            }
            Destroy(shot); report.sheets.Add(films);
            vfx.Clear();
        }

        private void Render(Texture2D sheet, Texture2D frame, int columns, int rows, int index, bool effects)
        {
            var camera = game.view; var previousTarget = camera.targetTexture; float previousSize = camera.orthographicSize;
            if (!effects) vfx.gameObject.SetActive(false);
            reticle.SetActive(effects);
            var target = RenderTexture.GetTemporary(FrameWidth, FrameHeight, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target; camera.orthographicSize = Zoom; camera.aspect = FrameWidth / (float)FrameHeight;
            camera.Render();
            var previousActive = RenderTexture.active; RenderTexture.active = target;
            frame.ReadPixels(new Rect(0, 0, FrameWidth, FrameHeight), 0, 0); frame.Apply();
            RenderTexture.active = previousActive;
            camera.targetTexture = previousTarget; camera.orthographicSize = previousSize; camera.ResetAspect();
            RenderTexture.ReleaseTemporary(target);
            vfx.gameObject.SetActive(true);
            for (int x = 0; x < FrameWidth; x++) { frame.SetPixel(x, 0, Color.black); frame.SetPixel(x, FrameHeight - 1, Color.black); }
            for (int y = 0; y < FrameHeight; y++) { frame.SetPixel(0, y, Color.black); frame.SetPixel(FrameWidth - 1, y, Color.black); }
            int column = index % columns, row = rows - 1 - index / columns;
            sheet.SetPixels(column * FrameWidth, row * FrameHeight, FrameWidth, FrameHeight, frame.GetPixels());
        }
        private void Save(Texture2D sheet, Texture2D frame, string name)
        {
            sheet.Apply(false);
            string path = Path.Combine(folder, name + ".png");
            File.WriteAllBytes(path, sheet.EncodeToPNG()); report.sheets.Add(path);
            Destroy(sheet); Destroy(frame);
        }
    }
}

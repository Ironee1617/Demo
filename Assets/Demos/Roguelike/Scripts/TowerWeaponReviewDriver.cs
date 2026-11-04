using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>
    /// Visual review of the gun and the orb in the real game scene on a level floor, stepped at a fixed rate: the pistol arm
    /// toward eight aims, aiming while running and in the air, two aimed volleys frame by frame (each frame drawn with the
    /// effects), the orb picking and shooting enemies, gameplay checks and a short film. Opt-in only: a player started with
    /// -towerWeaponReview &lt;folder&gt; [-towerWeaponTheme name] writes the sheets and weapon-review.json, then quits.
    /// </summary>
    public sealed class TowerWeaponReviewDriver : MonoBehaviour
    {
        [Serializable] private sealed class Report { public string mode = "towerWeaponReview", finishedUtc, theme; public bool success; public List<string> sheets = new List<string>(), checks = new List<string>(), errors = new List<string>(); }
        private const float Step = 1 / 60f, Zoom = 3f;
        private const int FrameWidth = 480, FrameHeight = 270;
        private static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

        private readonly Report report = new Report();
        private TowerGame game; private TowerVfx vfx; private string folder; private TowerThemeId? theme; private GameObject reticle;
        private bool firing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Enter()
        {
            string[] args = Environment.GetCommandLineArgs();
            string output = Arg(args, "-towerWeaponReview"); if (output == null) return;
            new GameObject("Tower weapon review").AddComponent<TowerWeaponReviewDriver>().StartCoroutine(Launch(Path.GetFullPath(output), Arg(args, "-towerWeaponTheme")));
        }
        private static string Arg(string[] args, string key) { int at = Array.IndexOf(args, key); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
        private static IEnumerator Launch(string output, string themeName)
        {
            TowerGame tower = null;
            for (int frame = 0; frame < 300 && tower == null; frame++) { tower = FindFirstObjectByType<TowerGame>(); if (tower == null) yield return null; }
            if (tower == null) { Application.Quit(1); yield break; }
            yield return null;
            var driver = FindFirstObjectByType<TowerWeaponReviewDriver>();
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
                new KeyValuePair<string, IEnumerator>("gun-hold", ReviewHold()),
                new KeyValuePair<string, IEnumerator>("gun-move", ReviewMove()),
                new KeyValuePair<string, IEnumerator>("gun-fire", ReviewFire()),
                new KeyValuePair<string, IEnumerator>("orb", ReviewOrb()),
                new KeyValuePair<string, IEnumerator>("checks", Checks()),
                new KeyValuePair<string, IEnumerator>("film", Film()),
                new KeyValuePair<string, IEnumerator>("practice", ReviewPractice()),
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
            game.AimOverride = null; game.AttackModeOverride = null;
            report.success = failure == null && report.errors.Count == 0;
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.theme = game.run != null ? game.run.Theme.Id.ToString() : "";
            File.WriteAllText(Path.Combine(folder, "weapon-review.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.success ? 0 : 1);
        }

        private void Setup(TowerAttackMode mode)
        {
            game.enabled = true; firing = false;
            if (game.hero != null && game.hero.root) game.hero.root.SetActive(false);
            foreach (var actor in game.allies) if (actor.root) actor.root.SetActive(false);
            game.AttackModeOverride = mode;
            game.NewRun(74); game.StartClimb(); game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = false;
            foreach (var enemy in game.enemies) if (enemy.root) { enemy.root.SetActive(false); Destroy(enemy.root); }
            game.enemies.Clear();
            foreach (var actor in game.allies) if (actor.root) actor.root.SetActive(false);
            foreach (var cache in game.caches) if (cache.root) Destroy(cache.root);
            game.caches.Clear();
            var world = (TowerWorld)typeof(TowerGame).GetField("world", Hidden).GetValue(game); var layout = world.CurrentLayout;
            var ground = new TowerSurface(new[] { TowerTerrainGenerator.TerrainLeft, TowerTerrainGenerator.TerrainRight }, new[] { TowerWorld.Ground, TowerWorld.Ground });
            world.SetTheme(theme ?? game.run.Theme.Id, game.run.Floor, game.seed, new TowerFloorLayout(layout.Id, layout.Archetype, layout.Floor, ground, new List<TowerPlatform>(), layout.ExitX));
            var hero = game.hero; hero.pos = new Vector2(0, TowerWorld.Ground); hero.velocityY = 0; hero.grounded = true; hero.support = -1; hero.lastX = 0;
            hero.root.transform.position = hero.pos; hero.facing = 1;
            Call("SyncAttackMode", false);
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
        private void Aim(float degrees, float distance) { AimAt(Chest + Direction(degrees) * distance); }
        private void AimAt(Vector2 point) { game.AimOverride = point; reticle.transform.position = (Vector3)point; }
        // One fixed step in Update's order: aim (no turning mid-dash), a dash under way, a shot when the trigger is held and
        // the gun is ready (dashing or not), the pose, shots, the orb and the effects.
        private void Advance(bool faceAim, float speed = 0)
        {
            var hero = game.hero;
            game.elapsed += Step; game.slashCooldown = Mathf.Max(0, game.slashCooldown - Step);
            Call("TickAim"); if (faceAim && game.dashTime <= 0) Call("FaceAim");
            if (game.dashTime > 0) { Call("TickDash", Step); hero.root.transform.position = hero.pos; }
            if (firing && (bool)Call("GunTriggerReady", true)) Call("FireGun");
            hero.art.SetLocomotion(hero.grounded, hero.velocityY, game.dashTime > 0, Mathf.Abs(speed), speed);
            hero.art.SetPose(Mathf.Abs(speed) > .08f, false, false, hero.facing, Step);
            foreach (var e in game.enemies) { e.hurt = Mathf.Max(0, e.hurt - Step); e.art.SetPose(false, false, e.hurt > 0, e.facing, Step); }
            Call("TickShots", Step); Call("TickOrb", Step, true);
            vfx.Tick(Step);
        }
        private TowerGame.Fighter Target(Vector2 center, int kind = 0)
        {
            var at = center - Vector2.up * .7f;
            var enemy = (TowerGame.Fighter)Call("Spawn", "Weapon check target", kind, true, at);
            game.enemies.Add(enemy); Call("ConfigureEnemy", enemy, kind, 0);
            enemy.pos = at; enemy.root.transform.position = at; enemy.maxHp = enemy.hp = 1000000; enemy.cooldown = enemy.stunTime = 999; enemy.facing = -1;
            return enemy;
        }
        private void Remove(TowerGame.Fighter enemy) { game.enemies.Remove(enemy); if (enemy.root) Destroy(enemy.root); }
        private IList Shots { get { return (IList)typeof(TowerGame).GetField("shots", Hidden).GetValue(game); } }
        private static T Field<T>(object shot, string name) { return (T)shot.GetType().GetField(name).GetValue(shot); }
        private void ClearShots() { foreach (var s in Shots) Destroy(Field<GameObject>(s, "root")); Shots.Clear(); }

        // The pistol arm toward eight aims (the hero turns to the ones behind).
        private IEnumerator ReviewHold()
        {
            Setup(TowerAttackMode.Gun); yield return null;
            float[] angles = { 0, 30, 65, 100, -30, -65, 180, 145 };
            var sheet = new Texture2D(FrameWidth * 4, FrameHeight * 2, TextureFormat.RGBA32, false);
            var frame = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGBA32, false);
            for (int i = 0; i < angles.Length; i++)
            {
                Aim(angles[i], 2.6f);
                for (int s = 0; s < 40; s++) Advance(true);
                yield return null;
                Render(sheet, frame, 4, 2, i, Zoom, game.hero.pos + Vector2.up * 1.2f);
            }
            Save(sheet, frame, "gun-hold");
        }

        // Aiming while running (the last one runs right while aiming left: a backpedal) and while rising and falling.
        private IEnumerator ReviewMove()
        {
            Setup(TowerAttackMode.Gun); yield return null;
            var hero = game.hero;
            var sheet = new Texture2D(FrameWidth * 4, FrameHeight * 2, TextureFormat.RGBA32, false);
            var frame = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGBA32, false);
            float[] run = { 15, 60, -25, 165 };
            for (int i = 0; i < run.Length; i++)
            {
                Aim(run[i], 3f);
                for (int s = 0; s < 33 + i * 4; s++) Advance(true, 6);
                yield return null;
                Render(sheet, frame, 4, 2, i, Zoom, hero.pos + Vector2.up * 1.2f);
            }
            hero.pos.y += 1.4f; hero.grounded = false; hero.root.transform.position = hero.pos;
            float[] air = { 30, 80, -45, 150 }, rise = { 4, 4, -4, -4 };
            for (int i = 0; i < air.Length; i++)
            {
                hero.velocityY = rise[i]; Aim(air[i], 3f);
                for (int s = 0; s < 30; s++) Advance(true);
                yield return null;
                Render(sheet, frame, 4, 2, 4 + i, Zoom, hero.pos + Vector2.up * .9f);
            }
            Save(sheet, frame, "gun-move");
        }

        // Two volleys frame by frame, one level and one steep, each at an enemy on the aim: kick, flash, tracer, impact.
        private IEnumerator ReviewFire()
        {
            Setup(TowerAttackMode.Gun); yield return null;
            var sheet = new Texture2D(FrameWidth * 6, FrameHeight * 2, TextureFormat.RGBA32, false);
            var frame = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGBA32, false);
            float[] angles = { 8, 50 }, marks = { Step, 3 * Step, 6 * Step, 10 * Step, 16 * Step, 24 * Step };
            for (int row = 0; row < 2; row++)
            {
                var target = Target(Chest + Direction(angles[row]) * 5.5f);
                AimAt(target.pos + Vector2.up * .7f);
                for (int s = 0; s < 30; s++) Advance(true);
                Call("FireGun"); float clock = 0;
                for (int m = 0; m < marks.Length; m++)
                {
                    while (clock + Step <= marks[m]) { Advance(true); clock += Step; }
                    yield return null;
                    Render(sheet, frame, 6, 2, row * 6 + m, 3.6f, Chest + Direction(angles[row]) * 2.6f);
                }
                for (int s = 0; s < 30; s++) Advance(true);
                Remove(target); vfx.Clear(); ClearShots();
            }
            Save(sheet, frame, "gun-fire");
        }

        // The orb with three enemies about: two within reach (the reticle near the lower one) and one beyond it.
        private IEnumerator ReviewOrb()
        {
            Setup(TowerAttackMode.Orb); yield return null;
            var sheet = new Texture2D(FrameWidth * 4, FrameHeight * 2, TextureFormat.RGBA32, false);
            var frame = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGBA32, false);
            var near = Target(Chest + new Vector2(4.2f, -.2f)); var high = Target(Chest + new Vector2(-3.4f, 2.2f)); var far = Target(Chest + new Vector2(9.5f, 0));
            AimAt(near.pos + new Vector2(-.4f, 1.1f));
            float[] marks = { .2f, .45f, .58f, .7f, 1.0f, 1.25f, 1.6f, 2.1f }; float clock = 0;
            for (int m = 0; m < marks.Length; m++)
            {
                while (clock + Step <= marks[m]) { Advance(true); clock += Step; }
                if (m == 4) AimAt(high.pos + Vector2.up * .7f);
                yield return null;
                Render(sheet, frame, 4, 2, m, 4.6f, game.hero.pos + new Vector2(1.2f, 1.4f));
            }
            Save(sheet, frame, "orb");
            Remove(near); Remove(high); Remove(far); vfx.Clear(); ClearShots();
        }

        private void Check(bool passed, string name)
        { report.checks.Add((passed ? "pass  " : "FAIL  ") + name); if (!passed) report.errors.Add("check failed: " + name); }
        private bool Hurt(TowerGame.Fighter enemy) { return enemy.hp < enemy.maxHp; }
        private void Shoot(Vector2 at)
        {
            AimAt(at); for (int s = 0; s < 12; s++) Advance(true);
            game.slashCooldown = 0; Call("FireGun");
            for (int s = 0; s < 50; s++) Advance(true);
        }
        private IEnumerator Checks()
        {
            Setup(TowerAttackMode.Gun); yield return null;
            var hero = game.hero;
            var ahead = Target(Chest + new Vector2(6, 0));
            Shoot(Chest + new Vector2(3, 0)); Check(Hurt(ahead), "a gun shot aimed ahead hits an enemy 6 ahead");
            var above = Target(Chest + new Vector2(6, 2.6f)); float before = above.hp;
            Shoot(Chest + new Vector2(3, 0)); Check(above.hp >= before, "a shot aimed level misses an enemy 2.6 above the line");
            Shoot(above.pos + Vector2.up * .7f); Check(Hurt(above), "a shot aimed at the raised enemy hits it");
            Remove(ahead); Remove(above);
            var beyond = Target(Chest + new Vector2(game.GunRange + 2.5f, 0));
            Shoot(Chest + new Vector2(3, 0)); Check(!Hurt(beyond), "a shot falls short of an enemy past the gun's reach (" + game.GunRange.ToString("0.0") + ")");
            Remove(beyond);
            var behind = Target(Chest + new Vector2(-5, .5f));
            Shoot(behind.pos + Vector2.up * .7f); Check(Hurt(behind) && hero.facing < 0, "aiming behind turns the hero and the shot hits");
            Remove(behind);
            hero.facing = 1; Vector2 point = Chest + new Vector2(2.4f, 1.6f); AimAt(point);
            for (int s = 0; s < 30; s++) Advance(true);
            Vector2 predicted = hero.art.MuzzleToward(point, hero.facing);
            Transform gun = null; foreach (var r in hero.root.GetComponentsInChildren<SpriteRenderer>(true)) if (r.name == TowerHeroPixelArt.CrossbowName && r.enabled) gun = r.transform;
            Vector2 drawn = gun != null ? (Vector2)gun.TransformPoint(TowerHeroPixelArt.CrossbowMuzzle) : Vector2.zero;
            Check(gun != null && gun.gameObject.activeInHierarchy && (drawn - predicted).magnitude < .08f, "shots leave the drawn muzzle (off by " + (drawn - predicted).magnitude.ToString("0.000") + ")");
            game.slashCooldown = 0; Call("FireGun");
            var shot = Shots[Shots.Count - 1]; Vector2 velocity = Field<Vector2>(shot, "velocity"), from = Field<Vector2>(shot, "pos");
            Check(Vector2.Angle(velocity, point - from) < 1, "a shot flies at the reticle itself");
            ClearShots(); vfx.Clear();

            // Dash links. A bullet landing within the close reach strikes harder than one landing far off.
            var close = Target(Chest + new Vector2(2.2f, 0)); Shoot(close.pos + Vector2.up * .7f); float closeDamage = close.maxHp - close.hp; Remove(close);
            var distant = Target(Chest + new Vector2(6.5f, 0)); Shoot(distant.pos + Vector2.up * .7f); float distantDamage = distant.maxHp - distant.hp; Remove(distant);
            float closeRatio = closeDamage / Mathf.Max(.01f, distantDamage);
            Check(distantDamage > 0 && Mathf.Abs(closeRatio - TowerGame.GunCloseScale) < .02f, "a bullet landing within " + TowerGame.GunCloseReach.ToString("0.#") + " strikes x" + closeRatio.ToString("0.00") + " of one landing 6 away (x" + TowerGame.GunCloseScale.ToString("0.00") + ")");
            ClearShots();
            // The trigger stays live through a dash. The dash's first shot fans out at GunDashScale a bullet and spends the window,
            // so a shot right after the dash, its window still open, is an ordinary one; the next dash readies another.
            hero.facing = 1; game.ReadyDash(); AimAt(Chest + new Vector2(6, 0)); for (int s = 0; s < 12; s++) Advance(true);
            game.slashCooldown = 0; Call("StartDash", 0f, false);
            Check(game.dashTime > 0 && (bool)Call("GunTriggerReady", true), "the gun's trigger stays live during a dash");
            int pulls = game.GunShots, dashShots = game.GunDashShots, first = Shots.Count;
            firing = true; Advance(true); firing = false;
            Check(game.GunShots == pulls + 1 && game.dashTime > 0, "the gun fires during a dash");
            int fanned = Shots.Count - first; bool fan = fanned == TowerGame.GunDashPellets;
            float spread = fan ? Vector2.Angle(Field<Vector2>(Shots[first], "velocity"), Field<Vector2>(Shots[first + fanned - 1], "velocity")) : 0;
            float middleOff = fan ? Vector2.Angle(Field<Vector2>(Shots[first + 1], "velocity"), game.AimPoint - Field<Vector2>(Shots[first + 1], "pos")) : 180;
            float pellet = fanned > 0 ? Field<float>(Shots[first], "damage") : 0;
            for (int s = 0; s < 30 && game.dashTime > 0; s++) Advance(true);
            game.slashCooldown = 0; firing = true; Advance(true); firing = false;
            bool windowOpen = (bool)typeof(TowerGame).GetProperty("DashSlashReady", Hidden).GetValue(game);
            float plain = Field<float>(Shots[Shots.Count - 1], "damage"), share = pellet / Mathf.Max(.01f, plain);
            Check(fan && game.GunDashShots == dashShots + 1 && Mathf.Abs(spread - 2 * TowerGame.GunDashSpread) < .5f && middleOff < 1 && Mathf.Abs(share - TowerGame.GunDashScale) < .01f,
                "a dash shot fans " + fanned + " bullets over " + spread.ToString("0.0") + " deg, the middle one at the reticle, at x" + share.ToString("0.00") + " each");
            Check(game.GunShots == pulls + 2 && game.GunDashShots == dashShots + 1 && windowOpen, "the next shot, inside the dash's window still, is an ordinary one");
            Call("StartDash", 0f, false); game.slashCooldown = 0; firing = true; Advance(true); firing = false;
            Check(game.GunDashShots == dashShots + 2, "the next dash readies another dash shot");
            for (int s = 0; s < 30 && game.dashTime > 0; s++) Advance(true);
            ClearShots(); vfx.Clear();
            // A dash shot's bullets pierce: each strikes up to GunDashPierce enemies on its line, where an ordinary bullet stops
            // at the first. Three enemies stand on the aim, 1.6 apart.
            hero.facing = 1; var lead = Target(Chest + new Vector2(2.6f, 0)); var middle = Target(Chest + new Vector2(4.2f, 0)); var rear = Target(Chest + new Vector2(5.8f, 0));
            Shoot(middle.pos + Vector2.up * .7f);
            Check(Hurt(lead) && !Hurt(middle) && !Hurt(rear), "an ordinary bullet stops at the first enemy on its line");
            lead.hp = lead.maxHp; int pierced = game.GunDashShots;
            game.ReadyDash(); game.slashCooldown = 0; Call("StartDash", 0f, false); firing = true; Advance(true); firing = false;
            for (int s = 0; s < 40 && Shots.Count > 0; s++) Advance(true);
            Check(game.GunDashShots == pierced + 1 && Hurt(lead) && Hurt(middle) && !Hurt(rear), "a dash shot's bullets each strike " + TowerGame.GunDashPierce + " enemies: the first two on the line are hit, the third is not");
            for (int s = 0; s < 30 && game.dashTime > 0; s++) Advance(true);
            Remove(lead); Remove(middle); Remove(rear); ClearShots(); vfx.Clear();
            yield return null;

            // Setup starts a new run with a new hero: the gun checks' dashes carried the old one off.
            Setup(TowerAttackMode.Orb); yield return null; hero = game.hero;
            var inReach = Target(Chest + new Vector2(game.OrbRange - 1.5f, .3f)); var outOfReach = Target(Chest + new Vector2(game.OrbRange + 2, 0));
            AimAt(Chest + new Vector2(3, 0)); int bolts = game.OrbShots;
            for (int s = 0; s < 120; s++) Advance(true);
            Check(Hurt(inReach), "the orb hits an enemy within its reach (" + game.OrbRange.ToString("0.0") + ") by itself");
            Check(!Hurt(outOfReach), "the orb leaves an enemy beyond its reach alone");
            Check(game.OrbShown && game.OrbShots - bolts >= 3, "the orb floats beside the hero and keeps firing (" + (game.OrbShots - bolts) + " bolts in 2 s)");
            Remove(inReach); Remove(outOfReach); ClearShots();
            var front = Target(Chest + new Vector2(3, 0)); var picked = Target(Chest + new Vector2(-2.6f, 1.6f));
            AimAt(picked.pos + Vector2.up * .7f);
            for (int s = 0; s < 90 && !Hurt(front) && !Hurt(picked); s++) Advance(false);
            Check(Hurt(picked) && !Hurt(front), "the orb shoots the enemy nearest the reticle first");
            Remove(front); Remove(picked); ClearShots();
            var definition = TowerLoot.All[0]; foreach (var d in TowerLoot.All) if (d.Hits >= 2) { definition = d; break; }
            var sprite = TowerGame.LootSprite(definition.Id);
            var root = new GameObject("Check cache"); root.transform.position = new Vector3(hero.pos.x + 3.5f, TowerWorld.Ground, 0); root.transform.localScale = Vector3.one * TowerGame.CacheScale;
            var body = new GameObject("Body"); body.transform.SetParent(root.transform, false); var cacheRenderer = body.AddComponent<SpriteRenderer>(); cacheRenderer.sprite = sprite;
            game.caches.Add(new TowerGame.Cache { root = root, body = body.transform, renderer = cacheRenderer, definition = definition, pos = new Vector2(hero.pos.x + 3.5f, TowerWorld.Ground), support = -1, height = sprite.bounds.size.y * TowerGame.CacheScale });
            for (int s = 0; s < 150 && game.caches.Count > 0; s++) Advance(false);
            Check(game.caches.Count == 0, "with no enemy about the orb breaks a " + definition.Hits + "-hit vessel in reach");
            ClearShots(); vfx.Clear();
            // A dash lets the orb loose at once, a step after its last bolt, and its own rhythm carries on from that bolt.
            var quarry = Target(Chest + new Vector2(3, .3f)); AimAt(quarry.pos + Vector2.up * .7f);
            int bolt = game.OrbShots; for (int s = 0; s < 120 && game.OrbShots == bolt; s++) Advance(false);
            bolt = game.OrbShots; game.ReadyDash(); Call("StartDash", 0f, false); Advance(false);
            bool atOnce = game.OrbShots == bolt + 1; int wait = 0;
            for (bolt = game.OrbShots; wait < 120 && game.OrbShots == bolt; wait++) Advance(false);
            Check(atOnce && wait < 120 && wait * Step >= game.OrbInterval - 1.5f * Step, "a dash lets the orb fire at once, then " + (wait * Step).ToString("0.00") + " s to its next bolt (interval " + game.OrbInterval.ToString("0.00") + ")");
            for (int s = 0; s < 30 && game.dashTime > 0; s++) Advance(false);
            Remove(quarry); ClearShots(); vfx.Clear();
            // The dash's bolt bursts on the enemy it strikes: another standing 0.8 from it takes the same blow and one 1.8 away
            // none, while an ordinary bolt strikes its target alone. The hero dashes away so no second bolt reaches the mark.
            var mark = Target(Chest + new Vector2(3.5f, .3f)); var beside = Target(mark.pos + new Vector2(.8f, .7f)); var apart = Target(mark.pos + new Vector2(1.8f, .7f));
            AimAt(mark.pos + Vector2.up * .7f);
            for (int s = 0; s < 120 && !Hurt(mark); s++) Advance(false);
            Check(Hurt(mark) && !Hurt(beside) && !Hurt(apart), "an ordinary orb bolt strikes its target alone");
            float markHp = mark.hp; int released = game.OrbShots;
            game.hero.facing = -1; game.ReadyDash(); Call("StartDash", 0f, false); Advance(false);
            bool dashBolt = game.OrbShots == released + 1 && Shots.Count > 0 && Field<int>(Shots[Shots.Count - 1], "volley") != 0;
            for (int s = 0; s < 60 && !Hurt(beside); s++) Advance(false);
            float struck = markHp - mark.hp, splash = beside.maxHp - beside.hp;
            Check(dashBolt && struck > 0 && Mathf.Abs(splash - struck) <= struck * .01f && !Hurt(apart),
                "the dash's orb bolt bursts " + TowerGame.OrbDashBurst.ToString("0.#") + " around its target: " + splash.ToString("0.0") + " to the enemy beside for " + struck.ToString("0.0") + " to the target, none to the one apart");
            for (int s = 0; s < 30 && game.dashTime > 0; s++) Advance(false);
            Remove(mark); Remove(beside); Remove(apart); ClearShots(); vfx.Clear();
            yield return null;

            Setup(TowerAttackMode.Sword); yield return null;
            for (int s = 0; s < 4; s++) Advance(true);
            Check(game.AttackMode == TowerAttackMode.Sword && game.hero.art.Weapon == TowerAttackMode.Sword && !game.OrbShown, "back on the sword the orb is gone and the blade is out");
            game.AttackModeOverride = null; Call("SyncAttackMode", false);
            Check(game.AttackMode == TowerAttackMode.Sword, "fixtures without an override fight with the sword");
        }

        // The skill practice range running live with each attack mode in hand: a screenshot with its HUD (practice-*.png),
        // the gun and the orb striking the yard's targets, and none of it counted in the skill damage tally.
        private IEnumerator ReviewPractice()
        {
            firing = false; game.AimOverride = null; game.AttackOverride = false; reticle.SetActive(false);
            var capture = game.GetComponent<TowerSmokeDriver>(); capture.PrepareForSuite(game);
            game.AttackModeOverride = TowerAttackMode.Sword; game.enabled = true; game.EnterPractice();
            for (int f = 0; f < 40; f++) yield return null;
            var targets = game.enemies.FindAll(e => e.hp > 0);
            Check(targets.Count > 0, "the practice yard has " + targets.Count + " targets");
            Check((bool)Call("HUDHit", new Vector2(1430, 480)), "the practice attack panel takes clicks instead of the attack");
            var modes = new[] { TowerAttackMode.Sword, TowerAttackMode.Gun, TowerAttackMode.Orb };
            string[] names = { "sword", "gun", "orb" };
            for (int i = 0; i < modes.Length; i++)
            {
                game.AttackModeOverride = modes[i]; game.AttackOverride = modes[i] == TowerAttackMode.Gun;
                if (targets.Count > 0) game.AimOverride = targets[0].pos + Vector2.up * .7f;
                int hits = game.PracticeHits; float tally = game.PracticeDamage; bool struck = false;
                for (int f = 0; f < 120; f++) { foreach (var t in targets) struck |= t.hurt > 0; yield return null; }
                game.AttackOverride = false;
                // The smoke driver's capture draws the real HUD offscreen; a hidden player's back buffer comes out blank.
                yield return capture.CaptureForSuite(Path.Combine(folder, "practice"), names[i]);
                report.sheets.Add(Path.Combine(folder, "practice-" + names[i] + ".png"));
                if (modes[i] == TowerAttackMode.Sword) continue;
                Check(struck, "in the practice range the " + names[i] + " strikes the targets");
                Check(game.PracticeHits == hits && Mathf.Approximately(game.PracticeDamage, tally), "the " + names[i] + "'s hits stay out of the skill damage tally");
            }
            game.AimOverride = null; game.AttackModeOverride = null;
        }

        // A short scripted bout at 30 frames a second (film/frame-###.png): the gun sweeps across three enemies and turns to
        // one behind, then the orb takes over while the hero walks on.
        private IEnumerator Film()
        {
            Setup(TowerAttackMode.Gun); yield return null;
            var hero = game.hero; string films = Path.Combine(folder, "film"); Directory.CreateDirectory(films); reticle.SetActive(true);
            var shot = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGBA32, false);
            var a = Target(Chest + new Vector2(5.5f, -.1f)); var b = Target(Chest + new Vector2(4.5f, 3)); var c = Target(Chest + new Vector2(-5, .3f));
            int step = 0, saved = 0; bool switched = false;
            for (float clock = 0; clock < 6; clock += Step, step++)
            {
                float speed = 0;
                if (clock < 3.2f)
                {
                    Vector2 from = clock < 1.1f ? a.pos : clock < 2.1f ? b.pos : c.pos, to = clock < 1.1f ? b.pos : clock < 2.1f ? c.pos : c.pos;
                    float t = clock < 1.1f ? Mathf.SmoothStep(0, 1, (clock - .6f) / .5f) : clock < 2.1f ? Mathf.SmoothStep(0, 1, (clock - 1.6f) / .5f) : 0;
                    AimAt(Vector2.Lerp(from, to, t) + Vector2.up * .7f);
                    firing = clock > .25f && clock < 3.1f;
                }
                else
                {
                    if (!switched) { switched = true; firing = false; game.AttackModeOverride = TowerAttackMode.Orb; Call("SyncAttackMode", true); }
                    speed = clock < 5.2f ? 2.6f : 0; hero.pos.x += speed * Step; hero.root.transform.position = hero.pos;
                    AimAt(Chest + new Vector2(3.2f, .6f + Mathf.Sin(clock * 2) * .8f));
                }
                Advance(true, speed);
                game.view.transform.position = new Vector3(hero.pos.x * .7f + .4f, TowerWorld.Ground + 2.1f, -10);
                if (step % 2 != 0) continue;
                yield return null;
                var camera = game.view; var target = RenderTexture.GetTemporary(FrameWidth, FrameHeight, 24, RenderTextureFormat.ARGB32);
                float size = camera.orthographicSize; camera.targetTexture = target; camera.orthographicSize = 4.4f; camera.aspect = FrameWidth / (float)FrameHeight; camera.Render();
                var active = RenderTexture.active; RenderTexture.active = target; shot.ReadPixels(new Rect(0, 0, FrameWidth, FrameHeight), 0, 0); shot.Apply();
                RenderTexture.active = active; camera.targetTexture = null; camera.orthographicSize = size; camera.ResetAspect(); RenderTexture.ReleaseTemporary(target);
                File.WriteAllBytes(Path.Combine(films, "frame-" + (saved++).ToString("000") + ".png"), shot.EncodeToPNG());
            }
            firing = false; Destroy(shot); report.sheets.Add(films);
            Remove(a); Remove(b); Remove(c); vfx.Clear(); ClearShots();
        }

        private void Render(Texture2D sheet, Texture2D frame, int columns, int rows, int index, float zoom, Vector2 center)
        {
            var camera = game.view; var previousTarget = camera.targetTexture; float previousSize = camera.orthographicSize; Vector3 previousPosition = camera.transform.position;
            reticle.SetActive(game.AimOverride.HasValue);
            var target = RenderTexture.GetTemporary(FrameWidth, FrameHeight, 24, RenderTextureFormat.ARGB32);
            camera.transform.position = new Vector3(center.x, center.y, -10);
            camera.targetTexture = target; camera.orthographicSize = zoom; camera.aspect = FrameWidth / (float)FrameHeight;
            camera.Render();
            var previousActive = RenderTexture.active; RenderTexture.active = target;
            frame.ReadPixels(new Rect(0, 0, FrameWidth, FrameHeight), 0, 0); frame.Apply();
            RenderTexture.active = previousActive;
            camera.targetTexture = previousTarget; camera.orthographicSize = previousSize; camera.ResetAspect(); camera.transform.position = previousPosition;
            RenderTexture.ReleaseTemporary(target);
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Explicit opt-in fixture. Every observed walk, jump and drop uses TowerGame.Update on the
    // randomly generated rolling terrain and sloped platforms of sixteen consecutive floors.
    public sealed class TowerPlatformSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class PlatformReport
        {
            public string mode = "towerPlatformSmoke", finishedUtc;
            public bool success, allLayoutsReachable, allThemesRendered, dropInputVerified, elevatedBulletEvasion, upperCameraTracks, hillsWalked;
            public int sampledPatterns, sampledThemes, jumpedLedges, droppedLedges, lowerPlatformCatches, groundBulletHits, elevatedBulletMisses, upperCameraChecks, slopedPlatforms, hillFloors;
            public List<string> fixtureAdjustments = new List<string>(), samples = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        private readonly PlatformReport report = new PlatformReport();
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private TowerGame game;
        private TowerWorld world;
        private TowerSmokeDriver capture;
        private string prefix, reportPath;
        private float deadline;
        private bool finished, initialized, quitOnFinish;
        private const int RunSeed = 20031, Floors = 16;
        public bool IsFinished { get { return finished; } }
        public bool Succeeded { get { return finished && report.success; } }
        public string FailureReason { get { return report.errors.Count > 0 ? report.errors[0] : null; } }

        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver, string prefix, string reportPath, bool quitOnFinish = false)
        {
            if (!tower || !captureDriver) throw new ArgumentException("A live game and capture host are required.");
            game = tower; capture = captureDriver;
            Configure(prefix, reportPath, quitOnFinish);
        }
        private void Configure(string capturePrefix, string outputPath, bool quit)
        {
            if (initialized) throw new InvalidOperationException("This platform suite was already initialized.");
            if (!Path.IsPathRooted(capturePrefix) || !Path.IsPathRooted(outputPath)) throw new ArgumentException("Platform output paths must be absolute.");
            prefix = Path.GetFullPath(capturePrefix); reportPath = Path.GetFullPath(outputPath); quitOnFinish = quit;
            initialized = true; deadline = Time.realtimeSinceStartup + 900;
            Application.logMessageReceived += RecordLog;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Enter()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-towerPlatformSmoke") < 0) return;
            var driver = new GameObject("Tower platform validation").AddComponent<TowerPlatformSmokeDriver>();
            driver.Configure(Arg(args, "-towerPlatformCapture") ?? Path.Combine(Application.persistentDataPath, "tower-platforms"),
                Arg(args, "-towerPlatformReport") ?? Path.Combine(Application.persistentDataPath, "tower-platforms.json"), true);
        }
        private IEnumerator Start() { if (initialized) yield return Guard(Suite()); }
        private static string Arg(string[] args, string key) { int at = Array.IndexOf(args, key); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private void RecordLog(string message, string trace, LogType type)
        { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && report.errors.Count < 64) report.errors.Add(message + "\n" + trace); }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }
        private void Update()
        { if (initialized && !finished && Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Finish(new TimeoutException("Platform suite exceeded 900 seconds.")); } }
        private object Call(string name, params object[] values) { return typeof(TowerGame).GetMethod(name, Hidden).Invoke(game, values); }
        private IEnumerator Guard(IEnumerator body)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(body);
            while (stack.Count > 0)
            {
                bool more = false; object next = null; Exception error = null;
                try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; } catch (Exception ex) { error = ex; }
                if (error != null) { Finish(error); yield break; }
                if (!more) { stack.Pop(); continue; }
                var nested = next as IEnumerator; if (nested != null) stack.Push(nested); else yield return next;
            }
        }
        private IEnumerator Suite()
        {
            Require(Path.IsPathRooted(prefix) && Path.IsPathRooted(reportPath), "Platform output paths must be absolute.");
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            for (int frame = 0; frame < 180 && game == null; frame++) { game = FindFirstObjectByType<TowerGame>(); if (game == null) yield return null; }
            Require(game != null, "The roguelike game did not load.");
            game.enabled = false; game.SmokeControlled = game.SuppressProgressPersistence = true;
            world = (TowerWorld)typeof(TowerGame).GetField("world", Hidden).GetValue(game);
            capture = game.GetComponent<TowerSmokeDriver>(); Require(capture != null, "Shared camera/HUD capture host is missing."); capture.PrepareForSuite(game);
            report.fixtureAdjustments.Add("Sixteen consecutive floors of one fixed run seed are loaded through SpawnFloor, cycling through all nine chapter themes (each on its own chapter floor); the first nine have overview captures. Each floor's rolling ground and sloped platforms are random but decided by the seed and floor alone.");
            report.fixtureAdjustments.Add("A breadth-first search over the shared jump model plans conservative normal-speed jumps at the maximum .04-second simulation step. Each edge starts with the player positioned on the source surface, then actual Update inputs walk to the launch point and jump to the destination. Source positioning isolates individual edges; the suite does not claim one uninterrupted run.");
            report.fixtureAdjustments.Add("One living, stunned enemy keeps the floor active. Attack, dash and skill inputs remain off. Drop is pressed for one frame and released; lower-platform and ground catches use production gravity. The player also walks the whole hilly ground of every floor with real movement input without leaving it.");
            report.fixtureAdjustments.Add("A real hostile bullet hits the stationary player at ground level, then an identical-height bullet passes under a platform reached by real jump input. Both bullet observations use zero invulnerability and unchanged production projectile collision.");
            var covered = new HashSet<int>(); var themes = new HashSet<TowerThemeId>();
            for (int floor = 1; floor <= Floors; floor++)
            {
                var theme = TowerThemeCatalog.All[(floor - 1) % TowerThemeCatalog.All.Count];
                int themeFloor = ChapterFloor(theme, (floor - 1) % TowerThemeCatalog.FloorsPerTheme + 1);
                SetFixture(theme, themeFloor);
                var layout = world.CurrentLayout;
                Require(layout != null && layout.Floor == themeFloor && world.Ledges.Length == layout.Platforms.Count, "Floor did not load its generated layout.");
                for (int i = 0; i < world.Ledges.Length; i++)
                    Require(ReferenceEquals(world.Ledges[i].surface, layout.Platforms[i].Surface), "Rendered/collision geometry diverged from the generated layout.");
                int sloped = 0; foreach (var ledge in world.Ledges) if (ledge.Top - ledge.Bottom > .05f) sloped++;
                report.slopedPlatforms += sloped; if (layout.HighestGround > TowerWorld.Ground + .3f) report.hillFloors++;
                var plans = TowerJumpModel.Reachable(layout, .04f);
                Require(plans.Count == world.Ledges.Length, layout.Name + " has unreachable platforms under conservative ordinary movement.");
                game.enabled = true;
                yield return WalkHills();
                yield return GroundBulletControl(plans[0].target);
                bool bulletChecked = false;
                foreach (var plan in plans)
                {
                    float sourceX = plan.source < 0 ? plan.launch : world.Ledges[plan.source].x;
                    Place(sourceX, world.SurfaceHeight(plan.source, sourceX), plan.source);
                    yield return WalkTo(plan.launch);
                    yield return JumpTo(plan);
                    report.jumpedLedges++;
                    if (world.Ledges[plan.target].y > 5)
                    {
                        var visible = game.view.WorldToViewportPoint(game.hero.pos + Vector2.up);
                        Require(game.view.transform.position.y > 3.5f && visible.y > .1f && visible.y < .9f, "The camera failed to keep the player visible on an upper platform.");
                        report.upperCameraChecks++;
                    }
                    if (!bulletChecked && plan.source < 0)
                    { yield return ElevatedBulletMiss(); bulletChecked = true; }
                    yield return DropFromLedge(); report.droppedLedges++;
                }
                covered.Add(layout.Id); themes.Add(theme.Id);
                report.sampledPatterns = covered.Count; report.sampledThemes = themes.Count;
                report.samples.Add(theme.Id + ": floor=" + themeFloor + ", layout=" + layout.Name + ", hill=" + (layout.HighestGround - TowerWorld.Ground).ToString("F2") + ", sloped=" + sloped + ", walked/jumped/dropped=" + plans.Count);
                if (floor <= TowerThemeCatalog.All.Count)
                {
                    game.enabled = false; StageOverview(); world.Tick(.4f, false);
                    string suffix = theme.Id.ToString().ToLowerInvariant();
                    yield return capture.CaptureForSuite(prefix, suffix);
                    report.screenshots.Add(prefix + "-" + suffix + ".png");
                }
            }
            report.allLayoutsReachable = report.sampledPatterns == Floors && report.jumpedLedges == report.droppedLedges;
            report.allThemesRendered = report.sampledThemes == TowerThemeCatalog.All.Count && report.screenshots.Count == TowerThemeCatalog.All.Count;
            report.dropInputVerified = report.droppedLedges > 0 && report.lowerPlatformCatches > 0;
            report.elevatedBulletEvasion = report.groundBulletHits == Floors && report.elevatedBulletMisses == Floors;
            report.upperCameraTracks = report.upperCameraChecks >= Floors;
            report.hillsWalked = report.hillFloors >= 8 && report.slopedPlatforms >= 16;
            Require(report.allLayoutsReachable && report.allThemesRendered && report.dropInputVerified && report.elevatedBulletEvasion && report.upperCameraTracks && report.hillsWalked, "Incomplete platform coverage.");
            Finish(null);
        }
        private void ClearActors()
        { foreach (var actor in game.enemies) if (actor.root) { actor.root.SetActive(false); Destroy(actor.root); } game.enemies.Clear(); }
        // A run's theme follows its floor, so a fixture visits a theme by moving to that chapter's floor.
        private static int ChapterFloor(TowerThemeDefinition theme, int floorInChapter)
        {
            int index = 0;
            for (int i = 0; i < TowerThemeCatalog.All.Count; i++) if (System.Object.ReferenceEquals(TowerThemeCatalog.All[i], theme)) index = i;
            return index * TowerThemeCatalog.FloorsPerTheme + floorInChapter;
        }
        private void SetFixture(TowerThemeDefinition theme, int floor)
        {
            game.enabled = false; game.NewRun(RunSeed); game.StartClimb(); ClearActors();
            typeof(TowerRun).GetField("<Floor>k__BackingField", Hidden).SetValue(game.run, floor);
            Require(game.run.Theme == theme, "Fixture floor " + floor + " is outside " + theme.Name + ".");
            Call("SpawnFloor");
            for (int i = game.enemies.Count - 1; i > 0; i--) { var actor = game.enemies[i]; actor.root.SetActive(false); Destroy(actor.root); game.enemies.RemoveAt(i); }
            Require(game.enemies.Count == 1 && !game.enemies[0].isBoss, "One ordinary enemy is required to keep movement fixtures active.");
            var sentinel = game.enemies[0]; sentinel.pos = new Vector2(TowerWorld.Right, TowerWorld.Ground); sentinel.root.transform.position = sentinel.pos; sentinel.stunTime = sentinel.cooldown = 999;
            game.intro = game.menu = game.party = game.help = false;
            game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = game.DropOverride = game.FocusOverride = false;
            game.invulnerable = 1000;
        }
        private void Place(float x, float y, int support)
        {
            game.MoveOverride = 0; game.JumpOverride = game.DropOverride = false;
            game.hero.pos = new Vector2(x, y); game.hero.velocityY = game.hero.dropTime = 0; game.hero.grounded = true; game.hero.support = support; game.hero.lastX = x; game.hero.root.transform.position = game.hero.pos;
            typeof(TowerGame).GetField("jumpBuffer", Hidden).SetValue(game, 0f);
            typeof(TowerGame).GetField("coyote", Hidden).SetValue(game, 0f);
        }
        /// <summary>Real movement input across the whole floor: the player must stay on the hilly ground the entire way.</summary>
        private IEnumerator WalkHills()
        {
            Place(TowerWorld.Left + .8f, world.GroundAt(TowerWorld.Left + .8f), -1);
            // Walking pace with room for the hills, over however long the floor runs.
            float until = game.elapsed + (TowerWorld.Right - TowerWorld.Left) / game.PlayerMoveSpeed * 1.4f + 2, climbed = 0, previousY = game.hero.pos.y;
            game.MoveOverride = 1;
            while (game.hero.pos.x < TowerWorld.Right - 1)
            {
                Require(game.elapsed < until && !game.Paused, "The hill walk did not reach the far side of the floor.");
                Require(game.hero.grounded && game.hero.support == -1 && Mathf.Abs(game.hero.pos.y - world.GroundAt(game.hero.pos.x)) < .01f, "Walking left the rolling ground at x=" + game.hero.pos.x.ToString("F2") + ".");
                climbed += Mathf.Max(0, game.hero.pos.y - previousY); previousY = game.hero.pos.y;
                yield return null;
            }
            game.MoveOverride = 0;
            if (world.CurrentLayout.HighestGround > TowerWorld.Ground + .3f) Require(climbed > .3f, "A hilly floor was walked without any climb.");
        }
        private IEnumerator WalkTo(float x)
        {
            float until = game.elapsed + 3; int support = game.hero.support;
            while (Mathf.Abs(x - game.hero.pos.x) > .035f)
            {
                Require(game.elapsed < until && !game.Paused && game.hero.grounded && game.hero.support == support, "Walking to the launch point left its source surface.");
                Aim(x); yield return null;
            }
            game.MoveOverride = 0;
        }
        private void Aim(float x)
        { game.MoveOverride = Mathf.Clamp((x - game.hero.pos.x) / (game.PlayerMoveSpeed * Mathf.Max(.001f, Mathf.Min(Time.unscaledDeltaTime, .04f))), -1, 1); }
        private IEnumerator JumpTo(TowerJumpModel.Plan plan)
        {
            Aim(plan.aim); game.JumpOverride = true; yield return null; game.JumpOverride = false;
            Require(!game.hero.grounded && game.hero.velocityY > 0, "Jump input did not launch from the source surface.");
            float until = game.elapsed + 2.5f;
            while (!game.hero.grounded)
            {
                Require(game.elapsed < until && !game.Paused, "Jump did not land."); Aim(plan.aim); yield return null;
            }
            game.MoveOverride = 0; var target = world.Ledges[plan.target];
            Require(game.hero.support == plan.target && Mathf.Abs(game.hero.pos.y - target.HeightAt(game.hero.pos.x)) < .035f && Mathf.Abs(game.hero.pos.x - target.x) <= target.width * .5f - .12f,
                "Real jump missed ledge " + plan.target + "; landed at " + game.hero.pos.ToString("F3") + " on support " + game.hero.support + ".");
        }
        private IEnumerator DropFromLedge()
        {
            float sourceY = game.hero.pos.y; game.MoveOverride = 0; game.DropOverride = true;
            yield return null; game.DropOverride = false;
            Require(!game.hero.grounded && game.hero.velocityY < 0, "One-frame drop input did not leave the platform.");
            float until = game.elapsed + 2;
            while (!game.hero.grounded)
            { Require(game.elapsed < until && !game.Paused, "Released drop input failed to land on a lower surface."); yield return null; }
            Require(game.hero.pos.y < sourceY - .2f && game.hero.pos.y >= world.GroundAt(game.hero.pos.x) - .001f, "Released drop input caught the departed platform or fell through the floor.");
            if (game.hero.support >= 0) report.lowerPlatformCatches++;
        }
        private void FireGroundBullet(float x)
        {
            float y = world.GroundAt(x) + .9f;
            Call("Fire", new Vector2(x - 4, y), new Vector2(x + 4, y), 8f, 10f, true, Color.red, 0f, null, 1, 0f);
            Require(game.HostileProjectiles > 0, "The real hostile bullet was not created.");
        }
        private IEnumerator WaitSeconds(float seconds)
        { float until = game.elapsed + seconds; while (game.elapsed < until) { Require(!game.Paused, "Movement fixture unexpectedly paused."); yield return null; } }
        private IEnumerator GroundBulletControl(int target)
        {
            float x = world.Ledges[target].x;
            Place(x, world.GroundAt(x), -1); game.invulnerable = 0; float hp = game.hero.hp;
            FireGroundBullet(game.hero.pos.x); yield return WaitSeconds(.8f);
            Require(game.hero.hp < hp, "Ground-level control bullet did not damage the player."); report.groundBulletHits++;
            game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 1000;
        }
        private IEnumerator ElevatedBulletMiss()
        {
            Require(game.hero.grounded && game.hero.support >= 0, "Bullet evasion requires an elevated platform reached by input.");
            game.invulnerable = 0; float hp = game.hero.hp;
            FireGroundBullet(game.hero.pos.x); yield return WaitSeconds(.8f);
            Require(Mathf.Abs(game.hero.hp - hp) < .001f && game.hero.grounded, "Ground-height bullet hit the elevated stationary player.");
            report.elevatedBulletMisses++; game.invulnerable = 1000;
        }
        private void StageOverview()
        {
            Place(-12, TowerWorld.Ground, -1);
            game.view.orthographicSize = 14; game.view.transform.position = new Vector3(6.5f, 1, -10);
            game.Notice(game.run.Theme.Name + " · " + world.CurrentLayout.Name + " · 일반 점프/하강 검증 완료", 20);
        }
        private void Finish(Exception error)
        {
            if (finished) return; finished = true;
            if (error != null) report.errors.Add(error is TargetInvocationException && error.InnerException != null ? error.InnerException.ToString() : error.ToString());
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0;
            if (game != null) { game.enabled = false; game.MoveOverride = 0; game.JumpOverride = game.DropOverride = false; }
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= RecordLog;
            if (quitOnFinish) Application.Quit(report.success ? 0 : 1);
        }
    }
}

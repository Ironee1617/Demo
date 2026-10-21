using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Explicit opt-in fixture. Every observed walk, jump and drop uses TowerGame.Update.
    public sealed class TowerPlatformSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class PlatformReport
        {
            public string mode = "towerPlatformSmoke", finishedUtc;
            public bool success, allLayoutsReachable, allThemesRendered, dropInputVerified, elevatedBulletEvasion, upperCameraTracks;
            public int sampledPatterns, sampledThemes, jumpedLedges, droppedLedges, lowerPlatformCatches, groundBulletHits, elevatedBulletMisses, upperCameraChecks;
            public List<string> fixtureAdjustments = new List<string>(), samples = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        private sealed class JumpPlan { public int source, target; public float launch, aim; }
        private readonly PlatformReport report = new PlatformReport();
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private TowerGame game;
        private TowerWorld world;
        private TowerSmokeDriver capture;
        private string prefix, reportPath;
        private float deadline;
        private bool finished, initialized, quitOnFinish;
        private const int RunSeed = 20031;
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
            report.fixtureAdjustments.Add("Every production layout is loaded through SpawnFloor with a fixed run seed. The sixteen layouts cycle through all seven themes; the first seven have overview captures.");
            report.fixtureAdjustments.Add("A breadth-first search plans conservative normal-speed jumps at the maximum .04-second simulation step. Each edge starts with the player positioned at the source platform center, then actual Update inputs walk to the launch point and jump to the destination. Source positioning isolates individual edges; the suite does not claim one uninterrupted run.");
            report.fixtureAdjustments.Add("One living, stunned enemy keeps the floor active. Attack, dash and skill inputs remain off. Drop is pressed for one frame and released; lower-platform and ground catches use production gravity.");
            report.fixtureAdjustments.Add("A real hostile bullet hits the stationary player at ground level, then an identical-height bullet passes under a platform reached by real jump input. Both bullet observations use zero invulnerability and unchanged production projectile collision.");
            var covered = new HashSet<int>(); var themes = new HashSet<TowerThemeId>();
            for (int ordinal = 0; ordinal < TowerPlatformCatalog.All.Count; ordinal++)
            {
                var pattern = TowerPlatformCatalog.All[ordinal];
                var theme = TowerThemeCatalog.All[ordinal % TowerThemeCatalog.All.Count];
                int floor = 1; while (floor <= 128 && TowerPlatformCatalog.Select(RunSeed, floor).Id != pattern.Id) floor++;
                Require(floor <= 128, "Pattern cannot be selected: " + pattern.Name);
                SetFixture(theme, floor);
                Require(world.CurrentPattern.Id == pattern.Id && world.Ledges.Length == pattern.Platforms.Count, "Floor did not load its selected pattern.");
                for (int i = 0; i < world.Ledges.Length; i++)
                {
                    var a = world.Ledges[i]; var b = pattern.Platforms[i];
                    Require(Mathf.Abs(a.x - b.X) < .001f && Mathf.Abs(a.y - b.Y) < .001f && Mathf.Abs(a.width - b.Width) < .001f, "Rendered/collision geometry diverged from the authored layout.");
                }
                var plans = ReachablePlans();
                Require(plans.Count == world.Ledges.Length, pattern.Name + " has unreachable platforms under conservative ordinary movement.");
                game.enabled = true;
                yield return GroundBulletControl(plans[0].target);
                bool bulletChecked = false;
                foreach (var plan in plans)
                {
                    float sourceY = plan.source < 0 ? TowerWorld.Ground : world.Ledges[plan.source].y;
                    float sourceX = plan.source < 0 ? plan.launch : world.Ledges[plan.source].x;
                    Place(sourceX, sourceY);
                    yield return WalkTo(plan.launch);
                    yield return JumpTo(plan);
                    report.jumpedLedges++;
                    if(world.Ledges[plan.target].y>5)
                    {
                        var visible=game.view.WorldToViewportPoint(game.hero.pos+Vector2.up);
                        Require(game.view.transform.position.y>3.5f&&visible.y>.1f&&visible.y<.9f,"The camera failed to keep the player visible on an upper platform.");
                        report.upperCameraChecks++;
                    }
                    if (!bulletChecked && plan.source < 0)
                    { yield return ElevatedBulletMiss(); bulletChecked = true; }
                    yield return DropFromLedge(); report.droppedLedges++;
                }
                covered.Add(pattern.Id); themes.Add(theme.Id);
                report.sampledPatterns = covered.Count; report.sampledThemes = themes.Count;
                report.samples.Add(theme.Id + ": floor=" + floor + ", pattern=" + pattern.Id + " " + pattern.Name + ", walked/jumped/dropped=" + plans.Count);
                if (ordinal < TowerThemeCatalog.All.Count)
                {
                    game.enabled = false; StageOverview(); world.Tick(.4f, false);
                    string suffix = theme.Id.ToString().ToLowerInvariant();
                    yield return capture.CaptureForSuite(prefix, suffix);
                    report.screenshots.Add(prefix + "-" + suffix + ".png");
                }
            }
            report.allLayoutsReachable = report.sampledPatterns == 16 && report.jumpedLedges == report.droppedLedges;
            report.allThemesRendered = report.sampledThemes == 7 && report.screenshots.Count == 7;
            report.dropInputVerified = report.droppedLedges > 0 && report.lowerPlatformCatches > 0;
            report.elevatedBulletEvasion = report.groundBulletHits == 16 && report.elevatedBulletMisses == 16;
            report.upperCameraTracks=report.upperCameraChecks>=16;
            Require(report.allLayoutsReachable && report.allThemesRendered && report.dropInputVerified && report.elevatedBulletEvasion && report.upperCameraTracks, "Incomplete platform coverage.");
            Finish(null);
        }
        private void ClearActors()
        { foreach (var actor in game.enemies) if (actor.root) { actor.root.SetActive(false); Destroy(actor.root); } game.enemies.Clear(); }
        private void SetFixture(TowerThemeDefinition theme, int floor)
        {
            game.enabled = false; game.NewRun(RunSeed); game.StartClimb(); ClearActors();
            typeof(TowerRun).GetField("<Floor>k__BackingField", Hidden).SetValue(game.run, floor);
            typeof(TowerRun).GetField("<Theme>k__BackingField", Hidden).SetValue(game.run, theme);
            Call("SpawnFloor");
            for (int i = game.enemies.Count - 1; i > 0; i--) { var actor = game.enemies[i]; actor.root.SetActive(false); Destroy(actor.root); game.enemies.RemoveAt(i); }
            Require(game.enemies.Count == 1 && !game.enemies[0].isBoss, "One ordinary enemy is required to keep movement fixtures active.");
            var sentinel = game.enemies[0]; sentinel.pos = new Vector2(TowerWorld.Right, TowerWorld.Ground); sentinel.root.transform.position = sentinel.pos; sentinel.stunTime = sentinel.cooldown = 999;
            game.intro = game.menu = game.party = game.help = false;
            game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = game.DropOverride = game.FocusOverride = false;
            game.invulnerable = 1000;
        }
        private void Place(float x, float y)
        {
            game.MoveOverride = 0; game.JumpOverride = game.DropOverride = false;
            game.hero.pos = new Vector2(x, y); game.hero.velocityY = game.hero.dropTime = 0; game.hero.grounded = true; game.hero.root.transform.position = game.hero.pos;
            typeof(TowerGame).GetField("jumpBuffer", Hidden).SetValue(game, 0f);
            typeof(TowerGame).GetField("coyote", Hidden).SetValue(game, 0f);
        }
        private IEnumerator WalkTo(float x)
        {
            float until = game.elapsed + 3;
            while (Mathf.Abs(x - game.hero.pos.x) > .035f)
            {
                Require(game.elapsed < until && !game.Paused && game.hero.grounded, "Walking to the launch point left its source platform.");
                Aim(x); yield return null;
            }
            game.MoveOverride = 0;
        }
        private void Aim(float x)
        { game.MoveOverride = Mathf.Clamp((x - game.hero.pos.x) / (game.PlayerMoveSpeed * Mathf.Max(.001f, Mathf.Min(Time.unscaledDeltaTime, .04f))), -1, 1); }
        private IEnumerator JumpTo(JumpPlan plan)
        {
            Aim(plan.aim); game.JumpOverride = true; yield return null; game.JumpOverride = false;
            Require(!game.hero.grounded && game.hero.velocityY > 0, "Jump input did not launch from the source platform.");
            float until = game.elapsed + 2.5f;
            while (!game.hero.grounded)
            {
                Require(game.elapsed < until && !game.Paused, "Jump did not land."); Aim(plan.aim); yield return null;
            }
            game.MoveOverride = 0; var target = world.Ledges[plan.target];
            Require(Mathf.Abs(game.hero.pos.y - target.y) < .035f && Mathf.Abs(game.hero.pos.x - target.x) <= target.width * .5f - .12f,
                "Real jump missed ledge " + plan.target + "; landed at " + game.hero.pos.ToString("F3") + ".");
        }
        private IEnumerator DropFromLedge()
        {
            float sourceY = game.hero.pos.y; game.MoveOverride = 0; game.DropOverride = true;
            yield return null; game.DropOverride = false;
            Require(!game.hero.grounded && game.hero.velocityY < 0, "One-frame drop input did not leave the platform.");
            float until = game.elapsed + 2;
            while (!game.hero.grounded)
            { Require(game.elapsed < until && !game.Paused, "Released drop input failed to land on a lower surface."); yield return null; }
            Require(game.hero.pos.y < sourceY - .2f && game.hero.pos.y >= TowerWorld.Ground, "Released drop input caught the departed platform or fell through the floor.");
            if (game.hero.pos.y > TowerWorld.Ground + .1f) report.lowerPlatformCatches++;
        }
        private void FireGroundBullet(float x)
        {
            float y = TowerWorld.Ground + .9f;
            Call("Fire", new Vector2(x - 4, y), new Vector2(x + 4, y), 8f, 10f, true, Color.red, 0f, null, 1, 0f);
            Require(game.HostileProjectiles > 0, "The real hostile bullet was not created.");
        }
        private IEnumerator WaitSeconds(float seconds)
        { float until = game.elapsed + seconds; while (game.elapsed < until) { Require(!game.Paused, "Movement fixture unexpectedly paused."); yield return null; } }
        private IEnumerator GroundBulletControl(int target)
        {
            Place(world.Ledges[target].x, TowerWorld.Ground); game.invulnerable = 0; float hp = game.hero.hp;
            FireGroundBullet(game.hero.pos.x); yield return WaitSeconds(.8f);
            Require(game.hero.hp < hp, "Ground-level control bullet did not damage the player."); report.groundBulletHits++;
            game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 1000;
        }
        private IEnumerator ElevatedBulletMiss()
        {
            Require(game.hero.grounded && game.hero.pos.y > TowerWorld.Ground + .9f, "Bullet evasion requires an elevated platform reached by input.");
            game.invulnerable = 0; float hp = game.hero.hp;
            FireGroundBullet(game.hero.pos.x); yield return WaitSeconds(.8f);
            Require(Mathf.Abs(game.hero.hp - hp) < .001f && game.hero.grounded, "Ground-height bullet hit the elevated stationary player.");
            report.elevatedBulletMisses++; game.invulnerable = 1000;
        }
        private void StageOverview()
        {
            Place(-12, TowerWorld.Ground);
            game.view.orthographicSize = 14; game.view.transform.position = new Vector3(6.5f, 1, -10);
            game.Notice(game.run.Theme.Name + " · " + world.CurrentPattern.Name + " · 일반 점프/하강 검증 완료", 20);
        }
        private List<JumpPlan> ReachablePlans()
        {
            var plans = new List<JumpPlan>(); var reached = new HashSet<int>(); var queue = new Queue<int>(); queue.Enqueue(-1);
            while (queue.Count > 0)
            {
                int source = queue.Dequeue();
                for (int target = 0; target < world.Ledges.Length; target++)
                {
                    if (reached.Contains(target)) continue;
                    var plan = Plan(source, target); if (plan == null) continue;
                    reached.Add(target); queue.Enqueue(target); plans.Add(plan);
                }
            }
            return plans;
        }
        private JumpPlan Plan(int source, int target)
        {
            var destination = world.Ledges[target]; float sourceY = source < 0 ? TowerWorld.Ground : world.Ledges[source].y;
            if (destination.y - sourceY > 2.05f || destination.y < sourceY - .1f) return null;
            float left = source < 0 ? destination.x - 3 : world.Ledges[source].x - world.Ledges[source].width * .5f + .3f;
            float right = source < 0 ? destination.x + 3 : world.Ledges[source].x + world.Ledges[source].width * .5f - .3f;
            for (float launch = left; launch <= right + .001f; launch += .15f)
                for (int aim = -1; aim <= 1; aim++)
                {
                    float aimX = destination.x + aim * (destination.width * .5f - .4f), x = launch, y = sourceY, velocity = 10.7f;
                    for (int frame = 0; frame < 100; frame++)
                    {
                        x = Mathf.MoveTowards(x, aimX, game.PlayerMoveSpeed * .04f); velocity -= 25 * .04f;
                        float next = y + velocity * .04f, supportY = TowerWorld.Ground; int support = -1;
                        if (velocity <= 0)
                            for (int i = 0; i < world.Ledges.Length; i++)
                            {
                                var ledge = world.Ledges[i];
                                if (Mathf.Abs(x - ledge.x) < ledge.width * .5f + .18f && y >= ledge.y - .02f && next <= ledge.y && ledge.y > supportY)
                                { supportY = ledge.y; support = i; }
                            }
                        if (next <= supportY)
                        {
                            if (support == target && Mathf.Abs(x - destination.x) <= destination.width * .5f - .25f)
                                return new JumpPlan { source = source, target = target, launch = launch, aim = aimX };
                            break;
                        }
                        y = next;
                    }
                }
            return null;
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

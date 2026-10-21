using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Opt-in theme integration fixtures; these captures do not simulate a full combat run.
    public sealed class TowerThemeSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class ThemeReport
        {
            public string mode = "towerThemeSmoke", finishedUtc;
            public bool success, allThemesRendered, isolatedPools, bossesTargetable, scaledStats, distinctSprites, actualFrameTicks, boundaryTransition, retiredRootsInactive;
            public bool bitmapCreaturesVerified, riggedCreaturesVerified, bitmapBackgroundsVerified, platformEdgesPreserved, noPreparationOverlays, rosterGalleryRendered, galleryFramingVerified;
            public int sampledThemes, sampledSpecies, distinctPixelHashes, sampledFrames, bitmapAtlases, inspectedPlatformEdges, inspectedCreatureRigs;
            public List<string> samples = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        private readonly ThemeReport report = new ThemeReport();
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private TowerGame game; private TowerWorld world; private TowerSmokeDriver capture;
        private string prefix, reportPath; private float deadline; private bool finished, initialized, quitOnComplete = true;
        public bool IsFinished { get { return finished; } }
        public bool Succeeded { get { return finished && report.success; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Enter()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-towerThemeSmoke") < 0) return;
            var driver = new GameObject("Tower theme validation").AddComponent<TowerThemeSmokeDriver>();
            driver.Configure(null, null,
                Arg(args, "-towerThemeCapture") ?? Path.Combine(Application.persistentDataPath, "tower-themes"),
                Arg(args, "-towerThemeReport") ?? Path.Combine(Application.persistentDataPath, "tower-themes.json"), true);
        }
        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver, string capturePrefix, string outputReportPath, bool quitOnComplete = false)
        {
            if (tower == null) throw new ArgumentNullException(nameof(tower));
            if (captureDriver == null) throw new ArgumentNullException(nameof(captureDriver));
            Configure(tower, captureDriver, capturePrefix, outputReportPath, quitOnComplete);
        }
        private void Configure(TowerGame tower, TowerSmokeDriver captureDriver, string capturePrefix, string outputReportPath, bool quit)
        {
            if (initialized) throw new InvalidOperationException("Create a new theme driver for each suite run.");
            if (!Path.IsPathRooted(capturePrefix) || !Path.IsPathRooted(outputReportPath)) throw new ArgumentException("Theme output paths must be absolute.");
            initialized = true; game = tower; capture = captureDriver; prefix = capturePrefix; reportPath = outputReportPath; quitOnComplete = quit;
            deadline = Time.realtimeSinceStartup + 180; Application.logMessageReceived += RecordLog;
        }
        private IEnumerator Start() { yield return Guard(Suite()); }
        private static string Arg(string[] args, string key) { int at = Array.IndexOf(args, key); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private void RecordLog(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message + "\n" + trace); }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }
        private void Update()
        { if (initialized && !finished && Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Finish(new TimeoutException("Theme suite exceeded 180 seconds.")); } }
        private object Call(string name, params object[] values) { return typeof(TowerGame).GetMethod(name, Hidden).Invoke(game, values); }
        private Transform EnvironmentRoot { get { return (Transform)typeof(TowerWorld).GetField("environment", Hidden).GetValue(world); } }
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
            Require(Path.IsPathRooted(prefix) && Path.IsPathRooted(reportPath), "Theme output paths must be absolute.");
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            if (game == null) game = FindFirstObjectByType<TowerGame>();
            for (int frame = 0; frame < 180 && game == null; frame++) { yield return null; game = FindFirstObjectByType<TowerGame>(); }
            Require(game != null, "The roguelike game did not load.");
            game.SmokeControlled = true; game.SuppressProgressPersistence = true; game.intro = false; game.enabled = false;
            world = (TowerWorld)typeof(TowerGame).GetField("world", Hidden).GetValue(game);
            capture = game.GetComponent<TowerSmokeDriver>(); Require(capture != null, "Shared HUD capture host is missing."); capture.PrepareForSuite(game);
            var hashes = new HashSet<ulong>(); var atlases = new HashSet<Texture>();
            foreach (var theme in TowerThemeCatalog.All)
            {
                foreach (var monster in theme.Monsters) CheckSprite(theme.Id, monster.Variant, false, hashes, atlases);
                CheckSprite(theme.Id, 0, true, hashes, atlases);
                SetFixture(theme, 10);
                Require(world.CurrentTheme == theme.Id && world.CurrentFloor == 10, "Map did not follow the requested theme.");
                Require(EnvironmentRoot != null && EnvironmentRoot.gameObject.activeInHierarchy && EnvironmentRoot.GetComponentsInChildren<SpriteRenderer>().Length > 10, "Theme map is empty or inactive.");
                VerifyEnvironmentArt(theme);
                VerifyActors(theme, 10);
                // Run actual game frames with attacks held off, then arrange the inspection capture.
                game.enabled = true; game.invulnerable = 1000; float beforeTick = game.elapsed;
                for (int frame = 0; frame < 6; frame++) { yield return null; report.sampledFrames++; }
                Require(game.elapsed > beforeTick, "The actual game update did not tick in the theme fixture.");
                game.enabled = false; StageCapture(); world.Tick(.4f, false);
                yield return capture.CaptureForSuite(prefix, theme.Id.ToString().ToLowerInvariant());
                report.screenshots.Add(prefix + "-" + theme.Id.ToString().ToLowerInvariant() + ".png");
                yield return CaptureRoster(theme);
                report.sampledThemes++;
            }
            Require(hashes.Count == 49 && report.sampledSpecies == 49, "Every regular species and boss must have distinct nonempty pixels.");
            report.distinctPixelHashes = hashes.Count; report.distinctSprites = true;
            Require(atlases.Count == 7, "Every theme must use its own bitmap art atlas.");
            report.bitmapAtlases = atlases.Count; report.bitmapCreaturesVerified = true;
            report.riggedCreaturesVerified=report.inspectedCreatureRigs==(TowerDifficulty.RegularCount(10)+2+7)*TowerThemeCatalog.All.Count;
            Require(report.riggedCreaturesVerified,"Every production creature and roster entry must use its own visible weighted rig.");
            yield return CheckBoundary();
            report.actualFrameTicks = report.sampledFrames == 42;
            report.allThemesRendered = report.sampledThemes == 7 && report.screenshots.Count == 14;
            report.bitmapBackgroundsVerified = report.platformEdgesPreserved = report.noPreparationOverlays = report.rosterGalleryRendered = report.galleryFramingVerified = true;
            report.isolatedPools = report.bossesTargetable = report.scaledStats = true;
            Finish(null);
        }
        private void ClearActors()
        {
            foreach (var actor in game.enemies) if (actor.root) { actor.root.SetActive(false); Destroy(actor.root); }
            game.enemies.Clear();
        }
        private void SetFixture(TowerThemeDefinition theme, int floor)
        {
            game.enabled = false;
            if (game.hero != null && game.hero.root) game.hero.root.SetActive(false);
            foreach (var actor in game.allies) if (actor.root) actor.root.SetActive(false);
            ClearActors(); game.NewRun(20031); game.StartClimb(); ClearActors();
            typeof(TowerRun).GetField("<Floor>k__BackingField", Hidden).SetValue(game.run, floor);
            typeof(TowerRun).GetField("<Theme>k__BackingField", Hidden).SetValue(game.run, theme);
            var oldEnvironment = EnvironmentRoot; Call("SpawnFloor");
            Require(!oldEnvironment || !oldEnvironment.gameObject.activeSelf, "Old environment remained active after SetTheme.");
            game.intro = game.menu = game.party = game.help = false; game.MoveOverride = 0;
            game.AttackOverride = game.JumpOverride = game.DashOverride = game.FocusOverride = false;
            foreach (var actor in game.enemies) { actor.stunTime = actor.cooldown = 999; actor.windup = 0; }
        }
        private void VerifyActors(TowerThemeDefinition theme, int floor)
        {
            var variants = new HashSet<int>(); int regular = 0, bosses = 0, flyers=0;
            foreach (var actor in game.enemies)
            {
                Require(actor.theme == theme.Id && actor.monster != null && actor.id == actor.monster.Id, "Actor came from another theme pool.");
                Require(game.CanTarget(actor, game.run.Player), "A theme fixture rejects the melee-only player.");
                if (actor.isBoss)
                {
                    bosses++; Require(System.Object.ReferenceEquals(actor.monster, theme.Boss), "Wrong theme boss.");
                    Near(actor.maxHp, TowerDifficulty.BossHealth(floor) * theme.Boss.HealthMultiplier, "boss health");
                    foreach (var definition in TowerRun.Definitions) Require(game.CanTarget(actor, new CompanionState(definition)), "An ally cannot target a grounded theme boss.");
                }
                else
                {
                    if(actor.airborne)flyers++;else regular++;
                    bool owned = false; foreach (var monster in theme.Monsters) owned |= System.Object.ReferenceEquals(actor.monster, monster);
                    Require(owned, "Regular monster is outside the active pool."); variants.Add(actor.monster.Variant);
                    Near(actor.maxHp, (actor.kind == 3 ? 52 : actor.kind == 0 ? 60 : actor.kind == 1 ? 48 : 80) * TowerDifficulty.HealthMultiplier(floor) * actor.monster.HealthMultiplier, "regular health");
                }
                Near(actor.damageScale, TowerDifficulty.DamageMultiplier(floor) * actor.monster.DamageMultiplier, "damage");
                Near(actor.moveScale, TowerDifficulty.MoveMultiplier(floor) * actor.monster.MoveMultiplier, "movement");
                Near(actor.bulletSpeedScale, TowerDifficulty.BulletMultiplier(floor) * actor.monster.BulletMultiplier, "bullet speed");
                Near(actor.attackRateScale, TowerDifficulty.AttackRateMultiplier(floor) * actor.monster.AttackRateMultiplier, "attack cadence");
                VerifyCreatureRig(actor.art,TowerThemeMonsterArt.Get(theme.Id,actor.monster.Variant,actor.isBoss));
            }
            Require(regular == TowerDifficulty.RegularCount(floor) && variants.Count == 6 && bosses == 1 && flyers==1, "Floor 10 did not instantiate six species, one flyer and one boss.");
            report.samples.Add(theme.Id + ": floor=" + floor + ", regular=" + regular + ", variants=" + variants.Count + ", bosses=" + bosses);
        }
        private Renderer VerifyCreatureRig(TowerActorArt art,Sprite expected)
        {
            var rig=art.GetComponent<TowerMonsterRig>();
            Require(rig!=null&&rig.SourceSprite==expected&&rig.BoneCount==14&&rig.WeightedBoneCount>=4,"The actor does not have the expected creature's weighted skeleton: "+expected.name);
            var renderer=rig.Renderer;
            Require(renderer is MeshRenderer&&renderer.enabled&&renderer.gameObject.activeInHierarchy,"The creature mesh is not rendering: "+expected.name);
            Require(renderer.sharedMaterial!=null&&renderer.sharedMaterial.mainTexture==expected.texture&&expected.texture.filterMode==FilterMode.Point,"The creature rig changed its source pixels: "+expected.name);
            Require(renderer.bounds.size.y>.3f&&renderer.bounds.size.y<5,"Invalid rendered creature height: "+expected.name);
            var filter=renderer.GetComponent<MeshFilter>();
            Require(filter!=null&&filter.sharedMesh!=null&&filter.sharedMesh.vertexCount>100&&filter.sharedMesh.triangles.Length>100,"The creature rig has no renderable skin: "+expected.name);
            var uv=filter.sharedMesh.uv;Require(uv.Length==filter.sharedMesh.vertexCount,"The skin is missing source UVs: "+expected.name);
            Vector2 minimum=Vector2.one*float.PositiveInfinity,maximum=Vector2.one*float.NegativeInfinity;
            foreach(var coordinate in uv)
            {
                Require(!float.IsNaN(coordinate.x)&&!float.IsNaN(coordinate.y)&&!float.IsInfinity(coordinate.x)&&!float.IsInfinity(coordinate.y),"Nonfinite creature UV: "+expected.name);
                minimum=Vector2.Min(minimum,coordinate);maximum=Vector2.Max(maximum,coordinate);
            }
            Rect source=expected.rect;
            Require(Vector2.Distance(minimum,new Vector2(source.xMin/expected.texture.width,source.yMin/expected.texture.height))<.00001f&&
                Vector2.Distance(maximum,new Vector2(source.xMax/expected.texture.width,source.yMax/expected.texture.height))<.00001f,"The visible skin samples another atlas species: "+expected.name);
            foreach(var sprite in art.GetComponentsInChildren<SpriteRenderer>())
                Require(!sprite.enabled,"A flat creature sprite renders over its weighted skin: "+expected.name);
            report.inspectedCreatureRigs++;
            return renderer;
        }
        private void StageCapture()
        {
            game.hero.pos = new Vector2(-10, TowerWorld.Ground); game.hero.root.transform.position = game.hero.pos;
            int ordinal = 0;
            foreach (var actor in game.enemies)
            {
                actor.pos = new Vector2(actor.isBoss ? 11.5f : -6 + ordinal++ * 2.3f, TowerWorld.Ground);
                actor.root.transform.position = actor.pos; actor.art.SetPose(false, false, false, -1, .05f);
            }
            game.view.orthographicSize = 7.5f; game.view.transform.position = new Vector3(1, .7f, -10);
            game.Notice(game.run.Theme.Name + " · 일반 6종 / 전용 보스 · 테마 검증 배치", 10);
        }
        private void VerifyEnvironmentArt(TowerThemeDefinition theme)
        {
            var texture = TowerThemeMonsterArt.SourceAtlas(theme.Id);
            bool background = false;
            var renderers = EnvironmentRoot.GetComponentsInChildren<SpriteRenderer>();
            foreach (var renderer in renderers)
                if (renderer.sprite != null && renderer.sprite.texture == texture && renderer.bounds.size.x > 15 && renderer.bounds.size.y > 5) background = true;
            Require(background, "The new theme bitmap background was not instantiated: " + theme.Id);
            Require(world.Ledges.Length == world.CurrentPattern.Platforms.Count, "Art changes altered the platform pattern count.");
            for (int i = 0; i < world.Ledges.Length; i++)
            {
                var ledge = world.Ledges[i]; var expected = world.CurrentPattern.Platforms[i];
                Near(ledge.x, expected.X, "platform x"); Near(ledge.y, expected.Y, "platform y"); Near(ledge.width, expected.Width, "platform width");
                bool edge = false;
                foreach (var renderer in renderers)
                    if (renderer.name == theme.Id + " platform walkable surface" && Mathf.Abs(renderer.bounds.center.x - ledge.x) < .015f && Mathf.Abs(renderer.bounds.max.y - ledge.y) < .015f && Mathf.Abs(renderer.bounds.size.x - ledge.width) < .015f) edge = true;
                Require(edge, "The visible landing edge no longer matches its collision plane: " + theme.Id + " / " + i);
                report.inspectedPlatformEdges++;
            }
            foreach (var item in game.GetComponentsInChildren<Transform>(true))
                Require(item.name != "Locked warning dash" && !item.name.EndsWith(" warning", StringComparison.OrdinalIgnoreCase) && !item.name.StartsWith("Enemy telegraph", StringComparison.OrdinalIgnoreCase), "Removed path or danger preview returned: " + item.name);
        }
        private IEnumerator CaptureRoster(TowerThemeDefinition theme)
        {
            // An explicit inspection gallery uses the same production rigs at a larger
            // presentation scale. It does not claim to simulate a normal combat arrangement.
            var saved = new List<TowerGame.Fighter>(game.enemies); var active = new List<bool>(); var positions = new List<Vector2>();
            foreach (var actor in saved) { active.Add(actor.root.activeSelf); positions.Add(actor.pos); actor.root.SetActive(false); }
            game.enemies.Clear(); bool heroActive = game.hero.root.activeSelf; game.hero.root.SetActive(false);
            EnvironmentRoot.gameObject.SetActive(false);
            var gallery = new GameObject(theme.Id + " bitmap roster inspection").transform; gallery.SetParent(game.transform, false);
            float previousAspect = game.view.aspect;
            // The editor Game view can retain a different aspect from the offscreen target.
            // Fit the row to the exact capture dimensions with a safe margin on both sides.
            game.view.aspect = Screen.width / (float)Screen.height;
            game.view.orthographicSize = 4.7f; game.view.transform.position = new Vector3(0, .9f, -10);
            game.view.backgroundColor = new Color(.035f, .045f, .075f);
            float rowWidth = game.view.orthographicSize * 2 * game.view.aspect * .88f;
            float cellWidth = rowWidth / 7;
            for (int column = 0; column < 7; column++)
            {
                bool boss = column == 6; var sprite = TowerThemeMonsterArt.Get(theme.Id, boss ? 0 : column, boss);
                var item=new GameObject(TowerThemeMonsterArt.Name(theme.Id,column,boss)).transform;
                item.SetParent(gallery,false);
                var art=TowerActorArt.Create(item,boss?4:theme.Monsters[column].Kind,true);art.SetThemeMonster(theme.Id,boss?0:column,boss);
                art.SetLocomotion(true,0,false);art.SetPose(false,false,false,-1,.05f);
                var renderer=VerifyCreatureRig(art,sprite);
                float scale = Mathf.Min(cellWidth * .84f / renderer.bounds.size.x, (boss ? 2.65f : 2.25f) / renderer.bounds.size.y);
                item.localScale = new Vector3(scale, scale, 1);
                float x = -rowWidth * .5f + (column + .5f) * cellWidth;
                item.localPosition = new Vector3(x, -.7f, 0);
                Vector3 minimum = game.view.WorldToViewportPoint(renderer.bounds.min), maximum = game.view.WorldToViewportPoint(renderer.bounds.max);
                Require(minimum.x > .045f && maximum.x < .955f && minimum.y > .2f && maximum.y < .8f, "A roster silhouette falls outside the inspection frame: " + item.name);
                // Reuse production world labels beneath the gallery's rigged creatures. Roots
                // stay hidden; proxy positions are restored before leaving this fixture.
                foreach (var actor in saved)
                {
                    if (actor.isBoss != boss || (!boss && actor.monster.Variant != column)) continue;
                    float offset = boss ? 3.1f : actor.kind == 3 ? 1.8f : actor.kind == 2 ? 1.6f : 1.2f;
                    actor.pos = new Vector2(x, -1.35f - offset); game.enemies.Add(actor); break;
                }
            }
            game.Notice(theme.Name + " · 외형 확대 검수 · 왼쪽부터 일반 6종 / 전용 보스", 10);
            string suffix = theme.Id.ToString().ToLowerInvariant() + "-roster";
            yield return capture.CaptureForSuite(prefix, suffix); report.screenshots.Add(prefix + "-" + suffix + ".png");
            gallery.gameObject.SetActive(false); Destroy(gallery.gameObject);
            game.enemies.Clear(); game.enemies.AddRange(saved);
            for (int i = 0; i < saved.Count; i++) { saved[i].pos = positions[i]; saved[i].root.SetActive(active[i]); }
            game.view.aspect = previousAspect;
            game.hero.root.SetActive(heroActive); EnvironmentRoot.gameObject.SetActive(true);
        }
        private IEnumerator CheckBoundary()
        {
            SetFixture(TowerThemeCatalog.Get(TowerThemeId.Lava), 5);
            var oldTheme = game.run.Theme; var oldEnvironment = EnvironmentRoot; var retired = new List<GameObject>();
            foreach (var actor in game.enemies) retired.Add(actor.root);
            ClearActors(); Require(game.run.ClearFloor() && game.run.ChooseOffer(game.run.Offers[0].Id), "Boundary fixture could not clear and choose a reward.");
            game.NextFloor();
            Require(game.run.Floor == 6 && game.run.Theme.Id != oldTheme.Id, "Actual next-floor flow did not exclude the previous theme.");
            Require(world.CurrentTheme == game.run.Theme.Id && world.CurrentFloor == 6, "Next floor map did not follow its newly selected theme.");
            Require(!oldEnvironment || !oldEnvironment.gameObject.activeSelf, "Previous environment remained active at the boundary.");
            foreach (var root in retired) Require(!root || !root.activeSelf, "Previous theme actor remained active.");
            int airborne = 0;
            foreach (var actor in game.enemies)
            {
                Require(actor.theme == game.run.Theme.Id && !actor.isBoss, "Old pool or extra boss leaked into floor 6.");
                if (actor.airborne)
                {
                    airborne++;
                    Require(System.Object.ReferenceEquals(actor.monster, game.run.Theme.Monsters[game.run.Theme.AirborneVariant]), "Unexpected airborne species after the boundary reward.");
                }
            }
            Require(airborne == 1 && game.enemies.Count == TowerDifficulty.RegularCount(6) + airborne, "The new theme did not spawn its flying monster independently of party composition.");
            yield return null;
            foreach (var root in retired) Require(!root, "Retired actor was not destroyed by the next frame.");
            Require(!oldEnvironment, "Retired environment was not destroyed by the next frame.");
            report.boundaryTransition = report.retiredRootsInactive = true;
        }
        private void CheckSprite(TowerThemeId theme, int variant, bool boss, HashSet<ulong> hashes, HashSet<Texture> atlases)
        {
            var sprite = TowerThemeMonsterArt.Get(theme, variant, boss);
            Require(sprite != null && sprite.bounds.size.y > 0 && sprite.texture.filterMode == FilterMode.Point, "Missing pixel creature sprite.");
            var texture = sprite.texture; var previous = RenderTexture.active;
            var source = TowerThemeMonsterArt.SourceAtlas(theme);
            Require(source != null && source.width >= 1024 && source.height >= 512, "The generated bitmap creature atlas was not loaded: " + sprite.name);
            atlases.Add(source);
            var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D pixels = null;
            try
            {
                Graphics.Blit(texture, target); RenderTexture.active = target;
                Rect rect = sprite.rect;
                pixels = new Texture2D(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), TextureFormat.RGBA32, false);
                pixels.ReadPixels(rect, 0, 0); pixels.Apply();
                ulong hash = 14695981039346656037UL; int opaque = 0, transparent = 0;
                foreach (var color in pixels.GetPixels32())
                {
                    if (color.a > 0) opaque++; else transparent++;
                    unchecked { hash = (hash ^ color.r) * 1099511628211UL; hash = (hash ^ color.g) * 1099511628211UL; hash = (hash ^ color.b) * 1099511628211UL; hash = (hash ^ color.a) * 1099511628211UL; }
                }
                Require(opaque > 200 && transparent > 20 && hashes.Add(hash), "Empty, rectangular or duplicated creature pixels: " + sprite.name); report.sampledSpecies++;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); if (pixels) Destroy(pixels); }
        }
        private static void Near(float actual, float expected, string label) { Require(Mathf.Abs(actual - expected) < .015f, "Incorrect " + label + ": " + actual + " != " + expected); }
        private void Finish(Exception error)
        {
            if (finished) return; finished = true;
            if (error != null) report.errors.Add(error is TargetInvocationException && error.InnerException != null ? error.InnerException.ToString() : error.ToString());
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0;
            if (game != null) game.enabled = false;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= RecordLog;
            if (quitOnComplete) Application.Quit(report.success ? 0 : 1);
            else Debug.Log("Tower theme suite " + (report.success ? "PASS: " : "FAIL: ") + reportPath);
        }
    }
}

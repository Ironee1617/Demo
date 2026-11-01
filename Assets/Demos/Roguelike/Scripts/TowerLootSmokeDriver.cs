using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Explicit validation of the breakable caches and gold: every theme's painted caches stand on its floors, the
    // production sword breaks them, hearts and coins scatter, land and are picked up, coins fly in after a clear and
    // are banked on leaving, and the merchant sells for gold. The saved account is never written.
    public sealed class TowerLootSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class LootReport
        {
            public string mode = "towerLootSmoke", finishedUtc;
            public bool success, everyThemePlaced, paintedSprites, standOnSupports, fragileBreaksAtOnce, sturdyTakesTwo, heavyBreaksSturdy;
            public bool lootLandsDuringFight, pickupsCollected, fullHealthLeavesHeart, woundedTakesHeart, coinsFlyAfterClear, looseCoinsBanked;
            public bool peacefulFloorsHaveNoCaches, shopRefusesWithoutGold, shopSellsForGold, oathTrialHasNoCaches, villageHasNoCaches, savedDataUntouched;
            public int themes, caches, cachesBroken, coins, hearts, goldCollected, goldFundedByFixture;
            public float heartHealed;
            public List<string> checks = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        private const string ProgressKey = "Tower.MythProgress.v1";
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly LootReport report = new LootReport();
        private TowerGame game; private TowerSmokeDriver capture;
        private TowerProgress originalProgress;
        private string prefix, reportPath, originalSaved; private bool finishing, quitOnComplete;
        private float deadline;

        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver, string capturePrefix, string outputReportPath, bool quitOnComplete = false)
        {
            if (game != null) throw new InvalidOperationException("Create a new loot driver for each suite run.");
            if (tower == null) throw new ArgumentNullException(nameof(tower));
            if (captureDriver == null) throw new ArgumentNullException(nameof(captureDriver));
            if (!Path.IsPathRooted(capturePrefix) || !Path.IsPathRooted(outputReportPath)) throw new ArgumentException("Suite capture and report paths must be absolute.");
            game = tower; capture = captureDriver; prefix = capturePrefix; reportPath = outputReportPath; this.quitOnComplete = quitOnComplete;
            originalProgress = game.progress; originalSaved = PlayerPrefs.GetString(ProgressKey, "");
            capture.PrepareForSuite(tower); game.SuppressProgressPersistence = true; game.SmokeControlled = true; game.LootFixture = true; enabled = true;
            Application.logMessageReceived += RecordLog; deadline = Time.realtimeSinceStartup + 180;
            StartCoroutine(Guard(Suite()));
        }
        private void RecordLog(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(condition + "\n" + trace); }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }
        private void Update()
        { if (!finishing && Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Finish(new TimeoutException("Loot suite exceeded 180 seconds.")); } }
        private object Call(string method, params object[] args)
        { return typeof(TowerGame).GetMethod(method, Hidden).Invoke(game, args); }
        private IEnumerator Guard(IEnumerator body)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(body);
            while (stack.Count > 0)
            {
                bool more = false; object value = null; Exception error = null;
                try { more = stack.Peek().MoveNext(); if (more) value = stack.Peek().Current; } catch (Exception ex) { error = ex; }
                if (error != null) { Finish(error); yield break; }
                if (!more) { stack.Pop(); continue; }
                var nested = value as IEnumerator; if (nested != null) stack.Push(nested); else yield return value;
            }
        }
        private IEnumerator Capture(string suffix)
        { yield return capture.CaptureForSuite(prefix, suffix); report.screenshots.Add(prefix + "-" + suffix + ".png"); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        private IEnumerator Suite()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            game.progress = new TowerProgress();
            yield return null;
            yield return Themes();
            yield return BreakAndCollect();
            yield return Hearts();
            yield return ClearedFloorCoins();
            yield return HeavyBlow();
            yield return Shop();
            Exclusions();
            Finish(null);
        }

        // A fresh run standing on the given floor, its monsters frozen and the hero untouchable.
        private void StartFloor(int seed, int floor, FloorType type)
        {
            report.cachesBroken += game.CachesBroken;
            game.NewRun(seed);
            foreach (var enemy in game.enemies) if (enemy.root) { enemy.root.SetActive(false); Destroy(enemy.root); }
            game.enemies.Clear();
            typeof(TowerRun).GetProperty("Floor").SetValue(game.run, floor);
            typeof(TowerRun).GetProperty("FloorType").SetValue(game.run, type);
            Call("SpawnFloor");
            game.intro = game.menu = game.party = game.help = false; game.noticeTime = 0;
            game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = game.DropOverride = false;
            Freeze();
        }
        // Stray sword blows near a cache must not kill the floor's monsters and clear it early.
        private void Freeze()
        { game.invulnerable = 999; foreach (var enemy in game.enemies) { enemy.stunTime = enemy.cooldown = 999; enemy.hp = enemy.maxHp = 1e6f; } }
        private void Place(Vector2 at, int facing)
        {
            var hero = game.hero; hero.pos = at; hero.velocityY = 0; hero.grounded = true; hero.facing = facing;
            hero.support = World.SupportAt(at.x, at.y); hero.lastX = at.x; hero.root.transform.position = at;
        }
        private TowerWorld World { get { return (TowerWorld)typeof(TowerGame).GetField("world", Hidden).GetValue(game); } }
        // Stands the hero on the cache's own surface just short of it, facing it.
        private void StandBeside(TowerGame.Cache cache, float gap = 1.1f)
        {
            float x = cache.pos.x - gap; int facing = 1;
            if (cache.support >= 0 && !World.Ledges[cache.support].Contains(x)) { x = cache.pos.x + gap; facing = -1; }
            if (x < TowerWorld.Left + 1) { x = cache.pos.x + gap; facing = -1; }
            Place(new Vector2(x, World.SurfaceHeight(cache.support, x)), facing);
        }
        private void Slash() { Call("PlayerSlash"); }
        private IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        private IEnumerator Until(Func<bool> condition, float seconds, string failure)
        {
            float limit = Time.realtimeSinceStartup + seconds;
            while (!condition()) { Require(Time.realtimeSinceStartup < limit, failure); yield return null; }
        }

        private IEnumerator Themes()
        {
            var seen = new HashSet<TowerThemeId>();
            report.paintedSprites = report.standOnSupports = true;
            for (int chapter = 0; chapter < 9; chapter++)
            {
                int floor = chapter * TowerThemeCatalog.FloorsPerTheme + 2;
                StartFloor(40 + chapter, floor, FloorType.Normal);
                var theme = game.run.Theme.Id;
                Require(game.caches.Count >= TowerLoot.MinCaches && game.caches.Count <= TowerLoot.MaxCaches, "Floor " + floor + " placed " + game.caches.Count + " caches.");
                foreach (var cache in game.caches)
                {
                    report.paintedSprites &= cache.renderer.sprite != null && cache.renderer.sprite.name == cache.definition.Id && cache.renderer.sprite.texture.width > 32;
                    report.standOnSupports &= Mathf.Abs(cache.pos.y - World.SurfaceHeight(cache.support, cache.pos.x)) < .01f && cache.definition.Theme == theme;
                }
                Require(report.paintedSprites && report.standOnSupports, "A " + theme + " cache is unpainted, off its support or from another theme.");
                report.caches += game.caches.Count; seen.Add(theme); report.themes++;
                report.checks.Add(theme + " floor " + floor + ": " + game.caches.Count + " caches PASS");
                // Frame the nearest cache with its neighbour for the screenshot.
                var first = game.caches[0]; foreach (var cache in game.caches) if (cache.pos.x < first.pos.x) first = cache;
                StandBeside(first, 2.4f);
                yield return Frames(30);
                yield return Capture("theme-" + theme.ToString().ToLowerInvariant());
            }
            report.everyThemePlaced = seen.Count == 9;
            Require(report.everyThemePlaced, "Not every tower theme placed its caches.");
        }

        private IEnumerator BreakAndCollect()
        {
            // Floor three of seed seven: break a cache while its monsters still stand.
            StartFloor(7, 3, FloorType.Normal);
            var target = game.caches.Find(c => !c.definition.Sturdy) ?? game.caches[0];
            int count = game.caches.Count, broken = game.CachesBroken;
            StandBeside(target); Slash();
            if (target.definition.Sturdy)
            {
                report.sturdyTakesTwo = game.caches.Count == count && target.hits == 1;
                Require(report.sturdyTakesTwo, "A sturdy cache broke from one ordinary blow."); Slash();
            }
            else report.fragileBreaksAtOnce = true;
            Require(game.caches.Count == count - 1 && game.CachesBroken == broken + 1 && game.pickups.Count > 0, "The sword did not break the cache into loot.");
            int value = 0, hearts = 0, scattered = game.pickups.Count; foreach (var p in game.pickups) { value += TowerLoot.GoldOf(p.kind); if (p.kind == LootKind.Heart) hearts++; }
            // Step back out of the magnet's reach; with monsters standing the loot waits where it fell.
            float side = target.pos.x - 6 > TowerWorld.Left + 1 ? -6 : 6;
            float x = target.pos.x + side; Place(new Vector2(x, World.GroundAt(x)), side < 0 ? 1 : -1);
            int gold = game.progress.Gold;
            yield return Until(() => game.pickups.TrueForAll(p => p.landed), 4, "Scattered loot did not come to rest.");
            yield return Frames(20);
            report.lootLandsDuringFight = game.progress.Gold == gold && game.pickups.Count == scattered && !game.run.IsFloorCleared;
            Require(report.lootLandsDuringFight, "Loot was collected from across the floor while monsters stood.");
            yield return Capture("scattered-loot");
            // Walk onto each piece: coins pay their value, a heart heals the wounded hero.
            game.hero.hp = game.PlayerMaxHealth * .5f; float wounded = game.hero.hp;
            for (int guard = 0; guard < 40 && game.pickups.Count > 0; guard++)
            {
                var p = game.pickups[0]; float y = p.pos.y - TowerGame.RestHeight(p.kind);
                Place(new Vector2(p.pos.x, y), 1);
                yield return Frames(3);
            }
            Require(game.pickups.Count == 0, "Loot under the hero was not picked up.");
            report.pickupsCollected = game.progress.Gold == gold + value && (hearts == 0 || game.hero.hp > wounded);
            report.coins += value; report.hearts += hearts;
            Require(report.pickupsCollected, "Pickups did not pay " + value + " gold or heal: " + game.progress.Gold + " vs " + (gold + value));
            yield return Frames(4);
            yield return Capture("collected");
        }
        private static int CoinsIn(List<TowerGame.Pickup> pickups) { int n = 0; foreach (var p in pickups) if (p.kind != LootKind.Heart) n++; return n; }

        private IEnumerator Hearts()
        {
            // A full-health hero leaves a heart lying; a wounded one takes it.
            game.hero.hp = game.PlayerMaxHealth;
            var heart = game.DropLoot(LootKind.Heart, game.hero.pos + Vector2.up * 1.4f, new System.Random(3));
            yield return Until(() => heart.landed, 3, "A dropped heart did not land.");
            yield return Frames(10);
            report.fullHealthLeavesHeart = game.pickups.Contains(heart) && game.hero.hp >= game.PlayerMaxHealth - .01f;
            Require(report.fullHealthLeavesHeart, "A full-health hero swallowed a heart.");
            Place(new Vector2(heart.pos.x, heart.pos.y - TowerGame.RestHeight(LootKind.Heart)), 1);
            game.hero.hp = game.PlayerMaxHealth * .5f; float before = game.hero.hp;
            yield return Until(() => !game.pickups.Contains(heart), 2, "A wounded hero standing on a heart did not take it.");
            report.heartHealed = game.hero.hp - before;
            report.woundedTakesHeart = Mathf.Abs(report.heartHealed - TowerLoot.HeartHeal(game.PlayerMaxHealth)) < .05f;
            Require(report.woundedTakesHeart, "A heart did not heal twelve percent of max health: " + report.heartHealed);
            report.hearts++;
        }

        private IEnumerator ClearedFloorCoins()
        {
            // Break the floor's last caches, retreat, then clear the floor: landed coins fly to the hero.
            game.hero.hp = game.PlayerMaxHealth;
            int gold = game.progress.Gold;
            while (game.caches.Count > 0) { var cache = game.caches[0]; StandBeside(cache); Slash(); if (game.caches.Contains(cache)) Slash(); }
            int value = 0; foreach (var p in game.pickups) value += TowerLoot.GoldOf(p.kind);
            float x = TowerWorld.Left + 1; Place(new Vector2(x, World.GroundAt(x)), 1);
            yield return Until(() => game.pickups.TrueForAll(p => p.landed || p.kind == LootKind.Heart), 4, "Loot of the last caches did not land.");
            Require(game.progress.Gold == gold, "Loot was collected from across the floor before the clear.");
            foreach (var enemy in game.enemies) if (enemy.root) { enemy.root.SetActive(false); Destroy(enemy.root); }
            game.enemies.Clear();
            Require(game.RemainingEnemies == 0, "Monsters were still queued on a fixture floor.");
            yield return Until(() => game.run.IsFloorCleared, 2, "The floor did not clear without monsters.");
            // Every floor's card screen pauses the floor; the coins fly in once a card is taken.
            if (game.run.IsRewardPending) game.SelectOffer(game.run.Offers[0].Id);
            yield return Until(() => CoinsIn(game.pickups) == 0, 6, "Coins did not fly to the hero after the clear.");
            report.coinsFlyAfterClear = game.progress.Gold == gold + value; report.coins += value;
            Require(report.coinsFlyAfterClear, "Coins that flew in after the clear did not pay their value.");
            // Coins still in the air when the hero leaves are banked, not lost.
            var random = new System.Random(5); int loose = 0;
            for (int i = 0; i < 4; i++) { var coin = game.DropLoot(i == 0 ? LootKind.GoldCoin : LootKind.Coin, game.hero.pos + new Vector2(8, 2), random); loose += TowerLoot.GoldOf(coin.kind); }
            gold = game.progress.Gold;
            if (game.run.IsRewardPending) game.SelectOffer(game.run.Offers[0].Id);
            game.NextFloor();
            report.looseCoinsBanked = game.run.Floor == 4 && game.pickups.Count == 0 && game.progress.Gold == gold + loose;
            Require(report.looseCoinsBanked, "Coins left in flight were lost on the stairs.");
            report.goldCollected = game.GoldCollected;
        }

        private TowerGame.Cache FindSturdy(int firstSeed, int floor)
        {
            for (int seed = firstSeed; seed < firstSeed + 30; seed++)
            {
                StartFloor(seed, floor, FloorType.Normal);
                var sturdy = game.caches.Find(c => c.definition.Sturdy);
                if (sturdy != null) return sturdy;
            }
            throw new InvalidOperationException("No sturdy cache appeared on thirty floors.");
        }
        private IEnumerator HeavyBlow()
        {
            // An ordinary blow only cracks a sturdy container; the second one breaks it.
            var sturdy = FindSturdy(60, 12); int count = game.caches.Count;
            StandBeside(sturdy); Slash();
            bool cracked = game.caches.Contains(sturdy) && sturdy.hits == 1;
            Slash();
            report.sturdyTakesTwo = cracked && !game.caches.Contains(sturdy) && game.caches.Count == count - 1;
            Require(report.sturdyTakesTwo, "A sturdy cache did not take exactly two ordinary blows.");
            // A combo finisher breaks one at once. The camera settles on it first so the shards are in the shot.
            sturdy = FindSturdy(100, 22); count = game.caches.Count;
            StandBeside(sturdy); yield return Frames(40);
            StandBeside(sturdy); typeof(TowerGame).GetField("slashCombo", Hidden).SetValue(game, 2); Slash();
            report.heavyBreaksSturdy = game.caches.Count == count - 1 && !game.caches.Contains(sturdy);
            Require(report.heavyBreaksSturdy, "A finisher did not break a sturdy cache.");
            yield return Frames(5);
            yield return Capture("heavy-break");
            yield return Frames(30);
        }

        private IEnumerator Shop()
        {
            game.progress = new TowerProgress();
            // Rest and shop floors are quiet stops: no caches to break beside the campfire or the merchant.
            StartFloor(90, 6, FloorType.Rest);
            bool restEmpty = game.caches.Count == 0;
            StartFloor(91, 6, FloorType.Shop);
            report.peacefulFloorsHaveNoCaches = restEmpty && game.caches.Count == 0;
            Require(report.peacefulFloorsHaveNoCaches, "A rest or shop floor placed caches.");
            Require(game.enemies.Count == 0, "A shop floor held monsters.");
            yield return Until(() => game.run.IsFloorCleared, 2, "The shop floor did not clear on arrival.");
            Place(new Vector2(TowerWorld.FeaturePosition.x, World.GroundAt(TowerWorld.FeaturePosition.x)), 1);
            game.hero.hp = game.PlayerMaxHealth * .4f;
            Require(game.InteractMerchant() && game.shopOpen, "The merchant did not open for a nearby hero.");
            report.shopRefusesWithoutGold = !game.BuyShopItem(ShopItem.Heal) && game.progress.Gold == 0 && !game.run.ShopTraded;
            Require(report.shopRefusesWithoutGold, "The merchant sold without gold.");
            game.shopOpen = false;
            // The fixture brings the gold a few battle floors of caches would have paid.
            report.goldFundedByFixture = game.progress.AddGold(TowerRun.HealPrice);
            Place(new Vector2(TowerWorld.FeaturePosition.x, World.GroundAt(TowerWorld.FeaturePosition.x)), 1);
            game.hero.hp = game.PlayerMaxHealth * .4f;
            Require(game.InteractMerchant() && game.shopOpen, "The merchant did not reopen.");
            yield return Frames(4);
            yield return Capture("shop-gold");
            int gold = game.progress.Gold; float hp = game.hero.hp;
            report.shopSellsForGold = game.BuyShopItem(ShopItem.Heal) && game.progress.Gold == gold - TowerRun.HealPrice && game.hero.hp > hp && game.run.ShopTraded;
            Require(report.shopSellsForGold, "The merchant did not sell a heal for gold.");
            yield return Frames(4);
        }

        private void Exclusions()
        {
            report.cachesBroken += game.CachesBroken;
            game.NewRun(5, "michael");
            report.oathTrialHasNoCaches = game.run.IsOathTrial && game.caches.Count == 0;
            Require(report.oathTrialHasNoCaches, "The oath trial placed caches.");
            game.EnterVillage();
            report.villageHasNoCaches = game.village && game.caches.Count == 0;
            Require(report.villageHasNoCaches, "The village placed caches.");
        }

        private void Finish(Exception error)
        {
            if (finishing) return; finishing = true;
            if (error != null) report.errors.Add(error is TargetInvocationException && error.InnerException != null ? error.InnerException.ToString() : error.ToString());
            report.savedDataUntouched = PlayerPrefs.GetString(ProgressKey, "") == originalSaved;
            if (game)
            {
                game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = game.DropOverride = false;
                game.LootFixture = false; game.progress = originalProgress; game.menu = true;
            }
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0 && report.savedDataUntouched;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= RecordLog;
            if (quitOnComplete) Application.Quit(report.success ? 0 : 1);
        }
    }
}

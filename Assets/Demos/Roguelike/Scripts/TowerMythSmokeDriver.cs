using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Explicit validation of the production myth screen, articulated atlas artwork, floor caches and gold shop path.
    public sealed class TowerMythSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class MythReport
        {
            public string mode = "towerMythSmoke", finishedUtc;
            public bool success, openingRecruit, allMythsOffered, floorCaches, goldReceipts, shopPurchase, savedDataUntouched;
            public int atlases, companions, portraits, stateFrames, visibleStateFrames, gameplayClears, cachedFloors;
            public List<string> checks = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        private const string ProgressKey = "Tower.MythProgress.v1";
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly MythReport report = new MythReport();
        private TowerGame game;
        private TowerSmokeDriver capture;
        private TowerProgress originalProgress;
        private bool originalSmoke, originalSuppression, finishing, quitOnFinish;
        private string originalSaved, prefix, reportPath;
        private Camera frameCamera;
        private RenderTexture frameTarget;
        private Texture2D framePixels;
        private GameObject frameRoot;

        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver, bool quitOnFinish = true)
        {
            game = tower; capture = captureDriver; this.quitOnFinish = quitOnFinish;
            string[] args = Environment.GetCommandLineArgs();
            prefix = Argument(args, "-towerMythCapture") ?? Path.Combine(Application.persistentDataPath, "tower-myth");
            reportPath = Argument(args, "-towerMythReport") ?? Path.Combine(Application.persistentDataPath, "tower-myth.json");
            Require(game && capture && Path.IsPathRooted(prefix) && Path.IsPathRooted(reportPath), "A tower, capture host and absolute output paths are required.");
            Directory.CreateDirectory(Path.GetDirectoryName(prefix)); Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            originalProgress = game.progress; originalSmoke = game.SmokeControlled; originalSuppression = game.SuppressProgressPersistence;
            originalSaved = PlayerPrefs.GetString(ProgressKey, "");
            game.SuppressProgressPersistence = true; game.SmokeControlled = true;
            game.enabled = false; capture.PrepareForSuite(game);
            Application.logMessageReceived += Log;
            StartCoroutine(Guard(Suite()));
        }
        private static string Argument(string[] args, string key)
        { int at = Array.IndexOf(args, key); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
        private void Log(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message + "\n" + trace); }
        private object Call(string method, params object[] arguments)
        { return typeof(TowerGame).GetMethod(method, Hidden).Invoke(game, arguments); }
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
        private IEnumerator Capture(string name)
        { yield return capture.CaptureForSuite(prefix, name); report.screenshots.Add(prefix + "-" + name + ".png"); }
        private IEnumerator Suite()
        {
            yield return Library();
            // Every climb opens with a companion-only card set drawn from all mythologies.
            game.progress = new TowerProgress(); game.SmokeControlled = false; game.NewRun(15); game.intro = true;
            yield return Capture("title");
            game.StartClimb();
            report.openingRecruit = !game.intro && game.run.IsRewardPending && game.run.IsRecruitOffer && game.run.Offers.Count == TowerRun.OfferSlots;
            foreach (var offer in game.run.Offers) report.openingRecruit &= offer.Kind == RewardKind.Recruit;
            Require(report.openingRecruit, "A new climb did not open with a companion-only recruit.");
            yield return Capture("opening-recruit");
            var myths = new HashSet<string>();
            for (int seed = 1; seed <= 40; seed++) foreach (var offer in new TowerRun(seed).Offers) myths.Add(TowerRun.GetDefinition(offer.CompanionId).MythId);
            report.allMythsOffered = myths.Count >= 8;
            Require(report.allMythsOffered, "Opening recruits did not span the mythologies.");
            game.SelectOffer(game.run.Offers[0].Id); Require(game.run.Companions.Count == 1 && !game.run.IsRewardPending, "The opening recruit did not join.");
            for (int i = 0; i < 2; i++) { ClearByGameplay(); if (i == 0) NextFloor(); }
            // Gold only comes out of caches; the balance survives a save round trip.
            game.progress.AddGold(37);
            game.progress = TowerProgress.Load(game.progress.Save());
            report.goldReceipts = game.progress.Gold == 37;
            Require(report.goldReceipts, "Reloading the account did not keep its gold.");
            game.NewRun(15); game.intro = false; game.SelectOffer(game.run.Offers[0].Id);
            for (int i = 0; i < 3; i++) { ClearByGameplay(); if (i < 2) NextFloor(); }
            report.floorCaches = game.progress.Gold == 37 && report.gameplayClears == 5 && report.cachedFloors == 5;
            Require(report.floorCaches, "Five production floors did not each place caches, or a clear paid gold by itself.");
            yield return Capture("five-floor-caches");
            // A shop floor clears on its first update and sells for gold.
            game.NewRun(16); game.intro = false; game.SelectOffer(game.run.Offers[0].Id);
            // Respawning the floor in place keeps the first floor's monsters, which a real stairway never carries over.
            foreach (var enemy in game.enemies) if (enemy.root) Destroy(enemy.root);
            game.enemies.Clear(); game.DropPendingSpawns();
            typeof(TowerRun).GetProperty("FloorType").SetValue(game.run, FloorType.Shop); Call("SpawnFloor");
            Require(game.enemies.Count == 0, "A shop floor spawned monsters."); Call("Update");
            Require(game.run.IsFloorCleared && !game.shopOpen && game.progress.Gold == 37, "The shop floor did not clear, paid gold by itself or opened by itself.");
            Require(!game.InteractMerchant() && !game.shopOpen, "The stall opened from across the floor.");
            game.hero.pos = TowerWorld.FeaturePosition; game.hero.root.transform.position = game.hero.pos;
            Require(game.InteractMerchant() && game.shopOpen, "Walking up to the merchant did not open the stall.");
            yield return Capture("shop");
            game.hero.hp = game.hero.maxHp * .4f; int gold = game.progress.Gold; float wounded = game.hero.hp;
            bool bought = game.BuyShopItem(ShopItem.Heal);
            Require(bought && game.progress.Gold == gold - TowerRun.HealPrice && game.hero.hp > wounded && !game.shopOpen,
                "A shop heal did not spend gold, heal and close the stall: bought=" + bought + " gold " + gold + "->" + game.progress.Gold + " hp " + wounded + "->" + game.hero.hp + "/" + game.PlayerMaxHealth + " open=" + game.shopOpen + " traded=" + game.run.ShopTraded + " cleared=" + game.run.IsFloorCleared + " type=" + game.run.FloorType);
            Require(!game.InteractMerchant() && !game.shopOpen && game.run.ShopTraded && game.ToastCount > 0, "The merchant traded twice or did not explain why.");
            report.shopPurchase = true;
            report.savedDataUntouched = PlayerPrefs.GetString(ProgressKey, "") == originalSaved;
            Require(report.savedDataUntouched, "Runtime validation modified the player's saved account.");
            Finish(null);
        }
        private void ClearByGameplay()
        {
            Require(!game.SmokeControlled && game.SuppressProgressPersistence && !game.run.IsFloorCleared, "Floor fixture must use normal reward logic with disk persistence suppressed.");
            game.intro = game.menu = game.party = game.help = false;
            Call("ResetEnemyCombat");
            Require(game.enemies.Count > 0, "Production floor spawned no enemies.");
            Require(game.caches.Count >= TowerLoot.MinCaches && game.caches.Count <= TowerLoot.MaxCaches, "A production floor did not place its breakable caches."); report.cachedFloors++;
            for (int i = game.enemies.Count - 1; i > 0; i--) { if (game.enemies[i].root) Destroy(game.enemies[i].root); game.enemies.RemoveAt(i); }
            // Long floors queue the packs further ahead; the fixture's last enemy stands for the whole floor.
            game.DropPendingSpawns();
            var target = game.enemies[0]; target.hp = 1; target.airborne = false; target.stunTime = target.cooldown = 999;
            game.hero.facing = 1; target.pos = game.hero.pos + Vector2.right; target.root.transform.position = target.pos;
            int before = game.progress.Gold;
            Call("PlayerSlash"); Require(game.enemies.Count == 0, "Actual player slash did not kill the fixture's final enemy.");
            Call("Update");
            Require(game.run.IsFloorCleared && game.progress.Gold == before, "The production clear path paid gold without a cache.");
            Call("Update"); Call("Update"); Require(game.progress.Gold == before, "A cleared floor paid gold by itself.");
            Require(PlayerPrefs.GetString(ProgressKey, "") == originalSaved, "A floor clear wrote test progression to PlayerPrefs.");
            report.gameplayClears++;
        }
        private void NextFloor()
        {
            if (game.run.IsRewardPending) game.SelectOffer(game.run.Offers[0].Id);
            game.NextFloor(); Require(!game.run.IsFloorCleared, "Production stairs failed to advance after a clear.");
        }
        private IEnumerator Library()
        {
            TowerMythArt.ValidateLibrary();
            var portraitHashes = new HashSet<ulong>();
            var cameraObject = new GameObject("Myth frame validation camera"); frameCamera = cameraObject.AddComponent<Camera>(); frameCamera.enabled = false;
            frameCamera.orthographic = true; frameCamera.orthographicSize = TowerMythArt.CellHeight * .5f; frameCamera.aspect = 1;
            frameCamera.clearFlags = CameraClearFlags.SolidColor; frameCamera.backgroundColor = Color.clear; frameCamera.cullingMask = 1 << 30;
            frameCamera.allowHDR = frameCamera.allowMSAA = false;
            frameTarget = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32) { antiAliasing = 1, filterMode = FilterMode.Point }; frameTarget.Create(); frameCamera.targetTexture = frameTarget;
            framePixels = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            string[] motions = { "Idle", "Run", "Attack", "Cast", "Hurt", "Death" };
            foreach (var myth in TowerMythCatalog.Definitions)
            {
                var atlas = Resources.Load<Texture2D>("Tower/Myths/" + myth.Id);
                Require(atlas != null && atlas.filterMode == FilterMode.Point && atlas.width >= 5 && atlas.height >= 6, "Myth atlas is missing or not point-filtered: " + myth.Id); report.atlases++;
                foreach (string id in myth.CompanionIds)
                {
                    var definition = TowerRun.GetDefinition(id);
                    Require(TowerMythArt.SheetId(definition.ArtIndex) == myth.Id, "Companion points at another myth's atlas: " + id);
                    var portrait = TowerActorArt.Portrait(definition.ArtIndex);
                    Require(portrait != null && portrait.filterMode == FilterMode.Point && Visible(portrait.GetPixels32()) > 32, "Portrait is missing, blank or filtered incorrectly: " + id);
                    Require(portraitHashes.Add(Hash(portrait.GetPixels32())), "Two mythology companions have identical portraits: " + id); report.portraits++;
                    frameRoot = new GameObject("Myth frame validation " + id); frameRoot.transform.position = new Vector3(10000, 10000, 0);
                    var art = TowerActorArt.Create(frameRoot.transform, definition.ArtIndex); var mythArt = art.GetComponent<TowerMythArt>();
                    var filters = art.GetComponentsInChildren<MeshFilter>();
                    var eden = art.GetComponent<TowerEdenArt>();
                    Require(eden != null || filters.Length > 0, "Myth companion has no rendered artwork: " + id);
                    var vertexCounts = new int[filters.Length];
                    for (int mesh = 0; mesh < filters.Length; mesh++) vertexCounts[mesh] = filters[mesh].sharedMesh.vertexCount;
                    foreach (Transform child in frameRoot.GetComponentsInChildren<Transform>()) child.gameObject.layer = 30;
                    frameCamera.transform.position = frameRoot.transform.position + new Vector3(0, TowerMythArt.CellHeight * .5f, -10);
                    var motionPoses = new HashSet<ulong>();
                    for (int row = 0; row < motions.Length; row++)
                    {
                        art.SetLocomotion(true, 0, false, 5);
                        if (row == 2) art.PlayAttack(.6f);
                        if (row == 3) art.PlayCast(SkillEffect.Fireball, .6f);
                        if (row == 4) art.InterruptAction();
                        if (row == 5) art.PlayDeath(.6f);
                        for (int frame = 0; frame < (row == 4 ? 6 : 18); frame++) art.SetPose(row == 1, false, row == 4 && frame == 0, 1, 1f / 60);
                        Require(mythArt.CurrentFrameRow == row && art.CurrentMotion == motions[row], "Production motion state mapping failed: " + id + "/" + motions[row]);
                        for (int mesh = 0; mesh < filters.Length; mesh++)
                        {
                            var renderer = filters[mesh].GetComponent<MeshRenderer>();
                            Require(filters[mesh].sharedMesh.vertexCount == vertexCounts[mesh] && renderer != null && renderer.sharedMaterial.mainTexture == atlas,
                                "A motion changed the character's mesh topology or source atlas: " + id + "/" + motions[row]);
                        }
                        Require(motionPoses.Add(eden != null ? (ulong)eden.CurrentFrame : GeometryHash(filters)),
                            "Two production motions rendered the same pose: " + id + "/" + motions[row]);
                        frameCamera.Render(); var previous = RenderTexture.active; RenderTexture.active = frameTarget;
                        framePixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0, false); framePixels.Apply(false, false); RenderTexture.active = previous;
                        report.stateFrames++; Require(Visible(framePixels.GetPixels32()) > 8, "Authored state rendered blank: " + id + "/" + motions[row]); report.visibleStateFrames++;
                    }
                    report.companions++; report.checks.Add(myth.Id + "/" + id + ": portrait and six distinct visible articulated motions PASS");
                    frameRoot.SetActive(false); Destroy(frameRoot); frameRoot = null;
                }
                yield return null;
            }
            Require(report.atlases == 10 && report.companions == 50 && report.portraits == 50 && report.stateFrames == 300 && report.visibleStateFrames == 300, "Not every mythology sprite was validated.");
            CleanupFrames();
        }
        private static int Visible(Color32[] pixels)
        { int count = 0; foreach (var p in pixels) if (p.a > 16) count++; return count; }
        private static ulong Hash(Color32[] pixels)
        { ulong hash = 1469598103934665603UL; foreach (var p in pixels) { hash ^= (uint)(p.r | p.g << 8 | p.b << 16 | p.a << 24); hash *= 1099511628211UL; } return hash; }
        private static ulong GeometryHash(MeshFilter[] filters)
        {
            ulong hash = 1469598103934665603UL;
            foreach (var filter in filters) foreach (var vertex in filter.sharedMesh.vertices)
            { hash = (hash ^ (uint)Mathf.RoundToInt(vertex.x * 100000)) * 1099511628211UL; hash = (hash ^ (uint)Mathf.RoundToInt(vertex.y * 100000)) * 1099511628211UL; }
            return hash;
        }
        private void CleanupFrames()
        {
            if (frameRoot) { frameRoot.SetActive(false); Destroy(frameRoot); frameRoot = null; }
            if (frameCamera) { frameCamera.targetTexture = null; Destroy(frameCamera.gameObject); frameCamera = null; }
            if (frameTarget) { frameTarget.Release(); Destroy(frameTarget); frameTarget = null; }
            if (framePixels) { Destroy(framePixels); framePixels = null; }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Finish(Exception error)
        {
            if (finishing) return; finishing = true; Application.logMessageReceived -= Log; CleanupFrames();
            if (error != null) report.errors.Add(error.ToString());
            if (game)
            {
                report.savedDataUntouched = PlayerPrefs.GetString(ProgressKey, "") == originalSaved;
                game.progress = originalProgress; game.SmokeControlled = originalSmoke; game.menu = true;
                game.SuppressProgressPersistence = quitOnFinish || originalSuppression;
            }
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0 && report.savedDataUntouched;
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true)); enabled = false;
            if (quitOnFinish) Application.Quit(report.success ? 0 : 1);
        }
        private void OnDestroy() { Application.logMessageReceived -= Log; CleanupFrames(); }
    }
}

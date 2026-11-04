using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Opt-in fixtures exercise production simulation; ordinary players never enter this suite.
    public sealed class TowerCombatSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class CombatReport
        {
            public string mode = "towerCombatSmoke", finishedUtc;
            public bool success, formationForward, formationReversed, stableSlots, companionScale, movingFormationMaintained;
            public bool airborneTargetEligibility, airborneMeleeDamage, airborneRangedDamage, allRolesAcquisition;
            public bool soloFlyerSpawns, dismissalPreservesFlight, meleeClimbsToFlyer, meleeHeightLimited;
            public bool lockedAim, preparationHasNoOverlays, preparationBeforeDamage, fanPattern, gapPattern, radialPattern, fixedBulletTrajectory;
            public bool waveStandingHit, waveApexJumpSafe, ringByDistance, heavyRingFollowUp, shooterFallClearsBolts, boltBudget, enemyBoltLife;
            public bool followUpOnTime, ringReachLocked, volleyReservedWhole;
            public bool stationaryBulletHit, jumpInputEvades, doubleJumpInput, dashInputInvulnerable, sweptBulletHit, hostileBulletDeath, cameraStableOnHit, hitCoreOnApproach, readySkillWaitsForDash, readySkillLeavesBodyFree, dashDropsHeldDashStrike;
            public bool nearestProjectileContact, sameFramePiercing, piercingDoesNotRepeat, splashKillRemoval, projectileRangeExpiry;
            public bool companionTargetExcluded, companionMeleeImmune, companionProjectilePassThrough, companionBossAreaImmune, companionStatusImmune, companionsStillAttack;
            public bool meleeLungeSweep, meleeLungeSidestep, meleeLungeCloses, doubleStrikeShortStep, ledgeLeap, fixtureNoLedgeLeap, offscreenThreatArrows;
            public bool meleeLungeShortRetreatHit, ledgeLeapThirdTier, leaperDropsBack, heavyYieldsMeleeTurn, postedShooterHolds;
            public bool comboRhythm, pogoDiminishes, pogoSameEnemyLimit, airDashRiseOnce, fastFallSlam, fastFallSlamHeight, fastFallSlamInterval;
            public bool meleeDodgePrimes, bossLandingDodgePrimes, dodgeRefundCapped, plainSlashKeepsBolts, dashSlashCutsBolts, counterBoltCutSpends, counterReflectsTwo;
            public bool telegraphFlashLastMoment, telegraphFlashOutOnRelease, telegraphFlashOutOnStun, rangedTelegraphFlash, telegraphTickPitches, bossTelegraphFlash, invulnerableBlinkRestores;
            public bool poiseSized, poiseBreaksWindup, poiseHeavyBlow, poiseHeavyMonster, poiseGuard, poiseRefills, poiseOnlyPlayer, poiseBullet, knockbackSlides, knockbackWall, bossNoPoise, fastFallPoise;
            public List<string> checks = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        private readonly CombatReport report = new CombatReport();
        private TowerGame game; private TowerSmokeDriver capture;
        private string prefix, reportPath; private bool finishing, quitOnComplete = true;
        public bool IsFinished { get { return finishing; } }
        public bool Succeeded { get { return finishing && report.success; } }
        private float deadline;
        private static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private IList Shots { get { return (IList)typeof(TowerGame).GetField("shots", Hidden).GetValue(game); } }
        private object Call(string method, params object[] args)
        { return typeof(TowerGame).GetMethod(method, Hidden).Invoke(game, args); }
        private static T Field<T>(object value, string name)
        { return (T)value.GetType().GetField(name).GetValue(value); }
        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver)
        {
            string[] args = Environment.GetCommandLineArgs();
            Initialize(tower, captureDriver,
                Argument(args, "-towerCombatCapture") ?? Path.Combine(Application.persistentDataPath, "tower-combat"),
                Argument(args, "-towerCombatReport") ?? Path.Combine(Application.persistentDataPath, "tower-combat.json"), true);
        }
        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver, string capturePrefix, string outputReportPath, bool quitOnComplete = false)
        {
            if (game != null) throw new InvalidOperationException("Create a new combat driver for each suite run.");
            if (tower == null) throw new ArgumentNullException(nameof(tower));
            if (captureDriver == null) throw new ArgumentNullException(nameof(captureDriver));
            if (!Path.IsPathRooted(capturePrefix) || !Path.IsPathRooted(outputReportPath)) throw new ArgumentException("Suite capture and report paths must be absolute.");
            game = tower; capture = captureDriver; prefix = capturePrefix; reportPath = outputReportPath; this.quitOnComplete = quitOnComplete;
            if (!quitOnComplete) capture.PrepareForSuite(tower);
            game.SmokeControlled = true; enabled = true;
            Application.logMessageReceived += RecordLog; deadline = Time.realtimeSinceStartup + 180;
            StartCoroutine(Guard(Suite()));
        }
        private static string Argument(string[] args, string key)
        { int at = Array.IndexOf(args, key); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
        private void RecordLog(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(condition + "\n" + trace); }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }
        private void Update()
        { if (!finishing && Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Finish(new TimeoutException("Combat suite exceeded 180 seconds.")); } }
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
        private IEnumerator Suite()
        {
            Require(Path.IsPathRooted(prefix) && Path.IsPathRooted(reportPath), "Absolute combat report paths are required.");
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            yield return null; yield return Formations(); yield return Airborne();
            ProjectileContacts(); CompanionSafety(); MeleeLunge(); LedgeLeap(); OffscreenThreats(); DashDropsHeldDashStrike(); DodgeEverywhere(); SwordCutsBolts(); Poise(); yield return AirStrikes(); yield return Patterns(); yield return ControlledDodge();
            yield return TelegraphCues();
            Finish(null);
        }
        private void Setup(params string[] companions)
        {
            game.enabled = true;
            if (game.hero != null && game.hero.root) game.hero.root.SetActive(false);
            foreach (var actor in game.allies) if (actor.root) actor.root.SetActive(false);
            foreach (var actor in game.enemies) if (actor.root) actor.root.SetActive(false);
            game.NewRun(74); game.StartClimb(); game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = false;
            var list = (IList)typeof(TowerRun).GetField("companions", Hidden).GetValue(game.run);
            foreach (string id in companions) list.Add(new CompanionState(TowerRun.GetDefinition(id)));
            game.ReconcileLoadout(); ClearEnemies(); ClearShots(); LevelFloor();
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.hero.facing = 1;
            // Enemies only attack while on screen. Frame the fixture around the player instead of the spawn view,
            // so a small editor Game view cannot push fixture attackers past the camera edge.
            game.view.transform.position = new Vector3(0, 0, -10);
            var sentinel = Spawn(25, TowerWorld.Ground); sentinel.stunTime = sentinel.cooldown = 999;
        }
        private void ClearEnemies()
        { foreach (var enemy in game.enemies) if (enemy.root) { enemy.root.SetActive(false); Destroy(enemy.root); } game.enemies.Clear(); }
        private void ClearShots()
        { foreach (object shot in Shots) { var root = Field<GameObject>(shot, "root"); if (root) Destroy(root); } Shots.Clear(); }
        /// <summary>
        /// Combat fixtures fire along fixed heights above the base ground, so they play on a level copy of the floor:
        /// same platforms and length, without the long floor's hills and pits under the line of fire.
        /// </summary>
        private void LevelFloor()
        {
            var world = (TowerWorld)typeof(TowerGame).GetField("world", Hidden).GetValue(game); var layout = world.CurrentLayout;
            var ground = new TowerSurface(new[] { TowerTerrainGenerator.TerrainLeft, TowerTerrainGenerator.TerrainRight }, new[] { TowerWorld.Ground, TowerWorld.Ground });
            world.SetTheme(game.run.Theme.Id, game.run.Floor, game.seed, new TowerFloorLayout(layout.Id, layout.Archetype, layout.Floor, ground, new List<TowerPlatform>(layout.Platforms), layout.ExitX));
        }
        private TowerGame.Fighter Spawn(float x, float y, int kind = 0)
        {
            var enemy = (TowerGame.Fighter)Call("Spawn", "Combat fixture", kind, true, new Vector2(x, y));
            game.enemies.Add(enemy); Call("ConfigureEnemy", enemy, kind, 0);
            enemy.maxHp = enemy.hp = 1000000; enemy.cooldown = 999; return enemy;
        }
        private static void Place(TowerGame.Fighter actor, Vector2 position)
        { actor.pos = position; actor.velocityY = 0; actor.grounded = true; actor.root.transform.position = position; }
        private IEnumerator Wait(float seconds)
        { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
        private IEnumerator Capture(string suffix, string message)
        {
            yield return null; yield return null;
            game.Notice(message, 5); yield return capture.CaptureForSuite(prefix, suffix);
            report.screenshots.Add(prefix + "-" + suffix + ".png");
        }
        private IEnumerator Formations()
        {
            Setup("michael", "gabriel", "uriel"); yield return Wait(2.5f);
            RequireSpacedSlots(1, "settle around the player");
            foreach (var state in game.run.Companions)
            {
                var actor = game.allies.Find(a => a.id == state.Id);
                Require(Mathf.Abs(actor.art.transform.localScale.x - .82f) < .001f && Mathf.Abs(actor.root.transform.localScale.x - 1) < .001f, "Companion art scale must be .82 without shrinking physics.");
            }
            report.formationForward = report.companionScale = true;
            yield return Capture("formation-right", "추적 검증 · 전사는 앞, 나머지는 뒤로 간격을 두고 정렬");
            game.MoveOverride = -1; yield return Wait(.24f); game.MoveOverride = 0; yield return Wait(2.6f);
            Require(game.hero.facing == -1, "Movement input did not reverse the hero facing.");
            RequireSpacedSlots(-1, "follow the reversing hero");
            report.formationReversed = report.stableSlots = true;
            yield return Capture("formation-left", "추적 검증 · 방향 전환 후 반대쪽으로 정렬");
            game.MoveOverride = -1; yield return Wait(2); game.MoveOverride = 0;
            foreach (var state in game.run.Companions) Require(Mathf.Abs(game.allies.Find(a => a.id == state.Id).pos.x - game.FormationAnchor(state).x) < .65f, "A companion fell behind the moving hero's formation: " + state.Id);
            report.movingFormationMaintained = true;
            report.checks.Add("Vanguards settle in spaced slots ahead of the player and other companions behind, swap sides after a reversal, keep up while moving, and only ally artwork scales to .82.");
        }
        // Grounded vanguards stand ahead of the hero's facing and other followers behind it, each in its own slot at least half a unit apart.
        private void RequireSpacedSlots(int facing, string action)
        {
            var spots = new List<float> { game.hero.pos.x };
            foreach (var state in game.run.Companions)
            {
                var actor = game.allies.Find(a => a.id == state.Id); var slot = game.FormationAnchor(state);
                Require(Mathf.Abs(actor.pos.x - slot.x) < .3f, "A companion did not " + action + ": " + state.Id);
                bool leads = TowerGame.LeadsFormation(state);
                Require((actor.pos.x - game.hero.pos.x) * facing * (leads ? 1 : -1) > .3f, leads ? "A vanguard stood behind the hero instead of in front: " + state.Id : "A companion stood in front of the hero instead of behind: " + state.Id);
                spots.Add(actor.pos.x);
            }
            spots.Sort(); for (int i = 1; i < spots.Count; i++) Require(spots[i] - spots[i - 1] > .5f, "The hero and companions overlap after they " + action + ".");
        }
        private IEnumerator Airborne()
        {
            Setup("michael", "gabriel"); ClearEnemies();
            game.enabled = false;
            var flyer = Spawn(2, TowerWorld.Ground + 1, 3); Place(flyer, new Vector2(2, TowerWorld.Ground + 1)); flyer.airborne = true; flyer.stunTime = 999;
            Require(game.CanTarget(flyer, game.run.Player), "The player cannot target a flying monster.");
            foreach(var definition in TowerRun.Definitions)Require(game.CanTarget(flyer,new CompanionState(definition)), "A companion role cannot target a flying monster: "+definition.Id);
            report.airborneTargetEligibility = true;
            // All attacks use actual reach; flight must not grant role immunity.
            float hp = flyer.hp; game.hero.facing = 1; Call("PlayerSlash");
            Require(flyer.hp<hp, "A player sword in reach could not damage a flying monster.");
            hp=flyer.hp;
            Fire(flyer.pos + Vector2.up, flyer.pos + Vector2.up, 0, 25, false, game.run.Player); Call("TickShots", .02f);
            Require(flyer.hp<hp, "A player-owned projectile could not damage a flying monster."); report.airborneMeleeDamage = true;
            hp=flyer.hp;
            ClearShots(); Fire(flyer.pos + Vector2.up, flyer.pos + Vector2.up, 0, 25, false, game.run.GetCompanion("gabriel")); Call("TickShots", .02f);
            Require(flyer.hp < hp, "An eligible ranged companion could not hit an airborne monster."); report.airborneRangedDamage = true;
            ClearShots(); foreach (var ally in game.allies) { Place(ally, new Vector2(flyer.pos.x - .8f, TowerWorld.Ground)); ally.cooldown = 0; ally.basicPending = false; }
            Call("TickAllies", .02f);
            Require(game.allies.Find(a => a.id == "michael").basicPending && game.allies.Find(a => a.id == "gabriel").basicPending, "Melee and ranged companions did not both acquire a flying target in reach.");
            report.allRolesAcquisition = true;
            game.Dismiss("gabriel"); Require(flyer.airborne, "Dismissing a ranged companion changed a monster's flight.");
            Call("TickEnemy",flyer,.02f);Require(flyer.airborne&&!flyer.grounded,"A flyer landed after its ranged companion was dismissed.");
            report.dismissalPreservesFlight = true;
            Setup();game.enabled=false;
            for(int floor=2;floor<=4;floor++)
            {
                ClearEnemies();typeof(TowerRun).GetField("<Floor>k__BackingField",Hidden).SetValue(game.run,floor);Call("SpawnFloor");
                var spawned=game.enemies.Find(enemy=>enemy.airborne);
                Require(spawned!=null&&game.run.Companions.Count==0,"A solo party did not spawn a flying monster.");
                // The generated shelves decide the exact height: the flyer hovers .8 above one of the stacked shelves (y >= 2).
                var shelves=(TowerWorld)typeof(TowerGame).GetField("world",Hidden).GetValue(game);bool perched=false;
                foreach(var ledge in shelves.Ledges)if(ledge.y>=2&&Mathf.Abs(spawned.pos.x-ledge.x)<.01f&&Mathf.Abs(spawned.pos.y-(ledge.y+.8f))<=.41f)perched=true;
                Require(perched,"The flyer is not hovering above a reachable upper shelf.");
            }
            report.soloFlyerSpawns=true;
            Setup("michael");ClearEnemies();game.enabled=false;
            var world=(TowerWorld)typeof(TowerGame).GetField("world",Hidden).GetValue(game);
            var perch=world.Ledges[world.Ledges.Length-1];
            flyer=Spawn(perch.x,perch.y+.8f,3);flyer.stunTime=999;
            Place(game.hero,new Vector2(perch.x,world.GroundAt(perch.x)));
            var melee=game.allies.Find(actor=>actor.id=="michael");Place(melee,game.hero.pos);melee.cooldown=0;
            hp=flyer.hp;Call("PlayerSlash");Require(flyer.hp==hp,"A sword hit a flyer far above its physical reach.");report.meleeHeightLimited=true;
            float highest=melee.pos.y;
            for(int frame=0;frame<1200&&flyer.hp==hp;frame++)
            {Call("TickAllies",.02f);Call("TickEnemy",flyer,.02f);highest=Mathf.Max(highest,melee.pos.y);}
            Require(flyer.hp<hp&&highest>perch.y-1.5f,"The melee companion did not climb ordinary platforms and hit the high flyer; ally="+melee.pos+" target="+flyer.pos);
            report.meleeClimbsToFlyer=true;
            Place(game.hero,new Vector2(perch.x,perch.y));game.hero.facing=flyer.pos.x>=game.hero.pos.x?1:-1;
            hp=flyer.hp;Call("PlayerSlash");Require(flyer.hp<hp,"A player on the upper perch cannot slash the flyer.");
            game.view.transform.position=new Vector3(perch.x,perch.y+1.5f,-10);
            yield return Capture("airborne","비행 몬스터 · 발판을 올라 근접 공격");
            report.checks.Add("Every role can target flying monsters. Sword and projectile contacts deal damage only in reach; a Vanguard climbs into reach of the highest flyer, solo floors spawn flyers, and dismissal preserves flight.");
        }
        private void Fire(Vector2 from, Vector2 to, float speed, float damage, bool hostile, HeroState owner = null, int pierce = 1, float area = 0)
        { Call("Fire", from, to, speed, damage, hostile, Color.cyan, area, owner, pierce, 0f); }
        private void ProjectileContacts()
        {
            Setup(); ClearEnemies(); game.enabled = false;
            Vector2 origin = new Vector2(0, TowerWorld.Ground + .7f), end = origin + Vector2.right * 10;
            var far = Spawn(4, TowerWorld.Ground); var near = Spawn(2, TowerWorld.Ground);
            Fire(origin, end, 200, 20, false); Call("TickShots", .04f);
            Require(near.hp < near.maxHp && far.hp == far.maxHp && Shots.Count == 0, "A projectile hit spawn order instead of the first crossed enemy.");
            report.nearestProjectileContact = true;

            ClearEnemies(); ClearShots();
            far = Spawn(4, TowerWorld.Ground); near = Spawn(2, TowerWorld.Ground); var middle = Spawn(3, TowerWorld.Ground);
            far.hp = near.hp = middle.hp = 10;
            Fire(origin, end, 200, 20, false, null, 3); Call("TickShots", .04f);
            Require(near.hp <= 0 && middle.hp <= 0 && far.hp <= 0 && game.enemies.Count == 0 && Shots.Count == 0,
                "A piercing shot skipped same-frame contacts or invalidated iteration when its victims died.");
            report.sameFramePiercing = true;

            ClearEnemies(); ClearShots(); near = Spawn(2, TowerWorld.Ground);
            Fire(origin, end, 100, 20, false, null, 3); Call("TickShots", .02f); float damaged = near.hp;
            Call("TickShots", .001f);
            Require(damaged < near.maxHp && near.hp == damaged && Shots.Count == 1, "A surviving piercing shot damaged the same enemy twice.");
            report.piercingDoesNotRepeat = true;

            ClearEnemies(); ClearShots();
            far = Spawn(2.6f, TowerWorld.Ground); middle = Spawn(2.3f, TowerWorld.Ground); near = Spawn(2, TowerWorld.Ground);
            far.hp = middle.hp = near.hp = 10;
            Fire(origin, end, 100, 20, false, null, 1, 1.25f); Call("TickShots", .04f);
            Require(game.enemies.Count == 0 && Shots.Count == 0, "A splash projectile skipped victims while removing killed enemies.");
            report.splashKillRemoval = true;

            ClearEnemies(); ClearShots(); near = Spawn(1, TowerWorld.Ground);
            Fire(origin, end, 10, 20, false);
            Shots[0].GetType().GetField("life").SetValue(Shots[0], .02f); Call("TickShots", .1f);
            Require(near.hp == near.maxHp && Shots.Count == 0, "A projectile moved or damaged beyond its remaining lifetime/range.");
            Fire(near.pos + Vector2.up * .7f, end, 10, 20, false);
            Shots[0].GetType().GetField("life").SetValue(Shots[0], 0f); Call("TickShots", .02f);
            Require(near.hp == near.maxHp && Shots.Count == 0, "An already expired projectile damaged an overlapping enemy.");
            game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0;
            Place(game.hero, new Vector2(2, TowerWorld.Ground)); typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, new Vector2(-2, TowerWorld.Ground));
            Vector2 stationary = new Vector2(0, TowerWorld.Ground + .9f);
            Fire(stationary, stationary, 0, 20, true);
            Shots[0].GetType().GetField("life").SetValue(Shots[0], .002f); Call("TickShots", .02f);
            Require(game.hero.hp == game.PlayerMaxHealth && Shots.Count == 0, "An expired hostile projectile hit a hero who crossed its position later in the frame.");
            report.projectileRangeExpiry = true;
            report.checks.Add("Projectile contacts follow sweep order; piercing handles several kills in one frame without duplicate damage; splash removal is safe; expiry clips both movement and damage.");
            game.enabled = true;
        }
        private void AssertCompanionsUnhit(Vector2 position, string attack)
        {
            foreach (var ally in game.allies)
                Require(ally.root && ally.hp == 100 && ally.hurt == 0 && ally.slowTime == 0 && ally.stunTime == 0 &&
                    ally.velocityY == 0 && ally.moveVelocity == 0 && ally.pos == position,
                    attack + " damaged, staggered, slowed, stunned or displaced " + ally.id);
        }
        private void PlaceCompanions(Vector2 position)
        {
            foreach (var ally in game.allies)
            {
                Place(ally, position); ally.hp = ally.maxHp = 100;
                ally.hurt = ally.slowTime = ally.stunTime = ally.moveVelocity = 0;
            }
        }
        private void CompanionSafety()
        {
            Setup("michael", "gabriel", "uriel", "raphael"); ClearEnemies(); game.enabled = false;
            game.view.transform.position = new Vector3(0, 0, -10);
            Vector2 overlap = new Vector2(0, TowerWorld.Ground);
            PlaceCompanions(overlap);
            // Positive fixture HP proves that immunity is a targeting policy,
            // rather than an accidental consequence of allies' unused health.
            foreach (var ally in game.allies)
            {
                Require(!game.CanTarget(ally, game.run.Player), "A companion is eligible for a damage/status target.");
                Require((float)Call("StrikeEnemy", ally, 30f, game.run.Player, 3f) == 0, "A direct strike accepted a companion.");
                Call("DamageEnemy", ally, 20f, 3f);
            }
            Require(!game.CanTarget(game.hero, game.run.Player), "A friendly attack accepts the player as an enemy.");
            AssertCompanionsUnhit(overlap, "Direct damage guard"); report.companionTargetExcluded = true;

            var melee = Spawn(0, TowerWorld.Ground); melee.cooldown = 0;
            // The player stands past melee's attack start (3.6), so the monster can only approach it.
            Place(game.hero, new Vector2(TowerGame.MeleeStart + .9f, TowerWorld.Ground)); Call("TickEnemy", melee, .01f);
            Require(melee.windup == 0 && melee.pos.x > 0, "The enemy attacked an overlapping companion instead of approaching the player.");
            Place(melee, overlap); Place(game.hero, new Vector2(1.2f, TowerWorld.Ground)); Call("TickEnemy", melee, .01f);
            Require(melee.windup > 0 && game.EnemyLockedAim(melee) == game.hero.pos, "Melee did not lock the player's position.");
            Vector2 meleeAim = game.hero.pos; PlaceCompanions(meleeAim);
            Place(game.hero, new Vector2(7, TowerWorld.Ground)); float heroHp = game.hero.hp;
            Call("TickEnemy", melee, .82f);
            Require(game.EnemyReleaseCount(melee) == 1 && game.hero.hp == heroHp, "The locked melee did not release or damaged the distant player.");
            AssertCompanionsUnhit(meleeAim, "Enemy melee");
            Call("ConfigureEnemy", melee, 0, 0); melee.cooldown = 0; Place(game.hero, meleeAim); game.invulnerable = 0;
            Call("TickEnemy", melee, .01f); Call("TickEnemy", melee, .82f);
            Require(game.hero.hp < heroHp && game.hero.hurt > 0, "Companion immunity also prevented player melee damage.");
            AssertCompanionsUnhit(meleeAim, "Shared melee contact"); report.companionMeleeImmune = true;

            ClearEnemies(); ClearShots(); PlaceCompanions(overlap);
            Place(game.hero, new Vector2(3, TowerWorld.Ground)); game.invulnerable = 0; heroHp = game.hero.hp;
            typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, game.hero.pos);
            var caster = Spawn(-3, TowerWorld.Ground, 1); caster.cooldown = 0;
            Call("TickEnemy", caster, .01f); Call("TickEnemy", caster, 1.01f);
            Require(game.EnemyReleaseCount(caster) == 1 && Shots.Count == 3, "The real enemy fan did not fire.");
            Call("TickShots", .75f);
            Require(Shots.Count == 3 && game.hero.hp == heroHp, "A companion consumed a passing enemy projectile.");
            AssertCompanionsUnhit(overlap, "Enemy projectile pass-through");
            Call("TickShots", .7f);
            Require(Shots.Count < 3 && game.hero.hp < heroHp, "A projectile passing through companions failed to hit the player.");
            AssertCompanionsUnhit(overlap, "Enemy projectile player contact"); report.companionProjectilePassThrough = true;

            ClearEnemies(); ClearShots();
            var boss = Spawn(-3, TowerWorld.Ground);
            for (int control = 0; control < 2; control++)
            {
                Place(boss, new Vector2(-3, TowerWorld.Ground)); Place(game.hero, overlap);
                Call("ConfigureBoss", boss, 15); boss.cooldown = 0; Call("TickEnemy", boss, .01f);
                Require(game.BossPatternName(boss) == "LeapingSlam" && game.BossLockedAim(boss) == overlap, "Boss slam did not target the player.");
                if (control == 0) Place(game.hero, new Vector2(6, TowerWorld.Ground));
                game.invulnerable = 0; heroHp = game.hero.hp;
                for (int frame = 0; frame < 140 && game.BossReleaseCount(boss) == 0; frame++) Call("TickEnemy", boss, .02f);
                Require(game.BossReleaseCount(boss) == 1, "Boss landing area did not release.");
                Require(control == 0 ? game.hero.hp == heroHp : game.hero.hp < heroHp, "Boss slam did not preserve normal player contact rules.");
                AssertCompanionsUnhit(overlap, "Boss landing area"); ClearShots();
            }
            report.companionBossAreaImmune = true;

            ClearEnemies(); var target = Spawn(1, TowerWorld.Ground); Place(game.hero, overlap);
            AugmentDefinition ice = null;
            foreach (var definition in TowerRun.Augments)
                if (definition.OwnerId == "player" && definition.Kind == RewardKind.Skill && definition.Effect == SkillEffect.IceNova) { ice = definition; break; }
            Require(ice != null, "IceNova status fixture is missing.");
            float enemyHp = target.hp;
            Call("CastSkill", game.run.Player, new SkillState(ice, game.run.Player), game.hero, target);
            Require(target.hp < enemyHp && target.hurt > 0 && target.slowTime > 0 && target.pos.x > 1, "Real status area did not damage, slow and push its enemy control.");
            AssertCompanionsUnhit(overlap, "Overlapping damage/status area"); report.companionStatusImmune = true;
            // Exercise every party role's attack after the hostile contacts.
            foreach (var ally in game.allies)
            {
                var state = game.run.GetCompanion(ally.id); ClearShots(); enemyHp = target.hp;
                Call("BeginAllyAttack", ally, state, target); Call("ResolveAllyAttack", ally, state, 1f);
                Call("TickShots", .25f);
                Require(target.hp < enemyHp, "An immune companion stopped attacking: " + ally.id);
            }
            report.companionsStillAttack = true;
            report.checks.Add("All four companion roles reject direct damage/status targets and remain unhurt/unmoved through real enemy melee, fan projectiles and boss landing areas. Projectiles pass through to damage the player; IceNova still damages/slows/pushes enemies; every companion role still attacks.");
            game.enabled = true;
        }
        // Melee winds up .55 seconds, then lunges along the locked facing and its blow sweeps the whole lunge at once:
        // standing where it aimed or stepping .4 or 1.2 back is hit (the sweep ends 2.1 past the locked point), walking 2.4
        // away is not, and a windup begun past the stance steps in to reach.
        private void MeleeLunge()
        {
            Setup(); ClearEnemies(); game.enabled = false; game.view.transform.position = new Vector3(0, 0, -10);
            float stance = TowerGame.MeleeStance, hp;
            var melee = Spawn(0, TowerWorld.Ground); melee.cooldown = 0;
            Place(game.hero, new Vector2(stance, TowerWorld.Ground)); game.invulnerable = 0; hp = game.hero.hp;
            Call("TickEnemy", melee, .01f);
            Require(Mathf.Abs(melee.windup - TowerGame.MeleeWindup) < .001f && game.EnemyLockedAim(melee) == game.hero.pos, "Melee did not lock a .55-second windup on the player at its stance: windup=" + melee.windup);
            for (int frame = 0; frame < 60 && game.EnemyReleaseCount(melee) == 0; frame++)
            {
                Call("TickEnemy", melee, .02f);
                if (game.EnemyReleaseCount(melee) == 0) Require(game.hero.hp == hp && melee.pos.x == 0, "Melee hit or moved before its windup from the stance ended.");
            }
            Require(game.EnemyReleaseCount(melee) == 1 && game.hero.hp < hp, "A player standing where the lunge was aimed was not hit.");
            // From the stance the lunge runs its whole reach (the locked point plus the overshoot) in LungeSeconds.
            float lungeStep = TowerGame.LungeReach / TowerGame.LungeSeconds * .02f; Call("TickEnemy", melee, .02f);
            Require(Mathf.Abs(stance + TowerGame.LungeOvershoot - TowerGame.LungeReach) < .001f && Mathf.Abs(melee.pos.x - lungeStep) < .02f,
                "The lunge did not cover " + TowerGame.LungeReach + " over " + TowerGame.LungeSeconds + " s: x=" + melee.pos.x + " after one .02 s step.");
            for (int frame = 0; frame < Mathf.CeilToInt(TowerGame.LungeSeconds / .02f); frame++) Call("TickEnemy", melee, .02f);
            Require(Mathf.Abs(melee.pos.x - TowerGame.LungeReach) < .02f && melee.grounded && Mathf.Abs(melee.pos.y - TowerWorld.Ground) < .01f,
                "The lunge did not carry the monster its full reach along the ground: x=" + melee.pos.x);
            report.meleeLungeSweep = true;

            // The sweep's far edge lies LungeOvershoot + LungeRadius past the locked point.
            float edge = TowerGame.LungeOvershoot + TowerGame.LungeRadius;
            Require(Mathf.Abs(edge - 2.1f) < .001f, "The lunge's sweep does not end 2.1 past the locked point: " + edge);
            foreach (float step in new[] { .4f, 1.2f, 2.4f })
            {
                ClearEnemies(); melee = Spawn(0, TowerWorld.Ground); melee.cooldown = 0;
                Place(game.hero, new Vector2(stance, TowerWorld.Ground)); game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0; hp = game.hero.hp;
                Call("TickEnemy", melee, .01f); Place(game.hero, new Vector2(stance + step, TowerWorld.Ground));
                for (int frame = 0; frame < 60 && game.EnemyReleaseCount(melee) == 0; frame++) Call("TickEnemy", melee, .02f);
                bool reached = step < edge;
                Require(game.EnemyReleaseCount(melee) == 1 && (game.hero.hp < hp) == reached, reached
                    ? "A player who stepped only " + step + " back from the locked point escaped the lunge."
                    : "A player who walked " + step + " away from the locked point was still hit by the lunge.");
            }
            report.meleeLungeSidestep = report.meleeLungeShortRetreatHit = true;

            ClearEnemies(); melee = Spawn(0, TowerWorld.Ground); melee.cooldown = 0; float far = TowerGame.MeleeStart - .2f;
            Place(game.hero, new Vector2(far, TowerWorld.Ground)); game.invulnerable = 0; hp = game.hero.hp;
            Call("TickEnemy", melee, .01f); Require(melee.windup > 0, "Melee did not begin its windup inside its attack start.");
            for (int frame = 0; frame < 60 && game.EnemyReleaseCount(melee) == 0; frame++) Call("TickEnemy", melee, .02f);
            Require(game.EnemyReleaseCount(melee) == 1 && game.hero.hp < hp && Mathf.Abs(far - melee.pos.x - stance) < .05f,
                "A windup begun past the stance did not step in and reach a player who stayed put: released from x=" + melee.pos.x);
            report.meleeLungeCloses = true;

            // A double-strike elite's second blow, a quarter second on, steps on no further than the overshoot allows.
            ClearEnemies(); melee = Spawn(0, TowerWorld.Ground); melee.cooldown = 0;
            object state = ((IDictionary)typeof(TowerGame).GetField("enemyCombat", Hidden).GetValue(game))[melee];
            state.GetType().GetField("affix").SetValue(state, TowerGame.EliteAffix.Double);
            Place(game.hero, new Vector2(1.2f, TowerWorld.Ground)); game.invulnerable = 0; hp = game.hero.hp;
            Call("TickEnemy", melee, .01f); float first = -1;
            for (int frame = 0; frame < 80 && game.EnemyReleaseCount(melee) < 2; frame++)
            {
                Call("TickEnemy", melee, .02f);
                if (game.EnemyReleaseCount(melee) == 1) { if (first < 0) first = game.hero.hp; game.invulnerable = 0; }
            }
            Require(game.EnemyReleaseCount(melee) == 2 && first < hp && game.hero.hp < first && melee.pos.x <= 1.2f + TowerGame.LungeOvershoot + .01f,
                "A double strike's second blow missed the locked point or carried past its overshoot: x=" + melee.pos.x);
            report.doubleStrikeShortStep = true;
            report.checks.Add("Melee: a .55-second windup, then a lunge of up to " + TowerGame.LungeReach + " in " + TowerGame.LungeSeconds + " s that hits a player standing at the locked point or stepping .4 or 1.2 back (sweep ends " + edge.ToString("F1") + " past it), misses one who walked 2.4 away, steps in from past its stance, and a double strike's second blow lands from where the lunge stopped.");
            ClearEnemies(); game.enabled = true;
        }
        // A windup lights the attacker white on its own flash channel as one tick sounds: for its last TelegraphFlashTime before
        // a blow (a lunge, a boss's slam), its last RangedFlashTime before a volley. The release or a stun puts it out, and an
        // elite's Tint stays as it was throughout. Each monster kind ticks its own clip at TelegraphTickHz times its step, a
        // boss at BossTickHz. Invulnerability after a hit blinks the hero's alpha on its own channel and hands back the tint's
        // alpha once it ends.
        private IEnumerator TelegraphCues()
        {
            Setup(); ClearEnemies(); game.enabled = false; game.view.transform.position = new Vector3(0, 0, -10);
            var elite = new Color(1, .84f, .76f); var heard = new List<string>();
            Func<int, bool> kindPitch = kind => Mathf.Abs(game.LastTelegraphTickHz - TowerGame.TelegraphTickHz * TowerGame.TelegraphTickSteps[kind]) < .01f;
            for (int stun = 0; stun < 2; stun++)
            {
                ClearEnemies(); var melee = Spawn(0, TowerWorld.Ground); melee.cooldown = 0; melee.art.Tint = elite;
                Place(game.hero, new Vector2(TowerGame.MeleeStance, TowerWorld.Ground)); game.invulnerable = 0;
                int starts = game.TelegraphStarts, ticks = game.TelegraphTicks; bool lit = false;
                Call("TickEnemy", melee, .01f);
                Require(melee.windup > TowerGame.TelegraphFlashTime && game.TelegraphStarts == starts + 1 && !melee.art.TelegraphLit,
                    "A melee windup did not sound its start, or lit up before its last " + TowerGame.TelegraphFlashTime + " s: windup=" + melee.windup);
                for (int frame = 0; frame < 60 && game.EnemyReleaseCount(melee) == 0; frame++)
                {
                    Call("TickEnemy", melee, .02f);
                    if (game.EnemyReleaseCount(melee) > 0) break;
                    bool last = melee.windup > 0 && melee.windup <= TowerGame.TelegraphFlashTime;
                    Require(melee.art.TelegraphLit == last && melee.art.Tint == elite && (!last || melee.art.ShownColor.g > 1.2f && melee.art.ShownColor.b > 1.2f),
                        "The telegraph flash did not light just its windup's last " + TowerGame.TelegraphFlashTime + " s on its own channel: windup=" + melee.windup + ", lit=" + melee.art.TelegraphLit + ", shown=" + melee.art.ShownColor);
                    if (!last) continue;
                    if (!lit) Require(game.TelegraphTicks == ticks + 1 && kindPitch(0), "The telegraph tick did not sound once, at the light walker's pitch, as the flash lit: ticks=" + (game.TelegraphTicks - ticks) + ", pitch=" + game.LastTelegraphTickHz);
                    lit = true;
                    if (stun == 1) { melee.stunTime = .5f; Call("TickEnemy", melee, .02f); break; }
                }
                Require(lit && game.TelegraphTicks == ticks + 1, "The telegraph flash never lit, or ticked more than once: ticks=" + (game.TelegraphTicks - ticks));
                Require(!melee.art.TelegraphLit && melee.art.Tint == elite && melee.art.ShownColor == elite && (stun == 1 ? melee.windup == 0 && game.EnemyReleaseCount(melee) == 0 : game.EnemyReleaseCount(melee) == 1),
                    (stun == 0 ? "The flash stayed lit after the blow was released" : "A stun mid-flash left the flash lit") + ": lit=" + melee.art.TelegraphLit + ", shown=" + melee.art.ShownColor);
            }
            report.telegraphFlashLastMoment = report.telegraphFlashOutOnRelease = report.telegraphFlashOutOnStun = true;
            heard.Add("light walker lunge " + TowerGame.TelegraphFlashTime + " s @ " + game.LastTelegraphTickHz.ToString("F0") + " Hz");

            // A volley lights for the shorter RangedFlashTime: a shooter's, a heavy's ranged turn (its second ordinal) and a
            // flyer's, each ticking once at its kind's own pitch; the blow's longer flash never lights them early.
            for (int kind = 1; kind < 4; kind++)
            {
                ClearEnemies(); ClearShots(); Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.invulnerable = 0;
                var shooter = Spawn(4, TowerWorld.Ground, kind); Call("ConfigureEnemy", shooter, kind, 1); shooter.cooldown = 0;
                int starts = game.TelegraphStarts, ticks = game.TelegraphTicks; bool lit = false;
                Call("TickEnemy", shooter, .01f);
                string pattern = game.EnemyPatternName(shooter);
                Require(pattern != "MeleeSlam" && shooter.windup > TowerGame.TelegraphFlashTime && game.TelegraphStarts == starts + 1 && !shooter.art.TelegraphLit
                    && Mathf.Abs(game.TelegraphFlashOf(shooter) - TowerGame.RangedFlashTime) < .0001f,
                    "Kind " + kind + " did not begin a ranged windup under the ranged flash: pattern=" + pattern + ", windup=" + shooter.windup + ", flash=" + game.TelegraphFlashOf(shooter));
                for (int frame = 0; frame < 80 && game.EnemyReleaseCount(shooter) == 0; frame++)
                {
                    Call("TickEnemy", shooter, .02f);
                    if (game.EnemyReleaseCount(shooter) > 0) break;
                    bool last = shooter.windup > 0 && shooter.windup <= TowerGame.RangedFlashTime;
                    Require(shooter.art.TelegraphLit == last, "Kind " + kind + "'s " + pattern + " flash did not light just its windup's last " + TowerGame.RangedFlashTime + " s: windup=" + shooter.windup + ", lit=" + shooter.art.TelegraphLit);
                    if (last && !lit) Require(game.TelegraphTicks == ticks + 1 && kindPitch(kind), "Kind " + kind + "'s tick did not sound once at its own pitch as the flash lit: ticks=" + (game.TelegraphTicks - ticks) + ", pitch=" + game.LastTelegraphTickHz);
                    lit |= last;
                }
                Require(lit && game.TelegraphTicks == ticks + 1 && !shooter.art.TelegraphLit && game.EnemyReleaseCount(shooter) == 1,
                    "Kind " + kind + "'s " + pattern + " flash never lit, ticked more than once or stayed lit past its release: ticks=" + (game.TelegraphTicks - ticks) + ", lit=" + shooter.art.TelegraphLit);
                heard.Add("kind " + kind + " " + pattern + " " + TowerGame.RangedFlashTime + " s @ " + game.LastTelegraphTickHz.ToString("F0") + " Hz");
            }
            ClearShots(); report.rangedTelegraphFlash = true;

            // Every kind's tick is a clip of its own that really sounds at TelegraphTickHz times its step (rising zero
            // crossings past the onset click).
            var tickCues = (Array)typeof(TowerGame).GetField("tickCues", Hidden).GetValue(game); var tickClips = new HashSet<AudioClip>();
            Require(tickCues.Length == TowerGame.TelegraphTickSteps.Length, "The telegraph ticks did not cover every monster kind: " + tickCues.Length);
            for (int kind = 0; kind < tickCues.Length; kind++)
            {
                object cue = tickCues.GetValue(kind); var clip = cue == null ? null : ((AudioClip[])cue.GetType().GetField("clips").GetValue(cue))[0];
                Require(clip != null && tickClips.Add(clip), "Monster kinds share a telegraph tick: " + kind);
                var data = new float[clip.samples]; Require(clip.GetData(data, 0), "The telegraph tick's samples could not be read: " + kind);
                int first = -1, last = -1, rises = 0;
                for (int i = Mathf.Max(1, Mathf.RoundToInt(.005f * clip.frequency)); i < data.Length; i++)
                    if (data[i - 1] < 0 && data[i] >= 0) { if (first < 0) first = i; last = i; rises++; }
                float measured = rises > 1 ? (rises - 1) * clip.frequency / (float)(last - first) : 0, expected = TowerGame.TelegraphTickHz * TowerGame.TelegraphTickSteps[kind];
                Require(Mathf.Abs(measured / expected - 1) < .03f, "Kind " + kind + "'s telegraph tick sounded at " + measured.ToString("F0") + " Hz instead of " + expected + " Hz.");
                heard.Add("kind " + kind + " tick clip " + measured.ToString("F0") + " Hz");
            }
            report.telegraphTickPitches = true;

            // A boss follows the same rule at its own lower tick: its slam's flash is a blow's, its crown volley's a volley's.
            foreach (int floor in new[] { 15, 5 })
            {
                ClearEnemies(); ClearShots(); var boss = Spawn(-3, TowerWorld.Ground); Place(game.hero, new Vector2(0, TowerWorld.Ground));
                Call("ConfigureBoss", boss, floor); boss.cooldown = 0;
                int bossStarts = game.TelegraphStarts, bossTicks = game.TelegraphTicks; bool bossLit = false;
                Call("TickEnemy", boss, .01f);
                string technique = game.BossPatternName(boss); bool slam = technique == "LeapingSlam";
                float flash = slam ? TowerGame.TelegraphFlashTime : TowerGame.RangedFlashTime;
                Require(technique == (floor == 15 ? "LeapingSlam" : "CrownVolley") && game.BossTelegraphRemaining(boss) > TowerGame.TelegraphFlashTime && game.TelegraphStarts == bossStarts + 1 && !boss.art.TelegraphLit
                    && Mathf.Abs(game.TelegraphFlashOf(boss) - flash) < .0001f,
                    "A boss technique did not sound its windup's start under its flash: " + technique + ", remaining=" + game.BossTelegraphRemaining(boss) + ", flash=" + game.TelegraphFlashOf(boss));
                for (int frame = 0; frame < 140 && game.BossTelegraphRemaining(boss) > 0; frame++)
                {
                    Call("TickEnemy", boss, .02f);
                    float left = game.BossTelegraphRemaining(boss);
                    Require(boss.art.TelegraphLit == (left > 0 && left <= flash),
                        "A boss's " + technique + " flash did not follow its windup's last " + flash + " s: remaining=" + left + ", lit=" + boss.art.TelegraphLit);
                    bossLit |= boss.art.TelegraphLit;
                }
                Require(bossLit && game.TelegraphTicks == bossTicks + 1 && !boss.art.TelegraphLit && game.BossTelegraphRemaining(boss) == 0 && Mathf.Abs(game.LastTelegraphTickHz - TowerGame.BossTickHz) < .01f,
                    "A boss's " + technique + " telegraph did not light and tick once at the boss's pitch, then go out as it began: ticks=" + (game.TelegraphTicks - bossTicks) + ", pitch=" + game.LastTelegraphTickHz);
                heard.Add("boss " + technique + " " + flash + " s @ " + game.LastTelegraphTickHz.ToString("F0") + " Hz");
            }
            ClearShots(); report.bossTelegraphFlash = true;

            // Real updates run the invulnerability down; a stunned sentinel far off keeps the floor from clearing.
            ClearEnemies(); var sentinel = Spawn(25, TowerWorld.Ground); sentinel.stunTime = sentinel.cooldown = 999;
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.hero.hp = game.PlayerMaxHealth; game.dashTime = 0; game.invulnerable = 0;
            game.enabled = true; yield return null;
            Require(game.hero.art.BlinkAlpha == 1 && Mathf.Approximately(game.hero.art.ShownColor.a, game.hero.art.Tint.a), "The hero blinked without invulnerability: blink=" + game.hero.art.BlinkAlpha);
            Call("HurtHero", 10f);
            Require(game.invulnerable > TowerGame.InvulnerableBlinkSettle && game.dashTime <= 0, "The blink fixture could not hurt the hero: invulnerable=" + game.invulnerable);
            bool dimmed = false; float limit = Time.realtimeSinceStartup + 3;
            while (game.invulnerable > 0)
            {
                Require(Time.realtimeSinceStartup < limit, "Invulnerability never ran out: " + game.invulnerable);
                yield return null;
                float blink = game.hero.art.BlinkAlpha;
                Require((blink == 1 || Mathf.Approximately(blink, TowerGame.InvulnerableBlinkAlpha)) && game.hero.art.Tint.a == 1 && Mathf.Abs(game.hero.art.ShownColor.a - blink) < .001f,
                    "The invulnerability blink left its own channel: blink=" + blink + ", tint alpha=" + game.hero.art.Tint.a + ", shown alpha=" + game.hero.art.ShownColor.a);
                dimmed |= blink < 1;
            }
            yield return null;
            Require(dimmed && game.hero.art.BlinkAlpha == 1 && game.hero.art.Tint.a == 1 && Mathf.Approximately(game.hero.art.ShownColor.a, game.hero.art.Tint.a),
                "The hero's alpha did not come back once invulnerability ended: dimmed=" + dimmed + ", blink=" + game.hero.art.BlinkAlpha + ", shown alpha=" + game.hero.art.ShownColor.a);
            report.invulnerableBlinkRestores = true;
            report.checks.Add("Telegraph: a windup's start sounds once; its last " + TowerGame.TelegraphFlashTime + " s before a blow (a lunge, a boss's slam) and its last " + TowerGame.RangedFlashTime + " s before a volley light the attacker on its own flash channel with one tick at its kind's pitch (an elite keeps its Tint), the release or a stun puts it out, and a boss follows the same rule at its lower tick (" + string.Join("; ", heard.ToArray()) + "). Invulnerability blinks the hero's alpha " + TowerGame.InvulnerableBlinkHz + " times a second down to " + TowerGame.InvulnerableBlinkAlpha + " and hands the tint's alpha back.");
            ClearEnemies(); game.enabled = true;
        }
        // A roused pack's melee monster leaps onto a ledge the player stands on just beyond its reach, the third tier (4.0 up)
        // included, strikes there and drops back through it after a player who steps down; a fixture monster outside any pack
        // stays on the ground beneath it. A heavy left out of its blow's reach gives its melee turn up for its ring, and a
        // pack's shooter posted on a ledge holds it over a player below.
        private void LedgeLeap()
        {
            Setup(); ClearEnemies(); game.enabled = false;
            game.view.transform.position = new Vector3(3, 0, -10);
            float top;
            foreach (float rise in new[] { 2.6f, 4f })
            {
                top = LedgeFloor(rise);
                for (int pass = 0; pass < 2; pass++)
                {
                    bool pack = pass == 0;
                    ClearEnemies(); Place(game.hero, new Vector2(5, top)); game.hero.support = 0; game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0; float hp = game.hero.hp;
                    var melee = Spawn(2.5f, TowerWorld.Ground);
                    if (pack) { Call("JoinPack", melee, 0); Call("AlertPack", 0); }
                    melee.cooldown = 0; bool rose = false, landed = false;
                    for (int frame = 0; frame < 200 && game.hero.hp == hp; frame++)
                    {
                        Call("TickEnemy", melee, .02f);
                        rose |= melee.pos.y > TowerWorld.Ground + .5f; landed |= melee.grounded && melee.support == 0;
                    }
                    if (pack) Require(rose && landed && melee.support == 0 && game.hero.hp < hp, "A roused melee monster did not leap onto the player's ledge " + rise + " up and strike there: pos=" + melee.pos + " support=" + melee.support);
                    else Require(!rose && melee.grounded && melee.support < 0 && melee.windup == 0 && game.hero.hp == hp, "A melee monster outside any pack leapt or struck at the player's ledge " + rise + " up: pos=" + melee.pos);
                    if (!pack) continue;
                    // Having leapt up, it drops back through the ledge after a player who steps down beneath it.
                    Place(game.hero, new Vector2(5, TowerWorld.Ground)); game.hero.support = -1;
                    for (int frame = 0; frame < 100 && !(melee.grounded && melee.support < 0); frame++) Call("TickEnemy", melee, .02f);
                    Require(melee.grounded && melee.support < 0 && Mathf.Abs(melee.pos.y - TowerWorld.Ground) < .01f,
                        "A monster that leapt onto the ledge " + rise + " up did not drop back after the player below: pos=" + melee.pos + " support=" + melee.support);
                }
            }
            report.ledgeLeap = report.fixtureNoLedgeLeap = report.ledgeLeapThirdTier = report.leaperDropsBack = true;

            // A heavy (a fixture, so it never leaps) ready for its melee turn under a player up the ledge gives the turn up
            // after HeavyReachWait and winds up its ring instead.
            top = LedgeFloor(2.6f);
            ClearEnemies(); Place(game.hero, new Vector2(5, top)); game.hero.support = 0; game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0;
            var heavy = Spawn(4, TowerWorld.Ground, 2); heavy.cooldown = 0; float waited = -1, heroHp = game.hero.hp;
            for (int frame = 1; frame <= 100 && waited < 0; frame++) { Call("TickEnemy", heavy, .02f); if (heavy.windup > 0) waited = frame * .02f; }
            Require(waited >= TowerGame.HeavyReachWait && waited <= TowerGame.HeavyReachWait + .08f && (string)Call("EnemyPatternName", heavy) == "RadialGap"
                && Mathf.Abs(heavy.windup - TowerGame.RadialWindup) < .001f && game.hero.hp == heroHp,
                "A heavy out of its blow's reach did not give its melee turn up for its ring after " + TowerGame.HeavyReachWait + " s: waited " + waited + ", pattern " + Call("EnemyPatternName", heavy) + ", windup " + heavy.windup);
            report.heavyYieldsMeleeTurn = true;

            // A pack's shooter posted on the ledge holds its post over a player on the ground below; only a leaper drops back.
            ClearEnemies(); Place(game.hero, new Vector2(1, TowerWorld.Ground)); game.hero.support = -1;
            var post = Spawn(5, top, 1); post.support = 0; Call("JoinPack", post, 0); post.cooldown = 999; bool held = true;
            for (int frame = 0; frame < 30; frame++)
            { Call("TickEnemy", post, .02f); held &= post.grounded && post.support == 0 && Mathf.Abs(post.pos.y - top) < .01f; }
            Require(held, "A pack shooter posted on a ledge dropped off it toward the player below: pos=" + post.pos + " support=" + post.support);
            report.postedShooterHolds = true;
            report.checks.Add("A roused pack's melee monster under a player on a ledge 2.6 or 4.0 up crouches, leaps onto it, strikes there and drops back after a player who steps down; a fixture monster outside any pack stays on the ground. A heavy out of its blow's reach casts its ring after " + waited.ToString("F2") + " s; a pack shooter posted on a ledge holds it for 30 steps over a player below.");
            ClearEnemies(); game.enabled = true;
        }
        /// <summary>A flat floor with one ledge from x 3 to 7, <paramref name="rise"/> above the ground; returns the ledge's top.</summary>
        private float LedgeFloor(float rise)
        {
            var world = (TowerWorld)typeof(TowerGame).GetField("world", Hidden).GetValue(game); var layout = world.CurrentLayout;
            float top = TowerWorld.Ground + rise;
            var flat = new TowerSurface(new[] { TowerTerrainGenerator.TerrainLeft, TowerTerrainGenerator.TerrainRight }, new[] { TowerWorld.Ground, TowerWorld.Ground });
            var ledges = new List<TowerPlatform> { new TowerPlatform(new TowerSurface(new[] { 3f, 7f }, new[] { top, top }), 2) };
            world.SetTheme(game.run.Theme.Id, game.run.Floor, game.seed, new TowerFloorLayout(layout.Id, layout.Archetype, layout.Floor, flat, ledges, layout.ExitX));
            return top;
        }
        // Roused monsters just past the screen's edge show an arrow, the nearer the stronger; in view, asleep or far, none.
        private void OffscreenThreats()
        {
            Setup(); ClearEnemies(); game.enabled = false; game.view.transform.position = new Vector3(0, 0, -10);
            float edge = game.view.ViewportToWorldPoint(new Vector3(1, .5f, 10)).x;
            var near = Spawn(edge + 1.5f, TowerWorld.Ground); var farther = Spawn(edge + 4.5f, TowerWorld.Ground); var beyond = Spawn(edge + 8, TowerWorld.Ground);
            var inView = Spawn(edge - 3, TowerWorld.Ground); var asleep = Spawn(edge + 2, TowerWorld.Ground);
            foreach (var enemy in new[] { near, farther, beyond, inView }) Call("JoinPack", enemy, 0);
            Call("AlertPack", 0);
            float a = game.ThreatCloseness(near), b = game.ThreatCloseness(farther);
            Require(a > b && b > 0 && a <= 1 && game.ThreatCloseness(beyond) < 0 && game.ThreatCloseness(inView) < 0 && game.ThreatCloseness(asleep) < 0,
                "Off-screen threat arrows did not mark exactly the roused monsters within 6 past the edge: near=" + a + " farther=" + b);
            report.offscreenThreatArrows = true;
            report.checks.Add("Off-screen arrows mark roused monsters up to " + TowerGame.ThreatRange + " past the screen edge, stronger as they near (" + a.ToString("F2") + " at 1.5, " + b.ToString("F2") + " at 4.5); none in view, asleep or farther out.");
            ClearEnemies(); game.enabled = true;
        }
        // Outside the practice range a dash drops a held dash strike and refunds its cooldown, so once the strike's release
        // time passes the dodge still runs its own way instead of jumping onto the strike's target.
        private void DashDropsHeldDashStrike()
        {
            Setup(); ClearEnemies(); game.enabled = false;
            Require(!game.practice, "The held dash strike fixture ran on the practice range.");
            var skills = (IList)typeof(HeroState).GetField("skills", Hidden).GetValue(game.run.Player); skills.Clear(); Call("ResetSkills");
            game.run.Player.Apply(TowerRun.GetAugment("player.dash")); game.ReconcileLoadout();
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.hero.facing = 1; game.dashTime = game.attackTimer = game.dashCooldown = 0;
            var target = Spawn(6, TowerWorld.Ground); target.stunTime = 999;
            var clocks = (IDictionary)typeof(TowerGame).GetField("skillClocks", Hidden).GetValue(game);
            foreach (DictionaryEntry entry in clocks) entry.Value.GetType().GetField("remaining").SetValue(entry.Value, 0f);
            var pending = (IList)typeof(TowerGame).GetField("pendingSkills", Hidden).GetValue(game);
            Call("TickSkills", .02f);
            Require(pending.Count == 1 && game.SkillLastStartedAt("player", "player.dash") >= 0 && game.SkillCasts("player", "player.dash") == 0,
                "The automatic dash strike did not begin a held cast at its target.");
            float release = Field<float>(pending[0], "remaining"), held = 0, x = game.hero.pos.x;
            Call("StartDash", -1f, false);
            Require(pending.Count == 0 && game.SkillRemaining("player", "player.dash") == 0, "A dash kept the held dash strike or spent its cooldown.");
            for (int frame = 0; frame < 30 && game.dashTime > 0; frame++)
            {
                Call("TickDash", .02f);
                Require(game.hero.pos.x < x, "The dashing hero stopped moving along the dash.");
                x = game.hero.pos.x; if (game.dashTime <= 0) break;
                Call("TickSkills", .02f); held += .02f;
                Require(game.hero.pos.x == x, "The dropped dash strike still moved the dodging hero: x=" + game.hero.pos.x + " expected " + x);
            }
            Require(held >= release && game.SkillCasts("player", "player.dash") == 0 && game.SkillRemaining("player", "player.dash") == 0 && target.hp == target.maxHp && x < 0,
                "A dash past the held release still fired the dash strike or spent its cooldown: release=" + release + " held=" + held + " x=" + x);
            report.dashDropsHeldDashStrike = true;
            report.checks.Add("A dash outside the practice range drops a held dash strike and refunds it: past the release time the hero only moved along the dash (x=" + x.ToString("F2") + ") and the strike's cooldown is ready.");
            skills.Clear(); Call("ResetSkills"); ClearEnemies(); game.dashTime = 0; game.invulnerable = 0; game.enabled = true;
        }
        private void SetCounter(float seconds)
        { typeof(TowerGame).GetField("<CounterRemaining>k__BackingField", Hidden).SetValue(game, seconds); }
        // A dash just begun when a melee lunge or a boss's landing meets the hero is a perfect dodge, as through a bolt: no
        // hit, the counter readies and one dash charge comes back, but no second charge within DodgeRefundInterval (3 s) of
        // the last. The fixture holds each dash through the blow (Update does not run, so the 0.12-second window stays open).
        private void DodgeEverywhere()
        {
            Setup(); ClearEnemies(); game.enabled = false; game.view.transform.position = new Vector3(0, 0, -10);
            var melee = Spawn(0, TowerWorld.Ground); melee.cooldown = 0;
            Place(game.hero, new Vector2(TowerGame.MeleeStance, TowerWorld.Ground)); game.hero.facing = 1; game.invulnerable = game.dashTime = 0; SetCounter(0);
            float hp = game.hero.hp; int dodges = game.PerfectDodges;
            Call("TickEnemy", melee, .01f);
            Require(melee.windup > 0 && game.EnemyLockedAim(melee) == game.hero.pos, "The melee dodge fixture did not lock a windup on the hero.");
            game.ReadyDash(); Call("StartDash", 0f, false);
            Require(game.dashTime > 0 && game.invulnerable > 0 && game.DashCharges == game.MaxDashCharges - 1, "The melee dodge fixture's dash did not spend one charge.");
            for (int frame = 0; frame < 60 && game.EnemyReleaseCount(melee) == 0; frame++) Call("TickEnemy", melee, .02f);
            Require(game.EnemyReleaseCount(melee) == 1 && game.hero.hp == hp && game.FloorHits == 0 && game.PerfectDodges == dodges + 1
                && Mathf.Abs(game.CounterRemaining - TowerGame.DodgeCounterDuration) < .001f && game.DashCharges == game.MaxDashCharges,
                "A dash through a melee lunge did not ready the counter and give a dash charge back: dodges +" + (game.PerfectDodges - dodges) + ", charges " + game.DashCharges + "/" + game.MaxDashCharges);
            report.meleeDodgePrimes = true;

            // Half a second short of DodgeRefundInterval (past the old 1.5-second pace), a fresh dash through a boss's landing
            // readies the counter again but gives nothing back; just past the interval since that refund, the next one gives a
            // charge again.
            float held = TowerGame.DodgeRefundInterval - .5f, past = .51f;
            ClearEnemies(); var boss = Spawn(-3, TowerWorld.Ground);
            for (int pass = 0; pass < 2; pass++)
            {
                ClearShots(); Place(boss, new Vector2(-3, TowerWorld.Ground)); Place(game.hero, new Vector2(0, TowerWorld.Ground));
                Call("ConfigureBoss", boss, 15); boss.cooldown = 0; Call("TickEnemy", boss, .01f);
                Require(game.BossPatternName(boss) == "LeapingSlam" && game.BossLockedAim(boss) == game.hero.pos, "The landing dodge fixture's boss did not aim its leaping slam at the hero.");
                game.elapsed += pass == 0 ? held : past; game.dashTime = game.invulnerable = 0; SetCounter(0);
                Call("StartDash", 0f, false);
                int charges = game.DashCharges; dodges = game.PerfectDodges; hp = game.hero.hp;
                for (int frame = 0; frame < 140 && game.BossReleaseCount(boss) == 0; frame++) Call("TickEnemy", boss, .02f);
                Require(game.BossReleaseCount(boss) == 1 && game.hero.hp == hp && game.FloorHits == 0 && game.PerfectDodges == dodges + 1
                    && Mathf.Abs(game.CounterRemaining - TowerGame.DodgeCounterDuration) < .001f,
                    "A dash through a boss's landing did not ready the counter without a hit (pass " + pass + ").");
                Require(game.DashCharges == charges + (pass == 0 ? 0 : 1), pass == 0
                    ? "A second perfect dodge " + held + " s after the refund, inside its " + TowerGame.DodgeRefundInterval + "-second pace, gave another dash charge back: " + charges + " -> " + game.DashCharges
                    : "A perfect dodge " + (held + past) + " s after the last refund, past its " + TowerGame.DodgeRefundInterval + "-second pace, gave no charge back: " + charges + " -> " + game.DashCharges);
            }
            report.bossLandingDodgePrimes = report.dodgeRefundCapped = true;
            report.checks.Add("A dash held through a melee lunge and through a boss's landing readies the counter without a hit; the first gives a dash charge back, a second " + held + " s later (inside " + TowerGame.DodgeRefundInterval + " s) does not, and one " + (held + past) + " s after the refund does.");
            ClearShots(); ClearEnemies(); SetCounter(0); game.dashTime = game.invulnerable = 0; game.enabled = true;
        }
        private void HostileBoltAt(Vector2 at) { Fire(at, at + Vector2.left, .5f, 9, true); }
        private int FriendlyShots { get { return Shots.Count - game.HostileProjectiles; } }
        // Plain cuts leave hostile bolts alone even within reach; a dash slash cuts those in its reach and no others; a counter
        // that strikes only bolts cuts them and is spent; a counter that connects cuts three and sends the nearest two straight
        // at the enemy as the player's own shots (14 u/s, 0.6 x damage, 1.2 s), which the bolt budget does not count.
        private void SwordCutsBolts()
        {
            Setup(); ClearEnemies(); game.enabled = false; game.view.transform.position = new Vector3(0, 0, -10);
            var dashSlash = typeof(TowerGame).GetField("slashIsDash", Hidden);
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.hero.facing = 1; game.AimOverride = null; game.dashTime = game.invulnerable = 0; SetCounter(0);
            Vector2 chest = game.hero.pos + Vector2.up * TowerGame.ChestHeight;
            float reach = game.run.Player.Range, dashReach = reach * TowerGame.DashSlashReach;
            int cut = game.BoltsCut, reflected = game.BoltsReflected;
            HostileBoltAt(chest + Vector2.right * .8f); HostileBoltAt(chest + Vector2.right * 1.4f);
            Require(game.HostileProjectiles == 2 && reach > 1.6f, "The bolt cut fixture did not place two bolts inside the plain swing's reach.");
            Call("PlayerSlash");
            Require(game.HostileProjectiles == 2 && game.BoltsCut == cut, "A plain slash cut hostile bolts."); report.plainSlashKeepsBolts = true;

            HostileBoltAt(chest + Vector2.right * (dashReach + 1.5f)); HostileBoltAt(chest + Vector2.left * 1.5f);
            dashSlash.SetValue(game, true); Call("PlayerSlash");
            bool keptOutside = game.HostileProjectiles == 2;
            foreach (object shot in Shots) { float x = Field<Vector2>(shot, "pos").x; keptOutside &= x > chest.x + dashReach || x < chest.x - .4f; }
            Require(keptOutside && game.BoltsCut == cut + 2 && FriendlyShots == 0 && game.BoltsReflected == reflected,
                "A dash slash did not cut exactly the bolts in its reach, or reflected one without a counter: cut " + (game.BoltsCut - cut) + ", left " + game.HostileProjectiles);
            report.dashSlashCutsBolts = true;

            ClearShots(); var far = Spawn(8, TowerWorld.Ground); far.stunTime = 999; int hits = game.CounterHits;
            SetCounter(TowerGame.DodgeCounterDuration); HostileBoltAt(chest + Vector2.right);
            Call("PlayerSlash");
            Require(game.HostileProjectiles == 0 && game.CounterRemaining == 0 && game.CounterHits == hits && FriendlyShots == 0 && far.hp == far.maxHp,
                "A counter that struck only a bolt kept its charge, counted a hit or reflected the bolt.");
            report.counterBoltCutSpends = true;

            ClearShots(); var target = Spawn(1.2f, TowerWorld.Ground); target.stunTime = 999; reflected = game.BoltsReflected;
            SetCounter(TowerGame.DodgeCounterDuration);
            HostileBoltAt(chest + Vector2.right * .6f); HostileBoltAt(chest + new Vector2(1, .3f)); HostileBoltAt(chest + new Vector2(1.4f, -.3f));
            float hp = target.hp; Call("PlayerSlash"); float slashed = target.hp;
            bool reflectedShots = FriendlyShots == 2;
            foreach (object shot in Shots)
                reflectedShots &= !Field<bool>(shot, "hostile") && Field<HeroState>(shot, "owner") == game.run.Player && Field<TowerGame.Fighter>(shot, "shooter") == null
                    && Mathf.Abs(Field<Vector2>(shot, "velocity").magnitude - TowerGame.ReflectSpeed) < .01f && Mathf.Abs(Field<float>(shot, "life") - TowerGame.ReflectLife) < .001f
                    && Mathf.Abs(Field<float>(shot, "damage") - game.PlayerDamage * TowerGame.ReflectDamageScale) < .01f;
            Require(slashed < hp && game.CounterHits == hits + 1 && game.CounterRemaining == 0 && game.HostileProjectiles == 0 && game.BoltsReflected == reflected + TowerGame.ReflectedBolts && reflectedShots,
                "A connected counter did not cut its three bolts and send exactly two back as the player's shots: reflected " + (game.BoltsReflected - reflected) + ", friendly " + FriendlyShots);
            Require(game.EnemyBoltsInView() == 0, "Reflected bolts counted toward the monsters' bolt budget.");
            Call("TickShots", .02f);
            Require(target.hp < slashed && Shots.Count == 0, "The reflected bolts did not strike the nearest enemy.");
            report.counterReflectsTwo = true;
            report.checks.Add("Plain cuts keep bolts within reach; a dash slash cuts only those within " + dashReach.ToString("F2") + "; a counter that strikes only a bolt is spent on it; a connected counter cuts three and reflects the nearest two (" + TowerGame.ReflectSpeed + " u/s, x" + TowerGame.ReflectDamageScale + " damage, " + TowerGame.ReflectLife + " s) outside the bolt budget, which then strike the enemy.");
            ClearShots(); ClearEnemies(); SetCounter(0); game.enabled = true;
        }
        // A melee fixture 1.6 ahead of the hero at x 0 (within a plain cut's reach), its windup begun.
        private TowerGame.Fighter WindingUp(int kind = 0)
        {
            var enemy = Spawn(1.6f, TowerWorld.Ground, kind); enemy.cooldown = 0;
            Call("TickEnemy", enemy, .01f);
            Require(enemy.windup > 0 && (string)Call("EnemyPatternName", enemy) == "MeleeSlam", "The poise fixture did not begin a melee windup: kind=" + kind + ", windup=" + enemy.windup);
            return enemy;
        }
        // One of the player's own cuts as PlayerSlash lands it: a dash slash, the finisher or a counter when asked.
        private void Cut(bool dash = false, bool finisher = false, bool counter = false)
        {
            typeof(TowerGame).GetField("slashIsDash", Hidden).SetValue(game, dash);
            typeof(TowerGame).GetField("slashCombo", Hidden).SetValue(game, finisher ? 2 : 0);
            SetCounter(counter ? TowerGame.DodgeCounterDuration : 0); Call("PlayerSlash");
        }
        // Holds a broken monster where it stands until its stagger ends, then lets it wind up again at once.
        private void WindUpAgain(TowerGame.Fighter enemy)
        {
            enemy.moveVelocity = 0; Place(enemy, new Vector2(1.6f, TowerWorld.Ground));
            for (int frame = 0; frame < 30 && enemy.staggerTime > 0; frame++) Call("TickEnemy", enemy, .02f);
            enemy.cooldown = 0;
            for (int frame = 0; frame < 5 && enemy.windup <= 0; frame++) Call("TickEnemy", enemy, .02f);
            Require(enemy.windup > 0 && enemy.staggerTime == 0 && enemy.poiseGuard > 0, "A broken monster did not wind up again within its guard: windup=" + enemy.windup + ", guard=" + enemy.poiseGuard);
        }
        // Poise (any monster but a boss, fixtures outside packs included): the third plain cut breaks a light monster's windup,
        // puts its telegraph flash out and holds it .35 s in its hurt pose; a heavy blow and one cut break it too, a heavy
        // monster takes five. For 1.5 s after a break plain cuts, the finisher and a dash slash take nothing while only a counter
        // breaks it again, and poise refills after a second without the player's blows. Companions' blows and skills take none
        // and still push at once; a bullet takes half a cut. The player's knockback slides out over about .15 s and stops at
        // the wall; bosses carry no poise and keep their small instant push.
        private void Poise()
        {
            Setup("michael", "gabriel"); ClearEnemies(); game.enabled = false; game.view.transform.position = new Vector3(0, 0, -10);
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.hero.facing = 1; game.AimOverride = null; game.dashTime = game.invulnerable = 0; SetCounter(0);
            var light = WindingUp(); var heavy = Spawn(30, TowerWorld.Ground, 2);
            bool sized = TowerGame.MaxPoise(light) == TowerGame.LightPoise && TowerGame.MaxPoise(heavy) == TowerGame.HeavyPoise;
            heavy.isElite = true; sized &= NearValue(TowerGame.MaxPoise(heavy), TowerGame.HeavyPoise * TowerGame.ElitePoiseScale);
            Require(sized, "Light and heavy monsters did not hold " + TowerGame.LightPoise + " and " + TowerGame.HeavyPoise + " poise, or an elite not " + TowerGame.ElitePoiseScale + " times as much.");
            report.poiseSized = true;

            // Three plain cuts while the telegraph flash is lit: two leave the windup running, the third breaks it.
            ClearEnemies(); light = WindingUp(); int breaks = game.PoiseBreaks;
            for (int frame = 0; frame < 40 && !light.art.TelegraphLit; frame++) Call("TickEnemy", light, .02f);
            Require(light.art.TelegraphLit && light.windup > .04f, "The poise fixture's windup never lit its telegraph flash: windup=" + light.windup);
            float hp = light.hp; Cut(); Cut(); Call("TickEnemy", light, .02f);
            Require(light.hp < hp && light.windup > 0 && light.staggerTime == 0 && light.art.TelegraphLit && NearValue(light.poiseDamage, 2 * TowerGame.SwordPoise) && game.PoiseBreaks == breaks,
                "Two plain cuts broke a light monster's windup or took the wrong poise: windup=" + light.windup + ", poise taken=" + light.poiseDamage);
            int released = game.EnemyReleaseCount(light); Cut();
            Require(light.windup == 0 && light.staggerTime == TowerGame.StaggerTime && light.cooldown >= TowerGame.StaggerCooldown && light.poiseGuard == TowerGame.BreakGuard
                && light.poiseDamage == 0 && game.PoiseBreaks == breaks + 1 && light.moveVelocity > TowerGame.BreakPush * TowerGame.KnockbackRate,
                "The third plain cut did not break the light windup into a stagger: windup=" + light.windup + ", stagger=" + light.staggerTime + ", cooldown=" + light.cooldown + ", slide=" + light.moveVelocity);
            for (int frame = 0; frame < 17; frame++)
            {
                Call("TickEnemy", light, .02f);
                Require(light.windup == 0 && !light.art.TelegraphLit && game.EnemyReleaseCount(light) == released && light.staggerTime > 0 && light.art.MotionName == "Hurt",
                    "A broken windup came back or released, kept its flash lit, or left the hurt pose while staggered: windup=" + light.windup + ", lit=" + light.art.TelegraphLit + ", motion=" + light.art.MotionName);
            }
            report.poiseBreaksWindup = true;

            // Within the guard plain cuts, the finisher and a dash slash take nothing, even into a fresh windup; only a counter
            // breaks it again, and within that break's guard a dash slash still cannot while another counter can.
            WindUpAgain(light);
            Cut(); Cut(); Cut(); Cut(finisher: true);
            Require(light.windup > 0 && light.staggerTime == 0 && light.poiseDamage == 0 && game.PoiseBreaks == breaks + 1,
                "Plain cuts or the finisher took poise or broke the windup again within the guard: poise taken=" + light.poiseDamage + ", windup=" + light.windup);
            Cut(dash: true);
            Require(light.windup > 0 && light.staggerTime == 0 && light.poiseDamage == 0 && game.PoiseBreaks == breaks + 1,
                "A dash slash took poise or broke the windup again within the guard: poise taken=" + light.poiseDamage + ", windup=" + light.windup);
            Cut(counter: true);
            Require(light.windup == 0 && light.staggerTime == TowerGame.StaggerTime && light.poiseGuard == TowerGame.BreakGuard && game.PoiseBreaks == breaks + 2,
                "A counter within the guard did not break the windup again: windup=" + light.windup);
            WindUpAgain(light); Cut(dash: true);
            Require(light.windup > 0 && light.staggerTime == 0 && light.poiseDamage == 0 && game.PoiseBreaks == breaks + 2,
                "A dash slash broke the windup within a counter break's guard: windup=" + light.windup);
            Cut(counter: true);
            Require(light.windup == 0 && light.staggerTime == TowerGame.StaggerTime && game.PoiseBreaks == breaks + 3, "A second counter within the guard did not break the windup again: windup=" + light.windup);
            report.poiseGuard = true;

            // Once the guard runs out a plain cut takes poise again, and a second without the player's blows refills it.
            light.stunTime = 999;
            for (int frame = 0; frame < 80 && light.poiseGuard > 0; frame++) Call("TickEnemy", light, .02f);
            light.moveVelocity = 0; Place(light, new Vector2(1.6f, TowerWorld.Ground)); Cut();
            Require(light.poiseGuard == 0 && NearValue(light.poiseDamage, TowerGame.SwordPoise), "A plain cut took no poise once the guard ran out: guard=" + light.poiseGuard + ", poise taken=" + light.poiseDamage);
            for (int frame = 0; frame < 49; frame++) Call("TickEnemy", light, .02f);
            Require(NearValue(light.poiseDamage, TowerGame.SwordPoise), "Poise refilled before a second had passed without a blow.");
            Call("TickEnemy", light, .03f);
            Require(light.poiseDamage == 0, "Poise did not refill after a second without the player's blows: " + light.poiseDamage);
            report.poiseRefills = true;

            // A dash slash, the finisher or a counter takes two, so one more plain cut breaks a light monster.
            string[] heavyBlows = { "dash slash", "finisher", "counter" };
            for (int blow = 0; blow < heavyBlows.Length; blow++)
            {
                ClearEnemies(); var struck = WindingUp(); int before = game.PoiseBreaks;
                Cut(dash: blow == 0, finisher: blow == 1, counter: blow == 2);
                Require(struck.windup > 0 && NearValue(struck.poiseDamage, TowerGame.HeavyBlowPoise) && game.PoiseBreaks == before,
                    "A " + heavyBlows[blow] + " did not take " + TowerGame.HeavyBlowPoise + " poise without breaking a light windup: " + struck.poiseDamage);
                Cut();
                Require(struck.windup == 0 && struck.staggerTime > 0 && game.PoiseBreaks == before + 1, "A " + heavyBlows[blow] + " and one plain cut did not break a light windup.");
            }
            report.poiseHeavyBlow = true;
            ClearEnemies(); var brute = WindingUp(2); int bruteBreaks = game.PoiseBreaks;
            for (int cut = 0; cut < 4; cut++) Cut();
            Require(brute.windup > 0 && game.PoiseBreaks == bruteBreaks, "Four plain cuts broke a heavy monster's windup.");
            Cut();
            Require(brute.windup == 0 && game.PoiseBreaks == bruteBreaks + 1, "The fifth plain cut did not break a heavy monster's windup.");
            report.poiseHeavyMonster = true;

            // Companions' blows and the player's skills take no poise and still push at once.
            ClearEnemies(); var watched = WindingUp();
            foreach (var ally in game.allies)
            {
                var state = game.run.GetCompanion(ally.id);
                for (int blow = 0; blow < 4; blow++)
                {
                    ClearShots(); Place(ally, new Vector2(watched.pos.x - 1, TowerWorld.Ground)); ally.cooldown = 0; ally.basicPending = false;
                    hp = watched.hp; float x = watched.pos.x;
                    Call("BeginAllyAttack", ally, state, watched); Call("ResolveAllyAttack", ally, state, 1f); Call("TickShots", .25f);
                    Require(watched.hp < hp && Mathf.Abs(watched.pos.x - x) > .05f && watched.moveVelocity == 0 && watched.poiseDamage == 0 && watched.windup > 0,
                        "A companion's blow took poise, slid instead of pushing at once, or broke the windup: " + ally.id + ", poise taken=" + watched.poiseDamage + ", moved " + (watched.pos.x - x));
                }
            }
            AugmentDefinition ice = null;
            foreach (var definition in TowerRun.Augments)
                if (definition.OwnerId == "player" && definition.Kind == RewardKind.Skill && definition.Effect == SkillEffect.IceNova) { ice = definition; break; }
            Require(ice != null, "IceNova poise fixture is missing.");
            ClearShots(); Place(watched, new Vector2(1, TowerWorld.Ground)); hp = watched.hp; float iceX = watched.pos.x;
            Call("CastSkill", game.run.Player, new SkillState(ice, game.run.Player), game.hero, watched);
            Require(watched.hp < hp && watched.pos.x > iceX && watched.moveVelocity == 0 && watched.poiseDamage == 0 && watched.windup > 0,
                "A player skill took poise, slid instead of pushing at once, or broke the windup: poise taken=" + watched.poiseDamage + ", moved " + (watched.pos.x - iceX));
            report.poiseOnlyPlayer = true;

            // A gun bullet takes half a cut's poise and slides its .1 knockback rather than pushing at once.
            ClearEnemies(); ClearShots(); var shotAt = Spawn(4, TowerWorld.Ground); shotAt.stunTime = 999;
            Call("WeaponShot", TowerAttackMode.Gun, game.hero.pos + Vector2.up * TowerGame.ChestHeight, Vector2.right * TowerGame.GunBulletSpeed, 10f, Color.white, 1f);
            float shotX = shotAt.pos.x; hp = shotAt.hp; Call("TickShots", .25f);
            Require(shotAt.hp < hp && NearValue(shotAt.poiseDamage, TowerGame.ShotPoise) && shotAt.pos.x == shotX && NearValue(shotAt.moveVelocity, .1f * TowerGame.KnockbackRate),
                "A gun bullet did not take half a cut's poise, or pushed at once: poise taken=" + shotAt.poiseDamage + ", slide=" + shotAt.moveVelocity);
            report.poiseBullet = true;

            // One plain cut knocks back .35 over time: nothing at once, about a quarter in the first .025 s, most by .15 s, all after.
            ClearEnemies(); ClearShots(); var slid = Spawn(1.6f, TowerWorld.Ground); slid.stunTime = 999;
            float from = slid.pos.x; Cut();
            Require(slid.pos.x == from && NearValue(slid.moveVelocity, .35f * TowerGame.KnockbackRate), "A plain cut pushed at once instead of sliding: moved " + (slid.pos.x - from));
            Call("TickEnemy", slid, .025f); float early = slid.pos.x - from;
            for (int frame = 1; frame < 6; frame++) Call("TickEnemy", slid, .025f);
            float middle = slid.pos.x - from;
            for (int frame = 0; frame < 24; frame++) Call("TickEnemy", slid, .025f);
            float settled = slid.pos.x - from;
            Require(early > 0 && early < .35f * .4f && middle > .35f * .75f && middle < .35f * .95f && Mathf.Abs(settled - .35f) < .015f && slid.moveVelocity == 0,
                "A plain cut's knockback did not slide .35 out over about .15 s: " + early.ToString("F3") + " at .025 s, " + middle.ToString("F3") + " at .15 s, " + settled.ToString("F3") + " at .75 s");
            report.knockbackSlides = true;
            // A dash slash's slide stops at the chamber's wall.
            ClearEnemies(); float wall = TowerWorld.Right - .8f; var walled = Spawn(wall - .3f, TowerWorld.Ground); walled.stunTime = 999;
            Place(game.hero, new Vector2(wall - 1.9f, TowerWorld.Ground)); Cut(dash: true);
            Require(walled.hp < walled.maxHp && walled.moveVelocity > 0, "The wall fixture's dash slash did not strike.");
            for (int frame = 0; frame < 30; frame++)
            { Call("TickEnemy", walled, .025f); Require(walled.pos.x <= wall + .0001f, "A sliding knockback passed the chamber's wall: x=" + walled.pos.x + ", wall=" + wall); }
            Require(Mathf.Abs(walled.pos.x - wall) < .001f && walled.moveVelocity == 0, "A knockback did not come to rest against the wall: x=" + walled.pos.x + ", wall=" + wall);
            report.knockbackWall = true;
            Place(game.hero, new Vector2(0, TowerWorld.Ground));

            // Bosses carry no poise: dash slashes neither break nor slide one, it keeps a small instant push and its telegraph.
            ClearEnemies(); var boss = Spawn(2, TowerWorld.Ground); Call("ConfigureBoss", boss, 15); boss.hp = boss.maxHp = 1000000; boss.cooldown = 0;
            Call("TickEnemy", boss, .01f); int bossBreaks = game.PoiseBreaks;
            Require(game.BossTelegraphRemaining(boss) > 0 && TowerGame.MaxPoise(boss) == 0, "The boss poise fixture did not begin its telegraph.");
            for (int blow = 0; blow < 4; blow++)
            {
                float x = boss.pos.x; Cut(dash: true);
                Require(NearValue(boss.pos.x - x, .9f * TowerGame.BossPushScale) && boss.moveVelocity == 0 && boss.staggerTime == 0 && boss.poiseDamage == 0,
                    "A dash slash took a boss's poise or slid it instead of the small instant push: moved " + (boss.pos.x - x));
            }
            Require(game.BossTelegraphRemaining(boss) > 0 && game.PoiseBreaks == bossBreaks, "The player's blows broke a boss's telegraph.");
            report.bossNoPoise = true;
            report.checks.Add("Poise: light " + TowerGame.LightPoise + ", heavy " + TowerGame.HeavyPoise + ", elite x" + TowerGame.ElitePoiseScale + ", none for bosses. The third plain cut (or a heavy blow and a cut, five for a heavy) breaks a windup, puts its flash out and holds the hurt pose " + TowerGame.StaggerTime +
                " s; for " + TowerGame.BreakGuard + " s after, plain cuts, the finisher and a dash slash take nothing while only a counter breaks it again; it refills after " + TowerGame.PoiseRefill + " s. Companions and skills take none and push at once; a bullet takes " + TowerGame.ShotPoise +
                ". A cut's .35 knockback slides " + early.ToString("F3") + " / " + middle.ToString("F3") + " / " + settled.ToString("F3") + " at .025 / .15 / .75 s and stops at the wall; a boss moves " + (.9f * TowerGame.BossPushScale).ToString("F3") + " at once.");
            ClearShots(); ClearEnemies(); SetCounter(0); game.invulnerable = 0; game.enabled = true;
        }
        // The sword's beat under a held attack through real Update (two quick cuts, then a finisher slower to swing and to
        // recover from, its swing not clipped short); the bounce off an enemy below a down-cut in the air (9, 6, 3, then none;
        // at most twice running off one enemy; the air jump given back once; landing starts afresh; none on the ground); a
        // dash aimed up lifting the hero once an airtime (a bounce or a landing gives it back); and a fast fall's landing
        // strike, the player's own, only within 1.2 of the feet, after a drop of 1.5 or more and .8 s after the last slam.
        private IEnumerator AirStrikes()
        {
            Setup(); BareFloor();
            var skills = (IList)typeof(HeroState).GetField("skills", Hidden).GetValue(game.run.Player); skills.Clear(); Call("ResetSkills");
            float interval = game.PlayerAttackInterval, swing = Mathf.Clamp(interval * .85f, .18f, .42f);
            var starts = new List<float>(); var cooldowns = new List<float>(); var lengths = new List<float>();
            float last = game.LastPlayerAttackStartedAt, limit = Time.realtimeSinceStartup + 5;
            game.AttackOverride = true;
            while (starts.Count < 4)
            {
                Require(Time.realtimeSinceStartup < limit, "A held attack did not swing four times.");
                yield return null;
                if (game.LastPlayerAttackStartedAt == last) continue;
                last = game.LastPlayerAttackStartedAt; starts.Add(last); cooldowns.Add(game.slashCooldown); lengths.Add(game.LastPlayerAttackDuration);
            }
            game.AttackOverride = false;
            float quick = interval * TowerGame.QuickBeat, heavy = interval * TowerGame.FinisherBeat;
            Require(NearValue(cooldowns[0], quick) && NearValue(cooldowns[1], quick) && NearValue(cooldowns[2], heavy) && NearValue(cooldowns[3], quick),
                "A held combo's cooldowns were not quick, quick, heavy, quick: " + string.Join(", ", cooldowns));
            Require(NearValue(lengths[0], swing * TowerGame.QuickBeat) && NearValue(lengths[1], swing * TowerGame.QuickBeat) && NearValue(lengths[2], swing * TowerGame.FinisherBeat),
                "The quick cuts or the finisher did not swing for their beat (the finisher clipped?): " + string.Join(", ", lengths));
            float firstGap = starts[1] - starts[0], secondGap = starts[2] - starts[1], finisherGap = starts[3] - starts[2];
            Require(firstGap < finisherGap && secondGap < finisherGap,
                "The first two cuts did not come quicker than the finisher's beat: " + firstGap.ToString("F3") + ", " + secondGap.ToString("F3") + ", " + finisherGap.ToString("F3"));
            report.comboRhythm = true;
            for (int frame = 0; frame < 60 && game.attackTimer > 0; frame++) yield return null;

            game.enabled = false; game.attackTimer = 0;
            var first = Spawn(.4f, TowerWorld.Ground); var second = Spawn(30, TowerWorld.Ground); first.stunTime = second.stunTime = 999;
            LiftHero();
            Require(NearValue(DownCut(first, second), TowerGame.PogoVelocities[0]) && game.airJumps == TowerGame.MaxAirJumps && game.PogoBounces == 1,
                "The first down-cut bounce in the air was not 9 or did not give the air jump back.");
            Require(NearValue(DownCut(second, first), TowerGame.PogoVelocities[1]) && game.airJumps == 0, "The second bounce was not 6, or gave the air jump back twice in one airtime.");
            Require(NearValue(DownCut(first, second), TowerGame.PogoVelocities[2]), "The third bounce was not 3.");
            Require(NearValue(DownCut(second, first), -3) && game.PogoBounces == 3, "A fourth down-cut in one airtime still bounced.");
            report.pogoDiminishes = true;
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); Call("LandingFeedback", true, 0f);
            Require(game.PogoBounces == 0, "Landing did not start the down-cut bounces afresh.");
            LiftHero();
            Require(NearValue(DownCut(first, second), TowerGame.PogoVelocities[0]) && NearValue(DownCut(first, second), TowerGame.PogoVelocities[1]), "Bounces after landing did not start again from 9.");
            Require(NearValue(DownCut(first, second), -3) && game.PogoBounces == 2, "A third bounce running off the same enemy was allowed.");
            Require(NearValue(DownCut(second, first), TowerGame.PogoVelocities[2]) && game.PogoBounces == 3, "Another enemy did not take the airtime's last bounce after the same-enemy limit.");
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); Call("LandingFeedback", true, 0f);
            Require(NearValue(DownCut(first, second), -3) && game.PogoBounces == 0, "A down-cut on the ground bounced the hero.");
            report.pogoSameEnemyLimit = true;

            // A dash aimed up in the air lifts the hero once an airtime; the next holds its height like a flat air dash until a
            // down-cut bounce or a landing gives the lift back. One from the floor leaves the airtime's lift unspent.
            LiftHero(); float firstRise = AirDashRise(), secondRise = AirDashRise();
            Require(firstRise > 2 && secondRise < .001f,
                "A second dash aimed up in one airtime still lifted the hero, or the first did not: " + firstRise.ToString("F2") + " then " + secondRise.ToString("F2"));
            LiftHero(); Require(NearValue(DownCut(first, second), TowerGame.PogoVelocities[0]), "The air dash fixture's down-cut did not bounce.");
            LiftHero(); float afterBounce = AirDashRise(), spentAfterBounce = AirDashRise();
            Require(afterBounce > 2 && spentAfterBounce < .001f,
                "A down-cut bounce did not give the rising air dash back once: " + afterBounce.ToString("F2") + " then " + spentAfterBounce.ToString("F2"));
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); Call("LandingFeedback", true, 0f);
            LiftHero(); float afterLanding = AirDashRise();
            Require(afterLanding > 2, "Landing did not give the rising air dash back: " + afterLanding.ToString("F2"));
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); Call("LandingFeedback", true, 0f);
            float fromFloor = AirDashRise(), airAfterFloor = AirDashRise(), spentAfterFloor = AirDashRise();
            Require(fromFloor > 2 && airAfterFloor > 2 && spentAfterFloor < .001f,
                "A dash aimed up from the floor spent the airtime's rising air dash, or a later one still lifted: " + fromFloor.ToString("F2") + ", " + airAfterFloor.ToString("F2") + ", " + spentAfterFloor.ToString("F2"));
            report.airDashRiseOnce = true;
            game.dashTime = game.invulnerable = 0; game.ReadyDash(); SetCounter(0);

            game.AimOverride = null; Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.hero.facing = 1; game.enabled = true;
            Place(first, new Vector2(1, TowerWorld.Ground)); Place(second, new Vector2(1.5f, TowerWorld.Ground));
            // The down-cuts above left their knockback unslid (nothing ticked the enemies) and broke their poise; placed
            // afresh they keep neither.
            first.moveVelocity = second.moveVelocity = 0;
            first.poiseDamage = first.poiseRest = first.poiseGuard = second.poiseDamage = second.poiseRest = second.poiseGuard = 0;
            first.hp = first.maxHp; second.hp = second.maxHp; int slams = game.FastFallSlams;
            yield return HopOnce(false);
            Require(game.FastFallSlams == slams && first.hp == first.maxHp, "An ordinary landing struck like a fast fall.");
            // A fast fall begun only .8 above the ground lands as an ordinary fall, and starts no wait for the next slam.
            yield return HopOnce(true, .8f);
            Require(game.FastFallSlams == slams && first.hp == first.maxHp,
                "A fast fall begun .8 above the ground still slammed (a slam needs " + TowerGame.FastFallSlamHeight + " of drop).");
            report.fastFallSlamHeight = true;
            yield return HopOnce(true);
            Require(game.FastFallSlams == slams + 1, "A fast fall did not land as a slam.");
            Require(NearValue(first.maxHp - first.hp, game.PlayerDamage * TowerGame.FastFallSlamScale) && second.hp == second.maxHp,
                "A fast fall's landing did not strike only within " + TowerGame.FastFallSlamRadius + " of the feet for half the player's damage: inside=" + (first.maxHp - first.hp) + " outside=" + (second.maxHp - second.hp));
            report.fastFallSlam = true;
            Require(NearValue(first.poiseDamage, TowerGame.SlamPoise) && first.moveVelocity > 0 && second.poiseDamage == 0,
                "A fast fall's landing did not take " + TowerGame.SlamPoise + " poise and slide its knockback: poise taken=" + first.poiseDamage + ", slide=" + first.moveVelocity);
            report.fastFallPoise = true;
            // A second fast fall within the interval after a slam lands as an ordinary fall; once it has passed the next slams again.
            float slammedAt = game.elapsed; first.moveVelocity = 0; Place(first, new Vector2(1, TowerWorld.Ground)); float slammedHp = first.hp;
            yield return HopOnce(true);
            Require(game.elapsed - slammedAt < TowerGame.FastFallSlamInterval,
                "The interval fixture's second hop took " + (game.elapsed - slammedAt).ToString("F2") + " s, too long to test the slam's " + TowerGame.FastFallSlamInterval + " s interval.");
            Require(game.FastFallSlams == slams + 1 && first.hp == slammedHp,
                "A second fast fall " + (game.elapsed - slammedAt).ToString("F2") + " s after a slam still slammed (interval " + TowerGame.FastFallSlamInterval + " s).");
            float waitLimit = Time.realtimeSinceStartup + 3;
            while (game.elapsed - slammedAt < TowerGame.FastFallSlamInterval)
            { Require(Time.realtimeSinceStartup < waitLimit, "Game time did not run out the slam's interval."); yield return null; }
            first.moveVelocity = 0; Place(first, new Vector2(1, TowerWorld.Ground));
            yield return HopOnce(true);
            Require(game.FastFallSlams == slams + 2 && first.hp < slammedHp, "A fast fall after the slam's " + TowerGame.FastFallSlamInterval + " s interval did not slam again.");
            report.fastFallSlamInterval = true;
            report.checks.Add("Held sword beat: cooldowns " + string.Join(", ", cooldowns) + ", gaps " + firstGap.ToString("F3") + "/" + secondGap.ToString("F3") + "/" + finisherGap.ToString("F3") +
                "; down-cut bounces 9, 6, 3 then none, at most twice off one enemy, air jump given back once, reset on landing; a dash aimed up rises " + firstRise.ToString("F2") +
                " once an airtime (then " + secondRise.ToString("F2") + "), given back by a bounce or a landing; a fast fall's landing strikes within " + TowerGame.FastFallSlamRadius +
                " only, after a drop of " + TowerGame.FastFallSlamHeight + " or more (none from .8) and no sooner than " + TowerGame.FastFallSlamInterval + " s after the last slam.");
            ClearEnemies(); game.FastFallOverride = false;
        }
        // A flat floor without platforms, so a hop lands where it rose.
        private void BareFloor()
        {
            var world = (TowerWorld)typeof(TowerGame).GetField("world", Hidden).GetValue(game); var layout = world.CurrentLayout;
            var ground = new TowerSurface(new[] { TowerTerrainGenerator.TerrainLeft, TowerTerrainGenerator.TerrainRight }, new[] { TowerWorld.Ground, TowerWorld.Ground });
            world.SetTheme(game.run.Theme.Id, game.run.Floor, game.seed, new TowerFloorLayout(layout.Id, layout.Archetype, layout.Floor, ground, new List<TowerPlatform>(), layout.ExitX));
        }
        private void LiftHero() { Place(game.hero, new Vector2(0, TowerWorld.Ground + 2)); game.hero.grounded = false; }
        // One right-button dash aimed steeply up, stepped to its end in Update's order (in the air from a fall at 3); how far
        // above where it began it carried the hero.
        private float AirDashRise()
        {
            var hero = game.hero; if (!hero.grounded) hero.velocityY = -3;
            float start = hero.pos.y, peak = start;
            game.AimOverride = hero.pos + Vector2.up * TowerGame.ChestHeight + new Vector2(1, 3); Call("TickAim"); Call("StartDash", 0f, true);
            for (int step = 0; step < 30 && game.dashTime > 0; step++)
            { Call("TickDash", .02f); Call("StepGravity", hero, .02f, false); peak = Mathf.Max(peak, hero.pos.y); }
            Require(game.dashTime <= 0, "The air dash fixture's dash never ended.");
            return peak - start;
        }
        // One cut aimed steeply down at the enemy just below the hero while the other stands far off; the hero's vertical
        // speed afterwards (the bounce, or the -3 it was falling at).
        private float DownCut(TowerGame.Fighter target, TowerGame.Fighter other)
        {
            Place(target, new Vector2(game.hero.pos.x + .4f, TowerWorld.Ground)); Place(other, new Vector2(game.hero.pos.x + 30, TowerWorld.Ground));
            game.hero.velocityY = -3; game.airJumps = 0;
            game.AimOverride = game.hero.pos + Vector2.up * TowerGame.ChestHeight + new Vector2(.35f, -2.6f);
            float hp = target.hp, otherHp = other.hp;
            Call("TickAim"); Call("AimSwing"); Call("PlayerSlash");
            Require(target.hp < hp && other.hp == otherHp, "A cut aimed down did not strike only the enemy below.");
            return game.hero.velocityY;
        }
        // A real jump from where the hero stands; with fastFall S is pressed fresh in the air on the way up, once the hero is
        // height above the ground (by default enough for a slam; the jump peaks near 2.29).
        private IEnumerator HopOnce(bool fastFall, float height = 1.6f)
        {
            float ground = game.hero.pos.y, limit = Time.realtimeSinceStartup + 3;
            game.JumpOverride = true; yield return null; game.JumpOverride = false;
            Require(!game.hero.grounded, "The hop did not leave the ground.");
            if (fastFall)
            {
                while (game.hero.pos.y < ground + height) { Require(Time.realtimeSinceStartup < limit, "The hop never rose " + height + " above the ground."); yield return null; }
                game.FastFallOverride = true; yield return null; game.FastFallOverride = false;
                Require(game.FastFalling && game.hero.velocityY <= TowerGame.FastFallSpeed, "A fresh S in the air did not drop at the fast fall's speed: " + game.hero.velocityY);
            }
            while (!game.hero.grounded) { Require(Time.realtimeSinceStartup < limit, "The hop never landed."); yield return null; }
        }
        private static bool NearValue(float a, float b) { return Mathf.Abs(a - b) < .01f; }
        private IEnumerator Patterns()
        {
            // Production preparation and release remain intact without projected paths or danger markers.
            Setup(); ClearEnemies(); game.enabled = false;
            Place(game.hero, new Vector2(0, TowerWorld.Ground));
            var melee = Spawn(1, TowerWorld.Ground); melee.cooldown = 0;
            var renderers = new HashSet<SpriteRenderer>(game.GetComponentsInChildren<SpriteRenderer>(true));
            Call("TickEnemy", melee, .01f);
            // The light slam's windup is .55 s (the second stage's shorter windups); it still precedes any damage below.
            Require((string)Call("EnemyPatternName", melee) == "MeleeSlam" && melee.windup >= .53f, "Melee attack lost its preparation delay.");
            float hp = game.hero.hp;
            while (melee.windup > .021f)
            {
                RequireNoPreparationOverlay(renderers); Call("TickEnemy", melee, .02f);
                Require(game.hero.hp == hp && (int)Call("EnemyReleaseCount", melee) == 0, "Melee damage occurred before preparation completed.");
            }
            RequireNoPreparationOverlay(renderers);
            yield return VerifyPatterns();
            yield return VerbBullets();
            report.preparationHasNoOverlays = report.preparationBeforeDamage = true;
        }
        private void RequireNoPreparationOverlay(HashSet<SpriteRenderer> before)
        {
            foreach (var renderer in game.GetComponentsInChildren<SpriteRenderer>(true))
                Require(before.Contains(renderer), "An enemy created a path or danger renderer during preparation: " + renderer.name);
        }
        private IEnumerator VerifyPatterns()
        {
            string[] names = { "AimedFan", "GapWave", "RadialGap" };
            // The fan's three bolts, the wave's first crest pair and, with the hero 7 away (out of ring reach), the ring
            // cast's tight bundle of three.
            int[] counts = { 3, 2, 3 };
            for (int pattern = 0; pattern < names.Length; pattern++)
            {
                Setup(); ClearEnemies(); game.enabled = false;
                Place(game.hero, new Vector2(-4, TowerWorld.Ground));
                var shooter = Spawn(3, TowerWorld.Ground, 1);
                Call("ConfigureEnemy", shooter, 1, pattern); shooter.cooldown = 0;
                var renderers = new HashSet<SpriteRenderer>(game.GetComponentsInChildren<SpriteRenderer>(true));
                Call("TickEnemy", shooter, .01f);
                Require((string)Call("EnemyPatternName", shooter) == names[pattern], "The deterministic enemy pattern did not match its ordinal.");
                Vector2 locked = (Vector2)Call("EnemyLockedAim", shooter);
                // Ranged windups are .7 s (.85 s for the ring cast) on every floor, and recovery is 1.6 s at the floor's rate.
                float windup = pattern == 2 ? TowerGame.RadialWindup : TowerGame.RangedWindup;
                Require(Mathf.Abs((float)Call("EnemyTelegraphRemaining", shooter) - windup) < .002f && Vector2.Distance(locked, game.hero.pos) < .001f, "Enemy aim or preparation duration changed.");
                Require(Mathf.Abs(shooter.cooldown - (windup + TowerGame.RangedRecovery / shooter.attackRateScale)) < .002f, "Ranged recovery did not follow the 1.6 s rule: " + shooter.cooldown);
                RequireNoPreparationOverlay(renderers); float hp = game.hero.hp;
                Place(game.hero, new Vector2(-1, TowerWorld.Ground)); Call("TickEnemy", shooter, .1f);
                Require((Vector2)Call("EnemyLockedAim", shooter) == locked, "An enemy retargeted during preparation."); report.lockedAim = true;
                if (pattern == 0) yield return Capture("preparation", "탄막 검증 · 경로 표시 없이 공격 준비 동작 유지");
                for (int frame = 0; frame < 150 && (int)Call("EnemyReleaseCount", shooter) == 0; frame++)
                {
                    RequireNoPreparationOverlay(renderers);
                    Require(Shots.Count == 0 && game.hero.hp == hp, "An enemy attacked before preparation completed.");
                    Call("TickEnemy", shooter, .02f);
                }
                Require((int)Call("EnemyReleaseCount", shooter) == 1 && Shots.Count == counts[pattern], "Incorrect first volley count for " + names[pattern] + ": " + Shots.Count);
                Vector2 direction = (locked - shooter.pos).normalized;
                var angles = new List<float>(); var velocities = new Dictionary<object, Vector2>(); var positions = new Dictionary<object, Vector2>();
                foreach (object shot in Shots)
                { var velocity = Field<Vector2>(shot, "velocity"); angles.Add(Vector2.SignedAngle(direction, velocity)); velocities.Add(shot, velocity); positions.Add(shot, Field<Vector2>(shot, "pos")); }
                angles.Sort();
                // Every monster bolt lives three seconds, flies straight at its fixed speed and remembers its shooter.
                foreach (object shot in Shots)
                    Require(Mathf.Abs(Field<float>(shot, "life") - TowerGame.EnemyBoltLife) < .001f && Field<TowerGame.Fighter>(shot, "shooter") == shooter, "A monster bolt did not live " + TowerGame.EnemyBoltLife + " s or lost its shooter.");
                report.enemyBoltLife = true;
                float aimedSpeed = TowerGame.AimedBoltSpeed * shooter.bulletSpeedScale;
                if (pattern == 0)
                {
                    Require(Mathf.Abs(angles[0] + 12) < .6f && Mathf.Abs(angles[1]) < .6f && Mathf.Abs(angles[2] - 12) < .6f, "Aimed fan spacing must be -12/0/+12 degrees.");
                    foreach (var velocity in velocities.Values) Require(Mathf.Abs(velocity.magnitude - aimedSpeed) < .01f, "The aimed fan did not fly at " + aimedSpeed);
                    report.fanPattern = true;
                }
                else if (pattern == 1)
                {
                    // The first crest: a level pair from the shooter's x toward the locked side, at chest height over the
                    // locked aim's ground and WavePair below it. The middle bolt fills the hero's chest line; no gap is left there.
                    var heights = new List<float>();
                    foreach (var pair in positions)
                    {
                        Require(Mathf.Abs(pair.Value.x - shooter.pos.x) < .001f && Mathf.Abs(velocities[pair.Key].y) < .001f && velocities[pair.Key].x < 0 && Mathf.Abs(velocities[pair.Key].magnitude - aimedSpeed) < .01f,
                            "A wave bolt did not leave level from the shooter's x toward the locked side.");
                        heights.Add(pair.Value.y - locked.y);
                    }
                    heights.Sort();
                    Require(Mathf.Abs(heights[1] - .9f) < .001f && Mathf.Abs(heights[0] - (.9f - TowerGame.WavePair)) < .001f, "The wave's first crest did not run at the locked chest height: " + string.Join(", ", heights));
                    report.gapPattern = true;
                }
                else
                {
                    Require(Mathf.Abs(angles[0] + 6) < .6f && Mathf.Abs(angles[1]) < .6f && Mathf.Abs(angles[2] - 6) < .6f, "Out of ring reach the ring cast did not fire a -6/0/+6 bundle.");
                    foreach (var velocity in velocities.Values) Require(Mathf.Abs(velocity.magnitude - aimedSpeed) < .01f, "The bundle did not fly at " + aimedSpeed);
                    report.radialPattern = true;
                }
                Place(game.hero, new Vector2(-8, TowerWorld.Ground)); typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, game.hero.pos); Call("TickShots", .1f);
                foreach (object shot in Shots)
                {
                    Require(Field<Vector2>(shot, "velocity") == velocities[shot], "A hostile bullet homed after release.");
                    Require(Vector2.Distance(Field<Vector2>(shot, "pos"), positions[shot] + velocities[shot] * .1f) < .001f, "A hostile bullet did not follow its fixed linear path.");
                }
                report.fixedBulletTrajectory = true;
                float firstRelease = (float)Call("EnemyLastReleaseAt", shooter);
                if (pattern == 1)
                {
                    // The second crest follows WaveCrestDelay later, WaveCrestRise higher.
                    var firstCrest = new List<object>(); foreach (object shot in Shots) firstCrest.Add(shot);
                    for (int frame = 0; frame < 40 && (int)Call("EnemyReleaseCount", shooter) < 2; frame++) Call("TickEnemy", shooter, .02f);
                    Require((int)Call("EnemyReleaseCount", shooter) == 2 && Shots.Count == 4, "Incorrect second crest count for " + names[pattern] + ": " + Shots.Count);
                    Require(Mathf.Abs((float)Call("EnemyLastReleaseAt", shooter) - firstRelease - TowerGame.WaveCrestDelay) < .025f, "The wave's second crest did not follow " + TowerGame.WaveCrestDelay + " s later.");
                    var heights = new List<float>();
                    foreach (object shot in Shots) if (!firstCrest.Contains(shot)) heights.Add(Field<Vector2>(shot, "pos").y - locked.y);
                    heights.Sort();
                    Require(heights.Count == 2 && Mathf.Abs(heights[1] - (.9f + TowerGame.WaveCrestRise)) < .001f && Mathf.Abs(heights[0] - (.9f + TowerGame.WaveCrestRise - TowerGame.WavePair)) < .001f,
                        "The wave's second crest did not run " + TowerGame.WaveCrestRise + " above the first: " + string.Join(", ", heights));
                    Require((Vector2)Call("EnemyLockedAim", shooter) == locked, "A follow-up wave retargeted the hero.");
                }
                else if (pattern == 2)
                {
                    // A light shooter's bundle has no second volley.
                    for (int frame = 0; frame < 40; frame++) Call("TickEnemy", shooter, .02f);
                    Require((int)Call("EnemyReleaseCount", shooter) == 1 && Shots.Count == 3, "The light shooter's bundle fired a follow-up: " + Shots.Count);
                }
                report.checks.Add(names[pattern] + ": locked aim / no preparation overlays / first volley " + counts[pattern] + " / fixed trajectories / verb-answer spacing and follow-up verified.");
                if (pattern == 2)
                {
                    typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, game.hero.pos);
                    Call("TickShots", .25f); Call("TickEffects", .25f); game.view.transform.position = new Vector3(-2, 0, -10);
                    yield return Capture("bullets", "탄막 검증 · 멀리서는 빠른 3발 묶음");
                }
            }
            game.enabled = true;
        }
        // Ranged volleys ask for a verb instead of leaving a gap to stand in. Steps run by hand at a fixed .01 s in the real
        // frame order on a floor without ledges, so the jump below meets the wave at the same moment on every machine.
        private IEnumerator VerbBullets()
        {
            // Standing at the locked aim, the hero is hit by the wave's first crest while the second still flies.
            var shooter = WaveFixture(); float hp = game.hero.hp;
            var crest = ReleaseFirstCrest(shooter);
            for (int step = 0; step < 150 && game.hero.hp == hp; step++) StepFixture(.01f, shooter);
            bool crestSpent = false; foreach (object shot in crest) if (!Shots.Contains(shot)) crestSpent = true;
            int later = 0; foreach (object shot in Shots) if (!crest.Contains(shot)) later++;
            Require(game.hero.hp < hp && crestSpent && later == 2 && (int)Call("EnemyReleaseCount", shooter) == 2, "A hero standing at the locked aim was not hit by the wave's first crest (hp " + game.hero.hp + ", later " + later + ").");
            report.waveStandingHit = true;

            // A full jump begun .07 s before the first crest reaches the chest clears it and is near its top as the second passes.
            shooter = WaveFixture(); hp = game.hero.hp;
            crest = ReleaseFirstCrest(shooter);
            float speed = Mathf.Abs(Field<Vector2>(crest[0], "velocity").x), peak = 0; bool jumped = false;
            for (int step = 0; step < 220; step++)
            {
                if (!jumped)
                {
                    float lead = float.MaxValue; foreach (object shot in crest) lead = Mathf.Min(lead, Field<Vector2>(shot, "pos").x - game.hero.pos.x);
                    if (lead <= TowerGame.PlayerBulletHitRadius + .16f + speed * .07f) { game.hero.velocityY = TowerPhysics.JumpVelocity; game.hero.grounded = false; jumped = true; }
                }
                StepFixture(.01f, shooter); peak = Mathf.Max(peak, game.hero.pos.y - TowerWorld.Ground);
            }
            int passed = 0; foreach (object shot in Shots) if (Field<Vector2>(shot, "pos").x < game.hero.pos.x - 1) passed++;
            Require(jumped && game.hero.hp == hp && peak > 2 && (int)Call("EnemyReleaseCount", shooter) == 2 && passed == 4,
                "A jump carried to its top did not clear both crests of the wave: hp " + game.hero.hp + "/" + hp + ", peak " + peak.ToString("F2") + ", passed " + passed + ".");
            report.waveApexJumpSafe = true;

            // The ring cast by distance: within CloseRingReach a full ring of eight with one bolt on the aim, beyond it a bundle of three.
            foreach (float distance in new[] { 3f, 6f })
            {
                Setup(); ClearEnemies(); ClearShots(); game.enabled = false; game.invulnerable = 0;
                Place(game.hero, new Vector2(0, TowerWorld.Ground));
                var caster = Spawn(distance, TowerWorld.Ground, 1); Call("ConfigureEnemy", caster, 1, 2); caster.cooldown = 0;
                Call("TickEnemy", caster, .01f);
                Require((string)Call("EnemyPatternName", caster) == "RadialGap" && Mathf.Abs(caster.windup - TowerGame.RadialWindup) < .002f, "The ring cast fixture did not begin its windup.");
                for (int frame = 0; frame < 60 && (int)Call("EnemyReleaseCount", caster) == 0; frame++) Call("TickEnemy", caster, .02f);
                bool close = distance < TowerGame.CloseRingReach;
                RequireVolley(caster, close ? new[] { 0f, 45, 90, 135, 180, 225, 270, 315 } : new[] { -6f, 0, 6 },
                    (close ? TowerGame.RingBoltSpeed : TowerGame.AimedBoltSpeed) * caster.bulletSpeedScale, close ? "close ring" : "far bundle");
                for (int frame = 0; frame < 40; frame++) Call("TickEnemy", caster, .02f);
                Require((int)Call("EnemyReleaseCount", caster) == 1, "A light shooter's ring cast fired a second ring.");
            }
            report.ringByDistance = true;

            // A heavy follows its close ring with four more a quarter turn apart, turned 22.5 degrees off it, .5 s later (61st floor).
            float[] eight = { 0f, 45, 90, 135, 180, 225, 270, 315 }, four = { 22.5f, 112.5f, 202.5f, 292.5f };
            var heavy = HeavyRingFixture(61); var first = new List<object>(); foreach (object shot in Shots) first.Add(shot);
            float firstAt = (float)Call("EnemyLastReleaseAt", heavy);
            for (int frame = 0; frame < 40 && (int)Call("EnemyReleaseCount", heavy) < 2; frame++) Call("TickEnemy", heavy, .02f);
            Require(TowerGame.RingBolts == 8 && TowerGame.HeavySecondRingBolts == 4 && TowerGame.RingBolts + TowerGame.HeavySecondRingBolts <= TowerGame.EnemyBoltCap(1),
                "A heavy's two rings are not 8 + 4 bolts within the first floors' budget.");
            Require((int)Call("EnemyReleaseCount", heavy) == 2 && Mathf.Abs((float)Call("EnemyLastReleaseAt", heavy) - firstAt - TowerGame.HeavyRingDelay) < .025f && Shots.Count == 12,
                "A heavy's second ring did not follow " + TowerGame.HeavyRingDelay + " s later with four: " + Shots.Count + " bolts.");
            RequireVolley(heavy, four, TowerGame.RingBoltSpeed * heavy.bulletSpeedScale, "heavy second ring", first);
            typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, game.hero.pos);
            Call("TickShots", .3f); Call("TickEffects", .3f);
            yield return Capture("ring", "탄막 검증 · 붙으면 고리, 중장은 엇갈린 두 번째 고리");
            // On the first floors (12 bolts) the four were reserved with the eight as the windup began: the second ring fires on
            // time without asking the budget again, while a fan that would crowd it (8 + 4 owed + 3) waits.
            heavy = HeavyRingFixture(1); firstAt = (float)Call("EnemyLastReleaseAt", heavy); first = new List<object>(); foreach (object shot in Shots) first.Add(shot);
            var crowd = Spawn(6, TowerWorld.Ground, 1); Call("ConfigureEnemy", crowd, 1, 0); crowd.cooldown = 0;
            int most = game.EnemyBoltsInView(); bool crowdWaited = true;
            for (int frame = 0; frame < 40 && (int)Call("EnemyReleaseCount", heavy) < 2; frame++)
            {
                Call("TickEnemy", heavy, .02f); Call("TickEnemy", crowd, .02f); most = Mathf.Max(most, game.EnemyBoltsInView());
                crowdWaited &= crowd.windup <= 0 && (int)Call("EnemyReleaseCount", crowd) == 0;
            }
            Require((int)Call("EnemyReleaseCount", heavy) == 2 && Mathf.Abs((float)Call("EnemyLastReleaseAt", heavy) - firstAt - TowerGame.HeavyRingDelay) < .025f,
                "A heavy's second ring did not fire on time under floor 1's budget: releases " + Call("EnemyReleaseCount", heavy) + ".");
            Require(crowdWaited && most <= TowerGame.EnemyBoltCap(1), "A fan began beside a heavy's owed second ring, or the screen held " + most + " monster bolts (budget 12).");
            RequireVolley(heavy, four, TowerGame.RingBoltSpeed * heavy.bulletSpeedScale, "floor 1 heavy second ring", first);
            report.heavyRingFollowUp = report.followUpOnTime = true;

            // The ring's reach is settled as its windup begins: a heavy pushed out past CloseRingReach before it looses still
            // casts its ring of eight, then the four.
            Setup(); ClearEnemies(); ClearShots(); game.enabled = false; game.invulnerable = 0;
            Place(game.hero, new Vector2(0, TowerWorld.Ground));
            heavy = Spawn(3, TowerWorld.Ground, 2); Call("ConfigureEnemy", heavy, 2, 1); heavy.cooldown = 0; Call("TickEnemy", heavy, .02f);
            Require((string)Call("EnemyPatternName", heavy) == "RadialGap" && heavy.windup > 0, "The pushed ring fixture did not begin its windup.");
            Place(heavy, new Vector2(TowerGame.CloseRingReach + 2, TowerWorld.Ground));
            for (int frame = 0; frame < 60 && (int)Call("EnemyReleaseCount", heavy) == 0; frame++) Call("TickEnemy", heavy, .02f);
            RequireVolley(heavy, eight, TowerGame.RingBoltSpeed * heavy.bulletSpeedScale, "ring pushed out of reach");
            heavy.cooldown = 999; first = new List<object>(); foreach (object shot in Shots) first.Add(shot);
            for (int frame = 0; frame < 40 && (int)Call("EnemyReleaseCount", heavy) < 2; frame++) Call("TickEnemy", heavy, .02f);
            RequireVolley(heavy, four, TowerGame.RingBoltSpeed * heavy.bulletSpeedScale, "second ring pushed out of reach", first);
            report.ringReachLocked = true;
            report.checks.Add("Wave: standing hit by the first crest, a full jump .07 s ahead clears both (peak " + peak.ToString("F2") + "). Ring cast: 8 within " + TowerGame.CloseRingReach + ", 3 beyond, settled as the windup begins; heavy's four turned bolts " + TowerGame.HeavyRingDelay + " s later on floors 61 and 1 alike, a crowding fan waiting behind the owed four (most on screen " + most + ").");

            BoltBudget();
            ShooterFallClearsBolts();
            game.enabled = true;
        }
        /// <summary>A floor without ledges, the hero standing at x 0 and a wave shooter 5 to its right with its windup begun.</summary>
        private TowerGame.Fighter WaveFixture()
        {
            Setup(); ClearEnemies(); ClearShots(); BareFloor(); game.enabled = false;
            game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0; game.dashTime = 0;
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.hero.support = -1; game.hero.lastX = 0;
            var shooter = Spawn(5, TowerWorld.Ground, 1); Call("ConfigureEnemy", shooter, 1, 1); shooter.cooldown = 0;
            StepFixture(.01f, shooter);
            Require((string)Call("EnemyPatternName", shooter) == "GapWave" && shooter.windup > 0, "The wave fixture did not begin its windup.");
            return shooter;
        }
        /// <summary>Steps to the wave's release and returns its first crest; the shooter then begins nothing new.</summary>
        private List<object> ReleaseFirstCrest(TowerGame.Fighter shooter)
        {
            for (int step = 0; step < 200 && (int)Call("EnemyReleaseCount", shooter) == 0; step++) StepFixture(.01f, shooter);
            var crest = new List<object>(); foreach (object shot in Shots) crest.Add(shot);
            Require((int)Call("EnemyReleaseCount", shooter) == 1 && crest.Count == 2, "The wave fixture did not release its first crest.");
            shooter.cooldown = 999; return crest;
        }
        // One fixed step in the real frame order: the hero under gravity, the shooters, then every bolt.
        private void StepFixture(float dt, params TowerGame.Fighter[] shooters)
        {
            typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, game.hero.pos);
            Call("StepGravity", game.hero, dt, false); game.hero.root.transform.position = game.hero.pos;
            foreach (var shooter in shooters) Call("TickEnemy", shooter, dt);
            Call("TickShots", dt);
        }
        /// <summary>A heavy 3 from the hero on <paramref name="floor"/>, stepped to the release of its first close ring.</summary>
        private TowerGame.Fighter HeavyRingFixture(int floor)
        {
            Setup(); ClearEnemies(); ClearShots(); game.enabled = false; game.invulnerable = 0;
            typeof(TowerRun).GetField("<Floor>k__BackingField", Hidden).SetValue(game.run, floor);
            Place(game.hero, new Vector2(0, TowerWorld.Ground));
            var heavy = Spawn(3, TowerWorld.Ground, 2); Call("ConfigureEnemy", heavy, 2, 1); heavy.cooldown = 0;
            for (int frame = 0; frame < 60 && (int)Call("EnemyReleaseCount", heavy) == 0; frame++) Call("TickEnemy", heavy, .02f);
            Require((string)Call("EnemyPatternName", heavy) == "RadialGap" && (int)Call("EnemyReleaseCount", heavy) == 1 && Shots.Count == 8, "The heavy did not open with a close ring of eight on floor " + floor + ": " + Shots.Count);
            heavy.cooldown = 999; return heavy;
        }
        /// <summary>Every live bolt but <paramref name="earlier"/> left <paramref name="shooter"/> at one of <paramref name="offsets"/> (degrees off its locked aim) at <paramref name="speed"/>.</summary>
        private void RequireVolley(TowerGame.Fighter shooter, float[] offsets, float speed, string what, List<object> earlier = null)
        {
            Vector2 aim = ((Vector2)Call("EnemyLockedAim", shooter) - shooter.pos).normalized; var unmatched = new List<float>(offsets);
            var volley = new List<object>(); foreach (object shot in Shots) if (earlier == null || !earlier.Contains(shot)) volley.Add(shot);
            Require(volley.Count == offsets.Length, what + ": expected " + offsets.Length + " bolts, found " + volley.Count);
            foreach (object shot in volley)
            {
                var velocity = Field<Vector2>(shot, "velocity"); float angle = Vector2.SignedAngle(aim, velocity);
                int match = unmatched.FindIndex(offset => Mathf.Abs(Mathf.DeltaAngle(offset, angle)) < .6f);
                Require(match >= 0 && Mathf.Abs(velocity.magnitude - speed) < .01f, what + ": a bolt left at " + angle.ToString("F1") + " degrees, " + velocity.magnitude.ToString("F2") + " u/s.");
                unmatched.RemoveAt(match);
            }
        }
        // The screen never holds more monster bolts than the floor allows (12 on the first thirty floors): a ring that would
        // pass it waits AttackBudgetWait at a time and fires once room frees; nothing is dropped.
        private void BoltBudget()
        {
            Require(TowerGame.EnemyBoltCap(1) == 12 && TowerGame.EnemyBoltCap(30) == 12 && TowerGame.EnemyBoltCap(31) == 16 && TowerGame.EnemyBoltCap(60) == 16 && TowerGame.EnemyBoltCap(61) == 20 && TowerGame.EnemyBoltCap(90) == 20,
                "The monster bolt budget is not 12 / 16 / 20 by thirty floors.");
            Setup(); ClearEnemies(); ClearShots(); game.enabled = false; game.invulnerable = 0;
            Place(game.hero, new Vector2(0, TowerWorld.Ground));
            var ringA = Spawn(3, TowerWorld.Ground, 1); var ringB = Spawn(-3, TowerWorld.Ground, 1); var fan = Spawn(6, TowerWorld.Ground, 1);
            Call("ConfigureEnemy", ringA, 1, 2); Call("ConfigureEnemy", ringB, 1, 2); Call("ConfigureEnemy", fan, 1, 0);
            var shooters = new[] { ringA, ringB, fan }; foreach (var shooter in shooters) shooter.cooldown = 0;
            int most = 0; float bFiredAt = -1, aFiredAt = -1, clock = 0;
            for (int frame = 0; frame < 300 && bFiredAt < 0; frame++, clock += .02f)
            {
                typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, game.hero.pos);
                foreach (var shooter in shooters)
                {
                    Call("TickEnemy", shooter, .02f); most = Mathf.Max(most, game.EnemyBoltsInView());
                    // Each shooter fires once here, so the waiting ring is not starved by the others' next casts.
                    if ((int)Call("EnemyReleaseCount", shooter) > 0) shooter.cooldown = Mathf.Max(shooter.cooldown, 999);
                }
                if (frame == 0) Require(ringA.windup > 0 && ringB.windup <= 0 && Mathf.Abs(ringB.cooldown - TowerGame.AttackBudgetWait) < .001f && fan.windup > 0,
                    "The second ring did not wait for the budget while the first ring and the fan (8 + 3 of 12) wound up.");
                if (aFiredAt < 0 && (int)Call("EnemyReleaseCount", ringA) > 0) aFiredAt = clock;
                if ((int)Call("EnemyReleaseCount", ringB) > 0) bFiredAt = clock;
                Call("TickShots", .02f); most = Mathf.Max(most, game.EnemyBoltsInView());
            }
            Require(bFiredAt > aFiredAt && aFiredAt >= 0 && most <= TowerGame.EnemyBoltCap(1), "The postponed ring never fired or the screen held " + most + " monster bolts (budget 12).");
            report.boltBudget = true;

            // A heavy's close ring asks for its turned four along with its eight as the windup begins: beside a fan winding up
            // (3 + 8 + 4 of 12) it waits, though the eight alone would fit.
            Setup(); ClearEnemies(); ClearShots(); game.enabled = false; game.invulnerable = 0;
            Place(game.hero, new Vector2(0, TowerWorld.Ground));
            var early = Spawn(6, TowerWorld.Ground, 1); var heavy = Spawn(3, TowerWorld.Ground, 2);
            Call("ConfigureEnemy", early, 1, 0); Call("ConfigureEnemy", heavy, 2, 1); early.cooldown = heavy.cooldown = 0;
            Call("TickEnemy", early, .02f); Call("TickEnemy", heavy, .02f);
            Require(early.windup > 0 && heavy.windup <= 0 && Mathf.Abs(heavy.cooldown - TowerGame.AttackBudgetWait) < .001f,
                "A heavy's close ring began beside a fan's three without reserving its second ring (3 + 8 + 4 of 12).");
            report.volleyReservedWhole = true;
            report.checks.Add("Bolt budget 12/16/20: a second close ring waited behind 8 + 3 bolts and fired " + (bFiredAt - aFiredAt).ToString("F2") + " s after the first; the screen never held more than " + most + ". A heavy's ring reserves 8 + 4 at once and waits beside a fan's 3.");
        }
        // A fallen shooter's bolts shrink away harmlessly over ShooterFallFade; another shooter's keep flying.
        private void ShooterFallClearsBolts()
        {
            Setup(); ClearEnemies(); ClearShots(); game.enabled = false; game.invulnerable = 0; game.hero.hp = game.PlayerMaxHealth;
            Place(game.hero, new Vector2(0, TowerWorld.Ground));
            var fallen = Spawn(5, TowerWorld.Ground, 1); var standing = Spawn(-5, TowerWorld.Ground, 1); fallen.cooldown = standing.cooldown = 0;
            for (int frame = 0; frame < 60 && ((int)Call("EnemyReleaseCount", fallen) == 0 || (int)Call("EnemyReleaseCount", standing) == 0); frame++)
            { Call("TickEnemy", fallen, .02f); Call("TickEnemy", standing, .02f); }
            fallen.cooldown = standing.cooldown = 999;
            typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, game.hero.pos); Call("TickShots", .05f);
            var theirs = new List<object>(); var others = new List<object>(); object onAim = null;
            foreach (object shot in Shots)
            {
                var owner = Field<TowerGame.Fighter>(shot, "shooter");
                if (owner == fallen) { theirs.Add(shot); if (onAim == null || Mathf.Abs(Field<Vector2>(shot, "velocity").y) < Mathf.Abs(Field<Vector2>(onAim, "velocity").y)) onAim = shot; }
                else if (owner == standing) others.Add(shot);
            }
            Require(theirs.Count == 3 && others.Count == 3, "The fall fixture did not release two fans.");
            fallen.hp = 1; Call("DamageEnemy", fallen, 10f, 0f);
            Require(fallen.hp <= 0 && !game.enemies.Contains(fallen), "The fall fixture's shooter did not fall.");
            // The hero steps into the fading middle bolt's path: it passes through without harm.
            Vector2 path = Field<Vector2>(onAim, "pos"); Place(game.hero, new Vector2(path.x, TowerWorld.Ground)); float hp = game.hero.hp;
            typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, game.hero.pos); Call("TickShots", .1f);
            foreach (object shot in theirs)
                Require(Shots.Contains(shot) && Field<float>(shot, "fade") > 0 && Field<GameObject>(shot, "root").transform.localScale.x < .99f, "A fallen shooter's bolt did not begin to fade.");
            Require(game.hero.hp == hp && game.EnemyBoltsInView() == others.Count, "A fading bolt still hurt the hero or counted toward the budget.");
            Call("TickShots", .15f);
            foreach (object shot in theirs) Require(!Shots.Contains(shot), "A fallen shooter's bolt outlived its " + TowerGame.ShooterFallFade + " s fade.");
            foreach (object shot in others) Require(Shots.Contains(shot) && Field<float>(shot, "fade") < 0, "Another shooter's bolt vanished with the fallen one.");
            report.shooterFallClearsBolts = true;
            report.checks.Add("A fallen shooter's 3 bolts faded harmlessly within " + TowerGame.ShooterFallFade + " s while the other shooter's 3 kept flying.");
        }
        private IEnumerator ControlledDodge()
        {
            // Speeds are measured over simulated time: a slow frame is clamped by Update and would read as a slowdown.
            Setup(); game.MoveOverride = 1; float start = game.hero.pos.x, at = game.elapsed;
            yield return Wait(.25f); float normalSpeed = (game.hero.pos.x - start) / (game.elapsed - at);
            // Ctrl's 45% walk is gone: a bolt keeping pace above and behind the walking hero heads for its chest the whole
            // time, so the hit core shows itself while the hero walks on at full speed.
            var hitCore = (GameObject)typeof(TowerGame).GetField("playerHitCore", Hidden).GetValue(game);
            Require(!game.FocusActive && !hitCore.activeInHierarchy, "The hit core showed with no hostile bolt near.");
            Vector2 trail = game.hero.pos + new Vector2(-1.2f, .9f + 1.1f);
            Fire(trail, trail + Vector2.right, game.PlayerMoveSpeed, 12, true);
            start = game.hero.pos.x; at = game.elapsed;
            yield return Wait(.25f); float closingSpeed = (game.hero.pos.x - start) / (game.elapsed - at);
            Require(game.FocusActive && hitCore.activeInHierarchy && game.HitCoreAlpha > .99f && closingSpeed / normalSpeed > .9f && game.hero.hp == game.PlayerMaxHealth,
                "A bolt closing on the chest did not reveal the collision core, or the hero still slowed down (" + (closingSpeed / normalSpeed).ToString("F2") + ").");
            // A bolt flying away does not count, and the core fades out once nothing closes in.
            ClearShots(); Fire(trail, trail + Vector2.left, 1, 12, true);
            yield return Wait(TowerGame.HitCoreHold + TowerGame.HitCoreFade + .2f);
            Require(!game.FocusActive && !hitCore.activeInHierarchy && game.HitCoreAlpha <= 0, "The hit core stayed after the closing bolt was gone, or showed for a receding one.");
            Require(Mathf.Abs(TowerGame.PlayerBulletHitRadius - .24f) < .001f, "Player bullet collision core unexpectedly changed size."); report.hitCoreOnApproach = true;
            Setup(); yield return Wait(.3f); game.hero.hp = game.PlayerMaxHealth;
            Fire(new Vector2(4, TowerWorld.Ground + 1), new Vector2(-4, TowerWorld.Ground + 1), 8, 12, true);
            yield return Wait(.7f); Require(game.hero.hp < game.PlayerMaxHealth, "A stationary hero was not hit by the dodge control shot."); report.stationaryBulletHit = true;
            ClearShots(); game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0; Place(game.hero, new Vector2(0, TowerWorld.Ground));
            Fire(new Vector2(4, TowerWorld.Ground + 1), new Vector2(-4, TowerWorld.Ground + 1), 8, 12, true);
            game.JumpOverride = true; yield return null; game.JumpOverride = false;
            yield return Wait(.6f); Require(game.hero.hp == game.PlayerMaxHealth && game.hero.pos.y > TowerWorld.Ground + .6f, "Actual jump input failed to evade the same horizontal shot."); report.jumpInputEvades = true;
            yield return Capture("dodge", "탄막 검증 · 실제 점프 입력으로 수평 탄 회피");
            yield return DoubleJump();
            ClearShots(); Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.hero.facing = 1; game.invulnerable = 0; game.ReadyDash();
            game.AttackOverride = true; yield return null; game.AttackOverride = false;
            Require(game.attackTimer > 0, "The dash cancellation fixture did not begin an attack.");
            var skillTarget = Spawn(6, TowerWorld.Ground); skillTarget.stunTime = skillTarget.cooldown = 999;
            game.run.Player.Apply(TowerRun.GetAugment("player.fireball")); game.ReconcileLoadout();
            var clocks = (IDictionary)typeof(TowerGame).GetField("skillClocks", Hidden).GetValue(game);
            foreach (DictionaryEntry entry in clocks) entry.Value.GetType().GetField("remaining").SetValue(entry.Value, 0f);
            Fire(game.hero.pos + Vector2.up, game.hero.pos + Vector2.up + Vector2.right * 5, 19, 12, true);
            game.DashOverride = true; yield return null; game.DashOverride = false; yield return Wait(.08f);
            Require(game.dashTime > 0 && game.invulnerable > 0 && game.hero.hp == game.PlayerMaxHealth && game.attackTimer == 0 && game.hero.art.MotionName == "Dash", "Actual dash input did not interrupt an attack and protect the hero from an overlapping parallel shot."); report.dashInputInvulnerable = true;
            Require(game.SkillLastStartedAt("player", "player.fireball") < 0, "A ready automatic player skill began during manual dash and replaced its pose."); report.readySkillWaitsForDash = true;
            // Out of the dash the ready skill begins without a wind-up: the hero keeps its facing and its sword, a second dash
            // does not cancel the cast, and the skill still flies at its target behind the hero's back.
            game.hero.facing = -1; float skillLimit = Time.realtimeSinceStartup + 1.5f;
            while (game.SkillLastStartedAt("player", "player.fireball") < 0) { Require(Time.realtimeSinceStartup < skillLimit, "The ready automatic player skill never began after the dash."); yield return null; }
            Require(game.hero.castRemaining <= 0 && game.hero.facing == -1 && game.hero.art.MotionName != "Cast", "An automatic player skill held the hero in a cast pose or turned it toward its target.");
            game.slashCooldown = 0; game.AttackOverride = true; yield return null; game.AttackOverride = false;
            Require(game.attackTimer > 0, "An automatic player skill held the sword.");
            game.ReadyDash(); game.DashOverride = true; yield return null; game.DashOverride = false;
            Require(game.dashTime > 0, "The second dash fixture did not dash.");
            while (game.SkillCasts("player", "player.fireball") == 0) { Require(Time.realtimeSinceStartup < skillLimit, "A dash cancelled the automatic player skill before its release."); yield return null; }
            bool skillAimed = skillTarget.hp < skillTarget.maxHp; foreach (object shot in Shots) if (Field<HeroState>(shot, "owner") == game.run.Player && Field<Vector2>(shot, "velocity").x > 0) skillAimed = true;
            Require(skillAimed && game.hero.facing == -1, "The automatic player skill did not fly at its target, or left the hero turned.");
            report.readySkillLeavesBodyFree = true; game.hero.facing = 1;
            ((IList)typeof(HeroState).GetField("skills", Hidden).GetValue(game.run.Player)).Clear(); Call("ResetSkills");
            ClearShots(); game.dashTime = 0; game.invulnerable = 0; Place(game.hero, new Vector2(0, TowerWorld.Ground));
            Fire(new Vector2(-5, TowerWorld.Ground + 1), new Vector2(5, TowerWorld.Ground + 1), 2000, 12, true);
            yield return Wait(.2f);
            Require(game.hero.hp < game.PlayerMaxHealth, "A fast hostile shot tunneled through the hero between frames: pos=" + game.hero.pos + ", invuln=" + game.invulnerable + ", shots=" + Shots.Count); report.sweptBulletHit = true;
            game.enabled = false; ClearShots(); game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0;
            Place(game.hero, new Vector2(2, TowerWorld.Ground)); typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, new Vector2(-2, TowerWorld.Ground));
            Fire(new Vector2(0, TowerWorld.Ground + .9f), new Vector2(0, TowerWorld.Ground + .9f), 0, 12, true); Call("TickShots", .02f);
            Require(game.hero.hp < game.PlayerMaxHealth, "Relative swept collision missed a hero crossing a stationary hostile bullet.");
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.enabled = true;
            ClearShots(); game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0;
            yield return Wait(1.8f); VerifyCameraHitAgainstControl();
            ClearShots(); game.hero.hp = 1; game.invulnerable = 0;
            Fire(new Vector2(-5, TowerWorld.Ground + 1), new Vector2(5, TowerWorld.Ground + 1), 2000, 1000, true);
            yield return Wait(.2f);
            Require(game.hero.hp == 0 && !game.run.IsRewardPending && !game.run.IsFloorCleared, "A swept hostile shot failed to kill or death cleared the floor."); report.hostileBulletDeath = true;
            report.checks.Add("Identical production bullets hit a stationary hero and miss after actual JumpOverride input; a falling hero jumps once more in the air; DashOverride grants invulnerability; fast swept shots hit/kill; hit camera remains fixed.");
        }
        // A second press while falling lifts the hero again; a third waits for landing, which restores the air jump.
        private IEnumerator DoubleJump()
        {
            ClearShots(); game.invulnerable = 0; Place(game.hero, new Vector2(0, TowerWorld.Ground)); yield return null;
            game.JumpOverride = true; yield return null; game.JumpOverride = false;
            float limit = Time.realtimeSinceStartup + 3;
            while (game.hero.grounded || game.hero.velocityY > -1) { Require(Time.realtimeSinceStartup < limit, "The double-jump fixture never began falling."); yield return null; }
            Require(game.airJumps == TowerGame.MaxAirJumps, "The ground jump spent the air jump.");
            float fallHeight = game.hero.pos.y;
            game.JumpOverride = true; yield return null; game.JumpOverride = false;
            Require(!game.hero.grounded && game.hero.velocityY > 0 && game.airJumps == 0, "A jump press while falling did not spend the air jump.");
            while (game.hero.pos.y < fallHeight + .5f) { Require(Time.realtimeSinceStartup < limit && game.hero.velocityY > 0, "The air jump did not lift the falling hero."); yield return null; }
            float rising = game.hero.velocityY;
            game.JumpOverride = true; yield return null; game.JumpOverride = false;
            Require(game.hero.velocityY <= rising, "A third jump press in the air jumped again.");
            limit = Time.realtimeSinceStartup + 3;
            while (!game.hero.grounded) { Require(Time.realtimeSinceStartup < limit, "The double-jumping hero never landed."); yield return null; }
            yield return null;
            Require(game.airJumps == TowerGame.MaxAirJumps, "Landing did not restore the air jump."); report.doubleJumpInput = true;
        }
        private void VerifyCameraHitAgainstControl()
        {
            bool wasEnabled = game.enabled, attack = game.AttackOverride, jump = game.JumpOverride, dash = game.DashOverride;
            float move = game.MoveOverride; game.enabled = false;
            game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = false;
            try
            {
                Require(game.dashTime <= 0 && game.attackTimer <= 0 && game.hero.castRemaining <= 0, "Camera control fixture still had an active player action.");
                ClearShots(); game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0;
                Vector2 heroPosition = game.hero.pos; int facing = game.hero.facing;
                Vector3 start = game.view.transform.position; var control = new Vector3[15];
                float controlMaxMotion = 0;
                // Both paths run in this same Unity frame, using the real Update and identical frame delta.
                // Comparing paths allows ordinary camera tracking to settle without duplicating its formula.
                for (int i = 0; i < control.Length; i++)
                {
                    Call("Update"); control[i] = game.view.transform.position;
                    controlMaxMotion = Mathf.Max(controlMaxMotion, Vector3.Distance(start, control[i]));
                }
                Require(game.hero.hp == game.PlayerMaxHealth && game.hero.pos == heroPosition && game.hero.facing == facing,
                    "The no-hit camera control did not keep its stationary hero unchanged.");
                game.view.transform.position = start; game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0;
                Fire(game.hero.pos + Vector2.up, game.hero.pos + Vector2.up, 0, 12, true);
                float maxPathError = 0;
                for (int i = 0; i < control.Length; i++)
                {
                    Call("Update"); maxPathError = Mathf.Max(maxPathError, Vector3.Distance(control[i], game.view.transform.position));
                }
                float damage = game.PlayerMaxHealth - game.hero.hp;
                report.checks.Add("Paired camera control: samples=" + control.Length + ", frameDelta=" + Time.unscaledDeltaTime.ToString("F6") +
                    ", noHitMaxMotion=" + controlMaxMotion.ToString("F6") + ", hitPathMaxError=" + maxPathError.ToString("F6") + ", actualDamage=" + damage.ToString("F3"));
                Require(damage > 0 && game.hero.pos == heroPosition && game.hero.facing == facing, "Camera hit path did not damage its stationary hero.");
                Require(maxPathError <= .00001f, "A hit changed the camera path relative to its no-hit control."); report.cameraStableOnHit = true;
            }
            finally
            {
                game.enabled = wasEnabled; game.MoveOverride = move;
                game.AttackOverride = attack; game.JumpOverride = jump; game.DashOverride = dash;
            }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Finish(Exception error)
        {
            if (finishing) return; finishing = true;
            if (error != null) report.errors.Add(error is TargetInvocationException && error.InnerException != null ? error.InnerException.ToString() : error.ToString());
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0;
            game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = false;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= RecordLog;
            if (quitOnComplete) Application.Quit(report.success ? 0 : 1);
        }
    }
}

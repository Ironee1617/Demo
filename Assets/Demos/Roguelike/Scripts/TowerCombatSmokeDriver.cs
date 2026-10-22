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
            public bool stationaryBulletHit, jumpInputEvades, doubleJumpInput, dashInputInvulnerable, sweptBulletHit, hostileBulletDeath, cameraStableOnHit, focusInputAndHitCore, readySkillWaitsForDash;
            public bool nearestProjectileContact, sameFramePiercing, piercingDoesNotRepeat, splashKillRemoval, projectileRangeExpiry;
            public bool companionTargetExcluded, companionMeleeImmune, companionProjectilePassThrough, companionBossAreaImmune, companionStatusImmune, companionsStillAttack;
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
            ProjectileContacts(); CompanionSafety(); yield return Patterns(); yield return ControlledDodge();
            Finish(null);
        }
        private void Setup(params string[] companions)
        {
            game.enabled = true;
            if (game.hero != null && game.hero.root) game.hero.root.SetActive(false);
            foreach (var actor in game.allies) if (actor.root) actor.root.SetActive(false);
            foreach (var actor in game.enemies) if (actor.root) actor.root.SetActive(false);
            game.NewRun(74); game.StartClimb(); game.MoveOverride = 0; game.FocusOverride = false; game.AttackOverride = game.JumpOverride = game.DashOverride = false;
            var list = (IList)typeof(TowerRun).GetField("companions", Hidden).GetValue(game.run);
            foreach (string id in companions) list.Add(new CompanionState(TowerRun.GetDefinition(id)));
            game.ReconcileLoadout(); ClearEnemies(); ClearShots();
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.hero.facing = 1;
            var sentinel = Spawn(25, TowerWorld.Ground); sentinel.stunTime = sentinel.cooldown = 999;
        }
        private void ClearEnemies()
        { foreach (var enemy in game.enemies) if (enemy.root) { enemy.root.SetActive(false); Destroy(enemy.root); } game.enemies.Clear(); }
        private void ClearShots()
        { foreach (object shot in Shots) { var root = Field<GameObject>(shot, "root"); if (root) Destroy(root); } Shots.Clear(); }
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
                Require(Mathf.Abs(spawned.pos.y-(2.4f+(floor-2)*1.4f+.8f))<=.41f,"The flyer is not hovering above its reachable upper tier.");
            }
            report.soloFlyerSpawns=true;
            Setup("michael");ClearEnemies();game.enabled=false;
            var world=(TowerWorld)typeof(TowerGame).GetField("world",Hidden).GetValue(game);
            var perch=world.Ledges[world.Ledges.Length-1];
            flyer=Spawn(perch.x,perch.y+.8f,3);flyer.stunTime=999;
            Place(game.hero,new Vector2(perch.x,TowerWorld.Ground));
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
            Place(game.hero, new Vector2(3, TowerWorld.Ground)); Call("TickEnemy", melee, .01f);
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
        private IEnumerator Patterns()
        {
            // Production preparation and release remain intact without projected paths or danger markers.
            Setup(); ClearEnemies(); game.enabled = false;
            Place(game.hero, new Vector2(0, TowerWorld.Ground));
            var melee = Spawn(1, TowerWorld.Ground); melee.cooldown = 0;
            var renderers = new HashSet<SpriteRenderer>(game.GetComponentsInChildren<SpriteRenderer>(true));
            Call("TickEnemy", melee, .01f);
            Require((string)Call("EnemyPatternName", melee) == "MeleeSlam" && melee.windup >= .79f, "Melee attack lost its preparation delay.");
            float hp = game.hero.hp;
            while (melee.windup > .021f)
            {
                RequireNoPreparationOverlay(renderers); Call("TickEnemy", melee, .02f);
                Require(game.hero.hp == hp && (int)Call("EnemyReleaseCount", melee) == 0, "Melee damage occurred before preparation completed.");
            }
            RequireNoPreparationOverlay(renderers);
            yield return VerifyPatterns();
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
            int[] counts = { 3, 4, 9 };
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
                Require((float)Call("EnemyTelegraphRemaining", shooter) >= (pattern == 2 ? 1.19f : .99f) && Vector2.Distance(locked, game.hero.pos) < .001f, "Enemy aim or preparation duration changed.");
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
                if (pattern == 0)
                { Require(Mathf.Abs(angles[0] + 18) < .6f && Mathf.Abs(angles[1]) < .6f && Mathf.Abs(angles[2] - 18) < .6f, "Aimed fan spacing must be -18/0/+18 degrees."); report.fanPattern = true; }
                else
                {
                    float safe = pattern == 1 ? 18 : 34;
                    foreach (float angle in angles) Require(Mathf.Abs(angle) > safe, "The intentional safe sector was filled by a bullet: " + names[pattern]);
                    if (pattern == 1) report.gapPattern = true; else report.radialPattern = true;
                }
                Place(game.hero, new Vector2(-8, TowerWorld.Ground)); typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, game.hero.pos); Call("TickShots", .1f);
                foreach (object shot in Shots)
                {
                    Require(Field<Vector2>(shot, "velocity") == velocities[shot], "A hostile bullet homed after release.");
                    Require(Vector2.Distance(Field<Vector2>(shot, "pos"), positions[shot] + velocities[shot] * .1f) < .001f, "A hostile bullet did not follow its fixed linear path.");
                }
                report.fixedBulletTrajectory = true;
                if (pattern > 0)
                {
                    for (int frame = 0; frame < 40 && (int)Call("EnemyReleaseCount", shooter) < 2; frame++) Call("TickEnemy", shooter, .02f);
                    Require((int)Call("EnemyReleaseCount", shooter) == 2 && Shots.Count == (pattern == 1 ? 10 : 19), "Incorrect follow-up wave count for " + names[pattern]);
                    Require((Vector2)Call("EnemyLockedAim", shooter) == locked, "A follow-up wave retargeted the hero.");
                }
                report.checks.Add(names[pattern] + ": locked aim / no preparation overlays / first volley " + counts[pattern] + " / fixed trajectories / safe gap and follow-up verified.");
                if (pattern == 2)
                {
                    typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, game.hero.pos);
                    Call("TickShots", .55f); Call("TickEffects", .55f); game.view.transform.position = new Vector3(-2, 0, -10);
                    yield return Capture("bullets", "탄막 검증 · 빈틈이 있는 방사형 2연속 패턴");
                }
            }
            game.enabled = true;
        }
        private IEnumerator ControlledDodge()
        {
            Setup(); game.MoveOverride = 1; float start = game.hero.pos.x, at = Time.realtimeSinceStartup;
            yield return Wait(.25f); float normalSpeed = (game.hero.pos.x - start) / (Time.realtimeSinceStartup - at);
            start = game.hero.pos.x; at = Time.realtimeSinceStartup; game.FocusOverride = true;
            yield return Wait(.25f); float focusSpeed = (game.hero.pos.x - start) / (Time.realtimeSinceStartup - at);
            var hitCore = (GameObject)typeof(TowerGame).GetField("playerHitCore", Hidden).GetValue(game);
            Require(game.FocusActive && hitCore.activeInHierarchy && focusSpeed / normalSpeed > .32f && focusSpeed / normalSpeed < .58f, "Focus input did not slow movement to 45% and reveal the collision core.");
            Require(Mathf.Abs(TowerGame.PlayerBulletHitRadius - .24f) < .001f, "Player bullet collision core unexpectedly changed size."); report.focusInputAndHitCore = true;
            Setup(); yield return Wait(.3f); game.hero.hp = game.PlayerMaxHealth;
            Fire(new Vector2(4, TowerWorld.Ground + 1), new Vector2(-4, TowerWorld.Ground + 1), 8, 12, true);
            yield return Wait(.7f); Require(game.hero.hp < game.PlayerMaxHealth, "A stationary hero was not hit by the dodge control shot."); report.stationaryBulletHit = true;
            ClearShots(); game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0; Place(game.hero, new Vector2(0, TowerWorld.Ground));
            Fire(new Vector2(4, TowerWorld.Ground + 1), new Vector2(-4, TowerWorld.Ground + 1), 8, 12, true);
            game.JumpOverride = true; yield return null; game.JumpOverride = false;
            yield return Wait(.6f); Require(game.hero.hp == game.PlayerMaxHealth && game.hero.pos.y > TowerWorld.Ground + .6f, "Actual jump input failed to evade the same horizontal shot."); report.jumpInputEvades = true;
            yield return Capture("dodge", "탄막 검증 · 실제 점프 입력으로 수평 탄 회피");
            yield return DoubleJump();
            ClearShots(); Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.hero.facing = 1; game.invulnerable = 0; game.dashCooldown = 0;
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
            bool wasEnabled = game.enabled, attack = game.AttackOverride, jump = game.JumpOverride, dash = game.DashOverride, focus = game.FocusOverride;
            float move = game.MoveOverride; game.enabled = false;
            game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = game.FocusOverride = false;
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
                game.AttackOverride = attack; game.JumpOverride = jump; game.DashOverride = dash; game.FocusOverride = focus;
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

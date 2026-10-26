using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Explicit validation only: every one of the fifty companion passives fires in a live fight and does what its card says.
    public sealed class TowerPassiveSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class PassiveReport
        {
            public string mode = "towerPassiveSmoke", finishedUtc;
            public bool success;
            public int verified, total;
            public List<string> passed = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        // Every roll lands under its chance, so chance-based passives fire on their first opportunity.
        private sealed class LoadedDice : System.Random { public override double NextDouble() { return 0; } }
        private readonly PassiveReport report = new PassiveReport();
        private readonly HashSet<string> verified = new HashSet<string>();
        private TowerGame game; private TowerSmokeDriver capture;
        private string prefix, reportPath; private bool finishing, quitOnComplete;
        private float deadline;
        private TowerGame.Fighter sentinel;
        private static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private static float Ground { get { return TowerWorld.Ground; } }
        public bool IsFinished { get { return finishing; } }
        public bool Succeeded { get { return finishing && report.success; } }
        private IList Shots { get { return (IList)typeof(TowerGame).GetField("shots", Hidden).GetValue(game); } }
        private object Call(string method, params object[] args) { return typeof(TowerGame).GetMethod(method, Hidden).Invoke(game, args); }
        private T Get<T>(string field) { return (T)typeof(TowerGame).GetField(field, Hidden).GetValue(game); }
        private void Set(string field, object value) { typeof(TowerGame).GetField(field, Hidden).SetValue(game, value); }
        private TowerPassiveState Passives { get { return game.run.Passives; } }

        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver, string capturePrefix, string outputReportPath, bool quitOnComplete = false)
        {
            if (game != null) throw new InvalidOperationException("Create a new passive driver for each suite run.");
            if (tower == null) throw new ArgumentNullException(nameof(tower));
            if (captureDriver == null) throw new ArgumentNullException(nameof(captureDriver));
            if (!Path.IsPathRooted(capturePrefix) || !Path.IsPathRooted(outputReportPath)) throw new ArgumentException("Suite capture and report paths must be absolute.");
            game = tower; capture = captureDriver; prefix = capturePrefix; reportPath = outputReportPath; this.quitOnComplete = quitOnComplete;
            capture.PrepareForSuite(tower); game.SuppressProgressPersistence = true; game.SmokeControlled = true; enabled = true;
            report.total = TowerPassiveCatalog.Passives.Count;
            Application.logMessageReceived += RecordLog; deadline = Time.realtimeSinceStartup + 240;
            StartCoroutine(Guard(Suite()));
        }
        private void RecordLog(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(condition + "\n" + trace); }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }
        private void Update()
        { if (!finishing && Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Finish(new TimeoutException("Passive suite exceeded 240 seconds.")); } }
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
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            yield return null;
            yield return MeleeEmpowered(); yield return MeleeOnHit(); yield return Volleys(); yield return Spells(); yield return Precision();
            yield return Slaughter(); yield return Spoils(); yield return Heavens(); yield return Commands(); yield return Seals();
            Guardians(); Wards(); yield return Dodges(); yield return Healers(); yield return Lore(); Judgments(); Fellowship();
            yield return Presentation();
            foreach (var passive in TowerPassiveCatalog.Passives)
                if (!verified.Contains(passive.CompanionId)) report.errors.Add("Passive never verified: " + passive.CompanionId + " " + passive.Name);
            Finish(null);
        }

        private void Setup(params string[] ids)
        {
            game.enabled = true;
            if (game.hero != null && game.hero.root) game.hero.root.SetActive(false);
            foreach (var actor in game.allies) if (actor.root) actor.root.SetActive(false);
            foreach (var actor in game.enemies) if (actor.root) actor.root.SetActive(false);
            game.NewRun(61); game.StartClimb();
            game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = game.DropOverride = game.FocusOverride = false;
            // The real join path wires each companion's stat bonuses, passives included.
            var join = typeof(TowerRun).GetMethod("AddCompanion", Hidden);
            foreach (string id in ids) join.Invoke(game.run, new object[] { TowerRun.GetDefinition(id) });
            game.ReconcileLoadout(); ClearEnemies(); ClearShots(); LevelFloor();
            Place(game.hero, new Vector2(0, Ground)); game.hero.facing = 1; game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0;
            Call("ResetAllyFormation"); Call("ResetPassiveFloor"); game.PassiveFired.Clear();
            game.view.transform.position = new Vector3(0, 0, -10);
            // A far, frozen enemy keeps the floor from clearing while fixtures swap their targets.
            sentinel = Spawn(30, 1e6f); sentinel.stunTime = 999;
        }
        private void ClearEnemies()
        { foreach (var enemy in game.enemies) if (enemy.root) { enemy.root.SetActive(false); Destroy(enemy.root); } game.enemies.Clear(); }
        private void ClearShots()
        {
            foreach (object shot in Shots) { var root = (GameObject)shot.GetType().GetField("root").GetValue(shot); if (root) Destroy(root); }
            Shots.Clear();
        }
        private void LevelFloor()
        {
            var world = (TowerWorld)typeof(TowerGame).GetField("world", Hidden).GetValue(game); var layout = world.CurrentLayout;
            var ground = new TowerSurface(new[] { TowerTerrainGenerator.TerrainLeft, TowerTerrainGenerator.TerrainRight }, new[] { TowerWorld.Ground, TowerWorld.Ground });
            world.SetTheme(game.run.Theme.Id, game.run.Floor, game.seed, new TowerFloorLayout(layout.Id, layout.Archetype, layout.Floor, ground, new List<TowerPlatform>(layout.Platforms), layout.ExitX));
        }
        /// <summary>A fixture enemy that never attacks; <paramref name="frozen"/> also keeps it in place.</summary>
        private TowerGame.Fighter Spawn(float x, float hp, bool frozen = true, bool elite = false, int kind = 0)
        {
            var enemy = (TowerGame.Fighter)Call("Spawn", "Passive fixture", kind, true, new Vector2(x, Ground));
            game.enemies.Add(enemy); Call("ConfigureEnemy", enemy, kind, 0);
            enemy.maxHp = enemy.hp = hp; enemy.cooldown = 999; enemy.isElite = elite; if (frozen) enemy.stunTime = 999;
            return enemy;
        }
        private List<TowerGame.Fighter> Pack(float from, float step, int count, float hp, bool elite = false)
        { var pack = new List<TowerGame.Fighter>(); for (int i = 0; i < count; i++) pack.Add(Spawn(from + i * step, hp, true, elite)); return pack; }
        private static void Place(TowerGame.Fighter actor, Vector2 position)
        { actor.pos = position; actor.velocityY = 0; actor.grounded = true; actor.support = -1; actor.lastX = position.x; actor.root.transform.position = position; }
        private IEnumerator Wait(float seconds)
        { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
        /// <summary>Waits until every listed companion's passive has fired or the time runs out.</summary>
        private IEnumerator Fight(float seconds, params string[] ids)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                bool all = true; foreach (string id in ids) if (game.PassiveFireCount(TowerRun.GetDefinition(id).Passive.Kind) == 0) all = false;
                if (all) yield break;
                yield return null;
            }
        }
        private void Prime(PassiveKind kind) { Passives.Periodic(kind, 999, false); }
        private void Expect(bool condition, string message) { if (!condition) report.errors.Add(message); }
        private void Verify(string id, bool condition, string detail)
        {
            var passive = TowerRun.GetDefinition(id).Passive;
            if (!condition) { report.errors.Add(id + " · " + passive.Name + ": " + detail); return; }
            if (verified.Add(id)) report.passed.Add(id + " · " + passive.Name + " · " + detail);
        }
        private void VerifyFired(string id, string detail = "fired")
        { int count = game.PassiveFireCount(TowerRun.GetDefinition(id).Passive.Kind); Verify(id, count > 0, detail + " (" + count + ")"); }
        private float DamageScale(TowerGame.Fighter enemy, HeroState owner) { return (float)Call("PassiveDamageScale", enemy, owner); }

        private IEnumerator MeleeEmpowered()
        {
            Setup("thor", "hanuman", "sun_wukong"); Pack(2.4f, .7f, 4, 1e6f);
            yield return Fight(9, "thor", "hanuman", "sun_wukong");
            VerifyFired("thor", "every fourth blow stuns"); VerifyFired("hanuman", "every third blow slams the ground"); VerifyFired("sun_wukong", "the staff sweeps everyone ahead");
        }
        private IEnumerator MeleeOnHit()
        {
            Setup("guan_yu", "cu_chulainn", "poseidon"); Pack(2.4f, .6f, 4, 1e6f);
            yield return Fight(6, "guan_yu", "cu_chulainn", "poseidon");
            VerifyFired("guan_yu", "cleaves around the target");
            Verify("cu_chulainn", game.PassiveFireCount(PassiveKind.GaeBolg) > 0 && game.Afflictions > 0, "barbed wounds bleed (" + game.Afflictions + " open)");
            Verify("poseidon", game.enemies.Exists(e => e.slowTime > 0), "the tide slows what it strikes");
        }
        private IEnumerator Volleys()
        {
            Setup("gabriel", "artemis", "vishnu"); Pack(3.5f, .7f, 4, 1e6f);
            yield return Fight(8, "gabriel", "artemis", "vishnu");
            VerifyFired("gabriel", "arrows split to other foes"); VerifyFired("artemis", "arrows ricochet"); VerifyFired("vishnu", "the discus pierces and grows");
        }
        private IEnumerator Spells()
        {
            Setup("zeus", "uriel", "erlang_shen"); Pack(3.5f, .7f, 4, 1e6f);
            yield return Fight(8, "zeus", "uriel", "erlang_shen");
            VerifyFired("zeus", "lightning chains"); VerifyFired("uriel", "attacks ignite"); VerifyFired("erlang_shen", "the third eye marks");
            var marked = game.enemies.Find(e => e != sentinel && DamageScale(e, game.run.Player) > 1.19f);
            Expect(marked != null, "erlang_shen: a marked foe takes more damage from the player");
        }
        private IEnumerator Precision()
        {
            Setup("fionn", "jumong", "horus"); Pack(3.5f, .8f, 3, 1e6f);
            yield return Fight(9, "fionn", "jumong");
            VerifyFired("fionn", "every fifth shot is a piercing critical"); VerifyFired("jumong", "the first shot at each foe is critical");
            var horus = game.run.GetCompanion("horus");
            Verify("horus", horus.CritChance >= .25f - .001f && Mathf.Abs(Passives.CriticalMultiplier(horus) - 2.5f) < .001f &&
                Mathf.Abs(Passives.CriticalMultiplier(game.run.Player) - TowerPassiveCatalog.BaseCriticalMultiplier) < .001f, "critical chance " + horus.CritChance + ", multiplier " + Passives.CriticalMultiplier(horus));
        }
        private IEnumerator Slaughter()
        {
            Setup("nezha", "hades", "shiva"); var nezha = game.run.GetCompanion("nezha"); float interval = nezha.AttackInterval;
            Pack(2.2f, .55f, 6, 45); game.hero.hp = game.PlayerMaxHealth * .5f; float hp = game.hero.hp; int kills = game.kills;
            yield return Fight(8, "nezha", "hades", "shiva");
            Verify("nezha", game.PassiveFireCount(PassiveKind.Momentum) > 0 && (Passives.MomentumStacks == 0 || nezha.AttackInterval < interval), "momentum " + Passives.MomentumStacks + " stacks");
            Verify("hades", game.PassiveFireCount(PassiveKind.SoulToll) > 0 && game.hero.hp > hp, "souls heal the player to " + Mathf.RoundToInt(game.hero.hp));
            Verify("shiva", game.PassiveFireCount(PassiveKind.Destruction) > 0 && game.kills > kills, "the slain explode (" + (game.kills - kills) + " kills)");
        }
        private IEnumerator Spoils()
        {
            Setup("freyja", "jacheongbi", "asmodeus"); Set("passiveRandom", new LoadedDice());
            // Drops landing by the player may be picked up at once, so collected loot counts too.
            int coins = game.pickups.FindAll(p => p.kind == LootKind.GoldCoin).Count + game.GoldCollected, hearts = game.pickups.FindAll(p => p.kind == LootKind.Heart).Count + game.HeartsCollected;
            Pack(2.6f, .6f, 3, 40, true);
            yield return Fight(8, "freyja", "jacheongbi", "asmodeus");
            Verify("freyja", game.pickups.FindAll(p => p.kind == LootKind.GoldCoin).Count + game.GoldCollected > coins, "slain elites drop gold coins");
            Verify("jacheongbi", game.pickups.FindAll(p => p.kind == LootKind.Heart).Count + game.HeartsCollected > hearts, "slain elites drop hearts");
            Verify("asmodeus", game.PassiveFireCount(PassiveKind.Wrath) > 0, "wrath " + Passives.WrathStacks + " stacks");
        }
        private IEnumerator Heavens()
        {
            // Mages stand behind the player, so the pack waits close enough for Ra's flare.
            Setup("odin", "ra", "agares"); var pack = Pack(.8f, .6f, 4, 1e6f);
            Prime(PassiveKind.Gungnir); Prime(PassiveKind.SunFlare); Prime(PassiveKind.Quake);
            yield return Fight(3, "odin", "ra", "agares");
            VerifyFired("odin", "gungnir strikes the strongest foe");
            Verify("ra", game.PassiveFireCount(PassiveKind.SunFlare) > 0 && game.Afflictions > 0, "the flare scorches and burns");
            Verify("agares", game.PassiveFireCount(PassiveKind.Quake) > 0 && pack.Exists(e => e.slowTime > 0), "the quake slows the ground");
        }
        private IEnumerator Commands()
        {
            Setup("zodiac_tiger", "zodiac_dragon", "paimon"); float interval = game.run.Player.AttackInterval;
            var pack = new List<TowerGame.Fighter> { Spawn(2, 1e6f, false), Spawn(2.8f, 1e6f, false), Spawn(5, 1e6f) };
            yield return Wait(.6f);
            Prime(PassiveKind.Roar); Prime(PassiveKind.PearlWave); Prime(PassiveKind.Melody);
            yield return Fight(3, "zodiac_tiger", "zodiac_dragon", "paimon");
            Verify("zodiac_tiger", game.PassiveFireCount(PassiveKind.Roar) > 0 && pack[0].stunTime > 0 && pack[0].stunTime < 2, "the roar stuns nearby foes");
            VerifyFired("zodiac_dragon", "the pearl wave pierces");
            Verify("paimon", game.PassiveFireCount(PassiveKind.Melody) > 0 && Passives.MelodyRemaining > 0 && game.run.Player.AttackInterval < interval, "the melody quickens the player to " + game.run.Player.AttackInterval);
        }
        private IEnumerator Seals()
        {
            Setup("hwanung", "nuwa", "vassago"); Pack(3, .8f, 3, 1e6f);
            for (int i = 0; i < 3; i++) { Prime(PassiveKind.ThreeSeals); yield return Wait(.25f); }
            Verify("hwanung", game.PassiveFireCount(PassiveKind.ThreeSeals) >= 3, "wind, rain and cloud took turns (" + game.PassiveFireCount(PassiveKind.ThreeSeals) + ")");
            Prime(PassiveKind.SkyStone); yield return Wait(.2f);
            Verify("nuwa", game.PlayerShield > 0, "five-colored stones shield the player (" + Mathf.RoundToInt(game.PlayerShield) + ")");
            Call("Fire", new Vector2(6, Ground + .9f), game.hero.pos + Vector2.up * .9f, 2.5f, 5f, true, Color.red, 0f, null, 1, 0f);
            Prime(PassiveKind.Foresight);
            yield return Fight(2, "vassago");
            Verify("vassago", game.PassiveFireCount(PassiveKind.Foresight) > 0 && game.HostileProjectiles == 0, "the incoming bullet is shot down");
        }
        private void Guardians()
        {
            Setup("athena", "zodiac_ox", "isis"); var near = Spawn(1, 1e6f, false);
            float armor = game.run.Player.Armor; game.hero.hp = game.PlayerMaxHealth * .45f; game.invulnerable = 0;
            float before = game.hero.hp; Call("HurtHero", 10f);
            Verify("athena", near.stunTime > 0 && before - game.hero.hp < 10 * .88f + .01f, "petrified the attacker, took " + (before - game.hero.hp));
            Verify("zodiac_ox", game.run.Player.Armor > armor, "armor " + armor + " → " + game.run.Player.Armor);
            Verify("isis", game.PlayerShield > 0, "wings shield " + Mathf.RoundToInt(game.PlayerShield));
        }
        private void Wards()
        {
            Setup("ganesha", "osiris", "tyr");
            float hp = game.hero.hp; Call("HurtHero", 20f);
            Expect(Mathf.Approximately(game.hero.hp, hp), "ganesha: the first blow of the floor is turned aside");
            Verify("ganesha", game.PassiveFireCount(PassiveKind.Pathfinder) > 0 && Mathf.Approximately(Passives.TrapDamageScale, .5f), "blocked the first blow, traps halved");
            game.invulnerable = 0; game.hero.hp = 1; Call("HurtHero", 50f);
            Verify("osiris", game.hero.hp >= game.PlayerMaxHealth * .3f - .5f && Passives.RebirthFloors == 3, "risen at " + Mathf.RoundToInt(game.hero.hp) + " HP");
            game.hero.hp = game.PlayerMaxHealth * .4f; var foe = Spawn(4, 1e6f);
            float scale = (float)typeof(TowerGame).GetProperty("PassiveDamageTakenScale", Hidden).GetValue(game, null);
            Verify("tyr", Mathf.Abs(DamageScale(foe, game.run.Player) - 1.3f) < .001f && Mathf.Abs(scale - .85f) < .001f, "wounded resolve: dealt ×" + DamageScale(foe, game.run.Player) + ", taken ×" + scale);
        }
        private IEnumerator Dodges()
        {
            Setup("loki", "bael", "zodiac_rat"); var near = Spawn(.8f, 1e6f);
            game.DashOverride = true; yield return null; game.DashOverride = false;
            Verify("bael", game.invulnerable > .6f && game.hero.art.Tint.a < .5f, "veiled for " + game.invulnerable + "s");
            // Past the base perfect-dodge window but inside the scout's.
            Set("dodgeStartedAt", game.elapsed - .18f); Set("dodgeCounterGranted", false); game.dashTime = Mathf.Max(game.dashTime, .05f); game.invulnerable = Mathf.Max(game.invulnerable, .1f);
            bool perfect = (bool)Call("TryPrimeDodgeCounter");
            Verify("zodiac_rat", perfect && game.dashCooldown == 0, "late perfect dodge counted, dash ready");
            yield return Wait(1);
            Verify("loki", game.PassiveFireCount(PassiveKind.Decoy) > 0 && near.hp < near.maxHp, "the illusion exploded for " + Mathf.RoundToInt(near.maxHp - near.hp));
        }
        private IEnumerator Healers()
        {
            Setup("raphael", "princess_bari", "zodiac_rabbit"); Pack(6, 1, 1, 1e6f);
            game.hero.hp = game.PlayerMaxHealth * .2f; Set("healClock", 4.99f);
            yield return Fight(1, "princess_bari", "zodiac_rabbit");
            VerifyFired("princess_bari", "heals more on a deep wound"); VerifyFired("zodiac_rabbit", "the moon herb regenerates");
            game.hero.hp = game.PlayerMaxHealth; Set("playerShield", 0f); Call("HealPlayer", 30f, true);
            Verify("raphael", game.PlayerShield > 0, "overflow became a " + Mathf.RoundToInt(game.PlayerShield) + " shield");
        }
        private IEnumerator Lore()
        {
            Setup("metatron", "brahma", "dagda"); Set("passiveRandom", new LoadedDice());
            game.run.GetCompanion("metatron").Apply(TowerRun.GetAugment("metatron.skill1"));
            game.run.GetCompanion("brahma").Apply(TowerRun.GetAugment("brahma.skill1"));
            game.run.Player.Apply(TowerRun.GetAugment("player.fireball"));
            game.ReconcileLoadout(); Pack(3, .8f, 3, 1e6f);
            yield return Fight(10, "metatron", "brahma");
            VerifyFired("metatron", "a cast shortened the party's cooldowns"); VerifyFired("brahma", "a cast echoed");
            game.hero.hp = game.PlayerMaxHealth * .4f; float hp = game.hero.hp;
            ClearEnemies(); yield return null; yield return null;
            Verify("dagda", game.PassiveFireCount(PassiveKind.Cauldron) > 0 && game.hero.hp >= hp + game.PlayerMaxHealth * .3f - 1, "the cauldron healed to " + Mathf.RoundToInt(game.hero.hp));
        }
        private void Judgments()
        {
            Setup("michael", "morrigan", "anubis");
            var elite = Spawn(4, 400, true, true);
            Verify("michael", Mathf.Abs(DamageScale(elite, game.run.Player) - 1.25f) < .001f, "elites take ×" + DamageScale(elite, game.run.Player));
            elite.hp = 100; var wounded = Spawn(5, 400); wounded.hp = 100;
            Verify("morrigan", Mathf.Abs(DamageScale(wounded, game.run.Player) - 1.35f) < .001f && Mathf.Abs(DamageScale(elite, game.run.Player) - 1.8f) < .001f,
                "wounded foes take ×" + DamageScale(wounded, game.run.Player) + ", wounded elites ×" + DamageScale(elite, game.run.Player));
            var doomed = Spawn(6, 300); doomed.hp = 60;
            Call("StrikeEnemy", doomed, 20f, game.run.Player, 0f);
            Call("StrikeEnemy", elite, 20f, game.run.Player, 0f);
            Verify("anubis", doomed.hp <= 0 && elite.hp > 0 && game.PassiveFireCount(PassiveKind.Judgment) > 0, "a low foe was judged, the elite spared");
        }
        private void Fellowship()
        {
            Setup("dangun", "lugh", "zodiac_ox"); var foe = Spawn(4, 1e6f);
            Verify("dangun", Mathf.Abs(DamageScale(foe, game.run.Player) - 1.18f) < .001f, "three companions give ×" + DamageScale(foe, game.run.Player));
            var lugh = game.run.GetCompanion("lugh"); var actor = game.allies.Find(a => a.id == "lugh");
            var far = Spawn(actor.pos.x + 12, 1e6f);
            Verify("lugh", Mathf.Abs(lugh.Range - lugh.Definition.Range * 1.3f) < .001f && Mathf.Abs(DamageScale(far, lugh) - 1.78f) < .001f,
                "range " + lugh.Range + ", far foes ×" + DamageScale(far, lugh));
        }

        private IEnumerator Presentation()
        {
            Setup("osiris", "paimon", "nezha");
            game.OpenParty("osiris"); yield return Capture("party-osiris", "패시브 검증 · 파티 창의 고유 패시브와 상태");
            game.party = false;
            Setup();
            // Deal a recruit set through the run's own entry, filling any optional parameters with their defaults.
            var begin = typeof(TowerRun).GetMethod("BeginReward", Hidden); var parameters = begin.GetParameters(); var arguments = new object[parameters.Length];
            arguments[0] = true;
            for (int i = 1; i < parameters.Length; i++)
                arguments[i] = parameters[i].ParameterType.IsEnum ? Enum.ToObject(parameters[i].ParameterType, parameters[i].DefaultValue) : parameters[i].DefaultValue;
            begin.Invoke(game.run, arguments);
            Expect(game.run.IsRecruitOffer && game.run.Offers.Count == 3, "a recruit card set is dealt for the capture");
            yield return Capture("recruit-cards", "패시브 검증 · 영입 카드의 고유 패시브");
        }
        private IEnumerator Capture(string suffix, string message)
        {
            yield return null; yield return null;
            game.Notice(message, 5); yield return capture.CaptureForSuite(prefix, suffix);
            report.screenshots.Add(prefix + "-" + suffix + ".png");
        }

        private void Finish(Exception error)
        {
            if (finishing) return; finishing = true;
            if (error != null) report.errors.Add(error is TargetInvocationException && error.InnerException != null ? error.InnerException.ToString() : error.ToString());
            report.verified = verified.Count;
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0 && verified.Count == report.total;
            if (game != null) { game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = game.DropOverride = false; }
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= RecordLog;
            if (quitOnComplete) Application.Quit(report.success ? 0 : 1);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        private enum EnemyPattern { MeleeSlam, AimedFan, GapWave, RadialGap }
        /// <summary>An elite's behaviour prefix: faster, a second slam right after the first, or roused from the start.</summary>
        internal enum EliteAffix { None, Swift, Relentless, Double }
        private sealed class EnemyState
        {
            public int ordinal, cycle, releases;
            public float clock, hoverClock, phase, hoverHeight, hoverX, lastRelease = -1, followUp = -1;
            public EnemyPattern pattern;
            public Transform shadow;
            // The floor pack this monster belongs to (-1: none, as for fixtures) and whether it has been roused.
            public int packId = -1;
            public bool alerted;
            // A flyer's dive toward the hero: progress 0..1 (-1: still perched) between two hover heights.
            public float swoop = -1, swoopFrom, swoopTo;
            public EliteAffix affix;
            // A melee lunge under way: time left, where it stops, its speed and the x it last left the monster at.
            public float lunge, lungeTo, lungeSpeed, lungeAt;
            // A ledge leap: crouch time left, whether airborne, where it lands and its run speed across. leapt: it leapt up
            // onto the ledge it stands on (cleared once it stands on the ground again), the only kind that drops back off.
            public float leapCrouch, leapSpeed;
            public bool leaping, leapt;
            public Vector2 leapTo;
            // A ring cast's reach as its windup began (a full ring, or the bundle beyond), and how long a heavy ready for its
            // melee turn has been left out of its blow's reach.
            public bool closeRing;
            public float stranded;
        }
        private readonly Dictionary<Fighter, EnemyState> enemyCombat = new Dictionary<Fighter, EnemyState>();
        // Packs roused on this floor, and whether the hero has reached the exit stretch with monsters still standing.
        private readonly HashSet<int> alertedPacks = new HashSet<int>();
        private bool converging;
        internal const float PackSight = 15, AlertedSight = 28, PackSprint = 3.4f, PackStride = 2.2f, HeavyStride = .75f, AlertedRetreat = 2.5f;
        internal const float StragglerGap = 14, AttackTokenWait = .25f, SwoopSeconds = .6f, SwoopClearance = 3, DoubleStrikeDelay = .25f, SwiftScale = 1.4f;
        // Melee: a short windup begun within MeleeStart (closing to the stance meanwhile), then a lunge along the locked facing,
        // LungeReach at most and never LungeOvershoot past the locked point. Its blow sweeps LungeRadius across and MeleeHeight
        // up or down from where it set off to where it stops; a double strike's second blow steps DoubleStrikeStep at most.
        internal const float MeleeWindup = .55f, HeavyMeleeWindup = .8f, MeleeStance = 2.6f, HeavyMeleeStance = 1.75f, MeleeStart = 3.6f, MeleeHeight = 1.8f;
        internal const float LungeSeconds = .17f, LungeReach = 3.5f, LungeOvershoot = .9f, LungeRadius = 1.2f, DoubleStrikeStep = .6f;
        // A roused pack's melee monster under a hero on a ledge out of its reach (up to the third tier) crouches, then leaps up
        // just short of the hero. A heavy ready for its melee turn but out of its blow's reach for HeavyReachWait takes its
        // ranged turn instead.
        internal const float LeapCrouch = .4f, LeapRise = 4.3f, LeapRange = 3, LeapClearance = .5f, LeapShort = 1, LeapRecover = .5f, HeavyReachWait = 1;

        private void ConfigureEnemy(Fighter actor, int kind, int ordinal)
        {
            actor.kind = kind;
            actor.isBoss = false; actor.isElite = false;
            actor.airborne = kind == 3;
            actor.difficultyFloor = run == null ? 1 : run.Floor;
            int scaled = DifficultyFloor(actor.difficultyFloor);
            actor.damageScale = TowerDifficulty.DamageMultiplier(scaled);
            actor.moveScale = TowerDifficulty.MoveMultiplier(scaled);
            actor.bulletSpeedScale = TowerDifficulty.BulletMultiplier(scaled);
            actor.attackRateScale = TowerDifficulty.AttackRateMultiplier(scaled);
            actor.hp = actor.maxHp = (kind == 3 ? 52 : kind == 2 ? 80 : kind == 1 ? 48 : 60) * TowerDifficulty.HealthMultiplier(scaled);
            ApplyMonsterStats(actor);
            // Members of one pack open fire a beat apart; packs further down the floor start fresh.
            actor.cooldown = (.9f + ordinal % 4 * .27f) / actor.attackRateScale;
            actor.windup = actor.attackPose = actor.slowTime = actor.stunTime = 0;
            actor.poiseDamage = actor.poiseRest = actor.staggerTime = actor.poiseGuard = actor.moveVelocity = 0;
            actor.facing = hero.pos.x >= actor.pos.x ? 1 : -1;
            actor.velocityY = 0; actor.grounded = !actor.airborne;
            float floorHeight = world.GroundAt(actor.pos.x);
            if (actor.airborne && actor.pos.y <= floorHeight) actor.pos.y = floorHeight + 3.8f;
            var state = CreateEnemyState(actor, ordinal);
            enemyCombat[actor] = state;
            if (actor.airborne) actor.pos.y = state.hoverHeight + Mathf.Sin(state.phase) * .4f;
            actor.root.transform.position = actor.pos;
            UpdateEnemyShadow(actor, state);
        }

        private static void ApplyMonsterStats(Fighter actor)
        {
            var definition = actor.monster;
            if (definition == null) return;
            actor.hp = actor.maxHp *= definition.HealthMultiplier;
            actor.damageScale *= definition.DamageMultiplier;
            actor.moveScale *= definition.MoveMultiplier;
            actor.bulletSpeedScale *= definition.BulletMultiplier;
            actor.attackRateScale *= definition.AttackRateMultiplier;
        }

        private static Color ThemedBoltColor(Fighter actor, Color fallback)
        {
            Color color;
            return actor.monster != null && ColorUtility.TryParseHtmlString("#" + TowerThemeCatalog.Get(actor.theme).AttackHex, out color) ? color : fallback;
        }

        private static EnemyState CreateEnemyState(Fighter actor, int ordinal)
        {
            ordinal = Mathf.Max(0, ordinal);
            return new EnemyState { ordinal = ordinal, cycle = ordinal % 3, phase = ordinal * 1.37f,
                hoverHeight = actor.pos.y, hoverX = actor.pos.x, shadow = actor.root.transform.Find("Shadow"),
                pattern = actor.kind == 0 ? EnemyPattern.MeleeSlam : actor.kind == 2 ? EnemyPattern.RadialGap : (EnemyPattern)(1 + ordinal % 3) };
        }

        /// <summary>Clears every threat of the previous floor: combat state, monsters still waiting to spawn and traps.</summary>
        private void ResetEnemyCombat()
        {
            enemyCombat.Clear();
            alertedPacks.Clear(); converging = false;
            ResetBossCombat();
            pendingSpawns.Clear();
            ClearTraps();
        }

        private void RemoveEnemyCombat(Fighter actor)
        {
            RemoveBossCombat(actor);
            EnemyState state;
            if (!enemyCombat.TryGetValue(actor, out state)) return;
            enemyCombat.Remove(actor);
            if (state.packId >= 0 && actor.hp <= 0) PackMemberFell(state.packId);
        }

        /// <summary>Records a streamed monster's pack; its opening stagger now lands when the pack is roused.</summary>
        private void JoinPack(Fighter actor, int pack)
        {
            EnemyState state;
            if (pack < 0 || !enemyCombat.TryGetValue(actor, out state)) return;
            state.packId = pack; actor.cooldown = 0;
            if (alertedPacks.Contains(pack)) AlertEnemy(actor, state);
        }

        private void AlertEnemy(Fighter actor, EnemyState state)
        {
            if (state.alerted) return;
            state.alerted = true;
            // Members of one pack open fire a beat apart once roused.
            actor.cooldown = Mathf.Max(actor.cooldown, (.9f + state.ordinal % 4 * .27f) / actor.attackRateScale);
        }

        /// <summary>Rouses every standing member of <paramref name="pack"/>; members still queued wake as they appear.</summary>
        private void AlertPack(int pack)
        {
            if (pack < 0 || !alertedPacks.Add(pack)) return;
            foreach (var other in enemies)
            {
                EnemyState state;
                if (other.hp > 0 && enemyCombat.TryGetValue(other, out state) && state.packId == pack) AlertEnemy(other, state);
            }
        }

        private void AlertPackOf(Fighter actor)
        { EnemyState state; if (actor.hp > 0 && enemyCombat.TryGetValue(actor, out state)) AlertPack(state.packId); }

        // A fallen member rouses the rest of its pack; the last walker to fall rouses the next pack down the floor.
        // A flyer wakes with its pack but never holds the alarm back from the next one.
        private void PackMemberFell(int pack)
        {
            bool standing = false, flying = false; int next = int.MaxValue;
            foreach (var other in enemies)
            {
                EnemyState state;
                if (other.hp <= 0 || !enemyCombat.TryGetValue(other, out state) || state.packId < 0) continue;
                if (other.airborne) { flying |= state.packId == pack; continue; }
                if (state.packId == pack) standing = true; else if (state.packId > pack) next = Mathf.Min(next, state.packId);
            }
            foreach (var spawn in pendingSpawns)
            {
                if (spawn.kind == 3) { flying |= spawn.pack == pack; continue; }
                if (spawn.pack == pack) standing = true; else if (spawn.pack > pack) next = Mathf.Min(next, spawn.pack);
            }
            if (standing || flying) AlertPack(pack);
            if (!standing && next != int.MaxValue) AlertPack(next);
        }

        /// <summary>Whether another pack monster may begin an attack: two prepare at once, three past the thirtieth floor.</summary>
        private bool AttackTokenFree()
        {
            int cap = run != null && run.Floor > 30 ? 3 : 2, busy = 0;
            foreach (var other in enemies) if (!other.isBoss && other.hp > 0 && other.windup > 0 && ++busy >= cap) return false;
            return true;
        }

        /// <summary>Whether a monster standing at <paramref name="at"/> shows inside the viewport widened by <paramref name="margin"/>.</summary>
        private bool InView(Vector2 at, float margin)
        {
            if (!view) return false;
            Vector3 visible = view.WorldToViewportPoint(at + Vector2.up * .8f);
            return visible.z > 0 && visible.x > margin && visible.x < 1 - margin && visible.y > margin && visible.y < 1 - margin;
        }

        /// <summary>
        /// Moves a roused straggler, unseen, just past the screen ahead of the hero so it comes back in from the front: a
        /// walker onto the ground, a flyer onto a post low enough to reach. False when no hidden spot lies inside the exit;
        /// the exit convergence then calls it in.
        /// </summary>
        private bool RelocateAhead(Fighter actor, EnemyState state)
        {
            const float hidden = -.05f;
            if (!view || InView(actor.pos, hidden)) return false;
            // Candidates start past the camera's actual right edge, so wide screens hide them as surely as 16:9 ones.
            float edge = view.ViewportToWorldPoint(new Vector3(1 - hidden, .5f, -view.transform.position.z)).x;
            for (int step = 0; step < 3; step++)
            {
                float x = Mathf.Max(hero.pos.x + 12, edge + .6f) + step + state.ordinal % 3 * .4f;
                if (x > TowerWorld.ExitX - 2 || x > TowerWorld.Right - .8f) return false;
                var spot = new Vector2(x, world.GroundAt(x));
                if (InView(spot, hidden)) continue;
                if (actor.airborne)
                {
                    // A flyer hovers no higher than a dive over that ground would leave it and roams as one that has dived.
                    state.hoverX = x; state.hoverHeight = Mathf.Min(state.hoverHeight, spot.y + SwoopClearance); state.swoop = 1;
                    actor.pos = new Vector2(x, state.hoverHeight + Mathf.Sin(state.hoverClock * 1.35f + state.phase) * .4f);
                }
                else { actor.pos = spot; actor.lastX = x; actor.velocityY = 0; actor.grounded = true; actor.support = -1; actor.dropTime = 0; }
                actor.cooldown = Mathf.Max(actor.cooldown, .6f);
                return true;
            }
            return false;
        }

        /// <summary>
        /// A pack flyer dives to just above the hero once roused, passed or left waiting, while the hero stands under it.
        /// The dive aims at the surface the hero stands on, never a jump or a fall in passing; a flyer left high above a hero
        /// who has since stepped down dives again.
        /// </summary>
        private void TickSwoop(EnemyState state, Vector2 delta, float dt)
        {
            if (state.swoop < 0 || state.swoop >= 1)
            {
                bool roused = state.swoop >= 1 || state.alerted || delta.x >= 6 || elapsed - floorStartedAt >= 25;
                if (!roused || !hero.grounded || Mathf.Abs(delta.x) >= 7 || state.hoverHeight - hero.pos.y <= 3.5f) return;
                state.swoop = 0; state.swoopFrom = state.hoverHeight; state.swoopTo = hero.pos.y + SwoopClearance;
            }
            state.swoop = Mathf.Min(1, state.swoop + dt / SwoopSeconds);
            state.hoverHeight = Mathf.Lerp(state.swoopFrom, state.swoopTo, Mathf.SmoothStep(0, 1, state.swoop));
        }

        /// <summary>Gives an elite its prefix, fixed by the floor's seed; ranged elites never draw the double slam.</summary>
        private EliteAffix ApplyEliteAffix(Fighter actor)
        {
            EnemyState state;
            if (!enemyCombat.TryGetValue(actor, out state)) return EliteAffix.None;
            bool melee = actor.kind == 0 || actor.kind == 2;
            uint roll = unchecked((uint)(seed * 73856093) ^ (uint)((run == null ? 1 : run.Floor) * 19349663) ^ (uint)(state.ordinal * 83492791));
            roll ^= roll >> 15; roll = unchecked(roll * 2246822519u); roll ^= roll >> 13;
            var affix = (EliteAffix)(1 + (int)(roll % (melee ? 3u : 2u)));
            state.affix = affix;
            if (affix == EliteAffix.Swift) { actor.moveScale *= SwiftScale; actor.attackRateScale *= SwiftScale; }
            else if (affix == EliteAffix.Relentless && state.packId >= 0) AlertEnemy(actor, state);
            return affix;
        }

        internal static string EliteAffixName(EliteAffix affix)
        { return affix == EliteAffix.Swift ? "신속" : affix == EliteAffix.Double ? "연타" : affix == EliteAffix.Relentless ? "추격" : ""; }

        internal string EnemyPatternName(Fighter actor)
        { if (actor != null && actor.isBoss) return BossPatternName(actor); EnemyState state; return enemyCombat.TryGetValue(actor, out state) ? state.pattern.ToString() : "None"; }
        internal float EnemyTelegraphRemaining(Fighter actor)
        { return actor == null ? 0 : Mathf.Max(0, actor.windup); }
        internal Vector2 EnemyLockedAim(Fighter actor)
        { return actor == null ? Vector2.zero : actor.strikePoint; }
        internal float EnemyLastReleaseAt(Fighter actor)
        { if (actor != null && actor.isBoss) return BossLastReleaseAt(actor); EnemyState state; return enemyCombat.TryGetValue(actor, out state) ? state.lastRelease : -1; }
        internal int EnemyReleaseCount(Fighter actor)
        { if (actor != null && actor.isBoss) return BossReleaseCount(actor); EnemyState state; return enemyCombat.TryGetValue(actor, out state) ? state.releases : 0; }

        private void TickEnemy(Fighter actor, float dt)
        {
            if (actor == null) return;
            if (practice) { TickPracticeTarget(actor, dt); return; }
            if (actor.isBoss) { TickBoss(actor, dt); return; }
            if (actor.hp <= 0 || !actor.root) { RemoveEnemyCombat(actor); return; }
            EnemyState state;
            // Fixture-created actors may already carry custom health/cooldowns. Lazy state
            // registration must not reset their gameplay stats or revive a dead actor.
            if (!enemyCombat.TryGetValue(actor, out state)) { state = CreateEnemyState(actor, enemies.IndexOf(actor)); enemyCombat.Add(actor, state); }
            state.clock += dt;
            // A struck member rouses its whole pack.
            if (actor.hurt > 0 && state.packId >= 0) AlertPack(state.packId);
            actor.hurt = Mathf.Max(0, actor.hurt - dt);
            actor.attackPose = Mathf.Max(0, actor.attackPose - dt);
            actor.cooldown = Mathf.Max(0, actor.cooldown - dt);
            actor.slowTime = Mathf.Max(0, actor.slowTime - dt);
            actor.stunTime = Mathf.Max(0, actor.stunTime - dt);
            // Poise refills and a break's stagger and guard run down; the player's knockback slides out (TowerPoise.cs).
            TickPoise(actor, dt);
            // Knockback, or a fixture placing the monster, ends a lunge where it now stands; a leap ends on touching down.
            if (state.lunge > 0 && Mathf.Abs(actor.pos.x - state.lungeAt) > .01f) state.lunge = 0;
            if (state.leaping && actor.grounded) { state.leaping = false; actor.cooldown = Mathf.Max(actor.cooldown, LeapRecover); }
            if (state.leapt && actor.grounded && actor.support < 0) state.leapt = false;
            float beforeX = actor.pos.x;
            bool moving = false;
            // A stun, or the stagger of a poise break, holds the monster and keeps its windup cancelled.
            if (actor.stunTime > 0 || actor.staggerTime > 0)
            {
                CancelEnemyWindup(actor, state);
            }
            else if (state.leapCrouch > 0 || state.leaping)
            {
                TickLeap(actor, state, dt);
            }
            else if (actor.windup > 0)
            {
                // strikePoint and facing were captured at the beginning of the windup.
                // Neither target tracking nor homing is applied during or after the windup;
                // a melee monster that began short of its stance only steps on toward the locked point.
                if (state.pattern == EnemyPattern.MeleeSlam) StepIntoStance(actor, dt);
                actor.windup = Mathf.Max(0, actor.windup - dt);
                if (actor.windup <= 0)
                {
                    if (EnemyCanFire(actor)) ReleaseEnemyPattern(actor, state, 0);
                    else actor.cooldown = Mathf.Max(actor.cooldown, .8f);
                }
            }
            else if (state.lunge > 0 || state.followUp >= 0)
            {
                // A lunge runs its course while a second blow or wave counts down. A later wave was reserved in the bolt
                // budget along with the first as the windup began, so it fires on time without asking again.
                if (state.lunge > 0) TickLunge(actor, state, dt);
                if (state.followUp >= 0 && (state.followUp -= dt) <= 0)
                { state.followUp = -1; if (EnemyCanFire(actor)) ReleaseEnemyPattern(actor, state, 1); }
            }
            else
            {
                Vector2 delta = hero.pos - actor.pos;
                float distance = delta.magnitude;
                actor.facing = delta.x >= 0 ? 1 : -1;
                // Pack monsters (streamed floors only) wake together and press in; fixtures keep the plain rules below.
                if (state.packId >= 0 && distance < PackSight) AlertPack(state.packId);
                bool alerted = state.packId >= 0 && state.alerted;
                if (alerted && delta.x > StragglerGap && RelocateAhead(actor, state))
                { beforeX = actor.pos.x; delta = hero.pos - actor.pos; distance = delta.magnitude; actor.facing = delta.x >= 0 ? 1 : -1; }
                if (state.packId >= 0 && actor.airborne) TickSwoop(state, delta, dt);
                bool swooping = state.swoop >= 0 && state.swoop < 1;
                // Near the exit roused stragglers come from anywhere on the floor.
                if (distance < (alerted ? (converging ? float.PositiveInfinity : AlertedSight) : PackSight))
                {
                    bool melee = actor.kind == 0 || actor.kind == 2 && state.cycle % 3 == 0;
                    float approach = melee ? MeleeStanceOf(actor) : actor.airborne ? 6.8f : 6.1f;
                    float pace = actor.kind == 2 ? 1.35f : actor.airborne ? 1.75f : 1.9f;
                    // Roused walkers run in until a couple of steps short of their stance, then close at a stride.
                    if (alerted && !actor.airborne) pace = (Mathf.Abs(delta.x) > approach + 2 ? PackSprint : PackStride) * (actor.kind == 2 ? HeavyStride : 1);
                    float speed = pace * actor.moveScale * (actor.slowTime > 0 ? .45f : 1);
                    // On the exit stretch, where no hidden spot ahead is left, a roused flyer's post drifts toward the hero.
                    if (alerted && converging && actor.airborne && Mathf.Abs(hero.pos.x - state.hoverX) > approach)
                        state.hoverX = Mathf.MoveTowards(state.hoverX, hero.pos.x, speed * dt);
                    // A monster the player's blow still slides back neither walks nor begins a lunging blow or a leap.
                    bool knocked = Knocked(actor);
                    if (!knocked && Mathf.Abs(delta.x) > approach + .3f)
                    { actor.pos.x += Mathf.Sign(delta.x) * speed * dt; moving = true; }
                    else if (!knocked && !melee && Mathf.Abs(delta.x) < (alerted ? AlertedRetreat : 3.6f) && actor.cooldown > .7f)
                    { actor.pos.x -= Mathf.Sign(delta.x) * speed * .65f * dt; moving = true; }
                    bool landed = actor.airborne || actor.kind != 3 || actor.grounded;
                    bool inReach = melee ? distance < MeleeStart && Mathf.Abs(delta.y) < MeleeHeight : distance < 11.8f;
                    if (inReach && landed && actor.cooldown <= 0 && !swooping && !(melee && knocked) && EnemyCanFire(actor))
                    {
                        // Only a few pack monsters prepare at once; the rest wait a beat for a free token.
                        if (state.packId >= 0 && !AttackTokenFree()) actor.cooldown = AttackTokenWait;
                        else BeginEnemyWindup(actor, state, melee);
                    }
                    // A ledge just above a roused pack's melee reach is no refuge: the monster leaps onto it.
                    else if (melee && alerted && actor.cooldown <= 0 && !knocked && HeroOnLedgeAbove(actor) && EnemyCanFire(actor))
                    {
                        if (!AttackTokenFree()) actor.cooldown = AttackTokenWait;
                        else BeginLeap(actor, state);
                    }
                    // A heavy ready for its melee turn whose blow cannot reach the hero (up a ledge, or still far off) gives
                    // the turn up after HeavyReachWait and casts instead.
                    state.stranded = actor.kind == 2 && melee && !inReach && !knocked && actor.cooldown <= 0 && state.leapCrouch <= 0 ? state.stranded + dt : 0;
                    if (state.stranded > HeavyReachWait) { state.cycle++; state.stranded = 0; }
                }
            }

            actor.pos.x = Mathf.Clamp(actor.pos.x, TowerWorld.Left + .8f, TowerWorld.Right - .8f);
            if (actor.airborne)
            {
                float old = actor.pos.y;
                if (actor.windup <= 0 && state.followUp < 0) state.hoverClock += dt;
                float leash = state.swoop >= 0 ? 4 : 1;
                actor.pos.x = Mathf.Clamp(actor.pos.x, state.hoverX - leash, state.hoverX + leash);
                actor.pos.y = state.hoverHeight + Mathf.Sin(state.hoverClock * 1.35f + state.phase) * .4f;
                actor.velocityY = dt > 0 ? (actor.pos.y - old) / dt : 0; actor.grounded = false;
            }
            else StepGravity(actor, dt, DropsToHero(actor, state));
            actor.root.transform.position = actor.pos;
            UpdateEnemyShadow(actor, state);
            float actualSpeed = dt > 0 ? (actor.pos.x - beforeX) / dt : 0;
            moving = Mathf.Abs(actualSpeed) > .08f;
            actor.art.SetLocomotion(actor.grounded, actor.velocityY, false, Mathf.Abs(actualSpeed), actualSpeed);
            TelegraphCue(actor);
            actor.art.SetPose(moving, actor.windup > 0 || actor.attackPose > 0, actor.hurt > 0 || actor.staggerTime > 0, actor.facing, dt);
        }

        private bool EnemyCanFire(Fighter actor)
        {
            if (hero.hp <= 0 || !view || Vector2.Distance(actor.pos, hero.pos) > 12.2f) return false;
            Vector3 visible = view.WorldToViewportPoint(actor.pos + Vector2.up * .8f);
            return visible.z > 0 && visible.x > .025f && visible.x < .975f && visible.y > .025f && visible.y < .975f;
        }

        private void BeginEnemyWindup(Fighter actor, EnemyState state, bool melee)
        {
            var pattern = melee ? EnemyPattern.MeleeSlam : NextRangedPattern(actor, state, hero.pos);
            // A ring cast's reach is settled now, so the budget, its release and a heavy's second ring all count the same volley.
            bool close = pattern == EnemyPattern.RadialGap && CloseRing(actor, hero.pos);
            // A volley whose stages together would push the monsters' bolts on screen past the floor's budget waits a beat;
            // none is dropped.
            if (!melee && !BoltBudgetAdmits(actor, VolleyTotal(actor, pattern, close))) { actor.cooldown = Mathf.Max(actor.cooldown, AttackBudgetWait); return; }
            state.pattern = pattern; state.closeRing = close;
            state.cycle++;
            float windup = melee ? actor.kind == 2 ? HeavyMeleeWindup : MeleeWindup : state.pattern == EnemyPattern.RadialGap ? RadialWindup : RangedWindup;
            actor.windup = windup;
            actor.cooldown = windup + (melee ? 1.1f : RangedRecovery) / actor.attackRateScale;
            actor.strikePoint = hero.pos;
            actor.facing = hero.pos.x >= actor.pos.x ? 1 : -1;
            state.followUp = -1;
            StartEnemyMotion(actor, windup);
            TelegraphBegan(actor);
        }

        private void CancelEnemyWindup(Fighter actor, EnemyState state)
        { actor.windup = 0; state.followUp = -1; state.lunge = 0; state.leapCrouch = 0; }

        private static float MeleeStanceOf(Fighter actor) { return actor.kind == 2 ? HeavyMeleeStance : MeleeStance; }

        /// <summary>
        /// A melee monster whose windup began short of its stance closes on the locked point at a stride while it winds up,
        /// so a blow begun anywhere inside <see cref="MeleeStart"/> still reaches a hero who stays put.
        /// </summary>
        private static void StepIntoStance(Fighter actor, float dt)
        {
            if (actor.airborne) return;
            float gap = (actor.strikePoint.x - actor.pos.x) * actor.facing - MeleeStanceOf(actor);
            if (gap <= 0) return;
            float stride = PackStride * (actor.kind == 2 ? HeavyStride : 1) * actor.moveScale * (actor.slowTime > 0 ? .45f : 1);
            actor.pos.x += actor.facing * Mathf.Min(gap, stride * dt);
        }

        /// <summary>
        /// Starts a melee lunge along the facing locked with the aim: <paramref name="reach"/> at most, never more than
        /// <see cref="LungeOvershoot"/> past the locked point, and never through the chamber's walls or off the ledge the
        /// monster stands on. Returns where it stops, on the surface beneath.
        /// </summary>
        private Vector2 BeginLunge(Fighter actor, EnemyState state, float reach)
        {
            int dir = actor.facing >= 0 ? 1 : -1;
            float travel = Mathf.Clamp((actor.strikePoint.x - actor.pos.x) * dir + LungeOvershoot, 0, reach);
            float low = TowerWorld.Left + .8f, high = TowerWorld.Right - .8f;
            if (actor.grounded && actor.support >= 0 && actor.support < world.Ledges.Length)
            { var ledge = world.Ledges[actor.support]; low = Mathf.Max(low, ledge.Left + .2f); high = Mathf.Min(high, ledge.Right - .2f); }
            // Held short by a wall or a ledge's end, never pulled back past where it stands.
            float to = Mathf.Clamp(actor.pos.x + dir * travel, Mathf.Min(low, actor.pos.x), Mathf.Max(high, actor.pos.x));
            float distance = Mathf.Abs(to - actor.pos.x);
            state.lunge = distance > .05f ? LungeSeconds : 0; state.lungeTo = to; state.lungeSpeed = distance / LungeSeconds; state.lungeAt = actor.pos.x;
            if (state.lunge > 0 && vfx != null) vfx.Dust(actor.pos, dir, .35f);
            return new Vector2(to, actor.grounded ? world.SurfaceHeight(actor.support, to) : actor.pos.y);
        }

        private void TickLunge(Fighter actor, EnemyState state, float dt)
        {
            state.lunge = Mathf.Max(0, state.lunge - dt);
            actor.pos.x = state.lunge > 0 ? Mathf.MoveTowards(actor.pos.x, state.lungeTo, state.lungeSpeed * dt) : state.lungeTo;
            state.lungeAt = actor.pos.x;
        }

        /// <summary>
        /// The one place an enemy melee blow reaches the hero: a hero standing in the sweep from <paramref name="from"/> to
        /// <paramref name="to"/> is hurt. Returns whether the hero stood in it.
        /// </summary>
        private bool TryHitHeroWithMelee(Fighter actor, Vector2 from, Vector2 to)
        {
            if (!InMeleeSweep(from, to, hero.pos)) return false;
            // A dash just begun through the blow is a perfect dodge, as through a bolt; the dash's invulnerability turns the hit aside.
            TryPrimeDodgeCounter();
            HurtHeroBy(PaceHit.Melee, (actor.kind == 2 ? 12 : 10) * actor.damageScale);
            return true;
        }

        /// <summary>Whether feet at <paramref name="at"/> stand within LungeRadius across of the swept span and MeleeHeight of its height there.</summary>
        internal static bool InMeleeSweep(Vector2 from, Vector2 to, Vector2 at)
        {
            float x = Mathf.Clamp(at.x, Mathf.Min(from.x, to.x), Mathf.Max(from.x, to.x));
            float y = Mathf.Abs(to.x - from.x) > .001f ? Mathf.Lerp(from.y, to.y, (x - from.x) / (to.x - from.x)) : from.y;
            return Mathf.Abs(at.x - x) < LungeRadius && Mathf.Abs(at.y - y) < MeleeHeight;
        }

        /// <summary>
        /// Whether the hero stands on a ledge above a melee monster's blow but within a leap: more than MeleeHeight and at
        /// most LeapRise up, less than LeapRange across.
        /// </summary>
        private bool HeroOnLedgeAbove(Fighter actor)
        {
            float rise = hero.pos.y - actor.pos.y;
            return !actor.airborne && actor.grounded && hero.grounded && hero.support >= 0 && hero.support < world.Ledges.Length
                && rise > MeleeHeight && rise <= LeapRise && Mathf.Abs(hero.pos.x - actor.pos.x) < LeapRange;
        }

        /// <summary>Crouches for LeapCrouch toward a spot on the hero's ledge LeapShort short of the hero, fixed as it begins.</summary>
        private void BeginLeap(Fighter actor, EnemyState state)
        {
            var ledge = world.Ledges[hero.support];
            int side = hero.pos.x >= actor.pos.x ? 1 : -1;
            float x = Mathf.Clamp(hero.pos.x - side * LeapShort, ledge.Left + .35f, ledge.Right - .35f);
            state.leapTo = new Vector2(x, ledge.HeightAt(x)); state.leapCrouch = LeapCrouch; actor.facing = side;
            StartEnemyMotion(actor, LeapCrouch);
        }

        /// <summary>After the crouch, springs to clear the landing spot by LeapClearance and drifts over it by the top of the arc.</summary>
        private void TickLeap(Fighter actor, EnemyState state, float dt)
        {
            if (state.leapCrouch > 0)
            {
                state.leapCrouch -= dt;
                if (state.leapCrouch > 0) return;
                state.leapCrouch = 0; state.leaping = state.leapt = true;
                actor.velocityY = Mathf.Sqrt(2 * TowerPhysics.Gravity * Mathf.Max(LeapClearance, state.leapTo.y - actor.pos.y + LeapClearance));
                state.leapSpeed = Mathf.Abs(state.leapTo.x - actor.pos.x) * TowerPhysics.Gravity / actor.velocityY;
                actor.grounded = false; actor.support = -1;
                if (vfx != null) vfx.Dust(actor.pos, 0, .45f);
            }
            actor.pos.x = Mathf.MoveTowards(actor.pos.x, state.leapTo.x, state.leapSpeed * dt);
        }

        /// <summary>
        /// A pack monster that leapt onto a ledge, left above a hero out of its reach, drops through it to follow. Shooters
        /// and packs posted on a ledge hold it.
        /// </summary>
        private bool DropsToHero(Fighter actor, EnemyState state)
        {
            return state.packId >= 0 && state.leapt && actor.grounded && actor.support >= 0 && hero.grounded && actor.windup <= 0 && state.lunge <= 0
                && state.leapCrouch <= 0 && actor.pos.y - hero.pos.y > MeleeHeight;
        }

        private void ReleaseEnemyPattern(Fighter actor, EnemyState state, int stage)
        {
            Vector2 origin = actor.pos + Vector2.up;
            Vector2 aim = actor.strikePoint + Vector2.up;
            Vector2 direction = (aim - origin).sqrMagnitude < .001f ? Vector2.right * actor.facing : (aim - origin).normalized;
            state.lastRelease = state.clock; state.releases++;
            actor.attackPose = Mathf.Max(actor.attackPose, .3f);
            Color color = state.pattern == EnemyPattern.AimedFan ? new Color(1, .43f, .26f) : state.pattern == EnemyPattern.GapWave ? new Color(1, .7f, .26f) : new Color(.95f, .4f, .7f);
            if (state.pattern == EnemyPattern.MeleeSlam)
            {
                // The blow lands at once along the whole lunge; the monster then covers it in LungeSeconds. A blow released
                // while a knockback still slides takes over from it, so the slide never cuts the lunge short.
                actor.moveVelocity = 0;
                Vector2 stop = BeginLunge(actor, state, stage == 0 ? LungeReach : DoubleStrikeStep);
                SlashArc(stop + new Vector2(actor.facing * .8f, .9f), actor.facing, color);
                TryHitHeroWithMelee(actor, actor.pos, stop);
                // A double-strike elite slams the same locked spot again a moment later, from where its lunge stopped.
                if (stage == 0 && state.affix == EliteAffix.Double) state.followUp = DoubleStrikeDelay;
            }
            else if (state.pattern == EnemyPattern.AimedFan)
            {
                // Three bolts 12 degrees apart: jumped over or dashed through.
                foreach (float offset in new[] { -12f, 0f, 12f }) EnemyBolt(actor, origin, direction, offset, AimedBoltSpeed, 9, color);
            }
            else if (state.pattern == EnemyPattern.GapWave)
            {
                // Two level crests from the shooter's x toward the locked side, each a pair WavePair apart: the first at chest
                // height over the surface under the locked aim (standing there is hit), the second WaveCrestDelay later and
                // WaveCrestRise higher, so a jump carried to its top clears both and one begun too early falls into it.
                Vector2 level = Vector2.right * (actor.strikePoint.x >= actor.pos.x ? 1 : -1);
                float lane = WaveBase(actor.strikePoint) + .9f + (stage == 0 ? 0 : WaveCrestRise);
                foreach (float drop in new[] { 0f, WavePair }) EnemyBolt(actor, new Vector2(actor.pos.x, lane - drop), level, 0, AimedBoltSpeed, 8, color);
                if (stage == 0) state.followUp = WaveCrestDelay;
            }
            else if (state.closeRing)
            {
                // A hero within ring reach as the windup began meets a full ring, a bolt every 45 degrees from the aim; a heavy
                // follows with four more a quarter turn apart, turned 22.5 degrees off the first ring's.
                int count = VolleyBolts(actor, state.pattern, true, stage);
                float rotation = stage == 0 ? 0 : 22.5f;
                for (int i = 0; i < count; i++) EnemyBolt(actor, origin, direction, i * 360f / count + rotation, RingBoltSpeed, actor.kind == 2 ? 11 : 9, color);
                if (stage == 0 && actor.kind == 2) state.followUp = HeavyRingDelay;
            }
            else
            {
                // Out of ring reach the same cast is a tight bundle of three.
                foreach (float offset in new[] { -6f, 0f, 6f }) EnemyBolt(actor, origin, direction, offset, AimedBoltSpeed, actor.kind == 2 ? 11 : 9, color);
            }
            Sound(hitSound, .25f);
        }

        // Ranged volleys: windups and recovery (all floors alike), bolt speeds, the wave's second crest, the ring's reach and
        // the heavy's second ring. The monsters' bolts on screen stay within EnemyBoltCap: a volley is reserved whole, later
        // stages included, as its windup begins, and one that would pass the cap waits AttackBudgetWait; a later stage then
        // fires on time. A shooter far off the wave's height (a ledge post, a flyer) fires the fan instead.
        internal const float RangedWindup = .7f, RadialWindup = .85f, RangedRecovery = 1.6f, AimedBoltSpeed = 7, RingBoltSpeed = 5.5f;
        internal const float WaveCrestDelay = .42f, WaveCrestRise = 1.3f, WavePair = .3f, WaveLaneReach = 1.2f, CloseRingReach = 4, HeavyRingDelay = .5f, AttackBudgetWait = .25f;
        internal const int RingBolts = 8, HeavySecondRingBolts = 4;

        /// <summary>The bolts monsters may hold on screen at once: 12 up to the 30th floor, 16 to the 60th, 20 beyond. Bosses' are not counted.</summary>
        internal static int EnemyBoltCap(int floor) { return floor <= 30 ? 12 : floor <= 60 ? 16 : 20; }

        /// <summary>The next ranged volley: fan, wave and ring in turn (a heavy alternates wave and ring).</summary>
        private EnemyPattern NextRangedPattern(Fighter actor, EnemyState state, Vector2 target)
        {
            var pattern = actor.kind == 2 ? state.cycle % 3 == 1 ? EnemyPattern.RadialGap : EnemyPattern.GapWave : (EnemyPattern)(1 + state.cycle % 3);
            if (pattern == EnemyPattern.GapWave && Mathf.Abs(WaveBase(target) - actor.pos.y) > WaveLaneReach) pattern = EnemyPattern.AimedFan;
            return pattern;
        }

        /// <summary>The surface the wave's heights stand on: under the locked aim, so a hero locked mid-jump lands into its lane.</summary>
        private float WaveBase(Vector2 aim) { return SurfaceBelow(aim.x, aim.y); }

        private static bool CloseRing(Fighter actor, Vector2 aim) { return Vector2.Distance(actor.pos, aim) < CloseRingReach; }

        /// <summary>How many bolts <paramref name="stage"/> of a volley fires; <paramref name="close"/>: a ring cast begun within ring reach.</summary>
        private static int VolleyBolts(Fighter actor, EnemyPattern pattern, bool close, int stage)
        {
            switch (pattern)
            {
                case EnemyPattern.AimedFan: return stage == 0 ? 3 : 0;
                case EnemyPattern.GapWave: return stage <= 1 ? 2 : 0;
                case EnemyPattern.RadialGap: return !close ? stage == 0 ? 3 : 0 : stage == 0 ? RingBolts : stage == 1 && actor.kind == 2 ? HeavySecondRingBolts : 0;
                default: return 0;
            }
        }

        /// <summary>Every bolt a volley fires, its later stage included.</summary>
        private static int VolleyTotal(Fighter actor, EnemyPattern pattern, bool close)
        { return VolleyBolts(actor, pattern, close, 0) + VolleyBolts(actor, pattern, close, 1); }

        /// <summary>Monsters' bolts inside the camera's view, not counting bosses' or ones fading with their fallen shooter.</summary>
        internal int EnemyBoltsInView()
        {
            if (!view) return 0;
            int count = 0;
            foreach (var shot in shots)
            {
                if (!shot.hostile || shot.fade >= 0 || shot.shooter == null || shot.shooter.isBoss) continue;
                Vector3 at = view.WorldToViewportPoint(shot.pos);
                if (at.z > 0 && at.x >= 0 && at.x <= 1 && at.y >= 0 && at.y <= 1) count++;
            }
            return count;
        }

        /// <summary>
        /// Whether <paramref name="bolts"/> more fit the budget beside those on screen and those other shooters still owe:
        /// every stage of a volley winding up, the later stage of one already loosed.
        /// </summary>
        private bool BoltBudgetAdmits(Fighter actor, int bolts)
        {
            if (bolts <= 0) return true;
            int committed = EnemyBoltsInView();
            foreach (var other in enemies)
            {
                EnemyState state;
                if (other == actor || other.isBoss || other.hp <= 0 || !enemyCombat.TryGetValue(other, out state)) continue;
                if (other.windup > 0) committed += VolleyTotal(other, state.pattern, state.closeRing);
                else if (state.followUp >= 0) committed += VolleyBolts(other, state.pattern, state.closeRing, 1);
            }
            return committed + bolts <= EnemyBoltCap(run == null ? 1 : run.Floor);
        }

        private void EnemyBolt(Fighter actor, Vector2 origin, Vector2 direction, float offset, float speed, float damage, Color color)
        {
            Vector2 velocity = Quaternion.Euler(0, 0, offset) * direction;
            Fire(origin, origin + velocity * 12, speed * actor.bulletSpeedScale, damage * actor.damageScale, true, ThemedBoltColor(actor, color));
            StyleHostileLastShot(.16f, actor, EnemyBoltLife);
        }

        private void UpdateEnemyShadow(Fighter actor, EnemyState state)
        {
            if (!state.shadow) return;
            // A walker that leapt onto a ledge casts its shadow there.
            float floorHeight = !actor.airborne && actor.support >= 0 ? world.SurfaceHeight(actor.support, actor.pos.x) : world.GroundAt(actor.pos.x), height = Mathf.Max(0, actor.pos.y - floorHeight);
            state.shadow.localPosition = new Vector3(0, floorHeight - actor.pos.y + .025f, 0);
            state.shadow.localScale = new Vector3(actor.airborne ? 1.05f : 1.2f, .13f, 1);
            var renderer = state.shadow.GetComponent<SpriteRenderer>();
            if (renderer) renderer.color = new Color(0, 0, 0, Mathf.Clamp(.34f - height * .035f, .15f, .34f));
        }
    }
}

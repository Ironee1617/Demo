using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        // Every boss alternates the three shared techniques with its theme's signature technique.
        private enum BossPattern { CrownVolley, TwinSpiral, LeapingSlam, MagmaRain, Rockfall, ThornSweep, FrostBreath, TidalWall, FeatherStorm, CurseRing,
            HellfirePillars, AbyssGaze, WorldFall, DevouringMaw }
        private enum BossStage { Approach, Warning, Volley, Leaping, Recovery }
        private sealed class BossState
        {
            public BossPattern pattern;
            public BossStage stage;
            public int first, cycle, step, releases;
            public bool enraged, final;
            public float clock, nextStep, jumpTime, lastRelease = -1;
            public Vector2 jumpFrom, jumpTo;
            public Transform shadow;
        }
        private readonly Dictionary<Fighter, BossState> bossCombat = new Dictionary<Fighter, BossState>();
        internal const float BossEnrageThreshold = .5f, BossEnragedCooldown = .75f;

        private void ConfigureBoss(Fighter actor, int floor)
        {
            RemoveEnemyCombat(actor);
            int scaled = DifficultyFloor(floor);
            actor.kind = 4; actor.isBoss = true; actor.airborne = false; actor.difficultyFloor = floor;
            actor.damageScale = TowerDifficulty.DamageMultiplier(scaled);
            actor.moveScale = TowerDifficulty.MoveMultiplier(scaled);
            actor.bulletSpeedScale = TowerDifficulty.BulletMultiplier(scaled);
            actor.attackRateScale = TowerDifficulty.AttackRateMultiplier(scaled);
            actor.hp = actor.maxHp = TowerDifficulty.BossHealth(run != null && run.IsOathTrial ? TowerDifficulty.TrialBossFloor(floor) : floor);
            ApplyMonsterStats(actor);
            actor.root.transform.localScale = Vector3.one * (IsFinalBoss(actor) ? 2.5f : 1.8f);
            actor.windup = actor.attackPose = actor.hurt = actor.slowTime = actor.stunTime = actor.velocityY = 0;
            actor.cooldown = 1.5f / actor.attackRateScale; actor.grounded = true;
            actor.pos.y = world.GroundAt(actor.pos.x);
            actor.facing = hero.pos.x >= actor.pos.x ? 1 : -1;
            var state = NewBossState(actor, floor);
            bossCombat.Add(actor, state);
            actor.root.transform.position = actor.pos;
            UpdateBossShadow(actor, state);
        }

        private static BossState NewBossState(Fighter actor, int floor)
        {
            int first = Mathf.Max(0, floor / 5 - 1) % 3;
            return new BossState { first = first, pattern = (BossPattern)first, stage = BossStage.Approach,
                shadow = actor.root.transform.Find("Shadow"), final = IsFinalBoss(actor) };
        }
        internal static bool IsFinalBoss(Fighter actor) { return actor != null && actor.monster != null && actor.monster.Variant == TowerThemeCatalog.FinalVariant; }
        // The evil god answers with its own techniques; enraged it uses nothing else.
        private static readonly BossPattern[] FinalTechniques = { BossPattern.WorldFall, BossPattern.DevouringMaw, BossPattern.AbyssGaze };

        private static BossPattern Signature(TowerThemeId theme)
        {
            switch (theme)
            {
                case TowerThemeId.Chinese: return BossPattern.MagmaRain;
                case TowerThemeId.Dangun: return BossPattern.Rockfall;
                case TowerThemeId.Eden: return BossPattern.ThornSweep;
                case TowerThemeId.Asgard: return BossPattern.FrostBreath;
                case TowerThemeId.Veda: return BossPattern.TidalWall;
                case TowerThemeId.Olympus: return BossPattern.FeatherStorm;
                case TowerThemeId.Hell: return BossPattern.HellfirePillars;
                case TowerThemeId.Abyss: return BossPattern.AbyssGaze;
                default: return BossPattern.CurseRing;
            }
        }
        internal static string SignatureName(TowerThemeId theme) { return PatternName(Signature(theme)); }
        private static string PatternName(BossPattern pattern)
        {
            switch (pattern)
            {
                case BossPattern.MagmaRain: return "축융의 화산탄 비";
                case BossPattern.Rockfall: return "태백산 낙석";
                case BossPattern.ThornSweep: return "선악과 가시 덩굴";
                case BossPattern.FrostBreath: return "니드호그의 서리 숨결";
                case BossPattern.TidalWall: return "강가의 해일 장벽";
                case BossPattern.FeatherStorm: return "제우스의 깃털 폭풍";
                case BossPattern.HellfirePillars: return "몰록의 불기둥";
                case BossPattern.AbyssGaze: return "심연의 응시";
                case BossPattern.WorldFall: return "멸망한 지구의 파편";
                case BossPattern.DevouringMaw: return "모든 것을 삼키는 아가리";
                default: return "파라오의 저주 고리";
            }
        }
        private static BossPattern NextBossPattern(Fighter actor, BossState state)
        {
            int cycle = state.cycle++;
            if (state.final && (state.enraged || cycle % 2 == 1)) return FinalTechniques[(state.enraged ? cycle : cycle / 2) % FinalTechniques.Length];
            return cycle % 2 == 0 ? (BossPattern)((state.first + cycle / 2) % 3) : Signature(actor.theme);
        }
        private static bool IsSignature(BossPattern pattern) { return pattern >= BossPattern.MagmaRain; }
        private static int BossMotion(BossPattern pattern)
        {
            switch (pattern)
            {
                case BossPattern.CrownVolley: case BossPattern.MagmaRain: case BossPattern.ThornSweep: case BossPattern.FrostBreath: return 0;
                case BossPattern.LeapingSlam: case BossPattern.Rockfall: case BossPattern.HellfirePillars: return 2;
                case BossPattern.WorldFall: case BossPattern.DevouringMaw: return 0;
                default: return 1;
            }
        }
        private static float BossWindup(BossPattern pattern)
        {
            switch (pattern)
            {
                case BossPattern.CrownVolley: case BossPattern.ThornSweep: return 1.2f;
                case BossPattern.TwinSpiral: case BossPattern.Rockfall: case BossPattern.TidalWall: case BossPattern.CurseRing: return 1.3f;
                case BossPattern.LeapingSlam: case BossPattern.HellfirePillars: return 1.35f;
                case BossPattern.AbyssGaze: case BossPattern.WorldFall: return 1.3f;
                default: return 1.25f;
            }
        }
        private static int BossSteps(BossState state)
        {
            int extra = state.enraged ? 1 : 0;
            switch (state.pattern)
            {
                case BossPattern.CrownVolley: return 3 + extra;
                case BossPattern.TwinSpiral: return 8 + extra * 2;
                case BossPattern.ThornSweep: case BossPattern.FrostBreath: return 4 + extra * 2;
                case BossPattern.TidalWall: return 2 + extra;
                case BossPattern.WorldFall: return 4 + extra;
                case BossPattern.DevouringMaw: return 4 + extra * 2;
                default: return 3 + extra;
            }
        }
        private static float BossInterval(BossState state)
        {
            switch (state.pattern)
            {
                case BossPattern.CrownVolley: return .4f;
                case BossPattern.TwinSpiral: return .22f;
                case BossPattern.MagmaRain: return .55f;
                case BossPattern.Rockfall: return .42f;
                case BossPattern.ThornSweep: return state.enraged ? .6f : .7f;
                case BossPattern.FrostBreath: return .3f;
                case BossPattern.TidalWall: return state.enraged ? 1f : 1.1f;
                case BossPattern.FeatherStorm: return .5f;
                case BossPattern.HellfirePillars: return .7f;
                case BossPattern.AbyssGaze: return .55f;
                case BossPattern.WorldFall: return .5f;
                case BossPattern.DevouringMaw: return state.enraged ? .55f : .65f;
                default: return .45f;
            }
        }

        private void ResetBossCombat()
        {
            bossCombat.Clear();
        }

        private void RemoveBossCombat(Fighter actor)
        {
            if (actor != null) bossCombat.Remove(actor);
        }

        internal string BossPatternName(Fighter actor)
        { BossState state; return actor != null && bossCombat.TryGetValue(actor, out state) ? state.pattern.ToString() : "None"; }
        internal string BossStageName(Fighter actor)
        { BossState state; return actor != null && bossCombat.TryGetValue(actor, out state) ? state.stage.ToString() : "None"; }
        internal bool BossEnraged(Fighter actor)
        { BossState state; return actor != null && bossCombat.TryGetValue(actor, out state) && state.enraged; }
        internal float BossTelegraphRemaining(Fighter actor)
        { return actor == null ? 0 : Mathf.Max(0, actor.windup); }
        internal Vector2 BossLockedAim(Fighter actor)
        { return actor == null ? Vector2.zero : actor.strikePoint; }
        internal float BossLastReleaseAt(Fighter actor)
        { BossState state; return actor != null && bossCombat.TryGetValue(actor, out state) ? state.lastRelease : -1; }
        internal int BossReleaseCount(Fighter actor)
        { BossState state; return actor != null && bossCombat.TryGetValue(actor, out state) ? state.releases : 0; }

        private void TickBoss(Fighter actor, float dt)
        {
            if (actor.hp <= 0 || !actor.root) { RemoveBossCombat(actor); return; }
            BossState state;
            Vector2 previousPosition = actor.pos;
            // Preserve fixture health/cooldowns when registering an existing boss.
            if (!bossCombat.TryGetValue(actor, out state)) { state = NewBossState(actor, actor.difficultyFloor); bossCombat.Add(actor, state); }
            state.clock += dt;
            actor.hurt = Mathf.Max(0, actor.hurt - dt);
            actor.slowTime = Mathf.Max(0, actor.slowTime - dt);
            actor.stunTime = Mathf.Max(0, actor.stunTime - dt);
            actor.attackPose = Mathf.Max(0, actor.attackPose - dt);
            actor.cooldown = Mathf.Max(0, actor.cooldown - dt);
            bool moving = false;
            if (!state.enraged && actor.hp < actor.maxHp * BossEnrageThreshold)
            {
                // Second phase: faster recovery and one more wave for every technique.
                state.enraged = true;
                Notice((actor.title ?? (actor.monster == null ? "보스" : actor.monster.Name)) + " 격노" + (state.final ? "  ·  「루멘도 지구처럼 삼켜 주마」" : ""), state.final ? 3.5f : 2.5f);
                if (vfx != null) vfx.ImpactRing(actor.pos + Vector2.up * 1.4f, 2.2f, crimson);
                Shake(.22f, .4f);
            }

            if (actor.stunTime > 0 && state.stage != BossStage.Leaping)
            {
                actor.art.InterruptAction(); actor.attackPose = 0;
                actor.windup = 0; state.stage = BossStage.Recovery;
                actor.cooldown = Mathf.Max(actor.cooldown, .7f);
            }
            else if (state.stage == BossStage.Warning)
            {
                actor.windup = Mathf.Max(0, actor.windup - dt);
                if (actor.windup <= 0)
                {
                    if (!EnemyCanFire(actor)) EndBossPattern(actor, state);
                    else if (state.pattern == BossPattern.LeapingSlam) BeginBossLeap(actor, state);
                    else { state.stage = BossStage.Volley; state.step = 0; ReleaseBossVolley(actor, state); }
                }
            }
            else if (state.stage == BossStage.Volley)
            {
                state.nextStep -= dt;
                if (state.nextStep <= 0)
                {
                    if (EnemyCanFire(actor)) ReleaseBossVolley(actor, state);
                    else EndBossPattern(actor, state);
                }
            }
            else if (state.stage == BossStage.Leaping)
            {
                state.jumpTime += dt;
                float progress = Mathf.Clamp01(state.jumpTime / .85f);
                Vector2 next = Vector2.Lerp(state.jumpFrom, state.jumpTo, progress) + Vector2.up * Mathf.Sin(progress * Mathf.PI) * 3.6f;
                actor.velocityY = dt > 0 ? (next.y - actor.pos.y) / dt : 0;
                actor.pos = next; actor.grounded = false;
                if (progress >= 1) LandBossSlam(actor, state);
            }
            else if (state.stage == BossStage.Recovery)
            {
                if (actor.cooldown <= 0) state.stage = BossStage.Approach;
            }
            else
            {
                float dx = hero.pos.x - actor.pos.x;
                actor.facing = dx >= 0 ? 1 : -1;
                float speed = 1.25f * actor.moveScale * (actor.slowTime > 0 ? .5f : 1) * (state.enraged ? 1.2f : 1);
                if (Mathf.Abs(dx) > 6.6f)
                { actor.pos.x += Mathf.Sign(dx) * speed * dt; moving = true; }
                if (actor.cooldown <= 0 && EnemyCanFire(actor)) BeginBossPattern(actor, state);
            }

            if (state.stage != BossStage.Leaping)
            {
                actor.pos.y = world.GroundAt(actor.pos.x); actor.velocityY = 0; actor.grounded = true;
            }
            actor.pos.x = Mathf.Clamp(actor.pos.x, TowerWorld.Left + 1.6f, TowerWorld.Right - 1.6f);
            actor.root.transform.position = actor.pos;
            UpdateBossShadow(actor, state);
            float actualVelocityX = dt > 0 ? (actor.pos.x - previousPosition.x) / dt : 0;
            actor.art.SetLocomotion(actor.grounded, actor.velocityY, false, Mathf.Abs(actualVelocityX), actualVelocityX);
            actor.art.SetPose(moving, actor.windup > 0 || actor.attackPose > 0, actor.hurt > 0, actor.facing, dt);
        }

        private void BeginBossPattern(Fighter actor, BossState state)
        {
            state.pattern = NextBossPattern(actor, state);
            state.stage = BossStage.Warning; state.step = 0; state.nextStep = 0;
            actor.strikePoint = hero.pos;
            actor.facing = hero.pos.x >= actor.pos.x ? 1 : -1;
            float windup = BossWindup(state.pattern);
            actor.windup = windup;
            int motion = BossMotion(state.pattern);
            float releaseRatio = motion == 0 ? 1.2f / 2.25f : motion == 1 ? 1.3f / 3.2f : 2.2f / 2.75f;
            float motionDuration = state.pattern == BossPattern.CrownVolley ? 2.25f : state.pattern == BossPattern.TwinSpiral ? 3.2f :
                state.pattern == BossPattern.LeapingSlam ? 2.75f : windup / releaseRatio;
            float activeDuration = state.pattern == BossPattern.CrownVolley ? 2.0f : state.pattern == BossPattern.TwinSpiral ? 2.84f :
                state.pattern == BossPattern.LeapingSlam ? 2.55f : (BossSteps(state) - 1) * BossInterval(state) + .6f;
            actor.cooldown = (activeDuration + 1.6f / actor.attackRateScale) * (state.enraged ? BossEnragedCooldown : 1);
            actor.attackPose = motionDuration;
            actor.art.PlayBossAttack(motion, motionDuration);
            if (IsSignature(state.pattern)) FloatText(actor.pos + Vector2.up * (state.final ? 4.6f : 3.6f), PatternName(state.pattern), brass);
        }

        private void ReleaseBossVolley(Fighter actor, BossState state)
        {
            if (state.pattern == BossPattern.LeapingSlam)
            {
                // A second, separately timed pair of ground waves follows the landing.
                BossGroundWaves(actor, state, 1.05f);
                state.lastRelease = state.clock; state.releases++;
                EndBossPattern(actor, state); return;
            }
            Vector2 origin = actor.pos + Vector2.up * 1.4f;
            Vector2 direction = (actor.strikePoint + Vector2.up - origin).normalized;
            if (direction.sqrMagnitude < .1f) direction = Vector2.right * actor.facing;
            state.lastRelease = state.clock; state.releases++;
            if (state.pattern == BossPattern.CrownVolley)
            {
                float sweep = (state.step - 1) * 6;
                foreach (float angle in new[] { -32f, -16f, 0f, 16f, 32f })
                    BossBolt(actor, origin, direction, angle + sweep, 3.7f, 10, new Color(1, .7f, .22f), .19f);
            }
            else if (state.pattern == BossPattern.TwinSpiral)
            {
                // Two opposite spiral arms rotate in fixed increments. The 105-degree
                // corridor pointing at the original aim stays open throughout the sequence.
                for (int arm = 0; arm < 2; arm++)
                    foreach (float spread in new[] { -5f, 5f })
                    {
                        float angle = 90 + state.step * 22.5f + arm * 180 + spread;
                        if (Mathf.Abs(Mathf.DeltaAngle(0, angle)) < 52.5f) continue;
                        BossBolt(actor, origin, direction, angle, 3.15f, 9, new Color(.73f, .5f, 1), .17f);
                    }
            }
            else ReleaseSignature(actor, state, origin, direction);
            state.step++; state.nextStep += BossInterval(state);
            if (state.step >= BossSteps(state)) EndBossPattern(actor, state);
            Sound(chimeSound, .18f);
        }

        private void ReleaseSignature(Fighter actor, BossState state, Vector2 origin, Vector2 direction)
        {
            Vector2 aim = actor.strikePoint; float ground = world.GroundAt(actor.pos.x); int step = state.step;
            Color color = new Color(1, .55f, .3f);
            switch (state.pattern)
            {
                case BossPattern.MagmaRain:
                {
                    // Eleven falling columns with a two-column gap that moves every wave.
                    int gap = (state.cycle * 5 + step * 3) % 9 + 1;
                    for (int k = 0; k <= 10; k++)
                    {
                        if (k == gap || k == gap + 1) continue;
                        BossBolt(actor, new Vector2(aim.x + (k - 5) * 1.6f, 8.8f), Vector2.down, 0, 6f, 10, color, .2f);
                    }
                    break;
                }
                case BossPattern.Rockfall:
                {
                    float[] lanes = step % 3 == 0 ? new[] { 0f } : step % 3 == 1 ? new[] { -2.6f, 2.6f } : new[] { -5.2f, 0f, 5.2f };
                    foreach (float lane in lanes)
                        foreach (float spread in new[] { -.32f, 0f, .32f })
                            BossBolt(actor, new Vector2(aim.x + lane + spread, 8.6f + Mathf.Abs(spread) * 1.5f), Vector2.down, 0, 7.2f, 12, color, .22f);
                    if (vfx != null) vfx.Dust(actor.pos, 0, 1.4f);
                    Shake(.12f, .2f);
                    break;
                }
                case BossPattern.ThornSweep:
                {
                    // Low vines must be jumped; high vines catch anyone standing on the first ledge.
                    float height = step % 2 == 0 ? .45f : 2.05f;
                    foreach (int side in new[] { -1, 1 })
                        foreach (float lift in new[] { 0f, .32f })
                            BossBolt(actor, new Vector2(actor.pos.x + side * 1.3f, ground + height + lift), Vector2.right * side, 0, 5.2f, 10, color, .2f);
                    break;
                }
                case BossPattern.FrostBreath:
                {
                    float shift = step % 2 == 0 ? 0 : 6;
                    foreach (float angle in new[] { -30f, -18f, -6f, 6f, 18f, 30f })
                        BossBolt(actor, origin, direction, angle + shift, 3f, 9, color, .19f);
                    break;
                }
                case BossPattern.TidalWall:
                {
                    // A rolling wall with one opening at ground or first-ledge height.
                    int side = aim.x >= actor.pos.x ? 1 : -1;
                    int gap = (state.cycle + step) % 2 == 0 ? 0 : 2;
                    for (int k = 0; k < 10; k++)
                    {
                        if (k == gap || k == gap + 1) continue;
                        BossBolt(actor, new Vector2(actor.pos.x + side * 1.4f, ground + .3f + k * .8f), Vector2.right * side, 0, 4.2f, 11, color, .2f);
                    }
                    break;
                }
                case BossPattern.FeatherStorm:
                {
                    float drift = step % 2 == 0 ? .45f : -.45f;
                    Vector2 fall = new Vector2(drift, -1).normalized;
                    for (int k = -3; k <= 3; k++)
                        BossBolt(actor, new Vector2(aim.x + k * 3f + step - drift * 6, 8.8f), fall, 0, 5.5f, 9, color, .18f);
                    break;
                }
                case BossPattern.HellfirePillars:
                {
                    // Fire bursts up through the floor in lanes around the locked aim; a two-lane gap moves every wave.
                    int gap = (state.cycle * 3 + step * 4) % 8 + 1;
                    for (int k = 0; k <= 9; k++)
                    {
                        if (k == gap || k == gap + 1) continue;
                        float x = aim.x + (k - 4.5f) * 1.5f;
                        BossBolt(actor, new Vector2(x, world.GroundAt(x) - 1.1f), Vector2.up, 0, 4.6f, 11, color, .22f);
                    }
                    if (vfx != null) vfx.Dust(actor.pos, 0, 1.2f);
                    Shake(.1f, .18f);
                    break;
                }
                case BossPattern.AbyssGaze:
                {
                    // The eye above weeps rings of blood with three gaps that turn every wave.
                    Vector2 eye = actor.pos + Vector2.up * (state.final ? 5.2f : 3.4f);
                    float rotation = step * 9f + state.cycle * 20f;
                    for (int i = 0; i < 24; i++)
                    {
                        if (i % 8 < 2) continue;
                        BossBolt(actor, eye, Vector2.right, i * 15f + rotation, 3.1f, 10, color, .19f);
                    }
                    break;
                }
                case BossPattern.WorldFall:
                {
                    // Burning shards of the devoured Earth fall in slanted rows with a wide and a narrow gap.
                    int wide = (state.cycle * 5 + step * 3) % 10, narrow = (wide + 6) % 12;
                    Vector2 fall = new Vector2(step % 2 == 0 ? .14f : -.14f, -1).normalized;
                    for (int k = 0; k <= 11; k++)
                    {
                        if (k == wide || k == wide + 1 || k == narrow) continue;
                        BossBolt(actor, new Vector2(aim.x + (k - 5.5f) * 1.5f, 9.2f + (k % 2) * .8f), fall, 0, 5.4f, 12, color, .26f);
                    }
                    Shake(.08f, .15f);
                    break;
                }
                case BossPattern.DevouringMaw:
                {
                    // Jaws of shadow close in from both edges of the arena at ground or ledge height.
                    float height = step % 2 == 0 ? .45f : 2.05f;
                    foreach (int side in new[] { -1, 1 })
                    {
                        float x = Mathf.Clamp(actor.pos.x + side * 13f, TowerWorld.Left + .5f, TowerWorld.Right - .5f), floor = world.GroundAt(x);
                        foreach (float lift in new[] { 0f, .34f })
                            BossBolt(actor, new Vector2(x, floor + height + lift), Vector2.right * -side, 0, 5.6f, 11, color, .21f);
                    }
                    break;
                }
                default:
                {
                    // An expanding ring with three gaps that turn ten degrees per wave.
                    float rotation = step * 10f;
                    for (int i = 0; i < 18; i++)
                    {
                        if (i % 6 < 2) continue;
                        BossBolt(actor, origin, Vector2.right, i * 20f + rotation, 3.4f, 10, color, .19f);
                    }
                    break;
                }
            }
        }

        private void BeginBossLeap(Fighter actor, BossState state)
        {
            state.stage = BossStage.Leaping; state.jumpTime = 0;
            state.jumpFrom = actor.pos;
            float landingX = Mathf.Clamp(actor.strikePoint.x, TowerWorld.Left + 1.6f, TowerWorld.Right - 1.6f);
            state.jumpTo = new Vector2(landingX, world.GroundAt(landingX));
            actor.grounded = false;
        }

        private void LandBossSlam(Fighter actor, BossState state)
        {
            actor.pos = state.jumpTo; actor.velocityY = 0; actor.grounded = true;
            state.lastRelease = state.clock; state.releases++;
            if (EnemyCanFire(actor))
            {
                if (Vector2.Distance(hero.pos, actor.pos) < 3.1f && Mathf.Abs(hero.pos.y - actor.pos.y) < 1.8f)
                    HurtHero(14 * actor.damageScale);
                BossGroundWaves(actor, state, .65f);
            }
            if (vfx != null) { vfx.Impact(SkillEffect.Shockwave, actor.pos + Vector2.up * .5f, 3.1f, new Color(1, .5f, .23f)); vfx.Dust(actor.pos, 0, 2); }
            Shake(.28f, .3f);
            Sound(hitSound, .7f);
            state.stage = BossStage.Volley; state.nextStep = .35f;
        }

        private void BossGroundWaves(Fighter actor, BossState state, float height)
        {
            Vector2 origin = actor.pos + Vector2.up * height;
            foreach (int side in new[] { -1, 1 })
                BossBolt(actor, origin + Vector2.right * side * 1.2f, Vector2.right * side, 0, 4.1f, 11, new Color(1, .43f, .25f), .22f);
        }

        private void BossBolt(Fighter actor, Vector2 origin, Vector2 direction, float angle, float speed, float damage, Color color, float radius)
        {
            Vector2 ray = Quaternion.Euler(0, 0, angle) * direction;
            Fire(origin, origin + ray * 12, speed * actor.bulletSpeedScale, damage * actor.damageScale, true, ThemedBoltColor(actor, color));
            StyleHostileLastShot(radius);
        }

        private static void EndBossPattern(Fighter actor, BossState state)
        {
            actor.windup = 0; state.stage = BossStage.Recovery;
            actor.cooldown = Mathf.Max(.45f, actor.cooldown);
        }

        private void UpdateBossShadow(Fighter actor, BossState state)
        {
            if (!state.shadow) return;
            float floorHeight = world.GroundAt(actor.pos.x);
            state.shadow.position = new Vector3(actor.pos.x, floorHeight + .025f, 0);
            Vector3 parentScale = actor.root.transform.lossyScale;
            state.shadow.localScale = new Vector3(3.1f / Mathf.Max(.01f, Mathf.Abs(parentScale.x)), .2f / Mathf.Max(.01f, Mathf.Abs(parentScale.y)), 1);
            var renderer = state.shadow.GetComponent<SpriteRenderer>();
            if (renderer) renderer.color = new Color(0, 0, 0, Mathf.Clamp(.34f - (actor.pos.y - floorHeight) * .035f, .16f, .34f));
        }
    }
}

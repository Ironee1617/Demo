using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    public sealed partial class TowerGame
    {
        private enum BossPattern { CrownVolley, TwinSpiral, LeapingSlam }
        private enum BossStage { Approach, Warning, Volley, Leaping, Recovery }
        private sealed class BossState
        {
            public BossPattern pattern;
            public BossStage stage;
            public int cycle, step, releases;
            public float clock, nextStep, jumpTime, warningAngle, lastRelease = -1;
            public Vector2 jumpFrom, jumpTo;
            public Transform shadow;
            public readonly EnemyState warning = new EnemyState();
        }
        private readonly Dictionary<Fighter, BossState> bossCombat = new Dictionary<Fighter, BossState>();

        private void ConfigureBoss(Fighter actor, int floor)
        {
            RemoveEnemyCombat(actor);
            actor.kind = 4; actor.isBoss = true; actor.airborne = false; actor.difficultyFloor = floor;
            actor.damageScale = TowerDifficulty.DamageMultiplier(floor);
            actor.moveScale = TowerDifficulty.MoveMultiplier(floor);
            actor.bulletSpeedScale = TowerDifficulty.BulletMultiplier(floor);
            actor.attackRateScale = TowerDifficulty.AttackRateMultiplier(floor);
            actor.hp = actor.maxHp = TowerDifficulty.BossHealth(floor);
            actor.root.transform.localScale = Vector3.one * 1.8f;
            actor.windup = actor.attackPose = actor.hurt = actor.slowTime = actor.stunTime = actor.velocityY = 0;
            actor.cooldown = 1.5f / actor.attackRateScale; actor.grounded = true;
            actor.pos.y = TowerWorld.Ground;
            actor.facing = hero.pos.x >= actor.pos.x ? 1 : -1;
            var state = NewBossState(actor, floor);
            bossCombat.Add(actor, state);
            actor.root.transform.position = actor.pos;
            UpdateBossShadow(actor, state);
        }

        private static BossState NewBossState(Fighter actor, int floor)
        {
            int first = Mathf.Max(0, floor / 3 - 1) % 3;
            return new BossState { cycle = first, pattern = (BossPattern)first, stage = BossStage.Approach,
                shadow = actor.root.transform.Find("Shadow") };
        }

        private void ResetBossCombat()
        {
            foreach (var state in bossCombat.Values) ClearEnemyTelegraph(state.warning);
            bossCombat.Clear();
        }

        private void RemoveBossCombat(Fighter actor)
        {
            BossState state;
            if (actor == null || !bossCombat.TryGetValue(actor, out state)) return;
            ClearEnemyTelegraph(state.warning); bossCombat.Remove(actor);
        }

        internal string BossPatternName(Fighter actor)
        { BossState state; return actor != null && bossCombat.TryGetValue(actor, out state) ? state.pattern.ToString() : "None"; }
        internal string BossStageName(Fighter actor)
        { BossState state; return actor != null && bossCombat.TryGetValue(actor, out state) ? state.stage.ToString() : "None"; }
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
            // Preserve fixture health/cooldowns when registering an existing boss.
            if (!bossCombat.TryGetValue(actor, out state)) { state = NewBossState(actor, actor.difficultyFloor); bossCombat.Add(actor, state); }
            state.clock += dt;
            actor.hurt = Mathf.Max(0, actor.hurt - dt);
            actor.slowTime = Mathf.Max(0, actor.slowTime - dt);
            actor.stunTime = Mathf.Max(0, actor.stunTime - dt);
            actor.attackPose = Mathf.Max(0, actor.attackPose - dt);
            actor.cooldown = Mathf.Max(0, actor.cooldown - dt);
            bool moving = false;

            if (actor.stunTime > 0 && state.stage != BossStage.Leaping)
            {
                actor.art.InterruptAction(); actor.attackPose = 0;
                actor.windup = 0; state.stage = BossStage.Recovery;
                actor.cooldown = Mathf.Max(actor.cooldown, .7f); ClearEnemyTelegraph(state.warning);
            }
            else if (state.stage == BossStage.Warning)
            {
                actor.windup = Mathf.Max(0, actor.windup - dt);
                state.warning.clock = state.clock;
                AnimateEnemyTelegraph(state.warning, actor.windup);
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
                float speed = 1.25f * actor.moveScale * (actor.slowTime > 0 ? .5f : 1);
                if (Mathf.Abs(dx) > 6.6f)
                { actor.pos.x += Mathf.Sign(dx) * speed * dt; moving = true; }
                if (actor.cooldown <= 0 && EnemyCanFire(actor)) BeginBossPattern(actor, state);
            }

            if (state.stage != BossStage.Leaping)
            {
                actor.pos.y = TowerWorld.Ground; actor.velocityY = 0; actor.grounded = true;
            }
            actor.pos.x = Mathf.Clamp(actor.pos.x, TowerWorld.Left + 1.6f, TowerWorld.Right - 1.6f);
            actor.root.transform.position = actor.pos;
            UpdateBossWarningPose(actor, state);
            UpdateBossShadow(actor, state);
            actor.art.SetLocomotion(actor.grounded, actor.velocityY, false, moving ? 1.25f * actor.moveScale : 0);
            actor.art.SetPose(moving, actor.windup > 0 || actor.attackPose > 0, actor.hurt > 0, actor.facing, dt);
        }

        private void BeginBossPattern(Fighter actor, BossState state)
        {
            state.pattern = (BossPattern)(state.cycle++ % 3);
            state.stage = BossStage.Warning; state.step = 0; state.nextStep = 0;
            actor.strikePoint = hero.pos;
            actor.facing = hero.pos.x >= actor.pos.x ? 1 : -1;
            float windup = state.pattern == BossPattern.CrownVolley ? 1.2f : state.pattern == BossPattern.TwinSpiral ? 1.3f : 1.35f;
            actor.windup = windup;
            float motionDuration = state.pattern == BossPattern.CrownVolley ? 2.25f : state.pattern == BossPattern.TwinSpiral ? 3.2f : 2.75f;
            float activeDuration = state.pattern == BossPattern.CrownVolley ? 2.0f : state.pattern == BossPattern.TwinSpiral ? 2.84f : 2.55f;
            actor.cooldown = activeDuration + 1.6f / actor.attackRateScale;
            actor.attackPose = motionDuration;
            actor.art.PlayBossAttack((int)state.pattern, motionDuration);
            BuildBossWarning(actor, state);
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
                state.step++; state.nextStep += .4f;
                if (state.step >= 3) EndBossPattern(actor, state);
            }
            else
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
                state.step++; state.nextStep += .22f;
                if (state.step >= 8) EndBossPattern(actor, state);
            }
            Sound(chimeSound, .18f);
        }

        private void BeginBossLeap(Fighter actor, BossState state)
        {
            state.stage = BossStage.Leaping; state.jumpTime = 0;
            state.jumpFrom = actor.pos;
            state.jumpTo = new Vector2(Mathf.Clamp(actor.strikePoint.x, TowerWorld.Left + 1.6f, TowerWorld.Right - 1.6f), TowerWorld.Ground);
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
            if (vfx != null) vfx.Impact(SkillEffect.Shockwave, actor.pos + Vector2.up * .5f, 3.1f, new Color(1, .5f, .23f));
            Sound(hitSound, .7f);
            ClearEnemyTelegraph(state.warning);
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
            Fire(origin, origin + ray * 12, speed * actor.bulletSpeedScale, damage * actor.damageScale, true, color);
            StyleHostileLastShot(radius);
        }

        private static void EndBossPattern(Fighter actor, BossState state)
        {
            actor.windup = 0; state.stage = BossStage.Recovery;
            actor.cooldown = Mathf.Max(.45f, actor.cooldown);
            ClearEnemyTelegraph(state.warning);
        }

        private void BuildBossWarning(Fighter actor, BossState state)
        {
            ClearEnemyTelegraph(state.warning);
            state.warning.telegraph = new GameObject("Boss " + state.pattern + " warning");
            state.warning.telegraph.transform.SetParent(transform, false);
            Vector2 origin = actor.pos + Vector2.up * 1.4f;
            Vector2 direction = (actor.strikePoint + Vector2.up - origin).normalized;
            if (direction.sqrMagnitude < .1f) direction = Vector2.right * actor.facing;
            state.warningAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (state.pattern != BossPattern.LeapingSlam) state.warning.telegraph.transform.position = origin;
            if (state.pattern == BossPattern.CrownVolley)
            {
                foreach (float angle in new[] { -32f, -16f, 0f, 16f, 32f })
                    GuideEnemyRay(state.warning, origin, direction, angle, 8.5f, new Color(1, .68f, .22f, .45f));
                foreach (float angle in new[] { -38f, 38f })
                    GuideEnemyRay(state.warning, origin, direction, angle, 5.2f, new Color(1, .4f, .2f, .4f));
            }
            else if (state.pattern == BossPattern.TwinSpiral)
            {
                float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * 22.5f;
                    if (Mathf.Abs(Mathf.DeltaAngle(0, angle)) < 52.5f) continue;
                    Vector2 point = origin + (Vector2)(Quaternion.Euler(0, 0, angle) * direction) * 2.5f;
                    AddEnemyGuide(state.warning, point, new Vector2(.36f, .055f), baseAngle + angle + 90, new Color(.8f, .53f, 1, .5f));
                }
                GuideEnemyRay(state.warning, origin + direction, direction, 0, 4.5f, new Color(.42f, .95f, .83f, .4f));
            }
            else
            {
                Vector2 landing = new Vector2(Mathf.Clamp(actor.strikePoint.x, TowerWorld.Left + 1.6f, TowerWorld.Right - 1.6f), TowerWorld.Ground + .06f);
                AddEnemyGuide(state.warning, landing, new Vector2(6.2f, .09f), 0, new Color(1, .37f, .2f, .55f));
                for (int side = -1; side <= 1; side += 2)
                {
                    AddEnemyGuide(state.warning, landing + Vector2.right * side * 3.1f + Vector2.up * .23f, new Vector2(.08f, .5f), 0, new Color(1, .7f, .24f, .7f));
                    AddEnemyGuide(state.warning, landing + Vector2.right * side * 1.2f + Vector2.up * .12f, new Vector2(.4f, .07f), side * 25, new Color(1, .7f, .24f, .7f));
                }
            }
        }

        private static void UpdateBossWarningPose(Fighter actor, BossState state)
        {
            // Projectile warnings follow a pushed emitter while retaining the locked aim.
            // Slam markers stay at their fixed landing point.
            if (state.pattern == BossPattern.LeapingSlam || !state.warning.telegraph) return;
            Vector2 origin = actor.pos + Vector2.up * 1.4f;
            Vector2 direction = (actor.strikePoint + Vector2.up - origin).normalized;
            if (direction.sqrMagnitude < .1f) direction = Vector2.right * actor.facing;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            state.warning.telegraph.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, 0, angle - state.warningAngle));
        }

        private static void UpdateBossShadow(Fighter actor, BossState state)
        {
            if (!state.shadow) return;
            state.shadow.position = new Vector3(actor.pos.x, TowerWorld.Ground + .025f, 0);
            Vector3 parentScale = actor.root.transform.lossyScale;
            state.shadow.localScale = new Vector3(3.1f / Mathf.Max(.01f, Mathf.Abs(parentScale.x)), .2f / Mathf.Max(.01f, Mathf.Abs(parentScale.y)), 1);
            var renderer = state.shadow.GetComponent<SpriteRenderer>();
            if (renderer) renderer.color = new Color(0, 0, 0, Mathf.Clamp(.34f - (actor.pos.y - TowerWorld.Ground) * .035f, .16f, .34f));
        }
    }
}

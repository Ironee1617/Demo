using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    // R47: per-kind traits and abilities (aliens F, divers R), the diver's fish drone, scores and endings,
    // and the AI that plays the drains.
    public sealed partial class AquariumModel
    {
        public const float HideSeconds = 20f;
        public const float AlienAbilityCooldown = 60f, InkBombSeconds = 5f, InkBombRadius = 7f;
        public const float InvisibleSeconds = 5f, InvisibleFadeSeconds = .7f;
        public const float ShockRadius = 6f, ShockStun = 4f;
        public const float DashDistance = 6f, DashSeconds = .35f, DashCooldown = 30f;
        public const float BlueStillSeconds = 3f;
        public const float DiverAbilityDelay = 180f, XraySeconds = 5f, XrayCooldown = 60f;
        // R49: the harpoon only counts a hit that goes properly through the body (core of the old hit volume),
        // and the body is then skewered on the shaft. Shared with the aiming camera through FishHitTime / PlayerHitTime.
        public const float HitCore = .8f;
        public const int CorpseScore = 10;
        public static bool StarvedCorpse(Swimmer p) { return p != null && !p.alive && !p.escaped && !p.collected && p.deathReason == "starved"; }
        private void Skewer(Harpoon h, Point centre, float radius)
        {
            var axis = h.direction.Length > .01f ? h.direction.Normalized : new Point(0, 0, 1);
            // Push the tip on through the body so it pokes out the far side, and keep the body where it was hit.
            float along = Point.Dot(centre - h.position, axis);
            h.position = h.position + axis * Math.Max(0, along + radius * .95f);
            h.catchDepth = radius * .95f + Math.Max(0, Point.Dot(h.position - centre, axis) - radius * .95f);
            h.catchOffset = axis * -h.catchDepth;
        }
        public const int XrayUses = 3, GlueUses = 1, FeedsPerGame = 3;
        public const float DroneSpeed = 3.4f, DroneWanderSpeed = 1.4f, DroneLeash = 4f;
        // Scores (R47 table).
        public const int ScrewScore = 5, LastEscapeBonus = 20, FishScore = 5, AllyEatScore = -10, EggLayScore = 10, EggBreakScore = 10;
        public const int CatchScore = 30, CaughtScore = -20, MisfireScore = -5, TeamWinScore = 40, WipedOutBonus = 20, LoseScore = -20;

        public static bool IsKind(Swimmer p, int kind) { return p != null && p.team == Team.Hider && p.alienKind == kind; }
        public static bool IsDiver(Swimmer p, int kind) { return p != null && p.team == Team.Seeker && p.diverKind == kind; }

        public static float HungerMax(Swimmer p) { return IsKind(p, 1) ? 120f : IsKind(p, 2) ? 80f : HungerSeconds; }
        public static float StaminaMax(Swimmer p) { return IsKind(p, 1) ? 120f : IsDiver(p, 1) ? 80f : MaxStamina; }
        public static int HitsToKill(Swimmer p) { return IsKind(p, 1) ? 3 : 2; }
        public static float MealRecoveryOf(Swimmer p) { return MealRecovery + (IsKind(p, 1) ? 10f : 0f); }
        public static int EggFoodFor(Swimmer p) { return IsKind(p, 0) ? 8 : EggFoodRequired; }
        public static float HatchSecondsFor(Swimmer p) { return IsKind(p, 0) ? 40f : HatchSeconds; }
        public static float StunScale(Swimmer p) { return IsKind(p, 4) ? .7f : 1f; }
        public static float LidSecondsFor(Swimmer p) { return IsKind(p, 4) ? 6f : LidSeconds; }
        public static float SprintBoost(Swimmer p) { return IsDiver(p, 1) ? .76f : .6f; }
        public static float HarpoonRangeOf(Swimmer p) { return p != null && p.team == Team.Seeker ? AquariumDiverKinds.Get(p.diverKind).harpoonRange : HarpoonRange; }
        public static float HarpoonSpeedOf(Swimmer p) { return HarpoonSpeed * (p != null && p.team == Team.Seeker ? AquariumDiverKinds.Get(p.diverKind).harpoonSpeedScale : 1f); }
        public static bool HasAlienAbility(Swimmer p) { return p != null && p.team == Team.Hider && p.alienKind != 1; }
        public static float AlienAbilityCooldownOf(Swimmer p) { return IsKind(p, 4) ? DashCooldown : AlienAbilityCooldown; }
        public static float DiverAbilityAt(float huntAt) { return huntAt + DiverAbilityDelay; }

        // How hidden a blue alien is right now (0 visible .. 1 gone): it fades out and back in.
        public static float Invisibility(Swimmer p, float time)
        {
            if (!IsKind(p, 2) || p.abilityUntil <= 0) return 0;
            float start = p.abilityUntil - InvisibleSeconds;
            if (time < start || time > p.abilityUntil) return 0;
            float a = Math.Min(1, (time - start) / InvisibleFadeSeconds), b = Math.Min(1, (p.abilityUntil - time) / InvisibleFadeSeconds);
            float t = Math.Min(a, b); return t * t * (3 - 2 * t);
        }
        public static bool Hidden(Swimmer p, float time) { return Invisibility(p, time) > .5f; }
        public static bool XrayOn(Swimmer p, float time) { return IsDiver(p, 1) && time < p.abilityUntil; }

        // Screw speed: two or more aliens on one drain each work 10% faster; the yellow alien adds 10% on top.
        public float ScrewRate(Swimmer p, int drain)
        {
            bool coop = false;
            foreach (var o in Players) if (o != p && o.alive && o.drainTask == DrainTask.Screw && o.drainIndex == drain) { coop = true; break; }
            return 1f + (coop ? .1f : 0f) + (IsKind(p, 3) ? .1f : 0f);
        }
        private bool FeedsAll(WaterEffect e) { return e.owner != 0 && IsDiver(Player(e.owner), 0); }

        public void SetDiver(int id, int kind)
        {
            var p = Player(id);
            if (p == null || Phase != RoundPhase.Lobby) return;
            p.diverKind = AquariumDiverKinds.Clamp(kind);
        }

        private void ResetAbilities(Swimmer p)
        {
            p.nextAbility = p.abilityUntil = p.dashUntil = p.stillFor = 0;
            p.feedsLeft = FeedsPerGame;
            p.abilityUses = p.team == Team.Seeker ? (p.diverKind == 0 ? GlueUses : p.diverKind == 1 ? XrayUses : 0) : 0;
            p.droneOut = p.droneControl = false; p.dronePosition = p.position; p.droneFacing = new Point(0, 0, 1); p.botGoal = -1;
        }

        // The closed, unglued drain lid in front of the diver within reach (shared with the HUD prompt), or -1.
        public static int GlueTarget(Swimmer p, AquariumDrainPlacement[] all)
        {
            int best = -1; float bestScore = float.MaxValue;
            if (p == null || all == null) return -1;
            var face = p.facing.Length > .01f ? p.facing.Normalized : new Point(0, 0, 1);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].glued || all[i].lid > 0) continue;
                var d = AquariumDrainLayout.LidCenter(all[i]) - p.position; float dist = d.Length;
                if (dist > CoverReach + .5f) continue;
                float along = dist < .1f ? 1 : Point.Dot(d / dist, face);
                if (along < .2f) continue;
                float score = dist - along; if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        private void UseAbility(Swimmer p, SwimInput input)
        {
            if (Time < HuntAt || Time < p.nextAbility || !p.alive || p.swallowedBy != 0) return;
            if (p.team == Team.Hider)
            {
                if (!HasAlienAbility(p)) return;
                switch (p.alienKind)
                {
                    case 0: Effect("inkBomb", p.position, p.facing, InkBombSeconds, p.id); break;
                    case 2: p.abilityUntil = Time + InvisibleSeconds; Effect("vanish", p.position, p.facing, InvisibleSeconds, p.id); break;
                    case 3: Shock(p); break;
                    case 4: CancelDrainTask(p, true); p.dashUntil = Time + DashSeconds; Effect("dash", p.position, p.facing, .8f, p.id); break;
                    default: return;
                }
                p.nextAbility = Time + AlienAbilityCooldownOf(p);
                return;
            }
            if (Time < DiverAbilityAt(HuntAt)) return;
            switch (p.diverKind)
            {
                case 0:
                {
                    if (p.abilityUses <= 0) return;
                    int i = GlueTarget(p, drains);
                    if (i < 0) return;
                    drains[i].glued = true; p.abilityUses--;
                    foreach (var o in Players) if (o.team == Team.Hider && o.drainIndex == i && o.drainTask != DrainTask.Enter) ClearDrainTask(o);
                    Effect("glue", AquariumDrainLayout.LidCenter(drains[i]), new Point(0, 1, 0), 2f, p.id);
                    p.nextAbility = Time + 1f;
                    break;
                }
                case 1:
                    // R49: no use limit, one minute cooldown from each use.
                    if (Time < p.abilityUntil) return;
                    p.abilityUntil = Time + XraySeconds;
                    Effect("xray", p.position, p.facing, XraySeconds, p.id);
                    p.nextAbility = Time + XrayCooldown;
                    break;
                case 2:
                    if (!p.droneOut)
                    {
                        p.droneOut = true; p.droneControl = true;
                        p.dronePosition = Constrain(p.position + p.facing * 1.2f, .35f);
                        p.droneFacing = p.facing.Length > .01f ? p.facing.Normalized : new Point(0, 0, 1);
                        Effect("droneLaunch", p.dronePosition, p.droneFacing, .6f, p.id);
                    }
                    else p.droneControl = !p.droneControl;
                    if (p.droneControl) { CancelDrainTask(p, true); p.velocity = new Point(); }
                    else { p.droneHome = p.dronePosition; p.droneTurnAt = 0; }
                    p.nextAbility = Time + .4f;
                    break;
            }
        }

        // Yellow alien: a sphere of electricity stuns fish and divers nearby (never aliens).
        private void Shock(Swimmer p)
        {
            foreach (var o in Players) if (o.team == Team.Seeker && o.alive && (o.position - p.position).Length < ShockRadius)
            { o.stunnedUntil = Math.Max(o.stunnedUntil, Time + ShockStun); o.velocity = new Point(); CancelDrainTask(o, false); o.visualAction = "Shocked"; o.visualAt = Time; }
            foreach (var f in Fish) if (Swimming(f) && (f.position - p.position).Length < ShockRadius) f.stunnedUntil = Time + ShockStun;
            Effect("shock", p.position, p.facing, 1.2f, p.id);
        }

        private void DriveDrone(Swimmer p, Point move, Point aim, float dt)
        {
            if (aim.Length > .1f) p.droneFacing = aim.Normalized;
            if (move.Length > 1) move = move.Normalized;
            p.dronePosition = Constrain(p.dronePosition + move * (DroneSpeed * dt), .3f);
        }

        // A drone left alone wanders slowly around where it was let go.
        private void TickDrones(float dt)
        {
            foreach (var p in Players)
            {
                if (p.team != Team.Seeker || !p.droneOut) continue;
                if (!p.alive) { p.droneControl = false; continue; }
                if (p.droneControl) continue;
                if (Time >= p.droneTurnAt)
                {
                    var goal = p.droneHome + new Point(Range(-1, 1), Range(-.3f, .3f), Range(-1, 1)) * DroneLeash;
                    p.droneSteer = goal - p.dronePosition; p.droneTurnAt = Time + Range(2f, 5f);
                }
                var to = p.droneSteer.Length > .2f ? p.droneSteer.Normalized : p.droneFacing;
                p.droneFacing = Point.TurnTowards(p.droneFacing, to, 2f * dt);
                var next = Constrain(p.dronePosition + p.droneFacing * (DroneWanderSpeed * dt), .3f);
                p.droneSteer = p.droneSteer - (next - p.dronePosition);
                p.dronePosition = next;
            }
        }

        // Blue alien: holding still for 3 s stops the hunger drain.
        private void TickStill(Swimmer p, Point move, float dt)
        {
            bool still = move.Length < .05f && p.velocity.Length < .25f;
            p.stillFor = still ? p.stillFor + dt : 0;
            if (IsKind(p, 2) && p.stillFor >= BlueStillSeconds) p.lastMealAt = Math.Min(Time, p.lastMealAt + dt);
        }

        // ---- Endings -------------------------------------------------------------------------------------
        private void CheckWinner()
        {
            if (Phase != RoundPhase.Playing) return;
            // Finish the last retrieval before freezing the round, including the four-second death view.
            if (Harpoons.Exists(h => h.stowedAt > Time)) return;
            bool inside = Players.Exists(p => p.team == Team.Hider && p.alive);
            if (!inside && Players.Exists(p => p.team == Team.Hider && Time < p.diedAt + DeathReviewSeconds)) return;
            // Nobody left in the tank (eggs still waiting keep the hunt going; at the time limit they no longer count).
            if (!inside && (Eggs.Count == 0 || Time >= EndsAt))
            {
                bool escaped = Players.Exists(p => p.team == Team.Hider && p.escaped);
                bool caught = Players.Exists(p => p.team == Team.Hider && !p.escaped);
                End(!escaped ? WinReason.AllAliensCaught : caught ? WinReason.HuntOver : WinReason.AllAliensEscaped);
            }
            else if (Fish.TrueForAll(f => f.eaten || f.dead)) End(WinReason.FoodGoalReached);
            else if (!Players.Exists(p => p.team == Team.Seeker)) End(WinReason.DiversLeft);
            else if (Time >= EndsAt) End(WinReason.TimeExpired);
        }

        // Endings: WIPED OUT (all caught) / HUNT OVER (some escaped, the rest caught) / ESCAPED (all got out) /
        // ALL EATEN (no fish left) / SURVIVED (time up with aliens in the tank). Living aliens share +40 when
        // the aliens' team wins; escaped aliens keep their own +20 instead.
        private void End(WinReason reason)
        {
            bool aliensWin = reason == WinReason.FoodGoalReached || reason == WinReason.TimeExpired || reason == WinReason.DiversLeft;
            foreach (var p in Players)
            {
                if (p.team == Team.Seeker)
                {
                    if (reason == WinReason.AllAliensCaught) p.score += WipedOutBonus;
                    else if (aliensWin || reason == WinReason.AllAliensEscaped) p.score += LoseScore;
                }
                else
                {
                    if (reason == WinReason.AllAliensCaught) p.score += LoseScore;
                    else if (aliensWin && p.alive && !p.escaped) p.score += TeamWinScore;
                }
            }
            Finish(reason == WinReason.AllAliensCaught || reason == WinReason.HuntOver ? Team.Seeker : Team.Hider, reason);
        }

        public static string EndingTitle(WinReason reason, Swimmer viewer)
        {
            if (viewer != null && viewer.team == Team.Hider && viewer.escaped) return "ESCAPED!";
            bool diver = viewer != null && viewer.team == Team.Seeker;
            switch (reason)
            {
                case WinReason.AllAliensCaught: return "WIPED OUT!";
                case WinReason.HuntOver: return "HUNT OVER!";
                case WinReason.AllAliensEscaped: return diver ? "YOU'RE FIRED!" : "ESCAPED!";
                case WinReason.FoodGoalReached: return diver ? "YOU'RE FIRED!" : "ALL EATEN!";
                case WinReason.TimeExpired: return diver ? "YOU'RE FIRED!" : "SURVIVED!";
                case WinReason.DiversLeft: return "SURVIVED!";
            }
            return "GAME OVER";
        }

        public static string EndingLine(WinReason reason, Swimmer viewer)
        {
            bool diver = viewer != null && viewer.team == Team.Seeker;
            bool alive = viewer != null && viewer.alive;
            if (viewer != null && viewer.team == Team.Hider && viewer.escaped) return "환수구 탈출 성공";
            switch (reason)
            {
                case WinReason.AllAliensCaught: return diver ? "외계인을 모두 잡았습니다" : "외계인이 모두 잡혔습니다";
                case WinReason.HuntOver: return "일부는 탈출하고, 나머지는 모두 잡혔습니다";
                case WinReason.AllAliensEscaped: return "외계인이 모두 환수구로 탈출했습니다";
                case WinReason.FoodGoalReached: return diver ? "수족관의 물고기가 모두 사라졌습니다" : alive ? "수족관의 물고기를 모두 먹어 치웠습니다" : "동료들이 물고기를 모두 먹었습니다. 죽은 외계인은 승리 점수가 없습니다";
                case WinReason.TimeExpired: return diver ? "제한 시간 안에 외계인을 다 잡지 못했습니다" : alive ? "제한 시간까지 살아남았습니다" : "동료가 살아남았습니다. 죽은 외계인은 승리 점수가 없습니다";
                case WinReason.DiversLeft: return "잠수부가 나갔습니다";
            }
            return "";
        }

        // ---- AI ------------------------------------------------------------------------------------------
        // Tutorials set this so AI players only drift in place.
        public bool BotsPassive;
        private SwimInput BotInputR47(Swimmer p) { if (BotsPassive) return new SwimInput { aimX = p.facing.x, aimY = p.facing.y, aimZ = p.facing.z }; return p.team == Team.Hider ? AlienBot(p) : DiverBot(p); }

        private static SwimInput Steer(Point from, Point to, float speed, Point aim)
        {
            var d = to - from; var dir = d.Length > .05f ? d.Normalized : new Point();
            var a = aim.Length > .01f ? aim.Normalized : dir.Length > .01f ? dir : new Point(0, 0, 1);
            return new SwimInput { x = dir.x * speed, y = dir.y * speed, z = dir.z * speed, aimX = a.x, aimY = a.y, aimZ = a.z };
        }


        // Bot steering with a little obstacle avoidance: probe ahead, swing round a blocked line, and take a
        // random detour for a moment when it has been stuck against something.
        private SwimInput Go(Swimmer p, Point to, float speed, Point aim)
        {
            var d = to - p.position;
            if (d.Length < .05f || speed <= 0) return Steer(p.position, p.position, 0, aim);
            if (p.velocity.Length > .5f) p.botMovingAt = Time;
            float pad = p.team == Team.Seeker ? .6f : .4f;
            Point waypoint = to;
            if (!SolidClear(p.position, to, pad))
            {
                if (p.botPath == null) p.botPath = new List<Point>();
                if (Time >= p.botPathAt || (to - p.botPathGoal).Length > 2f || p.botPathIndex >= p.botPath.Count)
                {
                    AquariumBotNav.Find(p.position, to, p.botPath);
                    p.botPathIndex = 0; p.botPathAt = Time + 2.5f; p.botPathGoal = to;
                }
                while (p.botPathIndex < p.botPath.Count - 1 && ((p.botPath[p.botPathIndex] - p.position).Length < 1.3f
                    || SolidClear(p.position, p.botPath[p.botPathIndex + 1], pad))) p.botPathIndex++;
                if (p.botPathIndex < p.botPath.Count) waypoint = p.botPath[p.botPathIndex];
            }
            var dir = (waypoint - p.position).Length > .05f ? (waypoint - p.position).Normalized : d.Normalized;
            if (Time < p.botDetourUntil) dir = p.botDetour;
            else if (Time - p.botMovingAt > 1.2f)
            {
                p.botDetour = new Point(Range(-1, 1), Range(.2f, .8f), Range(-1, 1)).Normalized;
                p.botDetourUntil = Time + .8f; p.botMovingAt = Time; p.botPathAt = 0; dir = p.botDetour;
            }
            var a = aim.Length > .01f ? aim.Normalized : dir;
            return new SwimInput { x = dir.x * speed, y = dir.y * speed, z = dir.z * speed, aimX = a.x, aimY = a.y, aimZ = a.z };
        }

        // R48: one roster array per tick for the bots' drain checks instead of a new copy per call.
        private Swimmer[] playerArray; private float playerArrayAt = float.NaN; private int playerArrayCount = -1;
        private Swimmer[] PlayerArray()
        {
            if (playerArray == null || playerArrayAt != Time || playerArrayCount != Players.Count) { playerArray = Players.ToArray(); playerArrayAt = Time; playerArrayCount = Players.Count; }
            return playerArray;
        }
        private bool DrainTaken(Swimmer p, int index, DrainTask task, int screw)
        {
            foreach (var o in Players) if (o != p && o.drainIndex == index && o.drainTask == task && (task != DrainTask.Screw || o.drainScrew == screw)) return true;
            return false;
        }

        private Swimmer NearestSeeker(Point at, out float distance)
        {
            Swimmer best = null; distance = float.MaxValue;
            foreach (var o in Players) if (o.team == Team.Seeker && o.alive) { var d = (o.position - at).Length; if (d < distance) { distance = d; best = o; } }
            return best;
        }

        // Alien AI: eat below 40% hunger, disguise and run from a diver within 8 m (or one looking at it),
        // work the most advanced drain while the diver is more than 15 m away, otherwise drift among fish.
        private SwimInput AlienBot(Swimmer p)
        {
            float hunger = HungerLeft(p, Time) / HungerMax(p);
            float diverDistance; var diver = NearestSeeker(p.position, out diverDistance);
            bool seen = diver != null && InAim(diver, p.position, 12.001f) && !Hidden(p, Time);
            bool danger = diver != null && Time >= HuntAt && (diverDistance < 8f || seen);
            bool ability = danger && HasAlienAbility(p) && Time >= p.nextAbility && diverDistance < 6f;
            // R68: eggs are laid only in the alien form. A disguised bot drops its disguise to lay only when no diver is
            // within 14 m (and does not disguise again meanwhile); otherwise it keeps the egg for later.
            bool lay = EggsReady(p) > 0 && !danger && (p.species < 0 || diverDistance > 14f);
            if ((p.drainTask == DrainTask.Screw || p.drainTask == DrainTask.Lid) && p.drainIndex >= 0 && p.drainIndex < drains.Length)
            {
                int part = p.drainTask == DrainTask.Lid ? DrainLidPart : p.drainScrew;
                var s = Go(p, p.position, 0, DrainPartPoint(drains[p.drainIndex], part) - p.position);
                if (danger || hunger < .25f) { s.interact = true; s.interactDrain = p.drainIndex; s.interactPart = part; }
                return s;
            }
            Prey food = null; float foodDistance = float.MaxValue;
            foreach (var f in Fish) if (Swimming(f)) { var d = (f.position - p.position).Length; if (d < foodDistance) { foodDistance = d; food = f; } }
            if (danger)
            {
                var away = p.position - diver.position; away = away.Length > .01f ? away.Normalized : new Point(1, 0, 0);
                var aim = food != null && foodDistance < 9f ? food.position - p.position : away;
                if (p.alienKind == 4 && ability) aim = away;
                var s = Go(p, p.position + away * 4f + new Point(0, .3f, 0), .9f, aim);
                s.morph = p.species < 0 && food != null && foodDistance < 9f; s.sprint = diverDistance < 5f; s.ability = ability;
                return s;
            }
            if (food != null && (hunger < .4f || diver == null))
            {
                var s = Go(p, food.position, .75f, food.position - p.position);
                s.action = foodDistance < 1.72f && Time >= HuntAt; s.morph = p.species < 0 && diverDistance < 18f; LayInAlienForm(p, s, lay);
                return s;
            }
            int drain, job;
            if (Time >= HuntAt && diverDistance > 15f && PickDrainJob(p, out drain, out job))
            {
                var at = DrainPartPoint(drains[drain], job);
                var approach = at + new Point(0, job == DrainHolePart ? SqueezeHover + .2f : .45f, 0);
                var s = Go(p, approach, .8f, at - p.position);
                if (p.species >= 0) s.unmorph = true;
                else if ((at - p.position).Length < DrainReach(job, Team.Hider) - .35f)
                { s.x = s.y = s.z = 0; s.interact = true; s.interactDrain = drain; s.interactPart = job; }
                LayInAlienForm(p, s, lay);
                return s;
            }
            var target = AquariumMaps.Layout((float)Math.Sin(Time * 0.23f + p.id) * 10, 3 + (float)Math.Sin(Time * 0.3f + p.id) * 1.5f, (float)Math.Cos(Time * 0.31f + p.id) * 6);
            var w = Go(p, target, .55f, food != null ? food.position - p.position : target - p.position);
            w.morph = p.species < 0 && food != null && foodDistance < 9f; LayInAlienForm(p, w, lay);
            return w;
        }

        // R68: a bot that wants to lay does not disguise this tick, and drops a disguise first (C, then R in the same tick).
        private static void LayInAlienForm(Swimmer p, SwimInput s, bool lay)
        {
            s.layEgg = lay;
            if (!lay) return;
            s.morph = false;
            if (p.species >= 0) s.unmorph = true;
        }

        private bool PickDrainJob(Swimmer p, out int drain, out int part)
        {
            drain = -1; part = -1; float best = float.MinValue;
            for (int i = 0; i < drains.Length; i++)
            {
                var d = drains[i]; int index = i;
                if (d.glued && !d.isOpen) continue;
                int choice = -1;
                if (d.isOpen) { if (!DrainTaken(p, index, DrainTask.Enter, -1)) choice = DrainHolePart; }
                else if (AquariumDrainLayout.AllScrewsOut(d)) { if (!DrainTaken(p, index, DrainTask.Lid, -1)) choice = DrainLidPart; }
                else if (d.screws != null) for (int k = 0; k < AquariumDrainLayout.ScrewCount && k < d.screws.Length; k++)
                {
                    int screw = k;
                    if (d.screws[k] >= 1 || DrainTaken(p, index, DrainTask.Screw, screw)) continue;
                    choice = k; break;
                }
                if (choice < 0) continue;
                float progress = AquariumDrainLayout.ScrewsOut(d) + d.lid * 6 + (d.isOpen ? 12 : 0);
                float score = progress * 2f - (AquariumDrainLayout.Mouth(d) - p.position).Length * .15f;
                if (score > best) { best = score; drain = i; part = choice; }
            }
            return drain >= 0;
        }

        // Diver AI: chase what it can see (x-ray shows everyone), cover moved lids, patrol the drains that
        // have the most loose screws, test fish with food three times a game and use its active.
        private SwimInput DiverBot(Swimmer p)
        {
            bool xray = XrayOn(p, Time);
            Swimmer prey = null; float best = float.MaxValue;
            foreach (var h in Players)
            {
                if (h.team != Team.Hider || !h.alive || h.swallowedBy != 0 || Hidden(h, Time)) continue;
                bool evident = h.species < 0 || h.revealUntil > Time || h.stunnedUntil > Time || h.drainTask != DrainTask.None;
                float d = (h.position - p.position).Length;
                if (((evident && InAim(p, h.position, 14f)) || (xray && d < 30f)) && d < best) { best = d; prey = h; }
            }
            bool egg = false; var eggAt = new Point();
            foreach (var e in Eggs) { float d = (e.position - p.position).Length; if (d < best && InAim(p, e.position, 14f)) { best = d; egg = true; eggAt = e.position; prey = null; } }
            float range = HarpoonRangeOf(p);
            SwimInput s;
            if (prey != null || egg)
            {
                var at = prey != null ? prey.position : eggAt;
                s = Go(p, at, best > range * .7f ? .85f : .2f, at - p.position);
                s.action = best < range * .95f; s.sprint = best > 8f;
            }
            else if (p.drainTask == DrainTask.Cover && p.drainIndex >= 0 && p.drainIndex < drains.Length)
                s = Go(p, p.position, 0, AquariumDrainLayout.LidCenter(drains[p.drainIndex]) - p.position);
            else
            {
                int open = -1; float od = float.MaxValue;
                for (int i = 0; i < drains.Length; i++) if (drains[i].lid > 0)
                { float d = (AquariumDrainLayout.LidCenter(drains[i]) - p.position).Length; if (d < od) { od = d; open = i; } }
                if (open >= 0)
                {
                    var lid = AquariumDrainLayout.LidCenter(drains[open]);
                    s = Go(p, lid + new Point(0, 1f, 0), .9f, lid - p.position); s.sprint = od > 6f;
                    string why;
                    if (od < CoverReach - .3f && CanUseDrainPart(p, drains, open, DrainLidPart, PlayerArray(), Time, HuntAt, out why))
                    { s.x = s.y = s.z = 0; s.interact = true; s.interactDrain = open; s.interactPart = DrainLidPart; }
                }
                else
                {
                    if (drains.Length > 0 && (p.botGoal < 0 || p.botGoal >= drains.Length || Time >= p.botTimer))
                    {
                        float top = float.MinValue; int pick = 0;
                        for (int i = 0; i < drains.Length; i++)
                        { float sc = AquariumDrainLayout.ScrewsOut(drains[i]) + Range(0, 2.5f) - (i == p.botGoal ? 3 : 0); if (sc > top) { top = sc; pick = i; } }
                        p.botGoal = pick; p.botTimer = Time + Range(10f, 18f);
                    }
                    var to = drains.Length > 0 ? AquariumDrainLayout.Mouth(drains[p.botGoal]) + new Point(0, 2f, 0) : p.position;
                    float d = (to - p.position).Length;
                    var look = d < 3f ? new Point((float)Math.Sin(Time * .7f), -.15f, (float)Math.Cos(Time * .7f)) : to - p.position;
                    s = Go(p, to, d < 3f ? .15f : .7f, look);
                }
            }
            if (p.feedsLeft > 0 && Time >= p.botFeedAt && Time >= HuntAt + 30f)
            {
                int near = 0; foreach (var f in Fish) if (Swimming(f) && (f.position - p.position).Length < FeedRadius) near++;
                if (near >= 4) { s.feed = true; p.botFeedAt = Time + Range(80f, 140f); }
            }
            if (Time >= DiverAbilityAt(HuntAt) && Time >= p.nextAbility)
            {
                if (p.diverKind == 0 && p.abilityUses > 0) { int g = GlueTarget(p, drains); if (g >= 0 && AquariumDrainLayout.ScrewsOut(drains[g]) >= 2) s.ability = true; }
                else if (p.diverKind == 1 && Time >= p.botAbilityAt) { s.ability = true; p.botAbilityAt = Time + Range(60f, 100f); }
            }
            return s;
        }
    }
}

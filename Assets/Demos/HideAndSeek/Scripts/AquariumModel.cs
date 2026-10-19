using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    public enum Team { Hider, Seeker }
    public enum RoundPhase { Lobby, Playing, Finished }
    public enum WinReason { None, AllAliensCaught, FoodGoalReached, TimeExpired, DiversLeft }

    [Serializable]
    public struct Point
    {
        public float x, y, z;
        public Point(float x, float z) : this(x, 0, z) { }
        public Point(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float Length { get { return (float)Math.Sqrt((double)x * x + (double)y * y + (double)z * z); } }
        public Point Normalized { get { var n = Length; return n > 0.0001f ? this / n : new Point(0, 1); } }
        public static Point operator +(Point a, Point b) { return new Point(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Point operator -(Point a, Point b) { return new Point(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Point operator *(Point a, float b) { return new Point(a.x * b, a.y * b, a.z * b); }
        public static Point operator /(Point a, float b) { return new Point(a.x / b, a.y / b, a.z / b); }
        public static Point TurnTowards(Point from,Point to,float radians)
        {
            if(to.Length<.0001f)return from;if(from.Length<.0001f)return to.Normalized;
            from=from.Normalized;to=to.Normalized;
            float dot=Math.Max(-1,Math.Min(1,Dot(from,to))),angle=(float)Math.Acos(dot);
            if(angle<=radians)return to;
            Point tangent=to-from*dot;
            if(tangent.Length<.0001f){var up=Math.Abs(from.y)<.9f?new Point(0,1,0):new Point(1,0,0);tangent=up-from*Dot(up,from);}
            return from*(float)Math.Cos(radians)+tangent.Normalized*(float)Math.Sin(radians);
        }
        public static float Dot(Point a, Point b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
    }

    [Serializable]
    public class Swimmer
    {
        public int id;
        public string name;
        public Team team;
        public bool ready, alive = true, bot, filled, eggUsed;
        public Point position, velocity, facing = new Point(0, 1);
        public Point swallowedFrom;
        public int species = -1, swallowedBy, deathSpecies = -1, swallowedSpecies = -1;
        public float releaseAt, revealUntil, nextAction, nextMorph, nextFeed, stunnedUntil;
        public float stamina = 100, lastMealAt, nextScan, scanUntil, morphLockedUntil, exposureSeconds, diedAt;
        public int personalEaten, score, exposureScore;
        public string deathReason = "";
        public string visualAction = "";
        public float visualAt = -100, deathAt = -100, morphAt = -100;
        public int visualTarget, releaseSource, morphFrom = -1;
    }

    [Serializable]
    public sealed class MorphTarget
    {
        public int id, species;
        public bool player;
        public Point position;
    }

    [Serializable]
    public class Prey
    {
        public int id, species;
        public Point position, facing;
        public bool dead, eaten;
        public float visualAt = -100;
        public int eater;
        public float turnAt;
        [NonSerialized] public Point steering;
        [NonSerialized] public float swimSpeed;
    }

    [Serializable]
    public class Harpoon
    {
        public int id, owner;
        public Point position, direction;
        public float life = 0.75f;
        public float born, returnAt, retrievedAt;
        public bool returning;
        public Point returnFrom;
    }

    [Serializable]
    public class WaterEffect
    {
        public int id, owner;
        public string kind;
        public Point position, direction;
        public float born, duration;
    }

    [Serializable]
    public class RevivalEgg
    {
        public int id, owner, revivePlayer;
        public Point position;
        public float hatchAt;
    }

    [Serializable]
    public class AquariumSnapshot
    {
        public int sequence, round, remaining, eaten, killed, target, winner = -1;
        public float time, huntAt, endsAt, reviewEndsAt, finishedAt;
        public WinReason winReason;
        public RoundPhase phase;
        public Swimmer[] players;
        public Prey[] fish;
        public Harpoon[] harpoons;
        public WaterEffect[] effects;
        public RevivalEgg[] eggs;
    }

    [Serializable]
    public class SwimInput
    {
        public float x, y, z, aimX, aimY, aimZ;
        public bool slow, action, morph, feed, sprint, scan, layEgg;
        public int morphTargetId;
        public bool morphTargetPlayer;
        public SwimInput Copy() { return (SwimInput)MemberwiseClone(); }
    }

    // The host owns all rules. This class has no Unity or networking dependency.
    public sealed class AquariumModel
    {
        public const int MaxPlayers = 5, FishCount = 32, EggFoodRequired = 3;
        public const float HiderSpeed = 4f, SeekerSpeed = 4.7f, RoundSeconds = 180f;
        public const float HarpoonCooldown = 3f, FeedCooldown = 20f, FeedRadius = 6f, FeedDuration = 5f;
        public const float HungerSeconds = 60f, EatStun = 2f, AllyStun = 3f, MorphLock = 6f, HatchSeconds = 12f;
        public const float ScanDuration = 5f, ScanCooldown = 18f, MaxStamina = 100f;
        public const float TraceDuration = 9f, ReviewSeconds = 20f;
        public const float FishFeedingSeconds = 4.2f, TentacleFeedingSeconds = 3.0f, SwallowVisualSeconds = 2.45f;
        public const float ReleaseKickSeconds = .85f, ReleaseKickSpeed = 5.4f, SpitAnimationSeconds = 1.2f;
        public const float DeathReviewSeconds = 2.5f, FeedSoundRadius = 10f;
        public const float EggSinkSpeed = 1.2f, EggRadius = 0.4f;
        public const float HalfWidth = 15f * AquariumMapOutline.HorizontalScale, HalfDepth = 10f * AquariumMapOutline.HorizontalScale, WaterHeight = 12f, Floor = 0.15f, RockHalfHeight = 1.4f;
        public static readonly float[] RockRadii = { 1.15f, 1.35f, 0.8f, 1.05f, 0.85f, 1.0f, 1.05f, 0.85f, 0.9f, 0.85f, 0.75f };
        public static readonly Point[] Rocks = FitReefs(new[] { new Point(-6, 0.9f, -2), new Point(5, 0.9f, 2.3f), new Point(1, 0.9f, -5), new Point(-10, 0.9f, 3.8f), new Point(-10.1f, 0.9f, -2.5f), new Point(9, 0.9f, -2.8f), new Point(10.5f, 0.9f, 3.8f), new Point(-3, 0.9f, 5.8f), new Point(7, 0.9f, -5.8f), new Point(-8, 0.9f, 5.8f), new Point(1.5f, 0.9f, 4.0f) });
        public readonly List<Swimmer> Players = new List<Swimmer>();
        public readonly List<Prey> Fish = new List<Prey>();
        public readonly List<Harpoon> Harpoons = new List<Harpoon>();
        public readonly List<WaterEffect> Effects = new List<WaterEffect>();
        public readonly List<RevivalEgg> Eggs = new List<RevivalEgg>();
        public RoundPhase Phase { get; private set; }
        public float Time { get; private set; }
        public float HuntAt { get; private set; }
        public float EndsAt { get; private set; }
        public float ReviewEndsAt { get; private set; }
        public float FinishedAt { get; private set; }
        public WinReason WinReason { get; private set; }
        public int Winner { get; private set; } = -1;
        public int Round { get; private set; }
        private int nextEffect, nextHarpoon, nextEgg, sequence;
        private readonly Random random;
        private readonly Dictionary<int, SwimInput> inputs = new Dictionary<int, SwimInput>();
        private readonly Dictionary<int, float> inputTimes = new Dictionary<int, float>();

        public readonly int SpeciesCount;
        public AquariumModel(int seed = 73, int speciesCount = 3)
        {
            SpeciesCount = Math.Max(1, Math.Min(99, speciesCount));
            random = new Random(seed);
            ResetFish();
        }

        public Swimmer Player(int id) { return Players.Find(p => p.id == id); }
        public static float FeedSoundGain(Point source, Point listener)
        { return Math.Max(0, 1f - (source - listener).Length / FeedSoundRadius); }
        public Swimmer AddPlayer(int id, string name, Team team, bool bot = false)
        {
            if (Phase != RoundPhase.Lobby || Players.Count >= MaxPlayers || Player(id) != null) return null;
            var p = new Swimmer { id = id, name = CleanName(name), team = team, bot = bot, ready = bot || id == 1 };
            p.position = Constrain(AquariumMapOutline.LayoutPoint(-9 + Players.Count * 2.4f, 2, 0), p.team == Team.Seeker ? .5f : .4f);
            Players.Add(p);
            return p;
        }

        public static string CleanName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Diver";
            value = value.Trim().Replace("\n", "").Replace("\r", "").Replace("<", "").Replace(">", "");
            return value.Substring(0, Math.Min(16, value.Length));
        }

        public void RemovePlayer(int id)
        {
            ReleaseCarried(id);
            Players.RemoveAll(p => p.id == id);
            inputs.Remove(id);
            inputTimes.Remove(id);
            Harpoons.RemoveAll(h => h.owner == id);
            Eggs.RemoveAll(e => e.revivePlayer == id);
            CheckWinner();
        }

        public void SetRole(int id, Team team, bool ready)
        {
            var p = Player(id);
            if (p == null || Phase != RoundPhase.Lobby) return;
            p.team = team == Team.Seeker ? Team.Seeker : Team.Hider;
            p.ready = ready || id == 1;
        }

        public string StartProblem()
        {
            if (Players.Count < 2) return "최소 2명이 필요합니다.";
            if (!Players.Exists(p => p.team == Team.Hider)) return "숨는 물고기가 1명 이상 필요합니다.";
            if (!Players.Exists(p => p.team == Team.Seeker)) return "술래가 1명 이상 필요합니다.";
            if (Players.FindAll(p => p.team == Team.Seeker).Count > 1) return "잠수부는 1명만 선택할 수 있습니다.";
            if (Players.Exists(p => !p.ready)) return "모든 참가자가 준비를 눌러 주세요.";
            return "";
        }

        public bool StartRound()
        {
            if (Phase != RoundPhase.Lobby || StartProblem().Length != 0) return false;
            int fillId = 1000;
            while (Players.FindAll(p => p.team == Team.Hider).Count < 4)
            {
                while (Player(fillId) != null) fillId++;
                var fill = AddPlayer(fillId++, "AI 외계인 " + (Players.FindAll(p => p.team == Team.Hider).Count + 1), Team.Hider, true);
                fill.filled = true;
            }
            Round++;
            ResetFish();
            Harpoons.Clear(); Effects.Clear(); Eggs.Clear(); inputs.Clear(); inputTimes.Clear();
            Phase = RoundPhase.Playing; Winner = -1; WinReason = WinReason.None; ReviewEndsAt = FinishedAt = 0;
            HuntAt = Time + 5f; EndsAt = HuntAt + RoundSeconds;
            int hider = 0, seeker = 0;
            foreach (var p in Players)
            {
                p.alive = true; p.swallowedBy = 0; p.swallowedSpecies = -1; p.species = p.bot && p.team == Team.Hider ? 0 : -1;
                p.releaseAt = p.revealUntil = p.nextAction = p.nextMorph = p.nextFeed = p.stunnedUntil = 0;
                p.visualAction = ""; p.visualAt = p.deathAt = p.morphAt = -100; p.visualTarget = 0;
                p.stamina = MaxStamina; p.lastMealAt = HuntAt; p.personalEaten = p.score = p.exposureScore = 0;
                p.eggUsed = false; p.nextScan = p.scanUntil = p.morphLockedUntil = p.exposureSeconds = 0; p.deathReason = "";
                p.velocity = new Point(); p.diedAt = 0; p.deathSpecies = -1;
                p.position = Constrain(p.team == Team.Hider ? AquariumMapOutline.LayoutPoint(-8 + hider++ * 2, 2, -1) : AquariumMapOutline.LayoutPoint(10, 2, -4 + seeker++ * 2), p.team == Team.Seeker ? .5f : .4f);
                p.facing = new Point(p.team == Team.Hider ? 1 : -1, 0);
            }
            return true;
        }

        public void ReturnToLobby()
        {
            if (Phase != RoundPhase.Finished || Time < ReviewEndsAt) return;
            Phase = RoundPhase.Lobby; Winner = -1; WinReason = WinReason.None; ReviewEndsAt = FinishedAt = 0;
            Harpoons.Clear(); Effects.Clear(); Eggs.Clear(); inputs.Clear(); inputTimes.Clear();
            Players.RemoveAll(p => p.filled);
            foreach (var p in Players)
            {
                p.ready = p.bot || p.id == 1; p.alive = true; p.swallowedBy = 0; p.swallowedSpecies = -1; p.species = -1;
                p.releaseAt = p.revealUntil = p.nextAction = p.nextMorph = p.nextFeed = p.stunnedUntil = 0;
                p.stamina = MaxStamina; p.nextScan = p.scanUntil = p.morphLockedUntil = 0; p.deathReason = "";
                p.velocity = new Point(); p.diedAt = 0; p.deathSpecies = -1;
            }
            ResetFish();
        }

        public void SetInput(int id, SwimInput input)
        {
            if (Phase != RoundPhase.Playing || Player(id) == null || input == null) return;
            // Reject non-finite data and clamp on the host, including diagonal movement.
            if (!ValidComponent(input.x) || !ValidComponent(input.y) || !ValidComponent(input.z)
                || !ValidComponent(input.aimX) || !ValidComponent(input.aimY) || !ValidComponent(input.aimZ)) return;
            SwimInput old;
            if (inputs.TryGetValue(id, out old))
            {
                if (old.morph && !input.morph) { input.morphTargetId = old.morphTargetId; input.morphTargetPlayer = old.morphTargetPlayer; }
                input.action |= old.action; input.morph |= old.morph; input.feed |= old.feed; input.scan |= old.scan; input.layEgg |= old.layEgg;
            }
            inputs[id] = input.Copy(); inputTimes[id] = Time;
        }

        private static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
        private static bool ValidComponent(float n) { return Finite(n) && Math.Abs(n) <= 1000; }
        private float Range(float a, float b) { return a + (float)random.NextDouble() * (b - a); }
        private void ResetFish()
        {
            Fish.Clear();
            for (int i = 0; i < FishCount; i++)
                Fish.Add(new Prey { id = 100 + i, species = i % SpeciesCount, position = Constrain(AquariumMapOutline.LayoutPoint(Range(-12, 12), Range(1.2f, WaterHeight-1.2f), Range(-7, 7)), 0.35f),
                    facing = new Point(Range(-1, 1), Range(-0.25f, 0.25f), Range(-1, 1)).Normalized, turnAt = Time + Range(1, 4) });
        }

        public void Tick(float dt)
        {
            if (!Finite(dt) || dt <= 0 || dt > 0.1f) return;
            Time += dt;
            if (Phase == RoundPhase.Finished) return;
            Effects.RemoveAll(e => Time >= e.born + e.duration);
            foreach (var f in Fish)
            {
                if (f.eaten) continue;
                if (f.dead) { f.position = Constrain(f.position + new Point(0, -0.8f * dt, 0), 0.35f); continue; }
                if (Time > f.turnAt) { f.steering = (f.facing + new Point(Range(-0.8f, 0.8f), Range(-0.3f, 0.3f), Range(-0.8f, 0.8f))).Normalized; f.turnAt = Time + Range(1.4f, 4.5f); }
                Point heading=f.steering.Length>.0001f?f.steering:f.facing;
                WaterEffect food = null; float foodDistance = FeedRadius;
                foreach (var e in Effects)
                    if (e.kind == "feed" && (e.position - f.position).Length < foodDistance)
                    { food = e; foodDistance = (e.position - f.position).Length; }
                float targetSpeed=1.4f+f.species*.18f;
                if (food != null)
                {
                    if (foodDistance > 0.15f) heading = (food.position - f.position).Normalized;
                }
                else if (Phase == RoundPhase.Playing && Time >= HuntAt)
                {
                    foreach(var diver in Players)if(diver.team==Team.Seeker && diver.alive && (diver.position-f.position).Length<4.5f)
                    {heading=(f.position-diver.position).Normalized;targetSpeed*=1.45f;break;}
                }
                f.facing=Point.TurnTowards(f.facing,heading,2.8f*dt);
                float speedStep=4f*dt;f.swimSpeed+=Math.Max(-speedStep,Math.Min(speedStep,targetSpeed-f.swimSpeed));
                float travel=f.swimSpeed*dt;
                if(food!=null)travel=Math.Min(travel,Math.Max(0,foodDistance-.15f));
                var wanted = f.position + f.facing * travel;
                var actual = Constrain(wanted, 0.35f);
                if ((wanted - actual).Length > 0.001f) { f.steering = (actual-wanted).Normalized; f.turnAt = Time + 0.9f; }
                f.position = actual;
            }
            if (Phase != RoundPhase.Playing) { AquariumBodyCollision.Resolve(Players,Fish,Time); return; }
            // Release before either player consumes input, regardless of roster order.
            foreach (var p in Players) if (p.alive && p.swallowedBy != 0)
            {
                var carrier = Player(p.swallowedBy);
                if (carrier == null || !carrier.alive || Time >= p.releaseAt) Release(p, carrier);
            }
            foreach (var p in Players)
            {
                if (!p.alive) { p.velocity = new Point(); continue; }
                if (p.team == Team.Hider && Time >= HuntAt && Time - p.lastMealAt >= HungerSeconds)
                { Kill(p, "starved"); continue; }
                if (p.swallowedBy != 0)
                {
                    p.velocity = new Point();
                    inputs.Remove(p.id); inputTimes.Remove(p.id);
                    var carrier = Player(p.swallowedBy);
                    if (carrier != null) p.position = carrier.position;
                    continue;
                }
                if (TickReleaseKick(p,dt)) continue;
                SwimInput input;
                if (p.bot) input = BotInput(p);
                else if (!inputs.TryGetValue(p.id, out input) || Time - inputTimes[p.id] > 0.4f) input = new SwimInput();
                var move = new Point(input.x, input.y, input.z);
                if (move.Length > 1) move = move.Normalized;
                var aim = new Point(input.aimX, input.aimY, input.aimZ);
                if (aim.Length > 0.1f) p.facing = aim.Normalized;
                if ((p.team == Team.Seeker && Time < HuntAt) || Time < p.stunnedUntil)
                { p.velocity = new Point(); input.action = input.morph = input.feed = input.scan = input.layEgg = false; p.stamina = Math.Min(MaxStamina, p.stamina + 18f * dt); continue; }
                float speed = p.team == Team.Seeker ? SeekerSpeed : (input.slow ? 1.65f : HiderSpeed);
                bool sprinting = input.sprint && move.Length > 0.01f && p.stamina > 0;
                if (sprinting) speed *= 1f + 0.6f * Math.Min(1f, p.stamina / (25f * dt));
                // A short acceleration/deceleration ramp smooths input changes without accumulating wall drift.
                var velocityChange = move * speed - p.velocity;
                float accelerationStep = 32f * dt;
                p.velocity += velocityChange.Length <= accelerationStep ? velocityChange : velocityChange.Normalized * accelerationStep;
                var wanted = p.position + p.velocity * dt;
                var movedTo = AquariumBodyCollision.Move(p, p.velocity * dt, Players, Fish, Time);
                if ((wanted - movedTo).Length > 0.001f) p.velocity = new Point();
                bool moved = Point.Dot(movedTo - p.position, move) > 0.001f;
                if (sprinting && moved) p.stamina = Math.Max(0, p.stamina - 25f * dt);
                else if (!input.sprint || move.Length <= 0.01f || !moved) p.stamina = Math.Min(MaxStamina, p.stamina + 18f * dt);
                p.position = movedTo;
                if (input.morph && p.team == Team.Hider && Time >= p.nextMorph && Time >= p.morphLockedUntil) Morph(p, input);
                if (input.action && Time >= p.nextAction && (p.team != Team.Hider || Time >= HuntAt))
                {
                    if (p.team == Team.Hider) Bite(p); else Shoot(p);
                }
                if (input.feed && p.team == Team.Seeker && Time >= p.nextFeed) Feed(p);
                if (input.scan && p.team == Team.Seeker && Time >= p.nextScan)
                { p.nextScan = Time + ScanCooldown; p.scanUntil = Time + ScanDuration; Effect("scan", p.position, p.facing, ScanDuration); }
                if (input.layEgg && p.team == Team.Hider && Time >= p.stunnedUntil) LayEgg(p);
                input.action = input.morph = input.feed = input.scan = input.layEgg = false;
            }
            AquariumBodyCollision.Resolve(Players,Fish,Time);
            TickHarpoons(dt);
            TickEggs(dt);
            TickExposure(dt);
            CheckWinner();
        }

        private SwimInput BotInput(Swimmer p)
        {
            Point target = AquariumMapOutline.LayoutPoint((float)Math.Sin(Time * 0.23f) * 10, 3 + (float)Math.Sin(Time * 0.3f) * 1.5f, (float)Math.Cos(Time * 0.31f) * 6);
            float closest = float.MaxValue;
            if (p.team == Team.Hider)
            {
                foreach (var f in Fish) if (!f.eaten && !f.dead && (f.position - p.position).Length < closest)
                { closest = (f.position - p.position).Length; target = f.position; }
            }
            else
            {
                // A visible alien form or eating mouth is evidence; neutral scan dots are never used to identify aliens.
                foreach (var h in Players) if (h.team == Team.Hider && h.alive && h.swallowedBy == 0 && (h.species < 0 || h.revealUntil > Time || h.stunnedUntil > Time) && InAim(p, h.position, 12.001f))
                    if ((h.position - p.position).Length < closest) { closest = (h.position - p.position).Length; target = h.position; }
                foreach (var egg in Eggs) if (InAim(p, egg.position, 12.001f) && (egg.position - p.position).Length < closest)
                { closest = (egg.position - p.position).Length; target = egg.position; }
            }
            var direction = (target - p.position).Normalized;
            return new SwimInput { x = direction.x * 0.7f, y = direction.y * 0.7f, z = direction.z * 0.7f, aimX = direction.x, aimY = direction.y, aimZ = direction.z,
                action = p.team == Team.Hider ? closest < 1.72f : closest < 8, morph = p.species < 0,
                feed = p.team == Team.Seeker && Fish.Exists(f => !f.dead && !f.eaten && (f.position - p.position).Length < FeedRadius),
                layEgg = p.team == Team.Hider && Players.Exists(h => h.team == Team.Hider && !h.alive),
                scan = p.team == Team.Seeker && closest > 8 };
        }

        // Both the local outline and authoritative action use this angular-first selector.
        public static MorphTarget FindMorphTarget(IList<Prey> fish, IList<Swimmer> players, Swimmer caster, Point aim, float time)
        {
            if (caster == null || !caster.alive || caster.team != Team.Hider || caster.swallowedBy != 0 || !ValidComponent(aim.x) || !ValidComponent(aim.y) || !ValidComponent(aim.z) || aim.Length < 0.1f) return null;
            aim = aim.Normalized;
            MorphTarget best = null; float bestAlignment = -1, bestDistance = float.MaxValue;
            if (fish != null) foreach (var f in fish)
                if (!f.dead && !f.eaten) ConsiderMorphTarget(caster, aim, f.id, f.species, false, f.position, ref best, ref bestAlignment, ref bestDistance);
            if (players != null) foreach (var ally in players)
                if (CopyableAlly(caster, ally, time)) ConsiderMorphTarget(caster, aim, ally.id, ally.species, true, ally.position, ref best, ref bestAlignment, ref bestDistance);
            return best;
        }

        public static bool CopyableAlly(Swimmer caster, Swimmer ally, float time)
        { return ally.id != caster.id && ally.team == Team.Hider && ally.alive && ally.swallowedBy == 0 && ally.species >= 0 && time >= ally.revealUntil; }

        public static bool CanMorphTarget(Swimmer caster, Point aim, Point position)
        {
            if (caster == null || !caster.alive || caster.team != Team.Hider || caster.swallowedBy != 0
                || !ValidComponent(aim.x) || !ValidComponent(aim.y) || !ValidComponent(aim.z) || aim.Length < 0.1f
                || !Finite(position.x) || !Finite(position.y) || !Finite(position.z)
                || !Finite(caster.position.x) || !Finite(caster.position.y) || !Finite(caster.position.z)) return false;
            float alignment, distance;
            return MorphVisible(caster, aim.Normalized, position, out alignment, out distance);
        }

        private static bool MorphVisible(Swimmer caster, Point aim, Point position, out float alignment, out float distance)
        {
            var delta = position - caster.position; distance = delta.Length;
            alignment = distance < 0.0001f ? 1 : Point.Dot(delta / distance, aim);
            return distance <= 2.8f && alignment >= 0.8f && ObstacleHit(caster.position, position) > 1;
        }

        private static void ConsiderMorphTarget(Swimmer caster, Point aim, int id, int species, bool player, Point position,
            ref MorphTarget best, ref float bestAlignment, ref float bestDistance)
        {
            float alignment, distance;
            if (!MorphVisible(caster, aim, position, out alignment, out distance)) return;
            if (best != null && (alignment < bestAlignment - 0.00001f || (Math.Abs(alignment - bestAlignment) <= 0.00001f && distance >= bestDistance))) return;
            best = new MorphTarget { id = id, species = species, player = player, position = position };
            bestAlignment = alignment; bestDistance = distance;
        }

        private void Morph(Swimmer p, SwimInput input)
        {
            MorphTarget target = null;
            if (input.morphTargetId != 0)
            {
                if (input.morphTargetPlayer)
                {
                    var ally = Player(input.morphTargetId);
                    if (ally != null && CopyableAlly(p, ally, Time)) target = new MorphTarget { id = ally.id, species = ally.species, player = true, position = ally.position };
                }
                else
                {
                    var fish = Fish.Find(f => f.id == input.morphTargetId && !f.dead && !f.eaten);
                    if (fish != null) target = new MorphTarget { id = fish.id, species = fish.species, position = fish.position };
                }
                float alignment, distance;
                if (target != null && !MorphVisible(p, p.facing, target.position, out alignment, out distance)) target = null;
            }
            else target = FindMorphTarget(Fish, Players, p, p.facing, Time);
            p.nextMorph = Time + 0.5f;
            if (target == null) return;
            p.morphFrom = p.species; p.morphAt = Time;
            p.species = target.species; p.revealUntil = Time + 0.65f;
            Effect("morph", p.position, p.facing, 0.65f);
        }

        private static bool InAim(Swimmer p, Point target, float range)
        {
            var delta = target - p.position;
            return delta.Length < range && (delta.Length < 0.1f || Point.Dot(delta.Normalized, p.facing) >= 0.8f) && ObstacleHit(p.position, target) > 1;
        }

        private bool InBite(Swimmer p, Point target, float best)
        {
            var delta = target - p.position;
            return delta.Length < best && (delta.Length < 0.3f || Point.Dot(delta.Normalized, p.facing) > 0.25f) && SolidHit(p.position, target) > 1;
        }

        public static bool HasFishDisguise(Swimmer p, float time)
        { return p.team == Team.Hider && p.alive && p.species >= 0 && p.revealUntil <= time; }

        private void Bite(Swimmer p)
        {
            if (Time < HuntAt) return;
            p.nextAction = Time + 0.65f;
            if (Players.Exists(h => h.swallowedBy == p.id)) return;
            Swimmer friend = null; Prey prey = null; float distance = 1.78f;
            foreach (var h in Players)
                if (h.id != p.id && h.team == Team.Hider && h.alive && h.swallowedBy == 0 && !Players.Exists(c => c.swallowedBy == h.id) && InBite(p, h.position, distance))
                { friend = h; distance = (h.position - p.position).Length; }
            foreach (var f in Fish) if (!f.eaten && !f.dead && InBite(p, f.position, distance))
            { prey = f; friend = null; distance = (f.position - p.position).Length; }
            Effect("bite", p.position + p.facing * 0.8f, p.facing, 0.3f);
            if (friend != null)
            {
                p.stunnedUntil = friend.stunnedUntil = Time + AllyStun;
                p.velocity = friend.velocity = new Point();
                p.morphLockedUntil = friend.morphLockedUntil = Time + MorphLock;
                p.visualAction = "TentacleEat"; p.visualAt = Time;
                friend.visualAction = "Eaten"; friend.visualAt = Time;
                // Preserve the visible disguise through capture; the forced reveal
                // happens out of sight inside the carrier before expulsion.
                friend.swallowedSpecies = friend.species >= 0 && (friend.revealUntil <= Time || Time-friend.morphAt < 1.4f) ? friend.species : -1;
                p.species = friend.species = -1; p.revealUntil = friend.revealUntil = Time + MorphLock;
                p.score -= 150;
                Effect("allyEat",p.position,p.facing,TraceDuration,p.id);
                friend.swallowedFrom = friend.position; friend.swallowedBy = p.id; friend.releaseAt = Time + AllyStun; friend.position = p.position;
            }
            else if (prey != null)
            {
                prey.eaten = true; prey.eater = p.id; prey.visualAt = Time;
                p.visualAction = HasFishDisguise(p, Time) ? "Bite" : "TentacleEat"; p.visualAt = Time; p.visualTarget = prey.id;
                float feeding=p.visualAction=="Bite"?FishFeedingSeconds:TentacleFeedingSeconds;
                p.nextAction=Time+feeding;p.nextMorph=Math.Max(p.nextMorph,Time+feeding);
                p.stunnedUntil = Time + EatStun;
                p.velocity = new Point();
                p.lastMealAt = Math.Max(Time, HuntAt); p.personalEaten++; p.score += 100;
                Effect("eat", prey.position, p.facing, TraceDuration,p.id);
            }
        }

        private void ReleaseCarried(int carrierId)
        {
            var carrier = Player(carrierId);
            foreach (var p in Players) if (p.swallowedBy == carrierId) Release(p, carrier);
        }

        private void Release(Swimmer swallowed, Swimmer carrier)
        {
            var direction = carrier == null || carrier.facing.Length < .001f ? new Point(0, 1) : carrier.facing.Normalized;
            var at = carrier == null ? swallowed.position : carrier.position;
            swallowed.swallowedBy = 0; swallowed.releaseAt = 0; swallowed.swallowedSpecies = -1; swallowed.species = -1;
            swallowed.releaseSource = carrier != null ? carrier.id : 0;
            swallowed.velocity = new Point();
            // Emerge just outside the two rounded hulls, then travel continuously.
            swallowed.position = Constrain(at + direction * (2*(AquariumBodyCollision.AlienRadius+AquariumBodyCollision.AlienHalf)+.03f), .4f);
            swallowed.revealUntil = Math.Max(swallowed.revealUntil, swallowed.morphLockedUntil);
            if (carrier != null) { carrier.revealUntil = Math.Max(carrier.revealUntil, carrier.morphLockedUntil); carrier.nextAction = Math.Max(carrier.nextAction, Time + 0.8f); }
            if (Phase == RoundPhase.Playing)
            {
                BeginReleaseKick(swallowed,direction,"SpitOut");
                if (carrier != null && carrier.alive) BeginReleaseKick(carrier,direction,"SpitRecoil");
            }
            Effect("burp", at, direction, TraceDuration,carrier != null ? carrier.id : 0);
        }

        public static bool IsReleaseKick(string motion) { return motion=="SpitOut" || motion=="SpitRecoil"; }
        private void BeginReleaseKick(Swimmer player,Point direction,string motion)
        {
            player.velocity = new Point();
            player.facing=direction;player.visualAction=motion;player.visualAt=Time;
            player.nextAction=Math.Max(player.nextAction,Time+(motion=="SpitRecoil"?SpitAnimationSeconds:ReleaseKickSeconds));
        }
        private static float ReleaseTravel(float age)
        {
            float u=Math.Max(0,Math.Min(1,age/ReleaseKickSeconds));
            return ReleaseKickSpeed*ReleaseKickSeconds*(u-u*u+u*u*u/3);
        }
        private bool TickReleaseKick(Swimmer player,float dt)
        {
            if (!IsReleaseKick(player.visualAction) || Time-dt>=player.visualAt+ReleaseKickSeconds) return false;
            float age=Time-player.visualAt;
            float travel=ReleaseTravel(age)-ReleaseTravel(age-dt);
            // The carrier keeps looking at the expelled alien while its body recoils.
            float sign=player.visualAction=="SpitRecoil"?-1:1;
            if (travel>0) player.position=AquariumBodyCollision.Move(player,player.facing*(travel*sign),Players,Fish,Time);
            // The impulse has priority over held movement/aim; action edges are not queued.
            SwimInput blocked;
            if (inputs.TryGetValue(player.id,out blocked)) blocked.action=blocked.morph=blocked.feed=blocked.scan=blocked.layEgg=false;
            player.stamina=Math.Min(MaxStamina,player.stamina+18f*dt);
            return true;
        }

        private void Shoot(Swimmer p)
        {
            p.nextAction = Time + HarpoonCooldown;
            Harpoons.Add(new Harpoon { id = ++nextHarpoon, owner = p.id, position = p.position + p.facing * 0.7f, direction = p.facing,
                born = Time, retrievedAt = p.nextAction });
            Effect("shot", p.position + p.facing, p.facing, 0.2f);
        }

        private void Feed(Swimmer p)
        {
            p.nextFeed = Time + FeedCooldown;
            Effect("feed", Constrain(p.position + p.facing * 1.2f, 0.35f), p.facing, FeedDuration);
        }

        private void LayEgg(Swimmer owner)
        {
            if (owner.eggUsed || owner.personalEaten < EggFoodRequired) return;
            var target = Players.Find(p => p.team == Team.Hider && !p.alive && !Eggs.Exists(e => e.revivePlayer == p.id));
            if (target == null) return;
            owner.eggUsed = true;
            var egg = new RevivalEgg { id = ++nextEgg, owner = owner.id, revivePlayer = target.id,
                position = Constrain(owner.position - owner.facing * 0.7f, EggRadius), hatchAt = Time + HatchSeconds };
            Eggs.Add(egg); Effect("egg", egg.position, owner.facing, 0.8f);
        }

        private void TickEggs(float dt)
        {
            for (int i = Eggs.Count - 1; i >= 0; i--)
            {
                var egg = Eggs[i]; var target = Player(egg.revivePlayer);
                if (target == null || target.alive) { Eggs.RemoveAt(i); continue; }
                // Use the shared terrain collision so eggs settle on sand or reef surfaces on every client.
                var fallingTo = egg.position + new Point(0, -EggSinkSpeed * dt, 0);
                float surface = ObstacleHit(egg.position, fallingTo, EggRadius);
                egg.position += (fallingTo - egg.position) * Math.Min(1, Math.Max(0, surface));
                if (Time < egg.hatchAt) continue;
                target.alive = true; target.position = egg.position; target.species = -1; target.swallowedBy = 0; target.swallowedSpecies = -1;
                target.visualAction = ""; target.visualAt = target.deathAt = target.morphAt = -100;
                target.stamina = MaxStamina; target.lastMealAt = Time; target.deathReason = "";
                target.velocity = new Point(); target.diedAt = 0; target.deathSpecies = -1;
                target.releaseAt = target.stunnedUntil = target.nextAction = target.nextMorph = target.morphLockedUntil = 0;
                target.revealUntil = Time + 0.8f; target.exposureScore = 0; target.exposureSeconds = 0;
                inputs.Remove(target.id); inputTimes.Remove(target.id);
                var owner = Player(egg.owner); if (owner != null) owner.score += 150;
                Effect("hatch", egg.position, new Point(0, 1), 1.5f); Eggs.RemoveAt(i);
            }
        }

        private void TickExposure(float dt)
        {
            if (Time < HuntAt) return;
            foreach (var alien in Players)
            {
                if (alien.team != Team.Hider || !alien.alive || alien.swallowedBy != 0 || alien.exposureScore >= 50) continue;
                bool visible = Players.Exists(diver => diver.team == Team.Seeker && diver.alive && InAim(diver, alien.position, 12.001f));
                if (!visible) continue;
                alien.exposureSeconds += dt;
                while (alien.exposureSeconds >= 2f && alien.exposureScore < 50)
                { alien.exposureSeconds -= 2f; alien.exposureScore += 5; alien.score += 5; }
            }
        }

        private void Kill(Swimmer player, string reason)
        {
            player.diedAt = Time; player.deathSpecies = player.species >= 0 && Time >= player.revealUntil ? player.species : -1;
            player.velocity = new Point();
            player.alive = false; player.deathAt = Time; player.visualAt = Time; player.visualAction = reason == "harpoon" ? "Harpoon" : "Eaten"; player.deathReason = reason; player.swallowedBy = 0;
            inputs.Remove(player.id); inputTimes.Remove(player.id); ReleaseCarried(player.id);
            Effect(reason == "starved" ? "starved" : "caught", player.position, player.facing, 1.2f);
        }

        private void TickHarpoons(float dt)
        {
            for (int i = Harpoons.Count - 1; i >= 0; i--)
            {
                var h = Harpoons[i]; var owner = Player(h.owner);
                if (owner == null || !owner.alive || Time >= h.retrievedAt) { Harpoons.RemoveAt(i); continue; }
                if (h.returning)
                {
                    // The host keeps a spent spear until the cooldown ends. It cannot hit twice.
                    float t = Math.Min(1, Math.Max(0, (Time - h.returnAt) / Math.Max(.01f, h.retrievedAt - h.returnAt)));
                    float reel = t * t * (3 - 2 * t);
                    var socket = owner.position + owner.facing * .7f;
                    h.position = h.returnFrom + (socket - h.returnFrom) * reel;
                    var away = h.position - owner.position;
                    if (away.Length > .01f) h.direction = away.Normalized;
                    continue;
                }
                var start = h.position; var end = start + h.direction * (20f * Math.Min(dt, h.life));
                h.life -= dt;
                float first = 2f; Swimmer hitPlayer = null; Prey hitFish = null; RevivalEgg hitEgg = null;
                // Segment collision prevents fast harpoons crossing thin targets between ticks.
                foreach (var p in Players) if (p.id != h.owner && p.team == Team.Hider && p.alive && p.swallowedBy == 0)
                { var t = HitTime(start, end, p.position, 0.48f); if (t < first) { first = t; hitPlayer = p; hitFish = null; } }
                foreach (var f in Fish) if (!f.eaten && !f.dead)
                { var t = HitTime(start, end, f.position, 0.42f); if (t < first) { first = t; hitFish = f; hitPlayer = null; } }
                foreach (var egg in Eggs)
                { var t = HitTime(start, end, egg.position, 0.45f); if (t < first) { first = t; hitEgg = egg; hitFish = null; hitPlayer = null; } }
                var obstruction = SolidHit(start, end);
                if (obstruction < first) { first = obstruction; hitFish = null; hitPlayer = null; hitEgg = null; }
                if (first <= 1f)
                {
                    h.position = start + (end - start) * first;
                    if (hitPlayer != null) Kill(hitPlayer, "harpoon");
                    else if (hitFish != null) { hitFish.dead = true; hitFish.visualAt = Time; Effect("miss", h.position, h.direction, 0.8f); }
                    else if (hitEgg != null) { Eggs.Remove(hitEgg); Effect("eggBreak", h.position, h.direction, 1f); }
                    else Effect("miss", h.position, h.direction, 0.35f);
                    BeginHarpoonReturn(h);
                }
                else
                {
                    h.position = end;
                    if (h.life <= 0 || !InsideTank(end) || end.y < AquariumSeabed.Height(end) || end.y > WaterHeight)
                    { h.position = Constrain(end, .04f); BeginHarpoonReturn(h); }
                }
            }
        }

        private void BeginHarpoonReturn(Harpoon h)
        { h.returning = true; h.returnAt = Time; h.returnFrom = h.position; }

        public static float HitTime(Point a, Point b, Point centre, float radius)
        {
            var d = b - a; var offset = a - centre;
            float c = Point.Dot(offset, offset) - radius * radius;
            if (c <= 0) return 0;
            float aa = Point.Dot(d, d), bb = 2 * Point.Dot(offset, d);
            float disc = bb * bb - 4 * aa * c;
            if (aa < 0.00001f || disc < 0) return 2;
            float t = (-bb - (float)Math.Sqrt(disc)) / (2 * aa);
            return t >= 0 && t <= 1 ? t : 2;
        }

        // Keep each cover ellipsoid and a swimmer-sized clearance inside the traced water.
        private static Point[] FitReefs(Point[] rocks)
        {
            var radii = RockRadii;
            for (int i = 0; i < rocks.Length; i++)
            {
                var original=AquariumMapOutline.LayoutPoint(rocks[i].x,rocks[i].y,rocks[i].z);float clearance=radii[i]+.85f;
                var p=AquariumMapOutline.Clamp(original,clearance);
                if(!AquariumWalkwayLayout.Contains(p,clearance)){p.y+=AquariumSeabed.Height(p)-Floor;rocks[i]=p;continue;}
                float best=float.MaxValue;
                for(float x=-HalfWidth;x<=HalfWidth;x+=.25f)for(float z=-HalfDepth;z<=HalfDepth;z+=.25f)
                {
                    var candidate=new Point(x,original.y,z);float distance=(candidate-original).Length;
                    if(distance>=best||!AquariumMapOutline.Contains(candidate,clearance)||AquariumWalkwayLayout.Contains(candidate,clearance))continue;
                    bool overlaps=false;
                    for(int j=0;j<i;j++)if((candidate-rocks[j]).Length<radii[i]+radii[j]+.3f){overlaps=true;break;}
                    if(!overlaps){best=distance;p=candidate;}
                }
                p.y+=AquariumSeabed.Height(p)-Floor;rocks[i]=p;
            }
            return rocks;
        }
        // The reference has a concave inlet, so the shared polygon owns collision too.
        public static float TankRadius(float angle) { return AquariumMapOutline.Radius(angle); }
        public static bool InsideTank(Point p, float padding = 0) { return AquariumMapOutline.Contains(p, padding); }
        private static Point ConstrainPerimeter(Point p, float padding) { return AquariumMapOutline.Clamp(p, padding); }

        public static Point Constrain(Point p, float radius)
        {
            if(FreeSpace(p,radius))return p;
            for(int pass=0;pass<8;pass++)
            {
            p = AquariumWalkwayLayout.Constrain(ConstrainPerimeter(p, radius),radius);
            p.y = Math.Max(AquariumSeabed.Height(p) + radius, Math.Min(WaterHeight - radius, p.y));
            for (int i = 0; i < Rocks.Length; i++)
            {
                var delta = ScaleRock(p - Rocks[i], i, radius);
                if (delta.Length < 1)
                {
                    var outward = delta.Normalized;
                    var resolved = Rocks[i] + new Point(outward.x * (RockRadii[i] + radius), outward.y * (RockHalfHeight + radius), outward.z * (RockRadii[i] + radius));
                    if (resolved.y < AquariumSeabed.Height(resolved) + radius)
                    {
                        var flat = new Point(delta.x, delta.z).Normalized;
                        float horizontal = (RockRadii[i] + radius) * (float)Math.Sqrt(Math.Max(0, 1 - delta.y * delta.y));
                        p = new Point(Rocks[i].x + flat.x * horizontal, p.y, Rocks[i].z + flat.z * horizontal);
                    }
                    else p = resolved;
                }
            }
            p.y = Math.Max(AquariumSeabed.Height(p) + radius, Math.Min(WaterHeight - radius, p.y));
            p = AquariumMonolithLayout.Constrain(p,radius);
            p.y = Math.Max(AquariumSeabed.Height(p) + radius, Math.Min(WaterHeight - radius, p.y));
            if(FreeSpace(p,radius))break;
            }
            if(!FreeSpace(p,radius))
            {
                // At a concave entrance joint the two inward normals can oppose one another.
                // Resolve onto the nearest free side instead of oscillating between the panes.
                for(float ring=.12f;ring<=12;ring+=.12f)for(int angle=0;angle<48;angle++)
                {
                    double a=angle*Math.PI/24;
                    var candidate=p+new Point((float)Math.Cos(a)*ring,0,(float)Math.Sin(a)*ring);
                    if(FreeSpace(candidate,radius+.002f))return candidate;
                }
            }
            return p;
        }

        private static bool FreeSpace(Point p,float radius)
        {
            if(p.y<AquariumSeabed.Height(p)+radius-.001f||p.y>WaterHeight-radius+.001f||!InsideTank(p,radius)||AquariumWalkwayLayout.Contains(p,radius)||AquariumMonolithLayout.Overlaps(p,radius))return false;
            for(int i=0;i<Rocks.Length;i++)if(ScaleRock(p-Rocks[i],i,radius).Length<1)return false;
            return true;
        }

        private static Point ScaleRock(Point delta, int index, float padding)
        { return new Point(delta.x / (RockRadii[index] + padding), delta.y / (RockHalfHeight + padding), delta.z / (RockRadii[index] + padding)); }

        // Shared swept ellipsoid check for harpoons, camera occlusion and aim rays.
        public static float ObstacleHit(Point start, Point end, float padding = 0)
        {
            float hit = Math.Min(AquariumMonolithLayout.Hit(start,end,padding),AquariumSeabed.Hit(start,end,padding));
            for (int i = 0; i < Rocks.Length; i++)
                hit = Math.Min(hit, HitTime(ScaleRock(start - Rocks[i], i, padding), ScaleRock(end - Rocks[i], i, padding), new Point(), 1));
            return hit;
        }

        // Glass is transparent to the camera but blocks projectiles and physical interaction.
        public static float SolidHit(Point start,Point end,float padding=0)
        { return Math.Min(ObstacleHit(start,end,padding),AquariumWalkwayLayout.Hit(start,end,padding)); }

        private void Effect(string kind, Point at, Point direction, float duration,int owner=0)
        { Effects.Add(new WaterEffect { id = ++nextEffect, owner=owner, kind = kind, position = at, direction = direction, born = Time, duration = duration }); }

        private void CheckWinner()
        {
            if (Phase != RoundPhase.Playing) return;
            if (!Players.Exists(p => p.team == Team.Hider && p.alive) && Eggs.Count == 0) Finish(Team.Seeker, WinReason.AllAliensCaught);
            else if (Fish.TrueForAll(f => f.eaten || f.dead)) Finish(Team.Hider, WinReason.FoodGoalReached);
            else if (!Players.Exists(p => p.team == Team.Seeker)) Finish(Team.Hider, WinReason.DiversLeft);
            else if (Time >= EndsAt) Finish(Team.Seeker, WinReason.TimeExpired);
        }

        private void Finish(Team team, WinReason reason)
        {
            foreach (var p in Players) if (p.swallowedBy != 0) Release(p, Player(p.swallowedBy));
            foreach (var p in Players) p.velocity = new Point();
            Winner = (int)team; WinReason = reason; Phase = RoundPhase.Finished; FinishedAt = Time; ReviewEndsAt = Time + ReviewSeconds;
            inputs.Clear(); inputTimes.Clear();
        }

        public AquariumSnapshot Snapshot()
        {
            int eaten = 0, killed = 0;
            foreach (var fish in Fish)
            {
                if (fish.eaten) eaten++;
                else if (fish.dead) killed++;
            }
            return new AquariumSnapshot { sequence = ++sequence, round = Round, time = Time, huntAt = HuntAt, endsAt = EndsAt, winner = Winner, phase = Phase,
                winReason = WinReason, reviewEndsAt = ReviewEndsAt, finishedAt = FinishedAt, target = Fish.Count - killed, killed = killed,
                remaining = Fish.Count - killed - eaten, eaten = eaten, players = Players.ToArray(), fish = Fish.ToArray(), harpoons = Harpoons.ToArray(), effects = Effects.ToArray(), eggs = Eggs.ToArray() };
        }
    }
}

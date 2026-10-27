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
        public bool ready, alive = true, bot, filled;
        public Point position, velocity, facing = new Point(0, 1);
        public Point swallowedFrom;
        public int species = -1, swallowedBy, deathSpecies = -1, swallowedSpecies = -1;
        public float releaseAt, revealUntil, nextAction, nextMorph, nextFeed, stunnedUntil;
        public float stamina = 100, lastMealAt, nextScan, scanUntil, morphLockedUntil, exposureSeconds, diedAt;
        public int personalEaten, score, exposureScore;
        // R25: one revival egg per EggFoodRequired fish eaten; this counts the eggs already laid.
        public int eggsLaid;
        public string deathReason = "";
        public string visualAction = "";
        public float visualAt = -100, deathAt = -100, morphAt = -100;
        public int visualTarget, releaseSource, morphFrom = -1;
        // A fish-disguised carrier that gulped an ally stays a fish until it has spat it out.
        public float revealAt;
        // Diver round opening: stands on the dive deck and enters the water, finishing at this time (0 = none).
        public float entryAt;
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
        public int eater, harpoonOwner;
        public float turnAt;
        [NonSerialized] public Point steering;
        [NonSerialized] public float swimSpeed;
        // R23 habit state (host only): a lurking shark or a bottom flatfish alternates moving and resting.
        [NonSerialized] public bool resting;
        [NonSerialized] public float habitUntil;
        // R25: the anemone a clownfish keeps to (AquariumAnemoneLayout index), and its current hover spot.
        [NonSerialized] public int home = -1;
        [NonSerialized] public Point hover;
        // R26: the anemone or coral head it is visiting now (AquariumAnemoneLayout spot), and the feel of
        // the current little swim (speed share, turn rate) plus a per-fish wobble phase.
        [NonSerialized] public int spot = -1;
        [NonSerialized] public float speedFactor, turnFactor, wobblePhase;
    }

    [Serializable]
    public class Harpoon
    {
        public int id, owner;
        public Point position, direction;
        public float life = 0.75f;
        public float born, returnAt, retrievedAt;
        public bool returning;
        public Point returnFrom, returnDirection;
        public int caughtFish, caughtPlayer;
        public Point catchOffset;
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
        public bool slow, action, morph, feed, sprint, scan, layEgg, unmorph;
        public int morphTargetId;
        public bool morphTargetPlayer;
        public SwimInput Copy() { return (SwimInput)MemberwiseClone(); }
    }

    // The host owns all rules. This class has no Unity or networking dependency.
    public sealed class AquariumModel
    {
        // R25: one egg per 10 fish eaten (was a single egg after 3).
        public const int MaxPlayers = 5, FishCount = 32, EggFoodRequired = 10;
        public const float FishSpeed = 4f, HiderSpeed = FishSpeed, SeekerSpeed = 4.7f, RoundSeconds = 180f;
        public const float HarpoonCooldown = 3f, FeedCooldown = 20f, FeedRadius = 6f, FeedDuration = 5f;
        public const float DiverSideSpeed = .85f, DiverBackSpeed = .70f;
        // The spear flies 5 m at most, then the line reels it back along a slack, swinging curve.
        public const float HarpoonSpeed = 20f, HarpoonRange = 5f, HarpoonTension = .22f;
        public const float HungerSeconds = 60f, EatStun = 2f, AllyStun = 3f, MorphLock = 6f, HatchSeconds = 12f;
        // R25: an alien can only eat once its hunger gauge has dropped below this fraction.
        public const float FeedingHungerFraction = .5f;
        public static float HungerLeft(Swimmer p, float time) { return Math.Max(0, Math.Min(HungerSeconds, HungerSeconds - (time - p.lastMealAt))); }
        public static bool HungryEnough(Swimmer p, float time) { return HungerLeft(p, time) < HungerSeconds * FeedingHungerFraction; }
        public static int EggsReady(Swimmer p) { return Math.Max(0, p.personalEaten / EggFoodRequired - p.eggsLaid); }
        public const float ScanDuration = 5f, ScanCooldown = 18f, MaxStamina = 100f;
        public const float TraceDuration = 9f, ReviewSeconds = 20f;
        // R18: faster meals. The fish gulp is greedy and quick; the alien tentacle meal runs 1.32x faster.
        public const float FishFeedingSeconds = 2.4f, TentacleFeedingSeconds = 2.3f, SwallowVisualSeconds = 2.45f;
        // The eater turns to the meal and closes in; the meal itself is never moved.
        public const float FishLungeReach = 1.2f, TentacleLungeReach = .9f, LungeSeconds = .45f, GulpRevealDelay = .75f;
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
        // Divers open the round on the dive deck and stride into the water as the hunt begins.
        public bool DiveEntry;
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
                p.entryAt = 0;
                p.alive = true; p.swallowedBy = 0; p.swallowedSpecies = -1; p.species = p.bot && p.team == Team.Hider ? 0 : -1;
                p.releaseAt = p.revealUntil = p.nextAction = p.nextMorph = p.nextFeed = p.stunnedUntil = 0;
                p.visualAction = ""; p.visualAt = p.deathAt = p.morphAt = -100; p.visualTarget = 0; p.revealAt = 0;
                p.stamina = MaxStamina; p.lastMealAt = HuntAt; p.personalEaten = p.score = p.exposureScore = 0;
                p.eggsLaid = 0; p.nextScan = p.scanUntil = p.morphLockedUntil = p.exposureSeconds = 0; p.deathReason = "";
                p.velocity = new Point(); p.diedAt = 0; p.deathSpecies = -1;
                p.position = Constrain(p.team == Team.Hider ? AquariumMapOutline.LayoutPoint(-8 + hider++ * 2, 2, -1) : AquariumMapOutline.LayoutPoint(10, 2, -4 + seeker++ * 2), p.team == Team.Seeker ? .5f : .4f);
                p.facing = new Point(p.team == Team.Hider ? 1 : -1, 0);
                if (DiveEntry && p.team == Team.Seeker)
                {
                    p.entryAt = HuntAt; p.position = AquariumDiveEntry.At(0, seeker - 1); p.facing = AquariumDiveEntry.Facing;
                }
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
                p.ready = p.bot || p.id == 1; p.alive = true; p.swallowedBy = 0; p.swallowedSpecies = -1; p.species = -1; p.revealAt = 0; p.entryAt = 0;
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
                input.action |= old.action; input.morph |= old.morph; input.feed |= old.feed; input.scan |= old.scan; input.layEgg |= old.layEgg; input.unmorph |= old.unmorph;
            }
            inputs[id] = input.Copy(); inputTimes[id] = Time;
        }

        private static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
        private static bool ValidComponent(float n) { return Finite(n) && Math.Abs(n) <= 1000; }
        private float Range(float a, float b) { return a + (float)random.NextDouble() * (b - a); }
        private void ResetFish()
        {
            Fish.Clear();
            int anemoneFish = 0;
            for (int i = 0; i < FishCount; i++)
            {
                int species = i % SpeciesCount; var habit = AquariumFishTraits.Habit(species);
                var at = AquariumMapOutline.LayoutPoint(Range(-12, 12), Range(1.2f, WaterHeight-1.2f), Range(-7, 7));
                var facing = new Point(Range(-1, 1), Range(-0.25f, 0.25f), Range(-1, 1)).Normalized;
                // R23: a flatfish starts lying flat on the sand.
                if (habit == FishHabit.Bottom) { at.y = AquariumSeabed.Height(at) + AquariumFishTraits.BottomRest(species); facing = Flat(facing); }
                var fish = new Prey { id = 100 + i, species = species, position = Constrain(at, AquariumFishTraits.WallRadius(species)), facing = facing, turnAt = Time + Range(1, 4) };
                if (habit == FishHabit.Bottom) { fish.resting = true; fish.habitUntil = Time + Range(1f, 12f); }
                // R25: a clownfish starts at its own anemone.
                if (habit == FishHabit.Anemone && AquariumAnemoneLayout.Points.Length > 0)
                {
                    fish.home = fish.spot = AquariumAnemoneLayout.HomeFor(anemoneFish++);
                    fish.position = fish.hover = Constrain(AquariumAnemoneLayout.HoverAround(fish.home, random), AquariumFishTraits.WallRadius(species));
                    fish.wobblePhase = Range(0, 100); fish.habitUntil = Time + Range(2f, 8f); fish.speedFactor = .5f; fish.turnFactor = 4;
                }
                Fish.Add(fish);
            }
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
                if (f.dead)
                {
                    bool tethered = false;
                    foreach (var h in Harpoons) if (h.caughtFish == f.id) { tethered = true; break; }
                    if (!tethered) f.position = Constrain(f.position + new Point(0, -0.8f * dt, 0), AquariumFishTraits.WallRadius(f.species));
                    continue;
                }
                TickFishHabit(f, dt);
            }
            if (Phase != RoundPhase.Playing) { AquariumBodyCollision.Resolve(Players,Fish,Time); return; }
            // A carrier that gulped an ally while disguised loses the disguise after the spit.
            foreach (var p in Players) if (p.revealAt > 0 && Time >= p.revealAt)
            {
                p.revealAt = 0;
                if (p.alive && p.species >= 0) { p.morphFrom = p.species; p.species = -1; p.revealUntil = Math.Max(p.revealUntil, Math.Max(p.morphLockedUntil, Time + 0.65f)); }
            }
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
                if (p.entryAt > 0 && Time < p.entryAt) { TickDiveEntry(p, dt); continue; }
                if (TickReleaseKick(p,dt)) continue;
                bool feeding = TickFeedingLunge(p, dt);
                SwimInput input;
                if (p.bot) input = BotInput(p);
                else if (!inputs.TryGetValue(p.id, out input) || Time - inputTimes[p.id] > 0.4f) input = new SwimInput();
                var move = new Point(input.x, input.y, input.z);
                if (move.Length > 1) move = move.Normalized;
                var aim = new Point(input.aimX, input.aimY, input.aimZ);
                if (aim.Length > 0.1f && !feeding) p.facing = aim.Normalized;
                if ((p.team == Team.Seeker && Time < HuntAt) || Time < p.stunnedUntil)
                { p.velocity = new Point(); input.action = input.morph = input.feed = input.scan = input.layEgg = input.unmorph = false; p.stamina = Math.Min(MaxStamina, p.stamina + 18f * dt); continue; }
                // R24: a disguised alien swims at its fish's own speed.
                float fullSpeed = p.team == Team.Seeker ? SeekerSpeed : HiderSpeed * (p.species >= 0 ? AquariumFishTraits.Speed(p.species) : 1);
                float speed = p.team == Team.Seeker ? SeekerSpeed : (input.slow ? Math.Min(1.65f, fullSpeed) : fullSpeed);
                // Divers can swim in every direction; fins push hardest forward, a little less sideways and backwards.
                if (p.team == Team.Seeker && move.Length > 0.01f && p.facing.Length > 0.01f)
                {
                    float along = Point.Dot(move.Normalized, p.facing.Normalized);
                    speed *= along >= 0 ? DiverSideSpeed + (1 - DiverSideSpeed) * along : DiverSideSpeed + (DiverBackSpeed - DiverSideSpeed) * -along;
                }
                bool sprinting = input.sprint && move.Length > 0.01f && p.stamina > 0;
                if (sprinting) speed *= 1f + 0.6f * Math.Min(1f, p.stamina / (25f * dt));
                // A short acceleration/deceleration ramp smooths input changes without accumulating wall drift.
                var velocityChange = move * speed - p.velocity;
                float accelerationStep = 32f * dt;
                p.velocity += velocityChange.Length <= accelerationStep ? velocityChange : velocityChange.Normalized * accelerationStep;
                var wanted = p.position + p.velocity * dt;
                var movedTo = AquariumBodyCollision.Move(p, p.velocity * dt, Players, Fish, Time,Harpoons);
                if ((wanted - movedTo).Length > 0.001f) p.velocity = new Point();
                bool moved = Point.Dot(movedTo - p.position, move) > 0.001f;
                if (sprinting && moved) p.stamina = Math.Max(0, p.stamina - 25f * dt);
                else if (!input.sprint || move.Length <= 0.01f || !moved) p.stamina = Math.Min(MaxStamina, p.stamina + 18f * dt);
                p.position = movedTo;
                if (input.morph && p.team == Team.Hider && Time >= p.nextMorph && Time >= p.morphLockedUntil) Morph(p, input);
                // R25: C drops the disguise and returns to the alien form.
                if (input.unmorph && p.team == Team.Hider) Unmorph(p);
                if (input.action && Time >= p.nextAction && (p.team != Team.Hider || Time >= HuntAt))
                {
                    if (p.team == Team.Hider) Bite(p); else Shoot(p);
                }
                if (input.feed && p.team == Team.Seeker && Time >= p.nextFeed) Feed(p);
                if (input.scan && p.team == Team.Seeker && Time >= p.nextScan)
                { p.nextScan = Time + ScanCooldown; p.scanUntil = Time + ScanDuration; Effect("scan", p.position, p.facing, ScanDuration); }
                if (input.layEgg && p.team == Team.Hider && Time >= p.stunnedUntil) LayEgg(p);
                input.action = input.morph = input.feed = input.scan = input.layEgg = input.unmorph = false;
            }
            AquariumBodyCollision.Resolve(Players,Fish,Time,Harpoons);
            TickHarpoons(dt);
            TickEggs(dt);
            TickExposure(dt);
            CheckWinner();
        }

        // The scripted deck entry: the host moves the diver along the shared path (no input, no collisions).
        private void TickDiveEntry(Swimmer p, float dt)
        {
            int slot = 0; foreach (var other in Players) { if (other == p) break; if (other.team == Team.Seeker) slot++; }
            float t = Time - (p.entryAt - AquariumDiveEntry.Seconds);
            p.position = AquariumDiveEntry.At(t, slot); p.velocity = AquariumDiveEntry.Velocity(t, slot); p.facing = AquariumDiveEntry.Facing;
            p.stamina = MaxStamina;
            if (t >= AquariumDiveEntry.SplashAt && t - dt < AquariumDiveEntry.SplashAt)
                Effect("splash", AquariumDiveEntry.SplashPoint(slot), AquariumDiveEntry.Facing, 2.4f, p.id);
            inputs.Remove(p.id); inputTimes.Remove(p.id);
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
                action = p.team == Team.Hider ? closest < 1.72f : closest < HarpoonRange, morph = p.species < 0,
                feed = p.team == Team.Seeker && Fish.Exists(f => !f.dead && !f.eaten && (f.position - p.position).Length < FeedRadius),
                layEgg = p.team == Team.Hider && EggsReady(p) > 0,
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

        // R25: drop the disguise on demand (same reveal as a forced one: the view morphs back to the alien).
        private void Unmorph(Swimmer p)
        {
            if (p.species < 0 || !p.alive || p.swallowedBy != 0 || Time < p.stunnedUntil) return;
            p.morphFrom = p.species; p.morphAt = Time; p.species = -1;
            p.revealUntil = Math.Max(p.revealUntil, Time + 0.65f); p.nextMorph = Math.Max(p.nextMorph, Time + 0.5f);
            Effect("morph", p.position, p.facing, 0.65f);
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

        private bool InBite(Swimmer p, Point target, float best, float bonus = 0)
        {
            var delta = target - p.position;
            return delta.Length - bonus < best && (delta.Length < 0.3f + Math.Max(0, bonus) || Point.Dot(delta.Normalized, p.facing) > 0.25f) && SolidHit(p.position, target) > 1;
        }
        // R23: bigger bodies (eater or meal) are bitten from further away centre to centre.
        private float BiteBonus(Swimmer eater, int mealSpecies)
        { return AquariumFishTraits.Reach(mealSpecies) + (HasFishDisguise(eater, Time) ? Math.Max(0, AquariumFishTraits.Reach(eater.species)) : 0); }

        public static bool HasFishDisguise(Swimmer p, float time)
        { return p.team == Team.Hider && p.alive && p.species >= 0 && p.revealUntil <= time; }

        private void Bite(Swimmer p)
        {
            if (Time < HuntAt) return;
            // R25: no eating while the hunger gauge is still above half.
            if (!HungryEnough(p, Time)) return;
            p.nextAction = Time + 0.65f;
            if (Players.Exists(h => h.swallowedBy == p.id)) return;
            Swimmer friend = null; Prey prey = null; float distance = 1.78f;
            // Only an ally that looks like a fish can be mistaken for food: aliens in their own form are never eaten.
            foreach (var h in Players)
                if (h.id != p.id && h.team == Team.Hider && h.alive && h.swallowedBy == 0 && HasFishDisguise(h, Time) && !Players.Exists(c => c.swallowedBy == h.id) && InBite(p, h.position, distance, BiteBonus(p, h.species)))
                { friend = h; distance = (h.position - p.position).Length - BiteBonus(p, h.species); }
            foreach (var f in Fish) if (!f.eaten && !f.dead && InBite(p, f.position, distance, BiteBonus(p, f.species)))
            { prey = f; friend = null; distance = (f.position - p.position).Length - BiteBonus(p, f.species); }
            Effect("bite", p.position + p.facing * 0.8f, p.facing, 0.3f);
            if (friend != null)
            {
                // A disguised carrier gulps the ally as a fish; the ally struggles inside
                // until it is spat out. An alien-form carrier keeps the tentacle meal.
                bool gulp = HasFishDisguise(p, Time);
                FaceMeal(p, friend.position);
                p.stunnedUntil = friend.stunnedUntil = Time + AllyStun;
                p.velocity = friend.velocity = new Point();
                p.morphLockedUntil = friend.morphLockedUntil = Time + MorphLock;
                p.visualAction = gulp ? "AllyGulp" : "TentacleEat"; p.visualAt = Time; p.visualTarget = 0;
                friend.visualAction = "Eaten"; friend.visualAt = Time;
                // Preserve the visible disguise through capture; the forced reveal
                // happens out of sight inside the carrier before expulsion.
                friend.swallowedSpecies = friend.species >= 0 && (friend.revealUntil <= Time || Time-friend.morphAt < 1.4f) ? friend.species : -1;
                friend.species = -1; friend.revealUntil = Time + MorphLock;
                if (gulp) p.revealAt = Time + AllyStun + GulpRevealDelay;
                else { p.species = -1; p.revealUntil = Time + MorphLock; }
                p.score -= 150;
                Effect("allyEat",p.position,p.facing,TraceDuration,p.id);
                friend.swallowedFrom = friend.position; friend.swallowedBy = p.id; friend.releaseAt = Time + AllyStun; friend.position = p.position;
            }
            else if (prey != null)
            {
                prey.eaten = true; prey.eater = p.id; prey.visualAt = Time;
                FaceMeal(p, prey.position);
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
            if (carrier != null)
            {
                // A disguised carrier spits as a fish and is exposed right after the spit.
                if (carrier.revealAt > 0 && carrier.alive) carrier.revealAt = Time + GulpRevealDelay;
                else carrier.revealUntil = Math.Max(carrier.revealUntil, carrier.morphLockedUntil);
                carrier.nextAction = Math.Max(carrier.nextAction, Time + 0.8f);
            }
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
        private static void FaceMeal(Swimmer p, Point meal)
        { var toward = meal - p.position; if (toward.Length > .01f) p.facing = toward.Normalized; }
        private static bool IsMeal(string motion) { return motion == "Bite" || motion == "TentacleEat" || motion == "AllyGulp"; }
        // The eater keeps turning to its meal while stunned and closes the gap quickly.
        // Returns true while the meal owns the eater's facing.
        private bool TickFeedingLunge(Swimmer p, float dt)
        {
            if (Time >= p.stunnedUntil || !IsMeal(p.visualAction)) return false;
            Point meal; bool found = false; meal = new Point(); int mealSpecies = -1;
            if (p.visualTarget != 0)
                foreach (var f in Fish) if (f.id == p.visualTarget && f.eaten && f.eater == p.id) { meal = f.position; mealSpecies = f.species; found = true; break; }
            if (!found)
                foreach (var h in Players) if (h.swallowedBy == p.id) { meal = h.swallowedFrom; mealSpecies = h.swallowedSpecies; found = true; break; }
            if (!found) return true;
            var delta = meal - p.position; float distance = delta.Length;
            if (distance < .001f) return true;
            var direction = delta / distance; p.facing = direction;
            if (Time - p.visualAt > LungeSeconds) return true;
            float reach = p.visualAction == "TentacleEat" ? TentacleLungeReach * AquariumFishTraits.AlienScale : FishLungeReach;
            // R23: stop at the meal's (and a big disguised eater's) skin, not at Fish0's.
            reach = Math.Max(.6f, reach + (mealSpecies >= 0 ? AquariumFishTraits.Reach(mealSpecies) : 0) + (p.visualAction == "TentacleEat" ? 0 : Math.Max(0, AquariumFishTraits.Reach(p.species))));
            float step = (distance - reach) * (1 - (float)Math.Exp(-dt * 13f));
            if (Math.Abs(step) > .0005f) p.position = AquariumBodyCollision.Move(p, direction * step, Players, Fish, Time);
            return true;
        }
        private static Point Flat(Point p) { var flat = new Point(p.x, 0, p.z); return flat.Length > .0001f ? flat.Normalized : new Point(0, 0, 1); }
        // R23/R24: each species keeps its own way of swimming (AquariumFishTraits.Habit) at its own
        // speed (AquariumFishTraits.Speed); sharks and mantas ignore the diver's food.
        private void TickFishHabit(Prey f, float dt)
        {
            var habit = AquariumFishTraits.Habit(f.species);
            if (habit == FishHabit.Anemone && f.home >= 0 && f.home < AquariumAnemoneLayout.Points.Length) { TickAnemoneFish(f, dt); return; }
            bool bottom = habit == FishHabit.Bottom;
            float wall = AquariumFishTraits.WallRadius(f.species);
            // A bottom dweller alternates a short scoot with a long, random rest on the sand.
            if (bottom && Time >= f.habitUntil)
            {
                f.resting = !f.resting;
                f.habitUntil = Time + (f.resting ? Range(6f, 16f) : Range(.6f, 1.5f));
                if (!f.resting) { f.steering = Flat(new Point(Range(-1, 1), 0, Range(-1, 1))); f.turnAt = f.habitUntil; }
            }
            if (Time > f.turnAt)
            {
                if (bottom) f.steering = Flat(f.facing + new Point(Range(-0.9f, 0.9f), 0, Range(-0.9f, 0.9f)));
                else if (habit == FishHabit.Cruise) f.steering = (f.facing + new Point(Range(-0.5f, 0.5f), Range(-0.15f, 0.15f), Range(-0.5f, 0.5f))).Normalized;
                else if (habit == FishHabit.Glide) f.steering = (f.facing + new Point(Range(-0.6f, 0.6f), Range(-0.12f, 0.12f), Range(-0.6f, 0.6f))).Normalized;
                else f.steering = (f.facing + new Point(Range(-0.8f, 0.8f), Range(-0.3f, 0.3f), Range(-0.8f, 0.8f))).Normalized;
                f.turnAt = Time + (habit == FishHabit.Cruise ? Range(3f, 7f) : habit == FishHabit.Glide ? Range(2.5f, 6f) : Range(1.4f, 4.5f));
            }
            Point heading = f.steering.Length > .0001f ? f.steering : f.facing;
            WaterEffect food = null; float foodDistance = FeedRadius;
            if (!AquariumFishTraits.IgnoresFeed(f.species))
                foreach (var e in Effects)
                    if (e.kind == "feed" && (e.position - f.position).Length < foodDistance)
                    { food = e; foodDistance = (e.position - f.position).Length; }
            bool still = bottom && f.resting;
            if (food != null)
            {
                // Food wakes even a resting flatfish.
                if (still) { f.resting = false; f.habitUntil = Time + 1.5f; still = false; }
                if (foodDistance > 0.15f) heading = (food.position - f.position).Normalized;
            }
            else if (Phase == RoundPhase.Playing && Time >= HuntAt)
            {
                // A flatfish trusts its camouflage until a diver is almost on top of it.
                float alarm = bottom ? 2.2f : 4.5f;
                foreach (var diver in Players) if (diver.team == Team.Seeker && diver.alive && (diver.position - f.position).Length < alarm)
                {
                    heading = (f.position - diver.position).Normalized;
                    if (still) { f.resting = false; f.habitUntil = Time + Range(.8f, 1.6f); still = false; }
                    break;
                }
            }
            if (bottom) heading = Flat(heading);
            float turnRate = habit == FishHabit.Cruise ? 1.3f : habit == FishHabit.Glide ? 1.6f : 2.8f;
            if (!still) f.facing = Point.TurnTowards(f.facing, heading, turnRate * dt);
            if (bottom) f.facing = Flat(f.facing);
            float targetSpeed = still ? 0 : FishSpeed * AquariumFishTraits.Speed(f.species);
            float speedStep = (still ? 2.5f : 4f) * dt; f.swimSpeed += Math.Max(-speedStep, Math.Min(speedStep, targetSpeed - f.swimSpeed));
            float travel = f.swimSpeed * dt;
            if (food != null) travel = Math.Min(travel, Math.Max(0, foodDistance - .15f));
            var wanted = f.position + f.facing * travel;
            if (bottom) wanted.y = AquariumSeabed.Height(wanted) + AquariumFishTraits.BottomRest(f.species);
            var actual = Constrain(wanted, wall);
            if ((wanted - actual).Length > 0.001f)
            {
                var away = actual - wanted; if (bottom) away = new Point(away.x, 0, away.z);
                if (away.Length > .0001f) { f.steering = away.Normalized; f.turnAt = Time + 0.9f; }
            }
            f.position = actual;
        }
        // R25/R26: a clownfish lives around its anemone but roams its neighbourhood: every few seconds it
        // picks a spot (home is favoured, otherwise another anemone or coral head within
        // AquariumAnemoneLayout.Roam) and fusses about there in an irregular mix of short darts, slow drifts,
        // pauses and nestling into an anemone, with a wobbling, never-straight path. A diver sends it into the
        // nearest anemone of its patch; it only takes food that lands beside the spot it is visiting.
        private static Point Wobble(Point d, float yaw, float pitch)
        { float c = (float)Math.Cos(yaw), sn = (float)Math.Sin(yaw); return new Point(d.x * c - d.z * sn, d.y + pitch, d.x * sn + d.z * c).Normalized; }
        private void TickAnemoneFish(Prey f, float dt)
        {
            var patch = AquariumAnemoneLayout.Territory(f.home);
            float wall = AquariumFishTraits.WallRadius(f.species);
            if (f.spot < 0 || f.spot >= AquariumAnemoneLayout.SpotCount) f.spot = f.home;
            if (Time >= f.habitUntil)
            {
                f.spot = patch.Length == 0 || Range(0, 1) < .4f ? f.home : patch[random.Next(patch.Length)];
                f.habitUntil = Time + Range(3f, 10f); f.turnAt = Time;
            }
            var spot = AquariumAnemoneLayout.Spot(f.spot); bool anemone = AquariumAnemoneLayout.IsAnemone(f.spot);
            if (Time > f.turnAt || !f.resting && (f.hover - f.position).Length < .2f)
            {
                float roll = Range(0, 1);
                if (roll < .22f) { f.resting = true; f.speedFactor = 0; f.turnAt = Time + Range(.3f, 1.4f); }
                else if (anemone && roll < .36f)
                {
                    f.resting = false; f.speedFactor = Range(.35f, .6f); f.turnAt = Time + Range(1f, 2.6f);
                    f.hover = spot + new Point(Range(-.12f, .12f), Range(.2f, .35f), Range(-.12f, .12f));
                }
                else
                {
                    f.resting = false; f.speedFactor = roll > .9f ? Range(1f, 1.25f) : Range(.3f, .9f); f.turnAt = Time + Range(.4f, 2.4f);
                    f.hover = Constrain(AquariumAnemoneLayout.HoverAround(f.spot, random), wall);
                }
                f.turnFactor = Range(2.5f, 6.5f);
            }
            Point target = f.hover; bool hurry = false, still = f.resting;
            WaterEffect food = null; float foodDistance = FeedRadius;
            foreach (var e in Effects)
                if (e.kind == "feed" && (e.position - f.position).Length < foodDistance && (e.position - spot).Length < AquariumAnemoneLayout.Leash + 1.5f)
                { food = e; foodDistance = (e.position - f.position).Length; }
            if (food != null) { target = food.position; still = false; }
            else if (Phase == RoundPhase.Playing && Time >= HuntAt)
                foreach (var diver in Players) if (diver.team == Team.Seeker && diver.alive && (diver.position - f.position).Length < 4.5f)
                {
                    // Into the nearest anemone of its patch.
                    int hide = f.home; float best = (AquariumAnemoneLayout.Spot(f.home) - f.position).Length;
                    foreach (int i in patch) if (AquariumAnemoneLayout.IsAnemone(i) && (AquariumAnemoneLayout.Spot(i) - f.position).Length < best) { best = (AquariumAnemoneLayout.Spot(i) - f.position).Length; hide = i; }
                    target = AquariumAnemoneLayout.Spot(hide) + new Point(0, .3f, 0); hurry = true; still = false;
                    break;
                }
            var delta = target - f.position; float distance = delta.Length;
            float t = Time + f.wobblePhase;
            float yaw = .55f * (float)Math.Sin(t * 2.3f) + .3f * (float)Math.Sin(t * 5.7f + 1.3f) + .15f * (float)Math.Sin(t * 11.1f + .4f);
            float pitch = .14f * (float)Math.Sin(t * 3.1f + .7f) + .06f * (float)Math.Sin(t * 7.3f);
            if (still)
            {
                // A pause is never quite still: small fin-driven yaw and bob.
                f.facing = Point.TurnTowards(f.facing, Wobble(f.facing, yaw * .5f, pitch * .5f), 1.2f * dt);
            }
            else
            {
                var desired = distance > .001f ? delta / distance : f.facing;
                if (!hurry && distance > .25f) desired = Wobble(desired, yaw * Math.Min(1, distance), pitch);
                f.facing = Point.TurnTowards(f.facing, desired, (hurry ? 6.5f : f.turnFactor) * dt);
            }
            float full = FishSpeed * AquariumFishTraits.Speed(f.species);
            float targetSpeed = hurry ? full : still ? full * .05f : full * f.speedFactor * Math.Min(1, distance / .8f + .2f);
            float speedStep = 7f * dt; f.swimSpeed += Math.Max(-speedStep, Math.Min(speedStep, targetSpeed - f.swimSpeed));
            float travel = f.swimSpeed * dt;
            if (!still) travel = Math.Min(travel, distance + .05f);
            if (food != null) travel = Math.Min(travel, Math.Max(0, foodDistance - .15f));
            var wanted = f.position + f.facing * travel;
            var actual = Constrain(wanted, wall);
            if ((wanted - actual).Length > .001f)
            {
                f.hover = Constrain(AquariumAnemoneLayout.HoverAround(f.spot, random), wall); f.turnAt = Time + Range(.4f, 1.6f);
                // Never pop through the reef: a resolve that lands far from the swim step is refused.
                if ((actual - f.position).Length > travel * 2 + .05f) actual = f.position;
            }
            f.position = actual;
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
            if (travel>0) player.position=AquariumBodyCollision.Move(player,player.facing*(travel*sign),Players,Fish,Time,Harpoons);
            // The impulse has priority over held movement/aim; action edges are not queued.
            SwimInput blocked;
            if (inputs.TryGetValue(player.id,out blocked)) blocked.action=blocked.morph=blocked.feed=blocked.scan=blocked.layEgg=blocked.unmorph=false;
            player.stamina=Math.Min(MaxStamina,player.stamina+18f*dt);
            return true;
        }

        private void Shoot(Swimmer p)
        {
            p.nextAction = Time + HarpoonCooldown;
            Harpoons.Add(new Harpoon { id = ++nextHarpoon, owner = p.id, position = p.position + p.facing * 0.7f, direction = p.facing,
                born = Time, retrievedAt = p.nextAction, life = HarpoonRange / HarpoonSpeed });
            Effect("shot", p.position + p.facing, p.facing, 0.2f);
        }

        private void Feed(Swimmer p)
        {
            p.nextFeed = Time + FeedCooldown;
            Effect("feed", Constrain(p.position + p.facing * 1.2f, 0.35f), p.facing, FeedDuration);
        }

        // R25: every EggFoodRequired fish eaten earns one egg. It can be laid with nobody dead; its
        // hatch countdown starts when it is laid, and it revives the first fallen ally after that.
        private void LayEgg(Swimmer owner)
        {
            if (EggsReady(owner) <= 0) return;
            owner.eggsLaid++;
            var target = Players.Find(p => p.team == Team.Hider && !p.alive && !Eggs.Exists(e => e.revivePlayer == p.id));
            var egg = new RevivalEgg { id = ++nextEgg, owner = owner.id, revivePlayer = target != null ? target.id : 0,
                position = Constrain(owner.position - owner.facing * 0.7f, EggRadius), hatchAt = Time + HatchSeconds };
            Eggs.Add(egg); Effect("egg", egg.position, owner.facing, 0.8f);
        }

        private void TickEggs(float dt)
        {
            for (int i = Eggs.Count - 1; i >= 0; i--)
            {
                var egg = Eggs[i]; var target = egg.revivePlayer != 0 ? Player(egg.revivePlayer) : null;
                // R25: an egg waits for a fallen ally instead of disappearing.
                if (target == null || target.alive)
                {
                    target = Players.Find(p => p.team == Team.Hider && !p.alive && !Eggs.Exists(e => e != egg && e.revivePlayer == p.id));
                    egg.revivePlayer = target != null ? target.id : 0;
                }
                // Use the shared terrain collision so eggs settle on sand or reef surfaces on every client.
                var fallingTo = egg.position + new Point(0, -EggSinkSpeed * dt, 0);
                float surface = ObstacleHit(egg.position, fallingTo, EggRadius);
                egg.position += (fallingTo - egg.position) * Math.Min(1, Math.Max(0, surface));
                if (Time < egg.hatchAt || target == null) continue;
                target.alive = true; target.position = egg.position; target.species = -1; target.swallowedBy = 0; target.swallowedSpecies = -1;
                target.visualAction = ""; target.visualAt = target.deathAt = target.morphAt = -100; target.revealAt = 0;
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
            player.revealAt = 0;
            player.alive = false; player.deathAt = Time; player.visualAt = Time; player.visualAction = reason == "harpoon" ? "Harpoon" : "Eaten"; player.deathReason = reason; player.swallowedBy = 0;
            inputs.Remove(player.id); inputTimes.Remove(player.id); ReleaseCarried(player.id);
            Effect(reason == "starved" ? "starved" : "caught", player.position, player.facing, 1.2f);
        }

        private void TickHarpoons(float dt)
        {
            for (int i = Harpoons.Count - 1; i >= 0; i--)
            {
                var h = Harpoons[i]; var owner = Player(h.owner);
                if (owner == null || !owner.alive) { Harpoons.RemoveAt(i); continue; }
                if (h.returning)
                {
                    // The host keeps a spent spear until the cooldown ends. It cannot hit twice.
                    ReelHarpoon(h, owner);
                    MoveHarpoonCatch(h);
                    if (Time >= h.retrievedAt) Harpoons.RemoveAt(i);
                    continue;
                }
                if (Time >= h.retrievedAt) { Harpoons.RemoveAt(i); continue; }
                var start = h.position; var end = start + h.direction * (HarpoonSpeed * Math.Min(dt, h.life));
                h.life -= dt;
                float first = 2f; Swimmer hitPlayer = null; Prey hitFish = null; RevivalEgg hitEgg = null;
                // Segment collision prevents fast harpoons crossing thin targets between ticks.
                foreach (var p in Players) if (p.id != h.owner && p.team == Team.Hider && p.alive && p.swallowedBy == 0)
                { var t = PlayerHitTime(start, end, p, Time); if (t < first) { first = t; hitPlayer = p; hitFish = null; } }
                foreach (var f in Fish) if (!f.eaten && !f.dead)
                { var t = FishHitTime(start, end, f); if (t < first) { first = t; hitFish = f; hitPlayer = null; } }
                foreach (var egg in Eggs)
                { var t = HitTime(start, end, egg.position, 0.45f); if (t < first) { first = t; hitEgg = egg; hitFish = null; hitPlayer = null; } }
                var obstruction = SolidHit(start, end);
                if (obstruction < first) { first = obstruction; hitFish = null; hitPlayer = null; hitEgg = null; }
                if (first <= 1f)
                {
                    h.position = start + (end - start) * first;
                    if (hitPlayer != null)
                    {
                        Kill(hitPlayer, "harpoon"); h.caughtPlayer = hitPlayer.id; h.catchOffset = CatchOffset(hitPlayer.position, h.position, h.direction, .78f);
                        // The same yellow ink as an alien eating an alien.
                        Effect("harpoonAlien", h.position, h.direction, TraceDuration);
                    }
                    else if (hitFish != null)
                    {
                        hitFish.dead = true; hitFish.visualAt = Time; hitFish.harpoonOwner = h.owner;
                        h.caughtFish = hitFish.id; h.catchOffset = CatchOffset(hitFish.position, h.position, h.direction, .6f);
                        // The same dark purple ink as an ordinary fish being eaten.
                        Effect("harpoonFish", h.position, h.direction, TraceDuration);
                    }
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
        { h.returning = true; h.returnAt = Time; h.returnFrom = h.position; h.returnDirection = h.direction; }

        // The line first snaps tight (the spear hangs and swings round to trail the line), then reels the spear
        // home along a slack curve: it sags under the line and swings a little to one side, speeding up and
        // slowing again near the gun. The view lines the last stretch up with the barrel and slides it in.
        private void ReelHarpoon(Harpoon h, Swimmer owner)
        {
            float span = Math.Max(.01f, h.retrievedAt - h.returnAt);
            float t = Math.Min(1, Math.Max(0, (Time - h.returnAt) / span));
            float tension = Math.Min(.35f, HarpoonTension / span);
            float pull = t <= tension ? 0 : (t - tension) / (1 - tension);
            float reel = pull * pull * pull * (pull * (pull * 6 - 15) + 10);
            var socket = HarpoonSocket(owner);
            var chord = socket - h.returnFrom; float length = chord.Length;
            var side = new Point(chord.z, 0, -chord.x); side = side.Length > .001f ? side.Normalized : new Point(1, 0, 0);
            if ((h.id & 1) == 1) side = side * -1;
            var control = h.returnFrom + chord * .5f + new Point(0, -Math.Min(.9f, .22f * length), 0) + side * (.12f * length);
            float a = 1 - reel;
            // While the line tightens the spear sinks a touch before it is pulled away.
            float settle = t <= tension ? (float)Math.Sin(Math.PI * .5f * t / tension) : 1 - reel;
            h.position = h.returnFrom * (a * a) + control * (2 * a * reel) + socket * (reel * reel) + new Point(0, -.10f * Math.Min(1, length / 2) * settle, 0);
            // A reeled spear trails its line: the tip points away from the way it is being pulled.
            var travel = (control - h.returnFrom) * (2 * a) + (socket - control) * (2 * reel);
            var trailing = travel.Length > .001f ? travel.Normalized * -1 : (h.position - owner.position).Normalized;
            float turn = Math.Min(1, t / Math.Max(.001f, tension * 1.6f)); turn = turn * turn * (3 - 2 * turn);
            h.direction = Slerp(h.returnDirection.Length > .01f ? h.returnDirection : trailing, trailing, turn);
        }

        // Where the reel delivers the spear: half a metre in front of the gun held at the diver's right side,
        // so the view only has to slide it straight back into the barrel.
        public static Point HarpoonSocket(Swimmer owner)
        {
            var forward = owner.facing.Length > .01f ? owner.facing.Normalized : new Point(0, 0, 1);
            var right = new Point(forward.z, 0, -forward.x); right = right.Length > .01f ? right.Normalized : new Point(1, 0, 0);
            return owner.position + forward * 1.9f + right * .62f + new Point(0, .07f, 0);
        }

        // Hold a speared body a little ahead of the spear tip so the spear tows it instead of sitting inside it.
        private static Point CatchOffset(Point centre, Point tip, Point flight, float clearance)
        {
            var offset = centre - tip;
            var away = offset.Length > .01f ? offset.Normalized : flight.Normalized;
            return away * Math.Max(offset.Length, clearance);
        }

        private static Point Slerp(Point from, Point to, float amount)
        {
            from = from.Normalized; to = to.Normalized;
            float angle = (float)Math.Acos(Math.Max(-1, Math.Min(1, Point.Dot(from, to))));
            return Point.TurnTowards(from, to, angle * Math.Max(0, Math.Min(1, amount)));
        }

        private void MoveHarpoonCatch(Harpoon h)
        {
            if (h.caughtFish != 0)
            {
                var fish = Fish.Find(f => f.id == h.caughtFish);
                if (fish != null && fish.dead && !fish.eaten) fish.position = h.position + h.catchOffset;
                else h.caughtFish = 0;
            }
            if (h.caughtPlayer != 0)
            {
                var player = Player(h.caughtPlayer);
                if (player != null && !player.alive && player.deathReason == "harpoon") player.position = h.position + h.catchOffset;
                else h.caughtPlayer = 0;
            }
        }

        // R23/R24: hit volumes follow the body sizes. A disguised alien is as big as the fish it copies;
        // the alien's own form is AlienScale. Shared by the host and the aiming camera.
        public static float FishHitTime(Point a, Point b, Prey f)
        { return HullHitTime(a, b, f.position, f.facing, .42f / AquariumBodyCollision.FishRadius * AquariumFishTraits.HullRadius(f.species), AquariumFishTraits.HullHalf(f.species)); }
        public static float PlayerHitTime(Point a, Point b, Swimmer p, float time)
        {
            return HasFishDisguise(p, time)
                ? HullHitTime(a, b, p.position, p.facing, .48f / AquariumBodyCollision.FishRadius * AquariumFishTraits.HullRadius(p.species), AquariumFishTraits.HullHalf(p.species))
                : HitTime(a, b, p.position, .48f * AquariumFishTraits.AlienScale);
        }
        // R23: a long body (shark) is hit along its length, not only near its centre.
        public static float HullHitTime(Point a, Point b, Point centre, Point facing, float radius, float half)
        {
            float t = HitTime(a, b, centre, radius);
            if (half <= .2f) return t;
            var axis = facing.Normalized * half;
            return Math.Min(t, Math.Min(HitTime(a, b, centre + axis, radius), HitTime(a, b, centre - axis, radius)));
        }
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
                        // R26: +2 mm so the projection lands just outside the rock instead of a rounding error inside it.
                        float horizontal = (RockRadii[i] + radius) * (float)Math.Sqrt(Math.Max(0, 1 - delta.y * delta.y)) + .002f;
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
                    // R26: keep a floor-hugging swimmer above the sand here; a candidate a hair under the
                    // padded seabed used to fail everywhere nearby and the search jumped metres away.
                    candidate.y=Math.Max(candidate.y,AquariumSeabed.Height(candidate)+radius+.002f);
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

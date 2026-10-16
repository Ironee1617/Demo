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
        public static float Dot(Point a, Point b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
    }

    [Serializable]
    public class Swimmer
    {
        public int id;
        public string name;
        public Team team;
        public bool ready, alive = true, bot, filled, eggUsed;
        public Point position, facing = new Point(0, 1);
        public int species = -1, swallowedBy;
        public float releaseAt, revealUntil, nextAction, nextMorph, nextFeed, stunnedUntil;
        public float stamina = 100, lastMealAt, nextScan, scanUntil, morphLockedUntil, exposureSeconds;
        public int personalEaten, score, exposureScore;
        public string deathReason = "";
    }

    [Serializable]
    public class Prey
    {
        public int id, species;
        public Point position, facing;
        public bool dead, eaten;
        public float turnAt;
    }

    [Serializable]
    public class Harpoon
    {
        public int id, owner;
        public Point position, direction;
        public float life = 0.75f;
    }

    [Serializable]
    public class WaterEffect
    {
        public int id;
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
        public float time, huntAt, endsAt, reviewEndsAt;
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
        public const float TraceDuration = 5f, ReviewSeconds = 20f;
        public const float HalfWidth = 14f, HalfDepth = 8.5f, WaterHeight = 7f, Floor = 0.15f, RockHalfHeight = 1.4f;
        public static readonly Point[] Rocks = { new Point(-6, 0.9f, -2), new Point(5, 0.9f, 3), new Point(1, 0.9f, -5) };
        public static readonly float[] RockRadii = { 1.15f, 1.35f, 0.8f };
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
        public WinReason WinReason { get; private set; }
        public int Winner { get; private set; } = -1;
        public int Round { get; private set; }
        private int nextEffect, nextHarpoon, nextEgg, sequence;
        private readonly Random random;
        private readonly Dictionary<int, SwimInput> inputs = new Dictionary<int, SwimInput>();
        private readonly Dictionary<int, float> inputTimes = new Dictionary<int, float>();

        public AquariumModel(int seed = 73)
        {
            random = new Random(seed);
            ResetFish();
        }

        public Swimmer Player(int id) { return Players.Find(p => p.id == id); }
        public Swimmer AddPlayer(int id, string name, Team team, bool bot = false)
        {
            if (Phase != RoundPhase.Lobby || Players.Count >= MaxPlayers || Player(id) != null) return null;
            var p = new Swimmer { id = id, name = CleanName(name), team = team, bot = bot, ready = bot || id == 1 };
            p.position = new Point(-9 + Players.Count * 2.4f, 2, 0);
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
            Phase = RoundPhase.Playing; Winner = -1; WinReason = WinReason.None; ReviewEndsAt = 0;
            HuntAt = Time + 5f; EndsAt = HuntAt + RoundSeconds;
            int hider = 0, seeker = 0;
            foreach (var p in Players)
            {
                p.alive = true; p.swallowedBy = 0; p.species = p.bot && p.team == Team.Hider ? 0 : -1;
                p.releaseAt = p.revealUntil = p.nextAction = p.nextMorph = p.nextFeed = p.stunnedUntil = 0;
                p.stamina = MaxStamina; p.lastMealAt = HuntAt; p.personalEaten = p.score = p.exposureScore = 0;
                p.eggUsed = false; p.nextScan = p.scanUntil = p.morphLockedUntil = p.exposureSeconds = 0; p.deathReason = "";
                p.position = p.team == Team.Hider ? new Point(-8 + hider++ * 2, 2, -1) : new Point(10, 2, -4 + seeker++ * 2);
                p.facing = new Point(p.team == Team.Hider ? 1 : -1, 0);
            }
            return true;
        }

        public void ReturnToLobby()
        {
            if (Phase != RoundPhase.Finished || Time < ReviewEndsAt) return;
            Phase = RoundPhase.Lobby; Winner = -1; WinReason = WinReason.None; ReviewEndsAt = 0;
            Harpoons.Clear(); Effects.Clear(); Eggs.Clear(); inputs.Clear(); inputTimes.Clear();
            Players.RemoveAll(p => p.filled);
            foreach (var p in Players)
            {
                p.ready = p.bot || p.id == 1; p.alive = true; p.swallowedBy = 0; p.species = -1;
                p.releaseAt = p.revealUntil = p.nextAction = p.nextMorph = p.nextFeed = p.stunnedUntil = 0;
                p.stamina = MaxStamina; p.nextScan = p.scanUntil = p.morphLockedUntil = 0; p.deathReason = "";
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
            if (inputs.TryGetValue(id, out old)) { input.action |= old.action; input.morph |= old.morph; input.feed |= old.feed; input.scan |= old.scan; input.layEgg |= old.layEgg; }
            inputs[id] = input.Copy(); inputTimes[id] = Time;
        }

        private static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
        private static bool ValidComponent(float n) { return Finite(n) && Math.Abs(n) <= 1000; }
        private float Range(float a, float b) { return a + (float)random.NextDouble() * (b - a); }
        private void ResetFish()
        {
            Fish.Clear();
            for (int i = 0; i < FishCount; i++)
                Fish.Add(new Prey { id = 100 + i, species = i % 3, position = Constrain(new Point(Range(-12, 12), Range(1, 5.6f), Range(-7, 7)), 0.35f),
                    facing = new Point(Range(-1, 1), Range(-0.25f, 0.25f), Range(-1, 1)).Normalized, turnAt = Time + Range(1, 4) });
        }

        public void Tick(float dt)
        {
            if (!Finite(dt) || dt <= 0 || dt > 0.1f) return;
            Time += dt;
            Effects.RemoveAll(e => Time >= e.born + e.duration);
            if (Phase == RoundPhase.Finished) return;
            foreach (var f in Fish)
            {
                if (f.eaten) continue;
                if (f.dead) { f.position = Constrain(f.position + new Point(0, -0.8f * dt, 0), 0.35f); continue; }
                if (Time > f.turnAt) { f.facing = (f.facing + new Point(Range(-0.8f, 0.8f), Range(-0.3f, 0.3f), Range(-0.8f, 0.8f))).Normalized; f.turnAt = Time + Range(1.4f, 4.5f); }
                WaterEffect food = null; float foodDistance = FeedRadius;
                foreach (var e in Effects)
                    if (e.kind == "feed" && (e.position - f.position).Length < foodDistance)
                    { food = e; foodDistance = (e.position - f.position).Length; }
                float travel = (1.4f + f.species * 0.18f) * dt;
                if (food != null)
                {
                    if (foodDistance > 0.15f) f.facing = (food.position - f.position).Normalized;
                    travel = Math.Min(travel, Math.Max(0, foodDistance - 0.15f));
                }
                else if (Phase == RoundPhase.Playing && Time >= HuntAt)
                {
                    var diver = Players.Find(p => p.team == Team.Seeker && p.alive && (p.position - f.position).Length < 4.5f);
                    if (diver != null) { f.facing = (f.position - diver.position).Normalized; travel *= 1.45f; }
                }
                var wanted = f.position + f.facing * travel;
                var actual = Constrain(wanted, 0.35f);
                if ((wanted - actual).Length > 0.001f) { f.facing = f.facing * -1; f.turnAt = Time + 0.9f; }
                f.position = actual;
            }
            if (Phase != RoundPhase.Playing) return;
            foreach (var p in Players)
            {
                if (!p.alive) continue;
                if (p.team == Team.Hider && Time >= HuntAt && Time - p.lastMealAt >= HungerSeconds)
                { Kill(p, "starved"); continue; }
                if (p.swallowedBy != 0)
                {
                    inputs.Remove(p.id); inputTimes.Remove(p.id);
                    var carrier = Player(p.swallowedBy);
                    if (carrier == null || !carrier.alive || Time >= p.releaseAt) Release(p, carrier);
                    else p.position = carrier.position;
                    continue;
                }
                SwimInput input;
                if (p.bot) input = BotInput(p);
                else if (!inputs.TryGetValue(p.id, out input) || Time - inputTimes[p.id] > 0.4f) input = new SwimInput();
                var move = new Point(input.x, input.y, input.z);
                if (move.Length > 1) move = move.Normalized;
                var aim = new Point(input.aimX, input.aimY, input.aimZ);
                if (aim.Length > 0.1f) p.facing = aim.Normalized;
                if ((p.team == Team.Seeker && Time < HuntAt) || Time < p.stunnedUntil)
                { input.action = input.morph = input.feed = input.scan = input.layEgg = false; p.stamina = Math.Min(MaxStamina, p.stamina + 18f * dt); continue; }
                float speed = p.team == Team.Seeker ? SeekerSpeed : (input.slow ? 1.65f : HiderSpeed);
                bool sprinting = input.sprint && move.Length > 0.01f && p.stamina > 0;
                if (sprinting) speed *= 1f + 0.6f * Math.Min(1f, p.stamina / (25f * dt));
                var movedTo = Constrain(p.position + move * speed * dt, p.team == Team.Seeker ? 0.5f : 0.4f);
                bool moved = (movedTo - p.position).Length > 0.001f;
                if (sprinting && moved) p.stamina = Math.Max(0, p.stamina - 25f * dt);
                else if (!input.sprint || !moved) p.stamina = Math.Min(MaxStamina, p.stamina + 18f * dt);
                p.position = movedTo;
                if (input.morph && p.team == Team.Hider && Time >= p.nextMorph && Time >= p.morphLockedUntil) Morph(p);
                if (input.action && Time >= p.nextAction)
                {
                    if (p.team == Team.Hider) Bite(p); else Shoot(p);
                }
                if (input.feed && p.team == Team.Seeker && Time >= p.nextFeed) Feed(p);
                if (input.scan && p.team == Team.Seeker && Time >= p.nextScan)
                { p.nextScan = Time + ScanCooldown; p.scanUntil = Time + ScanDuration; Effect("scan", p.position, p.facing, ScanDuration); }
                if (input.layEgg && p.team == Team.Hider && Time >= p.stunnedUntil) LayEgg(p);
                input.action = input.morph = input.feed = input.scan = input.layEgg = false;
            }
            TickHarpoons(dt);
            TickEggs();
            TickExposure(dt);
            CheckWinner();
        }

        private SwimInput BotInput(Swimmer p)
        {
            Point target = new Point((float)Math.Sin(Time * 0.23f) * 10, 3 + (float)Math.Sin(Time * 0.3f) * 1.5f, (float)Math.Cos(Time * 0.31f) * 6);
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
                action = p.team == Team.Hider ? closest < 1.35f : closest < 8, morph = p.species < 0,
                feed = p.team == Team.Seeker && Fish.Exists(f => !f.dead && !f.eaten && (f.position - p.position).Length < FeedRadius),
                layEgg = p.team == Team.Hider && Players.Exists(h => h.team == Team.Hider && !h.alive),
                scan = p.team == Team.Seeker && closest > 8 };
        }

        private void Morph(Swimmer p)
        {
            int species = -1; float distance = 2.8f;
            foreach (var f in Fish) if (!f.dead && !f.eaten && InAim(p, f.position, distance))
            { species = f.species; distance = (f.position - p.position).Length; }
            foreach (var ally in Players)
                if (ally.id != p.id && ally.team == Team.Hider && ally.alive && ally.swallowedBy == 0 && ally.species >= 0 && Time >= ally.revealUntil && InAim(p, ally.position, distance))
                { species = ally.species; distance = (ally.position - p.position).Length; }
            p.nextMorph = Time + 0.5f;
            if (species < 0) return;
            p.species = species; p.revealUntil = Time + 0.65f;
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
            return delta.Length < best && (delta.Length < 0.3f || Point.Dot(delta.Normalized, p.facing) > 0.25f) && ObstacleHit(p.position, target) > 1;
        }

        private void Bite(Swimmer p)
        {
            p.nextAction = Time + 0.65f;
            if (Players.Exists(h => h.swallowedBy == p.id)) return;
            Swimmer friend = null; Prey prey = null; float distance = 1.55f;
            foreach (var h in Players)
                if (h.id != p.id && h.team == Team.Hider && h.alive && h.swallowedBy == 0 && !Players.Exists(c => c.swallowedBy == h.id) && InBite(p, h.position, distance))
                { friend = h; distance = (h.position - p.position).Length; }
            foreach (var f in Fish) if (!f.eaten && !f.dead && InBite(p, f.position, distance))
            { prey = f; friend = null; distance = (f.position - p.position).Length; }
            Effect("bite", p.position + p.facing * 0.8f, p.facing, 0.3f);
            if (friend != null)
            {
                p.stunnedUntil = friend.stunnedUntil = Time + AllyStun;
                p.morphLockedUntil = friend.morphLockedUntil = Time + MorphLock;
                p.species = friend.species = -1; p.revealUntil = friend.revealUntil = Time + MorphLock;
                p.score -= 150;
                friend.swallowedBy = p.id; friend.releaseAt = Time + AllyStun; friend.position = p.position;
            }
            else if (prey != null)
            {
                prey.eaten = true;
                p.stunnedUntil = Time + EatStun;
                p.lastMealAt = Math.Max(Time, HuntAt); p.personalEaten++; p.score += 100;
                Effect("eat", prey.position, p.facing, TraceDuration);
            }
        }

        private void ReleaseCarried(int carrierId)
        {
            var carrier = Player(carrierId);
            foreach (var p in Players) if (p.swallowedBy == carrierId) Release(p, carrier);
        }

        private void Release(Swimmer swallowed, Swimmer carrier)
        {
            var direction = carrier == null ? new Point(0, 1) : carrier.facing;
            var at = carrier == null ? swallowed.position : carrier.position;
            swallowed.swallowedBy = 0; swallowed.releaseAt = 0;
            swallowed.position = Constrain(at + direction * 1.6f, 0.4f);
            swallowed.revealUntil = Math.Max(swallowed.revealUntil, swallowed.morphLockedUntil);
            if (carrier != null) { carrier.revealUntil = Math.Max(carrier.revealUntil, carrier.morphLockedUntil); carrier.nextAction = Math.Max(carrier.nextAction, Time + 0.8f); }
            Effect("burp", at, direction, TraceDuration);
        }

        private void Shoot(Swimmer p)
        {
            p.nextAction = Time + HarpoonCooldown;
            Harpoons.Add(new Harpoon { id = ++nextHarpoon, owner = p.id, position = p.position + p.facing * 0.7f, direction = p.facing });
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
                position = Constrain(owner.position - owner.facing * 0.7f, 0.4f), hatchAt = Time + HatchSeconds };
            Eggs.Add(egg); Effect("egg", egg.position, owner.facing, 0.8f);
        }

        private void TickEggs()
        {
            for (int i = Eggs.Count - 1; i >= 0; i--)
            {
                var egg = Eggs[i]; var target = Player(egg.revivePlayer);
                if (target == null || target.alive) { Eggs.RemoveAt(i); continue; }
                if (Time < egg.hatchAt) continue;
                target.alive = true; target.position = egg.position; target.species = -1; target.swallowedBy = 0;
                target.stamina = MaxStamina; target.lastMealAt = Time; target.deathReason = "";
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
            player.alive = false; player.deathReason = reason; player.swallowedBy = 0;
            inputs.Remove(player.id); inputTimes.Remove(player.id); ReleaseCarried(player.id);
            Effect(reason == "starved" ? "starved" : "caught", player.position, player.facing, 1.2f);
        }

        private void TickHarpoons(float dt)
        {
            for (int i = Harpoons.Count - 1; i >= 0; i--)
            {
                var h = Harpoons[i]; var start = h.position; var end = start + h.direction * (20f * dt);
                h.life -= dt;
                float first = 2f; Swimmer hitPlayer = null; Prey hitFish = null; RevivalEgg hitEgg = null;
                // Segment collision prevents fast harpoons crossing thin targets between ticks.
                foreach (var p in Players) if (p.id != h.owner && p.team == Team.Hider && p.alive && p.swallowedBy == 0)
                { var t = HitTime(start, end, p.position, 0.48f); if (t < first) { first = t; hitPlayer = p; hitFish = null; } }
                foreach (var f in Fish) if (!f.eaten && !f.dead)
                { var t = HitTime(start, end, f.position, 0.42f); if (t < first) { first = t; hitFish = f; hitPlayer = null; } }
                foreach (var egg in Eggs)
                { var t = HitTime(start, end, egg.position, 0.45f); if (t < first) { first = t; hitEgg = egg; hitFish = null; hitPlayer = null; } }
                var obstruction = ObstacleHit(start, end);
                if (obstruction < first) { first = obstruction; hitFish = null; hitPlayer = null; hitEgg = null; }
                if (first <= 1f)
                {
                    h.position = start + (end - start) * first;
                    if (hitPlayer != null) Kill(hitPlayer, "harpoon");
                    else if (hitFish != null) { hitFish.dead = true; Effect("miss", h.position, h.direction, 0.8f); }
                    else if (hitEgg != null) { Eggs.Remove(hitEgg); Effect("eggBreak", h.position, h.direction, 1f); }
                    else Effect("miss", h.position, h.direction, 0.35f);
                    Harpoons.RemoveAt(i);
                }
                else if (h.life <= 0 || Math.Abs(end.x) > HalfWidth || Math.Abs(end.z) > HalfDepth || end.y < Floor || end.y > WaterHeight) Harpoons.RemoveAt(i);
                else h.position = end;
            }
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

        public static Point Constrain(Point p, float radius)
        {
            p.x = Math.Max(-HalfWidth + radius, Math.Min(HalfWidth - radius, p.x));
            p.z = Math.Max(-HalfDepth + radius, Math.Min(HalfDepth - radius, p.z));
            p.y = Math.Max(Floor + radius, Math.Min(WaterHeight - radius, p.y));
            for (int i = 0; i < Rocks.Length; i++)
            {
                var delta = ScaleRock(p - Rocks[i], i, radius);
                if (delta.Length < 1)
                {
                    var outward = delta.Normalized;
                    var resolved = Rocks[i] + new Point(outward.x * (RockRadii[i] + radius), outward.y * (RockHalfHeight + radius), outward.z * (RockRadii[i] + radius));
                    if (resolved.y < Floor + radius)
                    {
                        var flat = new Point(delta.x, delta.z).Normalized;
                        float horizontal = (RockRadii[i] + radius) * (float)Math.Sqrt(Math.Max(0, 1 - delta.y * delta.y));
                        p = new Point(Rocks[i].x + flat.x * horizontal, p.y, Rocks[i].z + flat.z * horizontal);
                    }
                    else p = resolved;
                }
            }
            p.y = Math.Max(Floor + radius, Math.Min(WaterHeight - radius, p.y));
            return p;
        }

        private static Point ScaleRock(Point delta, int index, float padding)
        { return new Point(delta.x / (RockRadii[index] + padding), delta.y / (RockHalfHeight + padding), delta.z / (RockRadii[index] + padding)); }

        // Shared swept ellipsoid check for harpoons, camera occlusion and aim rays.
        public static float ObstacleHit(Point start, Point end, float padding = 0)
        {
            float hit = 2;
            for (int i = 0; i < Rocks.Length; i++)
                hit = Math.Min(hit, HitTime(ScaleRock(start - Rocks[i], i, padding), ScaleRock(end - Rocks[i], i, padding), new Point(), 1));
            return hit;
        }

        private void Effect(string kind, Point at, Point direction, float duration)
        { Effects.Add(new WaterEffect { id = ++nextEffect, kind = kind, position = at, direction = direction, born = Time, duration = duration }); }

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
            Winner = (int)team; WinReason = reason; Phase = RoundPhase.Finished; ReviewEndsAt = Time + ReviewSeconds;
            Harpoons.Clear(); inputs.Clear(); inputTimes.Clear();
        }

        public AquariumSnapshot Snapshot()
        {
            int eaten = Fish.FindAll(f => f.eaten).Count;
            int killed = Fish.FindAll(f => f.dead && !f.eaten).Count;
            return new AquariumSnapshot { sequence = ++sequence, round = Round, time = Time, huntAt = HuntAt, endsAt = EndsAt, winner = Winner, phase = Phase,
                winReason = WinReason, reviewEndsAt = ReviewEndsAt, target = Fish.Count - killed, killed = killed,
                remaining = Fish.Count - killed - eaten, eaten = eaten, players = Players.ToArray(), fish = Fish.ToArray(), harpoons = Harpoons.ToArray(), effects = Effects.ToArray(), eggs = Eggs.ToArray() };
        }
    }
}

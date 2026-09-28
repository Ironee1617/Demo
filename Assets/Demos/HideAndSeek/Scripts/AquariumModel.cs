using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    public enum Team { Hider, Seeker }
    public enum RoundPhase { Lobby, Playing, Finished }

    [Serializable]
    public struct Point
    {
        public float x, z;
        public Point(float x, float z) { this.x = x; this.z = z; }
        public float Length { get { return (float)Math.Sqrt((double)x * x + (double)z * z); } }
        public Point Normalized { get { var n = Length; return n > 0.0001f ? this / n : new Point(0, 1); } }
        public static Point operator +(Point a, Point b) { return new Point(a.x + b.x, a.z + b.z); }
        public static Point operator -(Point a, Point b) { return new Point(a.x - b.x, a.z - b.z); }
        public static Point operator *(Point a, float b) { return new Point(a.x * b, a.z * b); }
        public static Point operator /(Point a, float b) { return new Point(a.x / b, a.z / b); }
        public static float Dot(Point a, Point b) { return a.x * b.x + a.z * b.z; }
    }

    [Serializable]
    public class Swimmer
    {
        public int id;
        public string name;
        public Team team;
        public bool ready, alive = true, bot;
        public Point position, facing = new Point(0, 1);
        public int species = -1, swallowedBy;
        public float releaseAt, revealUntil, nextAction, nextMorph;
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
    public class AquariumSnapshot
    {
        public int sequence, round, remaining, eaten, winner = -1;
        public float time, huntAt, endsAt;
        public RoundPhase phase;
        public Swimmer[] players;
        public Prey[] fish;
        public Harpoon[] harpoons;
        public WaterEffect[] effects;
    }

    [Serializable]
    public class SwimInput
    {
        public float x, z, aimX, aimZ;
        public bool slow, action, morph;
        public SwimInput Copy() { return (SwimInput)MemberwiseClone(); }
    }

    // The host owns all rules. This class has no Unity or networking dependency.
    public sealed class AquariumModel
    {
        public const int MaxPlayers = 8, FishCount = 32;
        public const float HiderSpeed = 4f, SeekerSpeed = 4.7f, RoundSeconds = 180f;
        public const float HalfWidth = 14f, HalfDepth = 8.5f;
        public static readonly Point[] Rocks = { new Point(-6, -2), new Point(5, 3), new Point(1, -5) };
        public static readonly float[] RockRadii = { 1.15f, 1.35f, 0.8f };
        public readonly List<Swimmer> Players = new List<Swimmer>();
        public readonly List<Prey> Fish = new List<Prey>();
        public readonly List<Harpoon> Harpoons = new List<Harpoon>();
        public readonly List<WaterEffect> Effects = new List<WaterEffect>();
        public RoundPhase Phase { get; private set; }
        public float Time { get; private set; }
        public float HuntAt { get; private set; }
        public float EndsAt { get; private set; }
        public int Winner { get; private set; } = -1;
        public int Round { get; private set; }
        private int nextEffect, nextHarpoon, sequence;
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
            p.position = new Point(-9 + Players.Count * 2.4f, 0);
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
            if (Players.Exists(p => !p.ready)) return "모든 참가자가 준비를 눌러 주세요.";
            return "";
        }

        public bool StartRound()
        {
            if (Phase != RoundPhase.Lobby || StartProblem().Length != 0) return false;
            Round++;
            ResetFish();
            Harpoons.Clear(); Effects.Clear(); inputs.Clear(); inputTimes.Clear();
            Phase = RoundPhase.Playing; Winner = -1;
            HuntAt = Time + 5f; EndsAt = HuntAt + RoundSeconds;
            int hider = 0, seeker = 0;
            foreach (var p in Players)
            {
                p.alive = true; p.swallowedBy = 0; p.species = p.bot && p.team == Team.Hider ? 0 : -1;
                p.releaseAt = p.revealUntil = p.nextAction = p.nextMorph = 0;
                p.position = p.team == Team.Hider ? new Point(-8 + hider++ * 2, -1) : new Point(10, -4 + seeker++ * 2);
                p.facing = new Point(p.team == Team.Hider ? 1 : -1, 0);
            }
            return true;
        }

        public void ReturnToLobby()
        {
            Phase = RoundPhase.Lobby; Winner = -1;
            Harpoons.Clear(); Effects.Clear(); inputs.Clear(); inputTimes.Clear();
            foreach (var p in Players) { p.ready = p.bot || p.id == 1; p.alive = true; p.swallowedBy = 0; p.species = -1; }
            ResetFish();
        }

        public void SetInput(int id, SwimInput input)
        {
            if (Player(id) == null || input == null) return;
            // Reject non-finite data and clamp on the host, including diagonal movement.
            if (!Finite(input.x) || !Finite(input.z) || !Finite(input.aimX) || !Finite(input.aimZ)
                || Math.Abs(input.x) > 1000 || Math.Abs(input.z) > 1000 || Math.Abs(input.aimX) > 1000 || Math.Abs(input.aimZ) > 1000) return;
            SwimInput old;
            if (inputs.TryGetValue(id, out old)) { input.action |= old.action; input.morph |= old.morph; }
            inputs[id] = input.Copy(); inputTimes[id] = Time;
        }

        private static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
        private float Range(float a, float b) { return a + (float)random.NextDouble() * (b - a); }
        private void ResetFish()
        {
            Fish.Clear();
            for (int i = 0; i < FishCount; i++)
                Fish.Add(new Prey { id = 100 + i, species = i % 3, position = Constrain(new Point(Range(-12, 12), Range(-7, 7)), 0.35f),
                    facing = new Point(Range(-1, 1), Range(-1, 1)).Normalized, turnAt = Time + Range(1, 4) });
        }

        public void Tick(float dt)
        {
            if (!Finite(dt) || dt <= 0 || dt > 0.1f) return;
            Time += dt;
            Effects.RemoveAll(e => Time > e.born + e.duration);
            if (Phase == RoundPhase.Finished) return;
            foreach (var f in Fish)
            {
                if (f.eaten || f.dead) continue;
                if (Time > f.turnAt) { f.facing = (f.facing + new Point(Range(-0.8f, 0.8f), Range(-0.8f, 0.8f))).Normalized; f.turnAt = Time + Range(1.4f, 4.5f); }
                var wanted = f.position + f.facing * (1.4f + f.species * 0.18f) * dt;
                var actual = Constrain(wanted, 0.35f);
                if ((wanted - actual).Length > 0.001f) { f.facing = f.facing * -1; f.turnAt = Time + 0.9f; }
                f.position = actual;
            }
            if (Phase != RoundPhase.Playing) return;
            foreach (var p in Players)
            {
                if (!p.alive) continue;
                if (p.swallowedBy != 0)
                {
                    inputs.Remove(p.id); inputTimes.Remove(p.id);
                    var carrier = Player(p.swallowedBy);
                    if (carrier == null || !carrier.alive || Time >= p.releaseAt) Release(p, carrier);
                    else p.position = carrier.position;
                    continue;
                }
                if (p.team == Team.Seeker && Time < HuntAt) continue;
                SwimInput input;
                if (p.bot) input = BotInput(p);
                else if (!inputs.TryGetValue(p.id, out input) || Time - inputTimes[p.id] > 0.4f) input = new SwimInput();
                var move = new Point(input.x, input.z);
                if (move.Length > 1) move = move.Normalized;
                var aim = new Point(input.aimX, input.aimZ);
                if (aim.Length > 0.1f) p.facing = aim.Normalized;
                float speed = p.team == Team.Seeker ? SeekerSpeed : (input.slow ? 1.65f : HiderSpeed);
                p.position = Constrain(p.position + move * speed * dt, p.team == Team.Seeker ? 0.5f : 0.4f);
                if (input.morph && p.team == Team.Hider && Time >= p.nextMorph) Morph(p);
                if (input.action && Time >= p.nextAction)
                {
                    if (p.team == Team.Hider) Bite(p); else Shoot(p);
                }
                input.action = false; input.morph = false;
            }
            TickHarpoons(dt);
            CheckWinner();
        }

        private SwimInput BotInput(Swimmer p)
        {
            Point target = new Point((float)Math.Sin(Time * 0.23f) * 10, (float)Math.Cos(Time * 0.31f) * 6);
            float closest = float.MaxValue;
            if (p.team == Team.Hider)
            {
                foreach (var f in Fish) if (!f.eaten && (f.position - p.position).Length < closest)
                { closest = (f.position - p.position).Length; target = f.position; }
            }
            else
            {
                // Practice seekers investigate only exposed aliens, never secretly read disguised positions.
                foreach (var h in Players) if (h.team == Team.Hider && h.alive && h.swallowedBy == 0 && (h.species < 0 || h.revealUntil > Time))
                    if ((h.position - p.position).Length < closest) { closest = (h.position - p.position).Length; target = h.position; }
            }
            var direction = (target - p.position).Normalized;
            return new SwimInput { x = direction.x * 0.7f, z = direction.z * 0.7f, aimX = direction.x, aimZ = direction.z,
                action = p.team == Team.Hider ? closest < 1.35f : closest < 8, morph = p.species < 0 };
        }

        private void Morph(Swimmer p)
        {
            Prey target = null; float distance = 2.8f;
            foreach (var f in Fish) if (!f.dead && !f.eaten && (f.position - p.position).Length < distance)
            { target = f; distance = (f.position - p.position).Length; }
            p.nextMorph = Time + 0.5f;
            if (target == null) return;
            p.species = target.species; p.revealUntil = Time + 0.65f;
            Effect("morph", p.position, p.facing, 0.65f);
        }

        private bool InBite(Swimmer p, Point target, float best)
        {
            var delta = target - p.position;
            return delta.Length < best && (delta.Length < 0.3f || Point.Dot(delta.Normalized, p.facing) > 0.25f);
        }

        private void Bite(Swimmer p)
        {
            p.nextAction = Time + 0.65f;
            if (Players.Exists(h => h.swallowedBy == p.id)) return;
            Swimmer friend = null; Prey prey = null; float distance = 1.55f;
            foreach (var h in Players)
                if (h.id != p.id && h.team == Team.Hider && h.alive && h.swallowedBy == 0 && !Players.Exists(c => c.swallowedBy == h.id) && InBite(p, h.position, distance))
                { friend = h; distance = (h.position - p.position).Length; }
            foreach (var f in Fish) if (!f.eaten && InBite(p, f.position, distance))
            { prey = f; friend = null; distance = (f.position - p.position).Length; }
            Effect("bite", p.position + p.facing * 0.8f, p.facing, 0.3f);
            if (friend != null)
            {
                friend.swallowedBy = p.id; friend.releaseAt = Time + 1f; friend.position = p.position;
            }
            else if (prey != null)
            {
                prey.eaten = true;
                Effect("eat", prey.position, p.facing, 0.7f);
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
            swallowed.revealUntil = Time + 2f;
            if (carrier != null) { carrier.revealUntil = Time + 2f; carrier.nextAction = Time + 0.8f; }
            Effect("burp", at, direction, 2f);
        }

        private void Shoot(Swimmer p)
        {
            p.nextAction = Time + 0.85f;
            Harpoons.Add(new Harpoon { id = ++nextHarpoon, owner = p.id, position = p.position + p.facing * 0.7f, direction = p.facing });
            Effect("shot", p.position + p.facing, p.facing, 0.2f);
        }

        private void TickHarpoons(float dt)
        {
            for (int i = Harpoons.Count - 1; i >= 0; i--)
            {
                var h = Harpoons[i]; var start = h.position; var end = start + h.direction * (20f * dt);
                h.life -= dt;
                float first = 2f; Swimmer hitPlayer = null; Prey hitFish = null;
                // Segment collision prevents fast harpoons crossing thin targets between ticks.
                foreach (var p in Players) if (p.id != h.owner && p.team == Team.Hider && p.alive && p.swallowedBy == 0)
                { var t = HitTime(start, end, p.position, 0.48f); if (t < first) { first = t; hitPlayer = p; hitFish = null; } }
                foreach (var f in Fish) if (!f.eaten && !f.dead)
                { var t = HitTime(start, end, f.position, 0.42f); if (t < first) { first = t; hitFish = f; hitPlayer = null; } }
                for (int rock = 0; rock < Rocks.Length; rock++)
                { var t = HitTime(start, end, Rocks[rock], RockRadii[rock]); if (t < first) { first = t; hitFish = null; hitPlayer = null; } }
                if (first <= 1f)
                {
                    h.position = start + (end - start) * first;
                    if (hitPlayer != null) { hitPlayer.alive = false; ReleaseCarried(hitPlayer.id); Effect("caught", h.position, h.direction, 1.2f); }
                    else if (hitFish != null) { hitFish.dead = true; Effect("miss", h.position, h.direction, 0.8f); }
                    else Effect("miss", h.position, h.direction, 0.35f);
                    Harpoons.RemoveAt(i);
                }
                else if (h.life <= 0 || Math.Abs(end.x) > HalfWidth || Math.Abs(end.z) > HalfDepth) Harpoons.RemoveAt(i);
                else h.position = end;
            }
        }

        private static float HitTime(Point a, Point b, Point centre, float radius)
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
            for (int i = 0; i < Rocks.Length; i++)
            {
                var delta = p - Rocks[i]; float limit = radius + RockRadii[i];
                if (delta.Length < limit) p = Rocks[i] + delta.Normalized * limit;
            }
            return p;
        }

        private void Effect(string kind, Point at, Point direction, float duration)
        { Effects.Add(new WaterEffect { id = ++nextEffect, kind = kind, position = at, direction = direction, born = Time, duration = duration }); }

        private void CheckWinner()
        {
            if (Phase != RoundPhase.Playing) return;
            if (!Players.Exists(p => p.team == Team.Hider && p.alive)) Finish(Team.Seeker);
            else if (Fish.TrueForAll(f => f.eaten) || !Players.Exists(p => p.team == Team.Seeker)) Finish(Team.Hider);
            else if (Time >= EndsAt) Finish(Team.Seeker);
        }

        private void Finish(Team team) { Winner = (int)team; Phase = RoundPhase.Finished; Harpoons.Clear(); }

        public AquariumSnapshot Snapshot()
        {
            int eaten = Fish.FindAll(f => f.eaten).Count;
            return new AquariumSnapshot { sequence = ++sequence, round = Round, time = Time, huntAt = HuntAt, endsAt = EndsAt, winner = Winner, phase = Phase,
                remaining = Fish.Count - eaten, eaten = eaten, players = Players.ToArray(), fish = Fish.ToArray(), harpoons = Harpoons.ToArray(), effects = Effects.ToArray() };
        }
    }
}

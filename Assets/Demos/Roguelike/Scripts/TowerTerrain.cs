using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PrototypeLab.Roguelike
{
    /// <summary>
    /// A walkable piecewise-linear surface: the rolling ground or one floating platform.
    /// Heights are interpolated between nodes; outside the node range the end heights continue.
    /// </summary>
    public sealed class TowerSurface
    {
        private readonly float[] xs, ys;
        public float Left { get { return xs[0]; } }
        public float Right { get { return xs[xs.Length - 1]; } }
        public float Width { get { return Right - Left; } }
        public float CenterX { get { return (Left + Right) * .5f; } }
        public float CenterHeight { get { return HeightAt(CenterX); } }
        public float MinHeight { get; private set; }
        public float MaxHeight { get; private set; }
        public int NodeCount { get { return xs.Length; } }
        public float NodeX(int i) { return xs[i]; }
        public float NodeY(int i) { return ys[i]; }

        public TowerSurface(float[] nodeX, float[] nodeY)
        {
            if (nodeX == null || nodeY == null || nodeX.Length < 2 || nodeX.Length != nodeY.Length) throw new ArgumentException("A surface needs at least two matching nodes.");
            for (int i = 1; i < nodeX.Length; i++) if (!(nodeX[i] > nodeX[i - 1])) throw new ArgumentException("Surface nodes must increase in x.");
            xs = (float[])nodeX.Clone(); ys = (float[])nodeY.Clone();
            MinHeight = float.PositiveInfinity; MaxHeight = float.NegativeInfinity;
            foreach (float y in ys) { MinHeight = Math.Min(MinHeight, y); MaxHeight = Math.Max(MaxHeight, y); }
        }

        /// <summary>Surface height at x; x is clamped to the surface's span.</summary>
        public float HeightAt(float x)
        {
            if (x <= xs[0]) return ys[0];
            int last = xs.Length - 1;
            if (x >= xs[last]) return ys[last];
            int lo = 0, hi = last;
            while (hi - lo > 1) { int mid = (lo + hi) >> 1; if (xs[mid] <= x) lo = mid; else hi = mid; }
            float t = (x - xs[lo]) / (xs[hi] - xs[lo]);
            return ys[lo] + (ys[hi] - ys[lo]) * t;
        }

        /// <summary>Rise per unit of x at x (zero beyond the ends).</summary>
        public float SlopeAt(float x)
        {
            int last = xs.Length - 1;
            if (x <= xs[0] || x >= xs[last]) return 0;
            int lo = 0, hi = last;
            while (hi - lo > 1) { int mid = (lo + hi) >> 1; if (xs[mid] <= x) lo = mid; else hi = mid; }
            return (ys[hi] - ys[lo]) / (xs[hi] - xs[lo]);
        }

        public bool Contains(float x, float margin) { return x >= Left - margin && x <= Right + margin; }

        /// <summary>Lowest clearance between this surface and <paramref name="below"/> across the span.</summary>
        public float ClearanceOver(TowerSurface below, float step)
        {
            float clearance = float.PositiveInfinity;
            for (float x = Left; x <= Right + .0001f; x += step) clearance = Math.Min(clearance, HeightAt(x) - below.HeightAt(x));
            return Math.Min(clearance, HeightAt(Right) - below.HeightAt(Right));
        }
    }

    /// <summary>One floating platform. X/Y describe the centre; the surface carries the sloped profile.</summary>
    public readonly struct TowerPlatform
    {
        public float X { get { return Surface.CenterX; } }
        public float Y { get { return Surface.CenterHeight; } }
        public float Width { get { return Surface.Width; } }
        public TowerSurface Surface { get; }
        /// <summary>1-3 for platforms counted from the ground, 4-6 for the shelves stacked above a crown.</summary>
        public int Tier { get; }
        public TowerPlatform(TowerSurface surface, int tier) { Surface = surface; Tier = tier; }
    }

    /// <summary>Traps and launch pads laid out with the terrain.</summary>
    public enum TowerHazardKind { Spikes, FlameVent, Rockfall, BoltTotem, JumpPad }

    /// <summary>
    /// One trap or launch pad. X is its centre and Width its footprint along the support it sits on
    /// (a platform index, or -1 for the ground). Phase staggers the cycles of neighbouring timed traps.
    /// </summary>
    public readonly struct TowerHazard
    {
        public TowerHazardKind Kind { get; }
        public float X { get; }
        public float Width { get; }
        public int Support { get; }
        public float Phase { get; }
        public float Left { get { return X - Width * .5f; } }
        public float Right { get { return X + Width * .5f; } }
        public TowerHazard(TowerHazardKind kind, float x, float width, int support, float phase)
        { Kind = kind; X = x; Width = width; Support = support; Phase = phase; }
    }

    /// <summary>The generated ground profile, platforms and traps of one floor.</summary>
    public sealed class TowerFloorLayout
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public string Archetype { get; private set; }
        public int Floor { get; private set; }
        public TowerSurface Ground { get; private set; }
        public IReadOnlyList<TowerPlatform> Platforms { get; private set; }
        public IReadOnlyList<TowerHazard> Hazards { get; private set; }
        public float HighestPlatform { get; private set; }
        public float HighestGround { get { return Ground.MaxHeight; } }
        /// <summary>The ascent gate; the floor's right wall stands <see cref="TowerTerrainGenerator.ExitMargin"/> past it.</summary>
        public float ExitX { get; private set; }
        public float Right { get { return ExitX + TowerTerrainGenerator.ExitMargin; } }

        /// <summary>Hand-built layouts (the village) keep the compact chamber unless they name another exit.</summary>
        internal TowerFloorLayout(int id, string archetype, int floor, TowerSurface ground, List<TowerPlatform> platforms, float exitX = TowerTerrainGenerator.CompactExitX)
        {
            Id = id; Archetype = archetype; Floor = floor; Ground = ground; Platforms = platforms.AsReadOnly(); ExitX = exitX;
            Hazards = new List<TowerHazard>().AsReadOnly();
            HighestPlatform = TowerTerrainGenerator.BaseGround;
            foreach (var p in platforms) HighestPlatform = Math.Max(HighestPlatform, p.Surface.MaxHeight);
            Name = archetype + " · 발판 " + platforms.Count;
        }

        internal void SetHazards(List<TowerHazard> hazards) { Hazards = hazards.AsReadOnly(); }

        public float GroundAt(float x) { return Ground.HeightAt(x); }

        /// <summary>Index of the platform whose surface passes within <paramref name="tolerance"/> of (x, y); -1 for the ground or nothing.</summary>
        public int SupportAt(float x, float y, float tolerance = .12f)
        {
            int best = -1; float gap = tolerance;
            for (int i = 0; i < Platforms.Count; i++)
            {
                var s = Platforms[i].Surface;
                if (!s.Contains(x, TowerPhysics.FootMargin)) continue;
                float d = Math.Abs(s.HeightAt(x) - y);
                if (d < gap) { gap = d; best = i; }
            }
            return best;
        }

        /// <summary>Height of a support at x: the platform's surface, or the ground for -1.</summary>
        public float SurfaceHeight(int support, float x)
        { return support >= 0 && support < Platforms.Count ? Platforms[support].Surface.HeightAt(x) : Ground.HeightAt(x); }
    }

    /// <summary>
    /// Vertical motion against the floor layout, shared by the game, the companion planner,
    /// the reachability model and the tests so every consumer agrees on what lands where.
    /// </summary>
    public static class TowerPhysics
    {
        public const float Gravity = 25, JumpVelocity = 10.7f, WalkSpeed = 6;
        /// <summary>Feet may overhang a platform end by this much and still stand on it.</summary>
        public const float FootMargin = .18f;
        /// <summary>A grounded actor follows its surface while the height change per frame stays within this.</summary>
        public const float StepTolerance = .9f;
        public const float LandingTolerance = .02f;

        /// <summary>
        /// Advances one frame. <paramref name="xOld"/> is the x at the start of the frame and <paramref name="x"/> the
        /// x after horizontal movement. A grounded actor follows the surface it stands on (<paramref name="support"/>:
        /// platform index, -1 ground); an airborne one falls and lands on the first surface it crosses, skipping
        /// <paramref name="ignoreSupport"/> while dropping through it.
        /// </summary>
        public static void Step(TowerFloorLayout layout, float xOld, float x, ref float y, ref float velocityY, ref bool grounded, ref int support, float dt, int ignoreSupport)
        {
            float ground = layout.GroundAt(x);
            if (grounded && velocityY <= 0)
            {
                if (support < 0 && y > ground + StepTolerance) support = layout.SupportAt(x, y, StepTolerance);
                if (support >= 0 && support < layout.Platforms.Count)
                {
                    var p = layout.Platforms[support].Surface;
                    if (p.Contains(x, FootMargin))
                    {
                        float h = p.HeightAt(x);
                        if (Math.Abs(h - y) <= StepTolerance) { y = h; velocityY = 0; return; }
                    }
                    support = -1; grounded = false;
                }
                else if (Math.Abs(ground - y) <= StepTolerance) { support = -1; y = ground; velocityY = 0; return; }
                else { support = -1; grounded = false; }
            }
            float old = y; velocityY -= Gravity * dt; float next = old + velocityY * dt;
            if (velocityY <= 0)
            {
                int landed = -1; float landing = ground;
                for (int i = 0; i < layout.Platforms.Count; i++)
                {
                    if (i == ignoreSupport) continue;
                    var p = layout.Platforms[i].Surface;
                    if (!p.Contains(x, FootMargin)) continue;
                    float hOld = p.HeightAt(xOld), hNew = p.HeightAt(x);
                    if (old >= hOld - LandingTolerance && next <= hNew && hNew > landing) { landing = hNew; landed = i; }
                }
                if (next <= landing) { y = landing; velocityY = 0; grounded = true; support = landed; return; }
            }
            else if (next < ground) next = ground;
            y = next; grounded = false;
        }
    }

    /// <summary>Ordinary-jump reachability with the player's weakest movement: no dash, no air jump, no upgrades.</summary>
    public static class TowerJumpModel
    {
        public sealed class Plan { public int source, target; public float launch, aim; }
        /// <summary>No jump, even a long fall, carries the hero further than <see cref="MaxReach"/> between platform ends.</summary>
        public const float MaxRise = 2.05f, HeroMinX = -15.4f, MaxReach = 9;
        public static float HeroMaxX(TowerFloorLayout layout) { return layout.Right - .6f; }
        /// <summary>
        /// A plan must land on its target at every one of these frame steps (the .04 cap, 30, 60 and 90 fps), so a
        /// trajectory that only grazes past a neighbouring platform at one frame rate is never counted as a route.
        /// </summary>
        private static readonly float[] ConfirmSteps = { .04f, 1f / 30, 1f / 60, 1f / 90 };

        public static Plan Find(TowerFloorLayout layout, int source, int target, float dt)
        {
            var destination = layout.Platforms[target].Surface;
            TowerSurface origin = source < 0 ? null : layout.Platforms[source].Surface;
            float left = origin == null ? destination.CenterX - 3.5f : origin.Left + .3f;
            float right = origin == null ? destination.CenterX + 3.5f : origin.Right - .3f;
            // From the ground the hero may launch anywhere in the window, so a pit under the target does not rule it out.
            float sourceLow = origin == null ? GroundTop(layout.Ground, left, right) : origin.MaxHeight;
            if (destination.MinHeight - sourceLow > MaxRise) return null;
            if (origin != null && destination.MaxHeight < origin.MinHeight - .1f) return null;
            if (origin != null && (destination.Left - origin.Right > MaxReach || origin.Left - destination.Right > MaxReach)) return null;
            left = Math.Max(left, HeroMinX); right = Math.Min(right, HeroMaxX(layout));
            for (float launch = left; launch <= right + .001f; launch += .15f)
                for (int aim = -1; aim <= 1; aim++)
                {
                    float aimX = destination.CenterX + aim * (destination.Width * .5f - .4f);
                    if (!Lands(layout, source, target, launch, aimX, dt)) continue;
                    bool robust = true;
                    foreach (float step in ConfirmSteps) if (step != dt && !Lands(layout, source, target, launch, aimX, step)) { robust = false; break; }
                    if (robust) return new Plan { source = source, target = target, launch = launch, aim = aimX };
                }
            return null;
        }

        private static float GroundTop(TowerSurface ground, float left, float right)
        {
            float top = float.NegativeInfinity;
            for (float x = left; x <= right + .0001f; x += .25f) top = Math.Max(top, ground.HeightAt(x));
            return top;
        }

        /// <summary>Walks toward <paramref name="aimX"/> after a jump from <paramref name="launch"/> and reports whether the first landing is well inside the target.</summary>
        private static bool Lands(TowerFloorLayout layout, int source, int target, float launch, float aimX, float dt)
        {
            var destination = layout.Platforms[target].Surface;
            float x = launch, y = layout.SurfaceHeight(source, launch), velocity = TowerPhysics.JumpVelocity, maxX = HeroMaxX(layout);
            bool grounded = false; int support = -1;
            for (int frame = 0, frames = (int)Math.Ceiling(4 / dt); frame < frames; frame++)
            {
                float xOld = x; float step = TowerPhysics.WalkSpeed * dt;
                x += Math.Max(-step, Math.Min(step, aimX - x));
                x = Math.Max(HeroMinX, Math.Min(maxX, x));
                TowerPhysics.Step(layout, xOld, x, ref y, ref velocity, ref grounded, ref support, dt, -1);
                if (grounded) return support == target && Math.Abs(x - destination.CenterX) <= destination.Width * .5f - .25f;
            }
            return false;
        }

        /// <summary>Breadth-first plans from the ground; one plan per platform reached.</summary>
        public static List<Plan> Reachable(TowerFloorLayout layout, float dt)
        {
            var plans = new List<Plan>(); var reached = new HashSet<int>(); var queue = new Queue<int>(); queue.Enqueue(-1);
            while (queue.Count > 0)
            {
                int source = queue.Dequeue();
                for (int target = 0; target < layout.Platforms.Count; target++)
                {
                    if (reached.Contains(target)) continue;
                    var plan = Find(layout, source, target, dt); if (plan == null) continue;
                    reached.Add(target); queue.Enqueue(target); plans.Add(plan);
                }
            }
            return plans;
        }

        public static bool AllReachable(TowerFloorLayout layout, float dt)
        { return Reachable(layout, dt).Count == layout.Platforms.Count; }
    }

    /// <summary>
    /// Random rolling terrain and sloped platforms, decided by the run seed and floor alone. A floor runs about four
    /// screens from the spawn to the exit through three terrain segments of their own shape (hills, plateaus, spike
    /// pits). Every floor keeps a flat landing zone at the spawn, a flat exit lane (and gate lane on branch floors),
    /// tiered platforms with ordinary-jump reachability, narrower shelves above crown platforms, and traps.
    /// </summary>
    public static class TowerTerrainGenerator
    {
        public const float BaseGround = -3, MaxHill = 1.6f, MaxDip = 1.15f, MaxGroundSlope = .55f, MaxPlatformSlope = .34f;
        /// <summary>Generated floors end at <see cref="ExitX"/>; hand-built chambers such as the village keep the compact one.</summary>
        public const float SpawnX = -12, ExitX = 70, CompactExitX = 26, ExitMargin = 3, FeatureX = 5;
        public const float TerrainLeft = -40, TerrainRight = 100, NodeSpacing = .25f;
        public const float PlatformLeft = -15.4f, PlatformRight = ExitX - 1;
        public const float MinWidth = 2.6f, MaxWidth = 4.6f, MinClearance = 1.15f, MinSeparation = 1f;
        public static readonly float[] TierHeights = { -1.8f, -.4f, 1f };
        public const float ShelfRise = 1.4f;
        public const int BranchInterval = 5, MaxAttempts = 40, Segments = 3;
        /// <summary>Traps keep clear of the spawn lane, the exits and the campfire or merchant stall.</summary>
        public const float HazardLeft = -4, HazardExitClear = 6, HazardGap = 2.2f, FeatureClear = 3.4f;
        public const float SpikeMinWidth = 1.2f, SpikeMaxWidth = 2f, VentWidth = .9f, RockfallWidth = 2.4f, TotemWidth = .7f, PadWidth = 1.1f;
        // Diagnostics belong to the thread that generated, so a background prefetch never mixes into a caller's view.
        [ThreadStatic] private static int lastAttempts;
        [ThreadStatic] private static bool lastWasFallback;
        [ThreadStatic] private static string lastRejection;
        /// <summary>How many candidate layouts the last Generate call on this thread tried (diagnostics for tests).</summary>
        public static int LastAttempts { get { return lastAttempts; } private set { lastAttempts = value; } }
        public static bool LastWasFallback { get { return lastWasFallback; } private set { lastWasFallback = value; } }
        /// <summary>Why the most recent candidate on this thread was rejected (diagnostics for tests).</summary>
        public static string LastRejection { get { return lastRejection; } private set { lastRejection = value; } }
        private static readonly object prefetchLock = new object();
        private static readonly Dictionary<long, Task<TowerFloorLayout>> prefetched = new Dictionary<long, Task<TowerFloorLayout>>();
        private static long PrefetchKey(int runSeed, int floor) { return ((long)runSeed << 32) ^ (uint)floor; }

        /// <summary>
        /// Starts generating a floor on a worker thread. Verification takes tens to hundreds of milliseconds, so the
        /// world asks for the next floor while the current one is played and <see cref="Generate"/> then returns at once.
        /// </summary>
        public static void Prefetch(int runSeed, int floor)
        {
            if (floor < 1) return;
            long key = PrefetchKey(runSeed, floor);
            lock (prefetchLock)
            {
                if (prefetched.ContainsKey(key)) return;
                // Only the next floor or two are ever useful; drop stale guesses from abandoned runs.
                if (prefetched.Count >= 4) prefetched.Clear();
                prefetched.Add(key, Task.Run(() => Compute(runSeed, floor)));
            }
        }
        private static readonly string[] Archetypes = { "평탄한 들판", "완만한 언덕", "쌍봉 능선", "중앙 고원", "계단식 대지", "내리막 비탈", "분지 골짜기", "물결 능선", "가시 구덩이", "쌍둥이 구덩이" };

        private sealed class Rng
        {
            private uint state;
            public Rng(int seed, int floor, int attempt)
            {
                state = unchecked((uint)seed * 2654435761u ^ (uint)floor * 0x9E3779B9u ^ (uint)(attempt + 1) * 0x85EBCA6Bu);
                if (state == 0) state = 0x6D2B79F5u;
                for (int i = 0; i < 4; i++) Next();
            }
            public uint Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
            public float F() { return (Next() & 0xFFFFFF) / 16777216f; }
            public float R(float a, float b) { return a + (b - a) * F(); }
            public int I(int n) { return (int)(Next() % (uint)n); }
            public bool Chance(float p) { return F() < p; }
        }

        private sealed class Bump { public float c, w, a; public bool plateau; }

        public static bool IsBranchFloor(int floor) { return floor % BranchInterval == 0; }
        public static bool IsFeatureFloor(int floor) { return floor > 1 && floor % BranchInterval == 1; }

        /// <summary>Flat ground zones that must stay at the base level on this floor.</summary>
        public static List<float[]> FlatZones(int floor)
        {
            var zones = new List<float[]>();
            zones.Add(new[] { TerrainLeft, -9.5f });
            zones.Add(new[] { IsBranchFloor(floor) ? ExitX - 10.7f : ExitX - 2.2f, TerrainRight });
            if (IsFeatureFloor(floor)) zones.Add(new[] { 2.6f, 7.4f });
            return zones;
        }

        private static int ArchetypeFor(int runSeed, int floor)
        {
            int pick = (int)(new Rng(runSeed, floor, 977).Next() % (uint)Archetypes.Length);
            if (floor > 1)
            {
                int previous = (int)(new Rng(runSeed, floor - 1, 977).Next() % (uint)Archetypes.Length);
                if (previous == pick) pick = (pick + 1) % Archetypes.Length;
            }
            return pick;
        }

        /// <summary>One shape per terrain segment; the first follows the floor's own pick and neighbours never repeat.</summary>
        private static int[] SegmentArchetypes(int runSeed, int floor)
        {
            var picks = new int[Segments]; picks[0] = ArchetypeFor(runSeed, floor);
            var rng = new Rng(runSeed, floor, 978);
            for (int k = 1; k < Segments; k++)
            {
                int pick = rng.I(Archetypes.Length);
                if (pick == picks[k - 1]) pick = (pick + 1 + rng.I(Archetypes.Length - 1)) % Archetypes.Length;
                picks[k] = pick;
            }
            return picks;
        }

        private static string ArchetypeName(int[] picks)
        {
            var names = new string[picks.Length];
            for (int k = 0; k < picks.Length; k++) names[k] = Archetypes[picks[k]];
            return string.Join(" → ", names);
        }

        public static TowerFloorLayout Generate(int runSeed, int floor)
        {
            if (floor < 1) floor = 1;
            Task<TowerFloorLayout> ready = null;
            lock (prefetchLock)
            {
                long key = PrefetchKey(runSeed, floor);
                if (prefetched.TryGetValue(key, out ready)) prefetched.Remove(key);
            }
            if (ready != null)
            {
                try { return ready.Result; }
                catch (AggregateException) { }
            }
            return Compute(runSeed, floor);
        }

        private static TowerFloorLayout Compute(int runSeed, int floor)
        {
            var archetypes = SegmentArchetypes(runSeed, floor);
            LastWasFallback = false;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                LastAttempts = attempt + 1;
                var layout = Verified(Build(runSeed, floor, archetypes, attempt));
                if (layout == null) continue;
                layout.SetHazards(PlanHazards(runSeed, floor, layout));
                return layout;
            }
            LastWasFallback = true;
            var fallback = Fallback(runSeed, floor);
            fallback.SetHazards(PlanHazards(runSeed, floor, fallback));
            return fallback;
        }

        /// <summary>
        /// Checks ordinary-jump reachability at both frame steps. A long floor rarely gets every one of its thirty-odd
        /// platforms right, so up to <see cref="MaxPruned"/> stray platforms nobody can reach are dropped instead of
        /// rerolling the whole floor. Removing a platform never breaks another one's route: no verified trajectory
        /// touched it before landing on its own target.
        /// </summary>
        private static TowerFloorLayout Verified(TowerFloorLayout layout)
        {
            for (int pass = 0; layout != null && pass < 3; pass++)
            {
                var plans = TowerJumpModel.Reachable(layout, .04f);
                if (plans.Count == layout.Platforms.Count)
                {
                    var steady = TowerJumpModel.Reachable(layout, 1f / 60);
                    if (steady.Count == layout.Platforms.Count)
                    {
                        int entries = 0; foreach (var plan in plans) if (plan.source < 0) entries++;
                        if (entries < 2) { LastRejection = "fewer than two ground entries"; return null; }
                        return layout;
                    }
                    plans = steady;
                }
                int missing = layout.Platforms.Count - plans.Count;
                if (missing > MaxPruned) { LastRejection = "unreachable: " + plans.Count + "/" + layout.Platforms.Count; return null; }
                var reached = new HashSet<int>(); foreach (var plan in plans) reached.Add(plan.target);
                var kept = new List<TowerPlatform>(); bool crown = false, shelf = false;
                for (int i = 0; i < layout.Platforms.Count; i++)
                {
                    if (!reached.Contains(i)) continue;
                    var p = layout.Platforms[i]; kept.Add(p); crown |= p.Tier == 3; shelf |= p.Tier >= 4;
                }
                if (!crown || !shelf) { LastRejection = "pruning removed every crown or shelf"; return null; }
                layout = new TowerFloorLayout(layout.Id, layout.Archetype, layout.Floor, layout.Ground, kept, layout.ExitX);
            }
            if (layout != null) LastRejection = "pruning did not settle";
            return null;
        }
        public const int MaxPruned = 3;

        private static TowerFloorLayout Build(int runSeed, int floor, int[] archetypes, int attempt)
        {
            var rng = new Rng(runSeed, floor, attempt);
            var bumps = PlanBumps(rng, archetypes, FlatZones(floor));
            var ground = GroundSurface(bumps);
            var platforms = PlanPlatforms(rng, ground);
            if (platforms == null) return null;
            int id = unchecked((int)(new Rng(runSeed, floor, attempt).Next() & 0x7fffffff));
            return new TowerFloorLayout(id, ArchetypeName(archetypes), floor, ground, platforms, ExitX);
        }

        private static List<float[]> FreeRegions(List<float[]> zones)
        {
            // Hills live strictly between flat zones, which are sorted left to right and never overlap.
            var sorted = new List<float[]>(zones); sorted.Sort((a, b) => a[0].CompareTo(b[0]));
            var free = new List<float[]>();
            for (int i = 0; i + 1 < sorted.Count; i++) if (sorted[i + 1][0] - sorted[i][1] > 4) free.Add(new[] { sorted[i][1], sorted[i + 1][0] });
            return free;
        }

        /// <summary>Splits the hill-free stretch between the spawn and exit lanes into segments, each shaped by its own archetype.</summary>
        private static List<Bump> PlanBumps(Rng rng, int[] archetypes, List<float[]> zones)
        {
            var free = FreeRegions(zones); var bumps = new List<Bump>();
            if (free.Count == 0) return bumps;
            float start = free[0][0], span = (free[free.Count - 1][1] - start) / archetypes.Length;
            for (int k = 0; k < archetypes.Length; k++)
            {
                float a0 = start + span * k, a1 = a0 + span;
                var regions = new List<float[]>();
                foreach (var r in free) { float l = Math.Max(r[0], a0), h = Math.Min(r[1], a1); if (h - l > 4) regions.Add(new[] { l, h }); }
                if (regions.Count > 0) PlanSegment(rng, archetypes[k], regions, bumps);
            }
            return bumps;
        }

        private static void PlanSegment(Rng rng, int archetype, List<float[]> free, List<Bump> bumps)
        {
            // Negative amplitudes carve pits below the base ground; their walls obey the same slope limit as hills.
            void Add(float aMin, float aMax, float wMin, float wMax, bool plateau, float preferLeft)
            {
                float a = rng.R(aMin, aMax);
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    float w = rng.R(wMin, wMax);
                    w = Math.Max(w, plateau ? RampLength(a) + 1.6f : 2.9f * Math.Abs(a) + .2f);
                    var region = free[rng.I(free.Count)];
                    if (region[1] - region[0] < w * 2 + .4f) continue;
                    float t = preferLeft < 0 ? rng.F() : preferLeft > .5f ? rng.R(.5f, 1f) : rng.R(0, .5f);
                    float c = region[0] + w + .2f + (region[1] - region[0] - w * 2 - .4f) * t;
                    bool clear = true;
                    foreach (var other in bumps) if (Math.Abs(other.c - c) < other.w + w + .6f) clear = false;
                    if (!clear) continue;
                    bumps.Add(new Bump { c = c, w = w, a = a, plateau = plateau }); return;
                }
            }
            switch (archetype)
            {
                case 0: Add(.35f, .6f, 4, 6, false, -1); break;
                case 1: Add(.9f, 1.4f, 6, 8, false, -1); break;
                case 2: Add(.7f, 1.2f, 4.5f, 6, false, 0); Add(.7f, 1.2f, 4.5f, 6, false, 1); break;
                case 3: Add(1f, MaxHill, 7, 9, true, -1); break;
                case 4: Add(.5f, .8f, 5, 6, true, 0); Add(1.1f, MaxHill, 5.5f, 7, true, 1); break;
                case 5: Add(1.1f, MaxHill, 5.5f, 7, true, 0); Add(.5f, .8f, 5, 6, true, 1); break;
                case 6: Add(.9f, 1.3f, 4.5f, 5.5f, false, 0); Add(.9f, 1.3f, 4.5f, 5.5f, false, 1); break;
                case 7: Add(.4f, .8f, 3.5f, 4.5f, false, 0); Add(.4f, .8f, 3.5f, 4.5f, false, -1); Add(.4f, .8f, 3.5f, 4.5f, false, 1); break;
                case 8: Add(-MaxDip, -.85f, 3.6f, 4.6f, false, -1); Add(.5f, .9f, 3.5f, 4.5f, false, -1); break;
                default: Add(-MaxDip, -.8f, 3.6f, 4.4f, false, 0); Add(-MaxDip, -.8f, 3.6f, 4.4f, false, 1); break;
            }
            // A low swell now and then keeps long stretches from reading as one flat run.
            if (rng.Chance(.55f)) Add(.3f, .6f, 3f, 4f, false, -1);
        }

        private static float RampLength(float amplitude) { return Math.Max(2.6f, 2.75f * amplitude) + .2f; }

        private static float BumpHeight(Bump b, float x)
        {
            float t = Math.Abs(x - b.c) / b.w;
            if (t >= 1) return 0;
            if (!b.plateau) { float u = 1 - t * t; return b.a * u * u; }
            float ramp = RampLength(b.a) / b.w;
            if (t <= 1 - ramp) return b.a;
            float s = (1 - t) / ramp;
            return b.a * s * s * (3 - 2 * s);
        }

        private static TowerSurface GroundSurface(List<Bump> bumps)
        {
            int count = (int)Math.Round((TerrainRight - TerrainLeft) / NodeSpacing) + 1;
            var xs = new float[count]; var ys = new float[count];
            for (int i = 0; i < count; i++)
            {
                float x = TerrainLeft + i * NodeSpacing, h = BaseGround;
                foreach (var b in bumps) h += BumpHeight(b, x);
                xs[i] = x; ys[i] = Math.Max(BaseGround - MaxDip, Math.Min(BaseGround + MaxHill, h));
            }
            return new TowerSurface(xs, ys);
        }

        private static float Uplift(TowerSurface ground, float left, float right)
        {
            float top = BaseGround;
            for (float x = left - TowerPhysics.FootMargin; x <= right + TowerPhysics.FootMargin + .0001f; x += .125f) top = Math.Max(top, ground.HeightAt(x));
            return top - BaseGround;
        }

        private static TowerSurface Shelf(Rng rng, float cx, float width, float baseHeight, bool gentle)
        {
            // Profiles: flat, tilt (either way), hump, dip. The lowest point sits at baseHeight.
            int kind = gentle ? rng.I(3) : rng.I(5);
            // Segment slopes stay under MaxPlatformSlope: tilts span half the width per node, humps a quarter.
            float cap = kind == 3 || kind == 4 ? .115f : gentle ? .08f : .28f;
            float amplitude = Math.Min(rng.R(.18f, .5f), width * cap);
            float[] offsets;
            switch (kind)
            {
                case 1: offsets = new[] { 0, amplitude * .55f, amplitude }; break;                       // rising to the right
                case 2: offsets = new[] { amplitude, amplitude * .45f, 0 }; break;                       // falling to the right
                case 3: offsets = new[] { 0, amplitude * .7f, amplitude, amplitude * .7f, 0 }; break;    // hump
                case 4: offsets = new[] { amplitude, amplitude * .3f, 0, amplitude * .3f, amplitude }; break; // dip
                default: offsets = new[] { 0f, 0f }; break;
            }
            var xs = new float[offsets.Length]; var ys = new float[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                xs[i] = cx - width * .5f + width * i / (offsets.Length - 1);
                ys[i] = baseHeight + offsets[i];
            }
            return new TowerSurface(xs, ys);
        }

        public static bool TooClose(TowerSurface a, TowerSurface b)
        {
            float left = Math.Max(a.Left, b.Left) - .2f, right = Math.Min(a.Right, b.Right) + .2f;
            if (right < left) return false;
            for (float x = left; x <= right + .0001f; x += .1f) if (Math.Abs(a.HeightAt(x) - b.HeightAt(x)) < MinSeparation) return true;
            return false;
        }

        private static List<TowerPlatform> PlanPlatforms(Rng rng, TowerSurface ground)
        {
            var centres = new List<float>(); var tiers = new List<int>(); var widths = new List<float>();
            float x = rng.R(-9.9f, -9.1f); int tier = 1;
            while (true)
            {
                float width = rng.R(MinWidth, MaxWidth);
                if (centres.Count == 0) x = Math.Max(x, SpawnX + width * .5f + .65f);
                if (x + width * .5f > PlatformRight - .2f) break;
                if (centres.Count > 0)
                {
                    float roll = rng.F();
                    if (tier == 3) tier = roll < .62f ? 2 : 3;
                    else if (tier == 1) tier = roll < .55f ? 2 : 1;
                    else tier = roll < .4f ? 3 : roll < .68f ? 1 : 2;
                }
                centres.Add(x); tiers.Add(tier); widths.Add(width);
                x += rng.R(2.75f, 4.4f);
            }
            if (centres.Count < 16) { LastRejection = "too few platforms"; return null; }
            // Same-tier neighbours keep a visible gap: shrink widths, then split tiers if still crowded.
            for (int i = 1; i < centres.Count; i++)
            {
                if (tiers[i] != tiers[i - 1]) continue;
                float room = centres[i] - centres[i - 1] - .5f;
                if ((widths[i] + widths[i - 1]) * .5f <= room) continue;
                float scale = room * 2 / (widths[i] + widths[i - 1]);
                widths[i] = Math.Max(MinWidth, widths[i] * scale); widths[i - 1] = Math.Max(MinWidth, widths[i - 1] * scale);
                if ((widths[i] + widths[i - 1]) * .5f > room) tiers[i] = tiers[i] == 3 ? 2 : tiers[i] + 1;
            }
            // Hills lift platforms with the ground beneath them, so overlapping neighbours on different tiers
            // can end up too close in height. Settle left to right: each platform keeps its tier when it clears
            // everything before it, otherwise it tries the other tiers, then a narrower slab.
            var surfaces = new TowerSurface[centres.Count]; var profiles = new int[centres.Count];
            for (int i = 0; i < centres.Count; i++) profiles[i] = rng.I(1 << 20);
            for (int j = 0; j < centres.Count; j++)
            {
                bool settled = false;
                foreach (float width in new[] { widths[j], Math.Max(MinWidth, widths[j] * .75f), MinWidth })
                {
                    int[] order = { tiers[j], tiers[j] < 3 ? tiers[j] + 1 : tiers[j] - 1, tiers[j] > 1 ? tiers[j] - 1 : tiers[j] + 1, 1, 2, 3 };
                    foreach (int candidate in order)
                    {
                        var surface = Shelf(new Rng(profiles[j], j, 0), centres[j], width, TierHeights[candidate - 1] + Uplift(ground, centres[j] - width * .5f, centres[j] + width * .5f), false);
                        bool clear = true;
                        for (int i = 0; i < j && clear; i++) if (TooClose(surfaces[i], surface)) clear = false;
                        if (!clear) continue;
                        surfaces[j] = surface; tiers[j] = candidate; widths[j] = width; settled = true; break;
                    }
                    if (settled) break;
                }
                if (!settled) { LastRejection = "tier conflicts unresolved"; return null; }
            }
            var platforms = new List<TowerPlatform>();
            for (int i = 0; i < centres.Count; i++) platforms.Add(new TowerPlatform(surfaces[i], tiers[i]));
            // Three narrower shelves zigzag above crown platforms whose columns are free of other platforms; on the
            // long floor two or three such towers stand well apart.
            var candidates = new List<int>();
            for (int i = 0; i < centres.Count; i++) if (tiers[i] == 3) candidates.Add(i);
            if (candidates.Count == 0) { LastRejection = "no crown platform"; return null; }
            for (int k = candidates.Count - 1; k > 0; k--) { int swap = rng.I(k + 1); int t = candidates[k]; candidates[k] = candidates[swap]; candidates[swap] = t; }
            int stacks = 0, wanted = 2 + (rng.Chance(.5f) ? 1 : 0); var towers = new List<float>();
            foreach (int i in candidates)
            {
                if (stacks >= wanted) break;
                bool apart = true; foreach (float other in towers) if (Math.Abs(other - centres[i]) < 12) apart = false;
                if (!apart) continue;
                float direction = rng.Chance(.5f) ? 1 : -1, width = Math.Min(widths[i], 3.2f), top = surfaces[i].MaxHeight;
                float[] offsets = { direction * rng.R(.5f, .8f), -direction * rng.R(.15f, .35f), direction * rng.R(.35f, .55f) };
                var stack = new List<TowerPlatform>();
                for (int k = 0; k < 3; k++)
                {
                    float cx = centres[i] + offsets[k];
                    cx = Math.Max(cx, PlatformLeft + width * .5f + .1f); cx = Math.Min(cx, PlatformRight - width * .5f - .1f);
                    stack.Add(new TowerPlatform(Shelf(rng, cx, width, top + ShelfRise * (k + 1), true), 4 + k));
                }
                bool clear = true;
                foreach (var shelf in stack) foreach (var other in platforms) if (TooClose(shelf.Surface, other.Surface)) clear = false;
                if (!clear) continue;
                platforms.AddRange(stack); stacks++; towers.Add(centres[i]);
            }
            if (stacks == 0) { LastRejection = "no free column for shelves"; return null; }
            foreach (var p in platforms)
            {
                var s = p.Surface;
                if (s.Left < PlatformLeft || s.Right > PlatformRight || Math.Abs(s.CenterX - SpawnX) <= s.Width * .5f + .55f) { LastRejection = "platform out of bounds"; return null; }
                if (p.Tier <= 3 && s.ClearanceOver(ground, .125f) < MinClearance) { LastRejection = "platform too low over the hills"; return null; }
            }
            return platforms;
        }

        /// <summary>Traps besides the launch pads: three on the first floor, one more every eight floors, at most nine.</summary>
        public static int HazardCount(int floor) { return Math.Min(9, 3 + (Math.Max(1, floor) - 1) / 8); }

        /// <summary>
        /// Spikes line the bottom of most pits; flame vents, rockfall zones and bolt-spitting totems fill the rest of the
        /// floor's trap budget, and launch pads stand under up to two crown towers. All of it keeps clear of the spawn
        /// lane, the exits, the feature spot and each other, so every trap can be walked around or jumped.
        /// </summary>
        private static List<TowerHazard> PlanHazards(int runSeed, int floor, TowerFloorLayout layout)
        {
            var rng = new Rng(runSeed, floor, 4099);
            var hazards = new List<TowerHazard>();
            float right = (IsBranchFloor(floor) ? ExitX - 10.7f : ExitX) - HazardExitClear;
            var ground = layout.Ground;
            bool Free(float l, float r)
            {
                if (l < HazardLeft || r > right) return false;
                if (IsFeatureFloor(floor) && r > FeatureX - FeatureClear && l < FeatureX + FeatureClear) return false;
                foreach (var h in hazards) if (r > h.Left - HazardGap && l < h.Right + HazardGap) return false;
                return true;
            }
            bool Level(float l, float r, float tolerance)
            {
                float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
                for (float x = l; x <= r + .0001f; x += .125f) { float h = ground.HeightAt(x); lo = Math.Min(lo, h); hi = Math.Max(hi, h); }
                return hi - lo <= tolerance;
            }
            bool OpenSky(float l, float r)
            {
                foreach (var p in layout.Platforms) if (p.Surface.Right > l - .3f && p.Surface.Left < r + .3f) return false;
                return true;
            }
            int wanted = HazardCount(floor), traps = 0;
            foreach (var dip in Pits(ground))
            {
                if (traps >= wanted) break;
                float width = Math.Max(SpikeMinWidth, Math.Min(SpikeMaxWidth, dip[1] * .3f));
                if (!rng.Chance(.85f) || !Free(dip[0] - width * .5f, dip[0] + width * .5f)) continue;
                hazards.Add(new TowerHazard(TowerHazardKind.Spikes, dip[0], width, -1, 0)); traps++;
            }
            // Launch pads sit on level ground under the crowns of the shelf towers.
            int pads = 0;
            for (int i = 0; i < layout.Platforms.Count && pads < 2; i++)
            {
                if (layout.Platforms[i].Tier != 3) continue;
                var crown = layout.Platforms[i].Surface; bool tower = false;
                foreach (var p in layout.Platforms) if (p.Tier == 4 && Math.Abs(p.Surface.CenterX - crown.CenterX) < 1.2f) tower = true;
                float x = crown.CenterX;
                if (!tower || !Level(x - PadWidth * .5f, x + PadWidth * .5f, .12f) || !Free(x - PadWidth * .5f, x + PadWidth * .5f)) continue;
                hazards.Add(new TowerHazard(TowerHazardKind.JumpPad, x, PadWidth, -1, 0)); pads++;
            }
            var totemSpots = new List<int>();
            for (int i = 0; i < layout.Platforms.Count; i++)
            {
                var p = layout.Platforms[i];
                if ((p.Tier == 2 || p.Tier == 3) && p.Surface.Width >= 3 && Math.Abs(p.Surface.SlopeAt(p.Surface.CenterX)) < .12f) totemSpots.Add(i);
            }
            for (int attempt = 0; attempt < 240 && traps < wanted; attempt++)
            {
                // Spikes and vents from the first floor; rockfall from the second; totems from the third.
                int roll = rng.I(10);
                var kind = roll < 3 ? TowerHazardKind.Spikes : roll < 6 ? TowerHazardKind.FlameVent : roll < 8 ? TowerHazardKind.Rockfall : TowerHazardKind.BoltTotem;
                if (kind == TowerHazardKind.Rockfall && floor < 2 || kind == TowerHazardKind.BoltTotem && floor < 3) kind = TowerHazardKind.FlameVent;
                if (kind == TowerHazardKind.BoltTotem)
                {
                    if (totemSpots.Count == 0) continue;
                    int support = totemSpots[rng.I(totemSpots.Count)]; var s = layout.Platforms[support].Surface;
                    float x = rng.Chance(.5f) ? s.Left + .5f : s.Right - .5f;
                    if (!Free(x - TotemWidth * .5f, x + TotemWidth * .5f)) continue;
                    hazards.Add(new TowerHazard(kind, x, TotemWidth, support, rng.R(0, 3))); traps++;
                    continue;
                }
                float width = kind == TowerHazardKind.Spikes ? rng.R(SpikeMinWidth, SpikeMaxWidth) : kind == TowerHazardKind.FlameVent ? VentWidth : RockfallWidth;
                float cx = rng.R(HazardLeft + width * .5f, right - width * .5f), l = cx - width * .5f, r = cx + width * .5f;
                if (!Free(l, r)) continue;
                if (kind == TowerHazardKind.Spikes && !Level(l, r, .3f)) continue;
                if (kind == TowerHazardKind.FlameVent && !Level(l, r, .08f)) continue;
                if (kind == TowerHazardKind.Rockfall && (!Level(l, r, .5f) || !OpenSky(l, r))) continue;
                hazards.Add(new TowerHazard(kind, cx, width, -1, kind == TowerHazardKind.FlameVent ? rng.R(0, 3.4f) : 0)); traps++;
            }
            hazards.Sort((a, b) => a.X.CompareTo(b.X));
            return hazards;
        }

        /// <summary>Pits carved below the base ground: centre (lowest node), width and depth.</summary>
        private static List<float[]> Pits(TowerSurface ground)
        {
            var pits = new List<float[]>(); float start = float.NaN, low = 0, lowX = 0;
            for (int i = 0; i < ground.NodeCount; i++)
            {
                float x = ground.NodeX(i), y = ground.NodeY(i);
                if (y < BaseGround - .05f)
                {
                    if (float.IsNaN(start)) { start = x; low = y; lowX = x; }
                    else if (y < low) { low = y; lowX = x; }
                }
                else if (!float.IsNaN(start))
                {
                    if (BaseGround - low > .5f) pits.Add(new[] { lowX, x - start, BaseGround - low });
                    start = float.NaN;
                }
            }
            return pits;
        }

        /// <summary>Known-good layouts on flat ground, used only if every random attempt failed verification.</summary>
        private static TowerFloorLayout Fallback(int runSeed, int floor)
        {
            var ground = GroundSurface(new List<Bump>());
            float[][] zigzag = { new[] { -9.6f, -1.8f, 3.2f }, new[] { -6.8f, -.4f, 3f }, new[] { -3.6f, 1f, 3.2f }, new[] { .1f, -.4f, 3.2f }, new[] { 3.4f, -1.8f, 3.6f },
                new[] { 7.3f, -.4f, 3.2f }, new[] { 10.8f, 1f, 3.2f }, new[] { 14.6f, -.4f, 3.2f }, new[] { 18.3f, -1.8f, 3.4f }, new[] { 21.6f, -.4f, 3.4f } };
            float[][] towers = { new[] { -9.6f, -1.8f, 3f }, new[] { -7f, -.4f, 2.8f }, new[] { -4.2f, 1f, 3.6f }, new[] { -2.4f, -1.8f, 3.2f }, new[] { .7f, -.4f, 2.6f }, new[] { 6f, -1.8f, 4.8f },
                new[] { 11.7f, -1.8f, 3.2f }, new[] { 14.8f, -.4f, 2.8f }, new[] { 17.7f, 1f, 3.6f }, new[] { 20.3f, -1.8f, 3.4f }, new[] { 23.1f, -.4f, 2.8f } };
            bool even = floor % 2 == 0; var spec = even ? towers : zigzag;
            var platforms = new List<TowerPlatform>(); bool stacked = false;
            // The pattern repeats down the long floor, each copy shifted by 34 units.
            for (float shift = 0; shift < PlatformRight; shift += 34)
                foreach (var p in spec)
                {
                    float cx = p[0] + shift;
                    if (cx + p[2] * .5f > PlatformRight) continue;
                    int tier = Math.Abs(p[1] + 1.8f) < .01f ? 1 : Math.Abs(p[1] + .4f) < .01f ? 2 : 3;
                    platforms.Add(new TowerPlatform(new TowerSurface(new[] { cx - p[2] * .5f, cx + p[2] * .5f }, new[] { p[1], p[1] }), tier));
                    if (tier != 3 || stacked) continue;
                    stacked = true;
                    float[] offsets = { .65f, -.25f, .45f };
                    for (int k = 0; k < 3; k++)
                    {
                        float sx = cx + offsets[k], y = p[1] + ShelfRise * (k + 1);
                        platforms.Add(new TowerPlatform(new TowerSurface(new[] { sx - 1.6f, sx + 1.6f }, new[] { y, y }), 4 + k));
                    }
                }
            return new TowerFloorLayout(unchecked(runSeed * 31 + floor), even ? "쌍둥이 탑" : "오르막 지그재그", floor, ground, platforms, ExitX);
        }
    }
}

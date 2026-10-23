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

    /// <summary>The generated ground profile and platforms of one floor.</summary>
    public sealed class TowerFloorLayout
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public string Archetype { get; private set; }
        public int Floor { get; private set; }
        public TowerSurface Ground { get; private set; }
        public IReadOnlyList<TowerPlatform> Platforms { get; private set; }
        public float HighestPlatform { get; private set; }
        public float HighestGround { get { return Ground.MaxHeight; } }

        internal TowerFloorLayout(int id, string archetype, int floor, TowerSurface ground, List<TowerPlatform> platforms)
        {
            Id = id; Archetype = archetype; Floor = floor; Ground = ground; Platforms = platforms.AsReadOnly();
            HighestPlatform = TowerTerrainGenerator.BaseGround;
            foreach (var p in platforms) HighestPlatform = Math.Max(HighestPlatform, p.Surface.MaxHeight);
            Name = archetype + " · 발판 " + platforms.Count;
        }

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
        public const float MaxRise = 2.05f, HeroMinX = -15.4f, HeroMaxX = 28.4f;
        /// <summary>
        /// A plan must land on its target at every one of these frame steps (the .04 cap, 30, 60 and 90 fps), so a
        /// trajectory that only grazes past a neighbouring platform at one frame rate is never counted as a route.
        /// </summary>
        private static readonly float[] ConfirmSteps = { .04f, 1f / 30, 1f / 60, 1f / 90 };

        public static Plan Find(TowerFloorLayout layout, int source, int target, float dt)
        {
            var destination = layout.Platforms[target].Surface;
            TowerSurface origin = source < 0 ? null : layout.Platforms[source].Surface;
            float sourceLow = origin == null ? layout.Ground.HeightAt(destination.CenterX) : origin.MaxHeight;
            if (destination.MinHeight - sourceLow > MaxRise) return null;
            if (origin != null && destination.MaxHeight < origin.MinHeight - .1f) return null;
            float left = origin == null ? destination.CenterX - 3.5f : origin.Left + .3f;
            float right = origin == null ? destination.CenterX + 3.5f : origin.Right - .3f;
            left = Math.Max(left, HeroMinX); right = Math.Min(right, HeroMaxX);
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

        /// <summary>Walks toward <paramref name="aimX"/> after a jump from <paramref name="launch"/> and reports whether the first landing is well inside the target.</summary>
        private static bool Lands(TowerFloorLayout layout, int source, int target, float launch, float aimX, float dt)
        {
            var destination = layout.Platforms[target].Surface;
            float x = launch, y = layout.SurfaceHeight(source, launch), velocity = TowerPhysics.JumpVelocity;
            bool grounded = false; int support = -1;
            for (int frame = 0, frames = (int)Math.Ceiling(4 / dt); frame < frames; frame++)
            {
                float xOld = x; float step = TowerPhysics.WalkSpeed * dt;
                x += Math.Max(-step, Math.Min(step, aimX - x));
                x = Math.Max(HeroMinX, Math.Min(HeroMaxX, x));
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
    /// Random rolling terrain and sloped platforms, decided by the run seed and floor alone. Every floor
    /// keeps a flat landing zone at the spawn, a flat exit lane (and gate lane on branch floors), tiered
    /// platforms with ordinary-jump reachability, and three narrower shelves above a crown platform.
    /// </summary>
    public static class TowerTerrainGenerator
    {
        public const float BaseGround = -3, MaxHill = 1.6f, MaxGroundSlope = .55f, MaxPlatformSlope = .34f;
        public const float SpawnX = -12, ExitX = 26, FeatureX = 5;
        public const float TerrainLeft = -40, TerrainRight = 56, NodeSpacing = .25f;
        public const float MinWidth = 2.6f, MaxWidth = 4.6f, MinClearance = 1.15f, MinSeparation = 1f;
        public static readonly float[] TierHeights = { -1.8f, -.4f, 1f };
        public const float ShelfRise = 1.4f;
        public const int BranchInterval = 5, MaxAttempts = 40;
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
        private static readonly string[] Archetypes = { "평탄한 들판", "완만한 언덕", "쌍봉 능선", "중앙 고원", "계단식 대지", "내리막 비탈", "분지 골짜기", "물결 능선" };

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
            zones.Add(new[] { IsBranchFloor(floor) ? 15.3f : 23.8f, TerrainRight });
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
            int archetype = ArchetypeFor(runSeed, floor);
            LastWasFallback = false;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                LastAttempts = attempt + 1;
                var layout = Build(runSeed, floor, archetype, attempt);
                if (layout == null) continue;
                var plans = TowerJumpModel.Reachable(layout, .04f);
                if (plans.Count != layout.Platforms.Count) { LastRejection = "unreachable at dt=.04: " + (plans.Count) + "/" + layout.Platforms.Count; continue; }
                int entries = 0; foreach (var plan in plans) if (plan.source < 0) entries++;
                if (entries < 2) { LastRejection = "fewer than two ground entries"; continue; }
                if (!TowerJumpModel.AllReachable(layout, 1f / 60)) { LastRejection = "unreachable at 60fps"; continue; }
                return layout;
            }
            LastWasFallback = true;
            return Fallback(runSeed, floor);
        }

        private static TowerFloorLayout Build(int runSeed, int floor, int archetype, int attempt)
        {
            var rng = new Rng(runSeed, floor, attempt);
            var bumps = PlanBumps(rng, archetype, FlatZones(floor));
            var ground = GroundSurface(bumps);
            var platforms = PlanPlatforms(rng, ground);
            if (platforms == null) return null;
            int id = unchecked((int)(new Rng(runSeed, floor, attempt).Next() & 0x7fffffff));
            return new TowerFloorLayout(id, Archetypes[archetype], floor, ground, platforms);
        }

        private static List<float[]> FreeRegions(List<float[]> zones)
        {
            // Hills live strictly between flat zones, which are sorted left to right and never overlap.
            var sorted = new List<float[]>(zones); sorted.Sort((a, b) => a[0].CompareTo(b[0]));
            var free = new List<float[]>();
            for (int i = 0; i + 1 < sorted.Count; i++) if (sorted[i + 1][0] - sorted[i][1] > 4) free.Add(new[] { sorted[i][1], sorted[i + 1][0] });
            return free;
        }

        private static List<Bump> PlanBumps(Rng rng, int archetype, List<float[]> zones)
        {
            var free = FreeRegions(zones); var bumps = new List<Bump>();
            void Add(float aMin, float aMax, float wMin, float wMax, bool plateau, float preferLeft)
            {
                float a = rng.R(aMin, aMax);
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    float w = rng.R(wMin, wMax);
                    w = Math.Max(w, plateau ? RampLength(a) + 1.6f : 2.9f * a + .2f);
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
                default: Add(.4f, .8f, 3.5f, 4.5f, false, 0); Add(.4f, .8f, 3.5f, 4.5f, false, -1); Add(.4f, .8f, 3.5f, 4.5f, false, 1); break;
            }
            return bumps;
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
                xs[i] = x; ys[i] = Math.Min(BaseGround + MaxHill, h);
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
                if (x + width * .5f > 24.8f) break;
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
            if (centres.Count < 8) { LastRejection = "too few platforms"; return null; }
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
            // Three narrower shelves zigzag above a crown platform whose column is free of other platforms.
            var candidates = new List<int>();
            for (int i = 0; i < centres.Count; i++) if (tiers[i] == 3) candidates.Add(i);
            if (candidates.Count == 0) { LastRejection = "no crown platform"; return null; }
            for (int k = candidates.Count - 1; k > 0; k--) { int swap = rng.I(k + 1); int t = candidates[k]; candidates[k] = candidates[swap]; candidates[swap] = t; }
            int stacks = 0, wanted = 1 + (candidates.Count > 1 && rng.Chance(.6f) ? 1 : 0);
            foreach (int i in candidates)
            {
                if (stacks >= wanted) break;
                float direction = rng.Chance(.5f) ? 1 : -1, width = Math.Min(widths[i], 3.2f), top = surfaces[i].MaxHeight;
                float[] offsets = { direction * rng.R(.5f, .8f), -direction * rng.R(.15f, .35f), direction * rng.R(.35f, .55f) };
                var stack = new List<TowerPlatform>();
                for (int k = 0; k < 3; k++)
                {
                    float cx = centres[i] + offsets[k];
                    cx = Math.Max(cx, -15.4f + width * .5f + .1f); cx = Math.Min(cx, 25 - width * .5f - .1f);
                    stack.Add(new TowerPlatform(Shelf(rng, cx, width, top + ShelfRise * (k + 1), true), 4 + k));
                }
                bool clear = true;
                foreach (var shelf in stack) foreach (var other in platforms) if (TooClose(shelf.Surface, other.Surface)) clear = false;
                if (!clear) continue;
                platforms.AddRange(stack); stacks++;
            }
            if (stacks == 0) { LastRejection = "no free column for shelves"; return null; }
            foreach (var p in platforms)
            {
                var s = p.Surface;
                if (s.Left < -15.4f || s.Right > 25 || Math.Abs(s.CenterX - SpawnX) <= s.Width * .5f + .55f) { LastRejection = "platform out of bounds"; return null; }
                if (p.Tier <= 3 && s.ClearanceOver(ground, .125f) < MinClearance) { LastRejection = "platform too low over the hills"; return null; }
            }
            return platforms;
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
            foreach (var p in spec)
            {
                int tier = Math.Abs(p[1] + 1.8f) < .01f ? 1 : Math.Abs(p[1] + .4f) < .01f ? 2 : 3;
                platforms.Add(new TowerPlatform(new TowerSurface(new[] { p[0] - p[2] * .5f, p[0] + p[2] * .5f }, new[] { p[1], p[1] }), tier));
                if (tier != 3 || stacked) continue;
                stacked = true;
                float[] offsets = { .65f, -.25f, .45f };
                for (int k = 0; k < 3; k++)
                {
                    float cx = p[0] + offsets[k], y = p[1] + ShelfRise * (k + 1);
                    platforms.Add(new TowerPlatform(new TowerSurface(new[] { cx - 1.6f, cx + 1.6f }, new[] { y, y }), 4 + k));
                }
            }
            return new TowerFloorLayout(unchecked(runSeed * 31 + floor), even ? "쌍둥이 탑" : "오르막 지그재그", floor, ground, platforms);
        }
    }
}

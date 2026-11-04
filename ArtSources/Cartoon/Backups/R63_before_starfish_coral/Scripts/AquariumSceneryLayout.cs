using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    // R37: one seaweed plant or anemone of a game's scenery (world position of its foot on the sand or a ledge).
    [Serializable]
    public struct AquariumScenePlant
    {
        public Point position;
        public float yaw, height, phase, radius;
        // Seaweed: 0 broad kelp, 1 strap kelp, 2 eelgrass, 3 feathery red, 4 sargassum.
        public int form;
        public bool ledge;
    }
    [Serializable]
    public struct AquariumSceneAnemone
    {
        public Point position;
        public float yaw, size;
        public int kind;
        public bool ledge;
    }

    // R37: the tank's scenery is laid out anew for every game, from one seed the host picks and sends in the
    // snapshot (every machine builds the same layout from it):
    //  * every monumental cliff (AquariumMonolithLayout group) turns about its own centre by a random angle,
    //    only to angles that keep it clear of the glass, the walkways, the dive entry, the reef rocks, the
    //    spawn points, the bubble vents and the other cliffs (never closer than the authored layout already was);
    //  * every reef rock turns by a random angle (its collision is round, so only the look changes);
    //  * the four return drains are picked from the open floor that is left;
    //  * seaweed and anemones are scattered at random over the sand and the cliff ledges, spaced by their spread
    //    so they no longer pile into each other, and kept off the drains, walkways and glass.
    // The baked layout (seed 0) is the authored one; the environment builder always works in it.
    public sealed partial class AquariumSceneryLayout
    {
        public readonly int Seed;
        // Degrees about each cliff's pivot / each reef rock's centre (Unity's yaw: +x turns toward -z).
        public readonly float[] MonolithYaw, RockYaw;
        // World bounds of each cliff as it now stands (for the ray pre-test).
        public readonly Point[] MonolithMinimum, MonolithMaximum;
        // Null in the baked layout: the authored drain sites, plants, anemones and clownfish spots are used.
        public Point[] DrainSites { get; private set; }
        public AquariumScenePlant[] Plants { get; private set; }
        public AquariumSceneAnemone[] Anemones { get; private set; }
        public Point[] AnemoneSpots { get; private set; }
        public Point[] CoralSpots { get; private set; }
        public bool Baked { get { return Seed == 0; } }
        // R57: the map this layout belongs to (AquariumMaps). R58: the twin tanks have no cliffs or reef rocks of the
        // default tank; their own rocks, life and drains come from BuildTwin.
        public int Map { get; private set; }
        private readonly float[] cos, sin, rockCos, rockSin;

        public static readonly Point[] MonolithPivot;
        public static readonly AquariumSceneryLayout BakedLayout;
        private static AquariumSceneryLayout current;
        // R62: a layout worker thread sees the layout it is building (Use) instead of the game's.
        [ThreadStatic] private static AquariumSceneryLayout threadCurrent;
        [ThreadStatic] private static bool layoutWorker;
        public static AquariumSceneryLayout Current { get { return threadCurrent ?? current; } }
        // Bumped whenever Apply switches the layout (presentation and caches compare it).
        public static int Version { get; private set; }
        public static event Action Changed;

        static AquariumSceneryLayout()
        {
            int groups = AquariumMonolithLayout.Groups.Length;
            MonolithPivot = new Point[groups];
            for (int g = 0; g < groups; g++)
            {
                var min = AquariumMonolithLayout.Minimum[g]; var max = AquariumMonolithLayout.Maximum[g];
                MonolithPivot[g] = new Point((min.x + max.x) * .5f, 0, (min.z + max.z) * .5f);
            }
            BakedLayout = new AquariumSceneryLayout(0, new float[groups], new float[AquariumModel.RockRadii.Length]);
            current = BakedLayout;
        }

        private AquariumSceneryLayout(int seed, float[] monolithYaw, float[] rockYaw)
        {
            Seed = seed; MonolithYaw = monolithYaw; RockYaw = rockYaw;
            int groups = monolithYaw.Length;
            cos = new float[groups]; sin = new float[groups]; MonolithMinimum = new Point[groups]; MonolithMaximum = new Point[groups];
            for (int g = 0; g < groups; g++)
            {
                double a = monolithYaw[g] * Math.PI / 180; cos[g] = (float)Math.Cos(a); sin[g] = (float)Math.Sin(a);
                if (monolithYaw[g] == 0) { cos[g] = 1; sin[g] = 0; }
                var min = AquariumMonolithLayout.Minimum[g]; var max = AquariumMonolithLayout.Maximum[g];
                var lo = new Point(float.MaxValue, min.y, float.MaxValue); var hi = new Point(float.MinValue, max.y, float.MinValue);
                for (int corner = 0; corner < 4; corner++)
                {
                    var w = MonolithToWorld(new Point((corner & 1) == 0 ? min.x : max.x, 0, (corner & 2) == 0 ? min.z : max.z), g);
                    lo.x = Math.Min(lo.x, w.x); lo.z = Math.Min(lo.z, w.z); hi.x = Math.Max(hi.x, w.x); hi.z = Math.Max(hi.z, w.z);
                }
                MonolithMinimum[g] = lo; MonolithMaximum[g] = hi;
            }
            rockCos = new float[rockYaw.Length]; rockSin = new float[rockYaw.Length];
            for (int i = 0; i < rockYaw.Length; i++)
            {
                double a = rockYaw[i] * Math.PI / 180; rockCos[i] = (float)Math.Cos(a); rockSin[i] = (float)Math.Sin(a);
                if (rockYaw[i] == 0) { rockCos[i] = 1; rockSin[i] = 0; }
            }
        }

        // ---------------------------------------------------------------- frames
        public Point MonolithToLocal(Point p, int group)
        {
            float c = cos[group], s = sin[group]; if (s == 0 && c == 1) return p;
            var o = MonolithPivot[group]; float x = p.x - o.x, z = p.z - o.z;
            return new Point(o.x + c * x - s * z, p.y, o.z + s * x + c * z);
        }
        public Point MonolithToWorld(Point p, int group)
        {
            float c = cos[group], s = sin[group]; if (s == 0 && c == 1) return p;
            var o = MonolithPivot[group]; float x = p.x - o.x, z = p.z - o.z;
            return new Point(o.x + c * x + s * z, p.y, o.z - s * x + c * z);
        }
        public Point MonolithDirectionToWorld(Point d, int group)
        { float c = cos[group], s = sin[group]; return new Point(c * d.x + s * d.z, d.y, -s * d.x + c * d.z); }
        // A point of a reef rock's authored decoration, turned with the rock (height unchanged).
        public Point RockToWorld(Point p, int rock)
        {
            float c = rockCos[rock], s = rockSin[rock]; if (s == 0 && c == 1) return p;
            var o = AquariumModel.Rocks[rock]; float x = p.x - o.x, z = p.z - o.z;
            return new Point(o.x + c * x + s * z, p.y, o.z - s * x + c * z);
        }
        // The reef rock's own body, rubble skirt and coral cap; its farther planting pockets are not kept when it turns.
        public static float ReefKeep(int rock) { return AquariumModel.RockRadii[rock] * 1.25f + .5f; }

        // ---------------------------------------------------------------- switching
        public static void Apply(int seed)
        {
            int map = AquariumMaps.Current;
            if (seed == current.Seed && current.Map == map) return;
            current = map == AquariumMaps.Twin ? (seed == 0 ? TwinBaked : GetTwin(seed)) : seed == 0 ? BakedLayout : Create(seed);
            Version++;
            if (Changed != null) Changed();
        }
        // R57: AquariumMaps.Select switched the map: start from its authored layout (the game applies its seed next).
        public static void MapChanged()
        {
            current = AquariumMaps.IsTwin ? TwinBaked : BakedLayout;
            Version++;
            if (Changed != null) Changed();
        }
        private static AquariumSceneryLayout twinBaked;
        public static AquariumSceneryLayout TwinBaked { get { if (twinBaked == null) twinBaked = GetTwin(0); return twinBaked; } }
        // R58: the twin tanks get their own rocks, plants, corals and anemones (AquariumSceneryLayout.Twin.cs).
        private static AquariumSceneryLayout CreateTwin(int seed) { return BuildTwin(seed); }

        // R62: twin layouts are laid out on a worker thread ahead of time (the next game's seed is known in the lobby,
        // the authored one at load), so switching to one is free. The worker answers every map and layout query for
        // itself (AquariumMaps.UseOnThisThread, threadCurrent); the result is the same as building it here.
        private static readonly Dictionary<int, AquariumSceneryLayout> twinReady = new Dictionary<int, AquariumSceneryLayout>();
        private static readonly List<int> twinOrder = new List<int>();
        private static readonly HashSet<int> twinBuilding = new HashSet<int>();
        private static readonly object twinLock = new object();
        public static void PrepareTwin(int seed)
        {
            lock (twinLock)
            {
                if (twinReady.ContainsKey(seed) || twinBuilding.Contains(seed)) return;
                twinBuilding.Add(seed);
            }
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                AquariumSceneryLayout built = null;
                try { layoutWorker = true; AquariumMaps.UseOnThisThread(AquariumMaps.Twin); built = CreateTwin(seed); }
                catch (Exception) { }
                finally
                {
                    AquariumMaps.UseOnThisThread(-1); layoutWorker = false; threadCurrent = null;
                    lock (twinLock) { twinBuilding.Remove(seed); if (built != null) Keep(seed, built); System.Threading.Monitor.PulseAll(twinLock); }
                }
            });
        }
        private static void Keep(int seed, AquariumSceneryLayout layout)
        {
            twinReady[seed] = layout; twinOrder.Remove(seed); twinOrder.Add(seed);
            // A few recent seeds (and the authored one) are kept.
            while (twinOrder.Count > 4) { int old = twinOrder[0] == 0 ? twinOrder[1] : twinOrder[0]; twinOrder.Remove(old); twinReady.Remove(old); }
        }
        // Ready (or already shown) without any work on this thread.
        public static bool TwinReady(int seed)
        {
            if (current.Seed == seed && current.Map == AquariumMaps.Twin) return true;
            lock (twinLock) return twinReady.ContainsKey(seed);
        }
        // The layout of this seed: the prepared one, or waits for the worker, or (never prepared) builds it here.
        private static AquariumSceneryLayout GetTwin(int seed)
        {
            lock (twinLock)
            {
                AquariumSceneryLayout ready;
                for (int waits = 0; twinBuilding.Contains(seed) && waits < 600; waits++) System.Threading.Monitor.Wait(twinLock, 50);
                if (twinReady.TryGetValue(seed, out ready)) return ready;
            }
            var built = CreateTwin(seed);
            lock (twinLock) Keep(seed, built);
            return built;
        }
        private sealed class Scope : IDisposable
        {
            private readonly AquariumSceneryLayout previous; private readonly bool worker; private bool done;
            public Scope(AquariumSceneryLayout use)
            {
                worker = layoutWorker;
                if (worker) { previous = threadCurrent; threadCurrent = use; } else { previous = current; current = use; }
            }
            public void Dispose()
            {
                if (done) return; done = true;
                if (worker) threadCurrent = previous; else current = previous;
            }
        }
        // Temporarily evaluate the tank in the authored layout (environment building and its checks).
        public static IDisposable Baking() { return new Scope(BakedLayout); }
        private static IDisposable Use(AquariumSceneryLayout layout) { return new Scope(layout); }

        // ---------------------------------------------------------------- cliff footprints
        private const float Cell = .5f;
        private const float GlassGap = 2.2f, WalkwayGap = 2.2f, EntryGap = 1.6f, CliffGap = 2.6f, RockGap = 1.6f, SpawnGap = 1.6f, VentGap = 1.2f;
        private sealed class Footprint
        {
            public Point[] cells;        // authored xz of every column with rock above the sand
            public Point[] outline;      // the cells on its edge
            public float reach;          // farthest outline cell from the pivot
            public float glass, walkway, entry, spawn, vent;   // required clearances (authored layout or less)
            public float[] rock;         // required horizontal clearance to each reef rock's keep radius (-inf = embedded)
            public float[] cliff;        // required gap to each other cliff
            public int area;
        }
        private static Footprint[] footprints;
        private static Point[] spawns, vents;
        // Reef rocks sunk into a cliff in the authored layout (no reef of their own is drawn there).
        private static bool[] embedded;

        private static Footprint[] Footprints()
        {
            if (footprints != null) return footprints;
            int groups = AquariumMonolithLayout.Groups.Length;
            var result = new Footprint[groups];
            spawns = SpawnPoints(); vents = VentPoints();
            for (int g = 0; g < groups; g++)
            {
                var min = AquariumMonolithLayout.Minimum[g]; var max = AquariumMonolithLayout.Maximum[g];
                int nx = (int)Math.Ceiling((max.x - min.x) / Cell) + 1, nz = (int)Math.Ceiling((max.z - min.z) / Cell) + 1;
                var solid = new bool[nx, nz]; var cells = new List<Point>();
                for (int ix = 0; ix < nx; ix++) for (int iz = 0; iz < nz; iz++)
                {
                    float x = min.x + ix * Cell, z = min.z + iz * Cell;
                    // Rock above the lowest sand (deeply buried footings do not count; a turned cliff may stand on lower sand).
                    for (float y = Math.Max(.15f, min.y); y <= Math.Min(max.y, AquariumModel.WaterHeight); y += Cell)
                        if (AquariumMonolithLayout.LocalDistance(new Point(x, y, z), g) < 0) { solid[ix, iz] = true; cells.Add(new Point(x, 0, z)); break; }
                }
                var outline = new List<Point>(); var pivot = MonolithPivot[g]; float reach = 0;
                for (int ix = 0; ix < nx; ix++) for (int iz = 0; iz < nz; iz++)
                {
                    if (!solid[ix, iz]) continue;
                    bool edge = ix == 0 || iz == 0 || ix == nx - 1 || iz == nz - 1 || !solid[ix - 1, iz] || !solid[ix + 1, iz] || !solid[ix, iz - 1] || !solid[ix, iz + 1];
                    if (!edge) continue;
                    var p = new Point(min.x + ix * Cell, 0, min.z + iz * Cell); outline.Add(p);
                    float dx = p.x - pivot.x, dz = p.z - pivot.z; reach = Math.Max(reach, (float)Math.Sqrt(dx * dx + dz * dz));
                }
                result[g] = new Footprint { cells = cells.ToArray(), outline = outline.ToArray(), reach = reach, area = cells.Count };
            }
            // What the authored layout already allows: a turned cliff may come as close as the authored one does, never closer.
            for (int g = 0; g < groups; g++)
            {
                var f = result[g];
                float glass = float.MaxValue, walkway = float.MaxValue, entry = float.MaxValue, spawn = float.MaxValue, vent = float.MaxValue;
                foreach (var p in f.outline)
                {
                    glass = Math.Min(glass, GlassClearance(p)); walkway = Math.Min(walkway, AquariumWalkwayLayout.SignedDistance(p));
                    entry = Math.Min(entry, EntryClearance(p));
                    foreach (var s in spawns) spawn = Math.Min(spawn, Flat(p, s));
                    foreach (var v in vents) vent = Math.Min(vent, Flat(p, v));
                }
                f.glass = Math.Min(GlassGap, glass - .05f); f.walkway = Math.Min(WalkwayGap, walkway - .05f); f.entry = Math.Min(EntryGap, entry - .05f);
                f.spawn = Math.Min(SpawnGap, spawn - .05f); f.vent = Math.Min(VentGap, vent - .05f);
                f.rock = new float[AquariumModel.Rocks.Length];
                for (int i = 0; i < f.rock.Length; i++)
                {
                    float nearest = float.MaxValue;
                    foreach (var p in f.cells) nearest = Math.Min(nearest, Flat(p, AquariumModel.Rocks[i]) - ReefKeep(i));
                    // A rock sunk into this cliff in the authored layout (no reef of its own there) may stay against it.
                    f.rock[i] = nearest < .3f ? float.NegativeInfinity : Math.Min(RockGap, nearest - .05f);
                }
                f.cliff = new float[groups];
                for (int h = 0; h < groups; h++)
                {
                    if (h == g) continue;
                    float nearest = float.MaxValue;
                    foreach (var p in f.outline) foreach (var q in result[h].outline) nearest = Math.Min(nearest, Flat(p, q));
                    f.cliff[h] = Math.Min(CliffGap, nearest - .05f);
                }
            }
            embedded = new bool[AquariumModel.Rocks.Length];
            foreach (var f in result) for (int i = 0; i < embedded.Length; i++) if (float.IsNegativeInfinity(f.rock[i])) embedded[i] = true;
            return footprints = result;
        }
        private static float Flat(Point a, Point b) { float x = a.x - b.x, z = a.z - b.z; return (float)Math.Sqrt(x * x + z * z); }
        // Signed: how far inside the water (negative: how far through the glass).
        private static float GlassClearance(Point p)
        {
            if (!AquariumMapOutline.Contains(p, 0)) return -Flat(p, AquariumMapOutline.Clamp(p, 0));
            float lo = 0, hi = 12;
            for (int i = 0; i < 14; i++) { float mid = (lo + hi) * .5f; if (AquariumMapOutline.Contains(p, mid)) lo = mid; else hi = mid; }
            return lo;
        }
        private static float EntryClearance(Point p)
        {
            if (AquariumEntryLayout.KeepWaterClear(p, 0)) return -1;
            float lo = 0, hi = 12;
            for (int i = 0; i < 14; i++) { float mid = (lo + hi) * .5f; if (!AquariumEntryLayout.KeepWaterClear(p, mid)) lo = mid; else hi = mid; }
            return lo;
        }
        private static Point[] SpawnPoints()
        {
            var list = new List<Point>();
            for (int i = 0; i < 5; i++) list.Add(AquariumMapOutline.LayoutPoint(-9 + i * 2.4f, 2, 0));          // lobby
            for (int i = 0; i < 5; i++) list.Add(AquariumMapOutline.LayoutPoint(-8 + i * 2, 2, -1));            // hiders
            for (int i = 0; i < 2; i++) list.Add(AquariumMapOutline.LayoutPoint(10, 2, -4 + i * 2));            // seekers
            return list.ToArray();
        }
        // AquariumEnvironment.BuildAtmosphere's aeration vents.
        private static Point[] VentPoints()
        {
            return new[] { AquariumMapOutline.LayoutPoint(-10, .2f, 4), AquariumMapOutline.LayoutPoint(-6, .2f, -2), AquariumMapOutline.LayoutPoint(4, .2f, 4),
                AquariumMapOutline.LayoutPoint(10, .2f, -1), AquariumMapOutline.LayoutPoint(0, .2f, -5) };
        }

        private static bool CliffFits(int g, float yaw, float[] placedYaw, bool[] placed)
        {
            var all = Footprints(); var f = all[g]; var pivot = MonolithPivot[g];
            double a = yaw * Math.PI / 180; float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            Func<Point, Point, float, float, Point> turn = (p, o, cc, ss) => { float x = p.x - o.x, z = p.z - o.z; return new Point(o.x + cc * x + ss * z, 0, o.z - ss * x + cc * z); };
            var world = new Point[f.outline.Length];
            for (int i = 0; i < world.Length; i++) world[i] = turn(f.outline[i], pivot, c, s);
            foreach (var w in world)
            {
                if (f.glass >= 0 ? !AquariumMapOutline.Contains(w, f.glass) : GlassClearance(w) < f.glass) return false;
                if (AquariumWalkwayLayout.Contains(w, f.walkway)) return false;
                if (f.entry >= 0 && AquariumEntryLayout.KeepWaterClear(w, f.entry)) return false;
                foreach (var sp in spawns) if (Flat(w, sp) < f.spawn) return false;
                foreach (var v in vents) if (Flat(w, v) < f.vent) return false;
            }
            // Reef rocks: the whole footprint (a rock could otherwise end up inside it).
            for (int i = 0; i < f.rock.Length; i++)
            {
                if (float.IsNegativeInfinity(f.rock[i])) continue;
                var rock = AquariumModel.Rocks[i]; float need = f.rock[i] + ReefKeep(i);
                if (Flat(pivot, rock) > f.reach + need + .5f) continue;
                foreach (var p in f.cells) if (Flat(turn(p, pivot, c, s), rock) < need) return false;
            }
            for (int h = 0; h < placed.Length; h++)
            {
                if (!placed[h] || h == g) continue;
                var other = all[h]; float need = Math.Min(f.cliff[h], other.cliff[g]);
                if (Flat(pivot, MonolithPivot[h]) > f.reach + other.reach + need + .5f) continue;
                double b = placedYaw[h] * Math.PI / 180; float ch = (float)Math.Cos(b), sh = (float)Math.Sin(b);
                var theirs = new Point[other.outline.Length];
                for (int i = 0; i < theirs.Length; i++) theirs[i] = turn(other.outline[i], MonolithPivot[h], ch, sh);
                foreach (var w in world) foreach (var q in theirs) if (Flat(w, q) < need) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- the layout
        public static AquariumSceneryLayout Create(int seed)
        {
            if (seed == 0) return BakedLayout;
            var random = new Random(seed);
            var all = Footprints(); int groups = all.Length;
            var order = new List<int>(); for (int g = 0; g < groups; g++) order.Add(g);
            order.Sort((a, b) => all[b].area.CompareTo(all[a].area));
            var yaw = new float[groups];
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var placed = new bool[groups]; bool ok = true;
                foreach (int g in order)
                {
                    bool found = false;
                    for (int tries = 0; tries < 48 && !found; tries++)
                    {
                        float candidate = (float)(random.NextDouble() * 360);
                        if (CliffFits(g, candidate, yaw, placed)) { yaw[g] = candidate; found = true; }
                    }
                    if (!found && CliffFits(g, 0, yaw, placed)) { yaw[g] = 0; found = true; }
                    if (!found) { ok = false; break; }
                    placed[g] = true;
                }
                if (ok) break;
                for (int g = 0; g < groups; g++) yaw[g] = 0;
            }
            var rockYaw = new float[AquariumModel.Rocks.Length];
            for (int i = 0; i < rockYaw.Length; i++) rockYaw[i] = (float)(random.NextDouble() * 360);
            var layout = new AquariumSceneryLayout(seed, yaw, rockYaw);
            using (Use(layout)) layout.DrainSites = PickDrains(random);
            if (layout.DrainSites == null)
            {
                // Never expected: the turned cliffs left no room for four drains, so they keep their authored angles.
                layout = new AquariumSceneryLayout(seed, new float[groups], rockYaw);
                using (Use(layout)) layout.DrainSites = PickDrains(random);
            }
            using (Use(layout))
            {
                layout.PlaceLife(random);
                layout.CoralSpots = layout.TurnCorals();
            }
            return layout;
        }

        // Four drains on open floor, one per broad quadrant and far apart (as the authored sites were picked).
        private static Point[] PickDrains(Random random)
        {
            var pool = new List<Point>();
            for (float x = -36; x <= 36; x += .75f) for (float z = -23; z <= 25; z += .75f)
            {
                var p = new Point(x, 0, z);
                if (!AquariumDrainLayout.IsClear(p)) continue;
                bool nearVent = false; foreach (var v in VentPoints()) if (Flat(p, v) < 1.8f) { nearVent = true; break; }
                if (!nearVent) pool.Add(p);
            }
            for (int i = pool.Count - 1; i > 0; i--) { int j = random.Next(i + 1); var t = pool[i]; pool[i] = pool[j]; pool[j] = t; }
            foreach (float spacing in new[] { AquariumDrainLayout.MinimumSpacing, 12f, 8f, 4f })
            {
                var picked = new List<Point>();
                if (PickQuadrants(pool, picked, 0, spacing)) return picked.ToArray();
            }
            // Never expected: fall back to the authored sites.
            return null;
        }
        private static bool PickQuadrants(List<Point> pool, List<Point> picked, int sector, float spacing)
        {
            if (sector == AquariumDrainLayout.Count) return true;
            int budget = 0;
            foreach (var p in pool)
            {
                if ((p.x >= 1 ? 1 : 0) + (p.z >= 1 ? 2 : 0) != sector) continue;
                bool fits = true;
                foreach (var q in picked) if (Flat(p, q) < spacing) { fits = false; break; }
                if (!fits) continue;
                picked.Add(p);
                if (PickQuadrants(pool, picked, sector + 1, spacing)) return true;
                picked.RemoveAt(picked.Count - 1);
                if (++budget > 25) break;
            }
            return false;
        }

        // ---------------------------------------------------------------- seaweed and anemones
        private const int SandAnemones = 10, LedgeAnemones = 18, SandPlants = 190, LedgePlants = 26;
        private struct Spot { public Point p; public float r; }
        private sealed class Spacing
        {
            private readonly Dictionary<long, List<Spot>> cells = new Dictionary<long, List<Spot>>();
            private const float Size = 2.5f;
            private static long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }
            public bool Free(Point p, float r, float factor)
            {
                int cx = (int)Math.Floor(p.x / Size), cz = (int)Math.Floor(p.z / Size);
                for (int x = cx - 2; x <= cx + 2; x++) for (int z = cz - 2; z <= cz + 2; z++)
                {
                    List<Spot> list; if (!cells.TryGetValue(Key(x, z), out list)) continue;
                    foreach (var s in list)
                    {
                        float dx = s.p.x - p.x, dz = s.p.z - p.z, dy = Math.Abs(s.p.y - p.y);
                        float need = (s.r + r) * factor + .12f;
                        if (dy < 2.5f && dx * dx + dz * dz < need * need) return false;
                    }
                }
                return true;
            }
            public void Add(Point p, float r)
            {
                long key = Key((int)Math.Floor(p.x / Size), (int)Math.Floor(p.z / Size));
                List<Spot> list; if (!cells.TryGetValue(key, out list)) cells[key] = list = new List<Spot>();
                list.Add(new Spot { p = p, r = r });
            }
        }
        // How far a seaweed's blades spread from its foot (AquariumEnvironment.Seaweed's shapes).
        public static float PlantSpread(int form, float height)
        {
            switch (form)
            {
                case 0: return .3f + .22f * height;
                case 1: return .12f + .3f * height;
                case 2: return .1f + .26f * height;
                case 3: return .25f + .16f * height;
                case GrassForm: return .3f + .1f * height;
                default: return .3f + .19f * height;
            }
        }
        public static float AnemoneSpread(float size) { return size * 1.15f + .1f; }

        private bool SandClear(Point p, float radius, float height)
        {
            if (!AquariumMapOutline.Contains(p, radius + .3f) || AquariumWalkwayLayout.Contains(p, radius + .45f) || AquariumEntryLayout.KeepWaterClear(p, radius + .3f)) return false;
            float ground = AquariumSeabed.Height(p);
            // Clear of the cliffs from the sand up to its tips (overhangs included).
            for (float h = .2f; h < height + .3f; h += .55f)
                if (AquariumMonolithLayout.Overlaps(new Point(p.x, ground + h, p.z), radius * .75f + .12f)) return false;
            for (int i = 0; i < AquariumModel.Rocks.Length; i++)
                if (Flat(p, AquariumModel.Rocks[i]) < ReefKeep(i) - .25f + radius * .55f) return false;
            if (DrainSites != null) foreach (var d in DrainSites) if (Flat(p, d) < AquariumDrainLayout.ClearanceRadius + radius * .6f + .2f) return false;
            foreach (var v in VentPoints()) if (Flat(p, v) < .9f) return false;
            return true;
        }
        // A random point to try: along the glass, at the foot of a reef or cliff, or anywhere on the floor.
        private Point SandCandidate(Random random, out bool edge)
        {
            double roll = random.NextDouble(); edge = false;
            if (roll < .36)
            {
                edge = true;
                float t = (float)random.NextDouble();
                var inner = AquariumMapOutline.PointOn(t); var outer = AquariumMapOutline.PointOn(t, true);
                var inward = (inner - outer); inward.y = 0; inward = inward.Normalized;
                return inner + inward * (.7f + (float)random.NextDouble() * 2.8f);
            }
            if (roll < .62)
            {
                double which = random.NextDouble(); double a = random.NextDouble() * Math.PI * 2;
                Point foot; float reach;
                if (which < .5)
                {
                    int i = random.Next(AquariumModel.Rocks.Length); foot = AquariumModel.Rocks[i]; reach = ReefKeep(i) + .2f + (float)random.NextDouble() * 2.4f;
                }
                else
                {
                    int g = random.Next(AquariumMonolithLayout.Groups.Length); var f = Footprints()[g];
                    foot = MonolithToWorld(f.outline[random.Next(f.outline.Length)], g); reach = .4f + (float)random.NextDouble() * 2.2f;
                }
                return new Point(foot.x + (float)Math.Cos(a) * reach, 0, foot.z + (float)Math.Sin(a) * reach);
            }
            return new Point(-36 + (float)random.NextDouble() * 72, 0, -23 + (float)random.NextDouble() * 48);
        }
        // A flat-enough ledge on top of a cliff (authored frame scan, as the environment builder plants them).
        private bool LedgeCandidate(Random random, float maxHeight, out Point foot)
        {
            foot = new Point();
            int g = random.Next(AquariumMonolithLayout.Groups.Length);
            var min = AquariumMonolithLayout.Minimum[g]; var max = AquariumMonolithLayout.Maximum[g];
            float x = min.x + (float)random.NextDouble() * (max.x - min.x), z = min.z + (float)random.NextDouble() * (max.z - min.z), y = max.y;
            for (; y > .25f; y -= .07f) if (AquariumMonolithLayout.LocalDistance(new Point(x, y, z), g) < 0) break;
            if (y <= .25f || y > maxHeight) return false;
            var local = new Point(x, y + .012f, z);
            var world = MonolithToWorld(local, g);
            if (y < AquariumSeabed.Height(world) + .15f) return false;
            if (AquariumMonolithLayout.LocalNormal(local, g).y < .72f) return false;
            // Room above it (not under another bed of the same cliff).
            for (float h = .3f; h < 1.2f; h += .3f) if (AquariumMonolithLayout.Distance(new Point(world.x, world.y + h, world.z)) < .12f) return false;
            if (!AquariumMapOutline.Contains(world, .5f) || AquariumWalkwayLayout.Contains(world, .6f)) return false;
            foot = world; return true;
        }

        private void PlaceLife(Random random)
        {
            var spacing = new Spacing(); var anemones = new List<AquariumSceneAnemone>(); var plants = new List<AquariumScenePlant>();
            // Anemones first: they are the clownfish homes.
            for (int tries = 0; tries < 2400 && anemones.Count < LedgeAnemones; tries++)
            {
                Point foot; if (!LedgeCandidate(random, 5.8f, out foot)) continue;
                float size = .27f + (float)random.NextDouble() * .15f, r = AnemoneSpread(size);
                if (!spacing.Free(foot, r, 1.15f)) continue;
                spacing.Add(foot, r);
                anemones.Add(new AquariumSceneAnemone { position = foot, size = size, kind = random.Next(2), yaw = (float)(random.NextDouble() * 360), ledge = true });
            }
            for (int tries = 0; tries < 4000 && anemones.Count < LedgeAnemones + SandAnemones; tries++)
            {
                bool edge; var p = SandCandidate(random, out edge);
                float size = .36f + (float)random.NextDouble() * .12f, r = AnemoneSpread(size);
                if (!SandClear(p, r, size * 1.2f)) continue;
                p.y = AquariumSeabed.Height(p) + .02f;
                if (!spacing.Free(p, r, 1.15f)) continue;
                spacing.Add(p, r);
                anemones.Add(new AquariumSceneAnemone { position = p, size = size, kind = random.Next(2), yaw = (float)(random.NextDouble() * 360) });
            }
            for (int tries = 0; tries < 2400 && plants.Count < LedgePlants; tries++)
            {
                Point foot; if (!LedgeCandidate(random, 4.6f, out foot)) continue;
                int form = random.Next(5); float height = .7f + (float)random.NextDouble() * .9f, r = PlantSpread(form, height);
                if (!spacing.Free(foot, r, .9f)) continue;
                spacing.Add(foot, r);
                plants.Add(new AquariumScenePlant { position = foot, form = form, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360), ledge = true });
            }
            int sand = 0;
            for (int tries = 0; tries < 9000 && sand < SandPlants; tries++)
            {
                bool edge; var p = SandCandidate(random, out edge);
                // Eelgrass meadows favour the glass; the others mix evenly.
                int form = edge && random.NextDouble() < .45 ? 2 : random.Next(5);
                float height = form == 2 ? 1.4f + (float)random.NextDouble() * 3.0f : 1.0f + (float)random.NextDouble() * 2.6f;
                float r = PlantSpread(form, height);
                if (!SandClear(p, r, height)) continue;
                p.y = AquariumSeabed.Height(p) + .01f;
                if (!spacing.Free(p, r, .9f)) continue;
                spacing.Add(p, r); sand++;
                plants.Add(new AquariumScenePlant { position = p, form = form, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360) });
            }
            Anemones = anemones.ToArray(); Plants = plants.ToArray();
            var spots = new Point[Anemones.Length];
            for (int i = 0; i < spots.Length; i++) spots[i] = Anemones[i].position;
            AnemoneSpots = spots;
        }

        // The authored coral heads, moved with the rock or cliff they grow on (the planting pockets of a turned
        // reef rock are not drawn, so their corals are dropped; shoreline corals stay put).
        private Point[] TurnCorals()
        {
            var list = new List<Point>();
            foreach (var head in CoralTable.Points)
            {
                int rock = -1; bool pocket = false;
                Footprints();
                for (int i = 0; i < AquariumModel.Rocks.Length; i++)
                {
                    if (embedded[i]) continue;
                    float d = Flat(head, AquariumModel.Rocks[i]);
                    if (d <= ReefKeep(i)) { rock = i; break; }
                    if (d <= AquariumModel.RockRadii[i] + 3.2f) pocket = true;
                }
                if (rock >= 0)
                {
                    var p = RockToWorld(head, rock); p.y += AquariumSeabed.Height(p) - AquariumSeabed.Height(head); list.Add(p); continue;
                }
                if (pocket) continue;
                int group = -1;
                for (int g = 0; g < AquariumMonolithLayout.Groups.Length && group < 0; g++)
                {
                    var min = AquariumMonolithLayout.Minimum[g]; var max = AquariumMonolithLayout.Maximum[g];
                    if (head.x >= min.x && head.x <= max.x && head.z >= min.z && head.z <= max.z && head.y > AquariumSeabed.Height(head) + .15f
                        && AquariumMonolithLayout.LocalDistance(head - new Point(0, .6f, 0), g) < .3f) group = g;
                }
                list.Add(group >= 0 ? MonolithToWorld(head, group) : head);
            }
            return list.ToArray();
        }
    }
}

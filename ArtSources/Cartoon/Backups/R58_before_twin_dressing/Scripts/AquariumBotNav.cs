using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    // R47: a coarse navigation grid for the AI (2 m cells, four water layers). Built lazily for the current
    // scenery layout; links between cells are tested against the solid scenery the first time they are used.
    public static class AquariumBotNav
    {
        private const float Cell = 2f;
        private static readonly float[] Layers = { 1.9f, 4.2f, 6.6f, 9.0f };
        private static int nx, nz, nl, builtFor = int.MinValue;
        private static float minX, minZ;
        private static bool[] free;
        private static Point[] centres;
        private static int[] known, open;
        private static float[] g; private static int[] parent, stamp; private static int search;
        // R48: the grid and every link are worked out on a worker thread when a layout is chosen (Prewarm), so
        // the host never stalls for the ~2 s of scenery tests in play. Find never waits for it: while the
        // worker holds the gate it reports no path and the bot steers straight, exactly as for an unreachable goal.
        private static readonly object gate = new object();
        private static volatile bool warming;
        private static int linkedFor = int.MinValue;
        private static readonly List<KeyValuePair<float, int>> heap = new List<KeyValuePair<float, int>>(256);

        public static void Prewarm()
        {
            int version = AquariumSceneryLayout.Version;
            if (warming || linkedFor == version) return;
            warming = true;
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    lock (gate)
                    {
                        var layout = AquariumSceneryLayout.Current;
                        Build(); LinkAll(layout);
                        // The editor's environment builder can lend the tank another layout for a moment; anything
                        // worked out meanwhile is thrown away and rebuilt on the next search.
                        if (AquariumSceneryLayout.Current != layout || builtFor != AquariumSceneryLayout.Version) { free = null; linkedFor = int.MinValue; }
                    }
                }
                catch (Exception) { }
                finally { warming = false; }
            });
        }

        private static void LinkAll(AquariumSceneryLayout layout)
        {
            int version = builtFor;
            for (int i = 0; i < free.Length; i++)
            {
                if (AquariumSceneryLayout.Current != layout) return;
                if (!free[i]) continue;
                int x = i % nx, z = (i / nx) % nz, l = i / (nx * nz), k = -1;
                for (int dl = -1; dl <= 1; dl++) for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0 && dl == 0) continue;
                    k++;
                    int X = x + dx, Z = z + dz, L = l + dl;
                    if (X < 0 || Z < 0 || L < 0 || X >= nx || Z >= nz || L >= nl) continue;
                    int j = (L * nz + Z) * nx + X;
                    if (free[j]) Link(i, k, j);
                }
            }
            linkedFor = version;
        }

        private static void Build()
        {
            int version = AquariumSceneryLayout.Version;
            if (free != null && version == builtFor) return;
            builtFor = version;
            // R57: the selected map's extent (the twin tanks reach further than the default tank).
            float w = AquariumMaps.HalfWidth + 2, d = AquariumMaps.HalfDepth + 2;
            minX = -w; minZ = -d; nx = (int)Math.Ceiling(2 * w / Cell); nz = (int)Math.Ceiling(2 * d / Cell); nl = Layers.Length;
            int n = nx * nz * nl;
            free = new bool[n]; centres = new Point[n]; known = new int[n]; open = new int[n]; g = new float[n]; parent = new int[n]; stamp = new int[n];
            for (int l = 0; l < nl; l++) for (int z = 0; z < nz; z++) for (int x = 0; x < nx; x++)
            {
                int i = (l * nz + z) * nx + x;
                var c = new Point(minX + (x + .5f) * Cell, Layers[l], minZ + (z + .5f) * Cell);
                centres[i] = c;
                if (!AquariumModel.InsideTank(c, 1f) || c.y < AquariumSeabed.Height(c) + 1f) continue;
                free[i] = (AquariumModel.Constrain(c, .6f) - c).Length < .05f;
            }
        }

        private static int CellOf(Point p)
        {
            int x = (int)Math.Floor((p.x - minX) / Cell), z = (int)Math.Floor((p.z - minZ) / Cell);
            int l = 0; float best = float.MaxValue;
            for (int k = 0; k < nl; k++) { float dy = Math.Abs(p.y - Layers[k]); if (dy < best) { best = dy; l = k; } }
            x = Math.Max(0, Math.Min(nx - 1, x)); z = Math.Max(0, Math.Min(nz - 1, z));
            return (l * nz + z) * nx + x;
        }

        private static int NearestFree(Point p)
        {
            int start = CellOf(p); if (free[start]) return start;
            int best = -1; float bestD = float.MaxValue;
            int sx = start % nx, sz = (start / nx) % nz;
            for (int r = 1; r <= 4 && best < 0; r++)
                for (int l = 0; l < nl; l++) for (int z = sz - r; z <= sz + r; z++) for (int x = sx - r; x <= sx + r; x++)
                {
                    if (x < 0 || z < 0 || x >= nx || z >= nz) continue;
                    int i = (l * nz + z) * nx + x; if (!free[i]) continue;
                    float dd = (centres[i] - p).Length; if (dd < bestD) { bestD = dd; best = i; }
                }
            return best;
        }

        // 26 neighbour directions: bit k of known/open caches whether that link has been tested / is clear.
        private static bool Link(int i, int k, int j)
        {
            int bit = 1 << k;
            if ((known[i] & bit) == 0)
            {
                known[i] |= bit;
                if (AquariumModel.SolidHit(centres[i], centres[j], .5f) > 1f) open[i] |= bit;
            }
            return (open[i] & bit) != 0;
        }

        // Fills path with waypoints from near `from` to `to` (the last point is `to`). False when unreachable.
        public static bool Find(Point from, Point to, List<Point> path)
        {
            path.Clear();
            if (!System.Threading.Monitor.TryEnter(gate)) return false;
            try { return FindLocked(from, to, path); }
            finally { System.Threading.Monitor.Exit(gate); }
        }

        private static bool FindLocked(Point from, Point to, List<Point> path)
        {
            Build();
            int s = NearestFree(from), t = NearestFree(to);
            if (s < 0 || t < 0) return false;
            search++;
            heap.Clear();
            g[s] = 0; parent[s] = -1; stamp[s] = search; Push(heap, (centres[s] - centres[t]).Length, s);
            int expanded = 0; bool found = false;
            while (heap.Count > 0 && expanded < 4000)
            {
                int i = Pop(heap); expanded++;
                if (i == t) { found = true; break; }
                int x = i % nx, z = (i / nx) % nz, l = i / (nx * nz), k = -1;
                for (int dl = -1; dl <= 1; dl++) for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0 && dl == 0) continue;
                    k++;
                    int X = x + dx, Z = z + dz, L = l + dl;
                    if (X < 0 || Z < 0 || L < 0 || X >= nx || Z >= nz || L >= nl) continue;
                    int j = (L * nz + Z) * nx + X;
                    if (!free[j] || !Link(i, k, j)) continue;
                    float cost = g[i] + (centres[j] - centres[i]).Length;
                    if (stamp[j] == search && cost >= g[j]) continue;
                    stamp[j] = search; g[j] = cost; parent[j] = i;
                    Push(heap, cost + (centres[j] - centres[t]).Length, j);
                }
            }
            if (!found) return false;
            for (int i = t; i >= 0 && i != s; i = parent[i]) path.Add(centres[i]);
            path.Reverse();
            path.Add(to);
            return true;
        }

        private static void Push(List<KeyValuePair<float, int>> heap, float key, int value)
        {
            heap.Add(new KeyValuePair<float, int>(key, value));
            int c = heap.Count - 1;
            while (c > 0) { int p = (c - 1) / 2; if (heap[p].Key <= heap[c].Key) break; var tmp = heap[p]; heap[p] = heap[c]; heap[c] = tmp; c = p; }
        }
        private static int Pop(List<KeyValuePair<float, int>> heap)
        {
            int top = heap[0].Value; int last = heap.Count - 1; heap[0] = heap[last]; heap.RemoveAt(last);
            int c = 0;
            while (c < heap.Count)
            {
                int a = c * 2 + 1, b = a + 1, m = c;
                if (a < heap.Count && heap[a].Key < heap[m].Key) m = a;
                if (b < heap.Count && heap[b].Key < heap[m].Key) m = b;
                if (m == c) break;
                var tmp = heap[m]; heap[m] = heap[c]; heap[c] = tmp; c = m;
            }
            return top;
        }
    }
}

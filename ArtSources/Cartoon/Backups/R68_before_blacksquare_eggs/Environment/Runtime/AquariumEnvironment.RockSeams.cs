using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // R64: rocks that touch or overlap in a twin layout read as one stone. Every rock keeps its own baked mesh
    // (one per shape variant, reused with transforms), except at the joins: for each pair of rocks that come within
    // the blend of each other the region where their smooth union differs from the plain union is found, the rock
    // meshes' triangles in it are dropped and the region is meshed anew from the smooth union of all the rocks
    // there (AquariumRockSet.Distance, the collision field): fillets instead of hard intersection lines, slits and
    // cracks closed, a mossy, sandy dusting in the joins. Round a region's rim the patch runs on under the rock
    // meshes (pushed in 2 cm), so there is never a hole between them. Worked out on a worker thread per layout.
    public static partial class AquariumEnvironment
    {
        internal sealed class RockSeams
        {
            public readonly List<RockMeshData> patches = new List<RockMeshData>();
            // Rock index -> the triangles (vertex indices) of its baked mesh that stay.
            public readonly Dictionary<int, int[]> kept = new Dictionary<int, int[]>();
            public int pairs, regions, patchTriangles, droppedTriangles; public double ms, pairMs, patchMs, slowestPatchMs; public string slowest;
        }
        private struct Box
        {
            public Vector3 min, max;
            public bool Overlaps(Box b, float pad) { return min.x - pad <= b.max.x && b.min.x - pad <= max.x && min.y - pad <= b.max.y && b.min.y - pad <= max.y && min.z - pad <= b.max.z && b.min.z - pad <= max.z; }
            public bool Contains(Vector3 p) { return p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y && p.z >= min.z && p.z <= max.z; }
            public Box Grown(float g) { return new Box { min = min - Vector3.one * g, max = max + Vector3.one * g }; }
            public float Outside(Vector3 p)
            {
                float x = Math.Max(Math.Max(min.x - p.x, p.x - max.x), 0), y = Math.Max(Math.Max(min.y - p.y, p.y - max.y), 0), z = Math.Max(Math.Max(min.z - p.z, p.z - max.z), 0);
                return (float)Math.Sqrt(x * x + y * y + z * z);
            }
        }
        // A join region: the boxes of the joins it is made of (meshed only there, not over their whole bounding box).
        private sealed class Zone
        {
            public readonly List<Box> parts = new List<Box>(); public Box bounds;
            public bool Contains(Vector3 p) { foreach (var b in parts) if (b.Contains(p)) return true; return false; }
            public bool Contains(Vector3 p, float grow) { foreach (var b in parts) if (b.Grown(grow).Contains(p)) return true; return false; }
            public float Outside(Vector3 p, float grow) { float best = float.MaxValue; foreach (var b in parts) best = Math.Min(best, b.Grown(grow).Outside(p)); return best; }
            public bool Overlaps(Box o, float pad) { foreach (var b in parts) if (b.Overlaps(o, pad)) return true; return false; }
            public bool Overlaps(Zone o) { foreach (var b in parts) if (o.Overlaps(b, 0)) return true; return false; }
        }
        // QA: draw the rocks as before R64 (no joins) to compare.
        public static bool SeamsOff;
        private const float SeamSample = .4f, SeamMargin = .5f, SeamPush = .02f;

        private static readonly Dictionary<AquariumRockSet, RockSeams> seamsReady = new Dictionary<AquariumRockSet, RockSeams>();
        private static readonly List<AquariumRockSet> seamsOrder = new List<AquariumRockSet>();
        private static readonly HashSet<AquariumRockSet> seamsBuilding = new HashSet<AquariumRockSet>();
        private static readonly object seamsLock = new object();
        // Starts the joins of this layout's rocks on a worker thread (the layout worker calls it when it is done).
        public static void PrepareSeams(AquariumRockSet set)
        {
            if (set == null || set.Count == 0) return;
            lock (seamsLock) { if (seamsReady.ContainsKey(set) || seamsBuilding.Contains(set)) return; seamsBuilding.Add(set); }
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                RockSeams built = null;
                try { AquariumMaps.UseOnThisThread(AquariumMaps.Twin); built = ComputeSeams(set); }
                catch (Exception e) { Debug.LogWarning("R64 rock seams: " + e); }
                finally
                {
                    AquariumMaps.UseOnThisThread(-1);
                    lock (seamsLock) { seamsBuilding.Remove(set); if (built != null) KeepSeams(set, built); System.Threading.Monitor.PulseAll(seamsLock); }
                }
            });
        }
        private static void KeepSeams(AquariumRockSet set, RockSeams seams)
        {
            seamsReady[set] = seams; seamsOrder.Remove(set); seamsOrder.Add(set);
            while (seamsOrder.Count > 4) { seamsReady.Remove(seamsOrder[0]); seamsOrder.RemoveAt(0); }
        }
        internal static bool SeamsReady(AquariumRockSet set) { lock (seamsLock) return set == null || set.Count == 0 || seamsReady.ContainsKey(set); }
        // The joins of this layout: ready, or waits for the worker, or works them out here.
        internal static RockSeams Seams(AquariumRockSet set)
        {
            if (set == null || set.Count == 0) return new RockSeams();
            lock (seamsLock)
            {
                RockSeams ready;
                for (int waits = 0; seamsBuilding.Contains(set) && waits < 400; waits++) System.Threading.Monitor.Wait(seamsLock, 50);
                if (seamsReady.TryGetValue(set, out ready)) return ready;
            }
            var built = ComputeSeams(set);
            lock (seamsLock) KeepSeams(set, built);
            return built;
        }

        private static Box RockBox(AquariumRock r)
        {
            var lo = AquariumRockShapes.LocalMin[r.kind]; var hi = AquariumRockShapes.LocalMax[r.kind];
            var b = new Box { min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue };
            for (int c = 0; c < 8; c++)
            {
                var w = AquariumRockShapes.ToWorld(r, new Point((c & 1) == 0 ? lo.x : hi.x, (c & 2) == 0 ? lo.y : hi.y, (c & 4) == 0 ? lo.z : hi.z));
                var v = new Vector3(w.x, w.y, w.z); b.min = Vector3.Min(b.min, v); b.max = Vector3.Max(b.max, v);
            }
            return b;
        }
        // The collision field restricted to these rocks (ascending index, as AquariumRockSet.Distance), closed below the sand.
        private static float Union(AquariumRockSet set, int[] members, Point p)
        {
            float best = 100, k = AquariumRockSet.Blend;
            foreach (int i in members)
            {
                float bound = set.BoundDistance(i, p); if (bound >= best + k) continue;
                best = AquariumRockSet.SMin(best, bound > 2.5f ? bound : set.RockDistance(i, p), k);
            }
            return Math.Max(best, AquariumSeabed.TwinHeight(p.x, p.z) - .45f - p.y);
        }
        private static Point UnionNormal(AquariumRockSet set, int[] members, Point p)
        {
            const float e = .015f;
            return new Point(Union(set, members, new Point(p.x + e, p.y, p.z)) - Union(set, members, new Point(p.x - e, p.y, p.z)),
                Union(set, members, new Point(p.x, p.y + e, p.z)) - Union(set, members, new Point(p.x, p.y - e, p.z)),
                Union(set, members, new Point(p.x, p.y, p.z + e)) - Union(set, members, new Point(p.x, p.y, p.z - e))).Normalized;
        }

        internal static RockSeams ComputeSeams(AquariumRockSet set)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var seams = new RockSeams(); var rocks = set.Rocks; int n = rocks.Length; float k = AquariumRockSet.Blend;
            var boxes = new Box[n]; for (int i = 0; i < n; i++) boxes[i] = RockBox(rocks[i]);
            // 1. The joins: where two rocks' fields are both small and close to each other, the fillet shows.
            var regions = new List<Zone>();
            for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++)
            {
                if (!boxes[i].Overlaps(boxes[j], k)) continue;
                var a = boxes[i].Grown(k * .6f); var b = boxes[j].Grown(k * .6f);
                var lo = Vector3.Max(a.min, b.min); var hi = Vector3.Min(a.max, b.max);
                if (lo.x > hi.x || lo.y > hi.y || lo.z > hi.z) continue;
                var hit = new Box { min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue }; bool any = false;
                for (float x = lo.x; x <= hi.x; x += SeamSample) for (float y = lo.y; y <= hi.y; y += SeamSample) for (float z = lo.z; z <= hi.z; z += SeamSample)
                {
                    var p = new Point(x, y, z);
                    if (set.BoundDistance(i, p) > k * .6f || set.BoundDistance(j, p) > k * .6f) continue;
                    float di = set.RockDistance(i, p); if (di > k * .6f || di < -k) continue;
                    float dj = set.RockDistance(j, p); if (dj > k * .6f || dj < -k || Math.Abs(di - dj) > k) continue;
                    var v = new Vector3(x, y, z); hit.min = Vector3.Min(hit.min, v); hit.max = Vector3.Max(hit.max, v); any = true;
                }
                if (!any) continue;
                seams.pairs++; var zone = new Zone(); zone.parts.Add(hit.Grown(SeamSample + SeamMargin)); zone.bounds = zone.parts[0]; regions.Add(zone);
            }
            // Overlapping regions become one.
            for (bool merged = true; merged; )
            {
                merged = false;
                for (int a = 0; a < regions.Count && !merged; a++) for (int b = a + 1; b < regions.Count && !merged; b++)
                    if (regions[a].bounds.Overlaps(regions[b].bounds, 0) && regions[a].Overlaps(regions[b]))
                    {
                        regions[a].parts.AddRange(regions[b].parts);
                        regions[a].bounds = new Box { min = Vector3.Min(regions[a].bounds.min, regions[b].bounds.min), max = Vector3.Max(regions[a].bounds.max, regions[b].bounds.max) };
                        regions.RemoveAt(b); merged = true;
                    }
            }
            seams.regions = regions.Count; seams.pairMs = watch.Elapsed.TotalMilliseconds;
            // 2. Each region meshed from the union of the rocks there.
            var memberOf = new List<int>[n]; var regionMembers = new int[regions.Count][]; var regionStep = new float[regions.Count];
            for (int g = 0; g < regions.Count; g++)
            {
                var members = new List<int>(); bool giant = false;
                for (int i = 0; i < n; i++) if (regions[g].Overlaps(boxes[i], k)) { members.Add(i); giant |= AquariumRockShapes.IsGiant(rocks[i].kind); if (memberOf[i] == null) memberOf[i] = new List<int>(); memberOf[i].Add(g); }
                regionMembers[g] = members.ToArray(); regionStep[g] = giant ? .15f : .11f;
            }
            // The regions are independent: worked out side by side (one core is left to the game).
            var made = new RockMeshData[regions.Count]; var took = new double[regions.Count];
            var options = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
            System.Threading.Tasks.Parallel.For(0, regions.Count, options, g =>
            {
                AquariumMaps.UseOnThisThread(AquariumMaps.Twin);
                var w = System.Diagnostics.Stopwatch.StartNew();
                try { made[g] = SeamPatch(set, regionMembers[g], regions[g], regionStep[g]); }
                finally { AquariumMaps.UseOnThisThread(-1); took[g] = w.Elapsed.TotalMilliseconds; }
            });
            seams.patchMs = watch.Elapsed.TotalMilliseconds - seams.pairMs;
            for (int g = 0; g < regions.Count; g++) if (took[g] > seams.slowestPatchMs) { seams.slowestPatchMs = took[g]; var size = regions[g].bounds.max - regions[g].bounds.min; seams.slowest = size.x.ToString("0.0") + "x" + size.y.ToString("0.0") + "x" + size.z.ToString("0.0") + " members " + regionMembers[g].Length; }
            foreach (var patch in made) if (patch != null) { seams.patches.Add(patch); seams.patchTriangles += patch.triangles.Length / 3; }
            // 3. The rock meshes lose their triangles inside the regions.
            for (int i = 0; i < n; i++)
            {
                if (memberOf[i] == null) continue;
                var r = rocks[i]; var data = RockData(r.kind, r.variant); var v = data.vertices; var t = data.triangles;
                var keep = new List<int>(t.Length); int dropped = 0;
                for (int f = 0; f < t.Length; f += 3)
                {
                    var c = (v[t[f]] + v[t[f + 1]] + v[t[f + 2]]) / 3; var w = AquariumRockShapes.ToWorld(r, new Point(c.x, c.y, c.z)); var wp = new Vector3(w.x, w.y, w.z);
                    bool inside = false; foreach (int g in memberOf[i]) if (regions[g].Contains(wp)) { inside = true; break; }
                    if (inside) { dropped++; continue; }
                    keep.Add(t[f]); keep.Add(t[f + 1]); keep.Add(t[f + 2]);
                }
                if (dropped > 0) { seams.kept[i] = keep.ToArray(); seams.droppedTriangles += dropped; }
            }
            seams.ms = watch.Elapsed.TotalMilliseconds;
            return seams;
        }

        // Marching tetrahedra over the region (grown by a few cells), welded and settled onto the field, painted.
        private static RockMeshData SeamPatch(AquariumRockSet set, int[] members, Zone region, float step)
        {
            var outer = region.bounds.Grown(step * 3); float zoneGrow = step * 3; var clock = System.Diagnostics.Stopwatch.StartNew();
            var origin = new Point(outer.min.x, outer.min.y, outer.min.z);
            int nx = (int)Math.Ceiling((outer.max.x - outer.min.x) / step) + 1, ny = (int)Math.Ceiling((outer.max.y - outer.min.y) / step) + 1, nz = (int)Math.Ceiling((outer.max.z - outer.min.z) / step) + 1;
            if ((long)nx * ny * nz > 6000000) return null;
            var field = new float[nx * ny * nz];
            Func<int, Point> at = i => { int x = i % nx, y = (i / nx) % ny, z = i / (nx * ny); return new Point(origin.x + x * step, origin.y + y * step, origin.z + z * step); };
            const int B = 4; float reachOut = (float)Math.Sqrt(3) * B * step * .5f * 1.3f + step;
            // Slabs of blocks side by side on the worker cores.
            System.Threading.Tasks.Parallel.For(0, (nz + B - 1) / B, slab =>
            {
            int bz = slab * B;
            for (int by = 0; by < ny; by += B) for (int bx = 0; bx < nx; bx += B)
            {
                var centre = new Point(origin.x + (bx + B * .5f - .5f) * step, origin.y + (by + B * .5f - .5f) * step, origin.z + (bz + B * .5f - .5f) * step);
                var cv = new Vector3(centre.x, centre.y, centre.z);
                if (region.Outside(cv, zoneGrow) > B * step)
                {
                    for (int z = bz; z < Math.Min(nz, bz + B); z++) for (int y = by; y < Math.Min(ny, by + B); y++) for (int x = bx; x < Math.Min(nx, bx + B); x++) field[(z * ny + y) * nx + x] = float.NaN;
                    continue;
                }
                float dc = Union(set, members, centre); bool far = Math.Abs(dc) > reachOut;
                for (int z = bz; z < Math.Min(nz, bz + B); z++) for (int y = by; y < Math.Min(ny, by + B); y++) for (int x = bx; x < Math.Min(nx, bx + B); x++)
                {
                    int i = (z * ny + y) * nx + x;
                    var q = at(i);
                    field[i] = !region.Contains(new Vector3(q.x, q.y, q.z), zoneGrow) ? float.NaN : far ? dc : Union(set, members, q);
                }
            }
            });
            double evalMs = clock.Elapsed.TotalMilliseconds;
            var positions = new List<Vector3>(); var triangles = new List<int>(); var edges = new Dictionary<long, int>();
            Func<int, int, int> vertex = (a, b) =>
            {
                if (a > b) { int s = a; a = b; b = s; }
                long key = ((long)a << 32) | (uint)b; int index;
                if (edges.TryGetValue(key, out index)) return index;
                float t = Math.Max(.0002f, Math.Min(.9998f, field[a] / (field[a] - field[b])));
                var p = at(a) + (at(b) - at(a)) * t;
                index = positions.Count; positions.Add(new Vector3(p.x, p.y, p.z)); edges[key] = index; return index;
            };
            var ids = new int[8]; var inside = new int[4]; var outside = new int[4];
            for (int z = 0; z < nz - 1; z++) for (int y = 0; y < ny - 1; y++) for (int x = 0; x < nx - 1; x++)
            {
                int a = (z * ny + y) * nx + x;
                ids[0] = a; ids[1] = a + 1; ids[2] = a + 1 + nx * ny; ids[3] = a + nx * ny;
                ids[4] = a + nx; ids[5] = a + nx + 1; ids[6] = a + nx + 1 + nx * ny; ids[7] = a + nx + nx * ny;
                bool positive = false, negative = false, skipped = false;
                for (int i = 0; i < 8; i++) { float f = field[ids[i]]; skipped |= float.IsNaN(f); positive |= f >= 0; negative |= f < 0; }
                if (skipped || !positive || !negative) continue;
                for (int t = 0; t < 6; t++)
                {
                    int ni = 0, no = 0;
                    for (int c = 0; c < 4; c++) { int id = ids[RockTets[t, c]]; if (field[id] < 0) inside[ni++] = id; else outside[no++] = id; }
                    if (ni == 0 || no == 0) continue;
                    Point inner = new Point(), outerP = new Point();
                    for (int c = 0; c < ni; c++) inner += at(inside[c]);
                    for (int c = 0; c < no; c++) outerP += at(outside[c]);
                    var direction = outerP / no - inner / ni; var outward = new Vector3(direction.x, direction.y, direction.z);
                    Action<int, int, int> tri = (i0, i1, i2) =>
                    {
                        if (Vector3.Dot(Vector3.Cross(positions[i1] - positions[i0], positions[i2] - positions[i0]), outward) < 0) { int s = i1; i1 = i2; i2 = s; }
                        triangles.Add(i0); triangles.Add(i1); triangles.Add(i2);
                    };
                    if (ni == 1) tri(vertex(inside[0], outside[0]), vertex(inside[0], outside[1]), vertex(inside[0], outside[2]));
                    else if (ni == 3) tri(vertex(outside[0], inside[0]), vertex(outside[0], inside[1]), vertex(outside[0], inside[2]));
                    else
                    {
                        int v0 = vertex(inside[0], outside[0]), v1 = vertex(inside[0], outside[1]), v2 = vertex(inside[1], outside[1]), v3 = vertex(inside[1], outside[0]);
                        tri(v0, v1, v2); tri(v0, v2, v3);
                    }
                }
            }
            if (triangles.Count == 0) return null;
            double marchMs = clock.Elapsed.TotalMilliseconds - evalMs;
            // Weld onto a coarser grid, settle on the field, paint.
            float cluster = step * 1.8f;
            var lookup = new Dictionary<long, int>(); var sum = new List<Vector3>(); var count = new List<int>(); var remap = new int[positions.Count];
            for (int i = 0; i < positions.Count; i++)
            {
                var p = positions[i];
                long key = ((long)Mathf.FloorToInt(p.x / cluster + 4096) << 40) | ((long)Mathf.FloorToInt(p.y / cluster + 4096) << 20) | (long)Mathf.FloorToInt(p.z / cluster + 4096);
                int index; if (!lookup.TryGetValue(key, out index)) { index = sum.Count; lookup[key] = index; sum.Add(Vector3.zero); count.Add(0); }
                sum[index] += p; count[index]++; remap[i] = index;
            }

            var vertices = new Vector3[sum.Count]; var normals = new Vector3[sum.Count]; var colors = new Color[sum.Count];
            var rocks = set.Rocks; float k = AquariumRockSet.Blend;
            // The lattice holds the field exactly at its points: trilinear in between is close enough to settle the
            // welded points and give smooth normals (no more exact evaluations than the paint needs).
            Func<Vector3, float> sample = q =>
            {
                float fx = Mathf.Clamp((q.x - origin.x) / step, 0, nx - 1.001f), fy = Mathf.Clamp((q.y - origin.y) / step, 0, ny - 1.001f), fz = Mathf.Clamp((q.z - origin.z) / step, 0, nz - 1.001f);
                int x = (int)fx, y = (int)fy, z = (int)fz; float u = fx - x, w = fy - y, e = fz - z;
                int i0 = (z * ny + y) * nx + x, sy = nx, sz = nx * ny;
                float a = field[i0] + (field[i0 + 1] - field[i0]) * u, b = field[i0 + sy] + (field[i0 + sy + 1] - field[i0 + sy]) * u;
                float c = field[i0 + sz] + (field[i0 + sz + 1] - field[i0 + sz]) * u, d = field[i0 + sz + sy] + (field[i0 + sz + sy + 1] - field[i0 + sz + sy]) * u;
                float near = a + (b - a) * w, farv = c + (d - c) * w, value = near + (farv - near) * e;
                return float.IsNaN(value) ? 0 : value;
            };
            Func<Vector3, Vector3> gradient = q =>
            {
                float g = step * .5f;
                return new Vector3(sample(q + new Vector3(g, 0, 0)) - sample(q - new Vector3(g, 0, 0)), sample(q + new Vector3(0, g, 0)) - sample(q - new Vector3(0, g, 0)),
                    sample(q + new Vector3(0, 0, g)) - sample(q - new Vector3(0, 0, g))).normalized;
            };
            System.Threading.Tasks.Parallel.For(0, vertices.Length, i =>
            {
                var v = sum[i] / count[i];
                for (int it = 0; it < 2; it++) { var nn = gradient(v); v -= nn * sample(v); }
                var gn = gradient(v); var p = new Point(v.x, v.y, v.z); var normal = new Point(gn.x, gn.y, gn.z);
                // The dominant rock paints the point (its own colours and tint), the join gets moss and sand dust.
                int best = members[0]; float d1 = float.MaxValue, d2 = float.MaxValue;
                foreach (int m in members)
                {
                    if (set.BoundDistance(m, p) > d2) continue;
                    float d = set.RockDistance(m, p);
                    if (d < d1) { d2 = d1; d1 = d; best = m; } else if (d < d2) d2 = d;
                }
                var r = rocks[best];
                var lp = AquariumRockShapes.ToLocal(r, p);
                double ya = r.yaw * Math.PI / 180; float yc = (float)Math.Cos(ya), ys = (float)Math.Sin(ya);
                var ln = new Point(yc * normal.x - ys * normal.z, normal.y, ys * normal.x + yc * normal.z);
                var c = RockColour(r.kind, r.variant, lp, ln);
                var tint = Color.Lerp(new Color(1.05f, 1.0f, .92f), new Color(.9f, .97f, 1.03f), r.tint) * (.93f + .12f * Mathf.Abs(r.tint - .5f) * 2);
                c = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);
                float join = d2 < float.MaxValue ? Mathf.Clamp01(1 - (d2 - d1) / k) : 0;
                float up = Mathf.Clamp01((normal.y + .2f) / .8f);
                c.a = Mathf.Max(c.a, join * (.25f + .35f * up) * Mathf.Clamp01((AquariumRockShapes.Noise(p.x * 1.9f, p.y * 1.9f + 3, p.z * 1.9f) - .3f) / .4f));
                // Round the rim the patch runs on just under the rock meshes.
                var wp = new Vector3(p.x, p.y, p.z);
                float push = SeamPush * Mathf.Clamp01(region.Outside(wp, -SeamMargin * .6f) / (SeamMargin * .6f));
                vertices[i] = wp - new Vector3(normal.x, normal.y, normal.z) * push;
                normals[i] = new Vector3(normal.x, normal.y, normal.z); colors[i] = c;
            });
            var kept = new List<int>(triangles.Count); var seen = new HashSet<long>();
            for (int t = 0; t < triangles.Count; t += 3)
            {
                int a = remap[triangles[t]], b = remap[triangles[t + 1]], c = remap[triangles[t + 2]];
                if (a == b || b == c || a == c) continue;
                int lo = Math.Min(a, Math.Min(b, c)), hi = Math.Max(a, Math.Max(b, c)), mid = a + b + c - lo - hi;
                long key = ((long)lo << 42) | ((long)mid << 21) | (long)hi;
                if (!seen.Add(key)) continue;
                var face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (Vector3.Dot(face, normals[a] + normals[b] + normals[c]) < 0) { int s = b; b = c; c = s; }
                kept.Add(a); kept.Add(b); kept.Add(c);
            }
            lock (seamsLock) lastPatchTiming = "eval " + evalMs.ToString("0") + " march " + marchMs.ToString("0") + " paint " + (clock.Elapsed.TotalMilliseconds - evalMs - marchMs).ToString("0") + " lattice " + nx + "x" + ny + "x" + nz + " verts " + vertices.Length;
            return new RockMeshData { vertices = vertices, normals = normals, colors = colors, triangles = kept.ToArray() };
        }
        public static string lastPatchTiming = "";

        // QA: the joins of a layout worked out afresh (pairs, regions, triangles, time).
        public static string SeamReport(AquariumRockSet set)
        {
            var s = ComputeSeams(set);
            return "pairs " + s.pairs + " regions " + s.regions + " patch tris " + s.patchTriangles + " dropped tris " + s.droppedTriangles + " rocks trimmed " + s.kept.Count +  " in " + s.ms.ToString("0") + " ms (pairs " + s.pairMs.ToString("0") + ", patches " + s.patchMs.ToString("0") + ", slowest " + s.slowestPatchMs.ToString("0") + " ms " + s.slowest + ")";
        }
        // Unity meshes for a layout's joins (main thread).
        internal static Mesh SeamMesh(RockMeshData data, string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
            mesh.vertices = data.vertices; mesh.normals = data.normals; mesh.colors = data.colors; mesh.triangles = data.triangles;
            SurfaceBasis(mesh); mesh.RecalculateBounds(); return mesh;
        }
        // A rock's baked mesh with only the kept triangles (shares the prototype's vertices).
        internal static Mesh TrimmedRockMesh(int kind, int variant, int[] triangles)
        {
            var proto = RockPrototype(kind, variant).mesh;
            var mesh = new Mesh { name = proto.name + " (joined)", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
            mesh.vertices = proto.vertices; mesh.normals = proto.normals; mesh.tangents = proto.tangents; mesh.colors = proto.colors;
            mesh.triangles = triangles; mesh.RecalculateBounds(); return mesh;
        }
    }
}

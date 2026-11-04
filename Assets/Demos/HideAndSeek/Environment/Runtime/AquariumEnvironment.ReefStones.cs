using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // R65: the default tank's reef rocks as one stone each. A reef rock used to be a pile of separate faceted
    // ellipsoids (the body, nine seam boulders, its rubble skirt) that cut through each other with hard lines;
    // now the same stones (same places, sizes, colours and random stream) are a smooth union (a signed distance
    // field, smooth-min per stone) meshed as one surface: fillets where they meet, slits closed, moss in the joins
    // and sand at the foot. The anemone holdfast stones are joined the same way. At run time, where a reef rock
    // meets a cliff in the current layout (the cliffs and reefs turn per game), a join patch is meshed from the
    // smooth union of the two (AquariumSceneryPresentation): it runs on just under both meshes where they are
    // unchanged and shows only in the fillet, so neither baked mesh has to be cut.
    public static partial class AquariumEnvironment
    {
        internal struct ReefStone { public Vector3 at, scale; public Color color; public float rough, k; }
        public const float CliffBlend = .9f;
        // QA: leave the reef and cliff joins out (to compare).
        public static bool CliffJoinsOff;

        private sealed class StoneField
        {
            public readonly List<ReefStone> stones = new List<ReefStone>();
            public Func<float, float, float> ground;
            public void Add(Vector3 at, Vector3 scale, Color color, float rough, float k)
            { stones.Add(new ReefStone { at = at, scale = scale, color = color, rough = rough, k = k }); }
            public float Distance(Vector3 p)
            {
                float best = 100;
                for (int i = 0; i < stones.Count; i++)
                {
                    var s = stones[i]; float reach = Mathf.Max(s.scale.x, Mathf.Max(s.scale.y, s.scale.z)) * (1 + s.rough * .6f);
                    float k = i == 0 ? .01f : s.k;
                    if ((p - s.at).magnitude - reach >= best + k) continue;
                    best = AquariumRockSet.SMin(best, StoneDistance(s, p), k);
                }
                if (ground != null) best = Math.Max(best, ground(p.x, p.z) - .4f - p.y);
                return best;
            }
            // The nearest stone and the gap to the next (for the paint).
            public int Nearest(Vector3 p, out float gap)
            {
                int best = 0; float d1 = float.MaxValue, d2 = float.MaxValue;
                for (int i = 0; i < stones.Count; i++)
                {
                    var s = stones[i]; float reach = Mathf.Max(s.scale.x, Mathf.Max(s.scale.y, s.scale.z)) * (1 + s.rough * .6f);
                    if ((p - s.at).magnitude - reach > d2) continue;
                    float d = StoneDistance(s, p);
                    if (d < d1) { d2 = d1; d1 = d; best = i; } else if (d < d2) d2 = d;
                }
                gap = d2 - d1; return best;
            }
            public Color Paint(Vector3 p, Vector3 n)
            {
                float gap; var s = stones[Nearest(p, out gap)];
                float noise = AquariumRockShapes.Noise(p.x * 2.3f + 5, p.y * 2.3f, p.z * 2.3f - 3);
                var c = s.color * (1 + (noise - .5f) * s.rough * 3);
                float up = Mathf.Max(0, n.y);
                c = Color.Lerp(c, C(0x9bb4a2), up * s.rough * 1.3f);
                // Moss in the joins, sand settled at the foot.
                float join = Mathf.Clamp01(1 - gap / .35f) * Mathf.Clamp01((AquariumRockShapes.Noise(p.x * 3.1f, p.y * 3.1f + 9, p.z * 3.1f) - .25f) / .5f);
                c = Color.Lerp(c, C(0x6f8a68), join * .4f);
                if (ground != null)
                {
                    float foot = Mathf.Clamp01(1 - (p.y - ground(p.x, p.z)) / .28f);
                    c = Color.Lerp(c, C(0xb9b49a), foot * foot * .45f);
                }
                c.a = .75f + up * .25f;
                return c;
            }
            // The stones meshed as one surface (each group of touching stones on its own lattice), into g.
            public void MeshInto(Geo g, float step)
            {
                int n = stones.Count; if (n == 0) return;
                var parent = new int[n]; for (int i = 0; i < n; i++) parent[i] = i;
                Func<int, int> find = null; find = x => parent[x] == x ? x : (parent[x] = find(parent[x]));
                for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++)
                {
                    float ri = Reach(stones[i]), rj = Reach(stones[j]);
                    if ((stones[i].at - stones[j].at).magnitude < ri + rj + Mathf.Max(stones[i].k, stones[j].k)) parent[find(i)] = find(j);
                }
                var islands = new Dictionary<int, List<int>>();
                for (int i = 0; i < n; i++) { int r = find(i); List<int> l; if (!islands.TryGetValue(r, out l)) islands[r] = l = new List<int>(); l.Add(i); }
                foreach (var island in islands.Values)
                {
                    var sub = new StoneField { ground = ground };
                    var min = Vector3.one * float.MaxValue; var max = Vector3.one * float.MinValue;
                    foreach (int i in island) { sub.stones.Add(stones[i]); float r = Reach(stones[i]); min = Vector3.Min(min, stones[i].at - Vector3.one * r); max = Vector3.Max(max, stones[i].at + Vector3.one * r); }
                    min -= Vector3.one * step * 2; max += Vector3.one * step * 2;
                    if (ground != null) min.y = Math.Min(min.y, ground(min.x, min.z) - .55f);
                    var data = MeshField(p => sub.Distance(p), min, max, step, step * 1.7f, (p, nn) => sub.Paint(p, nn), null);
                    if (data == null) continue;
                    int first = g.Count;
                    for (int v = 0; v < data.vertices.Length; v++) g.V(data.vertices[v], data.colors[v]);
                    for (int t = 0; t < data.triangles.Length; t += 3) g.T(first + data.triangles[t], first + data.triangles[t + 1], first + data.triangles[t + 2]);
                }
            }
        }
        private static float Reach(ReefStone s) { return Mathf.Max(s.scale.x, Mathf.Max(s.scale.y, s.scale.z)) * (1 + s.rough * .6f); }
        // An ellipsoid swollen and dented by its own noise (as Geo.Ellipsoid's rough surface).
        internal static float StoneDistance(ReefStone s, Vector3 p)
        {
            var q = p - s.at; var u = new Vector3(q.x / s.scale.x, q.y / s.scale.y, q.z / s.scale.z); float len = u.magnitude;
            if (len < 1e-5f) return -Mathf.Min(s.scale.x, Mathf.Min(s.scale.y, s.scale.z));
            var unit = u / len;
            float noise = AquariumRockShapes.Noise(unit.x * 1.7f + s.at.x * 1.3f + 20, unit.y * 1.7f + 7, unit.z * 1.7f + s.at.z * 1.1f + 20);
            float f = 1 + (noise - .5f) * s.rough * 1.4f;
            float rx = s.scale.x * f, ry = s.scale.y * f, rz = s.scale.z * f;
            float ax = q.x / rx, ay = q.y / ry, az = q.z / rz, k0 = (float)Math.Sqrt(ax * ax + ay * ay + az * az);
            float bx = q.x / (rx * rx), by = q.y / (ry * ry), bz = q.z / (rz * rz), k1 = (float)Math.Sqrt(bx * bx + by * by + bz * bz);
            if (k1 < 1e-6f) return -Math.Min(rx, Math.Min(ry, rz));
            return k0 * (k0 - 1) / k1;
        }

        // Marching tetrahedra of a field over a box (lattice step), welded, settled on the lattice field, painted.
        // push (optional) sinks a vertex along its normal (the cliff joins run on under the baked meshes).
        internal static RockMeshData MeshField(Func<Vector3, float> fieldAt, Vector3 min, Vector3 max, float step, float weld, Func<Vector3, Vector3, Color> paint, Func<Vector3, float> push, Func<Vector3, bool> keep = null)
        {
            int nx = (int)Math.Ceiling((max.x - min.x) / step) + 1, ny = (int)Math.Ceiling((max.y - min.y) / step) + 1, nz = (int)Math.Ceiling((max.z - min.z) / step) + 1;
            if ((long)nx * ny * nz > 4000000 || nx < 2 || ny < 2 || nz < 2) return null;
            var field = new float[nx * ny * nz];
            System.Threading.Tasks.Parallel.For(0, nz, z =>
            {
                for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++) field[(z * ny + y) * nx + x] = fieldAt(new Vector3(min.x + x * step, min.y + y * step, min.z + z * step));
            });
            Func<int, Vector3> at = i => { int x = i % nx, y = (i / nx) % ny, z = i / (nx * ny); return new Vector3(min.x + x * step, min.y + y * step, min.z + z * step); };
            var positions = new List<Vector3>(); var triangles = new List<int>(); var edges = new Dictionary<long, int>();
            Func<int, int, int> vertex = (a, b) =>
            {
                if (a > b) { int s = a; a = b; b = s; }
                long key = ((long)a << 32) | (uint)b; int index;
                if (edges.TryGetValue(key, out index)) return index;
                float t = Math.Max(.0002f, Math.Min(.9998f, field[a] / (field[a] - field[b])));
                index = positions.Count; positions.Add(at(a) + (at(b) - at(a)) * t); edges[key] = index; return index;
            };
            var ids = new int[8]; var inside = new int[4]; var outside = new int[4];
            for (int z = 0; z < nz - 1; z++) for (int y = 0; y < ny - 1; y++) for (int x = 0; x < nx - 1; x++)
            {
                int a = (z * ny + y) * nx + x;
                ids[0] = a; ids[1] = a + 1; ids[2] = a + 1 + nx * ny; ids[3] = a + nx * ny;
                ids[4] = a + nx; ids[5] = a + nx + 1; ids[6] = a + nx + 1 + nx * ny; ids[7] = a + nx + nx * ny;
                bool positive = false, negative = false;
                for (int i = 0; i < 8; i++) { positive |= field[ids[i]] >= 0; negative |= field[ids[i]] < 0; }
                if (!positive || !negative) continue;
                for (int t = 0; t < 6; t++)
                {
                    int ni = 0, no = 0;
                    for (int c = 0; c < 4; c++) { int id = ids[RockTets[t, c]]; if (field[id] < 0) inside[ni++] = id; else outside[no++] = id; }
                    if (ni == 0 || no == 0) continue;
                    Vector3 inner = Vector3.zero, outer = Vector3.zero;
                    for (int c = 0; c < ni; c++) inner += at(inside[c]);
                    for (int c = 0; c < no; c++) outer += at(outside[c]);
                    var outward = outer / no - inner / ni;
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
            var lookup = new Dictionary<long, int>(); var sum = new List<Vector3>(); var count = new List<int>(); var remap = new int[positions.Count];
            for (int i = 0; i < positions.Count; i++)
            {
                var p = positions[i];
                long key = ((long)Mathf.FloorToInt(p.x / weld + 4096) << 40) | ((long)Mathf.FloorToInt(p.y / weld + 4096) << 20) | (long)Mathf.FloorToInt(p.z / weld + 4096);
                int index; if (!lookup.TryGetValue(key, out index)) { index = sum.Count; lookup[key] = index; sum.Add(Vector3.zero); count.Add(0); }
                sum[index] += p; count[index]++; remap[i] = index;
            }
            Func<Vector3, float> sample = q =>
            {
                float fx = Mathf.Clamp((q.x - min.x) / step, 0, nx - 1.001f), fy = Mathf.Clamp((q.y - min.y) / step, 0, ny - 1.001f), fz = Mathf.Clamp((q.z - min.z) / step, 0, nz - 1.001f);
                int x = (int)fx, y = (int)fy, z = (int)fz; float u = fx - x, w = fy - y, e = fz - z;
                int i0 = (z * ny + y) * nx + x, sy = nx, sz = nx * ny;
                float a = field[i0] + (field[i0 + 1] - field[i0]) * u, b = field[i0 + sy] + (field[i0 + sy + 1] - field[i0 + sy]) * u;
                float c = field[i0 + sz] + (field[i0 + sz + 1] - field[i0 + sz]) * u, d = field[i0 + sz + sy] + (field[i0 + sz + sy + 1] - field[i0 + sz + sy]) * u;
                float near = a + (b - a) * w, far = c + (d - c) * w; return near + (far - near) * e;
            };
            Func<Vector3, Vector3> gradient = q =>
            {
                float g = step * .5f;
                return new Vector3(sample(q + new Vector3(g, 0, 0)) - sample(q - new Vector3(g, 0, 0)), sample(q + new Vector3(0, g, 0)) - sample(q - new Vector3(0, g, 0)),
                    sample(q + new Vector3(0, 0, g)) - sample(q - new Vector3(0, 0, g))).normalized;
            };
            var vertices = new Vector3[sum.Count]; var normals = new Vector3[sum.Count]; var colors = new Color[sum.Count]; var wanted = new bool[sum.Count]; var rim = new bool[sum.Count];
            System.Threading.Tasks.Parallel.For(0, vertices.Length, i =>
            {
                var v = sum[i] / count[i];
                for (int it = 0; it < 2; it++) { var nn = gradient(v); v -= nn * sample(v); }
                var n = gradient(v);
                colors[i] = paint != null ? paint(v, n) : Color.white; wanted[i] = keep == null || keep(v);
                // At the lattice's rim the field (and so the normal) is not known all round: such a point is left out.
                float margin = step * 1.5f;
                rim[i] = v.x < min.x + margin || v.y < min.y + margin || v.z < min.z + margin || v.x > max.x - margin || v.y > max.y - margin || v.z > max.z - margin;
                if (push != null) v -= n * push(v);
                vertices[i] = v; normals[i] = n;
            });
            var kept = new List<int>(triangles.Count); var seen = new HashSet<long>();
            for (int t = 0; t < triangles.Count; t += 3)
            {
                int a = remap[triangles[t]], b = remap[triangles[t + 1]], c = remap[triangles[t + 2]];
                if (a == b || b == c || a == c) continue;
                if (!wanted[a] && !wanted[b] && !wanted[c]) continue;
                if (rim[a] || rim[b] || rim[c]) continue;
                int lo = Math.Min(a, Math.Min(b, c)), hi = Math.Max(a, Math.Max(b, c)), mid = a + b + c - lo - hi;
                long key = ((long)lo << 42) | ((long)mid << 21) | (long)hi;
                if (!seen.Add(key)) continue;
                var face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (Vector3.Dot(face, normals[a] + normals[b] + normals[c]) < 0) { int s = b; b = c; c = s; }
                kept.Add(a); kept.Add(b); kept.Add(c);
            }
            FixZeroNormals(vertices, normals, kept);   // R68: see there
            return new RockMeshData { vertices = vertices, normals = normals, colors = colors, triangles = kept.ToArray() };
        }

        // R68: a meshed field's point whose lattice gradient vanished (a flat spot, or the edge of the unknown cells)
        // got a zero normal; the shaders' normalize() made NaN of every pixel of the triangles touching it, and the
        // bloom spread that into a black square. Such a point takes the area-weighted normal of its faces (up if
        // even those cancel).
        internal static int FixZeroNormals(Vector3[] vertices, Vector3[] normals, List<int> triangles)
        {
            int bad = 0;
            for (int i = 0; i < normals.Length; i++) if (!(normals[i].sqrMagnitude > .25f)) bad++;
            if (bad == 0) return 0;
            var sum = new Vector3[normals.Length];
            for (int t = 0; t + 2 < triangles.Count; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                var face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                sum[a] += face; sum[b] += face; sum[c] += face;
            }
            for (int i = 0; i < normals.Length; i++)
            {
                if (normals[i].sqrMagnitude > .25f) continue;
                var n = sum[i].normalized;
                normals[i] = n.sqrMagnitude > .25f ? n : Vector3.up;
            }
            return bad;
        }

        // ---------------------------------------------------------------- the reef rocks' stones, recorded at bake time
        // (AquariumEnvironment.BuildReefs fills it; the editor writes AquariumReefStoneTable from it).
        internal static List<ReefStone>[] recordedReefStones;
        // Editor: the last bake's stones as table rows (11 numbers a stone).
        public static float[][] RecordedReefStones()
        {
            if (recordedReefStones == null) return new float[0][];
            var rows = new float[recordedReefStones.Length][];
            for (int r = 0; r < rows.Length; r++)
            {
                var list = recordedReefStones[r] ?? new List<ReefStone>(); var row = new float[list.Count * 11];
                for (int i = 0; i < list.Count; i++)
                {
                    var s = list[i]; int o = i * 11;
                    row[o] = s.at.x; row[o + 1] = s.at.y; row[o + 2] = s.at.z; row[o + 3] = s.scale.x; row[o + 4] = s.scale.y; row[o + 5] = s.scale.z;
                    row[o + 6] = s.color.r; row[o + 7] = s.color.g; row[o + 8] = s.color.b; row[o + 9] = s.rough; row[o + 10] = s.k;
                }
                rows[r] = row;
            }
            return rows;
        }
        // The baked reef rock's stones (world, authored layout), from the generated table; null when not baked yet.
        private static StoneField ReefField(int rock, float keep)
        {
            var table = AquariumReefStoneTable.Stones;
            if (table == null || rock < 0 || rock >= table.Length || table[rock] == null) return null;
            var f = new StoneField { ground = AquariumSeabed.BasicHeight }; var c = AquariumModel.Rocks[rock]; var data = table[rock];
            for (int i = 0; i + 10 < data.Length; i += 11)
            {
                var at = new Vector3(data[i], data[i + 1], data[i + 2]);
                if (i > 0 && new Vector2(at.x - c.x, at.z - c.z).magnitude > keep) continue;
                f.stones.Add(new ReefStone { at = at, scale = new Vector3(data[i + 3], data[i + 4], data[i + 5]), color = new Color(data[i + 6], data[i + 7], data[i + 8], 1), rough = data[i + 9], k = data[i + 10] });
            }
            return f;
        }

        // ---------------------------------------------------------------- run time: reef rocks meeting the cliffs
        // Join patches for every reef rock that touches a cliff in this layout (worker thread; reads the layout's
        // cliffs through AquariumMonolithLayout, so the layout must be the applied one).
        internal static List<RockMeshData> CliffJoins(AquariumSceneryLayout layout, Color cliffColour)
        {
            var result = new List<RockMeshData>(); float k = CliffBlend;
            for (int rock = 0; rock < AquariumModel.Rocks.Length; rock++)
            {
                var stones = ReefField(rock, AquariumSceneryLayout.ReefKeep(rock)); if (stones == null) continue;
                var centre = AquariumModel.Rocks[rock]; float yaw = layout.RockYaw.Length > rock ? layout.RockYaw[rock] : 0;
                float a = yaw * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                // World point -> the authored frame of the reef (the inverse of AquariumSceneryLayout.RockToWorld).
                Func<Vector3, Vector3> authored = p => { float x = p.x - centre.x, z = p.z - centre.z; return new Vector3(centre.x + ca * x - sa * z, p.y, centre.z + sa * x + ca * z); };
                Func<Vector3, float> reef = p => stones.Distance(authored(p));
                Func<Vector3, float> cliff = p => AquariumMonolithLayout.Distance(new Point(p.x, p.y, p.z));
                float reach = AquariumSceneryLayout.ReefKeep(rock) + .5f;
                if (cliff(new Vector3(centre.x, centre.y, centre.z)) > reach + k) continue;
                // Where both are near and close to each other: the fillet shows there.
                var lo = Vector3.one * float.MaxValue; var hi = Vector3.one * float.MinValue; bool any = false;
                for (float x = centre.x - reach; x <= centre.x + reach; x += .25f) for (float z = centre.z - reach; z <= centre.z + reach; z += .25f)
                    for (float y = centre.y - 1.6f; y <= centre.y + 2.2f; y += .25f)
                    {
                        var p = new Vector3(x, y, z); float m = cliff(p); if (m > k * .6f || m < -k) continue;
                        float r = reef(p); if (r > k * .6f || r < -k || Math.Abs(r - m) > k) continue;
                        lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); any = true;
                    }
                if (!any) continue;
                lo -= Vector3.one * .8f; hi += Vector3.one * .8f;
                Func<Vector3, float> union = p => Math.Max(AquariumRockSet.SMin(reef(p), cliff(p), k), AquariumSeabed.BasicHeight(p.x, p.z) - .4f - p.y);
                Func<Vector3, Vector3, Color> paint = (p, n) =>
                {
                    float r = reef(p), m = cliff(p);
                    var c = stones.Paint(authored(p), n);
                    float side = Mathf.Clamp01(.5f + (r - m) / (k * .8f));
                    var mixed = Color.Lerp(c, cliffColour, side);
                    float join = Mathf.Clamp01(1 - Math.Abs(r - m) / k);
                    mixed = Color.Lerp(mixed, C(0x6f8a68), join * .35f * Mathf.Clamp01(n.y + .3f)); mixed.a = c.a; return mixed;
                };
                // Under the baked meshes where the union is theirs (2.5 cm under the reef, 6 under the coarser cliff).
                Func<Vector3, float> push = p =>
                {
                    float r = reef(p), m = cliff(p), offset = Math.Min(r, m);
                    float under = r < m ? .025f : .06f;
                    return under * Mathf.Clamp01(1 - offset / .04f);
                };
                // Only the fillet itself is kept (where the union stands out of both); elsewhere the baked meshes show.
                Func<Vector3, bool> keep = p => { float r = reef(p), m = cliff(p); return Math.Min(r, m) > .012f && Math.Abs(r - m) < k * .8f && Math.Max(r, m) < k * .55f; };
                var data = MeshField(union, lo, hi, .1f, .17f, paint, push, keep);
                if (data != null && data.triangles.Length == 0) data = null;
                if (data != null) result.Add(data);
            }
            return result;
        }
    }
}

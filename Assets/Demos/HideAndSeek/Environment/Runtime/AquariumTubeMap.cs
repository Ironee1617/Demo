using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    // R73: the visitor tube of "보랏빛 터널 수조" (AquariumMaps.Grotto, laid out by AquariumTwinMap's single box). A
    // U-shaped acrylic tube lying on its side in the middle of the tank: both arms run out of the west wall, the bend
    // is in the east half, its axis is level at AxisY and it stands on pairs of legs. To swimmers it is solid (a
    // capsule chain round the axis, and the legs); fish, aliens and divers go under, over and round it. Pure
    // arithmetic, the same on every machine.
    public static class AquariumTubeMap
    {
        public const float Radius = 2.5f, AxisY = 5.4f, LegRadius = .3f;
        // R73e: the tank is 20% smaller (66 x 32 m): the tube's arms are closer and its bend further west.
        public const float ArmZ = 6.7f, BendX = 6f, WallX = -36f, WallFace = -33f;
        public static readonly Point[] Path = BuildPath();
        public static readonly Point[] Legs = BuildLegs();
        // Height of the legs' tops (they meet the tube a little above its bottom).
        public const float LegTop = AxisY - Radius * .85f;

        private static Point[] BuildPath()
        {
            var points = new List<Point>();
            Func<float, float> bow = x => 1.1f * (float)Math.Sin(Math.PI * Math.Max(0, Math.Min(1, (x - WallFace) / (BendX - WallFace))));
            for (float x = WallX; x < BendX; x += .5f) points.Add(new Point(x, 0, -ArmZ - bow(x)));
            for (int i = 0; i <= 48; i++)
            {
                double a = -Math.PI / 2 + Math.PI * i / 48;
                points.Add(new Point(BendX + (float)Math.Cos(a) * ArmZ, 0, (float)Math.Sin(a) * ArmZ));
            }
            for (float x = BendX - .5f; x >= WallX; x -= .5f) points.Add(new Point(x, 0, ArmZ + bow(x)));
            return points.ToArray();
        }
        // A pair of legs every ~7.5 m along the tube, splayed a little to each side of its bottom.
        private static Point[] BuildLegs()
        {
            var legs = new List<Point>(); float travel = 0, next = 6f;
            for (int i = 1; i < Path.Length; i++)
            {
                var a = Path[i - 1]; var b = Path[i]; var d = b - a; float length = d.Length; travel += length;
                if (travel < next || b.x < WallFace + 2.5f) continue;
                next = travel + 7.5f;
                var side = new Point(-d.z, 0, d.x).Normalized;
                legs.Add(b + side * 1.15f); legs.Add(b - side * 1.15f);
            }
            return legs.ToArray();
        }

        // ---------------------------------------------------------------- the horizontal distance to the axis, gridded
        private const float Step = .25f, MinX = -42, MaxX = 42, MinZ = -22, MaxZ = 22;
        private static float[] grid; private static int nx, nz;
        private static readonly object gridLock = new object();
        private static float[] Grid()
        {
            if (grid != null) return grid;
            lock (gridLock)
            {
                if (grid != null) return grid;
                int w = (int)Math.Round((MaxX - MinX) / Step) + 1, h = (int)Math.Round((MaxZ - MinZ) / Step) + 1;
                var values = new float[w * h];
                for (int iz = 0; iz < h; iz++) for (int ix = 0; ix < w; ix++) { Point q; values[iz * w + ix] = Exact(MinX + ix * Step, MinZ + iz * Step, out q); }
                nx = w; nz = h; grid = values;
            }
            return grid;
        }
        // The exact horizontal distance to the axis and its nearest point.
        public static float Exact(float x, float z, out Point nearest)
        {
            float best = float.MaxValue; nearest = Path[0];
            for (int i = 0; i < Path.Length - 1; i++)
            {
                var a = Path[i]; var b = Path[i + 1]; float dx = b.x - a.x, dz = b.z - a.z;
                float t = Math.Max(0, Math.Min(1, ((x - a.x) * dx + (z - a.z) * dz) / Math.Max(1e-6f, dx * dx + dz * dz)));
                float px = a.x + dx * t, pz = a.z + dz * t, e = (x - px) * (x - px) + (z - pz) * (z - pz);
                if (e < best) { best = e; nearest = new Point(px, AxisY, pz); }
            }
            return (float)Math.Sqrt(best);
        }
        // Horizontal distance to the axis (bilinear in the grid; far outside it, a large number).
        public static float Horizontal(float x, float z)
        {
            var g = Grid();
            float fx = (x - MinX) / Step, fz = (z - MinZ) / Step;
            if (fx < 0 || fz < 0 || fx >= nx - 1 || fz >= nz - 1) return 99;
            int ix = (int)fx, iz = (int)fz; float tx = fx - ix, tz = fz - iz; int i = iz * nx + ix;
            float a = g[i] + (g[i + 1] - g[i]) * tx, b = g[i + nx] + (g[i + nx + 1] - g[i + nx]) * tx;
            return a + (b - a) * tz;
        }
        // Under or near the tube's footprint (scenery and drains keep this clear).
        public static bool Near(float x, float z, float pad) { return Horizontal(x, z) < Radius + pad; }
        public static bool Near(Point p, float pad) { return Near(p.x, p.z, pad); }

        // ---------------------------------------------------------------- collision
        public static bool Overlaps(Point p, float pad)
        {
            float dy = p.y - AxisY, r = Radius + pad;
            if (dy > r || dy < -r - 6) return false;
            if (dy >= -r)
            {
                float h = Horizontal(p.x, p.z);
                if (h < r && h * h + dy * dy < r * r) return true;
            }
            if (p.y < LegTop + .3f && Horizontal(p.x, p.z) < 1.15f + LegRadius + pad + .3f)
            {
                float lr = LegRadius + pad;
                foreach (var leg in Legs) { float lx = p.x - leg.x, lz = p.z - leg.z; if (lx * lx + lz * lz < lr * lr) return true; }
            }
            return false;
        }
        // Pushes a body out of the tube (straight away from its axis) or off a leg (sideways).
        public static Point Constrain(Point p, float pad)
        {
            if (!Overlaps(p, pad)) return p;
            float r = Radius + pad, dy = p.y - AxisY;
            if (dy >= -r && Horizontal(p.x, p.z) < r)
            {
                Point q; float h = Exact(p.x, p.z, out q);
                if (h * h + dy * dy < r * r)
                {
                    var away = new Point(p.x - q.x, dy, p.z - q.z);
                    if (away.Length < 1e-4f) away = new Point(0, 1, 0);
                    p = q + away.Normalized * (r + .002f);
                }
            }
            if (p.y < LegTop + .3f)
            {
                float lr = LegRadius + pad;
                foreach (var leg in Legs)
                {
                    float lx = p.x - leg.x, lz = p.z - leg.z, d2 = lx * lx + lz * lz;
                    if (d2 >= lr * lr) continue;
                    float d = (float)Math.Sqrt(d2); if (d < 1e-4f) { lx = 1; lz = 0; d = 1; }
                    p = new Point(leg.x + lx / d * (lr + .002f), p.y, leg.z + lz / d * (lr + .002f));
                }
            }
            return p;
        }
        // The first blocked fraction of the segment, or 2 when it stays clear (as AquariumModel.ObstacleHit).
        public static float Hit(Point start, Point end, float pad = 0)
        {
            float top = AxisY + Radius + pad;
            if (start.y > top && end.y > top) return 2;
            var d = end - start; float length = d.Length;
            if (length < 1e-5f) return Overlaps(start, pad) ? 0 : 2;
            // quick reject: both ends far from the footprint and the segment short enough not to reach it
            float hs = Horizontal(start.x, start.z), he = Horizontal(end.x, end.z), reach = Radius + pad + 1.2f;
            if (hs - length > reach && he - length > reach) return 2;
            int steps = (int)Math.Ceiling(length / .2f) + 1; float previous = 0;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                if (Overlaps(start + d * t, pad)) return i == 0 ? 0 : previous;
                previous = t;
            }
            return 2;
        }
    }
}

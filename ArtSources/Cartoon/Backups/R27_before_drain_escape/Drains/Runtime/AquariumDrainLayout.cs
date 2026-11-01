using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    [Serializable]
    public struct AquariumDrainPlacement
    {
        public Point position;
        public float yaw;
        public bool isOpen;
    }

    // Pure host-side layout. Clients receive the actual placements in every snapshot.
    public static class AquariumDrainLayout
    {
        public const int Count = 4;
        public const float MinimumSpacing = 16f, OuterRadius = 1.08f, OpeningRadius = .65f;
        public const float ClearanceRadius = 1.65f;

        public static bool IsClear(Point p)
        {
            if (!AquariumModel.InsideTank(p, 3.5f) || AquariumWalkwayLayout.Contains(p, ClearanceRadius + .4f)
                || AquariumEntryLayout.KeepWaterClear(p, 2f)) return false;
            // Keep the entire working area outside rocks, planting and overhangs.
            if (AquariumMonolithLayout.Overlaps(new Point(p.x, AquariumSeabed.Height(p) + 1, p.z), ClearanceRadius + .4f)) return false;
            for (int i = 0; i < AquariumModel.Rocks.Length; i++)
            {
                var d = p - AquariumModel.Rocks[i]; d.y = 0;
                if (d.Length < AquariumModel.RockRadii[i] + ClearanceRadius + .5f) return false;
            }
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < 12; i++)
            {
                float a = i * (float)Math.PI / 6;
                float h = AquariumSeabed.Height(p.x + OuterRadius * (float)Math.Cos(a), p.z + OuterRadius * (float)Math.Sin(a));
                lo = Math.Min(lo, h); hi = Math.Max(hi, h);
            }
            // The deep mounting skirt follows shallow sand banks without requiring a perfectly level dune.
            return hi - lo < .18f;
        }

        public static AquariumDrainPlacement[] Create(Random random)
        {
            var pool = new List<Point>(AquariumDrainSites.Points);
            for (int i = pool.Count - 1; i > 0; i--) { int j = random.Next(i + 1); var t = pool[i]; pool[i] = pool[j]; pool[j] = t; }
            var picked = new List<Point>(Count);
            if (!Pick(pool, picked, 0)) throw new InvalidOperationException("Drain sites need rebaking: cannot place four separated drains.");
            var result = new AquariumDrainPlacement[Count];
            for (int i = 0; i < Count; i++)
            {
                var p = picked[i];
                // A recessed skirt accommodates the small remaining dune variation.
                p.y = AquariumSeabed.Height(p) + .04f;
                result[i] = new AquariumDrainPlacement { position = p, yaw = (float)random.NextDouble() * 360f };
            }
            result[random.Next(Count)].isOpen = true;
            return result;
        }

        private static bool Pick(List<Point> pool, List<Point> picked, int sector)
        {
            if (sector == Count) return true;
            foreach (var p in pool)
            {
                // One random clear location per broad quadrant, with pairwise spacing across quadrant boundaries.
                if ((p.x >= 1 ? 1 : 0) + (p.z >= 1 ? 2 : 0) != sector) continue;
                bool fits = true;
                foreach (var q in picked) { float x = p.x - q.x, z = p.z - q.z; if (x*x + z*z < MinimumSpacing*MinimumSpacing) { fits = false; break; } }
                if (!fits) continue;
                picked.Add(p);
                if (Pick(pool, picked, sector + 1)) return true;
                picked.RemoveAt(picked.Count - 1);
            }
            return false;
        }
    }
}

using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    [Serializable]
    public struct AquariumDrainPlacement
    {
        public Point position;
        public float yaw;
        // R27: true once the lid has been slid fully aside (the hole can be entered).
        public bool isOpen;
        // R27 escape state, owned by the host and sent in every snapshot:
        // how far each of the six screws has been backed out (1 = loose), and how far the lid has been
        // slid aside (0 = seated .. 1 = clear of the hole) toward the world yaw slideYaw (degrees).
        public float[] screws;
        public float lid, slideYaw;
        // R47: diver 1 glued this closed lid: aliens can no longer unscrew or move it.
        public bool glued;
    }

    // Pure host-side layout. Clients receive the actual placements in every snapshot.
    public static class AquariumDrainLayout
    {
        public const int Count = 4;
        // R27: the authored drain (ReturnDrain.fbx, 2 m lid, 1.3 m opening) is drawn at ModelScale so the
        // opening (.55 m) is narrower than the alien's head (.62-.68 m wide, 1.2 m across the arms): it has to
        // squeeze to get in. Everything below is in world metres.
        // R46: shrunk with the alien (AquariumFishTraits.AlienSizeRatio), so the squeeze still fits the same way.
        public const float ModelScale = .42f * AquariumFishTraits.AlienSizeRatio;
        public const float MinimumSpacing = 16f, OuterRadius = 1.08f * ModelScale, OpeningRadius = .65f * ModelScale;
        public const float ClearanceRadius = 1.65f;
        public const int ScrewCount = 6;
        // Authored (unscaled) prefab geometry: screw pivots on the lid rim, lid radius and lid/opening heights.
        private static readonly Point[] ScrewLocal = {
            new Point(-.7318f, .176f, -.4225f), new Point(0, .176f, -.845f), new Point(.7318f, .176f, -.4225f),
            new Point(.7318f, .176f, .4225f), new Point(0, .176f, .845f), new Point(-.7318f, .176f, .4225f) };
        public const float LidRadius = 1.0f * ModelScale, LidTop = .19f * ModelScale, MouthHeight = .06f * ModelScale;
        // How far the lid is dragged: just clear of the opening, resting on the flange rim and the sand.
        public const float SlideDistance = LidRadius + OpeningRadius + .08f;

        public static Point Local(AquariumDrainPlacement d, Point local)
        {
            float a = d.yaw * (float)Math.PI / 180f, c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return d.position + new Point(local.x * c + local.z * s, local.y, -local.x * s + local.z * c);
        }
        public static Point SlideDirection(AquariumDrainPlacement d)
        { float a = d.slideYaw * (float)Math.PI / 180f; return new Point((float)Math.Sin(a), 0, (float)Math.Cos(a)); }
        // Screw head on the seated lid (screws only matter while the lid is closed).
        public static Point ScrewPoint(AquariumDrainPlacement d, int screw)
        { return Local(d, ScrewLocal[Math.Max(0, Math.Min(ScrewCount - 1, screw))] * ModelScale); }
        // Centre of the lid's top where it is now.
        public static Point LidCenter(AquariumDrainPlacement d)
        { return d.position + new Point(0, LidTop, 0) + SlideDirection(d) * (SlideDistance * Math.Max(0, Math.Min(1, d.lid))); }
        public static Point Mouth(AquariumDrainPlacement d) { return d.position + new Point(0, MouthHeight, 0); }
        public static bool AllScrewsOut(AquariumDrainPlacement d)
        {
            if (d.screws == null || d.screws.Length < ScrewCount) return false;
            for (int i = 0; i < ScrewCount; i++) if (d.screws[i] < 1) return false;
            return true;
        }
        public static int ScrewsOut(AquariumDrainPlacement d)
        {
            int n = 0; if (d.screws != null) for (int i = 0; i < d.screws.Length; i++) if (d.screws[i] >= 1) n++;
            return n;
        }
        // The lid is dragged toward the most open side (away from reef rocks, monoliths and glass);
        // ties keep the drain's own heading, so open floor gives every drain its own direction.
        private static float PickSlide(Point p, float baseYaw)
        {
            float best = baseYaw, bestScore = float.MinValue;
            for (int i = 0; i < 12; i++)
            {
                float yaw = (baseYaw + i * 30f) % 360f, a = yaw * (float)Math.PI / 180f;
                var q = p + new Point((float)Math.Sin(a), 0, (float)Math.Cos(a)) * (SlideDistance + LidRadius * .6f);
                float score = 6;
                for (int r = 0; r < AquariumModel.Rocks.Length; r++)
                {
                    var d = q - AquariumModel.Rocks[r]; d.y = 0;
                    score = Math.Min(score, d.Length - AquariumModel.RockRadii[r]);
                }
                float pad = 0; while (pad < 3 && AquariumModel.InsideTank(q, pad + .25f)) pad += .25f;
                score = Math.Min(score, pad);
                if (AquariumMonolithLayout.Overlaps(new Point(q.x, AquariumSeabed.Height(q) + .6f, q.z), LidRadius)) score -= 10;
                if (AquariumWalkwayLayout.Contains(q, LidRadius)) score -= 10;
                if (score > bestScore + .001f) { bestScore = score; best = yaw; }
            }
            return best;
        }

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
            var picked = new List<Point>(Count);
            // R37: a game's scenery (turned cliffs, scattered seaweed) picks its own four open sites; the seaweed
            // keeps off them. The authored sites are only for the baked layout.
            var sites = AquariumSceneryLayout.Current.DrainSites;
            if (sites != null && sites.Length >= Count) for (int i = 0; i < Count; i++) picked.Add(sites[i]);
            else
            {
                var pool = new List<Point>(AquariumDrainSites.Points);
                for (int i = pool.Count - 1; i > 0; i--) { int j = random.Next(i + 1); var t = pool[i]; pool[i] = pool[j]; pool[j] = t; }
                if (!Pick(pool, picked, 0)) throw new InvalidOperationException("Drain sites need rebaking: cannot place four separated drains.");
            }
            var result = new AquariumDrainPlacement[Count];
            for (int i = 0; i < Count; i++)
            {
                var p = picked[i];
                // A recessed skirt accommodates the small remaining dune variation.
                p.y = AquariumSeabed.Height(p) + .04f * ModelScale;
                result[i] = new AquariumDrainPlacement { position = p, yaw = (float)random.NextDouble() * 360f };
            }
            // R27: every drain starts sealed with six tight screws; aliens have to open one themselves.
            // (The draw that used to pick one pre-opened drain is kept so later layouts stay the same.)
            random.Next(Count);
            for (int i = 0; i < Count; i++) { result[i].screws = new float[ScrewCount]; result[i].slideYaw = PickSlide(result[i].position, result[i].yaw); }
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

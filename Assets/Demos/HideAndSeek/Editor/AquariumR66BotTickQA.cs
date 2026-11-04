using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace PrototypeLab.Aquarium.Editor
{
    // R66: times AquariumModel.Tick for an all-AI round (fixed seeds, nav grid ready before the first tick) and
    // hashes every swimmer's position each tick, so a change to the bot code can be checked for identical play.
    public static class AquariumR66BotTickQA
    {
        // R66: the cliffs' distance field before the R66 rewrite (copied verbatim), compared bit for bit with the live one.
        static float RefFracture(Point p, AquariumMonolithLayout.Lobe l, float limit)
        {
            var q = p - l.centre; q.y -= q.x * .13f - q.z * .07f;
            float d = -100; var planes = l.planes;
            for (int i = 0; i < planes.Length; i++) { d = System.Math.Max(d, Point.Dot(q, planes[i].normal) - planes[i].offset); if (d >= limit) return d; }
            return d;
        }
        static float RefFracture(Point p, AquariumMonolithLayout.Lobe l)
        {
            var q = p - l.centre; q.y -= q.x * .13f - q.z * .07f;
            float d = -100; foreach (var plane in l.planes) d = System.Math.Max(d, Point.Dot(q, plane.normal) - plane.offset);
            return d;
        }
        static float RefHash(int x, int y, int z) { unchecked { uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)z * 2246822519u; h = (h ^ (h >> 13)) * 1274126177u; return (h ^ (h >> 16)) * (1f / 4294967295f); } }
        static float RefMix(float a, float b, float t) { return a + (b - a) * t; }
        static float RefNoise(Point p)
        {
            int x = (int)System.Math.Floor(p.x), y = (int)System.Math.Floor(p.y), z = (int)System.Math.Floor(p.z);
            float u = p.x - x, v = p.y - y, w = p.z - z; u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v); w = w * w * (3 - 2 * w);
            return RefMix(RefMix(RefMix(RefHash(x, y, z), RefHash(x + 1, y, z), u), RefMix(RefHash(x, y + 1, z), RefHash(x + 1, y + 1, z), u), v),
                RefMix(RefMix(RefHash(x, y, z + 1), RefHash(x + 1, y, z + 1), u), RefMix(RefHash(x, y + 1, z + 1), RefHash(x + 1, y + 1, z + 1), u), v), w);
        }
        static float RefLocal(Point p, int group, AquariumMonolithLayout.Lobe[][] breaks)
        {
            float distance = 100;
            foreach (var l in AquariumMonolithLayout.Groups[group]) { float d = RefFracture(p, l, distance + .02f); distance = System.Math.Min(distance, d - .02f); }
            if (group < breaks.Length) foreach (var cut in breaks[group]) distance = System.Math.Max(distance, -RefFracture(p, cut));
            float weather = (RefNoise(p * .72f) - .5f) * .36f + (RefNoise(p * 2.4f + new Point(7, 11, 3)) - .5f) * .12f + (RefNoise(p * 6.1f) - .5f) * .025f;
            return distance + weather;
        }
        static float RefBound(Point p, int group)
        {
            var min = AquariumMonolithLayout.Minimum[group]; var max = AquariumMonolithLayout.Maximum[group];
            float x = System.Math.Max(System.Math.Max(min.x - p.x, p.x - max.x), 0), y = System.Math.Max(System.Math.Max(min.y - p.y, p.y - max.y), 0), z = System.Math.Max(System.Math.Max(min.z - p.z, p.z - max.z), 0);
            return (float)System.Math.Sqrt(x * x + y * y + z * z);
        }
        public static string FieldCheck(int samples)
        {
            var breaks = (AquariumMonolithLayout.Lobe[][])typeof(AquariumMonolithLayout).GetField("EdgeBreaks", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            AquariumMaps.Select(AquariumMaps.Basic);
            var random = new System.Random(66); int localDiff = 0, distDiff = 0, overlapDiff = 0, near = 0;
            var layout = AquariumSceneryLayout.Current;
            for (int s = 0; s < samples; s++)
            {
                int g = random.Next(AquariumMonolithLayout.Groups.Length);
                var min = AquariumMonolithLayout.Minimum[g]; var max = AquariumMonolithLayout.Maximum[g];
                var p = new Point(min.x - 1 + (float)random.NextDouble() * (max.x - min.x + 2), min.y - 1 + (float)random.NextDouble() * (max.y - min.y + 2), min.z - 1 + (float)random.NextDouble() * (max.z - min.z + 2));
                if (AquariumMonolithLayout.LocalDistance(p, g) != RefLocal(p, g, breaks)) localDiff++;
                var w = layout.MonolithToWorld(p, g);
                float d = 100;
                for (int group = 0; group < AquariumMonolithLayout.Groups.Length; group++) { var q = layout.MonolithToLocal(w, group); if (RefBound(q, group) < d + .6f) d = System.Math.Min(d, RefLocal(q, group, breaks)); }
                if (AquariumMonolithLayout.Distance(w) != d) distDiff++;
                if (d < .5f) near++;
                float radius = (float)random.NextDouble() * .8f; bool overlap = false;
                for (int group = 0; group < AquariumMonolithLayout.Groups.Length && !overlap; group++) { var q = layout.MonolithToLocal(w, group); if (RefBound(q, group) <= radius && RefLocal(q, group, breaks) < radius) overlap = true; }
                if (AquariumMonolithLayout.Overlaps(w, radius) != overlap) overlapDiff++;
            }
            return "field check samples=" + samples + " nearSurface=" + near + " localDiff=" + localDiff + " distanceDiff=" + distDiff + " overlapDiff=" + overlapDiff;
        }

        public static string Run(int map, int ticks, string outPath) { return Run(map, ticks, outPath, false); }
        public static string Run(int map, int ticks, string outPath, bool passive)
        {
            var nav = typeof(AquariumBotNav);
            var warming = nav.GetField("warming", BindingFlags.Static | BindingFlags.NonPublic);
            var linked = nav.GetField("linkedFor", BindingFlags.Static | BindingFlags.NonPublic);
            var m = new AquariumModel(speciesCount: AquariumSpecies.Count, drainSeed: 4242) { DiveEntry = true };
            m.SetMap(map);
            m.AddPlayer(1, "D", Team.Seeker, true);
            for (int i = 2; i <= 5; i++) m.AddPlayer(i, "A" + i, Team.Hider, true);
            m.StartRound(); m.BotsPassive = passive;
            int waited = 0;
            for (int k = 0; k < 600; k++)
            {
                if (!(bool)warming.GetValue(null) && (int)linked.GetValue(null) == AquariumSceneryLayout.Version) break;
                System.Threading.Thread.Sleep(25); waited++;
            }
            var watch = new Stopwatch(); double max = 0, sum = 0, maxAt = 0; int over5 = 0, over10 = 0; long hash = 17;
            var slow = new StringBuilder(); double second = 0;
            for (int t = 0; t < ticks; t++)
            {
                watch.Reset(); watch.Start(); m.Tick(1f / 30); watch.Stop();
                double ms = watch.Elapsed.TotalMilliseconds; sum += ms;
                if (ms > max) { max = ms; maxAt = m.Time; }
                if (ms > 5) over5++;
                if (ms > 10) over10++;
                second += ms; if ((t + 1) % 30 == 0) { slow.Append(second.ToString("0")).Append(' '); second = 0; }
                foreach (var p in m.Players) hash = unchecked(hash * 31 + (long)(p.position.x * 1000) * 7 + (long)(p.position.y * 1000) * 13 + (long)(p.position.z * 1000));
            }
            string line = "map" + map + " ticks=" + ticks + " navWait=" + waited + " avgMs=" + (sum / ticks).ToString("0.00") + " maxMs=" + max.ToString("0.0") + "@" + maxAt.ToString("0.0")
                + " over5=" + over5 + " over10=" + over10 + " hash=" + hash + " msPerSecond=" + slow;
            File.AppendAllText(outPath, line + "\n");
            return line;
        }
    }
}

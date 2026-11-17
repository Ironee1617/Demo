using System;

namespace PrototypeLab.Aquarium
{
    // The diver's round opening: standing on the north dive deck (the take-off pad by the ladder),
    // a giant-stride entry off the edge, the splash, and the sink into clear water. The host owns the
    // root path (so every client sees the same entry and the diver is ready exactly when the hunt
    // starts); the view plays the DeckIdle / DiveIn clips on top of it.
    public static class AquariumDiveEntry
    {
        public const float Seconds = 2.6f;          // the entry ends at HuntAt
        public const float SplashAt = .93f;         // fins hit the water
        public const float PadTop = AquariumModel.WaterHeight + .3425f;   // take-off pad surface
        public const float StandHeight = .92f;      // rig root above the soles (fins flat on the pad)
        public const float SlotSpacing = 1.1f;

        // Pose-to-pose root keys: across stays put, inward is metres from the deck edge line's anchor,
        // height is relative to the standing root. Hold, push off with a small hop, fall, plunge, glide.
        private static readonly float[] Keys = { 0, .35f, .62f, .80f, .95f, 1.15f, 1.5f, 2.0f, 2.6f };
        private static readonly float[] Inward = { 2.45f, 2.45f, 2.85f, 3.18f, 3.40f, 3.62f, 3.85f, 4.15f, 4.55f };
        private static readonly float[] Height = { 0, 0, .06f, -.05f, -.40f, -1.05f, -1.75f, -2.25f, -2.40f };
        private static readonly float[] InwardSlope = Slopes(Keys, Inward), HeightSlope = Slopes(Keys, Height);

        public static Point Facing { get { return AquariumEntryLayout.Inward; } }
        public static float Across(int slot) { return (slot % 2 == 0 ? 1 : -1) * ((slot + 1) / 2) * SlotSpacing; }

        public static Point At(float t, int slot)
        {
            t = Math.Max(0, Math.Min(Seconds, t));
            return AquariumEntryLayout.At(Across(slot), Sample(Inward, InwardSlope, t), PadTop + StandHeight + Sample(Height, HeightSlope, t));
        }

        public static Point Velocity(float t, int slot)
        {
            if (t <= 0 || t >= Seconds) return t >= Seconds ? (At(Seconds, slot) - At(Seconds - .05f, slot)) * 20 : new Point();
            return (At(Math.Min(Seconds, t + .02f), slot) - At(Math.Max(0, t - .02f), slot)) * (1 / ((Math.Min(Seconds, t + .02f) - Math.Max(0, t - .02f))));
        }

        public static Point SplashPoint(int slot)
        { return AquariumEntryLayout.At(Across(slot), 3.42f, AquariumModel.WaterHeight); }

        // Monotone cubic (Fritsch-Carlson): eases into and out of every key, never overshoots.
        private static float[] Slopes(float[] x, float[] y)
        {
            int n = x.Length; var d = new float[n - 1]; var m = new float[n];
            for (int i = 0; i < n - 1; i++) d[i] = (y[i + 1] - y[i]) / (x[i + 1] - x[i]);
            m[0] = d[0]; m[n - 1] = d[n - 2];
            for (int i = 1; i < n - 1; i++) m[i] = d[i - 1] * d[i] <= 0 ? 0 : 3 * (x[i + 1] - x[i - 1]) / ((2 * x[i + 1] - x[i] - x[i - 1]) / d[i - 1] + (x[i + 1] + x[i] - 2 * x[i - 1]) / d[i]);
            for (int i = 0; i < n - 1; i++) if (d[i] == 0) { m[i] = 0; m[i + 1] = 0; }
            // Arrive at the last key still gliding (the swim takes over from there).
            m[n - 1] = d[n - 2] * .6f;
            return m;
        }
        private static float Sample(float[] y, float[] m, float t)
        {
            int i = 0; while (i < Keys.Length - 2 && t > Keys[i + 1]) i++;
            float h = Keys[i + 1] - Keys[i], s = (t - Keys[i]) / h, s2 = s * s, s3 = s2 * s;
            return (2 * s3 - 3 * s2 + 1) * y[i] + (s3 - 2 * s2 + s) * h * m[i] + (-2 * s3 + 3 * s2) * y[i + 1] + (s3 - s2) * h * m[i + 1];
        }
    }
}

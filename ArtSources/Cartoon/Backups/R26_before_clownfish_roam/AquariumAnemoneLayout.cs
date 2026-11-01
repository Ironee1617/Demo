using System;

namespace PrototypeLab.Aquarium
{
    // R25: where the living anemones grow, for fish that live with them (clownfish, FishHabit.Anemone).
    // Baked from AquariumEnvironment.BuildAnemoneGardens (seed 8129): the generated anemone mesh was split
    // into colonies and each colony's base taken (centre of its lowest tentacle ring, sand/ledge height).
    // Colonies closer than ~.4 m are one entry. Re-bake if the anemone gardens are re-authored.
    public static class AquariumAnemoneLayout
    {
        public static readonly Point[] Points =
        {
            // Foreground garden around reef rock 1.
            new Point(13.909f, .273f, 5.518f), new Point(10.591f, .249f, 6.813f), new Point(12.165f, .310f, 3.619f), new Point(13.168f, .273f, 7.031f),
            // Ledge colonies on the monumental reefs.
            new Point(-20.912f, .701f, -2.496f), new Point(-22.472f, 4.212f, -3.420f), new Point(-22.925f, 1.488f, -5.637f),
            new Point(28.497f, 4.220f, -.071f), new Point(21.140f, 5.554f, 1.856f), new Point(25.830f, 2.194f, 7.379f), new Point(22.420f, .868f, -1.224f),
            new Point(.162f, 5.370f, 4.872f), new Point(7.289f, .608f, 6.436f), new Point(9.070f, .954f, 3.590f), new Point(-3.091f, .888f, 1.259f),
            new Point(-12.836f, 2.381f, 9.410f), new Point(-11.564f, 2.801f, 9.899f), new Point(-14.798f, 1.054f, 9.108f), new Point(-12.891f, 3.564f, 10.964f),
            new Point(-24.619f, 1.731f, 6.648f), new Point(-22.004f, 1.038f, 6.935f), new Point(-23.870f, 2.719f, 7.789f), new Point(-22.834f, 2.928f, 8.634f),
            new Point(-20.297f, 3.025f, -7.535f), new Point(-19.976f, 1.624f, -9.566f), new Point(-19.787f, .584f, -10.617f),
            new Point(23.969f, 3.267f, -7.029f), new Point(22.996f, 2.347f, -8.912f),
        };
        // How far an anemone fish strays from its home crown (horizontal metres).
        public const float Leash = 1.5f;
        // The k-th anemone fish's home: spread over the tank (stride coprime with the colony count).
        public static int HomeFor(int k) { return Points.Length == 0 ? -1 : (k * 11) % Points.Length; }
        // A spot to hover over the crown: inside the leash, a little above the tentacles.
        public static Point HoverSpot(int home, Random r)
        {
            var a = Points[home]; double angle = r.NextDouble() * Math.PI * 2; float radius = .25f + (float)r.NextDouble() * (Leash - .45f);
            return new Point(a.x + (float)Math.Cos(angle) * radius, a.y + .45f + (float)r.NextDouble() * .75f, a.z + (float)Math.Sin(angle) * radius);
        }
    }
}

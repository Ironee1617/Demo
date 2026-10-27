using System;

namespace PrototypeLab.Aquarium
{
    // R23/R24: how a fish species lives in the tank.
    //  Wander: swims alone in random directions (Fish0, blue/yellow tang, clownfish).
    //  Cruise: never stops; a slow, steady patrol with long gentle turns (shark). (R23 "Lurk" slot.)
    //  Glide:  random wandering with wide, slow banking turns (manta ray).
    //  Bottom: lies on the sea floor and seldom moves; short scoots along the sand (flatfish).
    //  Anemone: R25, keeps to its own anemone, hovering over the crown and hiding in it (clownfish).
    public enum FishHabit { Wander, Cruise, Glide, Bottom, Anemone }

    // R23: per-species body size and habit, shared by the host model (hulls, harpoon hits, bite
    // reach, fish AI) and the view (rig scale, colliders). Filled from the AquariumSpecies assets
    // (AquariumSpecies.Count configures it); unconfigured species are Fish0-sized wanderers.
    public static class AquariumFishTraits
    {
        // R24: the alien's own form is a little smaller than Fish0's size reference (hull, hits, view).
        public const float AlienScale = .85f;
        private static float[] scales = { 1 }, widths = { 1 }, lengths = { 1 }, speeds = { 1 };
        private static FishHabit[] habits = { FishHabit.Wander };
        private static bool[] ignoresFeed = { false };

        public static void Configure(float[] scale, float[] width, float[] length, FishHabit[] habit, float[] speed, bool[] ignoreFeed)
        {
            if (scale == null || width == null || length == null || habit == null || speed == null || ignoreFeed == null) return;
            int n = Math.Min(Math.Min(Math.Min(scale.Length, width.Length), Math.Min(length.Length, habit.Length)), Math.Min(speed.Length, ignoreFeed.Length));
            var s = new float[n]; var w = new float[n]; var l = new float[n]; var h = new FishHabit[n]; var v = new float[n]; var f = new bool[n];
            for (int i = 0; i < n; i++)
            {
                s[i] = Valid(scale[i]) ? scale[i] : 1; w[i] = Valid(width[i]) ? width[i] : 1; l[i] = Valid(length[i]) ? length[i] : 1; h[i] = habit[i];
                v[i] = Valid(speed[i]) ? speed[i] : 1; f[i] = ignoreFeed[i];
            }
            scales = s; widths = w; lengths = l; habits = h; speeds = v; ignoresFeed = f;
        }
        private static bool Valid(float v) { return !float.IsNaN(v) && !float.IsInfinity(v) && v > .05f && v < 10; }

        public static float Scale(int species) { return species >= 0 && species < scales.Length ? scales[species] : 1; }
        public static FishHabit Habit(int species) { return species >= 0 && species < habits.Length ? habits[species] : FishHabit.Wander; }
        // R24: swimming speed relative to Fish0 (AquariumModel.FishSpeed). A disguised alien swims at it too.
        public static float Speed(int species) { return species >= 0 && species < speeds.Length ? speeds[species] : 1; }
        // R24: this species does not come to the diver's scattered food (shark, manta).
        public static bool IgnoresFeed(int species) { return species >= 0 && species < ignoresFeed.Length && ignoresFeed[species]; }
        // Rounded hull (capsule along the facing): radius across, half segment length along.
        public static float HullRadius(int species)
        { return AquariumBodyCollision.FishRadius * Scale(species) * (species >= 0 && species < widths.Length ? widths[species] : 1); }
        public static float HullHalf(int species)
        { return AquariumBodyCollision.FishHalf * Scale(species) * (species >= 0 && species < lengths.Length ? lengths[species] : 1); }
        // Clearance kept from glass, rocks, floor and surface (Fish0: .35).
        public static float WallRadius(int species)
        { return Habit(species) == FishHabit.Bottom ? BottomRest(species) : .35f * Math.Max(1, Scale(species) * .85f); }
        // Height of a bottom dweller's centre above the sand while it lies flat.
        public static float BottomRest(int species) { return .22f * Scale(species); }
        // Extra distance from centre to skin compared with Fish0 (bite reach, lunge stop).
        public static float Reach(int species)
        { return HullRadius(species) + HullHalf(species) - AquariumBodyCollision.FishRadius - AquariumBodyCollision.FishHalf; }
    }
}

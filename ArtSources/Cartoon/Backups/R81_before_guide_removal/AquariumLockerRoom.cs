using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R60: the divers' changing room. While the aliens hide, every diver waits here instead of on the dive deck:
    // a closed, dry room far below the tank (nothing of the tank can be seen from inside, and nothing inside can be
    // seen from the tank), where the divers walk about freely. Shortly before the hunt starts the screen fades to black
    // and the diver is on the dive deck for the giant-stride entry (AquariumDiveEntry).
    // Layout in room space (floor at y = 0, the room centred on Origin):
    //   x -5..5 (door to the dive hall on +x), z -3.5..3.5, ceiling 3.2;
    //   lockers along the back wall (+z) and the left wall (-x), a long bench in front of the back lockers,
    //   the fish-guide computer (R81; desk, big monitor) on the front wall (-z), towels by the door.
    public static class AquariumLockerRoom
    {
        public static readonly Point Origin = new Point(0, -150, 0);
        public const float HalfWidth = 5f, HalfDepth = 3.5f, Height = 3.2f;
        public const float WalkSpeed = 1.9f, RunSpeed = 3.0f;
        public const float EyeAboveRoot = .68f;      // eye above the rig root (the root stands StandHeight above the floor)
        public const float FadeOut = .55f, FadeIn = .5f;   // black before / after the cut to the dive deck
        private const float Body = .32f;
        // walkable floor (room space), and the bench footprint the divers walk round
        private const float MinX = -4.45f + Body, MaxX = 4.85f - Body, MinZ = -3.5f + .12f + Body, MaxZ = 3.5f - .55f - Body;
        public static readonly Rect Bench = new Rect(-2.6f, 1.55f, 5.2f, .44f);
        // R81: the fish-guide computer on the front wall: the desk's footprint (x, z; the divers walk round it and the
        // stool tucked under it), the desk top's height, and the monitor's screen (centre height, face z, size).
        public static readonly Rect Desk = new Rect(-1.5f, -3.5f, 3f, .7f);
        public const float DeskTop = .785f, ScreenWidth = 2.4f, ScreenHeight = 1.35f, ScreenY = 1.7f, ScreenZ = -3.29f;
        public static Vector3 ScreenCentre { get { return new Vector3(Origin.x, Origin.y + ScreenY, Origin.z + ScreenZ); } }

        public static float RootY { get { return Origin.y + AquariumDiveEntry.StandHeight; } }
        public static Point Facing { get { return new Point(0, 0, -1); } }   // toward the fish-guide computer

        public static Point Spawn(int slot)
        {
            float across = (slot % 2 == 0 ? 1 : -1) * ((slot + 1) / 2) * 1.05f;
            return new Point(Origin.x + across, RootY, Origin.z - .5f);
        }

        // Keeps a root inside the room and off the bench and the computer desk.
        public static Point Clamp(Point p)
        {
            float x = Mathf.Clamp(p.x - Origin.x, MinX, MaxX), z = Mathf.Clamp(p.z - Origin.z, MinZ, MaxZ);
            PushOut(ref x, ref z, Bench.xMin, Bench.xMax, Bench.yMin, Bench.yMax);
            PushOut(ref x, ref z, Desk.xMin, Desk.xMax, Desk.yMin, Desk.yMax + .1f);   // R81: the computer desk and its stool
            return new Point(Origin.x + x, RootY, Origin.z + z);
        }
        private static void PushOut(ref float x, ref float z, float x0, float x1, float z0, float z1)
        {
            float bx0 = x0 - Body, bx1 = x1 + Body, bz0 = z0 - Body, bz1 = z1 + Body;
            if (x <= bx0 || x >= bx1 || z <= bz0 || z >= bz1) return;
            float left = x - bx0, right = bx1 - x, front = z - bz0, back = bz1 - z, least = Mathf.Min(Mathf.Min(left, right), Mathf.Min(front, back));
            if (least == left) x = bx0; else if (least == right) x = bx1; else if (least == front) z = bz0; else z = bz1;
        }

        public static bool Contains(Vector3 world)
        {
            return Mathf.Abs(world.x - Origin.x) < HalfWidth + .3f && Mathf.Abs(world.z - Origin.z) < HalfDepth + .3f
                && world.y > Origin.y - .5f && world.y < Origin.y + Height + .5f;
        }
        public static bool Contains(Point p) { return Contains(new Vector3(p.x, p.y, p.z)); }

        // Is this diver waiting in the room at this time (before the cut to the deck)?
        public static bool Waiting(Swimmer p, float time)
        { return p != null && p.team == Team.Seeker && p.entryAt > 0 && time < p.entryAt - AquariumDiveEntry.Seconds; }

        // 0..1 black over the cut from the room to the deck, for the diver whose view it is.
        public static float Fade(Swimmer p, float time)
        {
            if (p == null || p.team != Team.Seeker || p.entryAt <= 0) return 0;
            float t = time - (p.entryAt - AquariumDiveEntry.Seconds);
            if (t < -FadeOut || t > FadeIn) return 0;
            return t < 0 ? Mathf.SmoothStep(0, 1, (t + FadeOut) / FadeOut) : 1 - Mathf.SmoothStep(0, 1, t / FadeIn);
        }

        // A light-footed walk over the standing pose (fins and all) for divers moving about the room.
        private static readonly Dictionary<AquariumRig, Transform[]> walkBones = new Dictionary<AquariumRig, Transform[]>();
        private static readonly string[] WalkNames = { "ThighL", "ThighR", "ShinL", "ShinR", "UpperArmL", "UpperArmR", "Body" };
        public static void WalkPose(AquariumRig rig, float time, float speed, float seed)
        {
            if (rig == null || rig.skin == null) return;
            float amount = Mathf.Clamp01((speed - .12f) / 1.4f); if (amount <= 0) return;
            Transform[] bones;
            if (!walkBones.TryGetValue(rig, out bones))
            {
                bones = new Transform[WalkNames.Length];
                foreach (var b in rig.skin.bones) if (b != null) { int i = Array.IndexOf(WalkNames, b.name); if (i >= 0) bones[i] = b; }
                walkBones[rig] = bones;
            }
            float phase = (time * (1.25f + .55f * Mathf.Clamp01(speed / RunSpeed)) + seed) * Mathf.PI * 2, s = Mathf.Sin(phase);
            Swing(bones[0], -26 * amount * s, Vector3.right); Swing(bones[1], 26 * amount * s, Vector3.right);
            Swing(bones[2], 34 * amount * Mathf.Max(0, s), Vector3.right); Swing(bones[3], 34 * amount * Mathf.Max(0, -s), Vector3.right);
            Swing(bones[4], 7 * amount * s, Vector3.right); Swing(bones[5], -20 * amount * s, Vector3.right);
            Swing(bones[6], 3.5f * amount * s, Vector3.forward);
        }
        private static void Swing(Transform bone, float degrees, Vector3 axis)
        { if (bone != null) bone.localRotation = Quaternion.AngleAxis(degrees, axis) * bone.localRotation; }
    }
}

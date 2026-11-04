using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    public static partial class AquariumEnvironment
    {
        // R56: the character-select stages (divers and aliens) stand inside a small piece of the tank instead of
        // an empty navy void: a sand floor that rises into a back reef of stones, corals, seaweed and anemones,
        // swaying with the tank's own shaders, sun shafts and two bubble vents, in front of a deep water wall.
        // No fish. The characters' strip (|x| < 3.6, z > -2.5) stays clear and flat so the pick hop and the glow
        // discs read as before. Everything is made with the tank's generators and materials (same look) and is
        // returned as one child of the stage, to be put on the stage's layer by the caller.
        public static GameObject LobbyBackdrop(Transform parent, int seed)
        {
            var root = new GameObject("Aquarium backdrop | no fish"); root.transform.SetParent(parent, false);
            var r = new System.Random(seed);
            Texture caustics = null;
            var tank = UnityEngine.Object.FindFirstObjectByType<AquariumEnvironmentRoot>();
            if (tank != null)
                foreach (var renderer in tank.GetComponentsInChildren<Renderer>(true))
                {
                    var m = renderer.sharedMaterial;
                    if (m != null && m.HasProperty("_Caustics") && m.GetTexture("_Caustics") != null) { caustics = m.GetTexture("_Caustics"); break; }
                }
            if (caustics == null) caustics = MakeCaustics();
            var sand = ScannedSurface("Lobby • lagoon sand", "SandGrain", caustics, .48f, true); sand.SetColor("_Color", C(0x7f9089));   // shaded, so the row stays brighter than the floor
            var rock = ScannedSurface("Lobby • reef stone", "Rock", caustics, .55f, false);
            var coral = LivingMaterial("Lobby • living coral", caustics, 0, 0, .65f);
            var kelp = LivingMaterial("Lobby • seaweed", caustics, 1, 0, .05f);
            var soft = LivingMaterial("Lobby • anemones", caustics, .42f, 1, .1f);
            var bubbleMat = Fx("Lobby • bubbles", C(0xb9f5ff), .62f, 1);
            var rayMat = Fx("Lobby • sun shafts", C(0xb9eaff), .13f, 2);
            var wallMat = new Material(Shader.Find("Sprites/Default")) { name = "Lobby • deep water" };

            // deep water wall: a vertical gradient, lighter toward the surface
            var wall = new Geo();
            float[] wy = { -6, 0, 2, 4, 7, 12 };
            Color[] wc = { C(0x0a2a37), C(0x0d3442), C(0x175f73), C(0x2a8fa2), C(0x56c0cc), C(0x7fd6dc) };
            for (int i = 0; i < wy.Length; i++) { wall.V(new Vector3(-24, wy[i], -12), wc[i]); wall.V(new Vector3(24, wy[i], -12), wc[i]); }
            for (int i = 0; i + 1 < wy.Length; i++) wall.Q(i * 2, i * 2 + 2, i * 2 + 3, i * 2 + 1);
            wall.Emit("Deep water wall", root.transform, wallMat, false);

            // sand: flat under the characters, gentle dunes behind, rising into the back reef
            var floor = new Geo(); const int nx = 72, nz = 34; const float x0 = -18, x1 = 18, z0 = 5, z1 = -12;
            for (int j = 0; j <= nz; j++) for (int i = 0; i <= nx; i++)
            {
                float x = Mathf.Lerp(x0, x1, i / (float)nx), z = Mathf.Lerp(z0, z1, j / (float)nz);
                floor.V(new Vector3(x, LobbyFloor(x, z), z), Color.white, new Vector2(x, z) * .25f);
                if (i < nx && j < nz) { int v = j * (nx + 1) + i; floor.Q(v, v + 1, v + nx + 2, v + nx + 1); }
            }
            floor.Emit("Sand floor", root.transform, sand, false);

            // stones: a back ridge, two side outcrops framing the row, a few pebbles
            var stones = new Geo();
            for (int i = 0; i < 11; i++)
            {
                float x = -14 + i * 2.8f + Range(r, -.6f, .6f), z = -9.0f + Range(r, -1.0f, .8f);
                var size = new Vector3(Range(r, .9f, 1.6f), Range(r, .6f, 1.3f), Range(r, .8f, 1.2f));
                stones.Ellipsoid(new Vector3(x, LobbyFloor(x, z) + size.y * .35f, z), size, Color.Lerp(C(0x6f7d70), C(0x8f9584), Range(r, 0, 1)), 14, 10, .32f);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * Range(r, 5.2f, 5.8f), z = Range(r, -1.6f, -1.0f);
                stones.Ellipsoid(new Vector3(x, LobbyFloor(x, z) + .22f, z), new Vector3(1.05f, .8f, .9f), C(0x7f8b7c), 14, 10, .3f);
                x = side * Range(r, 6.4f, 7.2f); z = Range(r, -3.4f, -2.6f);
                stones.Ellipsoid(new Vector3(x, LobbyFloor(x, z) + .5f, z), new Vector3(1.3f, 1.4f, 1.1f), C(0x76836f), 14, 10, .3f);
            }
            for (int i = 0; i < 14; i++)
            {
                float x = Range(r, -9, 9), z = Range(r, -6.5f, -3.2f); if (Mathf.Abs(x) < 3.8f && z > -3.6f) continue;
                float s = Range(r, .12f, .3f);
                stones.Ellipsoid(new Vector3(x, LobbyFloor(x, z) + s * .2f, z), new Vector3(s * 1.3f, s * .7f, s), C(0x8a907f), 9, 7, .3f);
            }
            // a far, darker row closes the horizon between the ridge stones
            for (int i = 0; i < 13; i++)
            {
                float x = -17 + i * 2.85f + Range(r, -.5f, .5f), z = -11.2f + Range(r, -.4f, .4f);
                var size = new Vector3(Range(r, 1.6f, 2.4f), Range(r, 1.1f, 1.9f), 1.2f);
                stones.Ellipsoid(new Vector3(x, LobbyFloor(x, z) + size.y * .2f, z), size, C(0x4a5852), 12, 9, .3f);
            }
            stones.Emit("Reef stones", root.transform, rock, false);

            // corals: lower behind the middle of the row, bigger toward the sides
            var corals = new Geo();
            Color[] palette = { C(0xddab6f), C(0xae818f), C(0xb57e5e), C(0x7eaa94), C(0xc58aab), C(0x7b92b3) };
            for (int i = 0; i < 18; i++)
            {
                float x = -9 + i + Range(r, -.4f, .4f), z = Range(r, -6.4f, -4.4f);
                float scale = Mathf.Lerp(.45f, 1.05f, Mathf.Clamp01((Mathf.Abs(x) - 1.5f) / 5)) * Range(r, .8f, 1.15f);
                var at = new Vector3(x, LobbyFloor(x, z) - .03f, z); var tint = palette[(i * 5 + seed) % palette.Length]; int s = seed + i * 13;
                switch ((i * 7 + seed) % 6)
                {
                    case 0: Staghorn(corals, at, Vector3.up * scale * .95f, scale * .075f, 3, tint, r); break;
                    case 1: ScrollCoral(corals, at, scale * 1.18f, tint, s); break;
                    case 2: SeaFan(corals, at, scale, tint, s); break;
                    case 3: BrainCoral(corals, at, scale, tint, s); break;
                    case 4: FingerCoral(corals, at, scale, tint, s); break;
                    default: TubeSponge(corals, at, scale, tint, s); break;
                }
            }
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 4.6f, z = -2.2f;
                BrainCoral(corals, new Vector3(x, LobbyFloor(x, z) - .03f, z), .55f, palette[side < 0 ? 1 : 3], seed + 7);
                x = side * 6.3f; z = -1.4f;
                SeaFan(corals, new Vector3(x, LobbyFloor(x, z) + .5f, z), .9f, palette[side < 0 ? 4 : 0], seed + 11);
            }
            corals.Emit("Corals", root.transform, coral, false);

            // seaweed: tall clumps at the sides and between the back stones, short tufts in the middle
            var weeds = new Geo();
            for (int i = 0; i < 26; i++)
            {
                float x = Range(r, -11, 11), z = Range(r, -7.6f, -3.6f);
                bool middle = Mathf.Abs(x) < 3.5f;
                float height = middle ? Range(r, .8f, 1.5f) : Range(r, 1.8f, 3.4f);
                Seaweed(weeds, new Vector3(x, LobbyFloor(x, z) - .02f, z), height, Range(r, 0, 6.28f), r, null);
            }
            foreach (float side in new[] { -1f, 1f }) for (int k = 0; k < 4; k++)
            {
                float x = side * Range(r, 4.4f, 6.8f), z = Range(r, -2.8f, -.6f);
                Seaweed(weeds, new Vector3(x, LobbyFloor(x, z) - .02f, z), Range(r, 2.2f, 3.6f), Range(r, 0, 6.28f), r, null);
            }
            weeds.Emit("Seaweed", root.transform, kelp, false);

            var anemones = new Geo();
            Vector3[] spots = { new Vector3(-3.9f, 0, -3.0f), new Vector3(4.1f, 0, -3.3f), new Vector3(-1.2f, 0, -4.6f), new Vector3(2.0f, 0, -4.9f), new Vector3(-6.0f, 0, -4.2f), new Vector3(6.6f, 0, -4.4f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var at = spots[i]; at.y = LobbyFloor(at.x, at.z) - .02f;
                LivingAnemone(anemones, at, i < 2 ? .42f : .32f, i % 2, seed + 17 + i * 5, null);
            }
            anemones.Emit("Anemones", root.transform, soft, false);

            // sun shafts slanting down through the back water, bubbles from two vents
            var beams = new Geo();
            for (int i = 0; i < 6; i++)
            {
                var top = new Vector3(-8.5f + i * 3.3f + Range(r, -.6f, .6f), 7.5f, -6.5f + Range(r, -1, 1));
                var bottom = top + new Vector3(-2.2f, -8.2f, 1.2f); float width = Range(r, .35f, .7f), phase = Range(r, 0, 6.28f);
                for (int plane = 0; plane < 2; plane++)
                {
                    Vector3 across = plane == 0 ? Vector3.right : Vector3.forward; int n = beams.Count; const int rows = 20, columns = 4;
                    for (int row = 0; row <= rows; row++) for (int column = 0; column <= columns; column++)
                    {
                        float depth = row / (float)rows, u = column / (float)columns;
                        var centre = Vector3.Lerp(top, bottom, depth); float spread = Mathf.Lerp(1, 2.6f, depth);
                        beams.V(centre + across * (u * 2 - 1) * width * spread, Color.white, new Vector2(u, 1 - depth), new Vector2(phase, 0));
                        if (row < rows && column < columns) { int v = n + row * (columns + 1) + column; beams.Q(v, v + 1, v + columns + 2, v + columns + 1); }
                    }
                }
            }
            var shafts = beams.Emit("Sun shafts", root.transform, rayMat, false);
            var shaftMesh = shafts.GetComponent<MeshFilter>().sharedMesh; var sb = shaftMesh.bounds; sb.Expand(2); shaftMesh.bounds = sb;

            var bubbles = new Geo();
            Vector3[] vents = { new Vector3(-4.5f, 0, -3.6f), new Vector3(5.4f, 0, -4.4f), new Vector3(.8f, 0, -6.0f) };
            foreach (var vent in vents) for (int i = 0; i < 22; i++)
            {
                var p = vent + new Vector3(Range(r, -.2f, .2f), 0, Range(r, -.2f, .2f)); p.y = LobbyFloor(p.x, p.z) + .05f;
                bubbles.Ellipsoid(p, Vector3.one * Range(r, .02f, .06f), Color.white, 8, 6, 0, new Vector2(Range(r, 0, 1), Range(r, .06f, .12f)));
            }
            var bubbleObject = bubbles.Emit("Rising bubbles", root.transform, bubbleMat, false);
            bubbleObject.GetComponent<MeshFilter>().sharedMesh.bounds = new Bounds(new Vector3(0, 5, -4), new Vector3(16, 14, 6));

            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off; }
            return root;
        }

        // Lobby sand height (stage space): level under the characters, dunes behind, rising into the back reef.
        private static float LobbyFloor(float x, float z)
        {
            float back = Mathf.Clamp01((-z - 1.6f) / 2.4f);
            float dunes = (.05f * Mathf.Sin(x * .9f + 1.3f) + .04f * Mathf.Sin(z * 1.3f + x * .4f)) * back;
            float rise = Mathf.Max(0, -z - 5); rise = rise * rise * .03f;
            float sides = Mathf.Max(0, Mathf.Abs(x) - 5.5f); sides = sides * sides * .03f * Mathf.Clamp01((-z + 1) / 3);
            return -.95f + dunes + rise + sides;
        }
    }
}

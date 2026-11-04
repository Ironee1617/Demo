using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // R56b: handle on one character-select diorama; the preview moves the light onto the picked character.
    public sealed class AquariumLobbyBackdrop
    {
        public GameObject Root;
        internal readonly List<Material> Lit = new List<Material>();
        internal Transform Beam; internal Light Spot;
        private Vector3 at; private float strength; private bool placed;
        // Eases the surface light (sand pool, shaft, spotlight) toward the stage point (local), 0..1 strength.
        public void Focus(Vector3 local, float target, float dt)
        {
            if (!placed) { at = local; placed = true; }
            float k = 1 - Mathf.Exp(-dt * 6);
            at = Vector3.Lerp(at, local, k); strength = Mathf.Lerp(strength, target, 1 - Mathf.Exp(-dt * 4));
            foreach (var m in Lit) m.SetVector("_Spot", new Vector4(at.x, at.z, strength, 0));
            if (Beam != null) { Beam.localPosition = new Vector3(at.x, 0, at.z); Beam.gameObject.SetActive(strength > .02f); }
            if (Spot != null) { Spot.transform.localPosition = new Vector3(at.x + .6f, 5.2f, at.z + 1.6f); Spot.transform.LookAt(Spot.transform.parent.TransformPoint(new Vector3(at.x, .1f, at.z))); Spot.intensity = 2.6f * strength; }
        }
    }

    public static partial class AquariumEnvironment
    {
        // R56b: the character-select stages stand inside a fish-free piece of the tank, art-directed like a cosy
        // aquarium game: a glowing water wall with a sun glow and surface ripples; three depth layers of reef
        // (hazy kelp forest far back, a colourful coral ridge, dark framing rocks and kelp at the sides); warm sand
        // with caustics and ripples; fanning sun shafts; drifting plankton and bubbles; and a light from the surface
        // that follows the picked character (a pool on the sand, its own shaft and a spotlight on the character).
        // The characters' strip (|x| < 3.7, z > -2.6) stays clear and level. All on the stage's layer.
        public static AquariumLobbyBackdrop LobbyBackdrop(Transform parent, int seed, int layer)
        {
            var handle = new AquariumLobbyBackdrop();
            var root = new GameObject("Aquarium backdrop | no fish"); root.transform.SetParent(parent, false); handle.Root = root;
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
            var shader = Shader.Find("Aquarium/Lobby Diorama");
            Func<string, Material> make = name => { var m = new Material(shader) { name = "Lobby • " + name }; m.SetTexture("_Caustics", caustics); handle.Lit.Add(m); return m; };
            var waterMat = make("water wall"); waterMat.SetFloat("_Background", 1);
            var sandMat = make("sand"); sandMat.SetFloat("_Ripple", 1); sandMat.SetFloat("_Saturation", 1.1f);
            var rockMat = make("reef stone"); rockMat.SetFloat("_Moss", 1); rockMat.SetFloat("_Saturation", 1.15f);
            var coralMat = make("coral"); coralMat.SetFloat("_Saturation", 1.12f); coralMat.SetFloat("_Glow", .04f); coralMat.SetFloat("_Pattern", 1);
            var kelpMat = make("kelp"); kelpMat.SetFloat("_Sway", 1); kelpMat.SetFloat("_Saturation", 1.35f);
            var softMat = make("anemones"); softMat.SetFloat("_Sway", .42f); softMat.SetFloat("_Saturation", 1.5f); softMat.SetFloat("_Glow", .16f);
            var bubbleMat = Fx("Lobby • bubbles", C(0xc8f7ff), .7f, 1);
            var moteMat = Fx("Lobby • plankton", C(0xe8fbff), .45f, 1);
            var rayMat = Fx("Lobby • sun shafts", C(0xd6f6ff), .26f, 2);
            var spotRayMat = Fx("Lobby • pick light", C(0xfff1d2), .32f, 2);

            // water wall (draws its own gradient, sun glow and surface ripples)
            var wall = new Geo();
            for (int j = 0; j <= 8; j++) for (int i = 0; i <= 8; i++)
            {
                wall.V(new Vector3(Mathf.Lerp(-30, 30, i / 8f), Mathf.Lerp(-8, 14, j / 8f), -17), Color.white);
                if (i < 8 && j < 8) { int v = j * 9 + i; wall.Q(v, v + 9, v + 10, v + 1); }
            }
            wall.Emit("Water wall", root.transform, waterMat, false);

            // sand: warm and level in the middle, darker and duned toward the sides and back
            var floor = new Geo(); const int nx = 80, nz = 40; const float x0 = -22, x1 = 22, z0 = 6, z1 = -16;
            for (int j = 0; j <= nz; j++) for (int i = 0; i <= nx; i++)
            {
                float x = Mathf.Lerp(x0, x1, i / (float)nx), z = Mathf.Lerp(z0, z1, j / (float)nz);
                float centre = Mathf.Clamp01(1 - (Mathf.Abs(x) - 2.5f) / 9) * Mathf.Clamp01(1 - (-z - 1) / 12);
                var c = Color.Lerp(C(0x7d8a80), C(0xf0dfb8), .25f + .75f * centre);
                floor.V(new Vector3(x, LobbyFloor(x, z), z), c);
                if (i < nx && j < nz) { int v = j * (nx + 1) + i; floor.Q(v, v + 1, v + nx + 2, v + nx + 1); }
            }
            floor.Emit("Sand", root.transform, sandMat, false);

            Color[] corals = { C(0xff7a8a), C(0xff9f43), C(0xa77bff), C(0x2ed1b5), C(0xffd166), C(0xff6fb5), C(0x5ab0ff) };
            var stones = new Geo(); var reef = new Geo(); var kelp = new Geo(); var soft = new Geo();

            // far: a hazy kelp forest and stone arches fading into the water
            for (int i = 0; i < 16; i++)
            {
                float x = -20 + i * 2.7f + Range(r, -.8f, .8f), z = Range(r, -14.5f, -12.5f);
                var size = new Vector3(Range(r, 1.8f, 3.2f), Range(r, .8f, 1.9f), 1.6f);
                stones.Ellipsoid(new Vector3(x, LobbyFloor(x, z) + size.y * .25f, z), size, C(0x51666a), 12, 9, .35f);
            }
            for (int i = 0; i < 20; i++)
            {
                float x = Range(r, -19, 19), z = Range(r, -13.5f, -10.5f); if (Mathf.Abs(x) < 4.5f) x *= 2.2f;   // keep the sun glow open
                Seaweed(kelp, new Vector3(x, LobbyFloor(x, z) - .05f, z), Range(r, 3.0f, 5.5f), Range(r, 0, 6.28f), r, null, 0);
            }

            // middle: a colourful coral ridge on mossy stones
            for (int i = 0; i < 12; i++)
            {
                float x = -15 + i * 2.75f + Range(r, -.5f, .5f), z = -8.8f + Range(r, -.9f, .9f);
                var size = new Vector3(Range(r, 1.0f, 1.7f), Range(r, .7f, 1.4f), Range(r, .9f, 1.3f));
                stones.Ellipsoid(new Vector3(x, LobbyFloor(x, z) + size.y * .3f, z), size, Color.Lerp(C(0x5d7480), C(0x7c8f8c), Range(r, 0, 1)), 14, 10, .32f);
            }
            for (int i = 0; i < 30; i++)
            {
                float x = -12 + i * .83f + Range(r, -.3f, .3f), z = Range(r, -7.6f, -3.6f);
                if (Mathf.Abs(x) < 3.9f && z > -4.4f) z -= 1.2f;
                float scale = Mathf.Lerp(.5f, 1.15f, Mathf.Clamp01((Mathf.Abs(x) - 1.5f) / 6)) * Range(r, .8f, 1.2f);
                var at = new Vector3(x, LobbyFloor(x, z) - .03f, z); var tint = corals[(i * 3 + seed) % corals.Length]; int s = seed + i * 13;
                // R63: the same forms, painted like the tank's (colourway and pattern per colony).
                int form = (i * 5 + seed) % 6, first = reef.Count;
                CoralForm(reef, at, form == 2 ? scale * 1.1f : scale, form, s, tint, r);
                PaintCoral(reef, first, at, form, i * 3 + seed, tint);
            }
            for (int i = 0; i < 18; i++)
            {
                float x = Range(r, -12, 12), z = Range(r, -8.6f, -4.2f);
                float height = Mathf.Abs(x) < 3.5f ? Range(r, .9f, 1.7f) : Range(r, 2.0f, 3.6f);
                Seaweed(kelp, new Vector3(x, LobbyFloor(x, z) - .02f, z), height, Range(r, 0, 6.28f), r, null);
            }
            Vector3[] spots = { new Vector3(-4.0f, 0, -3.1f), new Vector3(4.3f, 0, -3.4f), new Vector3(-1.6f, 0, -4.9f), new Vector3(1.9f, 0, -5.2f), new Vector3(-6.4f, 0, -4.6f), new Vector3(6.9f, 0, -4.9f), new Vector3(.2f, 0, -6.4f) };
            for (int i = 0; i < spots.Length; i++) { var at = spots[i]; at.y = LobbyFloor(at.x, at.z) - .02f; LivingAnemone(soft, at, i < 2 ? .45f : .34f, i % 2, seed + 17 + i * 5, null); }

            // near: dark framing stones and tall kelp at both edges of the frame
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 5.0f, z = -1.4f;
                stones.Ellipsoid(new Vector3(x, LobbyFloor(x, z) + .25f, z), new Vector3(1.1f, .95f, 1.0f), C(0x3f5560), 14, 10, .3f);
                x = side * 6.6f; z = -3.0f;
                stones.Ellipsoid(new Vector3(x, LobbyFloor(x, z) + .6f, z), new Vector3(1.5f, 1.9f, 1.3f), C(0x3a4f5a), 14, 10, .3f);
                var brainAt = new Vector3(side * 4.25f, LobbyFloor(side * 4.25f, -2.1f) - .03f, -2.1f); int first = reef.Count;
                BrainCoral(reef, brainAt, .5f, corals[side < 0 ? 0 : 3], seed + 7);
                PaintCoral(reef, first, brainAt, 3, side < 0 ? 0 : 6, corals[side < 0 ? 0 : 3]);
                var fanAt = new Vector3(side * 5.6f, LobbyFloor(side * 5.6f, -1.9f) + .55f, -1.9f); first = reef.Count;
                SeaFan(reef, fanAt, 1.0f, corals[side < 0 ? 5 : 2], seed + 11);
                PaintCoral(reef, first, fanAt, 2, side < 0 ? 3 : 1, corals[side < 0 ? 5 : 2]);
                for (int k = 0; k < 5; k++)
                {
                    float kx = side * Range(r, 4.4f, 6.4f), kz = Range(r, -3.2f, -.8f);
                    Seaweed(kelp, new Vector3(kx, LobbyFloor(kx, kz) - .02f, kz), Range(r, 2.6f, 4.2f), Range(r, 0, 6.28f), r, null, k % 2 == 0 ? 0 : -1);
                }
            }
            for (int i = 0; i < 16; i++)
            {
                float x = Range(r, -9, 9), z = Range(r, -6.5f, -2.8f); if (Mathf.Abs(x) < 3.9f && z > -3.8f) continue;
                float s = Range(r, .1f, .26f);
                stones.Ellipsoid(new Vector3(x, LobbyFloor(x, z) + s * .2f, z), new Vector3(s * 1.3f, s * .7f, s), C(0x8d9a93), 9, 7, .3f);
            }
            // R63: a few painted sea stars on the sand behind the characters' strip.
            Vector3[] starSpots = { new Vector3(-2.3f, 0, -3.1f), new Vector3(2.8f, 0, -2.95f), new Vector3(-5.7f, 0, -2.6f), new Vector3(.9f, 0, -4.3f), new Vector3(5.9f, 0, -3.9f) };
            for (int i = 0; i < starSpots.Length; i++)
            {
                var at = starSpots[i]; at.y = LobbyFloor(at.x, at.z) - .005f;
                SeaStar(reef, at, i == 3 ? .22f : .28f, i % StarSpeciesCount, (i * 2 + seed) % StarColourways, i * 1.3f + seed, seed + i, LobbyFloor);
            }
            stones.Emit("Stones", root.transform, rockMat, false);
            reef.Emit("Corals", root.transform, coralMat, false);
            kelp.Emit("Kelp", root.transform, kelpMat, false);
            soft.Emit("Anemones", root.transform, softMat, false);

            // sun shafts fanning down from the surface glow
            var beams = new Geo();
            for (int i = 0; i < 9; i++)
            {
                float u = i / 8f;
                var top = new Vector3(Mathf.Lerp(-7.5f, 7.5f, u) + Range(r, -.5f, .5f), 8.5f, Range(r, -9, -6));
                var bottom = new Vector3(top.x * 1.35f + Range(r, -.8f, .8f), -.9f, top.z + Range(r, 2.5f, 4.5f));
                Shaft(beams, top, bottom, Range(r, .45f, .95f), Range(r, 0, 6.28f));
            }
            var shafts = beams.Emit("Sun shafts", root.transform, rayMat, false);
            var shaftMesh = shafts.GetComponent<MeshFilter>().sharedMesh; var sb = shaftMesh.bounds; sb.Expand(3); shaftMesh.bounds = sb;

            // the pick's own light: a shaft (moved by Focus), plus a spotlight on the character
            var pick = new Geo(); Shaft(pick, new Vector3(.9f, 7.5f, -2.2f), new Vector3(0, -.9f, 0), .55f, 1.3f);
            var beam = pick.Emit("Pick light shaft", root.transform, spotRayMat, false).transform; handle.Beam = beam;
            var pm = beam.GetComponent<MeshFilter>().sharedMesh; var pb = pm.bounds; pb.Expand(3); pm.bounds = pb;
            var spot = new GameObject("Pick spotlight").AddComponent<Light>(); spot.transform.SetParent(root.transform, false);
            spot.type = LightType.Spot; spot.spotAngle = 34; spot.innerSpotAngle = 12; spot.range = 10; spot.intensity = 0;
            spot.color = new Color(1, .94f, .82f); spot.shadows = LightShadows.None; spot.cullingMask = 1 << layer; spot.renderMode = LightRenderMode.ForcePixel;
            handle.Spot = spot;

            // bubbles from three vents and slowly drifting plankton motes
            var bubbles = new Geo();
            Vector3[] vents = { new Vector3(-4.6f, 0, -3.8f), new Vector3(5.2f, 0, -4.6f), new Vector3(.9f, 0, -6.6f) };
            foreach (var vent in vents) for (int i = 0; i < 26; i++)
            {
                var p = vent + new Vector3(Range(r, -.18f, .18f), 0, Range(r, -.18f, .18f)); p.y = LobbyFloor(p.x, p.z) + .05f;
                bubbles.Ellipsoid(p, Vector3.one * Range(r, .02f, .065f), Color.white, 8, 6, 0, new Vector2(Range(r, 0, 1), Range(r, .06f, .12f)));
            }
            var bubbleObject = bubbles.Emit("Bubbles", root.transform, bubbleMat, false);
            bubbleObject.GetComponent<MeshFilter>().sharedMesh.bounds = new Bounds(new Vector3(0, 5, -4), new Vector3(20, 16, 10));
            var motes = new Geo();
            for (int i = 0; i < 170; i++)
            {
                var p = new Vector3(Range(r, -11, 11), Range(r, -8, -1), Range(r, -9, 2.5f));
                motes.Ellipsoid(p, Vector3.one * Range(r, .008f, .02f), Color.white, 5, 4, 0, new Vector2(Range(r, 0, 1), Range(r, .012f, .03f)));
            }
            var moteObject = motes.Emit("Plankton", root.transform, moteMat, false);
            moteObject.GetComponent<MeshFilter>().sharedMesh.bounds = new Bounds(new Vector3(0, 1, -3), new Vector3(24, 24, 14));

            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off; }
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            return handle;
        }

        // A crossed pair of rippling ribbons from top to bottom (the tank's shaft mesh, Atmosphere mode 2).
        private static void Shaft(Geo g, Vector3 top, Vector3 bottom, float width, float phase)
        {
            for (int plane = 0; plane < 2; plane++)
            {
                Vector3 across = plane == 0 ? Vector3.right : Vector3.forward; int n = g.Count; const int rows = 20, columns = 4;
                for (int row = 0; row <= rows; row++) for (int column = 0; column <= columns; column++)
                {
                    float depth = row / (float)rows, u = column / (float)columns;
                    var centre = Vector3.Lerp(top, bottom, depth); float spread = Mathf.Lerp(1, 2.4f, depth);
                    g.V(centre + across * (u * 2 - 1) * width * spread, Color.white, new Vector2(u, 1 - depth), new Vector2(phase, 0));
                    if (row < rows && column < columns) { int v = n + row * (columns + 1) + column; g.Q(v, v + 1, v + columns + 2, v + columns + 1); }
                }
            }
        }

        // Lobby sand height (stage space): level under the characters, dunes behind, rising into the back reef.
        internal static float LobbyFloor(float x, float z)
        {
            float back = Mathf.Clamp01((-z - 1.8f) / 2.4f);
            float dunes = (.06f * Mathf.Sin(x * .8f + 1.3f) + .05f * Mathf.Sin(z * 1.2f + x * .4f)) * back;
            float rise = Mathf.Max(0, -z - 5); rise = rise * rise * .035f;
            float sides = Mathf.Max(0, Mathf.Abs(x) - 5.0f); sides = sides * sides * .04f * Mathf.Clamp01((-z + 1) / 3);
            return -.95f + dunes + rise + sides;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // R59: one fish-guide poster on the changing-room wall.
    public struct LockerPoster { public Texture2D image; public string title, line1, line2; public Color tint; }

    public static partial class AquariumEnvironment
    {
        // R59: the divers' changing room (AquariumLockerRoom), built once at run time at the room's origin: tiled floor and
        // walls, a closed ceiling with three light panels, numbered lockers on two walls, a long bench, towels and the
        // double door to the dive hall, and the fish guide (one poster per species, with picture lights) on the front wall.
        public static GameObject LockerRoom(Transform parent, IList<LockerPoster> posters)
        {
            var o = AquariumLockerRoom.Origin;
            var root = new GameObject("DIVER CHANGING ROOM | closed, dry"); root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(o.x, o.y, o.z);
            var shader = Shader.Find("Aquarium/Locker Room");
            Func<string, float, float, Material> make = (name, gloss, glow) => { var m = new Material(shader) { name = "Changing room • " + name }; m.SetFloat("_Gloss", gloss); m.SetFloat("_Emission", glow); return m; };
            var tileMat = make("glazed tile", .55f, 0); var paintMat = make("paint and wood", .12f, 0); var metalMat = make("locker steel", .45f, 0); var glowMat = make("light panels", 0, 1.6f);
            const float W = AquariumLockerRoom.HalfWidth, D = AquariumLockerRoom.HalfDepth, H = AquariumLockerRoom.Height;
            var tiles = new Geo(); var paint = new Geo(); var metal = new Geo(); var glow = new Geo();

            // floor: grout bed and 0.5 m glazed tiles (two tones, slightly darker toward the walls)
            Flat(paint, new Vector3(-W, 0, -D), new Vector3(W, 0, D), C(0x50656b), 1);
            for (float x = -W; x < W - .01f; x += .5f) for (float z = -D; z < D - .01f; z += .5f)
            {
                bool odd = (Mathf.RoundToInt((x + W) * 2) + Mathf.RoundToInt((z + D) * 2)) % 2 == 1;
                float edge = Mathf.Min(Mathf.Min(x + W, W - x - .5f), Mathf.Min(z + D, D - z - .5f));
                var c = odd ? C(0x9cc3c9) : C(0xb6d6d9); c.a = Mathf.Lerp(.55f, 1, Mathf.Clamp01(edge / 1.2f));
                Flat(tiles, new Vector3(x + .012f, .004f, z + .012f), new Vector3(x + .488f, .004f, z + .488f), c, c.a);
            }
            // walls: tiled dado with a teal band, painted upper wall, a cornice; ceiling
            Wall(tiles, paint, new Vector3(-W, 0, D), new Vector3(W, 0, D), H);      // back
            Wall(tiles, paint, new Vector3(W, 0, -D), new Vector3(-W, 0, -D), H);    // front (fish guide)
            Wall(tiles, paint, new Vector3(-W, 0, -D), new Vector3(-W, 0, D), H);    // left
            Wall(tiles, paint, new Vector3(W, 0, D), new Vector3(W, 0, -D), H);      // right (door)
            Flat(paint, new Vector3(-W, H, -D), new Vector3(W, H, D), C(0xe7ecec), 1);
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 3.2f;
                paint.Box(new Vector3(x, H - .03f, .3f), new Vector3(1.9f, .05f, .8f), C(0xc9cfd0), Quaternion.identity);
                Flat(glow, new Vector3(x - .85f, H - .06f, -.05f), new Vector3(x + .85f, H - .06f, .65f), C(0xfff4dc), 1);
            }

            // lockers: back wall (13, facing -z) and left wall (7, facing +x)
            int number = 1;
            for (int i = 0; i < 13; i++) Locker(metal, paint, new Vector3(-4.16f + .32f + i * .64f, 0, D - .26f), Quaternion.Euler(0, 180, 0), i % 2 == 0 ? C(0x2e7fa8) : C(0x3995ba), root.transform, number++);
            for (int i = 0; i < 7; i++) Locker(metal, paint, new Vector3(-W + .26f, 0, -2.3f + .32f + i * .64f), Quaternion.Euler(0, 90, 0), i % 2 == 0 ? C(0x2a8a8f) : C(0x34a0a3), root.transform, number++);
            Label(root.transform, "잠수부 탈의실", new Vector3(0, 2.62f, D - .02f), Quaternion.identity, .03f, C(0x1f5f78));
            Label(root.transform, "DIVERS' CHANGING ROOM", new Vector3(0, 2.38f, D - .02f), Quaternion.identity, .013f, C(0x4c8193));

            // bench in front of the back lockers
            var bench = AquariumLockerRoom.Bench;
            paint.Box(new Vector3(bench.center.x, .45f, bench.center.y), new Vector3(bench.width, .06f, bench.height), C(0xb98a58), Quaternion.identity);
            paint.Box(new Vector3(bench.center.x, .41f, bench.center.y), new Vector3(bench.width - .1f, .03f, bench.height - .08f), C(0x8f6640), Quaternion.identity);
            foreach (float lx in new[] { -2.3f, 0f, 2.3f }) metal.Box(new Vector3(lx, .2f, bench.center.y), new Vector3(.06f, .4f, bench.height - .1f), C(0x9aa6aa), Quaternion.identity);
            // a kit bag and a folded towel on the bench
            paint.Box(new Vector3(-1.6f, .58f, bench.center.y), new Vector3(.55f, .2f, .3f), C(0xd4593f), Quaternion.Euler(0, 8, 0));
            paint.Box(new Vector3(1.2f, .51f, bench.center.y), new Vector3(.4f, .07f, .3f), C(0xf1e6c8), Quaternion.Euler(0, -5, 0));

            // right wall: double door to the dive hall with portholes, a sign and towel hooks
            paint.Box(new Vector3(W - .04f, 1.15f, -.2f), new Vector3(.08f, 2.3f, 1.9f), C(0x3a4c56), Quaternion.identity);
            foreach (float dz in new[] { -.6f, .2f })
            {
                metal.Box(new Vector3(W - .07f, 1.08f, dz), new Vector3(.05f, 2.1f, .78f), C(0x5f7f8d), Quaternion.identity);
                glow.Cylinder(new Vector3(W - .1f, 1.55f, dz), new Vector3(W - .095f, 1.55f, dz), .17f, .17f, C(0x59c3dd), 16);
                metal.Box(new Vector3(W - .12f, 1.05f, dz + (dz < 0 ? .3f : -.3f)), new Vector3(.04f, .3f, .04f), C(0xc7cfd2), Quaternion.identity);
            }
            Label(root.transform, "입수장  ▶", new Vector3(W - .02f, 2.52f, -.2f), Quaternion.Euler(0, 90, 0), .02f, C(0x1f6f86));
            Label(root.transform, "DIVE HALL", new Vector3(W - .02f, 2.33f, -.2f), Quaternion.Euler(0, 90, 0), .011f, C(0x4c8193));
            Color[] towels = { C(0xf2c94c), C(0xeb6f6f), C(0x56b4d3), C(0x7ccf9b) };
            for (int i = 0; i < 4; i++)
            {
                float z = 1.35f + i * .45f;
                metal.Box(new Vector3(W - .05f, 1.75f, z), new Vector3(.08f, .04f, .04f), C(0xc7cfd2), Quaternion.identity);
                paint.Box(new Vector3(W - .1f, 1.42f, z), new Vector3(.06f, .62f, .34f), towels[i], Quaternion.Euler(0, 0, 3 - i * 2));
            }
            for (int i = 0; i < 3; i++)   // fins and masks on a shelf by the door
            {
                metal.Box(new Vector3(W - .2f, 1.95f, -2.1f), new Vector3(.32f, .03f, 1.6f), C(0xb8c1c4), Quaternion.identity);
                paint.Box(new Vector3(W - .2f, 2.03f, -2.6f + i * .5f), new Vector3(.22f, .12f, .18f), i == 1 ? C(0xf2c94c) : C(0x2b3236), Quaternion.Euler(0, 0, 10));
            }

            // front wall: the fish guide
            Label(root.transform, "어종 도감", new Vector3(0, 2.72f, -D + .03f), Quaternion.Euler(0, 180, 0), .03f, C(0x1f5f78));
            Label(root.transform, "FISH GUIDE  ·  위장할 물고기의 습성을 익히세요", new Vector3(0, 2.5f, -D + .03f), Quaternion.Euler(0, 180, 0), .011f, C(0x4c8193));
            int count = posters == null ? 0 : posters.Count;
            float pw = count > 0 ? Mathf.Min(1.15f, 9.2f / count - .14f) : 1, gap = .14f, total = count * pw + Mathf.Max(0, count - 1) * gap;
            for (int i = 0; i < count; i++)
            {
                var poster = posters[i]; float cx = total / 2 - pw / 2 - i * (pw + gap);   // left to right as seen from inside
                float top = 2.3f, bottom = .95f;
                paint.Box(new Vector3(cx, (top + bottom) / 2, -D + .02f), new Vector3(pw + .08f, top - bottom + .08f, .03f), C(0x2c3a40), Quaternion.identity);
                var tint = poster.tint; tint.a = 1;
                Flat(paint, new Vector3(cx - pw / 2, bottom, -D + .036f), new Vector3(cx + pw / 2, top, -D + .036f), C(0xf6f0e2), 1, true);
                Flat(paint, new Vector3(cx - pw / 2, top - .07f, -D + .038f), new Vector3(cx + pw / 2, top, -D + .038f), Color.Lerp(tint, Color.white, .15f), 1, true);
                if (poster.image != null)
                {
                    var art = new Geo(); float aw = pw * .92f, ah = Mathf.Min(aw * .62f, .72f), ay = top - .1f - ah;
                    int n = art.Count;
                    art.V(new Vector3(cx + aw / 2, ay, -D + .04f), C(0xeaf3f2), new Vector2(0, 0)); art.V(new Vector3(cx - aw / 2, ay, -D + .04f), C(0xeaf3f2), new Vector2(1, 0));
                    art.V(new Vector3(cx - aw / 2, ay + ah, -D + .04f), C(0xeaf3f2), new Vector2(1, 1)); art.V(new Vector3(cx + aw / 2, ay + ah, -D + .04f), C(0xeaf3f2), new Vector2(0, 1));
                    art.Q(n, n + 3, n + 2, n + 1);
                    var picture = make("poster " + poster.title, .05f, 0); picture.SetFloat("_Tex", 1); picture.mainTexture = poster.image;
                    art.Emit("Fish guide • " + poster.title, root.transform, picture, false);
                }
                Label(root.transform, poster.title, new Vector3(cx, bottom + .42f, -D + .045f), Quaternion.Euler(0, 180, 0), .0115f, C(0x1e3f4a));
                if (!string.IsNullOrEmpty(poster.line1)) Label(root.transform, poster.line1, new Vector3(cx, bottom + .25f, -D + .045f), Quaternion.Euler(0, 180, 0), .0072f, C(0x41606a));
                if (!string.IsNullOrEmpty(poster.line2)) Label(root.transform, poster.line2, new Vector3(cx, bottom + .12f, -D + .045f), Quaternion.Euler(0, 180, 0), .0072f, C(0x41606a));
                metal.Box(new Vector3(cx, top + .12f, -D + .09f), new Vector3(pw * .5f, .04f, .08f), C(0x30383c), Quaternion.identity);   // picture lamp
            }

            tiles.Emit("Glazed tiles", root.transform, tileMat, true);
            paint.Emit("Paint, wood and fittings", root.transform, paintMat, true);
            metal.Emit("Locker steel", root.transform, metalMat, true);
            glow.Emit("Light panels", root.transform, glowMat, false);
            // the divers are lit by the same lamps (Unity lights, kept to the room)
            for (int i = -1; i <= 1; i++) Lamp(root.transform, new Vector3(i * 3.2f, 2.85f, .3f), new Color(1, .93f, .8f), 1.25f, 6.5f);
            Lamp(root.transform, new Vector3(4.2f, 2.4f, -.1f), new Color(.62f, .86f, 1f), .7f, 4.5f);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) { r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off; }
            return root;
        }

        private static void Lamp(Transform parent, Vector3 at, Color colour, float intensity, float range)
        {
            var light = new GameObject("Changing room lamp").AddComponent<Light>(); light.transform.SetParent(parent, false); light.transform.localPosition = at;
            light.type = LightType.Point; light.color = colour; light.intensity = intensity; light.range = range; light.shadows = LightShadows.None;
        }

        // A horizontal (or, with vertical, an x-y facing) rectangle between two corners.
        private static void Flat(Geo g, Vector3 a, Vector3 b, Color c, float occlusion, bool vertical = false)
        {
            c.a = occlusion; int n = g.Count;
            if (!vertical) { g.V(new Vector3(a.x, a.y, a.z), c); g.V(new Vector3(a.x, a.y, b.z), c); g.V(new Vector3(b.x, a.y, b.z), c); g.V(new Vector3(b.x, a.y, a.z), c); }
            else { g.V(new Vector3(a.x, a.y, a.z), c); g.V(new Vector3(a.x, b.y, a.z), c); g.V(new Vector3(b.x, b.y, a.z), c); g.V(new Vector3(b.x, a.y, a.z), c); }
            g.Q(n, n + 1, n + 2, n + 3);
        }

        // One wall from a to b (floor points): tiled dado to 1.2 m in small tiles, a teal band, painted above, cornice.
        private static void Wall(Geo tiles, Geo paint, Vector3 a, Vector3 b, float height)
        {
            var along = (b - a); float length = along.magnitude; along /= length;
            var inward = Vector3.Cross(Vector3.up, along);   // walls are listed so this points into the room
            Func<float, float, Vector3> at = (u, y) => a + along * u + Vector3.up * y;
            Band(paint, at, length, 0, 1.2f, C(0x5f7a80), 0, inward);   // grout behind the dado tiles
            for (float u = 0; u < length - .01f; u += .25f) for (float y = 0; y < 1.19f; y += .25f)
            {
                var c = ((int)(u * 4) + (int)(y * 4)) % 7 == 0 ? C(0xbfe3e6) : C(0xdcefef);
                c.a = Mathf.Lerp(.6f, 1, Mathf.Clamp01(y / .5f));
                int n = tiles.Count; var lift = inward * .006f;
                tiles.V(at(u + .01f, y + .01f) + lift, c); tiles.V(at(u + .01f, y + .24f) + lift, c); tiles.V(at(u + .24f, y + .24f) + lift, c); tiles.V(at(u + .24f, y + .01f) + lift, c);
                tiles.Q(n, n + 1, n + 2, n + 3);
            }
            Band(paint, at, length, 1.2f, 1.32f, C(0x2a9db0), .012f, inward);
            Band(paint, at, length, 1.32f, height - .12f, C(0xf1ebde), 0, inward);
            Band(paint, at, length, height - .12f, height, C(0xdfe5e4), .02f, inward);
            Band(paint, at, length, 0, .1f, C(0x3c5359), .015f, inward);   // skirting
        }
        private static void Band(Geo g, Func<float, float, Vector3> at, float length, float y0, float y1, Color c, float lift, Vector3 inward)
        {
            int n = g.Count; var o = inward * lift;
            g.V(at(0, y0) + o, c); g.V(at(0, y1) + o, c); g.V(at(length, y1) + o, c); g.V(at(length, y0) + o, c);
            g.Q(n, n + 1, n + 2, n + 3);
        }

        // One full-height locker (0.62 x 0.5 x 1.95) standing at foot (floor centre of its front edge line), facing rotation*forward.
        private static void Locker(Geo metal, Geo paint, Vector3 foot, Quaternion rotation, Color colour, Transform root, int number)
        {
            Func<Vector3, Vector3> P = local => foot + rotation * local;
            paint.Box(P(new Vector3(0, .05f, 0)), new Vector3(.62f, .1f, .48f), C(0x2d3b40), rotation);   // plinth
            metal.Box(P(new Vector3(0, 1.03f, 0)), new Vector3(.62f, 1.86f, .5f), Color.Lerp(colour, Color.black, .25f), rotation);
            metal.Box(P(new Vector3(0, 1.03f, .254f)), new Vector3(.57f, 1.8f, .012f), colour, rotation);   // door
            for (int v = 0; v < 4; v++) metal.Box(P(new Vector3(0, 1.72f - v * .055f, .262f)), new Vector3(.32f, .018f, .006f), C(0x1d3b48), rotation);   // vents
            metal.Box(P(new Vector3(-.21f, 1.05f, .27f)), new Vector3(.03f, .16f, .03f), C(0xd6dde0), rotation);   // handle
            paint.Box(P(new Vector3(0, 1.48f, .262f)), new Vector3(.16f, .08f, .006f), C(0xf4efe2), rotation);   // number plate
            Label(root, number.ToString("00"), P(new Vector3(0, 1.48f, .268f)), rotation * Quaternion.Euler(0, 180, 0), .0085f, C(0x2b3a40));
        }
    }
}

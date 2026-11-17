using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R78b: the tutorial tank (AquariumMaps.Tutorial, "연습 수조"). A small glass box, 22 x 14 m of water and 7 m deep,
    // standing in a dim gallery room: the same sand, glass, water, light shafts, painted rocks, corals and seaweed as
    // the playable tanks, but only what a lesson needs (three rocks to hide behind, two return drains, open water in
    // between). It is laid out by AquariumTwinMap's single box (TutorialShape, with a lid at the water surface), so
    // collision, the camera and the fish all use the ordinary map queries. Only the tutorials select it: the lobby
    // never offers it and it is never sent to another machine.
    public static class AquariumTutorialMap
    {
        public const float HalfX = AquariumTwinMap.TutorialHalfX, HalfZ = AquariumTwinMap.TutorialHalfZ, Top = AquariumTwinMap.TutorialTop;
        // The two return drains (east by the worn boulder, west by the pillar) and the way each lid slides.
        public static readonly Point[] DrainSites = { new Point(7.8f, 0, -3.4f), new Point(-7.8f, 0, 3.2f) };
        public static readonly float[] DrainYaw = { 20f, 200f }, SlideYaw = { 300f, 120f };
        public static Point Drain(int index) { var p = DrainSites[index]; p.y = AquariumSeabed.TwinHeight(p.x, p.z); return p; }
        // Rocks: kind (AquariumRockShapes), x, z, scale, yaw.
        public static readonly float[][] Rocks =
        {
            new[] { (float)AquariumRockShapes.Pillar, -3.2f, 4.0f, .95f, 30f },
            new[] { (float)AquariumRockShapes.Worn, 3.2f, -3.6f, .8f, 115f },
            new[] { (float)AquariumRockShapes.Split, 6.6f, 3.9f, .72f, 250f },
            new[] { (float)AquariumRockShapes.Pebbles, -7.6f, -4.2f, .7f, 0f },
            new[] { (float)AquariumRockShapes.Pebbles, .6f, 5.3f, .55f, 80f },
        };
    }

    public sealed partial class AquariumSceneryLayout
    {
        // The tutorial tank's dressing is the same for every game (the seed only names the layout).
        private static AquariumSceneryLayout BuildTutorial(int seed)
        {
            var layout = new AquariumSceneryLayout(seed, new float[AquariumMonolithLayout.Groups.Length], new float[0]);
            layout.Map = AquariumMaps.Tutorial;
            var random = new System.Random(7802);
            var rocks = new List<AquariumRock>();
            foreach (var spec in AquariumTutorialMap.Rocks)
            {
                var rock = MakeRock(random, (int)spec[0], spec[1], spec[2], spec[3]); rock.yaw = spec[4];
                rocks.Add(rock);
            }
            layout.TwinRocks = new AquariumRockSet(rocks.ToArray());
            layout.DrainSites = (Point[])AquariumTutorialMap.DrainSites.Clone();

            var plants = new List<AquariumScenePlant>(); var corals = new List<AquariumSceneCoral>();
            var anemones = new List<AquariumSceneAnemone>(); var details = new List<AquariumSceneDetail>();
            Func<float, float, float> range = (a, b) => a + (float)random.NextDouble() * (b - a);
            Func<float, float, float, bool> open = (x, z, pad) =>
            {
                if (AquariumTwinMap.Water(x, z) < pad + .25f) return false;
                foreach (var d in AquariumTutorialMap.DrainSites) if (Flat(new Point(x, 0, z), d) < AquariumDrainLayout.ClearanceRadius + pad) return false;
                return !layout.TwinRocks.Overlaps(new Point(x, AquariumSeabed.TwinHeight(x, z) + .3f, z), pad + .15f);
            };
            // Eelgrass and kelp along the back (north) glass and in the two back corners: a green backdrop that leaves the middle open.
            for (float x = -9.6f; x <= 9.6f; x += 1.15f)
            {
                float px = x + range(-.3f, .3f), pz = range(5.5f, 6.3f);
                if (!open(px, pz, .2f)) continue;
                int form = random.NextDouble() < .55 ? 2 : random.Next(5);
                float height = form == 2 ? range(1.8f, 3.4f) : range(1.1f, 2.4f);
                plants.Add(new AquariumScenePlant { position = new Point(px, AquariumSeabed.TwinHeight(px, pz) + .01f, pz), form = form, height = height, radius = PlantSpread(form, height),
                    phase = range(0, 6.28f), yaw = range(0, 360) });
            }
            foreach (var corner in new[] { new Point(-9.6f, 0, -5.6f), new Point(9.7f, 0, 5.4f), new Point(-9.8f, 0, 5.2f), new Point(9.5f, 0, -5.8f) })
                for (int i = 0; i < 3; i++)
                {
                    float px = corner.x + range(-.7f, .7f), pz = corner.z + range(-.6f, .6f);
                    if (!open(px, pz, .2f)) continue;
                    int form = random.Next(5); float height = range(1.0f, 2.2f);
                    plants.Add(new AquariumScenePlant { position = new Point(px, AquariumSeabed.TwinHeight(px, pz) + .01f, pz), form = form, height = height, radius = PlantSpread(form, height),
                        phase = range(0, 6.28f), yaw = range(0, 360) });
                }
            // Corals, grass tufts, pebbles, shells and sea stars round each rock's foot.
            foreach (var spec in AquariumTutorialMap.Rocks)
            {
                float foot = AquariumRockShapes.Footprint((int)spec[0]) * spec[3];
                bool heap = (int)spec[0] == AquariumRockShapes.Pebbles;
                for (int i = 0, made = 0; i < 40 && made < (heap ? 2 : 5); i++)
                {
                    double a = random.NextDouble() * Math.PI * 2; float d = foot * range(.75f, 1.05f);
                    float px = spec[1] + (float)Math.Cos(a) * d, pz = spec[2] + (float)Math.Sin(a) * d, size = range(.22f, .4f);
                    if (!open(px, pz, size * .5f)) continue;
                    corals.Add(new AquariumSceneCoral { position = new Point(px, AquariumSeabed.TwinHeight(px, pz) + .01f, pz), size = size, kind = random.Next(6), yaw = range(0, 360) }); made++;
                }
                for (int i = 0, made = 0; i < 60 && made < (heap ? 5 : 9); i++)
                {
                    double a = random.NextDouble() * Math.PI * 2; float d = foot * range(.8f, 1.35f);
                    float px = spec[1] + (float)Math.Cos(a) * d, pz = spec[2] + (float)Math.Sin(a) * d, height = range(.35f, .7f);
                    if (!open(px, pz, .1f)) continue;
                    plants.Add(new AquariumScenePlant { position = new Point(px, AquariumSeabed.TwinHeight(px, pz) + .005f, pz), form = GrassForm, height = height, radius = PlantSpread(GrassForm, height),
                        phase = range(0, 6.28f), yaw = range(0, 360) }); made++;
                }
                for (int i = 0, made = 0; i < 60 && made < 10; i++)
                {
                    double a = random.NextDouble() * Math.PI * 2; float d = foot * range(.8f, 1.5f);
                    float px = spec[1] + (float)Math.Cos(a) * d, pz = spec[2] + (float)Math.Sin(a) * d;
                    int kind = made % 5 == 4 ? 1 : 0; float size = kind == 0 ? range(.04f, .11f) : range(.07f, .12f);
                    if (!open(px, pz, 0)) continue;
                    details.Add(new AquariumSceneDetail { position = new Point(px, AquariumSeabed.TwinHeight(px, pz) + .01f, pz), size = size, kind = kind, yaw = range(0, 360) }); made++;
                }
            }
            // An anemone by each pebble heap, a few sea stars and shells on the open sand.
            foreach (var at in new[] { new Point(-6.3f, 0, -5.0f), new Point(1.9f, 0, 5.6f) })
                if (open(at.x, at.z, .3f)) anemones.Add(new AquariumSceneAnemone { position = new Point(at.x, AquariumSeabed.TwinHeight(at.x, at.z) + .02f, at.z), size = .42f, kind = anemones.Count % 2, yaw = range(0, 360) });
            for (int i = 0, made = 0; i < 80 && made < 16; i++)
            {
                float px = range(-10f, 10f), pz = range(-6.2f, 6.2f); int kind = made % 4 == 3 ? 2 : made % 4 == 2 ? 1 : 0;
                float size = kind == 0 ? range(.03f, .09f) : kind == 1 ? range(.07f, .12f) : range(.15f, .26f);
                if (!open(px, pz, 0)) continue;
                details.Add(new AquariumSceneDetail { position = new Point(px, AquariumSeabed.TwinHeight(px, pz) + .01f, pz), size = size, kind = kind, yaw = range(0, 360) }); made++;
            }
            layout.Anemones = anemones.ToArray(); layout.Plants = plants.ToArray(); layout.Corals = corals.ToArray(); layout.Details = details.ToArray();
            var spots = new Point[layout.Anemones.Length];
            for (int i = 0; i < spots.Length; i++) spots[i] = layout.Anemones[i].position;
            layout.AnemoneSpots = spots;
            var heads = new Point[layout.Corals.Length];
            for (int i = 0; i < heads.Length; i++) heads[i] = layout.Corals[i].position + new Point(0, layout.Corals[i].size * .7f, 0);
            layout.CoralSpots = heads;
            return layout;
        }
    }

    public static partial class AquariumEnvironment
    {
        private static void FaceQuad(Geo g, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want, Color tint)
        { if (Vector3.Dot(Vector3.Cross(b - a, c - a), want) >= 0) g.Quad(a, b, c, d, tint); else g.Quad(a, d, c, b, tint); }

        // A closed band following the water's outline: from `inner` to `outer` metres outside the glass, between two heights.
        private static void TutorialBand(Geo g, Point[] loop, Point[] outward, float inner, float outer, float low, float high, Color tint)
        {
            int n = loop.Length;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 ai = V(loop[i] + outward[i] * inner, 0), bi = V(loop[i] + outward[i] * outer, 0), aj = V(loop[j] + outward[j] * inner, 0), bj = V(loop[j] + outward[j] * outer, 0);
                Vector3 lo = Vector3.up * low, hi = Vector3.up * high, o = V(outward[i], 0);
                FaceQuad(g, ai + hi, aj + hi, bj + hi, bi + hi, Vector3.up, tint);
                FaceQuad(g, ai + lo, aj + lo, bj + lo, bi + lo, Vector3.down, tint * .8f);
                FaceQuad(g, ai + lo, aj + lo, aj + hi, ai + hi, -o, tint * .92f);
                FaceQuad(g, bi + lo, bj + lo, bj + hi, bi + hi, o, tint * .92f);
            }
        }

        private static AquariumEnvironmentRoot CreateTutorialTank(Transform parent)
        {
            var root = new GameObject("TUTORIAL TANK | Environment"); root.transform.SetParent(parent, false);
            var marker = root.AddComponent<AquariumEnvironmentRoot>();
            marker.map = AquariumMaps.Tutorial; marker.artVersion = "Tutorial Tank 1.0 / small glass training tank";
            var r = new System.Random(780113);
            var caustics = Resources.Load<Texture2D>("Aquarium/Caustics");
            if (caustics == null) caustics = MakeCaustics();
            var sand = ScannedSurface("Lagoon sand / fine carbonate grains", "SandGrain", caustics, .48f, true);
            var architecture = Lit("Gallery • limestone, bronze & oak", caustics, .38f, 0);
            var interior = new Material(Shader.Find("Aquarium/Dim Visitor Gallery")) { name = "Visitor gallery / charcoal stone" }; interior.SetFloat("_Exposure", .22f);
            var interiorFloor = new Material(interior) { name = "Visitor gallery / honed dark limestone" }; interiorFloor.SetFloat("_Paving", 1); interiorFloor.SetFloat("_Exposure", .30f);
            var trim = Lit("Recessed light strips", caustics, .6f, 0); trim.SetFloat("_Emission", 2.5f);
            var glow = Lit("Visitor gallery / low warm guidance", caustics, .3f, 0); glow.SetFloat("_Emission", .6f);
            var glass = Fx("Low iron glass", C(0x6ad6db), .2f, 0);
            var rays = Fx("Sunlight shafts • moving refraction", C(0xb9eaff), .15f, 2);
            var water = new Material(Shader.Find("Aquarium/Lagoon Water")) { name = "Clear turquoise water" };
            water.SetColor("_Color", new Color(.025f, .58f, .66f, .075f));

            var field = new TwinField();
            TwinSand(root.transform, sand, field);

            float top = AquariumTutorialMap.Top;
            var loop = AquariumTwinMap.Outline(0, .45f); int n = loop.Length;
            var outward = new Point[n]; for (int i = 0; i < n; i++) outward[i] = AquariumTwinMap.Outward(loop[i]);

            // ---- the water surface (seen from below and from the room)
            var surface = new Geo(); var centre = new Vector3(0, top, 0);
            for (int i = 0; i < n; i++)
            {
                Vector3 a = V(loop[i], top), b = V(loop[(i + 1) % n], top);
                surface.Triangle(centre, a, b, Color.white); surface.Triangle(centre, b, a, Color.white);
            }
            surface.Emit("T0 • Animated water surface", root.transform, water, false);

            // ---- four glass walls (rounded corners), from under the sand to a hand above the water
            var panes = new Geo();
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                FaceQuad(panes, V(loop[i], -.35f), V(loop[j], -.35f), V(loop[j], top + .3f), V(loop[i], top + .3f), -V(outward[i], 0), Color.white);
            }
            panes.Emit("04 • Curved aquarium glass / tutorial tank", root.transform, glass, false);

            // ---- the stone plinth round the foot, the bronze rail on top of the glass, slim posts
            var frame = new Geo();
            TutorialBand(frame, loop, outward, .03f, .62f, -.1f, .62f, Stone);
            TutorialBand(frame, loop, outward, .62f, .9f, -.1f, .2f, Stone * .86f);
            TutorialBand(frame, loop, outward, -.05f, .1f, top + .3f, top + .4f, Brass);
            for (int i = 0; i < n; i++)
            {
                // a post about every 3.6 m on the straight runs
                int every = Mathf.Max(1, Mathf.RoundToInt(3.6f / .45f)); if (i % every != 0) continue;
                var p = loop[i] + outward[i] * .06f;
                frame.Cylinder(new Vector3(p.x, .6f, p.z), new Vector3(p.x, top + .32f, p.z), .035f, .035f, Brass, 8);
            }
            frame.Emit("T1 • Limestone plinth and bronze rail", root.transform, architecture);

            // ---- the room: a dim gallery hall round the tank
            const float rx = 20f, rz = 15f, ceiling = 11.5f;
            var floor = new Geo();
            FaceQuad(floor, new Vector3(-rx, -.1f, -rz), new Vector3(rx, -.1f, -rz), new Vector3(rx, -.1f, rz), new Vector3(-rx, -.1f, rz), Vector3.up, C(0xb8aa8f));
            floor.Emit("T2 • Gallery floor / honed limestone", root.transform, interiorFloor, false);
            var hall = new Geo(); Color wall = C(0x8f8a84);
            FaceQuad(hall, new Vector3(-rx, -.1f, rz), new Vector3(rx, -.1f, rz), new Vector3(rx, ceiling, rz), new Vector3(-rx, ceiling, rz), Vector3.back, wall);
            FaceQuad(hall, new Vector3(-rx, -.1f, -rz), new Vector3(rx, -.1f, -rz), new Vector3(rx, ceiling, -rz), new Vector3(-rx, ceiling, -rz), Vector3.forward, wall);
            FaceQuad(hall, new Vector3(-rx, -.1f, -rz), new Vector3(-rx, -.1f, rz), new Vector3(-rx, ceiling, rz), new Vector3(-rx, ceiling, -rz), Vector3.right, wall * .92f);
            FaceQuad(hall, new Vector3(rx, -.1f, -rz), new Vector3(rx, -.1f, rz), new Vector3(rx, ceiling, rz), new Vector3(rx, ceiling, -rz), Vector3.left, wall * .92f);
            FaceQuad(hall, new Vector3(-rx, ceiling, -rz), new Vector3(rx, ceiling, -rz), new Vector3(rx, ceiling, rz), new Vector3(-rx, ceiling, rz), Vector3.down, wall * .7f);
            // wall pilasters, so the hall does not read as a bare box
            for (int k = -3; k <= 3; k++)
            {
                hall.Box(new Vector3(k * 5.6f, ceiling * .5f, rz - .25f), new Vector3(.7f, ceiling, .5f), wall * 1.08f, Quaternion.identity);
                hall.Box(new Vector3(k * 5.6f, ceiling * .5f, -rz + .25f), new Vector3(.7f, ceiling, .5f), wall * 1.08f, Quaternion.identity);
            }
            for (int k = -2; k <= 2; k++)
            {
                hall.Box(new Vector3(rx - .25f, ceiling * .5f, k * 5.6f), new Vector3(.5f, ceiling, .7f), wall * 1.08f, Quaternion.identity);
                hall.Box(new Vector3(-rx + .25f, ceiling * .5f, k * 5.6f), new Vector3(.5f, ceiling, .7f), wall * 1.08f, Quaternion.identity);
            }
            hall.Emit("T3 • Gallery hall walls and ceiling", root.transform, interior, false);
            // warm guidance strips along the foot of the walls and the lamps over the tank
            var lights = new Geo(); Color warm = C(0xffd9a0);
            lights.Box(new Vector3(0, .25f, rz - .06f), new Vector3(2 * rx - 2, .06f, .06f), warm, Quaternion.identity);
            lights.Box(new Vector3(0, .25f, -rz + .06f), new Vector3(2 * rx - 2, .06f, .06f), warm, Quaternion.identity);
            lights.Box(new Vector3(rx - .06f, .25f, 0), new Vector3(.06f, .06f, 2 * rz - 2), warm, Quaternion.identity);
            lights.Box(new Vector3(-rx + .06f, .25f, 0), new Vector3(.06f, .06f, 2 * rz - 2), warm, Quaternion.identity);
            lights.Emit("T4 • Warm guidance strips", root.transform, glow, false);
            var lamps = new Geo(); var housing = new Geo();
            for (int k = -1; k <= 1; k++)
            {
                lamps.Box(new Vector3(k * 6.4f, ceiling - .32f, 0), new Vector3(4.6f, .06f, 1.1f), Color.white, Quaternion.identity);
                housing.Box(new Vector3(k * 6.4f, ceiling - .18f, 0), new Vector3(4.9f, .26f, 1.4f), C(0x3a3d42), Quaternion.identity);
            }
            lamps.Emit("T5 • Lamps over the tank", root.transform, trim, false);
            housing.Emit("T5b • Lamp housings", root.transform, interior, false);
            Label(root.transform, "연 습 수 조", new Vector3(0, 8.6f, rz - .32f), Quaternion.identity, .06f, C(0x9fd8de));

            // ---- light shafts through the water
            var beams = new Geo(); int placed = 0;
            for (int i = 0; i < 80 && placed < 14; i++)
            {
                var location = new Point(Range(r, -9.5f, 9.5f), 0, Range(r, -5.5f, 5.5f));
                var foot = new Point(location.x - 2.1f, 0, location.z - 1.6f);
                if (AquariumTwinMap.Water(location.x, location.z) < .8f || AquariumTwinMap.Water(foot.x, foot.z) < .6f) continue;
                Vector3 high = new Vector3(location.x, top, location.z), low = new Vector3(foot.x, .5f, foot.z); float width = Range(r, .28f, .55f);
                float phase = Range(r, 0, Mathf.PI * 2);
                for (int plane = 0; plane < 2; plane++)
                {
                    Vector3 side = plane == 0 ? Vector3.right : Vector3.forward;
                    int start = beams.Count; const int rows = 14, columns = 4;
                    for (int row = 0; row <= rows; row++) for (int column = 0; column <= columns; column++)
                    {
                        float depth = row / (float)rows, u = column / (float)columns;
                        var c = Vector3.Lerp(high, low, depth); float spread = Mathf.Lerp(1, 2.4f, depth);
                        beams.V(c + side * (u * 2 - 1) * width * spread, Color.white, new Vector2(u, 1 - depth), new Vector2(phase, 0));
                        if (row < rows && column < columns) { int v = start + row * (columns + 1) + column; beams.Q(v, v + 1, v + columns + 2, v + columns + 1); }
                    }
                }
                placed++;
            }
            var shafts = beams.Emit("19 • Rippling refracted underwater sun shafts", root.transform, rays, false);
            var shaftMesh = shafts.GetComponent<MeshFilter>().sharedMesh; var bounds = shaftMesh.bounds; bounds.Expand(2); shaftMesh.bounds = bounds;
            return marker;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // R57: the "쌍둥이 수조" gallery. The same materials, glass, deck, visitor gallery, lighting and atmosphere as the
    // Coral Gallery, built around two tanks and the acrylic tunnel between them (AquariumTwinMap). There are no rocks,
    // cliffs, seaweed, corals or anemones in this map. The editor bakes it into the TwinGallery prefab.
    public static partial class AquariumEnvironment
    {
        public const string TwinResourcePath = "AquariumTwin/TwinGallery";

        // The gallery of a map: the one already in the scene, else its baked prefab, else built now.
        public static AquariumEnvironmentRoot EnsureMap(int map, Transform parent)
        {
            map = AquariumMaps.Clamp(map);
            foreach (var existing in UnityEngine.Object.FindObjectsByType<AquariumEnvironmentRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (existing.map == map && existing.gameObject.scene.IsValid()) { OptimizeRendering(existing); return existing; }
            var prefab = Resources.Load<GameObject>(map == AquariumMaps.Twin ? TwinResourcePath : ResourcePath);
            AquariumEnvironmentRoot environment;
            if (prefab != null) environment = UnityEngine.Object.Instantiate(prefab, parent).GetComponent<AquariumEnvironmentRoot>();
            else environment = map == AquariumMaps.Twin ? CreateTwin(parent) : Create(parent);
            OptimizeRendering(environment); return environment;
        }

        public static AquariumEnvironmentRoot CreateTwin(Transform parent = null)
        {
            int previous = AquariumMaps.Current;
            AquariumMaps.Select(AquariumMaps.Twin);
            try { return CreateTwinBaked(parent); }
            finally { AquariumMaps.Select(previous); }
        }

        private static AquariumEnvironmentRoot CreateTwinBaked(Transform parent)
        {
            var root = new GameObject("TWIN TANKS | Environment"); root.transform.SetParent(parent, false);
            var marker = root.AddComponent<AquariumEnvironmentRoot>();
            marker.map = AquariumMaps.Twin; marker.artVersion = "Twin Tanks 1.0 / two tanks and an acrylic tunnel";
            var r = new System.Random(570113);
            var caustics = Resources.Load<Texture2D>("Aquarium/Caustics");
            if (caustics == null) caustics = MakeCaustics();
            var sand = ScannedSurface("Lagoon sand / fine carbonate grains", "SandGrain", caustics, .48f, true);
            var architecture = Lit("Gallery • limestone, bronze & oak", caustics, .38f, 0);
            var interior = new Material(Shader.Find("Aquarium/Dim Visitor Gallery")) { name = "Visitor gallery / charcoal stone" }; interior.SetFloat("_Exposure", .20f);
            var interiorFixtures = new Material(interior) { name = "Visitor gallery / dark wood and bronze" }; interiorFixtures.SetFloat("_Exposure", .32f);
            var interiorFloor = new Material(interior) { name = "Visitor gallery / honed dark limestone" }; interiorFloor.SetFloat("_Paving", 1); interiorFloor.SetFloat("_Exposure", .28f);
            var interiorGlow = Lit("Visitor gallery / low warm guidance", caustics, .3f, 0); interiorGlow.SetFloat("_Emission", .35f);
            var paving = Lit("Gallery • continuous limestone paving", caustics, .33f, 0); paving.SetFloat("_Paving", 1);
            var garden = Lit("Botanical planters", caustics, .3f, 0); garden.SetFloat("_Sway", .22f);
            var trim = Lit("Recessed light strips", caustics, .6f, 0); trim.SetFloat("_Emission", 2.5f);
            var glass = Fx("Low iron glass", C(0x6ad6db), .24f, 0);
            var acrylic = Fx("Tunnel • cast acrylic arch", C(0x74d9de), .34f, 0);
            var bubbles = Fx("Aeration • pearl bubbles", C(0xb9f5ff), .62f, 1);
            var rays = Fx("Sunlight shafts • moving refraction", C(0xb9eaff), .15f, 2);
            var water = new Material(Shader.Find("Aquarium/Lagoon Water")) { name = "Clear turquoise water" };
            water.SetColor("_Color", new Color(.025f, .58f, .66f, .075f));

            var field = new TwinField();
            TwinSand(root.transform, sand, field);
            TwinShell(root.transform, architecture, paving, trim, glass, acrylic, water, interiorFloor, field, marker);
            TwinRim(root.transform, architecture, trim);
            TwinInterior(root.transform, interior, interiorFixtures, interiorGlow, marker);
            TwinFurniture(root.transform, architecture, garden, trim, r);
            TwinAtmosphere(root.transform, bubbles, rays, r, marker);
            return marker;
        }

        private static Vector3 V(Point p, float y) { return new Vector3(p.x, y, p.z); }

        // ------------------------------------------------------------------ sand
        private static void TwinSand(Transform root, Material material, TwinField field)
        {
            var sand = new Geo(); field.Surface(sand, field.Water, 0, Color.white);
            var go = sand.Emit("01 • Contoured sand bed / dunes and current ripples", root, material);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            var old = mesh.vertices; var indices = mesh.triangles; var remap = new int[old.Length];
            var unique = new List<Vector3>(); var lookup = new Dictionary<long, int>();
            for (int i = 0; i < old.Length; i++)
            {
                long key = ((long)Mathf.RoundToInt(old[i].x * 10000) << 32) | (uint)Mathf.RoundToInt(old[i].z * 10000);
                int index; if (!lookup.TryGetValue(key, out index)) { index = unique.Count; unique.Add(old[i]); lookup[key] = index; }
                remap[i] = index;
            }
            for (int i = 0; i < indices.Length; i++) indices[i] = remap[indices[i]];
            mesh.Clear(); mesh.SetVertices(unique); mesh.SetTriangles(indices, 0);
            var points = mesh.vertices; var normals = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var p = new Point(points[i].x, points[i].z); points[i].y = AquariumSeabed.Height(p);
                var n = AquariumSeabed.Normal(p); normals[i] = new Vector3(n.x, n.y, n.z);
            }
            mesh.vertices = points; mesh.normals = normals; SurfaceBasis(mesh); mesh.RecalculateBounds();
            var bounds = mesh.bounds; bounds.Expand(.7f); mesh.bounds = bounds;
        }

        // ------------------------------------------------------------------ deck, water, glass and the tunnel
        private static void TwinShell(Transform root, Material stone, Material paving, Material light, Material glass, Material acrylic,
            Material water, Material interiorFloor, TwinField field, AquariumEnvironmentRoot marker)
        {
            var deck = new Geo(); var soffit = new Geo(); var dryFloor = new Geo();
            field.Surface(deck, field.Deck, Elevated(7.275f), Stone);
            field.Surface(soffit, field.Deck, Elevated(6.96f), Stone * .78f, true);
            field.Surface(dryFloor, field.InteriorFloor, .23f, C(0xb8aa8f));
            deck.Emit("06 • Joined limestone promenade over both tanks and the tunnel hall", root, paving);
            soffit.Emit("06b • Continuous sealed deck soffit", root, stone);
            dryFloor.Emit("06c • Unified dry gallery and tunnel hall floor", root, interiorFloor);
            var surfaces = field.Contours(field.Tanks);
            if (surfaces.Count != 2) throw new InvalidOperationException("The twin tanks must have two open water surfaces, found " + surfaces.Count + ".");
            for (int s = 0; s < surfaces.Count; s++)
            {
                var waterGeo = new Geo(); FillWaterPolygon(waterGeo, surfaces[s]);
                waterGeo.Emit("05 • Animated water surface / tank " + (surfaces[s][0].x < 0 ? "A" : "B"), root, water, false);
            }

            var loops = field.Contours(field.Water);
            if (loops.Count != 1) throw new InvalidOperationException("The twin tanks and their tunnel must have one glass boundary, found " + loops.Count + ".");
            var loop = loops[0];
            var panels = new Geo(); var tunnel = new Geo(); var framing = new Geo(); var glow = new Geo(); var fascia = new Geo(); var trim = new Geo(); var rails = new Geo();
            float spring = AquariumTwinMap.Springline, top = Elevated(7.02f);
            float postTravel = 0;
            for (int i = 0; i < loop.Count; i++)
            {
                var a = loop[i]; var b = loop[(i + 1) % loop.Count]; var mid = (a + b) * .5f;
                bool inTunnel = AquariumTwinMap.InTunnel(mid.x);
                (inTunnel ? tunnel : panels).Quad(Height(a, .27f), Height(b, .27f), Height(b, inTunnel ? spring : top), Height(a, inTunnel ? spring : top), Color.white);
                postTravel += Vector3.Distance(a, b);
                if (!inTunnel && postTravel >= 1.5f)
                {
                    framing.Cylinder(Height(a, .27f), Height(a, top), .024f, .024f, Brass, 8);
                    framing.Cylinder(Height(a, .18f), Height(a, .33f), .075f, .075f, Navy, 10);
                    postTravel = 0;
                }
                if (inTunnel)
                {
                    // A brass rail where the walls spring into the arch, and the walls' low warm footlights.
                    var outward = Vector3.Cross(b - a, Vector3.up).normalized; var o = V(AquariumTwinMap.Outward(new Point(mid.x, mid.z)), 0);
                    if (Vector3.Dot(outward, o) < 0) outward = -outward;
                    rails.Cylinder(Height(a, spring) + outward * .05f, Height(b, spring) + outward * .05f, .045f, .045f, Brass, 8);
                }
            }
            BoundaryBand(framing, loop, .19f, .27f, .14f, Navy);
            PartialBand(framing, loop, p => !AquariumTwinMap.InTunnel(p.x), Elevated(6.98f), Elevated(7.025f), .09f, Brass);
            BoundaryBand(glow, loop, .32f, .35f, .035f, C(0x29d9f1));

            // The arch: a surface of revolution over the bore, flaring into each tank wall (dense rings in the bell mouths).
            var stations = new List<float>();
            for (int i = 0; i <= 16; i++) { float s = i / 16f; stations.Add(AquariumTwinMap.PlaneA + 3.2f * s * s); }
            for (float x = AquariumTwinMap.PlaneA + 3.7f; x < AquariumTwinMap.PlaneB - 3.45f; x += .5f) stations.Add(x);
            for (int i = 16; i >= 0; i--) { float s = i / 16f; stations.Add(AquariumTwinMap.PlaneB - 3.2f * s * s); }
            const int around = 40;
            int ring0 = tunnel.Count;
            foreach (float x in stations)
            {
                float radius = TunnelRadius(x);
                for (int k = 0; k <= around; k++)
                {
                    float phi = Mathf.PI * k / around;
                    tunnel.V(new Vector3(x, spring + radius * Mathf.Sin(phi), radius * Mathf.Cos(phi)), Color.white, new Vector2(k / (float)around, x));
                }
            }
            for (int s = 0; s < stations.Count - 1; s++) for (int k = 0; k < around; k++)
            { int v = ring0 + s * (around + 1) + k; tunnel.Q(v, v + 1, v + around + 2, v + around + 1); }
            // The tanks' front walls above each mouth.
            foreach (float plane in new[] { AquariumTwinMap.PlaneA, AquariumTwinMap.PlaneB })
            {
                float opening = AquariumTwinMap.TunnelHalfWidth + AquariumTwinMap.MouthFlare;
                const int columns = 48;
                for (int k = 0; k < columns; k++)
                {
                    float z0 = -opening + 2 * opening * k / columns, z1 = -opening + 2 * opening * (k + 1) / columns;
                    float y0 = spring + Mathf.Sqrt(Mathf.Max(0, opening * opening - z0 * z0)), y1 = spring + Mathf.Sqrt(Mathf.Max(0, opening * opening - z1 * z1));
                    panels.Quad(new Vector3(plane, y0, z0), new Vector3(plane, y1, z1), new Vector3(plane, top, z1), new Vector3(plane, top, z0), Color.white);
                }
                // Mullions continue over the portal wall.
                for (float z = -opening + 1.5f; z < opening - .2f; z += 1.5f)
                    framing.Cylinder(new Vector3(plane, spring + Mathf.Sqrt(Mathf.Max(0, opening * opening - z * z)), z), new Vector3(plane, top, z), .024f, .024f, Brass, 8);
            }
            panels.Emit("04 • Curved aquarium glass / twin tanks and portal walls", root, glass, false);
            tunnel.Emit("20 • Acrylic tunnel / bell mouths and arch", root, acrylic, false);
            framing.Emit("21 • Continuous glass plinths and bronze mullions", root, stone);
            rails.Emit("21c • Tunnel springline rails", root, stone);
            TwinTunnelFrames(root, stone, light);

            // Deck edges round both tanks: fascia, coping, glow, and glass balustrades except at the dive opening.
            var deckLoops = field.Contours(field.Open);
            if (deckLoops.Count != 2) throw new InvalidOperationException("Expected one deck edge round each tank, found " + deckLoops.Count + ".");
            var balustrade = new Geo();
            foreach (var edge in deckLoops)
            {
                BoundaryBand(fascia, edge, Elevated(6.96f), Elevated(7.12f), .045f, Navy);
                BoundaryBand(fascia, edge, Elevated(7.15f), Elevated(7.275f), .045f, Navy);
                BoundaryBand(trim, edge, Elevated(7.26f), Elevated(7.29f), .11f, C(0xd3bd91));
                BoundaryBand(glow, edge, Elevated(7.12f), Elevated(7.15f), .045f, C(0x77e2db));
                postTravel = 0;
                for (int i = 0; i < edge.Count; i++)
                {
                    var a = edge[i]; var b = edge[(i + 1) % edge.Count]; var mid = (a + b) * .5f;
                    var local = AquariumEntryLayout.Local(new Point(mid.x, mid.z));
                    bool entry = Mathf.Abs(AquariumEntryLayout.DeckDistance(new Point(mid.x, mid.z))) < .15f && local.z > -.2f;
                    bool entryOpening = entry && Mathf.Abs(local.x) < 1.3f && local.z > 3.05f;
                    if (entryOpening) { postTravel = 1.5f; continue; }
                    balustrade.Quad(Height(a, Elevated(7.36f)), Height(b, Elevated(7.36f)), Height(b, Elevated(8.12f)), Height(a, Elevated(8.12f)), Color.white);
                    trim.Cylinder(Height(a, Elevated(8.15f)), Height(b, Elevated(8.15f)), .024f, .024f, Brass, 8);
                    postTravel += Vector3.Distance(a, b);
                    if (postTravel >= 1.45f) { trim.Cylinder(Height(a, Elevated(7.28f)), Height(a, Elevated(8.15f)), .025f, .025f, Navy, 8); postTravel = 0; }
                }
            }
            fascia.Emit("06d • Joined deck edge fascia", root, stone);
            trim.Emit("07 • Continuous rounded coping and handrails", root, stone);
            balustrade.Emit("08 • Continuous observation and entry balustrades", root, glass, false);
            glow.Emit("22 • Continuous underwater and deck lighting", root, light, false);
            BuildDiveEntry(root, stone, light, marker);
        }

        // Bore radius at x, exact on the front walls (the bell mouth's lip meets the portal opening).
        private static float TunnelRadius(float x)
        {
            if (x <= AquariumTwinMap.PlaneA + .0001f || x >= AquariumTwinMap.PlaneB - .0001f) return AquariumTwinMap.TunnelHalfWidth + AquariumTwinMap.MouthFlare;
            return AquariumTwinMap.Radius(x);
        }

        private static void PartialBand(Geo g, List<Vector3> loop, Func<Vector3, bool> include, float low, float high, float width, Color tint)
        {
            for (int i = 0; i < loop.Count; i++)
            {
                int j = (i + 1) % loop.Count; var a = loop[i]; var b = loop[j];
                if (!include((a + b) * .5f)) continue;
                var u = Vector3.Cross((loop[j] - loop[(i + loop.Count - 1) % loop.Count]).normalized, Vector3.up) * (width * .5f);
                var v = Vector3.Cross((loop[(j + 1) % loop.Count] - loop[i]).normalized, Vector3.up) * (width * .5f);
                g.Quad(Height(a - u, high), Height(a + u, high), Height(b + v, high), Height(b - v, high), tint);
                g.Quad(Height(a - u, low), Height(a - u, high), Height(b - v, high), Height(b - v, low), tint * .85f);
                g.Quad(Height(a + u, high), Height(a + u, low), Height(b + v, low), Height(b + v, high), tint * .85f);
                g.Quad(Height(a - u, low), Height(b - v, low), Height(b + v, low), Height(a + u, low), tint * .74f);
            }
        }

        // A box chain along a cross-section path (x fixed) of the tunnel: ribs, portal frames, light bands.
        private static void ProfileChain(Geo g, float x, float radius, float bottom, float along, float depth, Color tint, int steps)
        {
            float spring = AquariumTwinMap.Springline;
            var path = new List<Vector3>();
            path.Add(new Vector3(x, bottom, -radius));
            for (int k = 0; k <= steps; k++) { float phi = Mathf.PI - Mathf.PI * k / steps; path.Add(new Vector3(x, spring + radius * Mathf.Sin(phi), radius * Mathf.Cos(phi))); }
            path.Add(new Vector3(x, bottom, radius));
            for (int i = 0; i < path.Count - 1; i++)
            {
                var a = path[i]; var b = path[i + 1]; var centre = (a + b) * .5f; var tangent = b - a; float length = tangent.magnitude;
                if (length < .0001f) continue;
                var outward = new Vector3(0, centre.y - spring, centre.z); if (centre.y <= spring + .001f) outward = new Vector3(0, 0, Mathf.Sign(centre.z));
                outward.Normalize();
                // Local x: along the tunnel; local y: radially out; the chain runs across in the plane x = const.
                var box = Quaternion.LookRotation(tangent / length, outward);
                g.Box(centre + outward * depth * .5f, new Vector3(along, depth, length + .03f), tint, box);
            }
        }

        private static void TwinTunnelFrames(Transform root, Material stone, Material light)
        {
            var ribs = new Geo(); var strips = new Geo();
            float width = AquariumTwinMap.TunnelHalfWidth;
            // Ribs every 2.2 m along the straight bore, just outside the acrylic, with a lit inner lip every other rib.
            int n = 0;
            for (float x = AquariumTwinMap.PlaneA + 4.4f; x <= AquariumTwinMap.PlaneB - 4.3f; x += 2.2f, n++)
            {
                float r = TunnelRadius(x);
                ProfileChain(ribs, x, r + .02f, .14f, .2f, .16f, Navy, 40);
                ProfileChain(ribs, x, r + .012f, .2f, .26f, .02f, Brass, 40);
                if (n % 2 == 0) ProfileChain(strips, x + .15f, r + .03f, .45f, .05f, .02f, C(0xa6f0ff), 40);
            }
            // Portal frames on the hall side of each tank wall, round the bell mouth's lip.
            foreach (float plane in new[] { AquariumTwinMap.PlaneA, AquariumTwinMap.PlaneB })
            {
                float inward = plane < 0 ? 1 : -1, opening = width + AquariumTwinMap.MouthFlare;
                ProfileChain(ribs, plane + inward * .22f, opening + .02f, .05f, .44f, .42f, Navy, 56);
                ProfileChain(ribs, plane + inward * .46f, opening + .05f, .05f, .05f, .38f, Brass, 56);
                ProfileChain(strips, plane + inward * .02f, opening + .01f, .3f, .04f, .03f, C(0x8fe7f2), 56);
                // A slim flush brass lip on the water side.
                ProfileChain(ribs, plane - inward * .015f, opening + .005f, .25f, .03f, .06f, Brass, 56);
            }
            ribs.Emit("21b • Tunnel ribs and portal frames", root, stone);
            strips.Emit("22b • Tunnel rib light lines", root, light, false);
        }

        // ------------------------------------------------------------------ the building's outer wall top
        private sealed class OffsetLoop
        {
            public readonly Point[] basePoints; private readonly Dictionary<float, Vector3[]> cache = new Dictionary<float, Vector3[]>();
            public OffsetLoop(float level, float spacing) { basePoints = AquariumTwinMap.Outline(level, spacing); }
            public int Count { get { return basePoints.Length; } }
            public Vector3[] At(float level)
            {
                Vector3[] result; if (cache.TryGetValue(level, out result)) return result;
                result = new Vector3[basePoints.Length];
                for (int i = 0; i < result.Length; i++) result[i] = V(AquariumTwinMap.Project(basePoints[i], level), 0);
                cache[level] = result; return result;
            }
            public Vector3 At(float level, int i, float y) { var p = At(level)[(i % Count + Count) % Count]; p.y = y; return p; }
        }
        // A wall band between two level lines (distances out from the water), like the Coral Gallery's Band.
        private static void LevelBand(Geo g, OffsetLoop ring, int i, float inner, float outer, float low, float high, Color c)
        {
            int j = i + 1;
            var ai = ring.At(-inner, i, high); var bi = ring.At(-inner, j, high); var ao = ring.At(-outer, i, high); var bo = ring.At(-outer, j, high);
            g.Quad(ai, ao, bo, bi, c);
            g.Quad(ring.At(-inner, i, low), ring.At(-inner, j, low), ring.At(-outer, j, low), ring.At(-outer, i, low), c * .74f);
            g.Quad(ai, ring.At(-inner, i, low), ring.At(-inner, j, low), bi, c * .84f);
            g.Quad(ao, bo, ring.At(-outer, j, low), ring.At(-outer, i, low), c * .8f);
        }
        private static OffsetLoop twinRing;
        private static OffsetLoop TwinRing { get { if (twinRing == null || twinRing.Count == 0) twinRing = new OffsetLoop(-AquariumTwinMap.RearWallOffset, .42f); return twinRing; } }
        private static float Off(float coral) { return coral / 2.43f * AquariumTwinMap.OuterOffset; }

        private static void TwinRim(Transform root, Material architecture, Material trim)
        {
            twinRing = null; var ring = TwinRing;
            var rim = new Geo(); var lights = new Geo(); float deckY = AquariumModel.WaterHeight + .24f;
            for (int i = 0; i < ring.Count; i++)
            {
                LevelBand(rim, ring, i, Off(2.16f), Off(2.43f), deckY, Elevated(8.0f), Navy);
                LevelBand(rim, ring, i, Off(2.13f), Off(2.46f), Elevated(7.99f), Elevated(8.06f), C(0x738b9c));
                LevelBand(lights, ring, i, Off(2.12f), Off(2.14f), Elevated(7.39f), Elevated(7.45f), C(0xffc77f));
            }
            rim.Emit("02 • Curved limestone gallery & bronze detailing", root, architecture);
            lights.Emit("03 • Continuous warm and turquoise light ribbons", root, trim, false);
        }

        // ------------------------------------------------------------------ visitor gallery and tunnel hall
        private static void TwinInterior(Transform root, Material wall, Material fixture, Material glow, AquariumEnvironmentRoot marker)
        {
            var ring = TwinRing;
            var shell = new Geo(); var panels = new Geo(); var fittings = new Geo(); var lights = new Geo(); var exhibits = new Geo();
            var print = new Material(Shader.Find("Aquarium/Exhibit Backlit Print")) { name = "Marine atlas / backlit interpretation" };
            print.mainTexture = Resources.Load<Texture2D>("Aquarium/Textures/MarineExhibitAtlas");
            for (int i = 0; i < ring.Count; i++)
            {
                LevelBand(shell, ring, i, Off(2.12f), Off(2.43f), .10f, Elevated(6.96f), C(0x686f72));
                LevelBand(panels, ring, i, Off(2.105f), Off(2.12f), .25f, 1.07f, C(0x414f51));
                LevelBand(panels, ring, i, Off(2.101f), Off(2.122f), 1.07f, 1.105f, Brass);
                LevelBand(panels, ring, i, Off(2.104f), Off(2.125f), 5.55f, Elevated(6.93f), C(0x26333d));
                LevelBand(lights, ring, i, Off(1.99f), Off(2.03f), 5.50f, 5.55f, C(0xffc782));
                LevelBand(lights, ring, i, Off(2.075f), Off(2.09f), .32f, .355f, C(0xffc68a));
            }
            string[] titles = { "살아 있는 산호초", "먼바다", "어둠 속의 바다", "층층이 사는 생물" };
            string[] captions = { "산호 군락  01", "먼바다 여행자  02", "빛을 내는 생물  03", "바다 무척추동물  04" };
            var rear = ring.At(-AquariumTwinMap.RearWallOffset); float perimeter = 0; for (int i = 0; i < ring.Count; i++) perimeter += Vector3.Distance(rear[i], rear[(i + 1) % ring.Count]);
            int bays = Mathf.RoundToInt(perimeter / 7.4f);
            for (int bay = 0; bay < bays; bay++)
            {
                int index = Mathf.RoundToInt((bay + .35f) * ring.Count / bays) % ring.Count;
                var back = ring.At(-Off(2.02f), index, 0); var front = ring.At(0, index, 0);
                var inward = (front - back); inward.y = 0; float depth = inward.magnitude; inward /= Mathf.Max(depth, .001f);
                var rotation = Quaternion.LookRotation(inward); var right = rotation * Vector3.right;
                var tangent = (ring.At(-Off(2.02f), index + 1, 0) - ring.At(-Off(2.02f), index - 1, 0)).normalized;
                var wallRotation = Quaternion.LookRotation(Vector3.Cross(tangent, Vector3.up));
                if (Vector3.Dot(wallRotation * Vector3.forward, inward) < 0) wallRotation = wallRotation * Quaternion.Euler(0, 180, 0);
                fittings.Box(back + Vector3.up * 6.08f, new Vector3(.18f, 11.7f, .22f), C(0x887a61), wallRotation);
                int panelIndex = index + Mathf.Max(1, Mathf.RoundToInt(ring.Count / (float)bays * .45f));
                var wallPoint = ring.At(-Off(2.12f), panelIndex, 0);
                var panelTangent = (ring.At(-Off(2.12f), panelIndex + 1, 0) - ring.At(-Off(2.12f), panelIndex - 1, 0)).normalized;
                var panelNormal = Vector3.Cross(panelTangent, Vector3.up).normalized;
                if (Vector3.Dot(panelNormal, ring.At(0, panelIndex, 0) - wallPoint) < 0) panelNormal = -panelNormal;
                var bayCentre = wallPoint + panelNormal * .42f;
                var turn = Quaternion.LookRotation(panelNormal);
                if (bay % 6 == 0)
                {
                    fittings.Box(bayCentre + Vector3.up * 1.62f, new Vector3(1.55f, 2.75f, .16f), C(0x243b43), turn);
                    fittings.Box(bayCentre + turn * new Vector3(0, 2.96f, .10f), new Vector3(1.64f, .09f, .12f), Brass, turn);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        fittings.Box(bayCentre + turn * new Vector3(side * .77f, 1.62f, .1f), new Vector3(.07f, 2.75f, .12f), Brass, turn);
                        fittings.Cylinder(bayCentre + turn * new Vector3(side * .12f, 1.35f, .16f), bayCentre + turn * new Vector3(side * .12f, 1.72f, .16f), .025f, .025f, Brass, 8);
                    }
                    lights.Box(bayCentre + turn * new Vector3(0, 3.16f, .13f), new Vector3(.8f, .19f, .025f), C(0xb4d7c3), turn);
                    Label(root, "전시실  " + (bay / 6 + 1).ToString("00"), bayCentre + turn * new Vector3(0, 3.53f, .14f), turn * Quaternion.Euler(0, 180, 0), .024f, C(0xeadabb));
                }
                else
                {
                    var display = bayCentre + Vector3.up * 2.25f;
                    fittings.Box(display, new Vector3(2.05f, 1.52f, .16f), C(0x364e55), turn);
                    int plate = bay % 4;
                    DisplayPanel(exhibits, display + turn * new Vector3(0, 0, .11f), turn, plate);
                    fittings.Box(display + turn * new Vector3(0, .93f, 0), new Vector3(2.05f, .30f, .15f), C(0x142630), turn);
                    Label(root, titles[plate], display + turn * new Vector3(0, .94f, .09f), turn * Quaternion.Euler(0, 180, 0), .027f, C(0xcbd1bb));
                    Label(root, captions[plate], display + turn * new Vector3(0, -.88f, .11f), turn * Quaternion.Euler(0, 180, 0), .013f, C(0x7f9fa5));
                    for (int e = -1; e <= 1; e += 2)
                    {
                        fittings.Box(display + turn * new Vector3(e * 1.015f, 0, .12f), new Vector3(.025f, 1.5f, .035f), Brass, turn);
                        fittings.Box(display + turn * new Vector3(0, e * .75f, .12f), new Vector3(2.05f, .025f, .035f), Brass, turn);
                        for (int h = -1; h <= 1; h += 2) fittings.Ellipsoid(display + turn * new Vector3(e * .97f, h * .70f, .14f), Vector3.one * .013f, Brass, 8, 5, 0);
                    }
                    lights.Box(display + turn * new Vector3(0, .79f, .16f), new Vector3(1.85f, .018f, .04f), C(0xccb17b), turn);
                    if (bay % 5 == 2)
                    {
                        var kiosk = bayCentre + turn * new Vector3(1.45f, .92f, 1.1f);
                        fittings.Box(kiosk, new Vector3(.42f, 1.38f, .40f), C(0x253944), turn);
                        fittings.Box(kiosk + Vector3.up * .73f, new Vector3(.72f, .08f, .57f), C(0x121e26), turn * Quaternion.Euler(-18, 0, 0));
                        Label(root, "바다 도감", kiosk + Vector3.up * .84f + turn * Vector3.forward * .16f, turn * Quaternion.Euler(58, 180, 0), .012f, C(0xaac6bc));
                    }
                    if (depth > 2.6f)
                    {
                        var seat = bayCentre + turn * new Vector3(0, .69f, .63f);
                        for (int s = 0; s < 4; s++) fittings.Box(seat + turn * new Vector3(0, 0, (s - 1.5f) * .12f), new Vector3(1.95f, .085f, .105f), C(0xab7849), turn);
                        foreach (float x in new[] { -.74f, .74f }) fittings.Box(seat + turn * new Vector3(x, -.22f, 0), new Vector3(.075f, .44f, .47f), Navy, turn);
                    }
                }
                // Pendant tracks and acoustic slats show a real ceiling through the glass (clear of the tunnel arch).
                var lamp = Vector3.Lerp(front, back, .48f) + Vector3.up * 4.8f;
                bool overArch = Mathf.Abs(lamp.z) < AquariumTwinMap.TunnelHalfWidth + .9f && AquariumTwinMap.InTunnel(lamp.x);
                if (!overArch)
                {
                    fittings.Box(lamp, new Vector3(2.25f, .14f, .18f), C(0x243c46), wallRotation);
                    lights.Box(lamp - Vector3.up * .078f, new Vector3(2.10f, .018f, .12f), C(0xffdb9e), wallRotation);
                    foreach (float side in new[] { -.8f, .8f }) fittings.Cylinder(lamp + right * side, lamp + right * side + Vector3.up * 7.06f, .011f, .011f, Navy, 6);
                }
                for (int s = 0; s < 5; s++)
                {
                    var slat = ring.At(-Off(1.05f), index + (s - 2) * 2, Elevated(6.73f));
                    fittings.Box(slat, new Vector3(.09f, .21f, Mathf.Max(.5f, depth * .72f)), C(0x8a6c4e), rotation);
                }
                marker.visitorGalleryBays++;
            }
            TwinTunnelHall(root, fittings, lights);
            shell.Emit("26 • Enclosed visitor gallery / continuous curved rear walls", root, wall);
            panels.Emit("27 • Visitor gallery stone wainscot and dark upper frieze", root, wall);
            fittings.Emit("28 • Interior doors, benches, ceiling baffles and bronze framing", root, fixture);
            lights.Emit("29 • Interior warm pendants and continuous cove lighting", root, glow, false);
            exhibits.Emit("30 • Aquarium interpretation panels / marine motifs", root, print, false);
        }

        // The hall either side of the tunnel: floor guide lights along the acrylic and a hanging sign before each portal.
        private static void TwinTunnelHall(Transform root, Geo fittings, Geo lights)
        {
            float wall = AquariumTwinMap.TunnelHalfWidth;
            for (int side = -1; side <= 1; side += 2)
                for (float x = AquariumTwinMap.PlaneA + 3.3f; x <= AquariumTwinMap.PlaneB - 3.2f; x += 1.1f)
                {
                    fittings.Box(new Vector3(x, .245f, side * (wall + .55f)), new Vector3(.36f, .03f, .1f), C(0x1d2c33), Quaternion.identity);
                    lights.Box(new Vector3(x, .262f, side * (wall + .55f)), new Vector3(.3f, .012f, .05f), C(0x8fe7f2), Quaternion.identity);
                }
            foreach (float plane in new[] { AquariumTwinMap.PlaneA, AquariumTwinMap.PlaneB })
            {
                float inward = plane < 0 ? 1 : -1; var face = Quaternion.LookRotation(new Vector3(inward, 0, 0));
                var at = new Vector3(plane + inward * .66f, 11.33f, 0);
                fittings.Box(at, new Vector3(4.2f, .56f, .1f), Navy, face);
                fittings.Box(at + face * new Vector3(0, -.3f, .01f), new Vector3(4.3f, .04f, .12f), Brass, face);
                lights.Box(at + face * new Vector3(0, -.25f, .06f), new Vector3(3.9f, .018f, .03f), C(0xffd39a), face);
                foreach (float rod in new[] { -1.7f, 1.7f }) fittings.Cylinder(at + face * new Vector3(rod, .28f, 0), at + face * new Vector3(rod, Elevated(6.96f) - 11.33f, 0), .012f, .012f, Navy, 6);
                Label(root, "수중 터널", at + face * new Vector3(0, .06f, .06f), face * Quaternion.Euler(0, 180, 0), .036f, C(0xe6d2a2));
                Label(root, plane < 0 ? "A 수조  ↔  B 수조" : "B 수조  ↔  A 수조", at + face * new Vector3(0, -.16f, .06f), face * Quaternion.Euler(0, 180, 0), .014f, C(0x7fc5cd));
            }
        }

        // ------------------------------------------------------------------ deck furniture
        private static void TwinFurniture(Transform root, Material architecture, Material garden, Material glow, System.Random r)
        {
            var fixtures = new Geo(); var plants = new Geo(); var warm = new Geo();
            var planters = new OffsetLoop(-Off(1.25f), .5f); var benches = new OffsetLoop(-Off(1.15f), .5f);
            int count = Mathf.RoundToInt(planters.Count * .5f / 11f);
            for (int i = 0; i < count; i++)
            {
                var p = planters.At(-Off(1.25f), Mathf.RoundToInt((i + .4f) * planters.Count / count), DeckYTop());
                if (AquariumEntryLayout.KeepPromenadeClear(new Point(p.x, p.z), .7f)) continue;
                fixtures.Cylinder(p, p + Vector3.up * .32f, .49f, .59f, Navy, 18);
                fixtures.Cylinder(p + Vector3.up * .32f, p + Vector3.up * .35f, .6f, .6f, Brass, 18);
                fixtures.Ellipsoid(p + Vector3.up * .35f, new Vector3(.52f, .07f, .52f), C(0x344e32), 12, 5, 0);
                for (int leaf = 0; leaf < 16; leaf++)
                {
                    float direction = leaf * 2.4f + i; Vector3 start = p + Vector3.up * .36f; float length = Range(r, .65f, 1.15f);
                    Vector3 end = start + new Vector3(Mathf.Cos(direction) * length * .7f, Range(r, .4f, 1.1f), Mathf.Sin(direction) * length * .7f);
                    Leaf(plants, start, end, Range(r, .12f, .25f), Color.Lerp(C(0x346e3f), C(0xa4ba46), Range(r, 0, 1)), direction, .65f);
                }
            }
            for (int i = 0; i < count; i++)
            {
                int index = Mathf.RoundToInt((i + .9f) * benches.Count / count);
                var p = benches.At(-Off(1.15f), index, DeckYTop());
                if (AquariumEntryLayout.KeepPromenadeClear(new Point(p.x, p.z), .9f)) continue;
                var tangent = (benches.At(-Off(1.15f), index + 1, 0) - benches.At(-Off(1.15f), index - 1, 0)).normalized;
                var rot = Quaternion.LookRotation(new Vector3(-tangent.z, 0, tangent.x));
                for (int slat = 0; slat < 4; slat++) fixtures.Box(p + Vector3.up * .37f + rot * new Vector3(0, 0, (slat - 1.5f) * .12f), new Vector3(1.3f, .07f, .09f), C(0xac784b), rot);
                for (int leg = -1; leg <= 1; leg += 2)
                {
                    fixtures.Box(p + rot * new Vector3(leg * .48f, .18f, 0), new Vector3(.065f, .36f, .45f), Navy, rot);
                    fixtures.Box(p + rot * new Vector3(leg * .55f, .58f, .25f), new Vector3(.05f, .54f, .05f), Navy, rot);
                }
                fixtures.Box(p + rot * new Vector3(0, .72f, .25f), new Vector3(1.3f, .13f, .06f), C(0xba8857), rot);
                // Warm bollards by the water's edge (not along the covered tunnel, where the deck runs on across).
                var edge = V(AquariumTwinMap.Project(new Point(p.x, p.z), -Off(.31f)), DeckYTop() - .02f);
                if (AquariumTwinMap.TankField(edge.x, edge.z) > -1.2f)
                {
                    fixtures.Box(edge + Vector3.up * .28f, new Vector3(.12f, .56f, .12f), Navy, Quaternion.identity);
                    warm.Box(edge + Vector3.up * .55f, new Vector3(.15f, .06f, .15f), C(0xffdc97), Quaternion.identity);
                }
            }
            // A sign wall on the south parapet, facing the deck.
            var south = new OffsetLoop(-Off(2.2f), .5f); int best = 0;
            for (int i = 0; i < south.Count; i++) { var q = south.basePoints[i]; if (Mathf.Abs(q.x) < 1.5f && q.z < south.basePoints[best].z) best = i; }
            var sign = south.At(-Off(2.2f), best, Elevated(6.57f));
            fixtures.Box(sign, new Vector3(4.6f, .72f, .12f), Navy, Quaternion.identity);
            fixtures.Emit("09 • Oak benches, bronze pots & bollards", root, architecture);
            plants.Emit("10 • Gallery botanical planting", root, garden);
            warm.Emit("11 • Warm gallery lanterns", root, glow, false);
            Label(root, "쌍둥이 수조", sign + new Vector3(0, .11f, .07f), Quaternion.Euler(0, 180, 0), .039f, C(0xe6d2a2));
            Label(root, "수   족   관", sign + new Vector3(0, -.18f, .08f), Quaternion.Euler(0, 180, 0), .023f, C(0x7fc5cd));
        }
        private static float DeckYTop() { return AquariumModel.WaterHeight + .24f + .02f; }

        // ------------------------------------------------------------------ bubbles and sun shafts
        private static void TwinAtmosphere(Transform root, Material bubbleMat, Material rayMat, System.Random r, AquariumEnvironmentRoot marker)
        {
            var bubbles = new Geo(); var beams = new Geo();
            var vents = new List<Vector3>();
            foreach (var v in new[] { new Point(-35, 6), new Point(-22, -7), new Point(-30, 10), new Point(22, 7), new Point(35, -5), new Point(31, 12) })
                if (AquariumMapOutline.Contains(v, 2) && !AquariumEntryLayout.KeepWaterClear(v, 1)) vents.Add(V(v, 0));
            foreach (var vent in vents) for (int i = 0; i < 28; i++)
            {
                float size = Range(r, .022f, .075f);
                Vector3 p = vent + new Vector3(Range(r, -.23f, .23f), 0, Range(r, -.23f, .23f)); p.y = AquariumSeabed.Height(p.x, p.z) + .06f;
                bubbles.Ellipsoid(p, Vector3.one * size, Color.white, 8, 6, 0, new Vector2(Range(r, 0, 1), Range(r, .065f, .13f)));
                marker.bubbleCount++;
            }
            var bubbleObject = bubbles.Emit("18 • Rising aeration bubbles / GPU animated", root, bubbleMat, false);
            bubbleObject.GetComponent<MeshFilter>().sharedMesh.bounds = new Bounds(Vector3.up * 6, new Vector3(2 * AquariumTwinMap.HalfWidth, AquariumModel.WaterHeight + 1, 2 * AquariumTwinMap.HalfDepth));
            int placed = 0;
            for (int i = 0; i < 160 && placed < 34; i++)
            {
                bool west = i % 2 == 0; var centre = west ? AquariumTwinMap.CentreA : AquariumTwinMap.CentreB;
                var location = new Point(centre.x + Range(r, -15, 15), centre.z + Range(r, -15, 15));
                var bottomPoint = new Point(location.x - 3.7f, location.z - 2.8f);
                if (AquariumTwinMap.TankField(location.x, location.z) < 1.4f || AquariumTwinMap.TankField(bottomPoint.x, bottomPoint.z) < 1.2f) continue;
                if (AquariumEntryLayout.DeckDistance(location) < .8f) continue;
                Vector3 top = new Vector3(location.x, AquariumModel.WaterHeight, location.z);
                Vector3 bottom = top + new Vector3(-3.7f, -AquariumModel.WaterHeight + .6f, -2.8f); float width = Range(r, .32f, .65f);
                float phase = Range(r, 0, Mathf.PI * 2);
                for (int plane = 0; plane < 2; plane++)
                {
                    Vector3 side = plane == 0 ? Vector3.right : Vector3.forward;
                    int n = beams.Count; const int rows = 20, columns = 4;
                    for (int row = 0; row <= rows; row++) for (int column = 0; column <= columns; column++)
                    {
                        float depth = row / (float)rows, u = column / (float)columns;
                        var c = Vector3.Lerp(top, bottom, depth); float spread = Mathf.Lerp(1, 2.8f, depth);
                        beams.V(c + side * (u * 2 - 1) * width * spread, Color.white, new Vector2(u, 1 - depth), new Vector2(phase, 0));
                        if (row < rows && column < columns) { int v = n + row * (columns + 1) + column; beams.Q(v, v + 1, v + columns + 2, v + columns + 1); }
                    }
                }
                placed++;
            }
            var shafts = beams.Emit("19 • Rippling refracted underwater sun shafts", root, rayMat, false);
            var shaftMesh = shafts.GetComponent<MeshFilter>().sharedMesh; var bounds = shaftMesh.bounds; bounds.Expand(2); shaftMesh.bounds = bounds;
        }

        // The twin plan sampled like the Coral Gallery's DeckField: water, open tank surfaces, deck and dry floor.
        private sealed class TwinField
        {
            private const float Step = .18f;
            private readonly Vector3[] points;
            private readonly int width, height;
            public readonly float[] Water, Tanks, Open, Deck, InteriorFloor;
            public TwinField()
            {
                float minX = -57.6f, maxX = 57.6f, minZ = -30.6f, maxZ = 30.6f;
                width = Mathf.CeilToInt((maxX - minX) / Step) + 2; height = Mathf.CeilToInt((maxZ - minZ) / Step) + 2;
                int size = width * height; points = new Vector3[size];
                Water = new float[size]; Tanks = new float[size]; Open = new float[size]; Deck = new float[size]; InteriorFloor = new float[size];
                for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
                {
                    int i = z * width + x; var p = new Point(minX + x * Step, minZ + z * Step); points[i] = new Vector3(p.x, 0, p.z);
                    float wet = AquariumTwinMap.Water(p.x, p.z), surface = Mathf.Min(wet, AquariumTwinMap.TankField(p.x, p.z));
                    float open = Mathf.Min(surface, AquariumEntryLayout.DeckDistance(p)), inside = wet + AquariumTwinMap.OuterOffset;
                    Water[i] = Nudge(wet); Tanks[i] = Nudge(surface); Open[i] = Nudge(open); Deck[i] = Nudge(Mathf.Min(inside, -open)); InteriorFloor[i] = Nudge(Mathf.Min(inside, -wet));
                }
            }
            private static float Nudge(float d) { return Mathf.Abs(d) < .00001f ? .00001f : d; }
            public void Surface(Geo output, float[] values, float y, Color tint, bool reverse = false)
            {
                for (int z = 0; z < height - 1; z++) for (int x = 0; x < width - 1; x++)
                {
                    int a = z * width + x, b = a + 1, c = a + width, d = c + 1;
                    Clip(output, values, a, c, d, y, tint, reverse); Clip(output, values, a, d, b, y, tint, reverse);
                }
            }
            private void Clip(Geo g, float[] f, int a, int b, int c, float y, Color tint, bool reverse)
            {
                if (f[a] < 0 && f[b] < 0 && f[c] < 0) return;
                if (f[a] > 0 && f[b] > 0 && f[c] > 0) { Face(g, points[a], points[b], points[c], y, tint, reverse); return; }
                int[] indices = { a, b, c }; var poly = new List<Vector3>(4);
                for (int i = 0; i < 3; i++)
                {
                    int j = indices[i], k = indices[(i + 1) % 3];
                    if (f[j] > 0) poly.Add(points[j]);
                    if ((f[j] > 0) != (f[k] > 0)) poly.Add(Crossing(j, k, f));
                }
                for (int i = 1; i < poly.Count - 1; i++) Face(g, poly[0], poly[i], poly[i + 1], y, tint, reverse);
            }
            private static void Face(Geo g, Vector3 a, Vector3 b, Vector3 c, float y, Color tint, bool reverse)
            { if (Vector3.Cross(b - a, c - a).sqrMagnitude < .0000000001f) return; g.Triangle(Height(a, y), Height(reverse ? c : b, y), Height(reverse ? b : c, y), tint); }
            private Vector3 Crossing(int a, int b, float[] f)
            { if (a > b) { int swap = a; a = b; b = swap; } return Vector3.LerpUnclamped(points[a], points[b], f[a] / (f[a] - f[b])); }
            private static long EdgeKey(int a, int b) { return ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b); }
            public List<List<Vector3>> Contours(float[] f)
            {
                var nodes = new Dictionary<long, Vector3>(); var neighbours = new Dictionary<long, List<long>>();
                for (int z = 0; z < height - 1; z++) for (int x = 0; x < width - 1; x++)
                { int a = z * width + x, b = a + 1, c = a + width, d = c + 1; ContourTriangle(a, c, d, f, nodes, neighbours); ContourTriangle(a, d, b, f, nodes, neighbours); }
                var loops = new List<List<Vector3>>(); var visited = new HashSet<long>();
                foreach (var start in nodes.Keys)
                {
                    if (visited.Contains(start)) continue; var loop = new List<Vector3>(); long previous = -1, current = start;
                    do
                    {
                        if (!visited.Add(current)) throw new InvalidOperationException("Twin deck boundary is not a simple closed loop.");
                        loop.Add(nodes[current]); var adjacent = neighbours[current];
                        if (adjacent.Count != 2) throw new InvalidOperationException("Open twin deck boundary.");
                        long next = adjacent[0] == previous ? adjacent[1] : adjacent[0]; previous = current; current = next;
                    } while (current != start);
                    loops.Add(Resample(loop, .22f));
                }
                return loops;
            }
            private void ContourTriangle(int a, int b, int c, float[] f, Dictionary<long, Vector3> nodes, Dictionary<long, List<long>> neighbours)
            {
                if ((f[a] > 0) == (f[b] > 0) && (f[b] > 0) == (f[c] > 0)) return;
                int[] ids = { a, b, c }; long first = -1, second = -1;
                for (int i = 0; i < 3; i++)
                {
                    int j = ids[i], k = ids[(i + 1) % 3]; if ((f[j] > 0) == (f[k] > 0)) continue;
                    long key = EdgeKey(j, k); if (!nodes.ContainsKey(key)) { nodes.Add(key, Crossing(j, k, f)); neighbours.Add(key, new List<long>(2)); }
                    if (first < 0) first = key; else second = key;
                }
                neighbours[first].Add(second); neighbours[second].Add(first);
            }
            private static List<Vector3> Resample(List<Vector3> source, float spacing)
            {
                float total = 0; for (int i = 0; i < source.Count; i++) total += Vector3.Distance(source[i], source[(i + 1) % source.Count]);
                int count = Mathf.CeilToInt(total / spacing); float step = total / count, accumulated = 0, target = 0;
                var result = new List<Vector3>(count);
                for (int i = 0; i < source.Count && result.Count < count; i++)
                {
                    var a = source[i]; var b = source[(i + 1) % source.Count]; float length = Vector3.Distance(a, b);
                    while (target <= accumulated + length && result.Count < count) { result.Add(Vector3.LerpUnclamped(a, b, (target - accumulated) / Mathf.Max(length, .000001f))); target += step; }
                    accumulated += length;
                }
                return result;
            }
        }
    }
}

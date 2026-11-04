using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R73: "보랏빛 터널 수조" (AquariumMaps.Grotto), built from the user's sketch: one big box tank with rough stone walls
    // and a stone ceiling, and in its middle a U-shaped acrylic visitor tube lying on its side, standing on legs
    // (AquariumTubeMap). Its arms run out of the west wall; visitors walk inside it while the game is played round it.
    // The dive hatch is a gap in the ceiling over the north wall in the east, with the usual dive deck under it.
    // The light is dim violet (the camera's grade and fog, AquariumGalleryCamera) with glowing crystals and lamps.
    // Object names start with T (the scenery presentation reads numbered names as reef parts).
    public static partial class AquariumEnvironment
    {
        public const float TubeCeiling = 18f;
        private const float TubeWallBottom = -.9f;

        private static void GrottoMaterials(Material sand, Material architecture, Material interior, Material fixtures, Material floor,
            Material paving, Material glass, Material acrylic, Material rays, Material water)
        {
            sand.SetColor("_Color", C(0xb3a2c4));
            architecture.SetColor("_Color", C(0x8d82a3));
            paving.SetColor("_Color", C(0x8e86a6));
            interior.SetColor("_Color", C(0x9a86b8)); fixtures.SetColor("_Color", C(0x9a86b8)); floor.SetColor("_Color", C(0x9286a8));
            var g = C(0xa58dff); g.a = .24f; glass.SetColor("_Color", g);
            var a = C(0xb6a4ff); a.a = .3f; acrylic.SetColor("_Color", a);
            var ray = C(0xd9c2ff); ray.a = .14f; rays.SetColor("_Color", ray);
            water.SetColor("_Color", new Color(.26f, .16f, .52f, .085f));
            sand.name += " (violet)"; architecture.name += " (violet)";
        }

        // Adds the quad facing the given way (whatever order its corners came in).
        private static void FacingQuad(Geo g, int a, int b, int c, int d, Vector3 pa, Vector3 pb, Vector3 pc, Vector3 want)
        {
            if (Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), want) >= 0) g.Q(a, b, c, d); else g.Q(a, d, c, b);
        }

        private static void BuildTubeTank(Transform root, Texture caustics, Material architecture, Material paving, Material trim, Material acrylic,
            Material water, Material bubbleMat, Material rayMat, TwinField field, AquariumEnvironmentRoot marker, System.Random r)
        {
            var stone = ScannedSurface("Tube tank • rough stone walls", "Rock", caustics, .26f, false);
            stone.SetColor("_Color", C(0x7f7196)); stone.SetFloat("_NormalStrength", 1.15f);
            var rubbleMat = Lit("Tube tank • fallen stone", caustics, .2f, 1); rubbleMat.SetColor("_Color", C(0x8a7ca0));
            var crystal = Lit("Tube tank • glowing crystals", caustics, .7f, 0); crystal.SetFloat("_Emission", 1.7f);
            var metal = Lit("Tube tank • bronze tube frame", caustics, .5f, 1); metal.SetColor("_Color", C(0x9c8fb8));
            var lampMat = Lit("Tube tank • lamps", caustics, 0, 0); lampMat.SetFloat("_Emission", 3f);
            var tubeFloor = new Material(paving) { name = "Tube tank • visitor tube floor" }; tubeFloor.SetColor("_Color", C(0x5d5274));
            var lampRays = new Material(rayMat) { name = "Tube tank • lamp shafts" }; var lt = C(0xeadcff); lt.a = .16f; lampRays.SetColor("_Color", lt);

            // ---- water surface
            foreach (var surface in field.Contours(field.Tanks))
            { var g = new Geo(); FillWaterPolygon(g, surface); g.Emit("T0 • Animated water surface", root, water, false); }

            // ---- stone walls (rough, set back a little from the water's edge), cut down to the deck at the dive entry
            var loop = AquariumTwinMap.Outline(0, .55f); int n = loop.Length;
            var walls = new Geo(); var rows = new List<float>();
            for (float y = TubeWallBottom; y < TubeCeiling + .5f; y += .45f) rows.Add(y);
            var index = new int[n, rows.Count]; var outward = new Point[n]; var entry = new bool[n];
            float along = 0;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) along += (loop[i] - loop[i - 1]).Length;
                outward[i] = AquariumTwinMap.Outward(loop[i]);
                var local = AquariumEntryLayout.Local(loop[i]); entry[i] = Mathf.Abs(local.x) < 5.4f && Mathf.Abs(local.z) < 1.5f;
                for (int k = 0; k < rows.Count; k++)
                {
                    float y = rows[k];
                    float push = .12f + .8f * Mathf.PerlinNoise(along * .16f + 3.3f, y * .2f + 7.1f) + .3f * Mathf.PerlinNoise(along * .55f, y * .6f + 1.7f);
                    if (y < .6f) push += .35f * (.6f - y);   // the foot spreads out under the sand
                    var p = loop[i] + outward[i] * push;
                    index[i, k] = walls.Count; walls.V(new Vector3(p.x, y, p.z), Color.white);
                }
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n; var inward = -V(outward[i], 0);
                for (int k = 0; k < rows.Count - 1; k++)
                {
                    if (entry[i] && entry[j] && rows[k] >= AquariumEntryLayout.DeckHeight - .1f) continue;
                    FacingQuad(walls, index[i, k], index[j, k], index[j, k + 1], index[i, k + 1],
                        WallPoint(walls, loop, outward, i, k, rows), WallPoint(walls, loop, outward, j, k, rows), WallPoint(walls, loop, outward, j, k + 1, rows), inward);
                }
            }
            walls.Emit("T1 • Rough stone walls", root, stone, false);

            // ---- the ceiling (seen from below), open over the dive hatch
            var ceiling = new Geo();
            const float step = 1f, minX = -42, maxX = 42, minZ = -24, maxZ = 24;
            int cx = Mathf.RoundToInt((maxX - minX) / step) + 1, cz = Mathf.RoundToInt((maxZ - minZ) / step) + 1;
            var cIndex = new int[cx * cz]; var keep = new bool[cx * cz];
            for (int iz = 0; iz < cz; iz++) for (int ix = 0; ix < cx; ix++)
            {
                float x = minX + ix * step, z = minZ + iz * step; int i = iz * cx + ix;
                var at = new Point(x, z); var local = AquariumEntryLayout.Local(at);
                bool overHatch = Mathf.Abs(local.x) < 3.4f && local.z > -.6f && local.z < 4.4f;
                keep[i] = !overHatch && (AquariumTwinMap.Water(x, z) > -3.5f || AquariumEntryLayout.KeepPromenadeClear(at, 1.5f));
                float y = TubeCeiling + (Mathf.PerlinNoise(x * .13f + 5.5f, z * .13f + 1.2f) - .5f) * 1.6f;
                cIndex[i] = ceiling.Count; ceiling.V(new Vector3(x, y, z), Color.white);
            }
            for (int iz = 0; iz < cz - 1; iz++) for (int ix = 0; ix < cx - 1; ix++)
            {
                int a = iz * cx + ix, b = a + 1, c = a + cx + 1, d = a + cx;
                if (!keep[a] || !keep[b] || !keep[c] || !keep[d]) continue;
                ceiling.Q(cIndex[a], cIndex[b], cIndex[c], cIndex[d]);   // faces down (as R73's first vault)
            }
            ceiling.Emit("T2 • Stone ceiling", root, stone, false);

            // ---- the dive entry: a stone alcove behind the cut wall, the deck, the hatch above with daylight
            var alcove = new Geo();
            var inwardDir = V(AquariumEntryLayout.Inward, 0); var rot = Quaternion.LookRotation(inwardDir);
            alcove.Box(EntryAt(0, -3.2f, 7.1f), new Vector3(11.4f, .35f, 5.6f), C(0x7e7393), rot);                  // floor
            alcove.Box(EntryAt(0, -6.1f, 10.2f), new Vector3(11.8f, 6.6f, .6f), C(0x6f6585), rot);                 // back wall
            foreach (int side in new[] { -1, 1 }) alcove.Box(EntryAt(side * 5.75f, -3f, 10.2f), new Vector3(.6f, 6.6f, 6.6f), C(0x6f6585), rot);
            alcove.Emit("T3 • Dive alcove", root, rubbleMat);
            BuildDiveEntry(root, architecture, trim, marker);
            var hatch = new Geo(); var sky = new Geo();
            var hatchCentre = EntryAt(0, 1.9f, 13f);
            foreach (int side in new[] { -1, 1 })
            {
                hatch.Box(EntryAt(side * 3.5f, 1.9f, 13.05f), new Vector3(.3f, .5f, 5.4f), Brass * .8f, rot);
                hatch.Box(EntryAt(0, 1.9f + side * 2.55f, 13.05f), new Vector3(7.3f, .5f, .3f), Brass * .8f, rot);
            }
            sky.Box(hatchCentre + Vector3.up * 3.2f, new Vector3(7.2f, .1f, 5.4f), C(0xf2e9ff), rot);
            for (int k = 0; k < 4; k++)
            {
                // the hatch's shaft walls up to the sky panel
                float s = k < 2 ? 1 : -1; bool across = k % 2 == 0;
                var centre = hatchCentre + Vector3.up * 1.6f + (across ? V(AquariumEntryLayout.Right, 0) * 3.5f * s : inwardDir * 2.55f * s);
                hatch.Box(centre, across ? new Vector3(.2f, 3.2f, 5.4f) : new Vector3(7.2f, 3.2f, .2f), C(0x5f5576), rot);
            }
            hatch.Emit("T4 • Dive hatch frame and shaft", root, rubbleMat);
            sky.Emit("T4b • Daylight over the hatch", root, lampMat, false);

            // ---- the visitor tube
            var path = AquariumTubeMap.Path; float R = AquariumTubeMap.Radius, axis = AquariumTubeMap.AxisY;
            var shell = new Geo(); var floor = new Geo(); var frame = new Geo(); var glow = new Geo();
            const int around = 36; float floorDrop = 1.5f, floorHalf = Mathf.Sqrt(R * R - floorDrop * floorDrop) - .05f;
            var sides = new Vector3[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                var d = path[Mathf.Min(path.Length - 1, i + 1)] - path[Mathf.Max(0, i - 1)];
                sides[i] = new Vector3(-d.z, 0, d.x).normalized;
            }
            for (int i = 0; i < path.Length; i++)
            {
                var c = new Vector3(path[i].x, axis, path[i].z);
                for (int k = 0; k <= around; k++)
                {
                    float phi = Mathf.PI * 2 * k / around;
                    shell.V(c + sides[i] * Mathf.Cos(phi) * R + Vector3.up * Mathf.Sin(phi) * R, Color.white, new Vector2(k / (float)around, i * .5f));
                    if (i > 0 && k < around) { int v = i * (around + 1) + k; shell.Q(v - around - 1, v - around, v + 1, v); }
                }
                floor.V(c + Vector3.down * floorDrop - sides[i] * floorHalf, Color.white); floor.V(c + Vector3.down * floorDrop + sides[i] * floorHalf, Color.white);
                if (i > 0) { int v = i * 2; floor.Q(v - 2, v - 1, v + 1, v); }
            }
            float travel = 0;
            for (int i = 1; i < path.Length; i++)
            {
                var a = new Vector3(path[i - 1].x, axis, path[i - 1].z); var b = new Vector3(path[i].x, axis, path[i].z);
                // glow lines along both edges of the walkway, handrails
                foreach (int s in new[] { -1, 1 })
                {
                    glow.Cylinder(a + Vector3.down * (floorDrop - .03f) + sides[i - 1] * s * (floorHalf - .06f), b + Vector3.down * (floorDrop - .03f) + sides[i] * s * (floorHalf - .06f), .025f, .025f, C(0xa98bff), 5);
                    frame.Cylinder(a + Vector3.down * (floorDrop - .95f) + sides[i - 1] * s * (floorHalf - .18f), b + Vector3.down * (floorDrop - .95f) + sides[i] * s * (floorHalf - .18f), .035f, .035f, Brass, 6);
                }
                travel += Vector3.Distance(a, b);
                if (travel < 2.5f || path[i].x < -37.5f) continue;
                travel = 0;
                // a rib round the acrylic
                for (int k = 0; k < 28; k++)
                {
                    float p0 = Mathf.PI * 2 * k / 28, p1 = Mathf.PI * 2 * (k + 1) / 28;
                    frame.Cylinder(b + sides[i] * Mathf.Cos(p0) * (R + .06f) + Vector3.up * Mathf.Sin(p0) * (R + .06f),
                        b + sides[i] * Mathf.Cos(p1) * (R + .06f) + Vector3.up * Mathf.Sin(p1) * (R + .06f), .07f, .07f, C(0x4a4262), 5);
                }
            }
            // the tube's mouths in the west wall, ringed with stone
            var rubble = new Geo();
            foreach (int end in new[] { 0, path.Length - 1 })
            {
                var c = new Vector3(-37.2f, axis, path[end].z); var side = sides[end];
                for (int k = 0; k < 16; k++)
                {
                    float phi = Mathf.PI * 2 * k / 16;
                    rubble.Ellipsoid(c + side * Mathf.Cos(phi) * (R + .7f) + Vector3.up * Mathf.Sin(phi) * (R + .7f) + Vector3.left * .3f,
                        new Vector3(Range(r, .8f, 1.2f), Range(r, .7f, 1.1f), Range(r, .8f, 1.2f)), C(0x84769c), 8, 5, .25f);
                }
            }
            // legs: a post, a round foot on the sand and a saddle under the tube
            var legs = new Geo();
            foreach (var leg in AquariumTubeMap.Legs)
            {
                float sand = AquariumSeabed.Height(leg);
                var foot = new Vector3(leg.x, sand, leg.z); var top = new Vector3(leg.x, AquariumTubeMap.LegTop + .25f, leg.z);
                legs.Cylinder(foot + Vector3.down * .3f, top, AquariumTubeMap.LegRadius, AquariumTubeMap.LegRadius * .85f, C(0x3e3756), 12);
                legs.Cylinder(foot + Vector3.down * .1f, foot + Vector3.up * .12f, .72f, .62f, C(0x51486c), 16);
                legs.Cylinder(top + Vector3.down * .35f, top + Vector3.up * .05f, .42f, .42f, Brass * .8f, 12);
            }
            shell.Emit("T5 • Visitor tube / cast acrylic", root, acrylic, false);
            floor.Emit("T5b • Visitor tube walkway", root, tubeFloor);
            frame.Emit("T5c • Visitor tube ribs and handrails", root, metal);
            glow.Emit("T5d • Visitor tube walkway lights", root, trim, false);
            legs.Emit("T6 • Visitor tube legs", root, metal);

            // ---- fallen stone along the wall foot
            along = 0;
            for (int i = 0; i < n; i += 3)
            {
                if (r.NextDouble() < .35) continue;
                var p = loop[i]; if (AquariumEntryLayout.KeepWaterClear(p, 1) || AquariumTubeMap.Near(p, 1.5f)) continue;
                var c = V(p + outward[i] * Range(r, .7f, 1.1f), AquariumSeabed.Height(p) - .1f);
                rubble.Ellipsoid(c, new Vector3(Range(r, .7f, 1.3f), Range(r, .4f, .9f), Range(r, .7f, 1.3f)), C(0x7d6f94), 8, 5, .3f);
            }
            rubble.Emit("T7 • Fallen stone", root, rubbleMat);

            // ---- crystals: clusters on the walls and the ceiling, a few on the sand by the walls
            var crystals = new Geo();
            Color[] tints = { C(0xa66bff), C(0x7f5cff), C(0x52e3ff), C(0xff6fe0), C(0xc39bff) };
            for (int i = 0; i < n; i += 5)
            {
                if (r.NextDouble() < .45) continue;
                var p = loop[i]; if (AquariumEntryLayout.KeepWaterClear(p, 2) || AquariumTubeMap.Near(p, 1)) continue;
                float y = r.NextDouble() < .35 ? AquariumSeabed.Height(p) + Range(r, 0, .2f) : Range(r, 1.2f, 16.5f);
                var tint = tints[r.Next(tints.Length)]; var inward = -V(outward[i], 0);
                int count = 3 + r.Next(4);
                for (int k = 0; k < count; k++)
                {
                    var foot = V(p + outward[i] * .25f, y) + new Vector3(Range(r, -.6f, .6f), Range(r, -.4f, .4f), Range(r, -.6f, .6f));
                    var dir = (inward * Range(r, .5f, 1f) + Vector3.up * Range(r, -.3f, .8f) + new Vector3(Range(r, -.3f, .3f), 0, Range(r, -.3f, .3f))).normalized;
                    crystals.Cylinder(foot, foot + dir * Range(r, .35f, 1.2f), Range(r, .07f, .18f), .012f, Color.Lerp(tint, Color.white, Range(r, 0, .2f)), 6);
                }
            }
            for (int i = 0, placed = 0; i < 400 && placed < 30; i++)
            {
                float x = Range(r, -36, 36), z = Range(r, -17, 17);
                if (AquariumTwinMap.Water(x, z) < -.5f) continue;
                var at = new Point(x, z); if (AquariumEntryLayout.KeepPromenadeClear(at, 2) || AquariumEntryLayout.KeepWaterClear(at, 2)) continue;
                var tint = tints[r.Next(tints.Length)]; placed++;
                for (int k = 0; k < 3; k++)
                {
                    var foot = new Vector3(x + Range(r, -.5f, .5f), TubeCeiling - .4f, z + Range(r, -.5f, .5f));
                    crystals.Cylinder(foot, foot + new Vector3(Range(r, -.3f, .3f), -1, Range(r, -.3f, .3f)).normalized * Range(r, .5f, 1.4f), Range(r, .08f, .17f), .012f, tint, 6);
                }
            }
            crystals.Emit("T8 • Glowing crystals", root, crystal, false);

            // ---- lamps in the ceiling over open water, their light falling to the sand; daylight down the hatch
            var lamps = new Geo(); var beams = new Geo();
            var lampSpots = new List<Vector3> { hatchCentre + Vector3.up * 2f };
            foreach (var spot in new[] { new Point(-28, 13), new Point(-28, -13), new Point(-12, 0), new Point(27, 6), new Point(22, -11), new Point(-4, 13.5f), new Point(-4, -13.5f) })
            {
                var top = new Vector3(spot.x, TubeCeiling - .2f, spot.z); lampSpots.Add(top);
                lamps.Cylinder(top + Vector3.down * .15f, top + Vector3.up * .1f, .9f, .9f, C(0xf0e4ff), 18);
            }
            foreach (var top in lampSpots)
                for (int i = 0; i < 4; i++)
                {
                    float angle = Range(r, 0, Mathf.PI * 2), rr = Range(r, 0, 1.2f);
                    var start = new Vector3(top.x + Mathf.Cos(angle) * rr, AquariumModel.WaterHeight, top.z + Mathf.Sin(angle) * rr);
                    var endXZ = new Point(start.x + Range(r, -1.5f, 1.5f), start.z + Range(r, -1.5f, 1.5f));
                    if (AquariumTwinMap.Water(endXZ.x, endXZ.z) < .5f) endXZ = new Point(start.x, start.z);
                    var end = new Vector3(endXZ.x, AquariumSeabed.Height(endXZ) + .5f, endXZ.z);
                    float width = Range(r, .5f, 1.1f), phase = Range(r, 0, Mathf.PI * 2);
                    for (int plane = 0; plane < 2; plane++)
                    {
                        Vector3 side = plane == 0 ? new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) : new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
                        int b0 = beams.Count; const int beamRows = 22, columns = 4;
                        for (int row = 0; row <= beamRows; row++) for (int column = 0; column <= columns; column++)
                        {
                            float depth = row / (float)beamRows, u = column / (float)columns;
                            beams.V(Vector3.Lerp(start, end, depth) + side * (u * 2 - 1) * width * Mathf.Lerp(1, 2.4f, depth), Color.white, new Vector2(u, 1 - depth), new Vector2(phase, 0));
                            if (row < beamRows && column < columns) { int v = b0 + row * (columns + 1) + column; beams.Q(v, v + 1, v + columns + 2, v + columns + 1); }
                        }
                    }
                }
            lamps.Emit("T9 • Ceiling lamps", root, lampMat, false);
            var beamObject = beams.Emit("T9b • Lamp light in the water", root, lampRays, false);
            var beamMesh = beamObject.GetComponent<MeshFilter>().sharedMesh; var beamBounds = beamMesh.bounds; beamBounds.Expand(2); beamMesh.bounds = beamBounds;

            // ---- aeration
            var bubbles = new Geo();
            foreach (var v in AquariumSceneryLayout.TubeVents)
                for (int i = 0; i < 28; i++)
                {
                    float size = Range(r, .022f, .075f);
                    var p = new Vector3(v.x + Range(r, -.23f, .23f), 0, v.z + Range(r, -.23f, .23f)); p.y = AquariumSeabed.Height(p.x, p.z) + .06f;
                    bubbles.Ellipsoid(p, Vector3.one * size, Color.white, 8, 6, 0, new Vector2(Range(r, 0, 1), Range(r, .065f, .13f)));
                    marker.bubbleCount++;
                }
            var bubbleObject = bubbles.Emit("T10 • Rising aeration bubbles / GPU animated", root, bubbleMat, false);
            bubbleObject.GetComponent<MeshFilter>().sharedMesh.bounds = new Bounds(Vector3.up * 6, new Vector3(80, AquariumModel.WaterHeight + 1, 40));
        }

        private static Vector3 WallPoint(Geo g, Point[] loop, Point[] outward, int i, int k, List<float> rows)
        {
            // only used for the winding test: the plain (un-pushed) wall point is close enough
            return new Vector3(loop[i].x, rows[k], loop[i].z);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R73: "보랏빛 동굴 수조", the third map. The twin tanks' gallery machinery (AquariumEnvironment.Twin) builds the
    // deck, glass, tunnel and visitor gallery round AquariumTwinMap's grotto shape; this adds what makes it a cave:
    //  * a rock vault over everything, low at the walls and high over the water, with stalactites;
    //  * the column tank (B) as a glass cylinder rising from its deck edge into the vault, under a round skylight
    //    whose light falls down the column into the water;
    //  * glowing violet and cyan crystals along the cave walls and on the sand by the glass;
    //  * dim violet materials (the camera adds the map's purple grade and fog, AquariumGalleryCamera).
    // Object names start with G (not a number): the scenery presentation reads numbered names as reef parts.
    public static partial class AquariumEnvironment
    {
        public const float GrottoSkylightRadius = 8.5f;
        private const float GrottoDeck = AquariumModel.WaterHeight + .3f;

        private static void GrottoMaterials(Material sand, Material architecture, Material interior, Material fixtures, Material floor,
            Material paving, Material glass, Material acrylic, Material rays, Material water)
        {
            sand.SetColor("_Color", C(0xb3a2c4));
            architecture.SetColor("_Color", C(0x8d82a3));
            paving.SetColor("_Color", C(0x8e86a6));
            interior.SetColor("_Color", C(0x9a86b8)); fixtures.SetColor("_Color", C(0x9a86b8)); floor.SetColor("_Color", C(0x9286a8));
            var g = C(0xa58dff); g.a = .24f; glass.SetColor("_Color", g);
            var a = C(0xae98ff); a.a = .32f; acrylic.SetColor("_Color", a);
            var ray = C(0xd9c2ff); ray.a = .14f; rays.SetColor("_Color", ray);
            water.SetColor("_Color", new Color(.26f, .16f, .52f, .085f));
            sand.name += " (violet grotto)"; architecture.name += " (violet grotto)";
        }

        // The vault's underside height over a point of the plan: down to the deck behind the gallery's back wall,
        // up to about 27 m over open water, rising round the skylight; a rough, lumpy rock surface.
        public static float GrottoCeiling(float x, float z)
        {
            float w = AquariumTwinMap.Water(x, z);
            float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-7.2f, 6f, w));
            float dome = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4f, 18f, w));
            var b = AquariumTwinMap.CentreB; float d = Mathf.Sqrt((x - b.x) * (x - b.x) + (z - b.z) * (z - b.z));
            float sky = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(GrottoSkylightRadius + 7, GrottoSkylightRadius, d));
            float lump = (Mathf.PerlinNoise(x * .11f + 13.7f, z * .11f + 4.1f) - .5f) * 2.6f + (Mathf.PerlinNoise(x * .37f + 2.3f, z * .37f + 9.4f) - .5f) * .9f;
            float h = GrottoDeck + 14.2f * t + 2.8f * dome + 3.2f * sky + lump * Mathf.Clamp01(t * 1.6f);
            return Mathf.Max(GrottoDeck, h);
        }

        private static void BuildGrotto(Transform root, Texture caustics, Material glass, Material rays, TwinField field, System.Random r)
        {
            var rock = ScannedSurface("Grotto • violet cave rock", "Rock", caustics, .3f, false);
            rock.SetColor("_Color", C(0x6f6286)); rock.SetFloat("_NormalStrength", 1.1f);
            var crystal = Lit("Grotto • glowing crystals", caustics, .7f, 0); crystal.SetFloat("_Emission", 1.7f);
            var frame = Lit("Grotto • bronze column frame", caustics, .45f, 0); frame.SetColor("_Color", C(0x9a8db5));
            var skyMat = Lit("Grotto • skylight", caustics, 0, 0); skyMat.SetFloat("_Emission", 3.2f);
            var shaftMat = new Material(rays) { name = "Grotto • skylight shafts" };
            var shaftTint = C(0xf1e6ff); shaftTint.a = .2f; shaftMat.SetColor("_Color", shaftTint);
            var centre = AquariumTwinMap.CentreB;

            // ---- the vault (a height field seen from below; nothing over the skylight)
            var vault = new Geo();
            const float step = 1f, minX = -60, maxX = 60, minZ = -33, maxZ = 33;
            int nx = Mathf.RoundToInt((maxX - minX) / step) + 1, nz = Mathf.RoundToInt((maxZ - minZ) / step) + 1;
            var index = new int[nx * nz]; var water = new float[nx * nz]; var hole = new bool[nx * nz];
            for (int iz = 0; iz < nz; iz++) for (int ix = 0; ix < nx; ix++)
            {
                float x = minX + ix * step, z = minZ + iz * step; int i = iz * nx + ix;
                water[i] = AquariumTwinMap.Water(x, z);
                float d = Mathf.Sqrt((x - centre.x) * (x - centre.x) + (z - centre.z) * (z - centre.z));
                hole[i] = d < GrottoSkylightRadius;
                index[i] = vault.Count;
                vault.V(new Vector3(x, GrottoCeiling(x, z), z), Color.white);
            }
            for (int iz = 0; iz < nz - 1; iz++) for (int ix = 0; ix < nx - 1; ix++)
            {
                int a = iz * nx + ix, b = a + 1, c = a + nx + 1, d = a + nx;
                if (hole[a] || hole[b] || hole[c] || hole[d]) continue;
                if (Mathf.Max(Mathf.Max(water[a], water[b]), Mathf.Max(water[c], water[d])) < -9.5f) continue;
                vault.Q(index[a], index[b], index[c], index[d]);   // wound to face down
            }
            vault.Emit("G0 • Grotto vault / violet cave rock", root, rock, false);

            // ---- the skylight: a rock lip, a short shaft up to a glowing sky, and its light down the column
            var lip = new Geo(); var shaft = new Geo(); var sky = new Geo();
            const int around = 48; float lipY = GrottoCeiling(centre.x + GrottoSkylightRadius + .5f, centre.z), skyY = lipY + 7.5f;
            for (int k = 0; k < 30; k++)
            {
                float angle = k * Mathf.PI * 2 / 30 + Range(r, -.05f, .05f), rr = GrottoSkylightRadius + Range(r, .1f, .9f);
                var p = new Vector3(centre.x + Mathf.Cos(angle) * rr, 0, centre.z + Mathf.Sin(angle) * rr);
                p.y = GrottoCeiling(p.x, p.z) - Range(r, .1f, .5f);
                lip.Ellipsoid(p, new Vector3(Range(r, 1.4f, 2.2f), Range(r, .8f, 1.4f), Range(r, 1.4f, 2.2f)), Color.white, 9, 6, .28f);
            }
            int ring = shaft.Count;
            for (int k = 0; k <= around; k++)
            {
                float angle = k * Mathf.PI * 2 / around, wobble = 1 + .05f * Mathf.Sin(angle * 5 + 1.3f);
                float rr = (GrottoSkylightRadius + .2f) * wobble;
                var bottom = new Vector3(centre.x + Mathf.Cos(angle) * rr, lipY - 1.2f, centre.z + Mathf.Sin(angle) * rr);
                var top = new Vector3(centre.x + Mathf.Cos(angle) * rr * .86f, skyY + .3f, centre.z + Mathf.Sin(angle) * rr * .86f);
                shaft.V(bottom, Color.white); shaft.V(top, Color.white);
            }
            for (int k = 0; k < around; k++) { int v = ring + k * 2; shaft.Q(v, v + 2, v + 3, v + 1); }   // faces in
            int skyCentre = sky.Count; sky.V(new Vector3(centre.x, skyY, centre.z), C(0xf3ecff));
            for (int k = 0; k <= around; k++)
            {
                float angle = k * Mathf.PI * 2 / around;
                sky.V(new Vector3(centre.x + Mathf.Cos(angle) * GrottoSkylightRadius * 1.05f, skyY, centre.z + Mathf.Sin(angle) * GrottoSkylightRadius * 1.05f), C(0xc9b3ff));
                if (k > 0) sky.T(skyCentre, skyCentre + k, skyCentre + k + 1);
            }
            lip.Emit("G1 • Skylight rim rocks", root, rock, false);
            shaft.Emit("G1b • Skylight shaft", root, rock, false);
            sky.Emit("G1c • Skylight / open sky", root, skyMat, false);

            var beams = new Geo();
            for (int i = 0; i < 18; i++)
            {
                float angle = Range(r, 0, Mathf.PI * 2), rr = Mathf.Sqrt(Range(r, 0, 1)) * (GrottoSkylightRadius - 1.5f);
                var top = new Vector3(centre.x + Mathf.Cos(angle) * rr, skyY - 1, centre.z + Mathf.Sin(angle) * rr);
                var bottomXZ = new Point(top.x + Range(r, -2.2f, 2.2f), top.z + Range(r, -2.2f, 2.2f));
                if (AquariumTwinMap.Water(bottomXZ.x, bottomXZ.z) < 1) bottomXZ = new Point(top.x, top.z);
                var bottom = new Vector3(bottomXZ.x, AquariumSeabed.Height(bottomXZ) + .5f, bottomXZ.z);
                float width = Range(r, .6f, 1.3f), phase = Range(r, 0, Mathf.PI * 2);
                for (int plane = 0; plane < 2; plane++)
                {
                    Vector3 side = plane == 0 ? new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) : new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
                    int n = beams.Count; const int rows = 28, columns = 4;
                    for (int row = 0; row <= rows; row++) for (int column = 0; column <= columns; column++)
                    {
                        float depth = row / (float)rows, u = column / (float)columns;
                        var c = Vector3.Lerp(top, bottom, depth); float spread = Mathf.Lerp(1, 2.2f, depth);
                        beams.V(c + side * (u * 2 - 1) * width * spread, Color.white, new Vector2(u, 1 - depth), new Vector2(phase, 0));
                        if (row < rows && column < columns) { int v = n + row * (columns + 1) + column; beams.Q(v, v + 1, v + columns + 2, v + columns + 1); }
                    }
                }
            }
            var beamObject = beams.Emit("G2 • Skylight light shafts down the column", root, shaftMat, false);
            var beamMesh = beamObject.GetComponent<MeshFilter>().sharedMesh; var beamBounds = beamMesh.bounds; beamBounds.Expand(3); beamMesh.bounds = beamBounds;

            // ---- the column tank's glass: its deck edge carried up into the vault
            var columnGlass = new Geo(); var framing = new Geo();
            List<Vector3> edge = null;
            foreach (var loop in field.Contours(field.Open))
            {
                float sum = 0; foreach (var p in loop) sum += p.x;
                if (sum / loop.Count > 0) edge = loop;
            }
            if (edge != null)
            {
                float bottomY = Elevated(8.12f), travel = 0;
                for (int i = 0; i < edge.Count; i++)
                {
                    var a = edge[i]; var b = edge[(i + 1) % edge.Count];
                    float ta = GrottoCeiling(a.x, a.z) + .4f, tb = GrottoCeiling(b.x, b.z) + .4f;
                    columnGlass.Quad(Height(a, bottomY), Height(b, bottomY), Height(b, tb), Height(a, ta), Color.white);
                    for (float y = bottomY + 2.6f; y < Mathf.Min(ta, tb) - .6f; y += 2.6f)
                        framing.Cylinder(Height(a, y), Height(b, y), .035f, .035f, Brass, 6);
                    travel += Vector3.Distance(a, b);
                    if (travel >= 2.4f) { framing.Cylinder(Height(a, bottomY), Height(a, ta), .04f, .04f, Brass, 6); travel = 0; }
                }
            }
            columnGlass.Emit("G3 • Column tank / glass cylinder up to the skylight", root, glass, false);
            framing.Emit("G3b • Column tank bronze rings and mullions", root, frame);

            // ---- stalactites and crystals
            var drips = new Geo(); var crystals = new Geo();
            Color[] glow = { C(0xa66bff), C(0x7f5cff), C(0x52e3ff), C(0xff6fe0), C(0xc39bff) };
            for (int i = 0, placed = 0; i < 900 && placed < 110; i++)
            {
                float x = Range(r, -52, 46), z = Range(r, -28, 28);
                float w = AquariumTwinMap.Water(x, z);
                if (w < -7f || w > 16f) continue;
                if (Mathf.Sqrt((x - centre.x) * (x - centre.x) + (z - centre.z) * (z - centre.z)) < GrottoSkylightRadius + 2.5f) continue;
                if (AquariumEntryLayout.DeckDistance(new Point(x, z)) < 4) continue;
                float y = GrottoCeiling(x, z); if (y < GrottoDeck + 3.5f) continue;
                float length = Range(r, 1.2f, 3.8f) * Mathf.Clamp01((y - GrottoDeck - 2) / 6f);
                drips.Cylinder(new Vector3(x, y + .4f, z), new Vector3(x + Range(r, -.15f, .15f), y - length, z + Range(r, -.15f, .15f)), Range(r, .35f, .8f), .03f, Color.white, 7);
                placed++;
            }
            // Crystal clusters along the cave wall (just behind the gallery rim), some hanging from the vault.
            var wallLine = AquariumTwinMap.Outline(-6.4f, 3.1f);
            foreach (var p in wallLine)
            {
                if (r.NextDouble() < .35) continue;
                if (AquariumEntryLayout.DeckDistance(p) < 3) continue;
                var outward = AquariumTwinMap.Outward(p);
                int count = 3 + r.Next(4); var tint = glow[r.Next(glow.Length)];
                for (int k = 0; k < count; k++)
                {
                    var foot = new Vector3(p.x + Range(r, -.9f, .9f), 0, p.z + Range(r, -.9f, .9f));
                    foot.y = Mathf.Max(GrottoDeck, GrottoCeiling(foot.x, foot.z) - .3f) - .05f;
                    var dir = new Vector3(-outward.x * Range(r, .1f, .6f) + Range(r, -.3f, .3f), Range(r, .7f, 1.1f), -outward.z * Range(r, .1f, .6f) + Range(r, -.3f, .3f)).normalized;
                    float len = Range(r, .5f, 1.7f);
                    crystals.Cylinder(foot - dir * .1f, foot + dir * len, Range(r, .09f, .22f), .015f, Color.Lerp(tint, Color.white, Range(r, 0, .25f)), 6);
                }
            }
            for (int i = 0, placed = 0; i < 600 && placed < 40; i++)
            {
                float x = Range(r, -52, 46), z = Range(r, -28, 28);
                float w = AquariumTwinMap.Water(x, z); if (w < -6.5f || w > 2f) continue;
                float y = GrottoCeiling(x, z); if (y < GrottoDeck + 4) continue;
                var tint = glow[r.Next(glow.Length)];
                for (int k = 0; k < 3; k++)
                {
                    var foot = new Vector3(x + Range(r, -.5f, .5f), y + .2f, z + Range(r, -.5f, .5f));
                    var dir = new Vector3(Range(r, -.35f, .35f), -1, Range(r, -.35f, .35f)).normalized;
                    crystals.Cylinder(foot, foot + dir * Range(r, .5f, 1.4f), Range(r, .08f, .18f), .012f, tint, 6);
                }
                placed++;
            }
            // A few small clusters on the sand right by the glass (decoration only; too small to hide in).
            for (int i = 0, placed = 0; i < 1500 && placed < 30; i++)
            {
                float x = Range(r, -50, 40), z = Range(r, -23, 23);
                float w = AquariumTwinMap.Water(x, z); if (w < .45f || w > 1.0f) continue;
                var at = new Point(x, z);
                if (AquariumTwinMap.InTunnel(x) || AquariumEntryLayout.KeepWaterClear(at, 1)) continue;
                var tint = glow[r.Next(glow.Length)]; var outward = AquariumTwinMap.Outward(at);
                for (int k = 0; k < 4; k++)
                {
                    var foot = new Vector3(x + Range(r, -.4f, .4f), 0, z + Range(r, -.4f, .4f)); foot.y = AquariumSeabed.Height(foot.x, foot.z) - .05f;
                    var dir = new Vector3(-outward.x * Range(r, 0, .5f) + Range(r, -.3f, .3f), 1, -outward.z * Range(r, 0, .5f) + Range(r, -.3f, .3f)).normalized;
                    crystals.Cylinder(foot, foot + dir * Range(r, .25f, .8f), Range(r, .05f, .13f), .01f, tint, 6);
                }
                placed++;
            }
            drips.Emit("G4 • Stalactites", root, rock, false);
            crystals.Emit("G5 • Glowing violet crystals", root, crystal, false);
        }
    }
}

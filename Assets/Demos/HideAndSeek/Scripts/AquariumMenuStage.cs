using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R71/R72: the main menu's living background. The character select's reef (AquariumEnvironment.LobbyBackdrop) filled
    // to the whole screen and seen from further back: the five aliens flee round a wide figure-of-eight with one diver
    // chasing them, while the tank's fish swim their own loops (a school of sky-blue fish and the other species) at
    // different depths. Aliens and fish are the game's own creatures at the game's own sizes (AquariumCreature), the
    // diver is the game's diver prefab, so the aliens look as small next to the diver as in a round. A warm light from
    // the surface follows the leading alien. Drawn by its own full-screen camera on the stages' layer; the tank's camera
    // rests while the menu is up (AquariumGame.UpdateMenuStage).
    public sealed class AquariumMenuStage : IDisposable
    {
        private const int Layer = AquariumAlienPreview.Layer;
        private static readonly Vector3 Origin = new Vector3(0, -400, -220);
        private readonly GameObject stage;
        private readonly Camera camera;
        private readonly AquariumLobbyBackdrop backdrop;
        private enum Route { Chase, School, Loop }
        private sealed class Runner
        {
            public Transform holder; public AquariumRig rig; public AquariumCreature creature; public int form; public Route route;
            public float offset, lateral, bob, seed, speedScale = 1; public Vector3 centre, radius; public float rate, rise;
            public Vector3 last; public float speed; public bool placed;
        }
        private readonly List<Runner> runners = new List<Runner>();
        private float lastUpdate = -1; private bool layered;
        public Camera Camera { get { return camera; } }

        public AquariumMenuStage()
        {
            stage = new GameObject("Main menu stage"); stage.transform.position = Origin;
            backdrop = AquariumEnvironment.LobbyBackdrop(stage.transform, 71, Layer);
            ForegroundSand();
            var random = new System.Random(72);
            // the chase: five aliens ahead, one diver behind
            var order = AquariumAlienKinds.LobbyOrder;
            float[] alienOffset = { 0, .2f, .38f, .55f, .72f };
            float[] alienSide = { 0, -.55f, .5f, -.25f, .3f };
            for (int i = 0; i < order.Length && i < alienOffset.Length; i++)
                runners.Add(Creature(-1, order[i], Route.Chase, alienOffset[i], alienSide[i], i, random));
            var diver = new Runner { route = Route.Chase, offset = -.62f, lateral = .1f, bob = 4.1f, seed = 3.3f, form = -2 };
            diver.holder = new GameObject("Diver").transform; diver.holder.SetParent(stage.transform, false);
            var prefab = Resources.Load<GameObject>("AquariumEquipment/Diver");
            if (prefab != null)
            {
                diver.rig = UnityEngine.Object.Instantiate(prefab, diver.holder).GetComponent<AquariumRig>();
                if (diver.rig != null) { var gun = AquariumFirstPersonEquipment.CreateGun(diver.rig.transform, diver.rig); AquariumDiverKinds.PaintGun(gun, 0); }
            }
            runners.Add(diver);
            // the fish: a school of the small sky-blue fish, and the other species on loops of their own
            var catalog = Resources.LoadAll<AquariumSpecies>("AquariumCharacters");
            int schoolSpecies = -1;
            foreach (var s in catalog) if (schoolSpecies < 0 && (s.normal != null && s.normal.name == "Fish0" || AquariumFishTraits.Habit(s.speciesId) == FishHabit.School)) schoolSpecies = s.speciesId;
            if (schoolSpecies < 0 && catalog.Length > 0) schoolSpecies = catalog[0].speciesId;
            for (int i = 0; i < 7 && schoolSpecies >= 0; i++)
            {
                var r = Creature(schoolSpecies, 0, Route.School, i * .045f, Range(random, -.5f, .5f), 20 + i, random);
                r.rise = Range(random, -.35f, .35f); runners.Add(r);
            }
            int n = 0;
            foreach (var s in catalog)
            {
                if (s.speciesId == schoolSpecies) continue;
                for (int k = 0; k < 2; k++, n++)
                {
                    var r = Creature(s.speciesId, 0, Route.Loop, Range(random, 0, 6.28f), 0, 40 + n, random);
                    bool back = (n % 3) != 0;
                    r.centre = new Vector3(Range(random, -6f, 8f), Range(random, .4f, 2.4f), back ? Range(random, -7.5f, -4.5f) : Range(random, -2.5f, .8f));
                    r.radius = new Vector3(Range(random, 2.5f, 5.5f), 0, Range(random, .8f, 2.2f));
                    r.rate = (k == 0 ? 1 : -1) * Range(random, .12f, .22f) * Mathf.Clamp(AquariumFishTraits.Speed(s.speciesId), .5f, 1.6f);
                    runners.Add(r);
                }
            }

            camera = new GameObject("Main menu camera").AddComponent<Camera>(); camera.transform.SetParent(stage.transform, false);
            camera.transform.localPosition = new Vector3(0, 2.1f, 13.2f); camera.transform.LookAt(stage.transform.TransformPoint(new Vector3(1.2f, .55f, -2.2f)));
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.04f, .14f, .2f);
            camera.cullingMask = 1 << Layer; camera.fieldOfView = 42; camera.nearClipPlane = .1f; camera.farClipPlane = 70;
            camera.depth = 40; camera.allowHDR = false; camera.allowMSAA = true;
            AddLight("Key", new Vector3(30, 200, 0), .66f, new Color(1, .97f, .92f));
            AddLight("Fill", new Vector3(12, 150, 0), .2f, new Color(.72f, .86f, 1));
            AddLight("Rim", new Vector3(18, 10, 0), .5f, new Color(.55f, .95f, 1));
            Relayer();
            stage.SetActive(false);
        }

        // The reef's sand ends 6 m in front of the stage; seen from the menu's further camera it is carried on to the lens
        // with the same material and the same colouring (level, warm in the middle, cooler toward the sides).
        private void ForegroundSand()
        {
            Material sand = null;
            foreach (var m in backdrop.Lit) if (m != null && m.name.Contains("sand")) { sand = m; break; }
            if (sand == null) return;
            const int nx = 60, nz = 16; const float x0 = -26, x1 = 26, z0 = 6, z1 = 22, y = -.95f;
            var vertices = new List<Vector3>(); var colours = new List<Color>(); var normals = new List<Vector3>(); var triangles = new List<int>();
            Color edge = new Color(.49f, .54f, .5f), middle = new Color(.94f, .87f, .72f);
            for (int j = 0; j <= nz; j++) for (int i = 0; i <= nx; i++)
            {
                float x = Mathf.Lerp(x0, x1, i / (float)nx), z = Mathf.Lerp(z0, z1, j / (float)nz);
                float centre = Mathf.Clamp01(1 - (Mathf.Abs(x) - 2.5f) / 9);
                vertices.Add(new Vector3(x, y, z)); colours.Add(Color.Lerp(edge, middle, .25f + .75f * centre)); normals.Add(Vector3.up);
                if (i < nx && j < nz) { int v = j * (nx + 1) + i; triangles.AddRange(new[] { v, v + nx + 1, v + nx + 2, v, v + nx + 2, v + 1 }); }
            }
            var mesh = new Mesh { name = "Menu foreground sand" };
            mesh.SetVertices(vertices); mesh.SetColors(colours); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            var go = new GameObject("Foreground sand"); go.transform.SetParent(stage.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = sand; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }

        private static float Range(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }

        private Runner Creature(int species, int alienLook, Route route, float offset, float lateral, int seed, System.Random random)
        {
            var holder = new GameObject(species < 0 ? "Alien" : "Fish " + species).transform; holder.SetParent(stage.transform, false);
            var visual = new GameObject("Rigged creature"); visual.transform.SetParent(holder, false);
            var creature = visual.AddComponent<AquariumCreature>(); creature.Initialize(species, seed, alienLook);
            return new Runner { holder = holder, creature = creature, form = species, route = route, offset = offset, lateral = lateral, bob = seed * 1.37f, seed = seed * .37f };
        }

        private void Relayer() { foreach (var t in stage.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer; }

        private void AddLight(string name, Vector3 euler, float intensity, Color color)
        {
            var light = new GameObject(name + " light").AddComponent<Light>(); light.transform.SetParent(stage.transform, false);
            light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(euler); light.intensity = intensity; light.color = color;
            light.cullingMask = 1 << Layer; light.shadows = LightShadows.None;
        }

        public bool Shown { get { return stage != null && stage.activeSelf; } }
        public void Show(bool visible) { if (stage != null && stage.activeSelf != visible) { stage.SetActive(visible); lastUpdate = -1; } }

        // The chase loop (stage space): a wide figure-of-eight over the open sand, rising and falling a little.
        private static Vector3 ChasePath(float s, float bob)
        { return new Vector3(1.4f + 5.6f * Mathf.Sin(s), .55f + .45f * Mathf.Sin(1.7f * s + bob), -2f + 1.9f * Mathf.Sin(2 * s)); }
        // The school's lane: a long oval behind the chase.
        private static Vector3 SchoolPath(float s)
        { return new Vector3(1f + 7.5f * Mathf.Sin(s), 1.9f + .5f * Mathf.Sin(2.3f * s), -5.2f + 1.6f * Mathf.Cos(s)); }

        private Vector3 Where(Runner r, float now, float s0)
        {
            switch (r.route)
            {
                case Route.Chase: return ChasePath(s0 + r.offset, r.bob);
                case Route.School: return SchoolPath(now * .085f + r.offset) + new Vector3(0, r.rise, 0);
                default:
                    float a = now * r.rate + r.offset;
                    return r.centre + new Vector3(Mathf.Sin(a) * r.radius.x, .3f * Mathf.Sin(a * 1.9f + r.seed), Mathf.Cos(a) * r.radius.z);
            }
        }

        public void Update(float now)
        {
            if (!Shown) return;
            if (!layered) { Relayer(); layered = true; }
            float dt = lastUpdate < 0 ? 0 : Mathf.Clamp(now - lastUpdate, 0, .1f); lastUpdate = now;
            // the chase surges a little: a short burst every few seconds
            float s0 = now * .3f + .2f * Mathf.Sin(now * .45f);
            Vector3 lead = Vector3.zero;
            foreach (var r in runners)
            {
                var on = Where(r, now, s0); var ahead = Where(r, now + .05f, s0 + .015f);
                var tangent = ahead - on; if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.forward; tangent.Normalize();
                var side = Vector3.Cross(Vector3.up, new Vector3(tangent.x, 0, tangent.z)).normalized;
                // aliens weave from side to side as they flee; the school ripples; the diver holds the line
                float weave = r.form == -1 ? .4f * Mathf.Sin(now * 2.3f + r.seed * 5) : r.route == Route.School ? .25f * Mathf.Sin(now * 1.3f + r.seed * 3) : 0;
                var target = on + side * (r.lateral + weave);
                if (!r.placed || dt == 0) { r.last = target; r.placed = true; }
                var velocity = dt > 0 ? (target - r.last) / dt : Vector3.zero; r.last = target;
                r.speed = Mathf.Lerp(r.speed, velocity.magnitude, dt > 0 ? 1 - Mathf.Exp(-dt * 6) : 1);
                r.holder.localPosition = target;
                var heading = velocity.sqrMagnitude > .0001f ? velocity : tangent;
                var face = new Vector3(heading.x, Mathf.Clamp(heading.y, -.35f, .35f) * .5f, heading.z);
                if (face.sqrMagnitude > .0001f)
                {
                    var want = Quaternion.LookRotation(face.normalized);
                    r.holder.localRotation = dt == 0 ? want : Quaternion.Slerp(r.holder.localRotation, want, 1 - Mathf.Exp(-dt * 7));
                }
                if (r.creature != null) r.creature.Present(r.form, "Locomotion", 0, now, Mathf.Max(.4f, r.speed));
                else if (r.rig != null) r.rig.SampleLocomotion(r.speed, now, r.seed, new Vector3(0, 0, r.speed));
                if (r.form == -1 && r.route == Route.Chase && Mathf.Abs(r.offset) < .01f) lead = target;
            }
            if (backdrop != null) backdrop.Focus(new Vector3(lead.x, 0, lead.z), .7f, dt > 0 ? dt : 1);
        }

        public void Dispose() { if (stage != null) UnityEngine.Object.Destroy(stage); }
    }
}

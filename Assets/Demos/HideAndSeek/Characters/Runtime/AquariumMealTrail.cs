using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // A world-space dye trail. Snapshot age owns both the path and fade, so
    // pausing/results do not keep simulating a Unity-time particle or TrailRenderer.
    // R32: two looks, both long and thick: the purple fish-blood wake after a meal (from the mouth) and the
    // green wound wake of a harpooned alien (from behind its body, for the whole injury). A wound keeps
    // bleeding while the alien hangs still: the dye then curls up into a cloud instead of a line.
    // R33: every bit of dye lingers AquariumModel.WakeLingerSeconds (5 s), fading slowly, and the trail is no
    // longer cut to a few metres, so the whole path of the last five seconds shows which way it swam.
    // R39: soft puffs instead of a ribbon. The dye spreads up to 1.16 m wide, but the mouth and the wound swing
    // round tighter than that whenever the swimmer turns (its facing snaps to the aim): a camera-facing ribbon
    // then folded back over itself and piled its dye into dark shards that looked like a broken mesh. Now the dye
    // is a chain of round, soft puffs laid along the path at fixed distances travelled (so they do not crawl as
    // the trail grows). A tight turn just becomes a rounder patch of dye. Each puff's opacity allows for how many
    // neighbours overlap it on screen (the shader knows the path's direction against the view), so the trail is
    // as dense as the old ribbon from any side, even looking down its length; as the dye spreads out, every other
    // puff fades away (they would only stack up).
    public sealed class AquariumMealTrail : MonoBehaviour
    {
        struct Sample { public Vector3 point; public float time, travelled; }
        // 30 samples a second for the 5 s each one lives, with room to spare.
        const int Capacity = 200;
        // (a hair of slack so a 30 or 60 fps frame clock does not skip every other sample)
        const float SampleInterval = 1f / 30;
        // One puff every 2.5 cm of path (fewer as the dye spreads: about one per 30% of its half-width), close
        // enough that even the thin fresh dye reads as a line, not a string of beads.
        const float PuffStep = .025f, PuffSpacing = .3f;
        // A wound bleeding in place still lays dye, as if it had moved this fast (the cloud above the alien).
        const float StillBleed = .5f;
        readonly Sample[] samples = new Sample[Capacity];
        readonly Vector3[] drifted = new Vector3[Capacity];
        readonly List<Vector3> vertices = new List<Vector3>(Capacity * 8);
        readonly List<Vector2> corners = new List<Vector2>(Capacity * 8);
        readonly List<Vector2> widths = new List<Vector2>(Capacity * 8), shares = new List<Vector2>(Capacity * 8);
        readonly List<Vector3> directions = new List<Vector3>(Capacity * 8);
        readonly List<Color> colors = new List<Color>(Capacity * 8);
        readonly List<int> indices = new List<int>(Capacity * 12);
        static Material material;
        Mesh mesh;
        MeshRenderer ribbon;
        MaterialPropertyBlock properties;
        int count;
        float lastAge = -100, lastSample = -100, seed, travelled;
        bool wound;
        // How long each bit of dye lasts, how long the trail may get, its half-width from fresh to spread, and opacity.
        float pointLifetime = AquariumModel.WakeLingerSeconds, maxLength = 45f, freshWidth = .2f, spreadWidth = .58f, oldWidth = .06f, opacity = .95f;
        public int SampleCount => count;
        public bool Wound => wound;
        public int PuffCount { get; private set; }

        public void Initialize(int eventId, bool woundWake = false)
        {
            seed = Mathf.Repeat(eventId * .618034f, 1);
            wound = woundWake;
            if (wound) { pointLifetime = AquariumModel.WakeLingerSeconds; maxLength = 45f; freshWidth = .2f; spreadWidth = .58f; oldWidth = .06f; opacity = .95f; }
            if (material == null) material = new Material(Resources.Load<Shader>("AquariumCharacters/MealTrail"))
                { name = "Diluted fish blood puffs", hideFlags = HideFlags.HideAndDontSave };
            mesh = new Mesh { name = wound ? "Green wound wake" : "Fish blood wake", indexFormat = IndexFormat.UInt32 }; mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            ribbon = gameObject.AddComponent<MeshRenderer>(); ribbon.sharedMaterial = material;
            ribbon.shadowCastingMode = ShadowCastingMode.Off; ribbon.receiveShadows = false; ribbon.enabled = false;
            properties = new MaterialPropertyBlock();
        }
        // The lifetime the effect needs after its last emission so the tail can dissolve.
        public static float FadeSeconds(bool woundWake) { return AquariumModel.WakeLingerSeconds; }

        public void Present(float age, float duration, Vector3 source, bool canEmit)
        {
            if (age < lastAge - .05f) { count = 0; lastSample = -100; travelled = 0; }
            lastAge = age;
            if (age < 0 || age >= duration) { ribbon.enabled = false; return; }
            while (count > 0 && age - samples[0].time > pointLifetime) RemoveFirst();
            // Stop feeding the trail early, leaving the last stretch to dissolve.
            if (canEmit && age <= duration - pointLifetime && age - lastSample >= SampleInterval - .002f)
            {
                if (count > 0 && Vector3.Distance(source, samples[count - 1].point) > 1.5f) count = 0;
                // A wound keeps bleeding in place; meal dye only follows a moving mouth.
                if (count == 0 || wound || Vector3.Distance(source, samples[count - 1].point) >= .018f)
                {
                    if (count == Capacity) RemoveFirst();
                    if (count > 0)
                        travelled += Mathf.Max(Vector3.Distance(source, samples[count - 1].point), wound ? StillBleed * (age - samples[count - 1].time) : 0);
                    samples[count++] = new Sample { point = source, time = age, travelled = travelled }; lastSample = age;
                }
            }
            while (count > 2 && PathLength() > maxLength) RemoveFirst();
            ribbon.enabled = count >= 2; if (!ribbon.enabled) { PuffCount = 0; return; }
            vertices.Clear(); corners.Clear(); widths.Clear(); shares.Clear(); directions.Clear(); colors.Clear(); indices.Clear();
            var lastDirection = Vector3.forward;
            // R45: a longer, softer end (was the last .8 s), squared so stacked puffs thin out evenly.
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(duration - Mathf.Max(1.6f, pointLifetime), duration, age)); fade *= fade;
            // Where each bit of dye has drifted to: meal dye slips down under the jaw; wound dye lifts and curls.
            // R33: gentle over the five seconds, so the line of the path still reads.
            var heading = samples[count - 1].point - samples[0].point;
            var side = Vector3.Cross(heading.sqrMagnitude > .0001f ? heading.normalized : Vector3.forward, Vector3.up);
            if (side.sqrMagnitude < .0001f) side = Vector3.right; side.Normalize();
            for (int i = 0; i < count; i++)
            {
                float life = Mathf.Clamp01((age - samples[i].time) / pointLifetime);
                Vector3 drift;
                if (wound)
                {
                    float t = (age - samples[i].time) * 1.3f + seed * 6.28f;
                    drift = Vector3.up * (life * .50f) + side * (Mathf.Sin(t * .9f) * life * .20f)
                        + Vector3.Cross(side, Vector3.up) * (Mathf.Cos(t * .7f) * life * .14f);
                }
                else drift = Vector3.down * (life * .45f) + side * (Mathf.Sin(life * 5 + seed * 6) * life * .05f);
                drifted[i] = samples[i].point + drift;
            }
            // Puffs at every PuffStep of distance travelled, between the oldest and the newest dye.
            float first = samples[0].travelled, last = samples[count - 1].travelled, span = Mathf.Max(1e-4f, last - first);
            int segment = 0, puffs = 0;
            for (int k = Mathf.CeilToInt(first / PuffStep); k * PuffStep <= last + 1e-5f; k++)
            {
                float at = k * PuffStep;
                while (segment < count - 2 && samples[segment + 1].travelled < at) segment++;
                var a = samples[segment]; var b = samples[segment + 1];
                float f = b.travelled > a.travelled ? Mathf.Clamp01((at - a.travelled) / (b.travelled - a.travelled)) : 1;
                float time = Mathf.Lerp(a.time, b.time, f);
                float life = Mathf.Clamp01((age - time) / pointLifetime), along = (at - first) / span;
                // R49: a taper. Thick where it leaves the source, thinning out toward the old end (the old end used to
                // fan out wide and looked fat). Irregular along the length: a slow swell, darker clumps, ragged edges.
                float h1 = Hash01(k, seed), h2 = Hash01(k + 7919, seed);
                float swell = .72f + .28f * Mathf.Sin(at * 9.1f + seed * 17) + .16f * Mathf.Sin(at * 23.7f + seed * 5);
                float taper = Mathf.Lerp(freshWidth, oldWidth, Mathf.Pow(life, .7f));
                float halfWidth = taper * swell * Mathf.Lerp(.55f, 1, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1, .96f, along)));
                float clump = Mathf.Clamp01((Mathf.Sin(at * 5.3f + seed * 31) + Mathf.Sin(at * 13.9f + seed * 11)) * .5f + h1 * .35f - .25f);
                // Old, thin dye keeps fewer puffs too (the overlap share in the shader keeps its density).
                float stride = Mathf.Max(Mathf.Log(Mathf.Max(1, PuffSpacing * halfWidth / PuffStep), 2), Mathf.Min(1.3f, life * 1.6f));
                float keep = Mathf.Clamp01(Rank(k) - stride + 1);
                if (keep <= .001f) continue;
                // R33: a slow, even fade over the whole five seconds (strong at first, gone at the end).
                float density = opacity * Mathf.Pow(1 - life, .75f) * fade * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .06f, along));
                if (keep * density <= .002f) continue;
                var world = Vector3.Lerp(drifted[segment], drifted[segment + 1], f);
                // The path's direction here: the shader shares the density out among the puffs that overlap on screen
                // (seen along the path, many more stack up than seen from the side).
                var direction = drifted[segment + 1] - drifted[segment];
                if (direction.sqrMagnitude < 1e-8f) direction = lastDirection; else direction.Normalize();
                lastDirection = direction; var localDirection = transform.InverseTransformDirection(direction);
                // Ragged edges and curls: each bit of dye wanders off the path line, more as it ages (tendrils).
                var across = Vector3.Cross(direction, Vector3.up); if (across.sqrMagnitude < 1e-4f) across = side; across.Normalize();
                var lift = Vector3.Cross(across, direction);
                float curl = at * 11.3f + seed * 40 + life * 2.2f, wander = halfWidth * (.25f + .9f * life) * (.6f + .4f * Mathf.Sin(at * 3.1f + seed * 9));
                world += (across * Mathf.Sin(curl) + lift * Mathf.Cos(curl * .83f)) * wander + across * ((h2 - .5f) * halfWidth * .35f);
                float spacing = PuffStep * Mathf.Pow(2, stride);
                AddPuff(transform.InverseTransformPoint(world), localDirection, halfWidth * (.85f + .3f * h2), time, density * (1 + .45f * clump), keep, spacing, clump, h1);
                // Now and then a droplet or a stray filament flicks off to the side.
                if (h1 > .9f && life > .05f)
                {
                    float sideOff = (h2 > .5f ? 1 : -1) * halfWidth * (1.3f + 1.4f * life);
                    AddPuff(transform.InverseTransformPoint(world + across * sideOff + lift * (halfWidth * (h2 - .5f))), localDirection,
                        halfWidth * (.22f + .15f * h2), time, Mathf.Min(1, density * 1.6f), 1, halfWidth * 4, 1, h2 + 2);
                    puffs++;
                }
                puffs++;
            }
            PuffCount = puffs;
            ribbon.enabled = puffs > 0; if (!ribbon.enabled) return;
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetNormals(directions);mesh.SetUVs(0, corners);mesh.SetUVs(1, widths);mesh.SetUVs(2, shares);mesh.SetColors(colors);mesh.SetTriangles(indices, 0);
            var bounds = mesh.bounds; bounds.Expand(freshWidth * 4 + .6f); mesh.bounds = bounds;
            properties.SetFloat("_Age", age);properties.SetFloat("_Seed", seed);
            // R43: a darker green (was .05/.45/.08 with a .40/.95/.18 edge).
            if (wound) { properties.SetColor("_Pigment", new Color(.02f, .19f, .035f, 1)); properties.SetColor("_EdgeTint", new Color(.15f, .40f, .07f, 1)); }
            else { properties.SetColor("_Pigment", new Color(.30f, .07f, .38f, 1)); properties.SetColor("_EdgeTint", new Color(.62f, .30f, .70f, 1)); }
            ribbon.SetPropertyBlock(properties);
        }
        // corner order -1-1, 1-1, 11, -11. uv2 = (half-width, emission time), uv3 = (kept share, spacing),
        // colour = (clump darkness, per-puff random seed (>2 for a droplet), unused, density).
        void AddPuff(Vector3 centre, Vector3 localDirection, float halfWidth, float time, float density, float keep, float spacing, float clump, float random)
        {
            int v = vertices.Count;
            for (int c = 0; c < 4; c++)
            {
                vertices.Add(centre); corners.Add(new Vector2(c == 0 || c == 3 ? -1 : 1, c < 2 ? -1 : 1));
                // The wisp pattern is pinned to the dye (its emission time), so it does not slide along the trail.
                widths.Add(new Vector2(halfWidth, time)); colors.Add(new Color(clump, random * .25f, 0, density));
                shares.Add(new Vector2(keep, spacing)); directions.Add(localDirection);
            }
            indices.Add(v); indices.Add(v + 1); indices.Add(v + 2); indices.Add(v); indices.Add(v + 2); indices.Add(v + 3);
        }
        static float Hash01(int k, float seed) { unchecked { uint n = (uint)(k * 374761393 + (int)(seed * 9973) * 668265263); n = (n ^ (n >> 13)) * 1274126177; return ((n ^ (n >> 16)) & 65535) / 65535f; } }
        // How many times k halves evenly (k = 0 counts as always kept).
        static int Rank(int k) { if (k == 0) return 30; int r = 0; while ((k & 1) == 0) { k >>= 1; r++; } return r; }
        float PathLength() { float l = 0; for (int i = 1; i < count; i++) l += Vector3.Distance(samples[i - 1].point, samples[i].point); return l; }
        void RemoveFirst() { for (int i=1;i<count;i++)samples[i-1]=samples[i];count--; }
        void OnDestroy() { if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);} }
    }
}

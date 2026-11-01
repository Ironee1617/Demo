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
        float pointLifetime = AquariumModel.WakeLingerSeconds, maxLength = 45f, freshWidth = .18f, spreadWidth = .58f, opacity = .95f;
        public int SampleCount => count;
        public bool Wound => wound;
        public int PuffCount { get; private set; }

        public void Initialize(int eventId, bool woundWake = false)
        {
            seed = Mathf.Repeat(eventId * .618034f, 1);
            wound = woundWake;
            if (wound) { pointLifetime = AquariumModel.WakeLingerSeconds; maxLength = 45f; freshWidth = .18f; spreadWidth = .58f; opacity = .95f; }
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
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(duration - .8f, duration, age));
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
                // A firm origin that fans out into a broad, soft wake.
                float halfWidth = Mathf.Lerp(freshWidth, spreadWidth, Mathf.Sqrt(life)) * Mathf.Lerp(.35f, 1, Mathf.Sin(along * Mathf.PI));
                // Spread dye needs fewer puffs: keep every 2^n-th (by how many times k halves), fading the next ones out.
                float stride = Mathf.Log(Mathf.Max(1, PuffSpacing * halfWidth / PuffStep), 2);
                float keep = Mathf.Clamp01(Rank(k) - stride + 1);
                if (keep <= .001f) continue;
                // R33: a slow, even fade over the whole five seconds (strong at first, gone at the end).
                float density = opacity * Mathf.Pow(1 - life, .75f) * fade * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .06f, along));
                if (keep * density <= .002f) continue;
                var centre = transform.InverseTransformPoint(Vector3.Lerp(drifted[segment], drifted[segment + 1], f));
                // The path's direction here: the shader shares the density out among the puffs that overlap on screen
                // (seen along the path, many more stack up than seen from the side).
                var direction = drifted[segment + 1] - drifted[segment];
                if (direction.sqrMagnitude < 1e-8f) direction = lastDirection; else direction.Normalize();
                lastDirection = direction; var localDirection = transform.InverseTransformDirection(direction);
                int v = vertices.Count;
                for (int c = 0; c < 4; c++)
                {
                    vertices.Add(centre); corners.Add(new Vector2(c == 0 || c == 3 ? -1 : 1, c < 2 ? -1 : 1));
                    // The wisp pattern is pinned to the dye (its emission time), so it does not slide along the trail.
                    widths.Add(new Vector2(halfWidth, time)); colors.Add(new Color(1, 1, 1, density));
                    shares.Add(new Vector2(keep, PuffStep * Mathf.Pow(2, stride))); directions.Add(localDirection);
                }
                indices.Add(v); indices.Add(v + 1); indices.Add(v + 2); indices.Add(v); indices.Add(v + 2); indices.Add(v + 3);
                puffs++;
            }
            PuffCount = puffs;
            ribbon.enabled = puffs > 0; if (!ribbon.enabled) return;
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetNormals(directions);mesh.SetUVs(0, corners);mesh.SetUVs(1, widths);mesh.SetUVs(2, shares);mesh.SetColors(colors);mesh.SetTriangles(indices, 0);
            var bounds = mesh.bounds; bounds.Expand(spreadWidth * 2 + .4f); mesh.bounds = bounds;
            properties.SetFloat("_Age", age);properties.SetFloat("_Seed", seed);
            if (wound) { properties.SetColor("_Pigment", new Color(.05f, .45f, .08f, 1)); properties.SetColor("_EdgeTint", new Color(.40f, .95f, .18f, 1)); }
            else { properties.SetColor("_Pigment", new Color(.30f, .07f, .38f, 1)); properties.SetColor("_EdgeTint", new Color(.62f, .30f, .70f, 1)); }
            ribbon.SetPropertyBlock(properties);
        }
        // How many times k halves evenly (k = 0 counts as always kept).
        static int Rank(int k) { if (k == 0) return 30; int r = 0; while ((k & 1) == 0) { k >>= 1; r++; } return r; }
        float PathLength() { float l = 0; for (int i = 1; i < count; i++) l += Vector3.Distance(samples[i - 1].point, samples[i].point); return l; }
        void RemoveFirst() { for (int i=1;i<count;i++)samples[i-1]=samples[i];count--; }
        void OnDestroy() { if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);} }
    }
}

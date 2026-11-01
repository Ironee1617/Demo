using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // A world-space dye ribbon. Snapshot age owns both the path and fade, so
    // pausing/results do not keep simulating a Unity-time particle or TrailRenderer.
    // R32: two looks, both long and thick: the purple fish-blood wake after a meal (from the mouth) and the
    // green wound wake of a harpooned alien (from behind its body, for the whole injury). A wound keeps
    // bleeding while the alien hangs still: the dye then curls up into a cloud instead of a line.
    public sealed class AquariumMealTrail : MonoBehaviour
    {
        struct Sample { public Vector3 point; public float time; }
        const int Capacity = 160;
        readonly Sample[] samples = new Sample[Capacity];
        readonly Vector3[] drifted = new Vector3[Capacity];
        readonly List<Vector3> vertices = new List<Vector3>(Capacity * 2);
        readonly List<Vector3> tangents = new List<Vector3>(Capacity * 2);
        readonly List<Vector2> uv = new List<Vector2>(Capacity * 2);
        readonly List<Vector2> widths = new List<Vector2>(Capacity * 2);
        readonly List<Color> colors = new List<Color>(Capacity * 2);
        readonly List<int> indices = new List<int>((Capacity - 1) * 6);
        static Material material;
        Mesh mesh;
        MeshRenderer ribbon;
        MaterialPropertyBlock properties;
        int count;
        float lastAge = -100, lastSample = -100, seed;
        bool wound;
        // How long each bit of dye lasts, how long the ribbon may get, its half-width from fresh to spread, and opacity.
        float pointLifetime = 1.6f, maxLength = 6.5f, freshWidth = .18f, spreadWidth = .60f, opacity = .95f;
        public int SampleCount => count;
        public bool Wound => wound;

        public void Initialize(int eventId, bool woundWake = false)
        {
            seed = Mathf.Repeat(eventId * .618034f, 1);
            wound = woundWake;
            if (wound) { pointLifetime = 2.0f; maxLength = 6.5f; freshWidth = .18f; spreadWidth = .60f; opacity = .95f; }
            if (material == null) material = new Material(Resources.Load<Shader>("AquariumCharacters/MealTrail"))
                { name = "Diluted fish blood ribbon", hideFlags = HideFlags.HideAndDontSave };
            mesh = new Mesh { name = wound ? "Green wound wake" : "Fish blood wake" }; mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            ribbon = gameObject.AddComponent<MeshRenderer>(); ribbon.sharedMaterial = material;
            ribbon.shadowCastingMode = ShadowCastingMode.Off; ribbon.receiveShadows = false; ribbon.enabled = false;
            properties = new MaterialPropertyBlock();
        }
        // The lifetime the effect needs after its last emission so the tail can dissolve.
        public static float FadeSeconds(bool woundWake) { return woundWake ? 2.0f : 1.6f; }

        public void Present(float age, float duration, Vector3 source, bool canEmit)
        {
            if (age < lastAge - .05f) { count = 0; lastSample = -100; }
            lastAge = age;
            if (age < 0 || age >= duration) { ribbon.enabled = false; return; }
            while (count > 0 && age - samples[0].time > pointLifetime) RemoveFirst();
            // Stop feeding the ribbon early, leaving the last stretch to dissolve.
            if (canEmit && age <= duration - pointLifetime && age - lastSample >= 1f / 45)
            {
                if (count > 0 && Vector3.Distance(source, samples[count - 1].point) > 1.5f) count = 0;
                // A wound keeps bleeding in place; meal dye only follows a moving mouth.
                if (count == 0 || wound || Vector3.Distance(source, samples[count - 1].point) >= .018f)
                {
                    if (count == Capacity) RemoveFirst();
                    samples[count++] = new Sample { point = source, time = age }; lastSample = age;
                }
            }
            while (count > 2 && PathLength() > maxLength) RemoveFirst();
            ribbon.enabled = count >= 2; if (!ribbon.enabled) return;
            vertices.Clear(); tangents.Clear(); uv.Clear(); widths.Clear(); colors.Clear(); indices.Clear();
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(duration - .8f, duration, age));
            // Where each bit of dye has drifted to: meal dye slips down under the jaw; wound dye lifts and curls.
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
                    drift = Vector3.up * (life * .60f) + side * (Mathf.Sin(t * 1.7f) * life * .35f)
                        + Vector3.Cross(side, Vector3.up) * (Mathf.Cos(t * 1.3f) * life * .25f);
                }
                else drift = Vector3.down * (life * .55f) + side * (Mathf.Sin(life * 5 + seed * 6) * life * .05f);
                drifted[i] = samples[i].point + drift;
            }
            var lastTangent = Vector3.forward;
            for (int i = 0; i < count; i++)
            {
                float life = Mathf.Clamp01((age - samples[i].time) / pointLifetime);
                var direction = drifted[Mathf.Min(count - 1, i + 1)] - drifted[Mathf.Max(0, i - 1)];
                if (direction.sqrMagnitude < .000004f) direction = lastTangent; else direction.Normalize();
                lastTangent = direction;
                var point = transform.InverseTransformPoint(drifted[i]);
                var tangent = transform.InverseTransformDirection(direction);
                float along = i / (float)(count - 1);
                float alpha = opacity * Mathf.Sqrt(1 - life) * fade * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .10f, along));
                // A firm origin that fans out into a broad, soft wake.
                float halfWidth = Mathf.Lerp(freshWidth, spreadWidth, Mathf.Sqrt(life)) * Mathf.Lerp(.35f, 1, Mathf.Sin(along * Mathf.PI));
                for (int s = 0; s < 2; s++)
                {
                    vertices.Add(point); tangents.Add(tangent); uv.Add(new Vector2(along, s));
                    widths.Add(new Vector2(halfWidth, samples[i].time)); colors.Add(new Color(1, 1, 1, alpha));
                }
                if (i > 0) { int j = i * 2; indices.Add(j-2);indices.Add(j);indices.Add(j-1);indices.Add(j-1);indices.Add(j);indices.Add(j+1); }
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetNormals(tangents);mesh.SetUVs(0, uv);mesh.SetUVs(1, widths);mesh.SetColors(colors);mesh.SetTriangles(indices, 0);
            var bounds = mesh.bounds; bounds.Expand(spreadWidth * 2 + .4f); mesh.bounds = bounds;
            properties.SetFloat("_Age", age);properties.SetFloat("_Seed", seed);
            if (wound) { properties.SetColor("_Pigment", new Color(.05f, .45f, .08f, 1)); properties.SetColor("_EdgeTint", new Color(.40f, .95f, .18f, 1)); }
            else { properties.SetColor("_Pigment", new Color(.30f, .07f, .38f, 1)); properties.SetColor("_EdgeTint", new Color(.62f, .30f, .70f, 1)); }
            ribbon.SetPropertyBlock(properties);
        }
        float PathLength() { float l = 0; for (int i = 1; i < count; i++) l += Vector3.Distance(samples[i - 1].point, samples[i].point); return l; }
        void RemoveFirst() { for (int i=1;i<count;i++)samples[i-1]=samples[i];count--; }
        void OnDestroy() { if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);} }
    }
}

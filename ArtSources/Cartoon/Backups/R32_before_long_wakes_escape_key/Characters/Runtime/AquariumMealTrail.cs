using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // A short world-space dye ribbon. Snapshot age owns both the path and fade, so
    // pausing/results do not keep simulating a Unity-time particle or TrailRenderer.
    public sealed class AquariumMealTrail : MonoBehaviour
    {
        struct Sample { public Vector3 point; public float time; }
        const int Capacity = 48;
        const float PointLifetime = .55f;
        readonly Sample[] samples = new Sample[Capacity];
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
        public int SampleCount => count;

        public void Initialize(int eventId)
        {
            seed = Mathf.Repeat(eventId * .618034f, 1);
            if (material == null) material = new Material(Resources.Load<Shader>("AquariumCharacters/MealTrail"))
                { name = "Diluted fish blood ribbon", hideFlags = HideFlags.HideAndDontSave };
            mesh = new Mesh { name = "Short fish blood path" }; mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            ribbon = gameObject.AddComponent<MeshRenderer>(); ribbon.sharedMaterial = material;
            ribbon.shadowCastingMode = ShadowCastingMode.Off; ribbon.receiveShadows = false; ribbon.enabled = false;
            properties = new MaterialPropertyBlock();
        }

        public void Present(float age, float duration, Vector3 mouth, bool canEmit)
        {
            if (age < lastAge - .05f) { count = 0; lastSample = -100; }
            lastAge = age;
            if (age < 0 || age >= duration) { ribbon.enabled = false; return; }
            while (count > 0 && age - samples[0].time > PointLifetime) RemoveFirst();
            // Stop feeding the ribbon early, leaving its final half second to dissolve.
            if (canEmit && age <= duration - PointLifetime && age - lastSample >= 1f / 45)
            {
                if (count > 0 && Vector3.Distance(mouth, samples[count - 1].point) > 1.5f) count = 0;
                if (count == 0 || Vector3.Distance(mouth, samples[count - 1].point) >= .018f)
                {
                    if (count == Capacity) RemoveFirst();
                    samples[count++] = new Sample { point = mouth, time = age }; lastSample = age;
                }
            }
            while (count > 2 && Vector3.Distance(samples[0].point, samples[count - 1].point) > 1.6f) RemoveFirst();
            ribbon.enabled = count >= 2; if (!ribbon.enabled) return;
            vertices.Clear(); tangents.Clear(); uv.Clear(); widths.Clear(); colors.Clear(); indices.Clear();
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(duration - .7f, duration, age));
            for (int i = 0; i < count; i++)
            {
                float life = Mathf.Clamp01((age - samples[i].time) / PointLifetime);
                var direction = samples[Mathf.Min(count - 1, i + 1)].point - samples[Mathf.Max(0, i - 1)].point;
                if (direction.sqrMagnitude < .00001f) direction = Vector3.forward;
                // Dye slips beneath the closed jaw instead of disappearing inside the swimmer's body.
                var drift = Vector3.down * (life * .55f) + Vector3.Cross(direction.normalized, Vector3.up) * (Mathf.Sin(life * 5 + seed * 6) * life * .035f);
                var point = transform.InverseTransformPoint(samples[i].point + drift);
                var tangent = transform.InverseTransformDirection(direction.normalized);
                float along = i / (float)(count - 1);
                float alpha = .4f * Mathf.Sqrt(1 - life) * fade * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .12f, along));
                // A slender origin fans into a small dilute wake, never a long solid rope.
                float halfWidth = Mathf.Lerp(.045f, .17f, life) * Mathf.Lerp(.2f, 1, Mathf.Sin(along * Mathf.PI));
                for (int side = 0; side < 2; side++)
                {
                    vertices.Add(point); tangents.Add(tangent); uv.Add(new Vector2(along, side));
                    widths.Add(new Vector2(halfWidth, samples[i].time)); colors.Add(new Color(1, 1, 1, alpha));
                }
                if (i > 0) { int j = i * 2; indices.Add(j-2);indices.Add(j);indices.Add(j-1);indices.Add(j-1);indices.Add(j);indices.Add(j+1); }
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetNormals(tangents);mesh.SetUVs(0, uv);mesh.SetUVs(1, widths);mesh.SetColors(colors);mesh.SetTriangles(indices, 0);
            var bounds = mesh.bounds; bounds.Expand(.4f); mesh.bounds = bounds;
            properties.SetFloat("_Age", age);properties.SetFloat("_Seed", seed);ribbon.SetPropertyBlock(properties);
        }
        void RemoveFirst() { for (int i=1;i<count;i++)samples[i-1]=samples[i];count--; }
        void OnDestroy() { if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);} }
    }
}

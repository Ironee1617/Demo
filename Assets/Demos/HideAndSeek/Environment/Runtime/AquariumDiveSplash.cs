using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // R49: the diver's entry splash. One static mesh of camera-facing quads (crown sheets, droplets, lacy foam,
    // soft irregular ripples, a burst of bubbles and a bubble trail behind the diver), all animated in the
    // DiveSplash shader from the effect's age, so a frame costs one property block and no mesh rebuild.
    // Object space: y up, the surface at y = 0, +z the way the diver jumps.
    public sealed class AquariumDiveSplash : MonoBehaviour
    {
        public const int Drops = 48, Foam = 16, Burst = 30, Ripples = 4, Sheets = 20, TrailSlots = 32, TrailPerSlot = 4;
        const float TrailInterval = .045f;
        static Material material;
        static Mesh shared;
        MeshRenderer view;
        MaterialPropertyBlock properties;
        readonly Vector4[] trail = new Vector4[TrailSlots];
        int nextSlot; float lastTrail = -1, lastAge = -100;

        public void Initialize()
        {
            if (material == null)
                material = new Material(Resources.Load<Shader>("Aquarium/Shaders/DiveSplash")) { name = "Dive splash", hideFlags = HideFlags.HideAndDontSave };
            if (shared == null) shared = Build();
            gameObject.AddComponent<MeshFilter>().sharedMesh = shared;
            view = gameObject.AddComponent<MeshRenderer>(); view.sharedMaterial = material;
            view.shadowCastingMode = ShadowCastingMode.Off; view.receiveShadows = false;
            properties = new MaterialPropertyBlock();
            Clear();
        }
        void Clear() { for (int i = 0; i < TrailSlots; i++) trail[i] = new Vector4(0, 0, 0, -10); nextSlot = 0; lastTrail = -1; }

        // seconds since the splash; diver = the diver's body centre in world space (or null once gone).
        public void Present(float seconds, Vector3 surface, Vector3 forward, Vector3? diver)
        {
            if (seconds < lastAge - .05f) Clear();
            lastAge = seconds;
            transform.position = surface;
            transform.rotation = Quaternion.LookRotation(forward.sqrMagnitude > 1e-4f ? forward : Vector3.forward, Vector3.up);
            view.enabled = seconds >= 0 && seconds < 3.2f;
            if (!view.enabled) return;
            if (diver.HasValue && seconds < 1.6f && seconds - lastTrail >= TrailInterval)
            {
                var local = transform.InverseTransformPoint(diver.Value);
                if (local.y < -.1f && local.y > -6f)
                { trail[nextSlot] = new Vector4(local.x, local.y, local.z, seconds); nextSlot = (nextSlot + 1) % TrailSlots; lastTrail = seconds; }
            }
            properties.SetFloat("_Age", seconds);
            properties.SetVectorArray("_Trail", trail);
            view.SetPropertyBlock(properties);
        }

        static float H(int i, int salt) { unchecked { uint n = (uint)(i * 374761393 + salt * 668265263); n = (n ^ (n >> 13)) * 1274126177; return ((n ^ (n >> 16)) & 65535) / 65535f; } }
        static Mesh Build()
        {
            var v = new List<Vector3>(); var c = new List<Vector2>(); var p = new List<Vector4>(); var q = new List<Vector4>(); var tri = new List<int>();
            System.Action<Vector4, Vector4> quad = (a, b) =>
            {
                int s = v.Count;
                for (int k = 0; k < 4; k++) { v.Add(Vector3.zero); c.Add(new Vector2(k == 0 || k == 3 ? -1 : 1, k < 2 ? -1 : 1)); p.Add(a); q.Add(b); }
                tri.Add(s); tri.Add(s + 1); tri.Add(s + 2); tri.Add(s); tri.Add(s + 2); tri.Add(s + 3);
            };
            // Order matters for blending (no sorting): ripples, foam, sheets, then drops and bubbles on top.
            for (int i = 0; i < Ripples; i++)
                quad(new Vector4(3, H(i, 1), .12f + .28f * i + .1f * H(i, 2), 1.4f - .25f * i + .3f * H(i, 3)), new Vector4((H(i, 4) - .5f) * .3f, (H(i, 5) - .5f) * .3f + .1f * i, 0, 0));
            for (int i = 0; i < Foam; i++)
                quad(new Vector4(1, H(i, 11), H(i, 12) * Mathf.PI * 2, .15f + .55f * H(i, 13)), new Vector4(.55f + .5f * H(i, 14), .03f * H(i, 15), 0, 0));
            for (int i = 0; i < Sheets; i++)
            {
                float a0 = i * Mathf.PI * 2 / Sheets, a1 = (i + 1.08f) * Mathf.PI * 2 / Sheets;
                float hmax = .55f + .5f * H(i, 21) + .35f * Mathf.Max(0, Mathf.Cos(a0 - Mathf.PI * .5f));
                quad(new Vector4(4, H(i, 22), a0, a1), new Vector4(.55f + .5f * H(i, 23), hmax, 0, 0));
            }
            for (int i = 0; i < Drops; i++)
            {
                bool plume = i < 9; float ang = plume ? H(i, 31) * Mathf.PI * 2 : (i + .5f * H(i, 32)) * Mathf.PI * 2 / (Drops - 9);
                float up = plume ? 3.4f + 1.5f * H(i, 33) : 2.0f + 1.8f * H(i, 33), sp = plume ? .2f + .3f * H(i, 34) : .7f + 1.1f * H(i, 34);
                float size = (plume ? .05f : .035f) + .05f * H(i, 35) * H(i, 36);
                quad(new Vector4(0, H(i, 37), ang, up), new Vector4(sp, size, .02f + .08f * H(i, 38), 0));
            }
            for (int i = 0; i < Burst; i++)
                quad(new Vector4(2, H(i, 41), H(i, 42) * Mathf.PI * 2, .4f * H(i, 43)), new Vector4(.25f + 1.1f * H(i, 44), .05f + .3f * H(i, 45), 1.3f + .8f * H(i, 46), .02f + .045f * H(i, 47)));
            for (int s = 0; s < TrailSlots; s++)
                for (int k = 0; k < TrailPerSlot; k++)
                {
                    int i = s * TrailPerSlot + k;
                    quad(new Vector4(5, H(i, 51), s, H(i, 52)), new Vector4(H(i, 53) * 2 - 1, H(i, 54) * 2 - 1, .015f + .035f * H(i, 55) * H(i, 56), 1.1f + .9f * H(i, 57)));
                }
            var mesh = new Mesh { name = "Dive splash quads", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(v); mesh.SetUVs(0, c); mesh.SetUVs(1, p); mesh.SetUVs(2, q); mesh.SetTriangles(tri, 0);
            mesh.bounds = new Bounds(new Vector3(0, -1.5f, 1), new Vector3(9, 7, 9));
            return mesh;
        }
    }
}

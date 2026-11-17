using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R73h: each alien ping stands in the water as a glowing 3D arrow in the pinger's alien colour, pointing down at
    // the spot: it drops in with a little bounce, then bobs and turns slowly, while rings spread out under its tip.
    // A ghost of it shows through rocks and the tube. Far pings are drawn larger so they stay easy to spot. Only the
    // aliens see them (as before); the name, distance and off-screen pointer stay on the HUD (PingMarkers).
    public sealed partial class AquariumGame
    {
        private sealed class PingArrow { public GameObject root; public Transform arrow, ring; public Material arrowMat, ringMat; }
        private readonly List<PingArrow> pingArrows = new List<PingArrow>();
        private Mesh pingArrowMesh, pingRingMesh;
        private Shader pingShader;
        public const float PingLife = 5.5f;

        // Update (after the view drew the frame's state): one arrow per live ping, the rest hidden.
        private void UpdatePingArrows()
        {
            var state = session.Connected ? session.State : null;
            var local = state != null ? Local() : null;
            bool show = state != null && local != null && local.team == Team.Hider && state.phase == RoundPhase.Playing && view != null && view.Camera != null;
            int used = 0;
            if (show)
            {
                float now = Time.realtimeSinceStartup;
                var cam = view.Camera.transform.position;
                foreach (var mark in session.Pings)
                {
                    float age = now - mark.shownAt; if (age < 0 || age > PingLife) continue;
                    var p = PingArrowAt(used++);
                    var kind = AquariumAlienKinds.Get(mark.alienKind); var tint = new Color(kind.r, kind.g, kind.b);
                    float fade = Mathf.Clamp01((PingLife - age) / .8f);
                    var spot = new Vector3(mark.at.x, mark.at.y, mark.at.z);
                    float scale = Mathf.Clamp(Vector3.Distance(cam, spot) / 11f, 1f, 3f);
                    // drop in (ease out with a small bounce), then bob
                    float drop = age < .45f ? 1 - Mathf.Sin(age / .45f * Mathf.PI * .5f) : 0;
                    float bounce = age >= .45f && age < .8f ? Mathf.Sin((age - .45f) / .35f * Mathf.PI) * .12f : 0;
                    float bob = .12f * Mathf.Sin(age * 3.2f);
                    float pop = age < .45f ? Mathf.Lerp(.4f, 1, age / .45f) : 1;
                    p.root.SetActive(true);
                    p.arrow.position = spot + Vector3.up * ((.35f + bob - bounce + drop * 2.2f) * scale);
                    p.arrow.rotation = Quaternion.Euler(0, age * 70f, 0);
                    p.arrow.localScale = Vector3.one * scale * pop;
                    p.arrowMat.SetColor("_Color", tint); p.arrowMat.SetFloat("_Fade", fade);
                    // a ring spreading out under the tip, once every 1.1 s
                    float g = Mathf.Repeat(age, 1.1f) / 1.1f;
                    p.ring.position = spot + Vector3.up * .03f;
                    p.ring.localScale = Vector3.one * (.4f + 1.4f * g) * scale;
                    p.ringMat.SetColor("_Color", tint); p.ringMat.SetFloat("_Fade", fade * (1 - g) * (age > .3f ? 1 : 0));
                }
            }
            for (int i = used; i < pingArrows.Count; i++) if (pingArrows[i].root.activeSelf) pingArrows[i].root.SetActive(false);
        }

        private PingArrow PingArrowAt(int index)
        {
            while (pingArrows.Count <= index)
            {
                if (pingShader == null) pingShader = Shader.Find("Aquarium/Ping Arrow");
                if (pingArrowMesh == null) { pingArrowMesh = ArrowMesh(); pingRingMesh = RingMesh(); }
                var a = new PingArrow { root = new GameObject("Alien ping " + pingArrows.Count) };
                a.root.hideFlags = HideFlags.DontSave;
                a.arrow = Part(a.root.transform, "arrow", pingArrowMesh, out a.arrowMat);
                a.ring = Part(a.root.transform, "ring", pingRingMesh, out a.ringMat); a.ringMat.SetFloat("_Ring", 1);
                pingArrows.Add(a);
            }
            return pingArrows[index];
        }
        private Transform Part(Transform parent, string name, Mesh mesh, out Material material)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); material = new Material(pingShader) { hideFlags = HideFlags.DontSave };
            r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return go.transform;
        }

        // A downward arrow, its tip at the origin: a cone head .55 m tall and .3 m wide, a rounded shaft above it.
        private static Mesh ArrowMesh()
        {
            const int sides = 28; var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            System.Action<float, float, float, float> band = (y0, r0, y1, r1) =>
            {
                int b = v.Count; float slope = (r0 - r1) / Mathf.Max(.001f, y1 - y0);
                for (int k = 0; k <= sides; k++)
                {
                    float a = Mathf.PI * 2 * k / sides; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    var normal = (dir + Vector3.up * slope).normalized;
                    v.Add(dir * r0 + Vector3.up * y0); n.Add(normal); v.Add(dir * r1 + Vector3.up * y1); n.Add(normal);
                    if (k < sides) { int i = b + k * 2; t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i + 1); t.Add(i + 3); t.Add(i + 2); }
                }
            };
            System.Action<float, float, bool> cap = (y, r, up) =>
            {
                int c = v.Count; v.Add(Vector3.up * y); n.Add(up ? Vector3.up : Vector3.down);
                for (int k = 0; k <= sides; k++) { float a = Mathf.PI * 2 * k / sides; v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)); n.Add(up ? Vector3.up : Vector3.down); }
                for (int k = 0; k < sides; k++) { if (up) { t.Add(c); t.Add(c + k + 2); t.Add(c + k + 1); } else { t.Add(c); t.Add(c + k + 1); t.Add(c + k + 2); } }
            };
            band(0, .001f, .55f, .3f);          // head
            cap(.55f, .3f, true); cap(.55f, .3f, false);
            band(.55f, .11f, 1.25f, .11f);      // shaft
            band(1.25f, .11f, 1.33f, .05f);     // rounded top
            cap(1.33f, .05f, true);
            var mesh = new Mesh { name = "Ping arrow" }; mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
            // the band winding above faces inward for some slopes: make every triangle face its normal
            var verts = mesh.vertices; var norms = mesh.normals; var tris = mesh.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                var a0 = verts[tris[i]]; var face = Vector3.Cross(verts[tris[i + 1]] - a0, verts[tris[i + 2]] - a0);
                if (Vector3.Dot(face, norms[tris[i]] + norms[tris[i + 1]] + norms[tris[i + 2]]) < 0) { int s = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = s; }
            }
            mesh.triangles = tris; return mesh;
        }
        // A flat ring 1 m across (uv.x 0 inside .. 1 outside), both faces.
        private static Mesh RingMesh()
        {
            const int sides = 48; var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int k = 0; k <= sides; k++)
            {
                float a = Mathf.PI * 2 * k / sides; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * .32f); uv.Add(new Vector2(0, 0)); v.Add(d * .5f); uv.Add(new Vector2(1, 0));
                if (k < sides) { int i = k * 2; t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i + 1); t.Add(i + 2); t.Add(i + 3);
                                 t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i + 1); t.Add(i + 3); t.Add(i + 2); }
            }
            var mesh = new Mesh { name = "Ping ring" }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}

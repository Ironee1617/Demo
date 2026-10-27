using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // Cartoon "X_X" eyes for a dead fish or alien. Each X is a decal moulded onto the actual eye
    // surface: when the eye set is first found, every vertex of the X (and of the fish's white lens
    // patch) is projected onto the rest-pose surface, so the mark hugs the curve instead of standing
    // off it as a flat badge. At run time the mark rides one skinned anchor (bone weights and blend
    // shapes of a real vertex), so it follows the death clip's tumbling and body bends exactly.
    // The alien's lids are shut first and covered by one smooth lid (no seam line), with the X on top.
    public sealed class AquariumDeadEyes
    {
        private sealed class Anchor
        {
            public int vertex;
            public float radius;
            public BoneWeight weight;
            public Vector3 rest, restNormal, restUp;
            public Vector3[] shapeDelta, shapeNormal;
            public Mesh cross, disc, lid;
            public Renderer lidRenderer;
            public Transform mark;
        }
        private sealed class EyeSet { public Anchor[] anchors; public int[] blinkKeys; public int closedKey = -1; public Matrix4x4[] bind; }

        // Fish0: the pupils sit at the centre of each eye disc (mesh space; found from the atlas and geometry).
        private static readonly Vector3[] FishPupils = { new Vector3(.276f, -.034f, .296f), new Vector3(-.277f, -.040f, .294f) };
        private const float FishEyeRadius = .115f;
        // Heights above the moulded surface, in eye radii.
        private const float CrossFront = .06f, CrossBack = .14f, DiscFront = .022f;
        private static readonly Dictionary<Mesh, EyeSet> cache = new Dictionary<Mesh, EyeSet>();
        private static Material ink, white;

        private readonly Transform parent;
        private readonly Dictionary<AquariumRig, Anchor[]> marks = new Dictionary<AquariumRig, Anchor[]>();
        private AquariumRig shown;

        public AquariumDeadEyes(Transform parent) { this.parent = parent; }

        public void Hide()
        {
            if (shown == null) return;
            foreach (var a in marks[shown]) if (a.mark != null) a.mark.gameObject.SetActive(false);
            shown = null;
        }

        public void Present(AquariumRig rig)
        {
            if (rig == null || rig.skin == null || rig.skin.sharedMesh == null) { Hide(); return; }
            if (shown != null && shown != rig) Hide();
            var set = Find(rig);
            if (set.anchors.Length == 0) return;
            if (!marks.TryGetValue(rig, out var anchors))
            {
                anchors = new Anchor[set.anchors.Length];
                for (int i = 0; i < anchors.Length; i++)
                {
                    var a = set.anchors[i];
                    anchors[i] = new Anchor { vertex = a.vertex, radius = a.radius, weight = a.weight, rest = a.rest, restNormal = a.restNormal, restUp = a.restUp, shapeDelta = a.shapeDelta, shapeNormal = a.shapeNormal, cross = a.cross, disc = a.disc, lid = a.lid };
                    anchors[i].mark = MakeMark(anchors[i], LidMaterial(rig));
                }
                marks.Add(rig, anchors);
            }
            shown = rig;
            // Shut the alien's lids completely under its X (the closed key is the last step of the blink ramp).
            if (set.closedKey >= 0)
            {
                foreach (int key in set.blinkKeys) rig.skin.SetBlendShapeWeight(key, 0);
                rig.skin.SetBlendShapeWeight(set.closedKey, 100);
            }
            foreach (var a in anchors)
            {
                if (!Skinned(rig.skin, rig.Bones, set.bind, a, out var point, out var normal, out var up, out float scale)) { a.mark.gameObject.SetActive(false); continue; }
                a.mark.gameObject.SetActive(true);
                a.mark.SetPositionAndRotation(point, Quaternion.LookRotation(normal, up));
                a.mark.localScale = Vector3.one * scale;
                // The lid cover takes the creature's own species tint so it matches the lids around it.
                if (a.lidRenderer != null) { rig.skin.GetPropertyBlock(lidProperties); a.lidRenderer.SetPropertyBlock(lidProperties); }
                // Keep the world size with an arbitrary parent scale.
                var parentScale = a.mark.parent != null ? a.mark.parent.lossyScale.x : 1;
                if (Mathf.Abs(parentScale) > .0001f) a.mark.localScale /= Mathf.Abs(parentScale);
            }
        }

        public void Dispose()
        {
            foreach (var list in marks.Values) foreach (var a in list) if (a.mark != null) Release(a.mark.gameObject);
            marks.Clear(); shown = null;
        }

        private static readonly MaterialPropertyBlock lidProperties = new MaterialPropertyBlock();
        // The palette material of the alien's eyelids (the lid parts live in the mesh's second submesh).
        private static Material LidMaterial(AquariumRig rig)
        {
            var materials = rig.skin.sharedMaterials;
            return materials.Length > 1 ? materials[1] : materials.Length > 0 ? materials[0] : null;
        }

        private Transform MakeMark(Anchor anchor, Material lidMaterial)
        {
            Mesh cross = anchor.cross, disc = anchor.disc;
            if (ink == null)
            {
                ink = new Material(Shader.Find("Standard")) { name = "Dead eye ink", hideFlags = HideFlags.HideAndDontSave, color = new Color(.035f, .03f, .06f) };
                ink.SetFloat("_Glossiness", .35f);
            }
            var go = new GameObject("Dead eye X"); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = cross;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = ink;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            if (disc != null)
            {
                // A white patch moulded onto the lens hides the pupil of an open fish eye.
                if (white == null) { white = new Material(Shader.Find("Standard")) { name = "Dead eye white", hideFlags = HideFlags.HideAndDontSave, color = new Color(.95f, .95f, .97f) }; white.SetFloat("_Glossiness", .55f); }
                var patch = new GameObject("Dead eye white"); patch.transform.SetParent(go.transform, false);
                patch.AddComponent<MeshFilter>().sharedMesh = disc;
                var r = patch.AddComponent<MeshRenderer>(); r.sharedMaterial = white; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
            if (anchor.lid != null && lidMaterial != null)
            {
                // One smooth shut lid over the alien's eye: hides the seam where the two lids meet.
                var cover = new GameObject("Dead eye lid"); cover.transform.SetParent(go.transform, false);
                cover.AddComponent<MeshFilter>().sharedMesh = anchor.lid;
                var r = cover.AddComponent<MeshRenderer>(); r.sharedMaterial = lidMaterial; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                anchor.lidRenderer = r;
            }
            go.SetActive(false); return go.transform;
        }

        // ------------------------------------------------------------------ surface moulding
        // A rest-pose patch of the eye surface around the mark, in the mark's own frame
        // (x right, y up, z along the eye normal; unit = eye radius).
        private sealed class Surface
        {
            public readonly List<Vector3> a = new List<Vector3>(), b = new List<Vector3>(), c = new List<Vector3>();
            public Vector3 origin, right, up, normal; public float radius;
            // Only faces turned toward the viewer count: a culled back layer must not lift the mark.
            public void Add(Vector3 p0, Vector3 p1, Vector3 p2)
            {
                var l0 = Local(p0); var l1 = Local(p1); var l2 = Local(p2);
                if (Vector3.Cross(l1 - l0, l2 - l0).z <= 0) return;
                a.Add(l0); b.Add(l1); c.Add(l2);
            }
            // The highest surface within a small ring: the mark bridges the seam between the shut lids.
            public bool Bridged(float x, float y, out float h)
            {
                bool any = Height(x, y, out h);
                for (int k = 0; k < 8; k++)
                {
                    float angle = k * Mathf.PI / 4;
                    if (Height(x + Mathf.Cos(angle) * .07f, y + Mathf.Sin(angle) * .07f, out float around) && (!any || around > h)) { h = around; any = true; }
                }
                return any;
            }
            private Vector3 Local(Vector3 p) { var d = p - origin; return new Vector3(Vector3.Dot(d, right), Vector3.Dot(d, up), Vector3.Dot(d, normal)) / radius; }
            // Height of the outermost surface above (x, y); false when nothing is under the point.
            public bool Height(float x, float y, out float h)
            {
                h = float.MinValue; bool hit = false;
                for (int i = 0; i < a.Count; i++)
                {
                    var p0 = a[i]; var p1 = b[i]; var p2 = c[i];
                    float d = (p1.x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (p1.y - p0.y);
                    if (Mathf.Abs(d) < 1e-9f) continue;
                    float u = ((x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (y - p0.y)) / d;
                    float v = ((p1.x - p0.x) * (y - p0.y) - (x - p0.x) * (p1.y - p0.y)) / d;
                    if (u < -1e-4f || v < -1e-4f || u + v > 1 + 1e-4f) continue;
                    float z = p0.z + u * (p1.z - p0.z) + v * (p2.z - p0.z);
                    if (z > h) { h = z; hit = true; }
                }
                return hit;
            }
        }

        // Projects each vertex of a flat template (z = height above the surface) onto the surface.
        private static Mesh Mould(List<Vector3> template, List<int> triangles, Surface surface, string name)
        {
            var heights = new float[template.Count]; var known = new bool[template.Count];
            for (int i = 0; i < template.Count; i++) known[i] = surface.Bridged(template[i].x, template[i].y, out heights[i]);
            for (int i = 0; i < template.Count; i++)
            {
                if (known[i]) continue;
                // Off the patch: borrow the height of the nearest moulded vertex.
                float best = float.MaxValue; heights[i] = 0;
                for (int j = 0; j < template.Count; j++)
                {
                    if (!known[j]) continue;
                    float d = new Vector2(template[i].x - template[j].x, template[i].y - template[j].y).sqrMagnitude;
                    if (d < best) { best = d; heights[i] = heights[j]; }
                }
            }
            // Smooth out facet bumps so the ink reads as one clean, glossy stroke.
            var vertices = new Vector3[template.Count];
            for (int i = 0; i < template.Count; i++)
            {
                float sum = 0, weight = 0;
                for (int j = 0; j < template.Count; j++)
                {
                    float d = new Vector2(template[i].x - template[j].x, template[i].y - template[j].y).sqrMagnitude;
                    if (d > .15f * .15f) continue;
                    float w = Mathf.Exp(-d / (2 * .06f * .06f)); sum += heights[j] * w; weight += w;
                }
                float h = Mathf.Max(sum / weight, heights[i] - .03f);
                vertices[i] = new Vector3(template[i].x, template[i].y, template[i].z + h);
            }
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vertices; mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        // Two crossed rounded bars (unit = eye radius), finely tessellated so they can follow the curve.
        // Only the top and the walls are built: the underside is buried in the eye.
        private static void CrossTemplate(List<Vector3> vertices, List<int> triangles)
        {
            const float half = .74f, width = .25f; const int rows = 24, columns = 4;
            for (int bar = 0; bar < 2; bar++)
            {
                var rotation = Quaternion.Euler(0, 0, bar == 0 ? 45 : -45);
                float front = bar == 0 ? CrossFront : CrossFront * .72f;
                int top = vertices.Count;
                for (int r = 0; r <= rows; r++)
                {
                    // Rows bunch up at the rounded ends.
                    float s = -Mathf.Cos(Mathf.PI * r / rows), x = s * half;
                    float end = Mathf.Abs(x) - (half - width), halfWidth = end > 0 ? Mathf.Sqrt(Mathf.Max(0, width * width - end * end)) : width;
                    for (int c = 0; c <= columns; c++)
                    {
                        float y = (-1 + 2f * c / columns) * halfWidth;
                        vertices.Add(rotation * new Vector3(x, y, front));
                    }
                }
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < columns; c++)
                    {
                        int i0 = top + r * (columns + 1) + c, i1 = i0 + 1, i2 = i0 + columns + 1, i3 = i2 + 1;
                        triangles.AddRange(new[] { i0, i2, i1, i1, i2, i3 });
                    }
                // Walls along both long edges (the end rows collapse to points, closing the caps).
                for (int side = 0; side < 2; side++)
                {
                    int c = side == 0 ? 0 : columns, wall = vertices.Count;
                    for (int r = 0; r <= rows; r++) { var p = vertices[top + r * (columns + 1) + c]; vertices.Add(new Vector3(p.x, p.y, -CrossBack)); }
                    for (int r = 0; r < rows; r++)
                    {
                        int t0 = top + r * (columns + 1) + c, t1 = top + (r + 1) * (columns + 1) + c, b0 = wall + r, b1 = wall + r + 1;
                        if (side == 0) triangles.AddRange(new[] { t0, b0, t1, t1, b0, b1 });
                        else triangles.AddRange(new[] { t0, t1, b0, b0, t1, b1 });
                    }
                }
            }
        }

        // A round patch (unit = eye radius) whose rim dips under the surface so it has no visible edge.
        private static void DiscTemplate(List<Vector3> vertices, List<int> triangles)
        {
            const int sides = 40, rings = 7; const float radius = .62f;
            vertices.Add(new Vector3(0, 0, DiscFront));
            for (int ring = 1; ring <= rings; ring++)
            {
                float t = ring / (float)rings, r = radius * t, z = DiscFront - (DiscFront + .03f) * Mathf.Pow(t, 6);
                for (int i = 0; i < sides; i++) { float a = i * Mathf.PI * 2 / sides; vertices.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z)); }
            }
            for (int i = 0; i < sides; i++) triangles.AddRange(new[] { 0, 1 + i, 1 + (i + 1) % sides });
            for (int ring = 1; ring < rings; ring++)
            {
                int a0 = 1 + (ring - 1) * sides, b0 = 1 + ring * sides;
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    triangles.AddRange(new[] { a0 + i, b0 + i, b0 + j, a0 + i, b0 + j, a0 + j });
                }
            }
        }

        // Unity front faces are clockwise seen from the viewer; templates are wound for that.
        private static void Moulded(Anchor a, Surface surface, bool disc)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            CrossTemplate(v, t); a.cross = Mould(v, t, surface, "Dead eye cross");
            if (!disc) return;
            v.Clear(); t.Clear(); DiscTemplate(v, t); a.disc = Mould(v, t, surface, "Dead eye white");
        }

        // The dead alien's eye reads as one smooth shut lid: a dome whose profile is fitted to the upper lid
        // (h = a r^2 + b r^4), lifted just over the highest lid point (the lash roll where the lids meet) and
        // rolled down into the socket at its rim. It is drawn with the lids' own palette skin colour, and the
        // X lies on top of it, so no seam or lash line shows under the X.
        private const float LidCoverRadius = 1.42f, LidRoll = .9f, LidDip = .35f, LidFront = .01f;
        private static readonly Vector2 LidSkinTexel = new Vector2(8.5f / 16, 1 - 3.5f / 4), LidSurface = new Vector2(0, .35f);
        private static void SmoothLid(Anchor a, Surface surface)
        {
            // Least-squares a, b from the upper lid (y > 0 in the mark frame).
            double s22 = 0, s24 = 0, s44 = 0, t2 = 0, t4 = 0;
            foreach (float r in new[] { .3f, .5f, .7f, .9f, 1.1f })
                for (int k = 0; k < 7; k++)
                {
                    float angle = Mathf.Deg2Rad * (25 + 130f * k / 6);
                    if (!surface.Height(r * Mathf.Cos(angle), r * Mathf.Sin(angle), out float h)) continue;
                    double r2 = r * r, r4 = r2 * r2;
                    s22 += r2 * r2; s24 += r2 * r4; s44 += r4 * r4; t2 += r2 * h; t4 += r4 * h;
                }
            double det = s22 * s44 - s24 * s24;
            float qa = det > 1e-12 ? (float)((t2 * s44 - t4 * s24) / det) : -.3f, qb = det > 1e-12 ? (float)((s22 * t4 - s24 * t2) / det) : 0;
            float lift = 0;
            for (float x = -1.3f; x <= 1.3f; x += .05f)
                for (float y = -1.3f; y <= 1.3f; y += .05f)
                {
                    float r2 = x * x + y * y; if (r2 > 1.3f * 1.3f) continue;
                    if (surface.Height(x, y, out float h)) lift = Mathf.Max(lift, h - (qa * r2 + qb * r2 * r2));
                }
            System.Func<float, float> profile = r => qa * r * r + qb * r * r * r * r + lift + LidFront;
            // Lid dome.
            const int rings = 16, sides = 64;
            var vertices = new List<Vector3> { new Vector3(0, 0, profile(0)) }; var triangles = new List<int>();
            for (int ring = 1; ring <= rings; ring++)
            {
                float t = ring / (float)rings, r = LidCoverRadius * t, roll = Mathf.Max(0, (t - LidRoll) / (1 - LidRoll));
                for (int i = 0; i < sides; i++) { float angle = i * Mathf.PI * 2 / sides; vertices.Add(new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, profile(r) - LidDip * roll * roll)); }
            }
            for (int i = 0; i < sides; i++) triangles.AddRange(new[] { 0, 1 + i, 1 + (i + 1) % sides });
            for (int ring = 1; ring < rings; ring++)
            {
                int a0 = 1 + (ring - 1) * sides, b0 = 1 + ring * sides;
                for (int i = 0; i < sides; i++) { int j = (i + 1) % sides; triangles.AddRange(new[] { a0 + i, b0 + i, b0 + j, a0 + i, b0 + j, a0 + j }); }
            }
            var lid = new Mesh { name = "Dead eye lid", hideFlags = HideFlags.HideAndDontSave };
            lid.SetVertices(vertices); lid.SetTriangles(triangles, 0);
            var uv = new Vector2[vertices.Count]; var uv2 = new Vector2[vertices.Count];
            for (int i = 0; i < uv.Length; i++) { uv[i] = LidSkinTexel; uv2[i] = LidSurface; }
            lid.uv = uv; lid.uv2 = uv2; lid.RecalculateNormals(); lid.RecalculateTangents(); lid.RecalculateBounds();
            a.lid = lid;
            // The X lies on the dome.
            var cross = new List<Vector3>(); var crossTriangles = new List<int>();
            CrossTemplate(cross, crossTriangles);
            for (int i = 0; i < cross.Count; i++) { var v = cross[i]; v.z += profile(new Vector2(v.x, v.y).magnitude); cross[i] = v; }
            a.cross = new Mesh { name = "Dead eye cross", hideFlags = HideFlags.HideAndDontSave };
            a.cross.SetVertices(cross); a.cross.SetTriangles(crossTriangles, 0); a.cross.RecalculateNormals(); a.cross.RecalculateBounds();
        }

        private static EyeSet Find(AquariumRig rig)
        {
            var mesh = rig.skin.sharedMesh;
            if (cache.TryGetValue(mesh, out var set)) return set;
            set = new EyeSet { anchors = new Anchor[0], blinkKeys = new int[0] };
            var vertices = mesh.vertices; var normals = mesh.normals; var triangles = mesh.triangles; var weights = mesh.boneWeights;
            var bones = rig.Bones; int eye = -1;
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null && bones[i].name == "Eye") eye = i;
            var found = new List<Anchor>();
            if (eye >= 0)
            {
                var blink = new List<int>();
                for (int k = 0; k < mesh.blendShapeCount; k++)
                {
                    string name = mesh.GetBlendShapeName(k);
                    if (name.StartsWith("Blink")) { blink.Add(k); if (name == "Blink_100") set.closedKey = k; }
                }
                set.blinkKeys = blink.ToArray();
                // Alien: the eyeball follows the Eye bone; its gaze axis runs from the ball's centre to its front-most point.
                var ball = new List<int>(); Vector3 centre = Vector3.zero;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var w = weights[i]; float share = (w.boneIndex0 == eye ? w.weight0 : 0) + (w.boneIndex1 == eye ? w.weight1 : 0) + (w.boneIndex2 == eye ? w.weight2 : 0) + (w.boneIndex3 == eye ? w.weight3 : 0);
                    if (share >= .6f) { ball.Add(i); centre += vertices[i]; }
                }
                if (ball.Count > 0 && set.closedKey >= 0)
                {
                    centre /= ball.Count; int best = ball[0]; float bestZ = float.MinValue, extent = 0;
                    foreach (int i in ball) { if (vertices[i].z > bestZ) { bestZ = vertices[i].z; best = i; } extent = Mathf.Max(extent, Mathf.Abs(vertices[i].x - centre.x)); }
                    var axis = (vertices[best] - centre).normalized; if (axis.sqrMagnitude < .5f) axis = Vector3.forward;
                    var closed = new Vector3[vertices.Length]; mesh.GetBlendShapeFrameVertices(set.closedKey, mesh.GetBlendShapeFrameCount(set.closedKey) - 1, closed, null, null);
                    for (int i = 0; i < closed.Length; i++) closed[i] += vertices[i];
                    // The shut lids: every triangle the closed blink moves.
                    var surface = Frame(vertices[best], axis, extent * .74f);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int i0 = triangles[i], i1 = triangles[i + 1], i2 = triangles[i + 2];
                        if (closed[i0] == vertices[i0] || closed[i1] == vertices[i1] || closed[i2] == vertices[i2]) continue;
                        surface.Add(closed[i0], closed[i1], closed[i2]);
                    }
                    if (surface.Height(0, 0, out float lid))
                    {
                        // Ride the lid vertex nearest the middle of the closed lids.
                        surface.origin += surface.normal * (lid * surface.radius);
                        var moved = new Surface { origin = surface.origin, right = surface.right, up = surface.up, normal = surface.normal, radius = surface.radius };
                        for (int i = 0; i < surface.a.Count; i++) moved.a.Add(surface.a[i] - new Vector3(0, 0, lid));
                        for (int i = 0; i < surface.b.Count; i++) moved.b.Add(surface.b[i] - new Vector3(0, 0, lid));
                        for (int i = 0; i < surface.c.Count; i++) moved.c.Add(surface.c[i] - new Vector3(0, 0, lid));
                        int anchor = -1; float nearest = float.MaxValue;
                        for (int i = 0; i < closed.Length; i++)
                        {
                            if (closed[i] == vertices[i]) continue;
                            float d = (closed[i] - moved.origin).sqrMagnitude;
                            if (d < nearest) { nearest = d; anchor = i; }
                        }
                        var a = new Anchor { vertex = anchor, radius = moved.radius, rest = moved.origin, restNormal = moved.normal, restUp = moved.up };
                        SmoothLid(a, moved); found.Add(a);
                    }
                }
            }
            else if (rig.cartoonKind == "Fish")
            {
                // R22: each painted fish brings its own eyes; Fish0 keeps its measured pupils.
                bool own = rig.eyePupils != null && rig.eyePupils.Length > 0 && rig.eyeRadius > 0;
                var pupils = own ? rig.eyePupils : FishPupils;
                float eyeRadius = own ? rig.eyeRadius : FishEyeRadius;
                for (int e = 0; e < pupils.Length; e++)
                {
                    var pupil = pupils[e];
                    var facing = own && rig.eyeAxes != null && e < rig.eyeAxes.Length ? rig.eyeAxes[e] : new Vector3(Mathf.Sign(pupil.x), 0, .6f);
                    int best = -1; float distance = float.MaxValue;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        float d = (vertices[i] - pupil).sqrMagnitude;
                        if (d < distance && Vector3.Dot(normals[i], facing) > .3f) { distance = d; best = i; }
                    }
                    if (best < 0 || distance >= .0025f) continue;
                    // The eye's own axis: the average normal of the lens around the pupil.
                    Vector3 axis = Vector3.zero;
                    for (int i = 0; i < vertices.Length; i++)
                        if ((vertices[i] - vertices[best]).sqrMagnitude < .05f * .05f && Vector3.Dot(normals[i], normals[best]) > .5f) axis += normals[i];
                    axis = axis.sqrMagnitude > 1e-8f ? axis.normalized : normals[best].normalized;
                    var surface = Frame(pupil, axis, eyeRadius);
                    float reach = eyeRadius * 1.25f;
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        Vector3 p0 = vertices[triangles[i]], p1 = vertices[triangles[i + 1]], p2 = vertices[triangles[i + 2]];
                        var mid = (p0 + p1 + p2) / 3; var off = mid - pupil;
                        if ((off - axis * Vector3.Dot(off, axis)).magnitude > reach || Mathf.Abs(Vector3.Dot(off, axis)) > reach) continue;
                        surface.Add(p0, p1, p2);
                    }
                    if (!surface.Height(0, 0, out float lens)) continue;
                    // Sit the mark on the lens right over the pupil.
                    var moved = new Surface { origin = pupil + axis * (lens * eyeRadius), right = surface.right, up = surface.up, normal = axis, radius = eyeRadius };
                    for (int i = 0; i < surface.a.Count; i++) { moved.a.Add(surface.a[i] - new Vector3(0, 0, lens)); moved.b.Add(surface.b[i] - new Vector3(0, 0, lens)); moved.c.Add(surface.c[i] - new Vector3(0, 0, lens)); }
                    var a = new Anchor { vertex = best, radius = eyeRadius, rest = moved.origin, restNormal = axis, restUp = moved.up };
                    Moulded(a, moved, true); found.Add(a);
                }
            }
            foreach (var a in found)
            {
                a.weight = weights[a.vertex];
                a.shapeDelta = new Vector3[mesh.blendShapeCount]; a.shapeNormal = new Vector3[mesh.blendShapeCount];
            }
            set.bind = mesh.bindposes;
            if (found.Count > 0 && mesh.blendShapeCount > 0)
            {
                var delta = new Vector3[vertices.Length]; var normalDelta = new Vector3[vertices.Length];
                for (int k = 0; k < mesh.blendShapeCount; k++)
                {
                    // The alien mark is already moulded onto the closed lids; only other shapes move it.
                    if (System.Array.IndexOf(set.blinkKeys, k) >= 0) continue;
                    mesh.GetBlendShapeFrameVertices(k, mesh.GetBlendShapeFrameCount(k) - 1, delta, normalDelta, null);
                    foreach (var a in found) { a.shapeDelta[k] = delta[a.vertex]; a.shapeNormal[k] = normalDelta[a.vertex]; }
                }
            }
            set.anchors = found.ToArray();
            cache[mesh] = set; return set;
        }

        private static Surface Frame(Vector3 origin, Vector3 normal, float radius)
        {
            var up = Vector3.ProjectOnPlane(Vector3.up, normal);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.forward, normal);
            up.Normalize();
            // Same handedness as Quaternion.LookRotation(normal, up): x = up × forward.
            var right = Vector3.Cross(up, normal).normalized;
            return new Surface { origin = origin, normal = normal.normalized, up = up, right = right, radius = radius };
        }

        // Unity's linear blend skinning for the mark's frame: blend shapes in mesh space, then bones.
        private static bool Skinned(SkinnedMeshRenderer skin, Transform[] bones, Matrix4x4[] bind, Anchor a, out Vector3 point, out Vector3 normal, out Vector3 up, out float scale)
        {
            var v = a.rest; var n = a.restNormal;
            for (int k = 0; k < a.shapeDelta.Length; k++)
            {
                float w = skin.GetBlendShapeWeight(k) * .01f;
                if (w == 0) continue;
                v += a.shapeDelta[k] * w; n += a.shapeNormal[k] * w;
            }
            var bw = a.weight; var m = new Matrix4x4(); float total = 0;
            Accumulate(bones, bind, bw.boneIndex0, bw.weight0, ref m, ref total);
            Accumulate(bones, bind, bw.boneIndex1, bw.weight1, ref m, ref total);
            Accumulate(bones, bind, bw.boneIndex2, bw.weight2, ref m, ref total);
            Accumulate(bones, bind, bw.boneIndex3, bw.weight3, ref m, ref total);
            point = Vector3.zero; normal = Vector3.forward; up = Vector3.up; scale = 0;
            if (total <= 0) return false;
            point = m.MultiplyPoint3x4(v);
            var worldNormal = m.MultiplyVector(n); var worldUp = m.MultiplyVector(a.restUp);
            scale = worldUp.magnitude * a.radius;
            if (worldNormal.sqrMagnitude < 1e-10f || worldUp.sqrMagnitude < 1e-10f) return false;
            normal = worldNormal.normalized; up = Vector3.ProjectOnPlane(worldUp, normal);
            if (up.sqrMagnitude < 1e-10f) return false;
            up.Normalize(); return true;
        }
        private static void Accumulate(Transform[] bones, Matrix4x4[] bind, int index, float weight, ref Matrix4x4 m, ref float total)
        {
            if (weight <= 0 || index < 0 || index >= bones.Length || bones[index] == null) return;
            var b = bones[index].localToWorldMatrix * bind[index];
            for (int i = 0; i < 16; i++) m[i] += b[i] * weight;
            total += weight;
        }
        private static void Release(Object value) { if (value == null) return; if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
    }
}

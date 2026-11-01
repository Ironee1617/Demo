using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // Cartoon "X_X" eyes for a dead fish or alien. Each X is a decal moulded onto the actual eye
    // surface: when the eye set is first found, every vertex of the X (and of the fish's white lens
    // patch) is projected onto the rest-pose surface, so the mark hugs the curve instead of standing
    // off it as a flat badge. At run time the mark rides one skinned anchor (bone weights and blend
    // shapes of a real vertex), so it follows the death clip's tumbling and body bends exactly.
    // R34: the alien keeps its eye open: its iris (the coloured part and the pupil) is painted over with the
    // eye white while it is dead, and the X lies straight on the eye. No lid or cover plate is added.
    public sealed class AquariumDeadEyes
    {
        private sealed class Anchor
        {
            public int vertex;
            public float radius;
            public BoneWeight weight;
            public Vector3 rest, restNormal, restUp;
            public Vector3[] shapeDelta, shapeNormal;
            public Mesh cross, disc;
            public Transform mark;
        }
        private sealed class EyeSet
        {
            public Anchor[] anchors; public int[] blinkKeys; public int closedKey = -1; public Matrix4x4[] bind;
            // R34 alien: a copy of the mesh whose iris dome is its own (last) submesh, drawn with the blank-eye
            // material, and the atlas spot of the eye white that paints over it.
            public Mesh blank; public Vector2 whiteUV;
        }
        // The original mesh and materials of a rig wearing its blank-eyed copy.
        private sealed class Swap { public Mesh mesh; public Material[] materials; }

        // Fish0: the pupils sit at the centre of each eye disc (mesh space; found from the atlas and geometry).
        private static readonly Vector3[] FishPupils = { new Vector3(.276f, -.034f, .296f), new Vector3(-.277f, -.040f, .294f) };
        private const float FishEyeRadius = .115f;
        // Heights above the moulded surface, in eye radii.
        private const float CrossFront = .06f, CrossBack = .14f, DiscFront = .022f;
        private static readonly Dictionary<Mesh, EyeSet> cache = new Dictionary<Mesh, EyeSet>();
        private static Material ink, white;
        private static Shader blankShader;
        private static readonly Dictionary<Material, Material> blankMaterials = new Dictionary<Material, Material>();
        private readonly Dictionary<AquariumRig, Swap> swaps = new Dictionary<AquariumRig, Swap>();

        private readonly Transform parent;
        private readonly Dictionary<AquariumRig, Anchor[]> marks = new Dictionary<AquariumRig, Anchor[]>();
        private AquariumRig shown;

        public AquariumDeadEyes(Transform parent) { this.parent = parent; }

        public void Hide()
        {
            if (shown != null && marks.TryGetValue(shown, out var shownMarks))
                foreach (var a in shownMarks) if (a.mark != null) a.mark.gameObject.SetActive(false);
            shown = null;
            Restore();
        }

        // Put the rigs' own meshes and materials back (the eye regains its iris).
        private void Restore()
        {
            foreach (var pair in swaps)
            {
                var skin = pair.Key != null ? pair.Key.skin : null;
                if (skin == null) continue;
                skin.sharedMesh = pair.Value.mesh; skin.sharedMaterials = pair.Value.materials;
            }
            swaps.Clear();
        }

        public void Present(AquariumRig rig)
        {
            if (rig == null || rig.skin == null || rig.skin.sharedMesh == null) { Hide(); return; }
            if (shown != null && shown != rig) Hide();
            var set = Find(rig, swaps.TryGetValue(rig, out var worn) ? worn.mesh : rig.skin.sharedMesh);
            if (set.anchors.Length == 0) return;
            if (!marks.TryGetValue(rig, out var anchors))
            {
                anchors = new Anchor[set.anchors.Length];
                for (int i = 0; i < anchors.Length; i++)
                {
                    var a = set.anchors[i];
                    anchors[i] = new Anchor { vertex = a.vertex, radius = a.radius, weight = a.weight, rest = a.rest, restNormal = a.restNormal, restUp = a.restUp, shapeDelta = a.shapeDelta, shapeNormal = a.shapeNormal, cross = a.cross, disc = a.disc };
                    anchors[i].mark = MakeMark(anchors[i]);
                }
                marks.Add(rig, anchors);
            }
            shown = rig;
            if (set.blank != null)
            {
                // R34: the alien's eye stays open and loses its iris: wear the blank-eyed mesh while dead.
                if (!swaps.ContainsKey(rig))
                {
                    var materials = rig.skin.sharedMaterials;
                    var blank = BlankMaterial(materials.Length > 0 ? materials[0] : null, set.whiteUV);
                    if (blank != null)
                    {
                        swaps.Add(rig, new Swap { mesh = rig.skin.sharedMesh, materials = materials });
                        var worn2 = new Material[set.blank.subMeshCount];
                        for (int i = 0; i < worn2.Length; i++) worn2[i] = i < materials.Length && i < set.blank.subMeshCount - 1 ? materials[i] : blank;
                        rig.skin.sharedMesh = set.blank; rig.skin.sharedMaterials = worn2;
                    }
                }
                // Wide open: no blink while dead.
                foreach (int key in set.blinkKeys) rig.skin.SetBlendShapeWeight(key, 0);
            }
            foreach (var a in anchors)
            {
                if (!Skinned(rig.skin, rig.Bones, set.bind, a, out var point, out var normal, out var up, out float scale)) { a.mark.gameObject.SetActive(false); continue; }
                a.mark.gameObject.SetActive(true);
                a.mark.SetPositionAndRotation(point, Quaternion.LookRotation(normal, up));
                a.mark.localScale = Vector3.one * scale;
                // Keep the world size with an arbitrary parent scale.
                var parentScale = a.mark.parent != null ? a.mark.parent.lossyScale.x : 1;
                if (Mathf.Abs(parentScale) > .0001f) a.mark.localScale /= Mathf.Abs(parentScale);
            }
        }

        public void Dispose()
        {
            Restore();
            foreach (var list in marks.Values) foreach (var a in list) if (a.mark != null) Release(a.mark.gameObject);
            marks.Clear(); shown = null;
        }

        // The body material with the blank-eye shader: same atlas and tint, but iris paint turns into the eye white.
        private static Material BlankMaterial(Material body, Vector2 whiteUV)
        {
            if (body == null) return null;
            if (blankMaterials.TryGetValue(body, out var made) && made != null) return made;
            if (blankShader == null) blankShader = Resources.Load<Shader>("AquariumCharacters/DeadEyeWhite");
            if (blankShader == null) return null;
            made = new Material(blankShader) { name = body.name + " (dead eye)", hideFlags = HideFlags.HideAndDontSave };
            made.mainTexture = body.mainTexture;
            if (body.HasProperty("_Tint")) made.SetColor("_Tint", body.GetColor("_Tint"));
            made.SetVector("_WhiteUV", new Vector4(whiteUV.x, whiteUV.y, 0, 0));
            blankMaterials[body] = made; return made;
        }

        private Transform MakeMark(Anchor anchor)
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

        // mesh: the rig's own mesh (not the blank-eyed copy it may be wearing).
        private static EyeSet Find(AquariumRig rig, Mesh mesh)
        {
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
                if (ball.Count > 0)
                {
                    centre /= ball.Count; int best = ball[0]; float bestZ = float.MinValue, extent = 0;
                    foreach (int i in ball) { if (vertices[i].z > bestZ) { bestZ = vertices[i].z; best = i; } extent = Mathf.Max(extent, Mathf.Abs(vertices[i].x - centre.x)); }
                    var axis = (vertices[best] - centre).normalized; if (axis.sqrMagnitude < .5f) axis = Vector3.forward;
                    var gaze = vertices[best];
                    // An alien variant whose iris is rolled away from straight ahead (the sleepy blue one looks down)
                    // carries the base eye's gaze point and axis, so its X sits where the base's does.
                    if (rig.eyePupils != null && rig.eyePupils.Length > 0 && rig.eyeAxes != null && rig.eyeAxes.Length > 0 && rig.eyeAxes[0].sqrMagnitude > .5f)
                    { gaze = rig.eyePupils[0]; axis = rig.eyeAxes[0].normalized; }
                    // R34: the eyeball (fully on the Eye bone) is the iris dome plus the white's inner lip round it.
                    var iris = IrisTriangles(mesh, weights, eye, vertices, out var lip);
                    if (iris.Count > 0)
                    {
                        // The lip goes blank too: a painted lid's lash line runs over it and would be left in pieces.
                        var blankTriangles = new List<int>(iris); blankTriangles.AddRange(lip);
                        set.blank = BlankMesh(mesh, blankTriangles);
                        // The eye white: the top of the lip (painted lids, where a variant has them, are below).
                        var all = mesh.triangles; int top = -1;
                        foreach (int t in lip) for (int k = 0; k < 3; k++) { int i = all[t + k]; if (top < 0 || vertices[i].y > vertices[top].y) top = i; }
                        var uv = mesh.uv; set.whiteUV = top >= 0 && top < uv.Length ? uv[top] : new Vector2(.5f, .5f);
                    }
                    // The open eye as seen: the outermost front-facing surface round the gaze point (the eyeball, or
                    // a variant's own lid where it hangs over it). The X is moulded onto that.
                    var surface = Frame(gaze, axis, extent * .74f);
                    float reach = surface.radius * 1.6f;
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        Vector3 p0 = vertices[triangles[i]], p1 = vertices[triangles[i + 1]], p2 = vertices[triangles[i + 2]];
                        var off = (p0 + p1 + p2) / 3 - gaze; float along = Vector3.Dot(off, axis);
                        if ((off - axis * along).magnitude > reach || along > reach || along < -reach) continue;
                        surface.Add(p0, p1, p2);
                    }
                    if (surface.Height(0, 0, out float front))
                    {
                        var moved = new Surface { origin = surface.origin + surface.normal * (front * surface.radius), right = surface.right, up = surface.up, normal = surface.normal, radius = surface.radius };
                        for (int i = 0; i < surface.a.Count; i++) { moved.a.Add(surface.a[i] - new Vector3(0, 0, front)); moved.b.Add(surface.b[i] - new Vector3(0, 0, front)); moved.c.Add(surface.c[i] - new Vector3(0, 0, front)); }
                        // Ride the eyeball itself: the iris vertex nearest the middle of the X.
                        int anchor = best; float nearest = float.MaxValue;
                        foreach (int t in iris) for (int k = 0; k < 3; k++)
                        {
                            int i = triangles[t + k]; float d = (vertices[i] - moved.origin).sqrMagnitude;
                            if (d < nearest) { nearest = d; anchor = i; }
                        }
                        var a = new Anchor { vertex = anchor, radius = moved.radius, rest = moved.origin, restNormal = moved.normal, restUp = moved.up };
                        Moulded(a, moved, false); found.Add(a);
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
                    // Blinks are held at 0 while the alien is dead (R34: its eye stays open); only other shapes move the mark.
                    if (System.Array.IndexOf(set.blinkKeys, k) >= 0) continue;
                    mesh.GetBlendShapeFrameVertices(k, mesh.GetBlendShapeFrameCount(k) - 1, delta, normalDelta, null);
                    foreach (var a in found) { a.shapeDelta[k] = delta[a.vertex]; a.shapeNormal[k] = normalDelta[a.vertex]; }
                }
            }
            set.anchors = found.ToArray();
            cache[mesh] = set; return set;
        }

        // R34: the iris dome's triangles (offsets into mesh.triangles): of the triangles wholly on the Eye bone,
        // the largest connected piece; the rest (the white's inner lip round the dome) comes back in lip.
        private static List<int> IrisTriangles(Mesh mesh, BoneWeight[] weights, int eye, Vector3[] vertices, out List<int> lip)
        {
            var triangles = mesh.triangles; lip = new List<int>();
            System.Func<int, bool> onEye = i =>
            {
                var w = weights[i];
                return (w.boneIndex0 == eye ? w.weight0 : 0) + (w.boneIndex1 == eye ? w.weight1 : 0) + (w.boneIndex2 == eye ? w.weight2 : 0) + (w.boneIndex3 == eye ? w.weight3 : 0) >= .99f;
            };
            var parent = new Dictionary<int, int>(); var welded = new Dictionary<Vector3Int, int>();
            System.Func<int, int> root = null;
            root = x => { int p = parent[x]; if (p == x) return x; int r = root(p); parent[x] = r; return r; };
            var eyeTriangles = new List<int>();
            for (int t = 0; t < triangles.Length; t += 3)
            {
                if (!onEye(triangles[t]) || !onEye(triangles[t + 1]) || !onEye(triangles[t + 2])) continue;
                eyeTriangles.Add(t);
                for (int k = 0; k < 3; k++)
                {
                    int i = triangles[t + k]; if (parent.ContainsKey(i)) continue;
                    parent[i] = i;
                    // Seams split vertices: the same position is the same point of the surface.
                    var key = new Vector3Int(Mathf.RoundToInt(vertices[i].x * 1e4f), Mathf.RoundToInt(vertices[i].y * 1e4f), Mathf.RoundToInt(vertices[i].z * 1e4f));
                    if (welded.TryGetValue(key, out int other)) parent[root(i)] = root(other); else welded[key] = i;
                }
            }
            foreach (int t in eyeTriangles) { int r0 = root(triangles[t]); parent[root(triangles[t + 1])] = r0; parent[root(triangles[t + 2])] = r0; }
            var pieces = new Dictionary<int, List<int>>();
            foreach (int t in eyeTriangles)
            {
                int r = root(triangles[t]);
                if (!pieces.TryGetValue(r, out var list)) pieces[r] = list = new List<int>();
                list.Add(t);
            }
            List<int> largest = new List<int>();
            foreach (var list in pieces.Values) if (list.Count > largest.Count) largest = list;
            foreach (var list in pieces.Values) if (list != largest) lip.AddRange(list);
            return largest;
        }

        // A copy of the mesh whose iris dome triangles are moved into an extra, last submesh.
        private static Mesh BlankMesh(Mesh mesh, List<int> iris)
        {
            var blank = Object.Instantiate(mesh); blank.name = mesh.name + " (dead eyes)"; blank.hideFlags = HideFlags.HideAndDontSave;
            var all = mesh.triangles; var inIris = new HashSet<int>(iris);
            int count = mesh.subMeshCount, offset = 0;
            var moved = new List<int>(iris.Count * 3);
            var kept = new List<int>[count];
            for (int s = 0; s < count; s++)
            {
                int n = (int)mesh.GetIndexCount(s); kept[s] = new List<int>(n);
                for (int t = 0; t < n; t += 3)
                {
                    int at = offset + t;
                    if (inIris.Contains(at)) { moved.Add(all[at]); moved.Add(all[at + 1]); moved.Add(all[at + 2]); }
                    else { kept[s].Add(all[at]); kept[s].Add(all[at + 1]); kept[s].Add(all[at + 2]); }
                }
                offset += n;
            }
            blank.subMeshCount = count + 1;
            for (int s = 0; s < count; s++) blank.SetTriangles(kept[s], s, false);
            blank.SetTriangles(moved, count, false);
            return blank;
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

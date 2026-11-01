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
    // R35: the dead alien wears one mesh copy built for it: the iris dome and the white's rim round it are
    // smoothed onto a single sphere (no ring where the iris was), and the X is part of that skinned mesh,
    // so it squashes, bulges and sways with the soft head exactly like the eye under it. A variant whose own
    // upper lid hangs over the eye (blue, red) wears the base alien's open lid and eye in that copy, so every
    // dead alien has the same round white eye and a full-size X, with no lid line across it.
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
            // R34 alien: a copy of the mesh whose iris dome is its own submesh, drawn with the blank-eye
            // material, and the atlas spot of the eye white that paints over it. R35: the X is its last submesh.
            public Mesh blank; public Vector2 whiteUV; public int blankSubmesh = -1, inkSubmesh = -1;
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
        // The base alien's own mesh (the purple one): the variants are built from it vertex for vertex.
        private static Mesh baseAlien; private static bool baseAlienLoaded;
        private static readonly Dictionary<Material, Material> blankMaterials = new Dictionary<Material, Material>(), inkMaterials = new Dictionary<Material, Material>();
        private static readonly Color InkColour = new Color(.035f, .03f, .06f);
        private readonly Dictionary<AquariumRig, Swap> swaps = new Dictionary<AquariumRig, Swap>();

        private readonly Transform parent;
        private readonly Dictionary<AquariumRig, Anchor[]> marks = new Dictionary<AquariumRig, Anchor[]>();
        private AquariumRig shown;

        public AquariumDeadEyes(Transform parent) { this.parent = parent; }

        // R48: the eye survey and dead-eye mesh of a rig's mesh (100-300 ms each) are built during the loading screen,
        // not on the frame a fish or alien first dies.
        public static void Prewarm(AquariumRig rig)
        {
            if (rig == null || rig.skin == null || rig.skin.sharedMesh == null) return;
            var set = Find(rig, rig.skin.sharedMesh);
            if (set.blank == null) return;
            var materials = rig.skin.sharedMaterials; var body = materials.Length > 0 ? materials[0] : null;
            DeadEyeMaterial(body, set.whiteUV, false); DeadEyeMaterial(body, set.whiteUV, true);
        }

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
                    var body = materials.Length > 0 ? materials[0] : null;
                    Material blank = DeadEyeMaterial(body, set.whiteUV, false), inked = DeadEyeMaterial(body, set.whiteUV, true);
                    if (blank != null && inked != null)
                    {
                        swaps.Add(rig, new Swap { mesh = rig.skin.sharedMesh, materials = materials });
                        var worn2 = new Material[set.blank.subMeshCount];
                        for (int i = 0; i < worn2.Length; i++) worn2[i] = i == set.inkSubmesh ? inked : i == set.blankSubmesh ? blank : i < materials.Length ? materials[i] : blank;
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

        // The body material with the blank-eye shader: same atlas and tint, but iris paint turns into the eye white;
        // the ink version draws the X (R35: the same shader, so the X also takes the body's ingestion warp).
        private static Material DeadEyeMaterial(Material body, Vector2 whiteUV, bool inked)
        {
            if (body == null) return null;
            var cache = inked ? inkMaterials : blankMaterials;
            if (cache.TryGetValue(body, out var made) && made != null) return made;
            if (blankShader == null) blankShader = Resources.Load<Shader>("AquariumCharacters/DeadEyeWhite");
            if (blankShader == null) return null;
            made = new Material(blankShader) { name = body.name + (inked ? " (dead eye X)" : " (dead eye)"), hideFlags = HideFlags.HideAndDontSave };
            made.mainTexture = body.mainTexture;
            if (body.HasProperty("_Tint")) made.SetColor("_Tint", body.GetColor("_Tint"));
            made.SetVector("_WhiteUV", new Vector4(whiteUV.x, whiteUV.y, 0, 0));
            made.SetColor("_InkColor", InkColour); made.SetFloat("_Ink", inked ? 1 : 0);
            cache[body] = made; return made;
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
            // R35: an alien's X is drawn by its own skinned mesh; its mark is only the eye's frame (for tools and cameras).
            if (cross == null) { go.SetActive(false); return go.transform; }
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
            public bool Bridged(float x, float y, out float h) { return Bridged(x, y, .07f, out h); }
            public bool Bridged(float x, float y, float ring, out float h)
            {
                bool any = Height(x, y, out h);
                for (int k = 0; k < 8; k++)
                {
                    float angle = k * Mathf.PI / 4;
                    if (Height(x + Mathf.Cos(angle) * ring, y + Mathf.Sin(angle) * ring, out float around) && (!any || around > h)) { h = around; any = true; }
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
        // over: never dip below the surface (R35: the alien X rides over a variant's lid edge instead of letting it
        // poke through), bridging a wider ring.
        private static Mesh Mould(List<Vector3> template, List<int> triangles, Surface surface, string name, bool over = false)
        {
            var heights = new float[template.Count]; var known = new bool[template.Count];
            for (int i = 0; i < template.Count; i++) known[i] = surface.Bridged(template[i].x, template[i].y, over ? .16f : .07f, out heights[i]);
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
                float h = Mathf.Max(sum / weight, heights[i] - (over ? 0 : .03f));
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
            var found = new List<Anchor>(); HashSet<int> fromBase = null;
            if (eye >= 0)
            {
                // A variant's sleepy or slanted lid (blue, red) and its rolled iris (blue) take the base alien's open pose.
                fromBase = FromBase(mesh, BaseAlien(), eye, weights, vertices, normals);
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
                        // The eye white: the top of the lip (painted lids, where a variant has them, are below).
                        int top = -1;
                        foreach (int t in lip) for (int k = 0; k < 3; k++) { int i = triangles[t + k]; if (top < 0 || vertices[i].y > vertices[top].y) top = i; }
                        var uv = mesh.uv; set.whiteUV = top >= 0 && top < uv.Length ? uv[top] : new Vector2(.5f, .5f);
                        // R35: one mesh for the dead eye: smooth white ball and the X skinned onto it.
                        set.blank = DeadEyeMesh(mesh, weights, eye, vertices, normals, triangles, gaze, axis, extent * .74f, iris, lip, set.whiteUV, fromBase, out var marker, out set.blankSubmesh, out set.inkSubmesh);
                        if (marker != null) found.Add(marker);
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
                    ShapeFrame(mesh, k, mesh.GetBlendShapeFrameCount(k) - 1, delta, normalDelta, null, fromBase);
                    foreach (var a in found) { a.shapeDelta[k] = delta[a.vertex]; a.shapeNormal[k] = normalDelta[a.vertex]; }
                }
            }
            set.anchors = found.ToArray();
            cache[mesh] = set; return set;
        }

        private static Mesh BaseAlien()
        {
            if (!baseAlienLoaded)
            {
                baseAlienLoaded = true;
                var prefab = Resources.Load<GameObject>(AquariumAlienKinds.Get(0).prefab);
                var skin = prefab != null ? prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
                baseAlien = skin != null ? skin.sharedMesh : null;
            }
            return baseAlien;
        }

        // R35: the points where a variant's eye differs from the base alien it was built from, put back to the base's
        // rest pose (positions and normals, in place). Two kinds:
        //  * its lid shell where it hangs over the eye (blue's sleepy lid, red's slanted one): Blink_100 shuts it into
        //    the very same closed lid as the base's, so a point that closes onto the base's closed point is that lid point;
        //  * its iris dome where it is rolled (blue looks down): on the Eye bone, same atlas spot.
        // With the base's open lid tucked back into the head, the dead eye is the same open ball for every alien.
        private static HashSet<int> FromBase(Mesh mesh, Mesh source, int eye, BoneWeight[] weights, Vector3[] vertices, Vector3[] normals)
        {
            var taken = new HashSet<int>();
            if (source == null || source == mesh || source.vertexCount > mesh.vertexCount) return taken;
            int closed = mesh.GetBlendShapeIndex("Blink_100"), sourceClosed = source.GetBlendShapeIndex("Blink_100");
            if (closed < 0 || sourceClosed < 0) return taken;
            int n = source.vertexCount;
            var shut = new Vector3[mesh.vertexCount]; var sourceShut = new Vector3[n];
            mesh.GetBlendShapeFrameVertices(closed, mesh.GetBlendShapeFrameCount(closed) - 1, shut, null, null);
            source.GetBlendShapeFrameVertices(sourceClosed, source.GetBlendShapeFrameCount(sourceClosed) - 1, sourceShut, null, null);
            var rest = source.vertices; var restNormals = source.normals; var uv = mesh.uv; var sourceUV = source.uv; var sourceWeights = source.boneWeights;
            if (rest.Length != n || restNormals.Length != n || sourceWeights.Length != n) return taken;
            float OnEye(BoneWeight w) => (w.boneIndex0 == eye ? w.weight0 : 0) + (w.boneIndex1 == eye ? w.weight1 : 0) + (w.boneIndex2 == eye ? w.weight2 : 0) + (w.boneIndex3 == eye ? w.weight3 : 0);
            for (int i = 0; i < n; i++)
            {
                if ((vertices[i] - rest[i]).sqrMagnitude < 1e-10f) continue;
                bool lid = shut[i].sqrMagnitude > 1e-8f && (vertices[i] + shut[i] - (rest[i] + sourceShut[i])).sqrMagnitude < 1e-8f;
                bool iris = i < uv.Length && i < sourceUV.Length && (uv[i] - sourceUV[i]).sqrMagnitude < 1e-10f && OnEye(weights[i]) >= .99f && OnEye(sourceWeights[i]) >= .99f;
                if (!lid && !iris) continue;
                vertices[i] = rest[i]; normals[i] = restNormals[i]; taken.Add(i);
            }
            return taken;
        }

        // A blend-shape frame of the mesh, with the base alien's own deltas at the points that took its rest pose.
        private static void ShapeFrame(Mesh mesh, int k, int f, Vector3[] dv, Vector3[] dn, Vector3[] dt, HashSet<int> fromBase)
        {
            mesh.GetBlendShapeFrameVertices(k, f, dv, dn, dt);
            if (fromBase == null || fromBase.Count == 0) return;
            var source = BaseAlien(); int ks = source != null ? source.GetBlendShapeIndex(mesh.GetBlendShapeName(k)) : -1;
            if (ks < 0 || f >= source.GetBlendShapeFrameCount(ks)) return;
            int n = source.vertexCount;
            Vector3[] sv = new Vector3[n], sn = dn != null ? new Vector3[n] : null, st = dt != null ? new Vector3[n] : null;
            source.GetBlendShapeFrameVertices(ks, f, sv, sn, st);
            foreach (int i in fromBase) { dv[i] = sv[i]; if (dn != null) dn[i] = sn[i]; if (dt != null) dt[i] = st[i]; }
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

        // R35: the dead alien's own mesh. Copies the body mesh and
        //  * smooths the iris dome and the white's rim round it onto one sphere fitted to the dome and the white
        //    just outside the rim (so no ring is left where the iris was), with that sphere's normals; near the
        //    rim's outer edge they take on the bone weights of the white next to them, so the seam stays shut
        //    when the soft head squashes;
        //  * moves those triangles, and the white round them out to the socket's crease, into their own submesh
        //    (the blank-eye material: one flat eye white);
        //  * adds the X as a last submesh, each vertex skinned (bones and blend shapes) like the surface under it.
        // The X is moulded onto the outermost surface round the gaze point (the smoothed eye, or a variant's own lid
        // where it hangs over it). marker carries the eye's frame for the "Dead eye X" transform.
        private static Mesh DeadEyeMesh(Mesh mesh, BoneWeight[] weights, int eye, Vector3[] vertices, Vector3[] normals, int[] triangles,
            Vector3 gaze, Vector3 axis, float radius, List<int> iris, List<int> lip, Vector2 whiteUV, HashSet<int> fromBase, out Anchor marker, out int blankSubmesh, out int inkSubmesh)
        {
            marker = null; blankSubmesh = inkSubmesh = -1;
            int n = vertices.Length;
            float Share(int i)
            {
                var w = weights[i];
                return (w.boneIndex0 == eye ? w.weight0 : 0) + (w.boneIndex1 == eye ? w.weight1 : 0) + (w.boneIndex2 == eye ? w.weight2 : 0) + (w.boneIndex3 == eye ? w.weight3 : 0);
            }
            float Lateral(Vector3 p) { var d = p - gaze; return (d - axis * Vector3.Dot(d, axis)).magnitude; }
            var irisVertices = new HashSet<int>(); var eyeVertices = new HashSet<int>();
            foreach (int t in iris) for (int k = 0; k < 3; k++) { irisVertices.Add(triangles[t + k]); eyeVertices.Add(triangles[t + k]); }
            foreach (int t in lip) for (int k = 0; k < 3; k++) eyeVertices.Add(triangles[t + k]);
            float domeEdge = 0, rimEdge = 0;
            foreach (int i in irisVertices) if (Vector3.Dot(normals[i], axis) > .2f) domeEdge = Mathf.Max(domeEdge, Lateral(vertices[i]));
            foreach (int i in eyeVertices) if (!irisVertices.Contains(i)) rimEdge = Mathf.Max(rimEdge, Lateral(vertices[i]));
            if (rimEdge <= domeEdge) rimEdge = domeEdge * 1.2f;
            // The white just outside the rim: partly on the Eye bone, facing forward.
            var white = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (eyeVertices.Contains(i) || Vector3.Dot(normals[i], axis) < .2f) continue;
                float s = Share(i), r = Lateral(vertices[i]);
                if (s >= .4f && s < .99f && r >= rimEdge * .98f && r <= rimEdge * 1.35f && Vector3.Dot(vertices[i] - gaze, axis) > -rimEdge) white.Add(i);
            }
            // Sphere through the dome and that white.
            var fit = new List<Vector3>();
            foreach (int i in irisVertices) if (Vector3.Dot(normals[i], axis) > .2f) fit.Add(vertices[i]);
            foreach (int i in white) fit.Add(vertices[i]);
            bool sphere = FitSphere(fit, out var centre, out float sphereRadius) && sphereRadius > domeEdge && sphereRadius < radius * 12;
            var positions = (Vector3[])vertices.Clone(); var smooth = (Vector3[])normals.Clone(); var skin = (BoneWeight[])weights.Clone();
            // Seams split vertices: anything sitting exactly on an eyeball point (a neighbour's copy across a UV seam)
            // moves with it, or the smoothed eye would tear open there.
            var moving = new HashSet<int>(eyeVertices);
            if (sphere)
            {
                Vector3Int Key(Vector3 p) { return new Vector3Int(Mathf.RoundToInt(p.x * 1e4f), Mathf.RoundToInt(p.y * 1e4f), Mathf.RoundToInt(p.z * 1e4f)); }
                var eyePoints = new HashSet<Vector3Int>(); foreach (int i in eyeVertices) eyePoints.Add(Key(vertices[i]));
                for (int i = 0; i < n; i++) if (!moving.Contains(i) && Lateral(vertices[i]) <= rimEdge * 1.05f && eyePoints.Contains(Key(vertices[i]))) moving.Add(i);
            }
            if (sphere)
                foreach (int i in moving)
                {
                    var d = vertices[i] - centre; if (d.sqrMagnitude < 1e-12f) continue;
                    positions[i] = centre + d.normalized * sphereRadius; smooth[i] = d.normalized;
                    if (white.Count == 0) continue;
                    float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(domeEdge, rimEdge, Lateral(positions[i])));
                    if (t <= 0) continue;
                    int near = white[0]; float best = float.MaxValue;
                    foreach (int j in white) { float dd = (vertices[j] - positions[i]).sqrMagnitude; if (dd < best) { best = dd; near = j; } }
                    skin[i] = Blend(weights[i], weights[near], t);
                }
            // The surface the X lies on, from the smoothed eye and everything else in front round the gaze point,
            // and the smoothed eye alone (to find a variant's own lid hanging over it).
            var surface = Frame(gaze, axis, radius); var ball = Frame(gaze, axis, radius);
            var inEyeTriangles = new HashSet<int>(iris); foreach (int t in lip) inEyeTriangles.Add(t);
            float reach = radius * 1.6f;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i0 = triangles[i], i1 = triangles[i + 1], i2 = triangles[i + 2];
                Vector3 p0 = positions[i0], p1 = positions[i1], p2 = positions[i2];
                var off = (p0 + p1 + p2) / 3 - gaze; float along = Vector3.Dot(off, axis);
                if ((off - axis * along).magnitude > reach || along > reach || along < -reach) continue;
                surface.Add(p0, p1, p2);
                if (inEyeTriangles.Contains(i)) ball.Add(p0, p1, p2);
            }
            // R35: should a lid still hang over the eye (a variant not built from the base alien), the X goes into the
            // open part below it (lowered and, if it must, a little smaller) instead of folding over the lid's edge.
            float lift = 0, shrink = 1;
            {
                float lidEdge = float.MaxValue;
                foreach (float x in new[] { -.45f, 0f, .45f })
                {
                    float edge = float.MaxValue;
                    for (float y = 1.3f; y >= -1.3f; y -= .02f)
                    {
                        bool onBall = ball.Height(x, y, out float hb), any = surface.Height(x, y, out float hs);
                        if (!onBall || !any) continue;
                        if (hs - hb > .06f) edge = y; else if (edge < float.MaxValue) break;
                    }
                    if (edge < float.MaxValue) lidEdge = Mathf.Min(lidEdge, edge);
                }
                const float CrossHalf = .78f;
                if (lidEdge < CrossHalf + .1f)
                {
                    float top = lidEdge - .1f, bottom = -1.15f;
                    shrink = Mathf.Clamp((top - bottom) / (2 * CrossHalf), .55f, 1f);
                    lift = top - CrossHalf * shrink;
                }
            }
            Mesh cross = null; Vector3[] crossPositions = null, crossNormals = null; int[] crossSource = null;
            if (surface.Height(0, 0, out float front))
            {
                var moved = new Surface { origin = surface.origin + surface.normal * (front * surface.radius), right = surface.right, up = surface.up, normal = surface.normal, radius = surface.radius };
                for (int i = 0; i < surface.a.Count; i++) { moved.a.Add(surface.a[i] - new Vector3(0, 0, front)); moved.b.Add(surface.b[i] - new Vector3(0, 0, front)); moved.c.Add(surface.c[i] - new Vector3(0, 0, front)); }
                var templ = new List<Vector3>(); var tris = new List<int>();
                CrossTemplate(templ, tris);
                for (int i = 0; i < templ.Count; i++) templ[i] = new Vector3(templ[i].x * shrink, templ[i].y * shrink + lift, templ[i].z * shrink);
                cross = Mould(templ, tris, moved, "Dead eye cross", true);
                var local = cross.vertices; var localNormals = cross.normals;
                crossPositions = new Vector3[local.Length]; crossNormals = new Vector3[local.Length]; crossSource = new int[local.Length];
                for (int i = 0; i < local.Length; i++)
                {
                    var l = local[i];
                    crossPositions[i] = moved.origin + (moved.right * l.x + moved.up * l.y + moved.normal * l.z) * moved.radius;
                    var ln = localNormals[i]; crossNormals[i] = (moved.right * ln.x + moved.up * ln.y + moved.normal * ln.z).normalized;
                }
                // The whole X is skinned like the middle of the eye (the iris point under its centre): it moves as one
                // piece with the eyeball. Per-vertex skinning would tear it where it crosses a variant's own lid.
                int anchor = 0; float nearest = float.MaxValue;
                foreach (int j in irisVertices) { float d = (positions[j] - moved.origin).sqrMagnitude; if (d < nearest) { nearest = d; anchor = j; } }
                for (int i = 0; i < crossSource.Length; i++) crossSource[i] = anchor;
                // The mark (for tools and cameras) sits at the middle of the X.
                float middle = moved.Height(0, lift, out float h0) ? h0 : 0;
                marker = new Anchor { vertex = anchor, radius = moved.radius * shrink, rest = moved.origin + (moved.up * lift + moved.normal * middle) * moved.radius, restNormal = moved.normal, restUp = moved.up };
            }
            int m = crossPositions != null ? crossPositions.Length : 0;
            // Assemble the copy: every channel the body has, plus the X's vertices.
            var dead = new Mesh { name = mesh.name + " (dead eyes)", hideFlags = HideFlags.HideAndDontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            var allPositions = new List<Vector3>(positions); var allNormals = new List<Vector3>(smooth);
            for (int i = 0; i < m; i++) { allPositions.Add(crossPositions[i]); allNormals.Add(crossNormals[i]); }
            dead.SetVertices(allPositions); dead.SetNormals(allNormals);
            var tangents = mesh.tangents;
            if (tangents != null && tangents.Length == n)
            {
                var all = new List<Vector4>(tangents);
                for (int i = 0; i < m; i++) { var tg = Vector3.Cross(Vector3.up, crossNormals[i]); if (tg.sqrMagnitude < 1e-6f) tg = Vector3.right; tg.Normalize(); all.Add(new Vector4(tg.x, tg.y, tg.z, 1)); }
                dead.SetTangents(all);
            }
            for (int channel = 0; channel < 8; channel++)
            {
                if (!mesh.HasVertexAttribute((UnityEngine.Rendering.VertexAttribute)((int)UnityEngine.Rendering.VertexAttribute.TexCoord0 + channel))) continue;
                var uvs = new List<Vector4>(); mesh.GetUVs(channel, uvs);
                // uv: the eye white texel (unused by the ink); uv2: no metal, a soft gloss like the old ink.
                for (int i = 0; i < m; i++) uvs.Add(channel == 0 ? new Vector4(whiteUV.x, whiteUV.y, 0, 0) : channel == 1 ? new Vector4(0, .35f, 0, 0) : Vector4.zero);
                dead.SetUVs(channel, uvs);
            }
            var colours = mesh.colors;
            if (colours != null && colours.Length == n) { var all = new List<Color>(colours); for (int i = 0; i < m; i++) all.Add(Color.white); dead.SetColors(all); }
            var allWeights = new BoneWeight[n + m];
            System.Array.Copy(skin, allWeights, n);
            for (int i = 0; i < m; i++) allWeights[n + i] = skin[crossSource[i]];
            dead.boneWeights = allWeights; dead.bindposes = mesh.bindposes;
            // Submeshes: the body's (without the eye), the eye, the X.
            var inEye = new HashSet<int>(iris); foreach (int t in lip) inEye.Add(t);
            int count = mesh.subMeshCount, offset = 0;
            // The eye takes the white round the rim too, out to the socket's crease (its points still mostly on the Eye
            // bone): the blank material paints all of it one flat white, so neither the iris's edge nor a variant's
            // painted lid (lash line, shade) is left as a ring in the white.
            int irisSubmesh = -1;
            for (int s = 0, at = 0; s < count && iris.Count > 0; at += (int)mesh.GetIndexCount(s), s++)
                if (iris[0] >= at && iris[0] < at + (int)mesh.GetIndexCount(s)) { irisSubmesh = s; break; }
            bool OnWhite(int i) => eyeVertices.Contains(i) || Share(i) >= .4f && Vector3.Dot(normals[i], axis) > .2f && Lateral(vertices[i]) <= rimEdge * 1.35f;
            var eyeTriangles = new List<int>(); var kept = new List<int>[count];
            for (int s = 0; s < count; s++)
            {
                int indices = (int)mesh.GetIndexCount(s); kept[s] = new List<int>(indices);
                for (int t = 0; t < indices; t += 3)
                {
                    int at = offset + t;
                    bool whiteTriangle = s == irisSubmesh && OnWhite(triangles[at]) && OnWhite(triangles[at + 1]) && OnWhite(triangles[at + 2]);
                    var list = inEye.Contains(at) || whiteTriangle ? eyeTriangles : kept[s];
                    list.Add(triangles[at]); list.Add(triangles[at + 1]); list.Add(triangles[at + 2]);
                }
                offset += indices;
            }
            dead.subMeshCount = count + (m > 0 ? 2 : 1);
            for (int s = 0; s < count; s++) dead.SetTriangles(kept[s], s, false);
            blankSubmesh = count; dead.SetTriangles(eyeTriangles, count, false);
            if (m > 0)
            {
                var crossTriangles = new List<int>(cross.triangles); for (int i = 0; i < crossTriangles.Count; i++) crossTriangles[i] += n;
                inkSubmesh = count + 1; dead.SetTriangles(crossTriangles, inkSubmesh, false);
            }
            // Blend shapes: the eye keeps its own (relative) deltas; each X vertex those of the point under it.
            var dv = new Vector3[n]; var dn = new Vector3[n]; var dt = new Vector3[n];
            for (int k = 0; k < mesh.blendShapeCount; k++)
            {
                string name = mesh.GetBlendShapeName(k);
                for (int f = 0; f < mesh.GetBlendShapeFrameCount(k); f++)
                {
                    ShapeFrame(mesh, k, f, dv, dn, dt, fromBase);
                    var v2 = new Vector3[n + m]; var n2 = new Vector3[n + m]; var t2 = new Vector3[n + m];
                    System.Array.Copy(dv, v2, n); System.Array.Copy(dn, n2, n); System.Array.Copy(dt, t2, n);
                    for (int i = 0; i < m; i++) { v2[n + i] = dv[crossSource[i]]; n2[n + i] = dn[crossSource[i]]; t2[n + i] = dt[crossSource[i]]; }
                    dead.AddBlendShapeFrame(name, mesh.GetBlendShapeFrameWeight(k, f), v2, n2, t2);
                }
            }
            var bounds = mesh.bounds; foreach (var p in allPositions) bounds.Encapsulate(p); dead.bounds = bounds;
            if (cross != null) Release(cross);
            return dead;
        }

        // Least-squares sphere through the points: |p - c|^2 = r^2, solved linearly for c and r^2 - |c|^2.
        private static bool FitSphere(List<Vector3> points, out Vector3 centre, out float radius)
        {
            centre = Vector3.zero; radius = 0;
            if (points.Count < 8) return false;
            var a = new double[4, 5];
            foreach (var p in points)
            {
                double[] row = { 2 * p.x, 2 * p.y, 2 * p.z, 1 }; double rhs = (double)p.x * p.x + (double)p.y * p.y + (double)p.z * p.z;
                for (int i = 0; i < 4; i++) { for (int j = 0; j < 4; j++) a[i, j] += row[i] * row[j]; a[i, 4] += row[i] * rhs; }
            }
            for (int c = 0; c < 4; c++)
            {
                int pivot = c; for (int r = c + 1; r < 4; r++) if (System.Math.Abs(a[r, c]) > System.Math.Abs(a[pivot, c])) pivot = r;
                if (System.Math.Abs(a[pivot, c]) < 1e-12) return false;
                if (pivot != c) for (int j = 0; j < 5; j++) { var tmp = a[c, j]; a[c, j] = a[pivot, j]; a[pivot, j] = tmp; }
                for (int r = 0; r < 4; r++)
                {
                    if (r == c) continue; double f = a[r, c] / a[c, c];
                    for (int j = c; j < 5; j++) a[r, j] -= f * a[c, j];
                }
            }
            double cx = a[0, 4] / a[0, 0], cy = a[1, 4] / a[1, 1], cz = a[2, 4] / a[2, 2], d = a[3, 4] / a[3, 3];
            double r2 = d + cx * cx + cy * cy + cz * cz; if (r2 <= 0) return false;
            centre = new Vector3((float)cx, (float)cy, (float)cz); radius = (float)System.Math.Sqrt(r2); return true;
        }

        // (1 - t) of one vertex's skinning plus t of another's, kept to the four largest bones.
        private static BoneWeight Blend(BoneWeight a, BoneWeight b, float t)
        {
            var sum = new Dictionary<int, float>();
            void Add(int bone, float w) { if (w <= 0) return; sum.TryGetValue(bone, out float o); sum[bone] = o + w; }
            Add(a.boneIndex0, a.weight0 * (1 - t)); Add(a.boneIndex1, a.weight1 * (1 - t)); Add(a.boneIndex2, a.weight2 * (1 - t)); Add(a.boneIndex3, a.weight3 * (1 - t));
            Add(b.boneIndex0, b.weight0 * t); Add(b.boneIndex1, b.weight1 * t); Add(b.boneIndex2, b.weight2 * t); Add(b.boneIndex3, b.weight3 * t);
            var list = new List<KeyValuePair<int, float>>(sum); list.Sort((x, y) => y.Value.CompareTo(x.Value));
            var result = new BoneWeight(); float total = 0;
            for (int i = 0; i < list.Count && i < 4; i++) total += list[i].Value;
            if (total <= 0) return a;
            if (list.Count > 0) { result.boneIndex0 = list[0].Key; result.weight0 = list[0].Value / total; }
            if (list.Count > 1) { result.boneIndex1 = list[1].Key; result.weight1 = list[1].Value / total; }
            if (list.Count > 2) { result.boneIndex2 = list[2].Key; result.weight2 = list[2].Value / total; }
            if (list.Count > 3) { result.boneIndex3 = list[3].Key; result.weight3 = list[3].Value / total; }
            return result;
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

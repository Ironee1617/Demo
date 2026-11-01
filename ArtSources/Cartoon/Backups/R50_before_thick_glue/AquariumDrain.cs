using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R27: the return drain's moving parts. The host owns the state (screw progress, lid slide); this only
    // poses the authored lid and six screws: a screw turns and rises as it is backed out and (R28) pops out of
    // its hole and is gone once it is free; the heavy lid scrapes sideways off the opening onto the sand.
    public sealed class AquariumDrain : MonoBehaviour
    {
        public Transform lid, opening, pipeExit;
        public Transform[] screws = new Transform[6];
        public float openingDiameter = AquariumDrainLayout.OpeningRadius * 2;
        // Turns to back a screw out, and how far it rises (prefab units) while it does.
        public const float ScrewTurns = 9f, ScrewRise = .09f, PopRise = .07f;
        // R34: a screw keeps its full size (easy to see and aim at) until it is completely out; only then does it
        // pop up and shrink away, over this long.
        public const float VanishSeconds = .35f;
        public bool IsOpen { get; private set; }
        private bool poseStored;
        private Vector3 lidRest;
        private Quaternion lidRotation;
        private Vector3[] screwRest, screwScale;
        private Quaternion[] screwRotation;

        private void Store()
        {
            if (poseStored) return;
            lidRest = lid.localPosition; lidRotation = lid.localRotation;
            screwRest = new Vector3[screws.Length]; screwScale = new Vector3[screws.Length]; screwRotation = new Quaternion[screws.Length];
            for (int i = 0; i < screws.Length; i++) { screwRest[i] = screws[i].localPosition; screwScale[i] = screws[i].localScale; screwRotation[i] = screws[i].localRotation; }
            poseStored = true;
        }

        // Authoring previews: everything sealed, or screws out and the lid fully aside.
        public void SetOpen(bool open)
        {
            var s = new float[screws.Length]; for (int i = 0; i < s.Length; i++) s[i] = open ? 1 : 0;
            Pose(s, open ? 1 : 0, 90, 0, 0);
        }

        // screwOut: 0..1 per screw; screwGone (optional): 0 while a screw is in, 0..1 while a freed one pops away,
        // 1 once it has vanished (without it a freed screw is simply gone); lidSlide: 0..1; slideLocalYaw: slide direction in the drain's own frame
        // (degrees); scrape: 0..1 while the lid is being dragged (a little judder).
        public void Pose(float[] screwOut, float lidSlide, float slideLocalYaw, float time, float scrape, float[] screwGone = null)
        {
            Store();
            float u = Mathf.Clamp01(lidSlide), e = u * u * (3 - 2 * u);
            IsOpen = u >= 1;
            float a = slideLocalYaw * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
            // Prefab units: the instance is drawn at AquariumDrainLayout.ModelScale.
            float distance = AquariumDrainLayout.SlideDistance / AquariumDrainLayout.ModelScale;
            // Once the lid's centre passes the rim its far edge drops onto the sand.
            float tip = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, 1, u));
            var axis = Vector3.Cross(Vector3.up, dir);
            var tilt = Quaternion.AngleAxis(6 * tip, axis);
            if ((tilt * dir).y > 0) tilt = Quaternion.AngleAxis(-6 * tip, axis);
            float judder = scrape * (Mathf.Sin(time * 47) * .004f + Mathf.Sin(time * 29 + 1.3f) * .003f);
            var offset = dir * (e * distance) + Vector3.up * (.012f * Mathf.Sin(Mathf.PI * Mathf.Min(1, u * 1.6f)) - .05f * tip + judder);
            lid.localPosition = lidRest + offset; lid.localRotation = tilt * lidRotation;
            for (int i = 0; i < screws.Length; i++)
            {
                float p = screwOut == null || i >= screwOut.Length ? 0 : Mathf.Clamp01(screwOut[i]);
                if (u > 0) p = 1;
                // R34: full size while it turns; once it is free it hops up and shrinks away, then it is gone.
                float v = u > 0 ? 1 : screwGone != null && i < screwGone.Length ? Mathf.Clamp01(screwGone[i]) : p >= .999f ? 1 : 0;
                bool gone = v >= 1;
                if (screws[i].gameObject.activeSelf == gone) screws[i].gameObject.SetActive(!gone);
                if (gone) continue;
                float pop = Mathf.SmoothStep(0, 1, v), hop = 1 - (1 - v) * (1 - v);
                float rise = ScrewRise * p + PopRise * hop;
                var spin = Quaternion.Euler(0, -p * ScrewTurns * 360f, 0);
                screws[i].localPosition = lidRest + offset + tilt * (screwRest[i] - lidRest + Vector3.up * rise);
                screws[i].localRotation = tilt * screwRotation[i] * spin;
                screws[i].localScale = screwScale[i] * (1 - pop);
            }
        }
    }

    // R27: the drain part under the crosshair (screw 0-5, AquariumModel.DrainLidPart, DrainHolePart).
    public sealed class DrainTarget
    {
        public int drain, part;
        public Vector3 point;
        public bool usable;
        public string reason = "";
    }

    public sealed class AquariumDrainPresentation
    {
        private readonly Transform parent;
        private GameObject group;
        private AquariumDrainPlacement[] last;
        private AquariumDrain[] instances = new AquariumDrain[0];
        private float[][] shownScrews = new float[0][], goneScrews = new float[0][];
        private float[] shownLid = new float[0], scrape = new float[0];
        private readonly Vector4[] apertures = new Vector4[4];
        private Material outline;
        private Renderer highlighted;
        private GameObject highlightShell;
        // R29: x-ray rims round the screws near the local alien: they show through its own body (and
        // anything else), cyan for the screws in reach of the eye, green (R31, was yellow) for the one under the crosshair.
        public const float RimRange = 5f, RimFade = 1.5f;
        private static readonly Color RimIdle = new Color(.6f, 1f, 1f, 1f), RimTarget = new Color(.45f, 1f, .35f, 1f);
        private Material xray;
        private MaterialPropertyBlock rimBlock;
        private Renderer[][] rims = new Renderer[0][];
        private readonly Dictionary<Mesh, Mesh> rimMeshes = new Dictionary<Mesh, Mesh>();
        private int targetDrain = -1, targetPart = -1;
        // R47: grey glue round a glued lid's rim (stays for the rest of the round); R49: brushed on, see BuildGlue.
        private GameObject[] glueGlobs = new GameObject[0];
        private Material glueMaterial;
        private static readonly int[] HoleIds = {
            Shader.PropertyToID("_AquariumDrain0"), Shader.PropertyToID("_AquariumDrain1"),
            Shader.PropertyToID("_AquariumDrain2"), Shader.PropertyToID("_AquariumDrain3") };
        public AquariumDrainPresentation(Transform parent) { this.parent = parent; }
        public int Count => instances.Length;
        public AquariumDrain Instance(int i) => i >= 0 && i < instances.Length ? instances[i] : null;
        // The displayed (smoothed) lid slide, for the arms and the hands that hold it.
        public float ShownLid(int i) => i >= 0 && i < shownLid.Length ? shownLid[i] : 0;

        public void Draw(AquariumSnapshot state) { Draw(state, Time.deltaTime); }
        public void Draw(AquariumSnapshot state, float dt)
        {
            var placements = state == null ? null : state.drains;
            int count = placements == null ? 0 : Mathf.Min(placements.Length, 4);
            bool same = last != null && last.Length == count;
            if (same) for (int i = 0; i < count; i++)
                if (!last[i].position.Equals(placements[i].position) || last[i].yaw != placements[i].yaw) { same = false; break; }
            if (!same) Build(placements, count);
            if (count == 0) return;
            float follow = 1 - Mathf.Exp(-Mathf.Max(0, dt) * 9), time = state.time;
            for (int i = 0; i < count; i++)
            {
                var d = placements[i];
                if (instances[i] == null) continue;
                for (int s = 0; s < shownScrews[i].Length; s++)
                {
                    float target = d.screws != null && s < d.screws.Length ? d.screws[s] : 0;
                    // Snap on big jumps (late join, new round); otherwise ease between snapshots.
                    bool snap = Mathf.Abs(target - shownScrews[i][s]) > .2f;
                    shownScrews[i][s] = snap ? target : Mathf.Lerp(shownScrews[i][s], target, follow);
                    // R34: a screw that has just come all the way out pops away; one already out on a snap is simply gone.
                    if (target < .999f) goneScrews[i][s] = 0;
                    else if (snap) goneScrews[i][s] = 1;
                    else if (shownScrews[i][s] >= .98f) goneScrews[i][s] = Mathf.Min(1, goneScrews[i][s] + Mathf.Max(0, dt) / AquariumDrain.VanishSeconds);
                }
                float lid = Mathf.Clamp01(d.lid), before = shownLid[i];
                shownLid[i] = Mathf.Abs(lid - before) > .35f ? lid : Mathf.Lerp(before, lid, follow);
                scrape[i] = Mathf.Lerp(scrape[i], Mathf.Abs(shownLid[i] - before) > .0004f ? 1 : 0, 1 - Mathf.Exp(-Mathf.Max(0, dt) * 8));
                instances[i].Pose(shownScrews[i], shownLid[i], d.slideYaw - d.yaw, time, scrape[i], goneScrews[i]);
                if (i < glueGlobs.Length && d.glued != (glueGlobs[i] != null))
                {
                    if (d.glued) glueGlobs[i] = BuildGlue(instances[i], i);
                    else { Release(glueGlobs[i]); glueGlobs[i] = null; }
                }
                if (i < glueGlobs.Length && glueGlobs[i] != null)
                {
                    float paint = GluePaint(state, d);
                    glueBlock.SetFloat("_Paint", paint); glueBlock.SetFloat("_Seed", i * 1.7f);
                    var gr = glueGlobs[i].GetComponent<MeshRenderer>(); gr.SetPropertyBlock(glueBlock);
                }
            }
        }

        // R49: glue painted round the lid's rim with a thick brush: five overlapping strokes of grey glue over the
        // gap between the lid and the frame, uneven in thickness with bristle streaks (GlueBrush shader), heavy
        // blobby dabs where strokes start and overlap, and drips down the frame's outer face. One mesh per drain,
        // built once; the application is animated in the shader (_Paint 0..1 over GluePaintSeconds, strokes
        // travelling round the rim), driven from the glue effect's age, and then stays.
        public const float GluePaintSeconds = 1.3f;
        private MaterialPropertyBlock glueBlock;
        private Mesh[] glueMeshes = new Mesh[0];
        private static readonly float[] GlueR = { .80f, .87f, .935f, .975f, .995f, 1.01f, 1.04f, 1.065f };
        private static readonly float[] GlueY = { .19f, .193f, .192f, .182f, .155f, .105f, .084f, .08f };
        private GameObject BuildGlue(AquariumDrain drain, int index)
        {
            if (drain == null || drain.lid == null) return null;
            if (glueMaterial == null)
            {
                var shader = Resources.Load<Shader>("AquariumDrains/GlueBrush");
                glueMaterial = new Material(shader != null ? shader : Shader.Find("Standard")) { name = "Brushed drain glue", hideFlags = HideFlags.DontSave, color = new Color(.52f, .54f, .56f) };
            }
            if (glueBlock == null) glueBlock = new MaterialPropertyBlock();
            var glue = new GameObject("Glued lid"); glue.transform.SetParent(group != null ? group.transform : parent, false);
            glue.transform.SetPositionAndRotation(drain.transform.position, drain.transform.rotation);
            var mesh = BuildGlueMesh(index);
            if (index < glueMeshes.Length) { if (glueMeshes[index] != null) Release(glueMeshes[index]); glueMeshes[index] = mesh; }
            glue.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = glue.AddComponent<MeshRenderer>(); renderer.sharedMaterial = glueMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return glue;
        }
        private static float GH(int k, int salt) { unchecked { uint n = (uint)(k * 374761393 + salt * 668265263); n = (n ^ (n >> 13)) * 1274126177; return ((n ^ (n >> 16)) & 65535) / 65535f; } }
        // A point on the rim's cross-section (s 0..1 from the lid top out to the frame), at angle a, in metres.
        private static Vector3 GlueProfile(float a, float s, out Vector3 normal)
        {
            float S = AquariumDrainLayout.ModelScale, f = Mathf.Clamp(s, 0, 1) * (GlueR.Length - 1);
            int i = Mathf.Min(GlueR.Length - 2, Mathf.FloorToInt(f)); float w = f - i;
            float r = Mathf.Lerp(GlueR[i], GlueR[i + 1], w) * S, y = Mathf.Lerp(GlueY[i], GlueY[i + 1], w) * S;
            float dr = (GlueR[i + 1] - GlueR[i]) * S, dy = (GlueY[i + 1] - GlueY[i]) * S;
            var outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var along = (outward * dr + Vector3.up * dy).normalized;
            normal = Vector3.Cross(along, Vector3.Cross(Vector3.up, outward)).normalized; if (normal.y < 0) normal = -normal;
            return outward * r + Vector3.up * y;
        }
        private static Mesh BuildGlueMesh(int index)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var paint = new List<Vector2>(); var rest = new List<Vector3>(); var tri = new List<int>();
            const int Strokes = 5, Across = 9;
            float S = AquariumDrainLayout.ModelScale, seed = index * 17 + 3;
            var strokeStart = new float[Strokes]; var strokeArc = new float[Strokes];
            for (int k = 0; k < Strokes; k++)
            {
                float a0 = (k + .25f * GH(k, (int)seed)) * Mathf.PI * 2 / Strokes, arc = Mathf.PI * 2 / Strokes * (1.25f + .35f * GH(k, (int)seed + 1));
                strokeStart[k] = a0; strokeArc[k] = arc;
                float t0 = k * .17f, t1 = t0 + .3f + .06f * GH(k, 5);
                float s0 = .02f + .12f * GH(k, 6), s1 = .92f + .08f * GH(k, 7);
                float rimR = .99f * S, lengthM = arc * rimR;
                int along = Mathf.Max(8, Mathf.CeilToInt(lengthM / .008f));
                float lift = .0012f * k, thick = (.0055f + .0035f * GH(k, 8));
                int baseIndex = v.Count;
                for (int i = 0; i <= along; i++)
                {
                    float u = i / (float)along, a = a0 + arc * u;
                    // Loaded brush at the start (heavy), dry at the end; lumps along the length.
                    float load = Mathf.Lerp(1.35f, .45f, u) * (.8f + .25f * Mathf.Sin(u * 23 + k * 3) + .15f * Mathf.Sin(u * 61 + seed));
                    float wob = .05f * Mathf.Sin(u * 9 + k * 2.1f) + .03f * Mathf.Sin(u * 27 + seed);
                    float feather = Mathf.Clamp01(Mathf.Min(u / .04f, (1 - u) / .22f));
                    float narrow = Mathf.Lerp(1, .7f, Mathf.SmoothStep(.6f, 1, u));
                    for (int j = 0; j < Across; j++)
                    {
                        float vv = j / (float)(Across - 1);
                        float mid = (s0 + s1) * .5f + wob, half = (s1 - s0) * .5f * narrow;
                        float s = mid + (vv * 2 - 1) * half;
                        var p = GlueProfile(a, s, out Vector3 n);
                        float bead = Mathf.Pow(Mathf.Sin(Mathf.PI * vv), .6f);
                        float ridge = 1 + .25f * Mathf.Sin(vv * 19 + u * 7 + k);
                        float h = (thick * load * bead * ridge + .0006f) + lift;
                        rest.Add(p + n * (.0004f + lift)); v.Add(p + n * h);
                        uv.Add(new Vector2(u * lengthM, vv)); paint.Add(new Vector2(Mathf.Lerp(t0, t1, u), feather));
                    }
                }
                for (int i = 0; i < along; i++) for (int j = 0; j < Across - 1; j++)
                {
                    int q = baseIndex + i * Across + j;
                    tri.Add(q); tri.Add(q + 1); tri.Add(q + Across + 1); tri.Add(q); tri.Add(q + Across + 1); tri.Add(q + Across);
                }
            }
            // Dabs: blobby piles where a stroke starts (the loaded brush pressed down) and a few where strokes overlap.
            for (int d = 0; d < 9; d++)
            {
                int k = d % Strokes; bool start = d < Strokes;
                float a = start ? strokeStart[k] + .04f : strokeStart[(k + 1) % Strokes] + .1f + .15f * GH(d, 11);
                float s = .35f + .45f * GH(d, 12), R = (.014f + .012f * GH(d, 13)), height = .007f + .006f * GH(d, 14);
                float time = start ? k * .17f + .02f : ((k + 1) % Strokes) * .17f + .12f;
                var c = GlueProfile(a, s, out Vector3 n);
                var tangent = Vector3.Cross(Vector3.up, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
                var bi = Vector3.Cross(n, tangent).normalized;
                const int Rings = 5, Spokes = 12; int b0 = v.Count;
                for (int r = 0; r <= Rings; r++) for (int sp = 0; sp < Spokes; sp++)
                {
                    float rr = r / (float)Rings, ang = sp * Mathf.PI * 2 / Spokes;
                    float lump = 1 + .25f * Mathf.Sin(ang * 3 + d) + .12f * Mathf.Sin(ang * 5 + d * 2);
                    var off = (tangent * Mathf.Cos(ang) * 1.4f + bi * Mathf.Sin(ang)) * (R * rr * lump);
                    float h = height * (1 - rr * rr) * (1 + .2f * Mathf.Sin(ang * 4 + d * 3) * rr) + .002f + .0012f * Strokes;
                    v.Add(c + off + n * h); rest.Add(c + n * .0005f);
                    uv.Add(new Vector2(ang * R * 2, .5f * (1 - rr))); paint.Add(new Vector2(time, 1));
                }
                for (int r = 0; r < Rings; r++) for (int sp = 0; sp < Spokes; sp++)
                {
                    int q0 = b0 + r * Spokes + sp, q1 = b0 + r * Spokes + (sp + 1) % Spokes;
                    tri.Add(q0); tri.Add(q1); tri.Add(q1 + Spokes); tri.Add(q0); tri.Add(q1 + Spokes); tri.Add(q0 + Spokes);
                }
            }
            // Drips down the frame's outer face, each with a fat bead at the bottom.
            for (int d = 0; d < 6; d++)
            {
                float a = (d + .6f * GH(d, 21)) * Mathf.PI * 2 / 6, len = .018f + .03f * GH(d, 22), w = .005f + .004f * GH(d, 23);
                float time = .25f + .12f * d + .1f * GH(d, 24);
                var outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var tangent = Vector3.Cross(Vector3.up, outward);
                var top = outward * (1.075f * S) + Vector3.up * (.082f * S);
                const int Steps = 8, Wide = 5; int b0 = v.Count;
                for (int i = 0; i <= Steps; i++) for (int j = 0; j < Wide; j++)
                {
                    float u = i / (float)Steps, vv = j / (float)(Wide - 1);
                    float width = w * (1 - .35f * u + 1.1f * Mathf.SmoothStep(.7f, 1, u) * Mathf.Sin(Mathf.PI * Mathf.Min(1, (u - .7f) / .3f + .5f)));
                    float bulge = Mathf.Sin(Mathf.PI * vv) * (.003f + .004f * Mathf.SmoothStep(.6f, 1, u));
                    var p = top + Vector3.down * (len * u) + tangent * ((vv * 2 - 1) * width) + outward * (bulge + .001f);
                    if (i == Steps) p = top + Vector3.down * (len + w * .6f) + outward * .002f;
                    v.Add(p); rest.Add(top + outward * .001f);
                    uv.Add(new Vector2(u * len, vv)); paint.Add(new Vector2(time, 1));
                }
                for (int i = 0; i < Steps; i++) for (int j = 0; j < Wide - 1; j++)
                {
                    int q = b0 + i * Wide + j;
                    tri.Add(q); tri.Add(q + Wide + 1); tri.Add(q + 1); tri.Add(q); tri.Add(q + Wide); tri.Add(q + Wide + 1);
                }
            }
            var mesh = new Mesh { name = "Brushed glue", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetUVs(1, paint); mesh.SetUVs(2, rest); mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
        // How far the glue on drain i has been painted (0..1), from its glue effect's age; 1 without one (late join).
        private static float GluePaint(AquariumSnapshot state, AquariumDrainPlacement d)
        {
            if (state.effects == null) return 1;
            var c = AquariumDrainLayout.LidCenter(d);
            foreach (var e in state.effects)
            {
                if (e.kind != "glue") continue;
                float dx = e.position.x - c.x, dz = e.position.z - c.z;
                if (dx * dx + dz * dz < .25f) return Mathf.Clamp01((state.time - e.born) / GluePaintSeconds);
            }
            return 1;
        }

        private void Build(AquariumDrainPlacement[] placements, int count)
        {
            Clear();
            last = new AquariumDrainPlacement[count];
            instances = new AquariumDrain[count]; shownScrews = new float[count][]; goneScrews = new float[count][]; shownLid = new float[count]; scrape = new float[count];
            rims = new Renderer[count][]; for (int i = 0; i < count; i++) rims[i] = new Renderer[AquariumDrainLayout.ScrewCount];
            glueGlobs = new GameObject[count];
            foreach (var m in glueMeshes) if (m != null) Release(m);
            glueMeshes = new Mesh[count];
            if (count == 0) return;
            var prefab = Resources.Load<GameObject>("AquariumDrains/ReturnDrain");
            if (prefab == null) { Debug.LogError("Missing AquariumDrains/ReturnDrain prefab"); return; }
            group = new GameObject("Round return drains"); group.transform.SetParent(parent, false);
            for (int i = 0; i < count; i++)
            {
                last[i] = placements[i]; var p = placements[i].position;
                var instance = Object.Instantiate(prefab, group.transform);
                instance.name = "Return drain " + (i + 1).ToString("00");
                instance.transform.SetPositionAndRotation(new Vector3(p.x, p.y, p.z), Quaternion.Euler(0, placements[i].yaw, 0));
                // R27: sized so the alien has to squeeze through the opening.
                instance.transform.localScale = Vector3.one * AquariumDrainLayout.ModelScale;
                instances[i] = instance.GetComponent<AquariumDrain>();
                shownScrews[i] = new float[AquariumDrainLayout.ScrewCount]; goneScrews[i] = new float[AquariumDrainLayout.ScrewCount];
                var screws = placements[i].screws;
                for (int s = 0; s < shownScrews[i].Length; s++)
                {
                    shownScrews[i][s] = screws != null && s < screws.Length ? screws[s] : 0;
                    goneScrews[i][s] = shownScrews[i][s] >= .999f ? 1 : 0;
                }
                shownLid[i] = Mathf.Clamp01(placements[i].lid);
                instances[i].Pose(shownScrews[i], shownLid[i], placements[i].slideYaw - placements[i].yaw, 0, 0, goneScrews[i]);
                apertures[i] = new Vector4(p.x, p.y, p.z, AquariumDrainLayout.OpeningRadius + .008f);
                Shader.SetGlobalVector(HoleIds[i], apertures[i]);
            }
            Shader.SetGlobalInt("_AquariumDrainCount", count);
        }

        // The same green silhouette as a disguise target (R31, was yellow), thinner for the small hardware.
        // R29: a targeted screw is drawn by its x-ray rim (see Rims) so the alien's body never hides it.
        public void Highlight(int drain, int part)
        {
            Renderer target = null;
            var d = Instance(drain);
            bool screw = d != null && part >= 0 && part < d.screws.Length && d.screws[part] != null;
            targetDrain = screw ? drain : -1; targetPart = screw ? part : -1;
            if (d != null && part == AquariumModel.DrainLidPart && d.lid != null) target = d.lid.GetComponent<Renderer>();
            if (target == highlighted) return;
            if (highlightShell != null) Release(highlightShell);
            highlightShell = null; highlighted = target;
            if (target == null) return;
            var filter = target.GetComponent<MeshFilter>(); if (filter == null || filter.sharedMesh == null) return;
            if (outline == null)
            {
                outline = new Material(Resources.Load<Shader>("AquariumTargetOutline"));
                outline.SetColor("_OutlineColor", new Color(.45f, 1f, .35f));
            }
            outline.SetFloat("_OutlineWidth", part == AquariumModel.DrainLidPart ? .02f : .012f);
            highlightShell = new GameObject("Drain target silhouette"); highlightShell.transform.SetParent(target.transform, false);
            highlightShell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var shell = highlightShell.AddComponent<MeshRenderer>();
            var slots = new Material[Mathf.Max(1, filter.sharedMesh.subMeshCount)]; for (int i = 0; i < slots.Length; i++) slots[i] = outline;
            shell.sharedMaterials = slots; shell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; shell.receiveShadows = false;
        }

        // R29: viewer is the local alien (null hides every rim). Screws already out, and screws under a
        // lid that has started to move, get none.
        public void Rims(Vector3? viewer)
        {
            for (int i = 0; i < instances.Length && i < rims.Length; i++)
            {
                var d = instances[i]; if (d == null) continue;
                float distance = viewer.HasValue ? Vector3.Distance(viewer.Value, d.transform.position) : float.MaxValue;
                float fade = Mathf.Clamp01((RimRange - distance) / RimFade);
                bool lidMoved = shownLid[i] > .001f;
                for (int s = 0; s < d.screws.Length && s < rims[i].Length; s++)
                {
                    bool target = targetDrain == i && targetPart == s && viewer.HasValue;
                    var screw = d.screws[s];
                    // R34: no rim on a freed screw while it pops away.
                    bool freed = i < goneScrews.Length && goneScrews[i] != null && s < goneScrews[i].Length && goneScrews[i][s] > 0;
                    bool show = (fade > 0 || target) && !lidMoved && !freed && screw != null && screw.gameObject.activeInHierarchy;
                    var rim = rims[i][s];
                    if (!show) { if (rim != null && rim.enabled) rim.enabled = false; continue; }
                    if (rim == null) rim = rims[i][s] = BuildRim(screw);
                    if (rim == null) continue;
                    rim.enabled = true;
                    rimBlock.SetColor("_OutlineColor", target ? RimTarget : new Color(RimIdle.r, RimIdle.g, RimIdle.b, RimIdle.a * fade));
                    rimBlock.SetFloat("_OutlinePixels", target ? 3.4f : 2.6f);
                    rim.SetPropertyBlock(rimBlock);
                }
            }
        }

        private Renderer BuildRim(Transform screw)
        {
            var filter = screw.GetComponent<MeshFilter>(); if (filter == null || filter.sharedMesh == null) return null;
            if (xray == null)
            {
                var shader = Resources.Load<Shader>("AquariumTargetOutlineXRay");
                if (shader == null) return null;
                // The shank and the washer sit in the lid's countersink: only the hex head gets the rim and glow.
                xray = new Material(shader) { name = "Screw x-ray rim", hideFlags = HideFlags.DontSave };
                xray.SetFloat("_ClipBelow", .025f);
                rimBlock = new MaterialPropertyBlock();
            }
            var rim = new GameObject("Screw x-ray rim"); rim.transform.SetParent(screw, false);
            rim.AddComponent<MeshFilter>().sharedMesh = RimMesh(filter.sharedMesh);
            var renderer = rim.AddComponent<MeshRenderer>(); renderer.sharedMaterial = xray;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return renderer;
        }

        // One submesh (so the stencil stamp covers the whole part before any rim is drawn) with normals
        // averaged across hard edges (so the widened shell has no cracks at the hex corners).
        private Mesh RimMesh(Mesh source)
        {
            Mesh mesh;
            if (rimMeshes.TryGetValue(source, out mesh) && mesh != null) return mesh;
            if (!source.isReadable) return source;
            var vertices = source.vertices; var normals = source.normals;
            var sums = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 sum; sums.TryGetValue(vertices[i], out sum);
                sums[vertices[i]] = sum + (normals.Length == vertices.Length ? normals[i] : Vector3.up);
            }
            var smooth = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++) smooth[i] = sums[vertices[i]].normalized;
            var triangles = new List<int>();
            for (int s = 0; s < source.subMeshCount; s++) triangles.AddRange(source.GetTriangles(s));
            mesh = new Mesh { name = source.name + " rim", hideFlags = HideFlags.DontSave };
            if (vertices.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices; mesh.normals = smooth; mesh.SetTriangles(triangles, 0);
            var bounds = source.bounds; bounds.Expand(.05f); mesh.bounds = bounds;
            rimMeshes[source] = mesh;
            return mesh;
        }

        public void Clear()
        {
            if (highlightShell != null) Release(highlightShell);
            highlightShell = null; highlighted = null;
            if (group != null) { group.SetActive(false); Release(group); }
            group = null; last = null; instances = new AquariumDrain[0]; shownScrews = new float[0][]; goneScrews = new float[0][]; shownLid = new float[0]; scrape = new float[0];
            rims = new Renderer[0][]; targetDrain = targetPart = -1;
            foreach (var g in glueGlobs) if (g != null) Release(g);
            glueGlobs = new GameObject[0];
            foreach (var m in glueMeshes) if (m != null) Release(m);
            glueMeshes = new Mesh[0];
            Shader.SetGlobalInt("_AquariumDrainCount", 0);
            for (int i = 0; i < 4; i++) Shader.SetGlobalVector(HoleIds[i], Vector4.zero);
        }
        public void Dispose()
        {
            Clear(); if (outline != null) Release(outline); outline = null;
            if (xray != null) Release(xray); xray = null;
            if (glueMaterial != null) Release(glueMaterial); glueMaterial = null;
            foreach (var mesh in rimMeshes.Values) if (mesh != null) Release(mesh);
            rimMeshes.Clear();
        }
        private static void Release(Object o) { if (o == null) return; if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }
    }
}

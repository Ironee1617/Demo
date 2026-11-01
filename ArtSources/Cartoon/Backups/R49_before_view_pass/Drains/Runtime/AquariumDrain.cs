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
        // R47: grey glue smeared round a glued lid's rim (stays for the rest of the round).
        private GameObject[] glueGlobs = new GameObject[0];
        private Material glueMaterial;
        private Mesh globMesh;
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
            }
        }

        // Thick glossy globs bridging the lid's edge and the frame round it, with a few drips down the side.
        private GameObject BuildGlue(AquariumDrain drain, int index)
        {
            if (drain == null || drain.lid == null) return null;
            if (glueMaterial == null)
            {
                glueMaterial = new Material(Shader.Find("Standard")) { name = "Drain glue", hideFlags = HideFlags.DontSave, color = new Color(.55f, .57f, .59f) };
                glueMaterial.SetFloat("_Glossiness", .9f);
            }
            if (globMesh == null) { var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere); globMesh = tmp.GetComponent<MeshFilter>().sharedMesh; Release(tmp); }
            var glue = new GameObject("Glued lid"); glue.transform.SetParent(group != null ? group.transform : parent, false);
            var centre = drain.lid.position + Vector3.up * (AquariumDrainLayout.LidTop * .55f);
            float rim = AquariumDrainLayout.LidRadius;
            const int count = 22;
            for (int k = 0; k < count; k++)
            {
                float h1 = Mathf.Repeat(Mathf.Sin((k + 1) * 12.9898f + index * 78.233f) * 43758.5453f, 1), h2 = Mathf.Repeat(Mathf.Sin((k + 3) * 39.346f + index * 11.135f) * 24634.6345f, 1);
                float angle = (k + .6f * h1) / count * Mathf.PI * 2;
                var outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var go = new GameObject("Glue glob"); go.transform.SetParent(glue.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = globMesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = glueMaterial; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bool drip = k % 4 == 1;
                float size = rim * (.16f + .14f * h2);
                go.transform.position = centre + outward * (rim * (drip ? 1.04f : .96f + .08f * h1)) - Vector3.up * (drip ? AquariumDrainLayout.LidTop * .7f : 0);
                go.transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, outward));
                go.transform.localScale = drip ? new Vector3(size * .55f, size * 1.3f, size * .55f) : new Vector3(size * 1.7f, size * .55f, size * .9f);
            }
            return glue;
        }

        private void Build(AquariumDrainPlacement[] placements, int count)
        {
            Clear();
            last = new AquariumDrainPlacement[count];
            instances = new AquariumDrain[count]; shownScrews = new float[count][]; goneScrews = new float[count][]; shownLid = new float[count]; scrape = new float[count];
            rims = new Renderer[count][]; for (int i = 0; i < count; i++) rims[i] = new Renderer[AquariumDrainLayout.ScrewCount];
            glueGlobs = new GameObject[count];
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

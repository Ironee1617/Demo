using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R27: the return drain's moving parts. The host owns the state (screw progress, lid slide); this only
    // poses the authored lid and six screws: a screw turns and rises as it is backed out, a loose screw sits
    // proud and a little crooked, and the heavy lid scrapes sideways off the opening, tipping onto the sand.
    public sealed class AquariumDrain : MonoBehaviour
    {
        public Transform lid, opening, pipeExit;
        public Transform[] screws = new Transform[6];
        public float openingDiameter = AquariumDrainLayout.OpeningRadius * 2;
        // Turns to back a screw out, and how far it rises (prefab units) while it does.
        public const float ScrewTurns = 9f, ScrewRise = .09f, LooseRise = .05f;
        public bool IsOpen { get; private set; }
        private bool poseStored;
        private Vector3 lidRest;
        private Quaternion lidRotation;
        private Vector3[] screwRest;
        private Quaternion[] screwRotation;

        private void Store()
        {
            if (poseStored) return;
            lidRest = lid.localPosition; lidRotation = lid.localRotation;
            screwRest = new Vector3[screws.Length]; screwRotation = new Quaternion[screws.Length];
            for (int i = 0; i < screws.Length; i++) { screwRest[i] = screws[i].localPosition; screwRotation[i] = screws[i].localRotation; }
            poseStored = true;
        }

        // Authoring previews: everything sealed, or screws out and the lid fully aside.
        public void SetOpen(bool open)
        {
            var s = new float[screws.Length]; for (int i = 0; i < s.Length; i++) s[i] = open ? 1 : 0;
            Pose(s, open ? 1 : 0, 90, 0, 0);
        }

        // screwOut: 0..1 per screw; lidSlide: 0..1; slideLocalYaw: slide direction in the drain's own frame
        // (degrees); scrape: 0..1 while the lid is being dragged (a little judder).
        public void Pose(float[] screwOut, float lidSlide, float slideLocalYaw, float time, float scrape)
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
                float rise = ScrewRise * p + (p >= 1 ? LooseRise : 0);
                var spin = Quaternion.Euler(0, -p * ScrewTurns * 360f, 0);
                // A loose screw leans a little in its hole (each one its own way).
                var lean = p >= 1 ? Quaternion.Euler(Mathf.Sin(i * 2.1f) * 7, 0, Mathf.Cos(i * 1.7f) * 7) : Quaternion.identity;
                screws[i].localPosition = lidRest + offset + tilt * (screwRest[i] - lidRest + Vector3.up * rise);
                screws[i].localRotation = tilt * lean * screwRotation[i] * spin;
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
        private float[][] shownScrews = new float[0][];
        private float[] shownLid = new float[0], scrape = new float[0];
        private readonly Vector4[] apertures = new Vector4[4];
        private Material outline;
        private Renderer highlighted;
        private GameObject highlightShell;
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
                    shownScrews[i][s] = Mathf.Abs(target - shownScrews[i][s]) > .2f ? target : Mathf.Lerp(shownScrews[i][s], target, follow);
                }
                float lid = Mathf.Clamp01(d.lid), before = shownLid[i];
                shownLid[i] = Mathf.Abs(lid - before) > .35f ? lid : Mathf.Lerp(before, lid, follow);
                scrape[i] = Mathf.Lerp(scrape[i], Mathf.Abs(shownLid[i] - before) > .0004f ? 1 : 0, 1 - Mathf.Exp(-Mathf.Max(0, dt) * 8));
                instances[i].Pose(shownScrews[i], shownLid[i], d.slideYaw - d.yaw, time, scrape[i]);
            }
        }

        private void Build(AquariumDrainPlacement[] placements, int count)
        {
            Clear();
            last = new AquariumDrainPlacement[count];
            instances = new AquariumDrain[count]; shownScrews = new float[count][]; shownLid = new float[count]; scrape = new float[count];
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
                shownScrews[i] = new float[AquariumDrainLayout.ScrewCount];
                var screws = placements[i].screws;
                for (int s = 0; s < shownScrews[i].Length; s++) shownScrews[i][s] = screws != null && s < screws.Length ? screws[s] : 0;
                shownLid[i] = Mathf.Clamp01(placements[i].lid);
                instances[i].Pose(shownScrews[i], shownLid[i], placements[i].slideYaw - placements[i].yaw, 0, 0);
                apertures[i] = new Vector4(p.x, p.y, p.z, AquariumDrainLayout.OpeningRadius + .008f);
                Shader.SetGlobalVector(HoleIds[i], apertures[i]);
            }
            Shader.SetGlobalInt("_AquariumDrainCount", count);
        }

        // The same yellow silhouette as a disguise target, thinner for the small hardware.
        public void Highlight(int drain, int part)
        {
            Renderer target = null;
            var d = Instance(drain);
            if (d != null)
            {
                if (part >= 0 && part < d.screws.Length && d.screws[part] != null) target = d.screws[part].GetComponent<Renderer>();
                else if (part == AquariumModel.DrainLidPart && d.lid != null) target = d.lid.GetComponent<Renderer>();
            }
            if (target == highlighted) return;
            if (highlightShell != null) Release(highlightShell);
            highlightShell = null; highlighted = target;
            if (target == null) return;
            var filter = target.GetComponent<MeshFilter>(); if (filter == null || filter.sharedMesh == null) return;
            if (outline == null)
            {
                outline = new Material(Resources.Load<Shader>("AquariumTargetOutline"));
                outline.SetColor("_OutlineColor", new Color(1f, .86f, .25f));
            }
            outline.SetFloat("_OutlineWidth", part == AquariumModel.DrainLidPart ? .02f : .012f);
            highlightShell = new GameObject("Drain target silhouette"); highlightShell.transform.SetParent(target.transform, false);
            highlightShell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var shell = highlightShell.AddComponent<MeshRenderer>();
            var slots = new Material[Mathf.Max(1, filter.sharedMesh.subMeshCount)]; for (int i = 0; i < slots.Length; i++) slots[i] = outline;
            shell.sharedMaterials = slots; shell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; shell.receiveShadows = false;
        }

        public void Clear()
        {
            if (highlightShell != null) Release(highlightShell);
            highlightShell = null; highlighted = null;
            if (group != null) { group.SetActive(false); Release(group); }
            group = null; last = null; instances = new AquariumDrain[0]; shownScrews = new float[0][]; shownLid = new float[0]; scrape = new float[0];
            Shader.SetGlobalInt("_AquariumDrainCount", 0);
            for (int i = 0; i < 4; i++) Shader.SetGlobalVector(HoleIds[i], Vector4.zero);
        }
        public void Dispose() { Clear(); if (outline != null) Release(outline); outline = null; }
        private static void Release(Object o) { if (o == null) return; if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }
    }
}

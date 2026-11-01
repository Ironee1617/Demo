using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // R47: presentation of the alien and diver abilities (AquariumModel.Abilities). Everything is a pure function of
    // snapshot time; objects are pooled or built once per event, and nothing allocates per frame.
    //  - shock: an expanding shell of blue-white light with forked arcs; stunned divers and fish crackle with sparks.
    //  - dash: bubbles left along the burst and speed streaks behind the alien (the stretch is done on the actor).
    //  - glue: wet flecks off the brush (the glue itself is brushed round the rim by AquariumDrainPresentation).
    //  - droneLaunch: a small bubble puff. The drone's red eyes: MakeDroneEyes / PresentDroneEyes.
    //  - vanish: the blue alien's skins wear a see-through ghost copy of their material (Ghost / EndGhosts).
    //  - xray: red silhouettes of every living creature drawn over everything by a camera command buffer.
    public sealed class AquariumAbilityFx : System.IDisposable
    {
        public static bool IsAbility(string kind) => kind == "inkBomb" || kind == "shock" || kind == "dash" || kind == "glue" || kind == "droneLaunch";

        private const int ShockArcs = 9, ArcPoints = 9, SparkArcs = 3, SparkPoints = 6, DashBubbles = 18, DashStreaks = 3, GlueDrops = 22, PuffBubbles = 12;
        private static readonly Color ArcColor = new Color(.62f, .86f, 1.9f, 1f), ShellColor = new Color(.25f, .5f, 1.3f, 1f);
        private readonly Transform root;
        private readonly List<Object> owned = new List<Object>();
        private readonly Material arcs, shell, coreGlow, eyeCore, eyeGlow, bubble, streak, glue;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private readonly Vector3[] arcBuffer = new Vector3[ArcPoints], sparkBuffer = new Vector3[SparkPoints], lineBuffer = new Vector3[2];
        private Mesh sphere;

        public AquariumAbilityFx(Transform root)
        {
            this.root = root;
            var glowShader = Resources.Load<Shader>("AquariumAbilityGlow");
            arcs = Glow(glowShader, ArcColor, 0, 1, 1);
            shell = Glow(glowShader, ShellColor, 1, 2.2f, 0);
            coreGlow = Glow(glowShader, new Color(.5f, .75f, 1.6f, 1), -1, 3, 0);
            eyeGlow = Glow(glowShader, new Color(2.2f, .12f, .06f, 1), -1, 2.2f, 0);
            eyeCore = new Material(Shader.Find("Standard")) { name = "Drone eye", hideFlags = HideFlags.DontSave, color = new Color(1f, .08f, .05f) };
            eyeCore.EnableKeyword("_EMISSION"); eyeCore.SetColor("_EmissionColor", new Color(3.2f, .18f, .08f)); eyeCore.SetFloat("_Glossiness", .9f); owned.Add(eyeCore);
            bubble = Sprite(new Color(.82f, .95f, 1f, .55f));
            streak = Sprite(Color.white);
            glue = new Material(Shader.Find("Standard")) { name = "Sticky glue", hideFlags = HideFlags.DontSave, color = new Color(.56f, .58f, .6f) };
            glue.SetFloat("_Glossiness", .88f); owned.Add(glue);
        }
        private Material Glow(Shader shader, Color color, float rim, float power, float vertexColor)
        {
            var m = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            m.name = "Ability glow"; m.hideFlags = HideFlags.DontSave;
            if (shader != null) { m.SetColor("_Color", color); m.SetFloat("_Rim", rim); m.SetFloat("_Power", power); m.SetFloat("_VertexColor", vertexColor); }
            owned.Add(m); return m;
        }
        private Material Sprite(Color color)
        {
            var m = new Material(Shader.Find("Sprites/Default")) { name = "Ability sprite", hideFlags = HideFlags.DontSave, color = color, renderQueue = 3000 };
            owned.Add(m); return m;
        }
        private static float Hash(int a, int b) { return Mathf.Repeat(Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f, 1); }
        private static Vector3 OnSphere(int a, int b)
        {
            float z = Hash(a, b) * 2 - 1, t = Hash(a, b + 7) * Mathf.PI * 2, s = Mathf.Sqrt(1 - z * z);
            return new Vector3(s * Mathf.Cos(t), z, s * Mathf.Sin(t));
        }
        private Transform Ball(string name, Transform parent, Material mat)
        {
            if (sphere == null) { var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere = tmp.GetComponent<MeshFilter>().sharedMesh; Release(tmp); }
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = sphere;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go.transform;
        }
        private LineRenderer Line(string name, Transform parent, Material mat, int points, float width)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var l = go.AddComponent<LineRenderer>(); l.sharedMaterial = mat; l.useWorldSpace = true; l.positionCount = points;
            l.widthMultiplier = width; l.numCapVertices = 2; l.shadowCastingMode = ShadowCastingMode.Off; l.receiveShadows = false;
            l.lightProbeUsage = LightProbeUsage.Off; l.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return l;
        }

        // ------------------------------------------------------------------ one object per event
        public GameObject Make(string kind, int id)
        {
            var fx = new GameObject(kind + " ability"); fx.transform.SetParent(root, false);
            var t = fx.transform;
            if (kind == "inkBomb") fx.AddComponent<AquariumInkCloud>().Initialize(id, false);
            else if (kind == "shock")
            {
                Ball("Shock shell", t, shell); Ball("Shock flash", t, coreGlow);
                for (int i = 0; i < ShockArcs; i++) Line("Shock arc", t, arcs, ArcPoints, .05f);
                var light = new GameObject("Shock light").AddComponent<Light>(); light.transform.SetParent(t, false);
                light.type = LightType.Point; light.color = new Color(.55f, .78f, 1f); light.shadows = LightShadows.None; light.intensity = 0;
            }
            else if (kind == "dash")
            {
                for (int i = 0; i < DashBubbles; i++) Ball("Dash bubble", t, bubble);
                for (int i = 0; i < DashStreaks; i++)
                {
                    var l = Line("Dash streak", t, streak, 2, .05f);
                    l.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, .1f));
                }
            }
            else if (kind == "glue")
            {
                // R49: the glue is brushed onto the rim by AquariumDrainPresentation (animated from this effect's age);
                // here only a few wet strings of glue stretch off the brush as it goes round.
                for (int i = 0; i < GlueDrops; i++) Ball("Glue drop", t, glue);
                // R51: and a puff of bubbles as the whole blob splats down at once.
                for (int i = 0; i < PuffBubbles; i++) Ball("Puff bubble", t, bubble);
            }
            else if (kind == "droneLaunch")
                for (int i = 0; i < PuffBubbles; i++) Ball("Puff bubble", t, bubble);
            return fx;
        }

        public void PresentShock(Transform fx, float age, float duration, Vector3 centre, float radius, int id)
        {
            fx.position = centre; fx.rotation = Quaternion.identity;
            float grow = 1 - Mathf.Exp(-age * 6.5f), r = radius * grow;
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(duration * .3f, duration, age));
            int tick = Mathf.FloorToInt(age * 24) + id * 31;
            int arc = 0;
            for (int c = 0; c < fx.childCount; c++)
            {
                var child = fx.GetChild(c);
                if (child.name == "Shock shell")
                {
                    child.localScale = Vector3.one * (2 * r);
                    var rr = child.GetComponent<Renderer>(); block.Clear(); block.SetColor("_Color", ShellColor * (fade * (.8f + .4f * Hash(tick, 3)))); rr.SetPropertyBlock(block);
                }
                else if (child.name == "Shock flash")
                {
                    float flash = Mathf.Exp(-age * 5);
                    child.localScale = Vector3.one * (1.2f + 2.5f * grow);
                    var rr = child.GetComponent<Renderer>(); block.Clear(); block.SetColor("_Color", new Color(.5f, .75f, 1.6f, 1) * (1.6f * flash)); rr.SetPropertyBlock(block);
                }
                else if (child.name == "Shock arc")
                {
                    int k = arc++; var line = child.GetComponent<LineRenderer>();
                    // Each arc forks out from the alien to the shell, re-struck 24 times a second.
                    bool on = Hash(tick + k * 5, 11) < .8f && fade > .02f;
                    line.enabled = on; if (!on) continue;
                    var dir = OnSphere(tick + k * 13, 4); float reach = r * (.55f + .45f * Hash(tick + k, 9));
                    var side = Vector3.Cross(dir, Mathf.Abs(dir.y) > .9f ? Vector3.right : Vector3.up).normalized; var up = Vector3.Cross(dir, side);
                    for (int i = 0; i < ArcPoints; i++)
                    {
                        float u = i / (float)(ArcPoints - 1), wob = (i == 0 || i == ArcPoints - 1) ? 0 : reach * .12f;
                        arcBuffer[i] = centre + dir * (reach * u) + side * ((Hash(tick + k * 7, i) - .5f) * 2 * wob) + up * ((Hash(tick + k * 3, i + 20) - .5f) * 2 * wob);
                    }
                    line.SetPositions(arcBuffer);
                    var col = new Color(1, 1, 1, fade); line.startColor = col; line.endColor = new Color(1, 1, 1, fade * .4f);
                    line.widthMultiplier = .05f + .03f * Hash(tick + k, 2);
                }
                else if (child.name == "Shock light")
                {
                    var light = child.GetComponent<Light>(); light.range = radius * 1.8f;
                    light.intensity = 6f * Mathf.Exp(-age * 3.5f) * (.7f + .3f * Hash(tick, 1)) * fade;
                }
            }
        }

        public void PresentDash(Transform fx, float age, float duration, Vector3 start, Vector3 direction, Vector3 body)
        {
            fx.position = start; fx.rotation = Quaternion.identity;
            if (direction.sqrMagnitude < .0001f) direction = Vector3.forward; direction.Normalize();
            var side = Vector3.Cross(Vector3.up, direction); if (side.sqrMagnitude < .001f) side = Vector3.right; side.Normalize();
            float travelled = Mathf.Max(.5f, Vector3.Dot(body - start, direction));
            float streakFade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(AquariumModel.DashSeconds * .6f, duration, age));
            int b = 0, s = 0;
            for (int c = 0; c < fx.childCount; c++)
            {
                var child = fx.GetChild(c);
                if (child.name == "Dash bubble")
                {
                    int j = b++; float along = (j + .5f) / DashBubbles * AquariumModel.DashDistance;
                    float a = age - along / AquariumModel.DashDistance * AquariumModel.DashSeconds, life = .5f + .3f * Hash(j, 5);
                    bool alive = a > 0 && a < life; child.gameObject.SetActive(alive); if (!alive) continue;
                    float size = (.05f + .06f * Hash(j, 6)) * Mathf.SmoothStep(0, 1, a / .06f) * (1 - Mathf.SmoothStep(.5f, 1, a / life));
                    child.position = start + direction * along + side * ((Hash(j, 7) - .5f) * .5f) + Vector3.up * ((Hash(j, 8) - .5f) * .35f + .3f * a);
                    child.localScale = Vector3.one * size;
                }
                else if (child.name == "Dash streak")
                {
                    int k = s++; var line = child.GetComponent<LineRenderer>();
                    line.enabled = streakFade > .01f; if (!line.enabled) continue;
                    var offset = side * ((k - 1) * .22f) + Vector3.up * (k == 1 ? .12f : -.04f);
                    lineBuffer[0] = body - direction * .35f + offset; lineBuffer[1] = body - direction * Mathf.Min(travelled, 3f) + offset * 1.4f;
                    line.SetPositions(lineBuffer); line.widthMultiplier = .045f;
                    line.startColor = new Color(.85f, .96f, 1f, .5f * streakFade); line.endColor = new Color(.85f, .96f, 1f, 0);
                }
            }
        }

        public void PresentGlue(Transform fx, float age, float duration, Vector3 lidTop, int id)
        {
            // R49: small wet flecks flicked off the brush as it travels round the rim (AquariumDrainPresentation
            // paints the glue itself over AquariumDrainPresentation.GluePaintSeconds), gone once it is done.
            fx.position = lidTop; fx.rotation = Quaternion.identity;
            float lid = AquariumDrainLayout.LidRadius, paint = AquariumDrainPresentation.GluePaintSeconds;
            int d = 0;
            for (int c = 0; c < fx.childCount; c++)
            {
                var child = fx.GetChild(c);
                if (child.name == "Puff bubble")
                {
                    int b = c; var ring = new Vector3(Mathf.Cos(b * 2.399f), 0, Mathf.Sin(b * 2.399f));
                    float bs = (.025f + .035f * Hash(b, id)) * Mathf.SmoothStep(0, 1, age / .05f) * (1 - Mathf.SmoothStep(.4f, 1, age / Mathf.Max(.1f, duration)));
                    child.gameObject.SetActive(bs > .002f);
                    child.localPosition = ring * (lid * 1.15f + .3f * (1 - Mathf.Exp(-age * 7))) + Vector3.up * (.04f + .45f * age);
                    child.localScale = Vector3.one * bs; continue;
                }
                if (child.name != "Glue drop") { child.gameObject.SetActive(false); continue; }
                // R51: the glue splats down all at once, flinging gobs outward round the whole rim.
                int j = d++; float born = .06f * Hash(id, j), a = age - born;
                bool alive = a > 0 && a < .5f;
                child.gameObject.SetActive(alive); if (!alive) continue;
                float angle = j * Mathf.PI * 2 / GlueDrops + .25f * Hash(j, id + 3);
                var outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var p = outward * (lid * 1.05f + (.55f + .35f * Hash(j, 5)) * a) + Vector3.up * (.02f + (.7f + .3f * Hash(j, 6)) * a - 3f * a * a);
                if (p.y < -.02f) { child.gameObject.SetActive(false); continue; }
                float size = (.014f + .012f * Hash(j, 9)) * (1 - a / .5f);
                child.localPosition = p; child.localRotation = Quaternion.LookRotation(outward + Vector3.up * (.7f - 6f * a));
                child.localScale = new Vector3(size, size, size * 2.4f);
            }
        }

        public void PresentPuff(Transform fx, float age, float duration, Vector3 at, int id)
        {
            fx.position = at; fx.rotation = Quaternion.identity;
            int j = 0;
            for (int c = 0; c < fx.childCount; c++)
            {
                var child = fx.GetChild(c); int k = j++;
                var dir = OnSphere(id * 17 + k, 3); float a = age;
                float size = (.03f + .04f * Hash(k, id)) * Mathf.SmoothStep(0, 1, a / .05f) * (1 - Mathf.SmoothStep(.55f, 1, a / duration));
                child.gameObject.SetActive(size > .002f);
                child.localPosition = dir * (.35f * (1 - Mathf.Exp(-a * 6))) + Vector3.up * (.5f * a);
                child.localScale = Vector3.one * size;
            }
        }

        // ------------------------------------------------------------------ sparks on stunned divers and fish
        private readonly List<LineRenderer[]> sparkPool = new List<LineRenderer[]>();
        private int sparksUsed;
        private GameObject sparkGroup;
        public void BeginFrame() { sparksUsed = 0; }
        public void Sparks(Vector3 centre, float radius, float time, int seed)
        {
            if (sparkGroup == null) { sparkGroup = new GameObject("Stun sparks"); sparkGroup.transform.SetParent(root, false); }
            if (sparksUsed == sparkPool.Count)
            {
                var set = new LineRenderer[SparkArcs];
                for (int i = 0; i < SparkArcs; i++) set[i] = Line("Stun spark", sparkGroup.transform, arcs, SparkPoints, .022f);
                sparkPool.Add(set);
            }
            var lines = sparkPool[sparksUsed++];
            int tick = Mathf.FloorToInt(time * 18) + seed * 101;
            for (int k = 0; k < lines.Length; k++)
            {
                var line = lines[k];
                bool on = Hash(tick + k * 3, seed) < .75f;
                if (line.gameObject.activeSelf != on) line.gameObject.SetActive(on);
                if (!on) continue;
                // A short crackle hopping over the body: from one point on its shell to a nearby one.
                var a = OnSphere(tick + k * 11, seed + 1); var b = (a + OnSphere(tick + k * 5, seed + 9) * .9f).normalized;
                for (int i = 0; i < SparkPoints; i++)
                {
                    float u = i / (float)(SparkPoints - 1);
                    var p = Vector3.Slerp(a, b, u) * radius * (1.05f + .25f * Mathf.Sin(u * Mathf.PI));
                    if (i > 0 && i < SparkPoints - 1) p += OnSphere(tick + k * 7 + i, seed + 4) * (radius * .12f);
                    sparkBuffer[i] = centre + p;
                }
                line.SetPositions(sparkBuffer);
                line.startColor = line.endColor = new Color(1, 1, 1, .6f + .4f * Hash(tick, k));
            }
        }
        public void EndFrame()
        {
            for (int s = sparksUsed; s < sparkPool.Count; s++)
                foreach (var line in sparkPool[s]) if (line.gameObject.activeSelf) line.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ the drone's red eyes
        // Fish0's pupils in its mesh space (as measured for AquariumDeadEyes).
        private static readonly Vector3[] Fish0Pupils = { new Vector3(.276f, -.034f, .296f), new Vector3(-.277f, -.040f, .294f) };
        public Transform[] MakeDroneEyes(Transform parent)
        {
            var eyes = new Transform[Fish0Pupils.Length];
            for (int i = 0; i < eyes.Length; i++)
            {
                var eye = new GameObject("Drone red eye").transform; eye.SetParent(parent, false);
                Ball("Red pupil", eye, eyeCore).localScale = Vector3.one * .09f;
                Ball("Red glow", eye, eyeGlow).localScale = Vector3.one * .3f;
                eyes[i] = eye;
            }
            return eyes;
        }
        public static void PresentDroneEyes(Transform[] eyes, AquariumRig rig, float time)
        {
            if (eyes == null) return;
            for (int i = 0; i < eyes.Length; i++)
            {
                if (eyes[i] == null) continue;
                bool on = rig != null && rig.skin != null && rig.gameObject.activeInHierarchy && i < Fish0Pupils.Length;
                if (eyes[i].gameObject.activeSelf != on) eyes[i].gameObject.SetActive(on);
                if (!on) continue;
                var pupil = Fish0Pupils[i]; var outward = new Vector3(Mathf.Sign(pupil.x) * .7f, 0, .7f);
                var at = rig.BodyPoint(pupil + outward * .012f);
                float scale = (rig.BodyPoint(pupil + Vector3.right * .5f) - rig.BodyPoint(pupil - Vector3.right * .5f)).magnitude;
                eyes[i].position = at;
                eyes[i].localScale = Vector3.one * (scale * (1 + .08f * Mathf.Sin(time * 9 + i)) / Mathf.Max(.0001f, eyes[i].parent != null ? eyes[i].parent.lossyScale.x : 1));
            }
        }

        // ------------------------------------------------------------------ vanish (blue alien)
        private sealed class Worn { public Material[] original, ghost; public bool wasOff; }
        private readonly Dictionary<Renderer, Worn> ghosts = new Dictionary<Renderer, Worn>();
        private readonly Dictionary<Material, Material> ghostMaterials = new Dictionary<Material, Material>();
        private readonly Dictionary<Renderer, bool> hiddenParts = new Dictionary<Renderer, bool>();
        private readonly HashSet<Renderer> ghostedNow = new HashSet<Renderer>();
        private readonly List<Renderer> ghostScratch = new List<Renderer>(), ghostDone = new List<Renderer>();
        private Shader ghostShader;
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha"), ShimmerId = Shader.PropertyToID("_Shimmer");
        public void BeginGhosts() { ghostedNow.Clear(); }
        // alpha 1 looks normal, 0 is gone (no shadow either); shimmer 1 is the teammates' cool ghost edge.
        public void Ghost(GameObject body, float alpha, float shimmer)
        {
            if (ghostShader == null) ghostShader = Resources.Load<Shader>("AquariumCharacters/AlienGhost");
            if (ghostShader == null) return;
            body.GetComponentsInChildren(false, ghostScratch);
            foreach (var renderer in ghostScratch)
            {
                if (renderer is LineRenderer || renderer.name == "Candidate silhouette") continue;
                if (renderer is SkinnedMeshRenderer)
                {
                    if (!ghosts.TryGetValue(renderer, out var worn))
                    {
                        var original = renderer.sharedMaterials; var ghost = new Material[original.Length];
                        for (int i = 0; i < original.Length; i++) ghost[i] = GhostOf(original[i]);
                        worn = new Worn { original = original, ghost = ghost, wasOff = renderer.forceRenderingOff }; ghosts.Add(renderer, worn);
                        renderer.sharedMaterials = ghost;
                    }
                    ghostedNow.Add(renderer);
                    renderer.forceRenderingOff = worn.wasOff || alpha < .01f;
                    renderer.GetPropertyBlock(block); block.SetFloat(AlphaId, alpha); block.SetFloat(ShimmerId, shimmer); renderer.SetPropertyBlock(block);
                }
                else
                {
                    // Small rigid parts (marks, glints) simply drop out once the body is more gone than there.
                    if (!hiddenParts.ContainsKey(renderer)) hiddenParts.Add(renderer, renderer.forceRenderingOff);
                    ghostedNow.Add(renderer);
                    renderer.forceRenderingOff = alpha < .5f;
                }
            }
        }
        private Material GhostOf(Material source)
        {
            if (source == null) source = eyeCore;
            if (ghostMaterials.TryGetValue(source, out var made) && made != null) return made;
            made = new Material(ghostShader) { name = source.name + " (ghost)", hideFlags = HideFlags.DontSave };
            if (source.HasProperty("_MainTex")) made.mainTexture = source.mainTexture;
            if (source.HasProperty("_Tint")) made.SetColor("_Tint", source.GetColor("_Tint"));
            else if (source.HasProperty("_Color")) made.SetColor("_Tint", source.color);
            ghostMaterials[source] = made; owned.Add(made); return made;
        }
        public void EndGhosts()
        {
            ghostDone.Clear();
            foreach (var pair in ghosts) if (!ghostedNow.Contains(pair.Key)) ghostDone.Add(pair.Key);
            foreach (var renderer in ghostDone)
            {
                if (renderer != null && renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials[0] == ghosts[renderer].ghost[0]) renderer.sharedMaterials = ghosts[renderer].original;
                if (renderer != null) renderer.forceRenderingOff = ghosts[renderer].wasOff;
                ghosts.Remove(renderer);
            }
            ghostDone.Clear();
            foreach (var pair in hiddenParts) if (!ghostedNow.Contains(pair.Key)) ghostDone.Add(pair.Key);
            foreach (var renderer in ghostDone) { if (renderer != null) renderer.forceRenderingOff = hiddenParts[renderer]; hiddenParts.Remove(renderer); }
        }

        // ------------------------------------------------------------------ x-ray (diver kind 1)
        private CommandBuffer xray;
        private Camera xrayCamera;
        private Material xrayMaterial;
        private readonly List<Renderer> xrayScratch = new List<Renderer>();
        private static readonly Color XrayRed = new Color(1f, .06f, .04f, .9f);
        public void BeginXray(Camera camera, float weight)
        {
            if (xrayMaterial == null)
            {
                var shader = Resources.Load<Shader>("AquariumXraySilhouette");
                if (shader == null) return;
                xrayMaterial = new Material(shader) { name = "X-ray silhouette", hideFlags = HideFlags.DontSave }; owned.Add(xrayMaterial);
            }
            if (xray == null) xray = new CommandBuffer { name = "Diver x-ray silhouettes" };
            if (camera != xrayCamera)
            {
                if (xrayCamera != null) xrayCamera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, xray);
                xrayCamera = camera;
                if (camera != null) camera.AddCommandBuffer(CameraEvent.AfterForwardAlpha, xray);
            }
            xray.Clear();
            xrayMaterial.SetColor("_Color", new Color(XrayRed.r, XrayRed.g, XrayRed.b, XrayRed.a * Mathf.Clamp01(weight)));
        }
        public void StopXray()
        {
            if (xray != null) xray.Clear();
            if (xrayCamera != null && xray != null) xrayCamera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, xray);
            xrayCamera = null;
        }
        public bool XrayActive => xray != null && xrayMaterial != null;
        public void XrayAdd(GameObject body)
        {
            if (xray == null || xrayMaterial == null || body == null) return;
            body.GetComponentsInChildren(false, xrayScratch);
            foreach (var renderer in xrayScratch)
            {
                if (!renderer.enabled || renderer is LineRenderer || renderer.name == "Candidate silhouette") continue;
                Mesh mesh = null;
                var skin = renderer as SkinnedMeshRenderer;
                if (skin != null) mesh = skin.sharedMesh;
                else { var filter = renderer.GetComponent<MeshFilter>(); if (filter != null) mesh = filter.sharedMesh; }
                if (mesh == null) continue;
                for (int s = 0; s < mesh.subMeshCount; s++) xray.DrawRenderer(renderer, xrayMaterial, s, 0);
            }
        }

        public void Dispose()
        {
            if (xrayCamera != null && xray != null) xrayCamera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, xray);
            xray?.Release(); xray = null; xrayCamera = null;
            BeginGhosts(); EndGhosts();
            if (sparkGroup != null) Release(sparkGroup);
            foreach (var o in owned) Release(o); owned.Clear();
        }
        private static void Release(Object o) { if (o == null) return; if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }
    }
}

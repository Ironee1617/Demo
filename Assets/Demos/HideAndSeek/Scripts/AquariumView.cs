using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // Procedural artwork stays separate from the host-authoritative game rules.
    public sealed class AquariumView : IDisposable
    {
        // Keep value samples: host snapshots share mutable Swimmer/Prey objects.
        private sealed class PositionHistory
        {
            private struct Sample { public Vector3 position; public float time; }
            private readonly Sample[] samples = new Sample[6];
            private int count;
            public void Clear() { count = 0; }
            public Vector3 Present(Vector3 position, float sampleTime, float renderTime, bool reset, out bool snapped)
            {
                snapped = reset || count == 0 || sampleTime < samples[0].time;
                if (!snapped)
                {
                    float gap = sampleTime - samples[0].time;
                    snapped = Vector3.Distance(position, samples[0].position) > Mathf.Max(3, gap * 10);
                }
                if (snapped)
                {
                    count = 1; samples[0] = new Sample { position = position, time = sampleTime };
                    return position;
                }
                if (sampleTime > samples[0].time)
                {
                    for (int i = Mathf.Min(count, samples.Length - 1); i > 0; i--) samples[i] = samples[i - 1];
                    count = Mathf.Min(count + 1, samples.Length);
                }
                samples[0] = new Sample { position = position, time = sampleTime };
                // Hold at the newest authoritative position instead of predicting through a wall.
                if (renderTime >= samples[0].time) return samples[0].position;
                for (int i = 1; i < count; i++)
                    if (renderTime >= samples[i].time)
                        return Vector3.Lerp(samples[i].position, samples[i - 1].position,
                            Mathf.InverseLerp(samples[i].time, samples[i - 1].time, renderTime));
                return samples[count - 1].position;
            }
        }
        private sealed class Actor
        {
            public GameObject root, normalMouth, predatorMouth;
            public Transform tail;
            public Transform[] tentacles;
            public string kind;
            public AquariumCreature creature;
            public AquariumRig diver;
            public AquariumHarpoonGun gun;
            public LineRenderer tether;
            public CapsuleCollider bodyCollider;
            public float moveSpeed,bank;
            public Vector3 velocity,shotTip,shotDirection,dockStart,dockAnchor;
            public bool shotPresented,docking;
            public readonly PositionHistory positions = new PositionHistory();
            public bool dead, carried;
        }
        private sealed class OutlinePart { public Renderer source, shell; }
        private readonly List<OutlinePart> outlineParts = new List<OutlinePart>();
        private Actor outlinedActor;
        private readonly Material outlineMaterial;
        private bool worldFrozen, freezeExistingPoses;
        private int frozenRound;
        private float environmentTime,presentationTime=-1,lastStateTime=-1;
        private int presentationRound;
        private RoundPhase presentationPhase;
        private readonly List<SkinnedMeshRenderer> outlineSkins=new List<SkinnedMeshRenderer>();
        private readonly List<MeshFilter> outlineFilters=new List<MeshFilter>();
        private readonly List<Renderer> cameraRenderers=new List<Renderer>();
        private readonly Transform root;
        private readonly Dictionary<string, Actor> actors = new Dictionary<string, Actor>();
        private readonly Dictionary<int,string> playerKeys=new Dictionary<int,string>();
        private readonly Dictionary<int,string> fishKeys=new Dictionary<int,string>();
        private static readonly string[] fishKinds={"fish0","fish1","fish2"};
        private string PlayerKey(int id)=>ActorKey(playerKeys,"p",id);
        private string FishKey(int id)=>ActorKey(fishKeys,"f",id);
        private static string ActorKey(Dictionary<int,string> cache,string prefix,int id)
        {if(!cache.TryGetValue(id,out var key)){key=prefix+id;cache.Add(id,key);}return key;}
        private static string FishKind(int species)=>species>=0 && species<fishKinds.Length?fishKinds[species]:"fish"+species;
        private readonly Dictionary<int, GameObject> effects = new Dictionary<int, GameObject>();
        private const float HarpoonDockSeconds = .6f;
        private readonly List<Material> materials = new List<Material>();


        private readonly List<Renderer> glassPanels = new List<Renderer>();
        private readonly HashSet<string> visible = new HashSet<string>();
        private readonly HashSet<int> activeEffects = new HashSet<int>();
        private readonly List<string> removedActors = new List<string>();
        private readonly List<int> expiredEffects = new List<int>();
        private readonly HashSet<Actor> hiddenByCamera = new HashSet<Actor>();
        private readonly Dictionary<Renderer, bool> hiddenRendererStates = new Dictionary<Renderer, bool>();
        private readonly Color[] colors = { new Color(1f, 0.49f, 0.12f), new Color(0.25f, 0.55f, 0.91f), new Color(0.94f, 0.45f, 0.62f) };
        private readonly Material sand, dark, teal, white, black, purple, steel, glass, mouthLining, eggShell;
        private readonly Material[] fishMaterials, finMaterials;
        private readonly Material lineMaterial;
        private readonly Mesh fanMesh, coneMesh;
        private readonly Mesh[] biscuitMeshes, crumbMeshes;
        private readonly Material splashWater, splashFoam, splashBubble;
        private readonly MaterialPropertyBlock splashProperties = new MaterialPropertyBlock();
        private readonly Material biscuit;
        private Texture2D biscuitTexture;
        private readonly LineRenderer localRing;
        private readonly System.Random random = new System.Random(54);
        public Camera Camera { get; private set; }
        private AquariumFirstPersonEquipment equipment;

        public AquariumView(Transform parent)
        {
            root = new GameObject("Aquarium visuals").transform; root.SetParent(parent);
            sand = Material(new Color(0.56f, 0.53f, 0.4f)); sand.SetFloat("_Glossiness", 0.08f);
            dark = Material(new Color(0.025f, 0.09f, 0.16f)); teal = Material(new Color(0.045f, 0.32f, 0.39f));
            white = Material(new Color(0.96f, 0.99f, 1f)); black = Material(new Color(0.012f, 0.015f, 0.045f));
            purple = Material(new Color(0.81f, 0.68f, 0.97f)); steel = Material(new Color(0.66f, 0.83f, 0.9f));
            mouthLining = Material(new Color(0.89f, 0.28f, 0.5f));
            // Revival egg: one glossy, candy-yellow shell (a faint warm glow keeps it readable in deep water).
            eggShell = Material(new Color(1f, 0.82f, 0.12f)); eggShell.SetFloat("_Glossiness", 0.9f); eggShell.SetFloat("_Metallic", 0.12f);
            eggShell.EnableKeyword("_EMISSION"); eggShell.SetColor("_EmissionColor", new Color(0.16f, 0.1f, 0f));
            glass = Material(new Color(0.15f, 0.48f, 0.56f, 0.055f), true);
            lineMaterial = new Material(Shader.Find("Sprites/Default")); materials.Add(lineMaterial);
            outlineMaterial = new Material(Resources.Load<Shader>("AquariumTargetOutline"));
            outlineMaterial.SetColor("_OutlineColor", new Color(1f, 0.86f, 0.25f)); outlineMaterial.SetFloat("_OutlineWidth", 0.035f); materials.Add(outlineMaterial);
            fishMaterials = new Material[3]; finMaterials = new Material[3];
            for (int i = 0; i < 3; i++) { fishMaterials[i] = Material(colors[i]); finMaterials[i] = Material(Color.Lerp(colors[i], Color.white, 0.38f)); }
            fanMesh = MakeFanMesh(); coneMesh = MakeConeMesh();
            biscuitTexture = MakeBiscuitTexture(); biscuit = Material(Color.white); biscuit.mainTexture = biscuitTexture; biscuit.SetFloat("_Glossiness", 0.12f);
            biscuitMeshes = new Mesh[4]; for (int i = 0; i < biscuitMeshes.Length; i++) biscuitMeshes[i] = MakeBiscuitMesh(i);
            crumbMeshes = new Mesh[3]; for (int i = 0; i < crumbMeshes.Length; i++) crumbMeshes[i] = MakeCrumbMesh(i);
            splashWater = Material(new Color(.92f, .98f, 1f, .95f), true); splashFoam = Material(new Color(1f, 1f, 1f, .8f), true);
            splashBubble = Material(new Color(.82f, .95f, 1f, .5f), true);
            AquariumCreature.Prewarm();
            BuildEnvironment();
            localRing = Ring("Your swimmer", root, 0.68f, new Color(0.59f, 1f, 0.81f, 0.85f), 0.035f);
            Camera = UnityEngine.Camera.main;
            if (Camera == null) { var go = new GameObject("Aquarium Camera"); go.tag = "MainCamera"; Camera = go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); }
            Camera.depthTextureMode|=DepthTextureMode.Depth;
            Camera.transform.position = new Vector3(0, 23, -20);
            Camera.transform.rotation = Quaternion.Euler(49, 0, 0);
            Camera.orthographic = false; Camera.fieldOfView = 68; Camera.nearClipPlane = 0.06f; Camera.farClipPlane = 100;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(0.025f, 0.12f, 0.18f);
            AquariumEnvironment.ConfigureCamera(Camera);
            AquariumEnvironment.ConfigureLighting(root);
            equipment = new AquariumFirstPersonEquipment(Camera);
        }

        private Material Material(Color color, bool transparent = false)
        {
            // This built-in alpha shader is also used by the rings and included in the player build.
            // It keeps procedural glass translucent without depending on a stripped Standard variant.
            var mat = new Material(Shader.Find(transparent ? "Sprites/Default" : "Standard")); mat.color = color;
            if (transparent) mat.renderQueue = 3000;
            else mat.SetFloat("_Glossiness", 0.28f);
            materials.Add(mat); return mat;
        }
        private GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 at, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = at; go.transform.localScale = size;
            ReleaseObject(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = mat;
            if (mat == glass)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                if (type == PrimitiveType.Cube) glassPanels.Add(renderer);
            }
            return go;
        }
        private GameObject Box(string name, Transform parent, Vector3 at, Vector3 size, Material mat)
        { return Shape(name, PrimitiveType.Cube, parent, at, size, mat); }
        private GameObject Ball(string name, Transform parent, Vector3 at, Vector3 size, Material mat)
        { return Shape(name, PrimitiveType.Sphere, parent, at, size, mat); }
        private float Range(float a, float b) { return a + (float)random.NextDouble() * (b - a); }

        private void BuildEnvironment()
        {
            var environment = AquariumEnvironment.Ensure(root);
            foreach (var renderer in environment.GetComponentsInChildren<Renderer>())
                if (renderer.name == "04 • Curved aquarium glass") glassPanels.Add(renderer);
        }

        private Mesh MakeFanMesh()
        {
            var mesh = new Mesh { name = "Shared aquarium fan" };
            mesh.vertices = new[] { new Vector3(0, 0, 0.2f), new Vector3(-0.45f, 0, -0.6f), new Vector3(0.45f, 0, -0.6f), new Vector3(0, 0.13f, -0.36f) };
            mesh.triangles = new[] { 0, 1, 3, 0, 3, 2, 1, 2, 3, 0, 2, 1 }; mesh.RecalculateNormals(); return mesh;
        }
        private Mesh MakeConeMesh()
        {
            const int sides = 8;
            var mesh = new Mesh { name = "Shared aquarium tooth" }; var vertices = new Vector3[sides + 2]; var triangles = new int[sides * 6];
            vertices[0] = Vector3.up; vertices[sides + 1] = Vector3.zero;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides; vertices[i + 1] = new Vector3(Mathf.Sin(angle) * 0.5f, 0, Mathf.Cos(angle) * 0.5f);
                int next = (i + 1) % sides + 1;
                triangles[i * 6] = 0; triangles[i * 6 + 1] = i + 1; triangles[i * 6 + 2] = next;
                triangles[i * 6 + 3] = sides + 1; triangles[i * 6 + 4] = next; triangles[i * 6 + 5] = i + 1;
            }
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); return mesh;
        }
        // ---------------------------------------------------------------- biscuit food
        // A square cracker (rounded corners, docking holes) broken along one or two jagged lines.
        // Top and bottom are planar-mapped onto the biscuit face of the texture; the baked crust and the
        // paler crumb of a broken edge use the two strips at its right.
        private static Mesh MakeBiscuitMesh(int seed)
        {
            const float half = 0.065f, corner = 0.018f, thick = 0.0125f, bevel = 0.005f;
            List<Vector2> outline = null; List<bool> broken = null;
            // The caps are fanned from the centre, so keep the broken outline star-shaped (retry with a finer break).
            for (int attempt = 0; attempt < 6; attempt++)
            {
                BiscuitOutline(seed * 13 + attempt, half, corner, attempt < 3 ? 1 : .4f, out outline, out broken);
                if (StarShaped(outline)) break;
            }
            var centre = Vector2.zero; foreach (var p in outline) centre += p; centre /= outline.Count;
            int count = outline.Count;
            var vertices = new List<Vector3>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
            System.Func<Vector2, Vector2> FaceUV = p => new Vector2(0.03f + 0.82f * (p.x / (2 * half) + 0.5f), 0.03f + 0.94f * (p.y / (2 * half) + 0.5f));
            for (int side = 1; side >= -1; side -= 2)
            {
                // Each face: an inset cap plus a bevel ring down to the outer edge (rounded, baked rim).
                int capCentre = vertices.Count; vertices.Add(new Vector3(centre.x, side * thick, centre.y)); uvs.Add(FaceUV(centre));
                int inner = vertices.Count;
                for (int i = 0; i < count; i++)
                {
                    var inset = outline[i] + (centre - outline[i]).normalized * bevel;
                    vertices.Add(new Vector3(inset.x, side * thick, inset.y)); uvs.Add(FaceUV(inset));
                }
                int outer = vertices.Count;
                for (int i = 0; i < count; i++)
                { vertices.Add(new Vector3(outline[i].x, side * (thick - bevel * 0.8f), outline[i].y)); uvs.Add(FaceUV(outline[i]) * 0.98f + new Vector2(0.01f, 0.01f)); }
                for (int i = 0; i < count; i++)
                {
                    int j = (i + 1) % count;
                    if (side > 0) { triangles.Add(capCentre); triangles.Add(inner + j); triangles.Add(inner + i); triangles.Add(inner + i); triangles.Add(inner + j); triangles.Add(outer + j); triangles.Add(inner + i); triangles.Add(outer + j); triangles.Add(outer + i); }
                    else { triangles.Add(capCentre); triangles.Add(inner + i); triangles.Add(inner + j); triangles.Add(inner + i); triangles.Add(outer + j); triangles.Add(inner + j); triangles.Add(inner + i); triangles.Add(outer + i); triangles.Add(outer + j); }
                }
            }
            // Side wall: crust on the baked rim, pale crumb on the broken edges (split vertices for hard edges).
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count; bool crumb = broken[i];
                float u = crumb ? 0.975f : 0.915f;
                int b = vertices.Count;
                float top = thick - bevel * 0.8f;
                vertices.Add(new Vector3(outline[i].x, top, outline[i].y)); vertices.Add(new Vector3(outline[j].x, top, outline[j].y));
                vertices.Add(new Vector3(outline[j].x, -top, outline[j].y)); vertices.Add(new Vector3(outline[i].x, -top, outline[i].y));
                for (int k = 0; k < 4; k++) uvs.Add(new Vector2(u, k < 2 ? 0.8f : 0.2f));
                triangles.Add(b); triangles.Add(b + 1); triangles.Add(b + 2); triangles.Add(b); triangles.Add(b + 2); triangles.Add(b + 3);
            }
            var mesh = new Mesh { name = "Biscuit piece " + seed };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            // Keep the outward winding whichever way the cut polygon was traversed.
            mesh.RecalculateNormals(); var normals = mesh.normals; float facing = 0;
            for (int i = 0; i < normals.Length; i++) facing += Vector3.Dot(normals[i], vertices[i] - new Vector3(centre.x, 0, centre.y));
            if (facing < 0) { var t = mesh.triangles; for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; } mesh.triangles = t; mesh.RecalculateNormals(); }
            mesh.RecalculateBounds(); return mesh;
        }
        private static void BiscuitOutline(int seed, float half, float corner, float jitter, out List<Vector2> outline, out List<bool> broken)
        {
            var rnd = new System.Random(311 + seed * 97);
            System.Func<float, float, float> R = (a, b) => a + (float)rnd.NextDouble() * (b - a);
            outline = new List<Vector2>(); broken = new List<bool>();
            for (int q = 0; q < 4; q++)
            {
                var c = new Vector2((q == 0 || q == 3 ? 1 : -1) * (half - corner), (q < 2 ? 1 : -1) * (half - corner));
                float start = q * 90f;
                for (int k = 0; k <= 6; k++) { float a = (start + k * 15f) * Mathf.Deg2Rad; outline.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * corner); broken.Add(false); }
            }
            int cuts = seed % 2 == 0 ? 1 : 2; float firstAngle = R(0, Mathf.PI * 2);
            for (int cut = 0; cut < cuts; cut++)
            {
                // A second break runs roughly across the first one, so no piece becomes a thin sliver.
                float angle = cut == 0 ? firstAngle : firstAngle + R(1.05f, 2.1f); var n = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float offset = cut == 0 ? R(-0.01f, 0.02f) : R(0.004f, 0.026f);
                var clipped = new List<Vector2>(); var clippedBroken = new List<bool>();
                for (int i = 0; i < outline.Count; i++)
                {
                    Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                    float da = Vector2.Dot(a, n) - offset, db = Vector2.Dot(b, n) - offset;
                    if (da <= 0) { clipped.Add(a); clippedBroken.Add(broken[i]); }
                    // Leaving the kept side starts the break edge; re-entering continues the original edge.
                    if ((da <= 0) != (db <= 0)) { clipped.Add(Vector2.Lerp(a, b, da / (da - db))); clippedBroken.Add(da <= 0 || broken[i]); }
                }
                // Roughen the straight break: subdivide every broken edge and jitter it in and out.
                outline = new List<Vector2>(); broken = new List<bool>();
                for (int i = 0; i < clipped.Count; i++)
                {
                    outline.Add(clipped[i]); broken.Add(clippedBroken[i]);
                    if (!clippedBroken[i]) continue;
                    Vector2 a = clipped[i], b = clipped[(i + 1) % clipped.Count]; float length = (b - a).magnitude;
                    int steps = Mathf.Max(2, Mathf.RoundToInt(length / 0.011f));
                    var normal = new Vector2(a.y - b.y, b.x - a.x).normalized;
                    for (int k = 1; k < steps; k++) { outline.Add(Vector2.Lerp(a, b, k / (float)steps) + normal * R(-0.0045f, 0.0045f) * jitter); broken.Add(true); }
                }
            }
        }
        private static bool StarShaped(List<Vector2> outline)
        {
            var centre = Vector2.zero; foreach (var p in outline) centre += p; centre /= outline.Count;
            float sign = 0;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = outline[i] - centre, b = outline[(i + 1) % outline.Count] - centre; float cross = a.x * b.y - a.y * b.x;
                if (Mathf.Abs(cross) < 1e-7f) continue;
                if (sign == 0) sign = Mathf.Sign(cross); else if (Mathf.Sign(cross) != sign) return false;
            }
            return true;
        }
        // ---------------------------------------------------------------- dive splash
        // The diver's entry: a crown of droplets thrown up on arcs (stretched along their flight, falling back),
        // a foam patch and two rings spreading on the surface (fast out, slow fade), and a bubble cloud dragged
        // down under the surface that wobbles back up a beat later (follow-through).
        private const int SplashDrops = 30, SplashBubbles = 26;
        private GameObject MakeSplash()
        {
            var fx = new GameObject("Dive splash"); fx.transform.SetParent(root, false);
            for (int i = 0; i < SplashDrops; i++) Quiet(Ball("Splash drop", fx.transform, Vector3.zero, Vector3.zero, splashWater));
            Quiet(Ball("Splash foam", fx.transform, Vector3.zero, Vector3.zero, splashFoam));
            for (int i = 0; i < 2; i++) Ring("Splash ring", fx.transform, 1, new Color(1, 1, 1, .8f), .07f);
            for (int i = 0; i < SplashBubbles; i++) Quiet(Ball("Splash bubble", fx.transform, Vector3.zero, Vector3.zero, splashBubble));
            return fx;
        }
        private static void Quiet(GameObject go)
        { var r = go.GetComponent<Renderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        private void PresentSplash(Transform fx, float seconds, WaterEffect e)
        {
            fx.position = new Vector3(e.position.x, e.position.y + .03f, e.position.z); fx.rotation = Quaternion.identity;
            var forward = new Vector3(e.direction.x, 0, e.direction.z); forward = forward.sqrMagnitude > .0001f ? forward.normalized : Vector3.forward;
            int drop = 0, bubble = 0, ring = 0;
            for (int c = 0; c < fx.childCount; c++)
            {
                var child = fx.GetChild(c);
                if (child.name == "Splash drop")
                {
                    // The first few drops form a tall central plume, the rest a wide crown.
                    int i = drop++; bool plume = i < 7; float a = seconds - .03f * Hash(i, 21);
                    float angle = (i + .4f * Hash(i, 22)) * Mathf.PI * 2 / (plume ? 7 : SplashDrops - 7);
                    var outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (plume ? .35f : 1) + forward * .35f;
                    float up = plume ? 3.6f + 1.2f * Hash(i, 23) : 2.3f + 1.6f * Hash(i, 23), spread = plume ? .25f + .2f * Hash(i, 24) : .7f + .9f * Hash(i, 24);
                    var position = outward * (.3f + spread * a) + Vector3.up * (up * a - 4.9f * a * a);
                    bool alive = a > 0 && position.y > -.03f;
                    child.gameObject.SetActive(alive); if (!alive) continue;
                    var velocity = outward * spread + Vector3.up * (up - 9.8f * a);
                    float size = (.085f + .075f * Hash(i, 25)) * (1 - .4f * Mathf.Clamp01(a / .8f)), stretch = 1 + Mathf.Min(1.4f, velocity.magnitude * .32f);
                    child.localPosition = position; child.localRotation = Quaternion.LookRotation(velocity.normalized);
                    child.localScale = new Vector3(size / Mathf.Sqrt(stretch), size / Mathf.Sqrt(stretch), size * stretch);
                }
                else if (child.name == "Splash foam")
                {
                    float grow = 1 - Mathf.Exp(-3.2f * seconds), fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, 1.9f, seconds));
                    child.gameObject.SetActive(fade > .01f);
                    child.localPosition = forward * .15f; child.localScale = new Vector3(.8f + 1.7f * grow, .03f, .8f + 1.5f * grow);
                    var r = child.GetComponent<Renderer>(); r.GetPropertyBlock(splashProperties); splashProperties.SetColor("_Color", new Color(1, 1, 1, .75f * fade)); r.SetPropertyBlock(splashProperties);
                }
                else if (child.name == "Splash ring")
                {
                    int k = ring++; float a = seconds - .18f * k, grow = 1 - Mathf.Exp(-2.0f * Mathf.Max(0, a));
                    float fade = a <= 0 ? 0 : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, 2.2f, a));
                    child.gameObject.SetActive(fade > .01f);
                    child.localPosition = forward * .2f * grow; child.localScale = Vector3.one * (.35f + (2.0f - .5f * k) * grow);
                    var line = child.GetComponent<LineRenderer>(); var color = new Color(1, 1, 1, .8f * fade); line.startColor = line.endColor = color;
                }
                else if (child.name == "Splash bubble")
                {
                    int j = bubble++; float born = .02f + .3f * Hash(j, 31), life = 1.3f + .7f * Hash(j, 33), a = seconds - born;
                    float angle = Hash(j, 34) * Mathf.PI * 2, reach = .35f * Hash(j, 35);
                    var start = new Vector3(Mathf.Cos(angle) * reach, -(.3f + 1.1f * Hash(j, 32)), Mathf.Sin(angle) * reach) + forward * (.25f + .7f * Hash(j, 36));
                    var position = start + Vector3.up * (.32f * a + .22f * a * a) + new Vector3(Mathf.Sin(a * 7 + j), 0, Mathf.Cos(a * 6 + j * 1.7f)) * .03f;
                    bool alive = a > 0 && a < life && position.y < -.04f;
                    child.gameObject.SetActive(alive); if (!alive) continue;
                    float size = (.03f + .05f * Hash(j, 37)) * Mathf.SmoothStep(0, 1, a / .08f) * (1 - Mathf.SmoothStep(.6f, 1, a / life));
                    child.localPosition = position; child.localRotation = Quaternion.identity; child.localScale = new Vector3(size, size * .85f, size);
                }
            }
        }

        // ---------------------------------------------------------------- biscuit crumbling
        // Each piece stays whole for a while, then breaks down in four chips: a quick crack (a small
        // pop and settle) knocks the piece down a size and throws two crumbs out on an arc; the crumbs
        // drag to a stop, sink and shrink away. The last nub dissolves into a final pair of crumbs, and
        // every piece is gone before the feed ends. Staggered start times keep the pieces from crumbling
        // in unison. Everything is a pure function of the feed's age, so replays and late joins agree.
        private const int CrumbsPerPiece = 10, BiscuitChips = 4;
        private static float Hash(int a, int b) { return Mathf.Repeat(Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f, 1); }
        private static void BiscuitSchedule(int piece, float duration, out float start, out float end)
        {
            start = duration * (.16f + .36f * Hash(piece, 1));
            end = Mathf.Min(duration * .8f, start + duration * (.26f + .07f * Hash(piece, 2)));
        }
        // Remaining size of a crumbling piece (1 = whole) and the time each chip breaks off (k = chip, BiscuitChips = last nub).
        private static float ChipTime(float start, float end, int k) { return Mathf.Lerp(start, end, k < BiscuitChips ? k * .2f : .84f); }
        private static float BiscuitSize(float start, float end, float t)
        {
            if (t <= start) return 1;
            float size = 1, pop = 0;
            for (int k = 0; k < BiscuitChips; k++)
            {
                float at = ChipTime(start, end, k), u = Mathf.Clamp01((t - at) / .09f);
                size -= .18f * Mathf.SmoothStep(0, 1, u);
                if (u > 0 && u < 1) pop += .10f * Mathf.Sin(u * Mathf.PI);
            }
            float last = ChipTime(start, end, BiscuitChips), fade = Mathf.Clamp01((t - last) / Mathf.Max(.05f, end - last));
            size *= 1 - fade * fade * (3 - 2 * fade);
            return Mathf.Max(0, size) * (1 + pop);
        }
        private static Vector3 BiscuitDrift(int i, float seconds)
        { return new Vector3(Mathf.Sin(i * 2.4f) * .5f, Mathf.Cos(i * 1.7f) * .25f + .04f * Mathf.Sin(seconds * 1.6f + i), Mathf.Cos(i * 2.4f) * .5f); }
        private void PresentBiscuits(Transform fx, float seconds, float duration)
        {
            int piece = 0, crumb = 0;
            for (int c = 0; c < fx.childCount; c++)
            {
                var child = fx.GetChild(c);
                if (child.name == "Biscuit piece")
                {
                    int i = piece++;
                    BiscuitSchedule(i, duration, out float start, out float end);
                    float size = BiscuitSize(start, end, seconds), baseScale = 1.5f + .45f * Mathf.Repeat(i * .618f, 1);
                    child.gameObject.SetActive(size > .001f);
                    child.localPosition = BiscuitDrift(i, seconds);
                    // Each crack jolts the piece's tumble a little (follow-through after the break).
                    float jolt = 0; for (int k = 0; k <= BiscuitChips; k++) { float u = seconds - ChipTime(start, end, k); if (u > 0 && seconds > start) jolt += 18 * (1 - Mathf.Exp(-u * 9)) * (Hash(i, 10 + k) - .5f); }
                    child.localRotation = Quaternion.Euler(Mathf.Sin(i * 1.3f) * 35 + seconds * (14 + i * 3) + jolt, i * 67 + seconds * (22 - i * 2.5f), Mathf.Cos(i * 2.1f) * 35 - jolt * .6f);
                    child.localScale = Vector3.one * (baseScale * size);
                }
                else if (child.name == "Biscuit crumb")
                {
                    int j = crumb++, owner = j / CrumbsPerPiece, k = j % CrumbsPerPiece / 2, which = j % 2;
                    BiscuitSchedule(owner, duration, out float start, out float end);
                    float born = ChipTime(start, end, k) + which * .04f, life = Mathf.Min(1.2f + .5f * Hash(owner * 31 + j, 3), duration - .05f - born), age = seconds - born;
                    bool alive = age >= 0 && age < life && seconds > start;
                    child.gameObject.SetActive(alive);
                    if (!alive) continue;
                    // Thrown out from the piece's edge on an arc; water drag stops it, then it sinks and shrinks away.
                    float angle = Hash(owner * 31 + j, 4) * Mathf.PI * 2, rise = Hash(owner * 31 + j, 5) - .35f;
                    var direction = new Vector3(Mathf.Cos(angle), rise * .8f, Mathf.Sin(angle)).normalized;
                    float baseScale = 1.5f + .45f * Mathf.Repeat(owner * .618f, 1), edge = .09f * baseScale * Mathf.Max(.3f, BiscuitSize(start, end, born));
                    float speed = .32f + .22f * Hash(owner * 31 + j, 6), travel = speed * (1 - Mathf.Exp(-3.2f * age)) / 3.2f;
                    child.localPosition = BiscuitDrift(owner, born) + direction * (edge + travel) + Vector3.down * (.11f * age * age / (age + .35f))
                        + new Vector3(Mathf.Sin(age * 3.1f + j), 0, Mathf.Cos(age * 2.7f + j)) * .012f * age;
                    child.localRotation = Quaternion.Euler(Hash(j, 7) * 360 + age * (160 + 120 * Hash(j, 8)), Hash(j, 9) * 360 + age * 90, Hash(j, 11) * 360);
                    float grow = Mathf.SmoothStep(0, 1, age / .06f), shrink = 1 - Mathf.SmoothStep(.45f, 1, age / life);
                    child.localScale = Vector3.one * ((.8f + .5f * Hash(j, 12)) * grow * shrink * (k == BiscuitChips ? 1.25f : 1));
                }
            }
        }
        // A small jagged crumb: an irregular toasted chunk, pale where it broke.
        private static Mesh MakeCrumbMesh(int seed)
        {
            int sides = 5 + seed % 2; var outline = new List<Vector2>(); var random = new System.Random(9000 + seed * 17);
            for (int i = 0; i < sides; i++) { float a = (i + (float)random.NextDouble() * .5f) / sides * Mathf.PI * 2; float r = .013f + .009f * (float)random.NextDouble(); outline.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r)); }
            float thick = .008f + .003f * (float)random.NextDouble();
            var vertices = new List<Vector3>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
            for (int side = 1; side >= -1; side -= 2)
            {
                int centre = vertices.Count; vertices.Add(new Vector3(0, side * thick * 1.15f, 0)); uvs.Add(new Vector2(.45f, .5f));
                for (int i = 0; i < sides; i++) { vertices.Add(new Vector3(outline[i].x, side * thick * (.7f + .3f * (float)random.NextDouble()), outline[i].y)); uvs.Add(new Vector2(.45f + outline[i].x * 4, .5f + outline[i].y * 4)); }
                for (int i = 0; i < sides; i++)
                {
                    int a = centre + 1 + i, b = centre + 1 + (i + 1) % sides;
                    if (side > 0) { triangles.Add(centre); triangles.Add(b); triangles.Add(a); } else { triangles.Add(centre); triangles.Add(a); triangles.Add(b); }
                }
            }
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides, b = vertices.Count;
                vertices.Add(vertices[1 + i]); vertices.Add(vertices[1 + j]); vertices.Add(vertices[sides + 2 + j]); vertices.Add(vertices[sides + 2 + i]);
                for (int k = 0; k < 4; k++) uvs.Add(new Vector2(.975f, k < 2 ? .8f : .2f));
                triangles.Add(b); triangles.Add(b + 1); triangles.Add(b + 2); triangles.Add(b); triangles.Add(b + 2); triangles.Add(b + 3);
            }
            var mesh = new Mesh { name = "Biscuit crumb " + seed };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); var normals = mesh.normals; float facing = 0;
            for (int i = 0; i < normals.Length; i++) facing += Vector3.Dot(normals[i], vertices[i]);
            if (facing < 0) { var t = mesh.triangles; for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; } mesh.triangles = t; mesh.RecalculateNormals(); }
            mesh.RecalculateBounds(); return mesh;
        }

        private static Texture2D MakeBiscuitTexture()
        {
            const int size = 128; var pixels = new Color32[size * size]; var rnd = new System.Random(7);
            Color face = new Color(0.90f, 0.66f, 0.34f), toasted = new Color(0.74f, 0.47f, 0.20f), hole = new Color(0.46f, 0.27f, 0.11f);
            Color crust = new Color(0.66f, 0.40f, 0.16f), crumb = new Color(0.96f, 0.84f, 0.60f);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size; Color c;
                if (u > 0.95f) c = crumb; else if (u > 0.88f) c = crust;
                else
                {
                    // Face: golden centre browning toward the rim, fine speckle and a 4x4 grid of docking holes.
                    float fx = (u - 0.03f) / 0.82f, fy = (v - 0.03f) / 0.94f;
                    float rim = Mathf.Max(Mathf.Abs(fx - 0.5f), Mathf.Abs(fy - 0.5f)) * 2;
                    c = Color.Lerp(face, toasted, Mathf.SmoothStep(0.55f, 1.05f, rim));
                    float gx = fx * 4 - Mathf.Floor(fx * 4) - 0.5f, gy = fy * 4 - Mathf.Floor(fy * 4) - 0.5f;
                    float d = Mathf.Sqrt(gx * gx + gy * gy);
                    c = Color.Lerp(c, hole, 1 - Mathf.SmoothStep(0.07f, 0.12f, d));
                    c = Color.Lerp(c, toasted, 0.35f * (1 - Mathf.SmoothStep(0.12f, 0.2f, d)) * Mathf.SmoothStep(0.07f, 0.12f, d));
                }
                float speck = (float)rnd.NextDouble(); if (speck > 0.965f) c *= 0.82f; else if (speck < 0.03f) c = Color.Lerp(c, Color.white, 0.12f);
                c.a = 1; pixels[x + y * size] = c;
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Biscuit food", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels); texture.Apply(true, true); return texture;
        }
        private Transform MeshShape(string name, Mesh mesh, Transform parent, Vector3 at, Vector3 size, Material mat)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = at; go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat; return go.transform;
        }
        private Transform Fin(Transform parent, Vector3 at, Vector3 size, Material mat)
        { return MeshShape("Fan fin", fanMesh, parent, at, size, mat); }
        private void PredatorMouth(Actor actor, Vector3 at)
        {
            actor.predatorMouth = new GameObject("Feeding mouth"); actor.predatorMouth.transform.SetParent(actor.root.transform, false); actor.predatorMouth.transform.localPosition = at;
            var t = actor.predatorMouth.transform;
            Ball("Mouth lip", t, Vector3.zero, new Vector3(0.75f, 0.73f, 0.25f), mouthLining);
            Ball("Mouth cavity", t, new Vector3(0, 0, 0.08f), new Vector3(0.61f, 0.59f, 0.2f), black);
            Ball("Tongue", t, new Vector3(0, -0.18f, 0.18f), new Vector3(0.27f, 0.12f, 0.13f), mouthLining);
            for (int side = -1; side <= 1; side += 2)
                for (int j = 0; j < 4; j++)
                {
                    float x = (j - 1.5f) * 0.13f;
                    var tooth = MeshShape("Predator tooth", coneMesh, t, new Vector3(x, side * (0.24f - Mathf.Abs(x) * 0.3f), 0.19f), new Vector3(0.095f, 0.14f, 0.09f), white);
                    if (side > 0) tooth.localRotation = Quaternion.Euler(0, 0, 180);
                }
            actor.predatorMouth.SetActive(false);
        }
        private Actor MakeActor(string kind)
        {
            var actor = new Actor { root = new GameObject(kind), kind = kind }; actor.root.transform.SetParent(root);
            var t = actor.root.transform;
            if (kind.StartsWith("fish") || kind == "alien")
            {
                var visual = new GameObject("Rigged creature");visual.transform.SetParent(t,false);
                actor.creature=visual.AddComponent<AquariumCreature>();actor.creature.Initialize(kind=="alien"?-1:int.Parse(kind.Substring(4)),random.Next(10000));
            }
            else if (kind == "diver")
            {
                actor.diver = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("AquariumEquipment/Diver"), t).GetComponent<AquariumRig>();
                actor.gun = AquariumFirstPersonEquipment.CreateGun(t, actor.diver);
            }
            else if (kind == "harpoon")
            {
                UnityEngine.Object.Instantiate(Resources.Load<GameObject>("AquariumEquipment/HarpoonHead"), t);
                // The line is tied to the tail of the spear's long steel rod, not to the head.
                var tail = new GameObject("Spear tail").transform; tail.SetParent(t, false); tail.localPosition = new Vector3(0, 0, -.75f);
                actor.tether = new GameObject("Braided retrieval line").AddComponent<LineRenderer>(); actor.tether.transform.SetParent(tail, false);
                actor.tether.sharedMaterial = Resources.Load<Material>("AquariumEquipment/Rope");
                actor.tether.positionCount = 25; actor.tether.useWorldSpace = true; actor.tether.widthMultiplier = .014f;
                actor.tether.numCapVertices = 3; actor.tether.numCornerVertices = 2; actor.tether.textureMode = LineTextureMode.Tile;
                actor.tether.generateLightingData = true; actor.tether.shadowCastingMode = ShadowCastingMode.Off;
            }
            else if (kind == "egg")
            {
                Ball("Glossy egg", t, new Vector3(0, 0.32f - AquariumModel.EggRadius, 0), new Vector3(0.47f, 0.64f, 0.47f), eggShell);
                Ball("Egg glint", t, new Vector3(-0.1f, 0.47f - AquariumModel.EggRadius, 0.18f), new Vector3(0.07f, 0.11f, 0.035f), white);
            }
            if(kind=="alien" || kind=="diver" || kind.StartsWith("fish"))
            {
                actor.bodyCollider=actor.root.AddComponent<CapsuleCollider>();actor.bodyCollider.direction=2;
                var physicsBody=actor.root.AddComponent<Rigidbody>();physicsBody.isKinematic=true;physicsBody.useGravity=false;
            }
            return actor;
        }
        private Actor Place(string key, string kind, Point at, Point facing, float dt, float time, bool dead = false,
            float sampleTime = float.NaN, float renderTime = float.NaN)
        {
            visible.Add(key); Actor actor;
            bool creatureKind=kind=="alien" || kind.StartsWith("fish");
            bool fresh = !actors.TryGetValue(key, out actor) || (actor.kind != kind && (actor.creature == null || !creatureKind));
            if (fresh)
            {
                if (actor != null) DestroyActor(actor);
                actor = MakeActor(kind); actors[key] = actor;
            }
            Vector3 target = new Vector3(at.x, at.y, at.z);
            if((fresh || actor.kind!=kind) && key.StartsWith("p") && actor.creature!=null && kind.StartsWith("fish"))actor.creature.PrepareFeedingForms(int.Parse(kind.Substring(4)));
            actor.kind=kind;
            if(actor.bodyCollider!=null)
            {
                // R23: sized per fish species (a disguise takes its fish's hull).
                int sizedSpecies=kind.StartsWith("fish")?int.Parse(kind.Substring(4)):-1;
                float radius=AquariumBodyCollision.Radius(kind=="diver",kind=="alien",sizedSpecies);
                actor.bodyCollider.radius=radius;actor.bodyCollider.height=2*(radius+AquariumBodyCollision.Half(kind=="diver",kind=="alien",sizedSpecies));
            }
            if (actor.dead != dead) actor.positions.Clear();
            actor.dead = dead;
            if (float.IsNaN(sampleTime)) sampleTime = time;
            if (float.IsNaN(renderTime)) renderTime = time;
            Vector3 before = actor.root.transform.position;
            Vector3 displayed = actor.positions.Present(target, sampleTime, renderTime, fresh || dead, out bool snapped);
            if (fresh || !freezeExistingPoses) actor.root.transform.position = displayed;
            float measured = snapped || dt <= 0 ? 0 : Vector3.Distance(actor.root.transform.position, before) / dt;
            actor.moveSpeed = snapped ? 0 : Mathf.Lerp(actor.moveSpeed, measured, 1 - Mathf.Exp(-Mathf.Max(0, dt) * 12));
            var measuredVelocity = snapped || dt <= 0 ? Vector3.zero : (actor.root.transform.position - before) / dt;
            actor.velocity = snapped ? Vector3.zero : Vector3.Lerp(actor.velocity, measuredVelocity, 1 - Mathf.Exp(-Mathf.Max(0, dt) * 12));
            var direction = new Vector3(facing.x, facing.y, facing.z);
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            var rot = Quaternion.LookRotation(direction, Mathf.Abs(direction.normalized.y) > 0.99f ? Vector3.forward : Vector3.up);
            if (dead && actor.creature == null) rot *= Quaternion.Euler(0, 0, 180);
            float turn=Vector3.SignedAngle(actor.root.transform.forward,direction,Vector3.up);
            float wantedBank=actor.moveSpeed>.15f?Mathf.Clamp(-turn*.35f,-10,10):0;
            actor.bank=Mathf.Lerp(actor.bank,wantedBank,1-Mathf.Exp(-Mathf.Max(0,dt)*6));
            rot*=Quaternion.Euler(0,0,actor.bank);
            if (fresh || !freezeExistingPoses) actor.root.transform.rotation = fresh || dead ? rot : Quaternion.Slerp(actor.root.transform.rotation, rot, 1-Mathf.Exp(-dt*8));
            if (actor.tail != null) actor.tail.localRotation = Quaternion.Euler(0, dead ? 0 : Mathf.Sin(time * 10 + at.z) * 22, 90);
            if (actor.tentacles != null) for (int i = 0; i < actor.tentacles.Length; i++)
                actor.tentacles[i].localRotation = Quaternion.Euler(Mathf.Sin(time * 3 + i * 1.8f) * 15, 0, Mathf.Cos(time * 3 + i) * 12);
            return actor;
        }
        private void PinHarpoonCatch(string key, Vector3 tip, Point offset)
        {
            if (!actors.TryGetValue(key, out var corpse)) return;
            corpse.root.transform.position = tip + new Vector3(offset.x, offset.y, offset.z);
            if (corpse.bodyCollider != null) corpse.bodyCollider.enabled = false;
        }

        private LineRenderer Ring(string name, Transform parent, float radius, Color color, float width)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = lineMaterial;
            line.useWorldSpace = false; line.loop = true; line.positionCount = 48; line.widthMultiplier = width;
            line.startColor = line.endColor = color; line.shadowCastingMode = ShadowCastingMode.Off;
            for (int i = 0; i < 48; i++) { float a = i * Mathf.PI * 2 / 48; line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius)); }
            return line;
        }

        public void Draw(AquariumSnapshot state, int localId, float dt, float renderTime = float.NaN)
        {
            if (state == null) return;
            bool finished = state.phase == RoundPhase.Finished;
            // Share the gallery shader clock so water and reef motion freeze with the final scene.
            if (!finished) environmentTime = Time.time;
            Shader.SetGlobalFloat("_AquariumPreviewTime", environmentTime);
            Shader.SetGlobalFloat("_AquariumPreviewTimeEnabled", 1);
            if (finished && worldFrozen && frozenRound == state.round) { CleanupFrozenWorld(state); return; }
            worldFrozen = finished; frozenRound = state.round; freezeExistingPoses = finished;
            bool resetMotion = lastStateTime < 0 || state.time < lastStateTime || state.round != presentationRound || state.phase != presentationPhase;
            if (resetMotion)
                foreach (var actor in actors.Values) { actor.positions.Clear(); actor.moveSpeed = 0; }
            if(presentationTime<0 || resetMotion)presentationTime=state.time;
            else presentationTime=Mathf.Min(state.time+.05f,Mathf.Max(state.time-.1f,presentationTime+Mathf.Clamp(dt,0,.1f)));
            if(presentationRound!=state.round)fishKeys.Clear();
            lastStateTime=state.time;presentationRound=state.round;presentationPhase=state.phase;
            float visualTime = finished ? state.finishedAt : presentationTime;
            float renderAt = float.IsNaN(renderTime) ? presentationTime - .05f : renderTime;
            if (finished) SetMorphTarget(null);
            visible.Clear();
            Swimmer local = null;
            foreach (var p in state.players)
            {
                if (p.id == localId) local = p;
                if (p.swallowedBy != 0 || (!p.alive && (finished || p.team != Team.Hider))) continue;
                float age=visualTime-p.visualAt;
                bool deathPose=!p.alive;
                bool morphing=visualTime-p.morphAt<1.4f;
                int form=deathPose?p.deathSpecies:p.species<0 || p.revealUntil>visualTime && !morphing ? -1:p.species;
                string kind=p.team==Team.Seeker?"diver":form<0?"alien":FishKind(form);
                bool frozenPose=finished && actors.ContainsKey(PlayerKey(p.id));
                if (actors.TryGetValue(PlayerKey(p.id), out var released) && released.carried)
                { released.positions.Clear(); released.carried = false; }
                var actor=Place(PlayerKey(p.id),kind,p.position,p.facing,dt,visualTime,deathPose,state.time,renderAt);
                if (actor.diver != null && !frozenPose)
                {
                    float remaining=p.nextAction-visualTime;
                    // Round opening: stand on the dive deck, then the giant-stride entry (the host moves the root).
                    float entry=p.entryAt>0?visualTime-(p.entryAt-AquariumDiveEntry.Seconds):float.NaN;
                    if(!float.IsNaN(entry) && visualTime<p.entryAt && actor.diver.HasClip("DiveIn") && actor.diver.HasClip("DeckIdle"))
                    {
                        if(entry<0)actor.diver.SamplePose("DeckIdle",visualTime,visualTime);
                        else actor.diver.SamplePose("DiveIn",entry,visualTime);
                    }
                    else if(actor.diver.cartoon && remaining>2.5f)actor.diver.SamplePose("Fire",3-remaining,visualTime);
                    else if(actor.diver.cartoon && remaining>0)actor.diver.SamplePose("Reload",3-remaining,visualTime);
                    else
                    {
                        // Swim direction in the diver's own heading frame picks the forward / back / sideways stroke.
                        var heading=new Vector3(p.facing.x,0,p.facing.z);
                        var swimLocal=heading.sqrMagnitude>.0001f?Quaternion.Inverse(Quaternion.LookRotation(heading.normalized))*actor.velocity:actor.velocity;
                        actor.diver.SampleLocomotion(actor.moveSpeed,visualTime,p.id*.37f,swimLocal);
                    }
                    actor.gun.SetLoaded(visualTime >= p.nextAction);
                }
                if(actor.creature!=null && !frozenPose)
                {
                    float actionLength=p.visualAction=="SpitRecoil"?AquariumModel.SpitAnimationSeconds:p.visualAction=="SpitOut"?AquariumModel.ReleaseKickSeconds:p.visualAction=="Bite"?AquariumModel.FishFeedingSeconds:p.visualAction=="TentacleEat"?AquariumModel.TentacleFeedingSeconds:p.visualAction=="AllyGulp"?AquariumModel.AllyStun+.2f:2.4f;
                    string motion=age<actionLength && !string.IsNullOrEmpty(p.visualAction)?p.visualAction:"Locomotion";
                    if((motion=="Bite" || motion=="AllyGulp") && form<0)motion="TentacleEat";
                    if(deathPose)motion=p.deathReason=="harpoon"?"Harpoon":"Eaten";
                    if(motion=="Bite" && form>=0 && age<AquariumFeeding.ReturnToFish)form+=100;
                    float poseTime=deathPose?Mathf.Min(visualTime,p.deathAt+2.4f):visualTime;
                    actor.creature.Present(form,motion,p.visualAt,poseTime,deathPose?0:actor.moveSpeed,deathPose);
                    actor.root.transform.localScale=Vector3.one;
                    actor.bodyCollider.enabled=!deathPose || visualTime-p.deathAt<2.4f;
                }
            }
            // Resolve swallowed players after every predator mouth has its current
            // pose, independent of player roster order and network interpolation.
            foreach(var released in state.players)
            {
                float age=visualTime-released.visualAt;
                if(released.visualAction!="SpitOut" || age<0 || age>=AquariumFeeding.ExpulsionEnd || released.releaseSource==0)continue;
                if(actors.TryGetValue(PlayerKey(released.id),out var prey) && actors.TryGetValue(PlayerKey(released.releaseSource),out var carrier) && prey.creature!=null && carrier.creature!=null)
                    prey.creature.PresentExpulsion(carrier.creature,age);
            }
            foreach(var p in state.players)
            {
                float age=visualTime-p.visualAt;
                if(p.swallowedBy==0 || age>=MealVisible(state,p.swallowedBy) || state.phase==RoundPhase.Finished)continue;
                int capturedForm=p.swallowedSpecies;
                var actor=Place(PlayerKey(p.id),capturedForm<0?"alien":FishKind(capturedForm),p.swallowedFrom,p.facing,dt,visualTime,false,state.time,renderAt);
                actor.carried = true;
                actor.root.transform.localScale=Vector3.one;
                actor.creature.Present(capturedForm,"Eaten",p.visualAt,visualTime,0);
                if(actors.TryGetValue(PlayerKey(p.swallowedBy),out var predator))IngestActor(actor,predator,age);
                actor.bodyCollider.enabled=false;
            }
            foreach (var f in state.fish)
            {
                float age=visualTime-f.visualAt;
                if(finished && (f.eaten || f.dead) || f.eaten && age>=MealVisible(state,f.eater))continue;
                bool frozenPose=finished && actors.ContainsKey(FishKey(f.id));
                var actor=Place(FishKey(f.id),FishKind(f.species),f.position,f.facing,dt,visualTime,f.dead && age>2.4f,state.time,renderAt);
                if(frozenPose)continue;
                actor.root.transform.localScale=Vector3.one;
                actor.creature.Present(f.species,f.eaten?"Eaten":f.dead?"Harpoon":"Locomotion",f.visualAt,visualTime,actor.moveSpeed,f.dead && !f.eaten);
                if(f.eaten && actors.TryGetValue(PlayerKey(f.eater),out var predator))IngestActor(actor,predator,age);
                actor.bodyCollider.enabled=!f.eaten;
            }
            foreach (var h in state.harpoons)
            {
                // Interpolated tip and a smoothed heading: snapshots arrive in steps, the spear must not.
                var shotActor = Place("h" + h.id, "harpoon", h.position, h.direction, dt, visualTime,false,state.time,renderAt);
                var heading = new Vector3(h.direction.x, h.direction.y, h.direction.z);
                if (heading.sqrMagnitude < .0001f) heading = Vector3.forward;
                shotActor.shotTip = shotActor.root.transform.position;
                shotActor.shotDirection = !shotActor.shotPresented ? heading.normalized : Vector3.Slerp(shotActor.shotDirection, heading.normalized, 1 - Mathf.Exp(-Mathf.Max(0, dt) * 14));
                shotActor.shotPresented = true;
            }
            if (state.eggs != null) foreach (var egg in state.eggs)
            {
                var actor = Place("e" + egg.id, "egg", egg.position, new Point(0, 1), dt, visualTime,false,state.time,renderAt);
                float urgency = Mathf.Clamp01(1 - (egg.hatchAt - visualTime) / AquariumModel.HatchSeconds);
                float pulse = Mathf.Sin(visualTime * (3 + urgency * 5) + egg.id) * (0.025f + urgency * 0.025f);
                // Pulse sideways while keeping the shell's foot planted on the collision surface.
                actor.root.transform.localScale = new Vector3(1 - pulse, 1, 1 - pulse);
            }
            removedActors.Clear();
            foreach (var pair in actors) if (!visible.Contains(pair.Key)) { DestroyActor(pair.Value); removedActors.Add(pair.Key); }
            foreach (var key in removedActors) actors.Remove(key);
            localRing.gameObject.SetActive(state.phase == RoundPhase.Playing && local != null && local.team == Team.Hider && local.alive && local.swallowedBy == 0);
            if (local != null)
            { var displayed = PlayerPosition(local); localRing.transform.position = new Vector3(displayed.x, 0.25f, displayed.z); }
            activeEffects.Clear();
            foreach (var e in state.effects)
            {
                activeEffects.Add(e.id); GameObject fx;
                if(finished && effects.ContainsKey(e.id))continue;
                // No expanding rings for any event (harpoon shot, miss, meal, morph, scan, egg...).
                // Events keep only their ink, splash or biscuits; their sounds are played elsewhere.
                if (e.kind != "feed" && e.kind != "splash" && !AquariumInkCloud.IsInk(e.kind)) continue;
                if (!effects.TryGetValue(e.id, out fx))
                {
                    if (e.kind == "splash") fx = MakeSplash();
                    else if (AquariumInkCloud.IsInk(e.kind))
                    {
                        fx=new GameObject(e.kind+" volumetric ink");fx.transform.SetParent(root,false);
                        fx.AddComponent<AquariumInkCloud>().Initialize(e.id,AquariumInkCloud.IsYellow(e.kind),false,e.kind=="burp",AquariumInkCloud.IsCapture(e.kind));
                    }
                    else
                    {
                        // Feed: just the biscuits, no ring around them.
                        fx = new GameObject(e.kind); fx.transform.SetParent(root, false);
                        for (int i = 0; i < 10; i++)
                        {
                            // Broken biscuit pieces, each a little different in size and tilt.
                            var piece = MeshShape("Biscuit piece", biscuitMeshes[i % biscuitMeshes.Length], fx.transform,
                                new Vector3(Mathf.Sin(i * 2.4f) * 0.5f, Mathf.Cos(i * 1.7f) * 0.25f, Mathf.Cos(i * 2.4f) * 0.5f), Vector3.one * (1.5f + 0.45f * Mathf.Repeat(i * 0.618f, 1)), biscuit);
                            piece.localRotation = Quaternion.Euler(Mathf.Sin(i * 1.3f) * 35, i * 67, Mathf.Cos(i * 2.1f) * 35);
                            // The crumbs this piece sheds as it breaks apart (hidden until each chip comes off).
                            for (int k = 0; k < CrumbsPerPiece; k++)
                            {
                                var crumb = MeshShape("Biscuit crumb", crumbMeshes[(i + k) % crumbMeshes.Length], fx.transform, Vector3.zero, Vector3.zero, biscuit);
                                crumb.gameObject.SetActive(false);
                            }
                        }
                    }
                    effects[e.id] = fx;
                }
                float age = Mathf.Clamp01((visualTime - e.born) / Mathf.Max(0.01f, e.duration));
                if(e.kind=="splash")PresentSplash(fx.transform,visualTime-e.born,e);
                else if(AquariumInkCloud.IsInk(e.kind))
                {
                    float seconds=visualTime-e.born-(e.kind=="burp"?0:.12f);
                    Vector3 source=new Vector3(e.position.x,e.position.y,e.position.z),direction=new Vector3(e.direction.x,e.direction.y,e.direction.z);
                    if(e.owner!=0 && actors.TryGetValue(PlayerKey(e.owner),out var feeder) && feeder.creature!=null)
                    {direction=feeder.creature.FeedingForward;source=feeder.creature.FeedingMouth+direction*.015f;}
                    fx.GetComponent<AquariumInkCloud>().Present(seconds,e.duration-(e.kind=="burp"?0:.12f),source,direction);
                }
                else
                {
                    fx.transform.position=new Vector3(e.position.x,e.position.y+age*.3f,e.position.z);
                    fx.transform.localScale=Vector3.one;
                    // The pieces sink-drift and tumble slowly in the water, then crumble away.
                    PresentBiscuits(fx.transform,visualTime-e.born,e.duration);
                }
            }
            expiredEffects.Clear();
            foreach (var e in effects) if (!activeEffects.Contains(e.Key)) { ReleaseObject(e.Value); expiredEffects.Add(e.Key); }
            foreach (int id in expiredEffects) effects.Remove(id);

        }

        // The meal is drawn until it is fully inside the eater's sealed mouth.
        private static float MealVisible(AquariumSnapshot state,int eaterId)
        {
            foreach(var eater in state.players)if(eater.id==eaterId)return AquariumFeeding.MealVisibleSeconds(eater.visualAction);
            return AquariumModel.SwallowVisualSeconds;
        }
        private void IngestActor(Actor prey,Actor predator,float age)
        {
            if(predator.creature==null)return;
            prey.creature.PresentIngestion(predator.creature,age);
        }

        public void PresentEquipment(AquariumSnapshot state, int followId, bool firstPerson)
        {
            Actor followed; actors.TryGetValue(PlayerKey(followId), out followed);
            equipment.Present(state, followId, firstPerson, followed == null ? 0 : followed.moveSpeed);
            if (state == null || state.phase == RoundPhase.Finished) return;
            foreach (var shot in state.harpoons)
            {
                Actor projectile, owner;
                if (!actors.TryGetValue("h" + shot.id, out projectile) || !actors.TryGetValue(PlayerKey(shot.owner), out owner) || owner.gun == null) continue;
                var gun = equipment.Visible && equipment.Owner == shot.owner ? equipment.gun : owner.gun;
                var tip = projectile.shotPresented ? projectile.shotTip : new Vector3(shot.position.x, shot.position.y, shot.position.z);
                var direction = projectile.shotPresented ? projectile.shotDirection : new Vector3(shot.direction.x, shot.direction.y, shot.direction.z).normalized;
                var dockedTip = gun.muzzle.position + gun.muzzle.forward * gun.headLength * gun.transform.lossyScale.z;
                if (!shot.returning) tip = Vector3.Lerp(dockedTip, tip, Mathf.Clamp01((state.time - shot.born) / .07f));
                else
                {
                    // The last stretch is reeled straight down the barrel: the spear swings onto the muzzle axis
                    // (tip out), then slides in tail first and seats exactly when the cooldown ends.
                    float u = Mathf.InverseLerp(shot.retrievedAt - HarpoonDockSeconds, shot.retrievedAt, state.time);
                    if (u > 0)
                    {
                        // One smooth curve from where the spear was when docking began (carried along with the swimming
                        // diver, but not swung by the reloading gun) to the seated tip, arriving tail first along the barrel.
                        var axis = gun.muzzle.forward; var anchor = owner.root.transform.position;
                        if (!projectile.docking) { projectile.docking = true; projectile.dockStart = tip; projectile.dockAnchor = anchor; }
                        var start = projectile.dockStart + (anchor - projectile.dockAnchor);
                        float span = Mathf.Max(.25f, Vector3.Distance(start, dockedTip));
                        float e = 1 - (1 - u) * (1 - u), e2 = e * e, e3 = e2 * e;
                        var leave = (dockedTip - start) * .8f; var arrive = -axis * span * 1.4f;
                        tip = (2 * e3 - 3 * e2 + 1) * start + (e3 - 2 * e2 + e) * leave + (-2 * e3 + 3 * e2) * dockedTip + (e3 - e2) * arrive;
                        direction = Vector3.Slerp(direction, axis, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .45f, u)));
                    }
                }
                float weaponScale=gun.transform.lossyScale.z;
                projectile.root.transform.localScale=Vector3.one*weaponScale;
                projectile.root.transform.SetPositionAndRotation(tip - direction * gun.headLength*weaponScale, Quaternion.LookRotation(direction));
                // Equipment docking adjusts the rendered tip after the world pose is drawn.
                if (shot.caughtFish != 0) PinHarpoonCatch(FishKey(shot.caughtFish), tip, shot.catchOffset);
                if (shot.caughtPlayer != 0) PinHarpoonCatch(PlayerKey(shot.caughtPlayer), tip, shot.catchOffset);
                projectile.tether.transform.parent.localPosition = new Vector3(0, 0, -gun.shaftLength);
                var from = gun.ropeExit.position; var to = projectile.tether.transform.parent.position;
                float distance = Vector3.Distance(from, to);
                float sag = Mathf.Min(.45f, distance * .035f) * (shot.returning ? 1 : .35f);
                var side = Vector3.Cross((to - from).normalized, Vector3.up);
                for (int i = 0; i < projectile.tether.positionCount; i++)
                {
                    float t = i / (float)(projectile.tether.positionCount - 1); float arc = Mathf.Sin(t * Mathf.PI);
                    var ripple = side * (Mathf.Sin(t * 9 - state.time * 2.5f) * Mathf.Min(.045f, distance * .009f));
                    projectile.tether.SetPosition(i, Vector3.Lerp(from, to, t) + (Vector3.down * sag + ripple) * arc);
                }
            }
        }

        // Render both passes for editor reviews and hidden-window smoke captures.
        public void RenderTo(RenderTexture target)
        {
            var previous = Camera.targetTexture;
            try
            {
                Camera.targetTexture = target; Camera.Render();
                foreach (var overlay in Camera.GetComponentsInChildren<Camera>()) if (overlay != Camera && overlay.enabled)
                {
                    var old = overlay.targetTexture;
                    try { overlay.targetTexture = target; overlay.Render(); }
                    finally { overlay.targetTexture = old; }
                }
            }
            finally { Camera.targetTexture = previous; }
        }

        public MorphTarget FindMorphTarget(AquariumSnapshot state, Swimmer caster, Point aim)
        {
            if (state == null || caster == null || !caster.alive || caster.team != Team.Hider || caster.swallowedBy != 0 || worldFrozen) return null;
            MorphTarget best = null; float screenDistance = float.MaxValue, worldDistance = float.MaxValue;
            foreach (var fish in state.fish) if (!fish.dead && !fish.eaten)
                ConsiderVisibleMorph(caster, aim, fish.id, fish.species, false, fish.position, ref best, ref screenDistance, ref worldDistance);
            foreach (var player in state.players)
                if (AquariumModel.CopyableAlly(caster, player, state.time))
                    ConsiderVisibleMorph(caster, aim, player.id, player.species, true, player.position, ref best, ref screenDistance, ref worldDistance);
            return best;
        }
        private void ConsiderVisibleMorph(Swimmer caster, Point aim, int id, int species, bool player, Point position,
            ref MorphTarget best, ref float bestScreenDistance, ref float bestWorldDistance)
        {
            if (!AquariumModel.CanMorphTarget(caster, aim, position)) return;
            Actor actor;
            if (!actors.TryGetValue((player?PlayerKey(id):FishKey(id)), out actor) || !actor.kind.StartsWith("fish")) return;
            var rendered = actor.root.transform.position;
            var screen = Camera.WorldToViewportPoint(rendered);
            if (screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) return;
            var cameraPosition = Camera.transform.position;
            if (AquariumModel.ObstacleHit(new Point(cameraPosition.x, cameraPosition.y, cameraPosition.z), new Point(rendered.x, rendered.y, rendered.z)) <= 1) return;
            float x = (screen.x - 0.5f) * Camera.aspect, y = screen.y - 0.5f;
            float screenDistance = x * x + y * y, worldDistance = (position - caster.position).Length;
            if (best != null && (screenDistance > bestScreenDistance + 0.000001f || (Mathf.Abs(screenDistance - bestScreenDistance) <= 0.000001f && worldDistance >= bestWorldDistance))) return;
            best = new MorphTarget { id = id, species = species, player = player, position = position };
            bestScreenDistance = screenDistance; bestWorldDistance = worldDistance;
        }
        // The same silhouette treatment is used for ordinary fish and disguised players.
        public void SetMorphTarget(MorphTarget target)
        {
            Actor actor = null;
            if (!worldFrozen && target != null) actors.TryGetValue((target.player?PlayerKey(target.id):FishKey(target.id)), out actor);
            if (actor != null && !actor.kind.StartsWith("fish")) actor = null;
            if (actor == outlinedActor) { EnsureOutlineParts(); UpdateOutlineVisibility(); return; }
            ClearOutline();
            outlinedActor = actor;
            EnsureOutlineParts();
            UpdateOutlineVisibility();
        }
        private bool HasOutline(Renderer source)
        { foreach(var part in outlineParts)if(part.source==source)return true;return false; }
        private void EnsureOutlineParts()
        {
            if(outlinedActor==null)return;
            // Forms and their bridge are created lazily; discover each skin once.
            outlinedActor.root.GetComponentsInChildren(true,outlineSkins);
            foreach(var skin in outlineSkins)
            {
                if(skin.sharedMesh==null || skin.name=="Candidate silhouette" || HasOutline(skin))continue;
                var shell=new GameObject("Candidate silhouette");shell.transform.SetParent(skin.transform,false);
                var renderer=shell.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=skin.sharedMesh;
                renderer.bones=skin.bones;renderer.rootBone=skin.rootBone;renderer.localBounds=skin.localBounds;
                renderer.sharedMaterial=outlineMaterial;renderer.updateWhenOffscreen=true;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                outlineParts.Add(new OutlinePart{source=skin,shell=renderer});
            }
            outlinedActor.root.GetComponentsInChildren(true,outlineFilters);
            foreach (var filter in outlineFilters)
            {
                if (filter.sharedMesh == null || filter.gameObject.name == "Candidate silhouette") continue;
                var sourceRenderer = filter.GetComponent<Renderer>();
                if (sourceRenderer == null || HasOutline(sourceRenderer)) continue;
                var shell = new GameObject("Candidate silhouette"); shell.transform.SetParent(filter.transform, false);
                shell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = shell.AddComponent<MeshRenderer>(); renderer.sharedMaterial = outlineMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                outlineParts.Add(new OutlinePart { source = sourceRenderer, shell = renderer });
            }
        }
        private void UpdateOutlineVisibility()
        {
            foreach (var part in outlineParts) if (part.shell != null)
            {
                part.shell.enabled = part.source != null && part.source.enabled && part.source.gameObject.activeInHierarchy && !hiddenByCamera.Contains(outlinedActor);
                var source=part.source as SkinnedMeshRenderer;var shell=part.shell as SkinnedMeshRenderer;
                if(source!=null && shell!=null && part.shell.enabled)
                {
                    shell.sharedMesh=source.sharedMesh;shell.localBounds=source.localBounds;
                    for(int i=0;i<source.sharedMesh.blendShapeCount;i++)shell.SetBlendShapeWeight(i,source.GetBlendShapeWeight(i));
                }
            }
        }
        private void ClearOutline()
        {
            foreach (var part in outlineParts) if (part.shell != null)
            { part.shell.gameObject.SetActive(false); ReleaseObject(part.shell.gameObject); }
            outlineParts.Clear(); outlinedActor = null;
        }
        private void CleanupFrozenWorld(AquariumSnapshot state)
        {
            // Keep the captured poses and effects untouched while removing objects absent from the snapshot.
            visible.Clear();
            foreach (var fish in state.fish) if (!fish.eaten && !fish.dead) visible.Add(FishKey(fish.id));
            foreach (var player in state.players) if (player.alive && player.swallowedBy == 0) visible.Add(PlayerKey(player.id));
            foreach (var harpoon in state.harpoons) visible.Add("h" + harpoon.id);
            if (state.eggs != null) foreach (var egg in state.eggs) visible.Add("e" + egg.id);
            removedActors.Clear();
            foreach (var pair in actors) if (!visible.Contains(pair.Key)) { DestroyActor(pair.Value); removedActors.Add(pair.Key); }
            foreach (var key in removedActors) actors.Remove(key);
            activeEffects.Clear();
            foreach (var effect in state.effects) activeEffects.Add(effect.id);
            expiredEffects.Clear();
            foreach (var effect in effects) if (!activeEffects.Contains(effect.Key)) { ReleaseObject(effect.Value); expiredEffects.Add(effect.Key); }
            foreach (int id in expiredEffects) effects.Remove(id);
            localRing.gameObject.SetActive(false);
        }
        public Vector3 PlayerPosition(Swimmer player)
        {
            Actor actor;
            return actors.TryGetValue(PlayerKey(player.id), out actor) ? actor.root.transform.position : new Vector3(player.position.x, player.position.y, player.position.z);
        }
        public void CameraOcclusion(int targetId, Vector3? target, bool hideTarget = false)
        {
            foreach (var pair in hiddenRendererStates) if (pair.Key != null) pair.Key.enabled = pair.Value;
            hiddenRendererStates.Clear(); hiddenByCamera.Clear();
            foreach (var panel in glassPanels)
            {
                panel.enabled = true;
                if (!target.HasValue) continue;
                var delta = target.Value - Camera.transform.position; float hit;
                if (panel.bounds.IntersectRay(new Ray(Camera.transform.position, delta.normalized), out hit) && hit < delta.magnitude)
                    panel.enabled = false;
            }
            if (target.HasValue)
            {
                Actor actor;
                if ((hideTarget || Vector3.Distance(Camera.transform.position, target.Value) < 0.9f) && actors.TryGetValue(PlayerKey(targetId), out actor)) HideNearCamera(actor);
                // Fish that brush past the lens must not cover a death review or the free-view result scene.
                foreach (var pair in actors)
                    if ((pair.Key.StartsWith("p") || pair.Key.StartsWith("f")) && (Camera.transform.position - pair.Value.root.transform.position).sqrMagnitude < 1.44f)
                        HideNearCamera(pair.Value);
            }
            UpdateOutlineVisibility();
        }
        private void HideNearCamera(Actor actor)
        {
            if (!hiddenByCamera.Add(actor)) return;
            // Only active renderers are touched: inactive feeding-mouth children keep their original state.
            actor.root.GetComponentsInChildren(false,cameraRenderers);
            foreach (var renderer in cameraRenderers)
            { hiddenRendererStates[renderer] = renderer.enabled; renderer.enabled = false; }
        }
        private void DestroyActor(Actor actor)
        { if (actor == outlinedActor) ClearOutline(); if(Application.isPlaying)UnityEngine.Object.Destroy(actor.root);else UnityEngine.Object.DestroyImmediate(actor.root); }
        private static void ReleaseObject(UnityEngine.Object value)
        { if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value); }
        public void Dispose()
        {
            equipment?.Dispose();
            Shader.SetGlobalFloat("_AquariumPreviewTimeEnabled", 0);
            ClearOutline();
            foreach (var actor in actors.Values) DestroyActor(actor);
            foreach (var mat in materials) ReleaseObject(mat);
            ReleaseObject(fanMesh); ReleaseObject(coneMesh); ReleaseObject(biscuitTexture);
            foreach (var mesh in biscuitMeshes) ReleaseObject(mesh);
            foreach (var mesh in crumbMeshes) ReleaseObject(mesh);
            ReleaseObject(root.gameObject);
        }
    }
}

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
            // R30: the rope catch sack on the diver's back the harpooned catch is stowed in.
            public Transform pouch;
            public AquariumFingers fingers;
            public LineRenderer tether;
            public CapsuleCollider bodyCollider;
            public float moveSpeed,bank;
            public Vector3 velocity,shotTip,shotDirection,dockStart,dockAnchor;
            public bool shotPresented,docking;
            public readonly PositionHistory positions = new PositionHistory();
            public bool dead, carried;
            // 2026-10-28: the player's chosen alien model (AquariumAlienKinds); a new pick rebuilds the actor.
            public int alienLook;
            // R27 drain motion shown on this actor (eased between snapshots).
            public DrainTask drainShown;
            public float drainWeight, drainDepth, drainU;
            public Vector3 drainA, drainB, drainMouth;
            // The diver rig's own placement under the actor, restored once a lid push ends.
            public bool rigPoseStored, rigMoved;
            public Vector3 rigRestPosition;
            public Quaternion rigRestRotation;
            // R47: the diver drone's glowing red pupils.
            public Transform[] droneEyes;
            // R77: the jelly wobble of a body skewered on a harpoon (AquariumSkewerJelly).
            public AquariumSkewerJelly jelly;
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
        private readonly AquariumDrainPresentation drains;
        // R37: the game's scenery layout (turned cliffs and reefs, scattered seaweed and anemones) on the gallery.
        private AquariumSceneryPresentation scenery;
        private readonly Dictionary<string, Actor> actors = new Dictionary<string, Actor>();
        private readonly Dictionary<int,string> playerKeys=new Dictionary<int,string>();
        private readonly Dictionary<int,string> fishKeys=new Dictionary<int,string>();
        // R47: ability effects, the diver drones and who the view is drawn for (the followed player).
        private readonly AquariumAbilityFx abilities;
        private readonly Dictionary<int,string> droneKeys=new Dictionary<int,string>();
        private int followId=int.MinValue;
        private static readonly string[] fishKinds={"fish0","fish1","fish2"};
        private string PlayerKey(int id)=>ActorKey(playerKeys,"p",id);
        private string FishKey(int id)=>ActorKey(fishKeys,"f",id);
        // R49: cached spear keys (were "h" + id strings built twice a frame per spear).
        private readonly Dictionary<int,string> harpoonKeys=new Dictionary<int,string>();
        private string HarpoonKey(int id)=>ActorKey(harpoonKeys,"h",id);
        private static string ActorKey(Dictionary<int,string> cache,string prefix,int id)
        {if(!cache.TryGetValue(id,out var key)){key=prefix+id;cache.Add(id,key);}return key;}
        private static string FishKind(int species)=>species>=0 && species<fishKinds.Length?fishKinds[species]:MoreFishKind(species);
        // R66: "fish" + species for the painted species past fish2, made once each instead of every frame per fish.
        private static readonly Dictionary<int,string> moreFishKinds=new Dictionary<int,string>();
        private static string MoreFishKind(int species){if(!moreFishKinds.TryGetValue(species,out var kind)){kind="fish"+species;moreFishKinds[species]=kind;}return kind;}
        // int.Parse(kind.Substring(4)) for a "fishN" kind, parsed once per kind.
        private static readonly Dictionary<string,int> kindSpecies=new Dictionary<string,int>();
        private static int KindSpecies(string kind){if(!kindSpecies.TryGetValue(kind,out var species)){species=int.Parse(kind.Substring(4));kindSpecies[kind]=species;}return species;}
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

        public AquariumView(Transform parent, Camera cameraOverride = null)
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
            outlineMaterial.SetColor("_OutlineColor", new Color(0.45f, 1f, 0.35f)); // R31: green, not yellow outlineMaterial.SetFloat("_OutlineWidth", 0.035f); materials.Add(outlineMaterial);
            fishMaterials = new Material[3]; finMaterials = new Material[3];
            for (int i = 0; i < 3; i++) { fishMaterials[i] = Material(colors[i]); finMaterials[i] = Material(Color.Lerp(colors[i], Color.white, 0.38f)); }
            fanMesh = MakeFanMesh(); coneMesh = MakeConeMesh();
            biscuitTexture = MakeBiscuitTexture(); biscuit = Material(Color.white); biscuit.mainTexture = biscuitTexture; biscuit.SetFloat("_Glossiness", 0.12f);
            biscuitMeshes = new Mesh[4]; for (int i = 0; i < biscuitMeshes.Length; i++) biscuitMeshes[i] = MakeBiscuitMesh(i);
            crumbMeshes = new Mesh[3]; for (int i = 0; i < crumbMeshes.Length; i++) crumbMeshes[i] = MakeCrumbMesh(i);
            splashWater = Material(new Color(.92f, .98f, 1f, .95f), true); splashFoam = Material(new Color(1f, 1f, 1f, .8f), true);
            splashBubble = Material(new Color(.82f, .95f, 1f, .5f), true);
            AquariumCreature.Prewarm();
            // R48: the diver glove's finger data and the catch pouch meshes are built now, not when a diver first spawns.
            {
                for (int diverKind = 0; diverKind < AquariumDiverKinds.Count; diverKind++)
                {
                    var diverPrefab = AquariumDiverKinds.DiverPrefab(diverKind);
                    if (diverPrefab == null || (diverKind > 0 && diverPrefab == AquariumDiverKinds.DiverPrefab(0))) continue;
                    var warm = UnityEngine.Object.Instantiate(diverPrefab, root); warm.SetActive(false);
                    var warmRig = warm.GetComponent<AquariumRig>();
                    if (warmRig != null) { AquariumFingers.Attach(warmRig, "HandR", false); AquariumCatchPose.CreatePouch(warmRig); }
                    if (Application.isPlaying) UnityEngine.Object.Destroy(warm); else UnityEngine.Object.DestroyImmediate(warm);
                }
            }
            AquariumMaps.Prewarm();
            BuildEnvironment();
            drains = new AquariumDrainPresentation(root);
            abilities = new AquariumAbilityFx(root);
            localRing = Ring("Your swimmer", root, 0.68f, new Color(0.59f, 1f, 0.81f, 0.85f), 0.035f);
            Camera = cameraOverride != null ? cameraOverride : UnityEngine.Camera.main;
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

        // R57: one baked environment per map (AquariumMaps); only the selected map's is active.
        private readonly AquariumEnvironmentRoot[] environments = new AquariumEnvironmentRoot[AquariumMaps.Count];
        private readonly AquariumSceneryPresentation[] sceneryByMap = new AquariumSceneryPresentation[AquariumMaps.Count];
        private readonly List<Renderer> environmentGlass = new List<Renderer>();
        private int shownMap = -1;
        private void BuildEnvironment() { ShowMap(AquariumMaps.Current); }
        private void ShowMap(int map)
        {
            map = AquariumMaps.Clamp(map);
            if (map == shownMap) return;
            if (environments[map] == null)
            {
                var environment = AquariumEnvironment.EnsureMap(map, root);
                environments[map] = environment;
                sceneryByMap[map] = new AquariumSceneryPresentation(environment);
            }
            for (int i = 0; i < environments.Length; i++)
                if (environments[i] != null && environments[i].gameObject.activeSelf != (i == map)) environments[i].gameObject.SetActive(i == map);
            scenery = sceneryByMap[map];
            foreach (var renderer in environmentGlass) glassPanels.Remove(renderer);
            environmentGlass.Clear();
            foreach (var renderer in environments[map].GetComponentsInChildren<Renderer>())
                if (renderer.name == "04 • Curved aquarium glass") environmentGlass.Add(renderer);
            glassPanels.AddRange(environmentGlass);
            shownMap = map;
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
        // R49: the diver's entry is AquariumDiveSplash (crown sheets and drops, lacy foam, soft uneven ripples,
        // a bubble burst and a bubble trail streaming off the diver), one static mesh animated in its shader.
        private GameObject MakeSplash()
        {
            var fx = new GameObject("Dive splash"); fx.transform.SetParent(root, false);
            fx.AddComponent<AquariumDiveSplash>().Initialize();
            return fx;
        }
        private static void Quiet(GameObject go)
        { var r = go.GetComponent<Renderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        private void PresentSplash(Transform fx, float seconds, WaterEffect e)
        {
            var forward = new Vector3(e.direction.x, 0, e.direction.z); forward = forward.sqrMagnitude > .0001f ? forward.normalized : Vector3.forward;
            Vector3? diver = null;
            if (actors.TryGetValue(PlayerKey(e.owner), out var jumper) && jumper.root != null) diver = jumper.root.transform.position + Vector3.up * .3f;
            fx.GetComponent<AquariumDiveSplash>().Present(seconds, new Vector3(e.position.x, e.position.y, e.position.z), forward, diver);
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
        private Actor MakeActor(string kind, int alienLook = 0)
        {
            var actor = new Actor { root = new GameObject(kind), kind = kind, alienLook = alienLook }; actor.root.transform.SetParent(root);
            var t = actor.root.transform;
            if (kind.StartsWith("fish") || kind == "alien")
            {
                var visual = new GameObject("Rigged creature");visual.transform.SetParent(t,false);
                actor.creature=visual.AddComponent<AquariumCreature>();actor.creature.Initialize(kind=="alien"?-1:int.Parse(kind.Substring(4)),random.Next(10000),alienLook);
            }
            else if (kind == "diver")
            {
                // R53: alienLook carries the diver kind for divers (yellow keeper, blue tracker, pink drone pilot).
                actor.diver = UnityEngine.Object.Instantiate(AquariumDiverKinds.DiverPrefab(alienLook), t).GetComponent<AquariumRig>();
                actor.gun = AquariumFirstPersonEquipment.CreateGun(t, actor.diver);
                AquariumDiverKinds.PaintGun(actor.gun, alienLook);
                // R31: the free (left) glove gets finger joints so it can open, grab and let go.
                actor.fingers = AquariumFingers.Attach(actor.diver, "HandR", false);
                actor.pouch = AquariumCatchPose.CreatePouch(actor.diver);
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
            float sampleTime = float.NaN, float renderTime = float.NaN, int alienLook = 0)
        {
            visible.Add(key); Actor actor;
            bool creatureKind=kind=="alien" || kind.StartsWith("fish");
            bool fresh = !actors.TryGetValue(key, out actor) || (actor.kind != kind && (actor.creature == null || !creatureKind))
                || (creatureKind && actor.creature != null && actor.alienLook != alienLook)
                || (kind == "diver" && actor.alienLook != alienLook);
            if (fresh)
            {
                if (actor != null) DestroyActor(actor);
                actor = MakeActor(kind, alienLook); actors[key] = actor;
            }
            Vector3 target = new Vector3(at.x, at.y, at.z);
            if((fresh || actor.kind!=kind) && key.StartsWith("p") && actor.creature!=null && kind.StartsWith("fish"))actor.creature.PrepareFeedingForms(KindSpecies(kind));
            actor.kind=kind;
            if(actor.bodyCollider!=null)
            {
                // R23: sized per fish species (a disguise takes its fish's hull).
                int sizedSpecies=kind.StartsWith("fish")?KindSpecies(kind):-1;
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
        // R31: the harpooned catch after the spear seats, as a cartoon throw (see AquariumCatchPose): third person,
        // the free hand snatches it, winds up and flings it over the left shoulder into the sack on the hip; first
        // person, the hand catches it ahead-left and yanks it up and away out of view.
        private void PresentCatchStow(AquariumSnapshot state, Harpoon shot, Actor owner, Actor caught, float radius, Vector3 seated)
        {
            float s=state.time-shot.retrievedAt;
            bool fps=equipment.Visible&&equipment.Owner==shot.owner;
            if(fps)AquariumCatchPose.PresentFirstPersonCatch(Camera.transform,equipment.ReceivingGrip,caught.root.transform,radius,seated,s);
            else if(owner.diver!=null)AquariumCatchPose.PresentStow(owner.diver,owner.fingers,owner.pouch,caught.root.transform,radius,seated,s);
            // R48: reused lists instead of two fresh component arrays every frame of the stow.
            caught.root.GetComponentsInChildren(true,stowTransforms);
            foreach(var tr in stowTransforms)tr.gameObject.layer=fps?AquariumFirstPersonEquipment.Layer:0;
            stowTransforms.Clear();
            // The normal lens-occlusion rule hides nearby fish. A held catch must remain visible.
            caught.root.GetComponentsInChildren(false,stowRenderers);
            foreach(var renderer in stowRenderers)
            {
                if(hiddenRendererStates.TryGetValue(renderer,out bool wasEnabled))
                {renderer.enabled=wasEnabled;hiddenRendererStates.Remove(renderer);}
            }
            stowRenderers.Clear();
            hiddenByCamera.Remove(caught);
            if(caught.bodyCollider!=null)caught.bodyCollider.enabled=false;
        }
        private readonly List<Transform> stowTransforms=new List<Transform>();
        // R77: a skewered alien body (a caught alien or a starved body) wobbles like jelly while it is reeled in and
        // stowed. Presentation only; the skewer pose above stays as it is (the wobble is about the body's middle).
        private readonly List<Actor> jellied=new List<Actor>();
        private void Jiggle(Harpoon shot,Actor caught,bool seated)
        {
            if(!AquariumSkewerJelly.Enabled || shot.caughtPlayer==0 || caught.creature==null)return;
            if(caught.jelly==null)caught.jelly=new AquariumSkewerJelly();
            if(!jellied.Contains(caught))jellied.Add(caught);
            caught.jelly.Present(caught.root.transform,caught.creature.transform,caught.kind=="alien"?AlienBodyLift:0,seated,Time.unscaledDeltaTime,shot.id);
        }
        // Bodies not presented on the spear last frame (released, stowed, removed) get their transform back.
        private void ReleaseJellies(bool all)
        {
            for(int i=jellied.Count-1;i>=0;i--)
            {
                var a=jellied[i];
                if(!all && a.jelly!=null && a.jelly.frame>=Time.frameCount-1 && a.root!=null)continue;
                if(a.jelly!=null)a.jelly.Release();
                jellied.RemoveAt(i);
            }
        }
        private readonly List<Renderer> stowRenderers=new List<Renderer>();
        // R49: a skewered body sits with its middle on the drawn shaft (catchDepth behind the tip, along the drawn
        // spear, so it stays on it while the reel swings the spear onto the barrel) and is turned side-on to the
        // spear: the spear runs through it across its length, from flank to flank.
        private void PinHarpoonCatch(string key, Vector3 tip, Vector3 direction, Harpoon shot)
        {
            if (!actors.TryGetValue(key, out var corpse)) return;
            if (shot.catchDepth > 0) SkewerPose(corpse, tip - direction * SkewerDepth(corpse, shot), direction);
            else corpse.root.transform.position = tip + new Vector3(shot.catchOffset.x, shot.catchOffset.y, shot.catchOffset.z);
            if (corpse.bodyCollider != null) corpse.bodyCollider.enabled = false;
        }
        // How far behind the tip the body middle rides: the model's depth, but at least far enough that the head
        // pokes out of the alien's wide body (its flank is about .5 m from the middle seen side-on).
        private static float SkewerDepth(Actor body, Harpoon shot) { return Mathf.Max(shot.catchDepth, body.kind == "alien" ? .5f : .3f); }
        // The alien's body middle is this far over its root (the fish are centred on theirs).
        private const float AlienBodyLift = .18f;
        private static void SkewerPose(Actor body, Vector3 onShaft, Vector3 spear)
        {
            var root = body.root.transform;
            spear = spear.sqrMagnitude > 1e-6f ? spear.normalized : root.right;
            var forward = Vector3.ProjectOnPlane(root.forward, spear);
            if (forward.sqrMagnitude < .01f) forward = Vector3.Cross(spear, Vector3.up);
            if (forward.sqrMagnitude < .01f) forward = Vector3.Cross(spear, Vector3.forward);
            forward.Normalize();
            var up = Vector3.Cross(forward, spear); if (up.y < 0) up = -up;
            root.rotation = Quaternion.LookRotation(forward, up);
            float lift = body.kind == "alien" ? AlienBodyLift * root.lossyScale.y : 0;
            root.position = onShaft - root.up * lift;
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
            AquariumMaps.Select(state.map); if (environments != null) ShowMap(state.map);
            scenery?.Sync(state.scenerySeed);
            drains.Draw(state, dt);
            bool finished = state.phase == RoundPhase.Finished;
            // Share the gallery shader clock so water and reef motion freeze with the final scene.
            if (!finished) environmentTime = Time.time;
            Shader.SetGlobalFloat("_AquariumPreviewTime", environmentTime);
            Shader.SetGlobalFloat("_AquariumPreviewTimeEnabled", 1);
            if (finished && worldFrozen && frozenRound == state.round) { CleanupFrozenWorld(state); abilities.BeginFrame(); abilities.EndFrame(); abilities.StopXray(); AquariumGalleryCamera.InkBombMurk = 0; return; }
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
            // R47: abilities are drawn for the followed player (the local one, or the one a spectator watches).
            Swimmer viewer = null; int viewerId = followId != int.MinValue ? followId : localId;
            foreach (var candidate in state.players) if (candidate.id == viewerId) { viewer = candidate; break; }
            if (viewer == null) foreach (var candidate in state.players) if (candidate.id == localId) { viewer = candidate; break; }
            abilities.BeginFrame(); abilities.BeginGhosts();
            foreach (var p in state.players)
            {
                if (p.id == localId) local = p;
                // R45: an alien that got through plays a short exit at the drain (peeks back up, then dives in).
                bool escaping=p.escaped && presentationTime>=p.escapedAt-.5f && presentationTime-p.escapedAt<EscapeOutroSeconds;
                if ((p.collected && !escaping) || p.swallowedBy != 0 || (!p.alive && !escaping && (finished || p.team != Team.Hider))) continue;
                float age=visualTime-p.visualAt;
                bool deathPose=!p.alive && !escaping;
                bool morphing=visualTime-p.morphAt<1.4f;
                int form=deathPose?p.deathSpecies:p.species<0 || p.revealUntil>visualTime && !morphing ? -1:p.species;
                string kind=p.team==Team.Seeker?"diver":form<0?"alien":FishKind(form);
                bool frozenPose=finished && !escaping && actors.ContainsKey(PlayerKey(p.id));
                if (actors.TryGetValue(PlayerKey(p.id), out var released) && released.carried)
                { released.positions.Clear(); released.carried = false; }
                var actor=Place(PlayerKey(p.id),kind,p.position,p.facing,dt,visualTime,deathPose,state.time,renderAt,p.team==Team.Seeker?p.diverKind:p.alienKind);
                if (actor.diver != null && !frozenPose)
                {
                    Harpoon heldShot=null;foreach(var h in state.harpoons)if(h.owner==p.id){heldShot=h;break;}
                    float firedAt=heldShot!=null?heldShot.born:p.nextAction-AquariumModel.HarpoonCooldown;
                    // R33: fire/reload follow the spear itself (out until it seats); the 5 s cooldown runs on past that.
                    float fireAge=visualTime-firedAt,remaining=heldShot!=null?1:0;
                    // Round opening: stand on the dive deck, then the giant-stride entry (the host moves the root).
                    float entry=p.entryAt>0?visualTime-(p.entryAt-AquariumDiveEntry.Seconds):float.NaN;
                    if(!float.IsNaN(entry) && visualTime<p.entryAt && actor.diver.HasClip("DiveIn") && actor.diver.HasClip("DeckIdle"))
                    {
                        // R60: waiting in the changing room: standing, or a walk over the standing pose when moving.
                        if(entry<0){actor.diver.SamplePose("DeckIdle",visualTime,visualTime);AquariumLockerRoom.WalkPose(actor.diver,visualTime,actor.moveSpeed,p.id*.31f);}
                        else actor.diver.SamplePose("DiveIn",entry,visualTime);
                    }
                    else if(actor.diver.cartoon && remaining>0 && fireAge<.5f)actor.diver.SamplePose("Fire",fireAge,visualTime);
                    else if(actor.diver.cartoon && remaining>0)actor.diver.SamplePose("Reload",Mathf.Min(2.95f,fireAge),visualTime);
                    else
                    {
                        // Swim direction in the diver's own heading frame picks the forward / back / sideways stroke.
                        var heading=new Vector3(p.facing.x,0,p.facing.z);
                        var swimLocal=heading.sqrMagnitude>.0001f?Quaternion.Inverse(Quaternion.LookRotation(heading.normalized))*actor.velocity:actor.velocity;
                        actor.diver.SampleLocomotion(actor.moveSpeed,visualTime,p.id*.37f,swimLocal);
                    }
                    actor.gun.SetLoaded(heldShot==null);
                    PresentDiverCover(p, actor, state, visualTime, dt);
                }
                if(actor.creature!=null && escaping)
                {
                    actor.creature.RevealUsesAlienBlood=false;
                    actor.creature.Present(-1,"Locomotion",p.visualAt,presentationTime,0,false);
                    actor.root.transform.localScale=Vector3.one;
                    PresentEscape(p,actor,state,presentationTime);
                    actor.bodyCollider.enabled=false;
                }
                else if(actor.creature!=null && !frozenPose)
                {
                    float actionLength=p.visualAction=="SpitRecoil"?AquariumModel.SpitAnimationSeconds:p.visualAction=="SpitOut"?AquariumModel.ReleaseKickSeconds:p.visualAction=="Bite"?AquariumModel.FishFeedingSeconds:p.visualAction=="TentacleEat"?AquariumModel.TentacleFeedingSeconds:p.visualAction=="AllyGulp"?AquariumModel.AllyStun+.2f:2.4f;
                    string motion=age<actionLength && !string.IsNullOrEmpty(p.visualAction)?p.visualAction:"Locomotion";
                    if((motion=="Bite" || motion=="AllyGulp") && form<0)motion="TentacleEat";
                    if(motion=="Hurt")motion="Locomotion";
                    if(deathPose)motion=p.deathReason=="harpoon"?"Harpoon":"Eaten";
                    if(motion=="Bite" && form>=0 && age<AquariumFeeding.ReturnToFish)form+=100;
                    float poseTime=deathPose?Mathf.Min(visualTime,p.deathAt+2.4f):visualTime;
                    actor.creature.RevealUsesAlienBlood=p.injuredUntil>visualTime || (deathPose&&p.deathReason=="harpoon");
                    actor.creature.Present(form,motion,p.visualAt,poseTime,deathPose?0:actor.moveSpeed,deathPose);
                    actor.root.transform.localScale=Vector3.one;
                    PresentAlienDrain(p,actor,state,visualTime,dt);
                    actor.bodyCollider.enabled=!deathPose || visualTime-p.deathAt<2.4f;
                }
                PresentPlayerAbilities(p,actor,visualTime,viewer,frozenPose);
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
                var actor=Place(PlayerKey(p.id),capturedForm<0?"alien":FishKind(capturedForm),p.swallowedFrom,p.facing,dt,visualTime,false,state.time,renderAt,p.alienKind);
                actor.carried = true;
                actor.root.transform.localScale=Vector3.one;
                actor.creature.Present(capturedForm,"Eaten",p.visualAt,visualTime,0);
                if(actors.TryGetValue(PlayerKey(p.swallowedBy),out var predator))IngestActor(actor,predator,age);
                actor.bodyCollider.enabled=false;
            }
            foreach (var f in state.fish)
            {
                float age=visualTime-f.visualAt;
                if(f.collected || finished && (f.eaten || f.dead) || f.eaten && age>=MealVisible(state,f.eater))continue;
                bool frozenPose=finished && actors.ContainsKey(FishKey(f.id));
                var actor=Place(FishKey(f.id),FishKind(f.species),f.position,f.facing,dt,visualTime,f.dead && age>2.4f,state.time,renderAt);
                if(frozenPose)continue;
                actor.root.transform.localScale=Vector3.one;
                actor.creature.Present(f.species,f.eaten?"Eaten":f.dead?"Harpoon":"Locomotion",f.visualAt,visualTime,actor.moveSpeed,f.dead && !f.eaten);
                if(f.eaten && actors.TryGetValue(PlayerKey(f.eater),out var predator))IngestActor(actor,predator,age);
                actor.bodyCollider.enabled=!f.eaten;
                // R47: a fish stunned by the yellow alien's shock hangs frozen, twitching and crackling.
                if(f.stunnedUntil>visualTime && !f.dead && !f.eaten)
                {
                    float jolt=Mathf.Clamp01((f.stunnedUntil-visualTime)/.6f);
                    actor.root.transform.rotation*=Quaternion.Euler(Mathf.Sin(visualTime*63+f.id)*4*jolt,Mathf.Sin(visualTime*79+f.id*2)*6*jolt,Mathf.Sin(visualTime*97+f.id)*7*jolt);
                    abilities.Sparks(actor.root.transform.position,AquariumFishTraits.HullRadius(f.species)*.7f,visualTime,1000+f.id);
                }
            }
            foreach (var h in state.harpoons)
            {
                // Interpolated tip and a smoothed heading: snapshots arrive in steps, the spear must not.
                var shotActor = Place(HarpoonKey(h.id), "harpoon", h.position, h.direction, dt, visualTime,false,state.time,renderAt);
                var heading = new Vector3(h.direction.x, h.direction.y, h.direction.z);
                if (heading.sqrMagnitude < .0001f) heading = Vector3.forward;
                shotActor.shotTip = shotActor.root.transform.position;
                shotActor.shotDirection = !shotActor.shotPresented ? heading.normalized : Vector3.Slerp(shotActor.shotDirection, heading.normalized, 1 - Mathf.Exp(-Mathf.Max(0, dt) * 14));
                shotActor.shotPresented = true;
            }
            if (state.eggs != null) foreach (var egg in state.eggs)
            {
                var actor = Place("e" + egg.id, "egg", egg.position, new Point(0, 1), dt, visualTime,false,state.time,renderAt);
                float urgency = egg.hatchAt <= 0 ? 0 : Mathf.Clamp01(1 - (egg.hatchAt - visualTime) / AquariumModel.HatchSeconds);   // R76: 0 = waiting
                float pulse = Mathf.Sin(visualTime * (3 + urgency * 5) + egg.id) * (0.025f + urgency * 0.025f);
                // Pulse sideways while keeping the shell's foot planted on the collision surface.
                actor.root.transform.localScale = new Vector3(1 - pulse, 1, 1 - pulse);
            }
            PresentDrones(state, dt, visualTime, renderAt, finished);
            removedActors.Clear();
            foreach (var pair in actors) if (!visible.Contains(pair.Key)) { DestroyActor(pair.Value); removedActors.Add(pair.Key); }
            foreach (var key in removedActors) actors.Remove(key);
            localRing.gameObject.SetActive(state.phase == RoundPhase.Playing && local != null && local.team == Team.Hider && local.alive && local.swallowedBy == 0);
            if (local != null)
            { var displayed = PlayerPosition(local); localRing.transform.position = new Vector3(displayed.x, 0.25f, displayed.z); }
            activeEffects.Clear(); float bombMurk = 0;
            foreach (var e in state.effects)
            {
                activeEffects.Add(e.id); GameObject fx;
                if(finished && effects.ContainsKey(e.id))continue;
                // No expanding rings for any event (harpoon shot, miss, meal, morph, scan, egg...).
                // Events keep only their ink, splash or biscuits; their sounds are played elsewhere.
                if (e.kind != "feed" && e.kind != "splash" && e.kind != "bloodTrail" && e.kind != "mealTrail" && !AquariumInkCloud.IsInk(e.kind) && !AquariumAbilityFx.IsAbility(e.kind)) continue;
                if (!effects.TryGetValue(e.id, out fx))
                {
                    if (e.kind == "splash") fx = MakeSplash();
                    else if (AquariumAbilityFx.IsAbility(e.kind)) fx = abilities.Make(e.kind, e.id);
                    else if (e.kind == "mealTrail" || e.kind == "bloodTrail")
                    {
                        // R32: both are long, thick dye ribbons: purple fish blood from the mouth after a meal,
                        // green alien blood from behind a harpooned alien for its whole injury.
                        fx=new GameObject(e.kind=="bloodTrail"?"Green wound wake":"Fish blood wake");fx.transform.SetParent(root,false);
                        fx.AddComponent<AquariumMealTrail>().Initialize(e.id,e.kind=="bloodTrail");
                    }
                    else if (AquariumInkCloud.IsInk(e.kind))
                    {
                        fx=new GameObject(e.kind+" volumetric ink");fx.transform.SetParent(root,false);
                        fx.AddComponent<AquariumInkCloud>().Initialize(e.id,e.kind);
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
                else if(AquariumAbilityFx.IsAbility(e.kind))bombMurk=Mathf.Max(bombMurk,PresentAbility(fx,e,visualTime,viewer));
                else if(e.kind=="mealTrail")
                {
                    Vector3 source=new Vector3(e.position.x,e.position.y,e.position.z);
                    Swimmer owner=null;foreach(var swimmer in state.players)if(swimmer.id==e.owner){owner=swimmer;break;}
                    bool canEmit=owner!=null&&owner.alive&&owner.swallowedBy==0;
                    if(canEmit&&actors.TryGetValue(PlayerKey(e.owner),out var feeder)&&feeder.creature!=null)
                        source=feeder.creature.FeedingMouth+feeder.creature.FeedingForward*.04f-Vector3.up*.16f;
                    else canEmit=false;
                    fx.GetComponent<AquariumMealTrail>().Present(visualTime-e.born,e.duration,source,canEmit);
                }
                else if(e.kind=="bloodTrail")
                {
                    Vector3 source=new Vector3(e.position.x,e.position.y,e.position.z);
                    Swimmer owner=null;foreach(var swimmer in state.players)if(swimmer.id==e.owner){owner=swimmer;break;}
                    bool canEmit=owner!=null&&owner.alive&&owner.swallowedBy==0&&!owner.escaped;
                    if(canEmit&&actors.TryGetValue(PlayerKey(e.owner),out var hurt))
                    {
                        // From the wound side of the body: just behind it and a little low.
                        var body=hurt.root.transform;
                        source=body.position-body.forward*.32f-Vector3.up*.05f;
                    }
                    else canEmit=false;
                    fx.GetComponent<AquariumMealTrail>().Present(visualTime-e.born,e.duration,source,canEmit);
                }
                else if(AquariumInkCloud.IsInk(e.kind))
                {
                    float seconds=visualTime-e.born-(e.kind=="burp"?0:.12f);
                    Vector3 source=new Vector3(e.position.x,e.position.y,e.position.z),direction=new Vector3(e.direction.x,e.direction.y,e.direction.z);
                    if(e.owner!=0 && actors.TryGetValue(PlayerKey(e.owner),out var feeder) && feeder.creature!=null)
                    {direction=feeder.creature.FeedingForward;source=feeder.creature.FeedingMouth+direction*.015f;}
                    var inkCloud=fx.GetComponent<AquariumInkCloud>();
                    inkCloud.Present(seconds,e.duration-(e.kind=="burp"?0:.12f),source,direction);
                    // R49: a diver swimming through alien ink (meal, ally gulp, spit, the green ink a harpooned alien bleeds) gets a murky view; fish blood does not.
                    if(viewer!=null&&viewer.team==Team.Seeker&&Camera!=null&&e.kind!="harpoonFish")
                        bombMurk=Mathf.Max(bombMurk,inkCloud.CameraMurk(Camera.transform.position));
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
            foreach (var e in effects) if (!activeEffects.Contains(e.Key)) { e.Value.SetActive(false); ReleaseObject(e.Value); expiredEffects.Add(e.Key); }
            foreach (int id in expiredEffects) effects.Remove(id);
            AquariumGalleryCamera.InkBombMurk = bombMurk;
            PresentXray(state, viewer, visualTime);
            abilities.EndGhosts(); abilities.EndFrame();
        }

        // ---------------------------------------------------------------- R47 abilities
        private static bool Teammates(Swimmer a, Swimmer b) { return a != null && b != null && a.team == b.team; }
        // Per player: the blue alien's invisibility, the red alien's dash stretch, a shocked diver's jitter and sparks.
        private void PresentPlayerAbilities(Swimmer p, Actor actor, float now, Swimmer viewer, bool frozenPose)
        {
            if (actor == null || actor.root == null) return;
            if (p.team == Team.Hider && p.alive && !p.escaped)
            {
                float gone = AquariumModel.Invisibility(p, now);
                if (gone > .001f)
                {
                    // The alien itself and its teammates keep a faint shimmering ghost; everyone else loses it completely.
                    bool friend = viewer == null ? false : viewer.id == p.id || Teammates(viewer, p);
                    abilities.Ghost(actor.root, friend ? Mathf.Lerp(1, .3f, gone) : 1 - gone, friend ? gone : 0);
                }
                if (p.dashUntil > now && !frozenPose)
                {
                    float u = Mathf.Clamp01(1 - (p.dashUntil - now) / AquariumModel.DashSeconds), s = Mathf.Sin(u * Mathf.PI);
                    actor.root.transform.localScale = new Vector3(1 - .2f * s, 1 - .2f * s, 1 + .55f * s);
                }
            }
            if (p.team == Team.Seeker && p.alive && p.stunnedUntil > now && !frozenPose)
            {
                // A shocked diver twitches on the spot and crackles with sparks.
                float jolt = Mathf.Clamp01((p.stunnedUntil - now) / .6f);
                if (actor.diver != null && actor.rigPoseStored && actor.drainWeight <= 0)
                {
                    var rig = actor.diver.transform;
                    rig.localPosition = actor.rigRestPosition + new Vector3(Mathf.Sin(now * 71 + p.id), Mathf.Sin(now * 83 + 2 * p.id), Mathf.Sin(now * 59)) * (.025f * jolt);
                    rig.localRotation = actor.rigRestRotation * Quaternion.Euler(Mathf.Sin(now * 67) * 5 * jolt, Mathf.Sin(now * 89 + p.id) * 6 * jolt, Mathf.Sin(now * 97) * 7 * jolt);
                    actor.rigMoved = true;
                }
                abilities.Sparks(actor.root.transform.position, AquariumBodyCollision.DiverRadius * 1.1f, now, p.id);
            }
        }

        // The fish drone of the third diver kind: a sky-blue Fish0 with glowing red pupils, never targetable.
        private void PresentDrones(AquariumSnapshot state, float dt, float now, float renderAt, bool finished)
        {
            foreach (var p in state.players)
            {
                if (p.team != Team.Seeker || !p.droneOut || (finished && !p.alive)) continue;
                string key = ActorKey(droneKeys, "d", p.id);
                bool frozenPose = finished && actors.ContainsKey(key);
                var actor = Place(key, "fish0", p.dronePosition, p.droneFacing, dt, now, false, state.time, renderAt);
                if (actor.bodyCollider != null) actor.bodyCollider.enabled = false;
                if (actor.creature == null || frozenPose) continue;
                actor.root.transform.localScale = Vector3.one;
                actor.creature.Present(0, "Locomotion", 0, now, Mathf.Max(actor.moveSpeed, .6f));
                if (actor.droneEyes == null) actor.droneEyes = abilities.MakeDroneEyes(actor.root.transform);
                AquariumAbilityFx.PresentDroneEyes(actor.droneEyes, actor.creature.CurrentRig, now);
            }
        }

        // One ability event; returns how much of the ink bomb's murk the viewer's camera is in (0..1).
        private float PresentAbility(GameObject fx, WaterEffect e, float now, Swimmer viewer)
        {
            float age = now - e.born; var at = V(e.position); var direction = V(e.direction);
            switch (e.kind)
            {
                case "inkBomb":
                {
                    var cloud = fx.GetComponent<AquariumInkCloud>(); if (cloud == null) return 0;
                    // Thick for divers (and anyone watching one); faint for the aliens that use it.
                    bool diver = viewer != null && (viewer.team == Team.Seeker || viewer.id == e.owner);   // R75: the alien that threw it sees it thick too
                    cloud.PresentBomb(age, e.duration, at, AquariumModel.InkBombRadius, diver ? 1 : .25f);
                    if (!diver || Camera == null || age < 0 || age >= e.duration) return 0;
                    float fadeIn = Mathf.Clamp01(age / .35f), fadeOut = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(e.duration - 1.6f, e.duration, age));
                    float inside = Mathf.Clamp01((AquariumModel.InkBombRadius + .5f - Vector3.Distance(Camera.transform.position, at)) / 2f);
                    return inside * fadeIn * fadeOut;
                }
                case "shock": abilities.PresentShock(fx.transform, age, e.duration, at, AquariumModel.ShockRadius, e.id); break;
                case "dash":
                {
                    Actor owner; var body = actors.TryGetValue(PlayerKey(e.owner), out owner) ? owner.root.transform.position : at + direction * AquariumModel.DashDistance;
                    abilities.PresentDash(fx.transform, age, e.duration, at, direction, body);
                    break;
                }
                case "glue": abilities.PresentGlue(fx.transform, age, e.duration, at, e.id); break;
                case "droneLaunch": abilities.PresentPuff(fx.transform, age, e.duration, at, e.id); break;
            }
            return 0;
        }

        // The x-ray diver (and whoever watches them) sees every living creature through walls in red for 5 s.
        private void PresentXray(AquariumSnapshot state, Swimmer viewer, float now)
        {
            bool on = viewer != null && viewer.alive && state.phase == RoundPhase.Playing && AquariumModel.XrayOn(viewer, now);
            if (!on) { abilities.StopXray(); return; }
            float start = viewer.abilityUntil - AquariumModel.XraySeconds;
            float weight = Mathf.Min(Mathf.Clamp01((now - start) / .25f), Mathf.Clamp01((viewer.abilityUntil - now) / .35f));
            abilities.BeginXray(Camera, weight);
            if (!abilities.XrayActive) return;
            Actor actor;
            foreach (var p in state.players)
                if (p.team == Team.Hider && p.alive && !p.escaped && p.swallowedBy == 0 && actors.TryGetValue(PlayerKey(p.id), out actor)) abilities.XrayAdd(actor.root);
            foreach (var f in state.fish)
                if (!f.dead && !f.eaten && !f.collected && fishKeys.ContainsKey(f.id) && actors.TryGetValue(FishKey(f.id), out actor)) abilities.XrayAdd(actor.root);
            if (state.eggs != null) foreach (var egg in state.eggs)
                if (actors.TryGetValue(EggKey(egg.id), out actor)) abilities.XrayAdd(actor.root);
        }
        private readonly Dictionary<int,string> eggKeys=new Dictionary<int,string>();
        private string EggKey(int id)=>ActorKey(eggKeys,"e",id);

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
            this.followId = followId;
            Actor followed; actors.TryGetValue(PlayerKey(followId), out followed);
            equipment.Present(state, followId, firstPerson, followed == null ? 0 : followed.moveSpeed);
            ReleaseJellies(state == null || state.phase == RoundPhase.Finished);
            if (state == null || state.phase == RoundPhase.Finished) return;
            foreach (var diver in actors.Values) { if (diver.pouch != null) AquariumCatchPose.PresentPouch(diver.pouch, 0); if (diver.fingers != null) diver.fingers.Pose(0, 0); }
            foreach (var shot in state.harpoons)
            {
                Actor projectile, owner;
                if (!actors.TryGetValue(HarpoonKey(shot.id), out projectile) || !actors.TryGetValue(PlayerKey(shot.owner), out owner) || owner.gun == null) continue;
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
                string catchKey=shot.caughtFish!=0?FishKey(shot.caughtFish):shot.caughtPlayer!=0?PlayerKey(shot.caughtPlayer):null;
                if(catchKey!=null && actors.TryGetValue(catchKey,out var caught))
                {
                    if(state.time<shot.retrievedAt)
                    {
                        PinHarpoonCatch(catchKey,tip,direction,shot);
                        Jiggle(shot,caught,false);   // R77
                        // R31: the free hand shoots out, fingers spread and waggling, while the spear is still coming in.
                        bool ownView=equipment.Visible&&equipment.Owner==shot.owner;
                        if(!ownView && shot.stowedAt>0 && owner.diver!=null)
                            AquariumCatchPose.PresentStow(owner.diver,owner.fingers,owner.pouch,null,0,Vector3.zero,state.time-shot.retrievedAt);
                    }
                    else
                    {
                        Prey prey=null;if(shot.caughtFish!=0)foreach(var f in state.fish)if(f.id==shot.caughtFish){prey=f;break;}
                        float radius=prey!=null?AquariumFishTraits.HullRadius(prey.species):AquariumBodyCollision.AlienRadius;
                        // R49: the stow starts from the body still skewered on the seated spear, side-on to it.
                        Vector3 seated;
                        if(shot.catchDepth>0)
                        {
                            var axis=gun.muzzle.forward;
                            SkewerPose(caught,dockedTip-axis*SkewerDepth(caught,shot),axis);
                            seated=caught.root.transform.position;
                        }
                        else seated=dockedTip+new Vector3(shot.catchOffset.x,shot.catchOffset.y,shot.catchOffset.z);
                        PresentCatchStow(state,shot,owner,caught,radius,seated);
                        Jiggle(shot,caught,true);   // R77
                    }
                }
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
            foreach (var fish in state.fish) if (!fish.collected && !fish.eaten && !fish.dead) visible.Add(FishKey(fish.id));
            foreach (var player in state.players) if (player.alive && player.swallowedBy == 0) visible.Add(PlayerKey(player.id));
            foreach (var harpoon in state.harpoons) visible.Add(HarpoonKey(harpoon.id));
            if (state.eggs != null) foreach (var egg in state.eggs) visible.Add("e" + egg.id);
            foreach (var player in state.players) if (player.team == Team.Seeker && player.droneOut) visible.Add(ActorKey(droneKeys, "d", player.id));
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
        // ---------------------------------------------------------------- R27 return drains
        private static Vector3 V(Point p) { return new Vector3(p.x, p.y, p.z); }
        // R49: how deep the body sits in the pipe. The arms pour in first and the body sinks until the head
        // jams in the mouth; from then on it stays wedged there for the whole squeeze (no gradual sinking),
        // straining with small jolts that recoil, and only the escape outro slips it in all at once.
        public static float SqueezeWedge { get { return AquariumModel.SqueezeHover + AlienHeadLift - .12f * AquariumFishTraits.AlienSizeRatio; } }
        public static float SqueezeDepth(float u, int seed)
        {
            const float lead = .1f;
            float wedge = SqueezeWedge;
            if (u < lead) return wedge * Mathf.SmoothStep(0, 1, u / lead);
            // Seconds since it jammed (the squeeze lasts ten): a shove every ~1.6 s, quick down then a recoil.
            float s = (u - lead) * 10f, beat = 1.55f + .25f * Mathf.Sin(seed * 1.7f);
            float j = Mathf.Repeat(s / beat + seed * .37f, 1f);
            float shove = j < .16f ? Mathf.Sin(j / .16f * Mathf.PI) : 0;
            // A tiny give over the whole squeeze, so the last seconds feel tighter, never a visible sink.
            return wedge + .035f * Mathf.Clamp01((u - lead) / (1 - lead)) + .04f * shove * Mathf.Clamp01(s * 2);
        }
        // R49: squash-in-place while wedged (multiplies SqueezeThin): above 1 is wider and shorter.
        public static float SqueezeStruggle(float now, int seed)
        {
            return 1 + .07f * Mathf.Sin(now * 6.3f + seed) + .035f * Mathf.Sin(now * 13.1f + seed * 2.3f);
        }
        // Thinning to the opening while the widest part goes through (and inside the pipe).
        // The alien's head is about .33 m across half its width; its widest part sits .2 m over the pivot.
        public const float AlienHalfWidth = .33f * AquariumFishTraits.AlienSizeRatio, AlienHeadLift = .2f * AquariumFishTraits.AlienSizeRatio;
        public static float SqueezeThin(float depth, float now)
        {
            float ratio = Mathf.Clamp(AquariumDrainLayout.OpeningRadius * .95f / AlienHalfWidth, .55f, 1);
            float centre = AquariumModel.SqueezeHover - depth + AlienHeadLift;
            float through = centre > .05f ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(1 - (centre - .05f) / .65f)) : 1;
            float strain = 1 + .05f * Mathf.Sin(now * 5.3f) * through;
            return Mathf.Lerp(1, ratio, through) * strain;
        }
        // R49: the last beat of an escape. It is still wedged in the mouth, struggling; it gathers itself (a quick
        // rise and squash), then stretches long and slips down the pipe all at once (accelerating) and is gone.
        // Times are from the escape (Swimmer.escapedAt); before it, t clamps to 0 (still wedged, same pose as the squeeze).
        public const float EscapeOutroSeconds = .95f;
        private const float EscapeHoldWedged = .3f, EscapeGather = .14f, EscapePlunge = .2f;
        public static void EscapeOutro(float t, int seed, out float depth, out float squeeze, out float wriggle)
        {
            float wedge = SqueezeDepth(1, seed), gone = wedge + 1.7f * AquariumFishTraits.AlienSizeRatio;
            if (t < EscapeHoldWedged)
            {
                depth = wedge + .012f * Mathf.Sin(t * 31 + seed);
                squeeze = SqueezeStruggle(t, seed); wriggle = 15 * Mathf.Sin(t * 19 + seed);
            }
            else if (t < EscapeHoldWedged + EscapeGather)
            {
                float u = (t - EscapeHoldWedged) / EscapeGather, k = Mathf.Sin(u * Mathf.PI * .5f);
                // Anticipation: pulls up a little and squashes wide, a shiver.
                depth = wedge - .05f * k;
                squeeze = Mathf.Lerp(1, 1.18f, k); wriggle = 6 * Mathf.Sin(u * 40);
            }
            else
            {
                float u = Mathf.Clamp01((t - EscapeHoldWedged - EscapeGather) / EscapePlunge);
                // Snap: stretched thin at once, then a fast accelerating plunge.
                depth = Mathf.LerpUnclamped(wedge - .05f, gone, u * u * (1.6f - .6f * u));
                squeeze = Mathf.Lerp(.62f, .5f, u); if (u < .15f) squeeze = Mathf.Lerp(1.18f, .62f, u / .15f);
                wriggle = 0;
            }
        }
        private void PresentEscape(Swimmer p, Actor actor, AquariumSnapshot state, float now)
        {
            var mouth = actor.drainMouth;
            foreach (var e in state.effects) if (e.kind == "escape" && e.owner == p.id) { mouth = V(e.position); break; }
            EscapeOutro(Mathf.Max(0, now - p.escapedAt), p.id, out float depth, out float squeeze, out float wriggle);
            actor.drainMouth = mouth; actor.drainU = 1; actor.drainDepth = depth; actor.drainWeight = 1; actor.drainShown = DrainTask.Enter;
            actor.creature.PresentSqueeze(mouth, AquariumDrainLayout.OpeningRadius, depth, SqueezeThin(depth, now) * squeeze, wriggle, 1, now);
        }
        private void PresentAlienDrain(Swimmer p, Actor actor, AquariumSnapshot state, float now, float dt)
        {
            if (actor.creature == null) return;
            var all = state.drains;
            bool valid = p.alive && all != null && p.drainIndex >= 0 && p.drainIndex < all.Length;
            var task = valid ? p.drainTask : DrainTask.None;
            float ease = 1 - Mathf.Exp(-Mathf.Max(0, dt) * 10);
            if (task == DrainTask.Enter)
            {
                var d = all[p.drainIndex];
                actor.drainU = actor.drainShown == DrainTask.Enter ? Mathf.Lerp(actor.drainU, p.drainProgress, ease) : p.drainProgress;
                actor.drainDepth = SqueezeDepth(actor.drainU, p.id);
                actor.drainMouth = V(AquariumDrainLayout.Mouth(d));
                actor.drainWeight = Mathf.MoveTowards(actor.drainShown == DrainTask.Enter ? actor.drainWeight : 0, 1, Mathf.Max(0, dt) / .25f);
                float wriggle = (14 * Mathf.Sin(now * 2.6f + p.id) + 9 * Mathf.Sin(now * 4.3f + 1) + 5 * Mathf.Sin(now * 9.7f + p.id * 3)) * actor.drainWeight;
                float struggle = Mathf.Lerp(1, SqueezeStruggle(now, p.id), actor.drainWeight);
                actor.creature.PresentSqueeze(actor.drainMouth, AquariumDrainLayout.OpeningRadius, actor.drainDepth, SqueezeThin(actor.drainDepth, now) * struggle, wriggle, actor.drainWeight, now);
                actor.drainShown = DrainTask.Enter;
                return;
            }
            if (actor.drainShown == DrainTask.Enter || actor.drainDepth > 0)
            {
                // Popped or backed out: the body springs back up out of the pipe.
                actor.drainDepth = Mathf.Lerp(actor.drainDepth, 0, 1 - Mathf.Exp(-Mathf.Max(0, dt) * 12));
                if (actor.drainDepth < .003f) actor.drainDepth = 0;
                if (actor.drainDepth > 0) actor.creature.PresentSqueeze(actor.drainMouth, AquariumDrainLayout.OpeningRadius, actor.drainDepth, SqueezeThin(actor.drainDepth, now), 0, 1, now);
                if (actor.drainShown == DrainTask.Enter) { actor.drainShown = DrainTask.None; actor.drainWeight = 0; }
            }
            if (task == DrainTask.Screw || task == DrainTask.Lid)
            {
                var instance = drains.Instance(p.drainIndex);
                if (instance == null) return;
                if (task == DrainTask.Screw && p.drainScrew >= 0 && p.drainScrew < instance.screws.Length)
                { actor.drainA = instance.screws[p.drainScrew].position + Vector3.up * .02f; actor.drainB = actor.drainA; }
                else if (task == DrainTask.Lid)
                {
                    // Two grips on the lid rim either side of the alien's line to it.
                    var centre = instance.lid.position + Vector3.up * AquariumDrainLayout.LidTop;
                    var toward = centre - actor.root.transform.position; toward.y = 0;
                    toward = toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.forward;
                    var side = Vector3.Cross(Vector3.up, toward);
                    float r = AquariumDrainLayout.LidRadius * .82f;
                    actor.drainA = centre - toward * (r * .45f) + side * (r * .85f);
                    actor.drainB = centre - toward * (r * .45f) - side * (r * .85f);
                }
                if (actor.drainShown != task) actor.drainWeight = 0;
                actor.drainWeight = Mathf.MoveTowards(actor.drainWeight, 1, Mathf.Max(0, dt) / .15f);
                actor.creature.PresentDrainReach(task == DrainTask.Screw ? AlienDrainMotion.Mode.Screw : AlienDrainMotion.Mode.Lid,
                    actor.drainA, actor.drainB, actor.drainWeight, now, now - p.drainAt);
                actor.drainShown = task;
            }
            else if (actor.drainShown == DrainTask.Screw || actor.drainShown == DrainTask.Lid)
            {
                // Letting go: the arm springs back to the body.
                actor.drainWeight = Mathf.MoveTowards(actor.drainWeight, 0, Mathf.Max(0, dt) / .3f);
                actor.creature.PresentDrainReach(actor.drainShown == DrainTask.Screw ? AlienDrainMotion.Mode.Screw : AlienDrainMotion.Mode.Lid,
                    actor.drainA, actor.drainB, actor.drainWeight * actor.drainWeight, now, 10);
                if (actor.drainWeight <= 0) actor.drainShown = DrainTask.None;
            }
        }
        // The diver shoves a moved lid back with the free left hand, in slow heavy strokes.
        private void PresentDiverCover(Swimmer p, Actor actor, AquariumSnapshot state, float now, float dt)
        {
            bool covering = p.alive && p.drainTask == DrainTask.Cover && state.drains != null && p.drainIndex >= 0 && p.drainIndex < state.drains.Length;
            actor.drainWeight = Mathf.MoveTowards(actor.drainWeight, covering ? 1 : 0, Mathf.Max(0, dt) / .3f);
            if (covering)
            {
                var instance = drains.Instance(p.drainIndex);
                if (instance != null)
                {
                    var centre = instance.lid.position + Vector3.up * AquariumDrainLayout.LidTop;
                    var toward = actor.root.transform.position - centre; toward.y = 0;
                    toward = toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.forward;
                    actor.drainA = centre + toward * (AquariumDrainLayout.LidRadius * .8f);
                    actor.drainB = -toward;
                }
            }
            if (actor.diver == null) return;
            var rig = actor.diver.transform;
            if (!actor.rigPoseStored) { actor.rigRestPosition = rig.localPosition; actor.rigRestRotation = rig.localRotation; actor.rigPoseStored = true; }
            if (actor.drainWeight <= 0)
            {
                if (actor.rigMoved) { rig.localPosition = actor.rigRestPosition; rig.localRotation = actor.rigRestRotation; actor.rigMoved = false; }
                return;
            }
            // The host keeps the diver's body clear of the sand, so the drawn diver stoops down over the lid:
            // leaning in with the left shoulder above the rim, the free hand flat on it, shoving in slow strokes.
            float w = Mathf.SmoothStep(0, 1, actor.drainWeight);
            float stroke = .5f - .5f * Mathf.Cos(now * 2 * Mathf.PI * .9f);
            var rim = actor.drainA; var away = -actor.drainB;
            var restPosition = actor.root.transform.TransformPoint(actor.rigRestPosition); var restRotation = actor.root.transform.rotation * actor.rigRestRotation;
            var upper = Array.Find(actor.diver.Bones, b => b.name == "UpperArmL"); var lower = Array.Find(actor.diver.Bones, b => b.name == "ForearmL"); var hand = Array.Find(actor.diver.Bones, b => b.name == "HandL");
            if (upper == null || lower == null || hand == null) return;
            float arm = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
            rig.rotation = Quaternion.LookRotation((-away + Vector3.down * .75f).normalized, Vector3.up);
            var shoulder = rim + Vector3.up * (arm * .78f - .04f * stroke) + away * (.06f + .05f * stroke);
            var leaned = rig.position + (shoulder - upper.position); var leanedRotation = rig.rotation;
            rig.position = Vector3.Lerp(restPosition, leaned, w); rig.rotation = Quaternion.Slerp(restRotation, leanedRotation, w);
            actor.rigMoved = true;
            AquariumCatchPose.SolveLeft(actor.diver, rim + Vector3.up * .03f - away * (.05f * stroke), w);
        }

        // What the crosshair is on: a screw, the lid or the open hole (aliens), or a moved lid (the diver).
        public DrainTarget FindDrainTarget(AquariumSnapshot state, Swimmer caster)
        {
            if (state == null || caster == null || !caster.alive || caster.swallowedBy != 0 || worldFrozen || state.drains == null || state.phase != RoundPhase.Playing) return null;
            var origin = Camera.transform.position; var ray = Camera.transform.forward;
            DrainTarget best = null; float nearest = 10f;
            for (int i = 0; i < state.drains.Length && i < drains.Count; i++)
            {
                var d = state.drains[i]; var instance = drains.Instance(i);
                if (instance == null) continue;
                var lidCentre = instance.lid.position + Vector3.up * AquariumDrainLayout.LidTop;
                if (caster.team == Team.Hider)
                {
                    if (d.lid <= 0 && d.screws != null)
                        for (int s = 0; s < instance.screws.Length && s < d.screws.Length; s++)
                        {
                            if (d.screws[s] >= 1) continue;
                            float t = SphereHit(origin, ray, instance.screws[s].position + Vector3.up * .03f, .14f);
                            if (t < nearest) { nearest = t; best = new DrainTarget { drain = i, part = s, point = instance.screws[s].position }; }
                        }
                    if (d.lid < 1)
                    {
                        // Screw heads sit on the lid: they win a near tie with the lid face.
                        float t = DiscHit(origin, ray, lidCentre, AquariumDrainLayout.LidRadius * 1.04f) + .25f;
                        if (t < nearest) { nearest = t; best = new DrainTarget { drain = i, part = AquariumModel.DrainLidPart, point = lidCentre }; }
                    }
                    if (d.isOpen)
                    {
                        var mouth = V(AquariumDrainLayout.Mouth(d));
                        float t = DiscHit(origin, ray, mouth, AquariumDrainLayout.OpeningRadius + .2f);
                        if (t < nearest) { nearest = t; best = new DrainTarget { drain = i, part = AquariumModel.DrainHolePart, point = mouth }; }
                    }
                }
                else if (d.lid > 0)
                {
                    float t = DiscHit(origin, ray, lidCentre, AquariumDrainLayout.LidRadius * 1.1f);
                    if (t < nearest) { nearest = t; best = new DrainTarget { drain = i, part = AquariumModel.DrainLidPart, point = lidCentre }; }
                }
            }
            if (best == null) return null;
            string reason;
            best.usable = AquariumModel.CanUseDrainPart(caster, state.drains, best.drain, best.part, state.players, state.time, state.huntAt, out reason);
            best.reason = reason;
            return best;
        }
        private static float SphereHit(Vector3 origin, Vector3 ray, Vector3 centre, float radius)
        {
            var offset = centre - origin; float along = Vector3.Dot(offset, ray);
            if (along < 0) return float.MaxValue;
            float miss = (offset - ray * along).sqrMagnitude;
            return miss <= radius * radius ? along - Mathf.Sqrt(radius * radius - miss) : float.MaxValue;
        }
        private static float DiscHit(Vector3 origin, Vector3 ray, Vector3 centre, float radius)
        {
            if (ray.y > -.02f) return float.MaxValue;
            float t = (centre.y - origin.y) / ray.y; if (t < 0) return float.MaxValue;
            var hit = origin + ray * t; hit.y = centre.y;
            return (hit - centre).sqrMagnitude <= radius * radius ? t : float.MaxValue;
        }
        public void SetDrainTarget(DrainTarget target)
        { drains.Highlight(target != null && !worldFrozen ? target.drain : -1, target != null ? target.part : -1); }
        // R29: x-ray rims round the screws near this alien (null hides them all).
        public void ShowDrainRims(Swimmer alien)
        { drains.Rims(alien != null && !worldFrozen ? PlayerPosition(alien) : (Vector3?)null); }
        // Where a part is drawn now (the lid eases between snapshots).
        public Vector3 DrainPartPosition(AquariumSnapshot state, int drain, int part)
        {
            var instance = drains.Instance(drain);
            if (instance != null && part >= 0 && part < instance.screws.Length) return instance.screws[part].position;
            if (instance != null && part == AquariumModel.DrainLidPart) return instance.lid.position + Vector3.up * AquariumDrainLayout.LidTop;
            if (state != null && state.drains != null && drain >= 0 && drain < state.drains.Length) return V(AquariumModel.DrainPartPoint(state.drains[drain], part));
            return Vector3.zero;
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
            foreach (var presentation in sceneryByMap) presentation?.Dispose();
            // R57: the scene's own (default) gallery stays in the scene, shown again; the twin gallery goes with the view.
            if (environments[AquariumMaps.Basic] != null) environments[AquariumMaps.Basic].gameObject.SetActive(true);
            AquariumMaps.Select(AquariumMaps.Basic); AquariumSceneryLayout.Apply(0);
            drains.Dispose();
            abilities.Dispose(); AquariumGalleryCamera.InkBombMurk = 0;
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

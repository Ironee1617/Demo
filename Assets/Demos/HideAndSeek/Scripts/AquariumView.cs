using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // Procedural artwork stays separate from the host-authoritative game rules.
    public sealed class AquariumView : IDisposable
    {
        private sealed class Actor
        {
            public GameObject root, normalMouth, predatorMouth;
            public Transform tail;
            public Transform[] tentacles;
            public string kind;
        }
        private sealed class OutlinePart { public Renderer source, shell; }
        private readonly List<OutlinePart> outlineParts = new List<OutlinePart>();
        private Actor outlinedActor;
        private readonly Material outlineMaterial;
        private bool worldFrozen, freezeExistingPoses;
        private int frozenRound;
        private float environmentTime;
        private readonly Transform root;
        private readonly Dictionary<string, Actor> actors = new Dictionary<string, Actor>();
        private readonly Dictionary<int, GameObject> effects = new Dictionary<int, GameObject>();
        private readonly List<Material> materials = new List<Material>();


        private readonly List<Renderer> glassPanels = new List<Renderer>();
        private readonly HashSet<string> visible = new HashSet<string>();
        private readonly HashSet<int> activeEffects = new HashSet<int>();
        private readonly List<string> removedActors = new List<string>();
        private readonly List<int> expiredEffects = new List<int>();
        private readonly HashSet<Actor> hiddenByCamera = new HashSet<Actor>();
        private readonly Dictionary<Renderer, bool> hiddenRendererStates = new Dictionary<Renderer, bool>();
        private readonly Color[] colors = { new Color(1f, 0.49f, 0.12f), new Color(0.25f, 0.55f, 0.91f), new Color(0.94f, 0.45f, 0.62f) };
        private readonly Material sand, dark, teal, white, black, purple, steel, glass, redInk, yellowInk, mouthLining, eggPearl, eggGold;
        private readonly MaterialPropertyBlock effectProperties = new MaterialPropertyBlock();
        private readonly Material[] fishMaterials, finMaterials;
        private readonly Material lineMaterial;
        private readonly Mesh fanMesh, coneMesh;
        private readonly LineRenderer localRing;
        private readonly System.Random random = new System.Random(54);
        public Camera Camera { get; private set; }

        public AquariumView(Transform parent)
        {
            root = new GameObject("Aquarium visuals").transform; root.SetParent(parent);
            sand = Material(new Color(0.56f, 0.53f, 0.4f)); sand.SetFloat("_Glossiness", 0.08f);
            dark = Material(new Color(0.025f, 0.09f, 0.16f)); teal = Material(new Color(0.045f, 0.32f, 0.39f));
            white = Material(new Color(0.96f, 0.99f, 1f)); black = Material(new Color(0.012f, 0.015f, 0.045f));
            purple = Material(new Color(0.81f, 0.68f, 0.97f)); steel = Material(new Color(0.66f, 0.83f, 0.9f));
            mouthLining = Material(new Color(0.89f, 0.28f, 0.5f));
            eggPearl = Material(new Color(1f, 0.9f, 0.69f)); eggGold = Material(new Color(1f, 0.63f, 0.18f));
            eggGold.EnableKeyword("_EMISSION"); eggGold.SetColor("_EmissionColor", new Color(0.3f, 0.12f, 0.015f));
            glass = Material(new Color(0.15f, 0.48f, 0.56f, 0.055f), true);
            redInk = Material(new Color(0.91f, 0.045f, 0.12f, 0.55f), true);
            yellowInk = Material(new Color(1f, 0.79f, 0.08f, 0.55f), true);
            lineMaterial = new Material(Shader.Find("Sprites/Default")); materials.Add(lineMaterial);
            outlineMaterial = new Material(Resources.Load<Shader>("AquariumTargetOutline"));
            outlineMaterial.SetColor("_OutlineColor", new Color(1f, 0.86f, 0.25f)); outlineMaterial.SetFloat("_OutlineWidth", 0.035f); materials.Add(outlineMaterial);
            fishMaterials = new Material[3]; finMaterials = new Material[3];
            for (int i = 0; i < 3; i++) { fishMaterials[i] = Material(colors[i]); finMaterials[i] = Material(Color.Lerp(colors[i], Color.white, 0.38f)); }
            fanMesh = MakeFanMesh(); coneMesh = MakeConeMesh();
            BuildEnvironment();
            localRing = Ring("Your swimmer", root, 0.68f, new Color(0.59f, 1f, 0.81f, 0.85f), 0.035f);
            Camera = UnityEngine.Camera.main;
            if (Camera == null) { var go = new GameObject("Aquarium Camera"); go.tag = "MainCamera"; Camera = go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); }
            Camera.transform.position = new Vector3(0, 23, -20);
            Camera.transform.rotation = Quaternion.Euler(49, 0, 0);
            Camera.orthographic = false; Camera.fieldOfView = 68; Camera.nearClipPlane = 0.06f; Camera.farClipPlane = 100;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(0.025f, 0.12f, 0.18f);
            AquariumEnvironment.ConfigureCamera(Camera);
            AquariumEnvironment.ConfigureLighting(root);
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
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
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
            if (kind.StartsWith("fish"))
            {
                int species = kind[kind.Length - 1] - '0';
                var mat = fishMaterials[species]; var fins = finMaterials[species];
                float width = species == 1 ? 0.42f : 0.56f, height = species == 2 ? 0.62f : 0.53f;
                Ball("Fish body", t, Vector3.zero, new Vector3(width, height, 1.05f), mat);
                Ball("Pale belly", t, new Vector3(0, -0.13f, 0.12f), new Vector3(width * 0.82f, height * 0.62f, 0.78f), fins);
                actor.tail = Fin(t, new Vector3(0, 0, -0.5f), new Vector3(0.9f, 1.1f, 0.75f), species == 1 ? eggGold : fins);
                actor.tail.localRotation = Quaternion.Euler(0, 0, 90);
                var dorsal = Fin(t, new Vector3(0, height * 0.4f, -0.06f), new Vector3(0.56f, 0.5f, 0.65f), fins); dorsal.localRotation = Quaternion.Euler(0, 0, 90);
                for (int side = -1; side <= 1; side += 2)
                {
                    var fin = Fin(t, new Vector3(side * width * 0.43f, -0.03f, -0.02f), new Vector3(0.5f, 1, 0.4f), fins); fin.localRotation = Quaternion.Euler(0, side * -25, side * -14);
                    Ball("Eye white", t, new Vector3(side * width * 0.35f, 0.15f, 0.34f), Vector3.one * 0.18f, white);
                    Ball("Pupil", t, new Vector3(side * width * 0.4f, 0.165f, 0.395f), Vector3.one * 0.102f, black);
                    Ball("Eye glint", t, new Vector3(side * width * 0.41f, 0.195f, 0.43f), Vector3.one * 0.032f, white);
                    for (int stripe = 0; stripe < 3; stripe++)
                    {
                        float z = 0.12f - stripe * 0.23f;
                        Ball("Species markings", t, new Vector3(side * width * 0.48f * Mathf.Sqrt(1 - z * z / 0.29f), 0.02f, z),
                            new Vector3(0.035f, height * (stripe == 2 ? 0.54f : 0.74f), species == 0 ? 0.09f : 0.045f), species == 1 ? finMaterials[1] : white);
                    }
                }
                actor.normalMouth = Ball("Mouth", t, new Vector3(0, -0.02f, 0.52f), new Vector3(0.1f, 0.07f, 0.045f), black);

            }
            else if (kind == "alien")
            {
                Ball("Lilac alien body", t, Vector3.zero, new Vector3(0.82f, 0.81f, 0.73f), purple);
                Ball("Cyclops eye rim", t, new Vector3(0, 0.14f, 0.33f), new Vector3(0.67f, 0.65f, 0.24f), finMaterials[2]);
                Ball("Single cyclops eye", t, new Vector3(0, 0.15f, 0.4f), new Vector3(0.56f, 0.54f, 0.2f), white);
                Ball("Glossy cyclops pupil", t, new Vector3(0, 0.14f, 0.49f), new Vector3(0.32f, 0.35f, 0.11f), black);
                Ball("Cyclops catchlight", t, new Vector3(0.075f, 0.23f, 0.54f), Vector3.one * 0.085f, white);
                actor.tentacles = new Transform[4];
                for (int i = 0; i < 4; i++)
                {
                    float angle = Mathf.PI * (0.25f + i * 0.5f);
                    var foot = new GameObject("Soft tentacle"); foot.transform.SetParent(t, false); foot.transform.localPosition = new Vector3(Mathf.Sin(angle) * 0.25f, -0.27f, Mathf.Cos(angle) * 0.2f);
                    Ball("Tentacle base", foot.transform, new Vector3(0, -0.13f, 0), new Vector3(0.25f, 0.4f, 0.26f), purple);
                    Ball("Tentacle tip", foot.transform, new Vector3(Mathf.Sin(angle) * 0.1f, -0.28f, Mathf.Cos(angle) * 0.1f), new Vector3(0.28f, 0.16f, 0.27f), purple);
                    actor.tentacles[i] = foot.transform;
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    Ball("Side jaw", t, new Vector3(side * 0.52f, -0.14f, 0.05f), new Vector3(0.49f, 0.28f, 0.4f), purple);
                    Ball("Side jaw lining", t, new Vector3(side * 0.52f, -0.04f, 0.08f), new Vector3(0.37f, 0.09f, 0.3f), mouthLining);
                    for (int j = 0; j < 3; j++)
                        MeshShape("Side jaw tooth", coneMesh, t, new Vector3(side * (0.4f + j * 0.1f), 0, 0.09f), new Vector3(0.075f, 0.11f, 0.075f), white);
                }

            }
            else if (kind == "diver")
            {
                Ball("Diving suit", t, Vector3.zero, new Vector3(0.72f, 0.64f, 1.16f), dark);
                Ball("Helmet", t, new Vector3(0, 0.1f, 0.49f), Vector3.one * 0.65f, teal);
                Ball("Visor rim", t, new Vector3(0, 0.2f, 0.65f), new Vector3(0.56f, 0.34f, 0.3f), eggGold);
                Ball("Visor", t, new Vector3(0, 0.23f, 0.71f), new Vector3(0.44f, 0.23f, 0.22f), steel);
                Box("Oxygen tank", t, new Vector3(0, 0.36f, -0.12f), new Vector3(0.37f, 0.29f, 0.65f), steel);
                Box("Tank strap", t, new Vector3(0, 0.38f, -0.12f), new Vector3(0.4f, 0.31f, 0.12f), dark);
                for (int side = -1; side <= 1; side += 2)
                    Box("Flipper", t, new Vector3(side * 0.24f, -0.05f, -0.79f), new Vector3(0.26f, 0.13f, 0.55f), eggGold);
                Box("Harpoon grip", t, new Vector3(0.5f, 0, 0.46f), new Vector3(0.14f, 0.16f, 0.8f), dark);
                Box("Harpoon barrel", t, new Vector3(0.5f, 0, 0.93f), new Vector3(0.065f, 0.065f, 0.6f), steel);
            }
            else if (kind == "harpoon")
            {
                Box("Spear shaft", t, Vector3.zero, new Vector3(0.055f, 0.055f, 0.95f), steel);
                var tip = Fin(t, new Vector3(0, 0, 0.65f), new Vector3(0.35f, 1, 0.4f), white); tip.localRotation = Quaternion.Euler(0, 180, 0);
            }
            else if (kind == "egg")
            {
                Ball("Pearl egg", t, new Vector3(0, 0.28f, 0), new Vector3(0.47f, 0.64f, 0.47f), eggPearl);
                Ball("Amber egg core", t, new Vector3(0, 0.2f, 0.18f), new Vector3(0.22f, 0.28f, 0.12f), eggGold);
                Ball("Egg glint", t, new Vector3(-0.1f, 0.43f, 0.18f), new Vector3(0.07f, 0.11f, 0.035f), white);
                var band = Ring("Shell seam", t, 0.23f, new Color(1f, 0.66f, 0.22f, 0.85f), 0.018f); band.transform.localPosition = new Vector3(0, 0.22f, 0);
            }
            return actor;
        }
        private Actor Place(string key, string kind, Point at, Point facing, float dt, float time, bool dead = false)
        {
            visible.Add(key); Actor actor;
            bool fresh = !actors.TryGetValue(key, out actor) || actor.kind != kind;
            if (fresh)
            {
                if (actor != null) DestroyActor(actor);
                actor = MakeActor(kind); actors[key] = actor;
            }
            Vector3 target = new Vector3(at.x, at.y, at.z);
            float blend = fresh || dead ? 1 : 1 - Mathf.Exp(-dt * 18);
            if (fresh || !freezeExistingPoses) actor.root.transform.position = Vector3.Lerp(actor.root.transform.position, target, blend);
            var direction = new Vector3(facing.x, facing.y, facing.z);
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            var rot = Quaternion.LookRotation(direction, Mathf.Abs(direction.normalized.y) > 0.99f ? Vector3.forward : Vector3.up);
            if (dead) rot *= Quaternion.Euler(0, 0, 180);
            if (fresh || !freezeExistingPoses) actor.root.transform.rotation = fresh || dead ? rot : Quaternion.Slerp(actor.root.transform.rotation, rot, blend);
            if (actor.tail != null) actor.tail.localRotation = Quaternion.Euler(0, dead ? 0 : Mathf.Sin(time * 10 + at.z) * 22, 90);
            if (actor.tentacles != null) for (int i = 0; i < actor.tentacles.Length; i++)
                actor.tentacles[i].localRotation = Quaternion.Euler(Mathf.Sin(time * 3 + i * 1.8f) * 15, 0, Mathf.Cos(time * 3 + i) * 12);
            return actor;
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

        public void Draw(AquariumSnapshot state, int localId, float dt)
        {
            if (state == null) return;
            bool finished = state.phase == RoundPhase.Finished;
            // Share the gallery shader clock so water and reef motion freeze with the final scene.
            if (!finished) environmentTime = Time.time;
            Shader.SetGlobalFloat("_AquariumPreviewTime", environmentTime);
            Shader.SetGlobalFloat("_AquariumPreviewTimeEnabled", 1);
            if (finished && worldFrozen && frozenRound == state.round) { CleanupFrozenWorld(state); return; }
            worldFrozen = finished; frozenRound = state.round; freezeExistingPoses = finished;
            float visualTime = finished ? state.finishedAt : state.time;
            if (finished) SetMorphTarget(null);
            visible.Clear();
            foreach (var f in state.fish) if (!f.eaten && (!finished || !f.dead)) Place("f" + f.id, "fish" + f.species, f.position, f.facing, dt, visualTime, f.dead);
            Swimmer local = null;
            foreach (var p in state.players)
            {
                if (p.id == localId) local = p;
                if (p.swallowedBy != 0 || (!p.alive && (state.phase != RoundPhase.Playing || p.team != Team.Hider))) continue;
                int species = p.alive ? p.species : p.deathSpecies;
                string kind = p.team == Team.Seeker ? "diver" : (species < 0 || (p.alive && p.revealUntil > visualTime) ? "alien" : "fish" + species);
                var actor = Place("p" + p.id, kind, p.position, p.facing, dt, p.alive ? visualTime : p.diedAt, !p.alive);
                bool feeding = state.phase != RoundPhase.Lobby && p.alive && p.team == Team.Hider && p.stunnedUntil > visualTime;
                if (feeding && actor.predatorMouth == null) PredatorMouth(actor, kind == "alien" ? new Vector3(0, -0.25f, 0.48f) : new Vector3(0, -0.02f, 0.56f));
                if (actor.predatorMouth != null)
                {
                    actor.predatorMouth.SetActive(feeding);
                    actor.predatorMouth.transform.localScale = new Vector3(1, 0.9f + Mathf.Sin(visualTime * 9) * 0.1f, 1);
                }
                if (actor.normalMouth != null) actor.normalMouth.SetActive(!feeding);
            }
            foreach (var h in state.harpoons) Place("h" + h.id, "harpoon", h.position, h.direction, dt, visualTime);
            if (state.eggs != null) foreach (var egg in state.eggs)
            {
                var actor = Place("e" + egg.id, "egg", egg.position, new Point(0, 1), dt, visualTime);
                float urgency = Mathf.Clamp01(1 - (egg.hatchAt - visualTime) / AquariumModel.HatchSeconds);
                float pulse = Mathf.Sin(visualTime * (3 + urgency * 5) + egg.id) * (0.025f + urgency * 0.025f);
                actor.root.transform.localScale = new Vector3(1 - pulse, 1 + pulse, 1 - pulse);
            }
            removedActors.Clear();
            foreach (var pair in actors) if (!visible.Contains(pair.Key)) { DestroyActor(pair.Value); removedActors.Add(pair.Key); }
            foreach (var key in removedActors) actors.Remove(key);
            localRing.gameObject.SetActive(state.phase == RoundPhase.Playing && local != null && local.team == Team.Hider && local.alive && local.swallowedBy == 0);
            if (local != null) localRing.transform.position = new Vector3(local.position.x, 0.25f, local.position.z);
            activeEffects.Clear();
            foreach (var e in state.effects)
            {
                activeEffects.Add(e.id); GameObject fx;
                if (!effects.TryGetValue(e.id, out fx))
                {
                    if (e.kind == "eat" || e.kind == "burp")
                    {
                        fx = new GameObject(e.kind + " ink cloud"); fx.transform.SetParent(root, false);
                        for (int i = 0; i < 12; i++)
                        {
                            float angle = i * 2.4f, radius = 0.18f + (i % 5) * 0.12f;
                            var puff = Ball("Ink plume", fx.transform,
                                new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(i * 1.7f) * 0.35f, Mathf.Sin(angle) * radius),
                                new Vector3(0.5f, 0.4f, 0.6f), e.kind == "eat" ? redInk : yellowInk);
                            puff.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                        }
                    }
                    else
                    {
                        Color color = e.kind == "morph" ? new Color(0.86f, 0.63f, 1) : e.kind == "caught" || e.kind == "miss" ? new Color(1, 0.4f, 0.3f) : e.kind == "hatch" || e.kind == "egg" ? new Color(1, 0.8f, 0.4f) : new Color(0.55f, 1, 0.76f);
                        fx = Ring(e.kind, root, 1, color, 0.055f).gameObject;
                        if (e.kind == "feed") for (int i = 0; i < 10; i++)
                            Ball("Food pellet", fx.transform, new Vector3(Mathf.Sin(i * 2.4f) * 0.5f, Mathf.Cos(i * 1.7f) * 0.25f, Mathf.Cos(i * 2.4f) * 0.5f), Vector3.one * 0.08f, fishMaterials[0]);
                    }
                    effects[e.id] = fx;
                }
                float age = Mathf.Clamp01((visualTime - e.born) / Mathf.Max(0.01f, e.duration));
                fx.transform.position = new Vector3(e.position.x, e.position.y + age * 0.3f, e.position.z);
                bool inkCloud = e.kind == "eat" || e.kind == "burp";
                fx.transform.localScale = Vector3.one * (inkCloud ? 0.6f + age * 1.6f : e.kind == "feed" ? 1 : 0.15f + age * 1.3f);
                if (inkCloud)
                {
                    var color = (e.kind == "eat" ? redInk : yellowInk).color;
                    color.a *= 1 - age * age;
                    effectProperties.SetColor("_Color", color);
                    foreach (var renderer in fx.GetComponentsInChildren<Renderer>()) renderer.SetPropertyBlock(effectProperties);
                }
                else
                {
                    var line = fx.GetComponent<LineRenderer>(); var c = line.startColor; c.a = 1 - age; line.startColor = line.endColor = c;
                }
            }
            expiredEffects.Clear();
            foreach (var e in effects) if (!activeEffects.Contains(e.Key)) { UnityEngine.Object.Destroy(e.Value); expiredEffects.Add(e.Key); }
            foreach (int id in expiredEffects) effects.Remove(id);

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
            if (!actors.TryGetValue((player ? "p" : "f") + id, out actor) || !actor.kind.StartsWith("fish")) return;
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
            if (!worldFrozen && target != null) actors.TryGetValue((target.player ? "p" : "f") + target.id, out actor);
            if (actor != null && !actor.kind.StartsWith("fish")) actor = null;
            if (actor == outlinedActor) { UpdateOutlineVisibility(); return; }
            ClearOutline();
            outlinedActor = actor;
            if (actor == null) return;
            foreach (var filter in actor.root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.gameObject.name == "Candidate silhouette") continue;
                var sourceRenderer = filter.GetComponent<Renderer>();
                if (sourceRenderer == null) continue;
                var shell = new GameObject("Candidate silhouette"); shell.transform.SetParent(filter.transform, false);
                shell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = shell.AddComponent<MeshRenderer>(); renderer.sharedMaterial = outlineMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                outlineParts.Add(new OutlinePart { source = sourceRenderer, shell = renderer });
            }
            UpdateOutlineVisibility();
        }
        private void UpdateOutlineVisibility()
        {
            foreach (var part in outlineParts) if (part.shell != null)
                part.shell.enabled = part.source != null && part.source.enabled && part.source.gameObject.activeInHierarchy && !hiddenByCamera.Contains(outlinedActor);
        }
        private void ClearOutline()
        {
            foreach (var part in outlineParts) if (part.shell != null)
            { part.shell.gameObject.SetActive(false); UnityEngine.Object.Destroy(part.shell.gameObject); }
            outlineParts.Clear(); outlinedActor = null;
        }
        private void CleanupFrozenWorld(AquariumSnapshot state)
        {
            // Keep the captured poses and effects untouched while removing objects absent from the snapshot.
            visible.Clear();
            foreach (var fish in state.fish) if (!fish.eaten && !fish.dead) visible.Add("f" + fish.id);
            foreach (var player in state.players) if (player.alive && player.swallowedBy == 0) visible.Add("p" + player.id);
            foreach (var harpoon in state.harpoons) visible.Add("h" + harpoon.id);
            if (state.eggs != null) foreach (var egg in state.eggs) visible.Add("e" + egg.id);
            removedActors.Clear();
            foreach (var pair in actors) if (!visible.Contains(pair.Key)) { DestroyActor(pair.Value); removedActors.Add(pair.Key); }
            foreach (var key in removedActors) actors.Remove(key);
            activeEffects.Clear();
            foreach (var effect in state.effects) activeEffects.Add(effect.id);
            expiredEffects.Clear();
            foreach (var effect in effects) if (!activeEffects.Contains(effect.Key)) { UnityEngine.Object.Destroy(effect.Value); expiredEffects.Add(effect.Key); }
            foreach (int id in expiredEffects) effects.Remove(id);
            localRing.gameObject.SetActive(false);
        }
        public Vector3 PlayerPosition(Swimmer player)
        {
            Actor actor;
            return actors.TryGetValue("p" + player.id, out actor) ? actor.root.transform.position : new Vector3(player.position.x, player.position.y, player.position.z);
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
                if ((hideTarget || Vector3.Distance(Camera.transform.position, target.Value) < 0.9f) && actors.TryGetValue("p" + targetId, out actor)) HideNearCamera(actor);
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
            foreach (var renderer in actor.root.GetComponentsInChildren<Renderer>())
            { hiddenRendererStates[renderer] = renderer.enabled; renderer.enabled = false; }
        }        private void DestroyActor(Actor actor)
        { if (actor == outlinedActor) ClearOutline(); UnityEngine.Object.Destroy(actor.root); }
        public void Dispose()
        {
            Shader.SetGlobalFloat("_AquariumPreviewTimeEnabled", 0);
            ClearOutline();
            foreach (var actor in actors.Values) DestroyActor(actor);
            foreach (var mat in materials) UnityEngine.Object.Destroy(mat);
            UnityEngine.Object.Destroy(fanMesh); UnityEngine.Object.Destroy(coneMesh);
            UnityEngine.Object.Destroy(root.gameObject);
        }
    }
}

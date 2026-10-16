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
        private readonly Transform root;
        private readonly Dictionary<string, Actor> actors = new Dictionary<string, Actor>();
        private readonly Dictionary<int, GameObject> effects = new Dictionary<int, GameObject>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Transform> bubbles = new List<Transform>();
        private readonly List<Transform> plants = new List<Transform>();
        private readonly List<Renderer> glassPanels = new List<Renderer>();
        private readonly HashSet<string> visible = new HashSet<string>();
        private readonly HashSet<int> activeEffects = new HashSet<int>();
        private readonly List<string> removedActors = new List<string>();
        private readonly List<int> expiredEffects = new List<int>();
        private Actor hiddenByCamera;
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
            Camera.allowHDR = false;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.024f;
            RenderSettings.fogColor = new Color(0.055f, 0.23f, 0.3f); RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.39f, 0.43f);
            var sun = UnityEngine.Object.FindFirstObjectByType<Light>();
            if (sun == null) { var go = new GameObject("Aquarium daylight"); go.transform.SetParent(root); sun = go.AddComponent<Light>(); sun.type = LightType.Directional; }
            sun.transform.rotation = Quaternion.Euler(48, -35, 0); sun.color = new Color(0.88f, 0.98f, 1); sun.intensity = 0.68f; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.35f;
            var fill = new GameObject("Soft aquarium fill"); fill.transform.SetParent(root); fill.transform.rotation = Quaternion.Euler(30, 145, 0);
            var fillLight = fill.AddComponent<Light>(); fillLight.type = LightType.Directional; fillLight.color = new Color(0.35f, 0.73f, 0.88f); fillLight.intensity = 0.18f;
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
            Box("Exhibition floor", root, new Vector3(0, -1.3f, 0), new Vector3(80, 1, 60), dark);
            Box("Tank foundation", root, new Vector3(0, -0.55f, 0), new Vector3(30, 1.1f, 19), teal);
            Box("Sand bed", root, new Vector3(0, -0.05f, 0), new Vector3(28, 0.3f, 17), sand);
            Box("Back glass", root, new Vector3(0, 3.5f, 8.65f), new Vector3(28.6f, 7, 0.12f), glass);
            Box("Left glass", root, new Vector3(-14.2f, 3.5f, 0), new Vector3(0.1f, 7, 17.4f), glass);
            Box("Right glass", root, new Vector3(14.2f, 3.5f, 0), new Vector3(0.1f, 7, 17.4f), glass);
            Box("Front glass", root, new Vector3(0, 3.5f, -8.6f), new Vector3(28.6f, 7, 0.1f), glass);
            Box("Water surface", root, new Vector3(0, 7.05f, 0), new Vector3(28, 0.04f, 17), glass);
            var glow = Material(new Color(0.38f, 0.95f, 0.92f)); glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(0.05f, 0.48f, 0.49f));
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Lower light rail", root, new Vector3(0, 0.21f, side * 8.7f), new Vector3(28.9f, 0.07f, 0.1f), glow);
                Box("Lower light rail", root, new Vector3(side * 14.25f, 0.21f, 0), new Vector3(0.1f, 0.07f, 17.5f), glow);
                Box("Upper frame", root, new Vector3(0, 7.1f, side * 8.7f), new Vector3(28.9f, 0.12f, 0.16f), teal);
                Box("Upper frame", root, new Vector3(side * 14.25f, 7.1f, 0), new Vector3(0.16f, 0.12f, 17.5f), teal);
                for (int z = -1; z <= 1; z += 2)
                    Box("Corner frame", root, new Vector3(side * 14.25f, 3.5f, z * 8.7f), new Vector3(0.15f, 7, 0.15f), teal);
            }
            Box("Exhibit backdrop", root, new Vector3(0, 3.2f, 12), new Vector3(43, 9, 0.4f), dark);
            for (int i = 0; i < 8; i++)
            {
                var rail = Box("Aquarium rear mullion", root, new Vector3(-14 + i * 4, 3.5f, 8.8f), new Vector3(0.05f, 7, 0.07f), teal);
                rail.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            var rock = Material(new Color(0.29f, 0.48f, 0.5f));
            var rockTop = Material(new Color(0.48f, 0.65f, 0.57f));
            for (int i = 0; i < AquariumModel.Rocks.Length; i++)
            {
                var at = AquariumModel.Rocks[i]; float radius = AquariumModel.RockRadii[i];
                // This ellipsoid matches the host collision volume exactly.
                Ball("Harpoon cover rock", root, new Vector3(at.x, at.y, at.z), new Vector3(radius * 2, AquariumModel.RockHalfHeight * 2, radius * 2), rock);
                // Small patches sit on the same surface; they do not imply extra cover.
                for (int j = 0; j < 3; j++)
                {
                    float angle = j * 2.1f + i;
                    Ball("Rock moss", root, new Vector3(at.x + Mathf.Sin(angle) * radius * 0.38f, 2.19f, at.z + Mathf.Cos(angle) * radius * 0.38f),
                        new Vector3(radius * 0.42f, 0.13f, radius * 0.4f), rockTop);
                }
            }
            var kelp = Material(new Color(0.1f, 0.59f, 0.43f)); var leafLight = Material(new Color(0.26f, 0.75f, 0.48f));
            var coral = Material(new Color(0.97f, 0.48f, 0.47f)); var lavender = Material(new Color(0.66f, 0.51f, 0.83f));
            for (int i = 0; i < 24; i++)
            {
                float x = Range(-13, 13), z = i < 16 ? Range(7.1f, 8) : Range(-8, -7.6f);
                var clump = new GameObject("Swaying sea grass"); clump.transform.SetParent(root, false); clump.transform.localPosition = new Vector3(x, 0.12f, z); plants.Add(clump.transform);
                for (int j = 0; j < 3; j++)
                {
                    float height = Range(0.65f, 1.9f);
                    var leaf = Ball("Kelp blade", clump.transform, new Vector3((j - 1) * 0.2f, height * 0.45f, 0), new Vector3(0.16f, height, 0.32f), j == 1 ? leafLight : kelp);
                    leaf.transform.localRotation = Quaternion.Euler(Range(-14, 14), 0, (j - 1) * -19);
                }
                if (i % 3 == 0)
                {
                    var stem = Shape("Coral stem", PrimitiveType.Capsule, root, new Vector3(x + 0.65f, 0.42f, z), new Vector3(0.18f, 0.35f, 0.18f), i % 2 == 0 ? coral : lavender);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var branch = Shape("Coral branch", PrimitiveType.Capsule, stem.transform, new Vector3(side * 1.1f, 0.3f, 0), new Vector3(0.75f, 0.75f, 0.75f), i % 2 == 0 ? coral : lavender);
                        branch.transform.localRotation = Quaternion.Euler(0, 0, side * -37);
                    }
                }
            }
            for (int i = 0; i < 65; i++)
            {
                float size = Range(0.04f, 0.12f);
                Ball("Sand pebble", root, new Vector3(Range(-13.5f, 13.5f), 0.14f, Range(-8, 8)), new Vector3(size * 2, size, size), i % 3 == 0 ? white : rockTop);
            }
            for (int i = 0; i < 25; i++)
            {
                var ripple = Ring("Soft sand caustic", root, Range(0.35f, 0.8f), new Color(0.8f, 1f, 0.94f, 0.16f), 0.018f);
                ripple.transform.localPosition = new Vector3(Range(-13, 13), 0.108f, Range(-8, 8)); ripple.transform.localScale = new Vector3(1.8f, 1, 0.8f);
                ripple.transform.localRotation = Quaternion.Euler(0, Range(-60, 60), 0);
            }
            for (int i = 0; i < 32; i++)
            {
                float size = Range(0.045f, 0.11f);
                bubbles.Add(Ball("Air bubble", root, new Vector3(Range(-13, 13), Range(0.1f, 6.5f), Range(-7, 8)), Vector3.one * size, glass).transform);
            }
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
            float blend = fresh ? 1 : 1 - Mathf.Exp(-dt * 18);
            actor.root.transform.position = Vector3.Lerp(actor.root.transform.position, target, blend);
            var direction = new Vector3(facing.x, facing.y, facing.z);
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            var rot = Quaternion.LookRotation(direction, Mathf.Abs(direction.normalized.y) > 0.99f ? Vector3.forward : Vector3.up);
            if (dead) rot *= Quaternion.Euler(0, 0, 180);
            actor.root.transform.rotation = fresh ? rot : Quaternion.Slerp(actor.root.transform.rotation, rot, blend);
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
            visible.Clear();
            foreach (var f in state.fish) if (!f.eaten) Place("f" + f.id, "fish" + f.species, f.position, f.facing, dt, state.time, f.dead);
            Swimmer local = null;
            foreach (var p in state.players)
            {
                if (p.id == localId) local = p;
                bool showAlien = state.phase == RoundPhase.Finished && p.team == Team.Hider;
                if ((!p.alive || p.swallowedBy != 0) && !showAlien) continue;
                string kind = p.team == Team.Seeker ? "diver" : (showAlien || p.species < 0 || p.revealUntil > state.time ? "alien" : "fish" + p.species);
                var actor = Place("p" + p.id, kind, p.position, p.facing, dt, state.time);
                bool feeding = state.phase == RoundPhase.Playing && p.team == Team.Hider && p.stunnedUntil > state.time;
                if (feeding && actor.predatorMouth == null) PredatorMouth(actor, kind == "alien" ? new Vector3(0, -0.25f, 0.48f) : new Vector3(0, -0.02f, 0.56f));
                if (actor.predatorMouth != null)
                {
                    actor.predatorMouth.SetActive(feeding);
                    actor.predatorMouth.transform.localScale = new Vector3(1, 0.9f + Mathf.Sin(state.time * 9) * 0.1f, 1);
                }
                if (actor.normalMouth != null) actor.normalMouth.SetActive(!feeding);
            }
            foreach (var h in state.harpoons) Place("h" + h.id, "harpoon", h.position, h.direction, dt, state.time);
            if (state.eggs != null) foreach (var egg in state.eggs)
            {
                var actor = Place("e" + egg.id, "egg", egg.position, new Point(0, 1), dt, state.time);
                float urgency = Mathf.Clamp01(1 - (egg.hatchAt - state.time) / AquariumModel.HatchSeconds);
                float pulse = Mathf.Sin(state.time * (3 + urgency * 5) + egg.id) * (0.025f + urgency * 0.025f);
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
                float age = Mathf.Clamp01((state.time - e.born) / Mathf.Max(0.01f, e.duration));
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
            for (int i = 0; i < bubbles.Count; i++)
            { var pos = bubbles[i].localPosition; pos.y += dt * (0.35f + i % 4 * 0.13f); if (pos.y > 6.8f) pos.y = 0.2f; bubbles[i].localPosition = pos; }
            for (int i = 0; i < plants.Count; i++) plants[i].localRotation = Quaternion.Euler(Mathf.Sin(state.time * 0.8f + i) * 3, 0, Mathf.Sin(state.time + i * 1.7f) * 5);
        }

        public Vector3 PlayerPosition(Swimmer player)
        {
            Actor actor;
            return actors.TryGetValue("p" + player.id, out actor) ? actor.root.transform.position : new Vector3(player.position.x, player.position.y, player.position.z);
        }
        public void CameraOcclusion(int targetId, Vector3? target, bool hideTarget = false)
        {
            if (hiddenByCamera != null && hiddenByCamera.root != null)
                foreach (var renderer in hiddenByCamera.root.GetComponentsInChildren<Renderer>()) renderer.enabled = true;
            hiddenByCamera = null;
            foreach (var panel in glassPanels)
            {
                panel.enabled = true;
                if (!target.HasValue) continue;
                var delta = target.Value - Camera.transform.position; float hit;
                if (panel.bounds.IntersectRay(new Ray(Camera.transform.position, delta.normalized), out hit) && hit < delta.magnitude)
                    panel.enabled = false;
            }
            Actor actor;
            if (target.HasValue && (hideTarget || Vector3.Distance(Camera.transform.position, target.Value) < 0.9f) && actors.TryGetValue("p" + targetId, out actor))
            {
                hiddenByCamera = actor;
                foreach (var renderer in actor.root.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            }
        }
        private void DestroyActor(Actor actor)
        { UnityEngine.Object.Destroy(actor.root); }
        public void Dispose()
        {
            foreach (var actor in actors.Values) DestroyActor(actor);
            foreach (var mat in materials) UnityEngine.Object.Destroy(mat);
            UnityEngine.Object.Destroy(fanMesh); UnityEngine.Object.Destroy(coneMesh);
            UnityEngine.Object.Destroy(root.gameObject);
        }
    }
}

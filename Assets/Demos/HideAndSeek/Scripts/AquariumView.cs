using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // Primitive art is deliberately separate from rules so an artist can replace it later.
    public sealed class AquariumView : IDisposable
    {
        private sealed class Actor { public GameObject root; public Transform tail; public string kind; }
        private readonly Transform root;
        private readonly Dictionary<string, Actor> actors = new Dictionary<string, Actor>();
        private readonly Dictionary<int, GameObject> effects = new Dictionary<int, GameObject>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Transform> bubbles = new List<Transform>();
        private readonly HashSet<string> visible = new HashSet<string>();
        private readonly Color[] colors = { new Color(1f, 0.64f, 0.19f), new Color(0.17f, 0.81f, 0.88f), new Color(1f, 0.43f, 0.57f) };
        private readonly Material sand, dark, teal, white, black, purple, steel, glass;
        private readonly Material[] fishMaterials, finMaterials;
        private readonly Material lineMaterial;
        private readonly LineRenderer localRing;
        private readonly System.Random random = new System.Random(54);
        public Camera Camera { get; private set; }

        public AquariumView(Transform parent)
        {
            root = new GameObject("Aquarium visuals").transform; root.SetParent(parent);
            sand = Material(new Color(0.54f, 0.75f, 0.68f));
            dark = Material(new Color(0.024f, 0.09f, 0.15f)); teal = Material(new Color(0.03f, 0.46f, 0.51f));
            white = Material(new Color(0.93f, 1f, 1f)); black = Material(new Color(0.015f, 0.035f, 0.075f));
            purple = Material(new Color(0.65f, 0.32f, 0.92f)); steel = Material(new Color(0.66f, 0.8f, 0.89f));
            glass = Material(new Color(0.19f, 0.8f, 0.9f, 0.12f), true);
            lineMaterial = new Material(Shader.Find("Sprites/Default")); materials.Add(lineMaterial);
            fishMaterials = new Material[3]; finMaterials = new Material[3];
            for (int i = 0; i < 3; i++) { fishMaterials[i] = Material(colors[i]); finMaterials[i] = Material(Color.Lerp(colors[i], Color.white, 0.32f)); }
            BuildEnvironment();
            localRing = Ring("Your swimmer", root, 0.7f, new Color(0.64f, 1, 0.54f), 0.045f);
            Camera = UnityEngine.Camera.main;
            if (Camera == null) { var go = new GameObject("Aquarium Camera"); go.tag = "MainCamera"; Camera = go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); }
            Camera.transform.position = new Vector3(0, 23, -20);
            Camera.transform.rotation = Quaternion.Euler(49, 0, 0);
            Camera.orthographic = true; Camera.orthographicSize = 12.7f; Camera.nearClipPlane = 0.1f; Camera.farClipPlane = 100;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(0.025f, 0.065f, 0.105f);
            Camera.allowHDR = false;
            RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.61f, 0.76f, 0.83f);
            var sun = UnityEngine.Object.FindFirstObjectByType<Light>();
            if (sun != null) { sun.transform.rotation = Quaternion.Euler(55, -25, 0); sun.color = new Color(0.81f, 0.96f, 1); sun.intensity = 1.1f; sun.shadows = LightShadows.Soft; }
        }

        private Material Material(Color color, bool transparent = false)
        {
            var mat = new Material(Shader.Find("Standard")); mat.color = color; mat.SetFloat("_Glossiness", 0.3f);
            if (transparent)
            {
                mat.SetFloat("_Mode", 3); mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0); mat.EnableKeyword("_ALPHABLEND_ON"); mat.renderQueue = 3000;
            }
            materials.Add(mat); return mat;
        }
        private GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 at, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = at; go.transform.localScale = size;
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
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
            Box("Back glass", root, new Vector3(0, 2.4f, 8.65f), new Vector3(28.6f, 5, 0.12f), glass);
            Box("Left glass", root, new Vector3(-14.2f, 2, 0), new Vector3(0.1f, 4, 17.4f), glass);
            Box("Right glass", root, new Vector3(14.2f, 2, 0), new Vector3(0.1f, 4, 17.4f), glass);
            Box("Front glass", root, new Vector3(0, 0.4f, -8.6f), new Vector3(28.6f, 0.8f, 0.1f), glass);
            var glow = Material(new Color(0.16f, 0.94f, 0.93f)); glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(0.03f, 0.45f, 0.5f));
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Rim light", root, new Vector3(0, 0.21f, side * 8.7f), new Vector3(28.9f, 0.09f, 0.12f), glow);
                Box("Rim light", root, new Vector3(side * 14.25f, 0.21f, 0), new Vector3(0.12f, 0.09f, 17.5f), glow);
            }
            for (int i = 0; i < 8; i++)
            {
                var rail = Box("Aquarium pillar", root, new Vector3(-14 + i * 4, 2.5f, 8.8f), new Vector3(0.08f, 5, 0.12f), teal);
                rail.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            var rock = Material(new Color(0.2f, 0.42f, 0.46f));
            for (int i = 0; i < AquariumModel.Rocks.Length; i++)
            {
                var at = AquariumModel.Rocks[i]; float radius = AquariumModel.RockRadii[i];
                var b = Ball("Harpoon cover rock", root, new Vector3(at.x, 0.9f, at.z), new Vector3(radius * 2, 2.8f, radius * 2), rock);
                b.transform.localRotation = Quaternion.Euler(0, i * 62, 9);
                Ball("Rock shoulder", root, new Vector3(at.x + 0.3f, 0.4f, at.z - 0.3f), new Vector3(radius * 1.7f, 1.4f, radius * 1.7f), rock);
            }
            var kelp = Material(new Color(0.08f, 0.55f, 0.39f)); var coral = Material(new Color(0.98f, 0.43f, 0.48f));
            for (int i = 0; i < 24; i++)
            {
                float x = Range(-13, 13), z = i < 14 ? Range(6.7f, 8) : Range(-8, -7);
                for (int j = 0; j < 3; j++)
                {
                    float height = Range(0.7f, 2.2f);
                    var leaf = Ball("Sea grass", root, new Vector3(x + j * 0.24f, height * 0.45f, z), new Vector3(0.16f, height, 0.3f), i % 4 == 0 ? coral : kelp);
                    leaf.transform.localRotation = Quaternion.Euler(Range(-14, 14), 0, Range(-20, 20));
                }
            }
            for (int i = 0; i < 65; i++)
            {
                float size = Range(0.04f, 0.14f);
                Ball("Sand pebble", root, new Vector3(Range(-13.5f, 13.5f), 0.17f, Range(-8, 8)), new Vector3(size * 2, size, size), i % 3 == 0 ? white : rock);
            }
            for (int i = 0; i < 32; i++)
            {
                float size = Range(0.055f, 0.14f);
                bubbles.Add(Ball("Air bubble", root, new Vector3(Range(-13, 13), Range(0.1f, 5), Range(-7, 8)), Vector3.one * size, glass).transform);
            }
            WorldLabel("ABYSS / 07", new Vector3(-10, 0.04f, -10.3f), 0.55f, white);
            WorldLabel("A Q U A R I U M", new Vector3(6.2f, 0.04f, -10.3f), 0.32f, white);
        }
        private void WorldLabel(string text, Vector3 at, float size, Material unused)
        {
            var go = new GameObject(text); go.transform.SetParent(root); go.transform.localPosition = at; go.transform.rotation = Quaternion.Euler(90, 0, 0);
            var label = go.AddComponent<TextMesh>(); label.text = text; label.characterSize = size; label.fontSize = 48;
            label.anchor = TextAnchor.MiddleCenter; label.color = new Color(0.45f, 0.82f, 0.83f);
        }

        private Transform Fin(Transform parent, Vector3 at, Vector3 size, Material mat)
        {
            var go = new GameObject("Fan fin"); go.transform.SetParent(parent, false); go.transform.localPosition = at; go.transform.localScale = size;
            var mesh = new Mesh { name = "Aquarium fan" };
            mesh.vertices = new[] { new Vector3(0, 0, 0.2f), new Vector3(-0.45f, 0, -0.6f), new Vector3(0.45f, 0, -0.6f), new Vector3(0, 0.13f, -0.36f) };
            mesh.triangles = new[] { 0, 1, 3, 0, 3, 2, 1, 2, 3, 0, 2, 1 }; mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }
        private Actor MakeActor(string kind)
        {
            var actor = new Actor { root = new GameObject(kind), kind = kind }; actor.root.transform.SetParent(root);
            var t = actor.root.transform;
            if (kind.StartsWith("fish"))
            {
                int species = kind[kind.Length - 1] - '0';
                var mat = fishMaterials[species]; var fins = finMaterials[species];
                Ball("Fish body", t, Vector3.zero, new Vector3(0.55f + species * 0.08f, 0.55f, 1.05f), mat);
                Ball("Belly", t, new Vector3(0, -0.11f, 0.15f), new Vector3(0.43f, 0.37f, 0.73f), fins);
                actor.tail = Fin(t, new Vector3(0, 0, -0.45f), new Vector3(0.95f, 1, 0.8f), fins);
                Fin(t, new Vector3(-0.23f, 0, 0), new Vector3(0.65f, 1, 0.45f), fins);
                Fin(t, new Vector3(0.23f, 0, 0), new Vector3(0.65f, 1, 0.45f), fins);
                for (int side = -1; side <= 1; side += 2)
                {
                    Ball("Eye white", t, new Vector3(side * 0.22f, 0.16f, 0.32f), Vector3.one * 0.17f, white);
                    Ball("Pupil", t, new Vector3(side * 0.24f, 0.195f, 0.37f), Vector3.one * 0.082f, black);
                }
                Ball("Mouth", t, new Vector3(0, 0, 0.52f), new Vector3(0.12f, 0.10f, 0.04f), black);
            }
            else if (kind == "alien")
            {
                Ball("Alien jelly", t, Vector3.zero, new Vector3(0.85f, 0.68f, 0.85f), purple);
                for (int i = 0; i < 4; i++) Ball("Jelly foot", t, new Vector3(Mathf.Sin(i * Mathf.PI / 2) * 0.3f, -0.25f, Mathf.Cos(i * Mathf.PI / 2) * 0.3f), Vector3.one * 0.38f, purple);
                for (int side = -1; side <= 1; side += 2) Ball("Alien eye", t, new Vector3(side * 0.17f, 0.2f, 0.32f), new Vector3(0.09f, 0.15f, 0.07f), black);
            }
            else if (kind == "diver")
            {
                Ball("Diving suit", t, Vector3.zero, new Vector3(0.72f, 0.64f, 1.16f), dark);
                Ball("Helmet", t, new Vector3(0, 0.1f, 0.49f), Vector3.one * 0.65f, teal);
                Ball("Visor", t, new Vector3(0, 0.23f, 0.64f), new Vector3(0.51f, 0.28f, 0.32f), white);
                Box("Oxygen tank", t, new Vector3(0, 0.36f, -0.12f), new Vector3(0.37f, 0.29f, 0.65f), steel);
                for (int side = -1; side <= 1; side += 2)
                    Box("Flipper", t, new Vector3(side * 0.24f, -0.05f, -0.79f), new Vector3(0.26f, 0.13f, 0.55f), teal);
                Box("Harpoon grip", t, new Vector3(0.5f, 0, 0.46f), new Vector3(0.14f, 0.16f, 0.8f), dark);
                Box("Harpoon barrel", t, new Vector3(0.5f, 0, 0.93f), new Vector3(0.065f, 0.065f, 0.6f), steel);
            }
            else if (kind == "harpoon")
            {
                Box("Spear shaft", t, Vector3.zero, new Vector3(0.055f, 0.055f, 0.95f), steel);
                var tip = Fin(t, new Vector3(0, 0, 0.65f), new Vector3(0.35f, 1, 0.4f), white);
                tip.localRotation = Quaternion.Euler(0, 180, 0);
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
            Vector3 target = new Vector3(at.x, dead ? 0.5f : 1.6f + Mathf.Sin(time * 2.2f + at.x) * 0.055f, at.z);
            float blend = fresh ? 1 : 1 - Mathf.Exp(-dt * 18);
            actor.root.transform.position = Vector3.Lerp(actor.root.transform.position, target, blend);
            var rot = Quaternion.LookRotation(new Vector3(facing.x, 0, facing.z));
            if (dead) rot *= Quaternion.Euler(0, 0, 180);
            actor.root.transform.rotation = fresh ? rot : Quaternion.Slerp(actor.root.transform.rotation, rot, blend);
            if (actor.tail != null) actor.tail.localRotation = Quaternion.Euler(0, dead ? 0 : Mathf.Sin(time * 10 + at.z) * 20, 0);
            return actor;
        }
        private LineRenderer Ring(string name, Transform parent, float radius, Color color, float width)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = lineMaterial;
            line.useWorldSpace = false; line.loop = true; line.positionCount = 48; line.widthMultiplier = width;
            line.startColor = line.endColor = color;
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
                if (!p.alive || p.swallowedBy != 0) continue;
                string kind = p.team == Team.Seeker ? "diver" : (p.species < 0 || p.revealUntil > state.time ? "alien" : "fish" + p.species);
                Place("p" + p.id, kind, p.position, p.facing, dt, state.time);
            }
            foreach (var h in state.harpoons) Place("h" + h.id, "harpoon", h.position, h.direction, dt, state.time);
            var removed = new List<string>();
            foreach (var pair in actors) if (!visible.Contains(pair.Key)) { DestroyActor(pair.Value); removed.Add(pair.Key); }
            foreach (var key in removed) actors.Remove(key);
            localRing.gameObject.SetActive(local != null && local.alive && local.swallowedBy == 0);
            if (local != null) localRing.transform.position = new Vector3(local.position.x, 0.25f, local.position.z);
            var activeEffects = new HashSet<int>();
            foreach (var e in state.effects)
            {
                activeEffects.Add(e.id); GameObject fx;
                if (!effects.TryGetValue(e.id, out fx))
                {
                    bool burp = e.kind == "burp";
                    Color color = burp || e.kind == "morph" ? new Color(0.9f, 0.4f, 1) : e.kind == "caught" || e.kind == "miss" ? new Color(1, 0.4f, 0.3f) : new Color(0.55f, 1, 0.58f);
                    fx = Ring(e.kind, root, 1, color, burp ? 0.12f : 0.065f).gameObject;
                    if (burp) for (int i = 0; i < 8; i++)
                        Ball("Burp sparkle", fx.transform, new Vector3(Mathf.Sin(i * Mathf.PI / 4), 0.2f, Mathf.Cos(i * Mathf.PI / 4)), Vector3.one * 0.12f, i % 2 == 0 ? purple : white);
                    effects[e.id] = fx;
                }
                float age = Mathf.Clamp01((state.time - e.born) / e.duration);
                fx.transform.position = new Vector3(e.position.x, 1.7f + age * 0.3f, e.position.z);
                fx.transform.localScale = Vector3.one * (0.15f + age * (e.kind == "burp" ? 2.7f : 1.3f));
                var line = fx.GetComponent<LineRenderer>(); var c = line.startColor; c.a = 1 - age; line.startColor = line.endColor = c;
            }
            var expired = new List<int>();
            foreach (var e in effects) if (!activeEffects.Contains(e.Key)) { UnityEngine.Object.Destroy(e.Value); expired.Add(e.Key); }
            foreach (int id in expired) effects.Remove(id);
            for (int i = 0; i < bubbles.Count; i++)
            { var pos = bubbles[i].localPosition; pos.y += dt * (0.35f + i % 4 * 0.13f); if (pos.y > 5) pos.y = 0.2f; bubbles[i].localPosition = pos; }
            Camera.orthographicSize = Mathf.Max(12.7f, 19f / Camera.aspect);
        }

        public Point Aim(Vector3 mouse, Point fallback)
        {
            var ray = Camera.ScreenPointToRay(mouse); float distance;
            return new Plane(Vector3.up, new Vector3(0, 1.6f, 0)).Raycast(ray, out distance) ? new Point(ray.GetPoint(distance).x, ray.GetPoint(distance).z) : fallback;
        }
        private void DestroyActor(Actor actor)
        {
            foreach (var filter in actor.root.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh != null && filter.sharedMesh.name == "Aquarium fan") UnityEngine.Object.Destroy(filter.sharedMesh);
            UnityEngine.Object.Destroy(actor.root);
        }
        public void Dispose()
        {
            foreach (var actor in actors.Values) DestroyActor(actor);
            foreach (var mat in materials) UnityEngine.Object.Destroy(mat);
            UnityEngine.Object.Destroy(root.gameObject);
        }
    }
}

using System;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // Lobby character select (2026-10-28): every playable alien stands in a row on a small stage far below
    // the tank, drawn by its own camera into a texture that the lobby GUI shows. The aliens idle with the
    // game's own idle and blinks. The picked one steps forward and hops (anticipation squash, stretch on
    // take-off, an arc, a landing squash, a damped settle) to face the viewer; the others glance at it.
    public sealed class AquariumAlienPreview : IDisposable
    {
        public const int Layer = 29;
        public const float Aspect = 2.4f;
        private const float Unselected = .84f, DiverScale = .8f;
        // R47: the same stage shows the three divers when the local player dives.
        private readonly bool divers;
        private readonly float spacing;
        private readonly Color[] colors;
        private static readonly Vector3 StageOrigin = new Vector3(0, -400, 0);
        private readonly GameObject stage;
        private readonly Camera camera;
        private readonly RenderTexture texture;
        private readonly Transform[] holders;
        private readonly AquariumRig[] rigs;
        private readonly Renderer[] discs;
        private readonly MaterialPropertyBlock discColor = new MaterialPropertyBlock();
        private readonly float[] size, yaw, step;
        private int selected = -1;
        private float selectedAt = -10, lastUpdate = -1;

        public Texture Texture { get { return texture; } }
        public int Slots { get { return holders.Length; } }

        public AquariumAlienPreview() : this(false) { }
        public AquariumAlienPreview(bool divers)
        {
            this.divers = divers; spacing = divers ? 2.2f : 1.55f;
            stage = new GameObject(divers ? "Diver lobby stage" : "Alien lobby stage"); stage.transform.position = StageOrigin + (divers ? new Vector3(60, 0, 0) : Vector3.zero);
            int n = divers ? AquariumDiverKinds.Count : AquariumAlienKinds.LobbyOrder.Length;
            holders = new Transform[n]; rigs = new AquariumRig[n]; discs = new Renderer[n]; colors = new Color[n];
            size = new float[n]; yaw = new float[n]; step = new float[n];
            var discMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Lobby stage glow", mainTexture = GlowTexture() };
            var quad = DiscMesh();
            for (int slot = 0; slot < n; slot++)
            {
                string key, prefabPath;
                if (divers) { var diver = AquariumDiverKinds.Get(slot); key = diver.key; prefabPath = "AquariumEquipment/Diver" + diver.model; colors[slot] = new Color(diver.r, diver.g, diver.b); }
                else { var alien = AquariumAlienKinds.Get(AquariumAlienKinds.LobbyOrder[slot]); key = alien.key; prefabPath = alien.prefab; colors[slot] = new Color(alien.r, alien.g, alien.b); }
                var holder = new GameObject(key).transform; holder.SetParent(stage.transform, false); holder.localPosition = SlotPosition(slot, 0);
                holders[slot] = holder; size[slot] = Unselected;
                var prefab = Resources.Load<GameObject>(prefabPath);
                if (prefab != null) { var go = UnityEngine.Object.Instantiate(prefab, holder); go.name = key; rigs[slot] = go.GetComponent<AquariumRig>(); if (divers) { go.transform.localScale = Vector3.one * DiverScale; go.transform.localPosition = new Vector3(0, .22f, 0); } }
                // R55: the divers stand on the pick stage with their own (recoloured) harpoon gun in hand, as in the game.
                if (divers && rigs[slot] != null)
                {
                    var gun = AquariumFirstPersonEquipment.CreateGun(rigs[slot].transform, rigs[slot]);
                    AquariumDiverKinds.PaintGun(gun, slot);
                }
                var disc = new GameObject("Glow").AddComponent<MeshFilter>(); disc.sharedMesh = quad; disc.transform.SetParent(stage.transform, false);
                disc.transform.localPosition = SlotPosition(slot, 0) + new Vector3(0, -.47f, 0);
                var r = disc.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = discMaterial; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                discs[slot] = r;
            }
            texture = new RenderTexture(1200, Mathf.RoundToInt(1200 / Aspect), 24) { name = "Alien lobby preview", antiAliasing = 4 };
            camera = new GameObject("Alien lobby camera").AddComponent<Camera>(); camera.transform.SetParent(stage.transform, false);
            camera.transform.localPosition = new Vector3(0, 1.05f, 8.9f); camera.transform.LookAt(stage.transform.TransformPoint(new Vector3(0, .42f, 0)));
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .15f, .21f);
            camera.cullingMask = 1 << Layer; camera.fieldOfView = 24; camera.nearClipPlane = .1f; camera.farClipPlane = 30;
            camera.targetTexture = texture; camera.depth = -20; camera.allowHDR = false;
            // Kept soft (the tank's own lights reach the stage too) so the white bodies keep their coloured spots.
            AddLight("Key", new Vector3(30, 200, 0), .62f, new Color(1, .97f, .92f));
            AddLight("Fill", new Vector3(12, 150, 0), .18f, new Color(.72f, .86f, 1));
            AddLight("Rim", new Vector3(18, 10, 0), .32f, new Color(.6f, .95f, 1));
            foreach (var t in stage.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            stage.SetActive(false);
        }

        private void AddLight(string name, Vector3 euler, float intensity, Color color)
        {
            var light = new GameObject(name + " light").AddComponent<Light>(); light.transform.SetParent(stage.transform, false);
            light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(euler); light.intensity = intensity; light.color = color;
            light.cullingMask = 1 << Layer; light.shadows = LightShadows.None;
        }

        // Slot 0 is drawn on the left (the stage camera looks down -z, so +x is screen left).
        private Vector3 SlotPosition(int slot, float forward)
        { return new Vector3(((holders.Length - 1) * .5f - slot) * spacing, 0, forward); }

        // Screen x (0..1 across the texture) to the slot drawn there.
        public int SlotAt(float u)
        {
            int best = -1; float nearest = float.MaxValue;
            for (int slot = 0; slot < holders.Length; slot++)
            {
                var p = camera.WorldToViewportPoint(stage.transform.TransformPoint(SlotPosition(slot, 0) + new Vector3(0, .3f, 0)));
                float d = Mathf.Abs(p.x - u); if (d < nearest) { nearest = d; best = slot; }
            }
            return nearest < .5f / holders.Length + .02f ? best : -1;
        }

        public void Show(bool visible) { if (stage.activeSelf != visible) stage.SetActive(visible); }

        public void Select(int kind, float now)
        {
            int slot = divers ? AquariumDiverKinds.Clamp(kind) : Array.IndexOf(AquariumAlienKinds.LobbyOrder, AquariumAlienKinds.Clamp(kind));
            if (slot == selected) return;
            selected = slot; selectedAt = now;
        }

        public void Update(float now)
        {
            float dt = lastUpdate < 0 ? 0 : Mathf.Clamp(now - lastUpdate, 0, .1f); lastUpdate = now;
            for (int slot = 0; slot < holders.Length; slot++)
            {
                bool picked = slot == selected;
                // Slow in / slow out toward the pose: the pick steps forward and grows, the others glance at it.
                float ease = 1 - Mathf.Exp(-dt * 7);
                size[slot] = Mathf.Lerp(size[slot], picked ? 1 : Unselected, ease);
                step[slot] = Mathf.Lerp(step[slot], picked ? .55f : 0, ease);
                float look = selected < 0 || picked ? 0 : slot < selected ? -22 : 22;
                // a slow glance, so the antennas' spring bones sway instead of flopping
                yaw[slot] = Mathf.Lerp(yaw[slot], look, 1 - Mathf.Exp(-dt * 2.5f));
                float lift = 0, stretch = 1, pitch = 0;
                if (picked) Hop(now - selectedAt, out lift, out stretch, out pitch);
                float across = 1 / Mathf.Sqrt(stretch);   // keep the volume
                var holder = holders[slot];
                holder.localPosition = SlotPosition(slot, step[slot]) + new Vector3(0, lift, 0);
                holder.localRotation = Quaternion.Euler(pitch, yaw[slot], 0);
                holder.localScale = new Vector3(across, stretch, across) * size[slot];
                if (rigs[slot] != null) rigs[slot].SampleLocomotion(0, now, slot * .37f);
                var tint = colors[slot];
                discs[slot].transform.localPosition = SlotPosition(slot, step[slot]) + new Vector3(0, -.47f * size[slot], 0);
                float glow = picked ? .55f + .1f * Mathf.Sin(now * 2.4f) : .16f;
                discColor.SetColor("_Color", new Color(tint.r, tint.g, tint.b, glow)); discs[slot].SetPropertyBlock(discColor);
                discs[slot].transform.localScale = Vector3.one * (picked ? 1.25f : .95f) * size[slot] * (1 - .35f * Mathf.Clamp01(lift / .3f));
            }
        }

        // Selection hop, 30 fps frames in brackets: anticipation squash (4), take-off stretch (2-3), an arc up
        // and down (8), a landing squash (2), a damped settle with one overshoot (10). The arms and antenna lag
        // behind on their spring bones (follow-through).
        private static void Hop(float t, out float lift, out float stretch, out float pitch)
        {
            lift = 0; stretch = 1; pitch = 0;
            if (t < 0 || t > 1.1f) return;
            // Height: a dip, then a ballistic arc (lands with speed: the squash sells the impact).
            if (t < .14f) lift = -.035f * Smooth(t / .14f);
            else if (t < .50f) { float u = (t - .14f) / .36f; lift = -.035f * (1 - u) + 4 * .30f * u * (1 - u); }
            // Squash and stretch keys, eased between.
            stretch = Keys(t, new[] { 0f, .14f, .22f, .36f, .47f, .52f, .68f, .86f, 1.1f }, new[] { 1f, .84f, 1.16f, 1f, 1.06f, .86f, 1.05f, .99f, 1f });
            // A small lean into the take-off and back on landing (the hop draws an arc, not a straight lift).
            pitch = Keys(t, new[] { 0f, .14f, .24f, .40f, .52f, .70f, .9f }, new[] { 0f, 6f, -7f, -2f, 5f, -1.5f, 0f });
        }
        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
        private static float Keys(float t, float[] at, float[] value)
        {
            if (t <= at[0]) return value[0];
            for (int i = 1; i < at.Length; i++)
                if (t <= at[i]) return Mathf.Lerp(value[i - 1], value[i], Smooth((t - at[i - 1]) / (at[i] - at[i - 1])));
            return value[value.Length - 1];
        }

        private static Texture2D GlowTexture()
        {
            const int n = 64; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Lobby glow" };
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + .5f) / n * 2 - 1, dy = (y + .5f) / n * 2 - 1, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1 - r); pixels[y * n + x] = new Color(1, 1, 1, a * a * (3 - 2 * a));
                }
            t.SetPixels(pixels); t.Apply(); return t;
        }
        // A flat ellipse on the stage floor under each alien.
        private static Mesh DiscMesh()
        {
            var m = new Mesh { name = "Lobby glow disc" };
            m.vertices = new[] { new Vector3(-.75f, 0, -.42f), new Vector3(.75f, 0, -.42f), new Vector3(.75f, 0, .42f), new Vector3(-.75f, 0, .42f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 }; m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        public void Dispose()
        {
            if (camera != null) camera.targetTexture = null;
            if (texture != null) { texture.Release(); UnityEngine.Object.Destroy(texture); }
            if (stage != null) UnityEngine.Object.Destroy(stage);
        }
    }
}

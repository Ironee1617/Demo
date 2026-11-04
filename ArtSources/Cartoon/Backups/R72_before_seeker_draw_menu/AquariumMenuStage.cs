using System;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R71: the main menu's living background. The same art-directed reef as the character select
    // (AquariumEnvironment.LobbyBackdrop) filled to the whole screen, with the five aliens fleeing round a wide
    // figure-of-eight and the three divers chasing them (two chases, crossing in the middle of the sand). A warm light
    // from the surface follows the leading alien. Drawn by its own full-screen camera on the stages' layer; the tank's
    // camera is switched off while the menu is up (AquariumGame.UpdateMenuStage).
    public sealed class AquariumMenuStage : IDisposable
    {
        private const int Layer = AquariumAlienPreview.Layer;
        private static readonly Vector3 Origin = new Vector3(0, -400, -220);
        private readonly GameObject stage;
        private readonly Camera camera;
        private readonly AquariumLobbyBackdrop backdrop;
        private sealed class Runner { public Transform holder; public AquariumRig rig; public bool diver; public float offset, lateral, bob, seed; public Vector3 last; public float speed; }
        private readonly Runner[] runners;
        private float lastUpdate = -1;
        public Camera Camera { get { return camera; } }

        public AquariumMenuStage()
        {
            stage = new GameObject("Main menu stage"); stage.transform.position = Origin;
            backdrop = AquariumEnvironment.LobbyBackdrop(stage.transform, 71, Layer);
            // aliens: two fleeing groups; divers: one behind the first group, two behind the second
            var list = new System.Collections.Generic.List<Runner>();
            var order = AquariumAlienKinds.LobbyOrder;
            float[] alienOffset = { 0, .26f, .5f, Mathf.PI + .05f, Mathf.PI + .32f };
            float[] alienSide = { 0, -.55f, .45f, .3f, -.4f };
            for (int i = 0; i < order.Length && i < alienOffset.Length; i++)
            {
                var kind = AquariumAlienKinds.Get(order[i]);
                list.Add(Spawn(kind.prefab, kind.key, false, alienOffset[i], alienSide[i], i));
            }
            float[] diverOffset = { -.62f, Mathf.PI - .6f, Mathf.PI - .98f };
            float[] diverSide = { .1f, -.25f, .35f };
            for (int d = 0; d < AquariumDiverKinds.Count && d < diverOffset.Length; d++)
            {
                var kind = AquariumDiverKinds.Get(d);
                var r = Spawn("AquariumEquipment/Diver" + kind.model, kind.key, true, diverOffset[d], diverSide[d], 10 + d);
                if (r.rig != null) { var gun = AquariumFirstPersonEquipment.CreateGun(r.rig.transform, r.rig); AquariumDiverKinds.PaintGun(gun, d); }
                list.Add(r);
            }
            runners = list.ToArray();

            camera = new GameObject("Main menu camera").AddComponent<Camera>(); camera.transform.SetParent(stage.transform, false);
            camera.transform.localPosition = new Vector3(0, 1.15f, 7.8f); camera.transform.LookAt(stage.transform.TransformPoint(new Vector3(.6f, .5f, -1.4f)));
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.04f, .14f, .2f);
            camera.cullingMask = 1 << Layer; camera.fieldOfView = 40; camera.nearClipPlane = .1f; camera.farClipPlane = 60;
            camera.depth = 40; camera.allowHDR = false; camera.allowMSAA = true;
            AddLight("Key", new Vector3(30, 200, 0), .66f, new Color(1, .97f, .92f));
            AddLight("Fill", new Vector3(12, 150, 0), .2f, new Color(.72f, .86f, 1));
            AddLight("Rim", new Vector3(18, 10, 0), .5f, new Color(.55f, .95f, 1));
            foreach (var t in stage.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            stage.SetActive(false);
        }

        private Runner Spawn(string prefabPath, string key, bool diver, float offset, float lateral, int seed)
        {
            var holder = new GameObject(key).transform; holder.SetParent(stage.transform, false);
            var r = new Runner { holder = holder, diver = diver, offset = offset, lateral = lateral, bob = seed * 1.37f, seed = seed * .37f };
            var prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab != null)
            {
                var go = UnityEngine.Object.Instantiate(prefab, holder); go.name = key; r.rig = go.GetComponent<AquariumRig>();
                go.transform.localScale = Vector3.one * (diver ? .8f : .95f);
            }
            return r;
        }

        private void AddLight(string name, Vector3 euler, float intensity, Color color)
        {
            var light = new GameObject(name + " light").AddComponent<Light>(); light.transform.SetParent(stage.transform, false);
            light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(euler); light.intensity = intensity; light.color = color;
            light.cullingMask = 1 << Layer; light.shadows = LightShadows.None;
        }

        public bool Shown { get { return stage != null && stage.activeSelf; } }
        public void Show(bool visible) { if (stage != null && stage.activeSelf != visible) { stage.SetActive(visible); lastUpdate = -1; } }

        // The chase loop (stage space): a wide figure-of-eight over the open sand, rising and falling a little.
        private static Vector3 Path(float s, float bob)
        {
            return new Vector3(1.2f + 4.4f * Mathf.Sin(s), .4f + .36f * Mathf.Sin(1.7f * s + bob), -1.2f + 1.5f * Mathf.Sin(2 * s));
        }

        public void Update(float now)
        {
            if (!Shown) return;
            float dt = lastUpdate < 0 ? 0 : Mathf.Clamp(now - lastUpdate, 0, .1f); lastUpdate = now;
            // the chase surges a little: a short burst every few seconds
            float s0 = now * .34f + .22f * Mathf.Sin(now * .45f);
            Vector3 lead = Vector3.zero;
            foreach (var r in runners)
            {
                float s = s0 + r.offset;
                var on = Path(s, r.bob); var ahead = Path(s + .02f, r.bob);
                var tangent = (ahead - on).normalized;
                var side = Vector3.Cross(Vector3.up, new Vector3(tangent.x, 0, tangent.z)).normalized;
                // aliens weave from side to side as they flee; divers hold their line
                float weave = r.diver ? 0 : .35f * Mathf.Sin(now * 2.3f + r.seed * 5);
                var target = on + side * (r.lateral + weave);
                if (r.last == Vector3.zero || dt == 0) r.last = target;
                var velocity = dt > 0 ? (target - r.last) / dt : Vector3.zero; r.last = target;
                r.speed = Mathf.Lerp(r.speed, velocity.magnitude, dt > 0 ? 1 - Mathf.Exp(-dt * 6) : 1);
                r.holder.localPosition = target;
                var heading = velocity.sqrMagnitude > .0001f ? velocity : tangent;
                var face = new Vector3(heading.x, Mathf.Clamp(heading.y, -.35f, .35f) * .5f, heading.z);
                if (face.sqrMagnitude > .0001f)
                {
                    var want = Quaternion.LookRotation(face.normalized);
                    r.holder.localRotation = dt == 0 ? want : Quaternion.Slerp(r.holder.localRotation, want, 1 - Mathf.Exp(-dt * 7));
                }
                if (r.rig != null)
                {
                    if (r.diver) r.rig.SampleLocomotion(r.speed, now, r.seed, new Vector3(0, 0, r.speed));
                    else r.rig.SampleLocomotion(r.speed * 1.4f, now, r.seed);
                }
                if (!r.diver && Mathf.Abs(r.offset) < .01f) lead = target;
            }
            if (backdrop != null) backdrop.Focus(new Vector3(lead.x, 0, lead.z), .75f, dt > 0 ? dt : 1);
        }

        public void Dispose() { if (stage != null) UnityEngine.Object.Destroy(stage); }
    }
}

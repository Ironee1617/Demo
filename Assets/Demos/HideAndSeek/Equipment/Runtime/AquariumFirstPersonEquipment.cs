using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // The separate depth pass keeps the held weapon readable beside aquarium glass.
    // Both cameras share projection, so its physical muzzle lines up with the world tether.
    public sealed class AquariumFirstPersonEquipment : IDisposable
    {
        public const int Layer = 30;
        public static readonly Vector3 Grip = new Vector3(.34f, -.20f, .60f);
        private readonly Camera world, overlay;
        private readonly Transform pivot;
        private readonly AquariumRig hands;
        public readonly AquariumHarpoonGun gun;
        private readonly int originalMask;
        public bool Visible { get; private set; }
        public int Owner { get; private set; }

        public AquariumFirstPersonEquipment(Camera camera)
        {
            world = camera; originalMask = camera.cullingMask; camera.cullingMask &= ~(1 << Layer);
            pivot = new GameObject("First person diver equipment").transform;
            pivot.SetParent(camera.transform, false);
            hands = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("AquariumEquipment/DiverHands"), pivot).GetComponent<AquariumRig>();
            gun = CreateGun(pivot, hands);
            foreach (var t in pivot.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            foreach (var r in pivot.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            var light = new GameObject("Equipment soft fill").AddComponent<Light>(); light.transform.SetParent(pivot, false);
            light.type = LightType.Point; light.transform.localPosition = new Vector3(-.5f, .7f, .1f);
            light.color = new Color(.72f, .92f, 1); light.intensity = 1.4f; light.range = 3; light.cullingMask = 1 << Layer;
            overlay = new GameObject("First person equipment camera").AddComponent<Camera>(); overlay.transform.SetParent(camera.transform, false);
            overlay.clearFlags = CameraClearFlags.Depth; overlay.cullingMask = 1 << Layer;
            overlay.nearClipPlane = .015f; overlay.farClipPlane = 3; overlay.depth = camera.depth + .5f;
            overlay.allowHDR = camera.allowHDR; overlay.allowMSAA = camera.allowMSAA;
            pivot.gameObject.SetActive(false); overlay.enabled = false;
        }

        public static AquariumHarpoonGun CreateGun(Transform parent, AquariumRig rig = null)
        {
            string asset = "HarpoonGun";
            var result = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("AquariumEquipment/" + asset), rig != null && rig.gunSocket != null ? rig.gunSocket : parent).GetComponent<AquariumHarpoonGun>();
            result.transform.localPosition = rig != null && rig.gunSocket != null ? Vector3.zero : Grip; result.transform.localRotation = Quaternion.identity; return result;
        }

        public void Present(AquariumSnapshot state, int followId, bool firstPerson, float speed)
        {
            var player = state == null ? null : Array.Find(state.players, p => p.id == followId);
            Visible = firstPerson && state != null && state.phase == RoundPhase.Playing && player != null && player.alive && player.team == Team.Seeker;
            Owner = Visible ? followId : 0; pivot.gameObject.SetActive(Visible); overlay.enabled = Visible;
            if (!Visible) return;
            overlay.fieldOfView = world.fieldOfView; overlay.aspect = world.aspect; overlay.rect = world.rect;
            overlay.targetTexture = world.targetTexture;
            float time = state.time, firedAt = player.nextAction - AquariumModel.HarpoonCooldown;
            float age = time - firedAt;
            float recoil = age >= 0 && age < .5f ? Mathf.Sin(Mathf.Clamp01(age / .06f) * Mathf.PI * .5f) * Mathf.Exp(-age * 12) : 0;
            float movement = Mathf.Clamp01(speed / 4.7f);
            float bob = Mathf.Sin(time * 5) * .004f * movement;
            float recovering = age > .3f && time < player.nextAction ? Mathf.Sin(Mathf.PI * Mathf.InverseLerp(.3f, AquariumModel.HarpoonCooldown, age)) : 0;
            // Place the original character wrist below the viewport's lower edge.
            pivot.localPosition = new Vector3(.065f + bob, -.20f - recovering * .025f + Mathf.Cos(time * 3.2f) * .002f, .18f - recoil * .07f);
            pivot.localRotation = Quaternion.Euler(-recoil * 7 + recovering * 2, bob * 70, -recovering * 2);
            pivot.localScale = Vector3.one * .72f;
            hands.SamplePose(hands.cartoon ? "FPSIdle" : "Idle", time, time);
            gun.SetLoaded(time >= player.nextAction);
        }

        public void Dispose()
        {
            if (world != null) world.cullingMask = originalMask;
            if (pivot != null) Destroy(pivot.gameObject); if (overlay != null) Destroy(overlay.gameObject);
        }
        static void Destroy(UnityEngine.Object o) { if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o); }
    }
}

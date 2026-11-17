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
        private readonly AquariumCatchPose receiver;
        public Transform ReceivingHand => receiver.Visible ? receiver.Hand : null;
        // R31: the palm middle of the free hand (where a caught fish is held), even while it is out of view.
        public Vector3 ReceivingGrip => receiver.Grip;
        public readonly AquariumHarpoonGun gun;
        private readonly int originalMask;
        public bool Visible { get; private set; }
        public int Owner { get; private set; }
        // R53: the hands and gun wear the followed diver's colours.
        private int paintedKind;
        private void Paint(int kind)
        {
            if (kind == paintedKind) return;
            paintedKind = kind;
            var m = AquariumDiverKinds.HandsMaterial(kind);
            if (m != null && hands != null && hands.skin != null) hands.skin.sharedMaterial = m;
            if (m != null) receiver.SetMaterial(m);
            AquariumDiverKinds.PaintGun(gun, kind);
        }

        public AquariumFirstPersonEquipment(Camera camera)
        {
            world = camera; originalMask = camera.cullingMask; camera.cullingMask &= ~(1 << Layer);
            pivot = new GameObject("First person diver equipment").transform;
            pivot.SetParent(camera.transform, false);
            hands = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("AquariumEquipment/DiverHands"), pivot).GetComponent<AquariumRig>();
            gun = CreateGun(pivot, hands);
            receiver = new AquariumCatchPose(pivot);
            foreach (var t in pivot.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            foreach (var r in pivot.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            var light = new GameObject("Equipment soft fill").AddComponent<Light>(); light.transform.SetParent(pivot, false);
            light.type = LightType.Point; light.transform.localPosition = new Vector3(-.5f, .7f, .1f);
            light.color = new Color(.72f, .92f, 1); light.intensity = 1.4f; light.range = 3; light.cullingMask = 1 << Layer;
            overlay = new GameObject("First person equipment camera").AddComponent<Camera>(); overlay.transform.SetParent(camera.transform, false);
            overlay.clearFlags = CameraClearFlags.Depth; overlay.cullingMask = 1 << Layer;
            overlay.nearClipPlane = .015f; overlay.farClipPlane = 8; overlay.depth = camera.depth + .5f;
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
            Visible = firstPerson && state != null && state.phase != RoundPhase.Lobby && player != null && player.alive && player.team == Team.Seeker;
            Owner = Visible ? followId : 0; pivot.gameObject.SetActive(Visible); overlay.enabled = Visible;
            if (!Visible) return;
            Paint(player.diverKind);
            overlay.fieldOfView = world.fieldOfView; overlay.aspect = world.aspect; overlay.rect = world.rect;
            overlay.targetTexture = world.targetTexture;
            float time = state.time;
            var shot=Array.Find(state.harpoons,h=>h.owner==player.id);
            float firedAt=shot!=null?shot.born:player.nextAction-AquariumModel.HarpoonCooldown;
            // R27: pushing a drain lid back takes the free left hand.
            if (player.drainTask == DrainTask.Cover) receiver.PresentPush(time, Mathf.Clamp01((time - player.drainAt) / .35f));
            else receiver.Present(shot,time);
            float age = time - firedAt;
            // The DiverHands rig is authored in camera space (the diver's own gun arm holding the
            // harpoon gun in the lower right). Recoil, reel-in and the lock snap are keyed in its
            // FPSFire / FPSReload clips; idle breathing and the swim sway blend by speed.
            pivot.localPosition = Vector3.zero; pivot.localRotation = Quaternion.identity; pivot.localScale = Vector3.one;
            if (hands.cartoon && age >= 0 && age < .5f) hands.SamplePose("FPSFire", age, time);
            // R33: the reload follows the spear (out until it seats); the rest of the 5 s cooldown is shown by the HUD.
            else if (hands.cartoon && age >= .5f && shot != null) hands.SamplePose("FPSReload", Mathf.Min(2.95f,age), time);
            else hands.SampleLocomotion(speed, time);
            gun.SetLoaded(shot == null);
        }

        public void Dispose()
        {
            receiver.Dispose();
            if (world != null) world.cullingMask = originalMask;
            if (pivot != null) Destroy(pivot.gameObject); if (overlay != null) Destroy(overlay.gameObject);
        }
        static void Destroy(UnityEngine.Object o) { if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o); }
    }
}

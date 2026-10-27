using System;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // Each living client owns its view; eliminated aliens watch the diver's first-person view.
    public sealed class AquariumThirdPersonCamera
    {
        private readonly Camera camera;
        private float yaw, pitch, distance = 4.2f;
        private float targetYaw, targetPitch, targetDistance = 4.2f;
        private float ownYaw, ownPitch, ownDistance, boomLength;
        private Vector3 followPivot;
        private int round = -1, owner;
        private bool snap, savedDeathView, observerView;
        public int FollowId { get; private set; }
        public bool Active { get; private set; }
        public bool FirstPerson { get; private set; }
        public bool FreeView { get; private set; }
        public bool SpectatingDiver { get; private set; }
        public bool DeathReview { get; private set; }
        // The diver's round opening (deck idle + dive-in) is filmed from outside; the first-person view takes over as it ends.
        public bool Cinematic { get; private set; }
        public float Yaw { get { return yaw; } }
        public float Pitch { get { return pitch; } }
        public AquariumThirdPersonCamera(Camera camera) { this.camera = camera; }

        public void Prepare(AquariumSnapshot state, int localId, int observingId = 0)
        {
            var local = state == null ? null : Array.Find(state.players, p => p.id == localId);
            if (state == null || localId == 0 || state.phase == RoundPhase.Lobby)
            { Active = FirstPerson = FreeView = SpectatingDiver = DeathReview = savedDeathView = observerView = false; FollowId = 0; return; }
            bool observing = local == null;
            var observed = observing ? Array.Find(state.players, p => p.id == observingId && p.alive) : null;
            if (!Active || round != state.round || owner != localId || observerView != observing)
            {
                var initial = local ?? observed;
                yaw = targetYaw = initial == null ? camera.transform.eulerAngles.y : Mathf.Atan2(initial.facing.x, initial.facing.z) * Mathf.Rad2Deg;
                pitch = targetPitch = initial == null ? Mathf.DeltaAngle(0, camera.transform.eulerAngles.x) : 0;
                distance = targetDistance = 4.2f;
                snap = true; owner = localId; round = state.round;
                FreeView = SpectatingDiver = DeathReview = savedDeathView = false;
            }
            observerView = observing;
            Active = true;
            if (state.phase == RoundPhase.Finished)
            {
                if (!FreeView)
                {
                    // Keep the exact displayed pose, discarding any pending mouse easing.
                    var displayed = camera.transform.eulerAngles;
                    yaw = targetYaw = displayed.y;
                    pitch = targetPitch = Mathf.DeltaAngle(0, displayed.x);
                    snap = false;
                }
                FreeView = true; FirstPerson = SpectatingDiver = DeathReview = false; FollowId = 0; return;
            }
            FreeView = false;
            if (observing)
            {
                DeathReview = savedDeathView = false;
                if (observed == null) { FirstPerson = SpectatingDiver = false; FollowId = 0; return; }
                var facing = observed.facing.Normalized;
                targetYaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
                targetPitch = -Mathf.Asin(Mathf.Clamp(facing.y, -1, 1)) * Mathf.Rad2Deg;
                if (FollowId != observed.id || !FirstPerson) { yaw = targetYaw; pitch = targetPitch; snap = true; }
                FollowId = observed.id; FirstPerson = true; SpectatingDiver = observed.team == Team.Seeker;
                return;
            }
            var target = local;
            if (!local.alive && local.team == Team.Hider)
            {
                if (!savedDeathView)
                { ownYaw = yaw; ownPitch = pitch; ownDistance = targetDistance; savedDeathView = true; }
                float sinceDeath = Mathf.Max(0, state.time - local.diedAt);
                DeathReview = sinceDeath < AquariumModel.DeathReviewSeconds;
                if (DeathReview)
                    targetDistance = Mathf.Lerp(ownDistance, 6, Mathf.SmoothStep(0, 1, sinceDeath / AquariumModel.DeathReviewSeconds));
                else target = Array.Find(state.players, p => p.alive && p.team == Team.Seeker) ?? local;
            }
            else
            {
                DeathReview = false;
                if (savedDeathView)
                {
                    yaw = targetYaw = ownYaw; pitch = targetPitch = ownPitch;
                    distance = targetDistance = ownDistance; savedDeathView = false; snap = true;
                }
            }
            bool spectating = !local.alive && target != local && target.team == Team.Seeker;
            if (spectating)
            {
                var facing = target.facing.Normalized;
                targetYaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
                targetPitch = -Mathf.Asin(Mathf.Clamp(facing.y, -1, 1)) * Mathf.Rad2Deg;
                if (!SpectatingDiver) { yaw = targetYaw; pitch = targetPitch; }
            }
            SpectatingDiver = spectating;
            if (target.swallowedBy != 0)
                target = Array.Find(state.players, p => p.id == target.swallowedBy) ?? target;
            if (FollowId != target.id) snap = true;
            FollowId = target.id;
            FirstPerson = target.team == Team.Seeker;
        }

        public void Look(float horizontal, float vertical, float scroll)
        {
            if (SpectatingDiver || Cinematic || (observerView && !FreeView)) return;
            targetYaw = Mathf.Repeat(targetYaw + horizontal * 2.3f, 360);
            targetPitch = Mathf.Clamp(targetPitch - vertical * 2.3f, -65, 65);
            if (!DeathReview) targetDistance = Mathf.Clamp(targetDistance - scroll * 0.5f, 2.4f, 5.5f);
        }

        public Point Movement(float right, float forward)
        {
            var world = Quaternion.Euler(targetPitch, targetYaw, 0) * Vector3.forward * forward
                + Quaternion.Euler(0, targetYaw, 0) * Vector3.right * right;
            if (world.sqrMagnitude > 1) world.Normalize();
            return ToPoint(world);
        }

        public void MoveFree(float right, float forward, float dt)
        {
            if (!FreeView) return;
            var move = Quaternion.Euler(pitch, yaw, 0) * new Vector3(right, 0, forward);
            if (move.sqrMagnitude < 0.000001f) return;
            if (move.sqrMagnitude > 1) move.Normalize();
            var position = camera.transform.position + move * (7f * dt);
            position.x = Mathf.Clamp(position.x, -AquariumModel.HalfWidth - 3, AquariumModel.HalfWidth + 3);
            position.y = Mathf.Clamp(position.y, AquariumSeabed.Height(position.x,position.z) + 0.2f, AquariumModel.WaterHeight + 5);
            position.z = Mathf.Clamp(position.z, -AquariumModel.HalfDepth - 3, AquariumModel.HalfDepth + 3);
            camera.transform.position = position;
        }

        public void UpdatePose(AquariumSnapshot state, AquariumView view, float dt)
        {
            Cinematic = false;
            if (!Active)
            {
                // An establishing view for menus only; every player uses perspective during a round.
                AquariumEnvironment.ConfigureOverview(camera);
                view?.CameraOcclusion(0, null);
                return;
            }
            dt = Mathf.Clamp(dt, 0, 0.2f);
            float rotationBlend = 1 - Mathf.Exp(-dt / 0.06f);
            yaw = Mathf.LerpAngle(yaw, targetYaw, rotationBlend);
            pitch = Mathf.Lerp(pitch, targetPitch, rotationBlend);
            distance = Mathf.Lerp(distance, targetDistance, 1 - Mathf.Exp(-dt / 0.1f));
            if (FreeView)
            {
                camera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
                view?.CameraOcclusion(0, camera.transform.position + camera.transform.forward * 40);
                return;
            }
            var player = Array.Find(state.players, p => p.id == FollowId);
            if (player == null) return;
            camera.orthographic = false; camera.fieldOfView = 68; camera.nearClipPlane = 0.06f;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            var centre = view != null ? view.PlayerPosition(player) : new Vector3(player.position.x, player.position.y, player.position.z);
            if (FirstPerson && player.team == Team.Seeker && player.entryAt > 0 && state.time < player.entryAt)
            {
                DiveEntryShot(state, player, centre, view);
                return;
            }
            if (FirstPerson)
            {
                var eye = centre + Vector3.up * 0.15f + rotation * Vector3.forward * 0.4f;
                camera.transform.SetPositionAndRotation(snap ? eye : Vector3.Lerp(camera.transform.position, eye, 1 - Mathf.Exp(-dt * 22)), rotation); snap = false;
                view?.CameraOcclusion(FollowId, centre, true);
                return;
            }
            var pivot = centre + Vector3.up * (DeathReview ? 0.1f : 0.25f);
            followPivot = snap ? pivot : Vector3.Lerp(followPivot, pivot, 1 - Mathf.Exp(-dt * 16));
            Vector3 wanted = followPivot - rotation * Vector3.forward * distance;
            // The boom may pass through glass/water, which fade locally. Compressing it at
            // the surface would put it inside the swimmer's head. Only solid rock/floor block it.
            if (wanted.y < AquariumSeabed.Height(wanted.x,wanted.z) + 0.22f)
            {
                wanted.y = AquariumSeabed.Height(wanted.x,wanted.z) + 0.22f;
                var horizontal = new Vector3(wanted.x - pivot.x, 0, wanted.z - pivot.z);
                if (horizontal.magnitude < 2) wanted = new Vector3(pivot.x, wanted.y, pivot.z) + horizontal.normalized * 2;
            }
            // Measure boom length from the same eased origin that produced wanted.
            // Subject lag is not an obstruction and must not instantly compress the boom.
            Vector3 delta = wanted - followPivot;
            float travel = 1;
            float rock = AquariumModel.ObstacleHit(ToPoint(pivot), ToPoint(wanted), 0.22f);
            if (rock <= 1) travel = Mathf.Min(travel, Mathf.Max(0, rock - 0.04f));
            float safeLength = delta.magnitude * travel;
            // The orbit stays centred while the subject eases. Obstructions compress instantly.
            boomLength = snap || safeLength < boomLength ? safeLength : Mathf.Lerp(boomLength, safeLength, 1 - Mathf.Exp(-dt * 16));
            var candidate = followPivot + delta.normalized * boomLength;
            candidate.y = Mathf.Max(AquariumSeabed.Height(candidate.x,candidate.z) + 0.22f, candidate.y);
            float blocked = AquariumModel.ObstacleHit(ToPoint(pivot), ToPoint(candidate), 0.2f);
            if (blocked <= 1) candidate = Vector3.Lerp(pivot, candidate, Mathf.Max(0, blocked - 0.04f));
            camera.transform.SetPositionAndRotation(candidate, rotation); snap = false;
            view?.CameraOcclusion(FollowId, centre);
        }

        // Establishing shot of the diver on the deck from over the water, a slow push-in, then the camera follows the
        // stride and the splash down under the surface and hands over to the diver's own eyes as the hunt begins.
        private void DiveEntryShot(AquariumSnapshot state, Swimmer player, Vector3 centre, AquariumView view)
        {
            float t = state.time - (player.entryAt - AquariumDiveEntry.Seconds);
            var heading = new Vector3(player.facing.x, 0, player.facing.z);
            if (heading.sqrMagnitude < .0001f) heading = Vector3.forward;
            yaw = targetYaw = Quaternion.LookRotation(heading).eulerAngles.y; pitch = targetPitch = 0;
            Vector3 wide = Shot(-7.0f, 9.5f, AquariumDiveEntry.PadTop + 4.3f), above = Shot(-3.4f, 6.6f, AquariumModel.WaterHeight + 1.0f), under = Shot(-2.2f, 7.4f, AquariumModel.WaterHeight - .9f);
            Vector3 position = t < .2f ? Vector3.Lerp(wide, above, .3f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-2.4f, .2f, t)))
                : t < 1.0f ? Vector3.Lerp(Vector3.Lerp(wide, above, .3f), above, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, 1.0f, t)))
                : Vector3.Lerp(above, under, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.0f, 2.0f, t)));
            var look = Quaternion.LookRotation(centre + Vector3.up * .25f - position);
            // Hand over to the diver's eyes over the last half second (the diver is level and gliding by then).
            float handover = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(AquariumDiveEntry.Seconds - .5f, AquariumDiveEntry.Seconds, t));
            var fps = Quaternion.Euler(0, yaw, 0); var eye = centre + Vector3.up * 0.15f + fps * Vector3.forward * 0.4f;
            camera.transform.SetPositionAndRotation(Vector3.Lerp(position, eye, handover), Quaternion.Slerp(look, fps, handover));
            Cinematic = handover < .9f; snap = false;
            view?.CameraOcclusion(FollowId, centre, !Cinematic);
        }
        private static Vector3 Shot(float across, float inward, float height)
        { var p = AquariumEntryLayout.At(across, inward, height); return new Vector3(p.x, p.y, p.z); }

        public Point Aim(AquariumSnapshot state, Swimmer local)
        {
            var forward = camera.transform.forward;
            var start = ToPoint(camera.transform.position);
            var end = start + ToPoint(forward) * 40;
            float nearest = AquariumModel.ObstacleHit(start, end);
            foreach (var fish in state.fish)
                if (!fish.eaten && !fish.dead)
                    nearest = Mathf.Min(nearest, AquariumModel.FishHitTime(start, end, fish));
            foreach (var p in state.players)
                if (p.id != local.id && p.team == Team.Hider && p.alive && p.swallowedBy == 0)
                    nearest = Mathf.Min(nearest, AquariumModel.PlayerHitTime(start, end, p, state.time));
            if (local.team == Team.Seeker && state.eggs != null)
                foreach (var egg in state.eggs)
                    nearest = Mathf.Min(nearest, AquariumModel.HitTime(start, end, egg.position, 0.45f));
            var target = start + (end - start) * Mathf.Min(nearest, 1);
            // When a wall pushes the camera forward, do not aim back through the player.
            var direction = target - local.position;
            return Point.Dot(direction, ToPoint(forward)) > 0.2f ? direction.Normalized : ToPoint(forward);
        }

        public static Point ToPoint(Vector3 p) { return new Point(p.x, p.y, p.z); }
    }
}

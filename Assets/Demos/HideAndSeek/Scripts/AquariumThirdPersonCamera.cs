using System;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // Each living client owns its view; eliminated aliens watch the diver's first-person view.
    public sealed class AquariumThirdPersonCamera
    {
        private readonly Camera camera;
        private float yaw, pitch = 10, distance = 4.2f;
        private float ownYaw, ownPitch;
        private int round = -1, owner;
        private bool snap;
        public int FollowId { get; private set; }
        public bool Active { get; private set; }
        public bool FirstPerson { get; private set; }
        public bool FreeView { get; private set; }
        public bool SpectatingDiver { get; private set; }
        public float Yaw { get { return yaw; } }
        public float Pitch { get { return pitch; } }
        public AquariumThirdPersonCamera(Camera camera) { this.camera = camera; }

        public void Prepare(AquariumSnapshot state, int localId)
        {
            var local = state == null ? null : Array.Find(state.players, p => p.id == localId);
            if (local == null || state.phase == RoundPhase.Lobby)
            { Active = FirstPerson = FreeView = SpectatingDiver = false; FollowId = 0; return; }
            if (!Active || round != state.round || owner != localId)
            {
                yaw = Mathf.Atan2(local.facing.x, local.facing.z) * Mathf.Rad2Deg;
                pitch = local.team == Team.Seeker ? 0 : 10; distance = 4.2f;
                snap = true; owner = localId; round = state.round; FreeView = SpectatingDiver = false;
            }
            Active = true;
            if (state.phase == RoundPhase.Finished)
            { FreeView = true; FirstPerson = SpectatingDiver = false; FollowId = 0; return; }
            FreeView = false;
            var target = local;
            if (!local.alive && local.team == Team.Hider)
                target = Array.Find(state.players, p => p.alive && p.team == Team.Seeker) ?? local;
            bool spectating = !local.alive && target != local && target.team == Team.Seeker;
            if (spectating && !SpectatingDiver) { ownYaw = yaw; ownPitch = pitch; }
            if (!spectating && SpectatingDiver) { yaw = ownYaw; pitch = ownPitch; }
            SpectatingDiver = spectating;
            if (SpectatingDiver)
            {
                var facing = target.facing.Normalized;
                yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
                pitch = -Mathf.Asin(Mathf.Clamp(facing.y, -1, 1)) * Mathf.Rad2Deg;
            }
            if (target.swallowedBy != 0)
                target = Array.Find(state.players, p => p.id == target.swallowedBy) ?? target;
            if (FollowId != target.id) snap = true;
            FollowId = target.id;
            FirstPerson = target.team == Team.Seeker;
        }

        public void Look(float horizontal, float vertical, float scroll)
        {
            if (SpectatingDiver) return;
            yaw = Mathf.Repeat(yaw + horizontal * 2.3f, 360);
            pitch = Mathf.Clamp(pitch - vertical * 2.3f, -65, 65);
            distance = Mathf.Clamp(distance - scroll * 0.5f, 2.4f, 5.5f);
        }

        public Point Movement(float right, float forward, float vertical)
        {
            var world = Quaternion.Euler(0, yaw, 0) * new Vector3(right, 0, forward) + Vector3.up * vertical;
            if (world.sqrMagnitude > 1) world.Normalize();
            return ToPoint(world);
        }

        public void MoveFree(float right, float forward, float vertical, float dt)
        {
            if (!FreeView) return;
            var move = Quaternion.Euler(pitch, yaw, 0) * new Vector3(right, 0, forward) + Vector3.up * vertical;
            if (move.sqrMagnitude > 1) move.Normalize();
            var position = camera.transform.position + move * (7f * dt);
            position.x = Mathf.Clamp(position.x, -AquariumModel.HalfWidth - 3, AquariumModel.HalfWidth + 3);
            position.y = Mathf.Clamp(position.y, AquariumSeabed.Height(position.x,position.z) + 0.2f, AquariumModel.WaterHeight + 5);
            position.z = Mathf.Clamp(position.z, -AquariumModel.HalfDepth - 3, AquariumModel.HalfDepth + 3);
            camera.transform.position = position;
        }

        public void UpdatePose(AquariumSnapshot state, AquariumView view, float dt)
        {
            if (!Active)
            {
                // An establishing view for menus only; every player uses perspective during a round.
                AquariumEnvironment.ConfigureOverview(camera);
                view.CameraOcclusion(0, null);
                return;
            }
            if (FreeView)
            {
                camera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
                view.CameraOcclusion(0, camera.transform.position + camera.transform.forward * 40);
                return;
            }
            var player = Array.Find(state.players, p => p.id == FollowId);
            if (player == null) return;
            camera.orthographic = false; camera.fieldOfView = 68; camera.nearClipPlane = 0.06f;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            var centre = view.PlayerPosition(player);
            if (FirstPerson)
            {
                var eye = centre + Vector3.up * 0.15f + rotation * Vector3.forward * 0.4f;
                camera.transform.SetPositionAndRotation(eye, rotation); snap = false;
                view.CameraOcclusion(FollowId, centre, true);
                return;
            }
            var pivot = centre + Vector3.up * 0.65f + rotation * Vector3.right * 0.6f;
            Vector3 wanted = pivot - rotation * Vector3.forward * distance;
            // The boom may pass through glass/water, which fade locally. Compressing it at
            // the surface would put it inside the swimmer's head. Only solid rock/floor block it.
            if (wanted.y < AquariumSeabed.Height(wanted.x,wanted.z) + 0.22f)
            {
                wanted.y = AquariumSeabed.Height(wanted.x,wanted.z) + 0.22f;
                var horizontal = new Vector3(wanted.x - pivot.x, 0, wanted.z - pivot.z);
                if (horizontal.magnitude < 2) wanted = new Vector3(pivot.x, wanted.y, pivot.z) + horizontal.normalized * 2;
            }
            Vector3 delta = wanted - pivot;
            float travel = 1;
            float rock = AquariumModel.ObstacleHit(ToPoint(pivot), ToPoint(wanted), 0.22f);
            if (rock <= 1) travel = Mathf.Min(travel, Mathf.Max(0, rock - 0.04f));
            wanted = pivot + delta * travel;
            // Snap inward to prevent crossing a wall; ease outward when cover clears.
            var candidate = snap ? wanted : Vector3.Lerp(camera.transform.position, wanted, 1 - Mathf.Exp(-dt * 16));
            candidate.y = Mathf.Max(AquariumSeabed.Height(candidate.x,candidate.z) + 0.22f, candidate.y);
            float blocked = AquariumModel.ObstacleHit(ToPoint(pivot), ToPoint(candidate), 0.2f);
            if (blocked <= 1) candidate = Vector3.Lerp(pivot, candidate, Mathf.Max(0, blocked - 0.04f));
            camera.transform.SetPositionAndRotation(candidate, rotation); snap = false;
            view.CameraOcclusion(FollowId, centre);
        }

        public Point Aim(AquariumSnapshot state, Swimmer local)
        {
            var forward = Quaternion.Euler(pitch, yaw, 0) * Vector3.forward;
            var start = ToPoint(camera.transform.position);
            var end = start + ToPoint(forward) * 40;
            float nearest = AquariumModel.ObstacleHit(start, end);
            foreach (var fish in state.fish)
                if (!fish.eaten && !fish.dead)
                    nearest = Mathf.Min(nearest, AquariumModel.HitTime(start, end, fish.position, 0.42f));
            foreach (var p in state.players)
                if (p.id != local.id && p.team == Team.Hider && p.alive && p.swallowedBy == 0)
                    nearest = Mathf.Min(nearest, AquariumModel.HitTime(start, end, p.position, 0.48f));
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

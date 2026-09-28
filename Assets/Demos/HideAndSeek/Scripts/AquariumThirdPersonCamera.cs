using System;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // Each client owns its view. Camera rotation is never taken from a remote player.
    public sealed class AquariumThirdPersonCamera
    {
        private readonly Camera camera;
        private float yaw, pitch = 10, distance = 4.2f;
        private int round = -1, owner;
        private bool snap;
        public int FollowId { get; private set; }
        public bool Active { get; private set; }
        public float Yaw { get { return yaw; } }
        public float Pitch { get { return pitch; } }
        public AquariumThirdPersonCamera(Camera camera) { this.camera = camera; }

        public void Prepare(AquariumSnapshot state, int localId)
        {
            var local = state == null ? null : Array.Find(state.players, p => p.id == localId);
            if (local == null || state.phase == RoundPhase.Lobby)
            { Active = false; FollowId = 0; return; }
            if (!Active || round != state.round || owner != localId)
            {
                yaw = Mathf.Atan2(local.facing.x, local.facing.z) * Mathf.Rad2Deg;
                pitch = 10; distance = 4.2f; snap = true; owner = localId; round = state.round;
            }
            Active = true;
            var target = local;
            if (!local.alive)
                target = Array.Find(state.players, p => p.alive && p.team == local.team) ?? local;
            if (target.swallowedBy != 0)
                target = Array.Find(state.players, p => p.id == target.swallowedBy) ?? target;
            if (FollowId != target.id) snap = true;
            FollowId = target.id;
        }

        public void Look(float horizontal, float vertical, float scroll)
        {
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

        public void UpdatePose(AquariumSnapshot state, AquariumView view, float dt)
        {
            if (!Active)
            {
                // An establishing view for menus only; every player uses perspective during a round.
                camera.orthographic = false; camera.fieldOfView = 52;
                camera.transform.SetPositionAndRotation(new Vector3(0, 21, -24), Quaternion.Euler(39, 0, 0));
                view.CameraOcclusion(0, null);
                return;
            }
            var player = Array.Find(state.players, p => p.id == FollowId);
            if (player == null) return;
            camera.orthographic = false; camera.fieldOfView = 68; camera.nearClipPlane = 0.06f;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            var centre = view.PlayerPosition(player);
            var pivot = centre + Vector3.up * 0.65f + rotation * Vector3.right * 0.6f;
            Vector3 wanted = pivot - rotation * Vector3.forward * distance;
            // The boom may pass through glass/water, which fade locally. Compressing it at
            // the surface would put it inside the swimmer's head. Only solid rock/floor block it.
            if (wanted.y < AquariumModel.Floor + 0.22f)
            {
                wanted.y = AquariumModel.Floor + 0.22f;
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
            candidate.y = Mathf.Max(AquariumModel.Floor + 0.22f, candidate.y);
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
                if (!fish.eaten && (!fish.dead || local.team == Team.Hider))
                    nearest = Mathf.Min(nearest, AquariumModel.HitTime(start, end, fish.position, 0.42f));
            foreach (var p in state.players)
                if (p.id != local.id && p.team == Team.Hider && p.alive && p.swallowedBy == 0)
                    nearest = Mathf.Min(nearest, AquariumModel.HitTime(start, end, p.position, 0.48f));
            var target = start + (end - start) * Mathf.Min(nearest, 1);
            // When a wall pushes the camera forward, do not aim back through the player.
            var direction = target - local.position;
            return Point.Dot(direction, ToPoint(forward)) > 0.2f ? direction.Normalized : ToPoint(forward);
        }

        public static Point ToPoint(Vector3 p) { return new Point(p.x, p.y, p.z); }
    }
}

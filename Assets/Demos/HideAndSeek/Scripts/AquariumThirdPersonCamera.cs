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
        // R49: eased push that keeps the lens outside fish and swimmer hulls.
        private Vector3 bodyPush;
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
        // R47: the drone diver is steering its fish drone: a third-person view behind the drone.
        public bool DroneView { get; private set; }
        private Vector3 dronePivot;
        private bool lockerView;
        private float diverYaw, diverPitch;
        public float Yaw { get { return yaw; } }
        public float Pitch { get { return pitch; } }
        public AquariumThirdPersonCamera(Camera camera) { this.camera = camera; }

        public void Prepare(AquariumSnapshot state, int localId, int observingId = 0)
        {
            var local = state == null ? null : Array.Find(state.players, p => p.id == localId);
            if (state == null || localId == 0 || state.phase == RoundPhase.Lobby)
            { Active = FirstPerson = FreeView = SpectatingDiver = DeathReview = savedDeathView = observerView = DroneView = false; FollowId = 0; return; }
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
            // R49: once the round is over everyone (fallen aliens too) gets the free-flying review camera.
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
                FreeView = true; FirstPerson = SpectatingDiver = DeathReview = DroneView = false; FollowId = 0; return;
            }
            FreeView = false;
            bool drone = !observing && local.alive && local.team == Team.Seeker && local.droneControl;
            if (drone != DroneView)
            {
                if (drone)
                {
                    diverYaw = targetYaw; diverPitch = targetPitch;
                    var f = local.droneFacing.Length > .01f ? local.droneFacing.Normalized : local.facing.Normalized;
                    targetYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; targetPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(f.y, -1, 1)) * Mathf.Rad2Deg + 8, -65, 65);
                    dronePivot = new Vector3(local.dronePosition.x, local.dronePosition.y, local.dronePosition.z);
                }
                else { targetYaw = yaw = diverYaw; targetPitch = pitch = diverPitch; }
                DroneView = drone; snap = true;
            }
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
                    targetDistance = ownDistance;
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
            targetPitch = Mathf.Clamp(targetPitch - vertical * 2.3f, -65, 89);   // R73h: the mouse looks all the way down
            if (!DeathReview) targetDistance = Mathf.Clamp(targetDistance - scroll * 0.5f, 2.4f, 5.5f);
        }

        public Point Movement(float right, float forward)
        {
            var world = Quaternion.Euler(targetPitch, targetYaw, 0) * Vector3.forward * forward
                + Quaternion.Euler(0, targetYaw, 0) * Vector3.right * right;
            if (world.sqrMagnitude > 1) world.Normalize();
            return ToPoint(world);
        }

        // R49: the post-game free camera flies: WASD along the view, vertical (Space up, Ctrl/C down) in world up,
        // fast with Shift, and it stays in the tank's water (glass, rocks, floor, surface).
        public void MoveFree(float right, float forward, float dt, float vertical = 0, bool fast = false)
        {
            if (!FreeView) return;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            var move = rotation * new Vector3(right, 0, forward) + Vector3.up * vertical;
            if (move.sqrMagnitude < 0.000001f) return;
            if (move.sqrMagnitude > 1) move.Normalize();
            var position = camera.transform.position + move * ((fast ? 16f : 7f) * dt);
            camera.transform.position = FreeConstrain(position);
        }
        private static Vector3 FreeConstrain(Vector3 position)
        {
            var p = AquariumModel.Constrain(ToPoint(position), FreeRadius);
            return new Vector3(p.x, p.y, p.z);
        }
        private const float FreeRadius = 0.3f;

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
            camera.orthographic = false; camera.fieldOfView = 68; camera.nearClipPlane = FirstPerson ? 0.025f : 0.05f;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            var centre = view != null ? view.PlayerPosition(player) : new Vector3(player.position.x, player.position.y, player.position.z);
            if (DroneView)
            {
                // R47: follow the (smoothed) drone from behind at a short boom; the diver's gun is hidden.
                var drone = new Vector3(player.dronePosition.x, player.dronePosition.y, player.dronePosition.z);
                dronePivot = snap ? drone : Vector3.Lerp(dronePivot, drone, 1 - Mathf.Exp(-dt * 12));
                var behind = dronePivot + Vector3.up * .35f - rotation * Vector3.forward * 2.4f;
                float hit = AquariumModel.ObstacleHit(ToPoint(dronePivot), ToPoint(behind), 0.2f);
                // R49: fish between the drone and the lens shorten the boom too.
                hit = Mathf.Min(hit, BodyHit(state, dronePivot, behind, 0, 0.7f));
                if (hit <= 1) behind = Vector3.Lerp(dronePivot, behind, Mathf.Max(0, hit - 0.04f));
                behind += EaseBodyPush(state, behind, 0, dt);
                behind.y = Mathf.Max(AquariumSeabed.Height(behind.x, behind.z) + 0.22f, behind.y);
                camera.transform.SetPositionAndRotation(snap ? behind : Vector3.Lerp(camera.transform.position, behind, 1 - Mathf.Exp(-dt * 18)), rotation); snap = false;
                view?.CameraOcclusion(0, dronePivot);
                return;
            }
            if (FirstPerson && player.team == Team.Seeker && player.entryAt > 0 && state.time < player.entryAt)
            {
                DiveEntryShot(state, player, centre, view);
                return;
            }
            if (FirstPerson)
            {
                var eye = centre + Vector3.up * 0.15f + rotation * Vector3.forward * 0.4f;
                var shown = snap ? eye : Vector3.Lerp(camera.transform.position, eye, 1 - Mathf.Exp(-dt * 22));
                // R49: a fish swimming into the diver's face nudges the eye back out of its hull instead of clipping.
                shown += EaseBodyPush(state, shown, FollowId, dt, 0.5f);
                camera.transform.SetPositionAndRotation(shown, rotation); snap = false;
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
            // R49: fish and swimmers crossing the boom shorten it too (quickly but eased, so a passing school does not
            // jerk the camera). Bodies hugging the subject are handled by the push below instead.
            float bodies = BodyHit(state, followPivot, wanted, FollowId, 0.9f);
            float bodyLength = bodies <= 1 ? delta.magnitude * Mathf.Max(0, bodies - 0.02f) : delta.magnitude;
            // The orbit stays centred while the subject eases. Rock compresses instantly.
            if (snap || safeLength < boomLength) boomLength = Mathf.Min(safeLength, snap ? bodyLength : safeLength);
            else if (bodyLength < boomLength) boomLength = Mathf.Lerp(boomLength, bodyLength, 1 - Mathf.Exp(-dt * 24));
            else boomLength = Mathf.Lerp(boomLength, Mathf.Min(safeLength, bodyLength), 1 - Mathf.Exp(-dt * 16));
            var candidate = followPivot + delta.normalized * boomLength;
            candidate += EaseBodyPush(state, candidate, FollowId, dt);
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
            // R82: the shot runs on the view's frame clock; on the snapshot time the camera moved 30 times a second.
            float t = (view != null ? view.FrameTime(state) : state.time) - (player.entryAt - AquariumDiveEntry.Seconds);
            // R60: before the cut the diver is in the changing room, looking about freely at eye height.
            if (t < 0)
            {
                Cinematic = false; lockerView = true;
                var roomEye = centre + Vector3.up * AquariumLockerRoom.EyeAboveRoot;
                var roomLook = Quaternion.Euler(pitch, yaw, 0);
                camera.transform.SetPositionAndRotation(snap ? roomEye : Vector3.Lerp(camera.transform.position, roomEye, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 20)), roomLook);
                camera.nearClipPlane = .05f; snap = false;
                view?.CameraOcclusion(FollowId, centre, true);
                return;
            }
            if (lockerView) { lockerView = false; snap = true; }
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
            // R47: the drone just looks where the camera looks.
            if (DroneView) return ToPoint(Quaternion.Euler(targetPitch, targetYaw, 0) * Vector3.forward);
            var start = ToPoint(camera.transform.position);
            var end = start + ToPoint(forward) * 40;
            float nearest = AquariumModel.ObstacleHit(start, end);
            foreach (var fish in state.fish)
                if (!fish.eaten && !fish.dead)
                    nearest = Mathf.Min(nearest, AquariumModel.FishHitTime(start, end, fish));
            foreach (var p in state.players)
                if (p.id != local.id && p.team == Team.Hider && p.alive && p.swallowedBy == 0)
                    nearest = Mathf.Min(nearest, AquariumModel.PlayerHitTime(start, end, p, state.time, state.drains));
            if (local.team == Team.Seeker && state.eggs != null)
                foreach (var egg in state.eggs)
                    nearest = Mathf.Min(nearest, AquariumModel.HitTime(start, end, egg.position, 0.45f));
            var target = start + (end - start) * Mathf.Min(nearest, 1);
            // When a wall pushes the camera forward, do not aim back through the player.
            var direction = target - local.position;
            return Point.Dot(direction, ToPoint(forward)) > 0.2f ? direction.Normalized : ToPoint(forward);
        }

        public static Point ToPoint(Vector3 p) { return new Point(p.x, p.y, p.z); }

        // ---- R49: keeping the lens outside fish and swimmer bodies -----------------------------------------
        private const float LensRadius = 0.26f;
        private struct Hull { public Point centre, facing; public float radius, half; }
        private static readonly System.Collections.Generic.List<Hull> hulls = new System.Collections.Generic.List<Hull>();
        // Every visible fish / swimmer hull (snapshot positions, AquariumFishTraits / AquariumBodyCollision sizes).
        private static void CollectHulls(AquariumSnapshot state, int ignoreId)
        {
            hulls.Clear();
            if (state == null) return;
            if (state.fish != null)
                foreach (var f in state.fish)
                    if (!f.eaten && !f.collected && !f.dead)
                        hulls.Add(new Hull { centre = f.position, facing = f.facing, radius = AquariumFishTraits.HullRadius(f.species), half = AquariumFishTraits.HullHalf(f.species) });
            if (state.players != null)
                foreach (var p in state.players)
                {
                    // Corpses on a harpoon line are reeled right up to the diver: they are not pushed against.
                    if (p.id == ignoreId || !p.alive || p.escaped || p.swallowedBy != 0) continue;
                    bool diver = p.team == Team.Seeker, disguised = !diver && AquariumModel.HasFishDisguise(p, state.time);
                    hulls.Add(new Hull { centre = p.position, facing = p.facing,
                        radius = diver ? AquariumBodyCollision.DiverRadius : disguised ? AquariumFishTraits.HullRadius(p.species) : AquariumBodyCollision.AlienRadius,
                        half = diver ? AquariumBodyCollision.DiverHalf : disguised ? AquariumFishTraits.HullHalf(p.species) : AquariumBodyCollision.AlienHalf });
                }
        }
        // Fraction of a->b where the lens first touches a hull (2 = clear). Bodies that already hold a, or that are
        // met closer than nearSubject metres from a, are left to the push.
        private static float BodyHit(AquariumSnapshot state, Vector3 a, Vector3 b, int ignoreId, float nearSubject)
        {
            CollectHulls(state, ignoreId);
            var pa = ToPoint(a); var pb = ToPoint(b); float length = Vector3.Distance(a, b), nearest = 2;
            if (length < 0.001f) return 2;
            foreach (var h in hulls)
            {
                float reach = h.radius + h.half + LensRadius;
                if (SegmentDistance(a, b, new Vector3(h.centre.x, h.centre.y, h.centre.z)) > reach) continue;
                float t = AquariumModel.HullHitTime(pa, pb, h.centre, h.facing, h.radius + LensRadius, h.half);
                if (t <= 0 || t > 1 || t * length < nearSubject) continue;
                nearest = Mathf.Min(nearest, t);
            }
            return nearest;
        }
        // How far 'at' has to move to clear every hull (sum of the separations), capped.
        private static Vector3 BodyPushRaw(AquariumSnapshot state, Vector3 at, int ignoreId, float cap)
        {
            CollectHulls(state, ignoreId);
            var push = Vector3.zero;
            foreach (var h in hulls)
            {
                var c = new Vector3(h.centre.x, h.centre.y, h.centre.z);
                var axis = new Vector3(h.facing.x, h.facing.y, h.facing.z);
                axis = axis.sqrMagnitude > 0.0001f ? axis.normalized * h.half : Vector3.zero;
                var closest = ClosestOnSegment(c - axis, c + axis, at);
                var away = at - closest; float need = h.radius + LensRadius, d = away.magnitude;
                if (d >= need) continue;
                push += (d > 0.0001f ? away / d : Vector3.up) * (need - d);
            }
            return Vector3.ClampMagnitude(push, cap);
        }
        private Vector3 EaseBodyPush(AquariumSnapshot state, Vector3 at, int ignoreId, float dt, float cap = 1f)
        {
            var wanted = BodyPushRaw(state, at, ignoreId, cap);
            bodyPush = snap ? wanted : Vector3.Lerp(bodyPush, wanted, 1 - Mathf.Exp(-dt * 18));
            // Never leave the lens pushed into rock or below the sand.
            var shifted = at + bodyPush;
            if (AquariumModel.ObstacleHit(ToPoint(at), ToPoint(shifted), 0.15f) <= 1 || shifted.y < AquariumSeabed.Height(shifted.x, shifted.z) + 0.2f)
                return Vector3.zero;
            return bodyPush;
        }
        private static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            var ab = b - a; float l = ab.sqrMagnitude;
            if (l < 0.000001f) return a;
            return a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / l);
        }
        private static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p) { return Vector3.Distance(ClosestOnSegment(a, b, p), p); }
    }
}

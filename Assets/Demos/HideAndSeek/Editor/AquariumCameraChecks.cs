using System;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumCameraChecks
    {
        private static int checks;
        private static void Check(bool condition, string description)
        { if (!condition) throw new Exception("Aquarium camera check: " + description); checks++; }

        private static void Pose(AquariumThirdPersonCamera camera, AquariumSnapshot state, int localId = 1, int frames = 1, int observingId = 0)
        { for (int i = 0; i < frames; i++) { camera.Prepare(state, localId, observingId); camera.UpdatePose(state, null, 1f / 60); } }

        private static Point Drive(AquariumModel model, Point move, int frames = 12)
        {
            var before = model.Player(1).position;
            for (int i = 0; i < frames; i++)
            {
                model.SetInput(1, new SwimInput { x = move.x, y = move.y, z = move.z });
                model.Tick(1f / 30);
            }
            return model.Player(1).position - before;
        }

        private static Point SwimFromRest(AquariumModel model, AquariumThirdPersonCamera camera, float right, float forward)
        {
            model.Player(1).position = new Point(0, 3.5f, 0); model.Player(1).velocity = new Point();
            return Drive(model, camera.Movement(right, forward));
        }

        private static void CheckLookSwimming(Camera unityCamera, Team role)
        {
            var model = new AquariumModel(); model.AddPlayer(1, "Swimmer", role);
            Team otherRole = role == Team.Hider ? Team.Seeker : Team.Hider;
            model.AddPlayer(2, "Partner", otherRole); model.SetRole(2, otherRole, true); model.StartRound();
            foreach (var player in model.Players) player.bot = false;
            for (int i = 0; i < 160; i++) model.Tick(1f / 30);
            model.Player(1).position = new Point(0, 3.5f, 0); model.Player(1).facing = new Point(0, 0, 1);
            var camera = new AquariumThirdPersonCamera(unityCamera); Pose(camera, model.Snapshot());
            Check(Math.Abs(camera.Pitch) < 0.0001f, role + " begins with a level swimming view");
            var level = SwimFromRest(model, camera, 0, 1);
            Check(level.z > 1 && Math.Abs(level.y) < 0.0001f, role + " looking straight and swimming forward maintains actual height");
            camera.Look(0, 45f / 2.3f, 0);
            var up = SwimFromRest(model, camera, 0, 1);
            Check(up.y > 0.8f && up.z > 0.8f, role + " looking up and pressing W raises the actual model position immediately");
            var back = SwimFromRest(model, camera, 0, -1);
            Check(back.y < -0.8f && back.z < -0.8f && (up + back).Length < 0.001f, role + " S moves opposite the full view direction");
            var side = SwimFromRest(model, camera, 1, 0);
            var left = SwimFromRest(model, camera, -1, 0);
            Check(side.x > 1 && left.x < -1 && Math.Abs(side.y) < 0.0001f && Math.Abs(left.y) < 0.0001f, role + " A and D remain horizontal while looking up");
            var diagonal = SwimFromRest(model, camera, 1, 1);
            Check(Math.Abs(diagonal.Length - up.Length) < 0.001f && Math.Abs(camera.Movement(1, 1).Length - 1) < 0.001f,
                role + " diagonal swimming has the same actual speed as forward swimming");
            camera.Look(0, -90f / 2.3f, 0);
            var down = SwimFromRest(model, camera, 0, 1);
            Check(down.y < -0.8f && down.z > 0.8f, role + " looking down and pressing W lowers the actual model position");
            Drive(model, camera.Movement(0, 0));
            Check(Drive(model, camera.Movement(0, 0)).Length < 0.0001f && model.Player(1).velocity.Length < 0.0001f,
                role + " releasing movement settles to a stationary position without vertical drift");
        }

        public static void Run()
        {
            checks = 0;
            var hostObject = new GameObject("Camera check host"); var remoteObject = new GameObject("Camera check client");
            try
            {
                var host = new AquariumThirdPersonCamera(hostObject.AddComponent<Camera>());
                var client = new AquariumThirdPersonCamera(remoteObject.AddComponent<Camera>());
                var model = new AquariumModel(); model.AddPlayer(1, "Host", Team.Hider); model.AddPlayer(2, "Client", Team.Seeker);
                model.AddPlayer(3, "Friend", Team.Hider); model.SetRole(2, Team.Seeker, true); model.SetRole(3, Team.Hider, true); model.StartRound();
                foreach (var player in model.Players) player.bot = false;
                model.Player(1).position = new Point(0, 4, 0);
                Pose(host, model.Snapshot()); Pose(client, model.Snapshot(), 2);
                Check(host.FollowId == 1 && client.FollowId == 2, "each client follows its own player");
                Check(!host.FirstPerson && client.FirstPerson && !client.FreeView, "diver first person and alien third person");
                var centre = new Vector3(0, 4, 0);
                Check(Math.Abs(Vector3.Dot(centre - hostObject.transform.position, hostObject.transform.right)) < 0.001f, "alien is horizontally centred without a shoulder offset");
                Check(Math.Abs(host.Pitch) < 0.0001f, "default view is level");
                var forward = host.Movement(0, 1); Check(forward.x > 0.99f && Math.Abs(forward.z) < 0.01f, "W follows camera heading");
                float initialYaw = host.Yaw, clientYaw = client.Yaw;
                host.Look(20, 2, 0);
                Check(host.Yaw == initialYaw && client.Yaw == clientYaw, "mouse input sets a target without snapping either view");
                Check(host.Movement(0, 1).z < -0.5f, "movement responds to intended heading immediately");
                Pose(host, model.Snapshot());
                Check(host.Yaw > initialYaw && host.Yaw < initialYaw + 46 && client.Yaw == clientYaw, "camera rotation eases locally toward input");
                Pose(host, model.Snapshot(), 1, 60);
                Check(Math.Abs(Mathf.DeltaAngle(host.Yaw, initialYaw + 46)) < 0.01f, "camera settles promptly at desired heading");
                client.Look(0, 1000, 0); Pose(client, model.Snapshot(), 2, 60);
                Check(client.Pitch >= -65 && client.Pitch <= 65, "smoothed pitch obeys its limits");
                Check(Math.Abs(host.Movement(1, 1).Length - 1) < 0.001f, "normalized diagonal swimming");
                float distanceBeforeZoom = Vector3.Distance(hostObject.transform.position, centre + Vector3.up * 0.25f);
                host.Look(0, 0, -2); Pose(host, model.Snapshot());
                float distanceDuringZoom = Vector3.Distance(hostObject.transform.position, centre + Vector3.up * 0.25f);
                Check(distanceDuringZoom > distanceBeforeZoom && distanceDuringZoom < distanceBeforeZoom + 0.5f, "zoom eases instead of jumping");
                host.Look(0, 0, 2); Pose(host, model.Snapshot(), 1, 60);
                model.Player(1).swallowedBy = 3; Pose(host, model.Snapshot()); Check(host.FollowId == 3, "follow carrier while swallowed");
                model.Player(1).swallowedBy = 0; Pose(host, model.Snapshot()); Check(host.FollowId == 1, "return after spit");

                float ownYaw = host.Yaw, ownPitch = host.Pitch;
                model.Player(1).alive = false; model.Player(1).diedAt = model.Time;
                model.Player(2).facing = new Point(0, 0.6f, 0.8f);
                var deathState = model.Snapshot(); Pose(host, deathState);
                Check(host.DeathReview && host.FollowId == 1 && !host.FirstPerson && !host.SpectatingDiver && !host.FreeView, "nonfinal death first follows the local corpse in third person");
                float deathStartDistance = Vector3.Distance(hostObject.transform.position, centre);
                for (int i = 1; i <= 72; i++)
                { deathState.time = model.Player(1).diedAt + i / 30f; Pose(host, deathState); }
                Check(host.DeathReview && Vector3.Distance(hostObject.transform.position, centre) > deathStartDistance + 1, "death view slowly pulls back while the corpse remains visible");
                Check(Math.Abs(Vector3.Dot(centre - hostObject.transform.position, hostObject.transform.right)) < 0.001f, "death orbit stays horizontally centred on the corpse");
                deathState.time = model.Player(1).diedAt + AquariumModel.DeathReviewSeconds + 0.01f; Pose(host, deathState);
                Check(!host.DeathReview && host.FollowId == 2 && host.FirstPerson && host.SpectatingDiver && !host.FreeView, "death review then switches to the living diver");
                Check(Math.Abs(host.Yaw) < 0.001f && Math.Abs(host.Pitch + 36.8699f) < 0.01f, "spectator begins in the diver's actual aim direction");
                host.Look(10, 10, 0); Pose(host, deathState);
                Check(Math.Abs(host.Yaw) < 0.001f, "spectator cannot rotate the diver view");
                model.Player(2).facing = new Point(1, 0, 0); Pose(host, deathState, 1, 60);
                Check(Math.Abs(host.Yaw - 90) < 0.01f && Math.Abs(host.Pitch) < 0.01f, "spectator smoothly tracks changing diver aim");
                model.Player(1).alive = true; Pose(host, model.Snapshot());
                Check(host.FollowId == 1 && !host.FirstPerson && !host.SpectatingDiver && !host.DeathReview && !host.FreeView, "revival restores the local third-person view");
                Check(Math.Abs(host.Yaw - ownYaw) < 0.001f && Math.Abs(host.Pitch - ownPitch) < 0.001f, "revival after spectating restores the prior camera angle");

                model.Player(1).alive = false; model.Player(1).diedAt = model.Time;
                Pose(host, model.Snapshot()); host.Look(10, 0, 0); Pose(host, model.Snapshot());
                model.Player(1).alive = true; Pose(host, model.Snapshot());
                Check(!host.DeathReview && Math.Abs(host.Yaw - ownYaw) < 0.001f, "revival during the death orbit also restores the prior angle");
                model.Player(1).alive = false; Pose(host, model.Snapshot()); host.Look(50, 10, 0);
                var finishPosition = hostObject.transform.position; var finishRotation = hostObject.transform.rotation;
                model.RemovePlayer(2); Pose(host, model.Snapshot());
                Check(host.FreeView && host.FollowId == 0 && !host.FirstPerson && !host.DeathReview, "round finish bypasses a pending death review");
                Check(Vector3.Distance(finishPosition, hostObject.transform.position) < 0.0001f && Quaternion.Angle(finishRotation, hostObject.transform.rotation) < 0.05f, "finish preserves the displayed pose and discards pending rotation");
                host.MoveFree(0, 0, 0.1f); Pose(host, model.Snapshot());
                Check(Vector3.Distance(finishPosition, hostObject.transform.position) < 0.0001f, "free view stays still without movement input");
                var playerBefore = model.Player(1).position; host.MoveFree(1, 1, 0.1f);
                Check(Vector3.Distance(hostObject.transform.position, finishPosition) > 0.5f && (model.Player(1).position - playerBefore).Length == 0, "free camera movement does not move a player");
                var survivor = new AquariumThirdPersonCamera(remoteObject.GetComponent<Camera>()); Pose(survivor, model.Snapshot(), 3);
                Check(survivor.FreeView && survivor.FollowId == 0, "survivors also enter free view immediately");

                var observerModel = new AquariumModel(); observerModel.AddPlayer(1, "Host", Team.Hider); observerModel.AddPlayer(2, "Diver", Team.Seeker);
                observerModel.SetRole(2, Team.Seeker, true); observerModel.StartRound();
                foreach (var player in observerModel.Players) player.bot = false;
                observerModel.Player(2).facing = new Point(1, 0.2f, 1);
                var observer = new AquariumThirdPersonCamera(remoteObject.GetComponent<Camera>());
                Pose(observer, observerModel.Snapshot(), 50, 1, 2);
                Check(observerModel.Player(50) == null && observer.Active && observer.FollowId == 2 && observer.FirstPerson && observer.SpectatingDiver && !observer.DeathReview,
                    "observer absent from the model immediately watches the live diver without a death intro");
                Check(Vector3.Angle(remoteObject.transform.forward, new Vector3(1, 0.2f, 1)) < 0.05f, "observer camera faces the diver's actual direction");
                float observerYaw = observer.Yaw, observerPitch = observer.Pitch;
                observer.Look(30, 30, 3); Pose(observer, observerModel.Snapshot(), 50, 10, 2);
                Check(Math.Abs(observer.Yaw - observerYaw) < 0.001f && Math.Abs(observer.Pitch - observerPitch) < 0.001f, "observer cannot rotate the diver's view");
                observerModel.Player(1).facing = new Point(-1, 0, 0); Pose(observer, observerModel.Snapshot(), 50, 1, 1);
                observerYaw = observer.Yaw; observer.Look(30, 30, 0); Pose(observer, observerModel.Snapshot(), 50, 10, 1);
                Check(observer.FollowId == 1 && observer.FirstPerson && !observer.DeathReview && Math.Abs(observer.Yaw - observerYaw) < 0.001f,
                    "fallback alien observation also uses its real direction and blocks free look");
                var observedPosition = remoteObject.transform.position; var observedRotation = remoteObject.transform.rotation;
                observerModel.RemovePlayer(2); Pose(observer, observerModel.Snapshot(), 50, 1, 1);
                Check(observer.FreeView && !observer.SpectatingDiver && !observer.DeathReview
                    && Vector3.Distance(observedPosition, remoteObject.transform.position) < 0.0001f && Quaternion.Angle(observedRotation, remoteObject.transform.rotation) < 0.05f,
                    "observer enters finished free view without changing the displayed pose");
                observerYaw = observer.Yaw; observer.Look(10, 0, 0); Pose(observer, observerModel.Snapshot(), 50, 60, 1);
                Check(Math.Abs(Mathf.DeltaAngle(observerYaw, observer.Yaw)) > 20, "finished observers regain free camera rotation");
                observedPosition = remoteObject.transform.position; observer.MoveFree(1, 0, 0.1f);
                Check(Vector3.Distance(observedPosition, remoteObject.transform.position) > 0.5f, "finished observers regain free camera movement");
                for (int i = 0; i < 610; i++) observerModel.Tick(1f / 30);
                observerModel.ReturnToLobby(); Pose(observer, observerModel.Snapshot(), 50, 1, 1);
                Check(!observer.Active && !observer.FreeView && !observer.FirstPerson && !observer.SpectatingDiver, "observer camera resets on return to lobby");
                observerModel.AddPlayer(2, "Diver", Team.Seeker); observerModel.SetRole(2, Team.Seeker, true);
                observerModel.AddPlayer(50, "Joined player", Team.Hider); observerModel.SetRole(50, Team.Hider, true); observerModel.StartRound();
                Pose(observer, observerModel.Snapshot(), 50);
                Check(observer.FollowId == 50 && observer.Active && !observer.FirstPerson && !observer.SpectatingDiver && !observer.FreeView && !observer.DeathReview,
                    "promoted observer owns the next round's normal third-person view");
                observerYaw = observer.Yaw; observer.Look(10, 0, 0); Pose(observer, observerModel.Snapshot(), 50, 60);
                Check(Math.Abs(Mathf.DeltaAngle(observerYaw, observer.Yaw)) > 20, "promoted player has normal camera control");

                var eggModel = new AquariumModel(); eggModel.AddPlayer(1, "Alien", Team.Hider); eggModel.AddPlayer(2, "Diver", Team.Seeker);
                eggModel.SetRole(2, Team.Seeker, true); eggModel.StartRound();
                foreach (var player in eggModel.Players) { player.bot = false; if (player.team == Team.Hider) { player.alive = false; player.diedAt = eggModel.Time; } }
                eggModel.Eggs.Add(new RevivalEgg { id = 1, owner = 1, revivePlayer = 1, position = new Point(0, 4, 0), hatchAt = eggModel.Time + AquariumModel.HatchSeconds });
                eggModel.Tick(1f / 30);
                var waiting = new AquariumThirdPersonCamera(remoteObject.GetComponent<Camera>()); Pose(waiting, eggModel.Snapshot());
                Check(eggModel.Phase == RoundPhase.Playing && waiting.DeathReview, "all aliens dead with an egg still receive the death review");
                eggModel.Eggs.Clear(); eggModel.Tick(1f / 30); Pose(waiting, eggModel.Snapshot());
                Check(eggModel.Phase == RoundPhase.Finished && waiting.FreeView && !waiting.DeathReview && !waiting.SpectatingDiver, "true final death skips directly to free view");
                for (int i = 0; i < 610; i++) model.Tick(1f / 30);
                model.ReturnToLobby(); host.Prepare(model.Snapshot(), 1);
                Check(!host.Active && !host.FreeView && !host.FirstPerson && !host.DeathReview, "lobby clears all camera modes");
                CheckLookSwimming(hostObject.GetComponent<Camera>(), Team.Hider);
                CheckLookSwimming(remoteObject.GetComponent<Camera>(), Team.Seeker);
                Debug.Log("AQUARIUM_CAMERA_CHECKS_OK (" + checks + " checks)");
            }
            finally { UnityEngine.Object.DestroyImmediate(hostObject); UnityEngine.Object.DestroyImmediate(remoteObject); }
        }
    }
}

using System;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumCameraChecks
    {
        private static void Check(bool condition, string description)
        { if (!condition) throw new Exception("Aquarium camera check: " + description); }

        public static void Run()
        {
            var hostObject = new GameObject("Camera check host"); var remoteObject = new GameObject("Camera check client");
            try
            {
                var host = new AquariumThirdPersonCamera(hostObject.AddComponent<Camera>());
                var client = new AquariumThirdPersonCamera(remoteObject.AddComponent<Camera>());
                var model = new AquariumModel(); model.AddPlayer(1, "Host", Team.Hider); model.AddPlayer(2, "Client", Team.Seeker);
                model.AddPlayer(3, "Friend", Team.Hider); model.SetRole(2, Team.Seeker, true); model.SetRole(3, Team.Hider, true); model.StartRound();
                host.Prepare(model.Snapshot(), 1); client.Prepare(model.Snapshot(), 2);
                Check(host.FollowId == 1 && client.FollowId == 2, "each client follows its own player");
                Check(!host.FirstPerson && client.FirstPerson && !client.FreeView, "diver first person and alien third person");
                var forward = host.Movement(0, 1, 0); Check(forward.x > 0.99f && Math.Abs(forward.z) < 0.01f, "W follows camera heading");
                float clientYaw = client.Yaw; host.Look(30, 100, 0);
                Check(client.Yaw == clientYaw && host.Yaw != clientYaw, "rotating one view cannot rotate another");
                Check(host.Pitch >= -65 && host.Pitch <= 65, "pitch limit");
                Check(Math.Abs(host.Movement(1, 1, 1).Length - 1) < 0.001f, "normalized 3-axis input");
                model.Player(1).swallowedBy = 3; host.Prepare(model.Snapshot(), 1); Check(host.FollowId == 3, "follow carrier while swallowed");
                model.Player(1).swallowedBy = 0; host.Prepare(model.Snapshot(), 1); Check(host.FollowId == 1, "return after spit");
                float ownYaw = host.Yaw, ownPitch = host.Pitch;
                model.Player(1).alive = false; model.Player(2).facing = new Point(0, 0.6f, 0.8f); host.Prepare(model.Snapshot(), 1);
                Check(host.FollowId == 2 && host.FirstPerson && host.SpectatingDiver && !host.FreeView, "eliminated alien spectates living diver in first person");
                Check(Math.Abs(host.Yaw) < 0.001f && Math.Abs(host.Pitch + 36.8699f) < 0.01f, "spectator follows diver aim");
                host.Look(10, 10, 0); Check(Math.Abs(host.Yaw) < 0.001f, "spectator cannot change diver view");
                model.Player(2).facing = new Point(1, 0, 0); host.Prepare(model.Snapshot(), 1);
                Check(Math.Abs(host.Yaw - 90) < 0.001f && Math.Abs(host.Pitch) < 0.001f, "spectator tracks changing diver aim");
                model.Player(1).alive = true; host.Prepare(model.Snapshot(), 1);
                Check(host.FollowId == 1 && !host.FirstPerson && !host.SpectatingDiver && !host.FreeView, "revived alien resumes own third-person view");
                Check(host.Yaw == ownYaw && host.Pitch == ownPitch, "revival restores own camera angle");
                model.Player(1).alive = false; host.Prepare(model.Snapshot(), 1);
                model.RemovePlayer(2); host.Prepare(model.Snapshot(), 1);
                Check(host.FreeView && host.FollowId == 0 && !host.FirstPerson, "caught players enter free view on finish");
                var survivor = new AquariumThirdPersonCamera(remoteObject.GetComponent<Camera>()); survivor.Prepare(model.Snapshot(), 3);
                Check(survivor.FreeView && survivor.FollowId == 0, "surviving players enter free view on finish");
                var before = hostObject.transform.position; var playerBefore = model.Player(1).position;
                host.MoveFree(1, 1, 0, 0.1f);
                Check(Vector3.Distance(hostObject.transform.position, before) > 0.5f && (model.Player(1).position - playerBefore).Length == 0, "free camera moves without moving player");
                float otherYaw = survivor.Yaw; host.Look(10, 0, 0);
                Check(survivor.Yaw == otherYaw, "free view stays local");
                for (int i = 0; i < 610; i++) model.Tick(1f / 30);
                model.ReturnToLobby(); host.Prepare(model.Snapshot(), 1); Check(!host.Active, "leave follow mode in lobby");
                Check(!host.FreeView && !host.FirstPerson, "lobby clears camera modes");
                Debug.Log("AQUARIUM_CAMERA_CHECKS_OK (20 checks)");
            }
            finally { UnityEngine.Object.DestroyImmediate(hostObject); UnityEngine.Object.DestroyImmediate(remoteObject); }
        }
    }
}

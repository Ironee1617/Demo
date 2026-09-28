using System;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumCameraChecks
    {
        private static void Check(bool condition, string description)
        { if (!condition) throw new Exception("Third-person check: " + description); }

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
                var forward = host.Movement(0, 1, 0); Check(forward.x > 0.99f && Math.Abs(forward.z) < 0.01f, "W follows camera heading");
                float clientYaw = client.Yaw; host.Look(30, 100, 0);
                Check(client.Yaw == clientYaw && host.Yaw != clientYaw, "rotating one view cannot rotate another");
                Check(host.Pitch >= -65 && host.Pitch <= 65, "pitch limit");
                Check(Math.Abs(host.Movement(1, 1, 1).Length - 1) < 0.001f, "normalized 3-axis input");
                model.Player(1).swallowedBy = 3; host.Prepare(model.Snapshot(), 1); Check(host.FollowId == 3, "follow carrier while swallowed");
                model.Player(1).swallowedBy = 0; host.Prepare(model.Snapshot(), 1); Check(host.FollowId == 1, "return after spit");
                model.Player(1).alive = false; host.Prepare(model.Snapshot(), 1); Check(host.FollowId == 3, "spectate surviving teammate");
                model.ReturnToLobby(); host.Prepare(model.Snapshot(), 1); Check(!host.Active, "leave follow mode in lobby");
                Debug.Log("AQUARIUM_CAMERA_CHECKS_OK (9 checks)");
            }
            finally { UnityEngine.Object.DestroyImmediate(hostObject); UnityEngine.Object.DestroyImmediate(remoteObject); }
        }
    }
}

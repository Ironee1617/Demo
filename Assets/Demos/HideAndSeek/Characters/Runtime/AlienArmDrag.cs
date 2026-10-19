using UnityEngine;
namespace PrototypeLab.Aquarium
{
    // Arms trail against the real travel direction. Runs after the keyed pose is
    // sampled each frame and bends each joint toward the water drag. Deeper joints
    // read a more delayed velocity, so turns and stops ripple from root to tip.
    [DefaultExecutionOrder(2000)]
    public sealed class AlienArmDrag : MonoBehaviour
    {
        public float fullSpeed = 3.5f;
        public float rootDegrees = 20f, jointDegrees = 10f;
        private int Arms, Joints;   // discovered from the Arm{i}_{k} bones (octopus 6x5, cute monster 5x4)
        private AquariumRig rig;
        private Transform body;
        private Transform[,] arm;
        private Vector3[] delayed;
        private Vector3 last;
        private bool primed;
        private int[] streamKeys;

        private void Awake()
        {
            rig = GetComponent<AquariumRig>();
            foreach (var b in rig.skin.bones)
                if (b.name.StartsWith("Arm") && b.name.Length == 6) { Arms = Mathf.Max(Arms, b.name[3] - '0' + 1); Joints = Mathf.Max(Joints, b.name[5] - '0' + 1); }
            arm = new Transform[Mathf.Max(1, Arms), Mathf.Max(1, Joints)]; delayed = new Vector3[Mathf.Max(1, Joints)];
            foreach (var b in rig.skin.bones)
            {
                if (b.name == "Body") body = b;
                if (b.name.StartsWith("Arm") && b.name.Length == 6) arm[b.name[3] - '0', b.name[5] - '0'] = b;
            }
            streamKeys = new int[8];
            for (int i = 0; i < 8; i++) streamKeys[i] = rig.skin.sharedMesh.GetBlendShapeIndex("Streamline_" + ((i + 1) * 125).ToString("0000"));
        }
        private void OnEnable() { primed = false; }

        private float Streamlined()
        {
            float amount = 0;
            for (int i = 0; i < 8; i++) if (streamKeys[i] >= 0) amount += rig.skin.GetBlendShapeWeight(streamKeys[i]) * .01f * (i + 1) / 8f;
            return Mathf.Clamp01(amount);
        }

        private void LateUpdate()
        {
            if (body == null || Arms == 0 || arm[0, 0] == null) return;
            float dt = Time.deltaTime; if (dt <= 0) return;
            var position = transform.position;
            if (!primed) { last = position; primed = true; for (int k = 0; k < Joints; k++) delayed[k] = Vector3.zero; return; }
            var velocity = (position - last) / dt; last = position;
            if (velocity.sqrMagnitude > 400) velocity = Vector3.zero; // teleport / respawn
            for (int k = 0; k < Joints; k++) delayed[k] = Vector3.Lerp(delayed[k], velocity, 1 - Mathf.Exp(-dt * (8f - 1.3f * k)));
            // Streamlined arms are reshaped by blend shapes, so only whole-arm swings stay valid there.
            float bendable = 1 - Streamlined();
            for (int i = 0; i < Arms; i++)
                for (int k = 0; k < Joints; k++)
                {
                    var drag = -delayed[k]; float speed = drag.magnitude; if (speed < .02f) continue;
                    float degrees = Mathf.Clamp01(speed / fullSpeed) * (k == 0 ? rootDegrees : jointDegrees * bendable);
                    if (degrees < .01f) continue;
                    var b = arm[i, k]; if (b == null || (k > 0 && arm[i, k - 1] == null) || (k < Joints - 1 && arm[i, k + 1] == null)) continue;
                    var segment = k < Joints - 1 ? arm[i, k + 1].position - b.position : b.position - arm[i, k - 1].position;
                    var axis = Vector3.Cross(segment.normalized, drag / speed);
                    float sin = axis.magnitude; if (sin < 1e-4f) continue;
                    b.rotation = Quaternion.AngleAxis(degrees * sin, axis / sin) * b.rotation;
                }
        }
    }
}

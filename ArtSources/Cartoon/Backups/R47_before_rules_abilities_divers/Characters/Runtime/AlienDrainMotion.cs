using UnityEngine;
namespace PrototypeLab.Aquarium
{
    // R27: the alien's arms at a return drain, layered on the sampled pose (after AlienArmDrag, before the
    // spring bones). Each reaching arm follows a curve from its root to its target, stretching like rubber
    // when the target is further than the arm is long:
    //  * Screw : the nearest arm shoots out (a quick reach with a little overshoot), wraps the screw head and
    //            keeps wringing it round.
    //  * Lid   : the two arms nearest the lid's rim grip it on either side and heave.
    //  * Squeeze: every arm funnels into the hole ahead of the body.
    // AquariumCreature.PresentDrain sets the targets every frame the pose is sampled; nothing is applied on
    // a frame without a fresh pose, so the offsets never accumulate.
    [DefaultExecutionOrder(2050)]
    public sealed class AlienDrainMotion : MonoBehaviour
    {
        public enum Mode { None, Screw, Lid, Squeeze }
        private AquariumRig rig;
        private Transform[,] arm;
        private int arms, joints;
        private Mode mode;
        private Vector3 a, b, hole;
        private float weight, time, holeRadius, depth, reachAge;
        private int setFrame = -1, screwArm = -1, lidArmA = -1, lidArmB = -1;
        private readonly Vector3[] chain = new Vector3[8];
        // Rest joint spacing: the rubber stretch is undone every frame (clips may not key positions).
        private Vector3[,] restLocal;
        private bool stretched;

        private void Awake() { Discover(); }
        private void OnDisable() { if (arm != null) Unstretch(); }
        private void Discover()
        {
            if (rig != null) return;
            rig = GetComponent<AquariumRig>(); if (rig == null || rig.skin == null) return;
            foreach (var bone in rig.skin.bones)
                if (bone.name.StartsWith("Arm") && bone.name.Length == 6) { arms = Mathf.Max(arms, bone.name[3] - '0' + 1); joints = Mathf.Max(joints, bone.name[5] - '0' + 1); }
            arm = new Transform[Mathf.Max(1, arms), Mathf.Max(1, joints)];
            restLocal = new Vector3[Mathf.Max(1, arms), Mathf.Max(1, joints)];
            var bones = rig.skin.bones;
            for (int n = 0; n < bones.Length; n++)
            {
                var bone = bones[n];
                if (!bone.name.StartsWith("Arm") || bone.name.Length != 6) continue;
                int i = bone.name[3] - '0', k = bone.name[5] - '0';
                arm[i, k] = bone;
                restLocal[i, k] = rig.restPositions != null && n < rig.restPositions.Length ? rig.restPositions[n] : bone.localPosition;
            }
        }
        private void Unstretch()
        {
            if (!stretched) return;
            for (int i = 0; i < arms; i++) for (int k = 1; k < joints; k++) if (arm[i, k] != null) arm[i, k].localPosition = restLocal[i, k];
            stretched = false;
        }

        // reachAge: seconds since this reach began (the arm shoots out over the first ReachSeconds).
        public void Set(Mode mode, Vector3 a, Vector3 b, float weight, float time, float reachAge)
        {
            Discover();
            if (mode != this.mode) { screwArm = lidArmA = lidArmB = -1; }
            this.mode = mode; this.a = a; this.b = b; this.weight = Mathf.Clamp01(weight); this.time = time; this.reachAge = reachAge;
            setFrame = Time.frameCount;
        }
        public void SetSqueeze(Vector3 mouth, float radius, float depth, float weight, float time)
        {
            Discover();
            mode = Mode.Squeeze; hole = mouth; holeRadius = radius; this.depth = depth; this.weight = Mathf.Clamp01(weight); this.time = time;
            setFrame = Time.frameCount;
        }

        public const float ReachSeconds = .45f;
        private void LateUpdate()
        {
            if (arm == null) return;
            Unstretch();
            if (setFrame != Time.frameCount || weight <= .001f || mode == Mode.None || arms == 0 || joints < 3 || joints > chain.Length) return;
            if (mode == Mode.Screw)
            {
                if (screwArm < 0) screwArm = Nearest(a, -1);
                // A quick shot out from where the tip was, overshooting a touch before it settles on the head.
                float u = Mathf.Clamp01(reachAge / ReachSeconds);
                float shoot = 1 - Mathf.Pow(1 - u, 3) + .12f * Mathf.Sin(Mathf.PI * u) * (1 - u);
                var tip = arm[screwArm, joints - 1].position;
                var target = Vector3.LerpUnclamped(tip, a, shoot);
                // Wringing the head round: the tip twists back and forth as the screw turns.
                float twist = u >= 1 ? 55 * Mathf.Sin(time * 2 * Mathf.PI * 1.3f) : 0;
                Reach(screwArm, target, weight, twist, .18f);
            }
            else if (mode == Mode.Lid)
            {
                if (lidArmA < 0) { lidArmA = Nearest(a, -1); lidArmB = Nearest(b, lidArmA); }
                float u = Mathf.Clamp01(reachAge / ReachSeconds), shoot = 1 - Mathf.Pow(1 - u, 3);
                // Heaving: the grip tightens and the arms shorten in a slow pulling rhythm.
                float heave = .5f + .5f * Mathf.Sin(time * 2 * Mathf.PI * .8f);
                Reach(lidArmA, Vector3.Lerp(arm[lidArmA, joints - 1].position, a, shoot), weight, 15 * heave, .10f + .08f * heave);
                if (lidArmB >= 0) Reach(lidArmB, Vector3.Lerp(arm[lidArmB, joints - 1].position, b, shoot), weight, -15 * heave, .10f + .08f * heave);
            }
            else
            {
                // Every arm pours into the hole ahead of the body, each keeping its own side of the pipe.
                var centre = transform.position;
                for (int i = 0; i < arms; i++)
                {
                    if (arm[i, 0] == null) continue;
                    var side = arm[i, 0].position - centre; side.y = 0;
                    side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.forward;
                    float wobble = Mathf.Sin(time * 3.1f + i * 1.7f) * .05f;
                    var target = hole + side * (holeRadius * (.42f + wobble)) + Vector3.down * (.35f + depth * 1.05f + .06f * i);
                    Reach(i, target, weight, 20 * Mathf.Sin(time * 2.4f + i), .05f);
                }
            }
        }

        private int Nearest(Vector3 target, int skip)
        {
            int best = 0; float bestDistance = float.MaxValue;
            for (int i = 0; i < arms; i++)
            {
                if (i == skip || arm[i, 0] == null) continue;
                float d = (arm[i, 0].position - target).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        // Lay arm i along a curve from its root to target (stretching segment spacing when it is too short),
        // blended over the sampled pose by w; twist turns the tip about its own axis; bow lifts the middle.
        private void Reach(int i, Vector3 target, float w, float twist, float bow)
        {
            if (i < 0 || i >= arms) return;
            for (int k = 0; k < joints; k++) if (arm[i, k] == null) return;
            var root = arm[i, 0].position;
            float length = 0;
            for (int k = 1; k < joints; k++) length += Vector3.Distance(arm[i, k - 1].position, arm[i, k].position);
            float tipLength = Vector3.Distance(arm[i, joints - 2].position, arm[i, joints - 1].position) * .8f;
            var toTarget = target - root; float distance = toTarget.magnitude;
            if (distance < 1e-4f) return;
            // The joint chain ends a tip's length short of the target so the soft tip lands on it.
            float reach = Mathf.Max(.05f, distance - tipLength);
            var end = root + toTarget * (reach / distance);
            var start = (arm[i, 1].position - root).normalized;
            var control = root + start * (reach * .35f) + Vector3.up * (reach * bow);
            float total = 0; chain[0] = root;
            for (int k = 1; k < joints; k++) { total += Vector3.Distance(arm[i, k - 1].position, arm[i, k].position); chain[k] = Vector3.zero; }
            float along = 0;
            for (int k = 1; k < joints; k++)
            {
                along += Vector3.Distance(arm[i, k - 1].position, arm[i, k].position);
                float t = along / Mathf.Max(1e-4f, total);
                chain[k] = (1 - t) * (1 - t) * root + 2 * (1 - t) * t * control + t * t * end;
            }
            for (int k = 0; k < joints - 1; k++)
            {
                var bone = arm[i, k]; var child = arm[i, k + 1];
                var want = Vector3.Lerp(child.position, chain[k + 1], w);
                float current = Vector3.Distance(child.position, bone.position), wanted = Vector3.Distance(want, bone.position);
                // Rubber arm: the spacing to the next joint grows (never shrinks below its rest length).
                if (current > 1e-4f && wanted > current) { child.localPosition *= wanted / current; stretched = true; }
                var from = child.position - bone.position; var to = want - bone.position;
                if (from.sqrMagnitude > 1e-8f && to.sqrMagnitude > 1e-8f) bone.rotation = Quaternion.FromToRotation(from, to) * bone.rotation;
            }
            var last = arm[i, joints - 1];
            var axis = last.position - arm[i, joints - 2].position; var aim = target - last.position;
            if (axis.sqrMagnitude > 1e-8f && aim.sqrMagnitude > 1e-8f)
            {
                last.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(axis, aim), w) * last.rotation;
                if (Mathf.Abs(twist) > .01f) last.rotation = Quaternion.AngleAxis(twist * w, aim.normalized) * last.rotation;
            }
        }
    }
}

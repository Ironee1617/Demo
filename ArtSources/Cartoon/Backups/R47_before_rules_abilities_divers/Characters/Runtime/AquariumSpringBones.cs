using UnityEngine;
namespace PrototypeLab.Aquarium
{
    // Soft-body secondary motion layered on top of the keyed pose (runs after
    // AquariumCreature samples the clip and after AlienArmDrag).
    //  * Chains (head, antenna...) : each bone's tail is a damped spring that lags
    //    behind the animated tail, so the head sways and the antenna whips (follow through).
    //  * Jelly                    : a damped 3D wobble driven by the head's acceleration
    //    squashes/stretches the head (volume kept) and bulges its sides and crown.
    [DefaultExecutionOrder(2100)]
    public sealed class AquariumSpringBones : MonoBehaviour
    {
        [System.Serializable]
        public class Spring
        {
            public string bone;
            [Tooltip("Pull toward the animated pose (1/s^2)")] public float stiffness = 90;
            [Tooltip("Velocity damping (1/s)")] public float damping = 9;
            [Tooltip("Largest lag from the animated pose")] public float maxAngle = 30;
            [Tooltip("0 = animated pose only, 1 = full spring")] public float weight = 1;
            [System.NonSerialized] public Transform t, child;
            [System.NonSerialized] public Vector3 tailLocal, pos, vel;
            [System.NonSerialized] public float length;
            [System.NonSerialized] public Quaternion written, before;
        }
        public Spring[] springs =
        {
            new Spring { bone = "Head_Base", stiffness = 160, damping = 14, maxAngle = 10, weight = .6f },
            new Spring { bone = "Head_Mid",  stiffness = 120, damping = 11, maxAngle = 14 },
            new Spring { bone = "Head_Top",  stiffness = 95,  damping = 9,  maxAngle = 18 },
            new Spring { bone = "Antenna_1", stiffness = 70,  damping = 6,  maxAngle = 32 },
            new Spring { bone = "Antenna_2", stiffness = 45,  damping = 4.5f, maxAngle = 40 },
        };
        [Header("Jelly head")]
        public string squashBone = "Head_Mid";
        public string bulgeLeft = "Head_Bulge_L", bulgeRight = "Head_Bulge_R", bulgeBack = "Head_Bulge_Back", bulgeCrown = "Head_Bulge_Crown";
        public float jellyStiffness = 110, jellyDamping = 7.5f;
        [Tooltip("Wobble per m/s^2 of head acceleration")] public float jellyGain = .010f;
        public float maxSquash = .16f, maxBulge = .22f;
        [Range(0, 2)] public float intensity = 1;

        private Transform squash, left, right, back, crown;
        private Vector3 jelly, jellyVel, lastHead, lastHeadVel;
        private bool primed;
        // If nothing re-sampled the pose since last frame, undo our own offsets first
        // so they never accumulate (paused creature, skipped Present call, edit mode).
        private Transform[] scaled = new Transform[5];
        private Vector3[] scaleWritten = new Vector3[5], scaleBefore = new Vector3[5];
        private void Restore()
        {
            foreach (var s in springs) if (s.t != null && s.t.localRotation == s.written) s.t.localRotation = s.before;
            for (int i = 0; i < scaled.Length; i++) if (scaled[i] != null && scaled[i].localScale == scaleWritten[i]) scaled[i].localScale = scaleBefore[i];
        }
        private void Scale(int slot, Transform t, Vector3 factor)
        {
            if (t == null) return; scaled[slot] = t; scaleBefore[slot] = t.localScale;
            t.localScale = Vector3.Scale(t.localScale, factor); scaleWritten[slot] = t.localScale;
        }

        private void Awake()
        {
            var rig = GetComponent<AquariumRig>();
            var bones = rig != null ? rig.Bones : GetComponentsInChildren<Transform>();
            Transform Find(string n) { foreach (var b in bones) if (b != null && b.name == n) return b; return null; }
            foreach (var s in springs)
            {
                s.t = Find(s.bone); if (s.t == null) continue;
                // the tail is the first child bone (or a point along the bone's up axis)
                foreach (Transform c in s.t) { foreach (var b in bones) if (b == c) { s.child = c; break; } if (s.child != null) break; }
                s.tailLocal = s.child != null ? s.child.localPosition : Vector3.up * .1f;
                s.length = s.tailLocal.magnitude;
            }
            squash = Find(squashBone); left = Find(bulgeLeft); right = Find(bulgeRight); back = Find(bulgeBack); crown = Find(bulgeCrown);
        }
        private void OnEnable() { primed = false; }

        /// <summary>Extra squash impulse (e.g. a gulp or an impact). Positive stretches, negative squashes.</summary>
        public void Kick(Vector3 localImpulse) { jellyVel += localImpulse; }

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, .05f); if (dt <= 0 || intensity <= 0) return;
            if (!primed) { Prime(); return; }
            Restore();
            int steps = Mathf.CeilToInt(dt / .011f); float h = dt / steps;
            // ---- chains (parent first; each bone sees its parent's corrected pose)
            foreach (var s in springs)
            {
                if (s.t == null) continue;
                Vector3 root = s.t.position, target = s.t.TransformPoint(s.tailLocal);
                if ((s.pos - target).sqrMagnitude > 4) { s.pos = target; s.vel = Vector3.zero; }
                for (int i = 0; i < steps; i++)
                {
                    s.vel += (s.stiffness * (target - s.pos) - s.damping * s.vel) * h;
                    s.pos += s.vel * h;
                }
                Vector3 want = target - root, have = s.pos - root;
                if (have.sqrMagnitude < 1e-10f || want.sqrMagnitude < 1e-10f) continue;
                float angle = Vector3.Angle(want, have);
                if (angle > s.maxAngle) { have = Vector3.Slerp(want, have, s.maxAngle / angle); s.pos = root + have.normalized * want.magnitude; }
                s.pos = root + have.normalized * want.magnitude;           // keep the bone length
                var turn = Quaternion.FromToRotation(want, have);
                s.before = s.t.localRotation;
                s.t.rotation = Quaternion.Slerp(Quaternion.identity, turn, s.weight * Mathf.Clamp01(intensity)) * s.t.rotation;
                s.written = s.t.localRotation;
            }
            // ---- jelly wobble driven by the head's own acceleration (in head space)
            if (squash == null) return;
            Vector3 headPos = squash.position, headVel = (headPos - lastHead) / dt;
            if (headVel.sqrMagnitude > 900) headVel = lastHeadVel;          // teleport
            Vector3 accel = (headVel - lastHeadVel) / dt; lastHead = headPos; lastHeadVel = headVel;
            Vector3 local = squash.parent != null ? squash.parent.InverseTransformDirection(accel) : accel;
            Vector3 drive = -local * jellyGain * intensity / Mathf.Max(.0001f, squash.lossyScale.y);
            for (int i = 0; i < steps; i++)
            {
                jellyVel += (jellyStiffness * (drive - jelly) - jellyDamping * jellyVel) * h;
                jelly += jellyVel * h;
            }
            jelly = Vector3.ClampMagnitude(jelly, maxBulge);
            float st = Mathf.Clamp(jelly.y, -maxSquash, maxSquash), side = 1 / Mathf.Sqrt(1 + st);
            Scale(0, squash, new Vector3(side, 1 + st, side));
            float bx = Mathf.Clamp(jelly.x, -maxBulge, maxBulge), bz = Mathf.Clamp(jelly.z, -maxBulge, maxBulge);
            Scale(1, left, Vector3.one * (1 + Mathf.Max(0, -bx) * .9f - st * .4f));
            Scale(2, right, Vector3.one * (1 + Mathf.Max(0, bx) * .9f - st * .4f));
            Scale(3, back, Vector3.one * (1 + Mathf.Max(0, -bz) * .9f - st * .3f));
            Scale(4, crown, Vector3.one * (1 + st * .5f));
        }
        private void Prime()
        {
            foreach (var s in springs) if (s.t != null) { s.pos = s.t.TransformPoint(s.tailLocal); s.vel = Vector3.zero; s.written = new Quaternion(0, 0, 0, 0); }
            for (int i = 0; i < scaled.Length; i++) scaled[i] = null;
            if (squash != null) { lastHead = squash.position; lastHeadVel = Vector3.zero; }
            jelly = jellyVel = Vector3.zero; primed = true;
        }
    }
}

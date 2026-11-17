using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R77: a harpooned alien (or a starved body) reeled in on the spear wobbles like jelly. Presentation only:
    // the skewer pose (R49, the body's middle on the drawn shaft) is kept; this layers on the creature transform
    // under the actor root, about the body's middle, so the shaft still runs through it.
    //  * Squash and stretch along the spear (the actor root's x axis after SkewerPose), volume kept: the body
    //    stretches with the reel speed, squashes on the hit and again when it lands at the diver, then wobbles out.
    //  * Swing about the shaft: the body lags behind sideways moves and swings back past them (follow through).
    //  * The tentacles trail further (AlienArmDrag pushed harder while skewered) and the head jelly gets a kick
    //    (AquariumSpringBones) on the hit and on the landing (overlapping action).
    public sealed class AquariumSkewerJelly
    {
        // Spring tunings: the stretch is bouncy (damping ratio about .33), the swing a slower, softer pendulum.
        private const float StretchK = 150f, StretchC = 8f, SwingK = 55f, SwingC = 4.5f;
        private const float StretchPerSpeed = .045f, MaxStretch = .3f;
        private const float HitKick = -3.2f, LandKick = -3.4f;   // squash (1/s)
        private const float SwingPerSpeed = 7f, MaxSwing = 24f;  // degrees

        // Off = the R49 rigid skewer (editor before/after reviews).
        public static bool Enabled = true;
        public int frame = -1;
        private int shot = int.MinValue;
        private bool primed, landed, wrote;
        private float stretch, stretchVel, swing, swingVel;
        private Vector3 lastPivot, velocity;
        private Transform body;
        private Vector3 basePos, writtenPos, baseScale, writtenScale;
        private Quaternion baseRot, writtenRot;
        private AlienArmDrag drag;
        private float dragRoot, dragJoint, dragFull;
        private AquariumSpringBones springs;

        public float Stretch { get { return stretch; } }
        public float Swing { get { return swing; } }

        public void Present(Transform root, Transform creature, float lift, bool seated, float dt, int shotId)
        {
            frame = Time.frameCount;
            if (creature != body) { Release(); body = creature; }
            if (body == null || root == null) return;
            // Undo last frame's layer where nothing else re-posed the creature since (Present resets rotation and
            // scale every frame, but not the local position).
            if (wrote)
            {
                if (body.localPosition == writtenPos) body.localPosition = basePos;
                if (body.localRotation == writtenRot) body.localRotation = baseRot;
                if (body.localScale == writtenScale) body.localScale = baseScale;
                wrote = false;
            }
            basePos = body.localPosition; baseRot = body.localRotation; baseScale = body.localScale;
            FindRig();

            var pivotLocal = new Vector3(0, lift, 0);
            var pivot = root.TransformPoint(pivotLocal);
            dt = Mathf.Clamp(dt, 0, .05f);
            if (!primed || shotId != shot)
            {
                shot = shotId; primed = true; landed = seated;
                lastPivot = pivot; velocity = Vector3.zero;
                stretch = 0; swing = 0;
                stretchVel = HitKick;                                   // the hit: a squash that springs back
                swingVel = ((shotId & 1) == 0 ? 1 : -1) * 70f;
                Kick(-1.1f);
            }
            if (dt <= 0) { Apply(pivotLocal); return; }
            var measured = (pivot - lastPivot) / dt; lastPivot = pivot;
            if (measured.sqrMagnitude > 3600) measured = velocity;      // a snap (stow start, respawned actor)
            velocity = Vector3.Lerp(velocity, measured, 1 - Mathf.Exp(-dt * 18));
            if (seated && !landed)
            {
                landed = true;                                          // arrives at the diver: squash, swing
                stretchVel += LandKick; swingVel += ((shotId & 1) == 0 ? -1 : 1) * 90f;
                Kick(-1.3f);
            }
            float along = Mathf.Abs(Vector3.Dot(velocity, root.right));
            float side = Vector3.Dot(velocity, root.forward);
            float stretchTarget = Mathf.Clamp(along * StretchPerSpeed, 0, MaxStretch);
            float swingTarget = Mathf.Clamp(-side * SwingPerSpeed, -MaxSwing, MaxSwing);
            int steps = Mathf.CeilToInt(dt / .008f); float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                stretchVel += (StretchK * (stretchTarget - stretch) - StretchC * stretchVel) * h; stretch += stretchVel * h;
                swingVel += (SwingK * (swingTarget - swing) - SwingC * swingVel) * h; swing += swingVel * h;
            }
            stretch = Mathf.Clamp(stretch, -.32f, .42f); swing = Mathf.Clamp(swing, -32f, 32f);
            Apply(pivotLocal);
        }

        // Scale along the spear (root x), turn about it, both about the body's middle on the shaft.
        private void Apply(Vector3 pivotLocal)
        {
            float along = 1 + stretch, across = 1 / Mathf.Sqrt(along);
            var squash = new Vector3(along, across, across);
            var turn = Quaternion.AngleAxis(swing, Vector3.right);
            body.localPosition = pivotLocal + turn * Vector3.Scale(squash, basePos - pivotLocal);
            body.localRotation = turn * baseRot;
            body.localScale = Vector3.Scale(squash, baseScale);
            writtenPos = body.localPosition; writtenRot = body.localRotation; writtenScale = body.localScale; wrote = true;
        }

        private void FindRig()
        {
            if (drag == null || !drag.isActiveAndEnabled)
            {
                RestoreDrag();
                drag = body.GetComponentInChildren<AlienArmDrag>();
                if (drag != null)
                {
                    dragRoot = drag.rootDegrees; dragJoint = drag.jointDegrees; dragFull = drag.fullSpeed;
                    drag.rootDegrees = dragRoot * 1.6f; drag.jointDegrees = dragJoint * 1.8f; drag.fullSpeed = dragFull * .6f;
                }
            }
            if (springs == null || !springs.isActiveAndEnabled) springs = body.GetComponentInChildren<AquariumSpringBones>();
        }
        private void Kick(float squash) { if (springs != null) springs.Kick(new Vector3(0, squash, 0)); }
        private void RestoreDrag()
        {
            if (drag != null) { drag.rootDegrees = dragRoot; drag.jointDegrees = dragJoint; drag.fullSpeed = dragFull; }
            drag = null;
        }

        // No longer skewered: put the creature transform and the arm drag back.
        public void Release()
        {
            if (wrote && body != null)
            {
                if (body.localPosition == writtenPos) body.localPosition = basePos;
                if (body.localRotation == writtenRot) body.localRotation = baseRot;
                if (body.localScale == writtenScale) body.localScale = baseScale;
            }
            wrote = false; primed = false; springs = null;
            RestoreDrag();
            body = null; shot = int.MinValue;
        }
    }
}

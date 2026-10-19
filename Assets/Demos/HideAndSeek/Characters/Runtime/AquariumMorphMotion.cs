using UnityEngine;
namespace PrototypeLab.Aquarium
{
    // Keyed body motion layered over the alien <-> fish shape interpolation.
    // `leave` plays while this form folds toward the shared midpoint (u 0..0.5),
    // `arrive` while it unfolds from it (u 0..0.5 of its own half). SetCartoonFold
    // neutralises the skeleton at the midpoint, so both sides still meet exactly.
    public sealed class AquariumMorphMotion : MonoBehaviour
    {
        public AnimationClip leave, arrive;
        private Vector3[] positions, scales;
        private Quaternion[] rotations;

        public void Overlay(AquariumRig rig, bool leaving, float u)
        {
            var clip = leaving ? leave : arrive;
            if (clip == null) return;
            float half = Mathf.Clamp01(leaving ? u * 2 : (u - .5f) * 2);
            float weight = leaving ? Mathf.SmoothStep(0, 1, half / .12f) : 1 - Mathf.SmoothStep(0, 1, (half - .86f) / .14f);
            var bones = rig.Bones;
            if (positions == null || positions.Length != bones.Length) { positions = new Vector3[bones.Length]; scales = new Vector3[bones.Length]; rotations = new Quaternion[bones.Length]; }
            for (int i = 0; i < bones.Length; i++) { positions[i] = bones[i].localPosition; rotations[i] = bones[i].localRotation; scales[i] = bones[i].localScale; }
            clip.SampleAnimation(rig.gameObject, half * clip.length);
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                b.localPosition = Vector3.Lerp(positions[i], b.localPosition, weight);
                b.localRotation = Quaternion.Slerp(rotations[i], b.localRotation, weight);
                b.localScale = Vector3.Lerp(scales[i], b.localScale, weight);
            }
        }
    }
}

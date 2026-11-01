using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R38 (2026-10-29): the aliens have no eyelid mesh; their lids are drawn by the cartoon shader (Aquarium/Cartoon
    // Character: _LidOn, _Blink) over the eye: green and yellow rest on a lower lid painted in their atlas, blue and
    // red on an upper lid the shader draws, and a blink slides the lids shut. The rig's blink stays the source of
    // truth: AquariumRig.AutoBlink (and the QA tools) set the same twelve Blink shapes that once closed the 3D lids;
    // this reads how far they have closed and hands it to the shader. It runs after everything else each frame
    // because AquariumCreature rewrites the renderer's property block (tint, morph) during Update. While dead
    // (AquariumDeadEyes swaps in its own mesh with a blank white eye and the X) the painted lids are switched off.
    [DisallowMultipleComponent, DefaultExecutionOrder(2300)]
    public sealed class AquariumPaintedLids : MonoBehaviour
    {
        public SkinnedMeshRenderer skin;
        // The mesh the lids are painted for (any other mesh on the renderer, e.g. the dead eyes', gets none).
        public Mesh restMesh;
        private static readonly int BlinkId = Shader.PropertyToID("_Blink"), LidOnId = Shader.PropertyToID("_LidOn");
        private MaterialPropertyBlock block;
        private Mesh keyedMesh;
        private int[] keys;

        // 0 open .. 1 shut: the blink shapes are twelve evenly spaced steps (BlinkStep_01, _02, Blink_25, ... Blink_100),
        // a blink cross-fades two neighbours, so the weighted step number is the closure.
        public float Closure()
        {
            if (skin == null || skin.sharedMesh == null) return 0;
            var mesh = skin.sharedMesh;
            if (mesh != keyedMesh)
            {
                keyedMesh = mesh; keys = new int[12];
                for (int i = 1; i <= 12; i++) keys[i - 1] = mesh.GetBlendShapeIndex(i % 3 == 0 ? "Blink_" + (i * 100 / 12) : "BlinkStep_" + i.ToString("00"));
            }
            float closure = 0;
            for (int i = 0; i < 12; i++) if (keys[i] >= 0) closure += skin.GetBlendShapeWeight(keys[i]) / 100 * (i + 1) / 12;
            return Mathf.Clamp01(closure);
        }

        public void Apply()
        {
            if (skin == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            bool painted = restMesh == null || skin.sharedMesh == restMesh;
            skin.GetPropertyBlock(block); block.SetFloat(BlinkId, painted ? Closure() : 0); block.SetFloat(LidOnId, painted ? 1 : 0); skin.SetPropertyBlock(block);
        }

        private void Reset() { skin = GetComponentInChildren<SkinnedMeshRenderer>(); }
        private void LateUpdate() { Apply(); }
    }
}

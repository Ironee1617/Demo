using UnityEngine;

namespace PrototypeLab.Aquarium
{
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class AquariumGalleryCamera : MonoBehaviour
    {
        private Material finish;
        // R43: 0..1 murk of green alien blood in the local diver's water (set by AquariumGame each frame):
        // the fog thickens and turns dark green, then clears when it drops back to 0.
        public static float Murk;
        // R47: murk of a purple alien's ink bomb around a diver's camera (set by AquariumView each frame).
        public static float InkBombMurk;
        public static readonly Color MurkColor = new Color(.025f, .12f, .035f);
        private static bool murky;
        private static Color clearFog;
        private void OnPreCull()
        {
            float submerged = Mathf.Clamp01((AquariumModel.WaterHeight+.15f - transform.position.y) * 2);
            if (AquariumLockerRoom.Contains(transform.position)) submerged = 0;   // R60: the dry changing room
            float scale=AquariumMapOutline.HorizontalScale;
            RenderSettings.fogDensity = Mathf.Lerp(.003f/scale, .018f/Mathf.Sqrt(scale), submerged);
            var background = Color.Lerp(AquariumEnvironment.C(0x15232c), AquariumEnvironment.C(0x11515f), submerged);
            float murk = Application.isPlaying ? Mathf.Clamp01(Mathf.Max(Murk, InkBombMurk)) : 0;
            if (murk > .001f || murky)
            {
                if (!murky) { clearFog = RenderSettings.fogColor; murky = true; }
                RenderSettings.fogColor = Color.Lerp(clearFog, MurkColor, .95f * murk);
                RenderSettings.fogDensity *= 1 + 16 * murk;
                background = Color.Lerp(background, MurkColor, .95f * murk);
                if (murk <= .001f) { RenderSettings.fogColor = clearFog; murky = false; }
            }
            GetComponent<Camera>().backgroundColor = background;
        }
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var shader = Shader.Find("Aquarium/Gallery Finish");
            if (shader == null || !shader.isSupported) { Graphics.Blit(source, destination); return; }
            if (finish == null) finish = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var a = RenderTexture.GetTemporary(Mathf.Max(1, source.width / 4), Mathf.Max(1, source.height / 4), 0, source.format);
            var b = RenderTexture.GetTemporary(a.width, a.height, 0, source.format);
            a.filterMode = b.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, a, finish, 0);
            Graphics.Blit(a, b, finish, 1);
            Graphics.Blit(b, a, finish, 1);
            finish.SetTexture("_Bloom", a);
            finish.SetFloat("_Underwater", AquariumLockerRoom.Contains(transform.position) ? 0 : Mathf.Clamp01((AquariumModel.WaterHeight+.15f - transform.position.y) * 2));
            Graphics.Blit(source, destination, finish, 2);
            RenderTexture.ReleaseTemporary(a); RenderTexture.ReleaseTemporary(b);
        }
        private void OnDisable()
        {
            if (finish == null) return;
            if (Application.isPlaying) Destroy(finish); else DestroyImmediate(finish);
        }
    }
}

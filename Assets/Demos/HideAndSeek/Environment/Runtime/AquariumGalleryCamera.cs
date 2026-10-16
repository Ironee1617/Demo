using UnityEngine;

namespace PrototypeLab.Aquarium
{
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class AquariumGalleryCamera : MonoBehaviour
    {
        private Material finish;
        private void OnPreCull()
        {
            float submerged = Mathf.Clamp01((AquariumModel.WaterHeight+.15f - transform.position.y) * 2);
            float scale=AquariumMapOutline.HorizontalScale;
            RenderSettings.fogDensity = Mathf.Lerp(.003f/scale, .018f/Mathf.Sqrt(scale), submerged);
            GetComponent<Camera>().backgroundColor = Color.Lerp(AquariumEnvironment.C(0x15232c), AquariumEnvironment.C(0x11515f), submerged);
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
            finish.SetFloat("_Underwater", Mathf.Clamp01((AquariumModel.WaterHeight+.15f - transform.position.y) * 2));
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

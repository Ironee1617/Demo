using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrototypeLab.Aquarium.Editor
{
    // R57: render a baked map prefab from fixed viewpoints in a preview scene (the open scene is never touched).
    public static class AquariumMapQA
    {
        public static string Render(string resource, string output, string[] names, Vector3[] from, Vector3[] at, float[] fov, int width = 1600, int height = 1000)
        {
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.NewPreviewScene();
            var log = new StringBuilder();
            var oldFog = RenderSettings.fog; var oldMode = RenderSettings.ambientMode; var oldSky = RenderSettings.ambientSkyColor; var oldEq = RenderSettings.ambientEquatorColor;
            var oldGround = RenderSettings.ambientGroundColor; var oldFogColor = RenderSettings.fogColor; var oldFogMode = RenderSettings.fogMode; var oldDensity = RenderSettings.fogDensity;
            var oldSun = RenderSettings.sun; var oldShadow = QualitySettings.shadowDistance;
            RenderTexture rt = null; Texture2D tex = null;
            try
            {
                var root = new GameObject("Map QA preview"); SceneManager.MoveGameObjectToScene(root, scene);
                var prefab = Resources.Load<GameObject>(resource);
                if (prefab == null) throw new Exception("missing prefab " + resource);
                UnityEngine.Object.Instantiate(prefab, root.transform);
                var sun = new GameObject("Gallery skylight").AddComponent<Light>(); sun.transform.SetParent(root.transform);
                var fillGo = new GameObject("QA cool fill"); fillGo.transform.SetParent(root.transform); var fill = fillGo.AddComponent<Light>();
                sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(57, -34, 0); sun.color = AquariumEnvironment.C(0xffe6bd); sun.intensity = .9f; sun.shadows = LightShadows.Soft;
                sun.shadowStrength = .68f; sun.shadowBias = .035f; sun.shadowNormalBias = .16f;
                fill.type = LightType.Directional; fill.transform.rotation = Quaternion.Euler(32, 135, 0); fill.color = AquariumEnvironment.C(0x83c8d5); fill.intensity = .2f; fill.shadows = LightShadows.None;
                RenderSettings.sun = sun;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = AquariumEnvironment.C(0x788b96); RenderSettings.ambientEquatorColor = AquariumEnvironment.C(0x425b68); RenderSettings.ambientGroundColor = AquariumEnvironment.C(0x172e39);
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogColor = AquariumEnvironment.C(0x177d8d);
                QualitySettings.shadowDistance = Mathf.Max(75, 65 * AquariumMapOutline.HorizontalScale);
                var cam = new GameObject("QA camera").AddComponent<Camera>(); cam.transform.SetParent(root.transform); cam.scene = scene;
                AquariumEnvironment.ConfigureCamera(cam);
                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; rt.Create(); cam.targetTexture = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                Shader.SetGlobalFloat("_AquariumPreviewTime", 7); Shader.SetGlobalFloat("_AquariumPreviewTimeEnabled", 1);
                var finish = cam.GetComponent<AquariumGalleryCamera>();
                for (int i = 0; i < names.Length; i++)
                {
                    cam.fieldOfView = fov[i]; cam.transform.position = from[i]; cam.transform.LookAt(at[i]);
                    var pre = typeof(AquariumGalleryCamera).GetMethod("OnPreCull", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (finish != null && pre != null) pre.Invoke(finish, null);
                    cam.Render();
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply(); RenderTexture.active = prev;
                    File.WriteAllBytes(Path.Combine(output, names[i] + ".png"), tex.EncodeToPNG());
                    log.Append(names[i]).Append(' ');
                }
            }
            finally
            {
                Shader.SetGlobalFloat("_AquariumPreviewTimeEnabled", 0);
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                EditorSceneManager.ClosePreviewScene(scene);
                RenderSettings.fog = oldFog; RenderSettings.ambientMode = oldMode; RenderSettings.ambientSkyColor = oldSky; RenderSettings.ambientEquatorColor = oldEq;
                RenderSettings.ambientGroundColor = oldGround; RenderSettings.fogColor = oldFogColor; RenderSettings.fogMode = oldFogMode; RenderSettings.fogDensity = oldDensity;
                RenderSettings.sun = oldSun; QualitySettings.shadowDistance = oldShadow;
            }
            return log.ToString();
        }

        // The fixed old-map viewpoints used to compare before and after the R57 map refactor.
        public static string BasicBaseline(string output)
        {
            var names = new[] { "overview", "underwater", "walkway-glass", "dive-entry", "visitor-gallery", "sand" };
            Func<float, float, float, Vector3> L = (x, y, z) => AquariumEnvironment.LayoutPosition(x, y, z);
            var from = new[] { new Vector3(0, 8.3f + 36.7f * 2.4f, -48), new Vector3(11.5f, 3.0f, -8), L(-.2f, 2.8f, .8f), new Vector3(-21, 20, 30), new Vector3(30, 3, -22), new Vector3(10, 1.65f, -2) };
            var at = new[] { new Vector3(0, 5.5f, 0), new Vector3(0, 6.8f, 4), L(-5.5f, 3.5f, 3.5f), new Vector3(-12, 12, 20), new Vector3(20, 4, -12), new Vector3(3, 1.0f, -9) };
            var fov = new[] { 43f, 74f, 64f, 55f, 70f, 64f };
            return Render(AquariumEnvironment.ResourcePath, output, names, from, at, fov);
        }
    }
}

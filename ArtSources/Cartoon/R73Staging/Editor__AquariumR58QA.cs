using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrototypeLab.Aquarium.Editor
{
    // R58: renders for the twin tanks' rocks and dressing (preview scene only, the open scene is never touched):
    // the twin gallery with a game's scenery seed applied, a lineup of the eight rock shapes, and the lobby's map
    // thumbnails (saved into Resources/AquariumMaps).
    public static class AquariumR58QA
    {
        public const string ThumbnailFolder = "Assets/Demos/HideAndSeek/Resources/AquariumMaps";

        internal sealed class View { public string name; public Vector3 from, at; public float fov; }

        // Renders the views of a scene that build(root) sets up (it returns something to dispose afterwards).
        internal static string Render(string output, Func<Transform, IDisposable> build, List<View> views, int width, int height, Color? background = null)
        {
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.NewPreviewScene();
            var log = new StringBuilder();
            var oldFog = RenderSettings.fog; var oldMode = RenderSettings.ambientMode; var oldSky = RenderSettings.ambientSkyColor; var oldEq = RenderSettings.ambientEquatorColor;
            var oldGround = RenderSettings.ambientGroundColor; var oldFogColor = RenderSettings.fogColor; var oldFogMode = RenderSettings.fogMode; var oldDensity = RenderSettings.fogDensity;
            var oldSun = RenderSettings.sun; var oldShadow = QualitySettings.shadowDistance;
            RenderTexture rt = null; Texture2D tex = null; IDisposable built = null;
            try
            {
                var root = new GameObject("R58 QA preview"); SceneManager.MoveGameObjectToScene(root, scene);
                built = build(root.transform);
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
                if (background.HasValue) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = background.Value; }
                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; rt.Create(); cam.targetTexture = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                Shader.SetGlobalFloat("_AquariumPreviewTime", 7); Shader.SetGlobalFloat("_AquariumPreviewTimeEnabled", 1);
                var finish = cam.GetComponent<AquariumGalleryCamera>();
                var pre = typeof(AquariumGalleryCamera).GetMethod("OnPreCull", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                foreach (var v in views)
                {
                    cam.fieldOfView = v.fov; cam.transform.position = v.from; cam.transform.LookAt(v.at);
                    if (finish != null && pre != null) pre.Invoke(finish, null);
                    cam.Render();
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply(); RenderTexture.active = prev;
                    File.WriteAllBytes(Path.Combine(output, v.name + ".png"), tex.EncodeToPNG());
                    log.Append(v.name).Append(' ');
                }
                cam.targetTexture = null; // R59: released below
            }
            finally
            {
                if (built != null) built.Dispose();
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

        private sealed class Restore : IDisposable
        {
            public AquariumSceneryPresentation presentation; public int map, seed;
            public void Dispose()
            {
                if (presentation != null) presentation.Dispose();
                AquariumMaps.Select(map); AquariumSceneryLayout.Apply(seed);
            }
        }
        // The map's baked gallery with the layout of this scenery seed.
        private static Func<Transform, IDisposable> Gallery(int map, int seed)
        {
            return root =>
            {
                var undo = new Restore { map = AquariumMaps.Current, seed = AquariumSceneryLayout.Current.Seed };
                var prefab = Resources.Load<GameObject>(AquariumEnvironment.ResourcePathOf(map));
                // R73: a map without a baked prefab yet (the grotto before its bake) is built here.
                var environment = prefab != null ? UnityEngine.Object.Instantiate(prefab, root).GetComponent<AquariumEnvironmentRoot>()
                    : AquariumEnvironment.CreateTwin(root, map);
                AquariumMaps.Select(map);
                undo.presentation = new AquariumSceneryPresentation(environment); undo.presentation.Sync(seed);
                return undo;
            };
        }

        public static string RenderGallery(int map, int seed, string output, string spec, int width = 1600, int height = 1000)
        {
            return Render(output, Gallery(map, seed), Views(spec), width, height);
        }
        // spec: name:fx,fy,fz>ax,ay,az@fov;...  (R59: shared with AquariumR59QA)
        internal static List<View> Views(string spec)
        {
            var views = new List<View>();
            foreach (var part in spec.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var bits = part.Split(':'); var rest = bits[1].Split('>'); var tail = rest[1].Split('@');
                Func<string, Vector3> vec = s => { var n = s.Split(','); return new Vector3(float.Parse(n[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(n[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(n[2], System.Globalization.CultureInfo.InvariantCulture)); };
                views.Add(new View { name = bits[0], from = vec(rest[0]), at = vec(tail[0]), fov = tail.Length > 1 ? float.Parse(tail[1], System.Globalization.CultureInfo.InvariantCulture) : 60 });
            }
            return views;
        }

        // The eight rock shapes in a row on a sand slab, both variants (front and back row).
        public static string RenderRockLineup(string output, int width = 1800, int height = 900)
        {
            Func<Transform, IDisposable> build = root =>
            {
                var rockMaterial = AquariumEnvironment.TwinRockMaterial();
                var sand = GameObject.CreatePrimitive(PrimitiveType.Plane); sand.transform.SetParent(root, false);
                sand.transform.localScale = new Vector3(8, 1, 4); sand.transform.position = new Vector3(0, 0, 2);
                var sandMat = new Material(Shader.Find("Aquarium/Natural Granular Sand")) { hideFlags = HideFlags.HideAndDontSave };
                sandMat.SetTexture("_MainTex", Resources.Load<Texture2D>("Aquarium/Textures/SandGrainAlbedo")); sandMat.SetTexture("_NormalTex", Resources.Load<Texture2D>("Aquarium/Textures/SandGrainNormal"));
                sandMat.SetTexture("_ARM", Resources.Load<Texture2D>("Aquarium/Textures/SandGrainARM")); sandMat.SetFloat("_WorldScale", .48f); sandMat.SetColor("_Color", AquariumEnvironment.C(0xe8d6b6));
                sand.GetComponent<Renderer>().sharedMaterial = sandMat;
                for (int variant = 0; variant < 2; variant++)
                    for (int kind = 0; kind < AquariumRockShapes.SmallCount; kind++)
                    {
                        
                        var go = new GameObject(AquariumRockShapes.Names[kind]); go.transform.SetParent(root, false);
                        go.transform.position = new Vector3(-24.5f + kind * 7f, -.12f, variant * 7.5f); go.transform.rotation = Quaternion.Euler(0, 20 + 35 * variant, 0);
                        go.AddComponent<MeshFilter>().sharedMesh = AquariumEnvironment.TwinRockMesh(kind, variant); go.AddComponent<MeshRenderer>().sharedMaterial = rockMaterial;
                    }
                return null;
            };
            var views = new List<View> { new View { name = "lineup", from = new Vector3(0, 7.5f, -21), at = new Vector3(0, 1.4f, 3.5f), fov = 62 },
                new View { name = "lineup_close_left", from = new Vector3(-14, 3.2f, -8.5f), at = new Vector3(-14, 1.5f, 2), fov = 60 },
                new View { name = "lineup_close_right", from = new Vector3(14, 3.2f, -8.5f), at = new Vector3(14, 1.5f, 2), fov = 60 } };
            return Render(output, build, views, width, height, new Color(.06f, .3f, .36f));
        }

        // R73: the grotto's lobby thumbnail (Map2), the same framing as the twin tanks'.
        public static string RenderGrottoThumbnail(int seed)
        {
            string temp = "D:/Demo/Temp/ClaudeQA/R73_thumbs";
            var gallery = Gallery(AquariumMaps.Grotto, seed);
            Func<Transform, IDisposable> cutaway = root =>
            {
                var undo = gallery(root);
                // seen from above: no skylight, shafts, stalactites or vault crystals over the tanks
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    if (renderer.name.StartsWith("31") || renderer.name.StartsWith("32") || renderer.name.StartsWith("34") || renderer.name.StartsWith("35") || renderer.name.StartsWith("33")) renderer.enabled = false;
                return undo;
            };
            string log = Render(temp, cutaway, new List<View> { new View { name = "Map2", from = new Vector3(-2, 104, -62), at = new Vector3(-4, 2, 2), fov = 39 } }, 960, 600);
            Directory.CreateDirectory(ThumbnailFolder);
            string path = ThumbnailFolder + "/Map2.png";
            File.Copy(temp + "/Map2.png", path, true);
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false; importer.sRGBTexture = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.maxTextureSize = 1024; importer.alphaSource = TextureImporterAlphaSource.None;
                importer.SaveAndReimport();
            }
            return log;
        }

        // Top-down-ish overviews of both maps, saved as the lobby's map thumbnails.
        public static string RenderThumbnails(int twinSeed)
        {
            string temp = "D:/Demo/Temp/ClaudeQA/R58_thumbs";
            var log = new StringBuilder();
            log.Append(Render(temp, Gallery(AquariumMaps.Basic, 0), new List<View> { new View { name = "Map0", from = new Vector3(0, 108, -58), at = new Vector3(0, 3, 2), fov = 29 } }, 960, 600));
            log.Append(Render(temp, Gallery(AquariumMaps.Twin, twinSeed), new List<View> { new View { name = "Map1", from = new Vector3(0, 104, -62), at = new Vector3(0, 2, 2), fov = 39 } }, 960, 600));
            Directory.CreateDirectory(ThumbnailFolder);
            for (int map = 0; map < 2; map++)
            {
                string path = ThumbnailFolder + "/Map" + map + ".png";
                File.Copy(temp + "/Map" + map + ".png", path, true);
                AssetDatabase.ImportAsset(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false; importer.sRGBTexture = true;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.maxTextureSize = 1024; importer.alphaSource = TextureImporterAlphaSource.None;
                    importer.SaveAndReimport();
                }
            }
            return log.ToString();
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Explicit opt-in art review fixture. Builds every theme's chamber directly through TowerWorld.SetTheme (no
    // encounter) and renders 1920x1080 camera captures at the walkway, the middle tiers and the top shelves, plus
    // a wide overview. HUD and actors other than the player are left out so only the map art is judged.
    public sealed class TowerGallerySmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class GalleryReport
        {
            public string mode = "towerGallery", finishedUtc;
            public bool success;
            public List<string> screenshots = new List<string>(), errors = new List<string>();
        }
        private readonly GalleryReport report = new GalleryReport();
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private TowerGame game; private TowerWorld world;
        private string prefix, reportPath; private bool initialized, finished;
        private static readonly (string name, float x, float y, float size)[] Shots = { ("ground", -1, 0, 5.625f), ("middle", 8, 3, 5.625f), ("top", 17, 6.7f, 5.625f), ("overview", 6.5f, 1.5f, 11) };

        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver, string capturePrefix, string outputPath, bool quit = false)
        {
            if (!tower) throw new ArgumentException("A live game is required.");
            if (!Path.IsPathRooted(capturePrefix) || !Path.IsPathRooted(outputPath)) throw new ArgumentException("Gallery output paths must be absolute.");
            game = tower; prefix = Path.GetFullPath(capturePrefix); reportPath = Path.GetFullPath(outputPath); initialized = true;
            Application.logMessageReceived += RecordLog;
        }
        private void RecordLog(string message, string trace, LogType type)
        { if ((type == LogType.Error || type == LogType.Exception) && report.errors.Count < 32) report.errors.Add(message + "\n" + trace); }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }

        private IEnumerator Start()
        {
            if (!initialized) yield break;
            Exception error = null; var body = Suite();
            while (true)
            {
                bool more;
                try { more = body.MoveNext(); } catch (Exception caught) { error = caught; break; }
                if (!more) break;
                yield return body.Current;
            }
            Finish(error);
        }

        private IEnumerator Suite()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            game.SmokeControlled = true; game.SuppressProgressPersistence = true; game.enabled = false;
            world = (TowerWorld)typeof(TowerGame).GetField("world", Hidden).GetValue(game);
            foreach (var actor in game.enemies) if (actor.root) actor.root.SetActive(false);
            foreach (var actor in game.allies) if (actor.root) actor.root.SetActive(false);
            foreach (TowerThemeId theme in Enum.GetValues(typeof(TowerThemeId)))
            {
                world.SetTheme(theme, 3, 20031);
                game.hero.pos = new Vector2(-10, world.GroundAt(-10)); game.hero.root.transform.position = game.hero.pos;
                world.Tick(.4f, false);
                yield return null;
                foreach (var shot in Shots) Capture(theme, shot.name, shot.x, shot.y, shot.size);
            }
        }

        private void Capture(TowerThemeId theme, string name, float x, float y, float size)
        {
            var view = game.view; Vector3 position = view.transform.position; float oldSize = view.orthographicSize, aspect = view.aspect;
            var target = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active; Texture2D pixels = null;
            var planes = FindObjectsByType<TowerParallaxLayer>(FindObjectsSortMode.None);
            try
            {
                view.targetTexture = target; view.aspect = 1920f / 1080; view.orthographicSize = size;
                view.transform.position = new Vector3(x, y, position.z);
                foreach (var plane in planes) if (plane.isActiveAndEnabled) plane.Follow(view);
                view.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
                string path = prefix + "-" + theme.ToString().ToLowerInvariant() + "-" + name + ".png";
                File.WriteAllBytes(path, pixels.EncodeToPNG()); report.screenshots.Add(path);
            }
            finally
            {
                view.targetTexture = null; view.aspect = aspect; view.orthographicSize = oldSize; view.transform.position = position;
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); if (pixels) Destroy(pixels);
                foreach (var plane in planes) if (plane && plane.isActiveAndEnabled) plane.Follow(view);
            }
        }

        private void Finish(Exception error)
        {
            if (finished) return; finished = true;
            if (error != null) report.errors.Add(error.ToString());
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= RecordLog;
        }
    }
}

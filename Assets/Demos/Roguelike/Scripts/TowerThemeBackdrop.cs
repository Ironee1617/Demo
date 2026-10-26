using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>
    /// High-resolution parallax backdrops baked by Tools/TowerArt/BackdropPainter. Each theme has four
    /// planes (sky, far, mid, near) stored as horizontally repeating strips; empty rows are split away
    /// into separate bands, and solid colour continues above or below a band where the strip ends.
    /// </summary>
    public static class TowerThemeBackdrop
    {
        public const string Folder = "Tower/Themes/Backdrops/";
        /// <summary>Camera position at which every plane lines up with world coordinates.</summary>
        public static readonly Vector2 Reference = new Vector2(7, 1);

        [Serializable] sealed class Manifest { public Band[] layers; }
        [Serializable] internal sealed class Band
        {
            public string theme, file, plane, under, over;
            public float factor, ppu, period, bottom;
            public int width, height, order;
        }

        static Manifest manifest;
        static string loadedTheme;
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static Sprite solid;

        static Manifest Load()
        {
            if (manifest != null) return manifest;
            var asset = Resources.Load<TextAsset>(Folder + "Backdrops");
            manifest = asset == null ? new Manifest { layers = new Band[0] } : JsonUtility.FromJson<Manifest>(asset.text);
            return manifest;
        }

        internal static List<Band> Bands(TowerThemeId theme)
        {
            string id = theme.ToString().ToLowerInvariant();
            var list = new List<Band>();
            foreach (var band in Load().layers) if (band.theme == id) list.Add(band);
            return list;
        }

        /// <summary>Starts loading a theme's strips in the background (see TowerWorld.PrefetchThemeArt).</summary>
        internal static void Prefetch(TowerThemeId theme, List<ResourceRequest> into)
        {
            foreach (var band in Bands(theme)) into.Add(Resources.LoadAsync<Texture2D>(Folder + band.file));
        }

        /// <summary>Builds the theme's planes under <paramref name="parent"/>; false when no bake exists.</summary>
        public static bool Build(Transform parent, TowerThemeId theme)
        {
            var bands = Bands(theme);
            if (bands.Count == 0) return false;
            string id = theme.ToString().ToLowerInvariant();
            if (loadedTheme != id) Release();
            loadedTheme = id;
            var planes = new Dictionary<string, TowerParallaxLayer>();
            foreach (var band in bands)
            {
                var texture = Texture(band);
                if (texture == null) return false;
                TowerParallaxLayer plane;
                if (!planes.TryGetValue(band.plane, out plane))
                {
                    var go = new GameObject(theme + " backdrop " + band.plane);
                    go.transform.SetParent(parent, false);
                    plane = go.AddComponent<TowerParallaxLayer>();
                    plane.Configure(band.factor, Reference);
                    planes.Add(band.plane, plane);
                }
                var sprite = Sprite(band, texture);
                float width = band.width / band.ppu, height = band.height / band.ppu;
                // Copies cover the camera's whole travel at every plane's drift factor; a long floor needs more of the
                // slow near planes than the compact chamber's three.
                int last = LastCopy(band);
                for (int k = -1; k <= last; k++)
                {
                    var copy = new GameObject(theme + " " + band.file + " tile " + (k + 1));
                    copy.transform.SetParent(plane.transform, false);
                    copy.transform.localPosition = new Vector3(k * band.period, band.bottom, 0);
                    var renderer = copy.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite; renderer.sortingOrder = band.order;
                    plane.Track(renderer);
                }
                Color fill;
                if (!string.IsNullOrEmpty(band.under) && ColorUtility.TryParseHtmlString("#" + band.under, out fill))
                    Fill(plane.transform, theme + " " + band.plane + " underfill", band, band.bottom - 40, band.bottom + 2 / band.ppu, fill);
                if (!string.IsNullOrEmpty(band.over) && ColorUtility.TryParseHtmlString("#" + band.over, out fill))
                    Fill(plane.transform, theme + " " + band.plane + " overfill", band, band.bottom + height - 2 / band.ppu, band.bottom + height + 40, fill);
            }
            return true;
        }

        /// <summary>Index of the right-most copy a plane needs to stay covered up to the camera's right limit.</summary>
        static int LastCopy(Band band)
        {
            const float HalfView = 14;
            float cameraRight = TowerWorld.Right - 7.5f;
            float visible = cameraRight * (1 - band.factor) + Reference.x * band.factor + HalfView;
            return Mathf.Max(1, Mathf.CeilToInt(visible / band.period) - 1);
        }

        static void Fill(Transform plane, string name, Band band, float y0, float y1, Color color)
        {
            if (solid == null)
            {
                var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                t.SetPixel(0, 0, Color.white); t.Apply();
                solid = UnityEngine.Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0, 0), 1);
            }
            var go = new GameObject(name);
            go.transform.SetParent(plane, false);
            go.transform.localPosition = new Vector3(-band.period, y0, 0);
            go.transform.localScale = new Vector3(band.period * (LastCopy(band) + 2), y1 - y0, 1);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = solid; renderer.color = color; renderer.sortingOrder = band.order;
        }

        static Texture2D Texture(Band band)
        {
            Texture2D texture;
            if (textures.TryGetValue(band.file, out texture) && texture != null) return texture;
            texture = Resources.Load<Texture2D>(Folder + band.file);
            if (texture == null) return null;
            texture.wrapMode = TextureWrapMode.Clamp;
            textures[band.file] = texture;
            return texture;
        }

        static Sprite Sprite(Band band, Texture2D texture)
        {
            Sprite sprite;
            if (sprites.TryGetValue(band.file, out sprite) && sprite != null) return sprite;
            sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero, band.ppu, 0, SpriteMeshType.FullRect);
            sprite.name = band.file;
            sprites[band.file] = sprite;
            return sprite;
        }

        /// <summary>Unloads the previous theme's strips; only one theme's planes stay resident.</summary>
        static void Release()
        {
            foreach (var sprite in sprites.Values) if (sprite != null) UnityEngine.Object.Destroy(sprite);
            foreach (var texture in textures.Values) if (texture != null) Resources.UnloadAsset(texture);
            sprites.Clear(); textures.Clear();
        }
    }
}

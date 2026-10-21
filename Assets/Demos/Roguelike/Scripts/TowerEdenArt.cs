using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>Eden's authored pixel poses, with a fixed scale and a measured ground pivot.</summary>
    public sealed class TowerEdenArt : MonoBehaviour
    {
        public const int FrameCount = 16;
        public const float Height = 1.9f;
        static readonly string[] Ids = { "michael", "gabriel", "raphael", "uriel", "metatron" };
        static readonly Dictionary<int, Sprite[]> library = new Dictionary<int, Sprite[]>();
        Sprite[] frames;
        SpriteRenderer rendererView;
        float gait, landingAge = 1;
        bool posed, wasGrounded = true;
        public int CurrentFrame { get; private set; }
        public SpriteRenderer Renderer { get { return rendererView; } }
        public float RenderedHeight { get { return Height * Mathf.Abs(transform.lossyScale.y); } }
        public static bool Contains(int index) { return index >= 100 && index <= 104; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { library.Clear(); }

        public static Sprite[] Frames(int index)
        {
            if (!Contains(index)) throw new ArgumentOutOfRangeException("index");
            Sprite[] result;
            if (library.TryGetValue(index, out result) && result[0] != null) return result;
            var texture = Resources.Load<Texture2D>("Tower/Myths/EdenCute/" + Ids[index - 100]);
            if (texture == null || !texture.isReadable)
                throw new InvalidOperationException("Missing readable Eden sprite sheet: " + index);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = texture.GetPixels32();
            // Generated drawings can extend past their nominal grid. Measure
            // empty gutters before trimming so a long sword is kept with its
            // owner and never appears as a detached sliver in the next pose.
            int[] rows = { 0, 0, 0, 0, texture.height };
            for (int row = 1; row < 4; row++)
                rows[row] = ClearCut(pixels, texture.width, texture.height * row / 4,
                    texture.height / 20, false, 0, texture.width);
            var bounds = new RectInt[FrameCount];
            for (int frame = 0; frame < FrameCount; frame++)
            {
                int column = frame % 4, row = frame / 4;
                int y0 = rows[3 - row], y1 = rows[4 - row];
                int x0 = column == 0 ? 0 : ClearCut(pixels, texture.width, column * texture.width / 4,
                    texture.width / 20, true, y0, y1);
                int x1 = column == 3 ? texture.width : ClearCut(pixels, texture.width, (column + 1) * texture.width / 4,
                    texture.width / 20, true, y0, y1);
                int left = x1, right = x0, bottom = y1, top = y0;
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                        if (pixels[y * texture.width + x].a >= 32)
                        {
                            left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                            bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                        }
                if (right <= left || top <= bottom)
                    throw new InvalidOperationException("Empty Eden pose: " + index + "/" + frame);
                bounds[frame] = new RectInt(left, bottom, right - left + 1, top - bottom + 1);
            }
            // One scale for the entire sheet: a crouch must get shorter and a
            // raised weapon must get taller. Never fit each pose to a fixed box.
            float unit = bounds[0].height / Height;
            result = new Sprite[FrameCount];
            for (int frame = 0; frame < FrameCount; frame++)
            {
                var b = bounds[frame];
                float rootX = (frame % 4 + .5f) * texture.width / 4;
                var pivot = new Vector2((rootX - b.x) / b.width, 0);
                result[frame] = Sprite.Create(texture, new Rect(b.x, b.y, b.width, b.height),
                    pivot, unit, 0, SpriteMeshType.FullRect);
                result[frame].name = Ids[index - 100] + " pixel pose " + frame;
            }
            library[index] = result;
            return result;
        }

        static int ClearCut(Color32[] pixels, int width, int center, int radius, bool vertical, int from, int to)
        {
            for (int distance = 0; distance <= radius; distance++)
                for (int side = -1; side <= 1; side += 2)
                {
                    int cut = center + distance * side;
                    bool clear = true;
                    for (int at = from; at < to; at++)
                        if (pixels[vertical ? at * width + cut : cut * width + at].a >= 32) { clear = false; break; }
                    if (clear) return cut;
                }
            throw new InvalidOperationException("Eden poses overlap: no transparent gutter near " + center);
        }

        public void Build(int index)
        {
            frames = Frames(index);
            rendererView = gameObject.AddComponent<SpriteRenderer>();
            rendererView.sortingOrder = 35;
            rendererView.sprite = frames[0];
        }

        public void Pose(string motion, bool moving, float time, float progress, float hurt,
            int facing, Color tint, bool grounded, float verticalVelocity, float speed, float dt, float release)
        {
            // Presentation can mirror/tint while paused; animation clocks stay frozen.
            rendererView.flipX = facing < 0;
            rendererView.color = tint;
            if (posed && dt <= 0) return;
            if (grounded && !wasGrounded) landingAge = 0;
            wasGrounded = grounded;
            landingAge += dt;
            if (moving && grounded)
            {
                float localSpeed = speed / Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.x));
                gait += dt * Mathf.Clamp(localSpeed * 2.4f, 5, 16);
            }
            int locomotion = !grounded ? (verticalVelocity > .15f ? 6 : 7)
                : landingAge < .09f ? 8
                : moving ? 2 + (Mathf.FloorToInt(gait) % 4)
                : Mathf.FloorToInt(time * 2.4f) % 2;
            int frame = locomotion;
            if (motion == "Death") frame = 15;
            else if (motion == "Hurt" || hurt > 0) frame = 14;
            else if (motion == "Attack")
            {
                // Anticipation persists until the gameplay contact, then the
                // forward strike is visible on the exact damage-release frame.
                frame = progress < release ? 9 : progress < release + .18f ? 10 : progress < .86f ? 11 : locomotion;
            }
            else if (motion == "Cast")
                frame = progress < release ? 12 : progress < .88f ? 13 : locomotion;
            CurrentFrame = frame;
            rendererView.sprite = frames[frame];
            posed = true;
        }
    }
}

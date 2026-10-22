using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>Moves a backdrop plane with the camera by its drift factor and keeps texels on whole screen pixels.</summary>
    public sealed class TowerParallaxLayer : MonoBehaviour
    {
        [SerializeField] float factor;
        [SerializeField] Vector2 reference;
        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        float lastScale = -1;

        public float Factor { get { return factor; } }
        public IReadOnlyList<SpriteRenderer> Renderers { get { return renderers; } }

        public void Configure(float drift, Vector2 at) { factor = drift; reference = at; }
        public void Track(SpriteRenderer renderer) { renderers.Add(renderer); }

        void LateUpdate() { Follow(Camera.main); }

        /// <summary>Positions the plane for <paramref name="view"/>; captures call this before rendering.</summary>
        public void Follow(Camera view)
        {
            if (view == null || transform.parent == null) return;
            Vector2 camera = transform.parent.InverseTransformPoint(view.transform.position);
            Vector2 offset = (camera - reference) * factor;
            float screenUnits = view.orthographic && view.pixelHeight > 0 ? 2 * view.orthographicSize / view.pixelHeight : 0;
            if (screenUnits > 0)
            {
                // Snap to the screen-pixel grid relative to the camera so texels land on whole pixels.
                Vector2 rel = offset - camera;
                rel.x = Mathf.Round(rel.x / screenUnits) * screenUnits; rel.y = Mathf.Round(rel.y / screenUnits) * screenUnits;
                offset = rel + camera;
                if (!Mathf.Approximately(screenUnits, lastScale)) { lastScale = screenUnits; ChooseFilter(1 / screenUnits); }
            }
            transform.localPosition = new Vector3(offset.x, offset.y, 0);
        }

        /// <summary>Whole-number magnification stays crisp with point sampling; anything else filters.</summary>
        void ChooseFilter(float pixelsPerUnit)
        {
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sprite == null) continue;
                float ratio = pixelsPerUnit / renderer.sprite.pixelsPerUnit;
                bool whole = ratio >= .99f && Mathf.Abs(ratio - Mathf.Round(ratio)) < .01f;
                renderer.sprite.texture.filterMode = whole ? FilterMode.Point : FilterMode.Bilinear;
            }
        }
    }
}

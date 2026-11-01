using System;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Weapons that leave a god's hand as the shot itself: Zeus hurls the bolt he holds. The flying weapon is the
    // same pixel sprite the hand let go of, tip on the shot's contact point, in a warm glow that trails a ribbon of light.
    public sealed partial class TowerVfx
    {
        /// <summary>
        /// Dresses a companion's basic shot as the weapon it threw. <paramref name="weapon"/> points up from its grip pivot,
        /// <paramref name="reach"/> art pixels to the tip, and is drawn at the thrower's <paramref name="scale"/>.
        /// </summary>
        public void ThrownWeapon(Transform root, Sprite weapon, float scale, float reach, Color tint)
        {
            if (!root || !weapon) return;
            EnsureTextures();
            var old = root.GetComponent<SpriteRenderer>(); if (old) old.enabled = false;
            Clip existing; if (attached.TryGetValue(root, out existing)) Retire(existing);
            root.localScale = Vector3.one;
            // The shot flies along the root's x axis; the weapon lies along it with its tip leading.
            float length = reach * scale / TowerGodPixelArt.PixelsPerUnit;
            // The hand holds the weapon at its middle, so the glow centres on the grip.
            Vector2 grip = new Vector2(-length, 0);
            Color light = Color.Lerp(tint, Color.white, .55f);
            var body = Attach(root, "Thrown weapon", weapon, weapon.bounds.size.x * scale, Color.white, 123, false, grip);
            body.angle = -90;
            var halo = Attach(root, "Thrown weapon glow", glow, length * 2.6f, Alpha(tint, .6f), 121, true, grip);
            halo.scale.y *= .26f; halo.pulse = .12f; halo.flicker = .25f;
            var tip = Attach(root, "Thrown weapon tip", glow, .6f, Alpha(light, .9f), 124); tip.pulse = .2f;
            Emitter(halo, 30, 0, ThrownRibbon(root.TransformPoint(grip), .2f, Alpha(tint, .55f), .16f));
            Emitter(tip, Emit.Sparkles, 18, .25f, light);
            Bundle(root, body, halo, tip);
        }

        private Action<Clip, Vector2> ThrownRibbon(Vector2 start, float thickness, Color color, float life)
        {
            Vector2 last = start;
            return (owner, p) =>
            {
                Vector2 now = owner.root.transform.position;
                if ((now - last).sqrMagnitude < .0025f) return;
                var band = Line(last, now, thickness, color, life, 120, 1.3f); band.axisEnd = new Vector2(1, .15f);
                last = now;
            };
        }
    }
}

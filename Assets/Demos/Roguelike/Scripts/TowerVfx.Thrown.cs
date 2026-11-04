using System;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Weapons that leave a god's hand as the shot itself: Zeus hurls the bolt he holds. The flying weapon is the
    // same pixel sprite the hand let go of, tip on the shot's contact point, in a soft glow with a painted sheen of light
    // along it, a turning sparkle at the tip and a painted streak trail (a white core line over a tinted band, over a soft
    // glow). When the shot ends (it struck, or ran out) a hit spark flares in its bloom where the tip was.
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
            Color light = Color.Lerp(tint, Color.white, .55f), tone = new Color(tint.r, tint.g, tint.b, 1);
            var body = Attach(root, "Thrown weapon", weapon, weapon.bounds.size.x * scale, Color.white, 123, false, grip);
            body.angle = -90;
            var halo = Attach(root, "Thrown weapon glow", glow, length * 2.4f, Alpha(tint, .4f), 121, true, grip);
            halo.scale.y *= .26f; halo.pulse = .12f; halo.flicker = .25f;
            // A painted spindle of light along the weapon (behind the sprite), so it reads as charged on any backdrop.
            var sheen = Attach(root, "Thrown weapon sheen", streak, length * 2.3f, Alpha(tone, .85f), 122, true, grip);
            sheen.scale.y *= .2f; sheen.flicker = .15f;
            var tip = Attach(root, "Thrown weapon tip", fleck, .62f, Alpha(light, .95f), 124); tip.spin = 300; tip.pulse = .15f;
            var tipGlow = Attach(root, "Thrown weapon tip glow", glow, .9f, Alpha(tint, .45f), 121); tipGlow.pulse = .15f;
            Emitter(halo, 30, 0, ThrownRibbon(root.TransformPoint(grip), .24f, Alpha(tone, .8f), .16f));
            Emitter(tip, Emit.Sparkles, 14, .25f, light);
            Bundle(root, body, halo, sheen, tip, tipGlow);
            ThrownWatch(root, tone);
        }

        /// <summary>The trail: each step lays a tinted band and a thinner white core line, both tapering as they fade, and every
        /// few steps a soft glow that shrinks away behind them.</summary>
        private Action<Clip, Vector2> ThrownRibbon(Vector2 start, float thickness, Color color, float life)
        {
            Vector2 last = start; int steps = 0;
            return (owner, p) =>
            {
                Vector2 now = owner.root.transform.position;
                if ((now - last).sqrMagnitude < .0025f) return;
                var band = Line(last, now, thickness, color, life, 120, 1.3f); band.axisEnd = new Vector2(1, .15f);
                if (++steps % 2 == 0) { var line = Line(last, now, thickness * .38f, Color.white, life * .8f, 121, 1.3f); line.axisEnd = new Vector2(1, .1f); }
                if (steps % 3 == 0) { var haze = Spawn("Thrown weapon trail glow", glow, now, thickness * 3.2f, Alpha(color, color.a * .4f), life * 1.4f, true, 119); haze.endScale = .3f; haze.ease = 1; haze.fadePower = 1.2f; }
                last = now;
            };
        }

        /// <summary>An invisible watcher that flares a hit spark in its bloom (and a few needles) where the weapon's tip was last
        /// seen once the shot is gone. It changes nothing about the shot itself.</summary>
        private void ThrownWatch(Transform root, Color tone)
        {
            var watcher = Spawn("Thrown weapon watcher", fleck, root.position, .1f, Color.clear, 12, true, 0);
            Vector2 last = root.position, heading = root.right; int frame = -1;
            Emitter(watcher, 5000, 0, (owner, p) =>
            {
                if (Time.frameCount == frame) return;
                frame = Time.frameCount;
                if (root) { Vector2 now = root.position; if ((now - last).sqrMagnitude > 1e-6f) heading = (now - last).normalized; last = now; return; }
                owner.emit = Emit.None; owner.life = Mathf.Min(owner.life, owner.age + .02f);
                StormSpark(last, 1.3f, tone, .12f, 0, .5f);
                float back = Mathf.Atan2(-heading.y, -heading.x) * Mathf.Rad2Deg;
                for (int i = 0; i < 3; i++) Spark(last, Direction(back + Range(-60, 60)) * Range(3, 5.5f), i == 0 ? Color.white : tone, Range(.24f, .34f), Range(.14f, .22f), 4, 3);
            });
        }
    }
}

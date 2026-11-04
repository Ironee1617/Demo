using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // The hero's ranged weapons: the crossbow's flash, its bolt and the spark where it strikes, and the orb that floats at
    // the shoulder with the homing bolts it looses and their soft bursts. The bolt and the orb are chuchuking's pixel art.
    public sealed partial class TowerVfx
    {
        /// <summary>Whether <paramref name="root"/> still carries the look attached to it (Clear removes every look).</summary>
        public bool Carries(Transform root) { return root && attached.ContainsKey(root); }

        private static Vector2 Heading(Vector2 direction) { return direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.right; }

        /// <summary>A gun bullet or an orb bolt riding on <paramref name="root"/>, which turns with its flight.</summary>
        public void WeaponProjectile(Transform root, TowerAttackMode weapon, Color tint)
        {
            if (!root) return;
            EnsureTextures();
            Clip existing; if (attached.TryGetValue(root, out existing)) Retire(existing);
            Color light = Color.Lerp(tint, Color.white, .55f);
            if (weapon == TowerAttackMode.Gun)
            {
                // chuchuking's crossbow bolt (the hero sheet stands it on end) with a short streak trailing behind it.
                var art = TowerHeroPixelArt.BoltSprite;
                var bolt = Attach(root, "Crossbow bolt", art, art.bounds.size.x, Color.white, 125, false); bolt.angle = -90;
                var tracer = Attach(root, "Bullet tracer", streak, .95f, Alpha(light, .7f), 124, true, new Vector2(-.6f, 0)); tracer.scale.y *= .35f;
                var halo = Attach(root, "Bullet glow", glow, .62f, Alpha(tint, .45f), 123); halo.pulse = .08f;
                Bundle(root, bolt, tracer, halo);
            }
            else
            {
                var heart = PlasmaOrb(root, "Orb bolt", .42f, Alpha(Color.Lerp(tint, Color.white, .15f), 1), 125);
                var spark = Attach(root, "Orb bolt core", core, .16f, Warm, 126);
                var halo = Attach(root, "Orb bolt glow", glow, .95f, Alpha(tint, .6f), 123); halo.pulse = .12f;
                Emitter(halo, Emit.Trace, 36, .03f, Alpha(light, .5f));
                Bundle(root, heart, spark, halo);
            }
        }

        /// <summary>The pistol firing at <paramref name="pos"/> along <paramref name="direction"/>: a flare, side flashes, sparks and a wisp of smoke.</summary>
        public void MuzzleFlash(Vector2 pos, Vector2 direction, Color tint)
        {
            EnsureTextures();
            Vector2 d = Heading(direction), n = new Vector2(-d.y, d.x); float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Color light = Color.Lerp(tint, Color.white, .45f);
            var flare = Spawn("Muzzle flare", streak, pos + d * .28f, .8f, Alpha(light, 1), .07f, true, 133); flare.angle = angle; flare.scale.y *= .9f; flare.startScale = .6f; flare.endScale = 1.1f; flare.ease = 3;
            SparkFlip(pos + d * .1f, .62f, Alpha(Warm, .95f), .06f, 134);
            var bloom = Spawn("Muzzle bloom", glow, pos, .95f, Alpha(tint, .5f), .1f, true, 130); bloom.endScale = 1.3f; bloom.ease = 1; bloom.fadePower = 1.4f;
            for (int i = 0; i < 2; i++)
            {
                var side = Spawn("Muzzle side flare", streak, pos + d * .1f, .38f, Alpha(light, .85f), .06f, true, 132);
                side.angle = angle + (i == 0 ? 62 : -62); side.scale.y *= .7f; side.startScale = .5f;
            }
            for (int i = 0; i < 3; i++) Spark(pos + d * .15f, d * Range(4, 8) + n * Range(-1.6f, 1.6f), i == 0 ? Color.white : light, Range(.14f, .24f), Range(.08f, .14f), 4, 3);
            var smoke = Puff(pos + d * .25f, .32f, new Color(.78f, .76f, .74f, .35f), d * .9f + Vector2.up * .5f, .38f, 126); smoke.endScale = 1.8f;
        }

        /// <summary>A gun bullet or orb bolt striking at <paramref name="pos"/> while flying along <paramref name="direction"/>.</summary>
        public void WeaponHit(TowerAttackMode weapon, Vector2 pos, Color tint, Vector2 direction)
        {
            EnsureTextures();
            Vector2 d = Heading(direction), n = new Vector2(-d.y, d.x);
            Color light = Color.Lerp(tint, Color.white, .4f);
            if (weapon == TowerAttackMode.Gun)
            {
                // Sparks kick back off the struck side.
                SparkFlip(pos, .7f, Alpha(Color.Lerp(Color.white, tint, .3f), .95f), .08f, 133);
                for (int i = 0; i < 5; i++) Spark(pos, -d * Range(2.5f, 6) + n * Range(-3.5f, 3.5f), i % 2 == 0 ? Color.white : light, Range(.16f, .28f), Range(.12f, .24f), 9, 2.5f);
                var dent = Spawn("Bullet ring", ring, pos, .55f, Alpha(tint, .7f), .12f, true, 131); dent.startScale = .3f; dent.endScale = 1; dent.ease = 3;
            }
            else
            {
                Flash(pos, .95f, Alpha(light, .95f), .12f);
                Wave(pos, 1.1f, Alpha(tint, .75f), .24f, 3);
                Sparkles(pos, 7, .3f, light);
            }
        }

        /// <summary>The orb at the hero's shoulder: a plasma heart in a slow-turning rune ring over a soft bloom, shedding motes.</summary>
        public void HeroOrb(Transform root, Color tint)
        {
            if (!root) return;
            EnsureTextures();
            Clip existing; if (attached.TryGetValue(root, out existing)) Retire(existing);
            // Kept close to the tint so it reads as a coloured orb, not a white light, on bright marble.
            Color light = Color.Lerp(tint, Color.white, .15f), deep = Color.Lerp(tint, new Color(.2f, .35f, .9f), .45f);
            // chuchuking's pixel orb at the heart.
            var art = TowerHeroPixelArt.OrbSprite;
            var heart = Attach(root, "Orb heart", art, art.bounds.size.x, Color.white, 125, false);
            var halo = Attach(root, "Orb halo", glow, 1.45f, Alpha(deep, .62f), 122); halo.pulse = .1f;
            var band = Attach(root, "Orb rune ring", runeRing, .95f, Alpha(light, .6f), 124); band.spin = 70;
            Emitter(halo, Emit.Motes, 5, .3f, Alpha(light, .8f));
            Bundle(root, heart, halo, band);
        }

        /// <summary>The orb letting a bolt go toward <paramref name="direction"/>: a ring popping off its face and a spark.</summary>
        public void OrbRelease(Vector2 pos, Vector2 direction, Color tint)
        {
            EnsureTextures();
            Vector2 d = Heading(direction); Color light = Color.Lerp(tint, Color.white, .45f);
            var pop = Spawn("Orb release", ring, pos + d * .2f, .7f, Alpha(light, .85f), .16f, true, 131); pop.startScale = .3f; pop.endScale = 1.2f; pop.ease = 3;
            SparkFlip(pos + d * .25f, .55f, Alpha(Warm, .9f), .06f, 132);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>
    /// High-resolution combat effects. Every element is generated once at 128 pixels per unit with trilinear
    /// filtering and drawn as layered additive light or alpha-blended smoke, so slashes, spells, projectiles,
    /// impacts and dash afterimages read as photographic light and volume instead of pixel frames.
    /// </summary>
    public sealed class TowerVfx : MonoBehaviour
    {
        public const int PixelsPerUnit = 128;
        public const int KnownEffectsCount = 16;
        private const int PoolLimit = 512;
        private const float Infinite = float.PositiveInfinity;

        private enum Emit { None, Embers, Smoke, Sparkles, Frost, Poison, Motes, Trace, Flames }

        private sealed class Clip
        {
            public GameObject root; public Transform external; public SpriteRenderer renderer;
            public SpriteRenderer[] copies; public MeshRenderer[] meshCopies; public Mesh[] ownedMeshes; public MaterialPropertyBlock meshTint;
            public List<Clip> layers;
            public Sprite[] frames; public float fps = 24; public bool loop;
            public Vector2 position, velocity, offset; public float drag, gravity;
            public Vector3 scale = Vector3.one; public float startScale = 1, endScale = 1, ease = 2, stretch;
            public Color color = Color.white; public Gradient ramp; public float fadeIn, fadePower = 1, flicker, pulse;
            public float age, life, spin, angle, angleOffset; public int seed;
            public bool alignVelocity, registered;
            public Emit emit; public float emitRate, emitBank, emitSpread; public Color emitColor;
        }

        private readonly List<Clip> clips = new List<Clip>();
        private readonly List<Clip> expired = new List<Clip>();
        private readonly HashSet<Clip> retiring = new HashSet<Clip>();
        private readonly Dictionary<Transform, Clip> attached = new Dictionary<Transform, Clip>();
        private readonly Stack<GameObject> pool = new Stack<GameObject>();
        private readonly System.Random random = new System.Random(2026);
        private int sequence;
        public int ActiveCount { get { return clips.Count; } }

        private static bool built;
        private static Sprite glow, core, ring, streak, spark, wisp, shard, sigil, arrow, bolt, hex, meteorRock;
        private static Sprite[] smoke, flame, slash, rock, turret;
        private static Material additiveMaterial, alphaMaterial;
        private static readonly Dictionary<Material, Material> ghostMaterials = new Dictionary<Material, Material>();
        private static Gradient fireRamp;

        private static readonly Color FireLight = new Color(1, .62f, .22f), IceLight = new Color(.6f, .9f, 1), StormLight = new Color(.72f, .8f, 1),
            ArcaneLight = new Color(.78f, .58f, 1), PoisonLight = new Color(.62f, .95f, .32f), LifeLight = new Color(.48f, 1, .7f),
            GoldLight = new Color(1, .86f, .48f), SteelLight = new Color(.55f, .88f, 1), Warm = new Color(1, .97f, .88f),
            DustTone = new Color(.8f, .72f, .6f, .42f), Soot = new Color(.26f, .21f, .2f, .58f);

        public static TowerVfx Create(Transform parent)
        {
            var root = new GameObject("Combat light effects");
            root.transform.SetParent(parent, false);
            EnsureTextures();
            return root.AddComponent<TowerVfx>();
        }

        // ---------------------------------------------------------------- simulation

        public void Tick(float dt)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                bool lost = !clip.root || (!ReferenceEquals(clip.external, null) && !clip.external);
                if (lost || clip.age + dt >= clip.life) { expired.Add(clip); continue; }
                clip.age += dt;
                Animate(clip, dt);
            }
            if (expired.Count > 0)
            {
                foreach (var clip in expired) { retiring.Add(clip); if (clip.layers != null) foreach (var layer in clip.layers) retiring.Add(layer); }
                for (int i = clips.Count - 1; i >= 0; i--) if (retiring.Contains(clips[i])) { Dispose(clips[i]); clips.RemoveAt(i); }
                expired.Clear(); retiring.Clear();
            }
            int survivors = clips.Count;
            for (int i = 0; i < survivors; i++) { var clip = clips[i]; if (clip.emit != Emit.None && clip.root && clip.age >= 0) Emission(clip, dt); }
        }

        private void Animate(Clip clip, float dt)
        {
            if (clip.age < 0) { if (clip.renderer) clip.renderer.enabled = false; return; }
            if (clip.renderer && !clip.renderer.enabled) clip.renderer.enabled = true;
            float p = Mathf.Clamp01(clip.age / clip.life);
            if (clip.frames != null && clip.renderer && clip.frames.Length > 1)
            {
                int frame = clip.loop ? (int)(clip.age * clip.fps) % clip.frames.Length : Mathf.Min(clip.frames.Length - 1, (int)(p * clip.frames.Length));
                clip.renderer.sprite = clip.frames[frame];
            }
            var transform = clip.root.transform;
            if (ReferenceEquals(clip.external, null))
            {
                if (clip.gravity != 0) clip.velocity.y -= clip.gravity * dt;
                if (clip.drag > 0) clip.velocity *= 1 / (1 + clip.drag * dt);
                clip.position += clip.velocity * dt;
                transform.position = new Vector3(clip.position.x, clip.position.y, 0);
            }
            else transform.localPosition = clip.offset;
            if (clip.alignVelocity && clip.velocity.sqrMagnitude > .0001f) clip.angle = Mathf.Atan2(clip.velocity.y, clip.velocity.x) * Mathf.Rad2Deg;
            else clip.angle += clip.spin * dt;
            float eased = clip.ease <= 0 ? p : 1 - Mathf.Pow(1 - p, clip.ease);
            float s = Mathf.Lerp(clip.startScale, clip.endScale, eased);
            if (clip.pulse > 0) s *= 1 + Mathf.Sin(clip.age * 21 + clip.seed) * clip.pulse;
            Vector3 scale = clip.scale * s;
            if (clip.stretch > 0) scale.x *= 1 + clip.velocity.magnitude * clip.stretch;
            transform.localRotation = Quaternion.Euler(0, 0, clip.angle + clip.angleOffset);
            transform.localScale = scale;
            Color tint = clip.ramp != null ? clip.ramp.Evaluate(p) * clip.color : clip.color;
            float alpha = tint.a;
            if (clip.fadeIn > 0 && clip.age < clip.fadeIn) alpha *= clip.age / clip.fadeIn;
            if (clip.fadePower > 0) alpha *= Mathf.Pow(1 - p, clip.fadePower);
            if (clip.flicker > 0) alpha *= 1 - clip.flicker * (.5f + .5f * Mathf.Sin(clip.age * 113 + clip.seed * 7));
            tint.a = alpha;
            if (clip.renderer) clip.renderer.color = tint;
            if (clip.copies != null) foreach (var copy in clip.copies) if (copy) copy.color = tint;
            if (clip.meshCopies != null)
            {
                clip.meshTint.SetColor("_Color", tint);
                foreach (var copy in clip.meshCopies) if (copy) copy.SetPropertyBlock(clip.meshTint);
            }
        }

        private void Dispose(Clip clip)
        {
            if (clip.registered && !ReferenceEquals(clip.external, null))
            {
                Clip current;
                if (attached.TryGetValue(clip.external, out current) && ReferenceEquals(current, clip)) attached.Remove(clip.external);
            }
            if (clip.ownedMeshes != null) foreach (var mesh in clip.ownedMeshes) if (mesh) Destroy(mesh);
            if (!clip.root) return;
            bool poolable = clip.renderer && ReferenceEquals(clip.external, null) && clip.copies == null && pool.Count < PoolLimit;
            if (poolable) { clip.root.SetActive(false); clip.renderer.sprite = null; pool.Push(clip.root); }
            else Destroy(clip.root);
        }

        private void Retire(Clip clip)
        {
            retiring.Add(clip); if (clip.layers != null) foreach (var layer in clip.layers) retiring.Add(layer);
            for (int i = clips.Count - 1; i >= 0; i--) if (retiring.Contains(clips[i])) { Dispose(clips[i]); clips.RemoveAt(i); }
            retiring.Clear();
        }

        public void Clear()
        {
            // External projectile/zone roots belong to gameplay. Only our child visuals are removed.
            foreach (var clip in clips) Dispose(clip);
            clips.Clear(); attached.Clear(); expired.Clear(); retiring.Clear();
        }

        private void OnDestroy() { Clear(); }

        // ---------------------------------------------------------------- projectiles and zones

        public void Projectile(Transform root, SkillEffect effect, Color tint)
        {
            if (!root) return;
            EnsureTextures();
            var old = root.GetComponent<SpriteRenderer>(); if (old) old.enabled = false;
            root.localScale = Vector3.one;
            Clip existing; if (attached.TryGetValue(root, out existing)) Retire(existing);
            Color light = Color.Lerp(tint, EffectColor(effect), .5f);
            Clip primary, halo, tail;
            switch (effect)
            {
                case SkillEffect.Fireball:
                    primary = Attach(root, "Fireball core", core, .46f, Warm, 123); primary.pulse = .08f;
                    halo = Attach(root, "Fireball heat", glow, 1.7f, Alpha(FireLight, .95f), 122); halo.pulse = .14f;
                    tail = Attach(root, "Fireball flames", flame[0], 1.1f, new Color(1, .82f, .5f, .95f), 121, true, new Vector2(-.55f, 0));
                    tail.frames = flame; tail.fps = 22; tail.angleOffset = 90; tail.pulse = .05f;
                    Emitter(primary, Emit.Embers, 30, .18f, FireLight); Emitter(halo, Emit.Smoke, 9, .2f, new Color(.3f, .22f, .2f, .4f));
                    Bundle(root, primary, halo, tail); break;
                case SkillEffect.ArcaneVolley:
                    primary = Attach(root, "Arcane bolt", bolt, .95f, new Color(.9f, .78f, 1), 123);
                    halo = Attach(root, "Arcane halo", glow, 1, Alpha(light, .85f), 122); halo.pulse = .1f;
                    Emitter(primary, Emit.Sparkles, 20, .15f, ArcaneLight); Emitter(halo, Emit.Trace, 30, .04f, Alpha(light, .55f));
                    Bundle(root, primary, halo); break;
                case SkillEffect.MultiShot:
                case SkillEffect.RainArrows:
                    primary = Attach(root, "Arrow", arrow, 1.15f, Color.white, 123, false);
                    tail = Attach(root, "Arrow wake", streak, 1.7f, new Color(1, .95f, .8f, .45f), 121, true, new Vector2(-.75f, 0)); tail.scale.y *= .3f;
                    halo = Attach(root, "Arrow light", glow, .55f, Alpha(GoldLight, .3f), 122);
                    Emitter(halo, Emit.Trace, 14, .03f, new Color(1, 1, 1, .3f));
                    Bundle(root, primary, tail, halo); break;
                case SkillEffect.PierceShot:
                    primary = Attach(root, "Piercing arrow", arrow, 1.6f, Color.white, 123, false);
                    tail = Attach(root, "Piercing wake", streak, 2.8f, Alpha(GoldLight, .75f), 121, true, new Vector2(-1.1f, 0)); tail.scale.y *= .5f;
                    halo = Attach(root, "Piercing light", glow, .9f, Alpha(GoldLight, .6f), 122); halo.pulse = .08f;
                    Emitter(primary, Emit.Sparkles, 22, .2f, GoldLight); Emitter(halo, Emit.Trace, 36, .04f, Alpha(GoldLight, .6f));
                    Bundle(root, primary, tail, halo); break;
                case SkillEffect.Shockwave:
                    primary = Attach(root, "Shockwave crest", ring, 1.2f, Alpha(GoldLight, .95f), 123); primary.scale.x *= .38f; primary.pulse = .06f;
                    halo = Attach(root, "Shockwave light", glow, 1.8f, Alpha(GoldLight, .55f), 122); halo.scale.x *= .6f;
                    tail = Attach(root, "Shockwave wake", streak, 1.8f, Alpha(GoldLight, .6f), 121, true, new Vector2(-.9f, 0)); tail.scale.y *= 1.6f;
                    Emitter(primary, Emit.Smoke, 16, .3f, DustTone); Emitter(halo, Emit.Embers, 12, .3f, GoldLight);
                    Bundle(root, primary, halo, tail); break;
                case SkillEffect.Sentry:
                    primary = Attach(root, "Sentry tracer", core, .22f, new Color(.85f, .97f, 1), 123);
                    tail = Attach(root, "Tracer wake", streak, 1, Alpha(SteelLight, .85f), 122, true, new Vector2(-.45f, 0)); tail.scale.y *= .25f;
                    Emitter(primary, Emit.Trace, 40, .02f, Alpha(SteelLight, .5f));
                    Bundle(root, primary, tail); break;
                default:
                    primary = Attach(root, "Energy bolt", core, .36f, Color.Lerp(light, Color.white, .6f), 123); primary.pulse = .06f;
                    halo = Attach(root, "Energy halo", glow, 1.15f, Alpha(light, .85f), 122); halo.pulse = .1f;
                    Emitter(halo, Emit.Trace, 30, .04f, Alpha(light, .5f));
                    Bundle(root, primary, halo); break;
            }
        }

        public void HostileProjectile(Transform root, float diameter)
        {
            if (!root) return;
            EnsureTextures();
            var old = root.GetComponent<SpriteRenderer>(); if (old) old.enabled = false;
            Clip existing; if (attached.TryGetValue(root, out existing)) Retire(existing);
            Color crimson = new Color(1, .22f, .4f);
            var primary = Attach(root, "Hostile bullet core", core, diameter * .55f, Warm, 146);
            var rim = Attach(root, "Hostile bullet rim", ring, diameter * 1.2f, Alpha(crimson, .95f), 145); rim.pulse = .06f;
            var halo = Attach(root, "Hostile bullet glow", glow, diameter * 2.7f, Alpha(crimson, .7f), 144); halo.pulse = .12f;
            var tail = Attach(root, "Hostile bullet wake", streak, diameter * 2.8f, Alpha(crimson, .5f), 144, true, new Vector2(-diameter * 1.1f, 0)); tail.scale.y *= diameter * 1.6f;
            Emitter(halo, Emit.Trace, 18, .03f, Alpha(crimson, .5f));
            Bundle(root, primary, rim, halo, tail);
        }

        public void AttachZone(GameObject root, SkillEffect effect, float radius, float duration)
        {
            if (!root) return;
            EnsureTextures();
            var old = root.GetComponent<SpriteRenderer>(); if (old) old.enabled = false;
            root.transform.localScale = Vector3.one;
            Clip existing;
            if (attached.TryGetValue(root.transform, out existing)) return;
            float life = Mathf.Max(.1f, duration);
            Vector2 center = root.transform.position;
            if (effect == SkillEffect.Sentry)
            {
                var body = Attach(root.transform, "Sentry turret", turret[0], 1.7f, Color.white, 113, false); body.frames = turret; body.fps = 9; body.life = life; body.fadePower = 0;
                var lens = Attach(root.transform, "Sentry optic", glow, .4f, Alpha(SteelLight, .8f), 114, true, new Vector2(-.13f, .14f)); lens.pulse = .18f; lens.life = life; lens.fadePower = 0;
                Bundle(root.transform, body, lens);
                return;
            }
            if (effect == SkillEffect.PoisonCloud)
            {
                var haze = Attach(root.transform, "Poison haze", glow, radius * 2.3f, Alpha(PoisonLight, .3f), 111); haze.pulse = .07f; haze.life = life; haze.fadePower = .7f;
                var volume = Attach(root.transform, "Poison volume", glow, radius * 1.4f, new Color(.3f, .6f, .2f, .25f), 110); volume.life = life; volume.fadePower = .7f; volume.pulse = .05f;
                Emitter(volume, Emit.Poison, 16, radius * .8f, new Color(.45f, .8f, .3f, .45f)); Emitter(haze, Emit.Sparkles, 6, radius * .7f, new Color(.8f, 1, .55f));
                Bundle(root.transform, haze, volume);
                for (int i = 0; i < 8; i++) Puff(center + Inside(radius * .7f), radius * Range(.45f, .8f), new Color(.5f, .85f, .3f, .5f), new Vector2(Range(-.5f, .5f), Range(.4f, 1)), Range(.6f, 1), 112);
                return;
            }
            if (effect == SkillEffect.BladeStorm)
            {
                var hub = Attach(root.transform, "Blade storm light", glow, radius * 1.3f, Alpha(GoldLight, .55f), 112); hub.pulse = .1f; hub.life = life; hub.fadePower = .8f;
                var orbit = Attach(root.transform, "Blade storm ring", ring, radius * 2.05f, Alpha(GoldLight, .5f), 113); orbit.spin = -220; orbit.life = life; orbit.fadePower = .8f;
                var blades = new List<Clip> { orbit };
                for (int i = 0; i < 3; i++)
                {
                    var blade = Attach(root.transform, "Blade storm edge", streak, radius * 2.15f, new Color(1, .9f, .62f, .7f), 114); blade.scale.y *= .38f; blade.angle = i * 120; blade.spin = 760; blade.life = life; blade.fadePower = .8f;
                    blades.Add(blade);
                }
                Emitter(hub, Emit.Sparkles, 26, radius * .9f, GoldLight);
                Bundle(root.transform, hub, blades.ToArray());
                return;
            }
            var field = Attach(root.transform, effect + " field", glow, Mathf.Max(1.5f, radius * 2), Alpha(EffectColor(effect), .5f), 113); field.pulse = .08f; field.life = life; field.fadePower = .8f;
            var edge = Attach(root.transform, effect + " edge", ring, Mathf.Max(1.5f, radius * 2), Alpha(EffectColor(effect), .6f), 113); edge.spin = 60; edge.life = life; edge.fadePower = .8f;
            Bundle(root.transform, field, edge);
        }

        // ---------------------------------------------------------------- sword strikes and movement

        public void BasicSlash(Vector2 pos, int facing, Color tint)
        {
            EnsureTextures();
            int side = facing < 0 ? -1 : 1;
            Color light = Color.Lerp(tint, Color.white, .35f);
            var bloom = Sheet("Sword arc bloom", slash, pos, 5.3f, Alpha(light, .5f), .26f, 129, 0, false); bloom.scale.x *= side; bloom.startScale = .9f; bloom.endScale = 1.12f; bloom.fadePower = 1.1f;
            var arc = Sheet("Sword arc", slash, pos, 4.8f, Alpha(light, 1), .24f, 130, 0, false); arc.scale.x *= side; arc.startScale = .88f; arc.endScale = 1.08f; arc.fadePower = 1;
            var edge = Sheet("Sword arc edge", slash, pos, 4.4f, new Color(1, 1, 1, .85f), .21f, 131, 0, false); edge.scale.x *= side; edge.startScale = .9f; edge.endScale = 1.05f; edge.fadePower = 1.3f;
            var wind = Puff(pos + new Vector2(side * .9f, -.15f), 2.1f, new Color(1, 1, 1, .12f), new Vector2(side * 2.6f, -.7f), .3f, 129); wind.spin = -side * 70; wind.endScale = 1.9f;
            var flash = Spawn("Edge flash", glow, pos + new Vector2(side * 1.25f, .15f), 1.2f, new Color(1, 1, 1, .6f), .1f, true, 131); flash.endScale = 1.7f; flash.ease = 1;
            for (int i = 0; i < 5; i++) Spark(pos + new Vector2(side * Range(1.1f, 1.6f), Range(-.3f, .5f)), new Vector2(side * Range(3, 6.5f), Range(-1.5f, 2.5f)), i % 2 == 0 ? Color.white : light, Range(.25f, .45f), Range(.22f, .38f), 7, 2.5f);
        }

        // Combo finishers and dash slashes: a double arc with a shock ring, rays, sparks and ground dust.
        public void HeavySlash(Vector2 pos, int facing, Color tint)
        {
            EnsureTextures();
            int side = facing < 0 ? -1 : 1;
            Color light = Color.Lerp(tint, Color.white, .3f);
            var arc = Sheet("Finisher arc", slash, pos, 6.2f, Alpha(light, .95f), .3f, 130, 0, false); arc.scale.x *= side; arc.scale.y *= 1.12f; arc.startScale = .85f; arc.endScale = 1.12f; arc.fadePower = 1.3f;
            var edge = Sheet("Finisher edge", slash, pos, 5.6f, new Color(1, 1, 1, .7f), .26f, 131, 0, false); edge.scale.x *= side; edge.scale.y *= 1.1f; edge.startScale = .88f; edge.endScale = 1.08f;
            var second = Sheet("Finisher second arc", slash, pos + new Vector2(side * .3f, .1f), 5.2f, Alpha(light, .7f), .3f, 129, 0, false); second.scale.x *= side; second.angle = -side * 26; second.age = -.05f; second.startScale = .8f; second.endScale = 1.1f;
            Vector2 tip = pos + new Vector2(side * 1.4f, .1f);
            var flash = Spawn("Finisher flash", glow, tip, 2.6f, Alpha(Warm, .85f), .14f, true, 132); flash.endScale = 1.8f; flash.ease = 1; flash.fadePower = 1.5f;
            ImpactRing(tip, .9f, tint);
            Rays(tip, 6, 1.3f, 2.3f, light, .22f);
            for (int i = 0; i < 12; i++) Spark(tip + Inside(.3f), new Vector2(side * Range(2.5f, 8), Range(-2, 4)), i % 3 == 0 ? Color.white : light, Range(.3f, .55f), Range(.25f, .45f), 8, 2.2f);
            for (int i = 0; i < 4; i++) Puff(pos + new Vector2(side * Range(.2f, 1.3f), -.85f), Range(.4f, .7f), DustTone, new Vector2(side * Range(1.5f, 3.5f), Range(.4f, 1.1f)), Range(.4f, .6f), 118);
        }

        public void Dust(Vector2 pos, int direction, float strength)
        {
            EnsureTextures();
            int count = Mathf.Clamp(Mathf.RoundToInt(4 + strength * 5), 3, 14);
            for (int i = 0; i < count; i++)
            {
                float side = direction == 0 ? (i % 2 == 0 ? 1 : -1) : -direction;
                float spread = Range(0, 1);
                var puff = Puff(pos + new Vector2(side * spread * .35f, .08f), Range(.28f, .5f) * (.8f + strength * .4f), DustTone,
                    new Vector2(side * (1 + spread * 2.2f) * (.5f + strength * .5f), .4f + spread * 1.1f), Range(.3f, .55f) + strength * .1f, 118);
                puff.gravity = -.4f; puff.endScale = 1.9f;
            }
            for (int i = 0; i < 3; i++)
            {
                float side = direction == 0 ? (i % 2 == 0 ? 1 : -1) : -direction;
                var grit = Spawn("Grit", core, pos + new Vector2(side * .1f, .1f), Range(.05f, .09f), new Color(.35f, .3f, .25f, .9f), Range(.3f, .45f), false, 119);
                grit.velocity = new Vector2(side * Range(2, 4.5f), Range(1, 3.2f)); grit.gravity = 12; grit.fadePower = .6f;
            }
        }

        public void SpeedLines(Vector2 center, int facing, Color tint)
        {
            EnsureTextures();
            int side = facing < 0 ? -1 : 1;
            for (int i = 0; i < 7; i++)
            {
                float y = center.y + Range(-.85f, .85f), length = Range(1.1f, 2.7f), thickness = Range(.05f, .13f);
                float x = center.x - side * Range(.2f, .8f);
                var line = Line(new Vector2(x, y), new Vector2(x - side * length, y), thickness, i % 2 == 0 ? new Color(1, 1, 1, .75f) : Alpha(tint, .85f), Range(.14f, .26f), 128);
                line.velocity = new Vector2(-side * Range(2, 5), 0); line.fadePower = 1.5f;
                if (i < 2) { var corel = Line(new Vector2(x, y), new Vector2(x - side * length * .8f, y), thickness * .35f, Color.white, .16f, 129); corel.velocity = line.velocity; }
            }
        }

        public void ImpactRing(Vector2 pos, float radius, Color tint)
        {
            EnsureTextures();
            var wave = Spawn("Shock ring", ring, pos, radius * 2.6f, Alpha(tint, .9f), .32f, true, 131); wave.startScale = .15f; wave.endScale = 1; wave.ease = 3; wave.fadePower = 1.2f;
            var inner = Spawn("Shock ring core", ring, pos, radius * 2.2f, new Color(1, 1, 1, .8f), .22f, true, 132); inner.startScale = .1f; inner.endScale = 1; inner.ease = 2.4f; inner.fadePower = 1.5f;
            var flash = Spawn("Impact flash", glow, pos, radius * 1.8f, new Color(1, 1, 1, .7f), .12f, true, 131); flash.endScale = 1.5f; flash.ease = 1;
            for (int i = 0; i < 10; i++)
            {
                Vector2 direction = Direction(i * 36 + Range(-14, 14));
                Spark(pos + direction * radius * .2f, direction * radius * Range(5, 9), i % 2 == 0 ? Color.white : Color.Lerp(tint, Color.white, .3f), Range(.25f, .5f), Range(.28f, .45f), 5, 3.5f);
            }
        }

        public void KillBurst(Vector2 pos, Color tint)
        {
            EnsureTextures();
            Color light = Color.Lerp(tint, Color.white, .4f);
            var flash = Spawn("Defeat flash", glow, pos, 3.8f, new Color(1, 1, 1, .9f), .2f, true, 132); flash.ramp = Ramp(Color.white, tint); flash.endScale = 1.6f; flash.ease = 1; flash.fadePower = 1.3f;
            var heart = Spawn("Defeat core", core, pos, 1.3f, Warm, .12f, true, 133); heart.endScale = .1f; heart.ease = 1;
            ImpactRing(pos, 1.4f, tint);
            Rays(pos, 7, 1.6f, 2.8f, light, .26f);
            for (int i = 0; i < 18; i++) Spark(pos + Inside(.2f), Direction(Range(0, 360)) * Range(4, 9), i % 3 == 0 ? Color.white : light, Range(.25f, .5f), Range(.3f, .55f), 7, 2.5f);
            for (int i = 0; i < 6; i++) { var puff = Puff(pos + Inside(.4f), Range(.5f, .9f), Soot, new Vector2(Range(-1, 1), Range(.8f, 2)), Range(.6f, .9f), 112); puff.endScale = 2.2f; puff.fadeIn = .08f; }
            for (int i = 0; i < 9; i++)
            {
                var soul = Spawn("Rising wisp", wisp, pos + Inside(.5f), Range(.14f, .3f), Alpha(light, .9f), Range(.6f, 1), true, 131);
                soul.velocity = new Vector2(Range(-.7f, .7f), Range(1.5f, 3.2f)); soul.fadeIn = .1f; soul.endScale = .3f; soul.flicker = .3f; soul.drag = .4f;
            }
        }

        public void Hit(Vector2 pos, Color tint)
        {
            EnsureTextures();
            Color light = Color.Lerp(tint, Color.white, .3f);
            var flash = Spawn("Hit flash", glow, pos, .95f, new Color(1, 1, 1, .8f), .09f, true, 132); flash.endScale = 1.7f; flash.ease = 1;
            for (int i = 0; i < 2; i++)
            {
                var cross = Spawn("Hit cross", streak, pos, 1.25f, i == 0 ? new Color(1, 1, 1, .9f) : Alpha(light, .9f), i == 0 ? .12f : .15f, true, 132);
                cross.scale.y *= .18f; cross.angle = i == 0 ? 35 + Range(-10, 10) : -55 + Range(-10, 10); cross.startScale = .3f; cross.endScale = 1; cross.ease = 3;
            }
            for (int i = 0; i < 7; i++) Spark(pos, Direction(Range(0, 360)) * Range(3, 7), i % 2 == 0 ? Color.white : light, Range(.2f, .4f), Range(.2f, .35f), 8, 3);
            var wave = Spawn("Hit ring", ring, pos, .75f, Alpha(tint, .6f), .16f, true, 131); wave.startScale = .2f; wave.endScale = 1; wave.ease = 3;
        }

        // ---------------------------------------------------------------- skill casts and impacts

        public void Cast(SkillEffect effect, Vector2 pos, float radius, Color tint)
        {
            EnsureTextures();
            float size = Mathf.Clamp(radius * 1.1f, 1.4f, 3.8f);
            Color accent = EffectColor(effect), light = Color.Lerp(tint, accent, .5f);
            var bloom = Spawn("Cast bloom", glow, pos, size * 1.4f, Alpha(light, .55f), .45f, true, 124); bloom.fadeIn = .08f; bloom.startScale = .6f; bloom.endScale = 1.25f; bloom.ease = 1; bloom.fadePower = 1.2f; bloom.pulse = .05f;
            Vector2 ground = pos + new Vector2(0, -.75f);
            switch (effect)
            {
                case SkillEffect.Fireball:
                    for (int i = 0; i < 3; i++) Flame(pos + Inside(.3f), size * Range(.45f, .6f), new Vector2(Range(-.5f, .5f), Range(1, 1.6f)), Range(.35f, .45f), 126);
                    Flash(pos, size * .5f, Warm, .15f);
                    Emitter(bloom, Emit.Embers, 50, .4f, FireLight);
                    for (int i = 0; i < 6; i++) Spark(pos, Direction(Range(0, 360)) * Range(2, 4), FireLight, Range(.2f, .4f), Range(.3f, .45f), 2, 1.5f);
                    break;
                case SkillEffect.IceNova:
                    Wave(pos, size * 1.8f, Alpha(IceLight, .85f), .5f, 2.5f);
                    for (int i = 0; i < 8; i++)
                    {
                        Vector2 direction = Direction(i * 45 + Range(-10, 10));
                        var crystal = Spawn("Frost crystal", shard, pos + direction * size * .45f, Range(.18f, .3f), new Color(.85f, .97f, 1, .95f), .45f, true, 127);
                        crystal.velocity = direction * 1.5f + Vector2.up; crystal.gravity = 4; crystal.spin = Range(-300, 300); crystal.fadePower = 1.6f;
                    }
                    Emitter(bloom, Emit.Frost, 24, size * .5f, new Color(.75f, .92f, 1, .45f));
                    Sparkles(pos, 10, size * .6f, new Color(.85f, .97f, 1));
                    break;
                case SkillEffect.ChainLightning:
                    for (int i = 0; i < 4; i++) Bolt(pos, pos + Direction(Range(0, 360)) * size * Range(.6f, .95f), StormLight, .05f, .18f, 0);
                    Flash(pos, size * .9f, new Color(.85f, .9f, 1), .12f).flicker = .5f;
                    bloom.flicker = .5f; Emitter(bloom, Emit.Sparkles, 30, size * .5f, StormLight);
                    break;
                case SkillEffect.Meteor:
                    GroundSigil(ground, size * 2.3f, Alpha(FireLight, .9f), .75f, 35);
                    GroundGlow(ground, size * 2.4f, Alpha(FireLight, .5f), .75f);
                    Emitter(bloom, Emit.Embers, 40, size * .7f, new Color(1, .5f, .15f));
                    break;
                case SkillEffect.ArcaneVolley:
                    var seal = Spawn("Arcane seal", sigil, pos, size * 1.5f, Alpha(ArcaneLight, .9f), .5f, true, 125); seal.spin = -120; seal.fadeIn = .06f; seal.startScale = .5f; seal.endScale = 1.1f; seal.fadePower = 1.3f;
                    Flash(pos, size * .5f, new Color(.9f, .8f, 1), .14f);
                    Emitter(bloom, Emit.Sparkles, 36, size * .55f, ArcaneLight);
                    break;
                case SkillEffect.Shockwave:
                    GroundWave(ground, size * 2.2f, Alpha(GoldLight, .9f), .4f);
                    for (int i = 0; i < 6; i++) Puff(ground + new Vector2(Range(-.4f, .4f), .05f), Range(.35f, .6f), DustTone, new Vector2(Range(-3, 3), Range(.5f, 1.3f)), Range(.35f, .55f), 118);
                    Flash(pos, size * .7f, Warm, .12f);
                    break;
                case SkillEffect.BladeStorm:
                    for (int i = 0; i < 3; i++) { var blade = Spawn("Spinning blade", streak, pos, size * 1.3f, Alpha(GoldLight, .85f), .45f, true, 127); blade.scale.y *= .32f; blade.angle = i * 120; blade.spin = 900; blade.fadeIn = .05f; blade.fadePower = 1.4f; }
                    Emitter(bloom, Emit.Sparkles, 30, size * .5f, GoldLight);
                    break;
                case SkillEffect.DashStrike:
                    var dash = Spawn("Dash flash", streak, pos, size * 2.6f, new Color(1, 1, 1, .85f), .25f, true, 130); dash.scale.y *= .7f; dash.startScale = .4f; dash.endScale = 1.2f; dash.ease = 3; dash.fadePower = 1.5f;
                    var dashTint = Spawn("Dash light", streak, pos, size * 2.9f, Alpha(light, .55f), .28f, true, 129); dashTint.scale.y *= 1.3f; dashTint.startScale = .4f; dashTint.endScale = 1.2f; dashTint.ease = 3;
                    bloom.scale = new Vector3(bloom.scale.x * 1.8f, bloom.scale.y * .55f, 1); bloom.color = Alpha(light, .4f);
                    for (int i = 0; i < 6; i++) { float y = pos.y + Range(-.8f, .8f); int side = i % 2 == 0 ? 1 : -1; var line = Line(new Vector2(pos.x, y), new Vector2(pos.x + side * Range(1.2f, 2.6f), y), Range(.05f, .12f), i % 3 == 0 ? Color.white : Alpha(light, .85f), Range(.15f, .25f), 128); line.velocity = new Vector2(side * 3, 0); }
                    break;
                case SkillEffect.MultiShot:
                case SkillEffect.PierceShot:
                case SkillEffect.RainArrows:
                    Flash(pos, size * .6f, new Color(1, .95f, .8f), .12f);
                    for (int i = 0; i < 3; i++) { var draw = Line(pos + Direction(Range(0, 360)) * size * .7f, pos, .06f, Alpha(GoldLight, .8f), .16f, 128); draw.startScale = .4f; draw.endScale = 1; draw.ease = 3; }
                    Emitter(bloom, Emit.Sparkles, 20, size * .4f, GoldLight);
                    break;
                case SkillEffect.PoisonCloud:
                    for (int i = 0; i < 5; i++) Puff(pos + Inside(.3f), size * Range(.4f, .5f), new Color(.5f, .85f, .3f, .5f), new Vector2(Range(-.6f, .6f), Range(.8f, 1.4f)), Range(.5f, .65f), 126);
                    Bubbles(pos, 6, size * .4f);
                    Emitter(bloom, Emit.Poison, 14, size * .4f, new Color(.45f, .8f, .3f, .45f));
                    break;
                case SkillEffect.HealPulse:
                    GroundSigil(ground, size * 2.4f, Alpha(LifeLight, .85f), .7f, 25);
                    Wave(pos, size * 1.6f, Alpha(LifeLight, .7f), .5f, 2);
                    Star(pos, size * 1.4f, new Color(1, 1, 1, .5f), .3f);
                    Emitter(bloom, Emit.Motes, 40, size * .6f, new Color(.55f, 1, .75f));
                    break;
                case SkillEffect.ShieldPulse:
                    var barrier = Spawn("Shield lattice", hex, pos, size * 1.7f, Alpha(GoldLight, .85f), .6f, true, 127); barrier.fadeIn = .08f; barrier.startScale = .8f; barrier.endScale = 1.05f; barrier.spin = 15; barrier.pulse = .03f; barrier.fadePower = 1.3f;
                    GroundSigil(ground, size * 2.1f, Alpha(GoldLight, .8f), .6f, 30);
                    Wave(pos, size * 1.9f, Alpha(GoldLight, .7f), .45f, 2.5f);
                    Emitter(bloom, Emit.Sparkles, 24, size * .6f, GoldLight);
                    break;
                case SkillEffect.LifeDrain:
                    for (int i = 0; i < 12; i++)
                    {
                        Vector2 offset = Direction(Range(0, 360)) * size * Range(.8f, 1.2f);
                        var drop = Spawn("Drain wisp", wisp, pos + offset, Range(.15f, .3f), Alpha(LifeLight, .9f), .42f, true, 127);
                        drop.velocity = -offset / .4f; drop.fadeIn = .05f; drop.fadePower = .8f; drop.endScale = .5f;
                    }
                    bloom.color = new Color(.2f, .7f, .4f, .6f);
                    Emitter(bloom, Emit.Sparkles, 20, size * .4f, LifeLight);
                    break;
                case SkillEffect.Sentry:
                    Wave(pos, size * 1.5f, Alpha(SteelLight, .8f), .3f, 3);
                    Flash(pos, size * .6f, new Color(.85f, .97f, 1), .12f);
                    for (int i = 0; i < 6; i++) Spark(pos, Direction(Range(0, 360)) * Range(2, 5), SteelLight, Range(.2f, .4f), Range(.25f, .4f), 6, 2);
                    for (int i = 0; i < 3; i++) Puff(pos + new Vector2(Range(-.3f, .3f), -.5f), Range(.35f, .55f), DustTone, new Vector2(Range(-1.2f, 1.2f), Range(.5f, 1)), Range(.4f, .55f), 118);
                    Emitter(bloom, Emit.Sparkles, 20, size * .4f, SteelLight);
                    break;
            }
        }

        public void Impact(SkillEffect effect, Vector2 pos, float radius, Color tint)
        {
            EnsureTextures();
            Color accent = EffectColor(effect), light = Color.Lerp(tint, accent, .5f);
            float r = effect == SkillEffect.Meteor ? Mathf.Clamp(radius, 1, 4) : Mathf.Clamp(radius * 1.2f, .85f, 3);
            switch (effect)
            {
                case SkillEffect.Fireball:
                    Explosion(pos, r, 1);
                    break;
                case SkillEffect.Meteor:
                    Explosion(pos, r, 1.5f);
                    GroundWave(pos + new Vector2(0, -.4f), r * 3.6f, Alpha(FireLight, .9f), .5f);
                    for (int i = 0; i < 10; i++)
                    {
                        var debris = Spawn("Meteor debris", rock[i % 3], pos + Inside(.3f), Range(.15f, .35f), Color.white, Range(.7f, 1.1f), false, 128);
                        debris.velocity = new Vector2(Range(-7, 7), Range(3, 9)); debris.gravity = 16; debris.spin = Range(-400, 400); debris.fadePower = .5f;
                    }
                    for (int i = 0; i < 6; i++) { var puff = Puff(pos + new Vector2(Range(-r, r) * .8f, -.3f), r * Range(.5f, .8f), Soot, new Vector2(Range(-2.5f, 2.5f), Range(.3f, 1)), Range(.7f, 1.1f), 112); puff.age = -Range(0, .08f); }
                    break;
                case SkillEffect.IceNova:
                    Flash(pos, r * 2.6f, new Color(.85f, .95f, 1), .15f);
                    Wave(pos, r * 2.8f, Alpha(IceLight, .9f), .45f, 3);
                    Wave(pos, r * 2.2f, new Color(1, 1, 1, .7f), .3f, 2.5f);
                    GroundWave(pos + new Vector2(0, -.6f), r * 3, Alpha(IceLight, .6f), .5f);
                    for (int i = 0; i < 14; i++)
                    {
                        Vector2 direction = Direction(i * 26 + Range(-12, 12));
                        var crystal = Spawn("Ice shard", shard, pos + direction * .2f, Range(.2f, .42f), new Color(.85f, .97f, 1, .95f), Range(.4f, .65f), true, 128);
                        crystal.velocity = direction * r * Range(3, 7) + Vector2.up * 1.5f; crystal.gravity = 4; crystal.drag = 1.5f; crystal.spin = Range(-360, 360); crystal.fadePower = 1.5f;
                    }
                    for (int i = 0; i < 8; i++) Puff(pos + Inside(r * .4f), r * Range(.5f, .8f), new Color(.75f, .92f, 1, .45f), Direction(Range(0, 360)) * Range(.5f, 1.5f), Range(.5f, .8f), 112);
                    Sparkles(pos, 18, r * .9f, new Color(.85f, .97f, 1));
                    break;
                case SkillEffect.ChainLightning:
                    Discharge(pos, r, light);
                    break;
                case SkillEffect.ArcaneVolley:
                    Flash(pos, r * 2.2f, new Color(.92f, .82f, 1), .15f);
                    var seal = Spawn("Arcane burst seal", sigil, pos, r * 2, Alpha(ArcaneLight, .9f), .35f, true, 129); seal.spin = 400; seal.startScale = .4f; seal.endScale = 1.1f; seal.ease = 3; seal.fadePower = 1.4f;
                    Wave(pos, r * 2, Alpha(ArcaneLight, .8f), .3f, 3);
                    Rays(pos, 4, r * .8f, r * 1.4f, ArcaneLight, .2f);
                    Sparkles(pos, 14, r * .8f, new Color(.85f, .7f, 1));
                    break;
                case SkillEffect.Shockwave:
                    GroundWave(pos + new Vector2(0, -.45f), r * 3.2f, Alpha(GoldLight, .9f), .45f);
                    Flash(pos, r * 1.4f, Warm, .12f);
                    for (int i = 0; i < 10; i++) Puff(pos + new Vector2(Range(-.5f, .5f) * r, -.3f), r * Range(.3f, .55f), DustTone, new Vector2(Range(-3, 3) * r, Range(.6f, 1.6f)), Range(.4f, .65f), 118);
                    for (int i = 0; i < 6; i++) { var pebble = Spawn("Shock pebble", rock[i % 3], pos + new Vector2(Range(-.4f, .4f), -.2f), Range(.08f, .18f), Color.white, Range(.5f, .8f), false, 128); pebble.velocity = new Vector2(Range(-5, 5), Range(3, 7)); pebble.gravity = 15; pebble.spin = Range(-500, 500); pebble.fadePower = .5f; }
                    Rays(pos, 5, r * .9f, r * 1.6f, GoldLight, .2f);
                    break;
                case SkillEffect.BladeStorm:
                    for (int i = 0; i < 4; i++) { var blade = Spawn("Blade flash", streak, pos, r * 2.4f, Alpha(GoldLight, .9f), .35f, true, 129); blade.scale.y *= .3f; blade.angle = i * 90 + Range(0, 45); blade.spin = 1100; blade.startScale = .5f; blade.endScale = 1.1f; blade.fadePower = 1.4f; }
                    Flash(pos, r * 1.2f, Warm, .1f);
                    Wave(pos, r * 2, Alpha(GoldLight, .6f), .25f, 3);
                    for (int i = 0; i < 10; i++) Spark(pos, Direction(Range(0, 360)) * Range(3, 7), i % 2 == 0 ? Color.white : GoldLight, Range(.25f, .45f), Range(.25f, .4f), 6, 2.5f);
                    break;
                case SkillEffect.DashStrike:
                    var slashLight = Spawn("Dash strike flash", streak, pos, r * 3.6f, new Color(1, 1, 1, .85f), .22f, true, 131); slashLight.scale.y *= r * .55f; slashLight.startScale = .4f; slashLight.endScale = 1.3f; slashLight.ease = 3; slashLight.fadePower = 1.5f;
                    var slashTint = Spawn("Dash strike light", streak, pos, r * 4, Alpha(light, .5f), .26f, true, 130); slashTint.scale.y *= r * 1.1f; slashTint.startScale = .4f; slashTint.endScale = 1.3f; slashTint.ease = 3;
                    Wave(pos, r * 2, Alpha(light, .8f), .3f, 3);
                    for (int i = 0; i < 8; i++) Spark(pos, new Vector2(Range(-8, 8), Range(-2, 2)), i % 2 == 0 ? Color.white : light, Range(.25f, .5f), Range(.2f, .4f), 4, 2.5f);
                    Flash(pos, r * 1.3f, Warm, .1f);
                    break;
                case SkillEffect.MultiShot:
                case SkillEffect.PierceShot:
                case SkillEffect.RainArrows:
                    Flash(pos, r * .9f, Warm, .1f);
                    for (int i = 0; i < 9; i++) Spark(pos, Direction(Range(0, 360)) * Range(3, 7.5f), i % 2 == 0 ? Color.white : light, Range(.2f, .4f), Range(.2f, .38f), 9, 2.5f);
                    for (int i = 0; i < 3; i++) Puff(pos + Inside(.2f), Range(.25f, .4f), DustTone, new Vector2(Range(-1, 1), Range(.3f, 1)), Range(.3f, .45f), 118);
                    Wave(pos, r * 1.1f, Alpha(light, .6f), .18f, 3);
                    break;
                case SkillEffect.PoisonCloud:
                    for (int i = 0; i < 6; i++) { var puff = Puff(pos + Inside(r * .3f), r * Range(.55f, .8f), new Color(.5f, .85f, .3f, .55f), new Vector2(Range(-.8f, .8f), Range(.4f, 1.2f)), Range(.6f, .9f), 112); puff.endScale = 2; }
                    Bubbles(pos, 8, r * .6f);
                    Flash(pos, r * 1.8f, PoisonLight, .4f).fadeIn = .05f;
                    Sparkles(pos, 8, r * .6f, new Color(.8f, 1, .55f));
                    break;
                case SkillEffect.HealPulse:
                    var grace = Flash(pos, r * 2.4f, Alpha(LifeLight, .8f), .5f); grace.fadeIn = .05f; grace.startScale = .5f; grace.endScale = 1.3f;
                    Wave(pos, r * 2.6f, Alpha(LifeLight, .8f), .45f, 2.5f);
                    Star(pos, r * 1.8f, new Color(1, 1, 1, .6f), .3f);
                    GroundSigil(pos + new Vector2(0, -.8f), r * 2.2f, Alpha(LifeLight, .7f), .5f, 40);
                    for (int i = 0; i < 18; i++)
                    {
                        var mote = Spawn("Healing mote", wisp, pos + new Vector2(Range(-r, r) * .7f, Range(-.8f, .2f)), Range(.12f, .26f), new Color(.6f, 1, .8f, .95f), Range(.5f, .9f), true, 131);
                        mote.velocity = new Vector2(Range(-.4f, .4f), Range(1.2f, 2.6f)); mote.fadeIn = .1f; mote.endScale = .3f; mote.flicker = .3f;
                    }
                    break;
                case SkillEffect.ShieldPulse:
                    var lattice = Spawn("Shield flash", hex, pos, r * 2.1f, Alpha(GoldLight, .95f), .35f, true, 130); lattice.startScale = .85f; lattice.endScale = 1.25f; lattice.fadePower = 1.6f;
                    Flash(pos, r * 1.6f, new Color(1, .95f, .75f), .12f);
                    Wave(pos, r * 2.4f, Alpha(GoldLight, .8f), .3f, 3);
                    Sparkles(pos, 12, r * .9f, new Color(1, .95f, .7f));
                    break;
                case SkillEffect.LifeDrain:
                    var sink = Flash(pos, r * 2, new Color(.3f, .85f, .5f, .8f), .35f); sink.startScale = 1.2f; sink.endScale = .4f; sink.ease = 1;
                    Wave(pos, r * 1.8f, Alpha(LifeLight, .7f), .3f, 3);
                    for (int i = 0; i < 10; i++) { var drop = Spawn("Drained wisp", wisp, pos + Inside(.2f), Range(.12f, .25f), Alpha(LifeLight, .9f), Range(.3f, .5f), true, 129); drop.velocity = Direction(Range(0, 360)) * Range(1, 3); drop.drag = 2; drop.endScale = .3f; }
                    Sparkles(pos, 10, r * .6f, LifeLight);
                    break;
                case SkillEffect.Sentry:
                    Flash(pos, r * 1.4f, new Color(.75f, .95f, 1), .1f);
                    for (int i = 0; i < 5; i++) Spark(pos, Direction(Range(0, 360)) * Range(3, 6), i % 2 == 0 ? Color.white : SteelLight, Range(.2f, .35f), Range(.18f, .3f), 7, 3);
                    for (int i = 0; i < 2; i++) Puff(pos, Range(.2f, .35f), new Color(.6f, .65f, .7f, .4f), new Vector2(Range(-.6f, .6f), Range(.5f, 1)), Range(.3f, .45f), 118);
                    Wave(pos, r * 1.1f, Alpha(SteelLight, .6f), .18f, 3);
                    break;
                default:
                    Flash(pos, r * 1.6f, light, .15f);
                    Wave(pos, r * 2, Alpha(light, .8f), .3f, 3);
                    for (int i = 0; i < 8; i++) Spark(pos, Direction(Range(0, 360)) * Range(3, 7), light, Range(.2f, .4f), Range(.2f, .4f), 6, 2.5f);
                    break;
            }
        }

        public void Trail(Vector2 from, Vector2 to, SkillEffect effect, Color tint)
        {
            EnsureTextures();
            if (effect == SkillEffect.ChainLightning)
            {
                Color color = StormLight;
                Bolt(from, to, color, .085f, .24f, 1);
                Bolt(from, to, color, .05f, .18f, 0);
                Flash(from, .7f, new Color(.9f, .93f, 1), .12f).flicker = .5f;
                Impact(SkillEffect.ChainLightning, to, .7f, color);
                return;
            }
            Vector2 delta = to - from; float length = delta.magnitude; if (length < .05f) return;
            Vector2 side = new Vector2(-delta.y, delta.x) / length;
            if (effect == SkillEffect.LifeDrain)
            {
                Vector2 previous = from;
                for (int i = 1; i <= 8; i++)
                {
                    float t = i / 8f; Vector2 point = Vector2.Lerp(from, to, t) + side * Mathf.Sin(t * Mathf.PI * 2.2f) * .5f;
                    var vein = Line(previous, point, .16f, new Color(.4f, .9f, .6f, .4f), .38f, 126, 1.2f); vein.age = -t * .12f; vein.fadePower = 1.3f;
                    previous = point;
                }
                for (int i = 0; i < 16; i++)
                {
                    float t = i / 15f; Vector2 point = Vector2.Lerp(from, to, t) + side * Mathf.Sin(t * Mathf.PI * 2.2f) * .5f;
                    var drop = Spawn("Life stream wisp", wisp, point + Inside(.12f), Range(.18f, .34f), Alpha(LifeLight, .95f), .32f, true, 128);
                    drop.velocity = delta / length * 4.5f; drop.age = -t * .2f; drop.fadeIn = .05f; drop.endScale = .4f;
                }
                var absorb = Flash(to, 1.3f, Alpha(LifeLight, .8f), .5f); absorb.fadeIn = .2f; absorb.age = -.1f;
                return;
            }
            if (effect == SkillEffect.Meteor)
            {
                Line(from, to, .5f, Alpha(FireLight, .9f), .3f, 128, 1.05f).fadePower = 1.3f;
                Line(from, to, .18f, Warm, .22f, 129, 1.02f);
                for (int i = 0; i < 10; i++) { float t = Range(0, 1); Spark(Vector2.Lerp(from, to, t) + Inside(.25f), delta / length * Range(2, 5) + Inside(1.5f), FireLight, Range(.2f, .4f), Range(.3f, .5f), 3, 1.5f).age = -t * .1f; }
                for (int i = 0; i < 4; i++) { float t = (i + .5f) / 4; var puff = Flame(Vector2.Lerp(from, to, t) + Inside(.25f), Range(.5f, .8f), delta / length * 1.5f, Range(.3f, .4f), 127); puff.age = -t * .08f; puff.angleOffset = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + 90; }
                return;
            }
            if (effect == SkillEffect.DashStrike)
            {
                Line(from, to, .55f, new Color(1, 1, 1, .9f), .22f, 130, 1.05f).fadePower = 1.5f;
                Line(from, to, 1, Alpha(Color.Lerp(tint, SteelLight, .5f), .6f), .26f, 129, 1.1f);
                for (int i = 0; i < 6; i++) { Vector2 a = Vector2.Lerp(from, to, Range(0, .8f)) + side * Range(-.6f, .6f); var line = Line(a, a + delta / length * Range(.8f, 1.6f), Range(.04f, .1f), i % 2 == 0 ? Color.white : Alpha(tint, .8f), Range(.14f, .24f), 128); line.velocity = delta / length * 3; }
                Sparkles(to, 8, .5f, Color.white);
                return;
            }
            Line(from, to, .35f, Alpha(Color.Lerp(tint, EffectColor(effect), .5f), .85f), .3f, 128, 1.05f);
            Line(from, to, .1f, Color.white, .22f, 129, 1.02f);
        }

        public void DescendMeteor(Vector2 pos, float delay)
        {
            EnsureTextures();
            delay = Mathf.Max(.1f, delay);
            Vector2 start = pos + new Vector2(-3.2f, 7.4f), velocity = (pos - start) / delay;
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            Vector2 direction = velocity.normalized;
            var stone = Spawn("Falling meteor", meteorRock, start, 1.5f, Color.white, delay + .02f, false, 127); stone.velocity = velocity; stone.angle = angle + 45; stone.fadePower = 0; stone.spin = 90;
            var heat = Spawn("Meteor heat", glow, start, 3.2f, Alpha(FireLight, .85f), delay + .02f, true, 126); heat.velocity = velocity; heat.fadePower = 0; heat.pulse = .08f;
            var tail = Sheet("Meteor flames", flame, start - direction * 1.3f, 1.9f, new Color(1, .8f, .5f, .95f), delay + .02f, 126, 22, true); tail.velocity = velocity; tail.angle = angle + 90; tail.fadePower = 0; tail.pulse = .04f;
            var tailLight = Line(start - direction * 3.4f, start + direction * .2f, 1.4f, Alpha(FireLight, .6f), delay + .02f, 125, 1); tailLight.velocity = velocity; tailLight.fadePower = 0;
            Emitter(stone, Emit.Embers, 60, .35f, new Color(1, .55f, .15f)); Emitter(heat, Emit.Smoke, 18, .4f, new Color(.3f, .22f, .2f, .5f));
        }

        // ---------------------------------------------------------------- floor traps

        /// <summary>One tongue of a floor vent's flame column; the game calls it every few frames while the vent erupts.</summary>
        public void VentFlame(Vector2 basePos, float height)
        {
            EnsureTextures();
            var tongue = Flame(basePos + new Vector2(Range(-.2f, .2f), .15f), Range(.75f, 1.05f), new Vector2(Range(-.25f, .25f), height * Range(2.6f, 3.2f)), Range(.32f, .42f), 126);
            tongue.drag = .4f; tongue.endScale = 1.35f;
            var core = Flame(basePos + new Vector2(Range(-.1f, .1f), .1f), Range(.45f, .6f), new Vector2(0, height * Range(1.8f, 2.2f)), Range(.22f, .3f), 127);
            core.color = new Color(1, .97f, .8f, .95f); core.drag = .4f;
            if (Chance(.6f)) Spark(basePos + new Vector2(Range(-.25f, .25f), Range(.3f, height)), new Vector2(Range(-1.2f, 1.2f), Range(3, 6.5f)), Chance(.3f) ? Warm : FireLight, Range(.12f, .22f), Range(.3f, .5f), -1, 1.5f).flicker = .3f;
            if (Chance(.3f)) { var puff = Puff(basePos + new Vector2(Range(-.2f, .2f), height * Range(.75f, 1)), Range(.45f, .75f), Soot, new Vector2(Range(-.4f, .4f), Range(1, 1.8f)), Range(.6f, .9f), 112); puff.gravity = -.4f; }
        }

        /// <summary>Embers and smoke spilling from a vent that is about to erupt.</summary>
        public void VentWarning(Vector2 basePos)
        {
            EnsureTextures();
            Spark(basePos + new Vector2(Range(-.35f, .35f), .05f), new Vector2(Range(-.6f, .6f), Range(1.4f, 3.2f)), FireLight, Range(.1f, .18f), Range(.25f, .45f), 1, 1.5f).flicker = .4f;
            if (Chance(.3f)) Puff(basePos + new Vector2(Range(-.3f, .3f), .1f), Range(.25f, .4f), Soot, new Vector2(Range(-.2f, .2f), Range(.4f, .9f)), Range(.45f, .65f), 112);
        }

        /// <summary>Grit trickling down ahead of a falling rock.</summary>
        public void RockTrickle(Vector2 pos)
        {
            EnsureTextures();
            var grit = Spawn("Falling grit", rock[random.Next(rock.Length)], pos + new Vector2(Range(-.45f, .45f), 0), Range(.05f, .11f), Color.white, .8f, false, 119);
            grit.velocity = new Vector2(Range(-.2f, .2f), -Range(1.5f, 3.5f)); grit.gravity = 14; grit.spin = Range(-400, 400); grit.fadePower = .3f;
        }

        /// <summary>A falling rock bursting on the ground.</summary>
        public void RockImpact(Vector2 pos, Color dust)
        {
            EnsureTextures();
            for (int i = 0; i < 9; i++)
            {
                var chip = Spawn("Rock chip", rock[i % rock.Length], pos + new Vector2(Range(-.3f, .3f), .15f), Range(.12f, .28f), Color.white, Range(.6f, .95f), false, 128);
                chip.velocity = new Vector2(Range(-4.5f, 4.5f), Range(2.5f, 6.5f)); chip.gravity = 16; chip.spin = Range(-600, 600); chip.fadePower = .5f;
            }
            for (int i = 0; i < 6; i++)
            {
                var puff = Puff(pos + new Vector2(Range(-.45f, .45f), .15f), Range(.5f, .85f), new Color(dust.r, dust.g, dust.b, .5f), new Vector2(Range(-2.4f, 2.4f), Range(.3f, 1.2f)), Range(.5f, .8f), 118);
                puff.gravity = -.3f; puff.endScale = 2.2f;
            }
            GroundWave(pos + Vector2.up * .05f, 2.4f, new Color(1, .95f, .85f, .5f), .3f);
        }

        /// <summary>A launch pad firing the hero upward.</summary>
        public void PadLaunch(Vector2 pos, Color tint)
        {
            EnsureTextures();
            var column = Spawn("Launch column", streak, pos + Vector2.up * 1.2f, 2.6f, Alpha(tint, .7f), .28f, true, 129); column.angle = 90; column.scale.y *= .45f; column.startScale = .4f; column.endScale = 1.1f; column.ease = 3; column.fadePower = 1.5f;
            GroundWave(pos + Vector2.up * .2f, 2, Alpha(tint, .85f), .3f);
            Flash(pos + Vector2.up * .3f, 1.4f, Alpha(Color.Lerp(tint, Color.white, .5f), .8f), .14f);
            for (int i = 0; i < 8; i++) Spark(pos + new Vector2(Range(-.4f, .4f), .25f), new Vector2(Range(-1.5f, 1.5f), Range(4, 8)), i % 2 == 0 ? Color.white : tint, Range(.15f, .28f), Range(.25f, .4f), 6, 2);
            Dust(pos, 0, .6f);
        }

        /// <summary>A stone totem shattering.</summary>
        public void TotemBreak(Vector2 pos, Color dust, Color gem)
        {
            EnsureTextures();
            Flash(pos + Vector2.up * .9f, 2.2f, Alpha(gem, .85f), .18f);
            Sparkles(pos + Vector2.up * 1.1f, 10, .3f, gem);
            for (int i = 0; i < 12; i++)
            {
                var chip = Spawn("Totem chip", rock[i % rock.Length], pos + new Vector2(Range(-.3f, .3f), Range(.3f, 1.4f)), Range(.12f, .3f), Color.white, Range(.7f, 1.1f), false, 128);
                chip.velocity = new Vector2(Range(-4, 4), Range(1.5f, 6)); chip.gravity = 15; chip.spin = Range(-500, 500); chip.fadePower = .5f;
            }
            for (int i = 0; i < 5; i++) { var puff = Puff(pos + new Vector2(Range(-.3f, .3f), Range(.3f, 1.2f)), Range(.5f, .9f), new Color(dust.r, dust.g, dust.b, .45f), new Vector2(Range(-1.5f, 1.5f), Range(.3f, 1.2f)), Range(.6f, .9f), 118); puff.endScale = 2; }
        }

        public void Afterimage(Transform actor, float seconds = .25f)
        {
            if (!actor) return;
            EnsureTextures();
            var root = new GameObject("Dash afterimage"); root.transform.SetParent(transform, false);
            Color ghost = new Color(.5f, .92f, 1, .72f);
            var copies = new List<SpriteRenderer>();
            var meshCopies = new List<MeshRenderer>();
            var ownedMeshes = new List<Mesh>();
            foreach (var source in actor.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!source.enabled || source.sprite == null || source.gameObject.name.Contains("Shadow")) continue;
                var part = new GameObject("Afterimage " + source.name); part.transform.SetParent(root.transform, false);
                part.transform.position = source.transform.position; part.transform.rotation = source.transform.rotation;
                part.transform.localScale = source.transform.lossyScale;
                var copy = part.AddComponent<SpriteRenderer>(); copy.sprite = source.sprite; copy.sharedMaterial = Additive;
                copy.sortingOrder = source.sortingOrder - 1; copy.flipX = source.flipX; copy.flipY = source.flipY;
                copy.color = ghost; copies.Add(copy);
            }
            var block = new MaterialPropertyBlock(); block.SetColor("_Color", ghost);
            foreach (var source in actor.GetComponentsInChildren<MeshRenderer>())
            {
                var sourceFilter = source.GetComponent<MeshFilter>();
                if (!source.enabled || sourceFilter == null || sourceFilter.sharedMesh == null) continue;
                var part = new GameObject("Afterimage " + source.name); part.transform.SetParent(root.transform, false);
                part.transform.position = source.transform.position; part.transform.rotation = source.transform.rotation;
                part.transform.localScale = source.transform.lossyScale;
                // Weighted vertices animate in the source mesh. Snapshot the mesh bytes now so the
                // afterimage keeps this pose while the original animation continues.
                var mesh = Instantiate(sourceFilter.sharedMesh); mesh.name = "Frozen afterimage " + sourceFilter.sharedMesh.name;
                part.AddComponent<MeshFilter>().sharedMesh = mesh; ownedMeshes.Add(mesh);
                var copy = part.AddComponent<MeshRenderer>();
                var materials = source.sharedMaterials; var ghosts = new Material[materials.Length];
                for (int i = 0; i < materials.Length; i++) ghosts[i] = GhostMaterial(materials[i]);
                copy.sharedMaterials = ghosts;
                copy.sortingLayerID = source.sortingLayerID; copy.sortingOrder = source.sortingOrder - 1;
                copy.SetPropertyBlock(block); meshCopies.Add(copy);
            }
            clips.Add(new Clip { root = root, copies = copies.ToArray(), meshCopies = meshCopies.ToArray(), ownedMeshes = ownedMeshes.ToArray(), meshTint = block,
                scale = Vector3.one, color = ghost, ramp = Ramp(new Color(.75f, 1, 1), new Color(.25f, .55f, 1)), fadePower = 1.5f, life = Mathf.Max(.02f, seconds), seed = sequence++ });
            Vector2 center = actor.position + Vector3.up * .9f;
            for (int i = 0; i < 4; i++)
            {
                var mote = Spawn("Afterimage mote", glow, center + new Vector2(Range(-.4f, .4f), Range(-.7f, .7f)), Range(.1f, .22f), new Color(.7f, .97f, 1, .9f), .2f, true, 131);
                mote.velocity = new Vector2(Range(-.6f, .6f), Range(.4f, 1.2f)); mote.flicker = .5f; mote.endScale = .2f;
            }
        }

        // ---------------------------------------------------------------- composite builders

        private void Explosion(Vector2 pos, float r, float intensity)
        {
            var flash = Spawn("Explosion flash", glow, pos, r * 3.4f * intensity, new Color(1, 1, 1, .95f), .18f, true, 132); flash.ramp = Ramp(Color.white, FireLight); flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.5f;
            var heart = Spawn("Explosion core", core, pos, r * 1.2f, Warm, .14f, true, 133); heart.startScale = .3f; heart.endScale = 1.3f; heart.ease = 3; heart.fadePower = 2;
            int flames = Mathf.RoundToInt(6 * intensity);
            for (int i = 0; i < flames; i++)
            {
                Vector2 direction = Direction(i * 360f / flames + Range(-20, 20));
                var tongue = Flame(pos + direction * r * .2f, r * Range(.6f, 1) * intensity, direction * r * Range(1.5f, 3) + Vector2.up, Range(.3f, .5f), 130);
                tongue.alignVelocity = true; tongue.angleOffset = -90; tongue.endScale = 1.4f;
            }
            Wave(pos, r * 3 * intensity, new Color(1, .6f, .25f, .9f), .4f, 3.2f);
            Wave(pos, r * 2.2f, new Color(1, 1, 1, .8f), .25f, 2.5f);
            int puffs = Mathf.RoundToInt(8 * intensity);
            for (int i = 0; i < puffs; i++)
            {
                Vector2 direction = Direction(Range(0, 360));
                var puff = Puff(pos + direction * r * .15f, r * Range(.6f, 1.1f) * intensity, Soot, direction * r * Range(.8f, 2) + Vector2.up * 1.2f, Range(.6f, 1), 112);
                puff.drag = 2; puff.gravity = -.6f; puff.endScale = 2.4f; puff.fadeIn = .08f; puff.age = -Range(0, .06f);
            }
            int embers = Mathf.RoundToInt(22 * intensity);
            for (int i = 0; i < embers; i++) Spark(pos + Inside(.2f), Direction(Range(0, 360)) * r * Range(3, 8) + Vector2.up * 2, i % 3 == 0 ? new Color(1, .9f, .6f) : FireLight, Range(.2f, .45f), Range(.35f, .7f), 7, 2.5f).flicker = .3f;
            Rays(pos, 6, r * 1.5f, r * 2.6f, new Color(1, .75f, .4f), .2f);
        }

        private void Discharge(Vector2 pos, float r, Color light)
        {
            Flash(pos, r * 2, new Color(.85f, .9f, 1, .95f), .14f).flicker = .5f;
            for (int i = 0; i < 6; i++) Bolt(pos, pos + Direction(Range(0, 360)) * r * Range(.8f, 1.5f), StormLight, .06f, .2f, 0);
            for (int i = 0; i < 10; i++) Spark(pos, Direction(Range(0, 360)) * Range(3, 7), i % 2 == 0 ? Color.white : StormLight, Range(.2f, .4f), Range(.2f, .35f), 6, 3);
            Wave(pos, r * 1.6f, Alpha(Color.Lerp(light, ArcaneLight, .5f), .7f), .25f, 3);
        }

        private void Bolt(Vector2 from, Vector2 to, Color color, float width, float life, int depth)
        {
            Vector2 delta = to - from; float length = delta.magnitude; if (length < .05f) return;
            int segments = Mathf.Clamp(Mathf.RoundToInt(length * 3.2f), 4, 18);
            Vector2 side = new Vector2(-delta.y, delta.x) / length;
            Vector2 previous = from; float jitter = Mathf.Min(.45f, length * .09f);
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float offset = i == segments ? 0 : Range(-jitter, jitter) * (.4f + .6f * Mathf.Sin(t * Mathf.PI));
                Vector2 point = Vector2.Lerp(from, to, t) + side * offset;
                Beam(previous, point, color, width, life);
                if (depth > 0 && i > 1 && i < segments - 1 && Chance(.3f))
                {
                    Vector2 branch = point + delta / segments * Range(1.5f, 3) + side * Range(-.6f, .6f) * Mathf.Max(.5f, length * .2f);
                    Bolt(point, branch, color, width * .55f, life * .8f, depth - 1);
                }
                previous = point;
            }
        }

        private void Beam(Vector2 a, Vector2 b, Color color, float width, float life)
        {
            var halo = Line(a, b, width * 3.4f, Alpha(color, .5f), life, 128, 1.3f); halo.flicker = .35f; halo.fadePower = 1.6f;
            var mid = Line(a, b, width * 1.5f, Alpha(Color.Lerp(color, Color.white, .5f), .85f), life, 129, 1.2f); mid.flicker = .3f; mid.fadePower = 1.4f;
            var inner = Line(a, b, width * .55f, Color.white, life * .9f, 130, 1.15f); inner.flicker = .25f; inner.fadePower = 1.3f;
        }

        private Clip Flash(Vector2 pos, float width, Color color, float life)
        {
            var flash = Spawn("Flash", glow, pos, width, color, life, true, 131); flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.4f; return flash;
        }

        private Clip Wave(Vector2 pos, float width, Color color, float life, float ease)
        {
            var wave = Spawn("Expanding ring", ring, pos, width, color, life, true, 131); wave.startScale = .12f; wave.endScale = 1; wave.ease = ease; wave.fadePower = 1.2f; return wave;
        }

        private Clip GroundWave(Vector2 pos, float width, Color color, float life)
        {
            var wave = Wave(pos, width, color, life, 3); wave.scale.y *= .34f; wave.renderer.sortingOrder = 116; return wave;
        }

        private Clip GroundSigil(Vector2 pos, float width, Color color, float life, float spin)
        {
            var seal = Spawn("Ground sigil", sigil, pos, width, color, life, true, 27); seal.scale.y *= .36f; seal.spin = spin; seal.fadeIn = .12f; seal.fadePower = 1.5f; seal.startScale = .85f; seal.endScale = 1.05f; seal.ease = 1;
            var halo = Spawn("Sigil halo", glow, pos, width * 1.1f, Alpha(color, .35f), life, true, 26); halo.scale.y *= .36f; halo.fadeIn = .12f; halo.fadePower = 1.5f; halo.pulse = .05f;
            return seal;
        }

        private Clip GroundGlow(Vector2 pos, float width, Color color, float life)
        {
            var halo = Spawn("Ground light", glow, pos, width, color, life, true, 26); halo.scale.y *= .35f; halo.fadeIn = .1f; halo.fadePower = 1.4f; halo.pulse = .06f; return halo;
        }

        private void Star(Vector2 pos, float width, Color color, float life)
        {
            for (int i = 0; i < 2; i++) { var ray = Spawn("Star flare", streak, pos, width, color, life, true, 131); ray.scale.y *= .12f; ray.angle = i * 90; ray.startScale = .3f; ray.endScale = 1; ray.ease = 3; ray.fadePower = 1.4f; }
        }

        private void Rays(Vector2 pos, int count, float minLength, float maxLength, Color color, float life)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * 360f / count + Range(-15, 15), length = Range(minLength, maxLength);
                var ray = Line(pos, pos + Direction(angle) * length, Range(.1f, .2f), i % 2 == 0 ? new Color(1, 1, 1, .8f) : Alpha(color, .8f), life, 130, 1);
                ray.startScale = .3f; ray.endScale = 1; ray.ease = 3; ray.fadePower = 1.5f;
            }
        }

        private void Sparkles(Vector2 pos, int count, float spread, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                var mote = Spawn("Sparkle", glow, pos + Inside(spread), Range(.08f, .2f), Alpha(color, .95f), Range(.25f, .5f), true, 131);
                mote.velocity = Direction(Range(0, 360)) * Range(.5f, 2.5f); mote.drag = 2; mote.flicker = .6f; mote.endScale = .1f; mote.ease = 1;
            }
        }

        private void Bubbles(Vector2 pos, int count, float spread)
        {
            for (int i = 0; i < count; i++)
            {
                var bubble = Spawn("Poison bubble", ring, pos + Inside(spread), Range(.08f, .18f), new Color(.75f, 1, .5f, .8f), Range(.35f, .6f), true, 127);
                bubble.velocity = new Vector2(Range(-.3f, .3f), Range(.6f, 1.4f)); bubble.endScale = 1.5f; bubble.ease = 1; bubble.fadePower = 1.5f;
            }
        }

        private Clip Spark(Vector2 pos, Vector2 velocity, Color color, float width, float life, float gravity, float drag)
        {
            var clip = Spawn("Spark", spark, pos, width, color, life, true, 131);
            clip.velocity = velocity; clip.gravity = gravity; clip.drag = drag; clip.alignVelocity = true; clip.endScale = .25f; clip.ease = 1; clip.fadePower = 1.2f;
            clip.scale.y *= .7f;
            return clip;
        }

        private Clip Puff(Vector2 pos, float width, Color color, Vector2 velocity, float life, int order)
        {
            var clip = Spawn("Smoke", smoke[random.Next(smoke.Length)], pos, width, color, life, false, order);
            clip.velocity = velocity; clip.drag = 3.2f; clip.startScale = .55f; clip.endScale = 1.8f; clip.ease = 1; clip.spin = Range(-90, 90); clip.fadeIn = .04f; clip.fadePower = 1.3f;
            clip.angle = Range(0, 360);
            return clip;
        }

        private Clip Flame(Vector2 pos, float width, Vector2 velocity, float life, int order)
        {
            var clip = Sheet("Flame", flame, pos, width, new Color(1, .85f, .55f, .95f), life, order, 22, true);
            clip.velocity = velocity; clip.drag = 1.5f; clip.startScale = .7f; clip.endScale = 1.5f; clip.ease = 1; clip.angle = Range(-15, 15); clip.fadePower = 1.3f;
            return clip;
        }

        // ---------------------------------------------------------------- clip construction

        private Clip Spawn(string name, Sprite sprite, Vector2 pos, float width, Color color, float life, bool additive, int order)
        {
            GameObject root; SpriteRenderer renderer;
            if (pool.Count > 0) { root = pool.Pop(); root.SetActive(true); renderer = root.GetComponent<SpriteRenderer>(); }
            else { root = new GameObject(); root.transform.SetParent(transform, false); renderer = root.AddComponent<SpriteRenderer>(); }
            root.name = name;
            root.transform.position = new Vector3(pos.x, pos.y, 0); root.transform.localRotation = Quaternion.identity;
            renderer.enabled = true; renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order; renderer.flipX = false; renderer.flipY = false;
            renderer.sharedMaterial = additive ? Additive : AlphaBlend;
            var clip = new Clip { root = root, renderer = renderer, position = pos, color = color, life = life, seed = sequence++, scale = Vector3.one * (width / Mathf.Max(.01f, sprite.bounds.size.x)) };
            root.transform.localScale = clip.scale; clips.Add(clip); return clip;
        }

        private Clip Sheet(string name, Sprite[] frames, Vector2 pos, float width, Color color, float life, int order, float fps, bool loop)
        {
            var clip = Spawn(name, frames[0], pos, width, color, life, true, order);
            clip.frames = frames; clip.fps = fps; clip.loop = loop; return clip;
        }

        private Clip Attach(Transform root, string name, Sprite sprite, float width, Color color, int order, bool additive = true, Vector2 offset = default(Vector2))
        {
            var go = new GameObject(name); go.transform.SetParent(root, false); go.transform.localPosition = offset;
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order; renderer.sharedMaterial = additive ? Additive : AlphaBlend;
            var clip = new Clip { root = go, renderer = renderer, external = root, offset = offset, color = color, life = Infinite, fadePower = 0, loop = true, seed = sequence++, scale = Vector3.one * (width / Mathf.Max(.01f, sprite.bounds.size.x)) };
            go.transform.localScale = clip.scale; clips.Add(clip); return clip;
        }

        private void Bundle(Transform root, Clip primary, params Clip[] layers)
        {
            primary.registered = true; primary.layers = new List<Clip>(layers); attached[root] = primary;
        }

        private static void Emitter(Clip clip, Emit kind, float rate, float spread, Color color)
        {
            clip.emit = kind; clip.emitRate = rate; clip.emitSpread = spread; clip.emitColor = color;
        }

        private Clip Line(Vector2 a, Vector2 b, float thickness, Color color, float life, int order, float overlap = 1)
        {
            Vector2 delta = b - a; float length = Mathf.Max(.02f, delta.magnitude) * overlap;
            var clip = Spawn("Light streak", streak, (a + b) * .5f, length, color, life, true, order);
            clip.scale = new Vector3(length / streak.bounds.size.x, thickness / streak.bounds.size.y, 1);
            clip.angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg; clip.ease = 1;
            clip.root.transform.localScale = clip.scale; clip.root.transform.localRotation = Quaternion.Euler(0, 0, clip.angle);
            return clip;
        }

        private void Emission(Clip clip, float dt)
        {
            clip.emitBank += clip.emitRate * dt;
            int count = Mathf.Min(6, (int)clip.emitBank); if (count <= 0) return;
            clip.emitBank = Mathf.Min(clip.emitBank - count, 2);
            Vector2 origin = clip.root.transform.position;
            for (int i = 0; i < count; i++)
            {
                Vector2 pos = origin + Inside(clip.emitSpread);
                switch (clip.emit)
                {
                    case Emit.Embers:
                        var ember = Spark(pos, Direction(Range(0, 360)) * Range(.6f, 2.2f) + Vector2.up * 1.2f, Color.Lerp(clip.emitColor, Color.white, Range(0, .4f)), Range(.12f, .28f), Range(.35f, .7f), -1.5f, 1.5f);
                        ember.flicker = .4f; break;
                    case Emit.Smoke:
                        var puff = Puff(pos, Range(.35f, .6f), clip.emitColor, new Vector2(Range(-.4f, .4f), Range(.6f, 1.4f)), Range(.5f, .9f), 112);
                        puff.startScale = .6f; puff.endScale = 2.2f; puff.fadeIn = .08f; break;
                    case Emit.Sparkles:
                        var mote = Spawn("Sparkle", glow, pos, Range(.08f, .18f), Alpha(Color.Lerp(clip.emitColor, Color.white, .4f), .95f), Range(.2f, .45f), true, 131);
                        mote.velocity = Direction(Range(0, 360)) * Range(.3f, 1.2f); mote.flicker = .6f; mote.endScale = 0; mote.ease = 1; break;
                    case Emit.Frost:
                        var mist = Puff(pos, Range(.3f, .5f), clip.emitColor, new Vector2(Range(-.5f, .5f), Range(.2f, .6f)), Range(.5f, .8f), 112); mist.endScale = 2;
                        if (i % 2 == 0) { var glint = Spawn("Frost glint", glow, pos, Range(.06f, .14f), new Color(.85f, .97f, 1, .95f), Range(.25f, .45f), true, 131); glint.velocity = new Vector2(Range(-.4f, .4f), Range(-.8f, -.2f)); glint.flicker = .6f; glint.endScale = .1f; }
                        break;
                    case Emit.Poison:
                        var haze = Puff(pos, Range(.45f, .8f), clip.emitColor, new Vector2(Range(-.4f, .4f), Range(.25f, .7f)), Range(.8f, 1.4f), 112); haze.endScale = 2.2f; haze.fadeIn = .15f; haze.drag = 1.5f;
                        if (i % 3 == 0) { var bubble = Spawn("Poison bubble", ring, pos, Range(.08f, .16f), new Color(.75f, 1, .5f, .8f), Range(.4f, .7f), true, 127); bubble.velocity = new Vector2(Range(-.3f, .3f), Range(.5f, 1.2f)); bubble.endScale = 1.5f; bubble.ease = 1; bubble.fadePower = 1.5f; }
                        break;
                    case Emit.Motes:
                        var heal = Spawn("Healing mote", wisp, pos, Range(.1f, .22f), Alpha(clip.emitColor, .95f), Range(.5f, .9f), true, 131);
                        heal.velocity = new Vector2(Range(-.3f, .3f), Range(1.2f, 2.4f)); heal.fadeIn = .1f; heal.endScale = .3f; heal.flicker = .3f; break;
                    case Emit.Trace:
                        var trace = Spawn("Light trace", glow, pos, Range(.25f, .4f), clip.emitColor, Range(.18f, .26f), true, clip.renderer ? clip.renderer.sortingOrder - 1 : 120);
                        trace.endScale = .2f; trace.ease = 1; trace.fadePower = 1.2f; break;
                    case Emit.Flames:
                        Flame(pos, Range(.35f, .6f), new Vector2(Range(-.3f, .3f), Range(.5f, 1.2f)), Range(.25f, .4f), 127); break;
                }
            }
        }

        // ---------------------------------------------------------------- helpers

        private float Range(float min, float max) { return min + (float)random.NextDouble() * (max - min); }
        private bool Chance(float probability) { return random.NextDouble() < probability; }
        private Vector2 Inside(float radius) { return radius <= 0 ? Vector2.zero : Direction(Range(0, 360)) * Mathf.Sqrt(Range(0, 1)) * radius; }
        private static Vector2 Direction(float degrees) { float a = degrees * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
        private static Color Alpha(Color color, float alpha) { return new Color(color.r, color.g, color.b, alpha); }
        private static Gradient Ramp(Color from, Color to)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(from, 0), new GradientColorKey(to, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            return gradient;
        }
        private static Color EffectColor(SkillEffect effect)
        {
            switch (effect)
            {
                case SkillEffect.Fireball: case SkillEffect.Meteor: return FireLight;
                case SkillEffect.IceNova: return IceLight;
                case SkillEffect.ChainLightning: return StormLight;
                case SkillEffect.ArcaneVolley: return ArcaneLight;
                case SkillEffect.PoisonCloud: return PoisonLight;
                case SkillEffect.HealPulse: case SkillEffect.LifeDrain: return LifeLight;
                case SkillEffect.Sentry: return SteelLight;
                case SkillEffect.DashStrike: return new Color(.6f, .95f, 1);
                default: return GoldLight;
            }
        }

        private static Material Additive
        {
            get
            {
                if (additiveMaterial) return additiveMaterial;
                Shader shader = Resources.Load<Shader>("Tower/Effects/TowerEffectAdditive");
                if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Additive");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                additiveMaterial = new Material(shader) { name = "Tower additive light" };
                return additiveMaterial;
            }
        }

        private static Material AlphaBlend
        {
            get
            {
                if (alphaMaterial) return alphaMaterial;
                alphaMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Tower smoke and debris" };
                return alphaMaterial;
            }
        }

        private static Material GhostMaterial(Material source)
        {
            Material ghost;
            if (source != null && ghostMaterials.TryGetValue(source, out ghost) && ghost) return ghost;
            ghost = new Material(Additive) { name = "Afterimage " + (source != null ? source.name : "light") };
            if (source != null && source.HasProperty("_MainTex")) ghost.mainTexture = source.mainTexture;
            if (source != null) ghostMaterials[source] = ghost;
            return ghost;
        }

        // ---------------------------------------------------------------- procedural textures

        private delegate Color Shade(float u, float v);
        private delegate Color FrameShade(float u, float v, int frame);

        private static void EnsureTextures()
        {
            if (built) return; built = true;
            fireRamp = new Gradient();
            fireRamp.SetKeys(new[] {
                new GradientColorKey(new Color(.35f, .04f, 0), 0), new GradientColorKey(new Color(.75f, .14f, .02f), .18f), new GradientColorKey(new Color(1, .42f, .08f), .4f),
                new GradientColorKey(new Color(1, .78f, .3f), .65f), new GradientColorKey(new Color(1, .95f, .7f), .85f), new GradientColorKey(new Color(1, 1, .95f), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });

            glow = Build("Light bloom", 256, 256, (u, v) => Radial(u, v, r => Gauss(r, .2f) * .8f + Gauss(r, .5f) * .55f));
            core = Build("Hot core", 128, 128, (u, v) => Radial(u, v, r => 1 - Smooth(.55f, .92f, r)));
            ring = Build("Shock ring", 256, 256, (u, v) => Radial(u, v, r => Gauss(r - .74f, .06f) + Gauss(r - .74f, .16f) * .3f + (r < .74f ? Gauss(r - .74f, .4f) * .12f : 0)));
            streak = Build("Light streak", 256, 64, (u, v) =>
            {
                float body = Mathf.Pow(Mathf.Max(0, 1 - u * u), 1.6f);
                return White(Gauss(v, .5f) * body + Gauss(v, .14f) * Mathf.Pow(Mathf.Max(0, 1 - u * u), 2.5f) * .8f);
            });
            spark = Build("Spark", 128, 32, (u, v) =>
            {
                float head = Gauss(Mathf.Sqrt(Sq((u - .5f) / .16f) + Sq(v / .62f)), 1);
                float p = Mathf.Clamp01((u + 1) / 1.5f);
                float tail = Mathf.Pow(p, 1.6f) * Gauss(v, .22f + p * .3f) * (1 - Smooth(.45f, .6f, u));
                float a = Mathf.Max(head, tail * .85f), l = Mathf.Lerp(.85f, 1, head);
                return new Color(l, l, l, a);
            });
            wisp = Build("Wisp", 64, 64, (u, v) => White(Gauss(Mathf.Sqrt(u * u + Sq(v * .8f - .15f)), .34f) * Window(Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)))));
            smoke = new Sprite[4];
            for (int i = 0; i < smoke.Length; i++)
            {
                float seed = i * 17.3f + 3;
                smoke[i] = Build("Smoke " + i, 160, 160, (u, v) =>
                {
                    float n = Fbm(u * 1.7f + seed, v * 1.7f + seed * .7f, 4), n2 = Fbm(u * 4.2f + seed + 9, v * 4.2f - seed, 3);
                    float r = Mathf.Sqrt(u * u + v * v) * (1 + (n2 - .5f) * .55f);
                    float mask = 1 - Smooth(.4f, 1, r);
                    float a = Mathf.Clamp01((n * 1.6f - .35f) * mask * 1.5f) * Window(Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)));
                    float light = Mathf.Clamp01(.68f + (n2 - .5f) * .55f + v * .14f);
                    return new Color(light, light, light, a);
                });
            }
            flame = Sheet("Flame", 8, 128, 192, (u, v, f) =>
            {
                float phase = f / 8f * Mathf.PI * 2, ox = Mathf.Cos(phase) * .9f, oy = Mathf.Sin(phase) * .9f;
                float h = (v + 1) * .5f;
                float w = h < .35f ? Mathf.Sqrt(Mathf.Max(0, 1 - Sq((h - .35f) / .36f))) * .88f : .88f * Mathf.Pow(Mathf.Max(0, 1 - (h - .35f) / .66f), 1.4f);
                float n = Fbm(u * 2.4f + ox + 5, v * 1.7f + oy - h * .6f + 11, 4);
                float edge = 1 - Mathf.Abs(u) / Mathf.Max(.03f, w);
                float a = Mathf.Clamp01(edge * 1.7f + (n - .5f) * 1.7f) * (1 - Smooth(.82f, 1, h)) * (1 - Smooth(.86f, 1, Mathf.Abs(u)));
                float heat = Mathf.Clamp01(a * (1.15f - h * .9f) * (.55f + n * .9f));
                Color c = fireRamp.Evaluate(heat); c.a = a * Mathf.Lerp(.55f, 1, heat); return c;
            });
            slash = Sheet("Sword arc", 6, 256, 256, (u, v, f) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r < .35f || r > 1) return Color.clear;
                float theta = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
                float lead = 75 - 150 * (f + 1) / 6f, behind = theta - lead;
                if (behind < -8 || behind > 95 || Mathf.Abs(theta) > 78) return Color.clear;
                float t = Mathf.Abs(theta) / 75f, rc = .76f, w = .17f * (1 - .5f * t * t);
                float band = Gauss(r - rc, w), front = Smooth(-8, 0, behind), trail = Mathf.Exp(-Mathf.Max(0, behind) / 34f);
                float blur = .72f + .4f * Fbm(theta * .06f + f * .37f, r * 7, 2);
                float edge = Gauss(r - (rc + w * .95f), .02f) * Mathf.Exp(-Mathf.Max(0, behind) / 60f) * front;
                float erosion = 1 - Smooth(.55f, 1, f / 5f) * Smooth(10, 60, behind);
                float a = Mathf.Clamp01((band * front * trail * blur + edge) * erosion * (f == 5 ? .55f : f == 4 ? .8f : 1));
                float l = Mathf.Lerp(.8f, 1, a); return new Color(l, l, l, a);
            });
            shard = Build("Ice shard", 96, 96, (u, v) =>
            {
                float hx = Mathf.Abs(u) * 2.4f, hy = Mathf.Abs(v), d = Mathf.Max(hx * .866f + hy * .5f, hy);
                float inside = 1 - Smooth(.9f, .98f, d);
                float facet = .5f + .5f * Mathf.Abs(Mathf.Sin((u * 2.6f + v * 4.2f) * 3.1416f)), rim = Gauss(d - .9f, .05f);
                Color c = Color.Lerp(new Color(.55f, .85f, 1), Color.white, Mathf.Clamp01(.55f + facet * .3f + rim * .6f));
                c.a = inside * Mathf.Clamp01(.7f + rim * .4f); return c;
            });
            sigil = Build("Magic sigil", 512, 512, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float deg = Mathf.Atan2(v, u) * Mathf.Rad2Deg; if (deg < 0) deg += 360;
                float a = Gauss(r - .93f, .011f) + Gauss(r - .87f, .007f) * .85f + Gauss(r - .5f, .009f) * .9f + Gauss(r - .22f, .008f) * .8f;
                float tick = Mathf.Abs(Mathf.Repeat(deg + 7.5f, 15) - 7.5f);
                a += (1 - Smooth(.6f, 1.3f, tick)) * (1 - Smooth(.915f, .925f, r)) * Smooth(.865f, .875f, r) * .9f;
                float seg = 1e9f;
                for (int i = 0; i < 6; i++)
                {
                    float a0 = i * 60 * Mathf.Deg2Rad, a1 = ((i + 2) % 6) * 60 * Mathf.Deg2Rad;
                    seg = Mathf.Min(seg, SegmentDistance(u, v, Mathf.Cos(a0) * .84f, Mathf.Sin(a0) * .84f, Mathf.Cos(a1) * .84f, Mathf.Sin(a1) * .84f));
                }
                a += Gauss(seg, .007f) * .9f;
                int slot = (int)(deg / 15); float local = Mathf.Repeat(deg, 15) / 15f; int hash = (slot * 7919 + 13) % 11;
                float len = .35f + hash % 5 * .12f;
                if (local > .18f && local < .18f + len && (hash % 3 != 0 || Mathf.Repeat(local * 9, 2) < 1))
                    a += (1 - Smooth(.705f, .72f, r)) * Smooth(.6f, .615f, r) * .8f * (hash % 2 == 0 ? 1 : Mathf.Repeat(r * 40, 2) < 1 ? 1 : .35f);
                a += Gauss(Mathf.Abs(u), .008f) * (1 - Smooth(.15f, .19f, Mathf.Abs(v))) + Gauss(Mathf.Abs(v), .008f) * (1 - Smooth(.15f, .19f, Mathf.Abs(u)));
                a += (1 - Smooth(.1f, .95f, r)) * .07f;
                return White(Mathf.Clamp01(a) * Window(r));
            });
            arrow = Build("Arrow", 256, 64, (u, v) =>
            {
                Color c = Color.clear;
                float shaft = (1 - Smooth(.1f, .13f, Mathf.Abs(v))) * Smooth(-.95f, -.92f, u) * (1 - Smooth(.5f, .53f, u));
                if (shaft > 0) { float l = .75f + (1 - Mathf.Abs(v) / .13f) * .45f; c = Over(c, new Color(.5f * l, .35f * l, .2f * l, shaft)); }
                float hw = .36f * Mathf.Clamp01(1 - (u - .5f) / .5f);
                float head = Smooth(.47f, .5f, u) * (1 - Smooth(hw - .03f, hw, Mathf.Abs(v))) * (1 - Smooth(.985f, 1, u));
                if (head > 0) { float l = .62f + .35f * Mathf.Clamp01((v + .12f) / .3f) + (Mathf.Abs(v - .04f) < .03f ? .25f : 0); c = Over(c, new Color(.72f * l, .76f * l, .84f * l, head)); }
                float fu = (u + .92f) / .34f;
                if (fu > 0 && fu < 1)
                {
                    float limit = .12f + .3f * Mathf.Pow(fu, .8f) * (1 - Smooth(.85f, 1, fu)), fv = Mathf.Abs(v);
                    float feather = Smooth(.11f, .14f, fv) * (1 - Smooth(limit - .03f, limit, fv));
                    if (feather > 0) { float stripe = .85f + .15f * Mathf.Sin(fu * 40); c = Over(c, v > 0 ? new Color(.82f * stripe, .18f * stripe, .2f * stripe, feather) : new Color(.92f * stripe, .9f * stripe, .84f * stripe, feather)); }
                }
                return c;
            });
            bolt = Build("Arcane bolt", 192, 64, (u, v) =>
            {
                float d = Mathf.Abs(u) + Mathf.Abs(v) / .72f, shape = 1 - Smooth(.92f, 1, d);
                float inner = Gauss(v, .14f) * (1 - Smooth(.55f, .95f, Mathf.Abs(u)));
                Color c = Color.Lerp(new Color(.55f, .35f, 1), new Color(1, .95f, 1), inner);
                c.a = Mathf.Clamp01(shape * .92f + inner * .3f) * Window(Mathf.Abs(u)); return c;
            });
            hex = Build("Shield lattice", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float inside = 1 - Smooth(.93f, .975f, r);
                float a = Gauss(HexEdge(u, v, .2f), .012f) * .85f * inside + inside * .07f + Gauss(r - .95f, .018f) * .95f + Gauss(r - .95f, .07f) * .3f;
                return White(Mathf.Clamp01(a) * Window(r));
            });
            meteorRock = Build("Meteor", 128, 128, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
                float rr = .86f + .1f * (Fbm(Mathf.Cos(th) * 1.3f + 4, Mathf.Sin(th) * 1.3f + 4, 3) - .5f) * 2;
                float inside = 1 - Smooth(rr - .05f, rr + .01f, r); if (inside <= 0) return Color.clear;
                float n = Fbm(u * 4 + 2, v * 4 + 7, 3), crater = Smooth(.62f, .7f, n) * .5f;
                float light = Mathf.Clamp01(.45f + .35f * (-u * .5f + v * .6f) + (n - .5f) * .5f - crater);
                Color stone = Color.Lerp(new Color(.22f, .15f, .12f), new Color(.5f, .36f, .28f), light);
                float heat = Smooth(-.1f, .95f, (u - v) * .72f) * Smooth(.55f, .98f, r / rr);
                Color c = Color.Lerp(stone, new Color(1, .6f, .2f), heat * .95f); c.a = inside; return c;
            });
            rock = new Sprite[3];
            for (int i = 0; i < rock.Length; i++)
            {
                int variant = i;
                rock[i] = Build("Debris " + i, 64, 64, (u, v) =>
                {
                    float r = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
                    float rr = .7f + .2f * (Fbm(Mathf.Cos(th) * 1.5f + variant * 7, Mathf.Sin(th) * 1.5f + variant * 3, 2) - .5f) * 2;
                    float inside = 1 - Smooth(rr - .08f, rr, r), n = Fbm(u * 5 + variant, v * 5 + variant * 2, 2);
                    float light = Mathf.Clamp01(.5f + .4f * (-u * .4f + v * .6f) + (n - .5f) * .4f);
                    return new Color(.42f * light, .36f * light, .31f * light, inside);
                });
            }
            turret = Sheet("Sentry turret", 8, 160, 128, (u, v, f) =>
            {
                Color c = Color.clear;
                float recoil = f == 2 ? .16f : f == 3 ? .08f : 0;
                float plate = (1 - Smooth(.1f, .14f, Mathf.Abs(v + .8f))) * (1 - Smooth(.72f, .8f, Mathf.Abs(u)));
                c = Over(c, Lit(new Color(.26f, .28f, .32f), .9f + (v + .8f) * 2, plate));
                float pedestal = (1 - Smooth(.2f, .23f, Mathf.Abs(u))) * Smooth(-.82f, -.78f, v) * (1 - Smooth(-.12f, -.08f, v));
                c = Over(c, Lit(new Color(.4f, .43f, .48f), 1 - Mathf.Abs(u) / .23f * .5f, pedestal));
                float hx = Mathf.Abs(u) - .36f, hy = Mathf.Abs(v - .18f) - .24f;
                float hd = Mathf.Sqrt(Sq(Mathf.Max(hx, 0)) + Sq(Mathf.Max(hy, 0))) + Mathf.Min(Mathf.Max(hx, hy), 0) - .1f;
                float house = 1 - Smooth(-.015f, .01f, hd);
                c = Over(c, Lit(new Color(.58f, .62f, .68f), .85f + (v - .18f) * .9f - Mathf.Abs(u) * .25f, house));
                c = Over(c, new Color(.2f, .22f, .25f, house * Gauss(v - .22f, .012f) * .8f));
                float bx0 = .3f, bx1 = 1 - recoil;
                float barrel = Smooth(bx0, bx0 + .02f, u) * (1 - Smooth(bx1 - .02f, bx1, u)) * (1 - Smooth(.1f, .13f, Mathf.Abs(v - .2f)));
                c = Over(c, Lit(new Color(.3f, .33f, .38f), 1.1f - Mathf.Abs(v - .2f) / .13f * .6f, barrel));
                float muzzle = Smooth(bx1 - .1f, bx1 - .08f, u) * (1 - Smooth(bx1 - .02f, bx1, u)) * (1 - Smooth(.13f, .16f, Mathf.Abs(v - .2f)));
                c = Over(c, Lit(new Color(.5f, .52f, .56f), 1, muzzle));
                float ld = Mathf.Sqrt(Sq((u + .16f) * 1.25f) + Sq(v - .22f)), lens = 1 - Smooth(.085f, .1f, ld), glowL = .6f + .4f * Mathf.Sin(f / 8f * 6.283f);
                c = Over(c, new Color(.35f * glowL + .3f, .85f * glowL + .15f, 1, lens));
                c = Over(c, new Color(1, 1, 1, lens * Gauss(ld - .03f, .03f) * .8f));
                if (f == 2) { float fd = Mathf.Sqrt(Sq((u - 1.02f) * 1.6f) + Sq(v - .2f)); c = Over(c, new Color(1, .92f, .6f, Gauss(fd, .18f) * .95f)); }
                return c;
            });
        }

        private static Sprite Build(string name, int width, int height, Shade shade)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "Tower effect " + name, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 2 };
            var colors = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + .5f) / height * 2 - 1;
                for (int x = 0; x < width; x++)
                {
                    Color c = shade((x + .5f) / width * 2 - 1, v);
                    colors[y * width + x] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a));
                }
            }
            texture.SetPixels32(colors); texture.Apply(true, false);
            return Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * .5f, PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private static Sprite[] Sheet(string name, int frames, int width, int height, FrameShade shade)
        {
            var sprites = new Sprite[frames];
            for (int f = 0; f < frames; f++) { int frame = f; sprites[f] = Build(name + " " + f, width, height, (u, v) => shade(u, v, frame)); }
            return sprites;
        }

        private static Color White(float alpha) { return new Color(1, 1, 1, Mathf.Clamp01(alpha)); }
        private static Color Radial(float u, float v, Func<float, float> alpha) { float r = Mathf.Sqrt(u * u + v * v); return White(Mathf.Clamp01(alpha(r)) * Window(r)); }
        private static float Window(float r) { return 1 - Smooth(.84f, .99f, r); }
        private static float Sq(float x) { return x * x; }
        private static float Gauss(float x, float width) { return Mathf.Exp(-x * x / (width * width)); }
        private static float Smooth(float from, float to, float x) { return Mathf.SmoothStep(0, 1, Mathf.Clamp01((x - from) / (to - from))); }
        private static float Fbm(float x, float y, int octaves)
        {
            float sum = 0, amplitude = .5f, norm = 0;
            for (int i = 0; i < octaves; i++) { sum += Mathf.PerlinNoise(x, y) * amplitude; norm += amplitude; x = x * 2.03f + 7.1f; y = y * 1.97f + 3.3f; amplitude *= .5f; }
            return sum / norm;
        }
        private static Color Over(Color under, Color over)
        {
            float a = over.a + under.a * (1 - over.a); if (a <= 0) return Color.clear;
            return new Color((over.r * over.a + under.r * under.a * (1 - over.a)) / a, (over.g * over.a + under.g * under.a * (1 - over.a)) / a, (over.b * over.a + under.b * under.a * (1 - over.a)) / a, a);
        }
        private static Color Lit(Color tone, float light, float alpha) { light = Mathf.Clamp(light, 0, 1.4f); return new Color(tone.r * light, tone.g * light, tone.b * light, Mathf.Clamp01(alpha)); }
        private static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay, t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
            return Mathf.Sqrt(Sq(px - (ax + dx * t)) + Sq(py - (ay + dy * t)));
        }
        private static float HexEdge(float x, float y, float s)
        {
            // Flat-top hexagonal lattice: even columns on whole rows, odd columns offset by half a row.
            float columnStep = s * 1.5f, rowStep = s * 1.7320508f, best = 1e9f;
            for (int pass = 0; pass < 2; pass++)
            {
                float ox = pass * columnStep, oy = pass * rowStep * .5f;
                float cx = Mathf.Round((x - ox) / (columnStep * 2)) * columnStep * 2 + ox, cy = Mathf.Round((y - oy) / rowStep) * rowStep + oy;
                float dx = Mathf.Abs(x - cx), dy = Mathf.Abs(y - cy);
                float d = Mathf.Max(dx * .8660254f + dy * .5f, dy);
                best = Mathf.Min(best, Mathf.Abs(d - s * .866f));
            }
            return best;
        }
    }
}

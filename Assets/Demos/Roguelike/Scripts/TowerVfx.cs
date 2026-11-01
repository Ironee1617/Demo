using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>
    /// High-resolution combat effects. Every element is generated once at 128 pixels per unit with trilinear
    /// filtering and drawn as layered additive light or alpha-blended smoke, so slashes, spells, projectiles,
    /// impacts and dash afterimages read as photographic light and volume instead of pixel frames.
    /// This file holds the simulation, the shared builders and the textures; each skill family draws its own
    /// casts, projectiles, impacts, trails and zones in a TowerVfx.&lt;Family&gt;.cs partial.
    /// </summary>
    public sealed partial class TowerVfx : MonoBehaviour
    {
        public const int PixelsPerUnit = 128;
        public const int KnownEffectsCount = 28;
        private const int PoolLimit = 1536;
        // Above SoftCap emitters thin out and Budget() shrinks optional particle counts; nothing is ever refused.
        private const int SoftCap = 900, HardCap = 1600, PrewarmCount = 384;
        private const float Infinite = float.PositiveInfinity;

        private enum Emit { None, Embers, Smoke, Sparkles, Frost, Poison, Motes, Trace, Flames, Custom }

        private sealed class Clip
        {
            public GameObject root; public Transform external; public SpriteRenderer renderer;
            public SpriteRenderer[] copies; public MeshRenderer[] meshCopies; public Mesh[] ownedMeshes; public MaterialPropertyBlock meshTint;
            public List<Clip> layers;
            public Sprite[] frames; public float fps = 24; public bool loop;
            public Vector2 position, velocity, offset; public float drag, gravity;
            public Vector3 scale = Vector3.one; public float startScale = 1, endScale = 1, ease = 2, stretch;
            // Per-axis growth on top of the uniform scale (spikes and pillars grow from their base).
            public Vector2 axisStart = Vector2.one, axisEnd = Vector2.one;
            public Color color = Color.white; public Gradient ramp; public float fadeIn, fadePower = 1, flicker, pulse, hold;
            public float age, life, spin, angle, angleOffset; public int seed;
            public bool alignVelocity, registered, fresh;
            // Set by LayFlat: a squashed parent so the clip spins in the ground plane instead of tilting.
            public Transform pivot;
            // Orbit: when orbit is non-zero the clip circles orbitCenter (which drifts with velocity) at a radius
            // that eases from orbitRadius to orbitRadiusEnd, so motes can spiral in to charge a cast or out to burst.
            public Vector2 orbitCenter; public float orbit, orbitAngle, orbitRadius, orbitRadiusEnd, orbitPower = 1;
            public Emit emit; public float emitRate, emitBank, emitSpread; public Color emitColor; public Action<Clip, Vector2> emitCustom;
        }

        /// <summary>Everything a cast (wind-up) effect may use: where, who, which way and how long until release.</summary>
        private struct CastInfo
        {
            public Vector2 pos, target; public float radius, size, windup; public int facing; public Transform anchor; public Color tint, light;
        }

        private readonly List<Clip> clips = new List<Clip>();
        private readonly List<Clip> expired = new List<Clip>();
        private readonly HashSet<Clip> retiring = new HashSet<Clip>();
        private readonly Dictionary<Transform, Clip> attached = new Dictionary<Transform, Clip>();
        private readonly Stack<GameObject> pool = new Stack<GameObject>();
        private readonly Stack<Transform> pivots = new Stack<Transform>();
        private readonly List<Clip> spawned = new List<Clip>();
        private readonly System.Random random = new System.Random(2026);
        private int sequence;
        public int ActiveCount { get { return clips.Count; } }

        private int grade = 1;
        /// <summary>
        /// The level (1 to 3) of the skill whose effects are being drawn. Gameplay sets it around a skill's calls and puts it
        /// back to 1 afterwards; higher grades draw bigger, denser effects and level three adds its awakening flourish.
        /// </summary>
        public int Grade { get { return grade; } set { grade = Mathf.Clamp(value, 1, 3); } }

        // Orbit skills: gameplay and the drawn orbs share one path so every hit lands where an orb is seen.
        public const float OrbitDegreesPerSecond = 250, OrbitTilt = .55f;
        /// <summary>Offset of orb <paramref name="index"/> of <paramref name="count"/> from its zone root, <paramref name="age"/> seconds after it was cast.</summary>
        public static Vector2 OrbitOffset(int index, int count, float age, float radius)
        {
            float angle = (age * OrbitDegreesPerSecond + index * 360f / Mathf.Max(1, count)) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * OrbitTilt);
        }

        private static bool built;
        private static Sprite glow, core, ring, streak, spark, wisp, shard, sigil, arrow, bolt, hex, meteorRock;
        private static Sprite crescent, burst, glint, pillar, crack, scorch, spike, swirl, petal, flake, orb, runeRing;
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
            var vfx = root.AddComponent<TowerVfx>();
            vfx.Prewarm(PrewarmCount);
            return vfx;
        }

        // A big skill spawns a few hundred sprites in its first frame; building them up front keeps a first cast from hitching.
        private void Prewarm(int count)
        {
            for (int i = pool.Count; i < count; i++)
            {
                var go = new GameObject("Pooled effect"); go.transform.SetParent(transform, false); go.AddComponent<SpriteRenderer>(); go.SetActive(false); pool.Push(go);
            }
        }

        private static bool codeWarmed;
        /// <summary>True once the effect code has been compiled ahead of its first use.</summary>
        public static bool CodeWarm { get; private set; }

        private void Start() { if (!codeWarmed) { codeWarmed = true; StartCoroutine(WarmCode()); } }

        // Mono compiles a method on its first call, so a skill's first cast would compile its whole family in one frame.
        // Compile the effect code (closures included) a slice per frame after load instead.
        private static IEnumerator WarmCode()
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var types = new List<Type> { typeof(TowerVfx) }; types.AddRange(typeof(TowerVfx).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            foreach (var type in types)
            {
                if (type.ContainsGenericParameters) continue;
                foreach (var method in type.GetMethods(all))
                {
                    if (method.IsAbstract || method.ContainsGenericParameters) continue;
                    try { method.MethodHandle.GetFunctionPointer(); } catch (Exception) { }
                    if (watch.Elapsed.TotalMilliseconds > 1.5) { yield return null; watch.Restart(); }
                }
            }
            CodeWarm = true;
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
            Settle();
        }

        private void LateUpdate() { Settle(); }

        // A clip spawned after a tick (gameplay calls, or emitters at the end of Tick) would otherwise be drawn once with
        // Spawn's raw transform: unrotated, full size, opaque, even while its start is still delayed. Pose it at age 0 first.
        private void Settle()
        {
            if (spawned.Count == 0) return;
            for (int i = 0; i < spawned.Count; i++) { var clip = spawned[i]; if (clip.fresh && clip.root) Animate(clip, 0); }
            spawned.Clear();
        }

        private void Animate(Clip clip, float dt)
        {
            clip.fresh = false;
            if (clip.age < 0) { if (clip.renderer) clip.renderer.enabled = false; return; }
            if (clip.renderer && !clip.renderer.enabled) clip.renderer.enabled = true;
            float p = Mathf.Clamp01(clip.age / clip.life);
            if (clip.frames != null && clip.renderer && clip.frames.Length > 1)
            {
                int frame = clip.loop ? (int)(clip.age * clip.fps) % clip.frames.Length : Mathf.Min(clip.frames.Length - 1, (int)(p * clip.frames.Length));
                clip.renderer.sprite = clip.frames[frame];
            }
            var transform = clip.root.transform;
            bool orbiting = clip.orbit != 0 && ReferenceEquals(clip.external, null);
            if (orbiting)
            {
                if (clip.drag > 0) clip.velocity *= 1 / (1 + clip.drag * dt);
                clip.orbitCenter += clip.velocity * dt;
                float radius = Mathf.Lerp(clip.orbitRadius, clip.orbitRadiusEnd, Mathf.Pow(p, clip.orbitPower));
                Vector2 next = clip.orbitCenter + Direction(clip.orbitAngle + clip.age * clip.orbit) * radius;
                if (clip.alignVelocity && (next - clip.position).sqrMagnitude > .000001f) clip.angle = Mathf.Atan2(next.y - clip.position.y, next.x - clip.position.x) * Mathf.Rad2Deg;
                clip.position = next;
                transform.position = new Vector3(next.x, next.y, 0);
            }
            else if (ReferenceEquals(clip.external, null))
            {
                if (clip.gravity != 0) clip.velocity.y -= clip.gravity * dt;
                if (clip.drag > 0) clip.velocity *= 1 / (1 + clip.drag * dt);
                clip.position += clip.velocity * dt;
                transform.position = new Vector3(clip.position.x, clip.position.y, 0);
            }
            else transform.localPosition = clip.offset;
            if (!orbiting)
            {
                if (clip.alignVelocity && clip.velocity.sqrMagnitude > .0001f) clip.angle = Mathf.Atan2(clip.velocity.y, clip.velocity.x) * Mathf.Rad2Deg;
                else clip.angle += clip.spin * dt;
            }
            else if (!clip.alignVelocity) clip.angle += clip.spin * dt;
            float eased = clip.ease <= 0 ? p : 1 - Mathf.Pow(1 - p, clip.ease);
            float s = Mathf.Lerp(clip.startScale, clip.endScale, eased);
            if (clip.pulse > 0) s *= 1 + Mathf.Sin(clip.age * 21 + clip.seed) * clip.pulse;
            Vector3 scale = clip.scale * s;
            if (clip.axisStart != Vector2.one || clip.axisEnd != Vector2.one)
            {
                Vector2 axis = Vector2.LerpUnclamped(clip.axisStart, clip.axisEnd, eased);
                scale.x *= axis.x; scale.y *= axis.y;
            }
            if (clip.stretch > 0) scale.x *= 1 + clip.velocity.magnitude * clip.stretch;
            transform.localRotation = Quaternion.Euler(0, 0, clip.angle + clip.angleOffset);
            transform.localScale = scale;
            Color tint = clip.ramp != null ? clip.ramp.Evaluate(p) * clip.color : clip.color;
            float alpha = tint.a;
            if (clip.fadeIn > 0 && clip.age < clip.fadeIn) alpha *= clip.age / clip.fadeIn;
            if (clip.fadePower > 0)
            {
                float fade = clip.hold > 0 ? Mathf.Clamp01((p - clip.hold) / Mathf.Max(.0001f, 1 - clip.hold)) : p;
                alpha *= Mathf.Pow(1 - fade, clip.fadePower);
            }
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
            clip.fresh = false;
            if (clip.pivot)
            {
                if (clip.root) clip.root.transform.SetParent(transform, false);
                clip.pivot.gameObject.SetActive(false); pivots.Push(clip.pivot);
            }
            clip.pivot = null;
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
            clips.Clear(); attached.Clear(); expired.Clear(); retiring.Clear(); spawned.Clear();
        }

        private void OnDestroy() { Clear(); }

        // ---------------------------------------------------------------- skill entry points (dispatch to the family partials)

        /// <summary>A skill projectile's look. Signature shots come from a learned skill; plain ones are companions' basic shots.</summary>
        public void Projectile(Transform root, SkillEffect effect, Color tint) { Projectile(root, effect, tint, true); }

        public void Projectile(Transform root, SkillEffect effect, Color tint, bool signature)
        {
            if (!root) return;
            EnsureTextures();
            var old = root.GetComponent<SpriteRenderer>(); if (old) old.enabled = false;
            root.localScale = Vector3.one;
            Clip existing; if (attached.TryGetValue(root, out existing)) Retire(existing);
            switch (effect)
            {
                case SkillEffect.Fireball: ProjectileFireball(root, tint, signature); break;
                case SkillEffect.ArcaneVolley: ProjectileArcaneVolley(root, tint, signature); break;
                case SkillEffect.MultiShot: ProjectileMultiShot(root, tint, signature); break;
                case SkillEffect.RainArrows: ProjectileRainArrows(root, tint, signature); break;
                case SkillEffect.PierceShot: ProjectilePierceShot(root, tint, signature); break;
                case SkillEffect.Shockwave: ProjectileShockwave(root, tint, signature); break;
                case SkillEffect.Sentry: ProjectileSentry(root, tint, signature); break;
                case SkillEffect.Boomerang: ProjectileBoomerang(root, tint, signature); break;
                case SkillEffect.Ricochet: ProjectileRicochet(root, tint, signature); break;
                default: DefaultProjectile(root, effect, tint); break;
            }
            if (signature && Grade > 1) GradeProjectile(root, effect, tint);
        }

        /// <summary>A projectile striking its target. Signature hits come from learned skills; plain ones from basic and hostile shots.</summary>
        public void ProjectileHit(SkillEffect effect, Vector2 pos, Color tint, bool signature, Vector2 direction)
        {
            EnsureTextures();
            direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.right;
            switch (effect)
            {
                case SkillEffect.Fireball: HitFireball(pos, tint, signature, direction); break;
                case SkillEffect.ArcaneVolley: HitArcaneVolley(pos, tint, signature, direction); break;
                case SkillEffect.MultiShot: HitMultiShot(pos, tint, signature, direction); break;
                case SkillEffect.RainArrows: HitRainArrows(pos, tint, signature, direction); break;
                case SkillEffect.PierceShot: HitPierceShot(pos, tint, signature, direction); break;
                case SkillEffect.Shockwave: HitShockwave(pos, tint, signature, direction); break;
                case SkillEffect.Sentry: HitSentry(pos, tint, signature, direction); break;
                case SkillEffect.Boomerang: HitBoomerang(pos, tint, signature, direction); break;
                case SkillEffect.Ricochet: HitRicochet(pos, tint, signature, direction); break;
                default: Impact(effect, pos, .35f, tint); break;
            }
        }

        public void AttachZone(GameObject root, SkillEffect effect, float radius, float duration) { AttachZone(root, effect, radius, duration, 1); }

        /// <summary>A lasting area's look. <paramref name="count"/> is how many orbs it carries (Orbit); other zones ignore it.</summary>
        public void AttachZone(GameObject root, SkillEffect effect, float radius, float duration, int count)
        {
            if (!root) return;
            EnsureTextures();
            var old = root.GetComponent<SpriteRenderer>(); if (old) old.enabled = false;
            root.transform.localScale = Vector3.one;
            Clip existing;
            if (attached.TryGetValue(root.transform, out existing)) return;
            float life = Mathf.Max(.1f, duration);
            switch (effect)
            {
                case SkillEffect.Sentry: ZoneSentry(root.transform, radius, life); break;
                case SkillEffect.PoisonCloud: ZonePoisonCloud(root.transform, radius, life); break;
                case SkillEffect.BladeStorm: ZoneBladeStorm(root.transform, radius, life); break;
                case SkillEffect.Fireball: ZoneFireball(root.transform, radius, life); break;
                case SkillEffect.HealPulse: ZoneHealPulse(root.transform, radius, life); break;
                case SkillEffect.Orbit: ZoneOrbit(root.transform, radius, life, Mathf.Max(1, count)); break;
                case SkillEffect.Mine: ZoneMine(root.transform, radius, life); break;
                case SkillEffect.Vortex: ZoneVortex(root.transform, radius, life); break;
                case SkillEffect.WarBanner: ZoneWarBanner(root.transform, radius, life); break;
                default: DefaultZone(root.transform, effect, radius, life); break;
            }
            if (Grade > 1) GradeZone(root.transform, effect, radius, life);
        }

        /// <summary>A fighting phantom of <paramref name="caster"/> standing at <paramref name="root"/> until it bursts.</summary>
        public void AttachDecoy(GameObject root, Transform caster, float radius, float duration, Color tint)
        {
            if (!root) return;
            EnsureTextures();
            var old = root.GetComponent<SpriteRenderer>(); if (old) old.enabled = false;
            root.transform.localScale = Vector3.one;
            Clip existing;
            if (attached.TryGetValue(root.transform, out existing)) return;
            ZoneDecoy(root.transform, caster, radius, Mathf.Max(.1f, duration), tint);
        }

        /// <summary>The wind-up shown while a hero casts. Release happens <paramref name="windup"/> seconds later.</summary>
        public void Cast(SkillEffect effect, Vector2 pos, float radius, Color tint) { Cast(effect, pos, radius, tint, .45f, 1, null, pos + Vector2.right * 3); }

        public void Cast(SkillEffect effect, Vector2 pos, float radius, Color tint, float windup, int facing, Transform anchor, Vector2 target)
        {
            EnsureTextures();
            var c = new CastInfo { pos = pos, target = target, radius = radius, size = Mathf.Clamp(radius * 1.1f, 1.4f, 3.8f), windup = Mathf.Clamp(windup, .08f, 1.2f),
                facing = facing < 0 ? -1 : 1, anchor = anchor, tint = tint, light = Color.Lerp(tint, EffectColor(effect), .5f) };
            switch (effect)
            {
                case SkillEffect.Fireball: CastFireball(c); break;
                case SkillEffect.Meteor: CastMeteor(c); break;
                case SkillEffect.IceNova: CastIceNova(c); break;
                case SkillEffect.PoisonCloud: CastPoisonCloud(c); break;
                case SkillEffect.ChainLightning: CastChainLightning(c); break;
                case SkillEffect.ArcaneVolley: CastArcaneVolley(c); break;
                case SkillEffect.Shockwave: CastShockwave(c); break;
                case SkillEffect.BladeStorm: CastBladeStorm(c); break;
                case SkillEffect.DashStrike: CastDashStrike(c); break;
                case SkillEffect.MultiShot: CastMultiShot(c); break;
                case SkillEffect.PierceShot: CastPierceShot(c); break;
                case SkillEffect.RainArrows: CastRainArrows(c); break;
                case SkillEffect.Sentry: CastSentry(c); break;
                case SkillEffect.HealPulse: CastHealPulse(c); break;
                case SkillEffect.ShieldPulse: CastShieldPulse(c); break;
                case SkillEffect.LifeDrain: CastLifeDrain(c); break;
                case SkillEffect.Boomerang: CastBoomerang(c); break;
                case SkillEffect.Ricochet: CastRicochet(c); break;
                case SkillEffect.HuntMark: CastHuntMark(c); break;
                case SkillEffect.Hook: CastHook(c); break;
                case SkillEffect.Quake: CastQuake(c); break;
                case SkillEffect.Orbit: CastOrbit(c); break;
                case SkillEffect.Decoy: CastDecoy(c); break;
                case SkillEffect.Mine: CastMine(c); break;
                case SkillEffect.Vortex: CastVortex(c); break;
                case SkillEffect.Curse: CastCurse(c); break;
                case SkillEffect.WarBanner: CastWarBanner(c); break;
                case SkillEffect.Bloom: CastBloom(c); break;
            }
            if (Grade > 1) GradeCast(effect, c);
        }

        public void Impact(SkillEffect effect, Vector2 pos, float radius, Color tint) { Impact(effect, pos, radius, tint, 0); }

        /// <summary>A skill's area burst. <paramref name="facing"/> is the caster's direction, or 0 when it does not matter.</summary>
        public void Impact(SkillEffect effect, Vector2 pos, float radius, Color tint, int facing)
        {
            EnsureTextures();
            switch (effect)
            {
                case SkillEffect.Fireball: ImpactFireball(pos, radius, tint, facing); break;
                case SkillEffect.Meteor: ImpactMeteor(pos, radius, tint, facing); break;
                case SkillEffect.IceNova: ImpactIceNova(pos, radius, tint, facing); break;
                case SkillEffect.PoisonCloud: ImpactPoisonCloud(pos, radius, tint, facing); break;
                case SkillEffect.ChainLightning: ImpactChainLightning(pos, radius, tint, facing); break;
                case SkillEffect.ArcaneVolley: ImpactArcaneVolley(pos, radius, tint, facing); break;
                case SkillEffect.Shockwave: ImpactShockwave(pos, radius, tint, facing); break;
                case SkillEffect.BladeStorm: ImpactBladeStorm(pos, radius, tint, facing); break;
                case SkillEffect.DashStrike: ImpactDashStrike(pos, radius, tint, facing); break;
                case SkillEffect.MultiShot: ImpactMultiShot(pos, radius, tint, facing); break;
                case SkillEffect.PierceShot: ImpactPierceShot(pos, radius, tint, facing); break;
                case SkillEffect.RainArrows: ImpactRainArrows(pos, radius, tint, facing); break;
                case SkillEffect.Sentry: ImpactSentry(pos, radius, tint, facing); break;
                case SkillEffect.HealPulse: ImpactHealPulse(pos, radius, tint, facing); break;
                case SkillEffect.ShieldPulse: ImpactShieldPulse(pos, radius, tint, facing); break;
                case SkillEffect.LifeDrain: ImpactLifeDrain(pos, radius, tint, facing); break;
                case SkillEffect.Boomerang: ImpactBoomerang(pos, radius, tint, facing); break;
                case SkillEffect.Ricochet: ImpactRicochet(pos, radius, tint, facing); break;
                case SkillEffect.HuntMark: ImpactHuntMark(pos, radius, tint, facing); break;
                case SkillEffect.Hook: ImpactHook(pos, radius, tint, facing); break;
                case SkillEffect.Quake: ImpactQuake(pos, radius, tint, facing); break;
                case SkillEffect.Orbit: ImpactOrbit(pos, radius, tint, facing); break;
                case SkillEffect.Decoy: ImpactDecoy(pos, radius, tint, facing); break;
                case SkillEffect.Mine: ImpactMine(pos, radius, tint, facing); break;
                case SkillEffect.Vortex: ImpactVortex(pos, radius, tint, facing); break;
                case SkillEffect.Curse: ImpactCurse(pos, radius, tint, facing); break;
                case SkillEffect.WarBanner: ImpactWarBanner(pos, radius, tint, facing); break;
                case SkillEffect.Bloom: ImpactBloom(pos, radius, tint, facing); break;
                default: DefaultImpact(effect, pos, radius, tint); break;
            }
            if (Grade > 1) GradeImpact(effect, pos, radius, tint, facing);
        }

        public void Trail(Vector2 from, Vector2 to, SkillEffect effect, Color tint)
        {
            EnsureTextures();
            if ((to - from).magnitude < .05f && effect != SkillEffect.ChainLightning) return;
            switch (effect)
            {
                case SkillEffect.ChainLightning: TrailChainLightning(from, to, tint); break;
                case SkillEffect.LifeDrain: TrailLifeDrain(from, to, tint); break;
                case SkillEffect.Meteor: TrailMeteor(from, to, tint); break;
                case SkillEffect.DashStrike: TrailDashStrike(from, to, tint); break;
                case SkillEffect.PierceShot: TrailPierceShot(from, to, tint); break;
                case SkillEffect.Shockwave: TrailShockwave(from, to, tint); break;
                case SkillEffect.Ricochet: TrailRicochet(from, to, tint); break;
                case SkillEffect.HuntMark: TrailHuntMark(from, to, tint); break;
                case SkillEffect.Hook: TrailHook(from, to, tint); break;
                case SkillEffect.Curse: TrailCurse(from, to, tint); break;
                case SkillEffect.Bloom: TrailBloom(from, to, tint); break;
                default: DefaultTrail(from, to, effect, tint); break;
            }
        }

        // ---------------------------------------------------------------- generic fallbacks

        private void DefaultProjectile(Transform root, SkillEffect effect, Color tint)
        {
            Color light = Color.Lerp(tint, EffectColor(effect), .5f);
            var primary = Attach(root, "Energy bolt", core, .36f, Color.Lerp(light, Color.white, .6f), 123); primary.pulse = .06f;
            var halo = Attach(root, "Energy halo", glow, 1.15f, Alpha(light, .85f), 122); halo.pulse = .1f;
            Emitter(halo, Emit.Trace, 30, .04f, Alpha(light, .5f));
            Bundle(root, primary, halo);
        }

        private void DefaultZone(Transform root, SkillEffect effect, float radius, float life)
        {
            var field = Attach(root, effect + " field", glow, Mathf.Max(1.5f, radius * 2), Alpha(EffectColor(effect), .5f), 113); field.pulse = .08f; field.life = life; field.fadePower = .8f;
            var edge = Attach(root, effect + " edge", ring, Mathf.Max(1.5f, radius * 2), Alpha(EffectColor(effect), .6f), 113); edge.spin = 60; edge.life = life; edge.fadePower = .8f;
            Bundle(root, field, edge);
        }

        private void DefaultImpact(SkillEffect effect, Vector2 pos, float radius, Color tint)
        {
            Color light = Color.Lerp(tint, EffectColor(effect), .5f); float r = Mathf.Clamp(radius * 1.2f, .85f, 3);
            Flash(pos, r * 1.6f, light, .15f);
            Wave(pos, r * 2, Alpha(light, .8f), .3f, 3);
            for (int i = 0; i < 8; i++) Spark(pos, Direction(Range(0, 360)) * Range(3, 7), light, Range(.2f, .4f), Range(.2f, .4f), 6, 2.5f);
        }

        private void DefaultTrail(Vector2 from, Vector2 to, SkillEffect effect, Color tint)
        {
            Line(from, to, .35f, Alpha(Color.Lerp(tint, EffectColor(effect), .5f), .85f), .3f, 128, 1.05f);
            Line(from, to, .1f, Color.white, .22f, 129, 1.02f);
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

        // ---------------------------------------------------------------- shared builders

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
            var seal = Spawn("Ground sigil", sigil, pos, width, color, life, true, 27); LayFlat(seal, .36f); seal.spin = spin; seal.fadeIn = .12f; seal.fadePower = 1.5f; seal.startScale = .85f; seal.endScale = 1.05f; seal.ease = 1;
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
            count = Budget(count);
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

        /// <summary>A sharp four-point star glint that snaps open and fades.</summary>
        private Clip Glint(Vector2 pos, float width, Color color, float life, float angle = 0)
        {
            var clip = Spawn("Glint", glint, pos, width, color, life, true, 133);
            clip.angle = angle; clip.startScale = .2f; clip.endScale = 1; clip.ease = 4; clip.fadePower = 1.6f; clip.spin = 40;
            return clip;
        }

        /// <summary>Anime impact frame: a ring of tapered speed lines bursting outward from the hit.</summary>
        private Clip ImpactLines(Vector2 pos, float width, Color color, float life)
        {
            var clip = Spawn("Impact lines", burst, pos, width, color, life, true, 132);
            clip.angle = Range(0, 360); clip.startScale = .55f; clip.endScale = 1.15f; clip.ease = 3; clip.fadePower = 1.4f;
            return clip;
        }

        /// <summary>A vertical column of light standing on <paramref name="ground"/> that shoots up and widens.</summary>
        private Clip Pillar(Vector2 ground, float width, float height, Color color, float life, int order = 128)
        {
            var clip = Spawn("Light pillar", pillar, ground, width, color, life, true, order);
            clip.scale = new Vector3(width / pillar.bounds.size.x, height / pillar.bounds.size.y, 1); clip.root.transform.localScale = clip.scale;
            clip.axisStart = new Vector2(.35f, .12f); clip.axisEnd = Vector2.one; clip.ease = 4; clip.fadePower = 1.3f;
            return clip;
        }

        /// <summary>A mark lying on the ground (squashed for the side view) that holds, then fades.</summary>
        private Clip Decal(Vector2 ground, Sprite sprite, float width, Color color, float life, bool additive, int order)
        {
            var clip = Spawn("Ground mark", sprite, ground, width, color, life, additive, order);
            clip.scale.y *= .3f; clip.root.transform.localScale = clip.scale;
            clip.hold = .45f; clip.fadePower = 1.2f; clip.fadeIn = .03f; clip.startScale = .7f; clip.endScale = 1; clip.ease = 6;
            return clip;
        }

        private Clip Scorch(Vector2 ground, float width, float life) { return Decal(ground, scorch, width, new Color(.07f, .05f, .05f, .62f), life, false, 24); }

        /// <summary>Dark ground cracks, with a hot glowing copy when <paramref name="glowColor"/> has alpha.</summary>
        private void Crack(Vector2 ground, float width, Color glowColor, float life)
        {
            var dark = Decal(ground, crack, width, new Color(.08f, .06f, .05f, .85f), life, false, 25); dark.angle = Range(-10, 10);
            if (glowColor.a > 0) { var hot = Decal(ground, crack, width, glowColor, life * .75f, true, 26); hot.angle = dark.angle; hot.fadePower = 1.8f; }
        }

        /// <summary>Crystal or rock spikes erupting from the ground in a fan, centre first.</summary>
        private void Spikes(Vector2 ground, int count, float spread, float height, Color color, float life, bool additive, int order = 127)
        {
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0 : i / (float)(count - 1) * 2 - 1;
                float h = height * Range(.6f, 1) * (1 - .45f * Mathf.Abs(t));
                var s = Spawn("Erupting spike", spike, ground + new Vector2(t * spread + Range(-.1f, .1f), -.05f), h / 3, color, life * Range(.85f, 1.1f), additive, order);
                s.angle = -t * 24 + Range(-8, 8); s.axisStart = new Vector2(.6f, 0); s.axisEnd = Vector2.one; s.ease = 6; s.hold = .55f; s.fadePower = 1.4f;
                s.age = -Mathf.Abs(t) * .07f;
            }
        }

        /// <summary>Motes spiralling in to <paramref name="pos"/> over <paramref name="life"/> seconds: the charge of a cast.</summary>
        private void Converge(Vector2 pos, int count, float radius, Color color, float life, float turns = .35f)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                bool streakMote = i % 3 == 0;
                var mote = Spawn("Gathering light", streakMote ? spark : glow, pos, streakMote ? Range(.25f, .45f) : Range(.12f, .26f), Alpha(color, .95f), life * Range(.7f, 1), true, 131);
                mote.orbitCenter = pos; mote.orbitAngle = Range(0, 360); mote.orbit = (i % 2 == 0 ? 1 : -1) * turns * 360 / life;
                mote.orbitRadius = radius * Range(.65f, 1.1f); mote.orbitRadiusEnd = 0; mote.orbitPower = 1.6f;
                mote.alignVelocity = streakMote; mote.fadeIn = life * .25f; mote.hold = .75f; mote.endScale = .45f; mote.ease = 1;
            }
        }

        private void Debris(Vector2 pos, int count, float speed, float size, float life, Color tint)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                var chip = Spawn("Debris", rock[i % rock.Length], pos + Inside(.25f), size * Range(.5f, 1.2f), tint, life * Range(.7f, 1.1f), false, 128);
                chip.velocity = new Vector2(Range(-1, 1) * speed, Range(.5f, 1.2f) * speed); chip.gravity = 18; chip.spin = Range(-600, 600); chip.fadePower = .6f; chip.hold = .5f;
            }
        }

        private void Shards(Vector2 pos, int count, float speed, float size, Color color, float life)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Direction(i * 360f / Mathf.Max(1, count) + Range(-14, 14));
                var piece = Spawn("Shard", shard, pos + direction * .15f, size * Range(.6f, 1.2f), color, life * Range(.7f, 1), true, 129);
                piece.velocity = direction * speed * Range(.6f, 1.1f) + Vector2.up * speed * .2f; piece.gravity = 6; piece.drag = 1.2f; piece.spin = Range(-500, 500); piece.fadePower = 1.4f;
            }
        }

        private void EmberBurst(Vector2 pos, int count, float speed, Color color, float life, float gravity = 5)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
                Spark(pos + Inside(.2f), Direction(Range(0, 360)) * speed * Range(.35f, 1) + Vector2.up * speed * .25f, i % 4 == 0 ? Color.white : color, Range(.15f, .32f), life * Range(.6f, 1), gravity, 1.6f).flicker = .35f;
        }

        private Clip Swirl(Vector2 pos, float width, Color color, float life, float spin, int order = 129)
        {
            var clip = Spawn("Vortex", swirl, pos, width, color, life, true, order); clip.spin = spin; clip.angle = Range(0, 360); clip.fadeIn = .06f; clip.fadePower = 1.3f; return clip;
        }

        private Clip Orb(Vector2 pos, float width, Color color, float life, int order = 130)
        {
            var clip = Spawn("Energy sphere", orb, pos, width, color, life, true, order); clip.fadePower = 1.2f; return clip;
        }

        private Clip Crescent(Vector2 pos, float width, Color color, float life, float angle, int order = 131)
        {
            var clip = Spawn("Crescent slash", crescent, pos, width, color, life, true, order); clip.angle = angle; clip.startScale = .75f; clip.endScale = 1.12f; clip.ease = 3; clip.fadePower = 1.3f; return clip;
        }

        private Clip RuneCircle(Vector2 pos, float width, Color color, float life, float spin, bool onGround, int order = 125)
        {
            var clip = Spawn("Rune circle", runeRing, pos, width, color, life, true, onGround ? 27 : order);
            if (onGround) LayFlat(clip, .34f);
            clip.spin = spin; clip.fadeIn = .08f; clip.startScale = .6f; clip.endScale = 1; clip.ease = 4; clip.fadePower = 1.4f; clip.hold = .4f;
            return clip;
        }

        /// <summary>Lays a clip on the ground for the side view. A free clip gets a squashed parent, so its spin and angle turn
        /// in the ground plane; squashing the clip itself would make a spinning ring tilt and stand up as it turns.</summary>
        private Clip LayFlat(Clip clip, float squash)
        {
            if (!ReferenceEquals(clip.external, null) || clip.pivot) { clip.scale.y *= squash; clip.root.transform.localScale = clip.scale; return clip; }
            Transform pivot;
            if (pivots.Count > 0) { pivot = pivots.Pop(); pivot.gameObject.SetActive(true); }
            else { pivot = new GameObject("Ground plane").transform; pivot.SetParent(transform, false); }
            pivot.position = new Vector3(clip.position.x, clip.position.y, 0); pivot.localRotation = Quaternion.identity; pivot.localScale = new Vector3(1, squash, 1);
            clip.root.transform.SetParent(pivot, true); clip.pivot = pivot;
            clip.root.transform.localScale = clip.scale;
            return clip;
        }

        /// <summary>A soft dark halo drawn behind bright light so it still reads on pale skies. It sits just behind the actors
        /// (they start at 30), so it darkens the backdrop around a caster or a hit without dimming the fighters themselves.</summary>
        private Clip Underlay(Vector2 pos, float width, float alpha, float life)
        {
            var clip = Spawn("Contrast underlay", glow, pos, width, new Color(.02f, .01f, .03f, alpha), life, false, 29); clip.fadeIn = .03f; clip.fadePower = 1.2f; return clip;
        }

        /// <summary>A full-view wash of light for the biggest hits.</summary>
        private Clip ScreenFlash(Vector2 center, Color color, float life)
        {
            var clip = Spawn("Screen flash", glow, center, 46, color, life, true, 134); clip.ease = 1; clip.fadePower = 2; return clip;
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
            var clip = new Clip { root = root, renderer = renderer, position = pos, color = color, life = life, seed = sequence++, scale = Vector3.one * (width / Mathf.Max(.01f, sprite.bounds.size.x)), fresh = true };
            root.transform.localScale = clip.scale; clips.Add(clip); spawned.Add(clip); return clip;
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
            var clip = new Clip { root = go, renderer = renderer, external = root, offset = offset, color = color, life = Infinite, fadePower = 0, loop = true, seed = sequence++, scale = Vector3.one * (width / Mathf.Max(.01f, sprite.bounds.size.x)), fresh = true };
            go.transform.localScale = clip.scale; clips.Add(clip); spawned.Add(clip); return clip;
        }

        /// <summary>A timed clip that rides on <paramref name="anchor"/> (a caster's hand glow), or a free clip when there is none.</summary>
        private Clip Anchored(Transform anchor, Vector2 worldPos, string name, Sprite sprite, float width, Color color, float life, int order, bool additive = true)
        {
            if (!anchor) return Spawn(name, sprite, worldPos, width, color, life, additive, order);
            Vector3 local = anchor.InverseTransformPoint(new Vector3(worldPos.x, worldPos.y, 0));
            var clip = Attach(anchor, name, sprite, width, color, order, additive, local);
            // Undo the anchor's own scale so widths stay in world units.
            Vector3 lossy = anchor.lossyScale; clip.scale = new Vector3(clip.scale.x / Mathf.Max(.0001f, Mathf.Abs(lossy.x)), clip.scale.y / Mathf.Max(.0001f, Mathf.Abs(lossy.y)), 1);
            clip.root.transform.localScale = clip.scale;
            clip.life = life; clip.fadePower = 1; clip.loop = false;
            return clip;
        }

        private void Bundle(Transform root, Clip primary, params Clip[] layers)
        {
            primary.registered = true; primary.layers = new List<Clip>(layers); attached[root] = primary;
        }

        private static void Emitter(Clip clip, Emit kind, float rate, float spread, Color color)
        {
            clip.emit = kind; clip.emitRate = rate; clip.emitSpread = spread; clip.emitColor = color;
        }

        /// <summary>An emitter whose particles are drawn by <paramref name="spawn"/> (called with the clip and a spawn point).</summary>
        private static void Emitter(Clip clip, float rate, float spread, Action<Clip, Vector2> spawn)
        {
            clip.emit = Emit.Custom; clip.emitRate = rate; clip.emitSpread = spread; clip.emitCustom = spawn;
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
            if (clips.Count > HardCap) return;
            clip.emitBank += clip.emitRate * dt * (clips.Count > SoftCap ? .35f : 1);
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
                        if (i % 2 == 0) { var glintMote = Spawn("Frost glint", glow, pos, Range(.06f, .14f), new Color(.85f, .97f, 1, .95f), Range(.25f, .45f), true, 131); glintMote.velocity = new Vector2(Range(-.4f, .4f), Range(-.8f, -.2f)); glintMote.flicker = .6f; glintMote.endScale = .1f; }
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
                    case Emit.Custom:
                        if (clip.emitCustom != null) clip.emitCustom(clip, pos);
                        break;
                }
            }
        }

        // ---------------------------------------------------------------- helpers

        private float Range(float min, float max) { return min + (float)random.NextDouble() * (max - min); }
        private bool Chance(float probability) { return random.NextDouble() < probability; }
        private Vector2 Inside(float radius) { return radius <= 0 ? Vector2.zero : Direction(Range(0, 360)) * Mathf.Sqrt(Range(0, 1)) * radius; }
        /// <summary>Optional particle counts shrink while many effects are alive, so stacked skills never stall a frame.</summary>
        private int Budget(int count)
        {
            // Higher skill levels draw denser particles (a third more at level two, two thirds more at three).
            if (grade > 1 && count > 1) count = Mathf.RoundToInt(count * (1 + (grade - 1) * .34f));
            if (clips.Count <= 500 || count <= 1) return count;
            float room = Mathf.Clamp01(1 - (clips.Count - 500) / 900f);
            return Mathf.Max(1, Mathf.RoundToInt(count * Mathf.Lerp(.25f, 1, room)));
        }
        private static Vector2 Direction(float degrees) { float a = degrees * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
        private static Color Alpha(Color color, float alpha) { return new Color(color.r, color.g, color.b, alpha); }
        private static Gradient Ramp(Color from, Color to)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(from, 0), new GradientColorKey(to, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            return gradient;
        }
        private static Gradient Ramp(Color from, Color middle, Color to)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(from, 0), new GradientColorKey(middle, .35f), new GradientColorKey(to, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
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
                case SkillEffect.Boomerang: return new Color(1, .8f, .42f);
                case SkillEffect.Ricochet: return new Color(1, .93f, .62f);
                case SkillEffect.HuntMark: return new Color(1, .38f, .34f);
                case SkillEffect.Hook: return new Color(.78f, .84f, .95f);
                case SkillEffect.Quake: return new Color(1, .66f, .34f);
                case SkillEffect.Orbit: return new Color(.52f, .8f, 1);
                case SkillEffect.Decoy: return new Color(.72f, .96f, 1);
                case SkillEffect.Mine: return new Color(1, .46f, .78f);
                case SkillEffect.Vortex: return new Color(.4f, .92f, .96f);
                case SkillEffect.Curse: return new Color(.7f, .38f, .95f);
                case SkillEffect.WarBanner: return new Color(1, .74f, .36f);
                case SkillEffect.Bloom: return new Color(1, .66f, .84f);
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
            BuildActionTextures();
            BuildFireTextures(); BuildFrostTextures(); BuildStormTextures(); BuildBladeTextures(); BuildArcheryTextures(); BuildGraceTextures();
            // The companions' newer families build theirs here too, so no first cast pays for them.
            HuntDiscTextures(); HuntChainTextures(); HuntMarkTextures(); HuntSlugTextures(); EarthTextures(); MysticEnsure(); GradeEnsure();
            StatusVaporTextures(); StatusGuardTextures();
        }

        // Shapes shared by every family: crescent slashes, impact lines, glints, light pillars, ground marks,
        // erupting spikes, vortices, petals, snowflakes, energy spheres and a rune ring.
        private static void BuildActionTextures()
        {
            crescent = Build("Crescent", 256, 256, (u, v) =>
            {
                float outer = Mathf.Sqrt(u * u + v * v) - .9f, inner = Mathf.Sqrt(Sq(u + .34f) + v * v) - .86f;
                float d = Mathf.Max(outer, -inner), body = 1 - Smooth(-.02f, .015f, d), depth = Mathf.Max(0, -outer);
                float rim = Gauss(outer + .03f, .028f) * body, halo = Gauss(Mathf.Max(0, d), .07f) * .4f;
                float a = body * (.2f + .8f * Mathf.Exp(-depth / .15f)) + rim * .7f + halo;
                return White(Mathf.Clamp01(a) * (1 - Smooth(.93f, .995f, Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)))));
            });
            burst = Build("Impact lines", 512, 512, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r < .16f || r > 1) return Color.clear;
                const int spokes = 44;
                float f = Mathf.Repeat(Mathf.Atan2(v, u) / (2 * Mathf.PI), 1) * spokes; int slot = Mathf.FloorToInt(f);
                float a = 0;
                for (int k = -1; k <= 1; k++)
                {
                    int s = (slot + k + spokes) % spokes; uint h = Hash(s + 17);
                    if ((h & 3u) == 0) continue;
                    float center = s + .5f + ((h >> 2 & 255) / 255f - .5f) * .7f, diff = f - center;
                    diff -= Mathf.Round(diff / spokes) * spokes;
                    float start = .18f + (h >> 18 & 63) / 63f * .22f, end = .58f + (h >> 10 & 255) / 255f * .4f;
                    if (r < start || r > end) continue;
                    float along = (r - start) / (end - start), width = (.006f + (h >> 24 & 15) / 15f * .016f) * Mathf.Sin(along * Mathf.PI) + .001f;
                    a = Mathf.Max(a, Gauss(diff * 2 * Mathf.PI * r / spokes, width) * Mathf.Pow(Mathf.Sin(along * Mathf.PI), .6f));
                }
                return White(a * Window(r));
            });
            glint = Build("Glint", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), au = Mathf.Abs(u), av = Mathf.Abs(v);
                float cross = Gauss(v, .02f + .025f * Mathf.Max(0, 1 - au)) * Mathf.Pow(Mathf.Max(0, 1 - au), 2.2f) + Gauss(u, .02f + .025f * Mathf.Max(0, 1 - av)) * Mathf.Pow(Mathf.Max(0, 1 - av), 2.2f);
                float du = (u + v) * .7071f, dv = (u - v) * .7071f;
                float diagonal = (Gauss(dv, .014f) * Mathf.Pow(Mathf.Max(0, 1 - Mathf.Abs(du) / .55f), 3) + Gauss(du, .014f) * Mathf.Pow(Mathf.Max(0, 1 - Mathf.Abs(dv) / .55f), 3)) * .45f;
                return White(Mathf.Clamp01(cross + diagonal + Gauss(r, .08f) + Gauss(r, .22f) * .35f) * Window(Mathf.Max(au, av)));
            });
            pillar = Build("Light pillar", 128, 512, (u, v) =>
            {
                float h = (v + 1) * .5f;
                float column = Gauss(u, .42f) * .5f + Gauss(u, .16f) * .75f + Gauss(u, .05f) * .5f;
                float rise = Smooth(0, .04f, h) * (1 - Smooth(.3f, 1, h)), bands = .78f + .22f * Mathf.PerlinNoise(u * 7 + 3, h * 1.3f);
                return White(Mathf.Clamp01(column * rise * bands) * (1 - Smooth(.86f, .99f, Mathf.Abs(u))));
            }, new Vector2(.5f, 0));
            crack = Build("Ground crack", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float theta = Mathf.Atan2(v, u), a = Gauss(r, .09f) * .6f + Gauss(r - .12f, .02f) * .5f;
                for (int k = 0; k < 11; k++)
                {
                    uint h = Hash(k + 101);
                    float length = (k < 7 ? .55f : .3f) + (h >> 8 & 255) / 255f * .4f, start = k < 7 ? 0 : .25f + (h >> 4 & 15) / 15f * .25f;
                    if (r > length || r < start) continue;
                    float angle = k * (2 * Mathf.PI / 7) + ((h & 255) / 255f - .5f) * .6f + (k < 7 ? 0 : .45f);
                    angle += (Mathf.PerlinNoise(r * 4.5f + k * 1.7f, k * 3.1f) - .5f) * 1.2f * r + Mathf.Sin(r * 23 + k) * .04f;
                    float diff = Mathf.Repeat(theta - angle + Mathf.PI, 2 * Mathf.PI) - Mathf.PI, along = (r - start) / Mathf.Max(.01f, length - start);
                    a += Gauss(Mathf.Abs(diff) * r, .02f * (1 - along) + .004f) * (1 - Smooth(.75f, 1, along));
                }
                return White(Mathf.Clamp01(a) * Window(r));
            });
            scorch = Build("Scorch", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), edge = .62f + (Fbm(u * 2.6f + 13, v * 2.6f + 5, 3) - .5f) * .5f;
                float a = (1 - Smooth(edge - .28f, edge, r)) * (.55f + .45f * Fbm(u * 7 + 1, v * 7 + 2, 2));
                return White(Mathf.Clamp01(a) * Window(r));
            });
            spike = Build("Crystal spike", 64, 192, (u, v) =>
            {
                float h = (v + 1) * .5f, half = .9f * Mathf.Pow(1 - h, .9f);
                float inside = (1 - Smooth(half - .1f, half, Mathf.Abs(u))) * Smooth(0, .05f, h);
                float ridge = .12f * (1 - h), face = u < ridge ? .97f : .72f;
                float rim = Gauss(Mathf.Abs(u) - half + .06f, .06f) * .25f, tip = Gauss(1 - h, .12f) * .2f;
                float l = Mathf.Clamp01(face + rim + tip - h * .08f);
                return new Color(l, l, Mathf.Clamp01(l * 1.03f), inside);
            }, new Vector2(.5f, 0));
            swirl = Build("Vortex", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float arms = Mathf.Pow(.5f + .5f * Mathf.Cos(Mathf.Atan2(v, u) * 3 - Mathf.Log(r + .05f) * 5.5f), 5);
                float a = arms * Smooth(.06f, .3f, r) * (1 - Smooth(.6f, .97f, r)) * (.6f + .4f * Fbm(u * 3 + 4, v * 3 + 9, 2)) + Gauss(r, .12f) * .3f;
                return White(Mathf.Clamp01(a) * Window(r));
            });
            petal = Build("Petal", 64, 64, (u, v) =>
            {
                float half = .48f * Mathf.Sqrt(Mathf.Max(0, 1 - u * u)) * (1 - .35f * u);
                float inside = 1 - Smooth(half - .1f, half, Mathf.Abs(v));
                float l = Mathf.Clamp01(.78f + .22f * (1 - Mathf.Abs(v) / Mathf.Max(.05f, half)) + Gauss(v, .035f) * .2f);
                return new Color(l, l, l, inside * Window(Mathf.Abs(u)));
            });
            flake = Build("Snowflake", 64, 64, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), sector = Mathf.Repeat(Mathf.Atan2(v, u) + Mathf.PI / 6, Mathf.PI / 3) - Mathf.PI / 6;
                float x = r * Mathf.Cos(sector), y = Mathf.Abs(r * Mathf.Sin(sector));
                float a = Gauss(y, .05f) * (1 - Smooth(.78f, .9f, x));
                for (int b = 1; b <= 2; b++) { float t = x - b * .3f; if (t > 0) a = Mathf.Max(a, Gauss(y - t * 1.1f, .045f) * (1 - Smooth(.12f, .22f, t))); }
                return White(Mathf.Clamp01(a + Gauss(r, .14f)) * Window(r));
            });
            orb = Build("Energy orb", 128, 128, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float rim = Gauss(r - .82f, .07f) + Gauss(r - .82f, .2f) * .25f, body = (1 - Smooth(.78f, .86f, r)) * (.12f + .3f * r * r);
                float shine = Gauss(Mathf.Sqrt(Sq(u + .32f) + Sq(v - .36f)), .12f) * .55f;
                return White(Mathf.Clamp01(rim + body + shine) * Window(r));
            });
            runeRing = Build("Rune ring", 512, 512, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float deg = Mathf.Repeat(Mathf.Atan2(v, u) * Mathf.Rad2Deg, 360);
                float a = Gauss(r - .955f, .009f) + Gauss(r - .735f, .009f) + Gauss(r - .7f, .005f) * .7f + Gauss(r - .4f, .007f) * .8f + Gauss(r - .16f, .006f) * .6f;
                if (r > .75f && r < .94f)
                {
                    float cell = deg / 12f; int slot = (int)cell; float lx = cell - slot, ly = (r - .75f) / .19f;
                    uint h = Hash(slot * 31 + 7); float g = 0, w = .075f;
                    if ((h & 1u) != 0) g = Mathf.Max(g, Gauss(lx - .5f, w) * Band(.15f, .85f, ly));
                    if ((h & 2u) != 0) g = Mathf.Max(g, Gauss(ly - .5f, w) * Band(.2f, .8f, lx));
                    if ((h & 4u) != 0) g = Mathf.Max(g, Gauss(ly - .82f, w) * Band(.2f, .8f, lx));
                    if ((h & 8u) != 0) g = Mathf.Max(g, Gauss(ly - .18f, w) * Band(.25f, .75f, lx));
                    if ((h & 16u) != 0) g = Mathf.Max(g, Gauss(lx - .2f - (ly - .15f) * .7f, w) * Band(.15f, .85f, ly));
                    if ((h & 32u) != 0) g = Mathf.Max(g, Gauss(Mathf.Sqrt(Sq(lx - .5f) + Sq(ly - .5f)) - .2f, w * .8f));
                    a += g * .95f * Band(.1f, .9f, lx);
                }
                float seg = 1e9f;
                for (int i = 0; i < 6; i++)
                {
                    float a0 = (90 + i * 120) * Mathf.Deg2Rad, a1 = (90 + (i + 1) * 120) * Mathf.Deg2Rad, turn = i < 3 ? 0 : Mathf.PI / 3;
                    seg = Mathf.Min(seg, SegmentDistance(u, v, Mathf.Cos(a0 + turn) * .68f, Mathf.Sin(a0 + turn) * .68f, Mathf.Cos(a1 + turn) * .68f, Mathf.Sin(a1 + turn) * .68f));
                }
                a += Gauss(seg, .006f) * .85f + (1 - Smooth(.05f, .72f, r)) * .06f;
                return White(Mathf.Clamp01(a) * Window(r));
            });
        }

        private static Sprite Build(string name, int width, int height, Shade shade) { return Build(name, width, height, shade, Vector2.one * .5f); }

        private static Sprite Build(string name, int width, int height, Shade shade, Vector2 pivot)
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
            return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
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
        /// <summary>1 between <paramref name="from"/> and <paramref name="to"/>, with short soft shoulders.</summary>
        private static float Band(float from, float to, float x) { return Smooth(from, from + .06f, x) * (1 - Smooth(to - .06f, to, x)); }
        private static uint Hash(int x) { uint h = (uint)x * 2654435761u; h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16; return h; }
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

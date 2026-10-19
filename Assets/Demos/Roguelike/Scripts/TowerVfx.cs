using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>Pixel effects. Original RPG sprites stay byte-identical; only missing magic uses generated pixel frames.</summary>
    public sealed class TowerVfx : MonoBehaviour
    {
        private const int PixelsPerUnit = 32, FrameSize = 48, FrameCount = 8;
        private sealed class Clip
        {
            public GameObject root;
            public Transform external;
            public SpriteRenderer renderer;
            public SpriteRenderer[] copies;
            public MeshRenderer[] meshCopies;
            public Mesh[] ownedMeshes;
            public MaterialPropertyBlock meshTint;
            public Sprite[] frames;
            public Vector2 position, velocity;
            public Vector3 scale;
            public Color color;
            public float age, life, spin, angle, growth;
            public bool loop, fade = true, pulse, projectile, registered;
        }

        private readonly List<Clip> clips = new List<Clip>();
        private readonly Dictionary<Transform, Clip> attached = new Dictionary<Transform, Clip>();
        private static readonly Dictionary<SkillEffect, Sprite[]> generated = new Dictionary<SkillEffect, Sprite[]>();
        private static Sprite[] slashFrames;
        private static Sprite swirl, arrow, pixel, hostileOrb;
        private int sequence;
        public int ActiveCount { get { return clips.Count; } }
        public const int KnownEffectsCount = 16;

        public static TowerVfx Create(Transform parent)
        {
            var root = new GameObject("Pixel combat effects");
            root.transform.SetParent(parent, false);
            return root.AddComponent<TowerVfx>();
        }

        public void Tick(float dt)
        {
            for (int i = clips.Count - 1; i >= 0; i--)
            {
                var clip = clips[i];
                if (!clip.root || (!ReferenceEquals(clip.external, null) && !clip.external)) { Remove(i); continue; }
                clip.age += dt;
                if (clip.age >= clip.life) { Remove(i); continue; }
                if (clip.frames != null && clip.renderer)
                {
                    float frame = clip.loop ? clip.age * 15 : clip.age / clip.life * clip.frames.Length;
                    clip.renderer.sprite = clip.frames[Mathf.Min(clip.frames.Length - 1, (int)frame % clip.frames.Length)];
                }
                if (ReferenceEquals(clip.external, null))
                {
                    clip.position += clip.velocity * dt;
                    clip.root.transform.position = Snap(clip.position);
                }
                clip.angle += clip.spin * dt;
                clip.root.transform.localRotation = Quaternion.Euler(0, 0, clip.angle);
                float progress = clip.age / clip.life;
                float scale = 1 + clip.growth * progress + (clip.pulse ? Mathf.Sin(clip.age * 18) * .06f : 0);
                clip.root.transform.localScale = clip.scale * scale;
                Color tint = clip.color;
                if (clip.fade) tint.a *= Mathf.Clamp01((1 - progress) * 2.5f);
                if (clip.renderer) clip.renderer.color = tint;
                if (clip.copies != null) foreach (var copy in clip.copies) if (copy) copy.color = tint;
                if (clip.meshCopies != null)
                {
                    clip.meshTint.SetColor("_Color", tint);
                    foreach (var copy in clip.meshCopies) if (copy) copy.SetPropertyBlock(clip.meshTint);
                }
            }
        }

        private void Remove(int index)
        {
            var clip = clips[index];
            if (clip.registered && !ReferenceEquals(clip.external, null))
            {
                Clip current;
                if (attached.TryGetValue(clip.external, out current) && ReferenceEquals(current, clip)) attached.Remove(clip.external);
            }
            if (clip.root) Destroy(clip.root);
            ReleaseMeshes(clip);
            clips.RemoveAt(index);
        }

        public void Clear()
        {
            // External projectile/zone roots belong to gameplay. Only our child visuals are removed.
            foreach (var clip in clips) { if (clip.root) Destroy(clip.root); ReleaseMeshes(clip); }
            clips.Clear(); attached.Clear();
        }

        private static void ReleaseMeshes(Clip clip)
        {
            if (clip.ownedMeshes != null) foreach (var mesh in clip.ownedMeshes) if (mesh) Destroy(mesh);
        }

        private void OnDestroy() { Clear(); }

        public void Projectile(Transform root, SkillEffect effect, Color tint)
        {
            if (!root) return;
            var old = root.GetComponent<SpriteRenderer>(); if (old) old.enabled = false;
            root.localScale = Vector3.one;
            Clip clip;
            if (!attached.TryGetValue(root, out clip) || !clip.root)
            {
                clip = NewClip("Pixel " + effect + " projectile", Vector2.zero, Frames(effect), .8f, Color.white, float.PositiveInfinity);
                clip.root.transform.SetParent(root, false);
                clip.root.transform.localPosition = Vector3.zero;
                clip.external = root; clip.projectile = true; clip.registered = true; clip.loop = true; clip.fade = false;
                attached[root] = clip;
            }
            clip.frames = Frames(effect); clip.age = 0;
            clip.renderer.sprite = clip.frames[0];
            bool physical = IsArrow(effect);
            float width = physical ? effect == SkillEffect.PierceShot ? 1.6f : 1.15f : effect == SkillEffect.Shockwave ? 1.9f : effect == SkillEffect.Fireball ? 1.3f : .82f;
            clip.scale = Vector3.one * (width / clip.frames[0].bounds.size.x);
            clip.root.transform.localScale = clip.scale;
            clip.color = physical ? Color.white : Tint(tint, .35f);
            clip.renderer.color = clip.color;
            clip.renderer.sortingOrder = 121;
        }

        public void HostileProjectile(Transform root,float diameter)
        {
            if(!root)return;var old=root.GetComponent<SpriteRenderer>();if(old)old.enabled=false;
            if(!hostileOrb)
            {
                const int size=14;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Hostile bullet white core",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {float d=Vector2.Distance(new Vector2(x,y),new Vector2(6.5f,6.5f));texture.SetPixel(x,y,d>6?Color.clear:d>4.8f?new Color(.35f,.04f,.15f):d>3?new Color(1,.22f,.43f):new Color(1,.96f,.8f));}
                texture.Apply();hostileOrb=Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,32);
            }
            Clip clip;
            if(!attached.TryGetValue(root,out clip)||!clip.root)
            {
                clip=NewClip("Hostile bullet",Vector2.zero,new[]{hostileOrb},diameter,Color.white,float.PositiveInfinity);
                clip.root.transform.SetParent(root,false);clip.root.transform.localPosition=Vector3.zero;
                clip.external=root;clip.projectile=true;clip.registered=true;clip.fade=false;attached[root]=clip;
            }
            clip.frames=new[]{hostileOrb};clip.renderer.sprite=hostileOrb;clip.color=Color.white;clip.age=0;
            clip.scale=Vector3.one*(diameter/hostileOrb.bounds.size.x);clip.root.transform.localScale=clip.scale;clip.renderer.sortingOrder=145;
        }

        public void AttachZone(GameObject root, SkillEffect effect, float radius, float duration)
        {
            if (!root) return;
            var old = root.GetComponent<SpriteRenderer>(); if (old) old.enabled = false;
            root.transform.localScale = Vector3.one;
            Clip existing;
            if (attached.TryGetValue(root.transform, out existing)) return;
            float width = effect == SkillEffect.Sentry ? 1.5f : Mathf.Max(1.5f, radius * 2);
            var clip = NewClip("Pixel " + effect + " field", Vector2.zero, Frames(effect), width, Color.white, Mathf.Max(.1f, duration));
            clip.root.transform.SetParent(root.transform, false); clip.root.transform.localPosition = Vector3.zero;
            clip.external = root.transform; clip.registered = true; clip.loop = true; clip.fade = effect != SkillEffect.Sentry;
            clip.color = effect == SkillEffect.PoisonCloud ? new Color(1, 1, 1, .83f) : Color.white;
            if (effect == SkillEffect.PoisonCloud) clip.scale = new Vector3(clip.scale.x, clip.scale.y * .65f, 1);
            if (effect == SkillEffect.BladeStorm) { clip.spin = 330; clip.color = new Color(1, .91f, .54f, .8f); }
            clip.root.transform.localScale = clip.scale;
            clip.renderer.sortingOrder = effect == SkillEffect.PoisonCloud ? 20 : 113;
            attached[root.transform] = clip;
            if (effect != SkillEffect.Sentry) RuneRing(root.transform.position, radius, EffectColor(effect), duration, root.transform);
        }

        public void BasicSlash(Vector2 pos, int facing, Color tint)
        {
            EnsureOriginals();
            var clip = NewClip("Original seven-frame sword trail", pos, slashFrames, 4.2f, tint, .24f);
            clip.scale = new Vector3(clip.scale.x * (facing < 0 ? -1 : 1), clip.scale.y, 1);
            clip.root.transform.localScale = clip.scale;
            clip.renderer.sortingOrder = 130;
            Shards(pos + new Vector2(facing * .5f, 0), tint, 3, .25f);
        }

        public void Hit(Vector2 pos, Color tint)
        {
            var spark = NewClip("Pixel hit cross", pos, new[] { Pixel() }, .42f, new Color(1, .96f, .72f), .10f);
            spark.scale = new Vector3(.07f, .65f, 1); spark.angle = 35;
            var second = NewClip("Pixel hit cross", pos, new[] { Pixel() }, .42f, tint, .13f);
            second.scale = new Vector3(.07f, .65f, 1); second.angle = -55;
            Shards(pos, tint, 5, .25f);
        }

        public void Cast(SkillEffect effect, Vector2 pos, float radius, Color tint)
        {
            float size = Mathf.Clamp(radius * 1.1f, 1.4f, 3.8f);
            var clip = NewClip("Pixel " + effect + " cast", pos, Frames(effect), size, IsArrow(effect) ? new Color(1, 1, 1, .45f) : Tint(tint, .18f), .48f);
            clip.growth = effect == SkillEffect.IceNova || effect == SkillEffect.HealPulse ? .5f : .1f;
            if (effect == SkillEffect.BladeStorm) clip.spin = 520;
            if (effect == SkillEffect.DashStrike) clip.scale = new Vector3(clip.scale.x * 1.8f, clip.scale.y * .55f, 1);
            if (effect == SkillEffect.HealPulse || effect == SkillEffect.ShieldPulse || effect == SkillEffect.IceNova)
                RuneRing(pos, Mathf.Min(radius, 3.5f), EffectColor(effect), .55f);
            if (effect == SkillEffect.MultiShot || effect == SkillEffect.RainArrows)
                for (int i = -1; i <= 1; i++)
                {
                    Vector2 indication = pos + new Vector2(i * .22f, effect == SkillEffect.RainArrows ? .35f : 0);
                    var shot = NewClip("Original arrow aim indication", indication, new[] { arrow }, 1.15f, new Color(1, 1, 1, .45f), .34f);
                    shot.angle = effect == SkillEffect.RainArrows ? -75 : i * 18;
                    // Charge previews remain still; the gameplay release creates the moving projectiles.
                    shot.velocity = Vector2.zero;
                }
            if (effect == SkillEffect.Sentry) clip.growth = -.08f;
            if (effect == SkillEffect.LifeDrain) Trail(pos + new Vector2(-1.3f, .6f), pos + new Vector2(1.3f, -.5f), effect, tint);
        }

        public void Impact(SkillEffect effect, Vector2 pos, float radius, Color tint)
        {
            if (effect == SkillEffect.Meteor)
            {
                RuneRing(pos, Mathf.Clamp(radius, 1, 4), new Color(1, .48f, .12f), .45f);
                for (int i = -2; i <= 2; i++)
                {
                    var flame = NewClip("Meteor impact flame", pos + new Vector2(i * .4f, .2f), Frames(SkillEffect.Fireball), 1.9f - Mathf.Abs(i) * .23f, Color.white, .48f);
                    flame.angle = 90 + i * 13; flame.velocity = new Vector2(i * 1.4f, 1.5f); flame.growth = -.3f;
                }
                Shards(pos, new Color(1, .58f, .15f), 13, .6f);
                return;
            }
            var clip = NewClip("Pixel " + effect + " impact", pos, Frames(effect), Mathf.Clamp(radius * 1.2f, .85f, 3), IsArrow(effect) ? tint : Color.white, .28f);
            clip.growth = .4f; if (effect == SkillEffect.BladeStorm) clip.spin = 650;
            Shards(pos, EffectColor(effect), effect == SkillEffect.Fireball ? 9 : 5, .32f);
        }

        public void Trail(Vector2 from, Vector2 to, SkillEffect effect, Color tint)
        {
            bool lightning = effect == SkillEffect.ChainLightning;
            Color color = lightning ? new Color(.72f, .81f, 1) : effect == SkillEffect.LifeDrain ? new Color(.35f, 1, .62f) : tint;
            int segments = lightning ? 9 : effect == SkillEffect.LifeDrain ? 12 : 4;
            Vector2 delta = to - from;
            Vector2 side = new Vector2(-delta.y, delta.x).normalized;
            Vector2 previous = from;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float jitter = i == segments ? 0 : lightning ? ((i + sequence) % 2 == 0 ? .24f : -.24f) : Mathf.Sin(t * Mathf.PI * 3) * .13f;
                Vector2 point = Vector2.Lerp(from, to, t) + side * jitter;
                Segment(previous, point, color, lightning ? .085f : .07f, lightning ? .19f : .3f);
                if (lightning)
                {
                    Segment(previous, point, new Color(1, 1, .94f), .03f, .14f);
                    if (i == 3 || i == 6) Segment(point, point + side * (i == 3 ? .65f : -.6f) + delta.normalized * .28f, color, .045f, .14f);
                }
                previous = point;
            }
            sequence++;
            if (lightning) Impact(SkillEffect.ChainLightning, to, .7f, color);
        }

        public void TelegraphMeteor(Vector2 pos, float radius, float delay)
        {
            delay = Mathf.Max(.1f, delay);
            RuneRing(pos, radius, new Color(1, .43f, .2f, .85f), delay);
            var meteor = NewClip("Descending pixel meteor", pos + new Vector2(-3, 7), Frames(SkillEffect.Meteor), 2.6f, Color.white, delay + .03f);
            meteor.velocity = new Vector2(3, -7) / delay; meteor.angle = -65; meteor.fade = false; meteor.loop = true;
        }

        public void Afterimage(Transform actor, float seconds = .25f)
        {
            if (!actor) return;
            var root = new GameObject("Original sprite afterimage"); root.transform.SetParent(transform, false);
            var copies = new List<SpriteRenderer>();
            var meshCopies = new List<MeshRenderer>();
            var ownedMeshes = new List<Mesh>();
            foreach (var source in actor.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!source.enabled || source.sprite == null || source.gameObject.name.Contains("Shadow")) continue;
                var part = new GameObject("Afterimage " + source.name); part.transform.SetParent(root.transform, false);
                part.transform.position = Snap(source.transform.position); part.transform.rotation = source.transform.rotation;
                part.transform.localScale = source.transform.lossyScale;
                var copy = part.AddComponent<SpriteRenderer>(); copy.sprite = source.sprite; copy.sharedMaterial = source.sharedMaterial;
                copy.sortingOrder = source.sortingOrder - 1; copy.flipX = source.flipX; copy.flipY = source.flipY;
                copy.color = new Color(.38f, .86f, 1, .38f); copies.Add(copy);
            }
            var block = new MaterialPropertyBlock(); block.SetColor("_Color", new Color(.38f, .86f, 1, .38f));
            foreach (var source in actor.GetComponentsInChildren<MeshRenderer>())
            {
                var sourceFilter = source.GetComponent<MeshFilter>();
                if (!source.enabled || sourceFilter == null || sourceFilter.sharedMesh == null) continue;
                var part = new GameObject("Afterimage " + source.name); part.transform.SetParent(root.transform, false);
                part.transform.position = Snap(source.transform.position); part.transform.rotation = source.transform.rotation;
                part.transform.localScale = source.transform.lossyScale;
                // Spine weighted vertices animate in the source mesh. Snapshot the mesh bytes
                // now so the afterimage keeps this pose while the original animation continues.
                var mesh = Instantiate(sourceFilter.sharedMesh); mesh.name = "Frozen original " + sourceFilter.sharedMesh.name;
                part.AddComponent<MeshFilter>().sharedMesh = mesh; ownedMeshes.Add(mesh);
                var copy = part.AddComponent<MeshRenderer>(); copy.sharedMaterials = source.sharedMaterials;
                copy.sortingLayerID = source.sortingLayerID; copy.sortingOrder = source.sortingOrder - 1;
                copy.SetPropertyBlock(block); meshCopies.Add(copy);
            }
            clips.Add(new Clip { root = root, copies = copies.ToArray(), meshCopies = meshCopies.ToArray(), ownedMeshes = ownedMeshes.ToArray(), meshTint = block,
                scale = Vector3.one, color = new Color(.38f, .86f, 1, .38f), life = Mathf.Max(.02f, seconds) });
        }

        private Clip NewClip(string name, Vector2 pos, Sprite[] frames, float width, Color color, float life)
        {
            var root = new GameObject(name); root.transform.SetParent(transform, false); root.transform.position = Snap(pos);
            var renderer = root.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.color = color; renderer.sortingOrder = 125;
            var clip = new Clip { root = root, renderer = renderer, frames = frames, position = pos, color = color,
                life = life, scale = Vector3.one * (width / Mathf.Max(.01f, frames[0].bounds.size.x)) };
            root.transform.localScale = clip.scale; clips.Add(clip); return clip;
        }

        private void Segment(Vector2 from, Vector2 to, Color color, float width, float life)
        {
            Vector2 delta = to - from;
            var clip = NewClip("Pixel energy segment", (from + to) * .5f, new[] { Pixel() }, 1, color, life);
            clip.scale = new Vector3(Mathf.Max(.03f, delta.magnitude), width, 1);
            clip.angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            clip.root.transform.localScale = clip.scale; clip.root.transform.localRotation = Quaternion.Euler(0, 0, clip.angle);
        }

        private void Shards(Vector2 pos, Color color, int count, float life)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (i * 137 + sequence * 23) * Mathf.Deg2Rad;
                var clip = NewClip("Pixel impact shard", pos, new[] { Pixel() }, .1f, i % 3 == 0 ? new Color(1, .98f, .8f) : color, life * (1 - i % 3 * .12f));
                clip.scale = new Vector3(.045f, .18f + i % 3 * .05f, 1);
                clip.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (2 + i % 4);
                clip.angle = angle * Mathf.Rad2Deg - 90; clip.spin = i % 2 == 0 ? 110 : -110;
                clip.root.transform.localScale = clip.scale;
            }
            sequence++;
        }

        private void RuneRing(Vector2 pos, float radius, Color color, float life, Transform parent = null)
        {
            int count = 16;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                Vector2 offset = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .34f);
                var clip = NewClip("Pixel rune ring", pos + offset, new[] { Pixel() }, 1, color, life);
                clip.scale = new Vector3(i % 4 == 0 ? .09f : .18f, i % 4 == 0 ? .28f : .055f, 1);
                clip.angle = angle * Mathf.Rad2Deg; clip.pulse = true;
                clip.root.transform.localScale = clip.scale; clip.root.transform.rotation = Quaternion.Euler(0, 0, clip.angle);
                if (parent)
                {
                    clip.root.transform.SetParent(parent, true);
                    clip.external = parent;
                    // Rune children are not the parent's main registration, so they expire independently.
                }
            }
        }

        private static Vector3 Snap(Vector2 p) { return new Vector3(Mathf.Round(p.x * PixelsPerUnit) / PixelsPerUnit, Mathf.Round(p.y * PixelsPerUnit) / PixelsPerUnit, 0); }
        private static Color Tint(Color color, float strength) { return Color.Lerp(Color.white, color, strength); }
        private static bool IsArrow(SkillEffect effect) { return effect == SkillEffect.MultiShot || effect == SkillEffect.PierceShot || effect == SkillEffect.RainArrows; }
        private static Color EffectColor(SkillEffect effect)
        {
            if (effect == SkillEffect.Fireball || effect == SkillEffect.Meteor) return new Color(1, .49f, .16f);
            if (effect == SkillEffect.IceNova) return new Color(.4f, .85f, 1);
            if (effect == SkillEffect.ChainLightning || effect == SkillEffect.ArcaneVolley) return new Color(.74f, .62f, 1);
            if (effect == SkillEffect.PoisonCloud) return new Color(.55f, .9f, .24f);
            if (effect == SkillEffect.HealPulse || effect == SkillEffect.LifeDrain) return new Color(.35f, 1, .65f);
            return new Color(1, .82f, .4f);
        }

        private static Sprite Pixel()
        {
            if (pixel) return pixel;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false); texture.name = "Code pixel geometry";
            texture.filterMode = FilterMode.Point; texture.SetPixel(0, 0, Color.white); texture.Apply();
            pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1); return pixel;
        }

        private static void EnsureOriginals()
        {
            if (slashFrames != null) return;
            slashFrames = new Sprite[7];
            for (int frame = 0; frame < 7; frame++)
            {
                var original = Resources.Load<Texture2D>("Tower/Effects/slash_" + frame);
                if (!original) throw new InvalidOperationException("Original RPG slash frame is missing: " + frame);
                var colors = original.GetPixels32();
                // The supplied frames have a cyan matte. Retain their original white/cyan
                // trail shape through a luminance mask, leaving the source PNG untouched.
                for (int i = 0; i < colors.Length; i++)
                {
                    byte alpha = (byte)(colors[i].a * colors[i].r / 255);
                    colors[i] = new Color32(255, 255, 255, alpha > 20 ? alpha : (byte)0);
                }
                var masked = new Texture2D(original.width, original.height, TextureFormat.RGBA32, false);
                masked.name = "Runtime mask of original slash " + frame; masked.filterMode = FilterMode.Point; masked.wrapMode = TextureWrapMode.Clamp;
                masked.SetPixels32(colors); masked.Apply();
                slashFrames[frame] = Sprite.Create(masked, new Rect(0, 0, masked.width, masked.height), Vector2.one * .5f, PixelsPerUnit);
            }
            swirl = Original("swirl"); arrow = Original("arrow");
        }

        private static Sprite Original(string name)
        {
            var texture = Resources.Load<Texture2D>("Tower/Effects/" + name);
            if (!texture) throw new InvalidOperationException("Original RPG effect is missing: " + name);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, PixelsPerUnit);
        }

        private static Sprite[] Frames(SkillEffect effect)
        {
            EnsureOriginals();
            if (IsArrow(effect)) return new[] { arrow };
            if (effect == SkillEffect.BladeStorm) return new[] { swirl };
            Sprite[] frames;
            if (generated.TryGetValue(effect, out frames)) return frames;
            frames = new Sprite[FrameCount];
            for (int f = 0; f < frames.Length; f++)
            {
                var texture = new Texture2D(FrameSize, FrameSize, TextureFormat.RGBA32, false);
                texture.name = "Code pixel " + effect + " frame " + f; texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
                var colors = new Color[FrameSize * FrameSize];
                for (int y = 0; y < FrameSize; y++) for (int x = 0; x < FrameSize; x++) colors[y * FrameSize + x] = PixelColor(effect, x, y, f);
                texture.SetPixels(colors); texture.Apply();
                frames[f] = Sprite.Create(texture, new Rect(0, 0, FrameSize, FrameSize), Vector2.one * .5f, PixelsPerUnit);
            }
            generated.Add(effect, frames); return frames;
        }

        private static Color PixelColor(SkillEffect effect, int x, int y, int frame)
        {
            int dx = x - 24, dy = y - 24;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            Color clear = Color.clear, white = new Color(1, .99f, .83f), gold = new Color(1, .69f, .18f), cyan = new Color(.3f, .83f, 1);
            int wave = frame % 4;
            switch (effect)
            {
                case SkillEffect.Fireball:
                    float head = Mathf.Sqrt((dx - 6) * (dx - 6) + dy * dy);
                    bool flame = dx < 7 && dx > -21 && Mathf.Abs(dy + Mathf.Sin((dx + frame * 3) * .7f) * 2) < (dx + 23) * .31f;
                    if (head < 4 + wave * .3f) return white;
                    if (head < 7 || flame && dx > -5) return gold;
                    if (head < 9 || flame) return new Color(.95f, .24f, .05f);
                    if (dx < -9 && (x + frame * 3) % 7 < 2 && Mathf.Abs(dy) < 3) return new Color(1, .63f, .13f);
                    return clear;
                case SkillEffect.IceNova:
                    float angle = Mathf.Atan2(dy, dx), spoke = Mathf.Abs(Mathf.Sin(angle * 6));
                    float length = 11 + wave * 2;
                    if (r > 5 && r < length && spoke < .13f) return r > length - 4 ? white : cyan;
                    if (Mathf.Abs(r - (9 + wave * 2)) < 1.2f) return new Color(.25f, .66f, .92f, .9f);
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) < 6) return new Color(.75f, .98f, 1);
                    return clear;
                case SkillEffect.ChainLightning:
                    int zig = (x / 6 + frame) % 2 == 0 ? 4 : -4;
                    if (x > 2 && x < 46 && Mathf.Abs(dy - zig) <= 1) return white;
                    if (x > 2 && x < 46 && Mathf.Abs(dy - zig) <= 3) return new Color(.5f, .52f, 1);
                    if (Mathf.Abs(dx - dy) < 2 && dx > -5 && dx < 11) return cyan;
                    return clear;
                case SkillEffect.Meteor:
                    float stone = Mathf.Sqrt((dx - 5) * (dx - 5) + (dy + 3) * (dy + 3));
                    if (stone < 8) return (x + y) % 7 < 3 ? new Color(.29f, .19f, .17f) : new Color(.52f, .29f, .18f);
                    if (stone < 10) return gold;
                    if (dx < 6 && dx > -21 && dy > -8 && Mathf.Abs(dy + dx * .35f) < (dx + 23) * .4f) return (x + frame) % 5 < 2 ? gold : new Color(.96f, .26f, .06f);
                    return clear;
                case SkillEffect.ArcaneVolley:
                    if (Mathf.Abs(dx - 5) + Mathf.Abs(dy) < 5) return white;
                    if (Mathf.Abs(dx - 5) + Mathf.Abs(dy) < 10) return new Color(.65f, .4f, .98f);
                    if (dx < 3 && dx > -20 && Mathf.Abs(dy) < 2 + wave % 2) return new Color(.43f, .39f, .82f);
                    if ((Mathf.Abs(dx + 10) < 2 && Mathf.Abs(dy) < 9) || (Mathf.Abs(dy) < 2 && dx > -18 && dx < -3)) return new Color(.74f, .72f, 1);
                    return clear;
                case SkillEffect.Shockwave:
                    float curve = Mathf.Sqrt((dx + 17) * (dx + 17) + dy * dy * .65f);
                    if (Mathf.Abs(curve - (27 + wave)) < 2 && dx > -3) return white;
                    if (Mathf.Abs(curve - (24 + wave)) < 3 && dx > -8) return gold;
                    if (dy < -13 && dx > -16 && dx < 15 && (x + frame * 2) % 7 < 4) return new Color(.81f, .53f, .24f);
                    return clear;
                case SkillEffect.DashStrike:
                    if (Mathf.Abs(dy) < 2 && dx > -18 && dx < 19) return white;
                    if (Mathf.Abs(dy) < 4 && dx > -15 && dx < 15) return cyan;
                    if ((Mathf.Abs(dy - 8) < 1 || Mathf.Abs(dy + 8) < 1) && dx < 5 && dx > -21 + wave) return new Color(.3f, .68f, .95f);
                    return clear;
                case SkillEffect.PoisonCloud:
                    bool cloud = (dx + 10) * (dx + 10) + (dy + 2) * (dy + 2) < 110 || dx * dx + (dy - 3) * (dy - 3) < 160 || (dx - 11) * (dx - 11) + dy * dy < 100;
                    if (!cloud) return clear;
                    if ((x + frame * 2) % 13 < 3 && (y + frame) % 11 < 3) return new Color(.8f, 1, .35f);
                    return (x / 4 + y / 4 + frame / 2) % 3 == 0 ? new Color(.34f, .59f, .16f, .85f) : new Color(.48f, .77f, .2f, .68f);
                case SkillEffect.HealPulse:
                    if (Mathf.Abs(dx) < 3 && Mathf.Abs(dy) < 12 || Mathf.Abs(dy) < 3 && Mathf.Abs(dx) < 12) return white;
                    if (Mathf.Abs(dx) < 5 && Mathf.Abs(dy) < 14 || Mathf.Abs(dy) < 5 && Mathf.Abs(dx) < 14) return new Color(.28f, .93f, .53f);
                    if (Mathf.Abs(r - (15 + wave)) < 1 && (x + y) % 4 < 2) return new Color(.53f, 1, .77f);
                    return clear;
                case SkillEffect.ShieldPulse:
                    float hex = Mathf.Max(Mathf.Abs(dx) * .86f + Mathf.Abs(dy) * .5f, Mathf.Abs(dy));
                    if (Mathf.Abs(hex - 17) < 1.4f) return white;
                    if (Mathf.Abs(hex - 14) < 1) return gold;
                    if (hex < 16 && hex > 11) return new Color(.23f, .68f, .73f, .55f);
                    if (Mathf.Abs(dx) < 2 && dy > -8 && dy < 10 || Mathf.Abs(dy - 3) < 2 && Mathf.Abs(dx) < 8) return gold;
                    return clear;
                case SkillEffect.LifeDrain:
                    if (r < 5) return new Color(.85f, 1, .64f);
                    if (r < 9) return new Color(.25f, .81f, .52f);
                    if (r > 11 && r < 20 && Mathf.Sin(Mathf.Atan2(dy, dx) * 3 + frame * .6f + r * .2f) > .65f) return new Color(.29f, .94f, .64f);
                    return clear;
                case SkillEffect.Sentry:
                    int recoil = frame == 2 || frame == 3 ? -2 : 0;
                    if (dy > -16 && dy < -11 && Mathf.Abs(dx) < 15 || dy > -11 && dy < -6 && Mathf.Abs(dx) < 10) return new Color(.34f, .36f, .4f);
                    if (dy >= -6 && dy < 8 && Mathf.Abs(dx) < 10) return (x / 3 + y / 3) % 2 == 0 ? new Color(.69f, .48f, .25f) : new Color(.45f, .3f, .2f);
                    if (dy > 2 && dy < 8 && dx > recoil && dx < 19 + recoil) return new Color(.41f, .48f, .56f);
                    if (dy > -1 && dy < 4 && dx > -6 && dx < 1) return cyan;
                    if (frame == 2 && dx > 16 && dx < 23 && Mathf.Abs(dy - 5) < 3) return white;
                    return clear;
                default: return clear;
            }
        }
    }
}

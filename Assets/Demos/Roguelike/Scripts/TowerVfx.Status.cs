using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Statuses worn by enemies (burning, frozen, stunned, marked, cursed, seeded) and the bursts of the reactions between them.
    // A status rides on a small holder under the enemy root (the root's scale undone, so sizes stay in world units) and lasts
    // exactly as long as gameplay says: applying it again refreshes the one instance kept per enemy and kind. The look is
    // measured from the enemy's own renderers, so a boss twice as tall wears its reticle, stars or sigil twice as high.
    // Shapes pair a dark alpha ink outline or a saturated alpha body (they read on the pale Olympus sky) with additive light
    // (it reads in the dark of Hell); big dark layers sit behind the actors. One driver per status (a custom emitter on its
    // first part, backed by a tiny runner for frames the tick skips emitters) animates the parts and spawns its few free
    // particles, so each status stays under about twenty clips. Head looks share one column: a stun sits lowest over the
    // head, a curse above it and a mark on top, so the three never pile into one blot.
    public sealed partial class TowerVfx
    {
        public enum StatusKind { Burn, Freeze, Stun, Mark, Curse, Seed }
        public enum ReactionKind { Superconduct, Steam, Defenseless, Toxic, Spread }

        private sealed class StatusPart
        {
            public Clip clip; public Vector3 scale; public Vector2 offset; public Color color;
            public float intro, from = 1, angle, phase, swing = 7, length; public bool growY, head; public int role, order;
        }

        private sealed class StatusLook
        {
            public Transform actor, holder; public StatusKind kind; public Clip primary; public readonly List<StatusPart> parts = new List<StatusPart>();
            public Vector2 body, anchor; public Vector3 rootScale = Vector3.one; public Color tint;
            public float size, reach, until, start, total, clock, bank, bank2, beatPhase, progress, orbitX, orbitY;
            // Head slots: stun sits lowest over the head, a curse above it and a mark on top; lift is how far this look is
            // raised over the ones below it, slot how much head room this look takes for the ones above.
            public float lift, slot;
            public int grade, beat = -1; public bool clearing, ended;
        }

        /// <summary>Keeps a status posed even when the effect tick skips emitters (over the hard clip cap): it runs after the
        /// tick and only catches up when the tick's own driver has not run this frame.</summary>
        private sealed class StatusRunner : MonoBehaviour
        {
            public TowerVfx vfx; public StatusLook look;
            private void LateUpdate()
            {
                if (vfx == null || look == null || look.primary == null || !look.primary.root) { enabled = false; return; }
                vfx.StatusDrive(look);
            }
        }

        private const int StatusRoleBeat = 1, StatusRoleOrbit = 2, StatusRoleSway = 3, StatusRoleBloom = 4, StatusRolePetal = 5, StatusRoleLeaf = 6, StatusRoleVine = 7, StatusRoleLight = 8;
        private const float StatusMarkBeat = .9f;

        private static readonly Color StatusFire = new Color(1, .55f, .16f), StatusIce = new Color(.62f, .9f, 1), StatusIceDeep = new Color(.05f, .17f, .42f, .6f),
            StatusGold = new Color(1, .8f, .26f), StatusGoldDeep = new Color(.34f, .17f, .01f, .88f), StatusCrimson = new Color(1, .16f, .24f),
            StatusCrimsonDeep = new Color(.24f, 0, .04f, .85f), StatusViolet = new Color(.62f, .3f, 1), StatusVioletDeep = new Color(.11f, 0, .19f, .86f),
            StatusPink = new Color(1, .55f, .8f);

        private static Sprite[] StatusReticle, StatusBracket, StatusChevron, StatusStar, StatusSigil, StatusEye, StatusGuard, StatusCrystal;
        private static Sprite StatusTendril, StatusWisp, StatusPetal, StatusShell, StatusShellLight, StatusFacet;
        private static Sprite[] StatusVapor;
        private static readonly float[] StatusCrystals = { -14, 12, 19, 96, 28, 15, -14, 18, 88, 26, 0, 0, 30, 136, 40 };

        private readonly Dictionary<long, StatusLook> StatusLooks = new Dictionary<long, StatusLook>();
        private readonly Dictionary<int, Vector2> StatusBodies = new Dictionary<int, Vector2>();
        private readonly List<Renderer> StatusRenderers = new List<Renderer>();
        private readonly List<long> StatusDead = new List<long>();
        private int StatusCalls;

        // ---------------------------------------------------------------- entry points

        /// <summary>
        /// Shows <paramref name="kind"/> on <paramref name="actor"/> for <paramref name="duration"/> seconds, following it.
        /// Calling again for the same actor and kind refreshes the time instead of stacking a second copy.
        /// </summary>
        public void Status(Transform actor, StatusKind kind, float duration, Color tint)
        {
            if (!actor) return;
            EnsureTextures(); StatusTextures(kind);
            duration = Mathf.Max(.05f, duration);
            if (++StatusCalls % 64 == 0 || StatusLooks.Count > 96) StatusPurge();
            long key = StatusKey(actor, kind);
            StatusLook look;
            if (StatusLooks.TryGetValue(key, out look) && StatusAlive(look) && look.grade >= Grade)
            {
                float now = look.primary.age, left = Mathf.Max(0, look.until - now);
                // A seed keeps growing from where it is and blooms exactly when the gameplay's remaining time runs out.
                if (kind == StatusKind.Seed) look.total = Mathf.Max(.05f, now - look.start + duration);
                // Holds keep the longer of the two; the other timers already arrive as the gameplay's remaining time.
                StatusTime(look, kind == StatusKind.Stun || kind == StatusKind.Freeze ? Mathf.Max(left, duration) : duration, StatusTail(kind));
                return;
            }
            Transform holder = null; float carry = 0;
            if (look != null)
            {
                if (StatusAlive(look)) { if (kind == StatusKind.Seed) carry = look.progress; StatusFade(look, .12f); }
                holder = look.holder;
            }
            StatusLooks[key] = StatusBuild(actor, holder, kind, duration, tint, carry);
        }

        /// <summary>Ends a status early (a burn put out by steam, a seed that bloomed, a mark passed on).</summary>
        public void ClearStatus(Transform actor, StatusKind kind)
        {
            if (StatusLooks.Count == 0 || !actor) return;
            if (kind == StatusKind.Burn && StatusLooks.Count > 48) StatusPurge();
            StatusLook look;
            if (!StatusLooks.TryGetValue(StatusKey(actor, kind), out look) || !StatusAlive(look)) return;
            if (kind == StatusKind.Freeze) StatusShatter(look);
            StatusFade(look, kind == StatusKind.Freeze ? .08f : kind == StatusKind.Seed ? .1f : .2f);
        }

        /// <summary>A reaction's burst where it struck; gameplay throttles these per enemy.</summary>
        public void Reaction(Vector2 pos, ReactionKind kind)
        {
            EnsureTextures();
            float g = 1 + (Grade - 1) * .15f;
            switch (kind)
            {
                case ReactionKind.Superconduct: StatusCrystalTexture(); StatusSuperconduct(pos, g); break;
                case ReactionKind.Steam: StatusVaporTextures(); StatusSteam(pos, g); break;
                case ReactionKind.Defenseless: StatusGuardTextures(); StatusDefenseless(pos, g); break;
                case ReactionKind.Toxic: StatusToxic(pos, g); break;
                case ReactionKind.Spread: StatusTextures(StatusKind.Mark); StatusSpread(pos, g); break;
            }
        }

        // ---------------------------------------------------------------- bookkeeping

        private static long StatusKey(Transform actor, StatusKind kind) { return ((long)actor.GetInstanceID() << 3) | (long)(int)kind; }

        private static float StatusTail(StatusKind kind) { return kind == StatusKind.Freeze ? .12f : kind == StatusKind.Seed ? .1f : kind == StatusKind.Stun ? .2f : .3f; }

        private bool StatusAlive(StatusLook look)
        {
            Clip current;
            return look.holder && !look.clearing && !look.ended && look.primary != null && attached.TryGetValue(look.holder, out current) && ReferenceEquals(current, look.primary);
        }

        /// <summary>Every part holds fully until <paramref name="remaining"/> seconds from now, then fades over <paramref name="tail"/>.</summary>
        private static void StatusTime(StatusLook look, float remaining, float tail)
        {
            look.until = look.primary.age + remaining;
            foreach (var part in look.parts)
            {
                var clip = part.clip; float end = Mathf.Max(0, clip.age) + remaining;
                clip.life = end + tail; clip.hold = end / clip.life; clip.fadePower = 1.4f;
            }
        }

        private static void StatusFade(StatusLook look, float seconds) { look.clearing = true; StatusTime(look, 0, seconds); }

        /// <summary>Drops looks whose enemy is gone, and looks that have fully faded (their empty holder is destroyed too),
        /// so no dead transform, part list or clip is held after a floor changes.</summary>
        private void StatusPurge()
        {
            StatusDead.Clear();
            foreach (var pair in StatusLooks)
            {
                var look = pair.Value;
                if (!look.actor || !look.holder) { StatusDead.Add(pair.Key); continue; }
                if (look.primary != null && look.primary.root) continue;
                Clip current;
                if (!attached.TryGetValue(look.holder, out current)) Destroy(look.holder.gameObject);
                look.parts.Clear(); StatusDead.Add(pair.Key);
            }
            foreach (var key in StatusDead) StatusLooks.Remove(key);
            StatusDead.Clear();
        }

        /// <summary>The head room a live <paramref name="kind"/> look on <paramref name="actor"/> takes (0 when there is none).</summary>
        private float StatusSlot(Transform actor, StatusKind kind)
        {
            StatusLook other;
            return actor && StatusLooks.TryGetValue(StatusKey(actor, kind), out other) && StatusAlive(other) ? other.slot : 0;
        }

        /// <summary>How far a head look is raised: a curse over a stun, a mark over both.</summary>
        private float StatusLiftTarget(StatusLook look)
        {
            if (look.kind != StatusKind.Curse && look.kind != StatusKind.Mark) return 0;
            float lift = StatusSlot(look.actor, StatusKind.Stun);
            if (look.kind == StatusKind.Mark) lift += StatusSlot(look.actor, StatusKind.Curse);
            return lift;
        }

        /// <summary>The width and height of an enemy's body above its feet, measured once from its own renderers.</summary>
        private Vector2 StatusBody(Transform actor)
        {
            int id = actor.GetInstanceID(); Vector2 body;
            if (StatusBodies.TryGetValue(id, out body)) return body;
            Vector3 origin = actor.position; float top = 0, left = 0, right = 0; bool any = false;
            actor.GetComponentsInChildren(false, StatusRenderers);
            foreach (var r in StatusRenderers)
            {
                if (!r.enabled) continue;
                var material = r.sharedMaterial;
                if (material != null && (material == additiveMaterial || material == alphaMaterial)) continue;
                if (r.name.IndexOf("Shadow", StringComparison.Ordinal) >= 0) continue;
                Bounds b = r.bounds; if (b.size.x * b.size.y < .0004f) continue;
                if (!any) { top = b.max.y - origin.y; left = b.min.x - origin.x; right = b.max.x - origin.x; any = true; }
                else { top = Mathf.Max(top, b.max.y - origin.y); left = Mathf.Min(left, b.min.x - origin.x); right = Mathf.Max(right, b.max.x - origin.x); }
            }
            StatusRenderers.Clear();
            // Small slimes stand under half a unit; heads, shells and buds sit on the measured top, not a guessed one.
            body = any ? new Vector2(Mathf.Clamp(right - left, .5f, 3.6f), Mathf.Clamp(top, .42f, 4.6f)) : new Vector2(1.1f, 1.3f) * Mathf.Max(.6f, Mathf.Abs(actor.lossyScale.y));
            if (StatusBodies.Count > 256) StatusBodies.Clear();
            StatusBodies[id] = body; return body;
        }

        /// <summary>Undoes the actor root's scale on the holder (bosses are scaled up), so offsets and widths stay in world units.</summary>
        private static void StatusNeutral(StatusLook look)
        {
            if (!look.actor || !look.holder) return;
            Vector3 l = look.actor.lossyScale; look.rootScale = l;
            look.holder.localScale = new Vector3(1 / StatusSafe(l.x), 1 / StatusSafe(l.y), 1);
            look.holder.localRotation = Quaternion.identity;
        }

        private static float StatusSafe(float x) { return Mathf.Abs(x) < .0001f ? (x < 0 ? -.0001f : .0001f) : x; }
        private static Color StatusTone(Color tint) { return new Color(tint.r, tint.g, tint.b, 1); }
        private static int StatusEvery(ref float bank, float rate, float dt) { bank += rate * dt; int n = (int)bank; bank -= n; return Mathf.Min(n, 3); }

        private static Clip StatusLater(Clip clip, float delay)
        {
            if (delay > 0) { clip.age = -delay; if (clip.renderer) clip.renderer.enabled = false; }
            return clip;
        }

        private static Clip StatusInk(Clip clip, int order) { clip.renderer.sharedMaterial = AlphaBlend; clip.renderer.sortingOrder = order; return clip; }

        // ---------------------------------------------------------------- building a status

        private StatusLook StatusBuild(Transform actor, Transform holder, StatusKind kind, float duration, Color tint, float carry)
        {
            if (!holder) { holder = new GameObject("Status " + kind).transform; holder.SetParent(actor, false); }
            var look = new StatusLook { actor = actor, holder = holder, kind = kind, tint = tint, grade = Grade, body = StatusBody(actor), total = duration };
            StatusNeutral(look);
            look.size = Mathf.Clamp(Mathf.Pow(look.body.y / 1.3f, .75f), .85f, 2.3f);
            float grow = 1, open = 0;
            if (kind == StatusKind.Seed && carry > 0)
            {
                // A seed redrawn at a higher level keeps how far it has grown and still blooms when its time runs out.
                float p = look.progress = Mathf.Min(carry, .95f);
                look.total = duration / (1 - p); look.start = -p * look.total;
            }
            switch (kind)
            {
                case StatusKind.Burn: StatusBurnLook(look); break;
                case StatusKind.Freeze: StatusFreezeLook(look); break;
                case StatusKind.Stun: StatusStunLook(look); break;
                case StatusKind.Mark: StatusMarkLook(look); break;
                case StatusKind.Curse: StatusCurseLook(look); break;
                default: StatusSeedLook(look); break;
            }
            if (kind == StatusKind.Seed)
            {
                grow = Mathf.Lerp(.55f, 1.12f, Mathf.Pow(look.progress, .7f)); open = Smooth(.4f, 1, look.progress);
                if (carry > 0) foreach (var part in look.parts) part.intro = 0;
            }
            look.lift = StatusLiftTarget(look);
            foreach (var part in look.parts) StatusPose(look, part, 0, 1, grow, open);
            look.primary = look.parts[0].clip;
            var layers = new Clip[look.parts.Count - 1];
            for (int i = 1; i < look.parts.Count; i++) layers[i - 1] = look.parts[i].clip;
            Bundle(holder, look.primary, layers);
            var owner = look;
            // The driver runs inside the effect tick (an emitter fast enough to fire every frame; it works once per frame);
            // the runner only catches up on frames the tick skipped it.
            Emitter(look.primary, 5000, 0, (clip, p) => StatusDrive(owner));
            var runner = holder.GetComponent<StatusRunner>();
            if (!runner) runner = holder.gameObject.AddComponent<StatusRunner>();
            runner.vfx = this; runner.look = look; runner.enabled = true;
            StatusTime(look, duration, StatusTail(kind));
            return look;
        }

        private StatusPart StatusAttach(StatusLook look, string name, Sprite sprite, float width, Color color, int order, bool additive, Vector2 offset)
        {
            var clip = Attach(look.holder, name, sprite, width, color, order, additive, offset);
            clip.fadeIn = .1f;
            var part = new StatusPart { clip = clip, scale = clip.scale, offset = offset, order = order, color = color };
            look.parts.Add(part); return part;
        }

        /// <summary>A part that rides the look's head slot (raised over a stun or curse below it).</summary>
        private StatusPart StatusHead(StatusLook look, string name, Sprite sprite, float width, Color color, int order, bool additive, Vector2 offset)
        {
            var part = StatusAttach(look, name, sprite, width, color, order, additive, offset); part.head = true; return part;
        }

        private static StatusPart StatusSize(StatusPart part, float width, float height)
        {
            Vector3 size = part.clip.renderer.sprite.bounds.size; part.scale = new Vector3(width / size.x, height / size.y, 1); return part;
        }

        private static StatusPart StatusIntro(StatusPart part, float seconds, float from, bool growY = false) { part.intro = seconds; part.from = from; part.growY = growY; return part; }

        private StatusPart StatusFlame(StatusLook look, string name, float width, Color color, int order, bool additive, Vector2 offset, float fps)
        {
            var part = StatusAttach(look, name, flame[0], width, color, order, additive, offset);
            part.clip.frames = flame; part.clip.fps = fps; part.clip.loop = true; return part;
        }

        // Burning: a warm glow behind the body, painted flames (alpha, the fire ramp's own colours) that keep their shape on a
        // pale sky, light flames licking up the body, and free tongues, embers and soot rising off it.
        private static float StatusFlameWidth(StatusLook look) { return Mathf.Clamp(Mathf.Max(look.body.x * .44f, look.body.y * .62f), .5f, 1.8f); }

        private void StatusBurnLook(StatusLook look)
        {
            float H = look.body.y, W = look.body.x, fw = StatusFlameWidth(look); int g = look.grade;
            Color fire = Color.Lerp(StatusFire, StatusTone(look.tint), .2f);
            var heat = StatusAttach(look, "Status burn heat", glow, W * 1.5f + .9f, Alpha(fire, .5f), 28, true, new Vector2(0, H * .45f)); heat.clip.flicker = .15f; heat.clip.pulse = .05f;
            // Only a faint warm wash on the body itself, so a burning enemy keeps its own colours and silhouette.
            var warmth = StatusAttach(look, "Status burn glow", glow, W * 1.1f + .4f, Alpha(fire, .07f), 120, true, new Vector2(0, H * .4f)); warmth.clip.flicker = .25f;
            // Painted flames stand behind the actor and climb past its outline, so the body stays readable while it burns.
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * W * .34f;
                var paint = StatusFlame(look, "Status burn painted flame", fw * (i == 1 ? 1.25f : 1.05f), new Color(1, .7f, .5f, .9f), 29, false, new Vector2(x, H * (i == 1 ? .35f : .12f) + fw * .62f), 14 + i * 3.5f);
                paint.clip.angle = -(i - 1) * 12; if (i == 2) paint.scale.x *= -1;
            }
            // Two light tongues lean out from the silhouette's edges (never over its middle); a higher level adds a crown
            // flame over the head, and an awakened burn a small white-hot heart above it.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1 : 1;
                var tongue = StatusFlame(look, "Status burn flame", fw * .6f, new Color(1, .86f, .58f, .55f), 125 + i, true, new Vector2(side * W * .42f, H * (i == 0 ? .58f : .72f) + fw * .36f), 19 + i * 3.5f);
                tongue.clip.angle = -side * 14; if (i == 1) tongue.scale.x *= -1;
            }
            if (g >= 2) StatusFlame(look, "Status burn crown flame", fw * .7f, new Color(1, .8f, .5f, .5f), 125, true, new Vector2(W * .04f, H * .9f + fw * .36f), 23);
            if (g >= 3) { var heart = StatusFlame(look, "Status burn white heat", fw * .35f, new Color(1, .97f, .86f, .85f), 127, true, new Vector2(0, H + fw * .2f), 27); heart.clip.pulse = .05f; }
        }

        // Frozen solid: a crystal shell (deep blue ink behind the actor, a translucent ice body and its facet light in front)
        // grows up from the feet, spikes jut out at its base and rime spreads on the floor; it shatters when the hold ends.
        private void StatusFreezeLook(StatusLook look)
        {
            float H = look.body.y, W = look.body.x, s = look.size;
            float SW = Mathf.Max(W * 1.12f, H * .72f) + .3f, SH = H * 1.1f + .22f;
            Color ice = Color.Lerp(StatusIce, StatusTone(look.tint), .2f);
            var frost = StatusAttach(look, "Status freeze glow", glow, SW * 1.45f, Alpha(ice, .4f), 27, true, new Vector2(0, H * .5f)); frost.clip.pulse = .03f;
            var rime = StatusAttach(look, "Status freeze rime", glow, SW * 1.8f, new Color(.7f, .9f, 1, .55f), 27, true, new Vector2(0, .04f)); rime.scale.y *= .26f;
            if (look.grade >= 3)
            {
                var rune = StatusAttach(look, "Status freeze rune", runeRing, SW * 1.3f, Alpha(Color.Lerp(ice, Color.white, .4f), .55f), 27, true, new Vector2(0, .04f));
                rune.scale.y *= .3f; StatusIntro(rune, .22f, .4f);
            }
            // The ice's solid faces stand behind the enemy; in front only a thin frosted film and the edge and gloss light,
            // drawn over the nova's own spikes, so the frozen enemy shows through its shell.
            StatusIntro(StatusSize(StatusAttach(look, "Status freeze shell ink", StatusShell, 1, StatusIceDeep, 28, false, new Vector2(0, -.05f)), SW * 1.1f, SH * 1.05f), .14f, .08f, true);
            StatusIntro(StatusSize(StatusAttach(look, "Status freeze shell", StatusShell, 1, new Color(1, 1, 1, .8f), 29, false, Vector2.zero), SW, SH), .12f, .08f, true);
            StatusIntro(StatusSize(StatusAttach(look, "Status freeze shell film", StatusShell, 1, new Color(.86f, .96f, 1, .38f), 129, false, Vector2.zero), SW, SH), .12f, .08f, true);
            var light = StatusIntro(StatusSize(StatusAttach(look, "Status freeze shell light", StatusShellLight, 1, Alpha(Color.Lerp(ice, Color.white, .55f), .9f), 130, true, Vector2.zero), SW, SH), .12f, .08f, true);
            light.clip.flicker = .1f;
            for (int i = 0; i < 3; i++)
            {
                float t = i - 1;
                var tooth = StatusAttach(look, "Status freeze spike", spike, .17f * s * (1.15f - .3f * Mathf.Abs(t)), new Color(.9f, .98f, 1, .95f), 129, false, new Vector2(t * SW * .5f, 0));
                tooth.clip.angle = -t * 38; StatusIntro(tooth, .1f + .035f * i, 0);
            }
            // The snap of freezing: a white flash, a frost ring and a puff of rime.
            Vector2 mid = (Vector2)look.holder.position + new Vector2(0, H * .5f);
            var snap = Flash(mid, 1.6f * s, new Color(.9f, .98f, 1, .8f), .16f); snap.startScale = .6f;
            var ringInk = Spawn("Status freeze ring ink", ring, mid, SW * 1.7f, Alpha(StatusIceDeep, .55f), .32f, false, 119); ringInk.startScale = .15f; ringInk.endScale = 1; ringInk.ease = 3; ringInk.fadePower = 1.3f;
            for (int i = 0; i < 2; i++)
            {
                var puff = Puff(mid + new Vector2((i * 2 - 1) * Range(.2f, .5f) * W, -.35f * H), Range(.5f, .75f) * s, new Color(.88f, .95f, 1, .55f), new Vector2((i * 2 - 1) * Range(.6f, 1.5f), Range(.2f, .8f)), Range(.5f, .75f), 124);
                puff.endScale = 2.1f;
            }
        }

        // Dazed: a gold ring floats over the head and stars circle it, passing behind it on the far side.
        private void StatusStunLook(StatusLook look)
        {
            float H = look.body.y, W = look.body.x, s = look.size;
            Color gold = Color.Lerp(StatusGold, StatusTone(look.tint), .15f);
            Vector2 c = look.anchor = new Vector2(0, H + .3f * s);
            look.orbitX = Mathf.Max(.5f * s, W * .3f); look.orbitY = look.orbitX * .3f;
            look.slot = .61f * s + look.orbitY - .08f;
            var halo = StatusAttach(look, "Status stun halo", glow, look.orbitX * 2.8f, Alpha(gold, .3f), 119, true, c); halo.role = StatusRoleBeat;
            var inkRing = StatusAttach(look, "Status stun ring ink", ring, look.orbitX * 2.8f, Alpha(StatusGoldDeep, .8f), 120, false, c); inkRing.scale.y *= .3f; StatusIntro(inkRing, .2f, 1.7f);
            var paintRing = StatusAttach(look, "Status stun ring paint", ring, look.orbitX * 2.66f, new Color(1, .7f, .12f, .85f), 120, false, c); paintRing.scale.y *= .3f; StatusIntro(paintRing, .2f, 1.7f);
            var litRing = StatusAttach(look, "Status stun ring", ring, look.orbitX * 2.72f, Alpha(Color.Lerp(gold, Warm, .3f), .85f), 121, true, c); litRing.scale.y *= .3f; StatusIntro(litRing, .2f, 1.7f);
            int stars = look.grade >= 3 ? 4 : 3; float size = .62f * s;
            for (int i = 0; i < stars; i++)
                for (int layer = 0; layer < 3; layer++)
                {
                    Color color = layer == 0 ? StatusGoldDeep : layer == 1 ? new Color(1, .76f, .16f, 1) : new Color(1, .97f, .82f, .95f);
                    var star = StatusAttach(look, "Status stun star", StatusStar[layer], size, color, 125 + layer, layer == 2, c);
                    star.role = StatusRoleOrbit; star.phase = i * 360f / stars; star.clip.spin = 170; star.clip.angle = i * 23;
                    StatusIntro(star, .18f, 0);
                }
        }

        // Marked: a crimson hunter's crosshair locks on over the head (it closes in from wide) and beats once a second,
        // throwing a lock-on ring; it stays square to the screen while four lock-on brackets turn round it. The body carries a
        // faint crimson glow. Awakened marks (they pass on when the host falls) wear four gold chevrons round the brackets.
        private void StatusMarkLook(StatusLook look)
        {
            float H = look.body.y, W = look.body.x, s = look.size;
            Color crimson = Color.Lerp(StatusCrimson, StatusTone(look.tint), .15f);
            float R = look.reach = Mathf.Clamp(1.1f * s, 1, 2.4f);
            Vector2 m = look.anchor = new Vector2(0, H + .16f + R * .45f);
            var halo = StatusHead(look, "Status mark glow", glow, R * 1.7f, Alpha(crimson, .42f), 119, true, m);
            var ink = StatusHead(look, "Status mark reticle ink", StatusReticle[0], R, StatusCrimsonDeep, 120, false, m);
            var body = StatusHead(look, "Status mark reticle", StatusReticle[1], R, new Color(1, .13f, .2f, .97f), 121, false, m);
            var hot = StatusHead(look, "Status mark reticle light", StatusReticle[2], R, new Color(1, .85f, .85f, .9f), 122, true, m);
            var bracketInk = StatusHead(look, "Status mark bracket ink", StatusBracket[0], R * 1.18f, StatusCrimsonDeep, 120, false, m);
            var bracket = StatusHead(look, "Status mark bracket", StatusBracket[1], R * 1.18f, new Color(1, .2f, .26f, .97f), 121, false, m);
            var bracketHot = StatusHead(look, "Status mark bracket light", StatusBracket[2], R * 1.18f, new Color(1, .82f, .8f, .85f), 122, true, m);
            foreach (var part in new[] { halo, ink, body, hot, bracketInk, bracket, bracketHot }) { part.role = StatusRoleBeat; StatusIntro(part, .24f, 2.4f); }
            foreach (var part in new[] { bracketInk, bracket, bracketHot }) part.clip.spin = 40;
            if (look.grade >= 3)
            {
                // The gold chevrons hold still on the crosshair's axes, outside the brackets' sweep, and beat with it.
                var crownInk = StatusHead(look, "Status mark crown ink", StatusChevron[0], R * 1.85f, StatusGoldDeep, 120, false, m);
                var crown = StatusHead(look, "Status mark crown", StatusChevron[1], R * 1.85f, new Color(1, .76f, .18f, .97f), 121, false, m);
                var crownHot = StatusHead(look, "Status mark crown light", StatusChevron[2], R * 1.85f, new Color(1, .96f, .78f, .9f), 122, true, m);
                foreach (var part in new[] { crownInk, crown, crownHot }) { part.role = StatusRoleBeat; StatusIntro(part, .32f, 2); }
            }
            var aura = StatusAttach(look, "Status mark aura", glow, W * 1.25f + .4f, Alpha(crimson, .17f), 120, true, new Vector2(0, H * .5f)); aura.clip.pulse = .05f;
        }

        // Cursed: an occult eye sigil turns over the head; smoky tendrils hang from it down the body, dark smoke drips off
        // and a violet shadow lies behind the actor. Awakened curses (they pass on twice) add a pale rune ring.
        private void StatusCurseLook(StatusLook look)
        {
            float H = look.body.y, W = look.body.x, s = look.size;
            Color violet = Color.Lerp(StatusViolet, StatusTone(look.tint), .2f);
            float S = look.reach = Mathf.Clamp(1.1f * s, 1, 2.4f);
            Vector2 g = look.anchor = new Vector2(0, H + .14f + S * .48f);
            look.slot = S * .9f + .04f;
            var halo = StatusHead(look, "Status curse glow", glow, S * 1.75f, Alpha(violet, .36f), 119, true, g);
            var ink = StatusHead(look, "Status curse sigil ink", StatusSigil[0], S, StatusVioletDeep, 120, false, g);
            var body = StatusHead(look, "Status curse sigil", StatusSigil[1], S, new Color(.52f, .1f, .84f, .96f), 121, false, g);
            var hot = StatusHead(look, "Status curse sigil light", StatusSigil[2], S, new Color(.93f, .78f, 1, .88f), 122, true, g);
            foreach (var part in new[] { ink, body, hot }) part.clip.spin = -26;
            // The eye inside stays level while the thorned ring turns around it.
            var eyeInk = StatusHead(look, "Status curse eye ink", StatusEye[0], S * .8f, StatusVioletDeep, 120, false, g);
            var eye = StatusHead(look, "Status curse eye", StatusEye[1], S * .8f, new Color(.6f, .14f, .9f, .97f), 121, false, g);
            var gaze = StatusHead(look, "Status curse eye light", StatusEye[2], S * .8f, new Color(1, .82f, 1, .95f), 123, true, g);
            foreach (var part in new[] { halo, ink, body, hot, eyeInk, eye, gaze }) { part.role = StatusRoleBeat; StatusIntro(part, .26f, .25f); }
            if (look.grade >= 3)
            {
                var rune = StatusHead(look, "Status curse awakened rune", runeRing, S * 1.42f, Alpha(new Color(.86f, .66f, 1), .5f), 122, true, g);
                rune.role = StatusRoleBeat; rune.clip.spin = 34; StatusIntro(rune, .3f, .25f);
            }
            StatusAttach(look, "Status curse shadow", glow, W * 1.7f + .6f, new Color(.09f, 0, .14f, .45f), 29, false, new Vector2(0, H * .5f));
            var miasma = StatusAttach(look, "Status curse miasma", glow, W * 1.2f + .3f, Alpha(violet, .14f), 120, true, new Vector2(0, H * .45f)); miasma.clip.flicker = .2f;
            // Smoke pours off the sigil's lower rim in soft wisps that curl out over the shoulders and thin away over the top
            // of the body; a shorter third one spills from under the eye. They hang from the sigil, so they stretch when it is
            // raised over a stun.
            float cosLean = Mathf.Cos(18 * Mathf.Deg2Rad);
            for (int i = 0; i < 3; i++)
            {
                bool mid = i == 2; float side = mid ? 0 : i == 0 ? -1 : 1;
                Vector2 top = mid ? g + Vector2.down * S * .3f : g + Direction(-90 + side * 40) * S * .42f;
                float width = (mid ? .5f : .7f) * s, length = mid ? Mathf.Max(H * .45f, top.y - H * .62f) : Mathf.Max(H * .5f, (top.y - H * .42f) / cosLean);
                var drip = StatusSize(StatusHead(look, "Status curse tendril ink", StatusWisp, 1, new Color(.16f, .01f, .26f, .85f), 121, false, top), width, length);
                drip.length = length;
                var parts = mid ? new[] { drip } : new[] { drip, StatusSize(StatusHead(look, "Status curse tendril light", StatusWisp, 1, Alpha(Color.Lerp(violet, Color.white, .3f), .5f), 122, true, top), width * .4f, length * .9f) };
                foreach (var part in parts)
                {
                    part.role = StatusRoleSway; part.angle = side * 18; part.swing = mid ? 8 : 12; part.phase = i * 1.9f; part.length = length;
                    if (i == 1) part.scale.x *= -1;
                    StatusIntro(part, .45f, 0, true);
                }
            }
        }

        // Seeded: a glowing bud sits on the chest with leaves and vines spreading over the host. It grows over the seed's
        // whole time, its heartbeat quickens, its petals part to show the light inside and it flares just before it blooms.
        private void StatusSeedLook(StatusLook look)
        {
            float H = look.body.y, W = look.body.x, s = look.size;
            Color pink = Color.Lerp(StatusPink, StatusTone(look.tint), .2f);
            // The bud is sized to the host's width and sits high on the chest, so even fully open it flowers over the head
            // instead of covering the enemy; leaves, vines and the dark blush stay behind the body.
            float P = look.reach = Mathf.Clamp(Mathf.Min(.46f * s, W * .3f + .12f), .3f, 1);
            Vector2 bud = look.anchor = new Vector2(W * .06f, Mathf.Max(H * .62f, H - P * .35f));
            var halo = StatusAttach(look, "Status seed glow", glow, P * 3.4f, Alpha(pink, .38f), 126, true, bud + Vector2.up * P * .8f); halo.role = StatusRoleBloom;
            var life = StatusAttach(look, "Status seed life", glow, P * 5, new Color(.45f, 1, .55f, .3f), 26, true, bud + Vector2.up * P * .6f); life.role = StatusRoleBloom;
            var blush = StatusAttach(look, "Status seed blush", glow, P * 3, new Color(.85f, .2f, .5f, .32f), 27, false, bud + Vector2.up * P * .8f); blush.role = StatusRoleBloom;
            if (look.grade >= 3)
            {
                // An awakened seed (it blooms twice as hard) wears a gold-white halo ring over a gold-brown ink ring.
                var crownInk = StatusAttach(look, "Status seed awakened ring ink", ring, P * 3, new Color(.36f, .2f, .02f, .6f), 117, false, bud + Vector2.up * P * .85f);
                crownInk.role = StatusRoleBloom;
                var crown = StatusAttach(look, "Status seed awakened ring", ring, P * 2.9f, new Color(1, .9f, .62f, .8f), 125, true, bud + Vector2.up * P * .85f);
                crown.role = StatusRoleBloom;
            }
            for (int i = 0; i < 2; i++)
            {
                var vine = StatusSize(StatusAttach(look, "Status seed vine", StatusTendril, 1, new Color(.16f, .46f, .12f, .9f), 28, false, bud), .28f * s, H * .6f);
                vine.role = StatusRoleVine; vine.angle = i == 0 ? 40 : -46; vine.clip.angle = vine.angle;
            }
            for (int i = 0; i < 2; i++)
            {
                float angle = i == 0 ? 200 : -20;
                var leaf = StatusAttach(look, "Status seed leaf", petal, P * 1.25f, new Color(.27f, .64f, .2f, .97f), 29, false, bud + Direction(angle) * P * .6f + Vector2.up * P * .1f);
                leaf.role = StatusRoleLeaf; leaf.clip.angle = angle;
            }
            var back = StatusAttach(look, "Status seed petal", StatusPetal, P, Color.white, 122, false, bud); back.role = StatusRolePetal;
            var light = StatusAttach(look, "Status seed light", core, P * .95f, new Color(1, .95f, .84f, .9f), 123, true, bud + Vector2.up * P * 1.05f); light.role = StatusRoleLight;
            for (int i = 0; i < 2; i++)
            {
                var side = StatusAttach(look, "Status seed petal", StatusPetal, P * .96f, Color.white, 124, false, bud);
                side.role = StatusRolePetal; side.angle = i == 0 ? 1 : -1; if (i == 1) side.scale.x *= -1;
            }
            foreach (var part in look.parts) if (part.role != StatusRoleVine) StatusIntro(part, .25f, 0);
        }

        // ---------------------------------------------------------------- driving a status

        private void StatusDrive(StatusLook look)
        {
            if (!look.holder) return;
            float now = look.primary.age, dt = now - look.clock; if (dt <= 0) return; look.clock = now;
            if (look.actor && look.actor.lossyScale != look.rootScale) StatusNeutral(look);
            bool live = !look.clearing && now < look.until;
            Vector2 at = look.holder.position;
            float beat = 1, grow = 1, open = 0;
            if (look.kind == StatusKind.Curse || look.kind == StatusKind.Mark)
                look.lift = Mathf.Lerp(look.lift, StatusLiftTarget(look), 1 - Mathf.Exp(-dt * 9));
            switch (look.kind)
            {
                case StatusKind.Mark:
                {
                    int cycle = Mathf.FloorToInt(now / StatusMarkBeat); float phase = now - cycle * StatusMarkBeat;
                    beat = 1 + .16f * Mathf.Exp(-phase * 9);
                    if (cycle != look.beat) { look.beat = cycle; if (live && now > .2f) StatusLockPulse(look, at); }
                    break;
                }
                case StatusKind.Curse: beat = 1 + .06f * Mathf.Sin(now * 4.2f); break;
                case StatusKind.Stun: beat = 1 + .05f * Mathf.Sin(now * 9); break;
                case StatusKind.Seed:
                {
                    float prog = look.progress = Mathf.Clamp01((now - look.start) / Mathf.Max(.05f, look.total));
                    grow = Mathf.Lerp(.55f, 1.12f, Mathf.Pow(prog, .7f)); open = Smooth(.4f, 1, prog);
                    look.beatPhase += dt * Mathf.Lerp(1.2f, 5.5f, prog * prog);
                    beat = 1 + Mathf.Lerp(.05f, .16f, prog) * Mathf.Pow(Mathf.Max(0, Mathf.Sin(look.beatPhase * Mathf.PI * 2)), 4);
                    break;
                }
            }
            foreach (var part in look.parts) StatusPose(look, part, now, beat, grow, open);
            if (!live)
            {
                if (look.kind == StatusKind.Freeze && !look.ended && !look.clearing) StatusShatter(look);
                return;
            }
            // Poses and the freeze's break always run; only the free particles thin out in a crowded fight.
            if (clips.Count > HardCap) return;
            dt *= clips.Count > SoftCap ? .4f : 1;
            switch (look.kind)
            {
                case StatusKind.Burn: StatusBurnTick(look, at, dt); break;
                case StatusKind.Freeze: StatusFreezeTick(look, at, dt); break;
                case StatusKind.Stun: StatusStunTick(look, at, dt); break;
                case StatusKind.Curse: StatusCurseTick(look, at, dt); break;
                case StatusKind.Seed: StatusSeedTick(look, at, dt, grow); break;
            }
        }

        private static void StatusPose(StatusLook look, StatusPart part, float now, float beat, float grow, float open)
        {
            var clip = part.clip; if (!clip.root) return;
            float k = 1;
            if (part.intro > 0) { float t = Mathf.Clamp01(now / part.intro); k = Mathf.LerpUnclamped(part.from, 1, 1 - (1 - t) * (1 - t) * (1 - t)); }
            Vector3 scale = part.scale;
            if (part.growY) scale.y *= k; else scale *= k;
            if (part.head) clip.offset = part.offset + new Vector2(0, look.lift);
            switch (part.role)
            {
                case StatusRoleBeat: scale *= beat; break;
                case StatusRoleOrbit:
                {
                    float th = (now * 290 + part.phase) * Mathf.Deg2Rad, sn = Mathf.Sin(th);
                    clip.offset = part.offset + new Vector2(Mathf.Cos(th) * look.orbitX, sn * look.orbitY);
                    scale *= Mathf.Lerp(.7f, 1.12f, (1 - sn) * .5f);
                    int order = sn > 0 ? part.order - 9 : part.order;
                    if (clip.renderer.sortingOrder != order) clip.renderer.sortingOrder = order;
                    break;
                }
                case StatusRoleSway:
                    clip.angle = part.angle + Mathf.Sin(now * 2.1f + part.phase) * part.swing;
                    scale.y *= 1 + .08f * Mathf.Sin(now * 1.6f + part.phase * 2);
                    if (part.head && part.length > 0) scale.y *= 1 + look.lift / part.length;
                    break;
                case StatusRoleBloom:
                    scale *= grow * beat; clip.offset = look.anchor + (part.offset - look.anchor) * grow;
                    clip.color = Alpha(part.color, part.color.a * Mathf.Lerp(.7f, 1.35f, look.progress));
                    break;
                case StatusRolePetal: scale *= grow * (1 + .1f * open); clip.angle = part.angle * (6 + 40 * open); break;
                case StatusRoleLeaf: scale *= grow; clip.offset = look.anchor + (part.offset - look.anchor) * grow; break;
                case StatusRoleVine: scale.y *= Mathf.Lerp(.2f, 1, look.progress); scale.x *= Mathf.Lerp(.6f, 1, look.progress); break;
                case StatusRoleLight:
                    scale *= grow * beat * Mathf.Lerp(.6f, 1.15f, look.progress); clip.offset = look.anchor + (part.offset - look.anchor) * grow;
                    clip.color = Alpha(part.color, part.color.a * Mathf.Lerp(.2f, 1, look.progress));
                    break;
            }
            clip.scale = scale;
        }

        private void StatusBurnTick(StatusLook look, Vector2 at, float dt)
        {
            float H = look.body.y, W = look.body.x, s = look.size, fw = StatusFlameWidth(look), lift = Mathf.Sqrt(s);
            int n = StatusEvery(ref look.bank, 4 + look.grade, dt);
            for (int i = 0; i < n; i++)
            {
                // Free tongues rise only off the top rim and shoulders, never across the middle of the body.
                float side = Chance(.5f) ? -1 : 1;
                var tongue = Flame(at + new Vector2(side * Range(.3f, .48f) * W, Range(.8f, 1) * H), Range(.45f, .65f) * fw, new Vector2(side * Range(0, .3f), Range(1.3f, 2.1f) * lift), Range(.3f, .42f), 126);
                tongue.drag = .6f;
                var ember = Spark(at + new Vector2(Range(-.4f, .4f) * W, Range(.35f, .95f) * H), new Vector2(Range(-.7f, .7f), Range(1.4f, 2.8f) * lift), Color.Lerp(StatusFire, Warm, Range(0, .6f)), Range(.12f, .22f) * lift, Range(.45f, .8f), -1.2f, 1.2f);
                ember.flicker = .4f;
            }
            if (StatusEvery(ref look.bank2, 2.2f, dt) > 0)
            {
                var soot = Puff(at + new Vector2(Range(-.22f, .22f) * W, H * Range(.85f, 1.05f)), Range(.4f, .58f) * s, new Color(.2f, .14f, .12f, .4f), new Vector2(Range(-.3f, .3f), Range(.8f, 1.3f)), Range(.7f, 1), 112);
                soot.endScale = 2.1f; soot.gravity = -.3f;
            }
        }

        private void StatusFreezeTick(StatusLook look, Vector2 at, float dt)
        {
            float H = look.body.y, W = look.body.x, s = look.size;
            if (StatusEvery(ref look.bank, 3.5f, dt) > 0)
            {
                var mist = Puff(at + new Vector2(Range(-.6f, .6f) * W, Range(.05f, .3f)), Range(.38f, .55f) * s, new Color(.86f, .94f, 1, .42f), new Vector2(Range(-.6f, .6f), Range(.05f, .3f)), Range(.6f, .9f), 124);
                mist.endScale = 2.2f; mist.drag = 2;
            }
            if (StatusEvery(ref look.bank2, 2.5f, dt) > 0)
                Glint(at + new Vector2(Range(-.4f, .4f) * W, Range(.2f, .95f) * H), Range(.35f, .55f) * s, new Color(.92f, .99f, 1, .95f), .3f, Range(0, 45));
        }

        private void StatusStunTick(StatusLook look, Vector2 at, float dt)
        {
            if (StatusEvery(ref look.bank, 3, dt) == 0) return;
            float th = Range(0, 360) * Mathf.Deg2Rad;
            var mote = Spawn("Status stun sparkle", glow, at + look.anchor + new Vector2(Mathf.Cos(th) * look.orbitX, Mathf.Sin(th) * look.orbitY), Range(.1f, .18f) * look.size, new Color(1, .9f, .55f, .95f), Range(.25f, .4f), true, 127);
            mote.velocity = new Vector2(Range(-.4f, .4f), Range(.2f, .7f)); mote.flicker = .5f; mote.endScale = .15f; mote.ease = 1;
        }

        private void StatusCurseTick(StatusLook look, Vector2 at, float dt)
        {
            float H = look.body.y, W = look.body.x, s = look.size;
            int n = StatusEvery(ref look.bank, 3, dt);
            for (int i = 0; i < n; i++)
            {
                // Dark smoke spills from just under the sigil and runs down over the body.
                var drip = Puff(at + new Vector2(Range(-.3f, .3f) * W, H + .05f + look.lift * .7f), Range(.5f, .68f) * s, new Color(.2f, .03f, .33f, .82f), new Vector2(Range(-.15f, .15f), -Range(.5f, .8f)), Range(.75f, 1), 121);
                drip.drag = 1; drip.startScale = .6f; drip.endScale = 1.5f; drip.fadeIn = .1f;
            }
            n = StatusEvery(ref look.bank2, 2.5f, dt);
            for (int i = 0; i < n; i++)
            {
                var mote = Spawn("Status curse mote", wisp, at + new Vector2(Range(-.4f, .4f) * W, Range(.4f, 1) * H), Range(.12f, .2f) * s, new Color(.78f, .5f, 1, .9f), Range(.5f, .8f), true, 123);
                mote.velocity = new Vector2(0, -Range(.4f, .9f)); mote.fadeIn = .08f; mote.endScale = .3f; mote.flicker = .3f;
            }
        }

        private void StatusSeedTick(StatusLook look, Vector2 at, float dt, float grow)
        {
            float prog = look.progress, P = look.reach, s = look.size;
            Vector2 heart = at + look.anchor + Vector2.up * P * grow;
            int n = StatusEvery(ref look.bank, Mathf.Lerp(2, 7, prog), dt);
            for (int i = 0; i < n; i++)
            {
                bool rose = Chance(.5f);
                var pollen = Spawn("Status seed pollen", glow, heart + Inside(P * .7f), Range(.07f, .14f) * s, rose ? new Color(1, .7f, .88f, .95f) : new Color(.6f, 1, .6f, .9f), Range(.5f, .8f), true, 127);
                pollen.velocity = new Vector2(Range(-.3f, .3f), Range(.4f, 1)); pollen.flicker = .5f; pollen.endScale = .2f; pollen.ease = 1;
            }
            if (prog > .7f && StatusEvery(ref look.bank2, (prog - .7f) * 14, dt) > 0)
                Glint(heart + Inside(P * .3f), Range(.5f, .8f) * s, new Color(1, .9f, .96f, .9f), .25f, Range(0, 45));
        }

        /// <summary>A mark's lock-on beat: a crimson ring snaps out of the reticle.</summary>
        private void StatusLockPulse(StatusLook look, Vector2 at)
        {
            Vector2 c = at + look.anchor + new Vector2(0, look.lift); float R = look.reach;
            var ink = Spawn("Status mark lock ink", ring, c, R * 1.25f, Alpha(StatusCrimsonDeep, .55f), .4f, false, 119); ink.startScale = .55f; ink.endScale = 1.35f; ink.ease = 2.5f; ink.fadePower = 1.3f;
            var lit = Spawn("Status mark lock", ring, c, R * 1.2f, new Color(1, .35f, .4f, .75f), .36f, true, 122); lit.startScale = .55f; lit.endScale = 1.3f; lit.ease = 2.5f; lit.fadePower = 1.5f;
        }

        /// <summary>The ice shell breaking when the hold ends or the frozen enemy falls.</summary>
        private void StatusShatter(StatusLook look)
        {
            look.ended = true;
            if (!look.holder) return;
            float H = look.body.y, W = look.body.x, s = look.size;
            Vector2 mid = (Vector2)look.holder.position + Vector2.up * H * .5f;
            Flash(mid, 1.4f * s, new Color(.85f, .97f, 1, .75f), .12f);
            // Pale ice chips (half of them lit) pop up and out over the top of the shell, then fall.
            int n = Budget(8);
            for (int i = 0; i < n; i++)
            {
                bool lit = i % 2 == 1; float angle = Range(25, 155);
                Vector2 dir = Direction(angle), from = mid + new Vector2(dir.x * .4f * W, Range(-.1f, .4f) * H);
                var chip = Spawn(lit ? "Status ice chip light" : "Status ice chip", shard, from, Range(.2f, .32f) * s, lit ? new Color(.85f, .97f, 1, .95f) : new Color(.9f, .98f, 1, .97f), Range(.4f, .6f), lit, lit ? 131 : 130);
                chip.velocity = dir * Range(2.4f, 4.2f) + Vector2.up * .8f; chip.gravity = 11; chip.spin = Range(-500, 500); chip.angle = Range(0, 360); chip.hold = .4f; chip.fadePower = 1.2f;
            }
            for (int i = 0; i < 2; i++) StatusLater(Glint(mid + new Vector2(Range(-.35f, .35f) * W, Range(0, .4f) * H), Range(.5f, .7f) * s, new Color(.92f, .99f, 1, .95f), .22f, Range(0, 45)), i * .05f);
            for (int i = 0; i < 3; i++)
            {
                var puff = Puff(mid + new Vector2(Range(-.4f, .4f) * W, Range(-.4f, .2f) * H), Range(.5f, .7f) * s, new Color(.88f, .95f, 1, .5f), new Vector2(Range(-1.2f, 1.2f), Range(.2f, .8f)), Range(.5f, .7f), 124);
                puff.endScale = 2;
            }
            Sparkles(mid, 5, .5f * W, new Color(.8f, .95f, 1));
        }

        // ---------------------------------------------------------------- reactions

        // Lightning on the slowed or stunned: a blue-white flash, jagged arcs crackling out (deep blue ink under white light),
        // frost flakes, ice chips and glints where the arcs end.
        private void StatusSuperconduct(Vector2 pos, float g)
        {
            Color storm = new Color(.6f, .8f, 1), hot = new Color(.9f, .97f, 1), ink = new Color(.03f, .08f, .32f, .75f);
            Color frost = new Color(.5f, .95f, 1);
            Underlay(pos, 2.6f * g, .3f, .32f);
            var flash = Spawn("Status superconduct flash", glow, pos, 2.1f * g, Alpha(storm, .85f), .18f, true, 131); flash.startScale = .45f; flash.endScale = 1.25f; flash.ease = 3; flash.fadePower = 1.4f;
            // A small six-armed ice crystal snaps open just after the strike (deep blue ink behind the body, cyan light in
            // front) and turns as it fades: an accent the size of the enemy, never a cover over it.
            float cw = 1.25f * (1 + (Grade - 1) * .08f);
            for (int layer = 0; layer < 2; layer++)
            {
                bool lit = layer == 1;
                var crystal = StatusLater(Spawn(lit ? "Status superconduct crystal" : "Status superconduct crystal ink", StatusCrystal[lit ? 1 : 0], pos, cw,
                    lit ? Alpha(frost, .7f) : new Color(.03f, .1f, .38f, .8f), lit ? .36f : .42f, lit, lit ? 131 : 29), .05f);
                crystal.startScale = .35f; crystal.endScale = 1.1f; crystal.ease = 5; crystal.spin = 70; crystal.hold = lit ? .15f : .3f; crystal.fadePower = 1.4f;
            }
            var crystalCore = StatusLater(Spawn("Status superconduct crystal core", StatusCrystal[2], pos, cw, Color.white, .24f, true, 132), .05f);
            crystalCore.startScale = .35f; crystalCore.endScale = 1.1f; crystalCore.ease = 5; crystalCore.spin = 70; crystalCore.fadePower = 1.6f;
            var heart = Spawn("Status superconduct core", core, pos, .55f * g, Color.white, .09f, true, 133); heart.startScale = .6f; heart.endScale = 1.5f; heart.ease = 2;
            var inkRing = Spawn("Status superconduct ink ring", ring, pos, 2.4f * g, ink, .3f, false, 119); inkRing.startScale = .15f; inkRing.endScale = 1; inkRing.ease = 3; inkRing.fadePower = 1.3f;
            Wave(pos, 2.7f * g, Alpha(storm, .9f), .28f, 3);
            // The crackle is the star: long blue-white arcs (one ink stroke under a two-segment bolt) reach well out of the
            // body just after the causing bolt's own flash, and one more snaps out as the crystal opens.
            int arcs = Grade >= 3 ? 5 : 4; float reach = 1 + (Grade - 1) * .08f;
            for (int i = 0; i < arcs + 1; i++)
            {
                bool late = i >= arcs;
                float angle = i * 360f / arcs + Range(-22, 22) + (late ? 36 : 0), length = Range(1.2f, 1.6f) * reach * (late ? .8f : 1), delay = late ? .2f : .05f + i % 2 * .05f;
                Vector2 dir = Direction(angle), side = new Vector2(-dir.y, dir.x), start = pos + dir * .12f;
                Vector2 bend = pos + dir * (.12f + length * Range(.42f, .58f)) + side * Range(-.14f, .14f), end = pos + dir * (.12f + length) + side * Range(-.08f, .08f);
                var shade = StatusLater(StatusInk(Line(start, end, .24f, ink, .24f, 119, 1.1f), 119), delay); shade.flicker = .2f;
                var boltA = StatusLater(Line(start, bend, .085f, hot, .22f, 130, 1.2f), delay); boltA.flicker = .35f;
                var boltB = StatusLater(Line(bend, end, .07f, Color.Lerp(hot, storm, .3f), .22f, 130, 1.2f), delay); boltB.flicker = .35f;
                if (!late) StatusLater(Glint(end, Range(.45f, .65f) * g, Alpha(hot, .95f), .2f, Range(0, 45)), delay + .02f);
            }
            int flakes = Budget(3);
            for (int i = 0; i < flakes; i++)
            {
                var mote = Spawn("Status superconduct frost", flake, pos + Inside(.3f), Range(.22f, .34f), new Color(.82f, .95f, 1, .95f), Range(.4f, .6f), true, 130);
                mote.velocity = Direction(Range(0, 360)) * Range(1.4f, 2.8f) * g; mote.drag = 2.5f; mote.spin = Range(-300, 300); mote.endScale = .5f; mote.ease = 1; mote.fadePower = 1.3f;
            }
            int chips = Budget(3);
            for (int i = 0; i < chips; i++)
            {
                var chip = Spawn("Status superconduct ice", shard, pos + Inside(.15f), Range(.16f, .26f), new Color(.85f, .96f, 1, .95f), Range(.35f, .5f), false, 124);
                chip.velocity = Direction(Range(0, 360)) * Range(2.5f, 4) * g + Vector2.up; chip.gravity = 8; chip.drag = 1; chip.spin = Range(-500, 500); chip.angle = Range(0, 360); chip.fadePower = 1.2f;
            }
            for (int i = 0; i < 3; i++) Spark(pos, Direction(Range(0, 360)) * Range(3, 6) * g, i % 2 == 0 ? Color.white : storm, Range(.2f, .3f), Range(.18f, .3f), 2, 3);
        }

        // Fire on the slowed or frost on the burning: just after the blow's own flash has passed, a white steam explosion
        // goes off on top of it (white vapour over cool blue-grey shading and a deep blue ink ring, so it has volume and an
        // edge on a pale sky), hissing white jets shoot up and a tall white column keeps boiling off the body.
        private void StatusSteam(Vector2 pos, float g)
        {
            const float Late = .09f;
            Color vapor = new Color(.97f, .99f, 1, .95f), shade = new Color(.55f, .66f, .8f, .45f);
            var flash = StatusLater(Spawn("Status steam flash", glow, pos, 1.9f * g, new Color(.92f, .97f, 1, .7f), .16f, true, 133), Late); flash.startScale = .5f; flash.endScale = 1.3f; flash.ease = 3; flash.fadePower = 1.4f;
            var inkRing = StatusLater(Spawn("Status steam ring ink", ring, pos, 2.8f * g, Alpha(StatusIceDeep, .5f), .4f, false, 119), Late); inkRing.startScale = .15f; inkRing.endScale = 1; inkRing.ease = 3; inkRing.fadePower = 1.3f;
            var pop = StatusLater(Spawn("Status steam ring", ring, pos, 2.6f * g, new Color(.97f, .99f, 1, .92f), .34f, false, 134), Late); pop.startScale = .15f; pop.endScale = 1; pop.ease = 3; pop.fadePower = 1.3f;
            int shades = Budget(5);
            for (int i = 0; i < shades; i++)
            {
                var puff = StatusLater(StatusVaporPuff(pos + Inside(.2f) + Vector2.down * .1f, Range(.6f, .9f) * g, shade, Direction(i * 360f / shades + Range(-25, 25)) * Range(2, 3.4f) * g, Range(.6f, .9f), 133), Late);
                puff.gravity = -1; puff.endScale = 2.3f;
            }
            int puffs = Budget(9);
            for (int i = 0; i < puffs; i++)
            {
                var puff = StatusLater(StatusVaporPuff(pos + Inside(.2f), Range(.55f, .85f) * g, vapor, Direction(i * 360f / puffs + Range(-20, 20)) * Range(2.6f, 4.4f) * g + Vector2.up * .5f, Range(.7f, 1.1f), 134), Late);
                puff.gravity = -1.6f; puff.endScale = 2.5f; puff.fadeIn = .02f;
            }
            // A white column keeps boiling up off the body after the burst, so the reaction outlasts the blow that caused it.
            int column = Budget(7);
            for (int i = 0; i < column; i++)
            {
                bool dark = i % 3 == 2; float delay = Late + .04f + i * .05f;
                var puff = StatusLater(StatusVaporPuff(pos + new Vector2(Range(-.25f, .25f), Range(-.1f, .25f)), Range(.6f, .85f) * g, dark ? shade : vapor,
                    new Vector2(Range(-.3f, .3f), Range(2.2f, 3.2f) * g), Range(1.2f, 1.4f), dark ? 133 : 135), delay);
                puff.gravity = -1.2f; puff.drag = 1.6f; puff.endScale = 3; puff.fadeIn = .06f;
            }
            for (int i = 0; i < 3; i++)
            {
                Vector2 dir = Direction(90 + Range(-40, 40));
                var jet = StatusLater(StatusInk(Line(pos + dir * .1f, pos + dir * Range(1.1f, 1.6f) * g, .2f, Color.white, .26f, 134), 134), Late);
                jet.velocity = dir * 2; jet.startScale = .3f; jet.endScale = 1; jet.ease = 3; jet.fadePower = 1.4f;
            }
            for (int i = 0; i < 6; i++)
            {
                var drop = StatusLater(Spark(pos, Direction(Range(15, 165)) * Range(2.5f, 5) * g, i % 2 == 0 ? new Color(.9f, .97f, 1, .95f) : new Color(.45f, .65f, .9f, .9f), Range(.12f, .18f), Range(.35f, .5f), 10, 1.2f), Late);
                if (i % 2 == 1) StatusInk(drop, 132);
            }
        }

        // Melee on the stunned: a golden guard crest flashes, cracks down the middle and its halves tumble away, under a
        // white cross flash, gold impact lines and a spray of gold facets.
        private void StatusDefenseless(Vector2 pos, float g)
        {
            Color gold = new Color(1, .8f, .3f), body = new Color(1, .7f, .16f, 1), hot = new Color(1, .96f, .82f), ink = new Color(.3f, .14f, .01f, .85f);
            Underlay(pos, 2.4f * g, .3f, .32f);
            var flash = Spawn("Status guard flash", glow, pos, 1.8f * g, Alpha(hot, .8f), .14f, true, 131); flash.startScale = .5f; flash.endScale = 1.3f; flash.ease = 3; flash.fadePower = 1.4f;
            // The crest is about the enemy's size; its halves fly apart sideways with a quick turn and are gone in a third of
            // a second, a white-gold glint on their faces as they break.
            float size = 1.05f * g;
            for (int half = 0; half < 2; half++)
            {
                float side = half == 0 ? -1 : 1;
                var whole = Spawn("Status guard", StatusGuard[half], pos, size, body, .07f, false, 121); whole.fadePower = 0;
                var wholeInk = Spawn("Status guard ink", StatusGuard[half + 2], pos, size, ink, .07f, false, 120); wholeInk.fadePower = 0;
                Vector2 velocity = new Vector2(side * Range(3.4f, 4.4f), Range(.8f, 1.4f)) * g; float spin = -side * Range(90, 140);
                var shadow = StatusLater(Spawn("Status guard half ink", StatusGuard[half + 2], pos, size, ink, .35f, false, 120), .06f);
                var shell = StatusLater(Spawn("Status guard half", StatusGuard[half], pos, size, body, .35f, false, 121), .06f);
                var shine = StatusLater(Spawn("Status guard half light", StatusGuard[half], pos, size, Alpha(hot, .8f), .2f, true, 122), .06f);
                foreach (var c in new[] { shadow, shell, shine }) { c.velocity = velocity; c.gravity = 7; c.drag = 1.2f; c.spin = spin; c.hold = .25f; c.fadePower = 1.3f; }
                shine.hold = 0; shine.fadePower = 1.6f;
            }
            var crack = StatusLater(Line(pos + Vector2.up * .42f * size, pos + Vector2.down * .45f * size, .1f, Color.white, .12f, 132, 1.05f), .03f); crack.fadePower = 1.2f;
            for (int i = 0; i < 2; i++)
            {
                var cross = Spawn("Status guard cross", streak, pos, 2.4f * g, i == 0 ? Color.white : Alpha(gold, .9f), .16f, true, 132);
                cross.scale.y *= .16f; cross.angle = i == 0 ? 45 : -45; cross.startScale = .3f; cross.endScale = 1; cross.ease = 3; cross.fadePower = 1.4f;
            }
            ImpactLines(pos, 3.4f * g, Alpha(gold, .75f), .24f);
            StatusInk(ImpactLines(pos, 3.6f * g, ink, .26f), 119);
            Wave(pos, 2.2f * g, Alpha(gold, .8f), .24f, 3);
            int facets = Budget(8);
            for (int i = 0; i < facets; i++)
            {
                bool lit = i % 3 == 0;
                var facet = Spawn("Status guard facet", StatusFacet, pos + Inside(.25f), Range(.16f, .26f) * g, lit ? Alpha(hot, .95f) : body, Range(.45f, .65f), lit, lit ? 129 : 123);
                facet.velocity = Direction(Range(0, 360)) * Range(3, 6) * g + Vector2.up; facet.gravity = 10; facet.drag = 1.2f; facet.spin = Range(-600, 600); facet.angle = Range(0, 360); facet.hold = .3f; facet.fadePower = 1.3f;
            }
            for (int i = 0; i < 6; i++) Spark(pos, Direction(Range(0, 360)) * Range(4, 7) * g, i % 2 == 0 ? Color.white : gold, Range(.22f, .36f), Range(.2f, .32f), 6, 3);
        }

        // Poison on the burning: an orange core inside a sickly green bloom, flare lines, a billow of green and olive smoke,
        // bubbles, falling acid drops and orange sparks.
        private void StatusToxic(Vector2 pos, float g)
        {
            Color acid = new Color(.62f, .96f, .2f), sick = new Color(.5f, .74f, .12f, .72f), olive = new Color(.2f, .26f, .04f, .58f), orange = new Color(1, .55f, .12f);
            Underlay(pos, 2.2f * g, .22f, .3f);
            var heart = Spawn("Status toxic core", glow, pos, 1.4f * g, Alpha(orange, .95f), .2f, true, 132); heart.startScale = .5f; heart.endScale = 1.25f; heart.ease = 3; heart.fadePower = 1.4f;
            var bloom = Spawn("Status toxic bloom", glow, pos, 2.3f * g, Alpha(acid, .55f), .3f, true, 130); bloom.startScale = .4f; bloom.endScale = 1.2f; bloom.ease = 3; bloom.fadePower = 1.4f;
            var stain = Spawn("Status toxic ring", ring, pos, 2.5f * g, new Color(.3f, .5f, .04f, .6f), .34f, false, 119); stain.startScale = .15f; stain.endScale = 1; stain.ease = 3; stain.fadePower = 1.3f;
            // Fire and poison together: an orange ring and orange flame tongues burst out over the green bloom.
            var flare = Spawn("Status toxic fire ring", ring, pos, 2.2f * g, new Color(1, .5f, .08f, .8f), .25f, false, 132); flare.startScale = .2f; flare.endScale = 1; flare.ease = 3; flare.fadePower = 1.3f;
            Wave(pos, 2.7f * g, Alpha(acid, .8f), .3f, 3);
            ImpactLines(pos, 3 * g, Alpha(orange, .7f), .22f);
            StatusInk(ImpactLines(pos, 3.2f * g, olive, .26f), 119);
            int tongues = Budget(5);
            for (int i = 0; i < tongues; i++)
            {
                Vector2 d = Direction(i * 360f / tongues + 90 + Range(-20, 20));
                var tongue = Flame(pos + d * .15f, Range(.5f, .7f) * g, d * Range(2.6f, 3.6f) * g, .3f, 132);
                tongue.color = new Color(1, .58f, .16f, .95f); tongue.alignVelocity = true; tongue.angleOffset = -90; tongue.drag = 4;
            }
            int darks = Budget(4);
            for (int i = 0; i < darks; i++)
            {
                var puff = Puff(pos + Inside(.2f), Range(.6f, .9f) * g, olive, Direction(i * 90 + Range(-30, 30)) * Range(1.6f, 2.8f) * g, Range(.7f, 1), 122);
                puff.gravity = -.9f; puff.endScale = 2.2f;
            }
            int puffs = Budget(5);
            for (int i = 0; i < puffs; i++)
            {
                var puff = Puff(pos + Inside(.2f), Range(.55f, .8f) * g, sick, Direction(i * 360f / puffs + Range(-20, 20)) * Range(1.8f, 3.2f) * g + Vector2.up * .4f, Range(.7f, 1.1f), 124);
                puff.gravity = -1.1f; puff.endScale = 2.3f;
            }
            Bubbles(pos, Budget(5), .45f);
            // Sickly smoke keeps welling up off the body, olive under acid green, lit orange from inside at first.
            int fumes = Budget(5);
            for (int i = 0; i < fumes; i++)
            {
                bool dark = i % 2 == 1;
                var fume = StatusLater(Puff(pos + new Vector2(Range(-.25f, .25f), Range(0, .3f)), Range(.55f, .8f) * g, dark ? olive : new Color(.62f, .8f, .14f, .72f),
                    new Vector2(Range(-.3f, .3f), Range(1.6f, 2.6f) * g), Range(.8f, 1.1f), dark ? 122 : 124), .08f + i * .06f);
                fume.gravity = -1; fume.drag = 1.8f; fume.endScale = 2.3f; fume.fadeIn = .06f;
                if (!dark) fume.ramp = Ramp(new Color(1, .75f, .4f), Color.white);
            }
            int drops = Budget(4);
            for (int i = 0; i < drops; i++)
            {
                var drop = StatusInk(Spark(pos + Inside(.2f), Direction(Range(25, 155)) * Range(2, 4) * g, new Color(.42f, .75f, .06f, .95f), Range(.16f, .24f), Range(.45f, .65f), 9, 1), 123);
                drop.flicker = 0;
            }
            for (int i = 0; i < 4; i++) Spark(pos, Direction(Range(0, 360)) * Range(3, 5.5f) * g, Color.Lerp(orange, Warm, .3f), Range(.18f, .28f), Range(.2f, .32f), 3, 2.5f);
        }

        // A curse, mark or seed passing to a new host: violet motes gather into it while four chevrons close in on it from
        // wide, then it flashes and rings ripple out. The emblem is neutral; the status that lands right after says which.
        private void StatusSpread(Vector2 pos, float g)
        {
            Color violet = new Color(.74f, .48f, 1), ink = new Color(.18f, .02f, .3f, .7f);
            Converge(pos, 6, 1.3f * g, violet, .22f, .4f);
            var flash = StatusLater(Spawn("Status spread flash", glow, pos, 1.6f * g, Alpha(violet, .75f), .2f, true, 131), .14f); flash.startScale = .5f; flash.endScale = 1.3f; flash.ease = 3; flash.fadePower = 1.4f;
            for (int layer = 0; layer < 3; layer++)
            {
                var chevrons = Spawn("Status spread chevrons", StatusChevron[layer], pos, 1.5f * g, layer == 0 ? ink : layer == 1 ? new Color(.62f, .3f, 1, .95f) : new Color(.95f, .85f, 1, .9f), .34f, layer == 2, 120 + layer);
                chevrons.startScale = 1.7f; chevrons.endScale = .75f; chevrons.ease = 3; chevrons.angle = 45; chevrons.hold = .55f; chevrons.fadePower = 1.4f; chevrons.fadeIn = .05f;
            }
            for (int i = 0; i < 3; i++)
            {
                float delay = .12f + i * .08f;
                var dark = StatusLater(Spawn("Status spread ripple ink", ring, pos, 2.6f * g, Alpha(ink, .6f), .42f, false, 119), delay); dark.startScale = .15f; dark.endScale = 1; dark.ease = 2.5f; dark.fadePower = 1.3f;
                var lit = StatusLater(Spawn("Status spread ripple", ring, pos, 2.5f * g, Alpha(violet, .85f), .4f, true, 131), delay); lit.startScale = .15f; lit.endScale = 1; lit.ease = 2.5f; lit.fadePower = 1.4f;
            }
            for (int i = 0; i < 2; i++) StatusLater(Glint(pos + Inside(.4f), Range(.5f, .7f) * g, new Color(.95f, .85f, 1, .9f), .22f, Range(0, 45)), .14f + i * .06f);
        }

        // ---------------------------------------------------------------- textures (built on first use, per kind)

        private static void StatusTextures(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Mark:
                    if (StatusReticle == null)
                    {
                        StatusReticle = StatusLayers("Status reticle", 96, (u, v) => StatusReticleShape(u * 1.2f, v * 1.2f) / 1.2f, .08f, .03f);
                        StatusBracket = StatusLayers("Status bracket", 96, (u, v) => StatusBracketShape(u * 1.15f, v * 1.15f) / 1.15f, .08f, .028f);
                        StatusChevron = StatusLayers("Status chevron", 96, (u, v) => StatusChevronShape(u * 1.15f, v * 1.15f) / 1.15f, .08f, .028f);
                    }
                    break;
                case StatusKind.Stun: if (StatusStar == null) StatusStar = StatusLayers("Status star", 48, (u, v) => StatusStarShape(u * 1.12f, v * 1.12f) / 1.12f, .13f, .1f); break;
                case StatusKind.Curse:
                    if (StatusSigil == null)
                    {
                        StatusSigil = StatusLayers("Status sigil", 96, (u, v) => StatusSigilShape(u * 1.18f, v * 1.18f) / 1.18f, .08f, .028f);
                        StatusEye = StatusLayers("Status eye", 64, (u, v) => StatusEyeShape(u * 1.18f, v * 1.18f) / 1.18f, .09f, .03f);
                    }
                    StatusWispTexture(); break;
                case StatusKind.Seed:
                    StatusTendrilTexture();
                    if (StatusPetal == null) StatusPetal = Build("Status petal", 48, 96, StatusPetalPixel, new Vector2(.5f, .03f));
                    break;
                case StatusKind.Freeze:
                    if (StatusShell == null)
                    {
                        StatusShell = Build("Status ice shell", 72, 108, (u, v) => StatusShellPixel(u * 48, (v + 1) * 72, false), new Vector2(.5f, 0));
                        StatusShellLight = Build("Status ice shell light", 72, 108, (u, v) => StatusShellPixel(u * 48, (v + 1) * 72, true), new Vector2(.5f, 0));
                    }
                    break;
            }
        }

        private static Sprite StatusTexture(string name, int width, int height, Color32[] colors, Vector2 pivot)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "Tower effect " + name, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 2 };
            texture.SetPixels32(colors); texture.Apply(true, false);
            return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        /// <summary>Three layers of one shape from its signed distance (negative inside): [0] a wide soft ink outline,
        /// [1] the body and [2] a thin hot core inset from the edges. The distance field is evaluated once.</summary>
        private static Sprite[] StatusLayers(string name, int size, Func<float, float, float> distance, float ink, float inset)
        {
            var field = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (y + .5f) / size * 2 - 1;
                for (int x = 0; x < size; x++) field[y * size + x] = distance((x + .5f) / size * 2 - 1, v);
            }
            float pixel = 2f / size;
            var layers = new Sprite[3];
            for (int layer = 0; layer < 3; layer++)
            {
                var colors = new Color32[field.Length];
                for (int i = 0; i < field.Length; i++)
                {
                    float d = field[i], a;
                    if (layer == 0) a = 1 - Smooth(ink - pixel * 1.5f, ink + pixel * 1.5f, d) + Gauss(Mathf.Max(0, d - ink), ink) * .3f;
                    else if (layer == 1) a = 1 - Smooth(-pixel, pixel, d);
                    else a = 1 - Smooth(-inset - pixel, -inset + pixel, d);
                    colors[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
                layers[layer] = StatusTexture(name + " " + layer, size, size, colors, new Vector2(.5f, .5f));
            }
            return layers;
        }

        /// <summary>A hunter's crosshair, bold enough to read small: a thick ring broken where four tapered ticks cross it
        /// on the axes, and a centre dot.</summary>
        private static float StatusReticleShape(float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
            float rel = Mathf.Abs(Mathf.Repeat(a + 45, 90) - 45), relRad = rel * Mathf.Deg2Rad;
            const float Gap = 17, Radius = .6f;
            float arc = (rel >= Gap ? Mathf.Abs(r - Radius) : Mathf.Sqrt(Sq(r - Radius) + Sq(r * (Gap - rel) * Mathf.Deg2Rad))) - .1f;
            float along = r * Mathf.Cos(relRad), across = r * Mathf.Sin(relRad), t = Mathf.Clamp01((along - .3f) / .66f);
            float tick = Mathf.Max(across - Mathf.Lerp(.05f, .13f, t), Mathf.Max(.3f - along, along - .96f));
            return Mathf.Min(Mathf.Min(arc, tick), r - .1f);
        }

        /// <summary>Four lock-on corner brackets (L shapes) on the diagonals.</summary>
        private static float StatusBracketShape(float u, float v)
        {
            float x = Mathf.Abs(u), y = Mathf.Abs(v);
            return Mathf.Min(SegmentDistance(x, y, .68f, .68f, .3f, .68f), SegmentDistance(x, y, .68f, .68f, .68f, .3f)) - .07f;
        }

        /// <summary>Four chevrons on the axes, pointing in at the centre.</summary>
        private static float StatusChevronShape(float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v), rel = Mathf.Abs(Mathf.Repeat(Mathf.Atan2(v, u) * Mathf.Rad2Deg + 45, 90) - 45) * Mathf.Deg2Rad;
            float along = r * Mathf.Cos(rel), across = r * Mathf.Sin(rel);
            return SegmentDistance(along, across, .66f, 0, .9f, .26f) - .075f;
        }

        /// <summary>A rounded five-point star (point up).</summary>
        private static float StatusStarShape(float u, float v)
        {
            const float Kx = .809017f, Ky = -.5877853f, Radius = .66f, Inner = .46f;
            float px = Mathf.Abs(u), py = v;
            float d1 = Mathf.Max(Kx * px + Ky * py, 0); px -= 2 * d1 * Kx; py -= 2 * d1 * Ky;
            float d2 = Mathf.Max(-Kx * px + Ky * py, 0); px += 2 * d2 * Kx; py -= 2 * d2 * Ky;
            px = Mathf.Abs(px); py -= Radius;
            float bx = Inner * -Ky, by = Inner * Kx - 1;
            float h = Mathf.Clamp((px * bx + py * by) / (bx * bx + by * by), 0, Radius);
            float dx = px - bx * h, dy = py - by * h;
            return Mathf.Sqrt(dx * dx + dy * dy) * Mathf.Sign(py * bx - px * by) - .05f;
        }

        /// <summary>The turning part of the curse sigil: a bold circle with eight thorns.</summary>
        private static float StatusSigilShape(float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
            float circle = Mathf.Abs(r - .62f) - .058f;
            float rel = Mathf.Abs(Mathf.Repeat(a, 45) - 22.5f) * Mathf.Deg2Rad;
            float along = r * Mathf.Cos(rel), across = r * Mathf.Sin(rel), t = Mathf.Clamp01((along - .58f) / .36f);
            float thorn = Mathf.Max(across - .11f * (1 - t), Mathf.Max(.58f - along, along - .94f));
            return Mathf.Min(circle, thorn);
        }

        /// <summary>The still part of the curse sigil: a wide eye with a slit pupil, always level.</summary>
        private static float StatusEyeShape(float u, float v)
        {
            float lid = Mathf.Max(Mathf.Sqrt(u * u + Sq(v + .2f)) - .43f, Mathf.Sqrt(u * u + Sq(v - .2f)) - .43f);
            return Mathf.Min(Mathf.Abs(lid) - .05f, Mathf.Sqrt(Sq(u * 2) + v * v) - .17f);
        }

        /// <summary>A bold six-armed ice crystal with two pairs of branches on each arm.</summary>
        private static void StatusCrystalTexture()
        {
            if (StatusCrystal != null) return;
            StatusCrystal = StatusLayers("Status crystal", 80, (u, v) =>
            {
                u *= 1.12f; v *= 1.12f;
                float r = Mathf.Sqrt(u * u + v * v), sector = (Mathf.Repeat(Mathf.Atan2(v, u) * Mathf.Rad2Deg + 30, 60) - 30) * Mathf.Deg2Rad;
                float x = r * Mathf.Cos(sector), y = Mathf.Abs(r * Mathf.Sin(sector));
                float arm = SegmentDistance(x, y, 0, 0, .86f, 0) - Mathf.Lerp(.075f, .03f, Mathf.Clamp01(x / .86f));
                float near = SegmentDistance(x, y, .42f, 0, .54f, .208f) - .04f, far = SegmentDistance(x, y, .64f, 0, .715f, .13f) - .032f;
                return Mathf.Min(Mathf.Min(arm, r - .16f), Mathf.Min(near, far)) / 1.12f;
            }, .08f, .028f);
        }

        /// <summary>A smoky tendril hanging from its top (pivot): wavy, tapering, ending in a drop.</summary>
        private static void StatusTendrilTexture()
        {
            if (StatusTendril != null) return;
            StatusTendril = Build("Status tendril", 32, 128, (u, v) =>
            {
                float t = (1 - v) * .5f, center = Mathf.Sin(t * 6.5f + .6f) * .26f * t, half = Mathf.Lerp(.6f, .14f, Mathf.Pow(t, .8f));
                float body = (1 - Smooth(.5f, 1, Mathf.Abs(u - center) / half)) * Smooth(0, .05f, t) * (1 - Smooth(.84f, .92f, t));
                float tipX = Mathf.Sin(.89f * 6.5f + .6f) * .26f * .89f;
                float drop = 1 - Smooth(.55f, 1, Mathf.Sqrt(Sq((u - tipX) / .32f) + Sq((t - .89f) / .042f)));
                float grain = .7f + .3f * Mathf.PerlinNoise(u * 1.7f + 3, t * 9 + 1);
                return White(Mathf.Max(body * grain, drop * .95f));
            }, new Vector2(.5f, 1));
        }

        /// <summary>A soft smoke wisp hanging from its top (pivot): broad and dense under the pivot, waving, and thinning
        /// into nothing over its lower half, so it reads as smoke pouring down rather than a limb.</summary>
        private static void StatusWispTexture()
        {
            if (StatusWisp != null) return;
            StatusWisp = Build("Status wisp", 48, 128, (u, v) =>
            {
                float t = (1 - v) * .5f, center = Mathf.Sin(t * 5 + .4f) * .2f * t, half = Mathf.Lerp(.78f, .34f, t);
                float body = (1 - Smooth(.3f, 1, Mathf.Abs(u - center) / half)) * Smooth(0, .08f, t) * (1 - Smooth(.45f, .98f, t));
                float grain = .55f + .45f * Mathf.PerlinNoise(u * 2.3f + 5, t * 7 + 2);
                return White(body * grain * 1.15f);
            }, new Vector2(.5f, 1));
        }

        /// <summary>Bright steam puffs: the shared smoke is grey at heart, so white vapour gets its own near-white texture,
        /// lit from above and a touch darker underneath for volume.</summary>
        private static void StatusVaporTextures()
        {
            if (StatusVapor != null) return;
            StatusVapor = new Sprite[2];
            for (int i = 0; i < StatusVapor.Length; i++)
            {
                float seed = i * 23.1f + 7;
                StatusVapor[i] = Build("Status vapor " + i, 112, 112, (u, v) =>
                {
                    float n = Fbm(u * 1.6f + seed, v * 1.6f + seed * .6f, 4), n2 = Fbm(u * 3.8f + seed + 4, v * 3.8f - seed, 3);
                    float r = Mathf.Sqrt(u * u + v * v) * (1 + (n2 - .5f) * .5f);
                    float a = Mathf.Clamp01((n * 1.7f - .3f) * (1 - Smooth(.45f, 1, r)) * 1.6f) * Window(Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)));
                    float light = Mathf.Clamp01(.93f + (n2 - .5f) * .22f + v * .08f);
                    return new Color(light, light, light, a);
                });
            }
        }

        /// <summary>A puff of white steam (the same motion as the shared smoke puff).</summary>
        private Clip StatusVaporPuff(Vector2 pos, float width, Color color, Vector2 velocity, float life, int order)
        {
            var clip = Spawn("Status steam", StatusVapor[Chance(.5f) ? 0 : 1], pos, width, color, life, false, order);
            clip.velocity = velocity; clip.drag = 3.2f; clip.startScale = .55f; clip.endScale = 1.8f; clip.ease = 1; clip.spin = Range(-90, 90); clip.fadeIn = .04f; clip.fadePower = 1.3f;
            clip.angle = Range(0, 360);
            return clip;
        }

        /// <summary>A pink petal (pivot at its base): deeper at the base, pale at the tip, with a dark rim that keeps its shape on pale skies.</summary>
        private static Color StatusPetalPixel(float u, float v)
        {
            float t = (v + 1) * .5f, half = .84f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(t), .8f)), .75f);
            float d = Mathf.Abs(u) - half, inside = (1 - Smooth(-.07f, .02f, d)) * Smooth(0, .04f, t);
            if (inside <= 0) return Color.clear;
            float across = Mathf.Clamp01(Mathf.Abs(u) / Mathf.Max(.05f, half));
            Color c = Color.Lerp(new Color(.97f, .46f, .7f), new Color(1, .86f, .93f), Mathf.Clamp01(t * .75f + (1 - across) * .2f));
            c = Color.Lerp(c, new Color(.62f, .1f, .34f), (1 - Smooth(0, .4f, t)) * .6f);
            c = Color.Lerp(c, new Color(.34f, .02f, .16f), Smooth(-.26f, -.05f, d) * .85f);
            c += new Color(.12f, .1f, .1f, 0) * Gauss(u, .06f) * (1 - t);
            c.a = inside; return c;
        }

        /// <summary>The ice shell in texture pixels (96 x 144, bottom centre at 0): three leaning crystals with lit and shaded
        /// faces, gloss, a bright rim and a dark blue outline, on a frosted mound. The light version keeps only the edges and ridges.</summary>
        private static Color StatusShellPixel(float x, float y, bool light)
        {
            Color c = Color.clear;
            for (int k = 0; k < 3; k++)
            {
                int o = k * 5;
                float lean = StatusCrystals[o + 1] * Mathf.Deg2Rad, hw = StatusCrystals[o + 2], h = StatusCrystals[o + 3], tip = StatusCrystals[o + 4];
                float dx = x - StatusCrystals[o], sn = Mathf.Sin(lean), cs = Mathf.Cos(lean);
                float along = -dx * sn + y * cs, across = dx * cs + y * sn;
                float taper = Mathf.Clamp01((h - along) / tip), width = hw * taper;
                float d = Mathf.Max(Mathf.Abs(across) - width, Mathf.Max(-along - 6, along - h));
                if (d > 3) continue;
                float ridge = across - hw * .2f * taper, inside = 1 - Smooth(-.7f, .5f, d);
                Color piece;
                if (light) piece = White(Gauss(d + .9f, 1.2f) * .95f + Gauss(ridge, 1.1f) * inside * .5f);
                else if (d <= 0)
                {
                    piece = ridge < 0 ? new Color(.8f, .94f, 1, .64f) : new Color(.44f, .7f, .96f, .72f);
                    piece = Color.Lerp(piece, new Color(1, 1, 1, .8f), Gauss(across + hw * .5f - along * .1f, 3.2f) * .55f);
                    // Rime: frosted white patches, densest low on the crystal.
                    float rime = Smooth(.5f, .72f, Mathf.PerlinNoise(x * .13f + k * 7, y * .13f + 3)) * (1 - Smooth(.2f, .9f, along / h));
                    piece = Color.Lerp(piece, new Color(.97f, .99f, 1, .9f), rime * .7f);
                    piece = Color.Lerp(piece, new Color(1, 1, 1, .95f), Gauss(d + .9f, 1.3f));
                    piece.a *= inside;
                }
                else piece = new Color(.06f, .18f, .42f, .85f * (1 - Smooth(.6f, 2.6f, d)));
                c = Over(c, piece);
            }
            float md = (Mathf.Sqrt(Sq(x / 44) + Sq((y - 3) / 13)) - 1) * 13;
            if (md < 3)
            {
                Color piece = light ? White(Gauss(md + .9f, 1.2f) * .8f) : md <= 0 ? new Color(.9f, .97f, 1, .82f) : new Color(.06f, .18f, .42f, .8f * (1 - Smooth(.6f, 2.6f, md)));
                c = Over(c, piece);
            }
            return c;
        }

        /// <summary>A heater shield split by a jagged crack: [0] left and [1] right halves (grey bevel, tinted gold), [2] and [3] their ink outlines.</summary>
        private static void StatusGuardTextures()
        {
            if (StatusGuard != null) return;
            const int Size = 64;
            var sets = new Color32[4][];
            for (int k = 0; k < 4; k++) sets[k] = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                float v = ((y + .5f) / Size * 2 - 1) * 1.12f;
                for (int x = 0; x < Size; x++)
                {
                    float u = ((x + .5f) / Size * 2 - 1) * 1.12f;
                    float w = v > .1f ? .7f : .7f * Mathf.Sqrt(Mathf.Max(0, 1 - Sq((v - .1f) / .96f)));
                    float d = Mathf.Max(Mathf.Abs(u) - w, Mathf.Max(v - .7f, -.86f - v));
                    float split = u - (.05f * Mathf.Sin(v * 14 + 1) + .025f * Mathf.Sin(v * 37));
                    float rim = Smooth(-.17f, -.05f, d);
                    float cross = Mathf.Max((1 - Smooth(.07f, .1f, Mathf.Abs(u))) * Band(-.5f, .5f, v), (1 - Smooth(.07f, .1f, Mathf.Abs(v - .2f))) * Band(-.36f, .36f, u));
                    byte l = (byte)(Mathf.Clamp01(.6f + rim * .4f + cross * .28f + (v - u) * .08f) * 255);
                    int i = y * Size + x;
                    for (int side = 0; side < 2; side++)
                    {
                        float dh = Mathf.Max(d, side == 0 ? split + .025f : .025f - split);
                        sets[side][i] = new Color32(l, l, l, (byte)((1 - Smooth(-.02f, .02f, dh)) * 255));
                        sets[side + 2][i] = new Color32(255, 255, 255, (byte)((1 - Smooth(.05f, .1f, dh)) * 255));
                    }
                }
            }
            StatusGuard = new Sprite[4];
            for (int k = 0; k < 4; k++) StatusGuard[k] = StatusTexture("Status guard " + k, Size, Size, sets[k], new Vector2(.5f, .5f));
            StatusFacet = Build("Status facet", 24, 48, (u, v) =>
            {
                float d = Mathf.Abs(u) / .8f + Mathf.Abs(v) / .95f, inside = 1 - Smooth(.84f, .96f, d);
                float l = Mathf.Clamp01((u < 0 ? (v > 0 ? 1 : .82f) : (v > 0 ? .7f : .56f)) + Gauss(d - .86f, .05f) * .3f);
                return new Color(l, l, l, inside);
            });
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>
    /// chuchuking's hero as a pixel actor: his Spine body frames, the free hand in front of the body and, behind it, the
    /// weapon hand on an arm from the shoulder that aims the sword, the crossbow or (with the orb) an open hand.
    /// TowerActorArt keeps the actions, their timing and the colour; this poses them. The sheet comes from
    /// Tools/TowerArt/GodPixels/paint_hero.py (Resources/Tower/Hero). Art pixels run from the ground point between the
    /// boots, x ahead and y up; arm and blade angles are rig angles, 0 straight ahead and 90 up.
    /// </summary>
    public sealed class TowerHeroPixelArt : MonoBehaviour
    {
        public const float PixelsPerUnit = 20;
        /// <summary>The crossbow's renderer, whose local <see cref="CrossbowMuzzle"/> is where a bolt leaves it.</summary>
        public const string CrossbowName = "Crossbow";
        const int WeaponHandCell = 11, FreeHandCell = 12, SwordCell = 13, CrossbowCell = 14, OrbCell = 15, BoltCell = 16, CardCell = 17;
        static readonly string[] FrameNames = { "Idle 0", "Idle 1", "Idle 2", "Idle 3", "Run 0", "Run 1", "Run 2", "Run 3", "Jump", "Fall", "Hurt" };

        [Serializable] sealed class Layout
        {
            public int cell, grip; public float top, arm, swordReach;
            public float[] bodyPivot, front, back, shoulder, muzzle; public int[] bob, card, trim;
        }
        sealed class Sheet { public Layout layout; public Sprite[] body; public Sprite weaponHand, freeHand, sword, crossbow, orb, bolt, card; }
        static Sheet sheet;
        static Texture2D portrait;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { sheet = null; portrait = null; }

        static Sheet Load()
        {
            if (sheet != null && sheet.body[0] != null && sheet.body[0].texture != null) return sheet;
            var asset = Resources.Load<TextAsset>("Tower/Hero/Hero");
            if (asset == null) throw new InvalidOperationException("Missing pixel hero layout: Tower/Hero/Hero");
            var layout = JsonUtility.FromJson<Layout>(asset.text);
            var texture = Resources.Load<Texture2D>("Tower/Hero/Hero");
            if (texture == null) throw new InvalidOperationException("Missing pixel hero sheet: Tower/Hero/Hero");
            if (texture.width < layout.cell * (CardCell + 1) || texture.height < layout.cell)
                throw new InvalidOperationException("The pixel hero sheet is not 18 cells wide.");
            texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
            // The body stands on the point between the boots; hands and weapons turn around their grip pixel.
            var feet = new Vector2(layout.bodyPivot[0] / layout.cell, 1 - layout.bodyPivot[1] / layout.cell);
            var result = new Sheet { layout = layout, body = new Sprite[TowerGodPixelArt.BodyFrames] };
            for (int i = 0; i < result.body.Length; i++) result.body[i] = Cut(texture, layout.cell, i, feet, "Hero " + FrameNames[i]);
            result.weaponHand = Held(texture, layout, WeaponHandCell, "Hero weapon hand");
            result.freeHand = Held(texture, layout, FreeHandCell, "Hero free hand");
            result.sword = Held(texture, layout, SwordCell, "Hero sword");
            result.crossbow = Held(texture, layout, CrossbowCell, "Hero crossbow");
            result.orb = Held(texture, layout, OrbCell, "Hero orb");
            result.bolt = Held(texture, layout, BoltCell, "Hero crossbow bolt");
            var card = new Rect(CardCell * layout.cell + layout.card[0], layout.card[1], layout.card[2], layout.card[3]);
            result.card = Sprite.Create(texture, card, new Vector2(.5f, 0), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            result.card.name = "Hero card portrait";
            sheet = result;
            return sheet;
        }

        static Sprite Cut(Texture2D texture, int size, int cell, Vector2 pivot, string name)
        {
            var sprite = Sprite.Create(texture, new Rect(cell * size, 0, size, size), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        // A hand or weapon cut to its drawn pixels (Hero.json trim: x, y from the cell's top left, width, height), still
        // turning about the grip pixel, so a turning sprite's quad stays close to what it shows.
        static Sprite Held(Texture2D texture, Layout layout, int cell, string name)
        {
            int at = (cell - WeaponHandCell) * 4, x = layout.trim[at], y = layout.trim[at + 1], w = layout.trim[at + 2], h = layout.trim[at + 3];
            float grip = layout.grip + .5f;
            var rect = new Rect(cell * layout.cell + x, layout.cell - y - h, w, h);
            var sprite = Sprite.Create(texture, rect, new Vector2((grip - x) / w, (y + h - grip) / h), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        /// <summary>chuchuking's orb, centred on its pivot, for the orb that floats at the hero's shoulder.</summary>
        public static Sprite OrbSprite { get { return Load().orb; } }
        /// <summary>chuchuking's crossbow bolt, centred on its pivot and pointing up.</summary>
        public static Sprite BoltSprite { get { return Load().bolt; } }
        /// <summary>Where a bolt leaves the crossbow, in the local space of the renderer named <see cref="CrossbowName"/>.</summary>
        public static Vector3 CrossbowMuzzle { get { var m = Load().layout.muzzle; return new Vector3(m[0], m[1], 0) / PixelsPerUnit; } }

        /// <summary>A cached camera portrait of the card cell, point filtered like the companions' portraits.</summary>
        public static Texture2D Portrait()
        {
            if (portrait != null) return portrait;
            var root = new GameObject("Hero portrait") { hideFlags = HideFlags.HideAndDontSave }; root.layer = 31; root.transform.position = new Vector3(10000, 10000, 0);
            var view = root.AddComponent<SpriteRenderer>(); view.sprite = Load().card;
            var cameraObject = new GameObject("Hero portrait camera") { hideFlags = HideFlags.HideAndDontSave }; var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            var bounds = view.sprite.bounds; float extent = Mathf.Max(bounds.size.x, bounds.size.y);
            camera.orthographic = true; camera.orthographicSize = extent * .5f; camera.aspect = 1; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << 31; camera.allowHDR = false; camera.allowMSAA = false;
            camera.transform.position = root.transform.position + new Vector3(bounds.center.x, bounds.center.y, -10);
            var target = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, antiAliasing = 1 }; target.Create(); camera.targetTexture = target;
            var previous = RenderTexture.active; camera.Render(); RenderTexture.active = target;
            portrait = new Texture2D(128, 128, TextureFormat.RGBA32, false) { name = "Hero portrait", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            portrait.ReadPixels(new Rect(0, 0, 128, 128), 0, 0, false); portrait.Apply(false, false);
            RenderTexture.active = previous; camera.targetTexture = null; root.SetActive(false); target.Release(); Destroy(target); Destroy(cameraObject); Destroy(root);
            return portrait;
        }

        Sheet art;
        Layout layout;
        Transform flip, pose, hand, handTurn, weapon, free;
        SpriteRenderer bodyView, handView, weaponView;
        SpriteRenderer[] views;
        TowerAttackMode mode = TowerAttackMode.Sword;
        Vector2 aimDirection = Vector2.right, swingDirection = Vector2.right, aimPoint;
        bool hasAimPoint, aimActive, aimedSwing, swingRising, guardLow, grounded = true, posed;
        float gait, castWeight, guardBlend, sinceSwing, shownArm, shownBlade, recoil, beckon, sink, lean, airLift;
        int facing = 1;

        public int CurrentFrame { get; private set; }
        public SpriteRenderer Body { get { return bodyView; } }
        /// <summary>Whether the sword rests in the low guard, so the next aimed swing rises.</summary>
        public bool GuardLow { get { return guardLow; } }

        public void Build()
        {
            var s = art = Load(); layout = s.layout;
            flip = Child("Pixel hero facing", transform);
            pose = Child("Pixel hero pose", flip);
            // The weapon hand hangs behind the body and the free hand rests in front of it, as on chuchuking's Zeus.
            hand = Child("Weapon hand", pose);
            weapon = Child("Sword", hand);
            handTurn = Child("Weapon hand grip", hand);
            free = Child("Free hand", pose);
            var list = new List<SpriteRenderer>();
            weaponView = View(weapon, s.sword, 32); list.Add(weaponView);
            handView = View(handTurn, s.weaponHand, 33); list.Add(handView);
            var bodyHost = Child("Body", pose); bodyView = View(bodyHost, s.body[0], 35); list.Add(bodyView);
            list.Add(View(free, s.freeHand, 37));
            views = list.ToArray();
            Guard(0, 0, out shownArm, out shownBlade);
            Pose("Idle", false, 0, 0, 0, 1, Color.white, true, 0, 0, 0, 0, .46f, .43f);
        }

        static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        static SpriteRenderer View(Transform owner, Sprite sprite, int order)
        {
            var view = owner.gameObject.AddComponent<SpriteRenderer>();
            view.sprite = sprite; view.sortingOrder = order;
            return view;
        }

        static Vector3 Px(Vector2 p) { return new Vector3(p.x / PixelsPerUnit, p.y / PixelsPerUnit, 0); }
        static Vector2 Direction(float degrees) { float a = degrees * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
        static float Ease(float t) { return Mathf.SmoothStep(0, 1, Mathf.Clamp01(t)); }
        static float LerpAngle(float from, float to, float t) { return from + Mathf.DeltaAngle(from, to) * t; }
        Vector2 Shoulder() { return new Vector2(layout.shoulder[0], layout.shoulder[1] - sink); }

        // ---------------------------------------------------------------- aim and weapons (TowerActorArt forwards these)

        public void SetAim(Vector2 direction, bool active) { if (direction.sqrMagnitude > .0001f) aimDirection = direction.normalized; aimActive = active; }
        public void SetAimPoint(Vector2 point) { aimPoint = point; hasAimPoint = true; }
        /// <summary>A swing begins: aimed ones cut through <paramref name="direction"/> and leave the opposite guard.</summary>
        public void StartSwing(bool aimed, Vector2 direction, bool rising)
        {
            aimedSwing = aimed; swingRising = aimed && rising; sinceSwing = 0;
            guardLow = aimed && !rising;
            if (direction.sqrMagnitude > .0001f) swingDirection = direction.normalized;
        }
        public void Interrupt() { aimedSwing = false; }
        public void Recoil() { recoil = 1; }
        public void Beckon() { beckon = 1; }
        public void SetWeapon(TowerAttackMode next)
        {
            var s = art;
            mode = next; aimedSwing = false; guardLow = false; recoil = beckon = 0;
            weaponView.sprite = next == TowerAttackMode.Gun ? s.crossbow : s.sword;
            weaponView.enabled = next != TowerAttackMode.Orb;
            weapon.name = next == TowerAttackMode.Gun ? CrossbowName : "Sword";
            // The orb fights on its own; the hand behind it opens toward the aim.
            handView.sprite = next == TowerAttackMode.Orb ? s.freeHand : s.weaponHand;
        }

        /// <summary>Where a bolt fired now toward <paramref name="point"/> leaves the crossbow, with the hero facing <paramref name="side"/>.</summary>
        public Vector2 MuzzleToward(Vector2 point, int side)
        {
            side = side < 0 ? -1 : 1;
            Vector2 shoulder = Shoulder();
            float angle = ShoulderAngle(shoulder, point, side);
            Vector2 grip = shoulder + Direction(angle) * layout.arm;
            Vector2 muzzle = grip + (Vector2)(Quaternion.Euler(0, 0, angle - 90) * new Vector3(layout.muzzle[0], layout.muzzle[1], 0));
            return ArtToWorld(muzzle, side);
        }

        float Unit { get { return Mathf.Abs(transform.lossyScale.y) / PixelsPerUnit; } }
        Vector2 WorldToArt(Vector2 point, int side)
        { Vector3 o = transform.position; float u = Unit; return new Vector2((point.x - o.x) / u * side, (point.y - o.y) / u); }
        Vector2 ArtToWorld(Vector2 local, int side)
        { Vector3 o = transform.position; float u = Unit; return new Vector2(o.x + local.x * u * side, o.y + local.y * u); }

        /// <summary>A world direction as a rig angle on the facing side; aims behind the hero count as ahead.</summary>
        float RigAngle(Vector2 direction) { return Mathf.Clamp(Mathf.Atan2(direction.y, Mathf.Max(direction.x * facing, .05f)) * Mathf.Rad2Deg, -80, 90); }
        /// <summary>The rig angle from the shoulder to a world point; a point behind or right at the shoulder counts as ahead.</summary>
        float ShoulderAngle(Vector2 shoulder, Vector2 point, int side)
        {
            Vector2 to = WorldToArt(point, side) - shoulder;
            float angle = to.sqrMagnitude > 14 ? Mathf.Atan2(to.y, Mathf.Max(to.x, to.magnitude * .05f)) * Mathf.Rad2Deg : RigAngle(aimDirection);
            return Mathf.Clamp(angle, -84, 86);
        }

        // The old bone rig's sword guards and four-beat swing, on chuchuking's arm: the resting guards turned part way
        // toward the aim, high with the blade raised (the idle pose when aiming straight ahead), low forward and down.
        void Guard(float aim, float low, out float arm, out float blade)
        {
            float highArm = -18 + aim * .55f, highBlade = 64 + aim * .6f, lowArm = -62 + aim * .5f, lowBlade = -12 + aim * .65f;
            arm = Mathf.Lerp(highArm, lowArm, low); blade = OffFloor(Mathf.Lerp(highBlade, lowBlade, low), arm);
        }
        // While standing, a blade that would dip through the floor lies along it instead.
        float OffFloor(float blade, float arm)
        {
            if (!grounded) return blade;
            float handHeight = Shoulder().y + layout.arm * Mathf.Sin(arm * Mathf.Deg2Rad);
            float lowest = Mathf.Asin(Mathf.Clamp((-1 - handHeight) / layout.swordReach, -1, 1)) * Mathf.Rad2Deg;
            float behind = Mathf.Repeat(blade + 180, 360) - 180;
            if (behind >= -90) return Mathf.Max(blade, blade - behind + lowest);
            return Mathf.Min(blade, blade - behind - 180 - lowest);
        }
        // Wind up past the starting guard, accelerate through the aim at contact, follow through past it, then settle into
        // the end guard at the live aim. A downward cut winds up overhead; a rising one draws the blade back low behind.
        void Swing(float progress, float contact, float aim, float liveAim, bool rising, float endLow, out float arm, out float blade)
        {
            float side = rising ? -1 : 1, startArm, startBlade, endArm, endBlade;
            Guard(aim, rising ? 1 : 0, out startArm, out startBlade); Guard(liveAim, endLow, out endArm, out endBlade);
            float windArm = rising ? aim - 112 : aim + 80, windBlade = OffFloor(rising ? aim - 160 : aim + 125, windArm);
            float hitArm = aim + side * 6, hitBlade = aim, followArm = aim - side * 72, followBlade = OffFloor(aim - side * 88, followArm);
            if (progress < .26f) { float t = Ease(progress / .26f); arm = Mathf.Lerp(startArm, windArm, t); blade = Mathf.Lerp(startBlade, windBlade, t); }
            else if (progress < contact) { float t = (progress - .26f) / (contact - .26f); t *= t; arm = Mathf.Lerp(windArm, hitArm, t); blade = Mathf.Lerp(windBlade, hitBlade, t); }
            else if (progress < .6f) { float t = (progress - contact) / (.6f - contact); t = 1 - (1 - t) * (1 - t); arm = Mathf.Lerp(hitArm, followArm, t); blade = Mathf.Lerp(hitBlade, followBlade, t); }
            else { float t = Ease((progress - .6f) / .4f); arm = Mathf.Lerp(followArm, endArm, t); blade = Mathf.Lerp(followBlade, endBlade, t); }
        }

        void Stance(bool swinging, float progress, float contact, float dt, out float arm, out float blade)
        {
            if (mode == TowerAttackMode.Sword)
            {
                // A low guard relaxes back to the raised one when the sword has rested a moment.
                if (!swinging) { sinceSwing += dt; if (guardLow && sinceSwing > .9f) guardLow = false; }
                guardBlend = swinging ? (guardLow ? 1 : 0) : Mathf.MoveTowards(guardBlend, guardLow ? 1 : 0, dt * 3.5f);
                float liveAim = aimActive ? RigAngle(aimDirection) : 0;
                if (swinging)
                {
                    // Unaimed swings (keyboard play, companions' fixtures) cut down ahead and return to the raised guard.
                    Swing(progress, contact, aimedSwing ? RigAngle(swingDirection) : 0, liveAim, aimedSwing && swingRising, aimedSwing && !swingRising ? 1 : 0, out arm, out blade);
                    shownArm = arm; shownBlade = blade;
                    return;
                }
                Guard(liveAim, guardBlend, out arm, out blade);
                // Ease toward a guard that follows the mouse so a quick flick of the aim does not snap the arm.
                if (aimActive) { float ease = 1 - Mathf.Exp(-dt * 18); shownArm = LerpAngle(shownArm, arm, ease); shownBlade = LerpAngle(shownBlade, blade, ease); arm = shownArm; blade = shownBlade; }
                else { shownArm = arm; shownBlade = blade; }
                return;
            }
            // The ranged stances hold the arm on the aim point: the crossbow kicks up on each shot, the open hand reaches
            // toward the aim and snaps straight each time the orb lets a bolt go.
            float aim = hasAimPoint ? ShoulderAngle(Shoulder(), aimPoint, facing) : RigAngle(aimDirection);
            if (mode == TowerAttackMode.Gun) { float kick = recoil * recoil; arm = aim + 7 * kick; blade = aim + 18 * kick; }
            else { arm = aim - 9 + 7 * Ease(beckon); blade = arm; }
        }

        public void Pose(string motion, bool moving, float time, float progress, float hurt, int side, Color color,
            bool onGround, float verticalVelocity, float speed, float velocity, float dt, float release, float contact)
        {
            // Mirroring and colour apply while paused; animation clocks stay frozen.
            facing = side < 0 ? -1 : 1;
            flip.localScale = new Vector3(facing, 1, 1);
            if (posed && dt <= 0) { Tint(color); return; }
            posed = true; grounded = onGround;
            recoil = Mathf.MoveTowards(recoil, 0, dt / .12f); beckon = Mathf.MoveTowards(beckon, 0, dt / .3f);

            // Facing the aim while running away from it plays the stride backwards.
            float localSpeed = speed / Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.x));
            bool backpedal = aimActive && motion != "Dash" && velocity * facing < -.5f;
            if (moving && grounded) gait += dt * Mathf.Clamp(localSpeed * 2.2f, 6, 15) * (backpedal ? -.85f : 1);
            bool death = motion == "Death", hurting = !death && (motion == "Hurt" || hurt > 0);
            // chuchuking's idle keys are 2/15 s apart.
            int frame = death || hurting ? TowerGodPixelArt.Hurt
                : !grounded ? (verticalVelocity > .15f ? TowerGodPixelArt.Jump : TowerGodPixelArt.Fall)
                : moving ? TowerGodPixelArt.Run + (Mathf.FloorToInt(gait) & 3)
                : TowerGodPixelArt.Idle + (Mathf.FloorToInt(time * 7.5f) & 3);
            CurrentFrame = frame;
            bodyView.sprite = art.body[frame];
            sink = layout.bob != null && frame < layout.bob.Length ? layout.bob[frame] : 0;

            // The free hand breathes with the body, swings while running and lifts in the air (over an eighth of a second).
            airLift = Mathf.MoveTowards(airLift, grounded ? 0 : 1, dt * 8);
            Vector2 back = new Vector2(layout.back[0], layout.back[1] - sink) + new Vector2(-1, 4) * airLift;
            if (grounded && moving) { float swing = Mathf.Sin(gait * Mathf.PI * .5f); back += new Vector2(-swing * 2.5f, Mathf.Abs(swing) * .5f); }
            else if (grounded) back.y += Mathf.Sin(time * 2.6f + 1.7f) * .6f;

            // The weapon arm: the stance of the weapon in hand, given over to a cast or the fall on death.
            float arm, blade;
            Stance(motion == "Attack" && mode == TowerAttackMode.Sword, progress, contact, dt, out arm, out blade);
            bool casting = motion == "Cast";
            castWeight = Mathf.MoveTowards(castWeight, casting || death ? 1 : 0, dt * 8);
            if (castWeight > 0)
            {
                float windup = progress < release ? Ease(progress / Mathf.Max(.01f, release)) : 1;
                float strike = progress < release ? 0 : Ease((progress - release) / .14f), fall = Ease(progress / .55f);
                // A cast raises the weapon overhead and points it at the enemy on release; on defeat the arm drops.
                float castArm = death ? Mathf.Lerp(arm, -100, fall) : Mathf.Lerp(Mathf.Lerp(-45, 75, windup), 25, strike);
                float castBlade = death ? Mathf.Lerp(blade, -10, fall) : castArm + (mode == TowerAttackMode.Sword ? 15 : 0);
                arm = LerpAngle(arm, castArm, castWeight); blade = LerpAngle(blade, castBlade, castWeight);
                if (casting) back += Vector2.Lerp(new Vector2(2, 2), new Vector2(4, 1), strike) * windup * castWeight;
            }
            float reach = mode == TowerAttackMode.Orb ? .75f + .25f * Ease(beckon) : 1;
            Vector2 grip = Shoulder() + Direction(arm) * layout.arm * reach;

            // Hurt recoil, the lean of a dash (eased in and out over a twelfth of a second) and the fall on death; the boots
            // stay planted on the actor root otherwise.
            Vector2 body = Vector2.zero;
            lean = Mathf.MoveTowards(lean, motion == "Dash" ? -10 : 0, dt * 120);
            float tilt = lean;
            if (hurting && hurt > 0)
            {
                float age = .20f - hurt, kick = Ease(age / .04f) * Ease(hurt / .16f);
                body.x -= 1.5f * kick; grip += new Vector2(-2, 2) * kick; back += new Vector2(-2, 2) * kick; blade += 20 * kick;
            }
            if (death)
            {
                float fall = Ease(progress / .55f);
                tilt = 82 * fall; body.y += 5 * fall; back = Vector2.Lerp(back, new Vector2(-11, 2), fall);
            }

            pose.localPosition = Px(body);
            pose.localRotation = Quaternion.Euler(0, 0, tilt);
            hand.localPosition = Px(grip);
            // chuchuking's arm hangs straight down from the sleeve to the fist.
            handTurn.localRotation = Quaternion.Euler(0, 0, arm + 90);
            // Weapon cells point up from the grip.
            weapon.localRotation = Quaternion.Euler(0, 0, blade - 90);
            free.localPosition = Px(back);
            Tint(color);
        }

        void Tint(Color color) { foreach (var view in views) view.color = color; }
    }
}

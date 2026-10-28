using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>
    /// Chibi pixel gods drawn after chuchuking's Zeus: a breathing body strip, two floating fists
    /// and a held weapon that swings, thrusts, draws, casts or throws by code.
    /// Sheets come from Tools/TowerArt/GodPixels (Resources/Tower/Myths/Pixel).
    /// </summary>
    public sealed class TowerGodPixelArt : MonoBehaviour
    {
        public const float PixelsPerUnit = 20;
        public const int Idle = 0, Run = 4, Jump = 8, Fall = 9, Hurt = 10, BodyFrames = 11;
        const int FrontCell = 11, BackCell = 12, WeaponCell = 13, OffhandCell = 14, CardCell = 15;
        static readonly string[] FrameNames = { "Idle 0", "Idle 1", "Idle 2", "Idle 3", "Run 0", "Run 1", "Run 2", "Run 3", "Jump", "Fall", "Hurt" };

        [Serializable] sealed class LayoutFile { public int version, cell, grip; public Layout[] gods; }
        [Serializable] sealed class Layout
        {
            public string id, kind; public int art;
            public float[] bodyPivot, front, back, extra; public int[] bob, card;
            public float top, rest, reach; public bool offhand, oneHand;
        }
        sealed class Sheet { public Layout layout; public Sprite[] body; public Sprite front, back, weapon, offhand, card; }

        static Dictionary<int, Layout> layouts;
        static int cellSize = 96, gripPixel = 48;
        static readonly Dictionary<int, Sheet> sheets = new Dictionary<int, Sheet>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { layouts = null; sheets.Clear(); }

        static Dictionary<int, Layout> Layouts()
        {
            if (layouts != null) return layouts;
            var asset = Resources.Load<TextAsset>("Tower/Myths/Pixel/Gods");
            if (asset == null) throw new InvalidOperationException("Missing pixel god layout: Tower/Myths/Pixel/Gods");
            var file = JsonUtility.FromJson<LayoutFile>(asset.text);
            cellSize = file.cell; gripPixel = file.grip;
            var result = new Dictionary<int, Layout>();
            foreach (var god in file.gods) result.Add(god.art, god);
            layouts = result;
            return layouts;
        }

        public static bool Contains(int artIndex) { return Layouts().ContainsKey(artIndex); }
        public static string SheetName(int artIndex) { return Layouts()[artIndex].id; }

        static Sheet Load(int artIndex)
        {
            Sheet sheet;
            if (sheets.TryGetValue(artIndex, out sheet) && sheet.body[0] != null && sheet.body[0].texture != null) return sheet;
            Layout layout;
            if (!Layouts().TryGetValue(artIndex, out layout))
                throw new ArgumentOutOfRangeException("artIndex", artIndex, "No pixel god uses this art index.");
            var texture = Resources.Load<Texture2D>("Tower/Myths/Pixel/" + layout.id);
            if (texture == null) throw new InvalidOperationException("Missing pixel god sheet: Tower/Myths/Pixel/" + layout.id);
            if (texture.width < cellSize * (CardCell + 1) || texture.height < cellSize)
                throw new InvalidOperationException("Pixel god sheet is not 16 cells wide: " + layout.id);
            texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
            // Bodies stand on the point between the boots; fists and weapons turn around their grip pixel.
            var feet = new Vector2(layout.bodyPivot[0] / cellSize, 1 - layout.bodyPivot[1] / cellSize);
            var grip = new Vector2((gripPixel + .5f) / cellSize, 1 - (gripPixel + .5f) / cellSize);
            sheet = new Sheet { layout = layout, body = new Sprite[BodyFrames] };
            for (int i = 0; i < BodyFrames; i++) sheet.body[i] = Cut(texture, i, feet, layout.id + " " + FrameNames[i]);
            sheet.front = Cut(texture, FrontCell, grip, layout.id + " front fist");
            sheet.back = Cut(texture, BackCell, grip, layout.id + " back fist");
            sheet.weapon = Cut(texture, WeaponCell, grip, layout.id + " weapon");
            if (layout.offhand) sheet.offhand = Cut(texture, OffhandCell, grip, layout.id + " off-hand");
            var card = new Rect(CardCell * cellSize + layout.card[0], layout.card[1], layout.card[2], layout.card[3]);
            sheet.card = Sprite.Create(texture, card, new Vector2(.5f, 0), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sheet.card.name = layout.id + " card portrait";
            sheets[artIndex] = sheet;
            return sheet;
        }

        static Sprite Cut(Texture2D texture, int cell, Vector2 pivot, string name)
        {
            var sprite = Sprite.Create(texture, new Rect(cell * cellSize, 0, cellSize, cellSize), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        public static void ValidateLibrary()
        {
            foreach (int index in Layouts().Keys) Load(index);
        }

        public static Sprite CardSprite(int artIndex) { return Load(artIndex).card; }
        public static Sprite[] Frames(int artIndex) { return Load(artIndex).body; }

        Sheet sheet;
        Layout layout;
        Transform flip, pose, frontPivot, backPivot, weaponPivot, offhandPivot;
        Transform[] extraPivots;
        SpriteRenderer bodyView, weaponView;
        SpriteRenderer[] views;
        float gait;
        bool posed;

        public int CurrentFrame { get; private set; }
        public string Kind { get { return layout.kind; } }
        public SpriteRenderer Body { get { return bodyView; } }
        public Transform WeaponPivot { get { return weaponPivot; } }
        public float RenderedHeight { get { return layout.top / PixelsPerUnit * Mathf.Abs(transform.lossyScale.y); } }
        /// <summary>World position of the weapon's far end, for effects that leave the blade or staff.</summary>
        public Vector3 WeaponTip { get { return weaponPivot.TransformPoint(Vector3.up * layout.reach / PixelsPerUnit); } }

        public void Build(int artIndex)
        {
            sheet = Load(artIndex); layout = sheet.layout;
            flip = Child("Pixel god facing", transform);
            pose = Child("Pixel god pose", flip);
            var list = new List<SpriteRenderer>();
            extraPivots = new Transform[layout.extra == null ? 0 : layout.extra.Length / 2];
            for (int i = 0; i < extraPivots.Length; i++)
            {
                extraPivots[i] = Child("Extra fist " + i, pose);
                list.Add(View(extraPivots[i], sheet.back, 32));
            }
            backPivot = Child("Back fist", pose);
            if (layout.offhand)
            {
                offhandPivot = Child("Off-hand item", pose);
                list.Add(View(offhandPivot, sheet.offhand, 34));
            }
            if (!layout.oneHand) list.Add(View(backPivot, sheet.back, 33));
            bodyView = View(pose, sheet.body[0], 35); list.Add(bodyView);
            frontPivot = Child("Front fist", pose);
            weaponPivot = Child("Weapon", frontPivot);
            weaponView = View(weaponPivot, sheet.weapon, 36); list.Add(weaponView);
            list.Add(View(frontPivot, sheet.front, 37));
            views = list.ToArray();
            Pose("Idle", false, 0, 0, 0, 1, Color.white, true, 0, 0, 0, .46f);
        }

        static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        SpriteRenderer View(Transform owner, Sprite sprite, int order)
        {
            var host = owner == pose ? new GameObject("Body").transform : owner;
            if (owner == pose) host.SetParent(pose, false);
            var view = host.gameObject.AddComponent<SpriteRenderer>();
            view.sprite = sprite; view.sortingOrder = order;
            return view;
        }

        static Vector3 Px(float x, float y) { return new Vector3(x / PixelsPerUnit, y / PixelsPerUnit, 0); }
        static float Ease(float t) { return Mathf.SmoothStep(0, 1, Mathf.Clamp01(t)); }

        public void Pose(string motion, bool moving, float time, float progress, float hurt, int facing, Color tint,
            bool grounded, float verticalVelocity, float speed, float dt, float release)
        {
            // Mirroring and tint apply while paused; animation clocks stay frozen.
            flip.localScale = new Vector3(facing < 0 ? -1 : 1, 1, 1);
            if (posed && dt <= 0) { Tint(tint, motion, progress, hurt); return; }
            posed = true;
            float localSpeed = speed / Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.x));
            if (moving && grounded) gait += dt * Mathf.Clamp(localSpeed * 2.2f, 6, 15);

            bool death = motion == "Death", hurting = !death && (motion == "Hurt" || hurt > 0);
            int frame = death || hurting ? Hurt
                : !grounded ? (verticalVelocity > .15f ? Jump : Fall)
                : moving ? Run + (Mathf.FloorToInt(gait) & 3)
                : Idle + (Mathf.FloorToInt(time * 5) & 3);
            CurrentFrame = frame;
            bodyView.sprite = sheet.body[frame];
            float sink = layout.bob != null && frame < layout.bob.Length ? layout.bob[frame] : 0;

            // Locomotion: fists breathe with the body, swing while running and lift in the air.
            Vector2 front = new Vector2(layout.front[0], layout.front[1] - sink);
            Vector2 back = new Vector2(layout.back[0], layout.back[1] - sink);
            float angle = layout.rest;
            float lean = 0;
            if (!grounded)
            {
                front += new Vector2(1, 3); back += new Vector2(-1, 4);
                angle += verticalVelocity > 0 ? 8 : -6;
            }
            else if (moving)
            {
                float swing = Mathf.Sin(gait * Mathf.PI * .5f);
                front += new Vector2(swing * 2.5f, Mathf.Abs(swing) * .5f);
                back += new Vector2(-swing * 2.5f, Mathf.Abs(swing) * .5f);
                if (layout.kind == "swing" || layout.kind == "thrust") angle -= 10;
            }
            else
            {
                front.y += Mathf.Sin(time * 2.6f) * .6f;
                back.y += Mathf.Sin(time * 2.6f + 1.7f) * .6f;
            }
            if (motion == "Dash")
            {
                lean = -10; front += new Vector2(-3, 1); back += new Vector2(-4, 2);
                angle += 35;
            }

            // Actions blend in over the first frames and back out after the follow-through, so an
            // attack never snaps away from (or back to) the live locomotion pose.
            Vector2 frontAction = Vector2.zero, backAction = Vector2.zero;
            float angleAction = 0;
            if (motion == "Attack" || motion == "Cast")
            {
                float windup = progress < release ? Ease(progress / Mathf.Max(.01f, release)) : 1;
                float strike = progress < release ? 0 : Ease((progress - release) / .14f);
                float envelope = Ease(progress / .1f) * (1 - Ease((progress - Mathf.Max(.62f, release + .16f)) / Mathf.Max(.05f, 1 - Mathf.Max(.62f, release + .16f))));
                if (motion == "Cast")
                {
                    frontAction = Vector2.Lerp(new Vector2(0, 12), new Vector2(5, 9), strike) * windup;
                    backAction = Vector2.Lerp(new Vector2(3, 11), new Vector2(1, 8), strike) * windup;
                    angleAction = Mathf.Lerp(-layout.rest, -layout.rest - 45, strike) * windup;
                }
                else Strike(windup, strike, ref frontAction, ref backAction, ref angleAction);
                frontAction *= envelope; backAction *= envelope; angleAction *= envelope;
            }
            front += frontAction; back += backAction; angle += angleAction;

            // Hurt recoil and the fall on death.
            // The boots stay planted on the actor root except when the body topples on defeat.
            Vector2 body = Vector2.zero;
            float tilt = lean;
            if (hurting && hurt > 0)
            {
                float age = .20f - hurt;
                float recoil = Ease(age / .04f) * Ease(hurt / .16f);
                body.x -= 1.5f * recoil; front += new Vector2(-2, 2) * recoil; back += new Vector2(-2, 2) * recoil;
                angle += 20 * recoil;
            }
            if (death)
            {
                float fall = Ease(progress / .55f);
                tilt = 82 * fall; body.y += 5 * fall;
                front = Vector2.Lerp(front, new Vector2(6, 2), fall); back = Vector2.Lerp(back, new Vector2(-11, 2), fall);
                angle = Mathf.Lerp(angle, -95, fall);
            }

            // A weapon raised behind the head passes behind the body instead of across the face.
            weaponView.sortingOrder = Mathf.DeltaAngle(layout.rest, angle) > 50 ? 34 : 36;
            pose.localPosition = Px(body.x, body.y);
            pose.localRotation = Quaternion.Euler(0, 0, tilt);
            frontPivot.localPosition = Px(front.x, front.y);
            backPivot.localPosition = Px(back.x, back.y);
            weaponPivot.localRotation = Quaternion.Euler(0, 0, angle);
            if (offhandPivot != null) offhandPivot.localPosition = Px(back.x - 1, back.y);
            for (int i = 0; i < extraPivots.Length; i++)
            {
                float lift = motion == "Cast" ? 4 * Ease(progress / Mathf.Max(.01f, release)) : 0;
                extraPivots[i].localPosition = Px(layout.extra[i * 2], layout.extra[i * 2 + 1] - sink + lift + Mathf.Sin(time * 2.2f + i * 2.1f));
            }
            Tint(tint, motion, progress, hurt);
        }

        void Strike(float windup, float strike, ref Vector2 front, ref Vector2 back, ref float angle)
        {
            switch (layout.kind)
            {
                case "swing":
                    // Raise the blade behind the head, then cut through the target and stop above the floor.
                    angle = Mathf.Lerp(115 * windup, -85, strike);
                    front = Vector2.Lerp(new Vector2(-3, 6) * windup, new Vector2(6, 2), strike);
                    back = new Vector2(-1, 1) * windup;
                    break;
                case "thrust":
                    angle = -90 - layout.rest;
                    front = Vector2.Lerp(new Vector2(-4, 1) * windup, new Vector2(8, 1), strike);
                    back = Vector2.Lerp(new Vector2(4, 0), new Vector2(7, 0), strike) * windup;
                    break;
                case "bow":
                    // The back fist draws the string to the chest and snaps open on release.
                    angle = Mathf.Lerp(0, 6, strike);
                    front = new Vector2(2, 0) * windup;
                    back = Vector2.Lerp(new Vector2(layout.front[0] - layout.back[0] - 5, layout.front[1] - layout.back[1]) * windup, new Vector2(-2, 1), strike);
                    break;
                case "throw":
                    angle = Mathf.Lerp(55 * windup, -70 - layout.rest, strike);
                    front = Vector2.Lerp(new Vector2(-4, 11) * windup, new Vector2(7, 5), strike);
                    back = new Vector2(2, 2) * windup;
                    break;
                default:
                    // Casters raise the focus, then point it at the enemy.
                    angle = Mathf.Lerp(-30 * windup, -70 - layout.rest, strike);
                    front = Vector2.Lerp(new Vector2(1, 7) * windup, new Vector2(5, 4), strike);
                    back = new Vector2(2, 3) * windup;
                    break;
            }
        }

        void Tint(Color tint, string motion, float progress, float hurt)
        {
            Color color = tint;
            if (hurt > .10f) color = Color.Lerp(color, new Color(1, .35f, .32f, color.a), .7f);
            if (motion == "Death") color.a *= 1 - Mathf.Clamp01((progress - .65f) / .35f) * .8f;
            foreach (var view in views) view.color = color;
        }
    }
}

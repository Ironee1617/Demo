using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Roguelike
{
    // A paper-doll model built from the original painted pixels. Each strip belongs
    // to one surface; adjacent armor, cloth and props never share stretched triangles.
    public sealed partial class TowerMythModel : IDisposable
    {
        const int Empty = 255, PartCount = 15;
        const float BodyHeight = 1.75f;
        static readonly int[] surfaceOrder = { 7, 8, 13, 9, 10, 11, 12, 1, 2, 3, 4, 5, 6, 0, 14 };
        static readonly Vector2[] michaelFlame = {
            new Vector2(100,153), new Vector2(111,148), new Vector2(122,143), new Vector2(126,147),
            new Vector2(123,154), new Vector2(132,158), new Vector2(145,160), new Vector2(152,168),
            new Vector2(160,172), new Vector2(168,183), new Vector2(175,186), new Vector2(177,179),
            new Vector2(184,188), new Vector2(184,196), new Vector2(194,204), new Vector2(213,222),
            new Vector2(215,230), new Vector2(201,232), new Vector2(179,224), new Vector2(156,211),
            new Vector2(137,203), new Vector2(120,193), new Vector2(109,185), new Vector2(100,177),
            new Vector2(94,172), new Vector2(98,163)
        };
        static readonly Vector2[] bariBell = {
            new Vector2(113,175), new Vector2(120,175), new Vector2(121,181), new Vector2(124,187),
            new Vector2(127,192), new Vector2(130,197), new Vector2(129,202), new Vector2(125,205),
            new Vector2(109,206), new Vector2(102,203), new Vector2(100,198), new Vector2(104,192),
            new Vector2(108,187), new Vector2(110,181)
        };
        static readonly Vector2[] bariDress = {
            new Vector2(129,167), new Vector2(153,167), new Vector2(168,208), new Vector2(176,241),
            new Vector2(183,258), new Vector2(176,266), new Vector2(166,270), new Vector2(145,267),
            new Vector2(127,270), new Vector2(109,270), new Vector2(95,265), new Vector2(80,265),
            new Vector2(67,261), new Vector2(58,253), new Vector2(62,245), new Vector2(75,232),
            new Vector2(87,218), new Vector2(101,202), new Vector2(111,183)
        };
        static readonly Vector2[] jacheongbiDress = {
            new Vector2(120,159), new Vector2(146,165), new Vector2(149,187), new Vector2(151,211),
            new Vector2(155,232), new Vector2(160,241), new Vector2(165,249), new Vector2(160,254),
            new Vector2(149,260), new Vector2(132,262),
            new Vector2(117,258), new Vector2(102,260), new Vector2(78,259), new Vector2(60,254),
            new Vector2(54,244), new Vector2(60,233), new Vector2(78,216), new Vector2(95,201), new Vector2(105,179)
        };
        static readonly Vector2[] solomonRobe = {
            new Vector2(110,149), new Vector2(134,154), new Vector2(140,181), new Vector2(148,212),
            new Vector2(153,239), new Vector2(144,246), new Vector2(125,245), new Vector2(109,242),
            new Vector2(95,244), new Vector2(76,240), new Vector2(65,236), new Vector2(68,218),
            new Vector2(83,190), new Vector2(97,164)
        };
        static readonly Vector2[] solomonRearFold = {
            new Vector2(150,193), new Vector2(158,203), new Vector2(160,213),
            new Vector2(159,228), new Vector2(153,240), new Vector2(144,240), new Vector2(146,218)
        };
        static readonly Vector2[] rabbitDress = {
            new Vector2(127,145), new Vector2(151,145), new Vector2(155,173), new Vector2(161,199),
            new Vector2(168,226), new Vector2(178,240), new Vector2(168,244), new Vector2(151,244),
            new Vector2(133,245), new Vector2(113,244), new Vector2(99,241), new Vector2(94,235),
            new Vector2(106,213), new Vector2(116,191), new Vector2(122,164)
        };
        static readonly Vector2[] freyaCapeHem = {
            new Vector2(159,217), new Vector2(167,228), new Vector2(175,242),
            new Vector2(185,255), new Vector2(181,260), new Vector2(160,258), new Vector2(153,238)
        };
        static readonly Vector2[] artemisArm = {
            new Vector2(89,120), new Vector2(104,119), new Vector2(109,125), new Vector2(104,133),
            new Vector2(98,140), new Vector2(89,145), new Vector2(80,140), new Vector2(83,129)
        };
        static readonly Vector2[] artemisForearm = {
            new Vector2(82,138), new Vector2(94,139), new Vector2(96,149), new Vector2(92,162),
            new Vector2(91,167), new Vector2(92,176), new Vector2(89,184), new Vector2(84,187),
            new Vector2(80,184), new Vector2(77,178), new Vector2(78,170), new Vector2(79,159), new Vector2(79,148)
        };
        static readonly Vector2[] morriganBlade = {
            new Vector2(81,198), new Vector2(92,208), new Vector2(105,217), new Vector2(117,229),
            new Vector2(133,237), new Vector2(146,243), new Vector2(158,243), new Vector2(160,247),
            new Vector2(150,250), new Vector2(136,250), new Vector2(121,245), new Vector2(107,239),
            new Vector2(95,231), new Vector2(83,221), new Vector2(74,213), new Vector2(70,207)
        };
        static readonly Vector2[] jumongArm = {
            new Vector2(85,111), new Vector2(99,111), new Vector2(110,119), new Vector2(107,132),
            new Vector2(101,145), new Vector2(91,150), new Vector2(77,146), new Vector2(73,139), new Vector2(77,126)
        };
        static readonly Vector2[] jumongForearm = {
            new Vector2(77,148), new Vector2(91,149), new Vector2(95,163), new Vector2(92,177),
            new Vector2(97,182), new Vector2(97,189), new Vector2(90,194), new Vector2(81,192),
            new Vector2(79,184), new Vector2(75,177), new Vector2(75,163)
        };
        static readonly Dictionary<Texture2D, Color32[]> pixels = new Dictionary<Texture2D, Color32[]>();
        static readonly Dictionary<Texture2D, Material> materials = new Dictionary<Texture2D, Material>();
        readonly TowerMythRig.Profile profile;
        readonly float unit;
        readonly float leftLegHem, rightLegHem;
        readonly int width, height, mapWidth, sourceX, sourceY;
        readonly Texture2D texture;
        readonly Color32[] source;
        readonly byte[] ownership;
        readonly Vector2 hip, shoulder, head, leftHand, rightHand, leftFoot, rightFoot, leftKnee, rightKnee, leftAnkle, rightAnkle;
        readonly Vector2 leftHip, rightHip, leftShoulder, rightShoulder, leftElbow, rightElbow;
        readonly Mesh mesh;
        readonly MeshRenderer renderer;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly Vector3[] rest, posed;
        struct SkinBinding
        {
            public int first, second, third;
            public float secondWeight, thirdWeight, bodyWeight, chestWeight;
        }
        readonly SkinBinding[] bindings;
        readonly int[] parts;
        readonly bool[] underpaint;
        readonly Matrix4x4[] current = new Matrix4x4[PartCount];
        readonly List<Vector3> buildVertices = new List<Vector3>();
        readonly List<Vector2> buildUvs = new List<Vector2>(), buildModes = new List<Vector2>();
        readonly List<Color32> buildColors = new List<Color32>();
        readonly List<int> buildParts = new List<int>(), triangles = new List<int>();
        readonly List<bool> buildUnderpaint = new List<bool>();
        struct PaintPatch { public int x, y, sx, sy, part; }
        readonly List<PaintPatch> patches = new List<PaintPatch>();

        public float DeltaTime { get; set; }
        public Renderer Renderer { get { return renderer; } }
        public float RenderedHeight { get { return mesh.bounds.max.y; } }
        public int PartCountPresent { get; private set; }

        public TowerMythModel(Transform parent, Sprite idle, TowerMythRig.Profile anatomy)
        {
            profile = anatomy; texture = idle.texture; unit = BodyHeight / anatomy.bodyHeight;
            sourceX = Mathf.RoundToInt(idle.rect.x); sourceY = Mathf.RoundToInt(idle.rect.y);
            width = Mathf.RoundToInt(idle.rect.width); height = Mathf.RoundToInt(idle.rect.height);
            mapWidth = width + ExtraWidth(anatomy.artIndex);
            source = ReadPixels(texture);
            var template = FindTemplate();
            ownership = template == null ? new byte[mapWidth * height] : template.ownership;
            hip = Point(anatomy.hipX, anatomy.hipY); shoulder = Point(anatomy.shoulderX, anatomy.shoulderY);
            head = Point(anatomy.headX, anatomy.headY);
            leftHand = Point(anatomy.leftHandX, anatomy.leftHandY); rightHand = Point(anatomy.rightHandX, anatomy.rightHandY);
            leftFoot = Point(anatomy.leftFootX, anatomy.leftFootY); rightFoot = Point(anatomy.rightFootX, anatomy.rightFootY);
            leftKnee = Point(anatomy.leftKneeX, anatomy.leftKneeY); rightKnee = Point(anatomy.rightKneeX, anatomy.rightKneeY);
            leftAnkle = anatomy.leftAnkleY > 0 ? Point(anatomy.leftAnkleX, anatomy.leftAnkleY) : leftFoot + Vector2.up * .10f;
            rightAnkle = anatomy.rightAnkleY > 0 ? Point(anatomy.rightAnkleX, anatomy.rightAnkleY) : rightFoot + Vector2.up * .10f;
            leftLegHem = LegHem(anatomy.leftLegOutline); rightLegHem = LegHem(anatomy.rightLegOutline);
            float spread = Mathf.Clamp((rightFoot.x - leftFoot.x) * .22f, .045f, .105f);
            leftHip = hip + Vector2.left * spread; rightHip = hip + Vector2.right * spread;
            leftShoulder = anatomy.leftShoulderY > 0 ? Point(anatomy.leftShoulderX, anatomy.leftShoulderY) : shoulder + Vector2.left * .085f;
            rightShoulder = anatomy.rightShoulderY > 0 ? Point(anatomy.rightShoulderX, anatomy.rightShoulderY) : shoulder + Vector2.right * .075f;
            leftElbow = anatomy.leftElbowY > 0 ? Point(anatomy.leftElbowX, anatomy.leftElbowY) : Vector2.Lerp(leftShoulder, leftHand, .52f) + Vector2.left * .025f;
            rightElbow = anatomy.rightElbowY > 0 ? Point(anatomy.rightElbowX, anatomy.rightElbowY) : Vector2.Lerp(rightShoulder, rightHand, .52f) + Vector2.right * .025f;
            if (template == null)
            {
                ClassifyPixels(); ClaimExteriorRims(2, 2); ClaimExteriorRims(3, 6);
                ClaimExteriorRims(7, 12); ClaimExteriorRims(14, 14); KeepConnectedWeapon();
                // Adjacent cuts overlap with their own painted texture, like
                // layered animation cels, including hidden costume continuations.
                AddHiddenLegs(); AddJointOverlaps(); AddTorsoRepair(); AddArmBackgrounds(); AddWeaponBackgrounds(); AddCrossedLegBackground(); AddOccludedSurfaces(); FlushPaintPatches();
                bool measuredArms = anatomy.leftArmOutline != null && anatomy.rightArmOutline != null;
                int[] order = measuredArms ? new[] { 13, 7, 8, 9, 10, 11, 12, 0, 1, 3, 4, 5, 6, 2, 14 } : anatomy.weaponHand == 1 ?
                    new[] { 13, 7, 8, 9, 10, 11, 12, 5, 6, 0, 1, 3, 4, 2, 14 } :
                    anatomy.artIndex == 108 ? new[] { 13, 7, 8, 9, 10, 11, 12, 3, 0, 1, 4, 5, 6, 2, 14 } :
                    new[] { 13, 7, 8, 9, 10, 11, 12, 3, 4, 0, 1, 5, 6, 2, 14 };
                foreach (int part in order) AddStrips(part);
                CompactPaintedVertices();
                rest = buildVertices.ToArray(); parts = buildParts.ToArray();
                underpaint = buildUnderpaint.ToArray(); bindings = new SkinBinding[rest.Length];
                for (int index = 0; index < rest.Length; index++) bindings[index] = Bind(parts[index], rest[index]);
                template = StoreTemplate();
            }
            else
            {
                rest = template.rest; parts = template.parts; underpaint = template.underpaint;
                bindings = template.bindings; PartCountPresent = template.partCount;
            }
            posed = (Vector3[])rest.Clone();
            var go = new GameObject("Layered myth model"); go.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Layered myth pixels " + anatomy.artIndex };
            if (rest.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.MarkDynamic(); mesh.vertices = rest; mesh.uv = template.uvs; mesh.uv2 = template.modes;
            mesh.colors32 = template.colors; mesh.triangles = template.triangles; mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh; renderer = go.AddComponent<MeshRenderer>(); renderer.sortingOrder = 35;
            Material material;
            if (!materials.TryGetValue(texture, out material) || material == null)
            {
                var shader = Resources.Load<Shader>("Tower/MythModel");
                if (shader == null) throw new InvalidOperationException("Missing Tower/MythModel shader.");
                material = new Material(shader) { name = "Myth layered pixel material " + texture.name, mainTexture = texture };
                materials[texture] = material;
            }
            renderer.sharedMaterial = material;
            for (int i = 0; i < PartCount; i++) current[i] = Matrix4x4.identity;
        }

        static Color32[] ReadPixels(Texture2D texture)
        {
            Color32[] result; if (pixels.TryGetValue(texture, out result)) return result;
            var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, target); RenderTexture.active = target;
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0, false); copy.Apply(false, false);
                result = copy.GetPixels32(); pixels.Add(texture, result);
            }
            finally
            {
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(copy);
                else
#endif
                    UnityEngine.Object.Destroy(copy);
            }
            return result;
        }

        Vector2 Point(float x, float y) { return new Vector2(x - profile.groundX, y - profile.groundY) * unit; }
        Vector2[] FrontGarment
        {
            get
            {
                switch (profile.artIndex)
                {
                    case 133: return bariDress;
                    case 134: return jacheongbiDress;
                    case 138: return solomonRobe;
                    case 148: return rabbitDress;
                    default: return null;
                }
            }
        }
        float LegHem(Vector2[] outline)
        {
            float hem = profile.legHemY;
            if (outline != null && outline.Length > 0)
            {
                float highest = 0;
                foreach (Vector2 point in outline) highest = Mathf.Max(highest, height - point.y);
                hem = Mathf.Min(hem, highest);
            }
            return Point(0, hem).y;
        }
        Color32 Pixel(int x, int y)
        {
            int sx = sourceX + x, sy = sourceY + y;
            return sx < 0 || sx >= texture.width || sy < 0 || sy >= texture.height ? new Color32() : source[sy * texture.width + sx];
        }
        void ClassifyPixels()
        {
            int min = profile.sourceMaxX > profile.sourceMinX ? Mathf.RoundToInt(profile.sourceMinX) : 0;
            int max = profile.sourceMaxX > profile.sourceMinX ? Mathf.RoundToInt(profile.sourceMaxX) : width;
            for (int y = 0; y < height; y++) for (int x = 0; x < mapWidth; x++)
            {
                int at = y * mapWidth + x; ownership[at] = Empty;
                if (x >= width)
                {
                    if (IsRecoveredPixel(x - width, height - 1 - y) && Pixel(x, y).a > 8) ownership[at] = 14;
                    continue;
                }
                if (x < min || x >= max || Pixel(x, y).a <= 8) continue;
                ownership[at] = (byte)Classify(Point(x + .5f, y + .5f), true);
            }
        }
        int Classify(Vector2 p, bool allowWeapon)
        {
            int sole = MeasuredSole(p); if (sole >= 0) return sole;
            Vector2 sourcePoint = SourceTopPoint(p);
            // Measured silhouettes take precedence at occlusions. A fist painted
            // over a shaft stays on the forearm, and a bent arm never borrows a
            // rectangular piece of the chest merely because it is near a line.
            if (InPolygonMargin(sourcePoint, profile.headOutline, 1.5f)) return 2;
            if (InPolygonMargin(sourcePoint, profile.leftForearmOutline, 1)) return 4;
            if (InPolygonMargin(sourcePoint, profile.rightForearmOutline, 1)) return 6;
            if (InPolygonMargin(sourcePoint, profile.leftArmOutline, 1)) return 3;
            if (InPolygonMargin(sourcePoint, profile.rightArmOutline, 1)) return 5;
            Vector2 d = p - head;
            float rx = profile.headRadiusX * unit, ry = profile.headRadiusY * unit;
            float fx = Mathf.Min(rx, .18f), fy = Mathf.Min(ry, .22f);
            if (profile.headOutline == null && Ellipse(d, fx, fy) < 1.15f) return 2;
            if (allowWeapon && IsWeapon(p)) return 14;
            if (profile.headOutline == null && Ellipse(d, rx, ry) < 1.15f) return 2;
            if (profile.artIndex == 109)
            {
                Vector2 top = SourceTopPoint(p);
                if (InPolygon(top, artemisForearm)) return 4;
                if (InPolygon(top, artemisArm)) return 3;
            }
            if (profile.artIndex == 131)
            {
                Vector2 top = SourceTopPoint(p);
                if (InPolygon(top, jumongForearm)) return 4;
                if (InPolygon(top, jumongArm)) return 3;
            }
            if (profile.artIndex == 108 && Ellipse(p - TopPoint(79, 173), 40 * unit, 47 * unit) < 1) return 4;
            if (IsSecondaryProp(p)) return 4;
            Vector2[] garment = FrontGarment;
            if (garment != null && InPolygon(sourcePoint, garment)) return 0;
            if (profile.leftForearmOutline == null && Vector2.Distance(p, leftHand) < .062f) return 4;
            if (profile.rightForearmOutline == null && Vector2.Distance(p, rightHand) < .062f) return 6;
            float hem = Point(0, profile.legHemY).y;
            if (profile.artIndex == 126 && p.y < hem + .07f)
                return Mathf.Abs(p.x - leftFoot.x) < Mathf.Abs(p.x - rightFoot.x) ? 9 : 12;
            bool measuredLegs = profile.leftLegOutline != null || profile.rightLegOutline != null;
            if (measuredLegs)
            {
                // Crossed legs cannot be divided by the nearest shoe. Preserve
                // the measured front-leg overlap and leave surrounding cloth out.
                bool left = InPolygonMargin(sourcePoint, profile.leftLegOutline, .6f);
                if (left || InPolygonMargin(sourcePoint, profile.rightLegOutline, .6f))
                {
                    Vector2 foot = left ? leftFoot : rightFoot, knee = left ? leftKnee : rightKnee;
                    if (p.y < foot.y + .12f) return left ? 9 : 12;
                    return p.y < knee.y ? left ? 8 : 11 : left ? 7 : 10;
                }
            }
            if (!measuredLegs && p.y < hem + .01f)
            {
                bool left = Vector2.Distance(p, leftFoot) < Vector2.Distance(p, rightFoot);
                Vector2 f = left ? leftFoot : rightFoot, k = left ? leftKnee : rightKnee, h = left ? leftHip : rightHip;
                float distance = Mathf.Min(SegmentDistance(p, h, k), SegmentDistance(p, k, f));
                if (distance < .165f && p.x > leftFoot.x - .16f && p.x < rightFoot.x + .16f)
                {
                    if (p.y < f.y + .12f) return left ? 9 : 12;
                    return p.y < k.y ? left ? 8 : 11 : left ? 7 : 10;
                }
            }
            // These front hems curve around independent ribbons and cape loops.
            // A rectangular pelvis cut leaves a straight tear through the same
            // painted cloth and assigns part of the rear loop to the front body.
            // Keep wings and broad capes out of arm corridors. Only pixels close
            // to an actual shoulder-to-hand chain acquire an arm owner.
            bool onLeft = p.x < shoulder.x;
            Vector2 s = onLeft ? leftShoulder : rightShoulder, hand = onLeft ? leftHand : rightHand;
            Vector2 elbow = onLeft ? leftElbow : rightElbow;
            float armDistance = Mathf.Min(SegmentDistance(p, s, elbow), SegmentDistance(p, elbow, hand));
            float alongArm = Vector2.Dot(p - s, hand - s) / Mathf.Max(.000001f, (hand - s).sqrMagnitude);
            float torsoCenter = Mathf.Lerp(hip.x, shoulder.x, Mathf.InverseLerp(hip.y, shoulder.y, p.y));
            bool bowDrawArm = (profile.artIndex == 109 || profile.artIndex == 131) && onLeft && alongArm > .10f;
            bool measuredArm = onLeft ? profile.leftArmOutline != null : profile.rightArmOutline != null;
            if (!measuredArm && alongArm < 1.10f && p.y > Mathf.Min(hip.y - .09f, hand.y - .07f) && p.y < shoulder.y + .06f &&
                (bowDrawArm || Mathf.Abs(p.x - torsoCenter) > .14f) && armDistance < .125f)
                return Vector2.Dot(p - s, hand - s) < Vector2.Dot(elbow - s, hand - s) ? onLeft ? 3 : 5 : onLeft ? 4 : 6;
            if (p.y < hip.y + .035f && garment != null) return 13;
            if (p.y < hip.y + .035f && Mathf.Abs(p.x - hip.x) < .30f) return 0;
            if (p.y < shoulder.y + .06f && p.y > hip.y - .04f && Mathf.Abs(p.x - torsoCenter) < .22f) return 1;
            return 13;
        }
        void ClaimExteriorRims(int firstPart, int lastPart)
        {
            // Measured silhouettes may stop just inside a dark or gold outline.
            // Move only a thin exterior rim with its neighboring painted part.
            // Only unassigned cloth can change: preserve the torso, face, other
            // measured limbs/props, and any cloth away from source transparency.
            // Read the original map throughout so this cannot recursively grow.
            var original = (byte[])ownership.Clone();
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                if (original[y * mapWidth + x] != 13) continue;
                int part = -1, nearest = 10;
                for (int dy = -3; dy <= 3; dy++) for (int dx = -3; dx <= 3; dx++)
                {
                    int distance = dx * dx + dy * dy, sx = x + dx, sy = y + dy;
                    if (distance >= nearest || sx < 0 || sx >= width || sy < 0 || sy >= height) continue;
                    int candidate = original[sy * mapWidth + sx];
                    if (candidate < firstPart || candidate > lastPart) continue;
                    part = candidate; nearest = distance;
                }
                if (part < 0) continue;
                bool edge = false;
                for (int dy = -4; !edge && dy <= 4; dy++) for (int dx = -4; !edge && dx <= 4; dx++)
                {
                    if (dx * dx + dy * dy > 16) continue;
                    int sx = x + dx, sy = y + dy;
                    edge = sx < 0 || sx >= width || sy < 0 || sy >= height || original[sy * mapWidth + sx] == Empty;
                }
                if (edge) ownership[y * mapWidth + x] = (byte)part;
            }
        }
        Vector2 TopPoint(float x, float y) { return Point(x, height - y); }
        bool Capsule(Vector2 p, float ax, float ay, float bx, float by, float fullWidth)
        { return SegmentDistance(p, TopPoint(ax, ay), TopPoint(bx, by)) < fullWidth * unit * .5f; }
        bool IsSecondaryProp(Vector2 p)
        {
            if (profile.secondaryWeaponOutline != null) return InPolygonMargin(SourceTopPoint(p), profile.secondaryWeaponOutline, 2.5f);
            switch (profile.artIndex)
            {
                case 112: return Capsule(p, 97, 195, 123, 239, 20);
                case 145: return Capsule(p, 96, 174, 65, 233, 22);
                case 133: return InPolygon(SourceTopPoint(p), bariBell);
                case 134: return Capsule(p, 82, 178, 55, 188, 16) || Capsule(p, 55, 188, 34, 177, 18) || Capsule(p, 34, 177, 27, 161, 12);
                default: return false;
            }
        }
        bool IsWeapon(Vector2 p)
        {
            // The bowstring passes behind Artemis's dress. Keep its two visible
            // segments separate from the curved bow instead of enclosing the
            // green cloth inside one large weapon polygon.
            if (profile.artIndex == 109 &&
                (Capsule(p, 183.5f, 88, 150.5f, 185.5f, 4.4f) ||
                 Capsule(p, 150.5f, 219, 154.5f, 227.5f, 4.4f))) return true;
            if (profile.weaponOutline != null) return InPolygonMargin(SourceTopPoint(p), profile.weaponOutline, 2.5f);
            if (profile.artIndex == 100)
            {
                Vector2 top = SourceTopPoint(p);
                return InPolygon(top, michaelFlame) || top.x >= 175 && top.x <= 219 && top.y >= 176 && top.y <= 235;
            }
            Vector2 start = Point(profile.weaponBaseX, profile.weaponBaseY), tip = Point(profile.weaponTipX, profile.weaponTipY);
            if (profile.artIndex == 143 && InPolygon(SourceTopPoint(p), morriganBlade)) return true;
            if (profile.artIndex == 102) start = TopPoint(143, 228);
            if (profile.artIndex == 132) start = TopPoint(158, 256);
            if (profile.artIndex == 108) start = Point(142, 13);
            if (profile.artIndex == 127 && Ellipse(p - Point(225, 166), 47 * unit, 63 * unit) < 1) return true;
            Vector2 delta = p - tip;
            if (profile.weaponTipRadiusX > 0 && profile.weaponTipRadiusY > 0 &&
                Ellipse(delta, profile.weaponTipRadiusX * unit, profile.weaponTipRadiusY * unit) < 1) return true;
            float radius = profile.weaponWidth * unit * .5f;
            // At a body crossing, the visible shaft is narrower than a flared cap.
            if (p.y < shoulder.y - .08f && p.y > hip.y - .10f && Mathf.Abs(p.x - hip.x) < .22f) radius = Mathf.Min(radius, .055f);
            return SegmentDistance(p, start, tip) < radius;
        }
        void KeepConnectedWeapon()
        {
            // These measured multipart weapons have a hand occlusion or separated
            // prongs. Connectivity cannot decide which visible shaft is genuine.
            if (profile.weaponOutline != null || profile.artIndex == 108 || profile.artIndex == 127 || profile.artIndex == 143) return;
            var visited = new bool[ownership.Length]; var groups = new List<List<int>>();
            int largest = -1;
            for (int at = 0; at < ownership.Length; at++)
            {
                if (visited[at] || ownership[at] != 14) continue;
                var group = new List<int>(); var queue = new Queue<int>(); queue.Enqueue(at); visited[at] = true;
                while (queue.Count > 0)
                {
                    int item = queue.Dequeue(); group.Add(item); int x = item % mapWidth, y = item / mapWidth;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= mapWidth || ny < 0 || ny >= height) continue;
                        int next = ny * mapWidth + nx;
                        if (visited[next] || ownership[next] != 14) continue;
                        visited[next] = true; queue.Enqueue(next);
                    }
                }
                groups.Add(group); if (largest < 0 || group.Count > groups[largest].Count) largest = groups.Count - 1;
            }
            if (largest < 0) return;
            Vector2 grip = profile.weaponHand == 1 ? leftHand : rightHand;
            for (int index = 0; index < groups.Count; index++)
            {
                if (index == largest) continue;
                var group = groups[index]; Vector2 center = Vector2.zero;
                bool recovered = false;
                foreach (int item in group) { int x = item % mapWidth, y = item / mapWidth; center += Point(x, y); recovered |= x >= width; }
                center /= group.Count;
                // A second shaft section or a detached divine spark belongs to the
                // prop; a broad disconnected patch of hair does not.
                if (recovered || group.Count < 18 || Vector2.Distance(center, grip) < .14f) continue;
                foreach (int item in group) ownership[item] = (byte)Classify(Point(item % mapWidth + .5f, item / mapWidth + .5f), false);
            }
        }

        void AddStrips(int part)
        {
            bool present = false;
            for (int y = 0; y < height; y++)
            {
                int x = 0;
                while (x < mapWidth)
                {
                    if (ownership[y * mapWidth + x] != part) { x++; continue; }
                    int first = x; while (x < mapWidth && ownership[y * mapWidth + x] == part) x++;
                    AddQuad(Point(first, y), Point(x, y + 1),
                        new Vector2((sourceX + first) / (float)texture.width, (sourceY + y) / (float)texture.height),
                        new Vector2((sourceX + x) / (float)texture.width, (sourceY + y + 1) / (float)texture.height),
                        Shade(part), part, false);
                    present = true;
                }
            }
            if (present) PartCountPresent++;
        }
        Color32 Shade(int part)
        {
            float shade = part == 13 ? .91f : part == 3 || part == 4 || part == 7 || part == 8 || part == 9 ? .94f : 1;
            byte value = (byte)(255 * shade); return new Color32(value, value, value, 255);
        }
        void AddJointOverlaps()
        {
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int owner = ownership[y * mapWidth + x];
                if (owner == Empty || owner == 2 || owner == 13 || owner == 14) continue;
                Vector2 p = Point(x + .5f, y + .5f);
                // Waist and shoulder cloth continues behind the next painted cel.
                if (owner == 0 && p.y > hip.y - .13f && Mathf.Abs(p.x - hip.x) < .28f) AddTexturedPixel(x, y, x, y, 1);
                if (owner == 1 && p.y < hip.y + .14f) AddTexturedPixel(x, y, x, y, 0);
                bool measuredLeft = profile.leftArmOutline != null, measuredRight = profile.rightArmOutline != null;
                if ((owner == 3 && (measuredLeft ? Vector2.Distance(p, leftShoulder) < .065f : p.y > shoulder.y - .13f && p.y < shoulder.y + .08f)) ||
                    (owner == 5 && (measuredRight ? Vector2.Distance(p, rightShoulder) < .065f : p.y > shoulder.y - .13f && p.y < shoulder.y + .08f)))
                    AddTexturedPixel(x, y, x, y, 1);
                if (owner == 1 && Vector2.Distance(p, leftShoulder) < (measuredLeft ? .065f : .13f)) AddTexturedPixel(x, y, x, y, 3);
                if (owner == 1 && Vector2.Distance(p, rightShoulder) < (measuredRight ? .065f : .13f)) AddTexturedPixel(x, y, x, y, 5);
                if (owner == 3 && Vector2.Distance(p, leftElbow) < .13f) AddTexturedPixel(x, y, x, y, 4);
                if (owner == 4 && Vector2.Distance(p, leftElbow) < .13f) AddTexturedPixel(x, y, x, y, 3);
                if (owner == 5 && Vector2.Distance(p, rightElbow) < .13f) AddTexturedPixel(x, y, x, y, 6);
                if (owner == 6 && Vector2.Distance(p, rightElbow) < .13f) AddTexturedPixel(x, y, x, y, 5);
                if (owner == 7 && Vector2.Distance(p, leftKnee) < .14f) AddTexturedPixel(x, y, x, y, 8);
                if (owner == 8 && Vector2.Distance(p, leftKnee) < .14f) AddTexturedPixel(x, y, x, y, 7);
                if (owner == 10 && Vector2.Distance(p, rightKnee) < .14f) AddTexturedPixel(x, y, x, y, 11);
                if (owner == 11 && Vector2.Distance(p, rightKnee) < .14f) AddTexturedPixel(x, y, x, y, 10);
                // Overlap the ankle cuff only. Duplicating toes or sole pixels on
                // the shin leaves a second shoe below the rigid foot when bent.
                if (p.y >= leftAnkle.y - .035f && Vector2.Distance(p, leftAnkle) < .065f)
                {
                    if (owner == 8) AddTexturedPixel(x, y, x, y, 9);
                    if (owner == 9) AddTexturedPixel(x, y, x, y, 8);
                }
                if (p.y >= rightAnkle.y - .035f && Vector2.Distance(p, rightAnkle) < .065f)
                {
                    if (owner == 11) AddTexturedPixel(x, y, x, y, 12);
                    if (owner == 12) AddTexturedPixel(x, y, x, y, 11);
                }
            }
        }
        void AddTorsoRepair()
        {
            // Behind a removed arm/prop, continue a nearby *textured* body patch.
            // Sampling a 2D patch preserves the source folds and armor pattern;
            // a single row color creates a conspicuous flat band during a swing.
            int shift = Mathf.Max(1, Mathf.RoundToInt(.10f / unit));
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int owner = ownership[y * mapWidth + x]; Vector2 p = Point(x + .5f, y + .5f);
                float center = Mathf.Lerp(hip.x, shoulder.x, Mathf.InverseLerp(hip.y, shoulder.y, p.y));
                if (owner == Empty || owner == 0 || owner == 1 || owner == 2 ||
                    p.y < hip.y - .10f || p.y > shoulder.y - .055f || Mathf.Abs(p.x - center) > .18f) continue;
                int sx = Mathf.Clamp(x + (p.x < center ? shift : -shift), 0, width - 1), sy = y;
                if (!IsBodyPixel(sx, sy))
                {
                    int best = int.MaxValue;
                    for (int dy = -shift; dy <= shift; dy++) for (int dx = -2 * shift; dx <= 2 * shift; dx++)
                    {
                        int nx = x + dx, ny = y + dy, distance = dx * dx + 2 * dy * dy;
                        if (distance >= best || !IsBodyPixel(nx, ny)) continue;
                        sx = nx; sy = ny; best = distance;
                    }
                    if (best == int.MaxValue) continue;
                }
                AddTexturedPixel(x, y, sx, sy, p.y < hip.y + .02f ? 0 : 1);
            }
        }
        bool IsBodyPixel(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return false;
            int owner = ownership[y * mapWidth + x]; return (owner == 0 || owner == 1) && Pixel(x, y).a > 220;
        }
        void AddArmBackgrounds()
        {
            // The source is a flattened painting: cape/chest pixels hidden by an
            // arm do not exist. Continue only rows enclosed by visible costume on
            // both sides, so moving an arm cannot punch its old silhouette out of
            // the cape. The outside silhouette is never expanded.
            int reach = Mathf.CeilToInt(.40f / unit);
            for (int side = 0; side < 2; side++)
            {
                if ((side == 0 ? profile.leftArmOutline : profile.rightArmOutline) == null) continue;
                var covered = new List<Vector2Int>(); int cloth = 0, body = 0;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    int owner = ownership[y * mapWidth + x];
                    if (owner < 3 + side * 2 || owner > 4 + side * 2) continue;
                    // The hanging sickle chain crosses genuinely open ribbon
                    // loops. Its footprint is not a sleeve-shaped garment hole.
                    if (profile.artIndex == 134 && height - y > 195 &&
                        !InPolygon(new Vector2(x + .5f, height - y - .5f), jacheongbiDress)) continue;
                    if (profile.artIndex == 133 && side == 0 && height - y > 160 &&
                        !InPolygon(new Vector2(x + .5f, height - y - .5f), bariDress)) continue;
                    int leftStep = BackdropDistance(x, y, -1, 0, reach, true);
                    int rightStep = BackdropDistance(x, y, 1, 0, reach, true);
                    if (leftStep < 0 || rightStep < 0) continue;
                    int left = x - leftStep, right = x + rightStep;
                    covered.Add(new Vector2Int(x, y));
                    if (ownership[y * mapWidth + left] == 13 || ownership[y * mapWidth + right] == 13) cloth++; else body++;
                }
                if (covered.Count == 0) continue;
                // One connected hidden surface uses one owner and one translated
                // texture patch. Selecting a different donor and body/cape owner
                // on each row creates conspicuous horizontal ladder stripes.
                int background = cloth >= body / 3 ? 13 : 1;
                Vector2 measured = side == 0 ? profile.leftArmBackgroundOffset : profile.rightArmBackgroundOffset;
                AddTranslatedBackground(covered, background, reach, new Vector2Int(Mathf.RoundToInt(measured.x), -Mathf.RoundToInt(measured.y)));
            }
        }
        void AddTranslatedBackground(List<Vector2Int> covered, int background, int reach, Vector2Int measuredOffset = default(Vector2Int))
        {
            int bestX = 0, bestY = 0; float best = float.MinValue;
            int sampleStep = Mathf.Max(1, covered.Count / 96);
            var samples = new List<Vector2Int>();
            var colors = new List<Color32>();
            if (measuredOffset == Vector2Int.zero)
            {
                for (int i = 0; i < covered.Count; i += sampleStep)
                {
                    Color32 reference;
                    if (!NearbySurfaceColor(covered[i], background, reach, out reference)) continue;
                    samples.Add(covered[i]); colors.Add(reference);
                }
            }
            // Hair, wings, trim and cloth can all be owned by the cape bone.
            // Choose one coherent patch that matches the neighboring visible
            // surface, rather than maximizing ownership coverage alone.
            for (int dy = -reach; measuredOffset == Vector2Int.zero && dy <= reach; dy += 4) for (int dx = -reach; dx <= reach; dx += 4)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) < 4) continue;
                float score = -(.02f * Mathf.Abs(dx) + .05f * Mathf.Abs(dy));
                for (int i = 0; i < samples.Count; i++)
                {
                    Vector2Int p = samples[i];
                    // Missing pixels must count against an offset. Otherwise a
                    // tiny matching strip wins and every remaining target pixel
                    // later collapses onto that strip during fallback.
                    if (!IsTextureDonor(p.x + dx, p.y + dy, background)) { score -= 2; continue; }
                    Color32 color = Pixel(p.x + dx, p.y + dy), reference = colors[i];
                    float difference = (Mathf.Abs(color.r - reference.r) + Mathf.Abs(color.g - reference.g) +
                        Mathf.Abs(color.b - reference.b)) / 765f;
                    score += 1 - 3 * difference;
                }
                if (score > best) { best = score; bestX = dx; bestY = dy; }
            }
            if (measuredOffset != Vector2Int.zero) { bestX = measuredOffset.x; bestY = measuredOffset.y; }
            foreach (Vector2Int p in covered)
            {
                int sx = p.x + bestX, sy = p.y + bestY;
                // A measured 2D garment patch can cross the torso/cape ownership
                // boundary while remaining the same painted cloth. Preserve its
                // mapping there instead of repeatedly sampling one boundary row.
                bool continuousCostume = measuredOffset != Vector2Int.zero &&
                    (background == 0 || background == 1 || background == 13) &&
                    IsCostumePixel(sx - 1, sy) && IsCostumePixel(sx, sy) && IsCostumePixel(sx + 1, sy);
                if (!continuousCostume && !IsTextureDonor(sx, sy, background))
                {
                    int distance = int.MaxValue, foundX = -1, foundY = -1;
                    // Repair at most a small texture edge. A reach-wide search
                    // maps a whole missing sleeve/skirt rectangle onto one row,
                    // producing horizontal bars unrelated to the painted folds.
                    for (int dy = -3; dy <= 3; dy++) for (int dx = -3; dx <= 3; dx++)
                    {
                        int d = dx * dx + dy * dy;
                        if (d > 9 || d >= distance || !IsTextureDonor(sx + dx, sy + dy, background)) continue;
                        distance = d; foundX = sx + dx; foundY = sy + dy;
                    }
                    if (foundX < 0) continue;
                    sx = foundX; sy = foundY;
                }
                AddTexturedPixel(p.x, p.y, sx, sy, background);
            }
        }
        bool IsTextureDonor(int x, int y, int background)
        {
            // One painted garment can cross the body/cape ownership boundary.
            // Its donor UVs must stay continuous across that semantic boundary.
            if (background != 0 && background != 1 && background != 13)
                return IsBackgroundPixel(x, y, background);
            return IsCostumeInterior(x, y) && Pixel(x, y).a >= 240;
        }
        bool IsBackgroundPixel(int x, int y, int background)
        {
            if (x < 2 || x >= width - 2 || y < 2 || y >= height - 2 || Pixel(x, y).a < 240) return false;
            return ownership[y * mapWidth + x] == background &&
                ownership[y * mapWidth + x - 2] == background && ownership[y * mapWidth + x + 2] == background &&
                ownership[(y - 2) * mapWidth + x] == background && ownership[(y + 2) * mapWidth + x] == background &&
                Pixel(x - 2, y).a >= 240 && Pixel(x + 2, y).a >= 240 &&
                Pixel(x, y - 2).a >= 240 && Pixel(x, y + 2).a >= 240;
        }
        bool NearbySurfaceColor(Vector2Int point, int background, int reach, out Color32 color)
        {
            for (int step = 1; step <= reach; step++)
            {
                if (IsBackgroundPixel(point.x - step, point.y, background)) { color = Pixel(point.x - step, point.y); return true; }
                if (IsBackgroundPixel(point.x + step, point.y, background)) { color = Pixel(point.x + step, point.y); return true; }
                if (IsBackgroundPixel(point.x, point.y - step, background)) { color = Pixel(point.x, point.y - step); return true; }
                if (IsBackgroundPixel(point.x, point.y + step, background)) { color = Pixel(point.x, point.y + step); return true; }
            }
            color = new Color32(); return false;
        }
        bool IsCostumeInterior(int x, int y)
        {
            return IsCostumePixel(x, y) && IsCostumePixel(x - 2, y) && IsCostumePixel(x + 2, y) &&
                IsCostumePixel(x, y - 2) && IsCostumePixel(x, y + 2);
        }
        bool IsCostumePixel(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height || Pixel(x, y).a < 220) return false;
            int owner = ownership[y * mapWidth + x];
            return owner == 0 || owner == 1 || owner == 13;
        }
        void AddWeaponBackgrounds()
        {
            // Wide swords/flames were painted across skirts and legs. Continue a
            // garment only where actual costume brackets the removed weapon.
            // A body/cloth ownership boundary does not make that costume vanish.
            // Diagonal props can hide a whole leg width, so also consider the
            // visible surface directly above and below the cut.
            var covered = new List<Vector2Int>[PartCount];
            var rearFold = new List<Vector2Int>();
            int reach = Mathf.CeilToInt(.42f / unit);
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                if (ownership[y * mapWidth + x] != 14) continue;
                Vector2 top = new Vector2(x + .5f, height - y - .5f);
                Vector2[] garment = FrontGarment;
                if (profile.artIndex == 138 && InPolygon(top, solomonRearFold) && !InPolygon(top, solomonRobe))
                {
                    rearFold.Add(new Vector2Int(x, y)); continue;
                }
                if (garment != null && top.y > height - profile.hipY &&
                    !InPolygon(top, garment)) continue;
                // The actual front dress is visible immediately beside Bari's
                // staff; restore its full curved footprint, including rows where
                // the separate rear ribbon cannot bracket the front surface.
                if ((profile.artIndex == 133 || profile.artIndex == 134) && InPolygon(top, garment))
                {
                    if (covered[0] == null) covered[0] = new List<Vector2Int>();
                    covered[0].Add(new Vector2Int(x, y)); continue;
                }
                if (profile.artIndex == 113 && top.y >= 225)
                {
                    if (!InPolygon(top, freyaCapeHem)) continue;
                    if (covered[13] == null) covered[13] = new List<Vector2Int>();
                    covered[13].Add(new Vector2Int(x, y)); continue;
                }
                int leftStep = BackdropDistance(x, y, -1, 0, reach, false);
                int rightStep = BackdropDistance(x, y, 1, 0, reach, false);
                int left = leftStep < 0 ? -1 : x - leftStep, right = rightStep < 0 ? -1 : x + rightStep;
                int firstX = left, firstY = y, secondX = right, secondY = y;
                int firstDistance = x - left, secondDistance = right - x;
                if (left < 0 || right < 0)
                {
                    int belowStep = BackdropDistance(x, y, 0, -1, reach, false);
                    int aboveStep = BackdropDistance(x, y, 0, 1, reach, false);
                    if (belowStep < 0 || aboveStep < 0) continue;
                    int below = y - belowStep, above = y + aboveStep;
                    firstX = secondX = x; firstY = below; secondY = above;
                    firstDistance = y - below; secondDistance = above - y;
                }
                int background = ownership[firstY * mapWidth + firstX];
                int other = ownership[secondY * mapWidth + secondX];
                if (background != other)
                {
                    if (background <= 1 && other <= 1) background = 0;
                    else
                    {
                        // Two separate legs can bracket genuinely empty space.
                        bool firstLeftLeg = background >= 7 && background <= 9;
                        bool firstRightLeg = background >= 10 && background <= 12;
                        bool secondLeftLeg = other >= 7 && other <= 9;
                        bool secondRightLeg = other >= 10 && other <= 12;
                        if (firstLeftLeg && secondRightLeg || firstRightLeg && secondLeftLeg) continue;
                        if (secondDistance < firstDistance) background = other;
                    }
                }
                // Bari's front dress occludes the separate red ribbon behind it.
                // A nearby ribbon pixel is not the surface hidden by the staff.
                if (profile.artIndex == 133) continue;
                if (covered[background] == null) covered[background] = new List<Vector2Int>();
                covered[background].Add(new Vector2Int(x, y));
            }
            if (rearFold.Count > 0) AddTranslatedBackground(rearFold, 13, reach, new Vector2Int(-20, 0));
            for (int part = 0; part < PartCount; part++)
                if (covered[part] != null)
                {
                    if (profile.artIndex == 113 && part == 13)
                    {
                        var hem = covered[part].FindAll(p => height - 1 - p.y >= 225);
                        covered[part].RemoveAll(p => height - 1 - p.y >= 225);
                        // The broad red cape supplies a complete 2D hem patch;
                        // the narrow ribbon to the staff's right cannot do so.
                        AddTranslatedBackground(hem, part, reach, new Vector2Int(-96, 0));
                    }
                    Vector2Int measured = profile.artIndex == 133 && part == 0 ? new Vector2Int(-26, 0) :
                        profile.artIndex == 134 && part == 0 ? new Vector2Int(-22, 0) :
                        profile.artIndex == 130 && (part == 0 || part == 13) ? new Vector2Int(0, 32) : Vector2Int.zero;
                    AddTranslatedBackground(covered[part], part, reach, measured);
                }
        }
        int BackdropDistance(int x, int y, int dx, int dy, int reach, bool costumeOnly)
        {
            for (int step = 1; step <= reach; step++)
            {
                int sx = x + dx * step, sy = y + dy * step;
                // Source air between a prop/arm and a loose ribbon remains air.
                // Looking across it incorrectly creates a floating opaque plate.
                if (sx < 0 || sx >= width || sy < 0 || sy >= height ||
                    ownership[sy * mapWidth + sx] == Empty || Pixel(sx, sy).a < 180) break;
                if (costumeOnly ? IsCostumeInterior(sx, sy) : IsWeaponBackdrop(sx, sy)) return step;
            }
            return -1;
        }
        bool IsWeaponBackdrop(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height || Pixel(x, y).a < 240) return false;
            int owner = ownership[y * mapWidth + x];
            if (owner <= 1 || owner == 13) return IsCostumeInterior(x, y);
            return owner >= 7 && owner <= 12;
        }
        void AddCrossedLegBackground()
        {
            if (profile.artIndex != 129) return;
            // Nuwa's crossed bare legs cover the inner dress in the flattened
            // source. When the legs separate, continue that dress behind their
            // original footprint, using the adjacent painted cloth on each side.
            // Shoes and genuinely transparent space are never extended.
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int owner = ownership[y * mapWidth + x];
                bool left = owner == 7 || owner == 8;
                if (!left && owner != 10 && owner != 11) continue;
                int top = height - 1 - y;
                if (top < 145 || top > 224) continue;
                int sx = x + (left ? -24 : 16), sy = y;
                if (!IsInnerDressPixel(sx, sy))
                {
                    int best = int.MaxValue, foundX = -1, foundY = -1;
                    for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++)
                    {
                        int distance = dx * dx + dy * dy;
                        if (distance >= best || !IsInnerDressPixel(sx + dx, sy + dy)) continue;
                        best = distance; foundX = sx + dx; foundY = sy + dy;
                    }
                    if (foundX < 0) continue;
                    sx = foundX; sy = foundY;
                }
                AddTexturedPixel(x, y, sx, sy, 0);
            }
        }
        bool IsInnerDressPixel(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height || Pixel(x, y).a < 240) return false;
            int owner = ownership[y * mapWidth + x];
            return owner == 0 || owner == 13;
        }
        Vector2 SourceTopPoint(Vector2 p)
        { return new Vector2(p.x / unit + profile.groundX, height - p.y / unit - profile.groundY); }
        int MeasuredSole(Vector2 p)
        {
            Vector2 top = SourceTopPoint(p);
            // Isis's staff overlaps the heel. Its complete measured sandal must
            // stay on the foot before the staff silhouette is considered.
            if (profile.artIndex == 122 && InPolygonMargin(top, profile.rightLegOutline, .6f)) return 12;
            if (top.y < height - profile.legHemY) return -1;
            Vector2 left, right, leftRadius, rightRadius;
            switch (profile.artIndex)
            {
                case 102: left = new Vector2(73,242); right = new Vector2(141,236); leftRadius = new Vector2(15,9); rightRadius = new Vector2(22,8); break;
                case 108: left = new Vector2(66,254); right = new Vector2(126,254); leftRadius = new Vector2(11,8); rightRadius = new Vector2(14,8); break;
                case 117: left = new Vector2(85,271); right = new Vector2(151,269); leftRadius = new Vector2(16,10); rightRadius = new Vector2(22,9); break;
                case 122: left = new Vector2(91,289); right = new Vector2(144,290); leftRadius = new Vector2(13,8); rightRadius = new Vector2(19,7); break;
                case 132: left = new Vector2(86,267); right = new Vector2(157,267); leftRadius = new Vector2(15,8); rightRadius = new Vector2(24,8); break;
                case 143: left = new Vector2(54,280); right = new Vector2(118,279); leftRadius = new Vector2(11,7); rightRadius = new Vector2(15,7); break;
                default: return -1;
            }
            if (Ellipse(top - left, leftRadius.x, leftRadius.y) < 1) return 9;
            return Ellipse(top - right, rightRadius.x, rightRadius.y) < 1 ? 12 : -1;
        }
        void AddOccludedSurfaces()
        {
            AddCastBackfills();
            AddAuthoredArmBackfills();
            AddFionnSwordBackfill();
        }
        void AddFionnSwordBackfill()
        {
            if (profile.artIndex != 141) return;
            // The cast painting exposes the narrow skirt and knee surfaces
            // hidden by Fionn's idle sword. Keep the air between his calves.
            var replacement = new Dictionary<int, PaintPatch>();
            AddCastPatch(replacement, 0,
                Points(110,182.6f,121,182.6f,133,181.9f,140,184,141,190.3f,137,195.9f,
                    129,200.1f,121,203.6f,112,205.7f,108,202.9f,108,193.1f),
                Points(108,182.6f,138,182.6f,108,203.6f), Points(101,215,131,215,101,245),
                Points(103,215,114,215,126,214,133,217,134,226,130,234,122,240,
                    114,245,105,248,101,244,101,230), 765);
            AddCastPatch(replacement, 8,
                Points(91,180,98,181,105,187,107,193,107,199,104,204,92,203,89,198,89,188),
                Points(89,180,109,180,89,193), Points(86,212,106,212,86,225),
                Points(88,212,95,213,102,219,104,225,104,231,101,236,89,235,86,230,86,220),
                765, 14, 14, profile.leftLegOutline);
            AddCastPatch(replacement, 11,
                Points(159,188,170,189,178,194,181,202,181,212,178,218,160,216,158,207,159,200,157,194),
                Points(157,188,181,188,157,218), Points(127,228,151,228,127,258),
                Points(129,228,140,229,148,234,151,242,151,252,148,258,130,256,128,247,129,240,127,234),
                765, 14, 14, profile.rightLegOutline);
            patches.RemoveAll(p => replacement.ContainsKey(p.y * mapWidth + p.x));
            foreach (PaintPatch patch in replacement.Values) patches.Add(patch);
        }
        void AddCastBackfills()
        {
            if (profile.artIndex != 100 && profile.artIndex != 103) return;
            // The original cast painting exposes costume that the idle flame
            // hides. Register each surface separately; these remain static source
            // pixels attached to the idle skeleton, rather than swapped frames.
            var replacement = new Dictionary<int, PaintPatch>();
            if (profile.artIndex == 100)
            {
                AddCastPatch(replacement, 13,
                    Points(66,183,82,184,85,200,78,214,72,238,57,242,49,235,53,215),
                    Points(50,185,85,185,50,239), Points(105,165,161,165,105,219),
                    Points(105,164,154,168,164,192,162,216,144,220,110,219,104,200));
                AddCastPatch(replacement, 0,
                    Points(122,184,137,184,136,214,130,224,120,220,119,199),
                    Points(122,184,137,184,122,224), Points(134,144,149,144,134,196),
                    Points(131,143,151,143,156,177,149,198,135,196,130,184));
                AddCastPatch(replacement, 7,
                    Points(103,201,119,200,119,214,108,226,97,225,97,215),
                    Points(116,190,93,227,127,198), Points(120,153,106,193,133,160),
                    Points(109,158,132,160,125,180,111,195,99,189));
                AddCastPatch(replacement, 10,
                    Points(138,196,153,196,163,207,166,215,159,222,145,217,137,207),
                    Points(140,194,168,218,150,188), Points(149,153,146,194,160,155),
                    Points(137,157,159,158,159,186,149,202,138,195));
                AddCastPatch(replacement, 13,
                    Points(115,225,145,225,148,236,136,242,114,238),
                    Points(115,225,145,225,115,242), Points(111,196,141,196,111,213),
                    Points(113,188,138,188,142,217,112,217));
            }
            else
            {
                AddCastPatch(replacement, 13,
                    Points(135,214,144,222,155,238,155,248,144,247,140,236,131,222),
                    Points(131,214,155,214,131,248), Points(131,178,155,178,131,212),
                    Points(127,176,146,178,159,201,157,216,141,216,129,201));
                AddCastPatch(replacement, 13,
                    Points(129,145,140,153,154,174,173,202,174,211,158,204,146,192,134,173),
                    Points(129,145,174,211,129,185), Points(127,119,170,195,139,167),
                    Points(139,154,153,161,167,181,170,197,158,191,143,176));
                AddCastPatch(replacement, 0,
                    Points(98,182,110,180,111,196,103,212,93,229,80,232,79,220,89,198),
                    Points(98,182,110,182,98,225), Points(108,145,120,145,108,188),
                    Points(98,146,121,146,128,177,115,201,96,196));
                AddCastPatch(replacement, 0,
                    Points(113,185,121,187,136,216,148,232,130,231,115,214,107,203),
                    Points(113,185,133,185,113,232), Points(113,148,133,148,113,195),
                    Points(114,151,137,157,151,190,137,205,119,199));
            }
            // An authored donor replaces the generic nearest-neighbor underpaint
            // at this exact pixel; otherwise the generic patch can cover it.
            patches.RemoveAll(p => replacement.ContainsKey(p.y * mapWidth + p.x));
            foreach (PaintPatch patch in replacement.Values) patches.Add(patch);
        }
        void AddCastPatch(Dictionary<int, PaintPatch> output, int part, Vector2[] sourcePolygon,
            Vector2[] sourceControl, Vector2[] targetControl, Vector2[] targetPolygon,
            int sourceFrameTop = 695, int firstOwner = 14, int lastOwner = 14, Vector2[] targetMask = null)
        {
            Vector2 u = targetControl[1] - targetControl[0], v = targetControl[2] - targetControl[0];
            float determinant = u.x * v.y - u.y * v.x;
            if (Mathf.Abs(determinant) < .001f) return;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int owner = ownership[y * mapWidth + x];
                if (owner < firstOwner || owner > lastOwner) continue;
                Vector2 top = new Vector2(x + .5f, height - y - .5f);
                if (!InPolygon(top, targetPolygon)) continue;
                if (targetMask != null && !InPolygon(top, targetMask)) continue;
                Vector2 offset = top - targetControl[0];
                float a = (offset.x * v.y - offset.y * v.x) / determinant;
                float b = (u.x * offset.y - u.y * offset.x) / determinant;
                Vector2 donor = sourceControl[0] + a * (sourceControl[1] - sourceControl[0]) +
                    b * (sourceControl[2] - sourceControl[0]);
                if (!InPolygon(donor, sourcePolygon)) continue;
                int sx = Mathf.FloorToInt(donor.x), sy = height - 1 - sourceFrameTop - Mathf.FloorToInt(donor.y);
                if (Pixel(sx, sy).a <= 220) continue;
                int key = y * mapWidth + x;
                PaintPatch existing;
                if (output.TryGetValue(key, out existing) && surfaceOrder[existing.part] >= surfaceOrder[part]) continue;
                output[key] = new PaintPatch { x = x, y = y, sx = sx, sy = sy, part = part };
            }
        }
        void AddAuthoredArmBackfills()
        {
            if (profile.artIndex != 101 && profile.artIndex != 102 && profile.artIndex != 104 && profile.artIndex != 105 &&
                profile.artIndex != 111 && profile.artIndex != 120 && profile.artIndex != 132 && profile.artIndex != 133) return;
            var replacement = new Dictionary<int, PaintPatch>();
            if (profile.artIndex == 101)
                AddCastPatch(replacement, 13,
                    Points(81,116,89,116,99,112,108,109,108,123,105,132,102,139,95,142,87,140,82,134),
                    Points(105,114,83,118,99,139), Points(125,126,94,133,117,164),
                    Points(109,120,123,123,129,135,127,149,121,156,117,165,109,168,101,163,
                        97,156,89,153,89,147,96,139,103,130), 468, 3, 4);
            else if (profile.artIndex == 102)
                AddCastPatch(replacement, 13,
                    Points(72,135,85,139,86,149,86,165,82,177,75,186,63,183,58,172,63,155),
                    Points(60,140,100,140,60,190), Points(75,125,115,125,75,175),
                    Points(87,120,100,124,101,134,101,150,97,162,90,171,78,168,73,157,78,140), 468, 3, 4);
            else if (profile.artIndex == 104)
                AddCastPatch(replacement, 13,
                    Points(54,144,64,144,65,160,60,176,52,180,49,173,51,160),
                    Points(52,142,66,142,52,180), Points(76,126,104,126,76,176),
                    Points(83,125,103,128,105,140,101,160,94,176,75,176,75,164,79,147), 468, 3, 4);
            else if (profile.artIndex == 105)
                AddCastPatch(replacement, 13,
                    Points(147,135,153,135,157,144,158,150,166,155,173,163,166,169,150,165,145,155),
                    Points(146,135,158,135,146,166), Points(152,126,167,126,152,163),
                    Points(155,127,162,131,167,142,172,158,166,164,154,156,152,142), 757, 5, 6);
            else if (profile.artIndex == 111)
                AddCastPatch(replacement, 13,
                    Points(72,110,86,109,104,116,107,128,107,143,99,154,89,163,80,172,71,184,62,180,58,172,58,156,63,141,68,126),
                    Points(105,116,72,183,74,130), Points(120,133,99,217,88,151),
                    Points(85,133,96,132,110,138,121,149,125,165,117,180,113,196,102,202,86,192,81,177,80,157), 516, 3, 4);
            else if (profile.artIndex == 120)
                AddCastPatch(replacement, 13,
                    Points(84,163,95,163,96,170,93,182,83,184,80,176,82,168),
                    Points(80,162,98,162,80,184), Points(71,132,100,132,71,212),
                    Points(90,129,99,136,99,170,96,188,87,210,74,212,72,188,74,165,80,148), 790, 3, 4);
            else if (profile.artIndex == 132)
                AddCastPatch(replacement, 13,
                    Points(70,166,88,161,95,168,94,179,103,187,104,201,88,214,73,219,53,213,47,205,51,190,60,178),
                    Points(50,160,100,160,50,220), Points(50,153,100,153,50,213),
                    Points(70,159,88,154,95,161,94,172,103,180,104,194,88,207,73,212,53,206,47,198,51,183,60,171), 748, 3, 4);
            else
                AddCastPatch(replacement, 0,
                    Points(149.29f,147,153.14f,150,154.43f,159,157,175,155.71f,183,140.93f,184,
                        138.36f,178,140.93f,167,144.14f,159,146.71f,152),
                    Points(139,146,157,146,139,184), Points(104,170,132,170,104,208),
                    Points(120,171,126,174,128,183,132,199,130,207,107,208,103,202,107,191,112,183,116,176), 748, 3, 4);
            // Preserve joint overlaps belonging to the moving limb. Replace only
            // the guessed stationary costume with the registered original cloth.
            patches.RemoveAll(p => (p.part <= 1 || p.part == 13) && replacement.ContainsKey(p.y * mapWidth + p.x));
            foreach (PaintPatch patch in replacement.Values) patches.Add(patch);
        }
        static Vector2[] Points(params float[] coordinates)
        {
            var result = new Vector2[coordinates.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = new Vector2(coordinates[i * 2], coordinates[i * 2 + 1]);
            return result;
        }
        void AddTexturedPixel(int x, int y, int sx, int sy, int part)
        {
            patches.Add(new PaintPatch { x = x, y = y, sx = sx, sy = sy, part = part });
        }
        void FlushPaintPatches()
        {
            // Adjacent texture continuations share one scanline quad. This keeps
            // the same pixel artwork without thousands of one-texel mesh updates.
            patches.Sort((a, b) => {
                int order = surfaceOrder[a.part].CompareTo(surfaceOrder[b.part]); if (order != 0) return order;
                order = a.y.CompareTo(b.y); if (order != 0) return order;
                order = a.sy.CompareTo(b.sy); if (order != 0) return order;
                order = (a.sx - a.x).CompareTo(b.sx - b.x); return order != 0 ? order : a.x.CompareTo(b.x);
            });
            for (int index = 0; index < patches.Count;)
            {
                PaintPatch first = patches[index++]; int end = first.x + 1;
                while (index < patches.Count)
                {
                    PaintPatch next = patches[index];
                    if (next.part != first.part || next.y != first.y || next.sy != first.sy || next.sx - next.x != first.sx - first.x || next.x > end) break;
                    end = Mathf.Max(end, next.x + 1); index++;
                }
                AddQuad(Point(first.x, first.y), Point(end, first.y + 1),
                    new Vector2((sourceX + first.sx) / (float)texture.width, (sourceY + first.sy) / (float)texture.height),
                    new Vector2((sourceX + first.sx + end - first.x) / (float)texture.width, (sourceY + first.sy + 1) / (float)texture.height),
                    Shade(first.part), first.part, false);
            }
            patches.Clear();
        }
        void AddQuad(Vector2 min, Vector2 max, Vector2 uvMin, Vector2 uvMax, Color32 color, int part, bool solid)
        {
            if (part == 2 || part == 9 || part == 12 || part == 14)
            {
                AddQuadSegment(min, max, uvMin, uvMax, color, part, solid);
                return;
            }
            // Skin weights vary nonlinearly across arms and cloth. A whole-row
            // quad samples only its endpoints; neighboring rows with different
            // widths then disagree along their shared edge and open dark seams.
            // Use the same four-source-pixel X grid for every deforming scanline.
            int first = Mathf.RoundToInt(min.x / unit + profile.groundX);
            int last = Mathf.RoundToInt(max.x / unit + profile.groundX);
            int cursor = first;
            while (cursor < last)
            {
                int next = Mathf.Min(last, (Mathf.FloorToInt(cursor / 4f) + 1) * 4);
                if (part == 13)
                {
                    // A partially covered scanline can end between the common
                    // four-pixel columns. In the narrow nonlinear body/cloth
                    // blend, that T-junction otherwise interpolates a different
                    // location than the neighboring row's explicit vertex.
                    bool blend = false;
                    for (int x = cursor; !blend && x <= next; x++)
                    {
                        float px = Point(x, 0).x;
                        float lower = ClothBodyDistance(new Vector2(px, min.y));
                        float upper = ClothBodyDistance(new Vector2(px, max.y));
                        blend = lower > unit && lower < .055f || upper > unit && upper < .055f;
                    }
                    if (blend) next = cursor + 1;
                }
                float a = (cursor - first) / (float)(last - first), b = (next - first) / (float)(last - first);
                AddQuadSegment(new Vector2(Point(cursor, 0).x, min.y), new Vector2(Point(next, 0).x, max.y),
                    new Vector2(Mathf.Lerp(uvMin.x, uvMax.x, a), uvMin.y),
                    new Vector2(Mathf.Lerp(uvMin.x, uvMax.x, b), uvMax.y), color, part, solid);
                cursor = next;
            }
        }
        void AddQuadSegment(Vector2 min, Vector2 max, Vector2 uvMin, Vector2 uvMax, Color32 color, int part, bool solid)
        {
            int first = buildVertices.Count;
            AddVertex(new Vector2(min.x, min.y), uvMin, color, part, solid, Vector3.zero);
            AddVertex(new Vector2(max.x, min.y), new Vector2(uvMax.x, uvMin.y), color, part, solid, Vector3.zero);
            AddVertex(new Vector2(min.x, max.y), new Vector2(uvMin.x, uvMax.y), color, part, solid, Vector3.zero);
            AddVertex(new Vector2(max.x, max.y), uvMax, color, part, solid, Vector3.zero);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
            triangles.Add(first + 1); triangles.Add(first + 2); triangles.Add(first + 3);
        }
        void AddVertex(Vector2 p, Vector2 uv, Color32 color, int part, bool solid, Vector3 normal)
        {
            buildVertices.Add(p); buildUvs.Add(uv); buildModes.Add(new Vector2(solid ? 1 : 0, part == 14 ? 1 : 0));
            buildColors.Add(color); buildParts.Add(part); buildUnderpaint.Add(solid);
        }
        public void Pose(Matrix4x4[] skinMatrices, Color tint, int facing)
        {
            for (int i = 0; i < PartCount; i++) current[i] = skinMatrices[i];
            for (int i = 0; i < rest.Length; i++)
            {
                posed[i] = SkinPoint(bindings[i], rest[i]);
            }
            mesh.vertices = posed; mesh.RecalculateBounds();
            renderer.transform.localScale = new Vector3(facing < 0 ? -1 : 1, 1, 1);
            block.SetColor("_Color", tint); renderer.SetPropertyBlock(block);
        }
        Vector3 Transform(int owner, Vector3 point)
        { return SkinPoint(Bind(owner, point), point); }
        SkinBinding Bind(int owner, Vector3 point)
        {
            if (owner == 0 || owner == 1)
                return TwoBones(0, 1, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(hip.y - .055f, hip.y + .12f, point.y)));
            // A shoe is a rigid surface. Blending it into the shin bends the sole
            // and shears the painted toes when the ankle rotates during a step.
            if (owner == 9 || owner == 12) return new SkinBinding { first = owner };
            if (owner >= 7 && owner <= 12 && profile.artIndex != 126)
            {
                bool left = owner <= 9;
                Vector2 knee = left ? leftKnee : rightKnee, ankle = left ? leftAnkle : rightAnkle;
                int thigh = left ? 7 : 10, shin = left ? 8 : 11, sole = left ? 9 : 12;
                float upper = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(knee.y - .065f, knee.y + .065f, point.y));
                float leg = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ankle.y - .035f, ankle.y + .07f, point.y));
                float hem = left ? leftLegHem : rightLegHem;
                // Isis's old hem describes the floor-length robe, not the leg
                // root. Pinning there leaves both her painted calf and the
                // hidden right shin behind while the rigid sandals step.
                if (profile.artIndex == 122) hem = knee.y + .04f;
                // The painted leg begins at the hem cut. Match the neighboring
                // body exactly at that boundary, while preserving knee/ankle
                // blending below it, so swinging cannot tear open the cut.
                float body = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(hem - .12f, hem + .01f, point.y));
                return new SkinBinding { first = sole, second = shin, third = thigh,
                    secondWeight = leg * (1 - upper), thirdWeight = leg * upper, bodyWeight = body,
                    chestWeight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(hip.y - .055f, hip.y + .12f, point.y)) };
            }
            if (owner >= 3 && owner <= 6 && !IsSecondaryProp(point) &&
                !(profile.artIndex == 108 && Ellipse((Vector2)point - TopPoint(79, 173), 40 * unit, 47 * unit) < 1))
            {
                bool left = owner <= 4;
                Vector2 root = left ? leftShoulder : rightShoulder, hand = left ? leftHand : rightHand;
                Vector2 axis = hand - root;
                float along = Vector2.Dot((Vector2)point - root, axis) / Mathf.Max(.000001f, axis.sqrMagnitude);
                Vector2 elbow = left ? leftElbow : rightElbow;
                float bend = Vector2.Dot(elbow - root, axis) / Mathf.Max(.000001f, axis.sqrMagnitude);
                return TwoBones(left ? 3 : 5, left ? 4 : 6,
                    Mathf.SmoothStep(0, 1, Mathf.InverseLerp(bend - .13f, bend + .13f, along)));
            }
            if (owner != 13) return new SkinBinding { first = owner };
            // Cloth roots stay on the chest; the distant hem follows the delayed
            // cloth matrix. The source core itself lives in a different rigid part.
            float trail = Mathf.Clamp01(Mathf.Abs(point.y - shoulder.y) / .65f);
            var cloth = TwoBones(1, 13, trail);
            // The front garment and outer cloth meet at the same painted edge.
            // Match the body within a narrow band outside its classification
            // rectangle, leaving distant cape and wing motion unchanged.
            float edgeDistance = ClothBodyDistance(point);
            cloth.bodyWeight = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(unit, .055f, edgeDistance));
            cloth.chestWeight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(hip.y - .055f, hip.y + .12f, point.y));
            // These narrow rear hems belong to the adjacent skirt. Letting their
            // inner edge stay on the body while the outer edge follows the large
            // cape rotation widens a few source pixels into a striped triangle.
            // Pin the complete lower fold, fading into the upper garment.
            Vector2 sourceTop = SourceTopPoint(point);
            if (profile.artIndex == 101 && sourceTop.x >= 155)
                cloth.bodyWeight = Mathf.Max(cloth.bodyWeight,
                    Mathf.SmoothStep(0, 1, Mathf.InverseLerp(140, 165, sourceTop.y)));
            if (profile.artIndex == 104 && sourceTop.x >= 140)
                cloth.bodyWeight = Mathf.Max(cloth.bodyWeight,
                    Mathf.SmoothStep(0, 1, Mathf.InverseLerp(190, 205, sourceTop.y)));
            if (profile.artIndex == 113 && sourceTop.x >= 155)
                cloth.bodyWeight = Mathf.Max(cloth.bodyWeight,
                    Mathf.SmoothStep(0, 1, Mathf.InverseLerp(185, 210, sourceTop.y)));
            if (profile.artIndex == 138 && sourceTop.x >= 135)
                cloth.bodyWeight = Mathf.Max(cloth.bodyWeight,
                    Mathf.SmoothStep(0, 1, Mathf.InverseLerp(180, 200, sourceTop.y)));
            if (profile.artIndex == 129)
            {
                // The grey veil hangs directly from Nuwa's raised forearm.
                // Pin its upper edge to that arm, fading into the existing robe
                // before the torso ownership boundary and lower flowing hem.
                Vector2 top = SourceTopPoint(point);
                if (top.x >= 180 && top.x <= 207 && top.y >= 112 && top.y <= 141)
                {
                    float pin = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(180, 187, top.x)) *
                        (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(125, 141, top.y)));
                    cloth.secondWeight *= 1 - pin;
                    cloth.third = 6; cloth.thirdWeight = pin;
                    cloth.bodyWeight *= 1 - pin;
                }
            }
            return cloth;
        }
        float ClothBodyDistance(Vector2 point)
        {
            Vector2[] garment = FrontGarment;
            if (garment != null && point.y < hip.y + .035f)
            {
                Vector2 top = SourceTopPoint(point);
                if (InPolygon(top, garment)) return 0;
                float distance = float.MaxValue;
                for (int a = 0, b = garment.Length - 1; a < garment.Length; b = a++)
                    distance = Mathf.Min(distance, SegmentDistance(top, garment[a], garment[b]) * unit);
                return distance;
            }
            float edgeX = Mathf.Max(0, Mathf.Abs(point.x - hip.x) - .30f);
            float edgeY = Mathf.Max(0, point.y - hip.y - .035f);
            return Mathf.Sqrt(edgeX * edgeX + edgeY * edgeY);
        }
        static SkinBinding TwoBones(int first, int second, float weight)
        { return new SkinBinding { first = first, second = second, secondWeight = weight }; }
        Vector3 SkinPoint(SkinBinding binding, Vector3 point)
        {
            float firstWeight = 1 - binding.secondWeight - binding.thirdWeight;
            Vector3 result = firstWeight > .000001f ? current[binding.first].MultiplyPoint3x4(point) * firstWeight : Vector3.zero;
            if (binding.secondWeight > .000001f) result += current[binding.second].MultiplyPoint3x4(point) * binding.secondWeight;
            if (binding.thirdWeight > .000001f) result += current[binding.third].MultiplyPoint3x4(point) * binding.thirdWeight;
            if (binding.bodyWeight > .000001f)
            {
                Vector3 body = Vector3.Lerp(current[0].MultiplyPoint3x4(point), current[1].MultiplyPoint3x4(point), binding.chestWeight);
                result = Vector3.Lerp(result, body, binding.bodyWeight);
            }
            return result;
        }
        public Vector2 SampleSourcePoint(Vector2 restPoint)
        {
            int x = Mathf.FloorToInt(restPoint.x / unit + profile.groundX), y = Mathf.FloorToInt(restPoint.y / unit + profile.groundY);
            int owner = x >= 0 && x < mapWidth && y >= 0 && y < height ? ownership[y * mapWidth + x] : Empty;
            if (owner == Empty) owner = Classify(restPoint, true);
            return Transform(owner, restPoint);
        }
        public Vector3[] CaptureVertices() { return (Vector3[])posed.Clone(); }
        public Vector3[] CaptureRestVertices() { return (Vector3[])rest.Clone(); }
        public int[] CapturePartIndices() { return (int[])parts.Clone(); }
        public int SourcePixelOwner(int topX, int topY)
        {
            int y = height - 1 - topY;
            return topX < 0 || topX >= mapWidth || y < 0 || y >= height ? Empty : ownership[y * mapWidth + topX];
        }
        public void Dispose() { if (mesh != null) UnityEngine.Object.Destroy(mesh); }
        static float Ellipse(Vector2 d, float rx, float ry)
        { return rx <= 0 || ry <= 0 ? float.MaxValue : d.x * d.x / (rx * rx) + d.y * d.y / (ry * ry); }
        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        { var d = b - a; return Vector2.Distance(p, a + d * Mathf.Clamp01(Vector2.Dot(p - a, d) / Mathf.Max(.000001f, d.sqrMagnitude))); }
        static bool InPolygon(Vector2 point, Vector2[] polygon)
        {
            if (polygon == null || polygon.Length < 3) return false;
            bool inside = false;
            for (int a = 0, b = polygon.Length - 1; a < polygon.Length; b = a++)
            {
                Vector2 first = polygon[a], second = polygon[b];
                if ((first.y > point.y) != (second.y > point.y) &&
                    point.x < (second.x - first.x) * (point.y - first.y) / (second.y - first.y) + first.x) inside = !inside;
            }
            return inside;
        }
        static bool InPolygonMargin(Vector2 point, Vector2[] polygon, float margin)
        {
            if (polygon == null || polygon.Length < 3) return false;
            if (InPolygon(point, polygon)) return true;
            for (int a = 0, b = polygon.Length - 1; a < polygon.Length; b = a++)
                if (SegmentDistance(point, polygon[a], polygon[b]) <= margin) return true;
            return false;
        }
        static int ExtraWidth(int index) { return index == 128 ? 34 : index == 147 ? 16 : index == 101 ? 6 : 0; }
        bool IsRecoveredPixel(int x, int top)
        {
            return profile.artIndex == 128 ? x < 34 && top >= 179 && top <= 227 :
                profile.artIndex == 147 ? x < 16 && top >= 216 && top <= 232 :
                profile.artIndex == 101 && x < 6 && top >= 165 && top <= 184;
        }
    }
}

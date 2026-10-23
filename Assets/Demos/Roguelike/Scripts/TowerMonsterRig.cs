using System;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>
    /// A small skeleton for the original painted creatures. Source-space skinning keeps
    /// connected pixels connected; faces and long props have rigid, measured regions.
    /// </summary>
    public sealed class TowerMonsterRig : MonoBehaviour
    {
        public enum Form { Biped, Robed, Quadruped, Crawler, Slime, Winged, Tentacle, Spirit }
        public enum AttackStyle { BodyPulse, Claw, Smash, Bite, Pincer, Sting, Scythe,
            Pounce, SpearThrust, SwordSlash, BowRelease, Channel, WingBuffet, Breath, Headbutt, TentacleLash, SpiritReach }
        public enum Joint { Root, Spine, Head, NearArm, NearHand, FarArm, FarHand,
            NearLeg, NearFoot, FarLeg, FarFoot, Appendage, Tip, Cloth, Count }
        internal sealed class Anatomy
        {
            public Form form;
            public AttackStyle attack;
            public Vector2 head, headRadii, hip, shoulder, wing;
            public Vector2 propStart, propEnd;
            public float propRadius;
            public bool hasProp, facesLeft=true;
        }
        struct BonePose { public Vector2 offset; public float angle; }
        static readonly int[] Parents = { -1, 0, 1, 1, 3, 1, 5, 0, 7, 0, 9, 1, 11, 0 };
        static readonly Form[,] Forms = {
            {Form.Slime,Form.Winged,Form.Biped,Form.Quadruped,Form.Quadruped,Form.Robed,Form.Biped},
            {Form.Crawler,Form.Crawler,Form.Biped,Form.Crawler,Form.Robed,Form.Biped,Form.Biped},
            {Form.Slime,Form.Crawler,Form.Biped,Form.Quadruped,Form.Quadruped,Form.Robed,Form.Biped},
            {Form.Slime,Form.Quadruped,Form.Biped,Form.Crawler,Form.Robed,Form.Biped,Form.Winged},
            {Form.Slime,Form.Crawler,Form.Biped,Form.Spirit,Form.Tentacle,Form.Robed,Form.Tentacle},
            {Form.Slime,Form.Winged,Form.Quadruped,Form.Spirit,Form.Biped,Form.Robed,Form.Winged},
            {Form.Crawler,Form.Biped,Form.Spirit,Form.Biped,Form.Biped,Form.Robed,Form.Robed}
        };
        static readonly AttackStyle[,] Attacks = {
            {AttackStyle.BodyPulse,AttackStyle.Claw,AttackStyle.Smash,AttackStyle.Bite,AttackStyle.Bite,AttackStyle.Channel,AttackStyle.Smash},
            {AttackStyle.Pincer,AttackStyle.Pincer,AttackStyle.Smash,AttackStyle.Sting,AttackStyle.Channel,AttackStyle.Claw,AttackStyle.Smash},
            {AttackStyle.BodyPulse,AttackStyle.Scythe,AttackStyle.Smash,AttackStyle.Pounce,AttackStyle.Bite,AttackStyle.Channel,AttackStyle.Smash},
            {AttackStyle.BodyPulse,AttackStyle.Bite,AttackStyle.SpearThrust,AttackStyle.Pincer,AttackStyle.Channel,AttackStyle.Claw,AttackStyle.Breath},
            {AttackStyle.BodyPulse,AttackStyle.Pincer,AttackStyle.Smash,AttackStyle.SpearThrust,AttackStyle.TentacleLash,AttackStyle.Channel,AttackStyle.TentacleLash},
            {AttackStyle.BodyPulse,AttackStyle.WingBuffet,AttackStyle.Headbutt,AttackStyle.SpiritReach,AttackStyle.SwordSlash,AttackStyle.Channel,AttackStyle.WingBuffet},
            {AttackStyle.Pincer,AttackStyle.SwordSlash,AttackStyle.SpiritReach,AttackStyle.BowRelease,AttackStyle.Smash,AttackStyle.Channel,AttackStyle.Channel}
        };
        // Face centers/radii are in the cropped artwork's normalized bottom-left space.
        // These are deliberately individual measurements, not the center of the atlas cell.
        static readonly Vector2[,] Heads = {
            {new Vector2(.46f,.38f),new Vector2(.20f,.52f),new Vector2(.43f,.63f),new Vector2(.19f,.35f),new Vector2(.18f,.55f),new Vector2(.40f,.71f),new Vector2(.43f,.72f)},
            {new Vector2(.29f,.39f),new Vector2(.28f,.42f),new Vector2(.34f,.70f),new Vector2(.29f,.25f),new Vector2(.46f,.68f),new Vector2(.34f,.65f),new Vector2(.37f,.76f)},
            {new Vector2(.40f,.37f),new Vector2(.23f,.72f),new Vector2(.35f,.57f),new Vector2(.29f,.70f),new Vector2(.18f,.43f),new Vector2(.40f,.63f),new Vector2(.35f,.67f)},
            {new Vector2(.37f,.34f),new Vector2(.20f,.45f),new Vector2(.43f,.70f),new Vector2(.35f,.40f),new Vector2(.39f,.64f),new Vector2(.32f,.64f),new Vector2(.19f,.60f)},
            {new Vector2(.43f,.29f),new Vector2(.30f,.47f),new Vector2(.40f,.71f),new Vector2(.34f,.78f),new Vector2(.36f,.79f),new Vector2(.44f,.70f),new Vector2(.53f,.55f)},
            {new Vector2(.30f,.34f),new Vector2(.18f,.45f),new Vector2(.18f,.56f),new Vector2(.24f,.64f),new Vector2(.48f,.72f),new Vector2(.43f,.72f),new Vector2(.26f,.71f)},
            {new Vector2(.25f,.48f),new Vector2(.43f,.75f),new Vector2(.22f,.77f),new Vector2(.35f,.78f),new Vector2(.30f,.68f),new Vector2(.36f,.72f),new Vector2(.38f,.75f)}
        };

        readonly Transform[] bones = new Transform[(int)Joint.Count];
        readonly Vector2[] anchors = new Vector2[(int)Joint.Count];
        readonly BonePose[] current = new BonePose[(int)Joint.Count], target = new BonePose[(int)Joint.Count];
        readonly Matrix4x4[] matrices = new Matrix4x4[(int)Joint.Count];
        readonly Matrix4x4[] boneToRig = new Matrix4x4[(int)Joint.Count];
        Transform skeleton;
        TowerMonsterModel model;
        Anatomy anatomy;
        float width, height, clock, phase, gait, deathProgress, actionWeight;
        bool boss, posed;
        string previousMotion = "Idle";

        public Renderer Renderer { get { return model == null ? null : model.Renderer; } }
        public Sprite SourceSprite { get; private set; }
        public Form Morphology { get { return anatomy == null ? Form.Biped : anatomy.form; } }
        public AttackStyle AttackProfile { get { return anatomy == null ? AttackStyle.Claw : anatomy.attack; } }
        public bool SourceFacesLeft { get { return anatomy == null || anatomy.facesLeft; } }
        public int BoneCount { get { return model == null ? 0 : bones.Length; } }
        public int WeightedBoneCount { get { return model == null ? 0 : model.WeightedBoneCount; } }
        public float RenderedHeight { get { return model == null ? 0 : model.RenderedHeight; } }
        public Vector3[] CaptureVertices() { return model == null ? new Vector3[0] : model.CaptureVertices(); }
        public Vector3[] CaptureRestVertices() { return model == null ? new Vector3[0] : model.CaptureRestVertices(); }
        public int[] CaptureTriangles() { return model == null ? new int[0] : model.CaptureTriangles(); }
        public float[] CaptureJointAngles()
        {var result=new float[current.Length];for(int i=0;i<result.Length;i++)result[i]=current[i].angle;return result;}

        public void Build(Sprite source, TowerThemeId theme, int variant, bool isBoss, float worldHeight)
        {
            var origin = TowerThemeMonsterArt.Source(theme, variant, isBoss);
            if (origin.Final) { clock = 1.7f; Build(source, FinalBossAnatomy(), true, worldHeight); return; }
            int row = origin.Row, column = origin.Column;
            var form = Forms[row, column];
            var profile = new Anatomy {
                form = form, attack=Attacks[row,column], head = Heads[row, column],
                headRadii = form == Form.Quadruped ? new Vector2(.18f,.23f) : new Vector2(.13f,.15f),
                hip = new Vector2(.53f, form == Form.Quadruped || form == Form.Crawler ? .43f : .38f),
                shoulder = new Vector2(.46f, form == Form.Quadruped || form == Form.Crawler ? .59f : .65f),
                wing = new Vector2(.53f,.60f)
            };
            // A staff/trident/bow stays on one hand bone from tip to grip.
            if (form == Form.Robed || row == 3 && column == 2 || row == 4 && column == 3 ||
                row == 6 && column == 3 || row == 6 && column == 1 || row == 5 && column == 4)
            {
                profile.hasProp = true; profile.propRadius = .065f;
                profile.propStart = new Vector2(.15f,.10f); profile.propEnd = new Vector2(.09f,.87f);
                if (row == 4 && column == 3) { profile.propStart = new Vector2(.39f,.10f); profile.propEnd = new Vector2(.04f,.91f); }
                if (row == 6 && column == 3) { profile.propStart = new Vector2(.17f,.22f); profile.propEnd = new Vector2(.09f,.94f); profile.propRadius = .09f; }
                if (row == 6 && column == 1) { profile.propStart = new Vector2(.41f,.32f); profile.propEnd = new Vector2(.02f,.58f); }
                if (row == 5 && column == 4) { profile.propStart = new Vector2(.45f,.42f); profile.propEnd = new Vector2(.04f,.13f); }
                if (row == 2 && column == 5) { profile.propStart = new Vector2(.23f,.10f); profile.propEnd = new Vector2(.08f,.85f); }
            }
            if (row == 3 && column == 6) profile.wing = new Vector2(.66f,.60f);
            if (row == 5 && column == 6) profile.wing = new Vector2(.66f,.64f);
            clock = (row * 7 + column) * .173f;
            Build(source, profile, isBoss, worldHeight);
        }
        // Measured on the StoryPainter canvas (400x480): the mask face, the waist under the ribcage, two clawed
        // arms and a tentacled robe. Its lower body sways like the tentacle creatures and it lashes with them.
        static Anatomy FinalBossAnatomy()
        {
            return new Anatomy { form = Form.Tentacle, attack = AttackStyle.TentacleLash,
                head = new Vector2(.45f,.81f), headRadii = new Vector2(.11f,.11f), hip = new Vector2(.51f,.42f),
                shoulder = new Vector2(.51f,.66f), wing = new Vector2(.54f,.83f), facesLeft = true };
        }
        public void BuildLegacy(Sprite source, int kind, float worldHeight)
        {
            var form = kind == 0 || kind == 1 || kind == 4 ? Form.Slime : Form.Robed;
            clock=kind*.173f;
            Build(source, new Anatomy { form = form, attack=form==Form.Robed?AttackStyle.Channel:AttackStyle.BodyPulse,
                facesLeft=false, head = new Vector2(kind<2?.68f:.5f,kind<2?.45f:.67f),
                headRadii = new Vector2(.21f,.20f), hip = new Vector2(.5f,.35f),
                shoulder = new Vector2(.5f,.65f), wing = new Vector2(.5f,.66f) }, kind == 4, worldHeight);
        }
        void Build(Sprite source, Anatomy profile, bool isBoss, float worldHeight)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (model != null) { model.Dispose(); model = null; }
            if (skeleton != null) { skeleton.gameObject.SetActive(false); Destroy(skeleton.gameObject); }
            SourceSprite = source; anatomy = profile; boss = isBoss; height = Mathf.Max(.1f, worldHeight);
            width = height * source.rect.width / source.rect.height;
            skeleton = new GameObject("Creature skeleton — " + profile.form).transform;
            skeleton.SetParent(transform, false);
            SetAnchor(Joint.Root, profile.hip); SetAnchor(Joint.Spine, profile.hip);
            SetAnchor(Joint.Head, new Vector2(profile.head.x, profile.head.y - profile.headRadii.y * .65f));
            SetAnchor(Joint.NearArm, new Vector2(.32f,profile.shoulder.y));
            SetAnchor(Joint.NearHand, new Vector2(.23f,.40f));
            SetAnchor(Joint.FarArm, new Vector2(.68f,profile.shoulder.y));
            SetAnchor(Joint.FarHand, new Vector2(.78f,.39f));
            SetAnchor(Joint.NearLeg, new Vector2(.34f,profile.hip.y));
            SetAnchor(Joint.NearFoot, new Vector2(.29f,.16f));
            SetAnchor(Joint.FarLeg, new Vector2(.67f,profile.hip.y));
            SetAnchor(Joint.FarFoot, new Vector2(.70f,.16f));
            SetAnchor(Joint.Appendage, profile.wing);
            SetAnchor(Joint.Tip, new Vector2(.80f,profile.form == Form.Winged ? .74f : .43f));
            SetAnchor(Joint.Cloth, new Vector2(.55f,.43f));
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i] = new GameObject(((Joint)i).ToString()).transform;
                bones[i].SetParent(Parents[i] < 0 ? skeleton : bones[Parents[i]], false);
                bones[i].localPosition = anchors[i] - (Parents[i] < 0 ? Vector2.zero : anchors[Parents[i]]);
                current[i] = target[i] = new BonePose(); matrices[i] = Matrix4x4.identity;
            }
            model = new TowerMonsterModel(transform, source, profile, width, height);
            phase = gait = deathProgress = 0; previousMotion = "Idle"; posed = false;
            Pose("Idle", false, 0, 0, 1, Color.white, true, Vector2.zero, 0, 0);
        }
        void SetAnchor(Joint joint, Vector2 normalized)
        { anchors[(int)joint] = new Vector2((normalized.x - .5f) * width, normalized.y * height); }

        public void Pose(string motion, bool moving, float actionProgress, float hurtTime, int facing,
            Color tint, bool grounded, Vector2 velocity, float horizontalSpeed, float dt, float releaseFraction = .48f)
        {
            if (model == null) return;
            dt = Mathf.Max(0, dt);
            if (posed && dt <= 0) { model.Pose(matrices, tint, facing); return; }
            clock += dt;
            bool dead = motion == "Death";
            actionWeight=(motion=="Attack"||motion=="Cast"||motion.StartsWith("Boss",StringComparison.Ordinal))?Envelope(actionProgress):0;
            float speed = horizontalSpeed / Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.x));
            gait = Mathf.MoveTowards(gait, moving && !dead ? 1 : 0, dt * 7);
            // Travel advances the gait continuously; changing speed never jumps clip time.
            phase = Mathf.Repeat(phase + speed * dt / Mathf.Max(.4f,height * 1.65f), 1);
            for (int i = 0; i < target.Length; i++) target[i] = new BonePose();
            AnimateLocomotion(grounded, velocity.y, dead);
            float p = Mathf.Clamp01(actionProgress), release = Mathf.Clamp(releaseFraction,.25f,.85f);
            if (motion.StartsWith("Boss", StringComparison.Ordinal))
            {
                if (motion == "BossCrownVolley") { release = 1.2f / 2.25f; AnimateAction(p, release, true); Burst(p, release); Burst(p,1.6f/2.25f); Burst(p,2f/2.25f); }
                else if (motion == "BossTwinSpiral") { AnimateAction(p,1.3f/3.2f,true); Add(Joint.Appendage,Mathf.Sin(p*Mathf.PI*4)*5*Envelope(p),0,0); Add(Joint.FarArm,-Mathf.Sin(p*Mathf.PI*4)*7*Envelope(p),0,0); }
                else { float crouch = Pulse(p,.39f,.04f,.64f), land = Pulse(p,.81f,.75f,1); Add(Joint.Root,0,0,-height*(crouch*.055f+land*.065f)); Add(Joint.Spine,-5*crouch+4*land,0,0); Add(Joint.NearArm,13*crouch-16*land,0,0); Add(Joint.FarArm,-11*crouch+14*land,0,0); }
            }
            else if (motion == "Attack" || motion == "Cast") AnimateAction(p,release,motion=="Cast");
            if (hurtTime > 0 && !dead)
            {
                float hurt = Mathf.Sin(Mathf.Clamp01(hurtTime / .2f) * Mathf.PI);
                Add(Joint.Root,0,height*.035f*hurt,0); Add(Joint.Spine,-5*hurt,0,0); Add(Joint.Head,3*hurt,0,0);
            }
            if (dead)
            {
                deathProgress = Mathf.Max(deathProgress,p);
                float fall = Mathf.SmoothStep(0,1,deathProgress);
                // Long horizontal necks and paws need a shallow collapse; the
                // upright torso fold would pinch the panther's foreleg texture.
                float fold=anatomy.form==Form.Quadruped?.6f:1;
                Add(Joint.Root,8*fall,height*.035f*fall,-height*.18f*fall);
                Add(Joint.Spine,12*fall*fold,0,0); Add(Joint.Head,8*fall*fold,0,0);
                Add(Joint.NearArm,-12*fall*fold,0,0); Add(Joint.FarArm,10*fall*fold,0,0);
                tint.a *= 1 - Mathf.SmoothStep(0,.85f,deathProgress);
            }
            // All state changes share the same damped bone channel. No quantized bob,
            // whole-image squash or action-to-idle pose snap remains.
            float blend = posed ? 1 - Mathf.Exp(-24 * dt) : 1;
            if (previousMotion == "Death" && !dead) deathProgress = 0;
            for (int i = 0; i < bones.Length; i++)
            {
                // These are single-view painted figures. Keep their articulation
                // inside the available silhouette rather than bending through
                // hidden surfaces that were never painted.
                if(i!=(int)Joint.Root)target[i].angle*=.72f;
                // A long staff overlaps a narrow robe edge in the source image.
                // Its grip arm uses a smaller arc; free arms retain the full gesture.
                if(anatomy.hasProp&&(i==(int)Joint.NearArm||i==(int)Joint.NearHand))target[i].angle*=.6f;
                current[i].angle = Mathf.LerpAngle(current[i].angle,target[i].angle,blend);
                current[i].offset = Vector2.Lerp(current[i].offset,target[i].offset,blend);
                Vector2 local = anchors[i] - (Parents[i] < 0 ? Vector2.zero : anchors[Parents[i]]) + current[i].offset;
                Quaternion rotation=Quaternion.Euler(0,0,current[i].angle);
                bones[i].localPosition = local; bones[i].localRotation = rotation;
                // Evaluate in rig space, avoiding cancellation when a portrait or QA
                // stage sits far from world origin, and repeated world-matrix inverses.
                Matrix4x4 localMatrix=Matrix4x4.TRS(local,rotation,Vector3.one);
                boneToRig[i]=Parents[i]<0?localMatrix:boneToRig[Parents[i]]*localMatrix;
                matrices[i] = boneToRig[i] *
                    Matrix4x4.Translate(-new Vector3(anchors[i].x,anchors[i].y,0));
            }
            model.Pose(matrices,tint,facing);
            previousMotion = motion; posed = true;
        }
        void AnimateLocomotion(bool grounded, float verticalVelocity, bool dead)
        {
            if (dead) return;
            float breath = Mathf.Sin(clock * 2.2f), stride = Mathf.Sin(phase * Mathf.PI * 2) * gait;
            float opposite = -stride, sway = Mathf.Sin(clock * 2.4f - .7f);
            Add(Joint.Spine,breath*.65f,0,height*.003f*breath);
            Add(Joint.Head,-breath*.4f,0,0);
            Add(Joint.Cloth,sway*1.6f,0,0);
            switch (anatomy.form)
            {
                case Form.Slime:
                    Add(Joint.Spine,breath*1.1f,0,height*.012f*breath);
                    Add(Joint.NearArm,2.5f*breath,0,0); Add(Joint.FarArm,-2.5f*breath,0,0);
                    Add(Joint.Root,0,-height*.009f*stride,height*.018f*Mathf.Abs(stride));
                    Add(Joint.Appendage,sway*3,0,0); break;
                case Form.Winged:
                    float flap = Mathf.Sin(clock * (boss ? 4.2f : 6.2f))*(1-actionWeight*.65f);
                    Add(Joint.Appendage,flap*(boss?7:10),0,0); Add(Joint.Tip,flap*4,0,0);
                    Add(Joint.FarArm,-flap*3,0,0); Add(Joint.Root,0,0,height*.009f*Mathf.Sin(clock*3.1f));
                    Add(Joint.NearLeg,stride*5,0,0); Add(Joint.FarLeg,-stride*5,0,0); break;
                case Form.Tentacle:
                case Form.Spirit:
                    Add(Joint.NearArm,Mathf.Sin(clock*2.8f)*4,0,0);
                    Add(Joint.FarArm,Mathf.Sin(clock*2.8f+1)*4,0,0);
                    Add(Joint.NearLeg,Mathf.Sin(clock*3.1f+.4f)*5,0,0);
                    Add(Joint.FarLeg,Mathf.Sin(clock*3.1f+1.7f)*5,0,0);
                    Add(Joint.Cloth,Mathf.Sin(clock*2.8f+2)*4,0,0);
                    Add(Joint.Tip,Mathf.Sin(clock*2.8f+2.8f)*4,0,0);
                    Add(Joint.Root,0,0,height*.009f*Mathf.Sin(clock*2.8f)); break;
                case Form.Quadruped:
                case Form.Crawler:
                    AddLeg(Joint.NearLeg,Joint.NearFoot,stride,8);
                    AddLeg(Joint.FarLeg,Joint.FarFoot,opposite,8);
                    Add(Joint.NearArm,opposite*6,0,0); Add(Joint.FarArm,stride*6,0,0);
                    Add(Joint.Appendage,sway*2.5f+stride*2,0,0);
                    Add(Joint.Root,0,0,height*.006f*Mathf.Abs(stride)); break;
                default:
                    float amplitude = anatomy.form == Form.Robed ? 3.5f : boss ? 6 : 9;
                    AddLeg(Joint.NearLeg,Joint.NearFoot,stride,amplitude);
                    AddLeg(Joint.FarLeg,Joint.FarFoot,opposite,amplitude);
                    Add(Joint.NearArm,opposite*(anatomy.hasProp?2:4),0,0);
                    Add(Joint.FarArm,stride*4,0,0);
                    Add(Joint.Cloth,stride*2,0,0); break;
            }
            if (!grounded && anatomy.form != Form.Winged && anatomy.form != Form.Spirit && anatomy.form != Form.Tentacle)
            {
                float lift = Mathf.Clamp(verticalVelocity / 8,-1,1);
                Add(Joint.NearLeg,-lift*4,0,0); Add(Joint.FarLeg,lift*3,0,0);
            }
        }
        void AddLeg(Joint leg,Joint foot,float stride,float angle)
        {
            Add(leg,stride*angle,0,0);
            Add(foot,-stride*angle*.65f,0,height*.012f*Mathf.Max(0,stride));
        }
        void AnimateAction(float p,float release,bool cast)
        {
            float charge = Pulse(p,release*.65f,0,release);
            float strike = Pulse(p,release,release*.67f,Mathf.Min(1,release+.25f));
            float forward=anatomy.facesLeft?-1:1;
            Add(Joint.Root,0,forward*height*(-charge*.018f+strike*.050f),0);
            Add(Joint.Spine,-charge*2.5f+strike*3.5f,0,0);
            Add(Joint.Head,charge*1.5f-strike*2,0,0);
            Add(Joint.Cloth,charge*2-strike*3,0,0);
            // The attack belongs to the painted anatomy. A mouth leads a bite,
            // a pincer closes from its wrist, and a staff stays attached to its grip.
            switch(cast?AttackStyle.Channel:anatomy.attack)
            {
                case AttackStyle.BodyPulse:
                    Add(Joint.Spine,-charge*1.5f+strike*2,0,height*(-charge*.018f+strike*.018f));
                    Add(Joint.NearArm,charge*4-strike*5,0,0);Add(Joint.FarArm,-charge*4+strike*5,0,0);
                    Add(Joint.Appendage,-charge*2+strike*3,0,0);break;
                case AttackStyle.Bite:
                case AttackStyle.Headbutt:
                    Add(Joint.Head,charge*3-strike*4,forward*height*strike*.012f,0);
                    Add(Joint.NearArm,charge*3-strike*4,0,0);Add(Joint.FarArm,-charge*2+strike*3,0,0);
                    Add(Joint.NearLeg,-charge*2+strike*2,0,0);Add(Joint.FarLeg,charge*2-strike*2,0,0);
                    Add(Joint.NearFoot,charge*1.5f-strike*1.5f,0,0);Add(Joint.FarFoot,-charge*1.5f+strike*1.5f,0,0);break;
                case AttackStyle.Pounce:
                    Add(Joint.Root,0,0,height*(-charge*.025f+strike*.045f));
                    Add(Joint.NearLeg,charge*4-strike*5,0,0);Add(Joint.FarLeg,-charge*4+strike*5,0,0);
                    Add(Joint.NearFoot,-charge*3+strike*4,0,0);Add(Joint.FarFoot,charge*3-strike*4,0,0);
                    Add(Joint.NearArm,charge*3-strike*4,0,0);Add(Joint.FarArm,-charge*3+strike*4,0,0);break;
                case AttackStyle.Smash:
                    Add(Joint.NearArm,charge*9-strike*9,0,0);Add(Joint.FarArm,-charge*8+strike*8,0,0);
                    Add(Joint.NearHand,charge*2-strike*4,0,0);Add(Joint.FarHand,-charge*2+strike*4,0,0);
                    Add(Joint.NearLeg,charge*2-strike,0,0);Add(Joint.FarLeg,-charge*2+strike,0,0);break;
                case AttackStyle.Pincer:
                case AttackStyle.Scythe:
                    Add(Joint.NearArm,charge*7-strike*8,0,0);Add(Joint.FarArm,-charge*6+strike*7,0,0);
                    Add(Joint.NearHand,-charge*3+strike*5,0,0);Add(Joint.FarHand,charge*3-strike*5,0,0);
                    Add(Joint.NearFoot,-charge+strike*1.5f,0,0);Add(Joint.FarFoot,charge-strike*1.5f,0,0);break;
                case AttackStyle.Sting:
                    Add(Joint.Appendage,-charge*4+strike*6,0,0);Add(Joint.Tip,-charge*3+strike*4,0,0);
                    Add(Joint.NearArm,charge*3-strike*4,0,0);Add(Joint.FarArm,-charge*3+strike*4,0,0);
                    Add(Joint.NearHand,-charge*2+strike*3,0,0);Add(Joint.FarHand,charge*2-strike*3,0,0);break;
                case AttackStyle.SpearThrust:
                    Add(Joint.NearArm,charge*6-strike*8,forward*height*(-charge*.006f+strike*.014f),0);
                    Add(Joint.FarArm,-charge*3+strike*4,0,0);Add(Joint.FarHand,charge*2-strike*3,0,0);
                    Add(Joint.NearLeg,-charge*1.5f+strike*2,0,0);Add(Joint.FarLeg,charge-strike*1.5f,0,0);break;
                case AttackStyle.SwordSlash:
                    Add(Joint.NearArm,charge*8-strike*10,0,0);
                    Add(Joint.FarArm,-charge*3+strike*4,0,0);Add(Joint.FarHand,charge-strike*2,0,0);
                    Add(Joint.Head,-charge+strike,0,0);break;
                case AttackStyle.BowRelease:
                    Add(Joint.NearArm,charge*3-strike*4,0,0);
                    Add(Joint.FarArm,-charge*7+strike*5,0,0);Add(Joint.FarHand,charge*4-strike*3,0,0);
                    Add(Joint.Head,-charge*2+strike,0,0);break;
                case AttackStyle.WingBuffet:
                case AttackStyle.Breath:
                    float wing=anatomy.attack==AttackStyle.Breath?.6f:1;
                    Add(Joint.Appendage,-charge*6*wing+strike*7*wing,0,0);Add(Joint.Tip,-charge*2*wing+strike*3*wing,0,0);
                    Add(Joint.NearArm,charge*5*wing-strike*6*wing,0,0);Add(Joint.NearHand,charge*2-strike*3,0,0);
                    Add(Joint.NearLeg,charge*2-strike*3,0,0);Add(Joint.FarLeg,-charge*2+strike*3,0,0);
                    if(anatomy.attack==AttackStyle.Breath)Add(Joint.Head,charge*3-strike*4,forward*height*strike*.01f,0);
                    break;
                case AttackStyle.TentacleLash:
                    Add(Joint.NearArm,-charge*4+strike*6,0,0);Add(Joint.FarArm,charge*4-strike*5,0,0);
                    Add(Joint.NearLeg,charge*3-strike*5,0,0);Add(Joint.FarLeg,-charge*3+strike*4,0,0);
                    Add(Joint.Tip,-charge*3+strike*4,0,0);Add(Joint.Cloth,charge*2-strike*3,0,0);break;
                case AttackStyle.SpiritReach:
                    Add(Joint.NearArm,charge*6-strike*8,0,0);Add(Joint.FarArm,-charge*4+strike*6,0,0);
                    Add(Joint.NearLeg,-charge*2+strike*3,0,0);Add(Joint.FarLeg,charge*2-strike*3,0,0);
                    Add(Joint.Cloth,-charge*3+strike*4,0,0);break;
                case AttackStyle.Channel:
                    Add(Joint.NearArm,charge*5-strike*7,0,0);Add(Joint.FarArm,-charge*7+strike*8,0,0);
                    Add(Joint.FarHand,charge*3-strike*4,0,0);Add(Joint.Head,-charge*2+strike*2,0,0);
                    Add(Joint.Appendage,charge*2-strike*3,0,0);break;
                default:
                    Add(Joint.NearArm,charge*7-strike*10,0,0);Add(Joint.NearHand,charge*3-strike*4,0,0);
                    Add(Joint.FarArm,-charge*3+strike*5,0,0);Add(Joint.FarHand,-charge+strike*2,0,0);break;
            }
        }
        void Burst(float p,float peak)
        { float burst=Pulse(p,peak,peak-.035f,peak+.07f); Add(Joint.Spine,-burst*3,0,0); Add(Joint.NearArm,burst*4,0,0); }
        static float Envelope(float p) { return Mathf.Sin(Mathf.Clamp01(p)*Mathf.PI); }
        static float Pulse(float p,float peak,float start,float end)
        { return p<peak?Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,peak,p)):Mathf.SmoothStep(1,0,Mathf.InverseLerp(peak,end,p)); }
        void Add(Joint joint,float angle,float x,float y)
        { target[(int)joint].angle+=angle;target[(int)joint].offset+=new Vector2(x,y); }
        void OnDestroy() { if(model!=null)model.Dispose(); }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>Plays supplied pixel textures, weighted geometry and sampled original bone clips.</summary>
    public sealed class TowerActorArt : MonoBehaviour
    {
        public const float PixelScale=1f/32f;
        [Serializable] sealed class ArtFile { public int sampleRate; public ActorData[] actors; }
        [Serializable] sealed class ActorData
        {
            public int index; public string sourceVersion; public float groundOffset,portraitX,portraitY,portraitSize;
            public BoneData[] bones; public PartData[] parts; public ClipData[] clips;
        }
        [Serializable] sealed class BoneData { public string name; public int parent,inherit; public float x,y,rotation,scaleX,scaleY,shearX,shearY; }
        [Serializable] sealed class PartData
        {
            public string name,texture; public int bone,order; public int[] counts,bindBones,triangles;
            public float[] bindValues,uv; public float x,y,angle,scaleX,scaleY;
        }
        [Serializable] sealed class ClipData { public string name; public float duration; public FrameData[] frames; public int boneTrackCount; }
        [Serializable] sealed class FrameData { public float time; public float[] pose; }
        sealed class MeshPart { public PartData source; public Mesh mesh; public MeshRenderer renderer; public Vector3[] vertices; }
        enum ActionKind { None,Attack,Cast,BossAttack,Death }
        struct Profile
        {
            public string name; public float contact,release,walkRate,idleRate;
            public Profile(string n,float c,float r,float w,float idle){name=n;contact=c;release=r;walkRate=w;idleRate=idle;}
        }
        static readonly Profile[] Profiles={
            new Profile("Oathblade",.43f,.43f,1f,1f),new Profile("Blackguard",.46f,.46f,.90f,.92f),
            new Profile("Dawnstaff",.50f,.50f,.92f,.85f),new Profile("FrostNinja",.38f,.38f,1.18f,1.06f),
            new Profile("FlameWitch",.50f,.50f,.94f,.94f),new Profile("BeastFist",.35f,.35f,1.12f,1.12f),
            new Profile("DragonLancer",.48f,.48f,.96f,.90f),new Profile("AstralFrost",.52f,.52f,.90f,.86f),
            new Profile("Lifebloom",.55f,.55f,.93f,.82f),new Profile("Corsair",.42f,.42f,1.05f,1.03f),
            new Profile("ShadowKnife",.34f,.34f,1.22f,1.08f),new Profile("Crossbow",.52f,.52f,1.08f,1.02f),
            new Profile("NightViolin",.50f,.50f,.98f,.88f),new Profile("IronFist",.40f,.40f,.93f,.96f),
            new Profile("SylvanBow",.58f,.58f,1.06f,.90f),new Profile("Unused",.45f,.45f,1f,1f),
            new Profile("ClockworkBlades",.44f,.44f,1.14f,1.10f)};
        static ArtFile library;
        static readonly Dictionary<int,Texture2D> portraits=new Dictionary<int,Texture2D>();
        static readonly Dictionary<Texture2D,Material> materials=new Dictionary<Texture2D,Material>();
        readonly List<MeshPart> meshes=new List<MeshPart>();
        MaterialPropertyBlock tintBlock;
        ActorData data; Profile profile; Transform rig; Transform[] bones;
        TowerMythArt mythArt;
        TowerHeroPixelArt heroPixel;
        TowerMonsterRig monsterRig;
        Matrix4x4[] boneMatrices; float[] pose,locomotionPose,locomotionFrom,entryPose,exitPose; bool[] lowerBody;
        ClipData idleClip,runClip,attackClip,deathClip;
        SpriteRenderer monsterRenderer; Vector3 monsterRest; float monsterScale,monsterWidth,monsterHeight;
        static readonly string[] BossMotions={"CrownVolley","TwinSpiral","LeapingSlam"};
        int bossMotion=-1;
        string themeMonsterName;
        int index,combo,monsterKind; bool isMonster,grounded=true,dashing,wasAttacking,wasHurt;
        float clock,actionTime,actionDuration=.4f,hurtTime,verticalVelocity,horizontalSpeed,horizontalVelocity,movingBlend,exitTime;
        float locomotionClock,locomotionRate=1,landingAge=.18f,locomotionAge;
        int locomotionState=-1;
        bool previousGrounded=true,playerPosed;
        ActionKind action; SkillEffect castEffect;
        // Mouse aim (bone rigs whose sword hangs from the back arm): the arm holds a guard turned toward the aim and each
        // aimed swing cuts through it to the opposite guard, high and low in turn. Rig angles: 0 is straight ahead, 90 up.
        int bodyBone=-1,headBone=-1,armBone=-1,forearmBone=-1,weaponBone=-1;
        Vector2 aimDirection=Vector2.right,swingDirection=Vector2.right;
        bool aimActive,aimedSwing,swingRising,guardLow;
        float aimWeight,guardBlend,sinceSwing,shownArm=-18,shownBlade=64;
        const float BladeLength=53,ShoulderHeight=24,ArmLength=12.7f,ElbowBend=-6;
        // Ranged stances (the same sword arm): the blade is put away and the arm points a pistol straight at the aim point,
        // or reaches toward it with an open hand while the orb fights. Their angles run from the shoulder to the point itself.
        TowerAttackMode weapon=TowerAttackMode.Sword;
        readonly List<MeshRenderer> swordParts=new List<MeshRenderer>();
        SpriteRenderer gun; static Sprite gunSprite;
        Vector2 aimPoint; bool hasAimPoint;
        float recoil,beckon;
        // The pistol in art pixels from the weapon bone: its grip sits in the closed hand, the barrel runs above it.
        static readonly Vector2 GunGrip=new Vector2(-2.5f,.3f),GunMuzzle=new Vector2(15,4.8f);
        const float GunElbow=-4,ReachElbow=-26;
        public Color Tint { get; set; }=Color.white;
        // A hit flash holds the pose and lights the body white without touching Tint; the held time plays back once it ends,
        // so the motion stays in step with what the actor is doing.
        float hitFlash,heldPose;
        const float HitFlashGain=2;
        public void HitFlash(float seconds){hitFlash=Mathf.Max(hitFlash,seconds);}
        /// <summary>Ends a hit flash at once (a pause); the held pose time plays back on the next SetPose.</summary>
        public void ClearHitFlash(){hitFlash=0;}
        // A poise break stops the motion under way and plays the hurt pose afresh, held at its height until the last .1 s
        // of the stagger, when it settles.
        float staggerHold;
        public void Stagger(float seconds)
        {if(action==ActionKind.Death)return;InterruptAction();hurtTime=.20f;wasHurt=true;staggerHold=Mathf.Max(staggerHold,seconds-.10f);}
        // The last moment of an attack's windup lights the body white on a channel of its own (the game sets TelegraphLit
        // every tick), under a hit flash and never through Tint. BlinkAlpha scales the drawn alpha (invulnerability) the
        // same way, so elite colours, the veil and death fades keep theirs. ShownColor is the colour last handed to the renderers.
        public bool TelegraphLit { get; set; }
        public float BlinkAlpha { get; set; }=1;
        public Color ShownColor { get; private set; }=Color.white;
        const float TelegraphFlashGain=1.7f;
        public int Facing { get; set; }=1;
        public int ArtIndex { get { return index; } }
        public string ProfileName { get { return isMonster?(themeMonsterName??(monsterKind==4?"CrownSlimeKing":monsterKind==3?"TowerOwlBat":"TowerGuardian")):profile.name; } }
        public string MotionName { get; private set; }="Idle";
        public string BossMotionName { get { return bossMotion>=0?BossMotions[bossMotion]:"Idle"; } }
        public float BossVisualHeight { get { return isMonster&&monsterKind==4?monsterHeight*monsterScale*Mathf.Abs(transform.lossyScale.y):0; } }
        public float RenderedHeight
        {
            get
            {
                if(mythArt!=null)return mythArt.RenderedHeight;
                if(monsterRig!=null)return monsterRig.RenderedHeight;
                if(monsterRenderer==null)return 0;
                float top=transform.position.y;var visual=monsterRenderer.transform;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
                    top=Mathf.Max(top,visual.TransformPoint(new Vector3(x*monsterWidth*.5f,y*monsterHeight*.5f,0)).y);
                return top-transform.position.y;
            }
        }
        public string CurrentMotion { get { return MotionName; } }
        /// <summary>The pixel god renderer of a companion, or null for the player and monsters.</summary>
        public TowerGodPixelArt PixelGod { get { return mythArt!=null?mythArt.Pixel:null; } }
        /// <summary>chuchuking's pixel hero drawing the player (art 0), or null for everyone else.</summary>
        public TowerHeroPixelArt PixelHero { get { return heroPixel; } }
        public float ActionTime { get { return actionTime; } }
        public float ActionDuration { get { return actionDuration; } }
        public float AttackHitTime(float duration){return Mathf.Max(.08f,duration)*(isMonster?.48f:profile.contact);}
        public float AttackReleaseTime(float duration){return Mathf.Max(.08f,duration)*(isMonster?.48f:profile.release);}
        public float CastReleaseTime(SkillEffect effect,float duration=.5f)
        {return Mathf.Max(.08f,duration)*(IsSupport(effect)?.55f:profile.release);}

        public static TowerActorArt Create(Transform parent,int artIndex,bool monster=false)
        {
            var go=new GameObject(monster?"Original pixel monster":"Original pixel actor "+artIndex);go.transform.SetParent(parent,false);
            var art=go.AddComponent<TowerActorArt>();art.Build(artIndex,monster);return art;
        }
        public void SetThemeMonster(TowerThemeId theme,int variant,bool boss)
        {
            if(!isMonster||monsterRenderer==null)return;
            monsterRenderer.sprite=TowerThemeMonsterArt.Get(theme,variant,boss);monsterRenderer.flipX=true;
            themeMonsterName=TowerThemeMonsterArt.Name(theme,variant,boss);gameObject.name=themeMonsterName;
            monsterKind=boss?4:monsterKind==4?0:monsterKind;
            monsterWidth=monsterRenderer.sprite.bounds.size.x;monsterHeight=monsterRenderer.sprite.bounds.size.y;
            monsterScale=PixelScale*(boss?50f/monsterHeight:Mathf.Min(1,42f/monsterHeight));
            monsterRest=new Vector3(0,monsterHeight*monsterScale*.5f,0);
            monsterRenderer.transform.localScale=new Vector3(monsterScale,monsterScale,1);monsterRenderer.transform.localPosition=monsterRest;
            if(monsterRig==null)monsterRig=gameObject.AddComponent<TowerMonsterRig>();
            monsterRig.Build(monsterRenderer.sprite,theme,variant,boss,monsterHeight*monsterScale);
            monsterRenderer.enabled=false;
        }
        static void LoadLibrary()
        {
            if(library!=null)return;
            var asset=Resources.Load<TextAsset>("Tower/ArtData");
            if(asset==null)throw new InvalidOperationException("Original pixel ArtData is missing.");
            library=JsonUtility.FromJson<ArtFile>(asset.text);
        }
        static ActorData FindData(int artIndex)
        {
            LoadLibrary();foreach(var actor in library.actors)if(actor.index==artIndex)return actor;
            return library.actors[0];
        }
        void Build(int artIndex,bool monster)
        {
            if(!monster&&TowerMythArt.IsMythIndex(artIndex))
            {
                index=artIndex;profile=new Profile(TowerMythArt.ProfileName(artIndex),.46f,.46f,1,1);
                mythArt=gameObject.AddComponent<TowerMythArt>();mythArt.Build(artIndex);SetPose(false,false,false,1,0);return;
            }
            if(!monster&&artIndex==0)
            {
                // The player is chuchuking's pixel hero; the original bone rig of art 0 is no longer drawn.
                index=0;profile=Profiles[0];
                heroPixel=gameObject.AddComponent<TowerHeroPixelArt>();heroPixel.Build();SetPose(false,false,false,1,0);return;
            }
            tintBlock=new MaterialPropertyBlock();index=Mathf.Clamp(artIndex,0,16);isMonster=monster;profile=Profiles[index];
            rig=new GameObject("Original bone rig").transform;rig.SetParent(transform,false);
            if(monster)
            {
                int kind=((artIndex%5)+5)%5;monsterKind=kind;var go=new GameObject("Original monster sprite");go.transform.SetParent(rig,false);
                monsterRenderer=go.AddComponent<SpriteRenderer>();monsterRenderer.sprite=Resources.Load<Sprite>("Tower/Monsters/monster_"+kind);monsterRenderer.sortingOrder=30;
                monsterWidth=kind==0?43:kind==1?47:kind==4?170:50;monsterHeight=kind==0?30:kind==1?33:kind==2?45:kind==4?130:50;
                monsterScale=PixelScale*(kind==4?50f/130f:1);monsterRest=new Vector3(0,monsterHeight*monsterScale*.5f,0);
                monsterRenderer.transform.localScale=Vector3.one*monsterScale;monsterRenderer.transform.localPosition=monsterRest;
                monsterRig=gameObject.AddComponent<TowerMonsterRig>();monsterRig.BuildLegacy(monsterRenderer.sprite,kind,monsterHeight*monsterScale);
                monsterRenderer.enabled=false;return;
            }
            data=FindData(index);bones=new Transform[data.bones.Length];boneMatrices=new Matrix4x4[bones.Length];pose=new float[bones.Length*7];locomotionPose=new float[pose.Length];locomotionFrom=new float[pose.Length];entryPose=new float[pose.Length];exitPose=new float[pose.Length];lowerBody=new bool[bones.Length];
            for(int i=0;i<bones.Length;i++)
            {
                var b=data.bones[i];bones[i]=new GameObject(b.name).transform;bones[i].SetParent(b.parent<0?rig:bones[b.parent],false);
                lowerBody[i]=b.name=="Rleg"||b.name=="Lleg"||(b.parent>=0&&lowerBody[b.parent]);
            }
            foreach(var source in data.parts)
            {
                var texture=Resources.Load<Texture2D>("Tower/Actors/cha_"+index.ToString("000")+"/"+source.texture);
                if(texture==null)throw new InvalidOperationException("Original pixel part missing: "+index+"/"+source.texture);
                Material material;
                if(!materials.TryGetValue(texture,out material))
                {material=new Material(Shader.Find("Sprites/Default")){name="Original pixel "+texture.name};material.mainTexture=texture;materials.Add(texture,material);}
                var go=new GameObject(source.name+" mesh");go.transform.SetParent(rig,false);
                var mesh=new Mesh{name="Original weighted "+source.name};mesh.MarkDynamic();
                var vertices=new Vector3[source.counts.Length];var uv=new Vector2[vertices.Length];
                for(int v=0;v<uv.Length;v++)uv[v]=new Vector2(source.uv[v*2],1-source.uv[v*2+1]);
                var colors=new Color32[vertices.Length];for(int v=0;v<colors.Length;v++)colors[v]=new Color32(255,255,255,255);
                mesh.vertices=vertices;mesh.colors32=colors;mesh.uv=uv;mesh.triangles=source.triangles;
                mesh.bounds=new Bounds(new Vector3(0,35,0),new Vector3(300,240,4));
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=30+source.order;
                meshes.Add(new MeshPart{source=source,mesh=mesh,renderer=renderer,vertices=vertices});
            }
            idleClip=Clip("idle");runClip=Clip("run");attackClip=Clip("atk");deathClip=Clip("die");
            bodyBone=Bone("Body");headBone=Bone("head");armBone=Bone("Rarm");weaponBone=Bone("Weapon");
            forearmBone=weaponBone>=0?data.bones[weaponBone].parent:-1;
            if(armBone<0||forearmBone<0||data.bones[forearmBone].parent!=armBone||data.bones[armBone].parent!=bodyBone)armBone=-1;
            if(armBone>=0)foreach(var part in meshes)if(Array.TrueForAll(part.source.bindBones,b=>b==weaponBone))swordParts.Add(part.renderer);
            SetPose(false,false,false,1,0);
        }
        ClipData Clip(string name){foreach(var c in data.clips)if(c.name==name)return c;return null;}
        void BeginAction(ActionKind kind,float duration)
        {
            if(pose!=null)Array.Copy(pose,entryPose,pose.Length);
            action=kind;actionTime=0;actionDuration=Mathf.Max(.08f,duration);exitTime=0;
        }
        public void PlayAttack(float duration,int combo=0)
        {if(action==ActionKind.Death)return;BeginAction(ActionKind.Attack,duration);this.combo=combo;aimedSwing=false;if(heroPixel!=null)heroPixel.StartSwing(false,Vector2.zero,false);}
        /// <summary>Whether the sword rests in the low guard, so the next aimed swing rises.</summary>
        public bool AimGuardLow { get { return heroPixel!=null?heroPixel.GuardLow:guardLow; } }
        /// <summary>The world direction the hero aims; while active a bone rig with a sword arm holds its guard toward it.</summary>
        public void SetAim(Vector2 direction,bool active)
        {if(heroPixel!=null){heroPixel.SetAim(direction,active);return;}if(direction.sqrMagnitude>.0001f)aimDirection=direction.normalized;aimActive=active&&armBone>=0;}
        /// <summary>A sword swing through <paramref name="direction"/>, rising from the low guard or cutting down from the high one.</summary>
        public void PlayAimedAttack(float duration,int combo,Vector2 direction,bool rising)
        {
            PlayAttack(duration,combo);if(action!=ActionKind.Attack)return;
            if(heroPixel!=null){heroPixel.StartSwing(true,direction,rising);return;}
            if(armBone<0)return;
            aimedSwing=true;swingRising=rising;guardLow=!rising;sinceSwing=0;
            if(direction.sqrMagnitude>.0001f)swingDirection=direction.normalized;
        }
        public TowerAttackMode Weapon { get { return weapon; } }
        /// <summary>Puts the sword away for the pistol or the orb, or takes it back up. Rigs without a sword arm keep their look.</summary>
        public void SetWeapon(TowerAttackMode mode)
        {
            if(mode==weapon)return;
            if(heroPixel!=null){weapon=mode;heroPixel.SetWeapon(mode);return;}
            if(armBone<0)return;
            weapon=mode;aimedSwing=false;guardLow=false;recoil=beckon=0;
            foreach(var part in swordParts)part.enabled=mode==TowerAttackMode.Sword;
            if(mode==TowerAttackMode.Gun&&gun==null)BuildGun();
            if(gun!=null)gun.gameObject.SetActive(mode==TowerAttackMode.Gun);
        }
        /// <summary>The world point aimed at; the ranged stances point the arm from the shoulder straight at it.</summary>
        public void SetAimPoint(Vector2 point){aimPoint=point;hasAimPoint=true;if(heroPixel!=null)heroPixel.SetAimPoint(point);}
        /// <summary>The pistol's kick for one shot.</summary>
        public void Recoil(){recoil=1;if(heroPixel!=null)heroPixel.Recoil();}
        /// <summary>The reaching hand snaps open toward the aim as the orb looses a bolt.</summary>
        public void Beckon(){beckon=1;if(heroPixel!=null)heroPixel.Beckon();}
        /// <summary>Where a shot fired now toward <paramref name="point"/> leaves the pistol, with the hero facing <paramref name="facing"/>.</summary>
        public Vector2 MuzzleToward(Vector2 point,int facing)
        {
            if(heroPixel!=null)return heroPixel.MuzzleToward(point,facing);
            if(armBone<0)return (Vector2)transform.position+new Vector2(facing*.6f,.9f);
            facing=facing<0?-1:1;float angle=ShoulderAngle(ShoulderPose(),point,facing);
            // The arm chain as ApplyPose builds it, with the steady pistol stance (no kick) on the last shown pose.
            var hand=BodyMatrix()*BoneLocal(armBone,angle-pose[2]-pose[bodyBone*7+2])*BoneLocal(forearmBone,GunElbow)*BoneLocal(weaponBone,-GunElbow);
            return RigToWorld(hand.MultiplyPoint3x4(GunMuzzle),facing);
        }
        public void PlayBossAttack(int motion,float duration)
        {
            if(!isMonster||monsterKind!=4||action==ActionKind.Death)return;
            bossMotion=Mathf.Clamp(motion,0,2);BeginAction(ActionKind.BossAttack,duration);
        }
        public float BossReleaseTime(int motion,float duration)
        {return Mathf.Max(.08f,duration)*(motion==0?1.2f/2.25f:motion==1?1.3f/3.2f:2.2f/2.75f);}
        public void PlayCast(SkillEffect effect,float duration=.5f)
        {if(action==ActionKind.Death)return;BeginAction(ActionKind.Cast,duration);castEffect=effect;}
        public void PlayDeath(float duration=.6f)
        {if(action==ActionKind.Death)return;BeginAction(ActionKind.Death,duration);}
        public void InterruptAction()
        {
            if(action==ActionKind.Death)return;
            if(pose!=null)Array.Copy(pose,exitPose,pose.Length);
            action=ActionKind.None;actionTime=0;exitTime=.10f;aimedSwing=false;
            if(heroPixel!=null)heroPixel.Interrupt();
        }
        public void SetLocomotion(bool grounded,float verticalVelocity,bool dashing,float horizontalSpeed=0,float horizontalVelocity=float.NaN)
        {this.grounded=grounded;this.verticalVelocity=verticalVelocity;this.dashing=dashing;this.horizontalSpeed=Mathf.Abs(horizontalSpeed);this.horizontalVelocity=float.IsNaN(horizontalVelocity)?horizontalSpeed*Facing:horizontalVelocity;}
        static bool IsSupport(SkillEffect effect){return effect==SkillEffect.HealPulse||effect==SkillEffect.ShieldPulse;}
        public void SetPose(bool moving,bool attacking,bool hurt,int facing,float dt)
        {
            Facing=facing==0?Facing:facing>0?1:-1;dt=Mathf.Max(0,dt);
            bool held=hitFlash>0;if(held){hitFlash=Mathf.Max(0,hitFlash-dt);heldPose+=dt;dt=0;}else if(heldPose>0){dt+=heldPose;heldPose=0;}
            float flash=held?HitFlashGain:TelegraphLit&&action!=ActionKind.Death?TelegraphFlashGain:0;
            Color shown=flash>0?new Color(flash,flash,flash,Tint.a):Tint;shown.a*=BlinkAlpha;ShownColor=shown;
            clock+=dt;
            if(attacking&&!wasAttacking&&action==ActionKind.None)PlayAttack(.4f);
            wasAttacking=attacking;
            if(hurt&&!wasHurt)hurtTime=.20f;wasHurt=hurt;hurtTime=Mathf.Max(0,hurtTime-dt);
            if(staggerHold>0){staggerHold=Mathf.Max(0,staggerHold-dt);hurtTime=Mathf.Max(hurtTime,.10f);}
            if(action!=ActionKind.None)
            {
                actionTime=Mathf.Min(actionDuration,actionTime+dt);
                if(actionTime>=actionDuration&&action!=ActionKind.Death)action=ActionKind.None;
            }
            MotionName=action==ActionKind.Death?"Death":action==ActionKind.BossAttack?"Boss"+BossMotionName:dashing?"Dash":action==ActionKind.Cast?"Cast":action==ActionKind.Attack?"Attack":hurtTime>0?"Hurt":!grounded?(verticalVelocity>0?"Jump":"Fall"):moving?(horizontalSpeed>4.5f?"Run":"Walk"):"Idle";
            if(heroPixel!=null)
            {
                heroPixel.Pose(MotionName,moving,clock,Mathf.Clamp01(actionTime/actionDuration),hurtTime,Facing,RigColor(),grounded,verticalVelocity,horizontalSpeed,horizontalVelocity,dt,
                    action==ActionKind.Cast&&IsSupport(castEffect)?.55f:profile.release,profile.contact);
                return;
            }
            if(mythArt!=null){mythArt.Pose(MotionName,moving,clock,Mathf.Clamp01(actionTime/actionDuration),hurtTime,Facing,shown,grounded,verticalVelocity,horizontalSpeed,horizontalVelocity,dt,action==ActionKind.Cast&&IsSupport(castEffect)?.55f:profile.release);return;}
            if(isMonster)
            {
                monsterRig.Pose(MotionName,moving,Mathf.Clamp01(actionTime/actionDuration),hurtTime,Facing,shown,grounded,
                    new Vector2(horizontalVelocity,verticalVelocity),horizontalSpeed,dt,action==ActionKind.Cast?CastReleaseTime(castEffect,actionDuration)/actionDuration:.48f);
                return;
            }
            if(playerPosed&&dt<=0){ApplyPose();return;}
            // Advance a continuous clip phase. Multiplying total elapsed time by
            // a new speed used to jump to a different stride at the walk/run boundary.
            float targetRate=dashing?2.1f:profile.walkRate*Mathf.Lerp(.48f,1.65f,Mathf.InverseLerp(0,7,horizontalSpeed));
            locomotionRate=Mathf.MoveTowards(locomotionRate,targetRate,dt*4.5f);
            // Facing the aim while running away from it plays the stride backwards.
            bool backpedal=aimActive&&!dashing&&horizontalVelocity*Facing<-.5f;
            if(moving&&grounded)locomotionClock+=dt*locomotionRate*(backpedal?-.85f:1);
            if(grounded&&!previousGrounded&&dt>0)landingAge=0;
            previousGrounded=grounded;landingAge=Mathf.Min(.18f,landingAge+dt);
            movingBlend=Mathf.MoveTowards(movingBlend,moving?1:0,dt*10);
            int state=dashing?3:!grounded?2:moving?1:0;
            if(state!=locomotionState)
            {
                Array.Copy(locomotionPose,locomotionFrom,pose.Length);
                locomotionAge=playerPosed?0:.12f;locomotionState=state;
            }
            locomotionAge+=dt;
            SampleLocomotion(moving);
            // Blend the locomotion layer itself so a start, stop, dash or landing
            // does not replace every joint in a single frame, even during actions.
            BlendFrom(locomotionFrom,Mathf.SmoothStep(0,1,locomotionAge/.12f));
            Array.Copy(pose,locomotionPose,pose.Length);
            float progress=Mathf.Clamp01(actionTime/actionDuration);
            if(action==ActionKind.Death)Sample(deathClip,progress*(deathClip==null?1:deathClip.duration),false);
            else if(action==ActionKind.Attack)
            {
                float phase=ActionPhase(progress,profile.contact);Sample(attackClip,phase*(attackClip==null?1:attackClip.duration),false);
                if(index==0)
                {float weight=Pulse(progress,profile.contact);AddRotation("Body",-3.5f*weight);AddRotation("head",2*weight);AddPosition("Body",1.6f*weight,0);}
                if(index==0&&combo%3!=0)
                {float contact=Pulse(progress,profile.contact);AddRotation("Weapon",(combo%3==1?8:-10)*contact);AddRotation("Rarm",combo%3==1?-3*contact:4*contact);AddPosition("Body",(combo%3==2?.6f:.3f)*contact,0);}
            }
            else if(action==ActionKind.Cast)
            {
                float release=IsSupport(castEffect)?.55f:profile.release;float charge=Pulse(progress,release);
                Sample(IsSupport(castEffect)?idleClip:attackClip,IsSupport(castEffect)?clock*.45f:ActionPhase(progress,release)*(attackClip==null?1:attackClip.duration),IsSupport(castEffect));
                AddRotation("Larm",-24*charge);AddRotation("Rarm",(index==12?12:22)*charge);
                AddRotation("head",-2*charge);AddPosition("Body",0,(IsSupport(castEffect)?1:.5f)*charge);
                if(castEffect==SkillEffect.Meteor||castEffect==SkillEffect.RainArrows)AddRotation("Weapon",-20*charge);
            }
            if(action==ActionKind.Attack||action==ActionKind.Cast)
            {
                float contact=action==ActionKind.Attack?profile.contact:IsSupport(castEffect)?.55f:profile.release;
                float entry=Mathf.SmoothStep(0,1,actionTime/Mathf.Min(.07f,actionDuration*.22f));
                BlendFrom(entryPose,entry);
                float recoveryStart=Mathf.Max(.64f,contact+.12f);
                BlendTo(locomotionPose,Mathf.SmoothStep(0,1,Mathf.InverseLerp(recoveryStart,1,progress)));
                BlendMovingLegs(!grounded?1:movingBlend*.88f);
            }
            if(exitTime>0)
            {exitTime=Mathf.Max(0,exitTime-dt);BlendFrom(exitPose,Mathf.SmoothStep(0,1,1-exitTime/.10f));}
            if(hurtTime>0&&action!=ActionKind.Death)
            {float age=.20f-hurtTime;float recoil=Mathf.SmoothStep(0,1,age/.04f)*Mathf.SmoothStep(0,1,hurtTime/.16f);AddPosition("Body",-2*recoil,0);AddRotation("Body",8*recoil);AddRotation("head",-5*recoil);}
            if(grounded&&landingAge<.18f&&action!=ActionKind.Death)
            {float compression=Mathf.Sin(Mathf.PI*landingAge/.18f);AddPosition("Body",0,-1.2f*compression);AddRotation("Rleg",5*compression);AddRotation("Lleg",-4*compression);}
            ApplyAimLayer(dt);
            ApplyPose();playerPosed=true;
        }
        /// <summary>A world direction as a rig angle on the facing side (0 ahead, 90 up); aims behind the hero count as ahead.</summary>
        float RigAngle(Vector2 direction)
        {return Mathf.Clamp(Mathf.Atan2(direction.y,Mathf.Max(direction.x*Facing,.05f))*Mathf.Rad2Deg,-80,90);}
        // The resting guards turned part way toward the aim: high holds the blade raised (the idle pose when aiming
        // straight ahead), low holds it forward and down, its tip kept off the floor while standing.
        void Guard(float aim,float low,out float arm,out float blade)
        {
            float highArm=-18+aim*.55f,highBlade=64+aim*.6f,lowArm=-62+aim*.5f,lowBlade=-12+aim*.65f;
            arm=Mathf.Lerp(highArm,lowArm,low);blade=OffFloor(Mathf.Lerp(highBlade,lowBlade,low),arm);
        }
        // While standing, a blade that would dip through the floor lies along it instead: forward ones level off ahead,
        // ones held behind level off behind.
        float OffFloor(float blade,float arm)
        {
            if(!grounded)return blade;
            float hand=ShoulderHeight+ArmLength*Mathf.Sin(arm*Mathf.Deg2Rad),lowest=Mathf.Asin(Mathf.Clamp((-1-hand)/BladeLength,-1,1))*Mathf.Rad2Deg;
            float behind=Mathf.Repeat(blade+180,360)-180;
            if(behind>=-90)return Mathf.Max(blade,blade-behind+lowest);
            return Mathf.Min(blade,blade-behind-180-lowest);
        }
        // An aimed swing in four beats: wind up past the starting guard, accelerate through the aim at contact, follow
        // through past it, then settle into the opposite guard at the live aim. A downward cut winds up overhead; a
        // rising one draws the blade back low behind the hero.
        void Swing(float progress,float aim,float liveAim,out float arm,out float blade)
        {
            float side=swingRising?-1:1,contact=profile.contact;
            float startArm,startBlade,endArm,endBlade;
            Guard(aim,swingRising?1:0,out startArm,out startBlade);Guard(liveAim,swingRising?0:1,out endArm,out endBlade);
            float windArm=swingRising?aim-112:aim+80,windBlade=OffFloor(swingRising?aim-160:aim+125,windArm);
            float hitArm=aim+side*6,hitBlade=aim,followArm=aim-side*72,followBlade=OffFloor(aim-side*88,followArm);
            if(progress<.26f){float t=Mathf.SmoothStep(0,1,progress/.26f);arm=Mathf.Lerp(startArm,windArm,t);blade=Mathf.Lerp(startBlade,windBlade,t);}
            else if(progress<contact){float t=(progress-.26f)/(contact-.26f);t*=t;arm=Mathf.Lerp(windArm,hitArm,t);blade=Mathf.Lerp(windBlade,hitBlade,t);}
            else if(progress<.6f){float t=(progress-contact)/(.6f-contact);t=1-(1-t)*(1-t);arm=Mathf.Lerp(hitArm,followArm,t);blade=Mathf.Lerp(hitBlade,followBlade,t);}
            else{float t=Mathf.SmoothStep(0,1,(progress-.6f)/.4f);arm=Mathf.Lerp(followArm,endArm,t);blade=Mathf.Lerp(followBlade,endBlade,t);}
        }
        void ApplyAimLayer(float dt)
        {
            if(armBone<0)return;
            if(weapon!=TowerAttackMode.Sword){ApplyRangedLayer(dt);return;}
            bool swinging=action==ActionKind.Attack&&aimedSwing;
            aimWeight=Mathf.MoveTowards(aimWeight,aimActive&&action!=ActionKind.Cast&&action!=ActionKind.Death?1:0,dt*8);
            // A low guard relaxes back to the raised one when the sword has rested a moment.
            if(!swinging){sinceSwing+=dt;if(guardLow&&sinceSwing>.9f)guardLow=false;}
            guardBlend=swinging?(guardLow?1:0):Mathf.MoveTowards(guardBlend,guardLow?1:0,dt*3.5f);
            if(aimWeight<=0)return;
            float liveAim=RigAngle(aimDirection),arm,blade;
            if(swinging)
            {
                float progress=Mathf.Clamp01(actionTime/actionDuration),aim=RigAngle(swingDirection);
                Swing(progress,aim,liveAim,out arm,out blade);shownArm=arm;shownBlade=blade;
                // The body leans into a cut aimed up or down.
                pose[bodyBone*7+2]+=Mathf.Clamp(aim,-60,80)*.1f*Pulse(progress,profile.contact)*aimWeight;
            }
            else
            {
                Guard(liveAim,guardBlend,out arm,out blade);blade+=Mathf.Sin(clock*9.4f)*2;
                // Ease toward the guard so an interrupted swing or a quick flick of the aim does not snap the arm.
                float ease=1-Mathf.Exp(-dt*18);shownArm+=(arm-shownArm)*ease;shownBlade+=(blade-shownBlade)*ease;
                arm=shownArm;blade=shownBlade;
            }
            float body=pose[2]+pose[bodyBone*7+2];
            pose[armBone*7+2]=BlendValue(pose[armBone*7+2],arm-body,2,aimWeight);
            pose[forearmBone*7+2]=BlendValue(pose[forearmBone*7+2],ElbowBend,2,aimWeight);
            pose[weaponBone*7+2]=BlendValue(pose[weaponBone*7+2],blade-arm-ElbowBend,2,aimWeight);
            // A swing that carries the long blade under the hand while standing turns it toward or away from the viewer:
            // the blade foreshortens so its tip stays on the floor instead of cutting through it.
            if(grounded)
            {
                float rad=body*Mathf.Deg2Rad,shoulder=pose[1]+pose[bodyBone*7+1]+pose[armBone*7]*Mathf.Sin(rad)+pose[armBone*7+1]*Mathf.Cos(rad);
                float hand=shoulder+pose[forearmBone*7]*Mathf.Sin(arm*Mathf.Deg2Rad)+pose[weaponBone*7]*Mathf.Sin((arm+ElbowBend)*Mathf.Deg2Rad);
                float drop=-Mathf.Sin(blade*Mathf.Deg2Rad)*BladeLength;
                if(drop>hand+1)pose[weaponBone*7+3]*=Mathf.Lerp(1,Mathf.Clamp((hand+1)/drop,.22f,1),aimWeight);
            }
            if(headBone>=0)pose[headBone*7+2]+=Mathf.Clamp(liveAim*.22f,-10,14)*aimWeight;
        }
        // The ranged stances hold the arm on the aim through every locomotion state, fixtures included (they aim along the
        // facing). The pistol kicks up with the forearm and the body rocks back a touch; the reaching hand bends at rest and
        // snaps straight toward the aim each time the orb fires.
        void ApplyRangedLayer(float dt)
        {
            aimWeight=Mathf.MoveTowards(aimWeight,action!=ActionKind.Cast&&action!=ActionKind.Death?1:0,dt*8);
            recoil=Mathf.MoveTowards(recoil,0,dt/.12f);beckon=Mathf.MoveTowards(beckon,0,dt/.3f);
            if(aimWeight<=0)return;
            bool pistol=weapon==TowerAttackMode.Gun;float kick=recoil*recoil;
            if(pistol)pose[bodyBone*7+2]+=2.5f*kick*aimWeight;
            Vector2 shoulder=ShoulderPose();
            float aim=hasAimPoint?ShoulderAngle(shoulder,aimPoint,Facing):RigAngle(aimDirection),arm,elbow,hand;
            if(pistol){arm=aim+7*kick;elbow=GunElbow-9*kick;hand=aim+18*kick;}
            else{float reach=Mathf.SmoothStep(0,1,beckon);arm=aim-9+7*reach;elbow=Mathf.Lerp(ReachElbow,-2,reach);hand=arm+elbow;}
            float body=pose[2]+pose[bodyBone*7+2];
            pose[armBone*7+2]=BlendValue(pose[armBone*7+2],arm-body,2,aimWeight);
            pose[forearmBone*7+2]=BlendValue(pose[forearmBone*7+2],elbow,2,aimWeight);
            pose[weaponBone*7+2]=BlendValue(pose[weaponBone*7+2],hand-arm-elbow,2,aimWeight);
            pose[weaponBone*7+3]=Mathf.Lerp(pose[weaponBone*7+3],1,aimWeight);pose[weaponBone*7+4]=Mathf.Lerp(pose[weaponBone*7+4],1,aimWeight);
            if(headBone>=0)pose[headBone*7+2]+=Mathf.Clamp(aim*.22f,-10,14)*aimWeight;
        }
        /// <summary>One bone's local matrix as ApplyPose builds it, turned to <paramref name="rotation"/>.</summary>
        Matrix4x4 BoneLocal(int bone,float rotation)
        {
            int p=bone*7;float rx=(rotation+pose[p+5])*Mathf.Deg2Rad,ry=(rotation+90+pose[p+6])*Mathf.Deg2Rad;
            var local=Matrix4x4.identity;local.m00=Mathf.Cos(rx)*pose[p+3];local.m10=Mathf.Sin(rx)*pose[p+3];local.m01=Mathf.Cos(ry)*pose[p+4];local.m11=Mathf.Sin(ry)*pose[p+4];local.m03=pose[p];local.m13=pose[p+1];
            return local;
        }
        /// <summary>The body bone in rig pixels (unflipped) through its parents, from the pose being built or the last one shown.</summary>
        Matrix4x4 BodyMatrix()
        {
            var matrix=Matrix4x4.identity;
            for(int bone=bodyBone;bone>=0;bone=data.bones[bone].parent)matrix=BoneLocal(bone,pose[bone*7+2])*matrix;
            return matrix;
        }
        Vector2 ShoulderPose(){return BodyMatrix().MultiplyPoint3x4(new Vector3(pose[armBone*7],pose[armBone*7+1],0));}
        /// <summary>The rig angle from the shoulder to a world point; a point behind or right at the shoulder counts as ahead.</summary>
        float ShoulderAngle(Vector2 shoulder,Vector2 point,int facing)
        {
            Vector2 to=WorldToRig(point,facing)-shoulder;
            float angle=to.sqrMagnitude>36?Mathf.Atan2(to.y,Mathf.Max(to.x,to.magnitude*.05f))*Mathf.Rad2Deg:RigAngle(aimDirection);
            return Mathf.Clamp(angle,-84,86);
        }
        Vector2 WorldToRig(Vector2 point,int facing)
        {Vector3 o=rig.position;float s=Mathf.Abs(rig.lossyScale.y);return new Vector2((point.x-o.x)/s*facing,(point.y-o.y)/s);}
        Vector2 RigToWorld(Vector2 local,int facing)
        {Vector3 o=rig.position;float s=Mathf.Abs(rig.lossyScale.y);return new Vector2(o.x+local.x*s*facing,o.y+local.y*s);}
        // The pistol rides on the weapon bone at one sprite pixel per art pixel, drawn where the sword was (behind the hand).
        void BuildGun()
        {
            if(gunSprite==null)gunSprite=PaintGun();
            var go=new GameObject("Relic pistol");go.transform.SetParent(bones[weaponBone],false);go.transform.localPosition=GunGrip;
            gun=go.AddComponent<SpriteRenderer>();gun.sprite=gunSprite;gun.sortingOrder=swordParts.Count>0?swordParts[0].sortingOrder:32;
        }
        // A brass-bound relic revolver, 20 x 12 art pixels with the barrel to the right and a rune glowing in the frame.
        static readonly string[] GunRows={
            "..oo................",
            ".oyBo............o..",
            ".oBbBooooooooooooloo",
            ".oBybBlllllllllllhho",
            "..oBdssssssssssssslo",
            "..odcdoddddddddddddo",
            "..oddBoooooooooooooo",
            ".oWWdoBBo...........",
            ".oWwWo.oBo..........",
            "oWWwo...oo..........",
            "oWwWo...............",
            "ooooo..............."};
        static Sprite PaintGun()
        {
            var palette=new Dictionary<char,Color32>{{'o',new Color32(24,18,30,255)},{'d',new Color32(58,62,78,255)},{'s',new Color32(112,120,140,255)},
                {'l',new Color32(178,186,204,255)},{'h',new Color32(236,240,248,255)},{'B',new Color32(128,86,40,255)},{'b',new Color32(206,158,72,255)},
                {'y',new Color32(244,214,128,255)},{'w',new Color32(82,48,34,255)},{'W',new Color32(128,78,50,255)},{'c',new Color32(110,236,255,255)}};
            int width=GunRows[0].Length,height=GunRows.Length;var pixels=new Color32[width*height];
            for(int row=0;row<height;row++)for(int x=0;x<width;x++)
            {Color32 c;pixels[(height-1-row)*width+x]=palette.TryGetValue(GunRows[row][x],out c)?c:new Color32(0,0,0,0);}
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name="Relic pistol",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);texture.Apply(false,true);
            // Pivot on the grip, three and a half pixels up the handle.
            return Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(2.5f/width,3.5f/height),1);
        }
        void SampleLocomotion(bool moving)
        {
            if(dashing)
            {Sample(runClip,locomotionClock,true);AddRotation("Body",-14);AddRotation("Larm",32);AddRotation("Rarm",-24);}
            else if(!grounded)
            {
                float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-1.5f,1.5f,verticalVelocity));
                Sample(idleClip,clock*.4f,true);AddRotation("Rleg",Mathf.Lerp(12,-27,rise));AddRotation("Lleg",Mathf.Lerp(-8,23,rise));
                AddRotation("Larm",Mathf.Lerp(-35,-20,rise));AddRotation("Rarm",Mathf.Lerp(30,18,rise));AddRotation("Body",Mathf.Lerp(5,-4,rise));
            }
            else
            {
                Sample(moving?runClip:idleClip,moving?locomotionClock:clock*profile.idleRate,true);
                if(moving){float lean=Mathf.InverseLerp(1.5f,7,horizontalSpeed);AddRotation("Body",-3.5f*lean);AddRotation("head",1.5f*lean);}
            }
        }
        static float BlendValue(float from,float to,int channel,float weight)
        {return channel==2||channel==5||channel==6?from+Mathf.DeltaAngle(from,to)*weight:Mathf.Lerp(from,to,weight);}
        void BlendFrom(float[] from,float weight)
        {for(int i=0;i<pose.Length;i++)pose[i]=BlendValue(from[i],pose[i],i%7,weight);}
        void BlendTo(float[] to,float weight)
        {for(int i=0;i<pose.Length;i++)pose[i]=BlendValue(pose[i],to[i],i%7,weight);}
        void BlendMovingLegs(float weight)
        {
            if(weight<=0)return;
            for(int bone=0;bone<bones.Length;bone++)
            {
                int p=bone*7;
                if(lowerBody[bone])for(int channel=0;channel<7;channel++)pose[p+channel]=BlendValue(pose[p+channel],locomotionPose[p+channel],channel,weight);
                else if(data.bones[bone].name=="Body"||data.bones[bone].parent<0)
                {
                    pose[p]=Mathf.Lerp(pose[p],locomotionPose[p],weight*.85f);pose[p+1]=Mathf.Lerp(pose[p+1],locomotionPose[p+1],weight*.85f);
                    pose[p+2]=BlendValue(pose[p+2],locomotionPose[p+2],2,weight*.3f);
                }
            }
        }
        static float ActionPhase(float p,float contact)
        {return p<contact?(p/contact)*.48f:.48f+(p-contact)/(1-contact)*.52f;}
        static float Pulse(float p,float peak)
        {if(p<peak)return Mathf.SmoothStep(0,1,p/peak);return Mathf.SmoothStep(1,0,(p-peak)/(1-peak));}
        void Sample(ClipData clip,float time,bool loop)
        {
            if(clip==null)
            {
                for(int i=0;i<bones.Length;i++){var b=data.bones[i];int q=i*7;pose[q]=b.x;pose[q+1]=b.y;pose[q+2]=b.rotation;pose[q+3]=b.scaleX;pose[q+4]=b.scaleY;pose[q+5]=b.shearX;pose[q+6]=b.shearY;}return;
            }
            time=loop&&clip.duration>0?Mathf.Repeat(time,clip.duration):Mathf.Clamp(time,0,clip.duration);
            int a=Mathf.Clamp(Mathf.FloorToInt(time*library.sampleRate),0,clip.frames.Length-1),bIndex=Mathf.Min(a+1,clip.frames.Length-1);
            var first=clip.frames[a];var next=clip.frames[bIndex];float mix=next.time>first.time?(time-first.time)/(next.time-first.time):0;
            // Sampled source angles are continuous and can intentionally turn
            // more than 180 degrees between frames (the ninja's spinning arm).
            // Shortest-angle blending belongs to transitions, not source clips.
            for(int i=0;i<pose.Length;i++)pose[i]=Mathf.Lerp(first.pose[i],next.pose[i],mix);
        }
        int Bone(string name){for(int i=0;i<data.bones.Length;i++)if(data.bones[i].name==name)return i;return -1;}
        void AddRotation(string name,float angle){int i=Bone(name);if(i>=0)pose[i*7+2]+=angle;}
        void AddPosition(string name,float x,float y){int i=Bone(name);if(i>=0){pose[i*7]+=x;pose[i*7+1]+=y;}}
        void ApplyPose()
        {
            // Keep the moving rig on its physical root. Independent whole-pixel
            // snapping fought the continuous camera and produced visible judder.
            rig.localPosition=new Vector3(0,Mathf.Round(data.groundOffset)*PixelScale,0);
            rig.localScale=new Vector3(Facing*PixelScale,PixelScale,PixelScale);
            for(int i=0;i<bones.Length;i++)
            {
                int p=i*7;float rx=(pose[p+2]+pose[p+5])*Mathf.Deg2Rad,ry=(pose[p+2]+90+pose[p+6])*Mathf.Deg2Rad;
                var local=Matrix4x4.identity;local.m00=Mathf.Cos(rx)*pose[p+3];local.m10=Mathf.Sin(rx)*pose[p+3];local.m01=Mathf.Cos(ry)*pose[p+4];local.m11=Mathf.Sin(ry)*pose[p+4];local.m03=pose[p];local.m13=pose[p+1];
                boneMatrices[i]=data.bones[i].parent<0?local:boneMatrices[data.bones[i].parent]*local;
                bones[i].localPosition=new Vector3(pose[p],pose[p+1],0);bones[i].localRotation=Quaternion.Euler(0,0,pose[p+2]);bones[i].localScale=new Vector3(pose[p+3],pose[p+4],1);
            }
            Color color=RigColor();tintBlock.SetColor("_Color",color);
            if(gun!=null)gun.color=color;
            foreach(var part in meshes)
            {
                int influence=0;var source=part.source;
                for(int v=0;v<part.vertices.Length;v++)
                {
                    Vector3 position=Vector3.zero;
                    for(int j=0;j<source.counts[v];j++,influence++)
                    {int q=influence*3;position+=boneMatrices[source.bindBones[influence]].MultiplyPoint3x4(new Vector3(source.bindValues[q],source.bindValues[q+1],0))*source.bindValues[q+2];}
                    part.vertices[v]=position;
                }
                part.mesh.vertices=part.vertices;part.renderer.SetPropertyBlock(tintBlock);
            }
        }
        // The player's colour (the hero takes no hit flash or telegraph): Tint reddened while hurt, the invulnerability
        // blink and the fade on death.
        Color RigColor()
        {
            Color color=Tint;if(hurtTime>.10f)color=Color.Lerp(color,new Color(1,.35f,.32f,color.a),.7f);
            color.a*=BlinkAlpha;
            if(action==ActionKind.Death)color.a*=1-Mathf.Clamp01((actionTime/actionDuration-.65f)/.35f)*.8f;
            ShownColor=color;return color;
        }
        public void Tick(bool moving,bool attacking,bool hurt,float dt){SetPose(moving,attacking,hurt,Facing,dt);}
        void OnDestroy(){foreach(var part in meshes)if(part.mesh!=null)Destroy(part.mesh);}

        /// <summary>A cached camera portrait of the unchanged original pixel meshes, with Point filtering.</summary>
        public static Texture2D Portrait(int artIndex)
        {
            if(TowerMythArt.IsMythIndex(artIndex))return TowerMythArt.Portrait(artIndex);
            artIndex=Mathf.Clamp(artIndex,0,16);if(artIndex==15)artIndex=0;
            if(artIndex==0)return TowerHeroPixelArt.Portrait();
            Texture2D texture;if(portraits.TryGetValue(artIndex,out texture))return texture;
            var root=new GameObject("Original pixel portrait temporary"){hideFlags=HideFlags.HideAndDontSave};root.transform.position=new Vector3(10000,10000,0);
            var art=Create(root.transform,artIndex);foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
            var cameraObject=new GameObject("Pixel portrait camera"){hideFlags=HideFlags.HideAndDontSave};var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            camera.orthographic=true;camera.orthographicSize=1;camera.aspect=1;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<31;camera.allowHDR=false;camera.allowMSAA=false;
            // A square of 64 original pixels focuses on the face and upper body; long weapons/wings can extend beyond a portrait.
            Vector3 focus=new Vector3(0,32,0);
            foreach(var part in art.data.parts)if(part.name=="head")
            {focus=art.boneMatrices[part.bone].MultiplyPoint3x4(new Vector3(part.x,part.y,0));focus.y+=art.data.groundOffset-6;break;}
            camera.transform.position=root.transform.position+new Vector3(Mathf.Round(focus.x)*PixelScale,Mathf.Round(focus.y)*PixelScale,-10);
            var target=new RenderTexture(128,128,0,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point,antiAliasing=1};target.Create();camera.targetTexture=target;
            var previous=RenderTexture.active;camera.Render();RenderTexture.active=target;
            texture=new Texture2D(128,128,TextureFormat.RGBA32,false){name="Original pixel portrait "+artIndex,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};texture.ReadPixels(new Rect(0,0,128,128),0,0,false);texture.Apply(false,false);
            RenderTexture.active=previous;camera.targetTexture=null;root.SetActive(false);target.Release();Destroy(target);Destroy(cameraObject);Destroy(root);
            portraits.Add(artIndex,texture);return texture;
        }
    }
}

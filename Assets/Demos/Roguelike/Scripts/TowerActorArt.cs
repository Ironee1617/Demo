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
        public Color Tint { get; set; }=Color.white;
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
            SetPose(false,false,false,1,0);
        }
        ClipData Clip(string name){foreach(var c in data.clips)if(c.name==name)return c;return null;}
        void BeginAction(ActionKind kind,float duration)
        {
            if(pose!=null)Array.Copy(pose,entryPose,pose.Length);
            action=kind;actionTime=0;actionDuration=Mathf.Max(.08f,duration);exitTime=0;
        }
        public void PlayAttack(float duration,int combo=0)
        {if(action==ActionKind.Death)return;BeginAction(ActionKind.Attack,duration);this.combo=combo;}
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
            action=ActionKind.None;actionTime=0;exitTime=.10f;
        }
        public void SetLocomotion(bool grounded,float verticalVelocity,bool dashing,float horizontalSpeed=0,float horizontalVelocity=float.NaN)
        {this.grounded=grounded;this.verticalVelocity=verticalVelocity;this.dashing=dashing;this.horizontalSpeed=Mathf.Abs(horizontalSpeed);this.horizontalVelocity=float.IsNaN(horizontalVelocity)?horizontalSpeed*Facing:horizontalVelocity;}
        static bool IsSupport(SkillEffect effect){return effect==SkillEffect.HealPulse||effect==SkillEffect.ShieldPulse;}
        public void SetPose(bool moving,bool attacking,bool hurt,int facing,float dt)
        {
            Facing=facing==0?Facing:facing>0?1:-1;dt=Mathf.Max(0,dt);clock+=dt;
            if(attacking&&!wasAttacking&&action==ActionKind.None)PlayAttack(.4f);
            wasAttacking=attacking;
            if(hurt&&!wasHurt)hurtTime=.20f;wasHurt=hurt;hurtTime=Mathf.Max(0,hurtTime-dt);
            if(action!=ActionKind.None)
            {
                actionTime=Mathf.Min(actionDuration,actionTime+dt);
                if(actionTime>=actionDuration&&action!=ActionKind.Death)action=ActionKind.None;
            }
            MotionName=action==ActionKind.Death?"Death":action==ActionKind.BossAttack?"Boss"+BossMotionName:dashing?"Dash":action==ActionKind.Cast?"Cast":action==ActionKind.Attack?"Attack":hurtTime>0?"Hurt":!grounded?(verticalVelocity>0?"Jump":"Fall"):moving?(horizontalSpeed>4.5f?"Run":"Walk"):"Idle";
            if(mythArt!=null){mythArt.Pose(MotionName,moving,clock,Mathf.Clamp01(actionTime/actionDuration),hurtTime,Facing,Tint,grounded,verticalVelocity,horizontalSpeed,horizontalVelocity,dt,action==ActionKind.Cast&&IsSupport(castEffect)?.55f:profile.release);return;}
            if(isMonster)
            {
                monsterRig.Pose(MotionName,moving,Mathf.Clamp01(actionTime/actionDuration),hurtTime,Facing,Tint,grounded,
                    new Vector2(horizontalVelocity,verticalVelocity),horizontalSpeed,dt,action==ActionKind.Cast?CastReleaseTime(castEffect,actionDuration)/actionDuration:.48f);
                return;
            }
            if(playerPosed&&dt<=0){ApplyPose();return;}
            // Advance a continuous clip phase. Multiplying total elapsed time by
            // a new speed used to jump to a different stride at the walk/run boundary.
            float targetRate=dashing?2.1f:profile.walkRate*Mathf.Lerp(.48f,1.65f,Mathf.InverseLerp(0,7,horizontalSpeed));
            locomotionRate=Mathf.MoveTowards(locomotionRate,targetRate,dt*4.5f);
            if(moving&&grounded)locomotionClock+=dt*locomotionRate;
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
            ApplyPose();playerPosed=true;
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
            time=loop&&clip.duration>0?time%clip.duration:Mathf.Clamp(time,0,clip.duration);
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
            Color color=Tint;if(hurtTime>.10f)color=Color.Lerp(color,new Color(1,.35f,.32f,color.a),.7f);
            if(action==ActionKind.Death)color.a*=1-Mathf.Clamp01((actionTime/actionDuration-.65f)/.35f)*.8f;
            tintBlock.SetColor("_Color",color);
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
        public void Tick(bool moving,bool attacking,bool hurt,float dt){SetPose(moving,attacking,hurt,Facing,dt);}
        void OnDestroy(){foreach(var part in meshes)if(part.mesh!=null)Destroy(part.mesh);}

        /// <summary>A cached camera portrait of the unchanged original pixel meshes, with Point filtering.</summary>
        public static Texture2D Portrait(int artIndex)
        {
            if(TowerMythArt.IsMythIndex(artIndex))return TowerMythArt.Portrait(artIndex);
            artIndex=Mathf.Clamp(artIndex,0,16);if(artIndex==15)artIndex=0;
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

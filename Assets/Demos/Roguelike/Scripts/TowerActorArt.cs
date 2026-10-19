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
        Matrix4x4[] boneMatrices; float[] pose,locomotionPose,entryPose,exitPose; bool[] lowerBody;
        ClipData idleClip,runClip,attackClip,deathClip;
        SpriteRenderer monsterRenderer; Vector3 monsterRest,monsterEntryPosition,monsterEntryScale,monsterExitPosition,monsterExitScale; float monsterScale,monsterWidth,monsterHeight,monsterEntryAngle,monsterExitAngle;
        static readonly string[] BossMotions={"CrownVolley","TwinSpiral","LeapingSlam"};
        static readonly float[] SlamTimes={0,.32f,.43f,.53f,.68f,.79f,.80f,.86f,1};
        static readonly float[] SlamWidth={1,1.1f,1.18f,.88f,.94f,.92f,1.26f,1.18f,1};
        static readonly float[] SlamHeight={1,.86f,.72f,1.14f,1.04f,1.12f,.66f,.80f,1};
        static readonly float[] SlamAngle={0,-3,-5,4,7,0,-2,1,0};
        int bossMotion=-1;
        int index,combo,monsterKind; bool isMonster,grounded=true,dashing,wasAttacking,wasHurt;
        float clock,actionTime,actionDuration=.4f,hurtTime,verticalVelocity,horizontalSpeed,movingBlend,exitTime;
        ActionKind action; SkillEffect castEffect;
        public Color Tint { get; set; }=Color.white;
        public int Facing { get; set; }=1;
        public int ArtIndex { get { return index; } }
        public string ProfileName { get { return isMonster?(monsterKind==4?"CrownSlimeKing":monsterKind==3?"TowerOwlBat":"TowerGuardian"):profile.name; } }
        public string MotionName { get; private set; }="Idle";
        public string BossMotionName { get { return bossMotion>=0?BossMotions[bossMotion]:"Idle"; } }
        public float BossVisualHeight { get { return isMonster&&monsterKind==4?monsterHeight*monsterScale*Mathf.Abs(transform.lossyScale.y):0; } }
        public float RenderedHeight
        {
            get
            {
                if(monsterRenderer==null)return 0;
                float top=transform.position.y;var visual=monsterRenderer.transform;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
                    top=Mathf.Max(top,visual.TransformPoint(new Vector3(x*monsterWidth*.5f,y*monsterHeight*.5f,0)).y);
                return top-transform.position.y;
            }
        }
        public string CurrentMotion { get { return MotionName; } }
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
            tintBlock=new MaterialPropertyBlock();index=Mathf.Clamp(artIndex,0,16);isMonster=monster;profile=Profiles[index];
            rig=new GameObject("Original bone rig").transform;rig.SetParent(transform,false);
            if(monster)
            {
                int kind=((artIndex%5)+5)%5;monsterKind=kind;var go=new GameObject("Original monster sprite");go.transform.SetParent(rig,false);
                monsterRenderer=go.AddComponent<SpriteRenderer>();monsterRenderer.sprite=Resources.Load<Sprite>("Tower/Monsters/monster_"+kind);monsterRenderer.sortingOrder=30;
                monsterWidth=kind==0?43:kind==1?47:kind==4?170:50;monsterHeight=kind==0?30:kind==1?33:kind==2?45:kind==4?130:50;
                monsterScale=PixelScale*(kind==4?50f/130f:1);monsterRest=new Vector3(0,monsterHeight*monsterScale*.5f,0);
                monsterRenderer.transform.localScale=Vector3.one*monsterScale;monsterRenderer.transform.localPosition=monsterRest;return;
            }
            data=FindData(index);bones=new Transform[data.bones.Length];boneMatrices=new Matrix4x4[bones.Length];pose=new float[bones.Length*7];locomotionPose=new float[pose.Length];entryPose=new float[pose.Length];exitPose=new float[pose.Length];lowerBody=new bool[bones.Length];
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
            if(monsterRenderer!=null){monsterEntryPosition=monsterRenderer.transform.localPosition;monsterEntryScale=monsterRenderer.transform.localScale;monsterEntryAngle=monsterRenderer.transform.localEulerAngles.z;}
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
            if(monsterRenderer!=null){monsterExitPosition=monsterRenderer.transform.localPosition;monsterExitScale=monsterRenderer.transform.localScale;monsterExitAngle=monsterRenderer.transform.localEulerAngles.z;}
            action=ActionKind.None;actionTime=0;exitTime=.10f;
        }
        public void SetLocomotion(bool grounded,float verticalVelocity,bool dashing,float horizontalSpeed=0)
        {this.grounded=grounded;this.verticalVelocity=verticalVelocity;this.dashing=dashing;this.horizontalSpeed=Mathf.Abs(horizontalSpeed);}
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
            if(isMonster){PoseMonster(moving,dt);return;}
            movingBlend=Mathf.MoveTowards(movingBlend,moving?1:0,dt*10);
            SampleLocomotion(moving);Array.Copy(pose,locomotionPose,pose.Length);
            float progress=Mathf.Clamp01(actionTime/actionDuration);
            if(action==ActionKind.Death)Sample(deathClip,progress*(deathClip==null?1:deathClip.duration),false);
            else if(action==ActionKind.Attack)
            {
                float phase=ActionPhase(progress,profile.contact);Sample(attackClip,phase*(attackClip==null?1:attackClip.duration),false);
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
            {float recoil=hurtTime/.20f;AddPosition("Body",-2*recoil,0);AddRotation("Body",8*recoil);AddRotation("head",-5*recoil);}
            ApplyPose();
        }
        void SampleLocomotion(bool moving)
        {
            if(dashing)
            {Sample(runClip,clock*2.1f,true);AddRotation("Body",-14);AddRotation("Larm",32);AddRotation("Rarm",-24);}
            else if(!grounded)
            {Sample(idleClip,clock*.4f,true);AddRotation("Rleg",verticalVelocity>0?-27:12);AddRotation("Lleg",verticalVelocity>0?23:-8);AddRotation("Larm",verticalVelocity>0?-20:-35);AddRotation("Rarm",verticalVelocity>0?18:30);AddRotation("Body",verticalVelocity>0?-4:5);}
            else Sample(moving?runClip:idleClip,clock*(moving?profile.walkRate*(horizontalSpeed>4.5f?1.35f:.8f):profile.idleRate),true);
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
            for(int i=0;i<pose.Length;i++)pose[i]=Mathf.Lerp(first.pose[i],next.pose[i],mix);
        }
        int Bone(string name){for(int i=0;i<data.bones.Length;i++)if(data.bones[i].name==name)return i;return -1;}
        void AddRotation(string name,float angle){int i=Bone(name);if(i>=0)pose[i*7+2]+=angle;}
        void AddPosition(string name,float x,float y){int i=Bone(name);if(i>=0){pose[i*7]+=x;pose[i*7+1]+=y;}}
        void ApplyPose()
        {
            var world=transform.position;var snapped=new Vector3(Mathf.Round(world.x/PixelScale)*PixelScale,Mathf.Round(world.y/PixelScale)*PixelScale,world.z);
            var correction=transform.InverseTransformVector(snapped-world);rig.localPosition=new Vector3(correction.x,Mathf.Round(data.groundOffset)*PixelScale+correction.y,0);
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
        void PoseMonster(bool moving,float dt)
        {
            rig.localScale=new Vector3(Facing,1,1);float p=Mathf.Clamp01(actionTime/actionDuration);
            if(monsterKind==4&&action!=ActionKind.Death){PoseBoss(p,dt);return;}
            float cycle=moving?Mathf.Abs(Mathf.Sin(clock*7)):0,swing=action==ActionKind.Attack?Pulse(p,.48f):0;
            float bob=monsterKind==3?Mathf.Round(Mathf.Sin(clock*4)*1.5f):Mathf.Round(cycle*3);
            var position=monsterRest+new Vector3(swing*(monsterKind==3?.10f:.2f),bob*PixelScale,0);
            var scale=new Vector3(monsterScale*(1+swing*.18f),monsterScale*(1-swing*.12f),1);
            float bank=monsterKind==3?(moving?-4:0)+Mathf.Sin(clock*4)*2:0;
            ApplyMonsterPose(position,scale,action==ActionKind.Death?Mathf.Lerp(0,-80,p):bank-swing*8,dt);
        }
        void PoseBoss(float p,float dt)
        {
            float width=1,height=1,angle=0,hop=0,recoil=0;
            if(action==ActionKind.BossAttack)
            {
                if(bossMotion==0)
                {
                    // Three visible flares meet the actual three bullet releases.
                    float charge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,1.2f/2.25f,p))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.89f,1,p)));
                    float burst=BossBurst(p,1.2f/2.25f)+BossBurst(p,1.6f/2.25f)+BossBurst(p,2f/2.25f);
                    width=1+.08f*charge-.14f*burst;height=1-.08f*charge+.20f*burst;angle=-4*charge+7*burst;recoil=-.06f*burst;
                }
                else if(bossMotion==1)
                {
                    float activity=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.22f,p))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.90f,1,p)));
                    // Visual levitation leaves the actor's ground targeting qualification unchanged.
                    angle=720*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,.92f,p));
                    float release=BossBurst(p,1.3f/3.2f);
                    width=1+.04f*Mathf.Cos(angle*Mathf.Deg2Rad)*activity;height=1+.08f*activity+.08f*release;hop=.26f*activity;
                }
                else
                {
                    // The AI moves the actor through the jump arc; this sprite only crouches, stretches and lands.
                    width=BossCurve(p,SlamWidth);height=BossCurve(p,SlamHeight);angle=BossCurve(p,SlamAngle);
                }
            }
            float radians=angle*Mathf.Deg2Rad;
            float halfHeight=.5f*monsterScale*(Mathf.Abs(Mathf.Sin(radians))*monsterWidth*width+Mathf.Abs(Mathf.Cos(radians))*monsterHeight*height);
            ApplyMonsterPose(new Vector3(recoil,halfHeight+hop,0),new Vector3(monsterScale*width,monsterScale*height,1),angle,dt);
        }
        static float BossBurst(float p,float peak)
        {return p<peak?Mathf.SmoothStep(0,1,Mathf.InverseLerp(peak-.045f,peak,p)):Mathf.SmoothStep(1,0,Mathf.InverseLerp(peak,peak+.07f,p));}
        static float BossCurve(float p,float[] values)
        {
            for(int i=1;i<SlamTimes.Length;i++)if(p<=SlamTimes[i])return Mathf.Lerp(values[i-1],values[i],Mathf.SmoothStep(0,1,Mathf.InverseLerp(SlamTimes[i-1],SlamTimes[i],p)));
            return values[values.Length-1];
        }
        void ApplyMonsterPose(Vector3 position,Vector3 scale,float angle,float dt)
        {
            if(action==ActionKind.BossAttack&&actionTime<.08f)
            {
                float entry=Mathf.SmoothStep(0,1,actionTime/.08f);position=Vector3.Lerp(monsterEntryPosition,position,entry);scale=Vector3.Lerp(monsterEntryScale,scale,entry);angle=monsterEntryAngle+Mathf.DeltaAngle(monsterEntryAngle,angle)*entry;
            }
            if(exitTime>0)
            {
                exitTime=Mathf.Max(0,exitTime-dt);float blend=Mathf.SmoothStep(0,1,1-exitTime/.10f);
                position=Vector3.Lerp(monsterExitPosition,position,blend);scale=Vector3.Lerp(monsterExitScale,scale,blend);angle=monsterExitAngle+Mathf.DeltaAngle(monsterExitAngle,angle)*blend;
            }
            monsterRenderer.transform.localPosition=position;monsterRenderer.transform.localScale=scale;monsterRenderer.transform.localRotation=Quaternion.Euler(0,0,angle);
            var color=Tint;if(hurtTime>0)color=Color.Lerp(color,new Color(1,.35f,.32f,color.a),.6f);if(action==ActionKind.Death)color.a*=1-Mathf.Clamp01(actionTime/actionDuration)*.8f;monsterRenderer.color=color;
        }
        public void Tick(bool moving,bool attacking,bool hurt,float dt){SetPose(moving,attacking,hurt,Facing,dt);}
        void OnDestroy(){foreach(var part in meshes)if(part.mesh!=null)Destroy(part.mesh);}

        /// <summary>A cached camera portrait of the unchanged original pixel meshes, with Point filtering.</summary>
        public static Texture2D Portrait(int artIndex)
        {
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

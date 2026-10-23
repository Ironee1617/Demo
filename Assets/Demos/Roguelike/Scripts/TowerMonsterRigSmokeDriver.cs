using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>Opt-in visual and numerical inspection of every monster skeleton.</summary>
    public sealed class TowerMonsterRigSmokeDriver : MonoBehaviour
    {
        [Serializable] sealed class AttackAudit
        {
            public string species,morphology,attackProfile,activeJoints;
            public int articulatedJoints;
            public float jointExcursionDegrees,localAttackDeformation,recoveryDifference;
            public bool anticipation,contact,recovery;
        }
        [Serializable] sealed class Report
        {
            public bool success;
            public int species, skeletons, bonesPerSkeleton=14, weightedBones, sampledFrames;
            public int legacySpecies,attackSampledFrames;
            public float maximumFrameStep, minimumTriangleAreaRatio=100, maximumTriangleAreaRatio, maximumRateDifference;
            public List<string> samples=new List<string>(),screenshots=new List<string>(),animationFrames=new List<string>(),errors=new List<string>();
            public List<string> attackSheets=new List<string>();
            public List<AttackAudit> attackAudits=new List<AttackAudit>();
        }
        readonly Report report=new Report();
        readonly List<TowerMonsterRig> gallery=new List<TowerMonsterRig>();
        string prefix,reportPath;
        bool initialized,finished,quit,animatedCapture=true;
        Camera view;
        GameObject stage;
        public bool IsFinished { get { return finished; } }
        public bool Succeeded { get { return finished&&report.success; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Enter()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"-towerMonsterRigSmoke")<0)return;
            var driver=new GameObject("Monster skeleton validation").AddComponent<TowerMonsterRigSmokeDriver>();
            driver.Initialize(Arg(args,"-towerMonsterRigCapture")??Path.Combine(Application.persistentDataPath,"monster-rigs"),
                Arg(args,"-towerMonsterRigReport")??Path.Combine(Application.persistentDataPath,"monster-rigs.json"),true,
                Array.IndexOf(args,"-towerMonsterRigNoAnimation")<0);
        }
        static string Arg(string[] args,string key)
        {int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:null;}
        public void Initialize(string capturePrefix,string outputReport,bool quitOnComplete=false,bool captureAnimation=true)
        {
            if(initialized)throw new InvalidOperationException("Create one driver for each rig suite.");
            if(!Path.IsPathRooted(capturePrefix)||!Path.IsPathRooted(outputReport))throw new ArgumentException("Rig output paths must be absolute.");
            prefix=capturePrefix;reportPath=outputReport;quit=quitOnComplete;animatedCapture=captureAnimation;initialized=true;
            Application.logMessageReceived+=RecordLog;
        }
        void RecordLog(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(message+"\n"+trace);}
        IEnumerator Start()
        {
            if(!initialized)yield break;
            var suite=Suite();
            while(true)
            {
                bool more;object next=null;
                try{more=suite.MoveNext();if(more)next=suite.Current;}
                catch(Exception ex){report.errors.Add(ex.ToString());break;}
                if(!more)break;
                yield return next;
            }
            Finish();
        }
        IEnumerator Suite()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            stage=new GameObject("49 articulated monster gallery");stage.transform.position=new Vector3(20000,20000,0);
            for(int row=0;row<7;row++)for(int column=0;column<7;column++)
            {
                bool boss=column==6;
                var source=TowerThemeMonsterArt.Get((TowerThemeId)row,boss?0:column,boss);
                var actor=TowerActorArt.Create(stage.transform,boss?4:0,true);
                actor.SetThemeMonster((TowerThemeId)row,boss?0:column,boss);
                var rig=actor.GetComponent<TowerMonsterRig>();
                Require(rig!=null&&rig.Renderer!=null&&rig.Renderer.enabled,"Missing visible monster rig: "+source.name);
                Require(rig.SourceSprite==source&&rig.BoneCount==14&&rig.WeightedBoneCount>=4,"Incomplete original-art skin: "+source.name);
                foreach(var legacy in actor.GetComponentsInChildren<SpriteRenderer>())
                    Require(!legacy.enabled,"A flat sprite is still rendering over the creature skeleton: "+source.name);
                float height=source.rect.height*TowerActorArt.PixelScale*(boss?50f/source.rect.height:Mathf.Min(1,42f/source.rect.height));
                actor.transform.localPosition=new Vector3((column-3)*3.1f,(3-row)*2.30f-.7f,0);
                gallery.Add(rig);
                try{CheckMotion(rig,(TowerThemeId)row,column,boss,height);CheckAttack(rig,(TowerThemeId)row,column,boss,height);}
                catch(Exception ex){report.errors.Add(source.name+": "+ex.Message);}
                report.species++;report.skeletons++;report.weightedBones+=rig.WeightedBoneCount;
                report.samples.Add(source.name+" / "+rig.Morphology+" / "+rig.BoneCount+" joints / "+rig.WeightedBoneCount+" weighted joints");
                yield return null;
            }
            Require(report.species==49,"All 42 regular monsters and 7 bosses must be inspected.");
            for(int kind=0;kind<5;kind++)
            {
                var actor=TowerActorArt.Create(stage.transform,kind,true);
                var rig=actor.GetComponent<TowerMonsterRig>();float h=kind==4?50f/32:kind==0?30f/32:kind==1?33f/32:kind==2?45f/32:50f/32;
                try{CheckMotion(rig,TowerThemeId.Chinese,kind,kind==4,h,true);CheckAttack(rig,TowerThemeId.Chinese,kind,kind==4,h,true);}
                catch(Exception ex){report.errors.Add("Legacy "+kind+": "+ex.Message);}
                actor.gameObject.SetActive(false);Destroy(actor.gameObject);report.legacySpecies++;
                yield return null;
            }
            var cameraObject=new GameObject("Monster rig contact camera");cameraObject.transform.SetParent(stage.transform,false);
            view=cameraObject.AddComponent<Camera>();view.enabled=false;view.orthographic=true;view.orthographicSize=8.3f;
            view.transform.localPosition=new Vector3(0,0,-10);view.clearFlags=CameraClearFlags.SolidColor;
            view.backgroundColor=new Color(.025f,.035f,.06f,1);view.allowHDR=false;view.allowMSAA=false;view.cullingMask=1<<30;
            foreach(var child in stage.GetComponentsInChildren<Transform>())child.gameObject.layer=30;
            foreach(string motion in new[]{"Idle","Walk","Attack"})
            {
                foreach(var rig in gallery)
                {
                    int row=gallery.IndexOf(rig)/7,column=gallery.IndexOf(rig)%7;bool boss=column==6;
                    var source=TowerThemeMonsterArt.Get((TowerThemeId)row,boss?0:column,boss);
                    float h=boss?50f/32:42f/32;
                    rig.Build(source,(TowerThemeId)row,boss?0:column,boss,h);
                    for(int f=0;f<40;f++)rig.Pose(motion,motion=="Walk",motion=="Attack"?.48f:0,0,-1,Color.white,true,new Vector2(2,0),motion=="Walk"?2:0,1f/60);
                }
                foreach(var child in stage.GetComponentsInChildren<Transform>())child.gameObject.layer=30;
                Capture(prefix+"-"+motion.ToLowerInvariant()+".png");
                yield return null;
            }
            // Each row is one species; columns are windup, contact and recovery.
            // This inspects all limbs at gameplay-sized joint arcs, rather than
            // presenting one attractive representative for the entire roster.
            foreach(var rig in gallery)rig.gameObject.SetActive(false);
            for(int theme=0;theme<8;theme++)
            {
                bool legacy=theme==7;int count=legacy?5:7;
                var attackStage=new GameObject("Per-species attack phases");attackStage.transform.SetParent(stage.transform,false);
                for(int row=0;row<count;row++)for(int phase=0;phase<3;phase++)
                {
                    bool boss=legacy?row==4:row==6;
                    var go=new GameObject("Attack "+row+" phase "+phase);go.transform.SetParent(attackStage.transform,false);
                    go.transform.localPosition=new Vector3((phase-1)*4.1f,((count-1)*.5f-row)*2.25f-.65f,0);
                    var rig=go.AddComponent<TowerMonsterRig>();
                    var source=legacy?Resources.Load<Sprite>("Tower/Monsters/monster_"+row):TowerThemeMonsterArt.Get((TowerThemeId)theme,boss?0:row,boss);
                    Rebuild(rig,source,(TowerThemeId)(legacy?0:theme),row,boss,1.5f,legacy);
                    int frames=phase==0?15:phase==1?30:57;
                    for(int f=0;f<frames;f++)rig.Pose("Attack",false,f/60f,0,-1,Color.white,true,Vector2.zero,0,1f/60);
                }
                foreach(var child in stage.GetComponentsInChildren<Transform>())child.gameObject.layer=30;
                // The three columns span the same width even when there are five
                // legacy rows. Preserve the horizontal margin for their side poses.
                view.orthographicSize=8.3f;view.transform.localPosition=new Vector3(0,0,-10);
                Capture(prefix+"-attack-phases-"+(legacy?"legacy":((TowerThemeId)theme).ToString().ToLowerInvariant())+".png",1440,1920,false,true);
                attackStage.SetActive(false);Destroy(attackStage);yield return null;
            }
            if(animatedCapture)
            {
                // One representative of every body plan, in two rows: slime, imp,
                // golem, hound / crawler, mage, jellyfish, wind spirit.
                int[] representatives={0,1,2,4,7,5,32,38};
                foreach(var rig in gallery)rig.gameObject.SetActive(false);
                for(int i=0;i<representatives.Length;i++)
                {
                    int ordinal=representatives[i],row=ordinal/7,column=ordinal%7;
                    var rig=gallery[ordinal];rig.gameObject.SetActive(true);
                    rig.transform.localPosition=new Vector3((i%4-1.5f)*3.1f,i<4?.6f:-2.0f,0);
                    rig.Build(TowerThemeMonsterArt.Get((TowerThemeId)row,column,false),(TowerThemeId)row,column,false,1.5f);
                }
                foreach(var child in stage.GetComponentsInChildren<Transform>())child.gameObject.layer=30;
                view.orthographicSize=3.6f;view.transform.localPosition=new Vector3(0,.15f,-10);
                for(int frame=0;frame<72;frame++)
                {
                    for(int sub=0;sub<5;sub++)
                    {
                        float t=(frame*5+sub)/60f,p=0,hurt=0;string motion="Idle";
                        if(t>=.75f&&t<2)motion="Walk";
                        else if(t>=2&&t<3){motion="Attack";p=t-2;}
                        else if(t>=3&&t<3.9f){motion="Cast";p=(t-3)/.9f;}
                        else if(t>=3.9f&&t<4.2f){motion="Hurt";hurt=.2f*(1-(t-3.9f)/.3f);}
                        else if(t>=4.8f){motion="Death";p=(t-4.8f)/1.2f;}
                        foreach(int ordinal in representatives)
                            gallery[ordinal].Pose(motion,motion=="Walk",p,hurt,1,Color.white,true,new Vector2(2,0),motion=="Walk"?2:0,1f/60);
                    }
                    Capture(prefix+"-motion-"+frame.ToString("000")+".png",1280,720,true);
                    yield return null;
                }
            }
        }
        static void Rebuild(TowerMonsterRig rig,Sprite source,TowerThemeId theme,int column,bool boss,float height,bool legacy)
        {if(legacy)rig.BuildLegacy(source,column,height);else rig.Build(source,theme,boss?0:column,boss,height);}
        void CheckMotion(TowerMonsterRig rig,TowerThemeId theme,int column,bool boss,float height,bool legacy=false)
        {
            Vector3[] rest=rig.CaptureRestVertices();int[] triangles=rig.CaptureTriangles();
            var source=rig.SourceSprite;
            foreach(string motion in new[]{"Walk","Attack","Cast","Hurt","Death","BossCrownVolley","BossTwinSpiral","BossLeapingSlam"})
            {
                if(motion.StartsWith("Boss",StringComparison.Ordinal)&&!boss)continue;
                Rebuild(rig,source,theme,column,boss,height,legacy);
                Vector3[] previous=rig.CaptureVertices();
                for(int frame=0;frame<61;frame++)
                {
                    float p=frame/60f;
                    string state=frame==60&&motion!="Death"?"Idle":motion;
                    rig.Pose(state,motion=="Walk",p,motion=="Hurt"?Mathf.Max(0,.2f-p):0,1,Color.white,true,
                        new Vector2(2,0),motion=="Walk"?2:0,1f/60);
                    var points=rig.CaptureVertices();report.sampledFrames++;
                    float frameStep=0;
                    for(int i=0;i<points.Length;i++)
                    {
                        Require(!float.IsNaN(points[i].x)&&!float.IsInfinity(points[i].y),"Nonfinite skin: "+source.name);
                        frameStep=Mathf.Max(frameStep,Vector3.Distance(points[i],previous[i])/height);
                    }
                    report.maximumFrameStep=Mathf.Max(report.maximumFrameStep,frameStep);
                    Require(frameStep<.10f,"Pose snapped: "+source.name+" "+state+" "+frameStep);
                    if(frame%10==0)
                    {
                        for(int i=0;i<triangles.Length;i+=3)
                        {
                            int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                            float initial=Cross(rest[b]-rest[a],rest[c]-rest[a]);
                            float area=(rig.SourceFacesLeft?-1:1)*Cross(points[b]-points[a],points[c]-points[a])/initial;
                            report.minimumTriangleAreaRatio=Mathf.Min(report.minimumTriangleAreaRatio,area);
                            report.maximumTriangleAreaRatio=Mathf.Max(report.maximumTriangleAreaRatio,area);
                            Require(area>.05f&&area<4,"Folded or excessively stretched creature skin: "+source.name+" "+state+" p="+p+" vertex="+rest[a]+" area="+area);
                        }
                    }
                    previous=points;
                }
            }
            Vector3[] reference=null;
            foreach(int hz in new[]{30,60,120})
            {
                Rebuild(rig,source,theme,column,boss,height,legacy);
                for(int f=1;f<=hz;f++)rig.Pose("Walk",true,0,0,1,Color.white,true,new Vector2(2,0),2,1f/hz);
                var points=rig.CaptureVertices();
                if(reference!=null)
                {
                    float difference=0;for(int i=0;i<points.Length;i++)difference=Mathf.Max(difference,Vector3.Distance(points[i],reference[i])/height);
                    report.maximumRateDifference=Mathf.Max(report.maximumRateDifference,difference);
                    Require(difference<.025f,"Frame-rate dependent monster animation: "+source.name+" "+difference);
                }
                reference=points;
            }
        }
        void CheckAttack(TowerMonsterRig rig,TowerThemeId theme,int column,bool boss,float height,bool legacy=false)
        {
            var source=rig.SourceSprite;Rebuild(rig,source,theme,column,boss,height,legacy);
            var idleObject=new GameObject("Matched idle control");idleObject.transform.SetParent(stage.transform,false);
            var idle=idleObject.AddComponent<TowerMonsterRig>();Rebuild(idle,source,theme,column,boss,height,legacy);idle.Renderer.enabled=false;
            var audit=new AttackAudit{species=legacy?"Legacy "+column+" "+source.name:source.name,
                morphology=rig.Morphology.ToString(),attackProfile=rig.AttackProfile.ToString()};
            bool[] moved=new bool[14];
            try
            {
                for(int frame=0;frame<=66;frame++)
                {
                    float p=frame/60f;rig.Pose(frame<=60?"Attack":"Idle",false,p,0,1,Color.white,true,Vector2.zero,0,1f/60);
                    idle.Pose("Idle",false,0,0,1,Color.white,true,Vector2.zero,0,1f/60);report.attackSampledFrames++;
                    var posed=rig.CaptureVertices();var control=idle.CaptureVertices();
                    var angles=rig.CaptureJointAngles();var idleAngles=idle.CaptureJointAngles();
                    float jointDelta=0;
                    for(int j=2;j<angles.Length;j++)
                    {
                        if(j==(int)TowerMonsterRig.Joint.Cloth)continue;
                        float d=Mathf.Abs(Mathf.DeltaAngle(angles[j],idleAngles[j]));jointDelta=Mathf.Max(jointDelta,d);
                        if(d>.75f)moved[j]=true;
                    }
                    audit.jointExcursionDegrees=Mathf.Max(audit.jointExcursionDegrees,jointDelta);
                    Vector3 translation=Vector3.zero;for(int v=0;v<posed.Length;v++)translation+=posed[v]-control[v];translation/=posed.Length;
                    float local=0,absolute=0;for(int v=0;v<posed.Length;v++)
                    {local=Mathf.Max(local,(posed[v]-control[v]-translation).magnitude/height);absolute=Mathf.Max(absolute,(posed[v]-control[v]).magnitude/height);}
                    audit.localAttackDeformation=Mathf.Max(audit.localAttackDeformation,local);
                    if(frame==15)audit.anticipation=jointDelta>.75f&&local>.0015f;
                    if(frame==30)audit.contact=jointDelta>.75f&&local>.002f;
                    if(frame==66){audit.recoveryDifference=absolute;audit.recovery=absolute<.025f;}
                }
                var names=new List<string>();for(int j=2;j<moved.Length;j++)if(moved[j])names.Add(((TowerMonsterRig.Joint)j).ToString());
                audit.activeJoints=String.Join(", ",names);audit.articulatedJoints=names.Count;report.attackAudits.Add(audit);
                Require(audit.anticipation&&audit.contact&&audit.recovery&&audit.articulatedJoints>=2,
                    "Missing articulated attack phase: "+audit.species+" / "+audit.attackProfile+" / "+audit.activeJoints);
            }
            finally{idleObject.SetActive(false);Destroy(idleObject);}
        }
        static float Cross(Vector3 a,Vector3 b){return a.x*b.y-a.y*b.x;}
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        void Capture(string path,int width=1920,int height=1440,bool animation=false,bool attackSheet=false)
        {
            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
            var old=RenderTexture.active;Texture2D image=null;
            try
            {
                view.targetTexture=rt;view.aspect=width/(float)height;view.Render();RenderTexture.active=rt;
                image=new Texture2D(width,height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                File.WriteAllBytes(path,image.EncodeToPNG());if(animation)report.animationFrames.Add(path);else if(attackSheet)report.attackSheets.Add(path);else report.screenshots.Add(path);
            }
            finally {view.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);if(image)Destroy(image);}
        }
        void Finish()
        {
            if(finished)return;finished=true;report.success=report.errors.Count==0&&report.species==49&&report.legacySpecies==5&&
                report.attackAudits.Count==54&&report.attackSheets.Count==8&&report.screenshots.Count==3;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));
            if(stage!=null){stage.SetActive(false);Destroy(stage);}Application.logMessageReceived-=RecordLog;
            if(quit)Application.Quit(report.success?0:1);
            else Debug.Log("Monster skeleton suite "+(report.success?"PASS: ":"FAIL: ")+reportPath);
        }
        void OnDestroy(){Application.logMessageReceived-=RecordLog;}
    }
}

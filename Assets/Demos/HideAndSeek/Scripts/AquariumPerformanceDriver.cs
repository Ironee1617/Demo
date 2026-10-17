#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Profiling;
using UnityEngine;
namespace PrototypeLab.Aquarium
{
    // Opt-in whole-match profiler. It drives real rules, drawing and cameras.
    [DefaultExecutionOrder(1100)]
    public sealed class AquariumPerformanceDriver:MonoBehaviour
    {
        [Serializable] public class Phase
        {
            public string name;
            public int frames,gcCollections;
            public float medianMs,p95Ms,p99Ms,maxMs,drawMs,simulationMs,cameraMs,targetMs;
            public float mainMs,renderMs,gpuMs,gcBytes,maxGcBytes,drawCalls,triangles,setPasses;
        }
        [Serializable] public class Report
        {
            public string device,version;
            public int width,height;
            public bool gcCounter,drawCounter,gpuTiming,batchMode;
            public string measurement="CPU game-loop profile; batch mode may disable automatic camera rendering. Check drawCalls and gpuTiming before interpreting graphics costs.";
            public List<Phase> phases=new List<Phase>();
            public List<string> errors=new List<string>();
        }
        struct Sample { public float frame,draw,simulation,camera,target,main,render,gpu,gc,calls,triangles,setPasses; }
        static readonly string[] Names={"swimming","fish_feeding","tentacle_feeding","ally_feeding","crowded_school"};
        readonly List<Sample> samples=new List<Sample>(8192);
        readonly FrameTiming[] timing=new FrameTiming[1];
        AquariumSession session;AquariumView view;AquariumThirdPersonCamera follow;
        ProfilerRecorder draw,simulation,camera,target,main,render,gc,calls,triangles,setPasses;
        Report report=new Report();string output;float started,phaseAt;int phase=-1,collections;bool completing;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static string Arg(string key){var a=Environment.GetCommandLineArgs();for(int i=0;i<a.Length-1;i++)if(a[i]==key)return a[i+1];return "";}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap(){string path=Arg("-aqPerfReport");if(path.Length==0)return;var driver=new GameObject("Opt-in frame profiler").AddComponent<AquariumPerformanceDriver>();driver.output=path;}
        void Start()
        {
            Application.runInBackground=true;Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;
            var game=FindFirstObjectByType<AquariumGame>();if(game==null)throw new Exception("Missing AquariumGame");
            session=(AquariumSession)typeof(AquariumGame).GetField("session",Private).GetValue(game);
            view=(AquariumView)typeof(AquariumGame).GetField("view",Private).GetValue(game);
            follow=(AquariumThirdPersonCamera)typeof(AquariumGame).GetField("followCamera",Private).GetValue(game);
            typeof(AquariumGame).GetField("automated",Private).SetValue(game,true);
            session.StartPractice("Frame profiler",Team.Hider);
            foreach(var p in session.Model.Players)p.bot=false;
            while(session.Model.Time<session.Model.HuntAt+.1f)session.Model.Tick(1f/30);
            draw=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"Aquarium.Draw");simulation=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"Aquarium.Session");
            camera=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"Aquarium.Camera");target=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"Aquarium.Target");
            main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread");render=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Render Thread");
            gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");
            calls=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count");setPasses=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count");
            report.batchMode=Application.isBatchMode;report.device=SystemInfo.graphicsDeviceName;report.version=Application.unityVersion;report.gcCounter=gc.Valid;report.drawCounter=draw.Valid;
            Application.logMessageReceived+=Log;started=Time.realtimeSinceStartup;BeginPhase();
        }
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(message);}
        void BeginPhase()
        {
            phase++;if(phase>=Names.Length){Finish();return;}
            samples.Clear();collections=GC.CollectionCount(0);phaseAt=Time.realtimeSinceStartup;
            var model=session.Model;var owner=model.Player(session.LocalId);
            foreach(var p in model.Players){p.velocity=new Point();p.stunnedUntil=0;p.nextAction=0;p.swallowedBy=0;p.visualAction="";p.visualAt=-100;p.species=-1;p.revealUntil=0;p.position=new Point(-25,9,-5-p.id*.8f);}
            owner.position=new Point(2.3f,8,-2);owner.facing=new Point(0,0,1);owner.species=phase==1?0:-1;owner.morphAt=-100;
            for(int i=0;i<model.Fish.Count;i++)
            {
                var f=model.Fish[i];f.dead=f.eaten=false;f.eater=0;f.visualAt=-100;f.turnAt=float.MaxValue;f.steering=new Point();f.swimSpeed=0;
                f.position=new Point(-6+(i%8)*2,7+(i%3)*.7f,3+(i/8)*2);f.facing=new Point(1,0,0);
                if(phase==4)f.position=new Point(2.3f+(i%4)*.7f,8+(i/16)*.8f,3+(i/4%4)*.7f);
            }
            model.Effects.Clear();
            if(phase>=1&&phase<=3)
            {
                if(phase==3)
                {var ally=model.Players.Find(p=>p.team==Team.Hider&&p.id!=owner.id);ally.position=owner.position+owner.facing*1.3f;ally.facing=new Point(0,0,-1);}
                else{var food=model.Fish[0];food.position=owner.position+owner.facing*1.3f;food.facing=new Point(0,0,-1);}
                model.SetInput(owner.id,new SwimInput{aimZ=1,action=true});model.Tick(1f/30);
                if(string.IsNullOrEmpty(owner.visualAction))report.errors.Add("Feeding phase did not start: "+Names[phase]);
            }
            if(phase==4)model.Effects.Add(new WaterEffect{id=100000,kind="feed",position=new Point(2.3f,8,3.8f),born=model.Time,duration=10});
            follow.Prepare(model.Snapshot(),session.LocalId);
        }
        static float Value(ProfilerRecorder recorder){return recorder.Valid?recorder.LastValue:0;}
        void LateUpdate()
        {
            if(completing||session==null)return;
            FrameTimingManager.CaptureFrameTimings();float gpu=0;
            if(FrameTimingManager.GetLatestTimings(1,timing)>0){gpu=(float)timing[0].gpuFrameTime;report.gpuTiming|=gpu>0;}
            float elapsed=Time.realtimeSinceStartup-phaseAt;
            if(elapsed>.8f)samples.Add(new Sample{frame=Time.unscaledDeltaTime*1000,draw=Value(draw)/1000000,simulation=Value(simulation)/1000000,camera=Value(camera)/1000000,target=Value(target)/1000000,
                main=Value(main)/1000000,render=Value(render)/1000000,gpu=gpu,gc=Value(gc),calls=Value(calls),triangles=Value(triangles),setPasses=Value(setPasses)});
            if(elapsed>=5){EndPhase();BeginPhase();}
        }
        void EndPhase()
        {
            var p=new Phase{name=Names[phase],frames=samples.Count,gcCollections=GC.CollectionCount(0)-collections};var times=new float[samples.Count];
            for(int i=0;i<samples.Count;i++)
            {var s=samples[i];times[i]=s.frame;p.drawMs+=s.draw;p.simulationMs+=s.simulation;p.cameraMs+=s.camera;p.targetMs+=s.target;p.mainMs+=s.main;p.renderMs+=s.render;p.gpuMs+=s.gpu;p.gcBytes+=s.gc;p.maxGcBytes=Mathf.Max(p.maxGcBytes,s.gc);p.drawCalls+=s.calls;p.triangles+=s.triangles;p.setPasses+=s.setPasses;}
            if(samples.Count>0)
            {float n=samples.Count;p.drawMs/=n;p.simulationMs/=n;p.cameraMs/=n;p.targetMs/=n;p.mainMs/=n;p.renderMs/=n;p.gpuMs/=n;p.gcBytes/=n;p.drawCalls/=n;p.triangles/=n;p.setPasses/=n;Array.Sort(times);p.medianMs=times[times.Length/2];p.p95Ms=times[Mathf.Min(times.Length-1,(int)(times.Length*.95f))];p.p99Ms=times[Mathf.Min(times.Length-1,(int)(times.Length*.99f))];p.maxMs=times[times.Length-1];}
            report.phases.Add(p);
        }
        void Finish()
        {
            completing=true;report.width=Screen.width;report.height=Screen.height;Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));
            Debug.Log("AQUARIUM_FRAME_PROFILE_OK "+output);
#if UNITY_EDITOR
            if(Application.isBatchMode)UnityEditor.EditorApplication.Exit(report.errors.Count==0?0:1);else UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit(report.errors.Count==0?0:1);
#endif
        }
        void OnDestroy(){Application.logMessageReceived-=Log;draw.Dispose();simulation.Dispose();camera.Dispose();target.Dispose();main.Dispose();render.Dispose();gc.Dispose();calls.Dispose();triangles.Dispose();setPasses.Dispose();}
    }
}
#endif
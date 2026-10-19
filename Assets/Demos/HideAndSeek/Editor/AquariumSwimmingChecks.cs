using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumSwimmingChecks
    {
        public static void Review()
        {
            Run();AquariumFeedingChecks.Geometry();AquariumFeedingChecks.JawGeometry();
            foreach(string scenario in new[]{"MouthEat","TentacleEat","AllyEat"})
            {Environment.SetEnvironmentVariable("AQUARIUM_QA_SCENARIO",scenario);AquariumCharacterChecks.InTank();}
        }
        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        [MenuItem("Aquarium/Characters/Check Swimming Performance")]
        public static void Run()
        {
            string output=Environment.GetEnvironmentVariable("AQUARIUM_SWIMMING_QA");if(string.IsNullOrEmpty(output))output="Artifacts/SwimmingQA";Directory.CreateDirectory(output);
            var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Swimming QA");SceneManager.MoveGameObjectToScene(root,scene);
            var result=new StringBuilder();
            try
            {
                foreach(string resource in new[]{"AquariumEquipment/Diver","AquariumCharacters/Fish0","AquariumCharacters/Alien"})
                {
                    var prefab=Resources.Load<GameObject>(resource);Require(prefab!=null,"Missing "+resource);
                    var rig=UnityEngine.Object.Instantiate(prefab,root.transform).GetComponent<AquariumRig>();
                    rig.SamplePose("Swim",0,0);var watch=new Stopwatch();watch.Start();
                    for(int i=0;i<240;i++){rig.SamplePose("Swim",i/60f,i/60f);rig.Neutralize(0);}
                    watch.Stop();result.AppendLine(resource+" pose_us="+(watch.Elapsed.TotalMilliseconds*1000/240).ToString("F2"));
                    float phase60=Simulate(rig,60),phase144=Simulate(rig,144);
                    Require(Mathf.Abs(phase60-phase144)<.05f,"Locomotion cadence depends on render rate: "+resource);
                    result.AppendLine("cadence_60_144_difference="+Mathf.Abs(phase60-phase144).ToString("F4"));
                    UnityEngine.Object.DestroyImmediate(rig.gameObject);
                }
                SnapshotCadence(root.transform,result);
                var load=Stopwatch.StartNew();AquariumCreature.Prewarm();load.Stop();result.AppendLine("loading_cache_ms="+load.Elapsed.TotalMilliseconds.ToString("F2"));
                var creature=new GameObject("Predator").AddComponent<AquariumCreature>();creature.transform.SetParent(root.transform);
                creature.Initialize(0,1);creature.PrepareFeedingForms(0);creature.Present(0,"Locomotion",0,0,1.5f);
                var timer=Stopwatch.StartNew();creature.Present(100,"Bite",1,1,0);timer.Stop();result.AppendLine("first_bite_ms="+timer.Elapsed.TotalMilliseconds.ToString("F2"));
                var camera=new GameObject("QA Camera").AddComponent<Camera>();camera.transform.SetParent(root.transform);camera.scene=scene;
                camera.transform.position=new Vector3(1.9f,.7f,2.5f);camera.transform.LookAt(Vector3.zero);camera.fieldOfView=40;camera.nearClipPlane=.03f;camera.depthTextureMode=DepthTextureMode.Depth;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.03f,.08f,.12f);
                var light=new GameObject("QA Light").AddComponent<Light>();light.transform.SetParent(root.transform);light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(35,-40,0);
                var rt=new RenderTexture(960,640,24);var pixels=new Texture2D(960,640,TextureFormat.RGB24,false);camera.targetTexture=rt;
                try
                {
                    for(int frame=0;frame<=270;frame++)
                    {
                        float age=frame/60f;creature.Present(age<AquariumFeeding.ReturnToFish?100:0,age<=AquariumModel.FishFeedingSeconds?"Bite":"Locomotion",1,1+age,0);
                        if(frame%30==0)Capture(camera,rt,pixels,Path.Combine(output,"feeding-"+frame.ToString("000")+".png"));
                        if(frame==195)Require(creature.Form==0&&!creature.Transitioning,"Chewing requires a completely closed normal fish");
                    }
                    UnityEngine.Object.DestroyImmediate(creature.gameObject);
                    foreach(string resource in new[]{"AquariumEquipment/Diver","AquariumCharacters/Fish0","AquariumCharacters/Alien"})
                    {
                        var rig=UnityEngine.Object.Instantiate(Resources.Load<GameObject>(resource),root.transform).GetComponent<AquariumRig>();
                        for(int frame=0;frame<=180;frame++)
                        {
                            rig.SampleLocomotion(frame<120?1.6f:0,frame/60f,.37f);
                            if(frame%30==0)Capture(camera,rt,pixels,Path.Combine(output,resource.Substring(resource.LastIndexOf('/')+1)+"-swim-"+frame.ToString("000")+".png"));
                            if(frame==60)
                            {
                                var position=camera.transform.position;var rotation=camera.transform.rotation;
                                camera.orthographic=true;camera.orthographicSize=1.7f;camera.transform.position=new Vector3(3.5f,.2f,0);camera.transform.LookAt(Vector3.zero);
                                Capture(camera,rt,pixels,Path.Combine(output,resource.Substring(resource.LastIndexOf('/')+1)+"-side.png"));
                                camera.orthographic=false;camera.transform.SetPositionAndRotation(position,rotation);
                            }
                        }
                        UnityEngine.Object.DestroyImmediate(rig.gameObject);
                    }
                    var ink=new GameObject("Ink").AddComponent<AquariumInkCloud>();ink.transform.SetParent(root.transform);
                    timer.Restart();ink.Initialize(1,false);timer.Stop();result.AppendLine("first_ink_ms="+timer.Elapsed.TotalMilliseconds.ToString("F2"));
                    camera.transform.position=new Vector3(0,0,-2.3f);camera.transform.LookAt(new Vector3(0,.15f,0));camera.fieldOfView=65;
                    ink.Present(1.3f,9,Vector3.zero,Vector3.forward);
                    Capture(camera,rt,pixels,Path.Combine(output,"ink-red.png"));
                    var volume=ink.GetComponentInChildren<Renderer>();var optimized=volume.sharedMaterial;
                    double optimizedMs=RenderCost(camera,rt,pixels);result.AppendLine("ink_render_sync_ms="+optimizedMs.ToString("F2"));
                    string baseline=Environment.GetEnvironmentVariable("AQUARIUM_INK_BASELINE");
                    if(!string.IsNullOrEmpty(baseline)&&File.Exists(baseline))
                    {
                        var shader=ShaderUtil.CreateShaderAsset(File.ReadAllText(baseline).Replace("Aquarium/Underwater Ink Volume","Aquarium/Ink Baseline"));
                        var material=new Material(optimized){shader=shader};volume.sharedMaterial=material;
                        result.AppendLine("ink_baseline_render_sync_ms="+RenderCost(camera,rt,pixels).ToString("F2"));
                        Capture(camera,rt,pixels,Path.Combine(output,"ink-red-baseline.png"));volume.sharedMaterial=optimized;
                        UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(shader);
                    }
                    var yellow=new GameObject("Yellow ink").AddComponent<AquariumInkCloud>();yellow.transform.SetParent(root.transform);yellow.Initialize(2,true);ink.gameObject.SetActive(false);
                    yellow.Present(1.3f,9,Vector3.zero,Vector3.forward);Capture(camera,rt,pixels,Path.Combine(output,"ink-yellow.png"));
                }
                finally{camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
                result.AppendLine("OK");File.WriteAllText(Path.Combine(output,"swimming-checks.txt"),result.ToString());
            }
            catch(Exception ex){result.AppendLine("FAIL "+ex);File.WriteAllText(Path.Combine(output,"swimming-checks.txt"),result.ToString());throw;}
            finally{EditorSceneManager.ClosePreviewScene(scene);}
            UnityEngine.Debug.Log("AQUARIUM_SWIMMING_CHECKS_OK "+result);
        }
        static void SnapshotCadence(Transform parent,StringBuilder result)
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            foreach(int fps in new[]{60,144})foreach(string kind in new[]{"diver","fish0"})
            {
                var holder=new GameObject("Snapshot cadence");holder.transform.SetParent(parent);
                try
                {
                    var type=typeof(AquariumView);
                    var view=(AquariumView)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
                    type.GetField("root",flags).SetValue(view,holder.transform);
                    var actors=type.GetField("actors",flags);actors.SetValue(view,Activator.CreateInstance(actors.FieldType));
                    type.GetField("visible",flags).SetValue(view,new System.Collections.Generic.HashSet<string>());
                    type.GetField("random",flags).SetValue(view,new System.Random(73));
                    var place=type.GetMethod("Place",flags);float min=float.MaxValue,max=0;
                    for(int frame=0;frame<fps*3;frame++)
                    {
                        float time=frame/(float)fps,sample=Mathf.Floor(time*20)/20;
                        var actor=place.Invoke(view,new object[]{"p1",kind,new Point(0,0,sample*2),new Point(0,0,1),1f/fps,time,false,sample,time-.05f});
                        float speed=(float)actor.GetType().GetField("moveSpeed").GetValue(actor);
                        if(frame>fps){min=Mathf.Min(min,speed);max=Mathf.Max(max,speed);}
                    }
                    Require(min>1.5f&&max<2.5f,"20 Hz snapshots make locomotion flicker at "+fps+" fps: "+kind+" range "+min+".."+max);
                    result.AppendLine(kind+" snapshot_20hz_render_"+fps+"_speed="+min.ToString("F2")+".."+max.ToString("F2"));
                }
                finally{UnityEngine.Object.DestroyImmediate(holder);}
            }
        }
        static float Simulate(AquariumRig rig,int fps)
        {
            // Separate instances keep phase and speed history independent.
            var other=UnityEngine.Object.Instantiate(rig.gameObject,rig.transform.parent).GetComponent<AquariumRig>();
            try
            {
                for(int frame=0;frame<=fps*6;frame++)
                {
                    float t=frame/(float)fps;other.SampleLocomotion(t<2?1.6f:t<3?0:4.7f,t,.37f);
                    Require(!float.IsNaN(other.Bones[0].localRotation.x),"Finite swim pose");
                }
                return other.LocomotionPhase;
            }
            finally{UnityEngine.Object.DestroyImmediate(other.gameObject);}
        }
        static double RenderCost(Camera camera,RenderTexture rt,Texture2D pixels)
        {
            Capture(camera,rt,pixels,null);var timer=Stopwatch.StartNew();for(int i=0;i<12;i++)Capture(camera,rt,pixels,null);timer.Stop();return timer.Elapsed.TotalMilliseconds/12;
        }
        static void Capture(Camera camera,RenderTexture rt,Texture2D pixels,string file)
        {
            var previous=RenderTexture.active;try{camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0,false);if(file!=null)File.WriteAllBytes(file,pixels.EncodeToPNG());}finally{RenderTexture.active=previous;}
        }
    }
}
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumFeedingChecks
    {
        public static void Preview(){AquariumCharacterImport.Run();Geometry();JawGeometry();Studio();}
        public static void ValidateAndBuild()
        {
            AquariumCharacterImport.Run();Geometry();JawGeometry();Studio();AquariumCharacterChecks.Run();
            foreach(string scene in new[]{"MouthEat","TentacleEat","AllyEat"})
            {Environment.SetEnvironmentVariable("AQUARIUM_QA_SCENARIO",scene);AquariumCharacterChecks.InTank();}
            AquariumCharacterChecks.ReleaseReview();AquariumBuild.Windows();
        }
        public static void SuctionBuild()
        {
            Geometry();
            foreach(string scene in new[]{"MouthEat","TentacleEat","AllyEat"})
            {Environment.SetEnvironmentVariable("AQUARIUM_QA_SCENARIO",scene);AquariumCharacterChecks.InTank();}
            AquariumBuild.Windows();
        }
        static float TravelProgress(float travel)
        {
            float lo=0,hi=1;
            for(int i=0;i<30;i++){float mid=(lo+hi)*.5f;if(Mathf.SmoothStep(0,1,mid)<travel)lo=mid;else hi=mid;}
            return (lo+hi)*.5f;
        }
        public static void Geometry()
        {
            int count=0;float stretch=0,minimumFill=1;
            foreach(float radius in new[]{.115f,.26f})foreach(float scale in new[]{.4f,1f,3.5f,8f})
            {
                var origin=new Vector3(0,0,scale*2);var axis=Vector3.forward;float half=scale*.6f,depth=radius>.2f?.295f:.12f;
                float bodyRadius=scale*.25f,travel=origin.z+half+.08f;
                float entryTime=TravelProgress((origin.z-half-depth)/travel);
                var front=origin-axis*half+Vector3.right*bodyRadius;
                var atEntry=AquariumFeeding.IngestPoint(front,origin,axis,half,Vector3.zero,radius,depth,entryTime);
                float fill=Mathf.Abs(atEntry.x)/Mathf.Min(radius,bodyRadius);minimumFill=Mathf.Min(minimumFill,fill);
                if(Mathf.Abs(atEntry.z-depth)>.0001f||fill<.9f||Mathf.Abs(atEntry.x)>radius+.0001f)throw new Exception("Prey must fill the aperture without exceeding its rim");
                for(int step=0;step<=40;step++)
                {
                    float previous=-100;Vector3 last=Vector3.zero;
                    for(int slice=0;slice<=40;slice++)
                    {
                        Vector3 p=origin+axis*Mathf.Lerp(-half,half,slice/40f)+Vector3.right*bodyRadius;
                        Vector3 q=AquariumFeeding.IngestPoint(p,origin,axis,half,Vector3.zero,radius,depth,step/40f);
                        if(float.IsNaN(q.x)||float.IsInfinity(q.z)||q.z<previous-.0001f)throw new Exception("Suction reverses or tears a longitudinal section");
                        if(step==40 && Vector3.Distance(q,Vector3.back*.06f)>.001f)throw new Exception("Entire oversized prey must enter the throat");
                        if(step==0 && Vector3.Distance(p,q)>.00001f)throw new Exception("Rest surface changed before suction");
                        if(slice>0)
                        {
                            float ratio=(q.z-last.z)/(half*2/40);stretch=Mathf.Max(stretch,ratio);
                            if(ratio>1.001f)throw new Exception("Suction elongates the body into a strand");
                        }
                        previous=q.z;last=q;count++;
                    }
                }
            }
            Debug.Log("AQUARIUM_FULL_INGEST_OK "+count+" samples; 0.4x to 8x; two apertures; minimum relative mouth fill="+minimumFill+"; maximum axial stretch="+stretch);
        }
        public static void Studio()
        {
            string output=Environment.GetEnvironmentVariable("AQUARIUM_CHARACTER_QA");Directory.CreateDirectory(output);
            foreach(string scenario in new[]{"Fish","LargeFish","Alien","Ally"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.25f,.31f,.38f);RenderSettings.fog=false;
                var key=new GameObject("Soft key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=.85f;key.transform.rotation=Quaternion.Euler(35,-35,0);
                var rim=new GameObject("Rim").AddComponent<Light>();rim.type=LightType.Directional;rim.intensity=.4f;rim.color=new Color(.4f,.7f,1);rim.transform.rotation=Quaternion.Euler(30,150,0);
                bool fish=scenario=="Fish"||scenario=="LargeFish";
                float scale=scenario=="LargeFish"||scenario=="Alien"?3.5f:scenario=="Ally"?1.5f:1;
                var predator=new GameObject("Predator").AddComponent<AquariumCreature>();predator.Initialize(fish?0:-1,0);
                var holder=new GameObject("Prey placement").transform;holder.position=new Vector3(0,0,scale>2?2.6f:1.5f);holder.rotation=Quaternion.Euler(0,180,0);holder.localScale=Vector3.one*scale;
                var prey=new GameObject("Prey").AddComponent<AquariumCreature>();prey.transform.SetParent(holder,false);prey.Initialize(scenario=="Ally"?-1:1,0);
                var cam=new GameObject("Camera").AddComponent<Camera>();cam.transform.position=new Vector3(3.6f,.85f,2.6f);cam.transform.LookAt(new Vector3(0,0,scale>2?1.2f:.65f));cam.orthographic=true;cam.orthographicSize=scale>2?2.25f:1.35f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.018f,.047f,.067f);cam.nearClipPlane=.01f;
                var rt=new RenderTexture(1100,760,24);cam.targetTexture=rt;
                for(int frame=0;frame<=90;frame++)
                {
                    float age=frame/20f;int form=fish?(age<AquariumFeeding.ReturnToFish?100:0):-1;
                    predator.Present(form,fish?"Bite":"TentacleEat",0,age,0);
                    prey.Present(scenario=="Ally"?-1:1,"Eaten",0,age,0);prey.PresentIngestion(predator,age);
                    if(frame==0)
                    {
                        var skin=prey.GetComponentInChildren<AquariumRig>().skin;var test=new Mesh();skin.BakeMesh(test,false);
                        Debug.Log("INGEST_BAKE_SCALE "+scenario+" source="+skin.sharedMesh.bounds.size+" baked="+test.bounds.size+" scale="+skin.transform.lossyScale);
                        UnityEngine.Object.DestroyImmediate(test);
                    }
                    AquariumCharacterChecks.Capture(cam,rt,Path.Combine(output,"Ingest-"+scenario+"-"+frame.ToString("000")+".png"));
                    if(fish && age>=2.25f && age<=2.4f)
                    {
                        if(predator.Form<200||predator.Transitioning)throw new Exception("Mouth must close completely before normal fish return");
                    }
                    if(fish && age>=3.10f && (predator.Form!=0||predator.Transitioning))throw new Exception("Chewing begins after the closed-mouth return");
                }
                cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            }
            Debug.Log("AQUARIUM_INGEST_STUDIO_OK 364 frames; fish, oversized fish, alien feeding, swallowed alien; closure before return");
            JawDetails(output);
        }
        public static void JawGeometry()
        {
            var prefab=Resources.Load<GameObject>("AquariumCharacters/Fish0Mouth");
            var go=UnityEngine.Object.Instantiate(prefab);var rig=go.GetComponent<AquariumRig>();var skin=rig.skin;
            if(skin.bones.Any(b=>b.name.StartsWith("Tongue")))throw new Exception("Tongue bones remain");
            if(rig.closedEnvelope==null||rig.closedEnvelope.vertexCount!=rig.envelope.vertexCount)throw new Exception("Closed endpoint topology mismatch");
            if(rig.throatAnchor.z>=rig.mouthAnchor.z-.15f)throw new Exception("Suction endpoint is not inside the throat");
            rig.Clip("Bite").SampleAnimation(go,2.3f);var posed=new Mesh();skin.BakeMesh(posed);var a=posed.vertices;
            rig.Neutralize(1);skin.SetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("ClosedPose"),100);
            var closed=new Mesh();skin.BakeMesh(closed);var b=closed.vertices;float error=0;
            for(int i=0;i<a.Length;i++)error=Mathf.Max(error,Vector3.Distance(a[i],b[i]));
            if(error>.002f)throw new Exception("Closed jaw jumps at morph entry: "+error);
            var normal=Resources.Load<GameObject>("AquariumCharacters/Fish0").GetComponent<AquariumRig>().skin.sharedMesh;
            var closedFish=Resources.Load<GameObject>("AquariumCharacters/Fish0Closed").GetComponent<AquariumRig>().skin.sharedMesh;
            if(!normal.triangles.SequenceEqual(closedFish.triangles)||!normal.uv.SequenceEqual(closedFish.uv)||!normal.boneWeights.SequenceEqual(closedFish.boneWeights))throw new Exception("Closed-to-normal topology, UV or weights differ");
            Debug.Log("AQUARIUM_JAW_GEOMETRY_OK tongue removed; articulated-to-closed error="+error+"; throat="+rig.throatAnchor);
            UnityEngine.Object.DestroyImmediate(posed);UnityEngine.Object.DestroyImmediate(closed);UnityEngine.Object.DestroyImmediate(go);
        }
        static void JawDetails(string output)
        {
            foreach(string view in new[]{"Side","Front","ThreeQuarter"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.32f,.37f,.43f);RenderSettings.fog=false;
                var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.8f;light.transform.rotation=Quaternion.Euler(30,-30,0);
                var creature=new GameObject("Jaw inspection").AddComponent<AquariumCreature>();creature.Initialize(0,0);
                var cam=new GameObject("Camera").AddComponent<Camera>();cam.transform.position=view=="Front"?new Vector3(0,.05f,3):view=="Side"?new Vector3(3,0,.1f):new Vector3(2,.45f,2);
                cam.transform.LookAt(new Vector3(0,0,.08f));cam.orthographic=true;cam.orthographicSize=.67f;cam.nearClipPlane=.01f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.025f,.05f,.065f);
                var rt=new RenderTexture(1000,800,24);cam.targetTexture=rt;
                for(int frame=0;frame<=64;frame++)
                {
                    float age=frame/20f;creature.Present(age<AquariumFeeding.ReturnToFish?100:0,"Bite",0,age,0);
                    if(frame>=33)AquariumCharacterChecks.Capture(cam,rt,Path.Combine(output,"Jaw-"+view+"-"+frame.ToString("000")+".png"));
                }
                cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            }
            Debug.Log("AQUARIUM_JAW_DETAIL_OK 96 close-up frames");
        }
    }
}

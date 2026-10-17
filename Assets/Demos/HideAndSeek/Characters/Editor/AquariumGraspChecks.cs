using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumGraspChecks
    {
        [Serializable] public class Shape {public string name;public float[] values;}
        [Serializable] public class PoseExport {public string[] bones;public float[] values;public Shape[] shapes;public float fps=30;}
        public static void Preview(){AquariumCharacterImport.Run();Routes();AquariumFeedingChecks.Geometry();AquariumFeedingChecks.JawGeometry();Review();}
        public static void Build()
        {
            AquariumCharacterImport.Run();Routes();AquariumFeedingChecks.Geometry();AquariumFeedingChecks.JawGeometry();
            Review();AquariumFeedingChecks.Studio();AquariumCharacterChecks.Run();AquariumCharacterChecks.SwimReview();AquariumCharacterChecks.SpitReview();
            foreach(string scene in new[]{"MouthEat","TentacleEat","AllyEat"}){Environment.SetEnvironmentVariable("AQUARIUM_QA_SCENARIO",scene);AquariumCharacterChecks.InTank();}
            AquariumCharacterChecks.ReleaseReview();AquariumBuild.Windows();
        }
        public static void Finish()
        {
            AquariumCharacterImport.Run();AquariumCharacterChecks.SwimReview();AquariumCharacterChecks.SpitReview();
            foreach(string scene in new[]{"MouthEat","TentacleEat","AllyEat"}){Environment.SetEnvironmentVariable("AQUARIUM_QA_SCENARIO",scene);AquariumCharacterChecks.InTank();}
            AquariumCharacterChecks.ReleaseReview();AquariumBuild.Windows();
        }
        public static void Routes()
        {
            int count=0;
            foreach(float size in new[]{.4f,1f,3.5f,8f})foreach(float pitch in new[]{-80f,-40f,0f,40f,80f})for(int yaw=0;yaw<360;yaw+=10)
            {
                Vector3 source=Quaternion.Euler(pitch,yaw,0)*Vector3.forward*(size+1);
                float stage=size*.7f+.5f,clearance=size+.65f;
                for(int i=0;i<=66;i++)
                {
                    float t=i/100f;Vector3 p=AquariumFeeding.StagePoint(source,Vector3.zero,Vector3.forward,Vector3.up,stage,clearance,t);
                    if(float.IsNaN(p.x)||float.IsInfinity(p.y))throw new Exception("Invalid staging path");
                    if(i==0 && Vector3.Distance(p,source)>.00001f)throw new Exception("Prey jumps at route start");
                    if(t>=AquariumFeeding.AlignmentEnd && Vector3.Distance(p,Vector3.forward*stage)>.00001f)throw new Exception("Suction starts outside the frontal staging point");
                    if(Vector3.Angle(source,Vector3.forward)>41 && t>=.12f && t<=.48f && p.magnitude<clearance-.0001f)throw new Exception("Lateral/rear route crosses the body clearance");
                    if(t<AquariumFeeding.AlignmentEnd && AquariumFeeding.Suction(t)>0)throw new Exception("Meal is ingested before frontal alignment");
                    count++;
                }
            }
            Debug.Log("AQUARIUM_FRONT_STAGING_OK "+count+" samples; all bearings, five elevations, four sizes");
        }
        public static void Review()
        {
            string output=Environment.GetEnvironmentVariable("AQUARIUM_CHARACTER_QA");Directory.CreateDirectory(output);
            foreach(string scenario in new[]{"WrapFish","WrapAlien","WrapLargeAlien","WrapAngledAlien","WrapSideFish","WrapRearFish","WrapRearAlien"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.25f,.31f,.38f);RenderSettings.fog=false;
                var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.9f;light.transform.rotation=Quaternion.Euler(35,-35,0);
                var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.4f;fill.transform.rotation=Quaternion.Euler(30,150,0);
                bool fish=scenario.EndsWith("Fish");float scale=scenario=="WrapLargeAlien"?3.5f:1;
                var predator=new GameObject("Predator").AddComponent<AquariumCreature>();predator.Initialize(fish?0:-1,0);
                var holder=new GameObject("Prey position").transform;holder.position=new Vector3(scenario=="WrapAngledAlien"?.55f:0,scenario=="WrapAngledAlien"?.18f:0,scale>1?2.6f:1.4f);
                if(scenario.Contains("Rear"))holder.position=new Vector3(0,0,-1.4f);
                if(scenario.Contains("Side"))holder.position=new Vector3(1.4f,0,0);
                holder.rotation=Quaternion.LookRotation(predator.FeedingThroat-holder.position);holder.localScale=Vector3.one*scale;
                var prey=new GameObject("Meal").AddComponent<AquariumCreature>();prey.transform.SetParent(holder,false);prey.Initialize(1,0);
                var cam=new GameObject("Camera").AddComponent<Camera>();cam.transform.position=new Vector3(2.8f,.65f,2);cam.transform.LookAt(new Vector3(0,-.03f,.45f));cam.orthographic=true;cam.orthographicSize=scale>1?1.8f:.92f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.018f,.047f,.067f);cam.nearClipPlane=.01f;
                var rt=new RenderTexture(1100,820,24);cam.targetTexture=rt;
                if(scenario.Contains("Rear")||scenario.Contains("Side")){cam.orthographicSize=1.75f;cam.transform.position=new Vector3(3,2.4f,3);cam.transform.LookAt(new Vector3(0,0,.2f));}
                var values=new List<float>();var shapeValues=new List<float>[0];AquariumRig rig=null;
                int count=fish?127:91;float maximumBoneStretch=0;
                for(int frame=0;frame<count;frame++)
                {
                    float age=frame/30f;
                    predator.Present(fish?(age<AquariumFeeding.ReturnToFish?100:0):-1,fish?"Bite":"TentacleEat",0,age,0);
                    prey.Present(1,"Eaten",0,age,0);prey.PresentIngestion(predator,age);
                    if(age>=AquariumFeeding.AlignmentEnd)
                    {
                        var skin=prey.GetComponentInChildren<AquariumRig>().skin;Vector3 centre=skin.transform.TransformPoint(skin.sharedMesh.bounds.center);
                        if(Vector3.Angle(centre-predator.FeedingThroat,predator.FeedingForward)>3)throw new Exception("Actual meal failed frontal alignment");
                    }
                    if(!fish)
                    {
                        if(rig==null){rig=predator.GetComponentInChildren<AquariumRig>();shapeValues=Enumerable.Range(0,rig.skin.sharedMesh.blendShapeCount).Select(_=>new List<float>()).ToArray();}
                        for(int i=0;i<rig.skin.bones.Length;i++)
                        {
                            var b=rig.skin.bones[i];var p=b.localPosition;var q=b.localRotation;var s=b.localScale;
                            values.AddRange(new[]{p.x,p.y,p.z,q.x,q.y,q.z,q.w,s.x,s.y,s.z});
                            if(float.IsNaN(p.x)||float.IsNaN(q.x))throw new Exception("Invalid grasp transform");
                            if((b.name.StartsWith("Arm")||b.name.StartsWith("Foot")||b.name.StartsWith("Tail")) && !b.name.EndsWith("0"))
                            {
                                float stretch=p.magnitude/rig.restPositions[i].magnitude;maximumBoneStretch=Mathf.Max(maximumBoneStretch,stretch);
                                if(Mathf.Abs(stretch-1)>.002f)throw new Exception("Tentacle joint length changed");
                            }
                        }
                        for(int i=0;i<shapeValues.Length;i++)shapeValues[i].Add(rig.skin.GetBlendShapeWeight(i));
                    }
                    if(frame%2==0)AquariumCharacterChecks.Capture(cam,rt,Path.Combine(output,scenario+"-"+frame.ToString("000")+".png"));
                }
                if(!fish)File.WriteAllText(Path.Combine(output,scenario+".json"),JsonUtility.ToJson(new PoseExport{bones=rig.skin.bones.Select(b=>b.name).ToArray(),values=values.ToArray(),shapes=Enumerable.Range(0,shapeValues.Length).Select(i=>new Shape{name=rig.skin.sharedMesh.GetBlendShapeName(i),values=shapeValues[i].ToArray()}).ToArray()}));
                cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
                Debug.Log("AQUARIUM_GRASP_REVIEW_OK "+scenario+" frames="+count+" maxBoneLengthRatio="+maximumBoneStretch);
            }
        }
    }
}

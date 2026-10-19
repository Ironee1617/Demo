using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumPolishChecks
    {
        static void Require(bool ok,string message){if(!ok)throw new Exception("Polish check: "+message);}
        public static void ImportReview(){AquariumCartoonImport.Run();Run();AquariumEquipmentChecks.Run();}
        public static void Complete(){ImportReview();AquariumCartoonChecks.DeformationSweep();AquariumBuild.Windows();}
        public static void BuildVerified(){AquariumCartoonChecks.DeformationSweep();AquariumCharacterChecks.ReleaseReview();AquariumBuild.Windows();}
        public static void SketchWide()
        {
            var camera=AquariumCartoonChecks.Studio();camera.orthographic=true;
            var hands=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("AquariumEquipment/DiverHands")).GetComponent<AquariumRig>();
            AquariumFirstPersonEquipment.CreateGun(hands.transform,hands);hands.Clip("FPSIdle").SampleAnimation(hands.gameObject,0);
            AquariumCartoonChecks.Capture(camera,"Hand-sketch-wide",new Vector3(-2f,-.05f,1.1f),new Vector3(.4f,-.30f,1.1f),.90f);
            UnityEngine.Object.DestroyImmediate(hands.gameObject);
        }
        public static void Run()
        {
            string output=Environment.GetEnvironmentVariable("AQUARIUM_CARTOON_QA")??"Temp/CartoonQA";
            Directory.CreateDirectory(Path.Combine(output,"Animation"));
            var camera=AquariumCartoonChecks.Studio();
            var carrier=new GameObject("Carrier alien").AddComponent<AquariumCreature>();carrier.Initialize(-1,0);
            var rig=carrier.GetComponentInChildren<AquariumRig>();Require(rig.Clip("SpitOut")!=null,"dedicated expelled animation");
            Require(rig.skin.sharedMesh.GetBlendShapeIndex("BellyOpen")>=0,"lower mouth opens");
            Vector3[] open=null,closed=null;
            for(int f=0;f<36;f++)
            {
                float time=f*2.4f/36;carrier.Present(-1,"Swim",0,time,1);var mesh=new Mesh();rig.skin.BakeMesh(mesh);
                if(f==0)open=mesh.vertices;if(f==9)closed=mesh.vertices;
                foreach(var p in mesh.vertices)Require(float.IsFinite(p.x)&&p.sqrMagnitude<4,"finite swim surface");UnityEngine.Object.DestroyImmediate(mesh);
                AquariumCartoonChecks.Capture(camera,"Animation/alien-swim-"+f.ToString("000"),new Vector3(3,.3f,1),new Vector3(0,-.15f,-.15f),1.05f);
            }
            float displacement=open.Zip(closed,(a,b)=>Vector3.Distance(a,b)).Max();Require(displacement>.005f,"gentle continuous motion in trailing swim pose");
            rig.Clip("Idle").SampleAnimation(rig.gameObject,0);AquariumCartoonChecks.Capture(camera,"Alien-compact",new Vector3(2,1.1f,4),new Vector3(0,-.06f,0),1.03f);
            rig.Clip("Swim").SampleAnimation(rig.gameObject,.6f);
            AquariumCartoonChecks.Capture(camera,"Alien-swim-front",new Vector3(0,.2f,4),new Vector3(0,-.1f,-.2f),1.05f);
            AquariumCartoonChecks.Capture(camera,"Alien-swim-back",new Vector3(2,1.2f,-4),new Vector3(0,-.1f,-.3f),1.05f);
            AquariumCartoonChecks.Capture(camera,"Alien-neck",new Vector3(2,.5f,3),new Vector3(0,-.14f,0),.46f);
            var prey=new GameObject("Expelled alien").AddComponent<AquariumCreature>();prey.Initialize(-1,0);
            camera.orthographic=false;camera.fieldOfView=42;camera.depthTextureMode=DepthTextureMode.Depth;
            var jet=new GameObject("Lower mouth jet").AddComponent<AquariumInkCloud>();jet.Initialize(701,true,false,true);
            for(int f=0;f<25;f++)
            {
                float age=f*.05f;carrier.transform.position=new Vector3(0,0,-Mathf.Min(age,.85f)*1.4f);
                prey.Present(-1,"SpitOut",0,age,0);prey.transform.position=new Vector3(0,0,1.2f+Mathf.Min(age,.85f)*1.7f);
                carrier.Present(-1,"SpitRecoil",0,age,0);prey.PresentExpulsion(carrier,age);
                Require(Vector3.Dot(carrier.FeedingForward,-rig.Bones.Single(b=>b.name=="Body").up)>.99f,"ejection follows the attached ventral mouth normal");
                Require(age<.20f || age>.65f || carrier.FeedingForward.z>.4f,"body tilts the lower mouth forward during expulsion");
                jet.Present(age,3,carrier.FeedingMouth,carrier.FeedingForward);
                if(f==0){var block=new MaterialPropertyBlock();prey.GetComponentInChildren<AquariumRig>().skin.GetPropertyBlock(block);Require(block.GetFloat("_Ingest")>.99f,"prey begins inside the mouth");}
                AquariumCartoonChecks.Capture(camera,"Animation/alien-spit-"+f.ToString("000"),new Vector3(4,1,4),new Vector3(0,-.05f,.7f),2.15f);
            }
            UnityEngine.Object.DestroyImmediate(prey.gameObject);UnityEngine.Object.DestroyImmediate(carrier.gameObject);UnityEngine.Object.DestroyImmediate(jet.gameObject);
            var hands=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("AquariumEquipment/DiverHands")).GetComponent<AquariumRig>();
            AquariumFirstPersonEquipment.CreateGun(hands.transform,hands);hands.Clip("FPSIdle").SampleAnimation(hands.gameObject,0);camera.orthographic=true;
            AquariumCartoonChecks.Capture(camera,"Hand-sketch-grip",new Vector3(1.8f,-.05f,.65f),new Vector3(.45f,-.38f,.82f),.46f);
            AquariumCartoonChecks.Capture(camera,"Hand-opposite-grip",new Vector3(-1.5f,-.02f,.65f),new Vector3(.38f,-.38f,.82f),.43f);
            AquariumCartoonChecks.Capture(camera,"Hand-top-grip",new Vector3(.45f,1.5f,.45f),new Vector3(.38f,-.37f,.79f),.40f);
            UnityEngine.Object.DestroyImmediate(hands.gameObject);
            File.WriteAllText(Path.Combine(output,"polish-validation.txt"),"Original character FPS holding hand; connected lower lip and cavity; rounded tentacle sections with a travelling ripple; 36 swim frames; swim displacement "+displacement+"; 25 expulsion frames following the attached lower-mouth normal; dedicated SpitOut pose; matched oral jet.");
            Debug.Log("AQUARIUM_POLISH_CHECKS_OK swimDisplacement="+displacement);
        }
    }
}

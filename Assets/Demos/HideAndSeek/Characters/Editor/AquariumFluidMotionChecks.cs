using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumFluidMotionChecks
    {
        static string Output=>Environment.GetEnvironmentVariable("AQUARIUM_CARTOON_QA");
        static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
        static AquariumRig Load(string name)=>UnityEngine.Object.Instantiate(Resources.Load<GameObject>("AquariumCharacters/"+name)).GetComponent<AquariumRig>();
        static Vector3[] Bake(AquariumRig rig){var mesh=new Mesh();rig.skin.BakeMesh(mesh);var p=mesh.vertices;UnityEngine.Object.DestroyImmediate(mesh);return p;}
        static float Delta(Vector3[] a,Vector3[] b){float maximum=0;for(int i=0;i<a.Length;i++)maximum=Mathf.Max(maximum,Vector3.Distance(a[i],b[i]));return maximum;}
        static float TentacleDelta(AquariumRig rig,Vector3[] a,Vector3[] b){var uv=rig.skin.sharedMesh.uv;float maximum=0;for(int i=0;i<a.Length;i++)if((int)(uv[i].x*4)+4*(int)(uv[i].y*4)==15)maximum=Mathf.Max(maximum,Vector3.Distance(a[i],b[i]));return maximum;}
        public static void ImportAndCheck(){AquariumCartoonImport.Run();Check();}
        public static void ImportPreview(){AquariumCartoonImport.Run();Extra();Preview();}
        public static void ExtraPreview(){Extra();Preview();}
        public static void FinalReview(){AquariumCartoonImport.Run();Check();Extra();Preview();AquariumBuild.Windows();}
        public static void Extra()
        {
            string folder=Path.Combine(Output,"ExtraContacts");Directory.CreateDirectory(folder);
            Vector3[] locations={new Vector3(0,0,1.65f),new Vector3(1.65f,0,0),new Vector3(0,0,-1.65f),new Vector3(0,1.5f,.5f),new Vector3(0,-1.5f,.5f)};
            float worstAim=0;
            for(int scenario=0;scenario<6;scenario++)
            {
                AquariumCartoonChecks.Studio();bool fish=scenario==5;
                var carrier=new GameObject("Facing carrier").AddComponent<AquariumCreature>();carrier.Initialize(fish?0:-1,0);
                var holder=new GameObject("Prey location");holder.transform.position=fish?new Vector3(0,0,1.5f):locations[scenario];holder.transform.rotation=Quaternion.Euler(0,180,0);
                var prey=new GameObject("NPC meal").AddComponent<AquariumCreature>();prey.transform.SetParent(holder.transform,false);prey.Initialize(0,0);
                for(int frame=0;frame<=72;frame++)
                {
                    float age=frame/30f;carrier.Present(fish?100:-1,fish?"Bite":"TentacleEat",0,age,0);prey.Present(0,"Eaten",0,age,0);prey.PresentIngestion(carrier,age);
                    if(!fish && frame<=18)Require(carrier.transform.position.magnitude<.0001f,"Carrier must turn at its idle position before approaching");
                    if(!fish && frame==36){var preyRig=prey.GetComponentInChildren<AquariumRig>();Vector3 head=preyRig.skin.transform.TransformPoint(preyRig.skin.sharedMesh.bounds.center);float error=Vector3.Angle(carrier.FeedingForward,head-carrier.FeedingMouth);worstAim=Mathf.Max(worstAim,error);Require(error<6,"Mouth misses prey body centre: "+scenario+" "+error);}
                    if(frame%3==0)AquariumDownstrokeMealChecks.DumpPair(Path.Combine(folder,(fish?"npc-fish":"angle-"+scenario)+"-"+frame.ToString("000")+".mesh"),carrier,prey);
                }
            }
            File.WriteAllText(Path.Combine(Output,"facing-validation.txt"),"Five target directions; maximum mouth-axis error = "+worstAim+" degrees. NPC fish feeding geometry also sampled.");
            Debug.Log("FLUID_MOUTH_FACING_OK "+worstAim);
        }
        public static void Preview()
        {
            Directory.CreateDirectory(Path.Combine(Output,"Animation"));var camera=AquariumCartoonChecks.Studio();
            var carrier=new GameObject("Preview carrier").AddComponent<AquariumCreature>();carrier.Initialize(-1,0);
            var holder=new GameObject("Preview victim root");holder.transform.position=new Vector3(0,-.1f,1.65f);holder.transform.rotation=Quaternion.Euler(0,180,0);
            var prey=new GameObject("Preview victim").AddComponent<AquariumCreature>();prey.transform.SetParent(holder.transform,false);prey.Initialize(0,0);
            for(int frame=0;frame<66;frame++)
            {
                float now=frame/12f,age=now-1;carrier.Present(-1,age<0?"Locomotion":age<3?"TentacleEat":"Locomotion",1,now,0);
                prey.gameObject.SetActive(age<AquariumModel.SwallowVisualSeconds);
                if(prey.gameObject.activeSelf){prey.Present(0,age<0?"Locomotion":"Eaten",1,now,0);if(age>=0)prey.PresentIngestion(carrier,age);}
                AquariumCartoonChecks.Capture(camera,"Animation/facing-meal-"+frame.ToString("000"),new Vector3(4,.6f,2.8f),new Vector3(0,.10f,.75f),1.3f);
            }
            camera=AquariumCartoonChecks.Studio();var fish=Load("Fish0");
            foreach(float time in new[]{0f,.25f,.5f,.9f}){fish.Clip("Bite").SampleAnimation(fish.gameObject,time);AquariumCartoonChecks.Capture(camera,"fish-refined-"+Mathf.RoundToInt(time*100),new Vector3(1.7f,.2f,3),Vector3.zero,.65f);}
            Debug.Log("FLUID_PREVIEW_OK");
        }
        public static void Check()
        {
            Directory.CreateDirectory(Output);var camera=AquariumCartoonChecks.Studio();var rig=Load("Alien");
            Vector3[] first=null;float idleTravel=0,maxStep=0;Vector3[] last=null;
            for(int f=0;f<=240;f++)
            {
                rig.SampleLocomotion(0,f/30f);var p=Bake(rig);if(first==null)first=p;
                idleTravel=Mathf.Max(idleTravel,TentacleDelta(rig,first,p));if(last!=null)maxStep=Mathf.Max(maxStep,TentacleDelta(rig,last,p));last=p;
                if(f==0||f==40||f==80)AquariumCartoonChecks.Capture(camera,"idle-"+f,new Vector3(2,.7f,3),Vector3.zero,1.1f);
            }
            Require(idleTravel>.045f,"Idle tentacles do not ripple: "+idleTravel);Require(maxStep<.025f,"Idle animation jumps: "+maxStep);
            float switchJump=0;
            for(int f=241;f<300;f++)rig.SampleLocomotion(4,f/30f);
            float now=299f/30;var before=Bake(rig);rig.SamplePose("TentacleEat",0,now);switchJump=Mathf.Max(switchJump,Delta(before,Bake(rig)));
            for(int f=1;f<=90;f++)rig.SamplePose("TentacleEat",f/30f,now+f/30f);
            now+=3;before=Bake(rig);rig.SampleLocomotion(0,now);switchJump=Mathf.Max(switchJump,Delta(before,Bake(rig)));
            for(int f=1;f<=30;f++)rig.SampleLocomotion(0,now+f/30f);
            now+=1;before=Bake(rig);rig.SamplePose("SpitRecoil",0,now);switchJump=Mathf.Max(switchJump,Delta(before,Bake(rig)));
            for(int f=1;f<=36;f++)rig.SamplePose("SpitRecoil",f/30f,now+f/30f);
            now+=1.2f;before=Bake(rig);rig.SampleLocomotion(0,now);switchJump=Mathf.Max(switchJump,Delta(before,Bake(rig)));
            Require(switchJump<.0001f,"Animation source pose discontinuity: "+switchJump);
            UnityEngine.Object.DestroyImmediate(rig.gameObject);
            var fish=Load("Fish0");var uv=fish.skin.sharedMesh.uv;var closed=Bake(fish);float eyeMove=0;
            for(int f=0;f<=16;f++)
            {
                fish.Clip("Bite").SampleAnimation(fish.gameObject,f/16f);var p=Bake(fish);
                // Eyes share the body bone. Compare in its local frame to ignore swimming sway.
                int key=fish.skin.sharedMesh.GetBlendShapeIndex("Alien_Mouth_Open");var dv=new Vector3[p.Length];fish.skin.sharedMesh.GetBlendShapeFrameVertices(key,0,dv,null,null);
                for(int i=0;i<p.Length;i++){int m=(int)(uv[i].x*4)+4*(int)(uv[i].y*4);if(m==2||m==5)eyeMove=Mathf.Max(eyeMove,dv[i].magnitude);}
                if(f==0||f==8||f==16)AquariumCartoonChecks.Capture(camera,"fish-mouth-"+f,new Vector3(1.7f,.2f,3),Vector3.zero,.65f);
            }
            Require(eyeMove<.000001f,"Mouth animation moves or shrinks fish eyes");UnityEngine.Object.DestroyImmediate(fish.gameObject);
            File.WriteAllText(Path.Combine(Output,"fluid-motion-validation.json"),"{\"idleTravel\":"+idleTravel.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"maximumIdleFrameStep\":"+maxStep.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"transitionStartJump\":"+switchJump.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"eyeMouthDisplacement\":"+eyeMove.ToString(System.Globalization.CultureInfo.InvariantCulture)+"}");
            Debug.Log("FLUID_MOTION_OK idleTravel="+idleTravel+" step="+maxStep+" switchJump="+switchJump+" eyeMove="+eyeMove);
            AquariumDownstrokeMealChecks.Review();AquariumDownstrokeMealChecks.SnapshotReview();AquariumFeedingChecks.Geometry();
        }
    }
}

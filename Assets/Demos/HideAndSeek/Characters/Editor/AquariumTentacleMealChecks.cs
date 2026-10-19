using System;
using System.IO;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumTentacleMealChecks
    {
        public static void ImportReview(){AquariumCartoonImport.Run();Review();}
        public static void ImportComplete(){AquariumCartoonImport.Run();Complete();}
        public static void Complete(){Review();AquariumFeedingChecks.Geometry();AquariumAlienFitChecks.Review();AquariumCartoonChecks.DeformationSweep();AquariumCharacterChecks.ReleaseReview();AquariumBuild.Windows();}
        public static void Review()
        {
            string output=Environment.GetEnvironmentVariable("AQUARIUM_CARTOON_QA");Directory.CreateDirectory(Path.Combine(output,"Animation"));
            Directory.CreateDirectory(Path.Combine(output,"Contacts"));
            var camera=AquariumCartoonChecks.Studio();
            RenderSettings.ambientLight=new Color(.32f,.38f,.44f);
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))light.intensity*=.65f;
            var carrier=new GameObject("Capturing alien").AddComponent<AquariumCreature>();carrier.Initialize(-1,0);
            var prey=new GameObject("Captured alien").AddComponent<AquariumCreature>();prey.Initialize(-1,0);
            var jet=new GameObject("Mouth jet").AddComponent<AquariumInkCloud>();jet.Initialize(704,true,false,true);
            camera.depthTextureMode=DepthTextureMode.Depth;
            for(int f=0;f<144;f++)
            {
                float time=f/24f;
                if(time<3.35f)
                {
                    carrier.transform.position=Vector3.zero;carrier.Present(-1,"TentacleEat",.35f,time,0);
                    prey.Present(-1,time<.35f?"Idle":"Eaten",.35f,time,0);prey.transform.position=new Vector3(0,-.10f,1.65f);
                    if(time>=.35f)prey.PresentIngestion(carrier,time-.35f);
                }
                else
                {
                    float age=time-3.35f;
                    carrier.transform.position=new Vector3(0,0,-Mathf.Min(age,.85f)*1.4f);carrier.Present(-1,"SpitRecoil",3.35f,time,0);
                    prey.Present(-1,"SpitOut",3.35f,time,0);prey.transform.position=new Vector3(0,0,1.2f+Mathf.Min(age,.85f)*1.7f);
                    prey.PresentExpulsion(carrier,age);jet.Present(age,3,carrier.FeedingMouth,carrier.FeedingForward);
                }
                AquariumCartoonChecks.Capture(camera,"Animation/tentacle-meal-"+f.ToString("000"),new Vector3(4,.45f,3.5f),new Vector3(0,-.22f,.6f),1.83f);
                if(f==24||f==32||f==40||f==90||f==96||f==102)AquariumCartoonChecks.Capture(camera,"Meal-detail-"+f.ToString("000"),new Vector3(3,-.10f,2),new Vector3(0,-.40f,.30f),1.13f);
                if(f>=12&&f<=56&&f%2==0 || f>=82&&f<=102&&f%2==0)
                    DumpPair(Path.Combine(output,"Contacts","meal-"+f.ToString("000")+".mesh"),carrier,prey);
            }
            UnityEngine.Object.DestroyImmediate(prey.gameObject);UnityEngine.Object.DestroyImmediate(carrier.gameObject);UnityEngine.Object.DestroyImmediate(jet.gameObject);
            File.WriteAllText(Path.Combine(output,"meal-preview.txt"),"144 frames: runtime tentacle capture, dedicated Eaten reaction, oral pull and expulsion. Current prefab and runtime material deformation.");
            Debug.Log("TENTACLE_MEAL_REVIEW_OK");
        }
        static void DumpPair(string path,AquariumCreature carrier,AquariumCreature prey)
        {
            using(var writer=new BinaryWriter(File.Create(path)))foreach(var creature in new[]{carrier,prey})
            {
                var rig=creature.GetComponentInChildren<AquariumRig>();var mesh=new Mesh();rig.skin.BakeMesh(mesh);
                var points=mesh.vertices;var triangles=mesh.triangles;var block=new MaterialPropertyBlock();rig.skin.GetPropertyBlock(block);
                float ingest=block.GetFloat("_Ingest");Vector4 mouth=block.GetVector("_IngestMouth");
                writer.Write(points.Length);writer.Write(triangles.Length);
                foreach(var p in points)
                {
                    Vector3 world=rig.skin.transform.TransformPoint(p);
                    if(ingest>0)world=AquariumFeeding.IngestPoint(world,block.GetVector("_IngestOrigin"),block.GetVector("_IngestAxis"),block.GetFloat("_IngestHalfLength"),mouth,mouth.w,block.GetFloat("_IngestDepth"),ingest,block.GetFloat("_IngestSeal")>.5f);
                    writer.Write(world.x);writer.Write(world.y);writer.Write(world.z);
                }
                foreach(int i in triangles)writer.Write(i);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }
}

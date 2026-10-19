using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumDownstrokeMealChecks
    {
        [Serializable] class Sample
        {
            public float age,headDrift,carrierTravel,carrierStep;
            public int form;
            public bool transitioning;
            public Vector3 mouth,carrier,head;
        }
        [Serializable] class Report { public int species;public Sample[] samples; }
        public static void ImportComplete(){AquariumCartoonImport.Run();Complete();}
        public static void Complete(){Review();SnapshotReview();AquariumFeedingChecks.Geometry();AquariumBuild.Windows();}
        public static void VerifyAndBuild(){SnapshotReview();AquariumFeedingChecks.Geometry();AquariumBuild.Windows();}
        public static void Review()
        {
            string output=Environment.GetEnvironmentVariable("AQUARIUM_CARTOON_QA");
            Directory.CreateDirectory(Path.Combine(output,"Animation"));Directory.CreateDirectory(Path.Combine(output,"Contacts"));
            foreach(int species in new[]{-1,0,1,2,3,4})ReviewPair(output,species,false);
            Debug.Log("PREDATOR_ENGULF_REVIEW_OK all six victim forms");
        }
        static Vector3 Head(AquariumCreature creature)
        {
            var rig=creature.GetComponentInChildren<AquariumRig>();
            return rig.cartoonKind=="Alien"?rig.BodyPoint(new Vector3(0,.22f,0)):rig.skin.transform.TransformPoint(rig.skin.sharedMesh.bounds.center);
        }
        static void Require(bool yes,string message){if(!yes)throw new Exception(message);}
        static void ReviewPair(string output,int species,bool render)
        {
            string name=species<0?"alien":"fish"+species;
            var camera=AquariumCartoonChecks.Studio();RenderSettings.ambientLight=new Color(.32f,.38f,.44f);
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))light.intensity*=.65f;
            var carrierRoot=new GameObject("Predator root");var preyRoot=new GameObject("Victim root");
            var carrier=new GameObject("Engulfing alien").AddComponent<AquariumCreature>();carrier.transform.SetParent(carrierRoot.transform,false);carrier.Initialize(-1,0);
            var prey=new GameObject("Swallowed disguise").AddComponent<AquariumCreature>();prey.transform.SetParent(preyRoot.transform,false);prey.Initialize(species,0);
            var jet=new GameObject("Mouth jet").AddComponent<AquariumInkCloud>();jet.Initialize(704,true,false,true);
            camera.depthTextureMode=DepthTextureMode.Depth;var samples=new List<Sample>();Vector3 last=Vector3.zero;float travel=0;
            for(int f=0;f<144;f++)
            {
                float time=f/24f,age=time-.35f,drift=0;
                if(time<3.35f)
                {
                    carrierRoot.transform.position=Vector3.zero;
                    carrier.Present(-1,time<.35f?"Idle":"TentacleEat",.35f,time,0);
                    preyRoot.transform.position=new Vector3(0,-.10f,1.65f);
                    preyRoot.transform.rotation=species<0?Quaternion.identity:Quaternion.Euler(0,180,0);
                    prey.gameObject.SetActive(age<AquariumModel.SwallowVisualSeconds);
                    if(prey.gameObject.activeSelf)
                    {
                        prey.Present(species,time<.35f?"Idle":"Eaten",.35f,time,0);
                        Vector3 before=Head(prey);
                        if(time>=.35f)prey.PresentIngestion(carrier,age);
                        drift=Vector3.Distance(before,Head(prey));
                        if(age>=0 && age<AquariumFeeding.AlienSuctionStart)Require(drift<.001f,"Victim is pulled during approach: "+name+" "+drift);
                        Require(prey.Form==species && !prey.Transitioning,"Disguise changes before swallowing: "+name);
                    }
                    if(age>.7f&&age<1.15f)travel=Mathf.Max(travel,carrier.transform.position.magnitude);
                    if(age>2.95f)Require(carrier.transform.localPosition.magnitude<.001f,"Carrier did not settle after swallowing");
                }
                else
                {
                    age=time-3.35f;
                    carrierRoot.transform.position=new Vector3(0,0,-Mathf.Min(age,.85f)*1.4f);carrier.Present(-1,"SpitRecoil",3.35f,time,0);
                    prey.gameObject.SetActive(true);preyRoot.transform.rotation=Quaternion.identity;
                    preyRoot.transform.position=new Vector3(0,0,1.2f+Mathf.Min(age,.85f)*1.7f);
                    prey.Present(-1,"SpitOut",3.35f,time,0);prey.PresentExpulsion(carrier,age);
                    jet.Present(age,3,carrier.FeedingMouth,carrier.FeedingForward);
                    Require(prey.Form==-1&&!prey.Transitioning,"Expulsion must show alien without an external morph: "+name);
                }
                float step=Vector3.Distance(last,carrier.transform.position);last=carrier.transform.position;
                Require(step<.36f,"Predator position jumps: "+name+" frame "+f+" "+step);
                samples.Add(new Sample{age=time-.35f,headDrift=drift,carrierTravel=carrier.transform.position.magnitude,carrierStep=step,form=prey.Form,transitioning=prey.Transitioning,mouth=carrier.FeedingMouth,carrier=carrier.transform.position,head=prey.gameObject.activeSelf?Head(prey):Vector3.zero});
                if(render)AquariumCartoonChecks.Capture(camera,"Animation/engulf-"+name+"-"+f.ToString("000"),new Vector3(4,.75f,3.5f),new Vector3(0,.35f,.85f),2.14f);
                if(render && (f==24||f==32||f==40||f==90||f==96||f==102))AquariumCartoonChecks.Capture(camera,"Engulf-"+name+"-detail-"+f.ToString("000"),new Vector3(3,.35f,2.4f),new Vector3(0,.0f,1.1f),1.48f);
                if((f>=12&&f<=66&&f%2==0 || f>=82&&f<=102&&f%2==0)&&prey.gameObject.activeSelf)
                    DumpPair(Path.Combine(output,"Contacts",name+"-"+f.ToString("000")+".mesh"),carrier,prey);
            }
            // The previous overhead orbit travelled farther than a direct,
            // mouth-facing approach. Validate the actual final separation.
            var approached=samples.Where(s=>s.age>1.08f && s.age<1.20f).ToArray();
            Require(travel>.45f && approached.All(s=>Vector3.Distance(s.mouth,s.head)<.90f),"Predator must bring its mouth to the victim: "+name+" travel="+travel);
            File.WriteAllText(Path.Combine(output,name+"-engulf-validation.json"),JsonUtility.ToJson(new Report{species=species,samples=samples.ToArray()},true));
            UnityEngine.Object.DestroyImmediate(preyRoot);UnityEngine.Object.DestroyImmediate(carrierRoot);UnityEngine.Object.DestroyImmediate(jet.gameObject);
        }
        internal static void DumpPair(string path,AquariumCreature carrier,AquariumCreature prey)
        {
            using(var writer=new BinaryWriter(File.Create(path)))foreach(var creature in new[]{carrier,prey})
            {
                var rig=creature.GetComponentInChildren<AquariumRig>();var mesh=new Mesh();rig.skin.BakeMesh(mesh);
                var points=mesh.vertices;var triangles=mesh.triangles;var uv=mesh.uv;var block=new MaterialPropertyBlock();rig.skin.GetPropertyBlock(block);
                float ingest=block.GetFloat("_Ingest");Vector4 mouth=block.GetVector("_IngestMouth");
                writer.Write(creature.Form);writer.Write(points.Length);writer.Write(triangles.Length);
                foreach(var p in points)
                {
                    Vector3 world=rig.skin.transform.TransformPoint(p);
                    if(ingest>0)world=AquariumFeeding.IngestPoint(world,block.GetVector("_IngestOrigin"),block.GetVector("_IngestAxis"),block.GetFloat("_IngestHalfLength"),mouth,mouth.w,block.GetFloat("_IngestDepth"),ingest,block.GetFloat("_IngestSeal")>.5f);
                    writer.Write(world.x);writer.Write(world.y);writer.Write(world.z);
                }
                foreach(var p in uv)writer.Write((int)(p.x*4)+4*(int)(p.y*4));
                foreach(int i in triangles)writer.Write(i);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
        public static void SnapshotReview()
        {
            string output=Environment.GetEnvironmentVariable("AQUARIUM_CARTOON_QA");Directory.CreateDirectory(output);
            foreach(int previous in new[]{-1,2})
            {
                var partial=new GameObject("Interrupted disguise check").AddComponent<AquariumCreature>();partial.Initialize(previous,0);
                partial.Present(0,"Locomotion",0,10,0);partial.Present(0,"Locomotion",0,10.2f,0);
                Require(partial.Transitioning,"Set up partial disguise");partial.Present(0,"Eaten",10.2f,10.2f,0);
                Require(partial.Form==0&&!partial.Transitioning,"Capture must keep an identifiable fish during an unfinished disguise");
                partial.Present(-1,"SpitOut",13.2f,13.2f,0);Require(partial.Form==-1&&!partial.Transitioning,"Hidden reveal after partial disguise");
                UnityEngine.Object.DestroyImmediate(partial.gameObject);
            }
            foreach(int species in new[]{-1,0,1,2,3,4})foreach(bool reverse in new[]{false,true})
            {
                AquariumCartoonChecks.Studio();var parent=new GameObject("Snapshot feeding verification");var view=new AquariumView(parent.transform);
                var model=new AquariumModel();model.AddPlayer(1,"Predator",Team.Hider);model.AddPlayer(2,"Diver",Team.Seeker);model.SetRole(2,Team.Seeker,true);model.AddPlayer(3,"Victim",Team.Hider);model.SetRole(3,Team.Hider,true);
                Require(model.StartRound(),"Start match");model.Players.RemoveAll(p=>p.filled);while(model.Time<model.HuntAt+.1f)model.Tick(1f/30);
                var carrier=model.Player(1);var victim=model.Player(3);carrier.position=new Point(2.3f,7,4);carrier.facing=new Point(1,0,0);victim.position=new Point(3.6f,7,4);victim.facing=new Point(-1,0,0);victim.species=species;victim.revealUntil=0;model.Player(2).position=new Point(-20,8,-5);
                for(int i=0;i<model.Fish.Count;i++){var fish=model.Fish[i];fish.position=AquariumModel.Constrain(new Point(-25+(i%4)*1.6f,8,-6+(i/4)*1.5f),.6f);fish.facing=new Point();fish.turnAt=float.MaxValue;}
                if(reverse)model.Players.Reverse();
                // Warm the view while the victim is visibly a fish, then use the
                // real model and JSON snapshots for the complete capture/release.
                view.Draw(JsonUtility.FromJson<AquariumSnapshot>(JsonUtility.ToJson(model.Snapshot())),-1,1f/30);
                var visiblePrey=parent.GetComponentsInChildren<AquariumCreature>().OrderBy(c=>(c.transform.position-new Vector3(3.6f,7,4)).sqrMagnitude).First();
                model.SetInput(1,new SwimInput{aimX=1,action=true});int captured=0,released=0;
                for(int f=0;f<165;f++)
                {
                    model.Tick(1f/30);var state=JsonUtility.FromJson<AquariumSnapshot>(JsonUtility.ToJson(model.Snapshot()));view.Draw(state,-1,1f/30);
                    var snapshotVictim=state.players.First(p=>p.id==3);
                    if(snapshotVictim.swallowedBy!=0 && state.time-snapshotVictim.visualAt<2)
                    {
                        Require(snapshotVictim.swallowedSpecies==species && visiblePrey.Form==species && !visiblePrey.Transitioning,"Snapshot lost captured disguise");captured++;
                    }
                    if(snapshotVictim.visualAction=="SpitOut")
                    {
                        if(visiblePrey==null)visiblePrey=parent.GetComponentsInChildren<AquariumCreature>().OrderBy(c=>(c.transform.parent.position-new Vector3(victim.position.x,victim.position.y,victim.position.z)).sqrMagnitude).First();
                        Require(visiblePrey.Form==-1 && !visiblePrey.Transitioning,"Snapshot expulsion shows fish or transitional body");released++;
                    }
                    if(species==0&&!reverse&&(f==24||f==50||f==95||f==105))
                    {
                        view.Camera.transform.position=new Vector3(3.0f,9.1f,11.7f);view.Camera.transform.LookAt(new Vector3(3.1f,7,4));view.Camera.fieldOfView=43;
                        var rt=new RenderTexture(1280,850,24);view.Camera.targetTexture=rt;
                        AquariumCharacterChecks.Capture(view.Camera,rt,Path.Combine(output,"Game-disguise-"+f.ToString("000")+".png"));
                        view.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
                    }
                }
                Require(captured>40 && released>50,"Snapshot capture and release were not exercised");UnityEngine.Object.DestroyImmediate(parent);
            }
            File.WriteAllText(Path.Combine(output,"snapshot-validation.txt"),"12 full model-to-JSON-to-view capture/release scenarios: alien plus 5 fish disguises, both roster orders; fish retained during capture; alien visible at expulsion; no transition mesh leaks.");
            Debug.Log("PREDATOR_SNAPSHOT_REVIEW_OK 12 scenarios");
        }
    }
}

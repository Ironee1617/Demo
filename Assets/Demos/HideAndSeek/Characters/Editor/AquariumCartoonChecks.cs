using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumCartoonChecks
    {
        static string Output=>Environment.GetEnvironmentVariable("AQUARIUM_CARTOON_QA")??"Temp/CartoonQA";
        static int checks;
        static void Require(bool yes,string text){checks++;if(!yes)throw new Exception("Cartoon check: "+text);}
        static AquariumRig Load(string name)=>UnityEngine.Object.Instantiate(Resources.Load<GameObject>((name.StartsWith("Diver")?"AquariumEquipment/":"AquariumCharacters/")+name)).GetComponent<AquariumRig>();
        internal static Camera Studio()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.59f,.65f);
            var key=new GameObject("Soft key").AddComponent<Light>();key.type=LightType.Directional;key.shadows=LightShadows.None;key.intensity=1.3f;key.transform.rotation=Quaternion.Euler(35,-35,0);
            var fill=new GameObject("Cool fill").AddComponent<Light>();fill.type=LightType.Directional;fill.shadows=LightShadows.None;fill.intensity=.45f;fill.color=new Color(.75f,.88f,1);fill.transform.rotation=Quaternion.Euler(25,140,0);
            var camera=new GameObject("Review camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.105f,.15f,.20f);camera.orthographic=true;camera.orthographicSize=1.48f;camera.nearClipPlane=.01f;camera.farClipPlane=30;return camera;
        }
        internal static void Capture(Camera camera,string file,Vector3 position,Vector3 at,float size=1.48f)
        {
            // Batch editor renders do not advance the GPU skinning player loop.
            // Bake the sampled pose for an exact image of the geometry being checked.
            var skins=UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            var proxies=new System.Collections.Generic.List<GameObject>();
            foreach(var skin in skins){var mesh=new Mesh();skin.BakeMesh(mesh);var go=new GameObject("Sampled skin");go.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);go.transform.localScale=skin.transform.lossyScale;go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=skin.sharedMaterials;var properties=new MaterialPropertyBlock();skin.GetPropertyBlock(properties);renderer.SetPropertyBlock(properties);renderer.receiveShadows=skin.receiveShadows;skin.enabled=false;proxies.Add(go);}
            camera.transform.position=position;camera.transform.LookAt(at);camera.orthographicSize=size;
            bool animation=file.StartsWith("Animation/");var rt=new RenderTexture(animation?720:1200,animation?600:1000,24){antiAliasing=4};camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
            var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,file+".png"),image.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
            foreach(var p in proxies){UnityEngine.Object.DestroyImmediate(p.GetComponent<MeshFilter>().sharedMesh);UnityEngine.Object.DestroyImmediate(p);}foreach(var skin in skins)skin.enabled=true;
        }
        public static void Run()
        {
            Directory.CreateDirectory(Output);checks=0;var camera=Studio();
            foreach(string name in new[]{"Fish0","Alien","Diver","DiverHands"})
            {
                var rig=Load(name);var mesh=rig.skin.sharedMesh;Require(rig.cartoon,name+" latest model marker");Require(mesh.triangles.Length/3<(name=="Diver"?70000:55000),name+" runtime budget");Require(mesh.subMeshCount<=2,name+" consolidated materials");
                Require(rig.clips.All(c=>c!=null),name+" animation references");Require(rig.skin.sharedMaterials.All(m=>m!=null && !m.shader.name.Contains("Error")),name+" materials");
                foreach(var w in mesh.boneWeights)if(Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)>.001f)throw new Exception("Unnormalized skin weight "+name);
                foreach(var clip in rig.clips)
                {
                    float maximum=0,stretch=0;Vector3[] first=null;var rest=mesh.vertices;var triangles=mesh.triangles;
                    for(int i=0;i<=12;i++)
                    {
                        clip.SampleAnimation(rig.gameObject,clip.length*i/12);var baked=new Mesh();rig.skin.BakeMesh(baked);var v=baked.vertices;
                        if(first==null)first=v;
                        for(int k=0;k<v.Length;k+=7){Require(float.IsFinite(v[k].x)&&v[k].sqrMagnitude<25,name+" stable "+clip.name);maximum=Mathf.Max(maximum,Vector3.Distance(first[k],v[k]));}
                        if(name.StartsWith("Diver"))for(int k=0;k<triangles.Length;k+=3)for(int edge=0;edge<3;edge++)
                        {int a=triangles[k+edge],b=triangles[k+(edge+1)%3];var reference=name=="DiverHands"?first:rest;float length=Vector3.Distance(reference[a],reference[b]);if(length>.002f)stretch=Mathf.Max(stretch,Vector3.Distance(v[a],v[b])/length);}
                        UnityEngine.Object.DestroyImmediate(baked);
                    }
                    Debug.Log("CARTOON_MOTION "+name+" "+clip.name+" displacement="+maximum);
                    if(name.StartsWith("Diver")){Debug.Log("CARTOON_SKIN_STRETCH "+name+" "+clip.name+" ratio="+stretch);if(stretch>=4.5f)DiverDiagnostics();Require(stretch<4.5f,name+" no stretched skin triangles "+clip.name);}
                    Require(maximum>.001f,name+" motion "+clip.name+" displacement="+maximum);
                }
                rig.Clip("Idle").SampleAnimation(rig.gameObject,0);
                if(name=="Fish0"||name=="Alien")
                {
                    Capture(camera,name+"-idle",new Vector3(2,1.1f,4),Vector3.zero,name=="Alien"?1.1f:.85f);
                    rig.Clip("Bite").SampleAnimation(rig.gameObject,.85f);Capture(camera,name+"-attack",new Vector3(1.7f,.4f,4),new Vector3(0,-.05f,0),1.1f);
                    rig.Clip("Idle").SampleAnimation(rig.gameObject,0);rig.SetCartoonFold(1);Capture(camera,name+"-matched",new Vector3(2,1.1f,4),Vector3.zero,.6f);
                }
                if(name=="Diver")
                {
                    var gun=AquariumFirstPersonEquipment.CreateGun(rig.transform,rig);Require(gun.transform.parent==rig.gunSocket,"gun attached to hand");
                    for(int frame=0;frame<12;frame++)
                    {
                        rig.Clip("Swim").SampleAnimation(rig.gameObject,frame*.1f);var head=rig.Bones.Single(b=>b.name=="Head");Require(Vector3.Dot(head.forward,Vector3.forward)>.97f,"diver face looks forward while swimming");Require(Vector3.Dot(gun.transform.forward,Vector3.forward)>.999f,"gun keeps aim during swimming");
                        if(frame%3==0)Capture(camera,"diver-swim-"+frame,new Vector3(3.4f,.5f,3.5f),Vector3.zero,1.3f);
                    }
                    rig.Clip("Swim").SampleAnimation(rig.gameObject,.1f);Capture(camera,"diver-side",new Vector3(4,.05f,0),Vector3.zero,1.2f);Capture(camera,"diver-grip",new Vector3(2.0f,.1f,2.2f),gun.transform.position,.48f);
                }
                UnityEngine.Object.DestroyImmediate(rig.gameObject);
            }
            var creature=new GameObject("Matched transition integration").AddComponent<AquariumCreature>();creature.Initialize(0,0);
            for(int i=0;i<100;i++)creature.Present(i<50?-1:0,"Locomotion",0,i*.04f,1);
            Require(!creature.Transitioning && creature.Form==0,"complete fish alien fish transition");UnityEngine.Object.DestroyImmediate(creature.gameObject);
            foreach(var s in Resources.LoadAll<AquariumSpecies>("AquariumCharacters"))Require(s.normal==s.feeding&&s.normal==s.closedFeeding,"continuous fish mouth forms");
            File.WriteAllText(Path.Combine(Output,"validation.txt"),checks+" assertions passed. Approved cartoon models; normalized weights; finite animation samples; face-forward swimming; attached gun and stable aim; matched transformation; continuous fish mouth.");Debug.Log("CARTOON_CHECKS_OK "+checks);
        }
        public static void DeformationSweep()
        {
            Directory.CreateDirectory(Path.Combine(Output,"Animation"));var camera=Studio();var fish=Load("Fish0");var alien=Load("Alien");
            foreach(var rig in new[]{fish,alien})
            {
                Vector3[] previous=null;float maximumStep=0;
                for(int sample=0;sample<=120;sample++)
                {
                    float fold=sample<=60?sample/60f:(120-sample)/60f;rig.Clip("Swim").SampleAnimation(rig.gameObject,sample/30f);rig.SetCartoonFold(fold);
                    var baked=new Mesh();rig.skin.BakeMesh(baked);var vertices=baked.vertices;
                    foreach(var v in vertices)Require(float.IsFinite(v.x)&&v.magnitude<(fold==1?.46f:1.5f),"bounded complete morph "+rig.name);
                    if(previous!=null)for(int i=0;i<vertices.Length;i++)maximumStep=Mathf.Max(maximumStep,Vector3.Distance(previous[i],vertices[i]));previous=vertices;UnityEngine.Object.DestroyImmediate(baked);
                }
                Require(maximumStep<.22f,"no isolated morph spikes "+rig.name);Debug.Log("CARTOON_MORPH_SWEEP "+rig.name+" samples=121 maxStep="+maximumStep);
            }
            for(int f=0;f<=36;f++)
            {
                bool first=f<=18;fish.gameObject.SetActive(first);alien.gameObject.SetActive(!first);var rig=first?fish:alien;
                rig.Clip("Idle").SampleAnimation(rig.gameObject,0);rig.SetCartoonFold(first?Mathf.SmoothStep(0,1,f/18f):1-Mathf.SmoothStep(0,1,(f-18)/18f));
                Capture(camera,"Animation/morph-"+f.ToString("000"),new Vector3(2,1.1f,4),Vector3.zero,1.12f);
            }
            UnityEngine.Object.DestroyImmediate(fish.gameObject);UnityEngine.Object.DestroyImmediate(alien.gameObject);
            var diver=Load("Diver");AquariumFirstPersonEquipment.CreateGun(diver.transform,diver);
            for(int f=0;f<24;f++){diver.Clip("Swim").SampleAnimation(diver.gameObject,f*.05f);Capture(camera,"Animation/swim-"+f.ToString("000"),new Vector3(3.5f,.8f,3.2f),new Vector3(0,0,.25f),1.45f);}
            UnityEngine.Object.DestroyImmediate(diver.gameObject);File.WriteAllText(Path.Combine(Output,"deformation-validation.txt"),"242 sampled morph poses: finite, bounded geometry and no isolated vertex spikes. Both exact midpoint surfaces stay inside 0.46 m radius. 24 horizontal flutter-kick frames rendered.");
            Debug.Log("CARTOON_DEFORMATION_SWEEP_OK");
        }
        public static void Review(){Run();DeformationSweep();AquariumEquipmentChecks.Run();}
        public static void DiverDiagnostics()
        {
            Directory.CreateDirectory(Output);var camera=Studio();var rig=Load("Diver");var gun=AquariumFirstPersonEquipment.CreateGun(rig.transform,rig);rig.Clip("Swim").SampleAnimation(rig.gameObject,.1f);
            Capture(camera,"diver-side",new Vector3(4,.05f,0),Vector3.zero,1.4f);Capture(camera,"diver-grip",new Vector3(2,.1f,2.2f),gun.transform.position,.48f);Capture(camera,"diver-fins",new Vector3(2.3f,.3f,-3),new Vector3(0,-.17f,-.8f),.65f);
            var mesh=rig.skin.sharedMesh;var rest=mesh.vertices;var triangles=mesh.triangles;var baked=new Mesh();rig.skin.BakeMesh(baked);var pose=baked.vertices;var edges=new System.Collections.Generic.List<Tuple<float,int,int>>();
            for(int k=0;k<triangles.Length;k+=3)for(int e=0;e<3;e++){int a=triangles[k+e],b=triangles[k+(e+1)%3];float length=Vector3.Distance(rest[a],rest[b]);if(length>.002f)edges.Add(Tuple.Create(Vector3.Distance(pose[a],pose[b])/length,a,b));}
            var lines=new System.Collections.Generic.List<string>();foreach(var item in edges.OrderByDescending(x=>x.Item1).Take(20)){lines.Add("Stretch "+item.Item1);foreach(int i in new[]{item.Item2,item.Item3}){var w=mesh.boneWeights[i];lines.Add(i+" rest "+rest[i].ToString("F5")+" pose "+pose[i].ToString("F5")+" bones "+rig.Bones[w.boneIndex0].name+":"+w.weight0+" "+rig.Bones[w.boneIndex1].name+":"+w.weight1+" "+rig.Bones[w.boneIndex2].name+":"+w.weight2+" "+rig.Bones[w.boneIndex3].name+":"+w.weight3);}}
            File.WriteAllLines(Path.Combine(Output,"diver-diagnostics.txt"),lines);Debug.Log("DIVER_DIAGNOSTICS_READY");
        }
        public static void ImportAndReview(){AquariumCartoonImport.Run();Review();}
        public static void ImportReviewAndBuild(){ImportAndReview();AquariumBuild.Windows();}
        public static void CheckAndBuild(){Review();AquariumBuild.Windows();}
    }
}

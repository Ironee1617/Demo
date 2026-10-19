using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumCharacterChecks
    {
        public static void TankAndBuild(){InTank();AquariumBuild.Windows();}
        public static void ReleaseAndBuild(){ReleaseReview();AquariumBuild.Windows();}
        public static void TentaclePreview(){AquariumCharacterImport.Run();SwimReview();SpitReview();}
        public static void SpitAndBuild(){AquariumCharacterImport.Run();Run();SwimReview();SpitReview();ReleaseReview();AquariumBuild.Windows();}
        public static void ReleaseReview()
        {
            string output=Environment.GetEnvironmentVariable("AQUARIUM_CHARACTER_QA");Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var parent=new GameObject("Two-sided release check");var view=new AquariumView(parent.transform);
            var model=new AquariumModel();model.AddPlayer(1,"Carrier",Team.Hider);model.AddPlayer(2,"Diver",Team.Seeker);model.SetRole(2,Team.Seeker,true);model.AddPlayer(3,"Swallowed",Team.Hider);model.SetRole(3,Team.Hider,true);
            if(!model.StartRound())throw new Exception("Release review failed to start");model.Players.RemoveAll(p=>p.filled);
            // Meals are disabled during the shared hiding countdown.
            while(model.Time<model.HuntAt+.1f)model.Tick(1f/30);
            var carrier=model.Player(1);var expelled=model.Player(3);
            carrier.position=new Point(2.3f,7,4);carrier.facing=new Point(1,0,0);expelled.position=new Point(3.6f,7,4);expelled.facing=new Point(-1,0,0);model.Player(2).position=new Point(-20,8,-5);
            for(int i=0;i<model.Fish.Count;i++){var fish=model.Fish[i];fish.position=AquariumModel.Constrain(new Point(-25+(i%4)*1.6f,8,-6+(i/4)*1.5f),.6f);fish.facing=new Point();fish.turnAt=float.MaxValue;}
            model.SetInput(1,new SwimInput{aimX=1,action=true});
            view.Camera.transform.position=new Vector3(3.0f,9.1f,11.7f);view.Camera.transform.LookAt(new Vector3(3.1f,7,4));view.Camera.fieldOfView=43;
            var rt=new RenderTexture(1280,850,24);view.Camera.targetTexture=rt;
            bool released=false;Point carrierAtRelease=new Point(),expelledAtRelease=new Point();float releasedAt=0;
            for(int frame=0;frame<165;frame++)
            {
                model.Tick(1f/30);
                // Exercise the same serializable fields used by remote players.
                var state=JsonUtility.FromJson<AquariumSnapshot>(JsonUtility.ToJson(model.Snapshot()));
                if(!released && carrier.visualAction=="SpitRecoil")
                {
                    released=true;carrierAtRelease=carrier.position;expelledAtRelease=expelled.position;releasedAt=model.Time;
                    if(expelled.visualAction!="SpitOut" || carrier.visualAt!=expelled.visualAt)throw new Exception("Both release motions must start together");
                }
                view.Draw(state,-1,1f/30);Capture(view.Camera,rt,Path.Combine(output,"Release-"+frame.ToString("000")+".png"));
            }
            float backwards=carrierAtRelease.x-carrier.position.x,forwards=expelled.position.x-expelledAtRelease.x;
            if(!released || backwards<1.4f || forwards<1.4f || Point.Dot(carrier.facing,expelled.facing)<.99f || carrier.facing.x<.99f)throw new Exception("Carrier must recoil backwards without turning away from the expelled alien");
            File.WriteAllText(Path.Combine(output,"release-validation.txt"),"Release time: "+releasedAt+"; carrier recoil: "+backwards+"m; expelled travel: "+forwards+"m; synchronized snapshot animation; 165 frames.");
            Debug.Log("AQUARIUM_RELEASE_REVIEW_OK carrier="+backwards+" expelled="+forwards);
            view.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(parent);
        }
        public static void InTank()
        {
            string output=Environment.GetEnvironmentVariable("AQUARIUM_CHARACTER_QA");Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            foreach(string scenario in new[]{"MouthEat","TentacleEat","AllyEat","Transform","Unmorph","Harpoon"})
            {
                string selected=Environment.GetEnvironmentVariable("AQUARIUM_QA_SCENARIO");if(!string.IsNullOrEmpty(selected) && selected!=scenario)continue;
                var parent=new GameObject("Character gameplay check");var view=new AquariumView(parent.transform);
                var p=new Swimmer{id=1,team=Team.Hider,position=new Point(2.3f,7,4),facing=new Point(0,0,1),species=scenario=="TentacleEat"||scenario=="AllyEat"||scenario=="Transform"?-1:0};
                var f=new Prey{id=1,species=1,position=new Point(2.3f,7,5.15f),facing=new Point(.25f,0,-1)};
                var state=new AquariumSnapshot{time=10,phase=RoundPhase.Playing,players=new[]{p},fish=new[]{f},harpoons=new Harpoon[0],effects=new WaterEffect[0],eggs=new RevivalEgg[0]};
                if(scenario=="Unmorph" || scenario=="Transform")state.fish=new Prey[0];
                var ally=new Swimmer{id=2,team=Team.Hider,species=-1,position=f.position,facing=f.facing};
                if(scenario=="AllyEat"){state.players=new[]{p,ally};state.fish=new Prey[0];}
                view.Camera.transform.position=new Vector3(4.2f,8.0f,7.4f);view.Camera.transform.LookAt(new Vector3(2.3f,7,4.5f));view.Camera.fieldOfView=40;
                var rt=new RenderTexture(1280,900,24);view.Camera.targetTexture=rt;
                for(int frame=0;frame<105;frame++)
                {
                    state.time=10+frame*.1f;
                    if(frame==4)
                    {
                        if(scenario=="Transform"){p.species=0;p.morphAt=state.time;}
                        else if(scenario=="Unmorph")p.species=-1;
                        else if(scenario=="Harpoon"){f.dead=true;f.visualAt=state.time;}
                        else
                        {
                            p.visualAction=scenario=="MouthEat"?"Bite":"TentacleEat";p.visualAt=state.time;f.eaten=true;f.eater=1;f.visualAt=state.time;
                            if(scenario=="AllyEat"){ally.swallowedFrom=ally.position;ally.swallowedBy=1;ally.visualAction="Eaten";ally.visualAt=state.time;}
                            state.effects=new[]{new WaterEffect{id=1,owner=1,kind=scenario=="AllyEat"?"allyEat":"eat",position=f.position,direction=p.facing,born=state.time,duration=AquariumModel.TraceDuration}};
                        }
                    }
                    view.Draw(state,-1,.1f);Capture(view.Camera,rt,Path.Combine(output,"Tank-"+scenario+"-"+frame.ToString("00")+".png"));
                    if(scenario=="MouthEat" && frame>=36 && frame<=44)
                    {
                        var predator=parent.GetComponentsInChildren<AquariumCreature>().First(c=>c.Form==0 || c.Form>=100);
                        if(predator.Form>=100 || predator.Transitioning)throw new Exception("Chewing must use completely closed fish geometry");
                    }
                }
                view.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(parent);
            }
            Debug.Log("AQUARIUM_CHARACTER_TANK_CHECKS_OK");
        }
        [MenuItem("Aquarium/Characters/Validate and Render")]
        public static void Run()
        {
            var current=Resources.Load<GameObject>("AquariumCharacters/Alien");
            if(current!=null && current.GetComponent<AquariumRig>().cartoon){AquariumCartoonChecks.Run();return;}
            string output=Environment.GetEnvironmentVariable("AQUARIUM_CHARACTER_QA");if(string.IsNullOrEmpty(output))output="Temp/CharacterQA";Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.46f,.53f,.63f);RenderSettings.fog=false;
            var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(40,-35,0);
            var rim=new GameObject("Rim").AddComponent<Light>();rim.type=LightType.Directional;rim.intensity=.7f;rim.color=new Color(.4f,.7f,1);rim.transform.rotation=Quaternion.Euler(30,150,0);
            var cam=new GameObject("Camera").AddComponent<Camera>();cam.transform.position=new Vector3(1.7f,.65f,2.4f);cam.transform.LookAt(Vector3.zero);cam.orthographic=true;cam.orthographicSize=.90f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.018f,.047f,.067f);cam.nearClipPlane=.01f;
            var rt=new RenderTexture(800,800,24);cam.targetTexture=rt;
            int total=0;
            foreach(string name in new[]{"Alien","Fish0","Fish0Mouth"})
            {
                var prefab=Resources.Load<GameObject>("AquariumCharacters/"+name);if(prefab==null)throw new Exception("Missing rig "+name);
                var rig=UnityEngine.Object.Instantiate(prefab).GetComponent<AquariumRig>();rig.skin.updateWhenOffscreen=true;
                if(rig.clips.Length!=(name=="Alien"?9:8)||rig.skin.sharedMesh.blendShapeCount<1||rig.envelope==null)throw new Exception("Incomplete animation/morph set "+name);
                foreach(var w in rig.skin.sharedMesh.boneWeights)if(Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)>.001f)throw new Exception("Invalid weights");
                foreach(var clip in rig.clips)
                {
                    clip.SampleAnimation(rig.gameObject,0);var first=new Mesh();rig.skin.BakeMesh(first);
                    float movement=0;
                    for(int frame=0;frame<12;frame++)
                    {
                        clip.SampleAnimation(rig.gameObject,clip.length*frame/12);var baked=new Mesh();rig.skin.BakeMesh(baked);
                        var a=first.vertices;var b=baked.vertices;for(int v=0;v<a.Length;v+=23)movement=Mathf.Max(movement,Vector3.Distance(a[v],b[v]));
                        UnityEngine.Object.DestroyImmediate(baked);Capture(cam,rt,Path.Combine(output,name+"-"+clip.name+"-"+frame.ToString("00")+".png"));total++;
                    }
                    UnityEngine.Object.DestroyImmediate(first);if(movement<.001f)throw new Exception("Animation is static: "+name+"/"+clip.name);
                    Debug.Log("CLIP_CHECK "+name+" "+clip.name+" maximum displacement="+movement);
                }
                UnityEngine.Object.DestroyImmediate(rig.gameObject);
            }
            foreach(var pair in new[]{new[]{-1,0},new[]{0,-1},new[]{0,1},new[]{0,100},new[]{100,0}})
            {
                var go=new GameObject("Transition");var creature=go.AddComponent<AquariumCreature>();creature.Initialize(pair[0],0);
                creature.Present(pair[1],"Locomotion",0,0,1);
                for(int frame=0;frame<=20;frame++)
                {creature.Present(pair[1],"Locomotion",0,frame*.075f,1);Capture(cam,rt,Path.Combine(output,"Transition-"+pair[0]+"-"+pair[1]+"-"+frame.ToString("00")+".png"));total++;}
                if(creature.Transitioning || creature.Form!=pair[1])throw new Exception("Incomplete morph transition");UnityEngine.Object.DestroyImmediate(go);
            }
            File.WriteAllText(Path.Combine(output,"validation.txt"),"25 animated clips; 5 transitions; normalized weights; "+total+" render samples.\n");
            cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);Debug.Log("AQUARIUM_CHARACTER_CHECKS_OK "+total);
        }
        public static void SwimReview()
        {AnimatedLimbReview("Swim",144,3,true);}
        public static void SpitReview()
        {AnimatedLimbReview("SpitRecoil",72,2,false);}
        static void AnimatedLimbReview(string motion,int steps,int stride,bool loop)
        {
            string output=Environment.GetEnvironmentVariable("AQUARIUM_CHARACTER_QA");Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.26f,.33f,.39f);RenderSettings.fog=false;
            var key=new GameObject("Soft key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=.8f;key.transform.rotation=Quaternion.Euler(35,-35,0);
            var rim=new GameObject("Soft rim").AddComponent<Light>();rim.type=LightType.Directional;rim.intensity=.45f;rim.color=new Color(.4f,.7f,1);rim.transform.rotation=Quaternion.Euler(30,150,0);
            var cam=new GameObject("Camera").AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=.85f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.018f,.047f,.067f);cam.nearClipPlane=.01f;
            var rt=new RenderTexture(800,800,24);cam.targetTexture=rt;
            var rig=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("AquariumCharacters/Alien")).GetComponent<AquariumRig>();
            var clip=rig.Clip(motion);var baked=new Mesh();
            clip.SampleAnimation(rig.gameObject,0);rig.skin.BakeMesh(baked);var first=baked.vertices;
            clip.SampleAnimation(rig.gameObject,clip.length);rig.skin.BakeMesh(baked);var last=baked.vertices;
            float seam=0;for(int i=0;i<first.Length;i++)seam=Mathf.Max(seam,Vector3.Distance(first[i],last[i]));
            if(loop && seam>.001f)throw new Exception(motion+" loop seam: "+seam);
            float maximumStep=0;Vector3[] previous=null;
            for(int frame=0;frame<steps;frame++)
            {
                clip.SampleAnimation(rig.gameObject,clip.length*frame/steps);rig.skin.BakeMesh(baked);var vertices=baked.vertices;
                if(previous!=null)for(int i=0;i<vertices.Length;i++)maximumStep=Mathf.Max(maximumStep,Vector3.Distance(vertices[i],previous[i]));
                previous=vertices;
                if(frame%stride!=0)continue;
                var views=new[]{new Vector3(1.7f,.55f,2.4f),new Vector3(2.4f,.1f,0),new Vector3(1.5f,.45f,-2.4f)};
                for(int v=0;v<views.Length;v++)
                {cam.transform.position=views[v];cam.transform.LookAt(Vector3.zero);Capture(cam,rt,Path.Combine(output,motion+"-"+v+"-"+(frame/stride).ToString("00")+".png"));}
            }
            if(maximumStep>.12f)throw new Exception(motion+" pose jumps between frames: "+maximumStep);
            if(motion=="SpitRecoil")
            {
                clip.SampleAnimation(rig.gameObject,.25f);
                foreach(string name in new[]{"ArmL7","ArmR7","FootL5","FootR5","Tail7"})
                {
                    var tip=rig.skin.bones.First(b=>b.name==name);
                    // Blender's +Y bone axis becomes Unity's -Z in this importer.
                    if(Vector3.Dot(tip.TransformDirection(Vector3.back),Vector3.forward)<.55f)throw new Exception("Spitting tentacle must point forward: "+name);
                }
                clip.SampleAnimation(rig.gameObject,clip.length);
                if(rig.skin.GetBlendShapeWeight(rig.skin.sharedMesh.GetBlendShapeIndex("BellyOpen"))>.01f)throw new Exception("Hidden belly mouth must close after spitting");
            }
            File.WriteAllText(Path.Combine(output,motion+"-validation.txt"),"Bones: "+rig.skin.bones.Length+"; endpoint difference: "+seam+"; max 60 Hz vertex step: "+maximumStep+"; "+steps/stride*3+" rendered views.");
            Debug.Log("AQUARIUM_"+motion.ToUpperInvariant()+"_REVIEW_OK seam="+seam+" step="+maximumStep);
            UnityEngine.Object.DestroyImmediate(baked);cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(rig.gameObject);
        }
        internal static void Capture(Camera cam,RenderTexture rt,string path)
        {
            // Batch-mode camera renders can reuse skinning buffers within one editor frame.
            // Bake the evaluated pose so every captured image represents this exact sample.
            var skins=UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None).Where(s=>s.enabled).ToArray();
            var meshes=new Mesh[skins.Length];var proxies=new GameObject[skins.Length];
            for(int i=0;i<skins.Length;i++)
            {
                meshes[i]=new Mesh();skins[i].BakeMesh(meshes[i]);var proxy=new GameObject("Pose capture");proxies[i]=proxy;proxy.transform.SetParent(skins[i].transform,false);
                // BakeMesh includes inherited skeleton scale; do not apply it a
                // second time through the capture proxy's parent hierarchy.
                var inherited=skins[i].transform.parent==null?Vector3.one:skins[i].transform.parent.lossyScale;
                proxy.transform.localScale=new Vector3(1/inherited.x,1/inherited.y,1/inherited.z);
                proxy.AddComponent<MeshFilter>().sharedMesh=meshes[i];var renderer=proxy.AddComponent<MeshRenderer>();renderer.sharedMaterials=skins[i].sharedMaterials;
                var block=new MaterialPropertyBlock();skins[i].GetPropertyBlock(block);int maw=skins[i].sharedMesh.GetBlendShapeIndex("BellyOpen");if(maw>=0)block.SetFloat("_MawOpen",skins[i].GetBlendShapeWeight(maw)/100);renderer.SetPropertyBlock(block);skins[i].enabled=false;
            }
            cam.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=previous;
            for(int i=0;i<skins.Length;i++){skins[i].enabled=true;UnityEngine.Object.DestroyImmediate(proxies[i]);UnityEngine.Object.DestroyImmediate(meshes[i]);}
        }
    }
}

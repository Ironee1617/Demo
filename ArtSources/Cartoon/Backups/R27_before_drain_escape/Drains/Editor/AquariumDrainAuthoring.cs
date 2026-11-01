using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumDrainAuthoring
    {
        const string Root = "Assets/Demos/HideAndSeek/Drains";
        const string ResourcesPath = Root + "/Resources/AquariumDrains";
        static Transform Find(Transform root, string name) { return root.GetComponentsInChildren<Transform>(true).First(t => t.name == name); }

        [MenuItem("Aquarium/Return drains/Import model and bake sites")]
        public static void ImportAndBake()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Root + "/Art/ReturnDrain.fbx");
            importer.globalScale = 1; importer.useFileScale = true; importer.isReadable = true;
            importer.importAnimation = false; importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/ReturnDrain.fbx");
            var go = UnityEngine.Object.Instantiate(source); go.name = "ReturnDrain";
            try
            {
                var mats = new Dictionary<string, Material>();
                string[] names = {"Brushed marine steel", "Machined silver edges", "Pipe interior", "EPDM seal", "Nickel bronze screws", "Inlaid flow markers"};
                Color[] colors = {new Color(.34f,.46f,.48f), new Color(.58f,.68f,.68f),new Color(.055f,.13f,.15f),new Color(.018f,.032f,.035f),new Color(.52f,.42f,.25f),new Color(.68f,.78f,.73f)};
                float[] metals = {.72f,.8f,.45f,0,.74f,.25f}, smooth = {.48f,.64f,.39f,.24f,.54f,.45f};
                var env = UnityEngine.Resources.Load<GameObject>(AquariumEnvironment.ResourcePath);
                var sand = env.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).First(m => m.shader.name == "Aquarium/Natural Granular Sand");
                for (int i=0;i<names.Length;i++)
                {
                    string path = ResourcesPath + "/" + names[i].Replace(" ", "_") + ".mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null) { mat = new Material(Shader.Find("Aquarium/Marine Hardware")); AssetDatabase.CreateAsset(mat,path); }
                    mat.name=names[i];mat.SetColor("_Color",colors[i]);mat.SetFloat("_Metallic",metals[i]);mat.SetFloat("_Smoothness",smooth[i]);
                    mat.SetTexture("_Caustics",sand.GetTexture("_Caustics"));mat.enableInstancing=true;EditorUtility.SetDirty(mat);mats.Add(names[i],mat);
                }
                // Material names and submesh assignments are read from the exporter manifest.
                var manifest=JsonUtility.FromJson<MeshManifest>(File.ReadAllText(Root+"/Art/material-slots.json"));
                foreach(var filter in go.GetComponentsInChildren<MeshFilter>())
                {
                    var slots=manifest.meshes.First(m=>m.name==filter.name).materials;
                    filter.GetComponent<Renderer>().sharedMaterials=slots.Select(s=>mats[s]).ToArray();
                }
                var marker=go.AddComponent<AquariumDrain>(); marker.lid=Find(go.transform,"Lid");marker.opening=Find(go.transform,"Opening");marker.pipeExit=Find(go.transform,"PipeExit");
                for(int i=0;i<6;i++)marker.screws[i]=Find(go.transform,"Screw_"+(i+1).ToString("00"));
                // All six screws share the same mesh; rotation stays independent at their own pivots.
                var screwMesh=marker.screws[0].GetComponent<MeshFilter>().sharedMesh;
                for(int i=1;i<6;i++)marker.screws[i].GetComponent<MeshFilter>().sharedMesh=screwMesh;
                PrefabUtility.SaveAsPrefabAsset(go,ResourcesPath+"/ReturnDrain.prefab");
                AssetDatabase.SaveAssets();
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            BakeSites();
            Debug.Log("AQUARIUM_DRAIN_IMPORT_OK");
        }
        [Serializable] class MeshSlots { public string name; public string[] materials; }
        [Serializable] class MeshManifest { public MeshSlots[] meshes; }

        [MenuItem("Aquarium/Return drains/Rebake clear floor sites")]
        public static void BakeSites()
        {
            const float step=.75f,minX=-34,minZ=-21;const int nx=96,nz=61;
            var blocked=new bool[nx,nz];
            var env=UnityEngine.Resources.Load<GameObject>(AquariumEnvironment.ResourcePath);
            int triangles=0;
            foreach(var filter in env.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<Renderer>();if(renderer==null)continue;
                string shader=renderer.sharedMaterial.shader.name;
                if(shader=="Aquarium/Natural Granular Sand"||shader=="Aquarium/Atmosphere"||shader=="Aquarium/Lagoon Water"||shader=="Aquarium/World Label")continue;
                if(shader.Contains("Glass"))continue;
                var mesh=filter.sharedMesh;if(mesh==null||!mesh.isReadable)continue;
                var vertices=mesh.vertices;var indices=mesh.triangles;var matrix=filter.transform.localToWorldMatrix;
                for(int i=0;i<vertices.Length;i++)vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);
                for(int i=0;i<indices.Length;i+=3)
                {
                    var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                    var lo=Vector3.Min(a,Vector3.Min(b,c));var hi=Vector3.Max(a,Vector3.Max(b,c));
                    if(lo.y>3||hi.y<.12f)continue;triangles++;
                    float pad=filter.name.Contains("rubble & shells")?AquariumDrainLayout.OuterRadius+.12f:AquariumDrainLayout.ClearanceRadius;
                    int x0=Mathf.Max(0,Mathf.CeilToInt((lo.x-pad-minX)/step)),x1=Mathf.Min(nx-1,Mathf.FloorToInt((hi.x+pad-minX)/step));
                    int z0=Mathf.Max(0,Mathf.CeilToInt((lo.z-pad-minZ)/step)),z1=Mathf.Min(nz-1,Mathf.FloorToInt((hi.z+pad-minZ)/step));
                    for(int x=x0;x<=x1;x++)for(int z=z0;z<=z1;z++)
                        if(!blocked[x,z]&&TriangleDistanceSquared(new Vector2(minX+x*step,minZ+z*step),a,b,c)<pad*pad)blocked[x,z]=true;
                }
            }
            var list=new List<Point>();
            for(int x=0;x<nx;x++)for(int z=0;z<nz;z++)
            {
                var p=new Point(minX+x*step,minZ+z*step);
                if(!blocked[x,z]&&AquariumDrainLayout.IsClear(p))list.Add(p);
            }
            if(list.Count<16)throw new Exception("Insufficient clear floor sites: "+list.Count);
            var text=new StringBuilder("// Generated from CoralGallery triangle bounds with 1.65 m clearance. Re-bake after reef changes.\nnamespace PrototypeLab.Aquarium\n{\n    public static class AquariumDrainSites\n    {\n        public static readonly Point[] Points = {\n");
            foreach(var p in list)text.AppendFormat(CultureInfo.InvariantCulture,"            new Point({0:0.###}f, {1:0.###}f),\n",p.x,p.z);
            text.Append("        };\n    }\n}\n");
            File.WriteAllText(Root+"/Runtime/AquariumDrainSites.cs",text.ToString());AssetDatabase.ImportAsset(Root+"/Runtime/AquariumDrainSites.cs");
            Debug.Log("AQUARIUM_DRAIN_SITES "+list.Count+" sites from "+triangles+" near-floor triangles");
        }

        static float SegmentDistanceSquared(Vector2 p,Vector2 a,Vector2 b)
        {var d=b-a;float t=d.sqrMagnitude<.000001f?0:Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);return (p-a-d*t).sqrMagnitude;}
        static float Cross(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
        static float TriangleDistanceSquared(Vector2 p,Vector3 av,Vector3 bv,Vector3 cv)
        {
            var a=new Vector2(av.x,av.z);var b=new Vector2(bv.x,bv.z);var c=new Vector2(cv.x,cv.z);
            float s=Cross(b-a,p-a),t=Cross(c-b,p-b),u=Cross(a-c,p-c);
            if(Mathf.Abs(Cross(b-a,c-a))>.000001f&&((s>=0&&t>=0&&u>=0)||(s<=0&&t<=0&&u<=0)))return 0;
            return Mathf.Min(SegmentDistanceSquared(p,a,b),Mathf.Min(SegmentDistanceSquared(p,b,c),SegmentDistanceSquared(p,c,a)));
        }

        [MenuItem("Aquarium/Return drains/Validate")]
        public static void Validate()
        {
            var prefab=UnityEngine.Resources.Load<GameObject>("AquariumDrains/ReturnDrain");if(prefab==null)throw new Exception("Missing drain prefab");
            var marker=prefab.GetComponent<AquariumDrain>();
            if(marker==null||marker.lid==null||marker.screws.Length!=6||marker.screws.Distinct().Count()!=6)throw new Exception("Lid and six separate screws required");
            foreach(var t in marker.screws)if(t==null||t.IsChildOf(marker.lid))throw new Exception("Screws must be independent of lid");
            var lid=marker.lid.GetComponent<MeshFilter>();var b=lid.sharedMesh.bounds;
            if(b.size.y>.35f||b.size.x<1.8f||b.size.x>2.1f)throw new Exception("FBX units/orientation invalid: "+b);
            int tris=prefab.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
            var pipe=Find(prefab.transform,"Pipe_Throat").GetComponent<MeshFilter>().sharedMesh.bounds;
            if(pipe.size.y<2.5f||pipe.size.x>1.6f)throw new Exception("Pipe size invalid "+pipe);
            for(int seed=0;seed<1000;seed++)
            {
                var sites=AquariumDrainLayout.Create(new System.Random(seed));
                if(sites.Length!=4)throw new Exception("Four drains required");
                if(sites.Count(s=>s.isOpen)!=1)throw new Exception("Exactly one open drain required");
                for(int i=0;i<4;i++)
                {
                    if(!AquariumDrainLayout.IsClear(sites[i].position))throw new Exception("Unsafe site "+seed);
                    for(int j=0;j<i;j++)if((sites[i].position-sites[j].position).Length<15.999f)throw new Exception("Spacing failed "+seed);
                }
            }
            var model=new AquariumModel(912,7);model.AddPlayer(1,"host",Team.Seeker);model.AddPlayer(2,"alien",Team.Hider,true);
            if(model.Snapshot().drains.Length!=0||!model.StartRound())throw new Exception("Lobby/start failed");
            var first=model.Snapshot();var restored=JsonUtility.FromJson<AquariumSnapshot>(JsonUtility.ToJson(first));
            if(restored.drains.Length!=4)throw new Exception("Network round trip failed");
            for(int i=0;i<4;i++)if(!first.drains[i].Equals(restored.drains[i]))throw new Exception("Client position mismatch");
            for(int i=0;i<80;i++)model.Tick(.1f);
            for(int i=0;i<4;i++)if(!first.drains[i].Equals(model.Snapshot().drains[i]))throw new Exception("Drains moved mid round");
            foreach(var player in model.Players)if(player.team==Team.Hider)player.alive=false;
            model.Tick(.1f);if(model.Phase!=RoundPhase.Finished)throw new Exception("Round end failed");
            for(int i=0;i<210;i++)model.Tick(.1f);
            model.ReturnToLobby();if(model.Snapshot().drains.Length!=0)throw new Exception("Lobby retained old sites");
            if(!model.StartRound())throw new Exception("Next round failed");
            if(first.drains.SequenceEqual(model.Snapshot().drains))throw new Exception("Next round reused the entire layout");
            var seededA=new AquariumModel(73,7,141);var seededB=new AquariumModel(73,7,142);
            foreach(var m in new[]{seededA,seededB}){m.AddPlayer(1,"host",Team.Seeker);m.AddPlayer(2,"alien",Team.Hider,true);m.StartRound();}
            if(seededA.Snapshot().drains.SequenceEqual(seededB.Snapshot().drains))throw new Exception("Session seed ignored");
            for(int i=0;i<seededA.Fish.Count;i++)if(!seededA.Fish[i].position.Equals(seededB.Fish[i].position))throw new Exception("Drain randomness changed fish simulation");
            var host=new GameObject("Drain QA host");var view=new AquariumDrainPresentation(host.transform);
            try
            {
                view.Draw(first);view.Draw(restored);
                if(host.GetComponentsInChildren<AquariumDrain>().Length!=4)throw new Exception("Duplicate/missing drain instances");
                if(host.GetComponentsInChildren<AquariumDrain>().Count(d=>d.IsOpen)!=1)throw new Exception("Open drain presentation mismatch");
                if(Shader.GetGlobalInt("_AquariumDrainCount")!=4)throw new Exception("Sand apertures missing");
                view.Draw(new AquariumSnapshot());
                if(host.GetComponentsInChildren<AquariumDrain>().Length!=0||Shader.GetGlobalInt("_AquariumDrainCount")!=0)throw new Exception("Lobby cleanup failed");
            }
            finally{view.Clear();UnityEngine.Object.DestroyImmediate(host);}
            foreach(var material in prefab.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials))
                if(material==null||ShaderUtil.ShaderHasError(material.shader))throw new Exception("Material/shader failed");
            Debug.Log("AQUARIUM_DRAIN_VALIDATED 1000 layouts, 6 screws, 4 instances, spacing >=16m, snapshot round trip; "+tris+" triangles/drain");
        }

        public static void Capture(string output)
        {
            Directory.CreateDirectory(output);
            var scene=EditorSceneManager.NewPreviewScene();
            var oldFog=RenderSettings.fog;var oldAmbient=RenderSettings.ambientLight;var oldMode=RenderSettings.ambientMode;
            var oldSky=RenderSettings.ambientSkyColor;var oldEquator=RenderSettings.ambientEquatorColor;var oldGround=RenderSettings.ambientGroundColor;
            var oldFogColor=RenderSettings.fogColor;var oldFogMode=RenderSettings.fogMode;var oldDensity=RenderSettings.fogDensity;var oldReflection=RenderSettings.reflectionIntensity;
            var oldCount=Shader.GetGlobalInt("_AquariumDrainCount");var oldHoles=new Vector4[4];for(int i=0;i<4;i++)oldHoles[i]=Shader.GetGlobalVector("_AquariumDrain"+i);
            RenderTexture rt=null;Texture2D tex=null;
            try
            {
                RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.36f,.43f,.46f);
                var root=new GameObject("Drain QA preview");SceneManager.MoveGameObjectToScene(root,scene);
                var drain=UnityEngine.Object.Instantiate(UnityEngine.Resources.Load<GameObject>("AquariumDrains/ReturnDrain"),root.transform);
                var d=drain.GetComponent<AquariumDrain>();
                var cam=new GameObject("Preview camera").AddComponent<Camera>();cam.transform.SetParent(root.transform);cam.scene=scene;
                cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.025f,.07f,.09f);cam.nearClipPlane=.02f;cam.farClipPlane=200;cam.fieldOfView=40;
                var light=new GameObject("Soft key").AddComponent<Light>();light.transform.SetParent(root.transform);light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(38,-30,0);
                var fill=new GameObject("Cool fill").AddComponent<Light>();fill.transform.SetParent(root.transform);fill.type=LightType.Directional;fill.color=new Color(.4f,.72f,1);fill.intensity=.7f;fill.transform.rotation=Quaternion.Euler(65,150,0);
                rt=new RenderTexture(1600,1200,24,RenderTextureFormat.ARGB32);rt.antiAliasing=4;rt.Create();cam.targetTexture=rt;
                tex=new Texture2D(1600,1200,TextureFormat.RGB24,false);
                Action<string,Vector3,Vector3> shot=(name,pos,target)=>{
                    cam.transform.position=pos;cam.transform.LookAt(target);cam.Render();var prev=RenderTexture.active;RenderTexture.active=rt;
                    tex.ReadPixels(new Rect(0,0,1600,1200),0,0);tex.Apply();RenderTexture.active=prev;File.WriteAllBytes(Path.Combine(output,name+".png"),tex.EncodeToPNG());
                };
                Shader.SetGlobalInt("_AquariumDrainCount",0);
                shot("01-closed",new Vector3(2.65f,3.2f,-3.5f),new Vector3(0,0,0));
                shot("02-screw",d.screws[0].position+new Vector3(.38f,.5f,-.48f),d.screws[0].position);
                d.lid.localPosition+=Vector3.up*.7f;foreach(var s in d.screws)s.localPosition+=Vector3.up*1.05f;
                shot("03-separated",new Vector3(3.4f,3.7f,-4.8f),new Vector3(0,-.4f,0));
                d.lid.gameObject.SetActive(false);foreach(var s in d.screws)s.gameObject.SetActive(false);
                shot("04-pipe",new Vector3(1.5f,2.8f,-1.9f),new Vector3(0,-.5f,0));
                UnityEngine.Object.DestroyImmediate(drain);
                var env=UnityEngine.Object.Instantiate(UnityEngine.Resources.Load<GameObject>(AquariumEnvironment.ResourcePath),root.transform);
                AquariumEnvironment.ConfigureCamera(cam);
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=AquariumEnvironment.C(0x788b96);RenderSettings.ambientEquatorColor=AquariumEnvironment.C(0x425b68);RenderSettings.ambientGroundColor=AquariumEnvironment.C(0x172e39);
                RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogColor=AquariumEnvironment.C(0x177d8d);RenderSettings.reflectionIntensity=.65f;
                light.color=AquariumEnvironment.C(0xffe6bd);light.intensity=.9f;light.transform.rotation=Quaternion.Euler(57,-34,0);light.shadows=LightShadows.Soft;
                fill.color=AquariumEnvironment.C(0x83c8d5);fill.intensity=.2f;fill.transform.rotation=Quaternion.Euler(32,135,0);
                var layout=AquariumDrainLayout.Create(new System.Random(491));
                var display=new AquariumDrainPresentation(root.transform);display.Draw(new AquariumSnapshot{drains=layout});
                var p=layout[0].position;var at=new Vector3(p.x,p.y,p.z);
                shot("05-in-aquarium",at+new Vector3(3.3f,2.7f,-4.1f),at+Vector3.up*.1f);
                var placed=root.GetComponentsInChildren<AquariumDrain>()[0];placed.lid.gameObject.SetActive(false);foreach(var s in placed.screws)s.gameObject.SetActive(false);
                shot("06-open-in-sand",at+new Vector3(1.7f,2.9f,-2.3f),at-Vector3.up*.3f);
                placed.lid.gameObject.SetActive(true);foreach(var s in placed.screws)s.gameObject.SetActive(true);
                shot("07-layout",new Vector3(0,66,-23),new Vector3(0,0,1));
                File.WriteAllText(Path.Combine(output,"layout.json"),JsonUtility.ToJson(new AquariumSnapshot{drains=layout},true));
                display.Clear();
                Debug.Log("AQUARIUM_DRAIN_PREVIEWS "+output);
            }
            finally
            {
                if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}if(tex!=null)UnityEngine.Object.DestroyImmediate(tex);
                EditorSceneManager.ClosePreviewScene(scene);RenderSettings.fog=oldFog;RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientMode=oldMode;
                RenderSettings.ambientSkyColor=oldSky;RenderSettings.ambientEquatorColor=oldEquator;RenderSettings.ambientGroundColor=oldGround;
                RenderSettings.fogColor=oldFogColor;RenderSettings.fogMode=oldFogMode;RenderSettings.fogDensity=oldDensity;RenderSettings.reflectionIntensity=oldReflection;
                Shader.SetGlobalInt("_AquariumDrainCount",oldCount);for(int i=0;i<4;i++)Shader.SetGlobalVector("_AquariumDrain"+i,oldHoles[i]);
            }
        }
    }
}

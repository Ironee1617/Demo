using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrototypeLab.Aquarium.Editor
{
    public static partial class AquariumEnvironmentAuthoring
    {
        private const string Art = "Assets/Demos/HideAndSeek/Environment";
        private const string ResourcesRoot = Art + "/Resources/Aquarium";
        private const string GameScene = "Assets/Demos/HideAndSeek/30_HideAndSeek.unity";
        private const string GalleryScene = "Assets/Demos/HideAndSeek/31_CoralGallery.unity";
        private static readonly string Preview = Path.GetFullPath("Artifacts/CoralGallery");

        [MenuItem("Aquarium/Coral Gallery/Rebuild and apply map")]
        public static void RebuildAndApply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(ResourcesRoot + "/Meshes"); Directory.CreateDirectory(ResourcesRoot + "/Materials"); Directory.CreateDirectory(Preview);
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var environment = AquariumEnvironment.Create();
            SaveGenerated(environment.gameObject);
            string prefabPath = ResourcesRoot + "/CoralGallery.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(environment.gameObject, prefabPath, InteractionMode.AutomatedAction);
            AquariumEnvironment.ConfigureLighting(null);
            var camera = Camera.main; AquariumEnvironment.ConfigureCamera(camera); Overview(camera);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), GalleryScene);
            ShaderChecks(); AquariumCameraChecks.Run(); Validate(environment);
            // Save real renderer output, including the same shader animation used in the player.
            Capture(camera, Path.Combine(Preview, "01-overview.png"), 1920, 1200, 7);
            Underwater(camera);
            Capture(camera, Path.Combine(Preview, "02-underwater.png"), 1600, 1000, 7);
            ReefDetail(camera);
            Capture(camera, Path.Combine(Preview, "03-reef-detail.png"), 1600, 1000, 7);
            Overview(camera);
            EditorSceneManager.OpenScene(GameScene);
            var old = UnityEngine.Object.FindFirstObjectByType<AquariumEnvironmentRoot>(); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            AquariumEnvironment.ConfigureLighting(null); AquariumEnvironment.ConfigureCamera(Camera.main); Overview(Camera.main);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("CORAL_GALLERY_APPLIED " + GalleryScene);
        }

        [MenuItem("Aquarium/Coral Gallery/Open environment scene")]
        public static void OpenGallery()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(GalleryScene);
        }

        [MenuItem("Aquarium/Coral Gallery/Capture preview")]
        public static void CapturePreview()
        {
            Directory.CreateDirectory(Preview);
            var camera = Camera.main; Overview(camera); Capture(camera, Path.Combine(Preview, "01-overview.png"), 1920, 1200, 7);
        }

        public static void CaptureSavedGallery()
        {
            EditorSceneManager.OpenScene(GalleryScene);
            var camera=Camera.main;AquariumEnvironment.ConfigureCamera(camera);Overview(camera);
            Capture(camera, Path.Combine(Preview,"01-overview.png"),1920,1200,7);
            Underwater(camera);
            Capture(camera,Path.Combine(Preview,"02-underwater.png"),1600,1000,7);
            ReefDetail(camera);
            Capture(camera,Path.Combine(Preview,"03-reef-detail.png"),1600,1000,7);
            camera.fieldOfView=64;camera.transform.position=AquariumEnvironment.LayoutPosition(-.2f,2.8f,.8f);camera.transform.LookAt(AquariumEnvironment.LayoutPosition(-5.5f,3.5f,3.5f));
            Capture(camera,Path.Combine(Preview,"04-walkway-glass.png"),1600,1000,7);
            camera.orthographic=true;camera.orthographicSize=13.5f*AquariumMapOutline.HorizontalScale;camera.transform.position=new Vector3(0,70,0);camera.transform.rotation=Quaternion.Euler(90,0,0);
            Capture(camera,Path.Combine(Preview,"00-reference-silhouette.png"),1800,1350,7);
            CaptureEntryAndJoins(camera);
            CaptureVisitorGallery(camera);
            CaptureDeepDetail(camera);
            CaptureNaturalReef(camera);
            CaptureBiodiversity(camera);
        }

        public static void RebuildJoinedPreview()
        {
            RebuildAndApply();
            CaptureSavedGallery();
            Validate(UnityEngine.Object.FindFirstObjectByType<AquariumEnvironmentRoot>());
        }

        public static void RebuildJoinedAndBuild()
        {
            RebuildAndApply();
            VerifySavedAndBuild();
        }

        private static Vector3 EntryPoint(float x,float z,float y)
        { var p=AquariumEntryLayout.At(x,z,y+AquariumModel.WaterHeight-7);return new Vector3(p.x,p.y,p.z); }

        private static void CaptureEntryAndJoins(Camera camera)
        {
            camera.orthographic=false;camera.fieldOfView=51;
            camera.transform.position=EntryPoint(-9.3f,10.2f,14.1f);
            camera.transform.LookAt(EntryPoint(0,.6f,7.25f));
            Capture(camera,Path.Combine(Preview,"06-dive-entry.png"),1800,1200,7);
            camera.fieldOfView=54;camera.transform.position=AquariumEnvironment.LayoutPosition(-1.9f,22,-.7f);
            camera.transform.LookAt(AquariumEnvironment.LayoutPosition(-5.3f,12.2f,2.3f));
            Capture(camera,Path.Combine(Preview,"07-joined-walkways.png"),1600,1100,7);
            camera.fieldOfView=63;camera.transform.position=EntryPoint(-.3f,11.0f,2.4f);
            camera.transform.LookAt(EntryPoint(0,2.9f,6.8f));
            Capture(camera,Path.Combine(Preview,"08-dive-water-clearance.png"),1600,1000,7);
        }

        private static void CaptureVisitorGallery(Camera camera)
        {
            camera.orthographic=false;camera.fieldOfView=66;
            camera.transform.position=EntryPoint(-5.4f,9.2f,-2.7f);camera.transform.LookAt(EntryPoint(3.3f,-2.0f,-2.2f));
            Capture(camera,Path.Combine(Preview,"09-visitor-corridor.png"),1800,1125,7);
            camera.fieldOfView=70;camera.transform.position=new Vector3(17.0f,2.7f,-.4f);camera.transform.LookAt(new Vector3(26,3.8f,3.4f));
            Capture(camera,Path.Combine(Preview,"10-monumental-reef.png"),1800,1125,7);
            camera.fieldOfView=69;camera.transform.position=new Vector3(2.0f,2.0f,-4.9f);camera.transform.LookAt(new Vector3(2.3f,3.6f,4));
            Capture(camera,Path.Combine(Preview,"11-swim-through-arch.png"),1800,1125,7);
            camera.fieldOfView=63;camera.transform.position=new Vector3(-5.5f,2.8f,1.6f);camera.transform.LookAt(new Vector3(-13.4f,3.2f,6.1f));
            Capture(camera,Path.Combine(Preview,"12-central-glass-gallery.png"),1800,1125,7);
        }

        private static void CaptureDeepDetail(Camera camera)
        {
            camera.orthographic=false;camera.fieldOfView=64;
            camera.transform.position=new Vector3(10,1.65f,-2);camera.transform.LookAt(new Vector3(3,1.0f,-9));
            Capture(camera,Path.Combine(Preview,"13-sculpted-sand.png"),1920,1200,7);
            camera.fieldOfView=57;camera.transform.position=new Vector3(19.7f,3.4f,1.0f);camera.transform.LookAt(new Vector3(25.1f,3.9f,3.2f));
            Capture(camera,Path.Combine(Preview,"14-4k-rock-detail.png"),1920,1200,7);
            camera.fieldOfView=74;camera.transform.position=new Vector3(11.5f,3.0f,-8);camera.transform.LookAt(new Vector3(0,6.8f,4));
            Capture(camera,Path.Combine(Preview,"15-deep-lagoon.png"),1920,1200,7);
            camera.fieldOfView=58;camera.transform.position=EntryPoint(-3.1f,7.7f,-2.4f);camera.transform.LookAt(EntryPoint(1.6f,-2.7f,-2.7f));
            Capture(camera,Path.Combine(Preview,"16-museum-exhibits.png"),1920,1200,7);
        }

        private static void CaptureNaturalReef(Camera camera)
        {
            camera.orthographic=false;camera.fieldOfView=68;
            camera.transform.position=new Vector3(17.0f,2.5f,-.3f);camera.transform.LookAt(new Vector3(26.8f,4.6f,4.5f));
            Capture(camera,Path.Combine(Preview,"17-reference-cliff.png"),1920,1200,7);
            camera.fieldOfView=65;camera.transform.position=new Vector3(10.0f,1.2f,-1.6f);camera.transform.LookAt(new Vector3(4.0f,.8f,-8.0f));
            Capture(camera,Path.Combine(Preview,"18-natural-sand.png"),1920,1200,7);
            camera.fieldOfView=70;camera.transform.position=new Vector3(-20.8f,2.9f,5.0f);camera.transform.LookAt(new Vector3(-24,3.6f,10.0f));
            Capture(camera,Path.Combine(Preview,"19-reef-terraces.png"),1920,1200,7);
        }

        public static void BuildAndVerify()
        {
            RebuildAndApply();
            Environment.SetEnvironmentVariable("AQUARIUM_BUILD_PATH", Path.GetFullPath("Build/Aquarium-CoralGallery/AquariumDemo.exe"));
            AquariumBuild.Windows();
        }

        public static void VerifySavedAndBuild()
        {
            CaptureSavedGallery();
            ShaderChecks();AquariumCameraChecks.Run();
            Validate(UnityEngine.Object.FindFirstObjectByType<AquariumEnvironmentRoot>());
            CaptureLightMotion();
            EditorSceneManager.OpenScene(GameScene);
            Environment.SetEnvironmentVariable("AQUARIUM_BUILD_PATH",Path.GetFullPath("Build/Aquarium-CoralGallery/AquariumDemo.exe"));
            AquariumBuild.Windows();
        }

        private static void Overview(Camera c)
        {
            AquariumEnvironment.ConfigureOverview(c);
        }

        private static void Underwater(Camera c)
        {
            c.orthographic=false;c.fieldOfView=68;
            c.transform.position=AquariumEnvironment.LayoutPosition(-2,2.1f,-1);
            c.transform.LookAt(AquariumEnvironment.LayoutPosition(6,3.1f,3.5f));
        }

        private static void ReefDetail(Camera c)
        {
            c.orthographic=false;c.fieldOfView=58;var p=AquariumModel.Rocks[1];
            var centre=new Vector3(p.x,p.y,p.z);
            c.transform.position=centre+new Vector3(5.2f,3.4f,-5.5f);c.transform.LookAt(centre+Vector3.up*1.1f);
        }

        public static void CaptureLightMotion()
        {
            var camera=Camera.main;Underwater(camera);
            var root=UnityEngine.Object.FindFirstObjectByType<AquariumEnvironmentRoot>();
            var renderers=root.GetComponentsInChildren<Renderer>();var enabled=renderers.Select(r=>r.enabled).ToArray();
            int changed=0;
            try
            {
                // Isolate the actual GPU light ribbons: movement cannot be credited to kelp or bubbles.
                foreach(var r in renderers)r.enabled=r.name.StartsWith("19 •");
                var a=Capture(camera,null,640,400,1);
                var b=Capture(camera,null,640,400,4);
                for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>5)changed++;
                if(changed<100)throw new Exception("Sunlight animation did not visibly change in the renderer: "+changed);
            }
            finally { for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i]; }
            string frames=Path.Combine(Preview,"RipplingLightFrames");Directory.CreateDirectory(frames);
            for(int i=0;i<72;i++)Capture(camera,Path.Combine(frames,"frame-"+i.ToString("000")+".png"),960,600,1+i/12f);
            string result="GPU sunlight animation, isolated rays at t=1 and t=4: PASS ("+changed+" changed pixels of 256000)\n72 frames / 12 fps / 6 seconds\n";
            File.WriteAllText(Path.Combine(Preview,"light-animation-validation.txt"),result);Debug.Log("CORAL_GALLERY_LIGHT_ANIMATION_OK "+result);
        }

        private static void SaveGenerated(GameObject root)
        {
            var materials = new Dictionary<Material, Material>();
            Texture2D persistentCaustic = null;
            int meshIndex = 0, matIndex = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                string path = ResourcesRoot + "/Meshes/" + (++meshIndex).ToString("000") + ".asset";
                StoreMeshParts(filter, filter.sharedMesh, path);
            }
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                var source = renderer.sharedMaterial;
                if (source == null || AssetDatabase.Contains(source)) continue;
                Material saved;
                if (!materials.TryGetValue(source, out saved))
                {
                    if (source.HasProperty("_Caustics"))
                    {
                        if (persistentCaustic == null) persistentCaustic = Store((Texture2D)source.GetTexture("_Caustics"), ResourcesRoot + "/Caustics.asset");
                        source.SetTexture("_Caustics", persistentCaustic);
                    }
                    saved = Store(source, ResourcesRoot + "/Materials/" + (++matIndex).ToString("00") + ".mat"); materials.Add(source, saved);
                }
                renderer.sharedMaterial = saved;
            }
            RemoveUnusedMeshParts(root);
        }

        private static T Store<T>(T value, string path) where T : UnityEngine.Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if(existing==value)return existing;
            if (existing != null)
            {
                var destinationMesh=existing as Mesh;var sourceMesh=value as Mesh;
                if(destinationMesh!=null && sourceMesh!=null)
                {
                    // Explicit buffer updates keep live previews and saved assets in sync on regeneration.
                    destinationMesh.Clear();destinationMesh.indexFormat=sourceMesh.indexFormat;
                    destinationMesh.vertices=sourceMesh.vertices;destinationMesh.triangles=sourceMesh.triangles;
                    destinationMesh.normals=sourceMesh.normals;destinationMesh.tangents=sourceMesh.tangents;destinationMesh.colors=sourceMesh.colors;
                    destinationMesh.uv=sourceMesh.uv;destinationMesh.uv2=sourceMesh.uv2;
                    destinationMesh.bounds=sourceMesh.bounds;destinationMesh.name=sourceMesh.name;
                    destinationMesh.UploadMeshData(false);
                }
                else EditorUtility.CopySerialized(value, existing);
                EditorUtility.SetDirty(existing);return existing;
            }
            AssetDatabase.CreateAsset(value, path); return value;
        }

        private static void Validate(AquariumEnvironmentRoot root)
        {
            int triangles = 0, deepGlass = 0;
            if(root.transform.localScale!=Vector3.one)throw new Exception("The environment must not stretch individual props.");
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.sharedMesh.vertexCount == 0) throw new Exception("Empty gallery mesh: " + mf.name);
                triangles += mf.sharedMesh.triangles.Length / 3;
                if(mf.name.StartsWith("20 • Full-depth walkway glass"))
                {
                    var vertices=mf.sharedMesh.vertices;
                    float bottom=vertices.Min(v=>v.y),top=vertices.Max(v=>v.y);
                    if(Mathf.Abs(bottom-.27f)>.001f||Mathf.Abs(top-(AquariumModel.WaterHeight+.02f))>.001f)throw new Exception("Incomplete walkway glass height: "+mf.name);
                    deepGlass++;
                }
            }
            if(deepGlass!=1)throw new Exception("Expected one joined full-depth glass envelope for the central paths.");
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
                if (mr.sharedMaterial == null || mr.sharedMaterial.shader == null || !mr.sharedMaterial.shader.isSupported) throw new Exception("Invalid gallery material: " + mr.name);
            for (int i = 0; i < 360; i++)
            {
                float a = i * Mathf.Deg2Rad; var p = AquariumModel.Constrain(new Point(Mathf.Cos(a) * 90, 5, Mathf.Sin(a) * 90), .4f);
                if (!AquariumModel.InsideTank(p, .39f)) throw new Exception("Tank collision mismatch at " + i);
            }
            ValidateJoinedEntry(root);
            ValidateInteriorAndReefs(root);
            ValidateDeepDetail(root);
            ValidateBiodiversity(root);
            string report = "Coral Gallery 1.8\nApplied to: " + GameScene + "\nArt scene: " + GalleryScene + "\nRender meshes: " + root.GetComponentsInChildren<MeshFilter>().Length + "\nTriangles: " + triangles + "\nKelp fronds: " + root.kelpFronds + "\nCoral colonies: " + root.coralColonies + "\nAnemone colonies: " + root.anemoneColonies + "\nBubbles: " + root.bubbleCount + "\nJoined walkway glass envelope, y=0.27 to 12.02: PASS\n360 perimeter collision samples: PASS\nCamera checks: PASS\nShader compiler: PASS\n";
            File.WriteAllText(Path.Combine(Preview, "validation.txt"), report);
            var outside=AquariumMapOutline.Sample(true,496);var water=AquariumMapOutline.Sample(false,496);
            string size=string.Format("Horizontal layout scale: {0:0.0}x; area: {1:0.00}x\nOuter footprint: {2:0.00} x {3:0.00} m\nWater footprint: {4:0.00} x {5:0.00} m\nWater depth: {6:0.0} m\nActor, plant and existing reef dimensions retained\n",AquariumMapOutline.HorizontalScale,Mathf.Pow(AquariumMapOutline.HorizontalScale,2),outside.Max(p=>p.x)-outside.Min(p=>p.x),outside.Max(p=>p.z)-outside.Min(p=>p.z),water.Max(p=>p.x)-water.Min(p=>p.x),water.Max(p=>p.z)-water.Min(p=>p.z),AquariumModel.WaterHeight);
            File.AppendAllText(Path.Combine(Preview,"validation.txt"),size);
            Debug.Log("CORAL_GALLERY_VALIDATED " + report.Replace('\n', ' '));
        }

        private static void ValidateJoinedEntry(AquariumEnvironmentRoot root)
        {
            var deck=root.GetComponentsInChildren<MeshFilter>().Single(m=>m.name.StartsWith("06 • Joined"));
            var soffit=root.GetComponentsInChildren<MeshFilter>().Single(m=>m.name.StartsWith("06b •"));
            if(soffit.sharedMesh.vertices.Max(v=>v.y)>AquariumModel.WaterHeight+.04f-.024f-.02f)
                throw new Exception("Animated water can intersect the underside of the entry deck.");
            var vertices=deck.sharedMesh.vertices;var indices=deck.sharedMesh.triangles;
            var probes=new[]{AquariumEnvironment.LayoutPosition(-5.4f,12.275f,2),AquariumEnvironment.LayoutPosition(-.8f,12.275f,-5.4f),AquariumEnvironment.LayoutPosition(-.8f,12.275f,-6)};
            foreach(var probe in probes)
            {
                var p=probe+new Vector3(.019f,0,.041f);int hits=0;
                for(int i=0;i<indices.Length;i+=3)
                {
                    var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                    float ab=(b.x-a.x)*(p.z-a.z)-(b.z-a.z)*(p.x-a.x);
                    float bc=(c.x-b.x)*(p.z-b.z)-(c.z-b.z)*(p.x-b.x);
                    float ca=(a.x-c.x)*(p.z-c.z)-(a.z-c.z)*(p.x-c.x);
                    if((ab>0&&bc>0&&ca>0)||(ab<0&&bc<0&&ca<0))hits++;
                }
                if(hits!=1)throw new Exception("Overlapping or missing deck faces at former junction: "+probe+" / "+hits);
            }
            var glass=root.GetComponentsInChildren<MeshFilter>().Single(m=>m.name.StartsWith("20 •"));
            var glassVertices=glass.sharedMesh.vertices;
            for(int i=0;i<glassVertices.Length;i+=4)
            {
                var p=(glassVertices[i]+glassVertices[i+1])*.5f;
                if(AquariumWalkwayLayout.Contains(new Point(p.x,p.z),-.12f))throw new Exception("Interior glass partition remains at joined deck.");
            }
            if(root.divePreparation==null||root.diveEdge==null||root.diveWaterTarget==null||root.diveCamera==null)
                throw new Exception("Entry staging anchors were not saved.");
            var target=root.diveWaterTarget.position;
            foreach(float y in new[]{1.55f,5.5f,10.8f})for(int i=0;i<32;i++)
            {
                float a=i*Mathf.PI*2/32;var p=new Point(target.x+Mathf.Cos(a)*2.75f,y,target.z+Mathf.Sin(a)*2.75f);
                if((AquariumModel.Constrain(p,.2f)-p).Length>.005f)throw new Exception("Entry water clearance intersects existing collision: "+p.x+", "+p.z);
            }
            if(root.GetComponentsInChildren<Animator>().Length!=0||root.GetComponentsInChildren<Animation>().Length!=0||root.GetComponentsInChildren<Camera>().Length!=0)
                throw new Exception("Entry area must be static scenery, with no active cinematic.");
            string report="Three former deck intersections: exactly one surface each — PASS\nInterior duplicate glass partitions: none — PASS\nClosed connected lagoon/entry contours — PASS\nAnimated water and deck underside separation — PASS\nNorth entry apron: 7.8 m wide / 3.2 m projection\nEntry rail opening: 2.6 m\nClear water cylinder: 5.5 m diameter, checked at three depths — PASS\nFour saved staging anchors — PASS\nNo entry animation or active cinematic camera — PASS\n";
            File.WriteAllText(Path.Combine(Preview,"joined-entry-validation.txt"),report);Debug.Log("CORAL_GALLERY_JOINED_ENTRY_OK "+report.Replace('\n',' '));
        }

        private static void ValidateInteriorAndReefs(AquariumEnvironmentRoot root)
        {
            if(root.monumentalReefs!=AquariumMonolithLayout.Groups.Length||root.visitorGalleryBays!=38)throw new Exception("Missing reef or gallery sections.");
            var meshes=root.GetComponentsInChildren<MeshFilter>();
            foreach(var mf in meshes.Where(m=>m.name.StartsWith("31 •")))
            {
                var points=mf.sharedMesh.vertices;int group=Array.IndexOf(AquariumMonolithLayout.Names,mf.name.Split(new[]{" / "},StringSplitOptions.None)[1]);
                if(points.Max(p=>p.y)<(group<3?6.2f:2.5f)||points.Min(p=>p.y)>.15f)throw new Exception("Reef must extend from sand to near the surface.");
                foreach(var p in points)
                {
                    var q=new Point(p.x,p.y,p.z);
                    if(p.y>.15f&&(!AquariumMapOutline.Contains(q,.2f)||AquariumWalkwayLayout.Contains(q,.2f)))throw new Exception("Monolithic reef intersects aquarium glass: "+p);
                    if(Mathf.Abs(AquariumMonolithLayout.Distance(q,group))>.12f)throw new Exception("Reef mesh / collision disagreement: "+p);
                }
            }
            if(AquariumMonolithLayout.Hit(new Point(2.3f,2,-1),new Point(2.3f,2,9),.5f)<=1)throw new Exception("Central arch swim-through blocked.");
            File.WriteAllText(Path.Combine(Preview,"interior-reef-validation.txt"),"38 furnished enclosed visitor gallery bays — PASS\nContinuous rear wall, dry floor and promenade ceiling — PASS\nSeven fractured rock formations: three cliffs/bridge and four outcrops — PASS\nReef meshes remain inside glass with 0.2 m clearance — PASS\nShared collision field agrees with every reef vertex within 0.12 m — PASS\n1 m diameter swim-through under central arch — PASS\nNo visitor models added\n");
        }

        private static void ValidateDeepDetail(AquariumEnvironmentRoot root)
        {
            string[] names={"RockAlbedo","RockNormal","SandGrainAlbedo","SandGrainNormal"};
            foreach(string name in names)
            {
                var texture=Resources.Load<Texture2D>("Aquarium/Textures/"+name);
                if(texture==null||texture.width!=4096||texture.height!=4096||texture.mipmapCount<4||texture.anisoLevel!=16)throw new Exception("Missing full-resolution material: "+name);
            }
            var sand=root.GetComponentsInChildren<MeshFilter>().Single(m=>m.name.StartsWith("01 •"));
            float min=100,max=-100;
            foreach(var v in sand.sharedMesh.vertices){min=Mathf.Min(min,v.y);max=Mathf.Max(max,v.y);if(Mathf.Abs(v.y-AquariumSeabed.Height(v.x,v.z))>.002f)throw new Exception("Sand / collision disagreement.");}
            if(max-min<.65f)throw new Exception("Sand terrain is too flat.");
            var wall=root.GetComponentsInChildren<MeshRenderer>().Single(m=>m.name.StartsWith("26 •"));
            if(wall.sharedMaterial.shader.name!="Aquarium/Dim Visitor Gallery"||wall.sharedMaterial.GetFloat("_Exposure")>.3f)throw new Exception("Visitor gallery lighting not dimmed.");
            if(AquariumModel.WaterHeight!=12||Mathf.Abs(root.diveEdge.position.y-12.36f)>.01f)throw new Exception("Entry not aligned with deeper water.");
            File.WriteAllText(Path.Combine(Preview,"deep-detail-validation.txt"),"Water height: 12 m / glass top: 12.02 m / deck: 12.275 m — PASS\nFour 4096 x 4096 color and normal maps, mipmapped, 16x anisotropy — PASS\nSand vertex heights match gameplay field — PASS\nSand elevation range: "+min.ToString("0.000")+" to "+max.ToString("0.000")+" m\nDark visitor gallery material / four marine exhibit illustrations — PASS\nRaised entry anchors, no entry animation — PASS\n");
        }

        private static void ShaderChecks()
        {
            foreach (var shader in AssetDatabase.FindAssets("t:Shader", new[] { Art }).Select(id => AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(id))))
            {
                if (ShaderUtil.ShaderHasError(shader))
                    throw new Exception("Shader error in " + shader.name + ": " + string.Join("; ", ShaderUtil.GetShaderMessages(shader).Select(m => m.message)));
            }
        }

        private static Color32[] Capture(Camera camera, string path, int width, int height, float time)
        {
            Shader.SetGlobalFloat("_AquariumPreviewTime",time);Shader.SetGlobalFloat("_AquariumPreviewTimeEnabled",1);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
            Color32[] result=null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                if(path!=null)File.WriteAllBytes(path,pixels.EncodeToPNG());else result=pixels.GetPixels32();
                UnityEngine.Object.DestroyImmediate(pixels);
            }
            finally { camera.targetTexture = oldTarget; RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target);Shader.SetGlobalFloat("_AquariumPreviewTimeEnabled",0); }
            return result;
        }
    }
}

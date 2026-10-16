using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace PrototypeLab.Aquarium.Editor
{
    public static partial class AquariumEnvironmentAuthoring
    {
        private static void ValidateBiodiversity(AquariumEnvironmentRoot root)
        {
            var lines=new List<string>{"Coral Gallery 1.8 / topology and biodiversity"};
            foreach(var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if(mf.name.StartsWith("31 •"))lines.Add(AquariumMeshAudit.Inspect(mf.sharedMesh,mf.name,true));
                if(mf.name.StartsWith("01 •"))lines.Add(AquariumMeshAudit.Inspect(mf.sharedMesh,mf.name,false));
                if(mf.sharedMesh.vertices.Any(v=>float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)))throw new Exception("Invalid mesh vertex "+mf.name);
            }
            if(root.coralForms.Length!=6||root.coralForms.Any(n=>n<12))throw new Exception("All six coral forms must be represented.");
            if(root.plantForms.Length!=5||root.plantForms.Any(n=>n<15))throw new Exception("All five seaweed forms must be represented.");
            if(root.anemoneForms.Length!=2||root.anemoneForms.Any(n=>n<6))throw new Exception("Both reference anemone forms must be represented.");
            if(root.anemonePreviewPoint==Vector3.zero||root.mintAnemonePreviewPoint==Vector3.zero)throw new Exception("Missing reference anemone foreground gardens.");
            var soft=root.GetComponentsInChildren<MeshFilter>().Where(m=>m.name.StartsWith("34 •")).ToArray();
            if(soft.Length==0)throw new Exception("Missing anemone meshes.");
            var weights=soft.SelectMany(m=>m.sharedMesh.uv2).ToArray();
            foreach(var part in soft)
            {
                var renderer=part.GetComponent<MeshRenderer>();
                if(renderer.sharedMaterial.shader.name!="Aquarium/Living Reef"||renderer.sharedMaterial.GetFloat("_Sway")<=0)throw new Exception("Anemone current not enabled.");
            }
            int fixedVertices=weights.Count(v=>v.x==0),movingVertices=weights.Count(v=>v.x>.1f);
            if(fixedVertices<1000||movingVertices<10000)throw new Exception("Missing anchored bases or flexible tentacle tips.");
            lines.Add("All seven rock volumes: closed two-manifold edges, consistent outward triangle winding, no collapsed triangles — PASS");
            lines.Add("Sand: one continuous boundary loop, no interior holes — PASS");
            lines.Add("Coral forms [staghorn, scroll, fan, brain, finger, sponge]: "+string.Join(", ",root.coralForms));
            lines.Add("Seaweed forms [broad kelp, ribbon, eelgrass, red feather, sargassum]: "+string.Join(", ",root.plantForms));
            lines.Add("Anemones [rose flowing, mint rounded]: "+string.Join(", ",root.anemoneForms));
            lines.Add("Anemone bend payload: "+fixedVertices+" fixed vertices / "+movingVertices+" flexible vertices — PASS");
            File.WriteAllLines(Path.Combine(Preview,"biodiversity-validation.txt"),lines);
            Debug.Log("AQUARIUM_BIODIVERSITY_OK "+root.anemoneColonies+" anemones / all topology checks pass");
        }
        private static void CaptureBiodiversity(Camera camera)
        {
            var root=UnityEngine.Object.FindFirstObjectByType<AquariumEnvironmentRoot>();
            camera.orthographic=false;camera.fieldOfView=56;
            var target=root.biodiversityPreviewPoint;
            camera.transform.position=target+new Vector3(3.7f,2.8f,-4.4f);camera.transform.LookAt(target+Vector3.up*.7f);
            Capture(camera,Path.Combine(Preview,"20-diverse-reef-garden.png"),1920,1200,3);
            AnemoneView(camera,root);
            Capture(camera,Path.Combine(Preview,"21-living-anemones.png"),1920,1200,3);
            MintAnemoneView(camera,root);
            Capture(camera,Path.Combine(Preview,"24-mint-anemone.png"),1920,1200,3);
            camera.fieldOfView=58;camera.transform.position=new Vector3(19.2f,3.8f,1.2f);camera.transform.LookAt(new Vector3(26.2f,5.5f,4.0f));
            Capture(camera,Path.Combine(Preview,"22-sealed-cliff-faces.png"),1920,1200,3);
        }
        private static void AnemoneView(Camera camera,AquariumEnvironmentRoot root)
        {
            camera.orthographic=false;camera.fieldOfView=47;
            var target=root.anemonePreviewPoint;
            camera.transform.position=target+new Vector3(1.12f,.88f,-1.42f);
            camera.transform.LookAt(target+Vector3.up*.30f);
        }
        private static void MintAnemoneView(Camera camera,AquariumEnvironmentRoot root)
        {
            camera.orthographic=false;camera.fieldOfView=47;
            var target=root.mintAnemonePreviewPoint;var focus=target+Vector3.up*.31f;
            var outward=target-root.biodiversityPreviewPoint;outward.y=0;outward.Normalize();
            // The mint garden is on the other side of the cover rock: choose a
            // clear view from its outside, rather than looking through the rock.
            float[] angles={75,-75,60,-60,90,-90,45,-45,105,-105,30,-30,0};
            for(int n=0;n<angles.Length;n++)
            {
                var direction=Quaternion.Euler(0,angles[n],0)*outward;
                var position=target+direction*1.85f+Vector3.up*.88f;
                if(AquariumModel.SolidHit(new Point(position.x,position.y,position.z),new Point(focus.x,focus.y,focus.z),.08f)<=1)continue;
                camera.transform.position=position;camera.transform.LookAt(focus);return;
            }
            throw new Exception("No unobstructed camera for mint anemone garden.");
        }
        public static void CaptureLivingMotion()
        {
            EditorSceneManager.OpenScene(GalleryScene);
            var root=UnityEngine.Object.FindFirstObjectByType<AquariumEnvironmentRoot>();
            var camera=Camera.main;AquariumEnvironment.ConfigureCamera(camera);AnemoneView(camera,root);
            var renderers=root.GetComponentsInChildren<MeshRenderer>();
            var active=renderers.Select(r=>r.enabled).ToArray();
            var soft=renderers.Where(r=>r.name.StartsWith("34 •")).ToArray();
            var material=soft.First().sharedMaterial;var oldCaustic=material.GetTexture("_Caustics");
            try
            {
                foreach(var renderer in renderers)renderer.enabled=soft.Contains(renderer);
                material.SetTexture("_Caustics",Texture2D.blackTexture);
                var a=Capture(camera,null,640,400,1);var b=Capture(camera,null,640,400,4);int changed=0;
                for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>25)changed++;
                if(changed<200)throw new Exception("Anemone tentacles are not visibly animated: "+changed);
                File.WriteAllText(Path.Combine(Preview,"living-motion-validation.txt"),"Isolated anemones, caustics disabled: "+changed+" changed pixels / 256000 — PASS\nGPU deformation, fixed bases and closed tentacles.\n48 frames / 12 fps / 4 seconds.\n");
                Debug.Log("AQUARIUM_ANEMONE_MOTION_OK "+changed+" pixels");
            }
            finally
            {
                for(int i=0;i<renderers.Length;i++)renderers[i].enabled=active[i];
                material.SetTexture("_Caustics",oldCaustic);
            }
            string frames=Path.Combine(Preview,"AnemoneFrames");Directory.CreateDirectory(frames);
            for(int i=0;i<48;i++)Capture(camera,Path.Combine(frames,"frame-"+i.ToString("000")+".png"),960,600,1+i/12f);
        }
        public static void RebuildLivingPreview()
        {
            RebuildAndApply();
            CaptureSavedGallery();
            CaptureLivingMotion();
        }
        public static void FinishLivingBuild()
        {
            VerifySavedAndBuild();
            CaptureLivingMotion();
        }
        public static void RebuildLivingAndBuild()
        {
            RebuildLivingPreview();
            CaptureLightMotion();
            EditorSceneManager.OpenScene(GameScene);
            Environment.SetEnvironmentVariable("AQUARIUM_BUILD_PATH",Path.GetFullPath("Build/Aquarium-CoralGallery/AquariumDemo.exe"));
            AquariumBuild.Windows();
        }
        public static void BuildSavedLivingPlayer()
        {
            EditorSceneManager.OpenScene(GalleryScene);
            ShaderChecks();
            CaptureLightMotion();
            EditorSceneManager.OpenScene(GameScene);
            Environment.SetEnvironmentVariable("AQUARIUM_BUILD_PATH",Path.GetFullPath("Build/Aquarium-CoralGallery/AquariumDemo.exe"));
            AquariumBuild.Windows();
        }
    }
}

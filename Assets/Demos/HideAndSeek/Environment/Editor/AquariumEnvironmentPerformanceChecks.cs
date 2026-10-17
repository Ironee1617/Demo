using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumEnvironmentPerformanceChecks
    {
        [Serializable]
        private sealed class RenderMeasurement
        {
            public string variant;
            public int view,warmupRenders=3,measuredRenders=12;
            public float frozenTime;
            public double medianSyncWallMs,p95SyncWallMs;
            public double[] syncWallMs;
            public int[] editorDrawCalls,editorTriangles;
            public bool editorStatsAvailable;
        }
        [Serializable]
        private sealed class RenderMeasurements
        {
            public string timing="Camera.Render + 1px ReadPixels GPU synchronization; CPU/GPU/readback wall time, not isolated GPU time";
            public string counters="UnityEditor.UnityStats are best-effort editor-wide counters; all-zero values mean unavailable";
            public RenderMeasurement[] measurements;
        }
        [MenuItem("Aquarium/Coral Gallery/Check Rendering Optimization")]
        public static void Run()
        {
            string output=Environment.GetEnvironmentVariable("AQUARIUM_ENVIRONMENT_QA");
            if(string.IsNullOrEmpty(output))output="Artifacts/EnvironmentPerformanceQA";
            Directory.CreateDirectory(output);
            var scene=EditorSceneManager.NewPreviewScene();
            var report=new StringBuilder();var measurements=new List<RenderMeasurement>();
            float oldClock=Shader.GetGlobalFloat("_AquariumPreviewTime"),oldClockEnabled=Shader.GetGlobalFloat("_AquariumPreviewTimeEnabled");
            var prefab=Resources.Load<GameObject>(AquariumEnvironment.ResourcePath);
            var environment=UnityEngine.Object.Instantiate(prefab).GetComponent<AquariumEnvironmentRoot>();
            SceneManager.MoveGameObjectToScene(environment.gameObject,scene);
            var cameraObject=new GameObject("Environment rendering QA camera");cameraObject.transform.SetParent(environment.transform,false);
            var camera=cameraObject.AddComponent<Camera>();camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=AquariumEnvironment.C(0x11515f);camera.nearClipPlane=.06f;camera.farClipPlane=200;camera.fieldOfView=56;
            var lightObject=new GameObject("Environment rendering QA light");lightObject.transform.SetParent(environment.transform,false);
            var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.shadows=LightShadows.Soft;
            light.transform.rotation=Quaternion.Euler(57,-34,0);
            var texture=new RenderTexture(640,400,24);var pixels=new Texture2D(640,400,TextureFormat.RGB24,false);
            var syncPixel=new Texture2D(1,1,TextureFormat.RGB24,false);camera.targetTexture=texture;
            try
            {
                var renderers=environment.GetComponentsInChildren<MeshRenderer>(true);
                var originalMaterials=new Material[renderers.Length];var originalBounds=new Bounds[renderers.Length];
                var meshes=new Dictionary<Mesh,Bounds>();
                for(int i=0;i<renderers.Length;i++)
                {
                    originalMaterials[i]=renderers[i].sharedMaterial;originalBounds[i]=renderers[i].localBounds;
                    var filter=renderers[i].GetComponent<MeshFilter>();
                    if(filter!=null && filter.sharedMesh!=null)meshes[filter.sharedMesh]=filter.sharedMesh.bounds;
                }
                int labelsBefore=LabelMaterials(renderers);report.AppendLine("label_materials_before="+labelsBefore);
                var before=new Color32[6][];var beforeTriangles=new long[6];
                for(int sample=0;sample<6;sample++)
                {
                    SetView(camera,environment,sample/2);Shader.SetGlobalFloat("_AquariumPreviewTime",sample%2==0?.7f:3.1f);Shader.SetGlobalFloat("_AquariumPreviewTimeEnabled",1);
                    before[sample]=Capture(camera,texture,pixels,Path.Combine(output,"view-"+sample+"-before.png"));
                    beforeTriangles[sample]=FrustumTriangles(camera,renderers);
                    measurements.Add(MeasureRendering(camera,texture,syncPixel,"before",sample,report));
                }
                // Reflection keeps this diagnostic independent of the optimizer's report structure.
                var optimize=typeof(AquariumEnvironment).GetMethod("OptimizeRendering",BindingFlags.Public|BindingFlags.Static);
                Require(optimize!=null,"Missing environment rendering optimizer.");
                var watch=System.Diagnostics.Stopwatch.StartNew();var stats=optimize.Invoke(null,new object[]{environment});watch.Stop();
                report.AppendLine("initialization_ms="+watch.Elapsed.TotalMilliseconds.ToString("F2"));
                report.AppendLine("optimization="+JsonUtility.ToJson(stats));
                report.AppendLine("label_materials_after="+LabelMaterials(renderers));
                Require(LabelMaterials(renderers)<labelsBefore,"Identical font atlas materials were not shared.");
                int tightened=0;
                for(int i=0;i<renderers.Length;i++)
                {
                    Require(SameLabelAppearance(originalMaterials[i],renderers[i].sharedMaterial),"Material appearance changed: "+renderers[i].name);
                    if(!renderers[i].localBounds.Equals(originalBounds[i])){tightened++;ValidateBounds(renderers[i]);}
                }
                Require(tightened==12,"Every saved environment partition must receive tight bounds; found "+tightened+" of 12.");report.AppendLine("tightened_bounds="+tightened);
                foreach(var pair in meshes)Require(pair.Key.bounds.Equals(pair.Value),"Optimization modified a shared mesh asset.");
                for(int sample=0;sample<6;sample++)
                {
                    SetView(camera,environment,sample/2);Shader.SetGlobalFloat("_AquariumPreviewTime",sample%2==0?.7f:3.1f);
                    var after=Capture(camera,texture,pixels,Path.Combine(output,"view-"+sample+"-after.png"));
                    measurements.Add(MeasureRendering(camera,texture,syncPixel,"after",sample,report));
                    long total=0;int changed=0;
                    for(int p=0;p<after.Length;p++)
                    {
                        int r=Math.Abs(before[sample][p].r-after[p].r),g=Math.Abs(before[sample][p].g-after[p].g),b=Math.Abs(before[sample][p].b-after[p].b);
                        total+=r+g+b;if(Math.Max(r,Math.Max(g,b))>3)changed++;
                    }
                    float mean=total/(after.Length*3f),fraction=changed/(float)after.Length;
                    report.AppendLine("view="+sample+" mean_channel_difference="+mean.ToString("F4")+" changed_pixels="+changed+" frustum_candidate_triangles="+beforeTriangles[sample]+" -> "+FrustumTriangles(camera,renderers));
                    Require(mean<.1f && fraction<.002f,"Rendering changed after culling/material optimization: view "+sample);
                }
                // A second call must preserve all overrides and canonical material references.
                var appliedBounds=new Bounds[renderers.Length];var appliedMaterials=new Material[renderers.Length];
                for(int i=0;i<renderers.Length;i++){appliedBounds[i]=renderers[i].localBounds;appliedMaterials[i]=renderers[i].sharedMaterial;}
                optimize.Invoke(null,new object[]{environment});
                for(int i=0;i<renderers.Length;i++)Require(appliedBounds[i].Equals(renderers[i].localBounds)&&appliedMaterials[i]==renderers[i].sharedMaterial,"Optimizer is not idempotent.");
                report.AppendLine("Frustum counts estimate camera candidates; Player GPU/draw counters measure actual submitted work.");
                report.AppendLine("Render timings include Camera.Render CPU work, GPU completion and a 1px readback; they are not isolated GPU milliseconds.");
                report.AppendLine("UnityStats counters are best-effort editor-wide snapshots; all-zero counters are unavailable, not zero-cost rendering.");
                report.AppendLine("OK");Debug.Log("AQUARIUM_ENVIRONMENT_PERFORMANCE_OK "+report);
            }
            catch(Exception ex){report.AppendLine("FAIL "+ex);throw;}
            finally
            {
                File.WriteAllText(Path.Combine(output,"environment-performance-checks.txt"),report.ToString());
                File.WriteAllText(Path.Combine(output,"environment-render-benchmark.json"),JsonUtility.ToJson(new RenderMeasurements{measurements=measurements.ToArray()},true));
                Shader.SetGlobalFloat("_AquariumPreviewTime",oldClock);Shader.SetGlobalFloat("_AquariumPreviewTimeEnabled",oldClockEnabled);
                camera.targetTexture=null;texture.Release();UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(syncPixel);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        private static int LabelMaterials(MeshRenderer[] renderers)
        {
            var labels=new HashSet<Material>();
            foreach(var renderer in renderers)if(renderer.sharedMaterial!=null && renderer.sharedMaterial.shader.name=="Aquarium/World Label")labels.Add(renderer.sharedMaterial);
            return labels.Count;
        }
        private static bool SameLabelAppearance(Material before,Material after)
        {
            if(before==after)return true;
            return before!=null&&after!=null&&before.shader==after.shader&&before.renderQueue==after.renderQueue&&
                before.GetTexture("_MainTex")==after.GetTexture("_MainTex")&&before.GetTextureScale("_MainTex").Equals(after.GetTextureScale("_MainTex"))&&
                before.GetTextureOffset("_MainTex").Equals(after.GetTextureOffset("_MainTex"))&&before.GetColor("_Color").Equals(after.GetColor("_Color"));
        }
        private static void ValidateBounds(MeshRenderer renderer)
        {
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var vertices=new List<Vector3>();var weights=new List<Vector2>();mesh.GetVertices(vertices);mesh.GetUVs(1,weights);
            string shader=renderer.sharedMaterial.shader.name;float sway=Mathf.Abs(renderer.sharedMaterial.GetFloat("_Sway"));
            Require(shader=="Aquarium/Living Reef"||shader=="Aquarium/Reef Lit","Unexpected animated partition shader.");
            var bound=renderer.localBounds;
            for(int i=0;i<vertices.Count;i++)
            {
                float bend=sway*(i<weights.Count?Mathf.Abs(weights[i].x):0);
                var movement=new Vector3((shader=="Aquarium/Living Reef"?.235f:.29f)*bend,0,(shader=="Aquarium/Living Reef"?.13f:.14f)*bend);
                if(!bound.Contains(vertices[i]-movement)||!bound.Contains(vertices[i]+movement))
                    throw new Exception("Shader displacement can leave culling bounds: "+renderer.name+" vertex "+i);
            }
        }
        private static void SetView(Camera camera,AquariumEnvironmentRoot root,int view)
        {
            if(view==0){AquariumEnvironment.ConfigureOverview(camera);return;}
            var target=view==1?root.anemonePreviewPoint:root.biodiversityPreviewPoint;
            camera.fieldOfView=view==1?47:56;camera.transform.position=target+new Vector3(3.7f,2.8f,-4.4f);camera.transform.LookAt(target+Vector3.up*.7f);
        }
        private static long FrustumTriangles(Camera camera,MeshRenderer[] renderers)
        {
            var planes=GeometryUtility.CalculateFrustumPlanes(camera);long triangles=0;
            foreach(var renderer in renderers)
            {
                if(!renderer.enabled||!renderer.gameObject.activeInHierarchy||!GeometryUtility.TestPlanesAABB(planes,renderer.bounds))continue;
                var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
                for(int submesh=0;submesh<filter.sharedMesh.subMeshCount;submesh++)triangles+=(long)filter.sharedMesh.GetIndexCount(submesh)/3;
            }
            return triangles;
        }
        private static Color32[] Capture(Camera camera,RenderTexture target,Texture2D pixels,string path)
        {
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();RenderTexture.active=previous;
            File.WriteAllBytes(path,pixels.EncodeToPNG());return pixels.GetPixels32();
        }
        private static RenderMeasurement MeasureRendering(Camera camera,RenderTexture target,Texture2D syncPixel,string variant,int sample,StringBuilder report)
        {
            var result=new RenderMeasurement{variant=variant,view=sample/2,frozenTime=sample%2==0?.7f:3.1f};
            for(int i=0;i<result.warmupRenders;i++)RenderAndSynchronize(camera,target,syncPixel);
            result.syncWallMs=new double[result.measuredRenders];result.editorDrawCalls=new int[result.measuredRenders];result.editorTriangles=new int[result.measuredRenders];
            var watch=new System.Diagnostics.Stopwatch();
            for(int i=0;i<result.measuredRenders;i++)
            {
                watch.Restart();RenderAndSynchronize(camera,target,syncPixel);watch.Stop();result.syncWallMs[i]=watch.Elapsed.TotalMilliseconds;
                result.editorDrawCalls[i]=UnityStats.drawCalls;result.editorTriangles[i]=UnityStats.triangles;
                if(result.editorDrawCalls[i]>0||result.editorTriangles[i]>0)result.editorStatsAvailable=true;
            }
            var sorted=(double[])result.syncWallMs.Clone();Array.Sort(sorted);
            result.medianSyncWallMs=(sorted[sorted.Length/2-1]+sorted[sorted.Length/2])*.5;
            result.p95SyncWallMs=sorted[(int)Math.Ceiling(sorted.Length*.95)-1];
            report.AppendLine("render_variant="+variant+" view="+result.view+" frozen_time="+result.frozenTime.ToString("F1")+
                " warmup="+result.warmupRenders+" samples="+result.measuredRenders+" render_sync_wall_median_ms="+result.medianSyncWallMs.ToString("F3")+
                " render_sync_wall_p95_ms="+result.p95SyncWallMs.ToString("F3")+" editor_stats_available="+result.editorStatsAvailable+
                " editor_draw_calls="+result.editorDrawCalls[result.measuredRenders-1]+" editor_triangles="+result.editorTriangles[result.measuredRenders-1]);
            return result;
        }
        private static void RenderAndSynchronize(Camera camera,RenderTexture target,Texture2D syncPixel)
        {
            var previous=RenderTexture.active;
            try
            {
                camera.Render();RenderTexture.active=target;
                // ReadPixels waits for the render target on the GPU. Applying would upload
                // this pixel again and add unrelated CPU-to-GPU work to the measurement.
                syncPixel.ReadPixels(new Rect(0,0,1,1),0,0,false);
            }
            finally{RenderTexture.active=previous;}
        }
    }
}

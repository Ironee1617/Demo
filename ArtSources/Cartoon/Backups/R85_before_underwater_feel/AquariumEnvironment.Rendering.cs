using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    public static partial class AquariumEnvironment
    {
        [Serializable]
        public struct RenderingOptimizationStats
        {
            public int tightenedBounds,sharedLabelRenderers,meshReads;
        }
        private struct PartBounds { public Bounds bounds;public float maximumWeight; }
        private static readonly Dictionary<Mesh,PartBounds> partBounds=new Dictionary<Mesh,PartBounds>();
        private static readonly List<Vector3> partVertices=new List<Vector3>();
        private static readonly List<Vector2> partWeights=new List<Vector2>();

        public static RenderingOptimizationStats OptimizeRendering(AquariumEnvironmentRoot root)
        {
            if(root.renderingOptimized)return root.renderingStats;
            var stats=new RenderingOptimizationStats();var labels=new List<Material>();
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var material=renderer.sharedMaterial;if(material==null||material.shader==null)continue;
                if(material.shader.name=="Aquarium/World Label")
                {
                    Material shared=null;
                    foreach(var candidate in labels)if(SameLabelMaterial(material,candidate)){shared=candidate;break;}
                    // Reuse an existing asset reference; no new runtime materials need ownership or destruction.
                    if(shared==null)labels.Add(material);
                    else if(shared!=material){renderer.sharedMaterial=shared;stats.sharedLabelRenderers++;}
                    continue;
                }
                string shader=material.shader.name;
                if(shader!="Aquarium/Living Reef"&&shader!="Aquarium/Reef Lit")continue;
                var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
                var mesh=filter.sharedMesh;
                // Only the generated size-limit partitions inherit oversized bounds.
                // Preserve bespoke bubble, water, ray and other renderer bounds.
                if(!mesh.isReadable||renderer.sharedMaterials.Length!=1||
                    (mesh.name.IndexOf(" / part ",StringComparison.Ordinal)<0&&renderer.name.IndexOf(" / part ",StringComparison.Ordinal)<0))continue;
                if(!partBounds.TryGetValue(mesh,out var part))
                {
                    mesh.GetVertices(partVertices);if(partVertices.Count==0)continue;
                    var min=partVertices[0];var max=min;
                    foreach(var vertex in partVertices){min=Vector3.Min(min,vertex);max=Vector3.Max(max,vertex);}
                    part.bounds=new Bounds();part.bounds.SetMinMax(min,max);
                    mesh.GetUVs(1,partWeights);
                    foreach(var weight in partWeights)part.maximumWeight=Mathf.Max(part.maximumWeight,Mathf.Abs(weight.x));
                    partBounds.Add(mesh,part);stats.meshReads++;
                }
                float bend=Mathf.Abs(material.GetFloat("_Sway"))*part.maximumWeight;
                // Exact sine/cosine amplitude bounds from LivingReef.vert and ReefLit.vert.
                var movement=shader=="Aquarium/Living Reef"?new Vector3(.235f*bend,0,.13f*bend):new Vector3(.29f*bend,0,.14f*bend);
                var bounds=part.bounds;bounds.extents+=movement+Vector3.one*.003f;
                if(bounds.Equals(renderer.localBounds))continue;
                renderer.localBounds=bounds;stats.tightenedBounds++;
            }
            root.renderingOptimized=true;root.renderingStats=stats;return stats;
        }
        private static bool SameLabelMaterial(Material a,Material b)
        {
            if(a==b)return true;
            // World Label declares only atlas and tint. Compare the render state as well;
            // custom keywords/pass overrides must not be silently merged.
            if(a.shader!=b.shader||a.shaderKeywords.Length!=0||b.shaderKeywords.Length!=0||a.renderQueue!=b.renderQueue||
                a.enableInstancing!=b.enableInstancing||a.doubleSidedGI!=b.doubleSidedGI||a.globalIlluminationFlags!=b.globalIlluminationFlags||
                a.GetTag("RenderType",false)!=b.GetTag("RenderType",false)||a.passCount!=b.passCount)return false;
            for(int pass=0;pass<a.passCount;pass++)
                if(a.GetShaderPassEnabled(a.GetPassName(pass))!=b.GetShaderPassEnabled(b.GetPassName(pass)))return false;
            return a.GetTexture("_MainTex")==b.GetTexture("_MainTex")&&a.GetTextureScale("_MainTex").Equals(b.GetTextureScale("_MainTex"))&&
                a.GetTextureOffset("_MainTex").Equals(b.GetTextureOffset("_MainTex"))&&a.GetColor("_Color").Equals(b.GetColor("_Color"));
        }
    }
}

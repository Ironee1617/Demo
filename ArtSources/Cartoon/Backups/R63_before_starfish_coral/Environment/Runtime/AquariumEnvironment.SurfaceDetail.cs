using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    public static partial class AquariumEnvironment
    {
        private static float Elevated(float originalHeight){return originalHeight+AquariumModel.WaterHeight-7;}
        private static Material ScannedSurface(string name,string prefix,Texture caustics,float scale,bool sand)
        {
            var material=new Material(Shader.Find(sand?"Aquarium/Natural Granular Sand":"Aquarium/Scanned Geology")){name=name};
            material.SetTexture("_MainTex",Resources.Load<Texture2D>("Aquarium/Textures/"+prefix+"Albedo"));
            material.SetTexture("_NormalTex",Resources.Load<Texture2D>("Aquarium/Textures/"+prefix+"Normal"));
            material.SetTexture("_ARM",Resources.Load<Texture2D>("Aquarium/Textures/"+prefix+"ARM"));
            material.SetTexture("_Caustics",caustics);material.SetFloat("_WorldScale",scale);
            if(!sand)material.SetFloat("_Sand",0);material.SetFloat("_NormalStrength",sand?.34f:.92f);
            material.SetColor("_Color",sand?C(0xe8d6b6):C(0xd4c5b3));
            return material;
        }
        private static void SurfaceBasis(Mesh mesh)
        {
            var normals=mesh.normals;var tangents=new Vector4[normals.Length];
            for(int i=0;i<normals.Length;i++)
            {
                var n=normals[i];var t=Vector3.Cross(Mathf.Abs(n.y)>.95f?Vector3.forward:Vector3.up,n).normalized;
                tangents[i]=new Vector4(t.x,t.y,t.z,1);
            }
            mesh.tangents=tangents;
        }
        private static void BuildSandBed(Transform root,Material material,DeckField field)
        {
            var sand=new Geo();field.Surface(sand,field.Water,0,Color.white);
            var go=sand.Emit("01 • Contoured sand bed / dunes and current ripples",root,material);
            var mesh=go.GetComponent<MeshFilter>().sharedMesh;
            // The clipped triangle lattice repeats vertices. Weld the sand only: its
            // normals and materials are continuous, reducing upload/memory without losing detail.
            var old=mesh.vertices;var indices=mesh.triangles;var remap=new int[old.Length];
            var unique=new List<Vector3>();var lookup=new Dictionary<long,int>();
            for(int i=0;i<old.Length;i++)
            {
                long key=((long)Mathf.RoundToInt(old[i].x*10000)<<32)|(uint)Mathf.RoundToInt(old[i].z*10000);
                int index;if(!lookup.TryGetValue(key,out index)){index=unique.Count;unique.Add(old[i]);lookup[key]=index;}remap[i]=index;
            }
            for(int i=0;i<indices.Length;i++)indices[i]=remap[indices[i]];
            mesh.Clear();mesh.SetVertices(unique);mesh.SetTriangles(indices,0);
            var points=mesh.vertices;var normals=new Vector3[points.Length];
            for(int i=0;i<points.Length;i++)
            {
                var p=new Point(points[i].x,points[i].z);points[i].y=AquariumSeabed.Height(p);
                var n=AquariumSeabed.Normal(p);normals[i]=new Vector3(n.x,n.y,n.z);
            }
            mesh.vertices=points;mesh.normals=normals;SurfaceBasis(mesh);mesh.RecalculateBounds();
        }
        private static void DisplayPanel(Geo g,Vector3 centre,Quaternion rotation,int panel)
        {
            float left=(panel%2)*.5f,right=left+.5f,bottom=panel<2?.5f:0,top=bottom+.5f;
            int v=g.Count;
            g.V(centre+rotation*new Vector3(-.95f,-.64f,0),Color.white,new Vector2(left+.002f,bottom+.002f));
            g.V(centre+rotation*new Vector3(.95f,-.64f,0),Color.white,new Vector2(right-.002f,bottom+.002f));
            g.V(centre+rotation*new Vector3(.95f,.64f,0),Color.white,new Vector2(right-.002f,top-.002f));
            g.V(centre+rotation*new Vector3(-.95f,.64f,0),Color.white,new Vector2(left+.002f,top-.002f));
            g.Q(v,v+1,v+2,v+3);
        }
    }
}

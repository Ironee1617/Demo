using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumMeshAudit
    {
        private struct Edge { public int count, balance; public Edge(int n,int b){count=n;balance=b;} }
        public static void InspectSaved()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Demos/HideAndSeek/Environment/Resources/Aquarium/CoralGallery.prefab");
            var lines=new List<string>();
            foreach(var mf in prefab.GetComponentsInChildren<MeshFilter>())
            {
                if(!mf.name.StartsWith("31 •")&&!mf.name.StartsWith("01 •"))continue;
                lines.Add(Inspect(mf.sharedMesh,mf.name,false));
            }
            Directory.CreateDirectory("Artifacts/CoralGallery");
            File.WriteAllLines("Artifacts/CoralGallery/mesh-audit-before.txt",lines);
            foreach(string line in lines)Debug.Log("MESH_AUDIT "+line);
        }
        public static string Inspect(Mesh mesh,string name,bool requireClosed)
        {
            var edges=new Dictionary<long,Edge>();var vertices=mesh.vertices;var triangles=mesh.triangles;int degenerate=0;
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                if(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).sqrMagnitude<1e-20f)degenerate++;
                Add(edges,a,b);Add(edges,b,c);Add(edges,c,a);
            }
            int boundary=edges.Values.Count(e=>e.count==1),nonmanifold=edges.Values.Count(e=>e.count>2);
            int reversed=edges.Values.Count(e=>e.count==2&&e.balance!=0);
            int interiorBoundary=0;var boundaryGraph=new Dictionary<int,List<int>>();
            foreach(var item in edges.Where(kv=>kv.Value.count==1))
            {
                int a=(int)(item.Key>>32),b=(int)(item.Key&0xffffffff);
                if(!boundaryGraph.ContainsKey(a))boundaryGraph[a]=new List<int>();
                if(!boundaryGraph.ContainsKey(b))boundaryGraph[b]=new List<int>();
                boundaryGraph[a].Add(b);boundaryGraph[b].Add(a);
                var mid=(vertices[a]+vertices[b])*.5f;var p=new Point(mid.x,mid.z);
                if(name.StartsWith("01 •")&&AquariumMapOutline.Contains(p,.035f)&&AquariumWalkwayLayout.SignedDistance(p)>.035f)interiorBoundary++;
            }
            int components=0;var visited=new HashSet<int>();
            foreach(int start in boundaryGraph.Keys)
            {
                if(!visited.Add(start))continue;components++;var pending=new Stack<int>();pending.Push(start);
                while(pending.Count>0)foreach(int next in boundaryGraph[pending.Pop()])if(visited.Add(next))pending.Push(next);
            }
            int irregularBoundary=boundaryGraph.Values.Count(neighbours=>neighbours.Count!=2);
            string result=$"{name}: vertices={vertices.Length}, triangles={triangles.Length/3}, boundary={boundary}, reversedSharedEdges={reversed}, nonmanifold={nonmanifold}, degenerate={degenerate}, boundaryLoops={components}, irregularBoundary={irregularBoundary}, analyticContourMarginSamples={interiorBoundary}";
            if(requireClosed&&(boundary!=0||reversed!=0||nonmanifold!=0||degenerate!=0))throw new Exception(result);
            if(name.StartsWith("01 •")&&(components!=1||irregularBoundary!=0||reversed!=0||nonmanifold!=0))throw new Exception("Sand must have one outer boundary and no internal holes: "+result);
            return result;
        }
        private static void Add(Dictionary<long,Edge> edges,int a,int b)
        {
            long key=((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);Edge e;edges.TryGetValue(key,out e);
            edges[key]=new Edge(e.count+1,e.balance+(a<b?1:-1));
        }
    }
}

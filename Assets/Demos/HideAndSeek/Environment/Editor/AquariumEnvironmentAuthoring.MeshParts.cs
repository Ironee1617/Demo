using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PrototypeLab.Aquarium.Editor
{
    public static partial class AquariumEnvironmentAuthoring
    {
        // Unity serializes the vertex/index buffers as hexadecimal in text assets.
        // Keep well below hosting limits, including headroom for the YAML wrapper.
        private const long MeshPartThreshold=40L*1024*1024;
        private const int PartVertices=160000,PartIndices=480000;
        private static readonly List<string> MeshPartReport=new List<string>();

        private sealed class MeshSource
        {
            public readonly Vector3[] positions,normals;
            public readonly Vector4[] tangents;
            public readonly Color[] colors;
            public readonly List<Vector4>[] uv=new List<Vector4>[8];
            public readonly int[] dimensions=new int[8],indices;
            public readonly Bounds bounds;
            public readonly string name;
            public MeshSource(Mesh mesh)
            {
                if(mesh.subMeshCount!=1||mesh.blendShapeCount!=0||mesh.bindposes.Length!=0)
                    throw new Exception("Mesh partitioning expects generated static, single-material meshes: "+mesh.name);
                positions=mesh.vertices;normals=mesh.normals;tangents=mesh.tangents;colors=mesh.colors;
                indices=mesh.triangles;bounds=mesh.bounds;name=mesh.name;
                for(int channel=0;channel<8;channel++)
                {
                    var attribute=(VertexAttribute)((int)VertexAttribute.TexCoord0+channel);
                    if(!mesh.HasVertexAttribute(attribute))continue;
                    dimensions[channel]=mesh.GetVertexAttributeDimension(attribute);
                    uv[channel]=new List<Vector4>();mesh.GetUVs(channel,uv[channel]);
                }
            }
        }

        private sealed class MeshPart
        {
            public Mesh mesh;
            public int[] sourceVertices;
            public int firstIndex,indexCount;
        }

        private static MeshPart MakeMeshPart(MeshSource source,List<int> vertices,List<int> indices,int firstIndex,int number)
        {
            var mesh=new Mesh {name=source.name+" / part "+number.ToString("000"),indexFormat=IndexFormat.UInt32};
            mesh.vertices=vertices.Select(i=>source.positions[i]).ToArray();
            if(source.normals.Length>0)mesh.normals=vertices.Select(i=>source.normals[i]).ToArray();
            if(source.tangents.Length>0)mesh.tangents=vertices.Select(i=>source.tangents[i]).ToArray();
            if(source.colors.Length>0)mesh.colors=vertices.Select(i=>source.colors[i]).ToArray();
            for(int channel=0;channel<8;channel++)
            {
                if(source.uv[channel]==null)continue;
                int c=channel;
                switch(source.dimensions[c])
                {
                    case 2:mesh.SetUVs(c,vertices.Select(i=>(Vector2)source.uv[c][i]).ToList());break;
                    case 3:mesh.SetUVs(c,vertices.Select(i=>(Vector3)source.uv[c][i]).ToList());break;
                    case 4:mesh.SetUVs(c,vertices.Select(i=>source.uv[c][i]).ToList());break;
                    default:throw new Exception("Unsupported generated UV channel dimension.");
                }
            }
            mesh.SetTriangles(indices,0,false);
            // Preserve the original normals/tangents and expanded animation bounds.
            // Recalculating normals separately would leave visible chunk seams.
            mesh.bounds=source.bounds;
            return new MeshPart {mesh=mesh,sourceVertices=vertices.ToArray(),firstIndex=firstIndex,indexCount=indices.Count};
        }

        private static List<MeshPart> PartitionMesh(MeshSource source)
        {
            var parts=new List<MeshPart>();var remap=new Dictionary<int,int>();
            var vertices=new List<int>();var indices=new List<int>();int first=0;
            for(int t=0;t<source.indices.Length;t+=3)
            {
                int additional=0;
                for(int c=0;c<3;c++)if(!remap.ContainsKey(source.indices[t+c]))additional++;
                if(indices.Count>0&&(indices.Count+3>PartIndices||vertices.Count+additional>PartVertices))
                {
                    parts.Add(MakeMeshPart(source,vertices,indices,first,parts.Count+1));
                    first=t;remap.Clear();vertices.Clear();indices.Clear();
                }
                for(int c=0;c<3;c++)
                {
                    int oldIndex=source.indices[t+c],local;
                    if(!remap.TryGetValue(oldIndex,out local))
                    {local=vertices.Count;remap.Add(oldIndex,local);vertices.Add(oldIndex);}
                    indices.Add(local);
                }
            }
            if(indices.Count>0)parts.Add(MakeMeshPart(source,vertices,indices,first,parts.Count+1));
            return parts;
        }

        private static void VerifyMeshPart(MeshSource original,MeshPart part,Mesh saved)
        {
            var actual=new MeshSource(saved);
            if(actual.positions.Length!=part.sourceVertices.Length||actual.indices.Length!=part.indexCount||!actual.bounds.Equals(original.bounds))
                throw new Exception("Partition count/bounds mismatch: "+saved.name);
            for(int i=0;i<part.sourceVertices.Length;i++)
            {
                int old=part.sourceVertices[i];
                if(!actual.positions[i].Equals(original.positions[old])||
                    (original.normals.Length>0&&!actual.normals[i].Equals(original.normals[old]))||
                    (original.tangents.Length>0&&!actual.tangents[i].Equals(original.tangents[old]))||
                    (original.colors.Length>0&&!actual.colors[i].Equals(original.colors[old])))
                    throw new Exception("Partition changed a vertex attribute: "+saved.name);
                for(int channel=0;channel<8;channel++)
                    if(original.uv[channel]!=null&&(actual.dimensions[channel]!=original.dimensions[channel]||!actual.uv[channel][i].Equals(original.uv[channel][old])))
                        throw new Exception("Partition changed UV/animation payload: "+saved.name);
            }
            for(int i=0;i<actual.indices.Length;i++)
                if(part.sourceVertices[actual.indices[i]]!=original.indices[part.firstIndex+i])
                    throw new Exception("Partition changed triangle order or connectivity: "+saved.name);
        }

        private static void StoreMeshParts(MeshFilter filter,Mesh mesh,string path)
        {
            long stride=0;
            for(int stream=0;stream<mesh.vertexBufferCount;stream++)stride+=mesh.GetVertexBufferStride(stream);
            long estimate=mesh.vertexCount*stride*2L+(long)mesh.GetIndexCount(0)*(mesh.indexFormat==IndexFormat.UInt32?8:4)+65536;
            if(estimate<=MeshPartThreshold)
            {filter.sharedMesh=Store(mesh,path);return;}
            var source=new MeshSource(mesh);var parts=PartitionMesh(source);
            var renderer=filter.GetComponent<MeshRenderer>();int indices=0;
            for(int i=0;i<parts.Count;i++)
            {
                string partPath=i==0?path:Path.ChangeExtension(path,null)+"_part"+(i+1).ToString("000")+".asset";
                var saved=Store(parts[i].mesh,partPath);
                VerifyMeshPart(source,parts[i],saved);indices+=parts[i].indexCount;
                if(i==0)filter.sharedMesh=saved;
                else
                {
                    var child=new GameObject(filter.name+" / part "+(i+1).ToString("000"));
                    child.layer=filter.gameObject.layer;child.isStatic=filter.gameObject.isStatic;
                    child.transform.SetParent(filter.transform,false);
                    child.AddComponent<MeshFilter>().sharedMesh=saved;
                    EditorUtility.CopySerialized(renderer,child.AddComponent<MeshRenderer>());
                }
            }
            if(indices!=source.indices.Length)throw new Exception("Partition lost triangles: "+source.name);
            MeshPartReport.Add(path+": "+parts.Count+" parts / "+(indices/3)+" triangles / positions, normals, tangents, colors, UVs, animation payload and triangle order identical — PASS");
        }

        private static void RemoveUnusedMeshParts(GameObject root)
        {
            var used=new HashSet<string>(root.GetComponentsInChildren<MeshFilter>().Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh)));
            foreach(string guid in AssetDatabase.FindAssets("t:Mesh",new[]{ResourcesRoot+"/Meshes"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                if(Regex.IsMatch(Path.GetFileName(path),@"^\d{3}_part\d{3}\.asset$")&&!used.Contains(path))AssetDatabase.DeleteAsset(path);
            }
        }

        public static void SplitSavedGalleryAndBuild()
        {
            MeshPartReport.Clear();Directory.CreateDirectory(Preview);
            EditorSceneManager.OpenScene(GalleryScene);
            var root=UnityEngine.Object.FindFirstObjectByType<AquariumEnvironmentRoot>();
            var camera=Camera.main;AquariumEnvironment.ConfigureCamera(camera);
            CaptureBiodiversity(camera);
            string before=Path.Combine(Preview,"MeshSplitBefore");Directory.CreateDirectory(before);
            foreach(string name in new[]{"20-diverse-reef-garden.png","21-living-anemones.png","24-mint-anemone.png"})
                File.Copy(Path.Combine(Preview,name),Path.Combine(before,name),true);
            long beforeTriangles=root.GetComponentsInChildren<MeshFilter>().Sum(f=>(long)f.sharedMesh.triangles.Length/3);
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
                StoreMeshParts(filter,filter.sharedMesh,AssetDatabase.GetAssetPath(filter.sharedMesh));
            RemoveUnusedMeshParts(root.gameObject);AssetDatabase.SaveAssets();
            long afterTriangles=root.GetComponentsInChildren<MeshFilter>().Sum(f=>(long)f.sharedMesh.triangles.Length/3);
            if(beforeTriangles!=afterTriangles)throw new Exception("Scene triangle count changed.");
            string prefab=ResourcesRoot+"/CoralGallery.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject,prefab,InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),GalleryScene);
            EditorSceneManager.OpenScene(GameScene);
            var old=UnityEngine.Object.FindFirstObjectByType<AquariumEnvironmentRoot>();
            if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            var assets=Directory.GetFiles(ResourcesRoot+"/Meshes","*.asset").Select(p=>new FileInfo(p)).ToArray();
            if(assets.Any(f=>f.Length>MeshPartThreshold))throw new Exception("A mesh asset is still over the partition threshold.");
            MeshPartReport.Add("Scene triangles before/after: "+beforeTriangles+" / "+afterTriangles+" — PASS");
            MeshPartReport.Add("Largest mesh asset: "+assets.Max(f=>f.Length)+" bytes; all below 40 MiB — PASS");
            File.WriteAllLines(Path.Combine(Preview,"mesh-partition-validation.txt"),MeshPartReport);
            Debug.Log("AQUARIUM_MESH_PARTITION_OK "+string.Join(" | ",MeshPartReport));
            CaptureSavedGallery();Validate(UnityEngine.Object.FindFirstObjectByType<AquariumEnvironmentRoot>());
            CaptureLivingMotion();BuildSavedLivingPlayer();
        }

        public static void VerifyPartitionRendering()
        {
            EditorSceneManager.OpenScene(GalleryScene);
            var root=UnityEngine.Object.FindFirstObjectByType<AquariumEnvironmentRoot>();
            var camera=Camera.main;AquariumEnvironment.ConfigureCamera(camera);
            var groups=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.Contains(" / part "))
                .GroupBy(f=>f.transform.parent).Select(group=>new[]{group.Key.GetComponent<MeshFilter>()}.Concat(group).ToArray()).ToArray();
            var merged=new List<Mesh>();
            var originals=groups.Select(group=>group[0].sharedMesh).ToArray();
            foreach(var group in groups)
            {
                var mesh=new Mesh {indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(group.Select(f=>new CombineInstance {mesh=f.sharedMesh,transform=Matrix4x4.identity}).ToArray(),true,false);
                mesh.bounds=group[0].sharedMesh.bounds;merged.Add(mesh);
            }
            string folder=Path.Combine(Preview,"MeshSplitComparison");Directory.CreateDirectory(folder);
            var lines=new List<string>();
            try
            {
                for(int view=0;view<3;view++)
                {
                    if(view==0)
                    {
                        camera.orthographic=false;camera.fieldOfView=56;var target=root.biodiversityPreviewPoint;
                        camera.transform.position=target+new Vector3(3.7f,2.8f,-4.4f);camera.transform.LookAt(target+Vector3.up*.7f);
                    }
                    else if(view==1)AnemoneView(camera,root);else MintAnemoneView(camera,root);
                    for(int g=0;g<groups.Length;g++)
                    {
                        groups[g][0].sharedMesh=merged[g];
                        foreach(var part in groups[g].Skip(1))part.GetComponent<MeshRenderer>().enabled=false;
                    }
                    Capture(camera,null,960,600,3);var before=Capture(camera,null,960,600,3);
                    Capture(camera,Path.Combine(folder,"view-"+view+"-joined.png"),960,600,3);
                    for(int g=0;g<groups.Length;g++)
                    {
                        groups[g][0].sharedMesh=originals[g];
                        foreach(var part in groups[g].Skip(1))part.GetComponent<MeshRenderer>().enabled=true;
                    }
                    Capture(camera,null,960,600,3);var after=Capture(camera,null,960,600,3);
                    Capture(camera,Path.Combine(folder,"view-"+view+"-split.png"),960,600,3);
                    long total=0;int changed=0;
                    for(int p=0;p<before.Length;p++)
                    {
                        int r=Math.Abs(before[p].r-after[p].r),g=Math.Abs(before[p].g-after[p].g),b=Math.Abs(before[p].b-after[p].b);
                        total+=r+g+b;if(Math.Max(r,Math.Max(g,b))>3)changed++;
                    }
                    float mean=total/(before.Length*3f),fraction=changed/(float)before.Length;
                    lines.Add("View "+view+": mean channel difference="+mean+", changed pixels (>3)="+changed+" / "+before.Length);
                    if(mean>.1f||fraction>.002f)throw new Exception("Partition rendering mismatch: "+lines.Last());
                }
                File.WriteAllLines(Path.Combine(Preview,"mesh-partition-render-validation.txt"),lines);
                Debug.Log("AQUARIUM_PARTITION_RENDER_OK "+string.Join(" | ",lines));
            }
            finally
            {
                for(int g=0;g<groups.Length;g++)
                {
                    groups[g][0].sharedMesh=originals[g];
                    foreach(var part in groups[g].Skip(1))part.GetComponent<MeshRenderer>().enabled=true;
                    UnityEngine.Object.DestroyImmediate(merged[g]);
                }
            }
        }
    }
}

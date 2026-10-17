using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumCharacterImport
    {
        public static void ImportAndCheck(){Run();AquariumCharacterChecks.Run();}
        public static void RefinementQA(){Run();AquariumCharacterChecks.Run();AquariumCharacterChecks.InTank();}
        public static void ValidateAndBuild(){Run();AquariumCharacterChecks.Run();AquariumCharacterChecks.SwimReview();AquariumCharacterChecks.InTank();AquariumBuild.Windows();}
        const string Root="Assets/Demos/HideAndSeek/Characters/Resources/AquariumCharacters";
        [Serializable] class Bone {public string name;public int parent;public float[] position,rotation,scale;}
        [Serializable] class Shape {public string name;public float[] values;}
        [Serializable] class Clip {public string name;public float duration;public bool loop;public float[] values;public Shape[] shapes;}
        [Serializable] class Data {public string name;public Bone[] bones;public Clip[] clips;public string[] shapeNames;public bool closedEnvelope;public float[] mouthAnchor,throatAnchor;}
        static Vector3 V(float[] f)=>new Vector3(f[0],f[1],f[2]);
        static Vector3 ReadV(BinaryReader r)=>new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
        static Vector3[] SurfaceNormals(Vector3[] positions,int[] triangles,Vector3[] weldKeys)
        {
            // UV seams duplicate vertices. Sum their geometric face normals
            // together so a lip shape does not reveal the UV islands as facets.
            var sum=new System.Collections.Generic.Dictionary<Vector3,Vector3>();
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];Vector3 n=Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]);
                foreach(int k in new[]{a,b,c}){sum.TryGetValue(weldKeys[k],out var old);sum[weldKeys[k]]=old+n;}
            }
            var result=new Vector3[positions.Length];for(int i=0;i<result.Length;i++)result[i]=sum[weldKeys[i]].normalized;return result;
        }
        [MenuItem("Aquarium/Characters/Import Blender Rigs")]
        public static void Run()
        {
            string source=Environment.GetEnvironmentVariable("AQUARIUM_RIG_SOURCE");
            if(string.IsNullOrEmpty(source))throw new Exception("Set AQUARIUM_RIG_SOURCE to the Blender Export folder.");
            Directory.CreateDirectory(Root);
            string models=Environment.GetEnvironmentVariable("AQUARIUM_RIG_MODELS");
            foreach(string name in (string.IsNullOrEmpty(models)?"Alien,Fish0,Fish0Mouth,Fish0Closed":models).Split(','))
            {
                if(name!="Alien" && name!="Fish0" && name!="Fish0Mouth" && name!="Fish0Closed")throw new Exception("Unknown rig "+name);
                Import(source,name);
            }
            var normal=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Fish0.prefab");var feeding=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Fish0Mouth.prefab");
            var colors=new[]{new Color(1,.94f,.83f),new Color(.73f,.87f,1),new Color(1,.80f,.88f)};
            for(int i=0;i<3;i++)
            {
                string p=Root+"/Species"+i+".asset";var s=AssetDatabase.LoadAssetAtPath<AquariumSpecies>(p);
                if(s==null){s=ScriptableObject.CreateInstance<AquariumSpecies>();AssetDatabase.CreateAsset(s,p);}
                s.speciesId=i;s.normal=normal;s.feeding=feeding;s.closedFeeding=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Fish0Closed.prefab");s.tint=colors[i];EditorUtility.SetDirty(s);
            }
            var metal=new Material(Shader.Find("Standard")){color=new Color(.32f,.40f,.46f)};metal.SetFloat("_Metallic",.8f);Save(metal,Root+"/HarpoonMetal.mat");
            Save(new Material(Shader.Find("Aquarium/Opaque Metamorphosis")),Root+"/Metamorphosis.mat");
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("AQUARIUM_CHARACTER_IMPORT_OK");
        }
        static void Save(UnityEngine.Object value,string path)
        {
            var old=AssetDatabase.LoadMainAssetAtPath(path);
            if(old==null)AssetDatabase.CreateAsset(value,path);
            else if(value is Mesh source && old is Mesh target)
            {
                target.Clear();target.ClearBlendShapes();target.indexFormat=source.indexFormat;target.vertices=source.vertices;target.normals=source.normals;target.tangents=source.tangents;target.uv=source.uv;target.colors=source.colors;target.triangles=source.triangles;target.boneWeights=source.boneWeights;target.bindposes=source.bindposes;target.bounds=source.bounds;
                for(int i=0;i<source.blendShapeCount;i++)
                {var v=new Vector3[source.vertexCount];var n=new Vector3[v.Length];var t=new Vector3[v.Length];source.GetBlendShapeFrameVertices(i,0,v,n,t);target.AddBlendShapeFrame(source.GetBlendShapeName(i),100,v,n,t);}
                EditorUtility.SetDirty(target);UnityEngine.Object.DestroyImmediate(source);
            }
            else{EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);}
        }
        static Mesh ImportEnvelope(string source,string name)
        {
            var envelope=new Mesh{name=name+"_Envelope",indexFormat=IndexFormat.UInt32};
            using(var r=new BinaryReader(File.OpenRead(Path.Combine(source,name+".aqenv"))))
            {
                int n=r.ReadInt32(),count=r.ReadInt32();var v=new Vector3[n];var normal=new Vector3[n];var colors=new Color[n];var weights=new BoneWeight[n];
                for(int i=0;i<n;i++){v[i]=ReadV(r);normal[i]=ReadV(r);colors[i]=new Color(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),1);r.ReadSingle();weights[i]=new BoneWeight{boneIndex0=0,weight0=1};}
                var indices=new int[count];for(int i=0;i<count;i++)indices[i]=r.ReadInt32();
                envelope.vertices=v;envelope.colors=colors;envelope.triangles=indices;envelope.boneWeights=weights;envelope.bindposes=new[]{Matrix4x4.identity};envelope.RecalculateNormals();envelope.RecalculateBounds();
            }
            Save(envelope,Root+"/"+name+"_Envelope.asset");
            envelope=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/"+name+"_Envelope.asset");envelope.ClearBlendShapes();
            using(var r=new BinaryReader(File.OpenRead(Path.Combine(source,name+".aqsmooth"))))
            {
                var v=envelope.vertices;var n=envelope.normals;var smooth=new Vector3[v.Length];var delta=new Vector3[v.Length];var deltaNormals=new Vector3[v.Length];
                for(int i=0;i<v.Length;i++){smooth[i]=ReadV(r);delta[i]=smooth[i]-v[i];}
                var temp=new Mesh{indexFormat=IndexFormat.UInt32};temp.vertices=smooth;temp.triangles=envelope.triangles;temp.RecalculateNormals();var sn=temp.normals;
                for(int i=0;i<v.Length;i++)deltaNormals[i]=sn[i]-n[i];UnityEngine.Object.DestroyImmediate(temp);
                envelope.AddBlendShapeFrame("Organic relaxation",100,delta,deltaNormals,null);EditorUtility.SetDirty(envelope);
            }
            return envelope;
        }
        static void Import(string source,string name)
        {
            var d=JsonUtility.FromJson<Data>(File.ReadAllText(Path.Combine(source,name+".json")));
            var root=new GameObject(name);var rig=root.AddComponent<AquariumRig>();var transforms=new Transform[d.bones.Length];var paths=new string[d.bones.Length];
            for(int i=0;i<d.bones.Length;i++)
            {
                var b=d.bones[i];var t=new GameObject(b.name).transform;transforms[i]=t;t.SetParent(b.parent<0?root.transform:transforms[b.parent],false);
                t.localPosition=V(b.position);t.localRotation=new Quaternion(b.rotation[0],b.rotation[1],b.rotation[2],b.rotation[3]);t.localScale=V(b.scale);
                paths[i]=AnimationUtility.CalculateTransformPath(t,root.transform);
            }
            Mesh mesh=new Mesh{name=name+" optimized skin",indexFormat=IndexFormat.UInt32};
            using(var r=new BinaryReader(File.OpenRead(Path.Combine(source,name+".aqmesh"))))
            {
                int n=r.ReadInt32(),count=r.ReadInt32();var vertices=new Vector3[n];var normals=new Vector3[n];var uv=new Vector2[n];var weights=new BoneWeight[n];var morph=new Vector3[n];
                for(int i=0;i<n;i++)
                {
                    vertices[i]=ReadV(r);normals[i]=ReadV(r);uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());
                    weights[i]=new BoneWeight{weight0=r.ReadSingle(),weight1=r.ReadSingle(),weight2=r.ReadSingle(),weight3=r.ReadSingle(),boneIndex0=r.ReadInt32(),boneIndex1=r.ReadInt32(),boneIndex2=r.ReadInt32(),boneIndex3=r.ReadInt32()};morph[i]=ReadV(r);
                }
                var indices=new int[count];for(int i=0;i<count;i++)indices[i]=r.ReadInt32();
                mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=indices;mesh.boneWeights=weights;
                mesh.bindposes=transforms.Select(t=>t.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();
                mesh.AddBlendShapeFrame("Metamorphosis",100,morph,null,null);mesh.RecalculateBounds();mesh.RecalculateTangents();
            }
            if(File.Exists(Path.Combine(source,name+".aqcolors")))using(var r=new BinaryReader(File.OpenRead(Path.Combine(source,name+".aqcolors"))))
            {var colors=new Color[mesh.vertexCount];for(int i=0;i<colors.Length;i++)colors[i]=new Color(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());mesh.colors=colors;}
            else mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();
            var weldKeys=mesh.vertices;var baseShapeNormals=SurfaceNormals(weldKeys,mesh.triangles,weldKeys);
            if(d.shapeNames!=null)foreach(string shape in d.shapeNames)using(var r=new BinaryReader(File.OpenRead(Path.Combine(source,name+"_"+shape+".aqshape"))))
            {
                var v=mesh.vertices;var normal=mesh.normals;var delta=new Vector3[v.Length];var dn=new Vector3[v.Length];
                for(int i=0;i<v.Length;i++){delta[i]=ReadV(r);v[i]+=delta[i];}
                var normals=SurfaceNormals(v,mesh.triangles,weldKeys);
                for(int i=0;i<v.Length;i++)dn[i]=normals[i]-baseShapeNormals[i];
                mesh.AddBlendShapeFrame(shape,100,delta,dn,null);
            }
            Save(mesh,Root+"/"+name+".asset");mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/"+name+".asset");
            rig.envelope=ImportEnvelope(source,name);
            rig.closedEnvelope=d.closedEnvelope?ImportEnvelope(source,name+"_Closed"):null;
            rig.mouthAnchor=d.mouthAnchor!=null?V(d.mouthAnchor):name=="Alien"?new Vector3(0,-.145f,.19f):new Vector3(0,-.025f,.34f);
            rig.throatAnchor=d.throatAnchor!=null?V(d.throatAnchor):name=="Alien"?new Vector3(0,-.11f,.07f):new Vector3(0,-.035f,.045f);
            foreach(string role in new[]{"BaseColor","Normal","RM"})
            {
                string path=Root+"/"+name+"_"+role+".png";string input=Path.Combine(source,name+"_"+role+".png");
                if(File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(input)))continue;
                File.Copy(input,path,true);AssetDatabase.ImportAsset(path);
                var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.maxTextureSize=4096;ti.mipmapEnabled=true;ti.anisoLevel=4;ti.sRGBTexture=role=="BaseColor";ti.textureCompression=TextureImporterCompression.CompressedHQ;
                if(role=="Normal")ti.textureType=TextureImporterType.NormalMap;ti.SaveAndReimport();
            }
            var material=new Material(Shader.Find("Aquarium/Articulated Skin"));material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+name+"_BaseColor.png"));material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+name+"_Normal.png"));material.SetTexture("_RM",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+name+"_RM.png"));Save(material,Root+"/"+name+".mat");
            var skin=new GameObject("Deforming surface").AddComponent<SkinnedMeshRenderer>();skin.transform.SetParent(root.transform,false);skin.sharedMesh=mesh;skin.bones=transforms;skin.rootBone=transforms[0];skin.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/"+name+".mat");skin.localBounds=new Bounds(Vector3.zero,Vector3.one*4);skin.quality=SkinQuality.Bone4;skin.updateWhenOffscreen=false;rig.skin=skin;
            rig.restPositions=transforms.Select(t=>t.localPosition).ToArray();rig.restRotations=transforms.Select(t=>t.localRotation).ToArray();rig.restScales=transforms.Select(t=>t.localScale).ToArray();
            rig.clips=new AnimationClip[d.clips.Length];
            for(int c=0;c<d.clips.Length;c++)
            {
                var input=d.clips[c];var clip=new AnimationClip{name=input.name,legacy=true,frameRate=30,wrapMode=input.loop?WrapMode.Loop:WrapMode.ClampForever};int frames=input.values.Length/d.bones.Length/10;
                for(int b=0;b<d.bones.Length;b++)for(int field=0;field<10;field++)
                {
                    string property=field<3?"m_LocalPosition."+"xyz"[field]:field<7?"m_LocalRotation."+"xyzw"[field-3]:"m_LocalScale."+"xyz"[field-7];
                    var keys=new Keyframe[frames];for(int f=0;f<frames;f++)keys[f]=new Keyframe(f/30f,input.values[(f*d.bones.Length+b)*10+field]);
                    var curve=new AnimationCurve(keys);for(int k=0;k<keys.Length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Auto);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Auto);}
                    clip.SetCurve(paths[b],typeof(Transform),property,curve);
                }
                if(input.shapes!=null)foreach(var shape in input.shapes)
                {
                    var keys=new Keyframe[shape.values.Length];for(int f=0;f<keys.Length;f++)keys[f]=new Keyframe(f/30f,shape.values[f]);
                    clip.SetCurve("Deforming surface",typeof(SkinnedMeshRenderer),"blendShape."+shape.name,new AnimationCurve(keys));
                }
                clip.EnsureQuaternionContinuity();string path=Root+"/"+name+"_"+input.name+".anim";Save(clip,path);rig.clips[c]=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            }
            PrefabUtility.SaveAsPrefabAsset(root,Root+"/"+name+".prefab");
            Debug.Log("RIG_VALIDATED "+name+" triangles="+mesh.triangles.Length/3+" bones="+transforms.Length+" clips="+rig.clips.Length);UnityEngine.Object.DestroyImmediate(root);
        }
    }
}

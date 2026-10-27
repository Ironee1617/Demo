using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium.Editor
{
    // Reproducible importer for the approved cartoon sculpts and transferred morphs.
    public static class AquariumCartoonImport
    {
        const string Characters="Assets/Demos/HideAndSeek/Characters/Resources/AquariumCharacters";
        const string Equipment="Assets/Demos/HideAndSeek/Equipment/Resources/AquariumEquipment";
        [Serializable] public class Bone {public string name;public int parent;public float[] position,rotation,scale,world;}
        [Serializable] public class Data {public string name,kind,atlas;public bool firstPerson;public Bone[] bones;public string[] shapeNames,parts;public int[] partVertexCounts;public int vertices,triangles;public float[] mouthAnchor,throatAnchor,gunPosition,gunRotation,muzzle,ropeExit,eyes;public float gunScale,headLength,eyeRadius;}
        static Vector3 V(float[] a)=>new Vector3(a[0],a[1],a[2]);
        static Vector3 V(BinaryReader r)=>new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
        static string Source=>Environment.GetEnvironmentVariable("AQUARIUM_CARTOON_SOURCE")??Path.GetFullPath(Path.Combine(Application.dataPath,"../ArtSources/Cartoon/Export"));
        static T Save<T>(T value,string path) where T:UnityEngine.Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old==null){AssetDatabase.CreateAsset(value,path);return value;}
            // CopySerialized keeps stale native bone bounds when a mesh changes vertex count.
            if(value is Mesh source && old is Mesh target)
            {
                target.Clear(false);target.ClearBlendShapes();target.indexFormat=source.indexFormat;target.vertices=source.vertices;target.normals=source.normals;target.tangents=source.tangents;target.uv=source.uv;target.uv2=source.uv2;target.subMeshCount=source.subMeshCount;
                for(int i=0;i<source.subMeshCount;i++)target.SetTriangles(source.GetTriangles(i),i);
                target.bindposes=source.bindposes;target.boneWeights=source.boneWeights;
                for(int i=0;i<source.blendShapeCount;i++){var v=new Vector3[source.vertexCount];var n=new Vector3[v.Length];var t=new Vector3[v.Length];source.GetBlendShapeFrameVertices(i,0,v,n,t);target.AddBlendShapeFrame(source.GetBlendShapeName(i),100,v,n,t);}
                target.bounds=source.bounds;EditorUtility.SetDirty(target);UnityEngine.Object.DestroyImmediate(source);return old;
            }
            EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;
        }
        static Transform Child(string name,Transform parent,Vector3 p)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=p;return t;}
        static Mesh Read(Data d,string folder,Transform[] bones,Transform root)
        {
            var mesh=new Mesh{name=d.name+" cartoon surface",indexFormat=IndexFormat.UInt32};
            using(var r=new BinaryReader(File.OpenRead(Path.Combine(Source,d.name+".cartoon"))))
            {
                int n=r.ReadInt32(),count=r.ReadInt32(),shapes=r.ReadInt32(),sub=r.ReadInt32();
                if(n!=d.vertices || shapes!=d.shapeNames.Length)throw new Exception("Export header mismatch "+d.name);
                var v=new Vector3[n];var normal=new Vector3[n];var uv=new Vector2[n];var surface=new Vector2[n];var weights=new BoneWeight[n];
                for(int i=0;i<n;i++)
                {
                    v[i]=V(r);normal[i]=V(r);uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());surface[i]=new Vector2(r.ReadSingle(),r.ReadSingle());
                    weights[i]=new BoneWeight{weight0=r.ReadSingle(),weight1=r.ReadSingle(),weight2=r.ReadSingle(),weight3=r.ReadSingle(),boneIndex0=(int)r.ReadSingle(),boneIndex1=(int)r.ReadSingle(),boneIndex2=(int)r.ReadSingle(),boneIndex3=(int)r.ReadSingle()};
                }
                var indices=new int[count];for(int i=0;i<count;i++)indices[i]=r.ReadInt32();
                var groups=new List<int>[sub];for(int i=0;i<sub;i++)groups[i]=new List<int>();
                for(int i=0;i<count;i+=3){var group=groups[r.ReadInt32()];group.Add(indices[i]);group.Add(indices[i+1]);group.Add(indices[i+2]);}
                mesh.vertices=v;mesh.normals=normal;mesh.uv=uv;mesh.uv2=surface;mesh.subMeshCount=sub;
                for(int i=0;i<sub;i++)mesh.SetTriangles(groups[i],i);
                if(bones!=null){mesh.boneWeights=weights;mesh.bindposes=bones.Select(t=>t.worldToLocalMatrix*root.localToWorldMatrix).ToArray();}
                for(int k=0;k<shapes;k++)
                {
                    var dv=new Vector3[n];var dn=new Vector3[n];for(int i=0;i<n;i++){dv[i]=V(r);dn[i]=V(r);}mesh.AddBlendShapeFrame(d.shapeNames[k],100,dv,dn,null);
                }
                if(r.BaseStream.Position!=r.BaseStream.Length)throw new Exception("Unread geometry "+d.name);
            }
            mesh.RecalculateBounds();mesh.RecalculateTangents();return Save(mesh,folder+"/"+d.name+".asset");
        }
        static Material[] Materials(Data d,string folder,bool glass)
        {
            string path=folder+"/"+d.atlas+"_Atlas.png";
            File.Copy(Path.Combine(Source,d.atlas+"_Atlas.png"),path,true);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.maxTextureSize=4096;importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.anisoLevel=4;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            var m=new Material(Shader.Find("Aquarium/Cartoon Character"));m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var main=Save(m,folder+"/"+d.atlas+"_Cartoon.mat");if(!glass)return new[]{main};
            var lens=new Material(Shader.Find("Standard"));lens.color=new Color(.80f,.94f,1,.035f);lens.SetFloat("_Mode",3);lens.SetInt("_SrcBlend",(int)BlendMode.One);lens.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);lens.SetInt("_ZWrite",0);lens.EnableKeyword("_ALPHAPREMULTIPLY_ON");lens.renderQueue=3000;lens.SetFloat("_Glossiness",.93f);
            return new[]{main,Save(lens,folder+"/Diver_Lens.mat")};
        }
        static Data Load(string name)=>JsonUtility.FromJson<Data>(File.ReadAllText(Path.Combine(Source,name+".json")));
        static AquariumRig Rig(Data d,string folder)
        {
            var go=new GameObject(d.name);var rig=go.AddComponent<AquariumRig>();var bones=new Transform[d.bones.Length];
            for(int i=0;i<bones.Length;i++){var b=d.bones[i];bones[i]=Child(b.name,b.parent<0?go.transform:bones[b.parent],V(b.position));}
            var skin=Child("Deforming surface",go.transform,Vector3.zero).gameObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh=Read(d,folder,bones,go.transform);skin.bones=bones;skin.rootBone=bones[0];skin.quality=SkinQuality.Bone4;skin.localBounds=new Bounds(Vector3.zero,Vector3.one*5);skin.sharedMaterials=Materials(d,folder,skin.sharedMesh.subMeshCount>1);
            skin.receiveShadows=false;
            rig.skin=skin;rig.cartoon=true;rig.cartoonKind=d.kind;rig.restPositions=bones.Select(b=>b.localPosition).ToArray();rig.restRotations=bones.Select(b=>b.localRotation).ToArray();rig.restScales=bones.Select(b=>b.localScale).ToArray();
            rig.mouthAnchor=V(d.mouthAnchor);rig.throatAnchor=V(d.throatAnchor);
            if(d.eyes!=null && d.eyes.Length>=6)
            {
                // R22: painted fish carry their eye positions (pupil xyz + gaze axis xyz per eye).
                int count=d.eyes.Length/6;rig.eyePupils=new Vector3[count];rig.eyeAxes=new Vector3[count];
                for(int i=0;i<count;i++){rig.eyePupils[i]=new Vector3(d.eyes[i*6],d.eyes[i*6+1],d.eyes[i*6+2]);rig.eyeAxes[i]=new Vector3(d.eyes[i*6+3],d.eyes[i*6+4],d.eyes[i*6+5]).normalized;}
                rig.eyeRadius=d.eyeRadius;
            }
            if(d.kind=="Diver")
            {
                var socket=Child("Harpoon grip socket",bones.Single(b=>b.name=="HandL"),V(d.gunPosition));socket.localRotation=new Quaternion(d.gunRotation[0],d.gunRotation[1],d.gunRotation[2],d.gunRotation[3]);socket.localScale=Vector3.one*d.gunScale;rig.gunSocket=socket;
                rig.firstPerson=d.firstPerson;
                if(!d.firstPerson)CorrectProneNormals(rig,d);
            }
            string[] names=d.kind=="Diver"?new[]{"Idle","Swim","Fire","Reload","FPSIdle","FPSFire","Eaten","Harpoon"}:new[]{"Idle","Swim","Bite","TentacleEat","Eaten","Harpoon","Morph","Unmorph","SpitRecoil","SpitOut"};
            rig.clips=names.Select(name=>CreateClip(rig,d,name,folder)).ToArray();Reset(rig);PrefabUtility.SaveAsPrefabAsset(go,folder+"/"+d.name+".prefab");return rig;
        }
        static void Reset(AquariumRig r)
        {for(int i=0;i<r.Bones.Length;i++){r.Bones[i].localPosition=r.restPositions[i];r.Bones[i].localRotation=r.restRotations[i];r.Bones[i].localScale=r.restScales[i];}for(int i=0;i<r.skin.sharedMesh.blendShapeCount;i++)r.skin.SetBlendShapeWeight(i,0);}
        static float S(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        static float Bump(float t,float a,float b)=>Mathf.Sin(Mathf.PI*Mathf.Clamp01((t-a)/(b-a)));
        static void Shape(AquariumRig r,string name,float value){int k=r.skin.sharedMesh.GetBlendShapeIndex(name);if(k>=0)r.skin.SetBlendShapeWeight(k,value*100);}
        static void CorrectProneNormals(AquariumRig rig,Data d)
        {
            // Large shoulder/ankle bends need the normals of the deformed continuous surface.
            Pose(rig,d,"Swim",0);var mesh=rig.skin.sharedMesh;var baked=new Mesh();rig.skin.BakeMesh(baked);
            int part=Array.IndexOf(d.parts,"Diver_ContinuousWetsuit"),start=d.partVertexCounts.Take(part).Sum(),end=start+d.partVertexCounts[part];
            var rest=mesh.vertices;var pose=baked.vertices;var original=mesh.normals;var weights=mesh.boneWeights;var indices=mesh.triangles;var sum=new Dictionary<Vector3,Vector3>();var before=(Vector3[])pose.Clone();
            for(int i=start;i<end;i++)sum[rest[i]]=Vector3.zero;
            for(int k=0;k<indices.Length;k+=3){int a=indices[k],b=indices[k+1],c=indices[k+2];if(a<start||a>=end)continue;var n=Vector3.Cross(pose[b]-pose[a],pose[c]-pose[a]);sum[rest[a]]+=n;sum[rest[b]]+=n;sum[rest[c]]+=n;}
            var matrices=rig.Bones.Select((bone,i)=>bone.localToWorldMatrix*mesh.bindposes[i]).ToArray();var delta=new Vector3[rest.Length];var positions=new Vector3[rest.Length];
            for(int i=start;i<end;i++)
            {
                var w=weights[i];var matrix=new Matrix4x4();int[] ids={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] amounts={w.weight0,w.weight1,w.weight2,w.weight3};
                for(int j=0;j<4;j++)for(int row=0;row<4;row++)for(int col=0;col<4;col++)matrix[row,col]+=matrices[ids[j]][row,col]*amounts[j];
                delta[i]=matrix.inverse.MultiplyVector(sum[rest[i]].normalized).normalized-original[i];
                positions[i]=matrix.inverse.MultiplyVector(pose[i]-before[i]);
            }
            mesh.AddBlendShapeFrame("ProneSurfaceNormals",100,positions,delta,null);d.shapeNames=d.shapeNames.Concat(new[]{"ProneSurfaceNormals"}).ToArray();EditorUtility.SetDirty(mesh);UnityEngine.Object.DestroyImmediate(baked);Reset(rig);
        }
        static void Blink(AquariumRig r,float v)
        {
            for(int i=1;i<=12;i++)
            {
                string key=i%3==0?"Blink_"+(i*100/12):"BlinkStep_"+i.ToString("00");
                Shape(r,key,i==12?Mathf.Clamp01(v*12-11):Mathf.Max(0,1-Mathf.Abs(v*12-i)));
            }
        }
        static void Aim(Transform bone,Transform child,Vector3 at){bone.rotation=Quaternion.FromToRotation(child.position-bone.position,at-bone.position)*bone.rotation;}
        static void Arm(AquariumRig rig,string side,Vector3 at,Vector3 pole,Quaternion handRotation)
        {
            var a=rig.Bones.Single(b=>b.name=="UpperArm"+side);var b=rig.Bones.Single(x=>x.name=="Forearm"+side);var c=rig.Bones.Single(x=>x.name=="Hand"+side);
            float l1=Vector3.Distance(a.position,b.position),l2=Vector3.Distance(b.position,c.position);Vector3 d=at-a.position;float length=Mathf.Clamp(d.magnitude,Mathf.Abs(l1-l2)+.001f,l1+l2-.001f);d.Normalize();
            float along=(l1*l1-l2*l2+length*length)/(2*length);Vector3 n=Vector3.ProjectOnPlane(pole-a.position,d).normalized;
            var elbow=a.position+d*along+n*Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
            // Orient from the authored limb axes, avoiding inherited torso roll in the wrist.
            a.rotation=Quaternion.FromToRotation(b.localPosition,elbow-a.position);
            b.rotation=Quaternion.FromToRotation(c.localPosition,a.position+d*length-b.position);c.rotation=handRotation;
        }
        static void Pose(AquariumRig r,Data d,string clip,float t)
        {
            Reset(r);bool idle=clip=="Idle";bool eat=clip=="Bite"||clip=="TentacleEat";bool dead=clip=="Harpoon"||clip=="Eaten";if(dead)t=Mathf.Min(t,2.1f);float wave=Mathf.Sin(t*Mathf.PI*2/1.2f);Transform body=r.Bones[1];
            if(d.firstPerson)
            {
                // These sleeves are modeled in camera space. Only the forearms bend;
                // palm, thumb and index move rigidly together with the weapon socket.
                float breath=Mathf.Sin(t*Mathf.PI*2/2.4f);
                float recoil=(clip=="Fire"||clip=="FPSFire")?Mathf.Sin(Mathf.PI*S(0,.28f,t))*(1-S(.18f,.45f,t)):0;
                float reload=clip=="Reload"?Mathf.Sin(Mathf.PI*S(0,2.4f,t)):0;
                body.localPosition+=new Vector3(breath*.0015f,breath*.003f-reload*.015f,-recoil*.025f);
                body.localRotation=Quaternion.Euler(-recoil*3+reload*2,0,0);
                r.Bones.Single(b=>b.name=="ForearmL").localRotation=Quaternion.Euler(breath*.3f,0,0);
                r.Bones.Single(b=>b.name=="ForearmR").localRotation=Quaternion.Euler(breath*.8f,0,-breath*.5f);
                return;
            }
            if(d.kind=="Diver")
            {
                bool fps=clip.StartsWith("FPS");float pace=idle?.25f:1;float kick=wave*18*pace;float recoil=(clip=="Fire"||clip=="FPSFire")?Mathf.Sin(Mathf.PI*S(0,.28f,t))*(1-S(.18f,.45f,t)):0;
                body.localRotation=Quaternion.Euler(idle?78:90,Mathf.Sin(t*2.6f)*1.2f*pace,Mathf.Sin(t*2.6f)*1.4f*pace);body.localPosition+=new Vector3(0,Mathf.Sin(t*2.6f)*.012f,0);
                r.Bones[2].rotation=Quaternion.Euler(3,0,0);
                foreach(string side in new[]{"L","R"})
                {
                    float sign=side=="L"?1:-1;float k=kick*sign;var thigh=r.Bones.Single(b=>b.name=="Thigh"+side);var shin=r.Bones.Single(b=>b.name=="Shin"+side);var fin=r.Bones.Single(b=>b.name=="Flipper"+side);
                    float drag=Mathf.Sin(t*Mathf.PI*2/1.2f-.65f)*sign*pace;
                    thigh.rotation=Quaternion.Euler(90+k,0,sign*2);shin.rotation=Quaternion.Euler(94+k*.55f+Mathf.Max(0,-k)*.8f,0,sign*2);fin.rotation=Quaternion.Euler(153+k*.35f+drag*9,0,sign*3);
                    r.Bones.Single(b=>b.name=="FinTip"+side).localRotation=Quaternion.Euler(22+Mathf.Sin(t*Mathf.PI*2/1.2f-1.05f)*sign*11*pace,0,0);
                }
                var q=new Quaternion(d.gunRotation[0],d.gunRotation[1],d.gunRotation[2],d.gunRotation[3]);
                var grip=new Vector3(fps?.48f:.58f,fps?-.24f:-.25f,fps?.57f:.66f);grip.z-=recoil*.045f;grip.y+=Mathf.Sin(t*Mathf.PI*2/2.4f)*.004f;
                if(clip=="Reload")grip.y-=Mathf.Sin(Mathf.PI*S(0,2.4f,t))*.055f;
                Arm(r,"L",grip,new Vector3(.9f,-.4f,.15f),Quaternion.Euler(-recoil*5,0,0)*Quaternion.Inverse(q));
                Arm(r,"R",new Vector3(fps?-.42f:-.53f,-.36f,fps?.50f:.42f),new Vector3(-.9f,-.4f,.1f),Quaternion.Euler(35,-20,-10));
                Shape(r,"ProneSurfaceNormals",1);
                return;
            }
            body.localPosition+=new Vector3(0,Mathf.Sin(t*Mathf.PI*2/2.4f)*.012f,0);
            if(d.kind=="Fish")
            {
                float amplitude=idle?.25f:1;Shape(r,"Swim_Left",Mathf.Max(0,wave)*amplitude);Shape(r,"Swim_Right",Mathf.Max(0,-wave)*amplitude);Shape(r,"Fin_Up",Mathf.Max(0,wave)*.65f);Shape(r,"Fin_Down",Mathf.Max(0,-wave)*.65f);
                body.localRotation=Quaternion.Euler(0,wave*(idle?1:2.5f),Mathf.Cos(t*5.236f)*1.2f);
                if(eat)
                {
                    // R18 greedy gulp, locked to AquariumFeeding: the maw snaps wide during the
                    // lunge (0-.22 s), holds through the violent suction (.30-.88 s), slams shut
                    // with a gulp, then three greedy chomps with swallowing bulges; settles by 2.4 s.
                    float gape=S(0,.14f,t)*(1-S(.86f,.95f,t));
                    float chew=t>1.0f?Mathf.Max(0,Mathf.Sin((t-1.0f)*Mathf.PI*2/.25f))*(1-S(1.50f,1.75f,t))*.55f:0;
                    Shape(r,"Alien_Mouth_Open",Mathf.Max(gape,chew));
                    float gulp=Mathf.Min(1,S(.80f,.96f,t)*(1-S(1.02f,1.22f,t))+.8f*Bump(t,1.18f,1.45f)+.6f*Bump(t,1.45f,1.72f)+.4f*Bump(t,1.70f,2.0f));
                    Shape(r,"Swallow",gulp);
                    // Frantic tail and fins while feeding, easing back into the swim.
                    float beat=Mathf.Sin(t*Mathf.PI*2/.42f),flap=Mathf.Sin(t*Mathf.PI*2/.30f),settle=S(1.9f,2.4f,t);
                    Shape(r,"Swim_Left",Mathf.Lerp(Mathf.Max(0,beat)*.8f,Mathf.Max(0,wave),settle));Shape(r,"Swim_Right",Mathf.Lerp(Mathf.Max(0,-beat)*.8f,Mathf.Max(0,-wave),settle));
                    Shape(r,"Fin_Up",Mathf.Lerp(Mathf.Max(0,flap),Mathf.Max(0,wave)*.65f,settle));Shape(r,"Fin_Down",Mathf.Lerp(Mathf.Max(0,-flap),Mathf.Max(0,-wave)*.65f,settle));
                    // Stretch into the lunge, squash on impact and on the slam, swell with each gulp.
                    float lunge=Bump(t,0,.24f),impact=Bump(t,.20f,.42f),slam=Bump(t,.86f,1.02f),swell=.06f*gulp;
                    body.localScale=new Vector3(1-.05f*lunge+.05f*impact+.05f*slam+swell,1-.05f*lunge+.04f*impact+.04f*slam+swell*.8f,1+.12f*lunge-.08f*impact-.07f*slam);
                    // No yaw while the meal is aimed; nod with each chomp afterwards.
                    float nod=t>1.0f?Mathf.Sin((t-1.0f)*Mathf.PI*2/.25f)*(1-S(1.5f,1.8f,t))*5:0;
                    body.localRotation=Quaternion.Slerp(Quaternion.Euler(nod-4*slam,0,0),Quaternion.Euler(0,wave*2.5f,Mathf.Cos(t*5.236f)*1.2f),settle);
                }
            }
            else
            {
                if(clip=="Eaten")
                {
                    // A live captured alien braces, folds its arms behind its
                    // entering head, and squeezes its eye shut during the pull.
                    float fold=S(.16f,.72f,t),struggle=S(.04f,.20f,t)*(1-S(.82f,1.30f,t));
                    for(int k=1;k<=8;k++)Shape(r,"Captured_"+(k*125).ToString("0000"),Mathf.Max(0,1-Mathf.Abs(fold*8-k)));
                    Shape(r,"CapturedCrest",S(.24f,.68f,t));
                    Blink(r,S(.58f,1.10f,t)*.92f);
                    body.localRotation=Quaternion.Euler(Mathf.Sin(t*19)*3*struggle,Mathf.Sin(t*16)*4*struggle,Mathf.Sin(t*23)*6*struggle);
                    body.localScale=new Vector3(1-.04f*struggle,1-.02f*struggle,1+.04f*struggle);
                    return;
                }
                float blink=S(.78f,.91f,Mathf.Repeat(t,2.4f))*(1-S(.96f,1.15f,Mathf.Repeat(t,2.4f)));Blink(r,blink);
                float reach=eat?S(.08f,.55f,t)*(1-S(2.0f,2.75f,t)):0;
                float spit=clip=="SpitRecoil"?S(0,.10f,t)*(1-S(.70f,1.2f,t)):0;
                float expelled=clip=="SpitOut"?1-S(.42f,.85f,t):0;
                bool streamlined=clip=="Swim" || clip=="SpitOut";
                float gather=S(.10f,.82f,.5f-.5f*Mathf.Cos(t*Mathf.PI*2/2.4f));
                body.localRotation=Quaternion.Euler(-58*spit-6*gather*(idle?.1f:1),0,Mathf.Sin(t*2.618f)*1.2f*(1-spit));
                if(spit>0)body.localScale=new Vector3(1+.08f*spit,1-.07f*spit,1-.035f*spit);
                Shape(r,"BellyOpen",Mathf.Max(eat?S(.45f,.90f,t)*(1-S(1.98f,2.65f,t)):0,spit));
                if(eat)
                {
                    float at=Mathf.Clamp(t/3,0,1)*96;int key=Mathf.Min(95,Mathf.FloorToInt(at));float blend=at-key;
                    Shape(r,"Grasp_"+key.ToString("00"),1-blend);Shape(r,"Grasp_"+(key+1).ToString("00"),blend);
                }
                if(idle)
                {
                    float idlePhase=Mathf.Repeat(t/2.4f,1)*16;int idleKey=Mathf.FloorToInt(idlePhase);float f=idlePhase-idleKey;
                    Shape(r,"IdleWave_"+idleKey.ToString("00"),1-f);Shape(r,"IdleWave_"+((idleKey+1)%16).ToString("00"),f);
                }
                if(clip=="SpitRecoil")for(int k=1;k<=8;k++)Shape(r,"SpitFan_"+(k*125).ToString("0000"),Mathf.Max(0,1-Mathf.Abs(spit*8-k)));
                if(streamlined)
                {
                    Shape(r,"Streamline_1000",1);
                    // A full cycle of section poses carries the bend from root to tip.
                    float phase=Mathf.Repeat(t/2.4f,1)*16;int key=Mathf.FloorToInt(phase);float blend=phase-key;
                    Shape(r,"StreamWave_"+key.ToString("00"),1-blend);
                    Shape(r,"StreamWave_"+((key+1)%16).ToString("00"),blend);
                    body.localRotation=Quaternion.Euler(-2+Mathf.Sin(t*2.618f)*1.2f,0,Mathf.Sin(t*2.618f)*.6f);
                }
                for(int i=0;i<6;i++)for(int j=0;j<3;j++)
                {
                    if(streamlined)continue;
                    var b=r.Bones[2+i*3+j];var root=V(d.bones[2+i*3].world);var rest=V(d.bones[3+i*3].world)-root;
                    float pulse=S(.10f,.82f,.5f-.5f*Mathf.Cos((t-j*.10f)*Mathf.PI*2/2.4f));
                    float sweep=Mathf.Lerp(idle?.08f:.92f,0,Mathf.Max(reach,spit))*pulse;
                    sweep=Mathf.Lerp(sweep,.94f,expelled);
                    float lane=(i%3==0?.38f:i%3==1?.60f:.23f)*Mathf.Sign(root.x);
                    var trailing=new Vector3(lane,-.16f+Mathf.Sin(t*2.618f-j*.55f)*.07f,-1).normalized;
                    var direction=Vector3.Lerp(rest.normalized,trailing,sweep).normalized;
                    // The sketched stroke opens like a fan, then gathers behind
                    // the face. Delayed distal joints carry the bend to the tips.
                    direction=Vector3.Lerp(direction,new Vector3(rest.x,-.05f,rest.z).normalized,spit*.7f);
                    var curlAxis=new Vector3(-root.z,0,root.x).normalized;
                    direction=Quaternion.AngleAxis(reach*(j==0?19:j==1?25:15),curlAxis)*direction;
                    b.rotation=body.rotation*Quaternion.FromToRotation(rest,direction);
                }
            }
            if(clip=="SpitRecoil" && d.kind=="Fish")
            {
                // R18: the swallowed ally is forced out of the wide maw. Heave, gape, recoil.
                float k=(1-S(0,1.1f,t)),heave=Bump(t,0,.16f);
                body.localRotation*=Quaternion.Euler(-14*k,0,6*k);body.localScale=new Vector3(1+.035f*k+.10f*heave,1-.02f*k+.08f*heave,1-.04f*k-.06f*heave);
                Shape(r,"Alien_Mouth_Open",S(0,.07f,t)*(1-S(.50f,.80f,t)));Shape(r,"Swallow",S(0,.04f,t)*(1-S(.10f,.28f,t)));
            }
            if(dead){float settle=S(.25f,2.1f,t);body.localRotation=Quaternion.Euler(0,0,clip=="Harpoon"?180*settle:12*wave*(1-settle));for(int i=0;i<r.skin.sharedMesh.blendShapeCount;i++)r.skin.SetBlendShapeWeight(i,r.skin.GetBlendShapeWeight(i)*(1-settle));}
            if(clip=="Morph"||clip=="Unmorph")r.SetCartoonFold(clip=="Morph"?S(0,1.4f,t):1-S(0,1.4f,t));
        }
        static AnimationClip CreateClip(AquariumRig rig,Data d,string name,string folder)
        {
            bool loop=name=="Idle"||name=="Swim"||name=="FPSIdle";float duration=loop?2.4f:name=="Fire"||name=="FPSFire"?.6f:name=="Morph"||name=="Unmorph"?1.4f:name=="SpitRecoil"?1.2f:name=="SpitOut"?.85f:3;
            int frames=Mathf.RoundToInt(duration*30)+1;var values=new float[frames,rig.Bones.Length,10];var shapes=new float[frames,d.shapeNames.Length];
            for(int f=0;f<frames;f++)
            {
                Pose(rig,d,name,f/30f);
                for(int b=0;b<rig.Bones.Length;b++){var x=rig.Bones[b];var p=x.localPosition;var q=x.localRotation;var s=x.localScale;float[] a={p.x,p.y,p.z,q.x,q.y,q.z,q.w,s.x,s.y,s.z};for(int k=0;k<10;k++)values[f,b,k]=a[k];}
                for(int k=0;k<d.shapeNames.Length;k++)shapes[f,k]=rig.skin.GetBlendShapeWeight(k);
            }
            var clip=new AnimationClip{name=name,legacy=true,frameRate=30,wrapMode=loop?WrapMode.Loop:WrapMode.ClampForever};
            for(int b=0;b<rig.Bones.Length;b++)for(int field=0;field<10;field++)
            {
                var keys=new Keyframe[frames];for(int f=0;f<frames;f++)keys[f]=new Keyframe(f/30f,values[f,b,field]);
                string property=field<3?"m_LocalPosition."+"xyz"[field]:field<7?"m_LocalRotation."+"xyzw"[field-3]:"m_LocalScale."+"xyz"[field-7];
                clip.SetCurve(AnimationUtility.CalculateTransformPath(rig.Bones[b],rig.transform),typeof(Transform),property,new AnimationCurve(keys));
            }
            for(int k=0;k<d.shapeNames.Length;k++){var keys=new Keyframe[frames];for(int f=0;f<frames;f++)keys[f]=new Keyframe(f/30f,shapes[f,k]);clip.SetCurve("Deforming surface",typeof(SkinnedMeshRenderer),"blendShape."+d.shapeNames[k],new AnimationCurve(keys));}
            clip.EnsureQuaternionContinuity();return Save(clip,folder+"/"+d.name+"_"+name+".anim");
        }
        static void Gun(string name)
        {
            var d=Load(name);var g=new GameObject(name);var component=g.AddComponent<AquariumHarpoonGun>();var skin=Child("Cartoon barrel",g.transform,Vector3.zero);skin.gameObject.AddComponent<MeshFilter>().sharedMesh=Read(d,Equipment,null,null);skin.gameObject.AddComponent<MeshRenderer>().sharedMaterials=Materials(d,Equipment,false);
            component.muzzle=Child("Muzzle",g.transform,V(d.muzzle));component.ropeExit=Child("Rope exit",g.transform,V(d.ropeExit));component.headLength=d.headLength;
            var h=Load("HarpoonHead");var mesh=Read(h,Equipment,null,null);var materials=Materials(h,Equipment,false);var head=Child("Loaded spear",g.transform,V(d.muzzle));head.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;head.gameObject.AddComponent<MeshRenderer>().sharedMaterials=materials;component.loadedHarpoon=head;
            PrefabUtility.SaveAsPrefabAsset(g,Equipment+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(g);
            var projectile=new GameObject("Cartoon tethered spear");projectile.AddComponent<MeshFilter>().sharedMesh=mesh;projectile.AddComponent<MeshRenderer>().sharedMaterials=materials;PrefabUtility.SaveAsPrefabAsset(projectile,Equipment+"/HarpoonHead.prefab");UnityEngine.Object.DestroyImmediate(projectile);
        }
        [MenuItem("Aquarium/Characters/Import approved cartoon models")]
        public static void Run()
        {
            if(string.IsNullOrEmpty(Source))throw new Exception("Set AQUARIUM_CARTOON_SOURCE");
            foreach(string name in new[]{"Alien","Fish0","Diver","DiverHands"}){var rig=Rig(Load(name),name.StartsWith("Diver")?Equipment:Characters);UnityEngine.Object.DestroyImmediate(rig.gameObject);}
            Gun("HarpoonGun");var fish=AssetDatabase.LoadAssetAtPath<GameObject>(Characters+"/Fish0.prefab");
            foreach(var species in Resources.LoadAll<AquariumSpecies>("AquariumCharacters")){species.normal=fish;species.feeding=fish;species.closedFeeding=fish;species.feedingMouthRadius=.21f;species.tint=species.speciesId==0?Color.white:species.speciesId==1?new Color(.91f,.96f,1):new Color(1,.94f,.98f);EditorUtility.SetDirty(species);}
            AssetDatabase.SaveAssets();RemoveObsolete();AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            // Hand-keyed octopus Swim/TentacleEat/Bite replace the generated alien slots (see AlienOctopusAnimation).
            AlienOctopusAnimation.Apply();DiverHarpoon.Apply();Debug.Log("CARTOON_IMPORT_OK");
        }
        static void RemoveObsolete()
        {
            var removed=new List<string>();
            foreach(string folder in new[]{Characters,Equipment})foreach(string path in Directory.GetFiles(folder))
            {
                string n=Path.GetFileName(path);if(n.EndsWith(".meta"))continue;
                bool old=folder==Characters?(n.StartsWith("Fish0Mouth")||n.StartsWith("Fish0Closed")||n.Contains("_Envelope")||n.EndsWith("_BaseColor.png")||n.EndsWith("_Normal.png")||n.EndsWith("_RM.png")||n=="Fish0.mat"||n=="Alien.mat"||n=="Metamorphosis.mat"):(n.Contains("_CleanBase")||n.Contains("_CleanSurface")||(n.EndsWith(".mat")&&(n.StartsWith("Diver_0")||n.StartsWith("Diver_1")||n.StartsWith("HarpoonGun_0"))));
                if(old){removed.Add(path.Replace('\\','/'));AssetDatabase.DeleteAsset(path);}
            }
            File.WriteAllText(Path.Combine(Source,"removed-assets.json"),JsonUtility.ToJson(new Removed{paths=removed.ToArray()},true));
        }
        [Serializable]class Removed{public string[] paths;}
    }
}

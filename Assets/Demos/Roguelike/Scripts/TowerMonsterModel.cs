using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>Continuous four-weight skin over original creature pixels; one draw call per creature.</summary>
    internal sealed class TowerMonsterModel : IDisposable
    {
        struct Binding { public int a,b,c,d; public float wa,wb,wc,wd; }
        static readonly Dictionary<Texture2D,Material> Materials=new Dictionary<Texture2D,Material>();
        readonly Mesh mesh;
        readonly GameObject root;
        readonly MeshRenderer renderer;
        readonly Vector3[] rest,posed;
        readonly Binding[] bindings;
        readonly MaterialPropertyBlock tintBlock=new MaterialPropertyBlock();
        readonly int[] triangles;
        readonly int sourceFacing;
        public Renderer Renderer { get { return renderer; } }
        public float RenderedHeight { get { return mesh.bounds.max.y; } }
        public int WeightedBoneCount { get; private set; }

        public TowerMonsterModel(Transform parent,Sprite source,TowerMonsterRig.Anatomy anatomy,float width,float height)
        {
            sourceFacing=anatomy.facesLeft?-1:1;
            // A shared grid keeps adjacent regions watertight, unlike slicing an entire
            // limb out of a single view and exposing unpainted pixels at its shoulder.
            const int rows=32;
            int columns=Mathf.Clamp(Mathf.CeilToInt(rows*width/height),20,64),count=(columns+1)*(rows+1);
            rest=new Vector3[count];posed=new Vector3[count];bindings=new Binding[count];
            var uv=new Vector2[count];var colors=new Color32[count];var used=new bool[(int)TowerMonsterRig.Joint.Count];
            Rect textureRect=source.rect;float tw=source.texture.width,th=source.texture.height;
            float[] weights=new float[(int)TowerMonsterRig.Joint.Count];
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {
                int i=y*(columns+1)+x;float u=x/(float)columns,v=y/(float)rows;
                rest[i]=new Vector3((u-.5f)*width,v*height,0);posed[i]=rest[i];
                uv[i]=new Vector2((textureRect.x+u*textureRect.width)/tw,(textureRect.y+v*textureRect.height)/th);
                colors[i]=new Color32(255,255,255,255);
                Weights(anatomy,u,v,weights);bindings[i]=Bind(weights,used);
            }
            foreach(bool value in used)if(value)WeightedBoneCount++;
            triangles=new int[columns*rows*6];int at=0;
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
            {
                int i=y*(columns+1)+x;
                triangles[at++]=i;triangles[at++]=i+columns+1;triangles[at++]=i+1;
                triangles[at++]=i+1;triangles[at++]=i+columns+1;triangles[at++]=i+columns+2;
            }
            root=new GameObject("Weighted creature pixels");root.transform.SetParent(parent,false);
            mesh=new Mesh{name=source.name+" four-weight skin"};mesh.MarkDynamic();
            mesh.vertices=rest;mesh.uv=uv;mesh.colors32=colors;mesh.triangles=triangles;mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=root.AddComponent<MeshRenderer>();renderer.sortingOrder=30;
            Material material;
            if(!Materials.TryGetValue(source.texture,out material))
            {material=new Material(Shader.Find("Sprites/Default")){name=source.name+" original pixels"};material.mainTexture=source.texture;Materials.Add(source.texture,material);}
            renderer.sharedMaterial=material;
        }
        static Binding Bind(float[] weights,bool[] used)
        {
            int a=Take(weights),b=Take(weights),c=Take(weights),d=Take(weights);
            float sum=weights[a]+weights[b]+weights[c]+weights[d];
            // Take marks selected values negative; dividing by their negative sum
            // restores positive, normalized weights.
            var binding=new Binding{a=a,b=b,c=c,d=d,wa=weights[a]/sum,wb=weights[b]/sum,wc=weights[c]/sum,wd=weights[d]/sum};
            if(binding.wa>.01f)used[a]=true;if(binding.wb>.01f)used[b]=true;if(binding.wc>.01f)used[c]=true;if(binding.wd>.01f)used[d]=true;
            return binding;
        }
        static int Take(float[] weights)
        {int best=-1;for(int i=0;i<weights.Length;i++)if(weights[i]>=0&&(best<0||weights[i]>weights[best]))best=i;weights[best]=-Mathf.Max(.000001f,weights[best]);return best;}
        static float Smooth(float a,float b,float v) { return Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,v)); }
        static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        {Vector2 d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.0001f,d.sqrMagnitude)));}
        static void Weights(TowerMonsterRig.Anatomy profile,float u,float v,float[] weights)
        {
            Array.Clear(weights,0,weights.Length);
            var form=profile.form;
            float low=1-Smooth(.18f,.48f,v),left=1-Smooth(.36f,.60f,u),right=1-left;
            weights[(int)TowerMonsterRig.Joint.Root]=.14f;
            weights[(int)TowerMonsterRig.Joint.Spine]=.62f;
            if(form==TowerMonsterRig.Form.Slime)
            {
                weights[0]=1-Smooth(.05f,.36f,v);
                weights[1]=Smooth(.08f,.44f,v)*.7f;
                weights[3]=(1-Smooth(.15f,.43f,u))*(1-Smooth(.24f,.67f,v))*.8f;
                weights[5]=Smooth(.57f,.90f,u)*(1-Smooth(.24f,.67f,v))*.8f;
                weights[11]=Smooth(.65f,.95f,v)*.6f;
            }
            else if(form==TowerMonsterRig.Form.Tentacle||form==TowerMonsterRig.Form.Spirit)
            {
                weights[1]=Smooth(.25f,.65f,v)*.9f+.08f;
                weights[7]=low*left;weights[9]=low*right;
                weights[3]=(1-Smooth(.15f,.36f,u))*(1-Smooth(.50f,.74f,v));
                weights[5]=Smooth(.70f,.90f,u)*(1-Smooth(.45f,.72f,v));
                weights[13]=(1-Smooth(.12f,.38f,v))*.35f;
                weights[12]=Smooth(.68f,.95f,u)*(1-Smooth(.25f,.48f,v))*.5f;
            }
            else
            {
                bool lowBody=form==TowerMonsterRig.Form.Quadruped||form==TowerMonsterRig.Form.Crawler;
                float legTop=lowBody?.48f:.38f;
                low=1-Smooth(legTop-.18f,legTop+.05f,v);
                float foot=1-Smooth(.05f,.21f,v);
                weights[1]=.25f+.55f*(1-low);
                weights[7]=low*left*(1-foot*.65f);weights[8]=low*left*foot;
                weights[9]=low*right*(1-foot*.65f);weights[10]=low*right*foot;
                float arms=Smooth(.13f,.30f,v)*(1-Smooth(.60f,.78f,v));
                weights[3]=(1-Smooth(.20f,.39f,u))*arms*.9f;
                weights[4]=(1-Smooth(.18f,.34f,u))*arms*(1-Smooth(.31f,.51f,v))*.4f;
                weights[5]=Smooth(.60f,.83f,u)*arms*.9f;
                weights[6]=Smooth(.69f,.88f,u)*arms*(1-Smooth(.31f,.51f,v))*.4f;
                weights[13]=(1-Smooth(.10f,.44f,v))*(1-Mathf.Abs(u-.5f)*1.4f)*(form==TowerMonsterRig.Form.Robed?2:.2f);
                if(form==TowerMonsterRig.Form.Winged)
                {
                    float wing=Smooth(profile.wing.x-.13f,profile.wing.x+.18f,u)*Smooth(.30f,.60f,v);
                    weights[11]=wing*3;weights[12]=wing*Smooth(.69f,.98f,u)*1.1f;
                }
                else if(lowBody)
                {
                    float tail=Smooth(.65f,.95f,u)*Smooth(.28f,.50f,v);
                    weights[11]=tail*.9f;weights[12]=tail*Smooth(.78f,1,u);
                }
            }
            // The center of the face is wholly rigid; the neck transition blends to
            // the chest. This protects eyes, teeth and crowns from rubber-sheet motion.
            Vector2 delta=new Vector2((u-profile.head.x)/profile.headRadii.x,(v-profile.head.y)/profile.headRadii.y);
            float face=1-Smooth(.45f,1.65f,delta.magnitude);
            for(int i=0;i<weights.Length;i++)weights[i]*=1-face;
            weights[2]+=face;
            if(profile.hasProp)
            {
                float distance=SegmentDistance(new Vector2(u,v),profile.propStart,profile.propEnd);
                float prop=1-Smooth(profile.propRadius*.5f,profile.propRadius*1.8f,distance);
                for(int i=0;i<weights.Length;i++)weights[i]*=1-prop;
                weights[4]+=prop;
            }
        }
        public void Pose(Matrix4x4[] bones,Color tint,int facing)
        {
            int mirror=facing<0?-sourceFacing:sourceFacing;float minY=float.PositiveInfinity;
            for(int i=0;i<rest.Length;i++)
            {
                var b=bindings[i];Vector3 p=bones[b.a].MultiplyPoint3x4(rest[i])*b.wa+
                    bones[b.b].MultiplyPoint3x4(rest[i])*b.wb+bones[b.c].MultiplyPoint3x4(rest[i])*b.wc+
                    bones[b.d].MultiplyPoint3x4(rest[i])*b.wd;
                p.x*=mirror;posed[i]=p;minY=Mathf.Min(minY,p.y);
            }
            // Feet never sink through a one-way platform, including the death settle.
            if(minY<0)for(int i=0;i<posed.Length;i++)posed[i].y-=minY;
            mesh.vertices=posed;mesh.RecalculateBounds();tintBlock.SetColor("_Color",tint);renderer.SetPropertyBlock(tintBlock);
        }
        public Vector3[] CaptureVertices(){return (Vector3[])posed.Clone();}
        public Vector3[] CaptureRestVertices(){return (Vector3[])rest.Clone();}
        public int[] CaptureTriangles(){return (int[])triangles.Clone();}
        public void Dispose(){if(root!=null){root.SetActive(false);UnityEngine.Object.Destroy(root);}if(mesh!=null)UnityEngine.Object.Destroy(mesh);}
    }
}

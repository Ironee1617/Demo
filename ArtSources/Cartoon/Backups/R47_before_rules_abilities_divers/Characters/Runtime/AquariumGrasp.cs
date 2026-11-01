using System;
using UnityEngine;
namespace PrototypeLab.Aquarium
{
    // Contact follows the same moving prey surface as the ingestion shader.
    // Bones retain their lengths; each limb has its own corridor around the meal.
    public sealed class AquariumGrasp
    {
        readonly AquariumRig rig;
        readonly int[][] chains=new int[5][];
        readonly float[][] lengths=new float[5][];
        readonly Vector3[] path=new Vector3[65];
        readonly float[] distances=new float[65];
        public AquariumGrasp(AquariumRig source)
        {
            rig=source;string[] names={"ArmL","ArmR","FootL","FootR","Tail"};
            for(int c=0;c<5;c++)
            {
                int count=c==2||c==3?6:8;chains[c]=new int[count];lengths[c]=new float[count];
                for(int j=0;j<count;j++)
                {
                    int k=Array.FindIndex(rig.Bones,b=>b.name==names[c]+j);chains[c][j]=k;
                    int next=Array.FindIndex(rig.Bones,b=>b.name==names[c]+(j+1));
                    if(next>=0)lengths[c][j]=rig.restPositions[next].magnitude;
                    else if(j>0)lengths[c][j]=lengths[c][j-1];
                }
            }
        }
        static float S(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
        {float u=1-t;return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;}
        public void Present(float age,Vector3 worldAxis,float mealRadius,float contactDistance)
        {
            float reach=S(.15f,.72f,age)*(1-S(1.72f,2.65f,age));
            float curl=S(.52f,1.08f,age),pull=S(1.03f,1.62f,age);
            float scale=Mathf.Max(.001f,rig.transform.lossyScale.x);
            float radius=Mathf.Clamp(mealRadius/scale,.11f,.25f);
            float front=Mathf.Clamp(contactDistance/scale,.26f,.45f);
            Vector3 axis=rig.transform.InverseTransformDirection(worldAxis);
            Quaternion aim=Quaternion.FromToRotation(Vector3.forward,Vector3.RotateTowards(Vector3.forward,axis,.55f,0));
            for(int c=0;c<5;c++)
            {
                var ids=chains[c];if(ids[0]<0)continue;
                for(int j=0;j<ids.Length;j++)
                {
                    int k=ids[j];var bone=rig.Bones[k];bone.localPosition=rig.restPositions[k];bone.localRotation=rig.restRotations[k];bone.localScale=rig.restScales[k];
                }
                // At zero reach the final blend leaves every joint at rest.
                if(reach<=0)continue;
                int sign=c==0||c==2?-1:1,start=c==4?2:1;var root=rig.Bones[ids[start]];
                Vector3 a=rig.transform.InverseTransformPoint(root.position),b,d,e;
                if(c<2)
                {
                    float side=radius+.095f,grip=Mathf.Lerp(front+.12f,.34f,pull);
                    b=new Vector3(sign*.43f,-.24f,.19f);
                    d=new Vector3(sign*(side+.07f),-.12f,grip+.08f);
                    e=new Vector3(sign*Mathf.Lerp(side+.06f,.15f,curl),0,grip+.10f);
                }
                else if(c<4)
                {
                    b=new Vector3(sign*.25f,-.39f,.10f);
                    d=new Vector3(sign*.25f,-.38f,Mathf.Lerp(.34f,.28f,pull));
                    e=new Vector3(sign*.18f,-.29f,Mathf.Lerp(.36f,.28f,pull));
                }
                else
                {
                    b=new Vector3(0,-.55f,-.12f);
                    d=new Vector3(0,-.64f,.18f);
                    e=new Vector3(0,-.48f,Mathf.Lerp(.34f,.26f,pull));
                }
                // Rotate only the fore-reaching part. Root collars stay attached.
                Vector3 pivot=new Vector3(0,-.145f,.19f);
                b=Vector3.Lerp(b,pivot+aim*(b-pivot),c<2?.75f:.3f);
                d=pivot+aim*(d-pivot);e=pivot+aim*(e-pivot);
                float required=0;for(int j=start;j<ids.Length;j++)required+=lengths[c][j];
                float factor=1;
                for(int attempt=0;attempt<12;attempt++)
                {
                    Vector3 m1=Vector3.Lerp(a,e,1f/3),m2=Vector3.Lerp(a,e,2f/3);
                    Vector3 bb=m1+(b-m1)*factor,dd=m2+(d-m2)*factor;
                    distances[0]=0;path[0]=a;
                    for(int j=1;j<=64;j++){path[j]=Bezier(a,bb,dd,e,j/64f);distances[j]=distances[j-1]+Vector3.Distance(path[j],path[j-1]);}
                    if(distances[64]>=required*1.04f)break;factor*=1.15f;
                }
                float along=0;int segment=1;
                for(int j=start;j<ids.Length;j++)
                {
                    var bone=rig.Bones[ids[j]];
                    along+=lengths[c][j];while(segment<64 && distances[segment]<along)segment++;
                    Vector3 target=rig.transform.TransformPoint(Vector3.Lerp(path[segment-1],path[segment],Mathf.InverseLerp(distances[segment-1],distances[segment],along)));
                    Vector3 delta=target-bone.position;if(delta.sqrMagnitude<.000001f)continue;
                    Quaternion turn=Quaternion.FromToRotation(bone.TransformDirection(Vector3.back),delta.normalized);
                    bone.rotation=turn*bone.rotation;
                }
                // Blend local joint rotations after solving the complete curve.
                // Blending each world-space aim first can reverse the last joints.
                float strength=reach*(c==2||c==3?.45f:c==4?.65f:1);
                for(int j=start;j<ids.Length;j++){int k=ids[j];var bone=rig.Bones[k];bone.localRotation=Quaternion.Slerp(rig.restRotations[k],bone.localRotation,strength);}
            }
        }
    }
}

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumHotPathChecks
    {
        private sealed class Pose
        {
            public Vector3[] positions,scales;
            public Quaternion[] rotations;
            public Pose(AquariumRig rig)
            {
                positions=new Vector3[rig.Bones.Length];scales=new Vector3[rig.Bones.Length];rotations=new Quaternion[rig.Bones.Length];
                Save(rig);
            }
            public void Save(AquariumRig rig)
            {
                for(int i=0;i<positions.Length;i++)
                {var bone=rig.Bones[i];positions[i]=bone.localPosition;rotations[i]=bone.localRotation;scales[i]=bone.localScale;}
            }
            public void Restore(AquariumRig rig)
            {
                for(int i=0;i<positions.Length;i++)
                {var bone=rig.Bones[i];bone.localPosition=positions[i];bone.localRotation=rotations[i];bone.localScale=scales[i];}
            }
            public void Compare(AquariumRig rig,float age,ref float maximumPosition,ref float maximumRotation)
            {
                for(int i=0;i<positions.Length;i++)
                {
                    var bone=rig.Bones[i];float position=Vector3.Distance(positions[i],bone.localPosition),rotation=Quaternion.Angle(rotations[i],bone.localRotation);
                    var a=rotations[i];var b=bone.localRotation;var qa=new Vector4(a.x,a.y,a.z,a.w);var qb=new Vector4(b.x,b.y,b.z,b.w);
                    float rotationDifference=Mathf.Min((qa-qb).sqrMagnitude,(qa+qb).sqrMagnitude);
                    maximumPosition=Mathf.Max(maximumPosition,position);maximumRotation=Mathf.Max(maximumRotation,rotation);
                    if(position>.00001f || rotationDifference>.0000000001f || Vector3.Distance(scales[i],bone.localScale)>.00001f)
                        throw new Exception("Grasp differs from 3e12b65 at age="+age+" bone="+bone.name+" position="+position+" angle="+rotation);
                }
            }
        }

        public static void Run()
        {
            string output=Environment.GetEnvironmentVariable("AQUARIUM_HOTPATH_QA");if(string.IsNullOrEmpty(output))output="Artifacts/HotPathQA";
            Directory.CreateDirectory(output);var result=new StringBuilder("baseline=3e12b65\nallocation_measurement=unavailable_in_this_Unity_Mono_process\n");
            var root=new GameObject("Grasp hot path QA");
            try
            {
                var prefab=Resources.Load<GameObject>("AquariumCharacters/Alien");if(prefab==null)throw new Exception("Missing Alien rig");
                var rig=UnityEngine.Object.Instantiate(prefab,root.transform).GetComponent<AquariumRig>();
                rig.SamplePose("TentacleEat",.72f,.72f);
                var starting=new Pose(rig);var expected=new Pose(rig);var baseline=new BaselineGrasp(rig);var current=new AquariumGrasp(rig);
                int samples=0;float maximumPosition=0,maximumRotation=0;
                foreach(float size in new[]{.4f,1f,3.5f})foreach(var axis in new[]{Vector3.forward,new Vector3(.5f,.3f,1).normalized,Vector3.back})
                {
                    root.transform.SetPositionAndRotation(new Vector3(2,-1,3),Quaternion.Euler(12,37,-8));root.transform.localScale=Vector3.one*size;
                    for(int frame=-6;frame<=210;frame++)
                    {
                        float age=frame/60f;
                        starting.Restore(rig);baseline.Present(age,axis,.18f*size,.36f*size);expected.Save(rig);
                        starting.Restore(rig);current.Present(age,axis,.18f*size,.36f*size);
                        expected.Compare(rig,age,ref maximumPosition,ref maximumRotation);samples++;
                    }
                }
                result.AppendLine("grasp_equivalence_samples="+samples+" max_position="+Number(maximumPosition)+" max_rotation_degrees="+Number(maximumRotation));
                root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=Vector3.one;
                foreach(string scenario in new[]{"start","active","settled","full_action"})
                {
                    double[] before=new double[5],after=new double[5];
                    for(int round=0;round<5;round++)
                    {
                        if(round%2==0){before[round]=Measure(baseline.Present,scenario);after[round]=Measure(current.Present,scenario);}
                        else{after[round]=Measure(current.Present,scenario);before[round]=Measure(baseline.Present,scenario);}
                    }
                    Array.Sort(before);Array.Sort(after);
                    result.AppendLine("grasp_"+scenario+" baseline_us="+Number(before[2])+" current_us="+Number(after[2]));
                }
                result.AppendLine("OK");File.WriteAllText(Path.Combine(output,"hotpath-checks.txt"),result.ToString());
            }
            catch(Exception ex){result.AppendLine("FAIL "+ex);File.WriteAllText(Path.Combine(output,"hotpath-checks.txt"),result.ToString());throw;}
            finally{UnityEngine.Object.DestroyImmediate(root);}
            UnityEngine.Debug.Log("AQUARIUM_HOTPATH_CHECKS_OK "+result);
        }
        private static string Number(double value)=>value.ToString("F5",CultureInfo.InvariantCulture);
        private static float Age(string scenario,int index)
        {return scenario=="start"?.1f:scenario=="active"?.9f:scenario=="settled"?2.9f:(index%217-6)/60f;}
        private static double Measure(Action<float,Vector3,float,float> present,string scenario)
        {
            for(int i=0;i<60;i++)present(Age(scenario,i),Vector3.forward,.18f,.36f);
            const int count=1800;var timer=new Stopwatch();timer.Start();
            for(int i=0;i<count;i++)present(Age(scenario,i),Vector3.forward,.18f,.36f);
            timer.Stop();
            return timer.Elapsed.TotalMilliseconds*1000/count;
        }
    }
}

// Exact grasp implementation from 3e12b65 for same-process comparisons.


namespace PrototypeLab.Aquarium.Editor
{
    // Contact follows the same moving prey surface as the ingestion shader.
    // Bones retain their lengths; each limb has its own corridor around the meal.
    internal sealed class BaselineGrasp
    {
        readonly AquariumRig rig;
        readonly int[][] chains=new int[5][];
        readonly float[][] lengths=new float[5][];
        readonly Vector3[] path=new Vector3[65];
        readonly float[] distances=new float[65];
        public BaselineGrasp(AquariumRig source)
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

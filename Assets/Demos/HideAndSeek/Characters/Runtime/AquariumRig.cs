using UnityEngine;
namespace PrototypeLab.Aquarium
{
    public sealed class AquariumRig : MonoBehaviour
    {
        public SkinnedMeshRenderer skin;
        public Mesh envelope;
        public Mesh closedEnvelope;
        public Vector3 mouthAnchor=new Vector3(0,-.025f,.34f);
        public Vector3 throatAnchor=new Vector3(0,-.035f,.045f);
        public AnimationClip[] clips;
        public Vector3[] restPositions,restScales;
        public Quaternion[] restRotations;
        private Vector3[] blendPositions,blendScales;
        private Quaternion[] blendRotations;
        private float[] blendShapes;
        private string previousClip;
        private float blendStarted, locomotionAt=-1, locomotionPhase, locomotionSpeed;
        private Transform[] bones;
        private AnimationClip sampledClip;
        private Vector3[] idlePositions,idleScales;
        private Quaternion[] idleRotations;
        public Transform[] Bones=>bones??(bones=skin.bones);
        private Transform body;
        private Matrix4x4 bodyBindPose;
        private bool bodyCached;
        public Vector3 BodyPoint(Vector3 anchor)
        {
            if(!bodyCached)
            {
                bodyCached=true;
                for(int i=0;i<Bones.Length;i++)if(Bones[i].name=="Body"){body=Bones[i];bodyBindPose=skin.sharedMesh.bindposes[i];break;}
            }
            return body!=null?body.TransformPoint(bodyBindPose.MultiplyPoint3x4(anchor)):transform.TransformPoint(anchor);
        }
        public float LocomotionPhase=>locomotionPhase;
        public float LocomotionSpeed=>locomotionSpeed;
        public void SampleLocomotion(float moveSpeed,float now,float phase=0)
        {
            float dt=locomotionAt<0?0:Mathf.Clamp(now-locomotionAt,0,.1f);locomotionAt=now;
            locomotionSpeed=Mathf.Lerp(locomotionSpeed,moveSpeed,1-Mathf.Exp(-dt*10));
            locomotionPhase+=dt*Mathf.Lerp(.55f,1.65f,Mathf.Clamp01(locomotionSpeed/4.7f));
            if(idlePositions==null)
            {
                Clip("Idle").SampleAnimation(gameObject,0);
                idlePositions=new Vector3[Bones.Length];idleScales=new Vector3[Bones.Length];idleRotations=new Quaternion[Bones.Length];
                for(int i=0;i<Bones.Length;i++){idlePositions[i]=Bones[i].localPosition;idleScales[i]=Bones[i].localScale;idleRotations[i]=Bones[i].localRotation;}
            }
            SamplePose("Swim",locomotionPhase+phase,now);
            float weight=Mathf.Lerp(.12f,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.03f,1.5f,locomotionSpeed)));
            for(int i=0;i<Bones.Length;i++)
            {
                var b=Bones[i];b.localPosition=Vector3.Lerp(idlePositions[i],b.localPosition,weight);
                b.localRotation=Quaternion.Slerp(idleRotations[i],b.localRotation,weight);b.localScale=Vector3.Lerp(idleScales[i],b.localScale,weight);
            }
        }
        public AnimationClip Clip(string name) { foreach(var c in clips) if(c.name==name || c.name.EndsWith("_"+name))return c; throw new System.InvalidOperationException("Missing clip "+name); }
        public void SamplePose(string name,float time,float now)
        {
            if(blendPositions==null){blendPositions=new Vector3[Bones.Length];blendScales=new Vector3[Bones.Length];blendRotations=new Quaternion[Bones.Length];blendShapes=new float[skin.sharedMesh.blendShapeCount];}
            if(previousClip!=name)
            {
                blendStarted=previousClip==null?now-1:now;previousClip=name;sampledClip=Clip(name);
                for(int i=0;i<Bones.Length;i++){blendPositions[i]=Bones[i].localPosition;blendRotations[i]=Bones[i].localRotation;blendScales[i]=Bones[i].localScale;}
                for(int i=1;i<blendShapes.Length;i++)blendShapes[i]=skin.GetBlendShapeWeight(i);
            }
            var clip=sampledClip;clip.SampleAnimation(gameObject,clip.wrapMode==WrapMode.Loop?Mathf.Repeat(time,clip.length):Mathf.Min(time,clip.length));
            float blend=Mathf.SmoothStep(0,1,(now-blendStarted)/.18f);
            for(int i=0;i<Bones.Length && blend<1;i++)
            {var b=Bones[i];b.localPosition=Vector3.Lerp(blendPositions[i],b.localPosition,blend);b.localRotation=Quaternion.Slerp(blendRotations[i],b.localRotation,blend);b.localScale=Vector3.Lerp(blendScales[i],b.localScale,blend);}
            for(int i=1;i<blendShapes.Length && blend<1;i++)skin.SetBlendShapeWeight(i,Mathf.Lerp(blendShapes[i],skin.GetBlendShapeWeight(i),blend));
        }
        public void Neutralize(float weight)
        {
            if(weight<=0)return;
            for(int i=1;i<skin.sharedMesh.blendShapeCount;i++)skin.SetBlendShapeWeight(i,skin.GetBlendShapeWeight(i)*(1-weight));
            for(int i=0;i<Bones.Length;i++)
            {
                var b=Bones[i];b.localPosition=Vector3.Lerp(b.localPosition,restPositions[i],weight);
                b.localRotation=Quaternion.Slerp(b.localRotation,restRotations[i],weight);b.localScale=Vector3.Lerp(b.localScale,restScales[i],weight);
            }
        }
    }
}

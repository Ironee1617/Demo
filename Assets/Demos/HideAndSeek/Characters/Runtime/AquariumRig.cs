using UnityEngine;
namespace PrototypeLab.Aquarium
{
    public sealed class AquariumRig : MonoBehaviour
    {
        public SkinnedMeshRenderer skin;
        public bool cartoon;
        public string cartoonKind;
        public Transform gunSocket;
        public bool firstPerson;
        public float CartoonFold {get;private set;}
        private int[] foldKeys;
        public void SetCartoonFold(float value)
        {
            CartoonFold=Mathf.Clamp01(value);
            if(foldKeys==null){foldKeys=new int[8];for(int i=0;i<8;i++)foldKeys[i]=skin.sharedMesh.GetBlendShapeIndex("To_Matched_"+((i+1)*125).ToString("0000"));}
            Neutralize(CartoonFold);
            for(int i=0;i<8;i++)if(foldKeys[i]>=0)skin.SetBlendShapeWeight(foldKeys[i],100*(i==7?Mathf.Clamp01(8*CartoonFold-7):Mathf.Max(0,1-Mathf.Abs(8*CartoonFold-(i+1)))));
        }
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
        private float blendStarted, blendDuration=.18f, locomotionAt=-1, locomotionPhase, locomotionSpeed;
        private Transform[] bones;

        private Vector3[] idlePositions,idleScales;
        private Quaternion[] idleRotations;
        private float[] idleShapes;
        private int[] streamKeys;
        private float StreamAmount()
        {
            if(streamKeys==null){streamKeys=new int[8];for(int i=0;i<8;i++)streamKeys[i]=skin.sharedMesh.GetBlendShapeIndex("Streamline_"+((i+1)*125).ToString("0000"));}
            float amount=0;for(int i=0;i<8;i++)if(streamKeys[i]>=0)amount+=skin.GetBlendShapeWeight(streamKeys[i])*.01f*(i+1)/8f;return Mathf.Clamp01(amount);
        }
        private void SetStreamAmount(float amount)
        {
            StreamAmount();amount=Mathf.Clamp01(amount);
            for(int i=0;i<8;i++)if(streamKeys[i]>=0)skin.SetBlendShapeWeight(streamKeys[i],100*(i==7?Mathf.Clamp01(8*amount-7):Mathf.Max(0,1-Mathf.Abs(8*amount-(i+1)))));
        }
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
            BeginTransition("Locomotion",now);
            float dt=locomotionAt<0?0:Mathf.Clamp(now-locomotionAt,0,.1f);locomotionAt=now;
            locomotionSpeed=Mathf.Lerp(locomotionSpeed,moveSpeed,1-Mathf.Exp(-dt*7));
            locomotionPhase+=dt*Mathf.Lerp(.55f,1.65f,Mathf.Clamp01(locomotionSpeed/4.7f));
            if(idlePositions==null)
            {
                idlePositions=new Vector3[Bones.Length];idleScales=new Vector3[Bones.Length];idleRotations=new Quaternion[Bones.Length];
                idleShapes=new float[skin.sharedMesh.blendShapeCount];
            }
            // Sample the moving idle each frame; frame-zero caching froze it.
            var idle=Clip("Idle");idle.SampleAnimation(gameObject,Mathf.Repeat(locomotionPhase+phase,idle.length));
            for(int i=0;i<Bones.Length;i++){idlePositions[i]=Bones[i].localPosition;idleRotations[i]=Bones[i].localRotation;idleScales[i]=Bones[i].localScale;}
            for(int i=0;i<idleShapes.Length;i++)idleShapes[i]=skin.GetBlendShapeWeight(i);
            var swim=Clip("Swim");swim.SampleAnimation(gameObject,Mathf.Repeat(locomotionPhase+phase,swim.length));
            float weight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.03f,1.5f,locomotionSpeed));
            for(int i=0;i<Bones.Length;i++)
            {
                var b=Bones[i];b.localPosition=Vector3.Lerp(idlePositions[i],b.localPosition,weight);
                b.localRotation=Quaternion.Slerp(idleRotations[i],b.localRotation,weight);b.localScale=Vector3.Lerp(idleScales[i],b.localScale,weight);
            }
            for(int i=1;i<idleShapes.Length;i++)skin.SetBlendShapeWeight(i,Mathf.Lerp(idleShapes[i],skin.GetBlendShapeWeight(i),weight));
            if(cartoonKind=="Alien"){SetStreamAmount(weight);LimitWaveFrames(weight);}
            FinishTransition(now);
        }
        public AnimationClip Clip(string name) { foreach(var c in clips) if(c.name==name || c.name.EndsWith("_"+name))return c; throw new System.InvalidOperationException("Missing clip "+name); }
        public void SamplePose(string name,float time,float now)
        {
            BeginTransition(name,now);
            var clip=Clip(name);clip.SampleAnimation(gameObject,clip.wrapMode==WrapMode.Loop?Mathf.Repeat(time,clip.length):Mathf.Min(time,clip.length));
            FinishTransition(now);
        }
        private void BeginTransition(string name,float now)
        {
            if(blendPositions==null){blendPositions=new Vector3[Bones.Length];blendScales=new Vector3[Bones.Length];blendRotations=new Quaternion[Bones.Length];blendShapes=new float[skin.sharedMesh.blendShapeCount];}
            if(previousClip!=name)
            {
                blendDuration=cartoonKind=="Alien"?(name=="Locomotion"?.55f:.40f):cartoonKind=="Fish"?.28f:.18f;
                blendStarted=previousClip==null?now-1:now;previousClip=name;
                for(int i=0;i<Bones.Length;i++){blendPositions[i]=Bones[i].localPosition;blendRotations[i]=Bones[i].localRotation;blendScales[i]=Bones[i].localScale;}
                for(int i=1;i<blendShapes.Length;i++)blendShapes[i]=skin.GetBlendShapeWeight(i);
            }
        }
        private void FinishTransition(float now)
        {
            // Blend after all idle/swim composition so the source pose is exact.
            float blend=Mathf.SmoothStep(0,1,(now-blendStarted)/blendDuration);
            for(int i=0;i<Bones.Length && blend<1;i++)
            {var b=Bones[i];b.localPosition=Vector3.Lerp(blendPositions[i],b.localPosition,blend);b.localRotation=Quaternion.Slerp(blendRotations[i],b.localRotation,blend);b.localScale=Vector3.Lerp(blendScales[i],b.localScale,blend);}
            for(int i=1;i<blendShapes.Length && blend<1;i++)skin.SetBlendShapeWeight(i,Mathf.Lerp(blendShapes[i],skin.GetBlendShapeWeight(i),blend));
            if(cartoonKind=="Alien")SetStreamAmount(StreamAmount());
        }
        private void LimitWaveFrames(float gather)
        {
                float ripple=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,1,gather));
                float idleRipple=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.02f,.12f,gather));
                for(int i=1;i<blendShapes.Length;i++)
                {
                    string name=skin.sharedMesh.GetBlendShapeName(i);
                    if(name.StartsWith("StreamWave"))skin.SetBlendShapeWeight(i,skin.GetBlendShapeWeight(i)*ripple);
                    else if(name.StartsWith("IdleWave"))skin.SetBlendShapeWeight(i,skin.GetBlendShapeWeight(i)*idleRipple);
                }
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

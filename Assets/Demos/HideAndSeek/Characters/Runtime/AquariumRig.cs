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
        // R22: a fish model's own eyes (mesh space pupil centres and gaze axes) for the dead "X_X"
        // marks. Empty on Fish0, which keeps its measured defaults in AquariumDeadEyes.
        public Vector3[] eyePupils,eyeAxes;
        public float eyeRadius;
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
        // Divers have SwimBack/SwimLeft/SwimRight beside Swim; they are blended by the local swim direction.
        private int directionalSwim=-1;
        private Vector2 swimDirection=new Vector2(0,1);
        private Vector3[] strokePositions,strokeScales;
        private Quaternion[] strokeRotations;
        public bool DirectionalSwim
        {
            get
            {
                if(directionalSwim<0)directionalSwim=HasClip("SwimBack") && HasClip("SwimLeft") && HasClip("SwimRight")?1:0;
                return directionalSwim==1;
            }
        }
        public bool HasClip(string name){foreach(var c in clips)if(c.name==name || c.name.EndsWith("_"+name))return true;return false;}
        public Vector2 SwimDirection=>swimDirection;
        // localVelocity: x = right, z = forward in the swimmer's own heading frame.
        public void SampleLocomotion(float moveSpeed,float now,float phase,Vector3 localVelocity)
        {
            float dt=locomotionAt<0?0:Mathf.Clamp(now-locomotionAt,0,.1f);
            var wanted=new Vector2(localVelocity.x,localVelocity.z);
            if(wanted.sqrMagnitude>.04f)swimDirection=Vector2.Lerp(swimDirection,wanted.normalized,1-Mathf.Exp(-dt*6));
            SampleLocomotion(moveSpeed,now,phase);
        }
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
            var swim=Clip("Swim");
            if(DirectionalSwim)
            {
                // Forward or back-pedal stroke, blended toward the sideways stroke by how sideways the swim is.
                var d=swimDirection.sqrMagnitude>.0001f?swimDirection.normalized:new Vector2(0,1);
                float side=Mathf.Abs(d.x)/Mathf.Max(.0001f,Mathf.Abs(d.x)+Mathf.Abs(d.y));
                var stroke=d.y>=0?swim:Clip("SwimBack");
                stroke.SampleAnimation(gameObject,Mathf.Repeat(locomotionPhase+phase,stroke.length));
                if(side>.001f)
                {
                    if(strokePositions==null){strokePositions=new Vector3[Bones.Length];strokeScales=new Vector3[Bones.Length];strokeRotations=new Quaternion[Bones.Length];}
                    for(int i=0;i<Bones.Length;i++){strokePositions[i]=Bones[i].localPosition;strokeRotations[i]=Bones[i].localRotation;strokeScales[i]=Bones[i].localScale;}
                    var sideways=Clip(d.x>0?"SwimRight":"SwimLeft");sideways.SampleAnimation(gameObject,Mathf.Repeat(locomotionPhase+phase,sideways.length));
                    for(int i=0;i<Bones.Length;i++)
                    {
                        var b=Bones[i];b.localPosition=Vector3.Lerp(strokePositions[i],b.localPosition,side);
                        b.localRotation=Quaternion.Slerp(strokeRotations[i],b.localRotation,side);b.localScale=Vector3.Lerp(strokeScales[i],b.localScale,side);
                    }
                }
            }
            else swim.SampleAnimation(gameObject,Mathf.Repeat(locomotionPhase+phase,swim.length));
            float weight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.03f,1.5f,locomotionSpeed));
            for(int i=0;i<Bones.Length;i++)
            {
                var b=Bones[i];b.localPosition=Vector3.Lerp(idlePositions[i],b.localPosition,weight);
                b.localRotation=Quaternion.Slerp(idleRotations[i],b.localRotation,weight);b.localScale=Vector3.Lerp(idleScales[i],b.localScale,weight);
            }
            for(int i=1;i<idleShapes.Length;i++)skin.SetBlendShapeWeight(i,Mathf.Lerp(idleShapes[i],skin.GetBlendShapeWeight(i),weight));
            if(cartoonKind=="Alien"){SetStreamAmount(weight);LimitWaveFrames(weight);AutoBlink(now);}
            FinishTransition(now);
        }
        // R18: the alien blinks on its own while idling and swimming. Irregular
        // intervals (2-5 s) with an occasional double blink; quick close, slower open.
        private int[] blinkKeys;
        private System.Random blinkRandom;
        private float blinkStart=-10,nextBlink=-1,blinkClock;
        private bool blinkTwice;
        private void AutoBlink(float now)
        {
            if(blinkKeys==null)
            {
                blinkKeys=new int[12];
                for(int i=1;i<=12;i++)blinkKeys[i-1]=skin.sharedMesh.GetBlendShapeIndex(i%3==0?"Blink_"+(i*100/12):"BlinkStep_"+i.ToString("00"));
                if(System.Array.IndexOf(blinkKeys,-1)>=0)blinkKeys=new int[0];
            }
            if(blinkKeys.Length==0)return;
            if(blinkRandom==null)blinkRandom=new System.Random(GetInstanceID());
            if(nextBlink<0 || now<blinkClock-.5f || now>nextBlink+10){nextBlink=now+.8f+(float)blinkRandom.NextDouble()*2.4f;blinkStart=-10;}
            blinkClock=now;
            if(now>=nextBlink)
            {
                blinkStart=now;
                blinkTwice=!blinkTwice && blinkRandom.NextDouble()<.2;
                nextBlink=now+(blinkTwice?.34f:2.0f+(float)blinkRandom.NextDouble()*3.0f);
            }
            float t=now-blinkStart;
            float b=t<0||t>.27f?0:t<.07f?Mathf.SmoothStep(0,1,t/.07f):t<.11f?1:Mathf.SmoothStep(1,0,(t-.11f)/.16f);
            for(int i=0;i<12;i++)
                skin.SetBlendShapeWeight(blinkKeys[i],100*(i==11?Mathf.Clamp01(b*12-11):Mathf.Max(0,1-Mathf.Abs(b*12-(i+1)))));
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

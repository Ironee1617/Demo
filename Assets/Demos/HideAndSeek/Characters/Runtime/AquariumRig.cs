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
        private float blendStarted;
        public AnimationClip Clip(string name) { foreach(var c in clips) if(c.name==name || c.name.EndsWith("_"+name))return c; throw new System.InvalidOperationException("Missing clip "+name); }
        public void SamplePose(string name,float time,float now)
        {
            if(blendPositions==null){blendPositions=new Vector3[skin.bones.Length];blendScales=new Vector3[skin.bones.Length];blendRotations=new Quaternion[skin.bones.Length];blendShapes=new float[skin.sharedMesh.blendShapeCount];}
            if(previousClip!=name)
            {
                blendStarted=previousClip==null?now-1:now;previousClip=name;
                for(int i=0;i<skin.bones.Length;i++){blendPositions[i]=skin.bones[i].localPosition;blendRotations[i]=skin.bones[i].localRotation;blendScales[i]=skin.bones[i].localScale;}
                for(int i=1;i<blendShapes.Length;i++)blendShapes[i]=skin.GetBlendShapeWeight(i);
            }
            var clip=Clip(name);clip.SampleAnimation(gameObject,clip.wrapMode==WrapMode.Loop?Mathf.Repeat(time,clip.length):Mathf.Min(time,clip.length));
            float blend=Mathf.SmoothStep(0,1,(now-blendStarted)/.18f);
            for(int i=0;i<skin.bones.Length && blend<1;i++)
            {var b=skin.bones[i];b.localPosition=Vector3.Lerp(blendPositions[i],b.localPosition,blend);b.localRotation=Quaternion.Slerp(blendRotations[i],b.localRotation,blend);b.localScale=Vector3.Lerp(blendScales[i],b.localScale,blend);}
            for(int i=1;i<blendShapes.Length && blend<1;i++)skin.SetBlendShapeWeight(i,Mathf.Lerp(blendShapes[i],skin.GetBlendShapeWeight(i),blend));
        }
        public void Neutralize(float weight)
        {
            for(int i=1;i<skin.sharedMesh.blendShapeCount;i++)skin.SetBlendShapeWeight(i,skin.GetBlendShapeWeight(i)*(1-weight));
            for(int i=0;i<skin.bones.Length;i++)
            {
                var b=skin.bones[i];b.localPosition=Vector3.Lerp(b.localPosition,restPositions[i],weight);
                b.localRotation=Quaternion.Slerp(b.localRotation,restRotations[i],weight);b.localScale=Vector3.Lerp(b.localScale,restScales[i],weight);
            }
        }
    }
}

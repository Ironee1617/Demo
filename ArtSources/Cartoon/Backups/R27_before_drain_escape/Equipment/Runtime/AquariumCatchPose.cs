using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // Uses the diver's existing continuous sleeve/palm mesh and bones for the receiving hand.
    public sealed class AquariumCatchPose : IDisposable
    {
        private readonly AquariumRig rig;
        public Transform Hand { get; private set; }
        public bool Visible { get; private set; }
        public AquariumCatchPose(Transform parent)
        {
            // The authored first-person sleeve already ends outside the frame. Mirror its continuous
            // round palm instead of cutting the torso skin, which leaves an open shoulder seam.
            rig=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("AquariumEquipment/DiverHands"),parent).GetComponent<AquariumRig>();
            rig.name="Receiving left hand";
            rig.skin.updateWhenOffscreen=true;rig.skin.forceMatrixRecalculationPerRender=true;
            foreach(var child in rig.GetComponentsInChildren<Transform>(true))child.gameObject.layer=AquariumFirstPersonEquipment.Layer;
            foreach(var r in rig.GetComponentsInChildren<Renderer>(true)){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
            Hand=Array.Find(rig.Bones,b=>b.name=="HandL");
            rig.gameObject.SetActive(false);
        }

        public void Present(Harpoon shot,float time)
        {
            Visible=shot!=null&&shot.stowedAt>0&&time>=shot.retrievedAt-.45f&&time<shot.stowedAt;
            rig.gameObject.SetActive(Visible);if(!Visible)return;
            rig.Clip("FPSIdle").SampleAnimation(rig.gameObject,time%2);
            float t=AquariumModel.CollectionProgress(shot,time);
            float reach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(shot.retrievedAt-.45f,shot.retrievedAt+.25f,time));
            float tuck=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,t));
            rig.transform.localPosition=Vector3.Lerp(new Vector3(-.05f,-.18f,-.10f),new Vector3(0,.03f,.07f),reach)+new Vector3(-.40f,-.18f,-.40f)*tuck;
            rig.transform.localRotation=Quaternion.Euler(0,Mathf.Lerp(0,38,tuck),0);
            rig.transform.localScale=new Vector3(-1,1,1);
        }

        // Two-bone elbow solve with bounded reach; no detached hands or independently stretched fingers.
        public static Transform Solve(AquariumRig rig,Vector3 target,float blend)
        {
            var upper=Array.Find(rig.Bones,b=>b.name=="UpperArmR");
            var lower=Array.Find(rig.Bones,b=>b.name=="ForearmR");
            var hand=Array.Find(rig.Bones,b=>b.name=="HandR");
            if(upper==null||lower==null||hand==null)return null;
            var start=upper.position;float a=Vector3.Distance(start,lower.position),b=Vector3.Distance(lower.position,hand.position);
            var wanted=Vector3.Lerp(hand.position,target,Mathf.Clamp01(blend));var delta=wanted-start;
            float d=Mathf.Clamp(delta.magnitude,.025f,Mathf.Max(.03f,(a+b)*.98f));var axis=delta.sqrMagnitude>.0001f?delta.normalized:rig.transform.forward;
            var pole=Vector3.ProjectOnPlane(-rig.transform.up-rig.transform.right*.45f,axis).normalized;
            float along=(a*a-b*b+d*d)/(2*d),height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            var elbow=start+axis*along+pole*height;var end=start+axis*d;
            upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,end-lower.position)*lower.rotation;
            // The mitten palm faces into the carried body while retaining its authored finger shape.
            hand.rotation=Quaternion.Slerp(hand.rotation,Quaternion.LookRotation(rig.transform.forward,rig.transform.up)*Quaternion.Euler(70,130,80),blend*.55f);
            return hand;
        }
        public void Dispose(){Destroy(rig!=null?rig.gameObject:null);}
        private static void Destroy(UnityEngine.Object o){if(o==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
    }
}

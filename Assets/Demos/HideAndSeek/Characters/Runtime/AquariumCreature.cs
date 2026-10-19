using System.Collections.Generic;
using UnityEngine;
namespace PrototypeLab.Aquarium
{
    // Opaque geometric interpolation: no alpha, dissolve or dither.
    public sealed class AquariumCreature : MonoBehaviour
    {
        private static AquariumSpecies[] catalog;
        private static GameObject alien;
        private static readonly Dictionary<string,Mesh> transitionMeshes=new Dictionary<string,Mesh>();
        private static readonly Dictionary<Mesh,float> bodyRadii=new Dictionary<Mesh,float>();
        private readonly Dictionary<int,AquariumRig> forms=new Dictionary<int,AquariumRig>();
        private MaterialPropertyBlock properties;
        private AquariumRig current,outgoing;
        private int form=int.MinValue,previousForm;
        private bool closedMouthReturn,directMouthReturn;
        private float transitionAt,transitionDuration,speed,phase;
        private GameObject spear;
        private SkinnedMeshRenderer bridge;
        private AquariumInkCloud revealInk;
        private float revealInkAt=-100;
        private AquariumGrasp grasp;
        private float lipWrap,jawEmbrace;
        private bool stagedMeal;
        private Vector3 unstagedPosition;
        private Quaternion unstagedRotation;
        private Vector3 engulfOffset,engulfApplied;
        private Quaternion engulfAim=Quaternion.identity;
        private float engulfBorn=float.NegativeInfinity,motionBorn,engulfSwell=1,engulfAppliedSwell=1;
        public int Form=>form;
        public bool Transitioning=>outgoing!=null;
        public int BoneCount=>current==null?0:current.Bones.Length;
        public Vector3 FeedingMouth=>FeedingAnchor(false);
        public Vector3 FeedingThroat=>FeedingAnchor(true);
        public Vector3 FeedingForward=>(FeedingMouth-FeedingThroat).normalized;
        public float FeedingRadius
        {
            get
            {
                if(form<0 && current!=null && current.cartoon)
                {
                    Vector3 x=current.BodyPoint(Vector3.right)-current.BodyPoint(Vector3.zero),z=current.BodyPoint(Vector3.forward)-current.BodyPoint(Vector3.zero);
                    return .095f*Mathf.Min(x.magnitude,z.magnitude);
                }
                if(form>=0 && current!=null && current.cartoon)
                {
                    // R17: the cartoon fish opens a wide funnel maw. The meal is squeezed to
                    // the current opening so it fills the mouth while it is drawn in.
                    int maw=current.skin.sharedMesh.GetBlendShapeIndex("Alien_Mouth_Open");
                    float open=maw<0?1:Mathf.Clamp01(current.skin.GetBlendShapeWeight(maw)*.01f);
                    return Species(form%100).feedingMouthRadius*Mathf.Lerp(.35f,1,open)*Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.y));
                }
                return (form<0?.095f:Species(form%100).feedingMouthRadius*(1-.28f*lipWrap)*(1-.32f*jawEmbrace))*Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.y));
            }
        }
        private Vector3 FeedingAnchor(bool throat)
        {
                var anchor=current!=null?(throat?current.throatAnchor:current.mouthAnchor):Vector3.zero;
                if(current!=null && current.cartoon && form<0)
                {
                    int key=current.skin.sharedMesh.GetBlendShapeIndex("BellyOpen");
                    if(key>=0)anchor.y-=.015f*(1-Smooth(-.20f,-.12f,anchor.y))*current.skin.GetBlendShapeWeight(key)*.01f;
                }
                if(!throat && (current==null || !current.cartoon) && form>=100 && form<200)anchor.z+=.14f*lipWrap-.045f*jawEmbrace;
                if(current==null)return transform.TransformPoint(anchor);
                return current.BodyPoint(anchor);
        }
        public void Initialize(int initial,float seed)
        {
            phase=seed*.317f;properties=new MaterialPropertyBlock();
            if(catalog==null)catalog=Resources.LoadAll<AquariumSpecies>("AquariumCharacters");
            if(alien==null)alien=Resources.Load<GameObject>("AquariumCharacters/Alien");
            form=initial;current=GetForm(initial);current.gameObject.SetActive(true);
        }
        private static AquariumSpecies Species(int id)
        {foreach(var s in catalog)if(s.speciesId==id)return s;return catalog[0];}
        private static Color Tint(int id)=>id<0?new Color(.91f,.82f,1):Species(id%100).tint;
        private AquariumRig GetForm(int id)
        {
            // The cartoon fish opens its existing surface; all mouth stages share one rig.
            if(alien!=null && alien.GetComponent<AquariumRig>().cartoon && id>=0)id%=100;
            if(forms.TryGetValue(id,out var rig))return rig;
            var s=id<0?null:Species(id%100);var prefab=id<0?alien:id>=200?(s.closedFeeding!=null?s.closedFeeding:s.normal):id>=100?s.feeding:s.normal;
            if(prefab==null)throw new System.InvalidOperationException("Missing aquarium form "+id);
            rig=Instantiate(prefab,transform).GetComponent<AquariumRig>();rig.gameObject.SetActive(false);forms.Add(id,rig);return rig;
        }
        public void Present(int target,string motion,float born,float now,float moveSpeed,bool impaled=false)
        {
            if(stagedMeal){transform.localPosition=unstagedPosition;transform.localRotation=unstagedRotation;stagedMeal=false;}
            engulfApplied=Vector3.zero;engulfAppliedSwell=1;motionBorn=born;
            lipWrap=0;jawEmbrace=0;
            if(current==null)Initialize(target,0);speed=moveSpeed;
            // The fish disguise changes inside the closed carrier, not in front
            // of its mouth. A partial ordinary morph must not leak into SpitOut.
            // Capture also settles an unfinished disguise into its fish endpoint,
            // so the victim cannot keep folding into a transition shape in the grip.
            bool fixedMealForm=motion=="SpitOut" && target<0 || motion=="Eaten" && target>=0;
            if(fixedMealForm && (form!=target || outgoing!=null))
            {
                foreach(var rig in forms.Values){rig.gameObject.SetActive(false);rig.skin.enabled=true;}
                outgoing=null;if(bridge!=null)bridge.gameObject.SetActive(false);
                form=target;current=GetForm(target);current.gameObject.SetActive(true);current.SetCartoonFold(0);
                revealInkAt=-100;
            }
            if(motion=="Bite" && target>=100 && target<200 && now-born>=AquariumFeeding.ClosingSilhouette)target+=100;
            // A delayed snapshot must still complete the fish disguise before opening its mouth.
            if(form<0 && target>=100)target%=100;
            if(current.cartoon && target>=0 && form>=0 && target%100==form%100)form=target;
            if(target!=form && outgoing==null)
            {
                previousForm=form;outgoing=current;form=target;current=GetForm(form);
                bool changingMouth=previousForm>=0 && form>=0 && previousForm%100==form%100;
                transitionAt=now;transitionDuration=changingMouth?(previousForm>=100 && previousForm<200 && form>=200?.35f:.60f):1.4f;
                closedMouthReturn=changingMouth && previousForm>=100 && previousForm<200 && (form<100 || form>=200);
                directMouthReturn=changingMouth && previousForm>=200 && form<100;
                if(!current.cartoon)MakeBridge(outgoing,current,closedMouthReturn,directMouthReturn);
                if(previousForm>=0 && form<0)
                {
                    if(revealInk==null)
                    {
                        var cloud=new GameObject("Disguise release ink");cloud.transform.SetParent(transform.root,false);
                        revealInk=cloud.AddComponent<AquariumInkCloud>();revealInk.Initialize(Mathf.RoundToInt(phase*1000)+41,true,true);
                    }
                    revealInkAt=now;
                }
            }
            bool expelled=motion=="SpitOut";
            string pose=motion;
            float poseTime=motion=="Locomotion"?now+phase:Mathf.Max(0,now-born);
            if(current.cartoon && (motion=="TentacleEat" || motion=="Eaten"))poseTime=AquariumFeeding.CapturePoseTime(poseTime);
            if(outgoing==null)
            {current.gameObject.SetActive(true);Sample(current,pose,poseTime,0,now);Apply(current,form);transform.localScale=Vector3.one;}
            else
            {
                float u=Mathf.Clamp01((now-transitionAt)/transitionDuration);
                if(current.cartoon && outgoing.cartoon)
                {
                    // Both meshes meet the authored identical midpoint before the handoff.
                    bool first=u<.5f;outgoing.gameObject.SetActive(first);current.gameObject.SetActive(!first);
                    var active=first?outgoing:current;Sample(active,pose,poseTime,0,now);
                    var morph=active.GetComponent<AquariumMorphMotion>();if(morph!=null)morph.Overlay(active,first,u);
                    active.SetCartoonFold(first?Smooth(0,.5f,u):1-Smooth(.5f,1,u));Apply(active,first?previousForm:form);transform.localScale=Vector3.one;
                    if(u>=1){outgoing.gameObject.SetActive(false);outgoing=null;current.SetCartoonFold(0);}
                }
                else if(directMouthReturn)
                {
                    // Both closed forms share UVs, weights and vertex order.
                    // Relax only the muzzle, preserving all fin/scale detail.
                    outgoing.gameObject.SetActive(true);outgoing.skin.enabled=false;current.gameObject.SetActive(false);bridge.gameObject.SetActive(true);
                    Sample(outgoing,pose,poseTime,0,now);Apply(outgoing,previousForm);
                    outgoing.skin.GetPropertyBlock(properties);bridge.SetPropertyBlock(properties);
                    for(int i=0;i<outgoing.skin.sharedMesh.blendShapeCount;i++)bridge.SetBlendShapeWeight(i,outgoing.skin.GetBlendShapeWeight(i));
                    bridge.SetBlendShapeWeight(bridge.sharedMesh.blendShapeCount-1,Mathf.SmoothStep(0,100,u));transform.localScale=Vector3.one;
                    if(u>=1)
                    {
                        outgoing.skin.enabled=true;outgoing.gameObject.SetActive(false);outgoing=null;bridge.gameObject.SetActive(false);
                        current.gameObject.SetActive(true);Sample(current,pose,poseTime,0,now);Apply(current,form);
                    }
                }
                else
                {
                // Meet each endpoint's surface before switching topology. A single
                // shared surface carries the source and target silhouettes in between.
                outgoing.gameObject.SetActive(u<.12f);bridge.gameObject.SetActive(u>=.12f && u<.88f);current.gameObject.SetActive(u>=.88f);
                if(u<.12f){Sample(outgoing,pose,poseTime,Smooth(0,.12f,u)*100,now,closedMouthReturn);Apply(outgoing,previousForm);}
                else if(u<.88f)
                {
                    float blend=Smooth(.12f,.88f,u);bridge.SetBlendShapeWeight(0,blend*100);bridge.SetBlendShapeWeight(1,Mathf.Sin(blend*Mathf.PI)*100);
                    properties.Clear();properties.SetFloat("_Blend",blend);properties.SetColor("_SourceTint",Tint(previousForm));properties.SetColor("_TargetTint",Tint(form));bridge.SetPropertyBlock(properties);
                }
                else{Sample(current,pose,poseTime,(1-Smooth(.88f,1,u))*100,now);Apply(current,form);}
                bool mouth=previousForm>=0 && form>=0 && previousForm%100==form%100;
                float swell=mouth?.035f:.16f;transform.localScale=Vector3.one*(1+swell*Mathf.Sin(u*Mathf.PI));
                if(u>=1){outgoing.gameObject.SetActive(false);outgoing=null;bridge.gameObject.SetActive(false);}
                }
            }
            // A brief body reaction settles into the normal swimming pose.
            float recoil=expelled?Mathf.Pow(1-Mathf.Clamp01((now-born)/AquariumModel.ReleaseKickSeconds),2):0;
            transform.localRotation=Quaternion.Euler(-14*recoil,0,9*recoil);
            if(expelled && outgoing==null)transform.localScale=new Vector3(1-.16f*recoil,1-.13f*recoil,1+.22f*recoil);
            if(impaled && spear==null)CreateSpear();if(spear!=null)spear.SetActive(impaled);
            if(revealInk!=null)revealInk.Present(now-revealInkAt+.08f,AquariumModel.TraceDuration,transform.position,transform.forward);
            if(form<0 && !current.cartoon && outgoing==null && (motion=="TentacleEat" || motion=="Bite"))
            {
                if(grasp==null)grasp=new AquariumGrasp(current);
                grasp.Present(now-born,transform.forward,.18f*transform.lossyScale.x,.36f*transform.lossyScale.x);
            }
            if(form>=100 && form<200 && motion=="Bite")jawEmbrace=.60f*Smooth(.76f,1.25f,now-born);
            if(motion=="TentacleEat" && Mathf.Abs(engulfBorn-born)<.001f)
            {
                SaveMealTransform();
                float remaining=1-Smooth(AquariumModel.SwallowVisualSeconds,2.95f,now-born);
                engulfAppliedSwell=Mathf.Lerp(1,engulfSwell,remaining);
                current.Bones[1].localScale*=engulfAppliedSwell;
                transform.localRotation=Quaternion.Slerp(unstagedRotation,engulfAim,remaining);
                var offset=engulfOffset*remaining;
                engulfApplied=transform.parent==null?offset:transform.parent.TransformVector(offset);
                transform.position+=engulfApplied;
            }
        }
        private void SaveMealTransform()
        {
            if(stagedMeal)return;
            unstagedPosition=transform.localPosition;unstagedRotation=transform.localRotation;stagedMeal=true;
        }
        private void OnDestroy()
        {if(revealInk!=null){if(Application.isPlaying)Destroy(revealInk.gameObject);else DestroyImmediate(revealInk.gameObject);}}
        private void Sample(AquariumRig rig,string pose,float t,float shape,float now,bool sealedMouth=false)
        {
            if(pose=="Locomotion")rig.SampleLocomotion(speed,now,phase);else rig.SamplePose(pose,t,now);rig.Neutralize(shape/100);rig.skin.SetBlendShapeWeight(0,shape);
            rig.skin.localBounds=new Bounds(Vector3.zero,Vector3.one*4);
            if(sealedMouth)
            {
                // ClosedPose is the exact baked articulated pose, so resetting the
                // skeleton here does not reopen the mouth at the topology switch.
                rig.Neutralize(1);var mesh=rig.skin.sharedMesh;rig.skin.SetBlendShapeWeight(0,0);
                rig.skin.SetBlendShapeWeight(mesh.GetBlendShapeIndex("ClosedPose"),100);
                rig.skin.SetBlendShapeWeight(mesh.GetBlendShapeIndex("ClosedMetamorphosis"),shape);
            }
        }
        public void PresentIngestion(AquariumCreature predator,float age)
        {
            if(predator.form<0 && predator.current.cartoon){PresentTentacleIngestion(predator,age);return;}
            Vector3 mouth=predator.FeedingThroat,outward=predator.FeedingForward;float radius=predator.FeedingRadius;
            var bounds=current.skin.sharedMesh.bounds;var frame=current.skin.transform;
            Vector3 source=frame.TransformPoint(bounds.center);
            if(!stagedMeal){unstagedPosition=transform.localPosition;unstagedRotation=transform.localRotation;stagedMeal=true;}
            float align=Smooth(0,AquariumFeeding.AlignmentEnd,age);
            Quaternion facing=Quaternion.LookRotation(-outward,predator.transform.up);
            transform.rotation=Quaternion.Slerp(transform.rotation,facing,align);
            var e=bounds.extents;
            float predatorScale=Mathf.Max(.001f,predator.transform.lossyScale.x);
            float stage=Vector3.Distance(predator.FeedingMouth,mouth)+.18f*predatorScale+e.z*Mathf.Abs(frame.lossyScale.z)*1.18f;
            float clearance=(e.magnitude*Mathf.Max(frame.lossyScale.x,frame.lossyScale.y,frame.lossyScale.z))+.65f*predatorScale;
            Vector3 placed=AquariumFeeding.StagePoint(source,mouth,outward,predator.transform.up,stage,clearance,age);
            transform.position+=placed-frame.TransformPoint(bounds.center);
            Vector3 origin=frame.TransformPoint(bounds.center),axis=outward;
            float half=Mathf.Abs(Vector3.Dot(axis,frame.TransformVector(Vector3.right*e.x)))+Mathf.Abs(Vector3.Dot(axis,frame.TransformVector(Vector3.up*e.y)))+Mathf.Abs(Vector3.Dot(axis,frame.TransformVector(Vector3.forward*e.z)));
            float progress=AquariumFeeding.Suction(age);
            float depth=Mathf.Max(.06f,Vector3.Dot(predator.FeedingMouth-mouth,axis));
            // Use the leading body's current section, not the disappearing root,
            // to place the grip. Oversized prey can be held by its entering end.
            float scale=Mathf.Max(.001f,predator.transform.lossyScale.x);
            float contact=.34f*scale;
            float radial=BodyRadius(current)*Mathf.Max(frame.lossyScale.x,frame.lossyScale.y);
            float available=radius+Mathf.Max(0,contact-depth)*1.15f;
            float fitted=Mathf.Min(radial,available);
            predator.EnvelopMeal(age,axis,fitted,contact,radial);
            mouth=predator.FeedingThroat;radius=predator.FeedingRadius;depth=Mathf.Max(.06f,Vector3.Dot(predator.FeedingMouth-mouth,axis));
            bool sealedCavity=predator.current.cartoon;
            IngestSkin(current.skin,origin,axis,half,mouth,radius,depth,progress,sealedCavity);
            if(outgoing!=null)IngestSkin(outgoing.skin,origin,axis,half,mouth,radius,depth,progress,sealedCavity);
            if(bridge!=null && bridge.gameObject.activeSelf)IngestSkin(bridge,origin,axis,half,mouth,radius,depth,progress,sealedCavity);
        }
        private static Quaternion OralFacing(Vector3 forward,Transform carrier)
        {
            Vector3 up=Vector3.ProjectOnPlane(carrier.up,forward);
            if(up.sqrMagnitude<.001f)up=Vector3.ProjectOnPlane(carrier.forward,forward);
            return Quaternion.LookRotation(forward,up.normalized);
        }
        private void PresentTentacleIngestion(AquariumCreature predator,float age)
        {
            float rawAge=age;
            age=AquariumFeeding.CapturePoseTime(age);
            predator.transform.position-=predator.engulfApplied;predator.engulfApplied=Vector3.zero;
            if(predator.stagedMeal)predator.transform.localRotation=predator.unstagedRotation;
            predator.SaveMealTransform();
            // A small, uniform gulp expansion leaves room for the fish's fins
            // without flattening either the tentacles or the swallowed body.
            predator.engulfSwell=1+(form>=0?.12f:.04f)*Smooth(0,.45f,age);
            predator.current.Bones[1].localScale*=predator.engulfSwell/predator.engulfAppliedSwell;
            predator.engulfAppliedSwell=predator.engulfSwell;
            var frame=current.skin.transform;var bounds=current.skin.sharedMesh.bounds;
            Vector3 head=current.cartoonKind=="Alien"?current.BodyPoint(new Vector3(0,.22f,0)):frame.TransformPoint(bounds.center);
            Vector3 source=head;
            // Aim the ventral mouth from the current idle position. Turning in
            // place precedes a short forward approach, never an overhead orbit.
            Vector3 pivot=predator.current.BodyPoint(Vector3.zero),toMeal=source-pivot;
            if(toMeal.sqrMagnitude>.0001f)
            {
                Quaternion aimed=Quaternion.FromToRotation(predator.FeedingForward,toMeal.normalized)*predator.transform.rotation;
                predator.transform.rotation=Quaternion.Slerp(predator.transform.rotation,aimed,Smooth(0,.72f,rawAge));
            }
            predator.engulfAim=predator.transform.localRotation;
            Vector3 mouth=predator.FeedingThroat,axis=predator.FeedingForward;
            if(!stagedMeal){unstagedPosition=transform.localPosition;unstagedRotation=transform.localRotation;stagedMeal=true;}
            Quaternion facing=OralFacing(-axis,predator.transform);
            if(current.cartoonKind=="Alien")
            {
                Vector3 forward=Vector3.ProjectOnPlane(predator.transform.forward,axis);
                if(forward.sqrMagnitude<.001f)forward=Vector3.ProjectOnPlane(predator.transform.up,axis);
                facing=Quaternion.LookRotation(forward.normalized,-axis);
            }
            transform.rotation=Quaternion.Slerp(transform.rotation,facing,Smooth(0,AquariumFeeding.AlienAlignmentEnd,age));
            float scale=Mathf.Max(.001f,predator.transform.lossyScale.x)*predator.engulfSwell;
            float distance=Mathf.Lerp(.78f,.66f,AquariumFeeding.AlienPull(age))*scale;
            Vector3 approach=(source-mouth-axis*distance)*Smooth(.60f,1.08f,rawAge);
            predator.engulfBorn=predator.motionBorn;
            predator.engulfOffset=predator.transform.parent==null?approach:predator.transform.parent.InverseTransformVector(approach);
            predator.engulfApplied=approach;predator.transform.position+=approach;
            mouth=predator.FeedingThroat;
            head=current.cartoonKind=="Alien"?current.BodyPoint(new Vector3(0,.22f,0)):frame.TransformPoint(bounds.center);
            transform.position+=source-head;
            // Bound both the folded tail and the head. The old rest bounds left
            // the long swimming arms outside the fully swallowed endpoint.
            Vector3 origin=frame.TransformPoint(bounds.center);var e=bounds.extents;
            float half=Mathf.Abs(Vector3.Dot(axis,frame.TransformVector(Vector3.right*e.x)))+Mathf.Abs(Vector3.Dot(axis,frame.TransformVector(Vector3.up*e.y)))+Mathf.Abs(Vector3.Dot(axis,frame.TransformVector(Vector3.forward*e.z)));
            if(current.cartoonKind=="Alien")half=Mathf.Max(half,1.65f*Mathf.Abs(frame.lossyScale.z));
            float depth=Mathf.Max(.06f,Vector3.Dot(predator.FeedingMouth-mouth,axis));
            float opening=predator.current.skin.GetBlendShapeWeight(predator.current.skin.sharedMesh.GetBlendShapeIndex("BellyOpen"))*.01f;
            float radius=predator.FeedingRadius*(1+.6f*opening),progress=AquariumFeeding.AlienSuction(age);
            IngestSkin(current.skin,origin,axis,half,mouth,radius,depth,progress,true);
            if(outgoing!=null)IngestSkin(outgoing.skin,origin,axis,half,mouth,radius,depth,progress,true);
        }
        public void PresentExpulsion(AquariumCreature carrier,float age)
        {
            if(age<0 || age>=AquariumFeeding.ExpulsionEnd || current==null)return;
            if(!stagedMeal){unstagedPosition=transform.localPosition;unstagedRotation=transform.localRotation;stagedMeal=true;}
            Vector3 mouth=carrier.FeedingThroat,axis=carrier.FeedingForward;
            var frame=current.skin.transform;var bounds=current.skin.sharedMesh.bounds;
            Vector3 destination=frame.TransformPoint(bounds.center);
            float half=(current.cartoonKind=="Alien"?1.65f:bounds.extents.z*1.1f)*Mathf.Abs(frame.lossyScale.z);
            float leave=Smooth(.62f,AquariumFeeding.ExpulsionEnd,age);
            Vector3 up=Vector3.ProjectOnPlane(carrier.transform.up,axis);
            if(up.sqrMagnitude<.001f)up=Vector3.ProjectOnPlane(carrier.transform.forward,axis);
            transform.rotation=Quaternion.Slerp(Quaternion.LookRotation(axis,up.normalized),transform.rotation,leave);
            Vector3 staged=carrier.FeedingMouth+axis*(half+.20f);
            transform.position+=Vector3.Lerp(staged,destination,leave)-frame.TransformPoint(bounds.center);
            Vector3 origin=frame.TransformPoint(bounds.center);
            float depth=Mathf.Max(.06f,Vector3.Dot(carrier.FeedingMouth-mouth,axis));
            // Reverse the continuous oral deformation: the leading end clears
            // the lower lip first, followed by the torso and trailing tentacles.
            float concealed=1-Smooth(.08f,.72f,age);
            IngestSkin(current.skin,origin,axis,half,mouth,carrier.FeedingRadius*1.6f,depth,concealed,carrier.form<0&&carrier.current.cartoon);
            if(outgoing!=null)IngestSkin(outgoing.skin,origin,axis,half,mouth,carrier.FeedingRadius*1.6f,depth,concealed,carrier.form<0&&carrier.current.cartoon);
        }
        private static float BodyRadius(AquariumRig rig)
        {
            var mesh=rig.skin.sharedMesh;if(bodyRadii.TryGetValue(mesh,out var radius))return radius;
            var weights=mesh.boneWeights;var vertices=mesh.vertices;var core=new bool[rig.Bones.Length];
            for(int i=0;i<core.Length;i++){string n=rig.Bones[i].name;core[i]=n=="Body"||n=="Head"||n=="Spine";}
            var bounds=new Bounds(mesh.bounds.center,Vector3.zero);
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];float share=(core[w.boneIndex0]?w.weight0:0)+(core[w.boneIndex1]?w.weight1:0)+(core[w.boneIndex2]?w.weight2:0)+(core[w.boneIndex3]?w.weight3:0);
                if(share>.65f)bounds.Encapsulate(vertices[i]);
            }
            radius=Mathf.Max(.04f,Mathf.Min(bounds.extents.x,bounds.extents.y));bodyRadii.Add(mesh,radius);return radius;
        }
        private void EnvelopMeal(float age,Vector3 axis,float radius,float distance,float bodyRadius)
        {
            if(outgoing!=null)return;
            if(form<0)
            {
                if(current.cartoon)return;
                if(grasp==null)grasp=new AquariumGrasp(current);
                grasp.Present(age,axis,radius,distance);
            }
            else if(form>=100 && form<200)
            {
                float relative=bodyRadius/Mathf.Max(.001f,transform.lossyScale.x);
                float grip=Smooth(.48f,.90f,age)*(1-Smooth(1.30f,1.65f,age));
                lipWrap=grip*Mathf.Lerp(1,.30f,Mathf.InverseLerp(.13f,.45f,relative));
                int key=current.skin.sharedMesh.GetBlendShapeIndex("LipEnvelope");
                if(key>=0)current.skin.SetBlendShapeWeight(key,lipWrap*100);
                key=current.skin.sharedMesh.GetBlendShapeIndex("BuccalGulp");
                if(key>=0)current.skin.SetBlendShapeWeight(key,Mathf.Sin(Mathf.PI*Smooth(.82f,1.65f,age))*65);
            }
        }
        private void IngestSkin(SkinnedMeshRenderer skin,Vector3 origin,Vector3 axis,float half,Vector3 mouth,float radius,float depth,float progress,bool sealedCavity=false)
        {
            skin.GetPropertyBlock(properties);properties.SetFloat("_Ingest",progress);properties.SetVector("_IngestOrigin",origin);properties.SetVector("_IngestAxis",axis);
            properties.SetFloat("_IngestHalfLength",Mathf.Max(.01f,half*1.1f));properties.SetVector("_IngestMouth",new Vector4(mouth.x,mouth.y,mouth.z,radius));
            properties.SetFloat("_IngestDepth",depth);properties.SetFloat("_IngestSeal",sealedCavity?1:0);skin.SetPropertyBlock(properties);
            var bounds=new Bounds(Vector3.zero,Vector3.one*4);bounds.Encapsulate(skin.transform.InverseTransformPoint(mouth));skin.localBounds=bounds;
        }
        private void Apply(AquariumRig rig,int id)
        {properties.Clear();properties.SetColor("_Tint",rig.cartoon?Color.Lerp(id<0?Color.white:Tint(id),Color.white,rig.CartoonFold):Tint(id));properties.SetFloat("_Morph",rig.skin.GetBlendShapeWeight(0)/100);int maw=rig.skin.sharedMesh.GetBlendShapeIndex("BellyOpen");properties.SetFloat("_MawOpen",maw<0?0:rig.skin.GetBlendShapeWeight(maw)/100);rig.skin.SetPropertyBlock(properties);}
        private static float Smooth(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        private static Mesh TransitionMesh(AquariumRig from,AquariumRig to,bool sealedMouth,bool direct)
        {
            string key=from.envelope.name+"_"+to.envelope.name+(sealedMouth?"_Closed":"")+(direct?"_Direct":"");
            if(!transitionMeshes.TryGetValue(key,out var mesh) || mesh==null)
            {
                if(direct)
                {
                    mesh=Instantiate(from.skin.sharedMesh);var a=mesh.vertices;var b=to.skin.sharedMesh.vertices;var n=mesh.normals;var nb=to.skin.sharedMesh.normals;
                    if(a.Length!=b.Length)throw new System.InvalidOperationException("Closed fish topology must match its normal form");
                    for(int i=0;i<a.Length;i++){b[i]-=a[i];nb[i]-=n[i];}
                    mesh.AddBlendShapeFrame("Return to normal muzzle",100,b,nb,null);transitionMeshes[key]=mesh;
                }
                else
                {
                var sourceEnvelope=sealedMouth?from.closedEnvelope:from.envelope;
                mesh=Instantiate(sourceEnvelope);mesh.name=key+" continuous surface";var a=mesh.vertices;var b=to.envelope.vertices;
                var na=mesh.normals;var nb=to.envelope.normals;
                var delta=new Vector3[a.Length];var normals=new Vector3[a.Length];var targetColors=new List<Vector4>(a.Length);var colors=to.envelope.colors;
                for(int i=0;i<a.Length;i++){delta[i]=b[i]-a[i];normals[i]=nb[i]-na[i];targetColors.Add(colors[i]);}
                var relaxA=new Vector3[a.Length];var relaxB=new Vector3[a.Length];var normalA=new Vector3[a.Length];var normalB=new Vector3[a.Length];
                sourceEnvelope.GetBlendShapeFrameVertices(0,0,relaxA,normalA,null);to.envelope.GetBlendShapeFrameVertices(0,0,relaxB,normalB,null);
                for(int i=0;i<a.Length;i++){relaxA[i]=(relaxA[i]+relaxB[i])*.5f;normalA[i]=(normalA[i]+normalB[i])*.5f;}
                mesh.ClearBlendShapes();mesh.AddBlendShapeFrame("Target silhouette",100,delta,normals,null);mesh.AddBlendShapeFrame("Organic relaxation",100,relaxA,normalA,null);mesh.SetUVs(2,targetColors);mesh.bounds=new Bounds(Vector3.zero,Vector3.one*3);transitionMeshes[key]=mesh;
                }
            }
            return mesh;
        }
        private void MakeBridge(AquariumRig from,AquariumRig to,bool sealedMouth,bool direct)
        {
            var mesh=TransitionMesh(from,to,sealedMouth,direct);
            if(bridge==null)
            {
                var go=new GameObject("Opaque silhouette transition");go.transform.SetParent(transform,false);bridge=go.AddComponent<SkinnedMeshRenderer>();bridge.bones=new[]{transform};bridge.rootBone=transform;bridge.localBounds=new Bounds(Vector3.zero,Vector3.one*3);
                bridge.sharedMaterial=Resources.Load<Material>("AquariumCharacters/Metamorphosis");
            }
            bridge.bones=direct?from.Bones:new[]{transform};bridge.rootBone=direct?from.skin.rootBone:transform;
            bridge.sharedMaterial=direct?from.skin.sharedMaterial:Resources.Load<Material>("AquariumCharacters/Metamorphosis");
            bridge.sharedMesh=mesh;bridge.SetBlendShapeWeight(0,0);
        }
        // Shared geometry and turbulence are prepared during the loading screen.
        public static void Prewarm()
        {
            AquariumInkCloud.Prewarm();
            var go=new GameObject("Creature loading cache");go.SetActive(false);
            try
            {
                var creature=go.AddComponent<AquariumCreature>();creature.Initialize(-1,0);var alienRig=creature.current;
                foreach(var species in catalog)
                {
                    var normal=creature.GetForm(species.speciesId);var mouth=creature.GetForm(species.speciesId+100);var closed=creature.GetForm(species.speciesId+200);
                    BodyRadius(normal);BodyRadius(alienRig);
                    if(alienRig.cartoon)continue;
                    TransitionMesh(alienRig,normal,false,false);TransitionMesh(normal,alienRig,false,false);
                    TransitionMesh(normal,mouth,false,false);TransitionMesh(mouth,closed,true,false);TransitionMesh(closed,normal,false,true);
                    TransitionMesh(mouth,alienRig,false,false);TransitionMesh(closed,alienRig,false,false);
                }
            }
            finally { if(Application.isPlaying)Destroy(go);else DestroyImmediate(go); }
        }
        public void PrepareFeedingForms(int species)
        { if(species>=0){GetForm(species%100+100);GetForm(species%100+200);} }
        private void CreateSpear()
        {
            spear=GameObject.CreatePrimitive(PrimitiveType.Cylinder);spear.name="Embedded harpoon";if(Application.isPlaying)Destroy(spear.GetComponent<Collider>());else DestroyImmediate(spear.GetComponent<Collider>());spear.transform.SetParent(transform,false);
            spear.transform.localRotation=Quaternion.Euler(90,0,0);spear.transform.localScale=new Vector3(.025f,.65f,.025f);spear.GetComponent<Renderer>().sharedMaterial=Resources.Load<Material>("AquariumCharacters/HarpoonMetal");
        }
    }
}

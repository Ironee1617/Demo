using System;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>Travel-driven joints over independent painted parts and a separate action layer.</summary>
    public sealed class TowerMythRig : MonoBehaviour
    {
        [Serializable]
        public sealed class Profile
        {
            public int artIndex;
            public float groundX, groundY, bodyHeight;
            public float hipX, hipY, headX, headY, shoulderX, shoulderY;
            public float leftKneeX, leftKneeY, rightKneeX, rightKneeY;
            public float leftFootX, leftFootY, rightFootX, rightFootY;
            public float leftAnkleX, leftAnkleY, rightAnkleX, rightAnkleY;
            public float leftHandX, leftHandY, rightHandX, rightHandY;
            public float leftShoulderX, leftShoulderY, leftElbowX, leftElbowY;
            public float rightShoulderX, rightShoulderY, rightElbowX, rightElbowY;
            public float weaponBaseX, weaponBaseY, weaponTipX, weaponTipY, weaponWidth;
            public float weaponTipRadiusX, weaponTipRadiusY;
            public int weaponHand;
            public float headRadiusX, headRadiusY, legHemY, strideScale, sourceMinX, sourceMaxX;
            // Measured outlines use top-origin pixels in the untrimmed idle cell.
            public Vector2[] headOutline, leftArmOutline, leftForearmOutline, rightArmOutline, rightForearmOutline, weaponOutline, secondaryWeaponOutline;
            public Vector2[] leftLegOutline, rightLegOutline;
            public Vector2 leftArmBackgroundOffset, rightArmBackgroundOffset;
        }

        // Shared with TowerMythModel. Preserve the source-part numbering.
        public enum Joint { Hip, Torso, Head, LeftArm, LeftForearm, RightArm, RightForearm,
            LeftThigh, LeftShin, LeftFoot, RightThigh, RightShin, RightFoot, Cloth, Weapon, Count }
        enum Archetype { Sword, Spear, Hammer, Bow, Staff, Orb, Dual }
        struct Bone { public Vector2 rest, position; public float angle; }
        struct FootCycle
        {
            public bool initialized, stance, settling;
            public float plant, start, end, duty, swingStart;
            public float settleTime, lastRoll, settleRoll, airRoll;
            public Vector2 lastTarget, settleFrom, airFrom;
        }
        struct ActionPose
        {
            public float lean, head, hipX, hipY, leftArm, rightArm, leftElbow, rightElbow;
            public float weaponAngle, weaponMix, cape;
            public Vector2 mainReach, freeReach;
            public static ActionPose Mix(ActionPose a, ActionPose b, float t)
            {
                return new ActionPose { lean=Mathf.Lerp(a.lean,b.lean,t), head=Mathf.Lerp(a.head,b.head,t),
                    hipX=Mathf.Lerp(a.hipX,b.hipX,t), hipY=Mathf.Lerp(a.hipY,b.hipY,t),
                    leftArm=Mathf.Lerp(a.leftArm,b.leftArm,t), rightArm=Mathf.Lerp(a.rightArm,b.rightArm,t),
                    leftElbow=Mathf.Lerp(a.leftElbow,b.leftElbow,t), rightElbow=Mathf.Lerp(a.rightElbow,b.rightElbow,t),
                    weaponAngle=Mathf.LerpAngle(a.weaponAngle,b.weaponAngle,t), weaponMix=Mathf.Lerp(a.weaponMix,b.weaponMix,t),
                    cape=Mathf.Lerp(a.cape,b.cape,t), mainReach=Vector2.Lerp(a.mainReach,b.mainReach,t),
                    freeReach=Vector2.Lerp(a.freeReach,b.freeReach,t) };
            }
        }

        const float Height=1.75f, Tau=Mathf.PI*2;
        readonly Bone[] bones=new Bone[(int)Joint.Count];
        readonly Matrix4x4[] matrices=new Matrix4x4[(int)Joint.Count];
        readonly float[] armReturnFrom=new float[4],armReturnTargets=new float[4];
        readonly float[] previousArmAngles=new float[4];
        readonly float[] ikAngles=new float[(int)Joint.Count];
        readonly bool[] ikInitialized=new bool[(int)Joint.Count];
        TowerMythModel model;
        Profile anatomy;
        CompanionRole role;
        Archetype archetype;
        Vector2 hip, shoulder, head, leftHip, rightHip, leftShoulder, rightShoulder;
        Vector2 leftKnee, rightKnee, leftFoot, rightFoot, leftHand, rightHand, leftElbow, rightElbow;
        Vector2 leftAnkle, rightAnkle;
        float leftGroundOffset, rightGroundOffset;
        Vector2 leftSoleMin, leftSoleMax, rightSoleMin, rightSoleMax;
        FootCycle leftCycle, rightCycle;
        ActionPose action, actionFrom;
        string upperState;
        float unit, clock, phase, travel, gaitBlend, runBlend, duty=.62f, cadence=1.8f, strideReach=.32f;
        float previousSpeed, clothAngle, landingTime, airTime, airPoseBlend, airLean, accelerationLean, supportDrop, actionAge, previousProgress;
        float leftFootAngle, rightFootAngle, bindWeaponAngle;
        float lastWeaponAngle, lastCarryAngle, weaponReturnFrom, weaponReturnTarget, weaponReturnAge, weaponReturnDuration;
        float sampledWeaponAngle,sampledWeaponMix,armReturnAge;
        bool posed, wasGrounded=true, twoHanded, weaponReturning,armsReturning,rotatingAction;
        int previousFacing=1;

        public Renderer Renderer { get { return model==null?null:model.Renderer; } }
        public float RenderedHeight { get { return model==null?0:model.RenderedHeight; } }
        public Vector2 LeftFootPosition { get { return bones[(int)Joint.LeftFoot].position; } }
        public Vector2 RightFootPosition { get { return bones[(int)Joint.RightFoot].position; } }
        public Vector2 RestLeftFoot { get { return leftFoot; } }
        public Vector2 RestRightFoot { get { return rightFoot; } }
        public Vector2 RestLeftAnkle { get { return leftAnkle; } }
        public Vector2 RestRightAnkle { get { return rightAnkle; } }
        public Vector2 LeftAnklePosition { get { return Map(Joint.LeftShin,leftAnkle); } }
        public Vector2 RightAnklePosition { get { return Map(Joint.RightShin,rightAnkle); } }
        public Vector2 RestHeadPosition { get { return head; } }
        public Vector2 RestHipPosition { get { return hip; } }
        public Vector2 RestShoulderPosition { get { return shoulder; } }
        public Vector2 RestLeftHandPosition { get { return leftHand; } }
        public Vector2 RestRightHandPosition { get { return rightHand; } }
        public Vector2 RestWeaponGripPosition { get { return MainLeft?leftHand:rightHand; } }
        public Vector2 RestWeaponBasePosition { get { return anatomy==null?Vector2.zero:Point(anatomy.weaponBaseX,anatomy.weaponBaseY); } }
        public Vector2 RestWeaponTipPosition { get { return anatomy==null?Vector2.zero:Point(anatomy.weaponTipX,anatomy.weaponTipY); } }
        public float WeaponWidth { get { return anatomy==null?0:anatomy.weaponWidth*unit; } }
        public Vector2 WeaponTipRadii { get { return anatomy==null?Vector2.zero:new Vector2(anatomy.weaponTipRadiusX,anatomy.weaponTipRadiusY)*unit; } }
        public Vector2 HeadRadii { get { return anatomy==null?Vector2.zero:new Vector2(anatomy.headRadiusX,anatomy.headRadiusY)*unit; } }
        public float MeshCellPadding { get { return unit; } }
        public float GarmentHemY { get { return anatomy!=null&&anatomy.legHemY>0?Point(0,anatomy.legHemY).y:hip.y; } }
        // Detached legs use a full gait even when a robe covers their upper segments.
        public float StrideScale { get { return 1; } }
        public float ReleaseFraction { get; set; }=.46f;
        public bool LeftFootInStance { get; private set; }
        public bool RightFootInStance { get; private set; }
        public bool Hovering { get { return anatomy!=null&&anatomy.artIndex==126; } }
        public float GaitDutyCycle { get { return duty; } }
        public Vector3[] CaptureVertices() { return model==null?new Vector3[0]:model.CaptureVertices(); }
        public Vector3[] CaptureRestVertices() { return model==null?new Vector3[0]:model.CaptureRestVertices(); }
        public int[] CapturePartIndices() { return model==null?new int[0]:model.CapturePartIndices(); }
        public int SourcePixelOwner(int topX,int topY) { return model==null?255:model.SourcePixelOwner(topX,topY); }
        public Vector2 SampleSourcePoint(Vector2 restPoint) { return model==null?restPoint:model.SampleSourcePoint(restPoint); }
        bool MainLeft { get { return anatomy!=null&&anatomy.weaponHand==1; } }

        public void Build(int artIndex,Sprite idle,Profile profile,CompanionRole combatRole=CompanionRole.Vanguard)
        {
            if(idle==null)throw new ArgumentNullException("idle");
            anatomy=profile??DefaultProfile(artIndex,idle.rect.width,idle.rect.height);
            if(anatomy.bodyHeight<=1)throw new ArgumentException("A measured myth body height is required.");
            role=combatRole; archetype=WeaponType(artIndex); unit=Height/anatomy.bodyHeight;
            clock=(artIndex-TowerMythArt.FirstArtIndex)*.173f;
            hip=Point(anatomy.hipX,anatomy.hipY); shoulder=Point(anatomy.shoulderX,anatomy.shoulderY);
            head=Point(anatomy.headX,anatomy.headY);
            leftKnee=Point(anatomy.leftKneeX,anatomy.leftKneeY); rightKnee=Point(anatomy.rightKneeX,anatomy.rightKneeY);
            leftFoot=Point(anatomy.leftFootX,anatomy.leftFootY); rightFoot=Point(anatomy.rightFootX,anatomy.rightFootY);
            leftAnkle=anatomy.leftAnkleY>0?Point(anatomy.leftAnkleX,anatomy.leftAnkleY):
                leftFoot+(leftKnee-leftFoot).normalized*.10f;
            rightAnkle=anatomy.rightAnkleY>0?Point(anatomy.rightAnkleX,anatomy.rightAnkleY):
                rightFoot+(rightKnee-rightFoot).normalized*.10f;
            leftHand=Point(anatomy.leftHandX,anatomy.leftHandY); rightHand=Point(anatomy.rightHandX,anatomy.rightHandY);
            float spread=Mathf.Clamp((rightFoot.x-leftFoot.x)*.22f,.045f,.105f);
            leftHip=hip+Vector2.left*spread; rightHip=hip+Vector2.right*spread;
            leftShoulder=shoulder+Vector2.left*.085f; rightShoulder=shoulder+Vector2.right*.075f;
            if(anatomy.leftShoulderX>0&&anatomy.leftShoulderY>0)leftShoulder=Point(anatomy.leftShoulderX,anatomy.leftShoulderY);
            if(anatomy.rightShoulderX>0&&anatomy.rightShoulderY>0)rightShoulder=Point(anatomy.rightShoulderX,anatomy.rightShoulderY);
            leftElbow=Vector2.Lerp(leftShoulder,leftHand,.52f)+Vector2.left*.025f;
            if(anatomy.leftElbowX>0&&anatomy.leftElbowY>0)leftElbow=Point(anatomy.leftElbowX,anatomy.leftElbowY);
            rightElbow=Vector2.Lerp(rightShoulder,rightHand,.52f)+Vector2.right*.025f;
            if(anatomy.rightElbowX>0&&anatomy.rightElbowY>0)rightElbow=Point(anatomy.rightElbowX,anatomy.rightElbowY);
            Bind(Joint.Hip,hip); Bind(Joint.Torso,hip); Bind(Joint.Head,shoulder+Vector2.up*.05f);
            Bind(Joint.LeftArm,leftShoulder); Bind(Joint.LeftForearm,leftElbow);
            Bind(Joint.RightArm,rightShoulder); Bind(Joint.RightForearm,rightElbow);
            Bind(Joint.LeftThigh,leftHip); Bind(Joint.LeftShin,leftKnee); Bind(Joint.LeftFoot,leftFoot);
            Bind(Joint.RightThigh,rightHip); Bind(Joint.RightShin,rightKnee); Bind(Joint.RightFoot,rightFoot);
            Bind(Joint.Cloth,shoulder); Bind(Joint.Weapon,RestWeaponGripPosition);
            Vector2 axis=RestWeaponTipPosition-RestWeaponBasePosition;
            bindWeaponAngle=axis.sqrMagnitude>.001f?Mathf.Atan2(axis.y,axis.x)*Mathf.Rad2Deg:90;
            Vector2 otherHand=MainLeft?rightHand:leftHand;
            twoHanded=archetype!=Archetype.Bow&&SegmentDistance(otherHand,RestWeaponBasePosition,RestWeaponTipPosition)<.13f&&
                Vector2.Distance(leftHand,rightHand)>.15f;
            model=new TowerMythModel(transform,idle,anatomy);
            var bindVertices=model.CaptureRestVertices();var bindParts=model.CapturePartIndices();
            float leftBottom=float.MaxValue,rightBottom=float.MaxValue;
            leftSoleMin=rightSoleMin=Vector2.one*float.MaxValue;
            leftSoleMax=rightSoleMax=Vector2.one*float.MinValue;
            for(int i=0;i<bindVertices.Length;i++)
            {
                if(bindParts[i]==9)
                {
                    leftBottom=Mathf.Min(leftBottom,bindVertices[i].y);
                    Vector2 sole=(Vector2)bindVertices[i]-leftFoot;
                    leftSoleMin=Vector2.Min(leftSoleMin,sole);leftSoleMax=Vector2.Max(leftSoleMax,sole);
                }
                if(bindParts[i]==12)
                {
                    rightBottom=Mathf.Min(rightBottom,bindVertices[i].y);
                    Vector2 sole=(Vector2)bindVertices[i]-rightFoot;
                    rightSoleMin=Vector2.Min(rightSoleMin,sole);rightSoleMax=Vector2.Max(rightSoleMax,sole);
                }
            }
            if(leftBottom<float.MaxValue&&rightBottom<float.MaxValue)
            {
                // Fighter.pos is the physical floor. The painted source may
                // extend below its measured pivot; do not use that as the floor.
                leftGroundOffset=-leftBottom;rightGroundOffset=-rightBottom;
            }
            for(int i=0;i<matrices.Length;i++)matrices[i]=Matrix4x4.identity;
            model.DeltaTime=0; model.Pose(matrices,Color.white,1);
        }

        public void Pose(string motion,bool moving,float time,float actionProgress,float hurtTime,
            int facing,Color tint,bool grounded,Vector2 velocity,float horizontalSpeed,float dt)
        {
            if(model==null)return;
            int face=facing<0?-1:1;
            if(posed&&dt<=0){model.DeltaTime=0;model.Pose(matrices,tint,face);return;}
            dt=Mathf.Max(0,dt);clock+=dt;
            float scale=Mathf.Max(.01f,Mathf.Abs(transform.lossyScale.x));
            float speed=Mathf.Abs(horizontalSpeed)/scale,signedSpeed=velocity.x/scale;
            travel+=signedSpeed*dt;
            bool dead=motion=="Death";
            bool locomoting=moving&&grounded&&speed>.04f&&!dead&&!Hovering;
            if(previousFacing!=face)
            {
                // Mirroring changes the view, not the gait phase. Rebase the
                // contact trajectory in the new view without dropping the legs
                // back toward their idle pose for one frame.
                float pivot=travel-signedSpeed*dt;
                MirrorCycle(ref leftCycle,pivot);MirrorCycle(ref rightCycle,pivot);
            }
            if(grounded&&!wasGrounded)leftCycle.initialized=rightCycle.initialized=false;
            if(grounded&&!wasGrounded)landingTime=.18f;
            landingTime=Mathf.Max(0,landingTime-dt);
            airTime=grounded?0:airTime+dt;
            airPoseBlend=Mathf.MoveTowards(airPoseBlend,grounded?0:1,dt*8);
            airLean=Mathf.Lerp(airLean,grounded?0:Mathf.Clamp(-velocity.y/scale*.8f,-8,7),1-Mathf.Exp(-dt*16));
            gaitBlend=Mathf.MoveTowards(gaitBlend,locomoting?1:0,dt*8);
            runBlend=Mathf.MoveTowards(runBlend,Mathf.InverseLerp(2.5f,5.2f,speed),dt*5);
            float walkCadence=Mathf.Clamp(1.25f+speed*.25f,1.35f,2.15f);
            float runCadence=Mathf.Clamp(2.25f+speed*.11f,2.4f,3.65f);
            float targetCadence=Mathf.Lerp(walkCadence,runCadence,runBlend);
            duty=Mathf.Lerp(.61f,.44f,runBlend);
            // A short-legged costume needs quicker steps, not a deeper crouch.
            // Budget only a small pelvis drop when choosing the planted stride.
            float reach=Mathf.Min(StrideReach(leftHip,leftKnee,leftAnkle),
                StrideReach(rightHip,rightKnee,rightAnkle));
            strideReach=reach;
            targetCadence=Mathf.Max(targetCadence,speed*duty/(2*reach));
            // A dash changes root speed immediately, but the swinging leg must
            // accelerate through its cycle instead of skipping half its arc.
            // Decelerating must also slow phase immediately; retaining dash
            // cadence after the root brakes makes the free foot kick forward.
            cadence=posed?Mathf.Min(targetCadence,cadence+dt*12):targetCadence;
            if(locomoting)phase=Mathf.Repeat(phase+cadence*dt,1);
            float direction=Mathf.Abs(signedSpeed)>.02f?Mathf.Sign(signedSpeed):face;
            float acceleration=dt>0?Mathf.Clamp((signedSpeed-previousSpeed)/dt,-20,20):0;
            accelerationLean=Mathf.Lerp(accelerationLean,acceleration,1-Mathf.Exp(-dt*14));
            previousSpeed=signedSpeed;
            Vector2 lf=SampleFoot(ref leftCycle,leftFoot,leftHip,phase,locomoting,grounded,speed,signedSpeed,direction,face,dt,out leftFootAngle);
            Vector2 rf=SampleFoot(ref rightCycle,rightFoot,rightHip,Mathf.Repeat(phase+.5f,1),locomoting,grounded,speed,signedSpeed,direction,face,dt,out rightFootAngle);
            if(grounded&&!dead&&!Hovering)
            {
                lf.y=Mathf.Max(lf.y,SoleHeight(Joint.LeftFoot,leftFootAngle));
                rf.y=Mathf.Max(rf.y,SoleHeight(Joint.RightFoot,rightFootAngle));
            }
            LeftFootInStance=grounded&&!Hovering&&leftCycle.stance;
            RightFootInStance=grounded&&!Hovering&&rightCycle.stance;
            float p=Mathf.Clamp01(actionProgress);
            SampleActionLayer(motion,p,hurtTime,dt);
            for(int i=0;i<4;i++)previousArmAngles[i]=bones[(int)Joint.LeftArm+i].angle;
            Evaluate(motion,p,grounded,speed,direction*face,accelerationLean*face,lf,rf,dt);
            for(int i=0;i<bones.Length;i++)
            {
                Bone b=bones[i];
                matrices[i]=Matrix4x4.TRS(new Vector3(b.position.x,b.position.y,0),Quaternion.Euler(0,0,b.angle),Vector3.one)*
                    Matrix4x4.Translate(new Vector3(-b.rest.x,-b.rest.y,0));
            }
            model.DeltaTime=dt;model.Pose(matrices,tint,face);
            wasGrounded=grounded;previousFacing=face;previousProgress=p;posed=true;
        }

        static void MirrorCycle(ref FootCycle foot,float pivot)
        {
            if(!foot.initialized)return;
            foot.plant=2*pivot-foot.plant;foot.start=2*pivot-foot.start;foot.end=2*pivot-foot.end;
        }

        Vector2 SampleFoot(ref FootCycle foot,Vector2 rest,Vector2 root,float footPhase,bool moving,bool grounded,
            float speed,float signedSpeed,float direction,int facing,float dt,out float roll)
        {
            roll=0;
            if(Hovering)return rest+Vector2.up*HoverHeight();
            if(!grounded)
            {
                // Take off from the rendered contact, including a stopped stride.
                // Returning to the painted idle feet here snaps both knees before
                // the airborne tuck has even started.
                if(wasGrounded||!posed)
                {
                    foot.airFrom=posed?bones[(int)(rest==leftFoot?Joint.LeftFoot:Joint.RightFoot)].position:rest;
                    foot.airRoll=posed?foot.lastRoll:0;
                }
                float tuck=Mathf.SmoothStep(0,1,airTime/.18f);
                Vector2 airborne=rest+new Vector2(rest==leftFoot?-.09f:.07f,rest==leftFoot?.16f:.10f);
                roll=Mathf.Lerp(foot.airRoll,0,tuck);
                foot.lastTarget=Vector2.Lerp(foot.airFrom,airborne,tuck);
                foot.lastRoll=roll;foot.stance=false;
                return foot.lastTarget;
            }
            // Both painted soles meet one floor, including a source pose where
            // perspective placed the far shoe a few pixels higher.
            rest.y+=rest==leftFoot?leftGroundOffset:rightGroundOffset;
            if(!moving)
            {
                if(!foot.initialized){foot.stance=true;foot.settling=false;foot.lastTarget=rest;return rest;}
                if(!foot.settling)
                {
                    foot.settling=true;foot.settleTime=foot.stance ? .20f : 0;
                    foot.settleFrom=foot.lastTarget;foot.settleRoll=foot.lastRoll;
                }
                foot.settleTime+=dt;
                float settle=Mathf.SmoothStep(0,1,foot.settleTime/.20f);
                roll=Mathf.Lerp(foot.settleRoll,0,settle);
                // Keep the stopping contact instead of sliding both shoes back
                // to the painted idle stance. A swinging shoe only settles down.
                Vector2 stopped=new Vector2(foot.settleFrom.x,rest.y);
                foot.lastTarget=Vector2.Lerp(foot.settleFrom,stopped,settle);
                foot.lastRoll=roll;foot.stance=settle>=1;
                return foot.lastTarget;
            }
            if(foot.settling)foot.initialized=false;
            foot.settling=false;
            if(!foot.initialized)foot.duty=duty;
            else if(foot.stance)foot.duty=Mathf.Min(foot.duty,duty);
            bool stance=footPhase<foot.duty;
            if(stance&&!foot.stance)foot.duty=duty;
            float halfTravel=Mathf.Min(strideReach,speed*duty/(2*cadence));
            if(!foot.initialized||stance!=foot.stance)
            {
                if(stance)foot.plant=foot.initialized?foot.end:travel+root.x*facing+direction*halfTravel;
                else{foot.start=foot.initialized?foot.plant:travel+rest.x*facing;foot.end=foot.start;foot.swingStart=footPhase;}
                foot.initialized=true;
            }
            foot.stance=stance;
            Vector2 target;
            if(stance)
            {
                target=new Vector2((foot.plant-travel)*facing,rest.y);
                roll=0;
            }
            else
            {
                // A speed change may shorten a planted contact, but must never
                // remap a foot already in mid-swing to a later point in its arc.
                float swing=Mathf.InverseLerp(foot.swingStart,1,footPhase);
                float remaining=(1-footPhase)/cadence;
                float predicted=travel+signedSpeed*remaining+root.x*facing+direction*halfTravel;
                foot.end=Mathf.Lerp(foot.end,predicted,1-Mathf.Exp(-dt*28));
                float worldX=Mathf.Lerp(foot.start,foot.end,Mathf.SmoothStep(0,1,swing));
                float lift=Mathf.Lerp(.065f,.13f,runBlend);
                target=new Vector2((worldX-travel)*facing,rest.y+Mathf.Sin(swing*Mathf.PI)*lift);
                roll=Mathf.Sin(swing*Mathf.PI)*8;
            }
            roll*=gaitBlend;
            foot.lastRoll=roll;
            foot.lastTarget=Vector2.Lerp(rest,target,gaitBlend);
            return foot.lastTarget;
        }

        void SampleActionLayer(string motion,float p,float hurt,float dt)
        {
            string state=motion=="Attack"||motion=="Cast"?motion:motion=="Death"?"Death":hurt>0||motion=="Hurt"?"Hurt":"Locomotion";
            ActionPose sample=ActionTarget(state,p,hurt);
            rotatingAction=state=="Attack"||state=="Cast";
            sampledWeaponAngle=sample.weaponAngle;
            sampledWeaponMix=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.76f,.94f,p));
            if(state!=upperState)
            {
                bool wasAction=upperState=="Attack"||upperState=="Cast";
                bool recovered=wasAction&&previousProgress>=.94f&&state=="Locomotion";
                if(wasAction&&!recovered&&(state=="Locomotion"||state=="Hurt"))
                {
                    weaponReturning=true;weaponReturnFrom=lastWeaponAngle;weaponReturnAge=0;
                    weaponReturnTarget=weaponReturnFrom+Mathf.DeltaAngle(weaponReturnFrom,lastCarryAngle);
                    float reach=Mathf.Max(Vector2.Distance(RestWeaponGripPosition,RestWeaponBasePosition),
                        Vector2.Distance(RestWeaponGripPosition,RestWeaponTipPosition)+WeaponTipRadii.magnitude);
                    float arc=Mathf.Abs(Mathf.DeltaAngle(lastWeaponAngle,lastCarryAngle))*Mathf.Deg2Rad;
                    weaponReturnDuration=Mathf.Clamp(1.5f*arc*reach/(Height*10),.14f,.36f);
                    armsReturning=true;armReturnAge=0;
                    for(int i=0;i<4;i++)armReturnFrom[i]=armReturnTargets[i]=bones[(int)Joint.LeftArm+i].angle;
                }
                else if(state=="Attack"||state=="Cast"||state=="Death")
                {
                    weaponReturning=false;armsReturning=false;
                    for(int i=(int)Joint.LeftArm;i<=(int)Joint.RightForearm;i++)ikInitialized[i]=false;
                }
                actionFrom=action;actionAge=posed&&!recovered?0:.07f;upperState=state;
            }
            actionAge+=dt;
            if(weaponReturning)weaponReturnAge+=dt;
            if(armsReturning)armReturnAge+=dt;
            float blend=Mathf.SmoothStep(0,1,actionAge/.07f);
            if((state=="Attack"||state=="Cast")&&p>=.32f||state=="Locomotion"&&previousProgress>=.94f)blend=1;
            action=ActionPose.Mix(actionFrom,sample,blend);
        }

        ActionPose ActionTarget(string state,float p,float hurt)
        {
            var a=new ActionPose();
            if(state=="Hurt")
            {
                float recoil=Mathf.Clamp01(hurt/.18f);if(hurt<=0)recoil=1-p;
                a.lean=11*recoil;a.head=-4*recoil;a.hipX=-.055f*Height*recoil;
                a.leftArm=14*recoil;a.rightArm=-8*recoil;a.cape=-6*recoil;return a;
            }
            if(state!="Attack"&&state!="Cast")return a;
            float contact=Mathf.Clamp(ReleaseFraction,.3f,.7f);
            float drive=Curve(p,0,-.40f,1,.55f,contact);
            float prep=p<contact?Mathf.Sin(Mathf.PI*Mathf.InverseLerp(0,contact,p)):0;
            float envelope=Mathf.SmoothStep(0,1,p/.10f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.76f,.94f,p)));
            bool casting=state=="Cast"||archetype==Archetype.Orb||archetype==Archetype.Staff&&anatomy.artIndex!=125;
            a.weaponMix=envelope;
            if(casting)
            {
                float rise=Curve(p,0,-.15f,1,.72f,contact);
                a.lean=-6*rise;a.head=3*rise;a.hipY=.015f*Height*Mathf.Max(0,rise);
                a.mainReach=new Vector2(.055f*Height*rise,.18f*Height*rise);
                Vector2 gestureHand=MainLeft?rightHand:leftHand,gestureShoulder=MainLeft?rightShoulder:leftShoulder;
                Vector2 gesture=gestureShoulder+new Vector2(.16f*Height,.045f*Height);
                a.freeReach=rise<0?new Vector2(.12f*Height*rise,.14f*Height*rise):
                    HandArc(gestureHand,gestureShoulder,gesture,rise,.20f,-.12f);
                // A shield, second blade or sickle stays in a carrying guard;
                // the dominant hand performs the spell without whipping the prop.
                if(anatomy.artIndex==108||anatomy.artIndex==112||anatomy.artIndex==134||anatomy.artIndex==145)
                    a.freeReach=Vector2.zero;
                a.weaponAngle=-10*rise;a.cape=-5*prep+7*drive;
                a.leftArm=-14*rise;a.rightArm=14*rise;return a;
            }
            if(archetype==Archetype.Bow)
            {
                float draw=Curve(p,0,1,1,.05f,contact);
                Vector2 drawHand=MainLeft?rightHand:leftHand;
                Vector2 cheek=head+new Vector2(-.035f*Height,-.065f*Height);
                Vector2 aim=head+new Vector2(.28f*Height,-.09f*Height);
                a.lean=-6*draw+4*Mathf.Max(0,drive);a.head=-2*draw;
                a.mainReach=(aim-RestWeaponGripPosition)*draw;
                a.freeReach=HandArc(drawHand,MainLeft?rightShoulder:leftShoulder,cheek,draw,.24f,-.08f);
                a.weaponAngle=-3*drive;a.cape=-4*drive;return a;
            }
            drive=LeadCurve(p,0,-.40f,1,.55f,contact);
            prep=LeadCurve(p,0,1,0,0,contact);
            a.lean=LeadCurve(p,0,8,-12,-7,contact);a.head=-a.lean*.3f;
            a.hipX=Height*.065f*drive;a.hipY=-Height*.018f*Mathf.Max(0,drive);
            a.mainReach=new Vector2(Height*.16f*drive,Height*(.065f*Mathf.Max(0,drive)+.10f*prep));
            a.freeReach=new Vector2(-Height*.045f*drive,Height*.035f*prep);
            // The recovered guandao end has a 1.70-unit lever, while the ox axe
            // begins almost a half-turn from its raised pose. Keep their windups
            // compact so these full painted contours do not whip at contact.
            float wind=anatomy.artIndex==100||anatomy.artIndex==103||anatomy.artIndex==128?70:anatomy.artIndex==146?115:
                archetype==Archetype.Hammer?125:archetype==Archetype.Spear?85:anatomy.artIndex==125?85:95;
            float hit=archetype==Archetype.Hammer?-12:archetype==Archetype.Spear?0:-8;
            float follow=archetype==Archetype.Spear?-5:-32;
            a.weaponAngle=Mathf.DeltaAngle(bindWeaponAngle,CurveAngle(p,bindWeaponAngle,wind,hit,follow,contact));
            if(archetype==Archetype.Spear)a.mainReach.x=Height*.22f*drive;
            if(archetype==Archetype.Hammer)a.mainReach.y+=Height*.07f*prep;
            a.cape=Curve(p,0,-7,2,13,Mathf.Min(.72f,contact+.10f));
            return a;
        }

        void Evaluate(string motion,float p,bool grounded,float speed,float moveDirection,float acceleration,
            Vector2 lf,Vector2 rf,float dt)
        {
            bool dead=motion=="Death";
            float cycle=phase*Tau,breath=Mathf.Sin(clock*2.25f);
            float halfPhase=Mathf.Repeat(phase,.5f)*2;
            float flight=duty<.5f&&halfPhase>duty*2?Mathf.Sin(Mathf.PI*Mathf.InverseLerp(duty*2,1,halfPhase)):0;
            float hover=Hovering?HoverHeight():0;
            float walkBounce=(.5f-.5f*Mathf.Cos(cycle*2))*.018f*Height;
            Vector2 offset=new Vector2(Mathf.Sin(cycle)*.014f*Height*gaitBlend,
                Mathf.Lerp(walkBounce,flight*.035f*Height,runBlend)*gaitBlend+hover);
            float land=landingTime>0?Mathf.Sin(Mathf.PI*(1-landingTime/.18f)):0;
            offset.y-=land*.055f*Height;
            offset+=new Vector2(action.hipX,action.hipY);
            float pelvisAngle=Mathf.Sin(cycle)*Mathf.Lerp(1.5f,3,runBlend)*gaitBlend;
            float lean=-Mathf.Lerp(3,11,runBlend)*gaitBlend*(moveDirection>=0?1:.35f)-acceleration*.12f;
            if(Hovering)lean=-Mathf.Clamp(speed*.6f,0,8)+Mathf.Sin(clock*3)*1.2f;
            float torsoAngle=lean+breath*.65f+Mathf.Sin(cycle)*1.8f*gaitBlend+action.lean;
            torsoAngle+=airLean;
            float fall=0;
            if(dead)
            {
                float kneel=Mathf.SmoothStep(0,1,p/.58f);
                offset+=new Vector2(.07f*Height*kneel,-.23f*Height*kneel);
                torsoAngle-=24*kneel;pelvisAngle=0;
                fall=-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,1,p))*72;
                lf=leftFoot;rf=rightFoot;
            }
            Vector2 lh=hip+offset+Rotate(leftHip-hip,pelvisAngle);
            Vector2 rh=hip+offset+Rotate(rightHip-hip,pelvisAngle);
            if(grounded&&!Hovering&&!dead)
            {
                float minimumX=float.MinValue,maximumX=float.MaxValue;
                if(LeftFootInStance)SupportRange(leftHip,leftKnee,leftAnkle,lf+Rotate(leftAnkle-leftFoot,leftFootAngle),lh.x-offset.x,ref minimumX,ref maximumX);
                if(RightFootInStance)SupportRange(rightHip,rightKnee,rightAnkle,rf+Rotate(rightAnkle-rightFoot,rightFootAngle),rh.x-offset.x,ref minimumX,ref maximumX);
                if(minimumX<=maximumX)
                {
                    float adjusted=Mathf.Clamp(offset.x,minimumX,maximumX);
                    lh.x+=adjusted-offset.x;rh.x+=adjusted-offset.x;offset.x=adjusted;
                }
            }
            float reachableDrop=0;
            if(grounded&&!Hovering)
            {
                // The supporting leg controls body height. A lifted swing foot
                // may bend freely without dragging the entire torso downward.
                if(LeftFootInStance||dead)
                    reachableDrop=Mathf.Min(reachableDrop,ReachableHipOffset(lh,leftHip,leftKnee,leftAnkle,lf+Rotate(leftAnkle-leftFoot,leftFootAngle)));
                if(RightFootInStance||dead)
                    reachableDrop=Mathf.Min(reachableDrop,ReachableHipOffset(rh,rightHip,rightKnee,rightAnkle,rf+Rotate(rightAnkle-rightFoot,rightFootAngle)));
            }
            // A speed drop can shorten the next step and instantly release a
            // deep reach correction. Rise through the knees instead of popping
            // the whole body upward. Lowering still obeys the reach ceiling
            // immediately, so planted soles never slide or overextend the legs.
            supportDrop=reachableDrop<supportDrop?reachableDrop:Mathf.MoveTowards(supportDrop,reachableDrop,dt*3);
            offset.y+=supportDrop;lh.y+=supportDrop;rh.y+=supportDrop;
            Set(Joint.Hip,hip+offset,pelvisAngle);Set(Joint.Torso,hip+offset,torsoAngle);
            Vector2 chest=Map(Joint.Torso,shoulder);
            Set(Joint.Head,Map(Joint.Torso,bones[(int)Joint.Head].rest),torsoAngle*.32f+action.head+breath*.3f);
            float baseCloth=lean*.48f-Mathf.Clamp(speed*.65f,0,8)+Mathf.Sin(cycle-.7f)*3.5f*gaitBlend+Mathf.Sin(clock*3.1f)*.9f;
            clothAngle=Mathf.LerpAngle(clothAngle,baseCloth,1-Mathf.Exp(-dt*11));
            Set(Joint.Cloth,chest,clothAngle+action.cape);
            float swing=Mathf.Sin(cycle)*moveDirection*gaitBlend;
            float armRange=Mathf.Lerp(13,25,runBlend);
            float leftSwing=swing*(MainLeft?armRange*.4f:armRange),rightSwing=-swing*(MainLeft?armRange:armRange*.4f);
            leftSwing=Mathf.Lerp(leftSwing,-18,airPoseBlend);rightSwing=Mathf.Lerp(rightSwing,15,airPoseBlend);
            if(dead){leftSwing=18*p;rightSwing=23*p;}
            Arm(Joint.LeftArm,Joint.LeftForearm,leftShoulder,leftElbow,torsoAngle,leftSwing+action.leftArm,action.leftElbow);
            Arm(Joint.RightArm,Joint.RightForearm,rightShoulder,rightElbow,torsoAngle,rightSwing+action.rightArm,action.rightElbow);
            if(armsReturning)
            {
                float recovery=Mathf.SmoothStep(0,1,armReturnAge/.20f);
                for(int i=0;i<4;i++)
                {
                    Joint joint=(Joint)((int)Joint.LeftArm+i);Bone b=bones[(int)joint];
                    armReturnTargets[i]+=Mathf.DeltaAngle(armReturnTargets[i],b.angle);
                    b.angle=Mathf.Lerp(armReturnFrom[i],armReturnTargets[i],recovery);bones[(int)joint]=b;
                }
                Set(Joint.LeftForearm,Map(Joint.LeftArm,leftElbow),bones[(int)Joint.LeftForearm].angle);
                Set(Joint.RightForearm,Map(Joint.RightArm,rightElbow),bones[(int)Joint.RightForearm].angle);
                if(armReturnAge>=.20f)armsReturning=false;
            }
            Joint mainUpper=MainLeft?Joint.LeftArm:Joint.RightArm,mainLower=MainLeft?Joint.LeftForearm:Joint.RightForearm;
            Joint freeUpper=MainLeft?Joint.RightArm:Joint.LeftArm,freeLower=MainLeft?Joint.RightForearm:Joint.LeftForearm;
            Vector2 mainHand=RestWeaponGripPosition,freeHand=MainLeft?rightHand:leftHand;
            if(action.weaponMix>0&&!armsReturning)
            {
                SolveArm(mainUpper,mainLower,MainLeft?leftShoulder:rightShoulder,MainLeft?leftElbow:rightElbow,mainHand,
                    Map(Joint.Torso,mainHand+action.mainReach),action.weaponMix,dt);
                if(!twoHanded)
                    SolveArm(freeUpper,freeLower,MainLeft?rightShoulder:leftShoulder,MainLeft?rightElbow:leftElbow,freeHand,
                        Map(Joint.Torso,freeHand+action.freeReach),action.weaponMix,dt);
            }
            Vector2 grip=Map(mainLower,mainHand);
            float carryAngle=Mathf.Sin(bones[(int)mainLower].angle*Mathf.Deg2Rad)*15;
            // The action curve already eases its angular speed. Applying the
            // arm-entry blend and an entry envelope again delays then rushes it.
            float weaponAngle=Mathf.LerpAngle(carryAngle,rotatingAction?sampledWeaponAngle:action.weaponAngle,
                rotatingAction?sampledWeaponMix:action.weaponMix);
            if(weaponReturning)
            {
                // Recover the rendered angle itself. Fading both a target angle
                // and its weight can cross an angular wrap and snap a long prop.
                weaponReturnTarget+=Mathf.DeltaAngle(weaponReturnTarget,carryAngle);
                weaponAngle=Mathf.Lerp(weaponReturnFrom,weaponReturnTarget,
                    Mathf.SmoothStep(0,1,weaponReturnAge/weaponReturnDuration));
                if(weaponReturnAge>=weaponReturnDuration)weaponReturning=false;
            }
            // Long painted blades amplify a small angle change at the hand.
            // Bound their turn rate through attacks and interruptions as well.
            if(posed&&dt>0)
                weaponAngle=lastWeaponAngle+Mathf.Clamp(Mathf.DeltaAngle(lastWeaponAngle,weaponAngle),-600*dt,600*dt);
            if(grounded&&!dead&&anatomy.weaponWidth>0)
            {
                float baseline=Mathf.Min(0,WeaponMinimumY(mainHand,0));
                float lift=Mathf.Max(0,baseline-WeaponMinimumY(grip,weaponAngle));
                if(lift>0)
                {
                    SolveArm(mainUpper,mainLower,MainLeft?leftShoulder:rightShoulder,MainLeft?leftElbow:rightElbow,mainHand,grip+Vector2.up*lift,1,dt);
                    grip=Map(mainLower,mainHand);
                }
            }
            Set(Joint.Weapon,grip,weaponAngle);
            lastWeaponAngle=weaponAngle;lastCarryAngle=carryAngle;
            if(twoHanded&&!dead)
                SolveArm(freeUpper,freeLower,MainLeft?rightShoulder:leftShoulder,MainLeft?rightElbow:leftElbow,freeHand,
                    grip+Rotate(freeHand-mainHand,weaponAngle),1,dt);
            Leg(Joint.LeftThigh,Joint.LeftShin,Joint.LeftFoot,lh,leftHip,leftKnee,leftAnkle,leftFoot,lf,leftFootAngle,LeftFootInStance,grounded&&!dead&&!Hovering);
            Leg(Joint.RightThigh,Joint.RightShin,Joint.RightFoot,rh,rightHip,rightKnee,rightAnkle,rightFoot,rf,rightFootAngle,RightFootInStance,grounded&&!dead&&!Hovering);
            if(fall!=0)for(int i=0;i<bones.Length;i++)
            {Bone b=bones[i];b.position=rightFoot+Rotate(b.position-rightFoot,fall);b.angle+=fall;bones[i]=b;}
        }

        void Arm(Joint upper,Joint lower,Vector2 s,Vector2 e,float torsoAngle,float shoulderAngle,float elbowAngle)
        {
            Set(upper,Map(Joint.Torso,s),torsoAngle+shoulderAngle);
            Set(lower,Map(upper,e),torsoAngle+shoulderAngle+elbowAngle);
        }
        void SolveArm(Joint upperJoint,Joint lowerJoint,Vector2 s,Vector2 e,Vector2 hand,Vector2 target,float weight,float dt)
        {
            Bone beforeUpper=bones[(int)upperJoint],beforeLower=bones[(int)lowerJoint];
            Vector2 start=Map(Joint.Torso,s),elbow,solved;
            SolveTwoBone(start,target,Vector2.Distance(s,e),Vector2.Distance(e,hand),Cross(hand-s,e-s)>=0?1:-1,out elbow,out solved);
            float upperAngle=ContinuousIKAngle(upperJoint,beforeUpper.angle,Vector2.SignedAngle(e-s,elbow-start));
            float lowerAngle=ContinuousIKAngle(lowerJoint,beforeLower.angle,Vector2.SignedAngle(hand-e,solved-elbow));
            // A short action-entry blend can still rotate a forearm carrying a
            // long prop by thirty degrees in one tick. Limit the actual joint
            // rotation, then rebuild the elbow/hand so the limb stays connected.
            Set(upperJoint,Vector2.Lerp(beforeUpper.position,start,weight),
                LimitArmTurn(upperJoint,Mathf.Lerp(beforeUpper.angle,upperAngle,weight),dt));
            Set(lowerJoint,Map(upperJoint,e),
                LimitArmTurn(lowerJoint,Mathf.Lerp(beforeLower.angle,lowerAngle,weight),dt));
        }
        float LimitArmTurn(Joint joint,float target,float dt)
        {
            if(!posed||dt<=0)return target;
            float previous=previousArmAngles[(int)joint-(int)Joint.LeftArm];
            return previous+Mathf.Clamp(Mathf.DeltaAngle(previous,target),-720*dt,720*dt);
        }
        float ContinuousIKAngle(Joint joint,float from,float target)
        {
            int index=(int)joint;
            if(!ikInitialized[index]){ikAngles[index]=from;ikInitialized[index]=true;}
            ikAngles[index]+=Mathf.DeltaAngle(ikAngles[index],target);
            return ikAngles[index];
        }
        static Vector2 HandArc(Vector2 hand,Vector2 shoulder,Vector2 target,float progress,float forward,float down)
        {
            float t=Mathf.Clamp01(progress),q=1-t;
            Vector2 control=shoulder+new Vector2(forward*Height,down*Height);
            return hand*(q*q)+control*(2*q*t)+target*(t*t)-hand;
        }
        float SoleHeight(Joint foot,float roll)
        {
            Vector2 min=foot==Joint.LeftFoot?leftSoleMin:rightSoleMin,max=foot==Joint.LeftFoot?leftSoleMax:rightSoleMax;
            float sine=Mathf.Sin(roll*Mathf.Deg2Rad),cosine=Mathf.Cos(roll*Mathf.Deg2Rad);
            return -(Mathf.Min(min.x*sine,max.x*sine)+Mathf.Min(min.y*cosine,max.y*cosine));
        }
        void Leg(Joint thigh,Joint shin,Joint footJoint,Vector2 root,Vector2 bindRoot,Vector2 knee,Vector2 ankle,Vector2 foot,Vector2 target,float roll,bool planted,bool grounded)
        {
            Vector2 ankleOffset=Rotate(ankle-foot,roll),targetAnkle=target+ankleOffset;
            Vector2 solvedKnee,solvedAnkle;
            float upper=Vector2.Distance(bindRoot,knee),lower=Vector2.Distance(knee,ankle);
            // A lifted short shin must not pass through the hip and flip the
            // whole knee solution. Reduce that leg's lift to its usable reach.
            if(!planted&&!Hovering)
                targetAnkle.y=Mathf.Min(targetAnkle.y,root.y-Mathf.Max(.06f,Mathf.Abs(upper-lower)+.025f));
            float floor=grounded?SoleHeight(footJoint,roll)+ankleOffset.y:float.NegativeInfinity;
            targetAnkle.y=Mathf.Max(targetAnkle.y,floor);
            float authoredSide=Cross(ankle-bindRoot,knee-bindRoot)>=0?1:-1;
            SolveTwoBone(root,targetAnkle,upper,lower,authoredSide,out solvedKnee,out solvedAnkle);
            if(((root-bindRoot)-(targetAnkle-ankle)).sqrMagnitude<.0000001f)
            {solvedKnee=knee+root-bindRoot;solvedAnkle=targetAnkle;}
            solvedAnkle.y=Mathf.Max(solvedAnkle.y,floor);
            Set(thigh,root,Vector2.SignedAngle(knee-bindRoot,solvedKnee-root));
            Set(shin,solvedKnee,Vector2.SignedAngle(ankle-knee,solvedAnkle-solvedKnee));
            Set(footJoint,solvedAnkle-ankleOffset,roll);
        }
        static void SupportRange(Vector2 root,Vector2 knee,Vector2 ankle,Vector2 target,float rootX,ref float minimum,ref float maximum)
        {
            float upper=Vector2.Distance(root,knee),lower=Vector2.Distance(knee,ankle);
            float length=upper+lower-.004f,vertical=Mathf.Abs(upper-lower)+.055f;
            float reach=Mathf.Sqrt(Mathf.Max(.01f,length*length-vertical*vertical));
            minimum=Mathf.Max(minimum,target.x-reach-rootX);
            maximum=Mathf.Min(maximum,target.x+reach-rootX);
        }
        static float StrideReach(Vector2 root,Vector2 knee,Vector2 ankle)
        {
            float length=LegLength(root,knee,ankle)-.004f;
            float height=Mathf.Max(.01f,root.y-ankle.y-.055f);
            return Mathf.Clamp(Mathf.Sqrt(Mathf.Max(.01f,length*length-height*height)),.14f,.32f);
        }
        static void SolveTwoBone(Vector2 start,Vector2 target,float upper,float lower,float side,out Vector2 middle,out Vector2 end)
        {
            Vector2 line=target-start;
            float distance=Mathf.Clamp(line.magnitude,Mathf.Abs(upper-lower)+.001f,upper+lower-.001f);
            Vector2 direction=line.sqrMagnitude>.000001f?line.normalized:Vector2.down;
            float along=(upper*upper-lower*lower+distance*distance)/(2*distance);
            float bend=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            middle=start+direction*along+new Vector2(-direction.y,direction.x)*bend*side;
            end=start+direction*distance;
        }
        static float ReachableHipOffset(Vector2 root,Vector2 bindHip,Vector2 knee,Vector2 foot,Vector2 target)
        {
            float length=LegLength(bindHip,knee,foot)-.002f,dx=target.x-root.x;
            return target.y+Mathf.Sqrt(Mathf.Max(.001f,length*length-dx*dx))-root.y;
        }
        static float LegLength(Vector2 h,Vector2 k,Vector2 f){return Vector2.Distance(h,k)+Vector2.Distance(k,f);}
        float HoverHeight(){return Mathf.Max(.10f,anatomy.groundY*unit)+.035f+Mathf.Sin(clock*3.8f)*.025f;}
        float WeaponMinimumY(Vector2 grip,float angle)
        {
            Vector2 a=grip+Rotate(RestWeaponBasePosition-RestWeaponGripPosition,angle);
            Vector2 b=grip+Rotate(RestWeaponTipPosition-RestWeaponGripPosition,angle);
            float r=angle*Mathf.Deg2Rad,x=WeaponTipRadii.x*Mathf.Sin(r),y=WeaponTipRadii.y*Mathf.Cos(r);
            return Mathf.Min(a.y,b.y-Mathf.Sqrt(x*x+y*y));
        }
        void Bind(Joint j,Vector2 rest){bones[(int)j]=new Bone{rest=rest,position=rest};}
        void Set(Joint j,Vector2 position,float angle){Bone b=bones[(int)j];b.position=position;b.angle=angle;bones[(int)j]=b;}
        Vector2 Point(float x,float y){return new Vector2(x-anatomy.groundX,y-anatomy.groundY)*unit;}
        Vector2 Map(Joint j,Vector2 p){Bone b=bones[(int)j];return b.position+Rotate(p-b.rest,b.angle);}
        static Vector2 Rotate(Vector2 p,float angle)
        {float r=angle*Mathf.Deg2Rad,c=Mathf.Cos(r),s=Mathf.Sin(r);return new Vector2(c*p.x-s*p.y,s*p.x+c*p.y);}
        static float Cross(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
        static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        {Vector2 d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.00001f,d.sqrMagnitude)));}
        static float Curve(float p,float rest,float prep,float hit,float follow,float contact)
        {
            float wind=contact*.48f,through=Mathf.Min(.75f,contact+.17f);
            if(p<wind)return Mathf.Lerp(rest,prep,Mathf.SmoothStep(0,1,p/wind));
            if(p<contact)return Mathf.Lerp(prep,hit,Mathf.SmoothStep(0,1,Mathf.InverseLerp(wind,contact,p)));
            if(p<through)return Mathf.Lerp(hit,follow,Mathf.SmoothStep(0,1,Mathf.InverseLerp(contact,through,p)));
            return Mathf.Lerp(follow,rest,Mathf.SmoothStep(0,1,Mathf.InverseLerp(through,.94f,p)));
        }
        static float CurveAngle(float p,float rest,float prep,float hit,float follow,float contact)
        {
            float lift=Mathf.Abs(Mathf.DeltaAngle(rest,prep)),strike=Mathf.Abs(Mathf.DeltaAngle(prep,hit));
            float wind=contact*Mathf.Clamp(lift/Mathf.Max(1,lift+strike),.18f,.72f);
            float through=Mathf.Min(.75f,contact+.17f);
            if(p<wind)return Mathf.LerpAngle(rest,prep,ArcEase(p/wind));
            if(p<contact)return Mathf.LerpAngle(prep,hit,ArcEase(Mathf.InverseLerp(wind,contact,p)));
            if(p<through)return Mathf.LerpAngle(hit,follow,ArcEase(Mathf.InverseLerp(contact,through,p)));
            return Mathf.LerpAngle(follow,rest,ArcEase(Mathf.InverseLerp(through,.94f,p)));
        }
        static float LeadCurve(float p,float rest,float prep,float hit,float follow,float contact)
        {
            // The hips initiate the strike before the prop reaches its windup.
            float wind=contact*.25f,braced=contact*.72f,through=Mathf.Min(.75f,contact+.17f);
            if(p<wind)return Mathf.Lerp(rest,prep,ArcEase(p/wind));
            if(p<braced)return Mathf.Lerp(prep,hit,ArcEase(Mathf.InverseLerp(wind,braced,p)));
            if(p<contact)return hit;
            if(p<through)return Mathf.Lerp(hit,follow,ArcEase(Mathf.InverseLerp(contact,through,p)));
            return Mathf.Lerp(follow,rest,ArcEase(Mathf.InverseLerp(through,.94f,p)));
        }
        static float ArcEase(float t)
        {
            // Short acceleration/deceleration shoulders preserve a readable arc
            // without SmoothStep's large midpoint speed spike on a long weapon.
            const float ramp=.10f,peak=1/(1-ramp);
            t=Mathf.Clamp01(t);
            if(t<ramp)return .5f*peak*t*t/ramp;
            if(t>1-ramp)return 1-.5f*peak*(1-t)*(1-t)/ramp;
            return peak*(t-ramp*.5f);
        }
        static Archetype WeaponType(int index)
        {
            switch(index)
            {
                case 100:case 103:case 114:case 123:case 130:case 135:case 141:case 143:case 147:return Archetype.Sword;
                case 106:case 108:case 110:case 115:case 124:case 126:case 127:case 128:case 139:case 140:case 142:case 149:return Archetype.Spear;
                case 111:case 118:case 119:case 144:case 146:return Archetype.Hammer;
                case 109:case 131:return Archetype.Bow;
                case 104:case 105:case 116:case 137:return Archetype.Orb;
                case 112:case 145:return Archetype.Dual;
                default:return Archetype.Staff;
            }
        }
        static Profile DefaultProfile(int index,float w,float h)
        {
            return new Profile{artIndex=index,groundX=w*.55f,groundY=h*.035f,bodyHeight=h*.86f,
                hipX=w*.55f,hipY=h*.38f,shoulderX=w*.56f,shoulderY=h*.64f,headX=w*.57f,headY=h*.8f,
                leftKneeX=w*.43f,leftKneeY=h*.21f,rightKneeX=w*.65f,rightKneeY=h*.21f,
                leftFootX=w*.4f,leftFootY=h*.035f,rightFootX=w*.67f,rightFootY=h*.035f,
                leftHandX=w*.33f,leftHandY=h*.36f,rightHandX=w*.77f,rightHandY=h*.48f};
        }
        void OnDestroy(){if(model!=null)model.Dispose();}
    }
}

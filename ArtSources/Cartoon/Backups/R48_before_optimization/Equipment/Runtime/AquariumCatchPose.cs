using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototypeLab.Aquarium
{
    // Uses the diver's existing continuous sleeve/palm mesh and bones for the receiving hand.
    // R31: the stow is a cartoon throw. Seconds s run from the moment the spear seats (CollectSeconds = 1.25):
    //   -.45  the free hand shoots out, fingers spread wide, and waggles "gimme" while the spear comes in
    //    0    the catch pops off the spear and zips into the hand (.12 s), the fingers snap shut, the glove
    //         pops bigger, the body squashes and the diver dips with the impact
    //   .16   wind-up: the catch is dragged down and across the front, torso coiled the other way (held .40)
    //   .40   whoosh up and over the left shoulder (smeared along the swing), let go at .55 with the
    //         fingers flicking open, arm follows through
    //   .55   the catch tumbles in an arc into the sack on the hip (open wide to receive), plops in at .86
    //         and the sack bounces; the hand pats it once and the diver settles back
    // First person shows the same beats from the eyes: hand up from below, snap, wind-up low left, then
    // yanked up and away over the left shoulder, out of view.
    public sealed class AquariumCatchPose : IDisposable
    {
        private readonly AquariumRig rig;
        private readonly AquariumFingers fingers;
        public Transform Hand { get; private set; }
        public bool Visible { get; private set; }
        // The palm middle of the receiving hand: where the catch is held.
        public Vector3 Grip { get { return fingers != null ? fingers.Grip : Hand.position; } }
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
            // The gun hand mesh is a right hand; mirrored by the rig's scale it reads as the free left hand.
            fingers=AquariumFingers.Attach(rig,"HandL",true);
            rig.gameObject.SetActive(false);
        }

        // ---- first person -----------------------------------------------------------------------------------

        public void Present(Harpoon shot,float time)
        {
            float s=shot!=null&&shot.stowedAt>0?time-shot.retrievedAt:-10;
            Visible=s>=-.45f&&s<.62f;
            rig.gameObject.SetActive(Visible);if(!Visible)return;
            rig.Clip("FPSIdle").SampleAnimation(rig.gameObject,time%2);
            if(Hand!=null)Hand.localScale*=T1(HandPop,s);
            if(fingers!=null)fingers.Pose(Curl(s),T1(Spread,s),T1(Wiggle,s),time);
            rig.transform.localScale=new Vector3(-1,1,1);
            rig.transform.localRotation=Quaternion.Euler(T3(FirstPersonTurn,s));
            rig.transform.localPosition=Vector3.zero;
            if(rig.transform.parent!=null)rig.transform.localPosition=T3(FirstPersonGrip,s)-rig.transform.parent.InverseTransformPoint(Grip);
        }

        // The catch seen from the diver's own eyes: zips from the spear into the hand, is shown off ahead-left,
        // then trails the hand's yank out of view (gone by .62).
        public static void PresentFirstPersonCatch(Transform view,Vector3 grip,Transform caught,float radius,Vector3 seated,float s)
        {
            if(caught==null)return;
            float carry=Mathf.Min(1,.25f/Mathf.Max(.05f,radius));
            float r=radius*Mathf.Lerp(1,carry,Ease(Mathf.InverseLerp(0,.14f,s)));
            var velocity=view.TransformVector((T3(FirstPersonGrip,s+.012f)-T3(FirstPersonGrip,s-.012f))/.024f);
            float speed=velocity.magnitude;var along=speed>.01f?velocity/speed:view.forward;
            // Held up by the tail, nose ahead and a little up; dragged behind the hand while it moves fast.
            var shown=view.TransformDirection(new Vector3(-.25f,.40f,.88f).normalized);
            float trail=Mathf.Clamp01((speed-1)/3);
            var hang=Vector3.Slerp(shown,-along,trail);
            var held=grip+hang*(r*.75f);
            Vector3 position;
            if(s<.12f)position=Hop(seated,held,view.up,s/.12f);
            else if(s<Release)position=held;
            else
            {
                // Let go off screen: keeps flying along the swing.
                var release=view.TransformVector((T3(FirstPersonGrip,Release+.012f)-T3(FirstPersonGrip,Release-.012f))/.024f);
                position=held+release*(s-Release);
                r*=1-Ease(Mathf.InverseLerp(Release,.62f,s));
            }
            var up=Mathf.Abs(Vector3.Dot(hang,view.up))>.95f?view.forward:view.up;
            var rotation=Quaternion.LookRotation(hang,up);
            caught.position=position;
            caught.rotation=Quaternion.Slerp(caught.rotation,rotation,Ease(s/.14f));
            caught.localScale=Squash(Vector3.one*Mathf.Max(.001f,r/Mathf.Max(.05f,radius)),s,speed);
        }

        // ---- third person -----------------------------------------------------------------------------------

        // Poses the diver (body, head, free arm, wrist, fingers), the sack and, once the spear has seated, the
        // catch. Called after the frame's clip pose; everything here is added on top of it. caught may be null.
        public static void PresentStow(AquariumRig rig,AquariumFingers fingers,Transform pouch,Transform caught,float radius,Vector3 seated,float s)
        {
            if(rig==null)return;
            float w=StowWeight(s);
            var basis=rig.transform;
            if(pouch!=null)PresentPouch(pouch,PouchSquash(s));
            if(w>0)
            {
                var body=Bone(rig,"Body");var head=Bone(rig,"Head");var hand=Bone(rig,"HandR");
                if(body!=null)body.localRotation=body.localRotation*Quaternion.Euler(T3(BodyTurn,s)*w);
                if(head!=null)head.localRotation=head.localRotation*Quaternion.Euler(T3(HeadTurn,s)*w);
                if(hand!=null)
                {
                    hand.localScale*=Mathf.Lerp(1,T1(HandPop,s),w);
                    var gripTarget=basis.TransformPoint(T3(HandPath,s));
                    var desired=hand.rotation;var offset=Vector3.zero;
                    if(fingers!=null)
                    {
                        var fw=basis.TransformDirection(T3(FingerAim,s));var nw=basis.TransformDirection(T3(PalmAim,s));
                        desired=Quaternion.LookRotation(fw,nw)*Quaternion.Inverse(Quaternion.LookRotation(fingers.FingerLocal,fingers.PalmLocal));
                        offset=desired*Vector3.Scale(hand.lossyScale,fingers.GripLocal);
                    }
                    SolveArm(rig,gripTarget-offset,w,basis.TransformDirection(T3(ElbowAim,s)));
                    if(fingers!=null)hand.rotation=Quaternion.Slerp(hand.rotation,desired,w*.9f);
                }
                if(fingers!=null)fingers.Pose(Mathf.Lerp(0,Curl(s),w),T1(Spread,s)*w,T1(Wiggle,s)*w,s);
            }
            if(caught==null||s<0)return;
            PlaceCatch(basis,fingers!=null?fingers.Grip:(Bone(rig,"HandR")!=null?Bone(rig,"HandR").position:basis.position),pouch,caught,radius,seated,s);
        }

        private static void PlaceCatch(Transform basis,Vector3 grip,Transform pouch,Transform caught,float radius,Vector3 seated,float s)
        {
            float carry=Mathf.Min(1,.32f/Mathf.Max(.05f,radius));
            float r=radius*Mathf.Lerp(1,carry,Ease(Mathf.InverseLerp(0,.14f,s)));
            float scale=r/Mathf.Max(.05f,radius);
            Vector3 position;Quaternion rotation;float speed;
            if(s<Release)
            {
                var velocity=basis.TransformVector((T3(HandPath,s+.012f)-T3(HandPath,s-.012f))/.024f);
                speed=velocity.magnitude;
                var hang=HoldHang(basis,velocity);
                var held=grip+hang*(r*.62f);
                position=s<.12f?Hop(seated,held,basis.up,s/.12f):held;
                var up=Mathf.Abs(Vector3.Dot(hang,basis.up))>.95f?basis.forward:basis.up;
                rotation=Quaternion.Slerp(caught.rotation,Quaternion.LookRotation(hang,up),Ease(s/.14f));
                if(s<.12f)speed=Mathf.Min(6,Vector3.Distance(seated,held)/.12f);
            }
            else
            {
                // Thrown: tossed high over the left shoulder (above the helmet), tumbling, down into the sack's mouth.
                var releaseVelocity=basis.TransformVector((T3(HandPath,Release+.012f)-T3(HandPath,Release-.012f))/.024f);
                var releaseHang=HoldHang(basis,releaseVelocity);
                var start=basis.TransformPoint(T3(HandPath,Release))+releaseHang*(r*.5f);
                var mouth=pouch!=null?pouch.position+pouch.up*.10f:basis.TransformPoint(new Vector3(-.56f,-.40f,-.28f));
                var top=basis.TransformPoint(new Vector3(-.78f,1.9f,-.30f));
                float u=Mathf.Clamp01((s-Release)/(Plop-Release));
                position=Bezier(start,top,mouth,u);
                var tangent=(mouth-top)*u+(top-start)*(1-u);speed=tangent.magnitude*2/(Plop-Release)*.35f;
                var up=Mathf.Abs(Vector3.Dot(releaseHang,basis.up))>.95f?basis.forward:basis.up;
                rotation=Quaternion.AngleAxis(-540*Ease(u),basis.right)*Quaternion.LookRotation(releaseHang,up);
                scale*=Mathf.Lerp(1,.55f,Ease(u));
                // Plop: sinks through the rim and is gone.
                float plop=Ease(Mathf.InverseLerp(Plop,Plop+.06f,s));
                if(pouch!=null)position=Vector3.Lerp(position,mouth-pouch.up*.12f,plop);
                scale*=1-plop;
            }
            caught.position=position;caught.rotation=rotation;
            caught.localScale=Squash(Vector3.one*Mathf.Max(.001f,scale),s,speed);
        }
        // Hanging off the hand by the tail: low and ahead while still, dragged behind the hand while it swings.
        private static Vector3 HoldHang(Transform basis,Vector3 velocity)
        {
            float speed=velocity.magnitude;
            var rest=basis.TransformDirection(new Vector3(.15f,-.55f,.82f).normalized);
            return speed>.01f?Vector3.Slerp(rest,-velocity/speed,Mathf.Clamp01((speed-1)/3)):rest;
        }
        // From the spear to the hand: a quick hop that speeds up into the palm.
        private static Vector3 Hop(Vector3 from,Vector3 to,Vector3 up,float u)
        {
            u=Mathf.Clamp01(u);float e=u*u*(3-2*u);
            return Vector3.Lerp(from,to,e)+up*(Mathf.Sin(u*Mathf.PI)*.16f);
        }
        // Squash on the grab, then smear along the body (local z) with speed; volume is kept.
        private static Vector3 Squash(Vector3 scale,float s,float speed)
        {
            float grab=s>=.12f?-.28f*Mathf.Exp(-(s-.12f)*16)*Mathf.Cos((s-.12f)*38):0;
            float smear=Mathf.Clamp(speed*.08f,0,.45f);
            float along=Mathf.Max(.4f,1+grab+smear);float across=1/Mathf.Sqrt(along);
            return new Vector3(scale.x*across,scale.y*across,scale.z*along);
        }
        // The sack opens a little to receive, then bounces from the plop.
        private static float PouchSquash(float s)
        {
            if(s<.78f)return 0;
            if(s<Plop)return -.18f*Ease(Mathf.InverseLerp(.78f,.89f,s));
            float tau=s-Plop;
            return -.18f*Mathf.Exp(-tau*30)+.42f*Mathf.Exp(-tau*7)*Mathf.Sin(tau*30);
        }
        // How much of the free arm the stow takes: in as the spear comes, back to the swim pose at the end.
        public static float StowWeight(float s)
        {
            return Ease(Mathf.InverseLerp(-.45f,-.28f,s))*(1-Ease(Mathf.InverseLerp(1.05f,1.24f,s)));
        }
        private static float Curl(float s){return Mathf.Clamp(T1(Fist,s),-.45f,1.1f);}
        // Let go of the catch / catch lands in the sack (seconds after the spear seats).
        private const float Release=.53f,Plop=.92f;

        // ---- the beats ----------------------------------------------------------------------------------------
        // Hand (grip point) path in the diver's own space: x right, y up, z forward; the free hand is the left (-x).
        private static readonly Key[] HandPath=
        {
            K(-.45f,-.30f,.00f,.40f),K(-.30f,-.30f,.00f,.40f),K(-.18f,-.27f,-.05f,.37f,true),K(.06f,-.27f,-.05f,.37f,true),
            K(.12f,-.25f,-.06f,.40f),K(.16f,-.23f,-.16f,.34f,true),K(.24f,-.26f,-.10f,.33f),
            K(.40f,-.10f,-.30f,.30f,true),K(.47f,-.50f,-.05f,.10f),K(.53f,-.42f,.28f,-.08f),K(.62f,-.32f,.34f,-.22f,true),
            K(.80f,-.48f,.05f,-.32f),K(.94f,-.55f,-.18f,-.28f),K(1.00f,-.56f,-.30f,-.27f,true),K(1.06f,-.53f,-.21f,-.25f,true),K(1.25f,-.45f,-.25f,-.05f)
        };
        // Where the fingers point and the palm faces (diver space) at each beat.
        private static readonly Key[] FingerAim=
        {
            K(-.45f,0,1,0),K(-.18f,0,1,0),K(.06f,0,1,0),K(.16f,.2f,-.3f,.93f),K(.40f,.3f,-.5f,.8f),K(.47f,-.3f,.5f,.6f),
            K(.53f,0,.6f,-.8f),K(.62f,-.2f,.4f,-.9f),K(.90f,-.2f,-.1f,-1),K(1.25f,-.2f,-.1f,-1)
        };
        private static readonly Key[] PalmAim=
        {
            K(-.45f,.55f,0,.83f),K(-.18f,.55f,0,.83f),K(.06f,.55f,0,.83f),K(.16f,.5f,-.85f,-.15f),K(.40f,.3f,-.8f,-.5f),K(.47f,-.6f,-.5f,-.4f),
            K(.53f,0,-.3f,-.95f),K(.62f,0,-.9f,-.3f),K(.90f,0,-1,0),K(1.25f,0,-1,0)
        };
        // Which way the elbow points while the arm works: down and out, then out to the side for the throw.
        private static readonly Key[] ElbowAim=
        {
            K(-.45f,-.6f,-1,0),K(.24f,-.6f,-1,.1f),K(.40f,-.4f,-1,.3f),K(.47f,-1,-.3f,0),K(.53f,-1,.2f,-.3f),K(.62f,-1,.1f,-.4f),K(.90f,-.7f,-.6f,.2f),K(1.25f,-.7f,-.6f,.2f)
        };
        // Whole-body acting (degrees, pitch/yaw/roll on the Body bone): lean in, coil right, throw left, settle.
        private static readonly Key[] BodyTurn=
        {
            K(-.45f,0,0,0),K(-.18f,6,5,0,true),K(.06f,6,5,0,true),K(.16f,11,6,-2,true),K(.24f,8,8,-2),K(.40f,12,12,-5,true),K(.47f,4,0,0),
            K(.53f,-10,-12,5),K(.62f,-12,-15,6,true),K(.80f,-4,-10,3),K(1.00f,8,-8,-2,true),K(1.12f,-2,-3,0),K(1.25f,0,0,0)
        };
        // The helmet watches the catch: in, down at it, back over the shoulder, into the sack.
        private static readonly Key[] HeadTurn=
        {
            K(-.45f,0,0,0),K(-.18f,10,10,0),K(.16f,18,6,0),K(.40f,22,8,0,true),K(.53f,-15,-15,0),K(.66f,-30,-25,0),K(.85f,5,-30,0),K(.96f,25,-28,0,true),K(1.10f,10,-12,0),K(1.25f,0,0,0)
        };
        // Finger curl (x): wide open, waggle, snap shut, hold, flick open on the release, relax, flat for the pat.
        private static readonly Key[] Fist=
        {
            K(-.45f,.10f,0,0),K(-.30f,-.40f,0,0,true),K(-.18f,-.30f,0,0),K(.08f,-.30f,0,0),K(.11f,-.42f,0,0,true),K(.14f,1.05f,0,0,true),K(.20f,.85f,0,0),
            K(.40f,.98f,0,0),K(.50f,.98f,0,0,true),K(.545f,-.38f,0,0,true),K(.64f,0,0,0),K(.92f,.25f,0,0),K(1.00f,.02f,0,0,true),K(1.12f,.20f,0,0),K(1.25f,.10f,0,0)
        };
        private static readonly Key[] Spread=
        {
            K(-.45f,0,0,0),K(-.30f,1,0,0,true),K(.11f,1,0,0,true),K(.14f,.1f,0,0,true),K(.50f,.1f,0,0,true),K(.545f,1,0,0,true),K(.70f,.6f,0,0),K(.98f,.3f,0,0),K(1.25f,0,0,0)
        };
        private static readonly Key[] Wiggle={K(-.28f,0,0,0,true),K(-.16f,.18f,0,0,true),K(.05f,.18f,0,0,true),K(.10f,0,0,0,true)};
        // The glove pops bigger on the grab and the flick, and squashes on the pat.
        private static readonly Key[] HandPop=
        {
            K(-.45f,1,0,0),K(.12f,1,0,0,true),K(.145f,1.22f,0,0,true),K(.20f,.94f,0,0,true),K(.27f,1,0,0,true),K(.51f,1,0,0,true),K(.545f,1.15f,0,0,true),
            K(.63f,.98f,0,0,true),K(.70f,1,0,0,true),K(.98f,1,0,0,true),K(1.005f,.9f,0,0,true),K(1.06f,1.04f,0,0,true),K(1.12f,1,0,0,true)
        };
        // First person: the grip point in camera space, and the hand's own turn.
        private static readonly Key[] FirstPersonGrip=
        {
            K(-.45f,-.14f,-.62f,.40f),K(-.28f,-.21f,-.22f,.60f),K(-.18f,-.20f,-.27f,.62f,true),K(.06f,-.20f,-.27f,.62f,true),K(.12f,-.19f,-.25f,.64f),
            K(.16f,-.20f,-.34f,.61f,true),K(.24f,-.22f,-.30f,.61f),K(.40f,-.30f,-.45f,.55f,true),K(.47f,-.50f,-.22f,.46f),K(.55f,-.70f,.30f,.18f),K(.62f,-.80f,.55f,-.10f)
        };
        private static readonly Key[] FirstPersonTurn=
        {
            K(-.45f,25,0,0),K(-.18f,0,0,0,true),K(.06f,0,0,0,true),K(.16f,8,0,-5,true),K(.40f,14,10,15,true),K(.55f,-25,-30,-40),K(.62f,-35,-40,-50)
        };

        private struct Key
        {
            public float t;public Vector3 v;public bool flat;
        }
        private static Key K(float t,float x,float y,float z,bool flat=false){return new Key{t=t,v=new Vector3(x,y,z),flat=flat};}
        private static float T1(Key[] k,float s){return T3(k,s).x;}
        // Cubic Hermite through the keys (Catmull-Rom tangents; flat keys ease in and out of a hold).
        private static Vector3 T3(Key[] k,float s)
        {
            int n=k.Length;if(s<=k[0].t)return k[0].v;if(s>=k[n-1].t)return k[n-1].v;
            int i=0;while(i<n-2&&s>k[i+1].t)i++;
            float dt=k[i+1].t-k[i].t,u=(s-k[i].t)/dt,u2=u*u,u3=u2*u;
            var m0=Tangent(k,i);var m1=Tangent(k,i+1);
            return (2*u3-3*u2+1)*k[i].v+(u3-2*u2+u)*dt*m0+(-2*u3+3*u2)*k[i+1].v+(u3-u2)*dt*m1;
        }
        private static Vector3 Tangent(Key[] k,int i)
        {
            if(k[i].flat||i==0||i==k.Length-1)return Vector3.zero;
            return (k[i+1].v-k[i-1].v)/(k[i+1].t-k[i-1].t);
        }
        private static Transform Bone(AquariumRig rig,string name){return Array.Find(rig.Bones,b=>b.name==name);}

        // Two-bone arm solve for the free left arm ("R" chain) with a chosen elbow direction.
        private static Transform SolveArm(AquariumRig rig,Vector3 target,float blend,Vector3 elbowAim)
        {
            var upper=Bone(rig,"UpperArmR");var lower=Bone(rig,"ForearmR");var hand=Bone(rig,"HandR");
            if(upper==null||lower==null||hand==null)return null;
            var start=upper.position;float a=Vector3.Distance(start,lower.position),b=Vector3.Distance(lower.position,hand.position);
            var wanted=Vector3.Lerp(hand.position,target,Mathf.Clamp01(blend));var delta=wanted-start;
            float d=Mathf.Clamp(delta.magnitude,.025f,Mathf.Max(.03f,(a+b)*.98f));var axis=delta.sqrMagnitude>.0001f?delta.normalized:rig.transform.forward;
            var pole=Vector3.ProjectOnPlane(elbowAim,axis);
            pole=pole.sqrMagnitude>.0001f?pole.normalized:Vector3.ProjectOnPlane(-rig.transform.up,axis).normalized;
            float along=(a*a-b*b+d*d)/(2*d),height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            var elbow=start+axis*along+pole*height;var end=start+axis*d;
            upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,end-lower.position)*lower.rotation;
            return hand;
        }

        // R30: a small rope-weave catch sack on the diver's left hip, behind the tank: where the catch goes.
        public static readonly Vector3 PouchCentre=new Vector3(-.56f,-.50f,-.28f);
        private static float Ease(float u){u=Mathf.Clamp01(u);return u*u*(3-2*u);}
        private static Vector3 Bezier(Vector3 a,Vector3 c,Vector3 b,float u){float v=1-u;return a*(v*v)+c*(2*v*u)+b*(u*u);}
        public static Transform CreatePouch(AquariumRig rig)
        {
            var body=Array.Find(rig.Bones,b=>b.name=="Body");
            var material=Resources.Load<Material>("AquariumEquipment/Rope");
            if(body==null||material==null)return null;
            var pouch=new GameObject("Catch pouch").transform;pouch.SetParent(body,false);
            pouch.position=rig.transform.TransformPoint(PouchCentre);pouch.rotation=rig.transform.rotation*Quaternion.Euler(0,-20,8);
            const int seg=18;
            if(sackMesh==null)BuildPouchMeshes(seg);
            var sack=new GameObject("Sack");sack.transform.SetParent(pouch,false);
            sack.AddComponent<MeshFilter>().sharedMesh=sackMesh;var renderer=sack.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            var tint=new MaterialPropertyBlock();tint.SetColor("_Color",new Color(.62f,.56f,.42f));renderer.SetPropertyBlock(tint);
            // The dark mouth so the open top reads as a bag, and a thick rope rim with a short tie to the belt.
            var cap=new GameObject("Mouth");cap.transform.SetParent(pouch,false);cap.AddComponent<MeshFilter>().sharedMesh=mouthMesh;var capRenderer=cap.AddComponent<MeshRenderer>();capRenderer.sharedMaterial=material;
            var dark=new MaterialPropertyBlock();dark.SetColor("_Color",new Color(.08f,.07f,.06f));capRenderer.SetPropertyBlock(dark);
            var rim=new GameObject("Rim").AddComponent<LineRenderer>();rim.transform.SetParent(pouch,false);
            rim.sharedMaterial=material;rim.useWorldSpace=false;rim.loop=true;rim.positionCount=seg;rim.widthMultiplier=.03f;rim.textureMode=LineTextureMode.Tile;
            for(int j=0;j<seg;j++){float a=j*Mathf.PI*2/seg;rim.SetPosition(j,new Vector3(Mathf.Cos(a)*.135f,.12f,Mathf.Sin(a)*.135f));}
            var tie=new GameObject("Tie").AddComponent<LineRenderer>();tie.transform.SetParent(pouch,false);
            tie.sharedMaterial=material;tie.useWorldSpace=false;tie.positionCount=3;tie.widthMultiplier=.022f;tie.textureMode=LineTextureMode.Tile;
            tie.SetPosition(0,new Vector3(.12f,.12f,0));tie.SetPosition(1,new Vector3(.17f,.2f,.02f));tie.SetPosition(2,new Vector3(.2f,.25f,.05f));
            foreach(var r2 in pouch.GetComponentsInChildren<Renderer>(true)){r2.shadowCastingMode=ShadowCastingMode.On;r2.receiveShadows=true;}
            return pouch;
        }
        // Shared by every diver: a lathed sack (open rim at the top, fullest just below it, rounded bottom) and its mouth.
        private static Mesh sackMesh,mouthMesh;
        private static void BuildPouchMeshes(int seg)
        {
            float[] r={.13f,.17f,.185f,.18f,.145f,.085f,0};float[] y={.12f,.07f,0,-.07f,-.14f,-.195f,-.215f};
            var vertices=new Vector3[r.Length*(seg+1)];var uv=new Vector2[vertices.Length];var tris=new System.Collections.Generic.List<int>();
            for(int i=0;i<r.Length;i++)for(int j=0;j<=seg;j++)
            {
                float a=j*Mathf.PI*2/seg;int k=i*(seg+1)+j;
                vertices[k]=new Vector3(Mathf.Cos(a)*r[i],y[i],Mathf.Sin(a)*r[i]);uv[k]=new Vector2(j*4f/seg,i*2.2f/(r.Length-1));
                if(i>0&&j<seg){int p=k-(seg+1);tris.Add(p);tris.Add(p+1);tris.Add(k);tris.Add(p+1);tris.Add(k+1);tris.Add(k);}
            }
            sackMesh=new Mesh{name="Catch pouch",hideFlags=HideFlags.DontSave};sackMesh.vertices=vertices;sackMesh.uv=uv;sackMesh.SetTriangles(tris,0);sackMesh.RecalculateNormals();sackMesh.RecalculateBounds();
            var mv=new Vector3[seg+1];var mt=new System.Collections.Generic.List<int>();
            mv[seg]=new Vector3(0,.105f,0);for(int j=0;j<seg;j++){float a=j*Mathf.PI*2/seg;mv[j]=new Vector3(Mathf.Cos(a)*.125f,.105f,Mathf.Sin(a)*.125f);mt.Add(seg);mt.Add((j+1)%seg);mt.Add(j);}
            mouthMesh=new Mesh{name="Catch pouch mouth",hideFlags=HideFlags.DontSave};mouthMesh.vertices=mv;mouthMesh.SetTriangles(mt,0);mouthMesh.RecalculateNormals();
        }
        // R31: squash > 0 flattens the sack (a catch landing in it), < 0 stretches it open; 0 is at rest.
        public static void PresentPouch(Transform pouch,float squash)
        {
            if(pouch==null)return;
            pouch.localScale=new Vector3(1+.25f*squash,Mathf.Max(.4f,1-.35f*squash),1+.25f*squash);
        }

        // R27: the free left hand pushing a drain lid back, seen from the diver's own eyes: low in front,
        // palm down on the lid, shoving in slow strokes.
        public void PresentPush(float time,float weight)
        {
            Visible=weight>.01f;
            rig.gameObject.SetActive(Visible);if(!Visible)return;
            rig.Clip("FPSIdle").SampleAnimation(rig.gameObject,time%2);
            if(fingers!=null)fingers.Pose(-.15f,.35f);
            float stroke=.5f-.5f*Mathf.Cos(time*2*Mathf.PI*.9f);
            var rest=new Vector3(-.08f,-.34f,-.12f);
            var shove=new Vector3(.03f,-.10f,.10f)+new Vector3(0,-.025f,.11f)*stroke;
            rig.transform.localPosition=Vector3.Lerp(rest,shove,weight);
            rig.transform.localRotation=Quaternion.Euler(Mathf.Lerp(0,24+6*stroke,weight),Mathf.Lerp(0,-8,weight),0);
            rig.transform.localScale=new Vector3(-1,1,1);
        }

        // R27: the same two-bone solve for the free left arm (the right hand keeps the gun).
        public static Transform SolveLeft(AquariumRig rig,Vector3 target,float blend)
        {
            var upper=Array.Find(rig.Bones,b=>b.name=="UpperArmL");
            var lower=Array.Find(rig.Bones,b=>b.name=="ForearmL");
            var hand=Array.Find(rig.Bones,b=>b.name=="HandL");
            if(upper==null||lower==null||hand==null)return null;
            var start=upper.position;float a=Vector3.Distance(start,lower.position),b=Vector3.Distance(lower.position,hand.position);
            var wanted=Vector3.Lerp(hand.position,target,Mathf.Clamp01(blend));var delta=wanted-start;
            float d=Mathf.Clamp(delta.magnitude,.025f,Mathf.Max(.03f,(a+b)*.98f));var axis=delta.sqrMagnitude>.0001f?delta.normalized:rig.transform.forward;
            var pole=Vector3.ProjectOnPlane(-rig.transform.up+rig.transform.right*.45f,axis).normalized;
            float along=(a*a-b*b+d*d)/(2*d),height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            var elbow=start+axis*along+pole*height;var end=start+axis*d;
            upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,end-lower.position)*lower.rotation;
            return hand;
        }

        public void Dispose(){Destroy(rig!=null?rig.gameObject:null);}
        private static void Destroy(UnityEngine.Object o){if(o==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
    }
}

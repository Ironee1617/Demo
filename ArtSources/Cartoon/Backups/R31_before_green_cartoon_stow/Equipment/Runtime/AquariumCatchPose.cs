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

        // R30: the stow, seen from the diver's own eyes. The free hand comes up from below as the spear seats and
        // waits a little ahead and to the left; the catch arrives into it, is pulled out to the left side and then
        // down past the hip, out of view (the third-person view shows it going into the sack on the back). The
        // hand is placed so its palm sits on the body's near side all the way.
        public void Present(Harpoon shot,float time)
        {
            Visible=shot!=null&&shot.stowedAt>0&&time>=shot.retrievedAt-.45f&&time<shot.stowedAt;
            rig.gameObject.SetActive(Visible);if(!Visible)return;
            rig.Clip("FPSIdle").SampleAnimation(rig.gameObject,time%2);
            float seconds=time-shot.retrievedAt,t=Mathf.Max(0,seconds/AquariumModel.CollectSeconds);
            float reach=Ease(Mathf.InverseLerp(-.45f,0,seconds));
            var target=Vector3.Lerp(new Vector3(-.10f,-.60f,.25f),FirstPersonBody(t)+FirstPersonGrip,reach);
            rig.transform.localRotation=Quaternion.Euler(0,Mathf.Lerp(0,-28,Ease((t-.45f)/.40f)),0);
            rig.transform.localScale=new Vector3(-1,1,1);
            rig.transform.localPosition=Vector3.zero;
            if(Hand!=null&&rig.transform.parent!=null)rig.transform.localPosition=target-rig.transform.parent.InverseTransformPoint(Hand.position);
        }
        // Camera-space path of the carried body (t over CollectSeconds): held ahead-left, out to the side, down and gone.
        public static readonly Vector3 FirstPersonHold=new Vector3(-.20f,-.22f,.80f),FirstPersonSide=new Vector3(-.62f,-.05f,.46f),FirstPersonOut=new Vector3(-.74f,-.55f,-.20f);
        // Where the palm sits relative to the body's centre: under it, on the near side.
        public static readonly Vector3 FirstPersonGrip=new Vector3(.10f,-.15f,-.14f);
        public static Vector3 FirstPersonBody(float t)
        {
            if(t<.25f)return FirstPersonHold;
            if(t<.55f)return Bezier(FirstPersonHold,new Vector3(-.46f,-.02f,.78f),FirstPersonSide,Ease((t-.25f)/.30f));
            return Bezier(FirstPersonSide,new Vector3(-.84f,-.14f,.22f),FirstPersonOut,Ease((t-.55f)/.30f));
        }

        // R30: the third-person stow, in the diver's own space (x right, y up, z forward). The free arm is the
        // rig's "R" chain on the diver's left. t runs over CollectSeconds from the moment the spear seats.
        public static readonly Vector3 StowGrab=new Vector3(-.26f,-.10f,.36f),StowOver=new Vector3(-.34f,.30f,-.06f),StowDrop=new Vector3(-.50f,-.28f,-.25f);
        public static readonly Vector3 PouchCentre=new Vector3(-.56f,-.50f,-.28f);
        private static float Ease(float u){u=Mathf.Clamp01(u);return u*u*(3-2*u);}
        private static Vector3 Bezier(Vector3 a,Vector3 c,Vector3 b,float u){float v=1-u;return a*(v*v)+c*(2*v*u)+b*(u*u);}
        public static Vector3 StowHand(float t)
        {
            if(t<.25f)return StowGrab;
            if(t<.62f)return Bezier(StowGrab,new Vector3(-.52f,.24f,.30f),StowOver,Ease((t-.25f)/.37f));
            return Bezier(StowOver,new Vector3(-.56f,.10f,-.36f),StowDrop,Ease((t-.62f)/.23f));
        }
        // Which way the carried body hangs from the hand: ahead and low, then down the back, then into the pouch.
        public static Vector3 StowHang(float t)
        {
            var front=new Vector3(.10f,-.30f,1f).normalized;var back=new Vector3(.05f,-.45f,-1f).normalized;var down=new Vector3(0,-1,-.12f).normalized;
            if(t<.25f)return front;
            if(t<.62f)return Vector3.Slerp(front,back,Ease((t-.25f)/.37f));
            return Vector3.Slerp(back,down,Ease((t-.62f)/.23f));
        }
        // The free arm's pull toward the stow path: reaches in as the spear seats, lets go after the drop.
        public static float StowWeight(float seconds)
        {
            float t=seconds/AquariumModel.CollectSeconds;
            return Ease(Mathf.InverseLerp(-.45f,.05f,seconds))*(1-Ease(Mathf.InverseLerp(.86f,.99f,t)));
        }

        // R30: a small rope-weave catch sack on the diver's left hip, behind the tank: where the catch goes.
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
        // A catch going in makes the sack swell and settle.
        public static void PresentPouch(Transform pouch,float gulp)
        {
            if(pouch==null)return;
            float g=Mathf.Clamp01(gulp),swell=Mathf.Sin(g*Mathf.PI)*(1-.35f*g);
            pouch.localScale=new Vector3(1+.22f*swell,1-.10f*swell,1+.22f*swell);
        }

        // R27: the free left hand pushing a drain lid back, seen from the diver's own eyes: low in front,
        // palm down on the lid, shoving in slow strokes.
        public void PresentPush(float time,float weight)
        {
            Visible=weight>.01f;
            rig.gameObject.SetActive(Visible);if(!Visible)return;
            rig.Clip("FPSIdle").SampleAnimation(rig.gameObject,time%2);
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

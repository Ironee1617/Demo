using UnityEngine;
namespace PrototypeLab.Aquarium
{
    public static class AquariumFeeding
    {
        // R18 fish disguise: a fast, greedy gulp. The carrier turns and lunges onto a
        // meal that stays where it was caught, sucks it in within ~0.6 s, then chomps.
        public const float AlignmentEnd=.30f, SuctionStart=.30f, SuctionEnd=.88f;
        public const float MouthClosed=1.00f, ReturnToFish=1.40f, ChewStart=1.02f;
        public const float ClosingSilhouette=.95f;
        // The meal is fully inside the sealed mouth; it is no longer drawn after this.
        public const float FishMealVisible=1.05f;
        // R18 alien form: the authored 3 s tentacle meal plays AlienPace times faster.
        // Alignment/suction constants below are in authored clip time.
        public const float AlienPace=1.32f;
        public const float AlienAlignmentEnd=.45f, AlienSuctionStart=1.18f, AlienSuctionEnd=1.98f, ExpulsionEnd=.90f;
        public const float AlienMealVisible=1.85f;
        // The carrier glides from its meal pose back onto its authoritative body.
        public const float MealSettle=.42f;
        public static float MealVisibleSeconds(string eaterAction)=>eaterAction=="TentacleEat"?AlienMealVisible:eaterAction=="Bite"||eaterAction=="AllyGulp"?FishMealVisible:AquariumModel.SwallowVisualSeconds;
        public static float AlienClock(float age)=>age*AlienPace;
        public static float AlienPull(float age)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1.20f,age));
        public static float AlienSuction(float age)=>Mathf.Clamp01((age-AlienSuctionStart)/(AlienSuctionEnd-AlienSuctionStart));
        // Give the carrier time to turn in place and approach. Match velocity at
        // the timing joins, then settle before the 3-second spit.
        public static float CapturePoseTime(float age)
        {
            if(age<=.85f)return Hermite(age/.85f,0,.45f,.85f*.53f,.85f*.72f);
            if(age<=2.38f)return Hermite((age-.85f)/1.53f,.45f,1.98f,1.53f*.72f,1.53f*1.25f);
            return Hermite(Mathf.Clamp01((age-2.38f)/.62f),1.98f,3,.62f*1.25f,.62f*1.65f);
        }
        private static float Hermite(float t,float a,float b,float da,float db)
        {float t2=t*t,t3=t2*t;return (2*t3-3*t2+1)*a+(t3-2*t2+t)*da+(-2*t3+3*t2)*b+(t3-t2)*db;}

        public static float Suction(float age)=>Mathf.Clamp01((age-SuctionStart)/(SuctionEnd-SuctionStart));
        // Fish lunge: closes the gap, snaps a little past it into the meal, then holds.
        public static float Lunge(float age)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(.02f,.22f,age));
        public static float LungeSnap(float age)=>Mathf.Sin(Mathf.PI*Mathf.Clamp01((age-.12f)/.30f));
        // Greedy head shake while the meal is drawn in (radians of yaw, around the lips).
        public static float HeadShake(float age)
        {
            float envelope=Mathf.SmoothStep(0,1,Mathf.InverseLerp(SuctionStart+.04f,SuctionStart+.16f,age))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(SuctionEnd-.14f,SuctionEnd+.04f,age)));
            return envelope*(Mathf.Sin((age-SuctionStart)*Mathf.PI*2*4.2f)*.13f+Mathf.Sin((age-SuctionStart)*Mathf.PI*2*7.1f)*.04f);
        }
        // Legacy staging path (kept for QA tools). Meals are no longer moved to the mouth.
        public static Vector3 StagePoint(Vector3 source,Vector3 throat,Vector3 forward,Vector3 up,float distance,float clearance,float age)
        {
            forward=forward.normalized;Vector3 delta=source-throat;float start=delta.magnitude;
            Vector3 from=start>.00001f?delta/start:forward;
            float angle=Vector3.Angle(from,forward)*Mathf.Deg2Rad;
            float orbit=Mathf.Max(start,distance,clearance*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,.70f,angle)));
            float turn=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.06f,.24f,age));
            Vector3 normal=Vector3.Cross(from,forward);
            if(normal.sqrMagnitude<.000001f)normal=Mathf.Abs(Vector3.Dot(from,up.normalized))<.95f?up-from*Vector3.Dot(up,from):Vector3.Cross(from,Vector3.right);
            Vector3 direction=Quaternion.AngleAxis(angle*Mathf.Rad2Deg*turn,normal.normalized)*from;
            float radius=Mathf.Lerp(start,orbit,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.06f,age)));
            radius=Mathf.Lerp(radius,distance,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.22f,AlignmentEnd,age)));
            if(age>=AlignmentEnd)return throat+forward*distance;
            return throat+direction*radius;
        }
        // CPU reference for geometry QA; the skin and transition shaders share
        // the equivalent mapping in AquariumIngestion.cginc.
        public static Vector3 IngestPoint(Vector3 p,Vector3 origin,Vector3 axis,float halfLength,Vector3 mouth,float radius,float depth,float progress,bool sealedCavity=false)
        {
            if(progress<=0)return p;
            Vector3 delta=p-origin;float axial=Vector3.Dot(delta,axis);
            float distance=Vector3.Dot(origin-mouth,axis);
            float travel=Mathf.Max(.08f,distance+halfLength+.08f)*Mathf.SmoothStep(0,1,Mathf.Clamp01(progress));
            float section=distance+axial-travel;
            Vector3 centre=mouth+axis*Mathf.Max(sealedCavity?.012f:-.06f,section);
            Vector3 radial=delta-axis*axial;depth=Mathf.Max(.06f,depth);
            Vector3 lateral=(origin-mouth)-axis*distance;
            float keep=Mathf.Lerp(1,Mathf.SmoothStep(0,1,Mathf.Clamp01((section-depth*.5f)/(.55f+depth))),Mathf.SmoothStep(0,1,Mathf.Clamp01(progress/.25f)));
            centre+=lateral*keep;
            float limit=radius*(sealedCavity?.97f:1)+Mathf.Max(0,section-depth)*(sealedCavity?.52f:1.15f);
            float ratio=radial.magnitude/Mathf.Max(.001f,limit);
            float fit=Mathf.Pow(1+Mathf.Pow(Mathf.Min(ratio,100),8),-1f/8);
            float inside=Mathf.SmoothStep(0,1,Mathf.Clamp01((depth-section)/(depth+(sealedCavity?0:.06f))));
            radial*=Mathf.Lerp(1,fit*(1-inside),Mathf.SmoothStep(0,1,Mathf.Clamp01(progress/.06f)));
            return centre+radial;
        }
    }
}

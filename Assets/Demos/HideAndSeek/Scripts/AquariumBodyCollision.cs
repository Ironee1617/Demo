using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    // Host-owned rounded character hulls. Unity colliders use these same dimensions.
    public static class AquariumBodyCollision
    {
        // R24: the alien hull follows its smaller form (AquariumFishTraits.AlienScale, was .62/.25).
        public const float FishRadius=.54f, FishHalf=.16f, AlienRadius=.62f*AquariumFishTraits.AlienScale, AlienHalf=.25f*AquariumFishTraits.AlienScale, DiverRadius=.60f, DiverHalf=.62f;
        public static float Radius(bool diver,bool alien) { return diver?DiverRadius:alien?AlienRadius:FishRadius; }
        public static float Half(bool diver,bool alien) { return diver?DiverHalf:alien?AlienHalf:FishHalf; }
        // R23: a fish (or an alien disguised as one) takes its species' sized hull.
        public static float Radius(bool diver,bool alien,int species) { return diver?DiverRadius:alien?AlienRadius:AquariumFishTraits.HullRadius(species); }
        public static float Half(bool diver,bool alien,int species) { return diver?DiverHalf:alien?AlienHalf:AquariumFishTraits.HullHalf(species); }
        static float Clamp(float x) { return Math.Max(0,Math.Min(1,x)); }
        public static Point Penetration(Point a,Point ad,float ar,float ah,Point b,Point bd,float br,float bh)
        {
            Point p=a-ad.Normalized*ah,q=b-bd.Normalized*bh,d=ad.Normalized*(2*ah),e=bd.Normalized*(2*bh),r=p-q;
            float aa=Point.Dot(d,d),ee=Point.Dot(e,e),de=Point.Dot(d,e),dr=Point.Dot(d,r),er=Point.Dot(e,r),s=0,t=0;
            if(aa<.00001f)t=ee<.00001f?0:Clamp(er/ee);
            else if(ee<.00001f)s=Clamp(-dr/aa);
            else
            {
                float denominator=aa*ee-de*de;s=denominator>.00001f?Clamp((de*er-dr*ee)/denominator):0;t=(de*s+er)/ee;
                if(t<0){t=0;s=Clamp(-dr/aa);}else if(t>1){t=1;s=Clamp((de-dr)/aa);}
            }
            Point delta=p+d*s-q-e*t;float distance=delta.Length,depth=ar+br-distance;
            if(depth<=0)return new Point();
            if(distance<.0001f){delta=a-b;if(delta.Length<.0001f)delta=new Point(1,.27f,.43f);delta=delta.Normalized;}
            else delta=delta/distance;
            return delta*(depth+.002f);
        }
        // R28: players hug the sand and the glass again. Clearances follow the drawn bodies (measured at their
        // scale): the alien's arms reach .5 m below its centre and .62 m to the side; the diver stands upright,
        // feet 1.32 m below the centre and .86 m to the side. Fish keep a small margin around their own hull.
        public const float AlienFloor = .55f * AquariumFishTraits.AlienSizeRatio, AlienWall = .70f * AquariumFishTraits.AlienSizeRatio, DiverFloor = 1.0f, DiverWall = .62f;   // R73i: the diver fits narrower gaps (was 1.38 / .95)
        public static float FloorPadding(bool diver,bool alien,int species) { return diver?DiverFloor:alien?AlienFloor:AquariumFishTraits.WallRadius(species); }
        public static float WallReach(bool diver,bool alien,int species)
        { return diver?DiverWall:alien?AlienWall:AquariumFishTraits.HullRadius(species)+AquariumFishTraits.HullHalf(species)+.08f; }
        // Keep the full silhouette inside glass. Floor padding stays separate so bottom fish can rest on sand.
        public static Point ConstrainBody(Point at,Point facing,float radius,float half,float floorPadding,bool bottom=false,float wallReach=-1)
        {
            float reach=wallReach>0?wallReach:radius+half+.08f;
            var p=AquariumModel.Constrain(at,Math.Min(floorPadding,reach));
            for(int pass=0;pass<3;pass++)
            {
                p=AquariumMapOutline.Clamp(p,reach);
                p=AquariumWalkwayLayout.Constrain(p,reach);
                float pitch=Math.Abs(facing.Normalized.y)*half;
                float top=Math.Max(floorPadding,radius+.16f)+pitch;
                float foot=(bottom?floorPadding:Math.Max(floorPadding,radius+.10f))+pitch;
                p.y=Math.Max(AquariumSeabed.Height(p)+foot,Math.Min(AquariumModel.WaterHeight-top,p.y));
                // R57: the twin map's tunnel roof keeps the whole silhouette under the acrylic as well.
                if(AquariumMaps.IsTwin)p=AquariumTwinMap.ConstrainRoof(p,Math.Max(top,reach*.8f));
                if(AquariumMapOutline.Contains(p,reach)&&!AquariumWalkwayLayout.Contains(p,reach))break;
            }
            return p;
        }
        struct Body
        {
            public Swimmer player;public Prey fish;public Point position,direction;public float radius,half,weight,wallRadius,wallReach;public bool bottom;
        }
        [ThreadStatic] private static List<Body> contactBodies;
        private static bool Tethered(int id,bool player,List<Harpoon> harpoons)
        {
            if(harpoons!=null)foreach(var h in harpoons)if((player?h.caughtPlayer:h.caughtFish)==id)return true;
            return false;
        }
        public static Point Move(Swimmer player,Point travel,List<Swimmer> players,List<Prey> fish,float time,List<Harpoon> harpoons=null)
        {
            bool diver=player.team==Team.Seeker,alien=player.species<0 || player.revealUntil>time;
            bool bottom=!diver&&!alien&&AquariumFishTraits.Habit(player.species)==FishHabit.Bottom;
            float radius=Radius(diver,alien,player.species),half=Half(diver,alien,player.species),wallRadius=FloorPadding(diver,alien,player.species),wallReach=WallReach(diver,alien,player.species);Point position=player.position;
            int steps=Math.Max(1,(int)Math.Ceiling(travel.Length/.18f));
            for(int step=0;step<steps;step++)
            {
                position=ConstrainBody(position+travel/steps,player.facing,radius,half,wallRadius,bottom,wallReach);
                for(int pass=0;pass<3;pass++)
                {
                    bool hit=false;
                    foreach(var other in players)
                    {
                        if(other==player || other.swallowedBy!=0 || other.escaped || !other.alive && (time-other.deathAt>2.4f || Tethered(other.id,true,harpoons)))continue;
                        bool od=other.team==Team.Seeker,oa=other.species<0 || other.revealUntil>time;
                        float otherR=Radius(od,oa,other.species),otherH=Half(od,oa,other.species);
                        if((position-other.position).Length>radius+half+otherR+otherH)continue;
                        var correction=Penetration(position,player.facing,radius,half,other.position,other.facing,otherR,otherH);
                        if(correction.Length>.001f){position+=correction;hit=true;}
                    }
                    foreach(var other in fish)
                    {
                        float fr=AquariumFishTraits.HullRadius(other.species),fh=AquariumFishTraits.HullHalf(other.species);
                        if(other.collected || other.eaten || other.dead && (other.harpoonOwner==player.id || Tethered(other.id,false,harpoons)) || (position-other.position).Length>radius+half+fr+fh)continue;
                        var correction=Penetration(position,player.facing,radius,half,other.position,other.facing,fr,fh);
                        if(correction.Length>.001f){position+=correction;hit=true;}
                    }
                    if(!hit)break;position=ConstrainBody(position,player.facing,radius,half,wallRadius,bottom,wallReach);
                }
            }
            return position;
        }
        public static void Resolve(List<Swimmer> players,List<Prey> fish,float time,List<Harpoon> harpoons=null)
        {
            var bodies=contactBodies??(contactBodies=new List<Body>(players.Count+fish.Count));bodies.Clear();
            foreach(var p in players)
            {
                // R27: an alien squeezing into a drain is pinned over the mouth by the host (and an escaped one is gone).
                if(p.swallowedBy!=0 || p.entryAt>time || p.escaped || p.drainTask==DrainTask.Enter || !p.alive && (time-p.deathAt>2.4f || Tethered(p.id,true,harpoons)))continue;
                bool diver=p.team==Team.Seeker,alien=p.species<0 || p.revealUntil>time;
                bodies.Add(new Body{player=p,position=p.position,direction=p.facing,radius=Radius(diver,alien,p.species),half=Half(diver,alien,p.species),wallRadius=FloorPadding(diver,alien,p.species),wallReach=WallReach(diver,alien,p.species),bottom=!diver&&!alien&&AquariumFishTraits.Habit(p.species)==FishHabit.Bottom,weight=time<p.stunnedUntil?0:diver?.6f:1});
            }
            foreach(var f in fish)if(!f.collected && !f.eaten && !(f.dead && Tethered(f.id,false,harpoons)))bodies.Add(new Body{fish=f,position=f.position,direction=f.facing,radius=AquariumFishTraits.HullRadius(f.species),half=AquariumFishTraits.HullHalf(f.species),wallRadius=AquariumFishTraits.WallRadius(f.species),wallReach=WallReach(false,false,f.species),bottom=AquariumFishTraits.Habit(f.species)==FishHabit.Bottom,weight=2*AquariumFishTraits.Scale(f.species)});
            // Repeated projected contacts handle crowds and contacts beside glass/rocks.
            for(int pass=0;pass<8;pass++)
            {
                bool contact=false;
                for(int i=0;i<bodies.Count;i++)for(int j=i+1;j<bodies.Count;j++)
                {
                    var a=bodies[i];var b=bodies[j];if((a.position-b.position).Length>a.radius+b.radius+a.half+b.half)continue;
                    // A retrieved catch can sink past its owner without shoving the diver backwards.
                    if(a.player!=null && b.fish!=null && b.fish.dead && b.fish.harpoonOwner==a.player.id
                        || b.player!=null && a.fish!=null && a.fish.dead && a.fish.harpoonOwner==b.player.id)continue;
                    Point correction=Penetration(a.position,a.direction,a.radius,a.half,b.position,b.direction,b.radius,b.half);
                    if(correction.Length<.001f)continue;contact=true;
                    float fraction=a.weight+b.weight>.001f?a.weight/(a.weight+b.weight):.5f;
                    a.position=ConstrainBody(a.position+correction*fraction,a.direction,a.radius,a.half,a.wallRadius,a.bottom,a.wallReach);
                    b.position=ConstrainBody(b.position-correction*(1-fraction),b.direction,b.radius,b.half,b.wallRadius,b.bottom,b.wallReach);
                    bodies[i]=a;bodies[j]=b;
                }
                if(!contact)break;
            }
            foreach(var body in bodies)
            {
                var at=ConstrainBody(body.position,body.direction,body.radius,body.half,body.wallRadius,body.bottom,body.wallReach);
                if(body.player!=null)body.player.position=at;else body.fish.position=at;
            }
            bodies.Clear();
        }
    }
}

using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    // Host-owned rounded character hulls. Unity colliders use these same dimensions.
    public static class AquariumBodyCollision
    {
        public const float FishRadius=.54f, FishHalf=.16f, AlienRadius=.62f, AlienHalf=.25f, DiverRadius=.60f, DiverHalf=.62f;
        public static float Radius(bool diver,bool alien) { return diver?DiverRadius:alien?AlienRadius:FishRadius; }
        public static float Half(bool diver,bool alien) { return diver?DiverHalf:alien?AlienHalf:FishHalf; }
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
        struct Body
        {
            public Swimmer player;public Prey fish;public Point position,direction;public float radius,half,weight,wallRadius;
        }
        [ThreadStatic] private static List<Body> contactBodies;
        public static Point Move(Swimmer player,Point travel,List<Swimmer> players,List<Prey> fish,float time)
        {
            bool diver=player.team==Team.Seeker,alien=player.species<0 || player.revealUntil>time;
            float radius=Radius(diver,alien),half=Half(diver,alien),wallRadius=diver?.5f:.4f;Point position=player.position;
            int steps=Math.Max(1,(int)Math.Ceiling(travel.Length/.18f));
            for(int step=0;step<steps;step++)
            {
                position=AquariumModel.Constrain(position+travel/steps,wallRadius);
                for(int pass=0;pass<3;pass++)
                {
                    bool hit=false;
                    foreach(var other in players)
                    {
                        if(other==player || other.swallowedBy!=0 || !other.alive && time-other.deathAt>2.4f)continue;
                        bool od=other.team==Team.Seeker,oa=other.species<0 || other.revealUntil>time;
                        if((position-other.position).Length>radius+half+Radius(od,oa)+Half(od,oa))continue;
                        var correction=Penetration(position,player.facing,radius,half,other.position,other.facing,Radius(od,oa),Half(od,oa));
                        if(correction.Length>.001f){position+=correction;hit=true;}
                    }
                    foreach(var other in fish)
                    {
                        if(other.eaten || (position-other.position).Length>radius+half+FishRadius+FishHalf)continue;
                        var correction=Penetration(position,player.facing,radius,half,other.position,other.facing,FishRadius,FishHalf);
                        if(correction.Length>.001f){position+=correction;hit=true;}
                    }
                    if(!hit)break;position=AquariumModel.Constrain(position,wallRadius);
                }
            }
            return position;
        }
        public static void Resolve(List<Swimmer> players,List<Prey> fish,float time)
        {
            var bodies=contactBodies??(contactBodies=new List<Body>(players.Count+fish.Count));bodies.Clear();
            foreach(var p in players)
            {
                if(p.swallowedBy!=0 || !p.alive && time-p.deathAt>2.4f)continue;
                bool diver=p.team==Team.Seeker,alien=p.species<0 || p.revealUntil>time;
                bodies.Add(new Body{player=p,position=p.position,direction=p.facing,radius=Radius(diver,alien),half=Half(diver,alien),wallRadius=diver?.5f:.4f,weight=time<p.stunnedUntil?0:diver?.6f:1});
            }
            foreach(var f in fish)if(!f.eaten)bodies.Add(new Body{fish=f,position=f.position,direction=f.facing,radius=FishRadius,half=FishHalf,wallRadius=.35f,weight=2});
            // Repeated projected contacts handle crowds and contacts beside glass/rocks.
            for(int pass=0;pass<8;pass++)
            {
                bool contact=false;
                for(int i=0;i<bodies.Count;i++)for(int j=i+1;j<bodies.Count;j++)
                {
                    var a=bodies[i];var b=bodies[j];if((a.position-b.position).Length>a.radius+b.radius+a.half+b.half)continue;
                    Point correction=Penetration(a.position,a.direction,a.radius,a.half,b.position,b.direction,b.radius,b.half);
                    if(correction.Length<.001f)continue;contact=true;
                    float fraction=a.weight+b.weight>.001f?a.weight/(a.weight+b.weight):.5f;
                    a.position=AquariumModel.Constrain(a.position+correction*fraction,a.wallRadius);
                    b.position=AquariumModel.Constrain(b.position-correction*(1-fraction),b.wallRadius);
                    bodies[i]=a;bodies[j]=b;
                }
                if(!contact)break;
            }
            foreach(var body in bodies){if(body.player!=null)body.player.position=body.position;else body.fish.position=body.position;}
            bodies.Clear();
        }
    }
}

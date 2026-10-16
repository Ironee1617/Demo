using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    /// <summary>The same swept curves define the central decks, full-height glass and swim collision.</summary>
    public static class AquariumWalkwayLayout
    {
        public static readonly float[] HalfWidths = { .57f * AquariumMapOutline.HorizontalScale, .56f * AquariumMapOutline.HorizontalScale, .58f * AquariumMapOutline.HorizontalScale };
        public static readonly Point[][] Paths = CreatePaths();

        private static Point[][] CreatePaths()
        {
            var crescent = new List<Point>();
            for (int i = 0; i <= 100; i++)
            {
                double a = (35 + 290f * i / 100) * Math.PI / 180;
                crescent.Add(new Point(-5.4f + (float)Math.Cos(a) * 2.3f, 4.3f + (float)Math.Sin(a) * 2.3f));
            }
            var paths = new[] {
                Curve(new[] { new Point(-.8f,-6),new Point(-1.5f,-4),new Point(-3.8f,-2.2f),new Point(-5.6f,-.5f),new Point(-5.9f,.85f),new Point(-5.4f,2) }),
                crescent.ToArray(),
                Curve(new[] { new Point(-.8f,-5.4f),new Point(2.8f,-5.55f),new Point(5.8f,-4.4f),new Point(7.5f,-2.5f),new Point(7.4f,-1.7f) })
            };
            foreach(var path in paths)for(int i=0;i<path.Length;i++)
                path[i]=AquariumMapOutline.LayoutPoint(path[i].x,0,path[i].z);
            return paths;
        }

        private static Point[] Curve(Point[] control)
        {
            var result = new List<Point>();
            for (int i = 0; i < control.Length - 1; i++) for (int n = 0; n < 14; n++)
            {
                float t = n / 14f;
                var a = control[Math.Max(0,i-1)];var b=control[i];var c=control[i+1];var d=control[Math.Min(control.Length-1,i+2)];
                result.Add((b*2+(c-a)*t+(a*2-b*5+c*4-d)*(t*t)+(a*-1+b*3-c*3+d)*(t*t*t))*.5f);
            }
            result.Add(control[control.Length-1]);return result.ToArray();
        }

        public static float DistanceToPath(Point p, int path, out Point nearest, out Point tangent)
        {
            float best=float.MaxValue;nearest=new Point();tangent=new Point(1,0);
            var points=Paths[path];
            for(int i=0;i<points.Length-1;i++)
            {
                var a=points[i];var b=points[i+1];var d=b-a;
                float t=Math.Max(0,Math.Min(1,((p.x-a.x)*d.x+(p.z-a.z)*d.z)/Math.Max(.000001f,d.x*d.x+d.z*d.z)));
                var q=a+d*t;float dx=p.x-q.x,dz=p.z-q.z;float distance=dx*dx+dz*dz;
                if(distance<best){best=distance;nearest=q;tangent=d.Normalized;}
            }
            return (float)Math.Sqrt(best);
        }

        public static bool Contains(Point p, float padding=0, int exceptPath=-1)
        {
            return SignedDistance(p,exceptPath)<padding;
        }

        // Round the concave joints between whole corridors; each individual path keeps its width.
        // This same field drives the unified glass mesh, swimmers and projectile collision.
        public static float SignedDistance(Point p,int exceptPath=-1)
        {
            float clearance=float.MaxValue;
            for(int i=0;i<Paths.Length;i++)
            {
                if(i==exceptPath)continue;
                Point q,t;float d=DistanceToPath(p,i,out q,out t)-HalfWidths[i];
                const float rounding=1.2f;
                float h=Math.Max(rounding-Math.Abs(clearance-d),0)/rounding;
                clearance=Math.Min(clearance,d)-h*h*rounding*.25f;
            }
            return clearance;
        }

        public static Point Constrain(Point p,float padding)
        {
            float height=p.y;
            for(int pass=0;pass<16;pass++)
            {
                float deepest=0;Point push=new Point();
                for(int i=0;i<Paths.Length;i++)
                {
                    Point q,t;float distance=DistanceToPath(p,i,out q,out t);float penetration=HalfWidths[i]+padding-distance;
                    if(penetration<=deepest)continue;
                    var outward=new Point(p.x-q.x,p.z-q.z);
                    if(outward.Length<.0001f)outward=new Point(-t.z,t.x);
                    deepest=penetration;push=outward.Normalized*(penetration+.002f);
                }
                if(deepest<=0)break;
                p+=push;
            }
            // Resolve the small filleted area outside the original capsule union as well.
            for(int pass=0;pass<10;pass++)
            {
                float d=SignedDistance(p);if(d>=padding)break;
                const float e=.01f;
                float x=SignedDistance(p+new Point(e,0))-SignedDistance(p-new Point(e,0));
                float z=SignedDistance(p+new Point(0,e))-SignedDistance(p-new Point(0,e));
                var gradient=new Point(x,z);if(gradient.Length<.000001f)gradient=new Point(1,0);
                p+=gradient.Normalized*(padding-d+.002f);
            }
            p.y=height;return p;
        }

        // Exact 2D ray/capsule intersections; the gallery walls span the entire water depth.
        public static float Hit(Point start,Point end,float padding=0)
        {
            var s=new Point(start.x,start.z);var e=new Point(end.x,end.z);var ray=e-s;
            float first=2;
            for(int path=0;path<Paths.Length;path++)
            {
                var points=Paths[path];float radius=HalfWidths[path]+padding;
                for(int i=0;i<points.Length-1;i++)
                {
                    var a=points[i];var b=points[i+1];var axis=b-a;float length=axis.Length;axis=axis/length;
                    var normal=new Point(-axis.z,axis.x);var offset=s-a;
                    float x=Point.Dot(offset,axis),y=Point.Dot(offset,normal);
                    float dx=Point.Dot(ray,axis),dy=Point.Dot(ray,normal);
                    if(x>=0&&x<=length&&Math.Abs(y)<=radius)first=0;
                    if(Math.Abs(dy)>.000001f)for(int side=-1;side<=1;side+=2)
                    {
                        float t=(side*radius-y)/dy;float along=x+dx*t;
                        if(t>=0&&t<=1&&along>=0&&along<=length)first=Math.Min(first,t);
                    }
                    first=Math.Min(first,AquariumModel.HitTime(s,e,a,radius));
                    if(i==points.Length-2)first=Math.Min(first,AquariumModel.HitTime(s,e,b,radius));
                }
            }
            // Conservative distance stepping catches the rounded joining wedges too.
            float rayLength=ray.Length;
            if(rayLength>.000001f && first>0)
            {
                float t=0,limit=Math.Min(1,first);
                for(int step=0;step<192 && t<=limit;step++)
                {
                    float clearance=SignedDistance(s+ray*t)-padding;
                    if(clearance<.0005f){first=Math.Min(first,t);break;}
                    t+=Math.Max(.000001f,clearance/rayLength*.9f);
                }
            }
            return first;
        }
    }
}

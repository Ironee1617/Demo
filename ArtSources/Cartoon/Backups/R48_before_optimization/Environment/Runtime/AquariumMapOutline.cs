using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    /// <summary>Paired water/deck contours traced from the supplied 1536 x 1024 reference.</summary>
    public static class AquariumMapOutline
    {
        // Scale the layout, not inhabitants or individual reef assets. Water depth is controlled separately by AquariumModel.WaterHeight.
        public const float HorizontalScale = 2.4f;
        public static Point LayoutPoint(float x, float y, float z)
        { return new Point(x * HorizontalScale, y, z * HorizontalScale); }
        // water X/Y, outside X/Y, in reference-image pixels. The top-right inlet and
        // the narrow southern entrance require a polygon, not an ellipse/radius approximation.
        private static readonly float[,] Trace = {
            {490,100,475,20}, {548,105,537,27}, {600,132,596,49},
            {653,168,654,86}, {702,191,704,107}, {748,190,748,98},
            {787,192,797,104}, {818,211,842,126}, {840,244,893,156},
            {875,276,951,181}, {924,297,1000,188}, {979,307,1038,186},
            {1023,316,1061,184}, {1041,340,1072,183}, {1062,348,1083,181},
            {1082,335,1106,178}, {1094,304,1138,175}, {1122,275,1177,178},
            {1174,257,1224,184}, {1227,266,1284,207}, {1280,295,1337,240},
            {1324,340,1381,286}, {1356,394,1421,346}, {1378,453,1445,413},
            {1380,510,1450,478}, {1363,569,1438,547}, {1334,626,1409,610},
            {1294,680,1371,674}, {1238,731,1320,733}, {1173,779,1250,797},
            {1103,820,1174,851}, {1029,849,1091,897}, {953,862,1000,929},
            {881,855,910,946}, {821,831,835,944}, {773,800,785,935},
            {743,765,777,958}, {719,746,747,976}, {697,758,708,981},
            {669,784,669,970}, {636,809,649,950}, {600,820,625,942},
            {570,813,584,939}, {545,788,540,925}, {520,759,504,900},
            {478,738,467,872}, {430,720,423,847}, {375,695,368,824},
            {322,662,309,794}, {275,618,249,755}, {236,563,195,704},
            {209,502,151,644}, {194,436,119,577}, {196,392,101,512},
            {202,350,95,446}, {220,305,105,368}, {250,262,129,295},
            {290,218,165,229}, {338,175,213,169}, {389,139,276,117},
            {437,111,344,70}, {465,102,410,36}
        };
        private static readonly Point[] water = Sample(false, 496);
        private static readonly AquariumClearanceGrid clearance=BuildClearance();
        private static AquariumClearanceGrid BuildClearance()
        {
            float minX=float.MaxValue,minZ=float.MaxValue,maxX=float.MinValue,maxZ=float.MinValue;
            foreach(var p in water){minX=Math.Min(minX,p.x);maxX=Math.Max(maxX,p.x);minZ=Math.Min(minZ,p.z);maxZ=Math.Max(maxZ,p.z);}
            return new AquariumClearanceGrid(minX-2,minZ-2,maxX+2,maxZ+2,SignedClearance);
        }
        private static float SignedClearance(Point p)
        {Point nearest,normal;float squared;bool inside=Nearest(p,out nearest,out normal,out squared);return (inside?1:-1)*(float)Math.Sqrt(squared);}

        private static Point Control(int index, bool outer)
        {
            int count = Trace.GetLength(0); index = (index % count + count) % count;
            int col = outer ? 2 : 0;
            return LayoutPoint((Trace[index,col] - 770) * .024f, 0, (505 - Trace[index,col+1]) * .023f);
        }

        public static Point PointOn(float parameter, bool outer = false)
        {
            int count = Trace.GetLength(0);
            float s = (parameter - (float)Math.Floor(parameter)) * count;
            int i = (int)s; float t = s - i;
            var a = Control(i-1,outer); var b = Control(i,outer); var c = Control(i+1,outer); var d = Control(i+2,outer);
            // Closed Catmull-Rom interpolation retains the traced crests and inlets.
            return (b * 2 + (c-a)*t + (a*2-b*5+c*4-d)*(t*t) + (b*3-a-c*3+d)*(t*t*t)) * .5f;
        }

        public static Point[] Sample(bool outer, int count)
        {
            var result = new Point[count]; for (int i=0;i<count;i++) result[i]=PointOn(i/(float)count,outer); return result;
        }

        private static bool Nearest(Point p, out Point closest, out Point inward, out float distanceSquared)
        {
            bool inside = false; closest = new Point(); inward = new Point(0,-1); distanceSquared = float.MaxValue;
            for (int i=0;i<water.Length;i++)
            {
                var a=water[i]; var b=water[(i+1)%water.Length];
                float dx=b.x-a.x,dz=b.z-a.z;
                if ((a.z>p.z)!=(b.z>p.z) && p.x < dx*(p.z-a.z)/dz+a.x) inside=!inside;
                float length2=dx*dx+dz*dz;
                float t=length2<.0000001f?0:Math.Max(0,Math.Min(1,((p.x-a.x)*dx+(p.z-a.z)*dz)/length2));
                var q=new Point(a.x+dx*t,a.z+dz*t);
                float distance=(p.x-q.x)*(p.x-q.x)+(p.z-q.z)*(p.z-q.z);
                if(distance<distanceSquared)
                {
                    distanceSquared=distance; closest=q;
                    float length=(float)Math.Sqrt(Math.Max(length2,.0000001f));
                    inward=new Point(dz/length,-dx/length);
                }
            }
            return inside;
        }

        public static bool Contains(Point p,float padding=0)
        {
            bool cached;float threshold=(float)Math.Sqrt(Math.Max(0,padding*padding-.00001f));
            if(clearance.TryAtLeast(p,threshold,out cached))return cached;
            Point nearest,normal; float d;
            bool inside=Nearest(p,out nearest,out normal,out d);
            return inside && d+0.00001f>=padding*padding;
        }

        public static Point Clamp(Point p,float padding)
        {
            bool cached;
            if(clearance.TryAtLeast(p,Math.Abs(padding),out cached) && cached)return p;
            float height=p.y;
            for(int iteration=0;iteration<8;iteration++)
            {
                Point nearest,normal;float d;
                bool inside=Nearest(p,out nearest,out normal,out d);
                if(inside && d+0.000001f>=padding*padding) { p.y=height;return p; }
                if(inside && d>.000001f)
                {
                    float distance=(float)Math.Sqrt(d);
                    normal=new Point((p.x-nearest.x)/distance,(p.z-nearest.z)/distance);
                }
                p=nearest+normal*(padding+.001f);p.y=height;
            }
            // A very sharp concave join can have two simultaneous nearest edges.
            for(int i=0;i<32 && !Contains(p,padding);i++) { p.x*=.97f;p.z*=.97f; }
            p.y=height;return p;
        }

        public static float Radius(float angle)
        {
            float dx=(float)Math.Cos(angle),dz=(float)Math.Sin(angle),nearest=100;
            for(int i=0;i<water.Length;i++)
            {
                var a=water[i];var b=water[(i+1)%water.Length];float ex=b.x-a.x,ez=b.z-a.z;
                float cross=dx*ez-dz*ex;if(Math.Abs(cross)<.000001f)continue;
                float ray=(a.x*ez-a.z*ex)/cross,t=(a.x*dz-a.z*dx)/cross;
                if(ray>=0 && t>=0 && t<=1)nearest=Math.Min(nearest,ray);
            }
            return nearest;
        }

        // Ear clipping supports the non-star-shaped inlet without overlapping floor triangles.
        public static int[] Triangulate(Point[] contour)
        {
            var remaining=new List<int>();var result=new List<int>();
            for(int i=0;i<contour.Length;i++)remaining.Add(i);
            int guard=contour.Length*contour.Length;
            while(remaining.Count>2 && guard-->0)
            {
                bool clipped=false;
                for(int i=0;i<remaining.Count;i++)
                {
                    int ia=remaining[(i+remaining.Count-1)%remaining.Count],ib=remaining[i],ic=remaining[(i+1)%remaining.Count];
                    var a=contour[ia];var b=contour[ib];var c=contour[ic];
                    if(Cross(a,b,c)>=-.0000001f)continue;
                    bool occupied=false;
                    for(int j=0;j<remaining.Count;j++)
                    {
                        int ip=remaining[j];if(ip==ia||ip==ib||ip==ic)continue;var p=contour[ip];
                        if(Cross(a,b,p)<=0 && Cross(b,c,p)<=0 && Cross(c,a,p)<=0) { occupied=true;break; }
                    }
                    if(occupied)continue;
                    result.Add(ia);result.Add(ib);result.Add(ic);remaining.RemoveAt(i);clipped=true;break;
                }
                if(!clipped)throw new InvalidOperationException("Reference outline could not be triangulated.");
            }
            return result.ToArray();
        }
        private static float Cross(Point a,Point b,Point c) { return (b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x); }
    }
}

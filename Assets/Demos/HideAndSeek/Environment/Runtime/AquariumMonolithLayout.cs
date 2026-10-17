using System;

namespace PrototypeLab.Aquarium
{
    // Intersected fracture planes form solid angular beds. The same field is used by
    // the mesh, swimmers, projectiles and camera; no ellipsoid or soft-clay blending.
    public static class AquariumMonolithLayout
    {
        public struct FracturePlane
        {
            public Point normal;public float offset;
            public FracturePlane(Point n,float h){normal=n;offset=h;}
        }
        public struct Lobe
        {
            public Point centre,radius,extent;public FracturePlane[] planes;
            public Lobe(float x,float y,float z,float rx,float ry,float rz,float yaw=0)
            {
                centre=new Point(x,y,z);radius=new Point(rx,ry,rz);
                float a=yaw*(float)Math.PI/180,c=(float)Math.Cos(a),s=(float)Math.Sin(a);
                extent=new Point(Math.Abs(c)*rx+Math.Abs(s)*rz,ry*1.2f+(rx+rz)*.18f,Math.Abs(s)*rx+Math.Abs(c)*rz);
                var axes=new[]{new Point(1,0,0),new Point(-1,0,0),new Point(-.10f,1,.08f),new Point(.08f,-1,.05f),new Point(0,0,1),new Point(0,0,-1),
                    new Point(1,.20f,.67f),new Point(-1,.16f,-.61f),new Point(.65f,-.12f,-1),new Point(-.62f,.09f,1),
                    new Point(.75f,1,.32f),new Point(-.52f,1,-.79f),new Point(-.67f,-1,.24f),new Point(.43f,-1,-.55f)};
                planes=new FracturePlane[axes.Length];
                for(int i=0;i<axes.Length;i++)
                {
                    var n=new Point(axes[i].x/rx,axes[i].y/ry,axes[i].z/rz);float length=n.Length;
                    float cut=i<6?1:1.23f+((i*7+(int)Math.Abs(x*13+z*9))%5)*.035f;
                    n/=length;planes[i]=new FracturePlane(new Point(c*n.x+s*n.z,n.y,-s*n.x+c*n.z),cut/length);
                }
            }
        }
        public static readonly string[] Names={
            "Western fractured escarpment","Eastern reference cantilever cliff","Central broken-bed swim-through",
            "Crescent lagoon outcrop","Northwest stepped reef","Southwest collapsed slabs","Southeast talus ridge"
        };
        public static readonly Lobe[][] Groups={
            // Tall left silhouette: staggered beds and a narrow broken crown.
            new[]{
                new Lobe(-24.5f,.4f,-1.2f,3.25f,1.05f,4.2f,3),new Lobe(-25.0f,1.6f,-1.0f,2.55f,1.25f,3.3f,-7),
                new Lobe(-24.6f,3.0f,-.7f,2.55f,.95f,3.45f,4),new Lobe(-25.3f,4.15f,.1f,1.85f,1.3f,2.7f,-10),
                new Lobe(-23.9f,5.2f,.3f,3.05f,1.05f,3.0f,8),new Lobe(-24.65f,6.15f,.8f,2.45f,.53f,2.7f,2),
                new Lobe(-25.1f,7.2f,1.5f,1.35f,.9f,1.8f,-12),new Lobe(-23.0f,6.9f,1.7f,1.45f,.55f,1.85f,12),
                new Lobe(-26.1f,2.0f,-4.4f,1.4f,1.9f,1.9f,18),new Lobe(-22.3f,1.1f,-4.3f,1.8f,.67f,1.65f,-20),
                new Lobe(-21.55f,.45f,-2.8f,1.0f,.55f,1.25f,23),new Lobe(-23.1f,1.1f,3.3f,1.65f,.9f,1.45f,14)
            },
            // User photo: a high right-hand cliff, long jagged shelf projecting left,
            // a strongly recessed underside and a broken talus base.
            new[]{
                new Lobe(26.5f,.35f,3.0f,3.35f,1.0f,4.05f,-3),new Lobe(28.1f,1.7f,3.7f,1.7f,1.5f,3.0f,5),
                new Lobe(27.4f,3.35f,4.4f,2.05f,1.0f,2.65f,-5),new Lobe(28.2f,4.9f,4.7f,1.65f,1.1f,2.3f,7),
                new Lobe(28.15f,4.4f,4.8f,1.8f,4.5f,2.3f,4),
                new Lobe(27.2f,4.5f,4.1f,1.8f,2.2f,2.6f,4),new Lobe(24.8f,6.65f,3.5f,4.05f,1.15f,3.05f,-5),
                new Lobe(23.65f,6.2f,2.35f,2.45f,.37f,1.7f,8),new Lobe(26.6f,7.65f,4.3f,2.8f,1.3f,2.7f,7),
                new Lobe(28.3f,8.35f,4.7f,1.5f,.65f,2.0f,-9),new Lobe(27.4f,9.0f,5.6f,1.1f,.55f,1.2f,14),
                new Lobe(29.0f,2.3f,.1f,1.35f,2.1f,1.7f,-17),new Lobe(24.0f,.65f,-.45f,1.8f,.8f,1.6f,24),
                new Lobe(23.2f,1.15f,1.7f,1.1f,1.1f,1.25f,-23),new Lobe(25.5f,1.4f,6.6f,1.35f,.9f,1.15f,11),
                new Lobe(21.5f,.36f,2.7f,1.0f,.5f,1.15f,25),
                new Lobe(22.2f,5.9f,3.3f,.95f,.65f,1.1f,23),new Lobe(25.2f,7.2f,.9f,.9f,.65f,.85f,-18)
            },
            // Asymmetric natural bridge. The middle is kept clear for a 1 m diver.
            new[]{
                new Lobe(-2.3f,1.1f,3.8f,2.0f,1.8f,2.0f,8),new Lobe(-2.8f,.25f,3.5f,2.6f,.6f,2.4f,-13),
                new Lobe(-2.45f,3.2f,3.9f,1.45f,1.25f,1.65f,-5),new Lobe(-1.1f,4.5f,3.8f,2.0f,.65f,1.9f,5),
                new Lobe(6.7f,1.25f,4.1f,2.0f,1.95f,2.2f,-6),new Lobe(7.5f,.25f,4.4f,2.45f,.6f,2.4f,16),
                new Lobe(6.6f,3.7f,3.9f,1.8f,1.25f,1.95f,7),new Lobe(2.3f,5.0f,3.9f,4.0f,.85f,1.9f,-2),
                new Lobe(3.7f,5.65f,4.4f,3.1f,.85f,2.0f,5),new Lobe(5.6f,6.5f,4.6f,1.7f,.45f,1.55f,-12),
                new Lobe(-3.8f,.55f,1.6f,.95f,.6f,1.1f,25),new Lobe(8.6f,.5f,2.6f,.9f,.6f,1.3f,-25)
            },
            new[]{
                new Lobe(-12.9f,.25f,10.3f,2.55f,.65f,2.35f,4),new Lobe(-13.5f,1.25f,10.5f,1.7f,.8f,1.7f,-11),
                new Lobe(-12.7f,2.15f,10.7f,2.0f,.42f,1.65f,9),new Lobe(-13.0f,2.9f,11.1f,1.0f,.65f,1.2f,-8),
                new Lobe(-11.15f,.6f,9.7f,.85f,.65f,.9f,22),new Lobe(-14.6f,.6f,9.0f,.8f,.65f,.75f,-25)
            },
            new[]{
                new Lobe(-24.0f,.3f,9.2f,2.45f,.7f,2.6f,-6),new Lobe(-24.4f,1.45f,9.9f,1.9f,1.05f,1.9f,9),
                new Lobe(-23.4f,2.45f,9.8f,2.2f,.45f,2.1f,-11),new Lobe(-24.0f,3.25f,10.4f,1.4f,.65f,1.65f,4),
                new Lobe(-24.8f,4.0f,10.7f,.7f,.65f,1.15f,-16),new Lobe(-22.1f,.5f,8.0f,.8f,.6f,1.2f,24),
                new Lobe(-25.35f,.8f,7.0f,.9f,.95f,1.1f,-23)
            },
            new[]{
                new Lobe(-19.3f,.25f,-8.6f,2.15f,.65f,2.15f,8),new Lobe(-20.0f,1.15f,-8.5f,1.4f,.7f,1.5f,-14),
                new Lobe(-18.95f,1.95f,-8.2f,1.5f,.38f,1.55f,13),new Lobe(-19.7f,2.6f,-7.95f,.9f,.55f,1.0f,-10),
                new Lobe(-17.4f,.45f,-9.1f,.65f,.55f,1.05f,25),new Lobe(-20.6f,.45f,-10.0f,.85f,.55f,.8f,-22)
            },
            new[]{
                new Lobe(24.0f,.25f,-8.2f,2.45f,.65f,1.95f,-9),new Lobe(24.7f,1.2f,-7.8f,1.55f,.9f,1.5f,8),
                new Lobe(23.45f,2.1f,-8.0f,1.95f,.4f,1.5f,-11),new Lobe(24.0f,2.85f,-7.6f,1.15f,.6f,1.1f,11),
                new Lobe(21.8f,.5f,-8.3f,.8f,.65f,.95f,21),new Lobe(24.7f,.5f,-9.9f,.85f,.6f,.7f,-18)
            }
        };
        private static readonly Lobe[][] EdgeBreaks={
            new[]{new Lobe(-21.05f,5.35f,-.8f,.65f,.7f,.65f,27),new Lobe(-23.3f,6.45f,-2.15f,.5f,.55f,.6f,-23)},
            new[]{new Lobe(21.0f,6.35f,2.1f,.6f,.75f,.75f,-21),new Lobe(23.6f,6.85f,.55f,.55f,.6f,.8f,18),new Lobe(24.8f,6.35f,6.4f,.55f,.6f,.8f,-13)},
            new[]{new Lobe(1.1f,5.5f,2.1f,.5f,.55f,.65f,25),new Lobe(4.9f,6.35f,6.0f,.55f,.6f,.65f,-19)}
        };
        public static readonly Point[] Minimum,Maximum;
        static AquariumMonolithLayout()
        {
            Minimum=new Point[Groups.Length];Maximum=new Point[Groups.Length];
            for(int g=0;g<Groups.Length;g++)
            {
                var min=new Point(100,100,100);var max=new Point(-100,-100,-100);
                foreach(var l in Groups[g])
                {
                    min.x=Math.Min(min.x,l.centre.x-l.extent.x-.3f);min.y=Math.Min(min.y,l.centre.y-l.extent.y-.3f);min.z=Math.Min(min.z,l.centre.z-l.extent.z-.3f);
                    max.x=Math.Max(max.x,l.centre.x+l.extent.x+.3f);max.y=Math.Max(max.y,l.centre.y+l.extent.y+.3f);max.z=Math.Max(max.z,l.centre.z+l.extent.z+.3f);
                }
                Minimum[g]=min;Maximum[g]=max;
            }
        }
        private static float BoundDistance(Point p,int group)
        {
            var min=Minimum[group];var max=Maximum[group];
            float x=Math.Max(Math.Max(min.x-p.x,p.x-max.x),0),y=Math.Max(Math.Max(min.y-p.y,p.y-max.y),0),z=Math.Max(Math.Max(min.z-p.z,p.z-max.z),0);
            return (float)Math.Sqrt(x*x+y*y+z*z);
        }
        public static float Distance(Point p)
        {
            float d=100;
            for(int group=0;group<Groups.Length;group++)
                if(BoundDistance(p,group)<d+.6f)d=Math.Min(d,Distance(p,group));
            return d;
        }
        public static float Distance(Point p,int group)
        {
            float distance=100;
            foreach(var l in Groups[group])
            {
                float d=FractureDistance(p,l);
                // Only 2 cm of weathered edge; the broad fracture faces remain planar.
                distance=Math.Min(distance,d-.02f);
            }
            // Aperiodic erosion, much smaller than the fracture beds. Avoid the former
            // sinusoidal blobs that softened every face into a moulded clay surface.
            if(group<EdgeBreaks.Length)foreach(var cut in EdgeBreaks[group])distance=Math.Max(distance,-FractureDistance(p,cut));
            float weather=(Noise(p*.72f)-.5f)*.36f+(Noise(p*2.4f+new Point(7,11,3))-.5f)*.12f+(Noise(p*6.1f)-.5f)*.025f;
            return distance+weather;
        }
        private static float FractureDistance(Point p,Lobe l)
        {
            var q=p-l.centre;
            // Bedding has a physical dip, visible in the shelf silhouette and underside.
            q.y-=q.x*.13f-q.z*.07f;
            float d=-100;foreach(var plane in l.planes)d=Math.Max(d,Point.Dot(q,plane.normal)-plane.offset);
            return d;
        }
        private static float Hash(int x,int y,int z)
        {unchecked{uint h=(uint)x*374761393u+(uint)y*668265263u+(uint)z*2246822519u;h=(h^(h>>13))*1274126177u;return (h^(h>>16))*(1f/4294967295f);}}
        private static float Mix(float a,float b,float t){return a+(b-a)*t;}
        private static float Noise(Point p)
        {
            int x=(int)Math.Floor(p.x),y=(int)Math.Floor(p.y),z=(int)Math.Floor(p.z);
            float u=p.x-x,v=p.y-y,w=p.z-z;u=u*u*(3-2*u);v=v*v*(3-2*v);w=w*w*(3-2*w);
            return Mix(Mix(Mix(Hash(x,y,z),Hash(x+1,y,z),u),Mix(Hash(x,y+1,z),Hash(x+1,y+1,z),u),v),
                Mix(Mix(Hash(x,y,z+1),Hash(x+1,y,z+1),u),Mix(Hash(x,y+1,z+1),Hash(x+1,y+1,z+1),u),v),w);
        }
        public static bool Overlaps(Point p,float radius)
        {
            for(int group=0;group<Groups.Length;group++)
                if(BoundDistance(p,group)<=radius && Distance(p,group)<radius)return true;
            return false;
        }
        private static float NormalSample(Point p,int group){return group<0?Distance(p):Distance(p,group);}
        public static Point Normal(Point p,int group=-1)
        {
            const float e=.014f;
            return new Point(NormalSample(p+new Point(e,0,0),group)-NormalSample(p-new Point(e,0,0),group),NormalSample(p+new Point(0,e,0),group)-NormalSample(p-new Point(0,e,0),group),NormalSample(p+new Point(0,0,e),group)-NormalSample(p-new Point(0,0,e),group)).Normalized;
        }
        public static Point Constrain(Point p,float radius)
        {
            if(!Overlaps(p,radius))return p;
            for(int pass=0;pass<32;pass++)
            {
                float d=Distance(p);if(d>=radius)break;
                var normal=Normal(p);float step=radius-d+.015f;
                if(p.y+normal.y*step<AquariumSeabed.Height(p)+radius||p.y+normal.y*step>AquariumModel.WaterHeight-radius)
                {normal.y=0;if(normal.Length<.0001f)normal=new Point(1,0);normal=normal.Normalized;}
                p+=normal*step;
            }
            return p;
        }
        public static float Hit(Point start,Point end,float padding=0)
        {
            var ray=end-start;float length=ray.Length;
            if(length<.00001f)return Distance(start)<=padding?0:2;
            // The unpadded field, including weathering and the hit tolerance,
            // fits these bounds. Positive plane padding can grow corners by
            // more than padding, so those rays retain the original march.
            if(padding==0)
            {
                bool candidate=false;
                for(int group=0;group<Groups.Length;group++)
                    if(IntersectsBounds(start,ray,Minimum[group],Maximum[group])){candidate=true;break;}
                if(!candidate)return 2;
            }
            float t=0;
            for(int step=0;step<400&&t<=1;step++)
            {
                float d=Distance(start+ray*t)-padding;
                if(d<.0015f)return t;
                t+=Math.Max(.000001f,d*.38f/length);
            }
            return 2;
        }
        private static bool IntersectsBounds(Point start,Point ray,Point min,Point max)
        {
            const float tolerance=.002f;float first=0,last=1;
            return IntersectsSlab(start.x,ray.x,min.x-tolerance,max.x+tolerance,ref first,ref last)
                &&IntersectsSlab(start.y,ray.y,min.y-tolerance,max.y+tolerance,ref first,ref last)
                &&IntersectsSlab(start.z,ray.z,min.z-tolerance,max.z+tolerance,ref first,ref last);
        }
        private static bool IntersectsSlab(float start,float direction,float min,float max,ref float first,ref float last)
        {
            if(direction==0)return start>=min&&start<=max;
            float a=(min-start)/direction,b=(max-start)/direction;
            first=Math.Max(first,Math.Min(a,b));last=Math.Min(last,Math.Max(a,b));
            return first<=last;
        }
    }
}

using System;

namespace PrototypeLab.Aquarium
{
    // Depositional banks and shallow scours rather than a uniform sine-wave carpet.
    // Rendering and every moving body use this exact height field.
    public static class AquariumSeabed
    {
        // R57: the selected map's sand (AquariumMaps); BasicHeight is the default tank's.
        public static float Height(float x,float z){return AquariumMaps.IsTwin?TwinHeight(x,z):BasicHeight(x,z);}
        public static float BasicHeight(float x,float z)
        {
            float drift=.16f+.20f*Noise(x*.105f,z*.105f)+.10f*Noise(x*.31f+11,z*.31f-7);
            float banks=.65f*Hill(x-7,z+7,7,4.2f)+.39f*Hill(x+20,z+5,4.2f,3.8f)+.40f*Hill(x-22,z-7,4.0f,3.5f);
            float scour=-.075f*Hill(x-3,z+1,4.3f,2.8f)-.055f*Hill(x+9,z-9,3.7f,3.5f);
            float mask=Math.Max(0,(Noise(x*.13f+17,z*.13f+17)-.3f)/.7f);
            float phase=x*10.5f+z*3.0f+Noise(x*.7f,z*.7f)*3+Noise(x*.21f,z*.21f)*5;
            return drift+banks+scour+(float)Math.Sin(phase)*.007f*mask;
        }
        public static float Height(Point p){return Height(p.x,p.z);}
        // R57: the twin tanks' sand: the same drift, ripples and shading as the default tank, its own banks and
        // scours in each tank, and a level bed through the tunnel whose ripples run across it like a current.
        public static float TwinHeight(float x,float z)
        {
            float drift=.16f+.20f*Noise(x*.105f+40,z*.105f-25)+.10f*Noise(x*.31f+57,z*.31f-3);
            float banks=.62f*Hill(x+38,z-8,7,4.6f)+.42f*Hill(x+21,z+10,4.6f,3.8f)+.36f*Hill(x+41,z+9,4,3.4f)
                +.66f*Hill(x-38,z+7,7.5f,4.8f)+.40f*Hill(x-23,z-11,4.4f,3.6f)+.38f*Hill(x-42,z-10,4.2f,3.5f)+.30f*Hill(x-30,z-15,5,3);
            float scour=-.07f*Hill(x+27,z+1,4.5f,3)-.06f*Hill(x-29,z-1,4,3.3f);
            float mask=Math.Max(0,(Noise(x*.13f+17,z*.13f+17)-.3f)/.7f);
            // Between the mouths the bed levels out and the ripples always show.
            float ax=Math.Abs(x),tunnel=ax>=14?0:ax<=10.5f?1:(14-ax)/3.5f;tunnel=tunnel*tunnel*(3-2*tunnel);
            float level=.21f+.05f*Noise(x*.4f+3,z*.4f-9);
            float height=drift+banks+scour;height+=(level-height)*tunnel;mask+=(1-mask)*tunnel*.85f;
            float phase=x*10.5f+z*3.0f+Noise(x*.7f,z*.7f)*3+Noise(x*.21f,z*.21f)*5;
            return height+(float)Math.Sin(phase)*.007f*mask;
        }
        private static float Hill(float x,float z,float rx,float rz)
        {return (float)Math.Exp(-(x*x/(rx*rx)+z*z/(rz*rz)));}
        private static float Hash(int x,int z)
        {unchecked{uint h=(uint)x*374761393u+(uint)z*668265263u;h=(h^(h>>13))*1274126177u;return (h^(h>>16))*(1f/4294967295f);}}
        private static float Noise(float x,float z)
        {
            int ix=(int)Math.Floor(x),iz=(int)Math.Floor(z);float a=x-ix,b=z-iz;a=a*a*(3-2*a);b=b*b*(3-2*b);
            float n0=Hash(ix,iz)+(Hash(ix+1,iz)-Hash(ix,iz))*a;
            float n1=Hash(ix,iz+1)+(Hash(ix+1,iz+1)-Hash(ix,iz+1))*a;return n0+(n1-n0)*b;
        }
        public static Point Normal(Point p)
        {
            const float e=.025f;
            return new Point(Height(p.x-e,p.z)-Height(p.x+e,p.z),2*e,Height(p.x,p.z-e)-Height(p.x,p.z+e)).Normalized;
        }
        public static float Hit(Point start,Point end,float padding=0)
        {
            var delta=end-start;float length=delta.Length;
            if(length<.00001f)return start.y<=Height(start)+padding?0:2;
            float t=0;
            for(int step=0;step<320&&t<=1;step++)
            {
                var p=start+delta*t;float clearance=p.y-Height(p)-padding;
                if(clearance<.001f)return t;
                t+=Math.Max(.00001f,clearance*.5f/length);
            }
            return 2;
        }
    }
}

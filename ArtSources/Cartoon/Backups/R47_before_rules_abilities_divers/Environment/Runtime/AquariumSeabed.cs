using System;

namespace PrototypeLab.Aquarium
{
    // Depositional banks and shallow scours rather than a uniform sine-wave carpet.
    // Rendering and every moving body use this exact height field.
    public static class AquariumSeabed
    {
        public static float Height(float x,float z)
        {
            float drift=.16f+.20f*Noise(x*.105f,z*.105f)+.10f*Noise(x*.31f+11,z*.31f-7);
            float banks=.65f*Hill(x-7,z+7,7,4.2f)+.39f*Hill(x+20,z+5,4.2f,3.8f)+.40f*Hill(x-22,z-7,4.0f,3.5f);
            float scour=-.075f*Hill(x-3,z+1,4.3f,2.8f)-.055f*Hill(x+9,z-9,3.7f,3.5f);
            float mask=Math.Max(0,(Noise(x*.13f+17,z*.13f+17)-.3f)/.7f);
            float phase=x*10.5f+z*3.0f+Noise(x*.7f,z*.7f)*3+Noise(x*.21f,z*.21f)*5;
            return drift+banks+scour+(float)Math.Sin(phase)*.007f*mask;
        }
        public static float Height(Point p){return Height(p.x,p.z);}
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

using System;

namespace PrototypeLab.Aquarium
{
    // Static staging space only. No spawning, timeline, camera activation or entry animation.
    public static class AquariumEntryLayout
    {
        public const float DeckHeight = AquariumModel.WaterHeight+.275f;
        public static readonly Point Anchor = AquariumMapOutline.PointOn(.089f);
        public static readonly Point Right = (AquariumMapOutline.PointOn(.090f)-AquariumMapOutline.PointOn(.088f)).Normalized;
        public static readonly Point Inward = new Point(Right.z,-Right.x);

        public static Point At(float across,float inward,float height)
        { var p=Anchor+Right*across+Inward*inward;p.y=height;return p; }

        public static Point Local(Point p)
        { var d=p-Anchor;return new Point(Point.Dot(d,Right),Point.Dot(d,Inward)); }

        // Rounded apron 7.8 m wide, from the existing promenade to 3.2 m over water.
        public static float DeckDistance(Point p)
        {
            var q=Local(p);float x=Math.Abs(q.x)-3.25f,z=Math.Abs(q.z-.6f)-1.95f;
            return (float)Math.Sqrt(Math.Max(x,0)*Math.Max(x,0)+Math.Max(z,0)*Math.Max(z,0))
                +Math.Min(Math.Max(x,z),0)-.65f;
        }

        public static bool KeepWaterClear(Point p,float padding=0)
        {
            var q=Local(p);float z=q.z-6.0f;
            return q.x*q.x+z*z<(3.15f+padding)*(3.15f+padding) || DeckDistance(p)<padding;
        }

        public static bool KeepPromenadeClear(Point p,float padding=0)
        { var q=Local(p);return Math.Abs(q.x)<5.2f+padding && q.z>-5.0f-padding && q.z<3.5f+padding; }
    }
}

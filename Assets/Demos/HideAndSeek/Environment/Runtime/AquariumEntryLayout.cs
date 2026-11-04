using System;

namespace PrototypeLab.Aquarium
{
    // Static staging space only. No spawning, timeline, camera activation or entry animation.
    public static class AquariumEntryLayout
    {
        public const float DeckHeight = AquariumModel.WaterHeight+.275f;
        // R57: the deck of the selected map (AquariumMaps): the default tank's north deck, or the twin map's
        // deck on tank A's north wall.
        private static readonly Point BasicAnchor = AquariumMapOutline.BasicPointOn(.089f);
        private static readonly Point BasicRight = (AquariumMapOutline.BasicPointOn(.090f)-AquariumMapOutline.BasicPointOn(.088f)).Normalized;
        public static Point Anchor { get { return AquariumMaps.IsTwin ? AquariumTwinMap.EntryAnchor : BasicAnchor; } }
        public static Point Right { get { return AquariumMaps.IsTwin ? AquariumTwinMap.EntryRight : BasicRight; } }
        public static Point Inward { get { var right = Right; return new Point(right.z,-right.x); } }

        public static Point At(float across,float inward,float height)
        { var right=Right;var p=Anchor+right*across+new Point(right.z,0,-right.x)*inward;p.y=height;return p; }

        public static Point Local(Point p)
        { var right=Right;var d=p-Anchor;d.y=0;return new Point(Point.Dot(d,right),Point.Dot(d,new Point(right.z,0,-right.x))); }

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

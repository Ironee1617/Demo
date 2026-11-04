using System;

namespace PrototypeLab.Aquarium
{
    // R57: the playable maps. The host picks one in the lobby (AquariumModel.Map), it travels in every snapshot
    // (AquariumSnapshot.map) and every machine selects it here before laying the tank out, so the host rules,
    // the bots' grid, the camera and the presentation all ask the same water shape. Map 0 is the default tank.
    public static class AquariumMaps
    {
        public const int Basic = 0, Twin = 1, Count = 2;
        private static volatile int current;
        public static int Current { get { return current; } }
        public static bool IsTwin { get { return current == Twin; } }
        // Bumped whenever the selected map changes (presentation compares it).
        public static int Version { get; private set; }
        public static event Action Changed;

        public static int Clamp(int id) { return id == Twin ? Twin : Basic; }
        public static string Name(int id) { return Clamp(id) == Twin ? "쌍둥이 수조" : "기본 수조"; }
        public static string Description(int id)
        {
            return Clamp(id) == Twin ? "유리 터널로 이어진 넓은 수조 두 개" : "바위 절벽과 산호초가 있는 수조";
        }

        // Switch every map-dependent query (outline, sand, walls, entry deck, scenery layout) to this map.
        public static void Select(int id)
        {
            id = Clamp(id);
            if (id == current) return;
            current = id; Version++;
            AquariumSceneryLayout.MapChanged();
            if (Changed != null) Changed();
        }

        // Works out the twin tanks' outline, collision cache and tunnel table on a worker thread (about half a
        // second, once per run), so picking the map in the lobby does not stall the frame.
        private static bool prewarmed;
        public static void Prewarm()
        {
            if (prewarmed) return; prewarmed = true;
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try { var polygon = AquariumTwinMap.Polygon; AquariumTwinMap.Radius(0); var entry = AquariumTwinMap.EntryAnchor; }
                catch (Exception) { }
            });
        }

        // A point given in the default tank's nominal layout frame (about x -15..15, z -10..10, the frame the
        // spawn points and bot wander targets were written in), spread over the selected map's water.
        public static Point Layout(float x, float y, float z)
        { return IsTwin ? AquariumTwinMap.Layout(x, y, z) : AquariumMapOutline.LayoutPoint(x, y, z); }

        // Half extents of a box around the selected map's water (bot grid and searches).
        public static float HalfWidth { get { return IsTwin ? AquariumTwinMap.HalfWidth : AquariumModel.HalfWidth; } }
        public static float HalfDepth { get { return IsTwin ? AquariumTwinMap.HalfDepth : AquariumModel.HalfDepth; } }

        // Where a fish that is wedged against a wall heads for (the middle of the water it is in).
        public static Point Middle(Point p)
        { return IsTwin ? AquariumTwinMap.Middle(p) : AquariumMapOutline.LayoutPoint(0, p.y, 0); }
    }
}

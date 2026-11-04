using System;

namespace PrototypeLab.Aquarium
{
    // R57: the playable maps. The host picks one in the lobby (AquariumModel.Map), it travels in every snapshot
    // (AquariumSnapshot.map) and every machine selects it here before laying the tank out, so the host rules,
    // the bots' grid, the camera and the presentation all ask the same water shape. Map 0 is the default tank.
    public static class AquariumMaps
    {
        public const int Basic = 0, Twin = 1, Grotto = 2, Count = 3;
        private static volatile int current;
        // R62: a worker thread can lay a map out without touching the selected one (AquariumSceneryLayout.PrepareTwin):
        // it answers every map query on that thread for its own map (0 = none, else map + 1).
        [ThreadStatic] private static int threadMap;
        public static int Current { get { int t = threadMap; return t > 0 ? t - 1 : current; } }
        // R73: true for every map laid out by AquariumTwinMap: the twin tanks and map 2 (the tube tank, named Grotto in code).
        public static bool IsTwin { get { int c = Current; return c == Twin || c == Grotto; } }
        public static bool IsGrotto { get { return Current == Grotto; } }
        public static bool TwinFamily(int id) { return id == Twin || id == Grotto; }
        public static void UseOnThisThread(int map) { threadMap = map < 0 ? 0 : Clamp(map) + 1; }
        // Bumped whenever the selected map changes (presentation compares it).
        public static int Version { get; private set; }
        public static event Action Changed;

        public static int Clamp(int id) { return id == Twin || id == Grotto ? id : Basic; }
        public static string Name(int id) { id = Clamp(id); return id == Twin ? "쌍둥이 수조" : id == Grotto ? "보랏빛 터널 수조" : "기본 수조"; }
        public static string Description(int id)
        {
            id = Clamp(id);
            return id == Twin ? "유리 터널로 이어진 넓은 수조 두 개, 판마다 바위와 산호 배치가 바뀝니다"
                : id == Grotto ? "돌벽으로 둘러싸인 큰 수조 한가운데 U자 관람 터널이 다리 위에 떠 있는 어두운 보랏빛 수조"
                : "바위 절벽과 산호초, 유리 산책로가 있는 수조";
        }
        // R58: the lobby's map cards (R73: the cards show the name only; kept for other text).
        public static string Size(int id) { id = Clamp(id); return id == Twin ? "92 x 44 m, 수심 12 m" : id == Grotto ? "74 x 36 m, 수심 12 m" : "72 x 48 m, 수심 12 m"; }
        public static string Short(int id) { id = Clamp(id); return id == Twin ? "수조 2개 + 터널" : id == Grotto ? "돌벽 수조 + U자 관람 터널" : "큰 수조 1개"; }

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
            AquariumEnvironment.PrewarmRocks(); // R58: the twin tanks' rock meshes
            AquariumSceneryLayout.PrepareTwin(0, Twin); // R62: the twin tanks' authored layout, off the main thread
            AquariumSceneryLayout.PrepareTwin(0, Grotto); // R73: and the grotto's
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                foreach (int map in new[] { Twin, Grotto })
                {
                    UseOnThisThread(map);   // R73: each twin-family map keeps its own outline caches
                    try { var polygon = AquariumTwinMap.Polygon; AquariumTwinMap.Radius(0); var entry = AquariumTwinMap.EntryAnchor; }
                    catch (Exception) { }
                    finally { UseOnThisThread(-1); }
                }
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

using Unity.Profiling;

namespace PrototypeLab.Roguelike
{
    /// <summary>
    /// Profiler markers around the tower's per-frame systems and floor builds. They show up in the Unity
    /// Profiler and are sampled frame by frame by <see cref="TowerPerfSmokeDriver"/>.
    /// </summary>
    internal static class TowerProfiling
    {
        internal static readonly ProfilerMarker Enemies = new ProfilerMarker("Tower.Enemies");
        internal static readonly ProfilerMarker Allies = new ProfilerMarker("Tower.Allies");
        internal static readonly ProfilerMarker Skills = new ProfilerMarker("Tower.Skills");
        internal static readonly ProfilerMarker Shots = new ProfilerMarker("Tower.Shots");
        internal static readonly ProfilerMarker Effects = new ProfilerMarker("Tower.Effects");
        internal static readonly ProfilerMarker World = new ProfilerMarker("Tower.World");
        internal static readonly ProfilerMarker Hud = new ProfilerMarker("Tower.HUD");
        internal static readonly ProfilerMarker SpawnFloor = new ProfilerMarker("Tower.SpawnFloor");
        internal static readonly ProfilerMarker Generate = new ProfilerMarker("Tower.Generate");
        internal static readonly ProfilerMarker Terrain = new ProfilerMarker("Tower.Terrain");
        internal static readonly ProfilerMarker Backdrop = new ProfilerMarker("Tower.Backdrop");
        internal static readonly ProfilerMarker Gate = new ProfilerMarker("Tower.Gate");
        internal static readonly ProfilerMarker Music = new ProfilerMarker("Tower.Music");
        internal static readonly ProfilerMarker SetTheme = new ProfilerMarker("Tower.SetTheme");
        internal static readonly ProfilerMarker Actors = new ProfilerMarker("Tower.Actors");
        internal static readonly ProfilerMarker Party = new ProfilerMarker("Tower.Party");
        internal static readonly string[] Names =
        {
            "Tower.Enemies", "Tower.Allies", "Tower.Skills", "Tower.Shots", "Tower.Effects", "Tower.World", "Tower.HUD",
            "Tower.SpawnFloor", "Tower.SetTheme", "Tower.Generate", "Tower.Terrain", "Tower.Backdrop", "Tower.Gate", "Tower.Music", "Tower.Actors", "Tower.Party"
        };
    }
}

using UnityEngine;
namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumPerformanceChecks
    {
        public static void RunAndBuild()
        {
            Run();
            AquariumBuild.Windows();
        }
        public static void Run()
        {
            AquariumHotPathChecks.Run();
            AquariumNetworkPerformanceChecks.Run();
            AquariumSwimmingChecks.Run();
            AquariumEnvironmentPerformanceChecks.Run();
            Debug.Log("AQUARIUM_PERFORMANCE_CHECKS_OK");
        }
    }
}

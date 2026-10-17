using System;
namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumMergeChecks
    {
        public static void Run()
        {
            AquariumGraspChecks.Routes();
            AquariumFeedingChecks.Geometry();
            AquariumFeedingChecks.JawGeometry();
            foreach(string scene in new[]{"MouthEat","TentacleEat","AllyEat"})
            {
                Environment.SetEnvironmentVariable("AQUARIUM_QA_SCENARIO",scene);
                AquariumCharacterChecks.InTank();
            }
            AquariumCharacterChecks.ReleaseReview();
            AquariumBuild.Windows();
        }
    }
}

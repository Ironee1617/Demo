using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumMouthAssistChecks
    {
        public static void Review()
        {
            AquariumCartoonImport.Run();
            AquariumDownstrokeMealChecks.Review();
            AquariumFluidMotionChecks.Extra();
            AquariumFluidMotionChecks.Preview();
            Debug.Log("MOUTH_ASSIST_PREVIEW_OK");
        }
        public static void Build()
        {
            AquariumDownstrokeMealChecks.SnapshotReview();
            AquariumBuild.Windows();
        }
        public static void ReviewAndBuild(){Review();Build();}
    }
}

using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // The generated, editable prefab is shared by the gallery and the live game.
    public sealed class AquariumEnvironmentRoot : MonoBehaviour
    {
        public string artVersion = "Coral Gallery 1.8 / sealed reef meshes and living biodiversity";
        public float horizontalLayoutScale = AquariumMapOutline.HorizontalScale;
        public int kelpFronds, coralColonies, bubbleCount;
        public int visitorGalleryBays, monumentalReefs;
        public int anemoneColonies;
        public int[] coralForms=new int[6], plantForms=new int[5], anemoneForms=new int[2];
        public Vector3 anemonePreviewPoint, mintAnemonePreviewPoint, biodiversityPreviewPoint;
        public Transform divePreparation, diveEdge, diveWaterTarget, diveCamera;
    }
}

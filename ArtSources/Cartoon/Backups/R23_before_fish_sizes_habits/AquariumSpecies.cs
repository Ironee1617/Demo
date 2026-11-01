using UnityEngine;
namespace PrototypeLab.Aquarium
{
    // Add a catalog entry and two rig prefabs to extend the disguise roster.
    [CreateAssetMenu(menuName="Aquarium/Species")]
    public sealed class AquariumSpecies : ScriptableObject
    {
        public static int Count
        {
            get {int count=0;foreach(var s in Resources.LoadAll<AquariumSpecies>("AquariumCharacters"))count=Mathf.Max(count,s.speciesId+1);return Mathf.Max(1,count);}
        }
        public int speciesId;
        public GameObject normal, feeding, closedFeeding;
        public Color tint = Color.white;
        public Vector3 mouth = new Vector3(0,0,.48f);
        public float feedingMouthRadius=.26f;
    }
}

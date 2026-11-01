using UnityEngine;
namespace PrototypeLab.Aquarium
{
    // Add a catalog entry and two rig prefabs to extend the disguise roster.
    [CreateAssetMenu(menuName="Aquarium/Species")]
    public sealed class AquariumSpecies : ScriptableObject
    {
        public static int Count
        {
            get
            {
                int count=0;var all=Resources.LoadAll<AquariumSpecies>("AquariumCharacters");
                foreach(var s in all)count=Mathf.Max(count,s.speciesId+1);count=Mathf.Max(1,count);
                // R23: every model is built through here, so the host's hulls and fish habits
                // always match the catalog the view draws.
                var scale=new float[count];var width=new float[count];var length=new float[count];var habit=new FishHabit[count];var speed=new float[count];var ignore=new bool[count];
                for(int i=0;i<count;i++){scale[i]=width[i]=length[i]=speed[i]=1;habit[i]=FishHabit.Wander;}
                foreach(var s in all)if(s.speciesId>=0 && s.speciesId<count){scale[s.speciesId]=s.bodyScale;width[s.speciesId]=s.hullWidth;length[s.speciesId]=s.hullLength;habit[s.speciesId]=s.habit;speed[s.speciesId]=s.swimSpeed;ignore[s.speciesId]=s.ignoresFeed;}
                AquariumFishTraits.Configure(scale,width,length,habit,speed,ignore);
                return count;
            }
        }
        public int speciesId;
        public GameObject normal, feeding, closedFeeding;
        public Color tint = Color.white;
        public Vector3 mouth = new Vector3(0,0,.48f);
        public float feedingMouthRadius=.26f;
        [Header("R23 size and behaviour")]
        [Tooltip("Uniform size of the fish (Fish0 = 1). Disguised aliens take the same size.")]
        public float bodyScale=1;
        [Tooltip("Collision hull across / along the body, relative to Fish0 at this size.")]
        public float hullWidth=1, hullLength=1;
        public FishHabit habit=FishHabit.Wander;
        [Tooltip("R24: swimming speed relative to Fish0 (1 = 4 m/s). An alien disguised as this fish swims at the same speed.")]
        public float swimSpeed=1;
        [Tooltip("R24: does not come to the diver's scattered food.")]
        public bool ignoresFeed;
    }
}

using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // Authoring references only; unscrewing, lifting and entering will be designed separately.
    public sealed class AquariumDrain : MonoBehaviour
    {
        public Transform lid, opening, pipeExit;
        public Transform[] screws = new Transform[6];
        public float openingDiameter = AquariumDrainLayout.OpeningRadius * 2;
        public bool IsOpen { get; private set; }
        private bool poseStored;
        private Vector3 lidRest;
        private Quaternion lidRotation;
        private Vector3[] screwRest;
        private Quaternion[] screwRotation;

        public void SetOpen(bool open)
        {
            if(!poseStored)
            {
                lidRest=lid.localPosition;lidRotation=lid.localRotation;
                screwRest=new Vector3[screws.Length];screwRotation=new Quaternion[screws.Length];
                for(int i=0;i<screws.Length;i++){screwRest[i]=screws[i].localPosition;screwRotation[i]=screws[i].localRotation;}
                poseStored=true;
            }
            IsOpen=open;
            // Removable cover resting on the far rim, with the captive screws withdrawn.
            // Keep every object independent and the assembly inside the reserved clear footprint.
            var rotation=open?Quaternion.Euler(78,0,0):Quaternion.identity;
            var shift=open?new Vector3(0,.97f,.70f):Vector3.zero;
            lid.localPosition=lidRest+shift;lid.localRotation=rotation*lidRotation;
            for(int i=0;i<screws.Length;i++)
            {
                screws[i].localPosition=lidRest+shift+rotation*(screwRest[i]-lidRest+(open?Vector3.up*.18f:Vector3.zero));
                screws[i].localRotation=rotation*screwRotation[i];
            }
        }
    }

    public sealed class AquariumDrainPresentation
    {
        private readonly Transform parent;
        private GameObject group;
        private AquariumDrainPlacement[] last;
        private readonly Vector4[] apertures = new Vector4[4];
        private static readonly int[] HoleIds = {
            Shader.PropertyToID("_AquariumDrain0"), Shader.PropertyToID("_AquariumDrain1"),
            Shader.PropertyToID("_AquariumDrain2"), Shader.PropertyToID("_AquariumDrain3") };
        public AquariumDrainPresentation(Transform parent) { this.parent = parent; }

        public void Draw(AquariumSnapshot state)
        {
            var placements = state.drains;
            int count = placements == null ? 0 : Mathf.Min(placements.Length, 4);
            bool same = last != null && last.Length == count;
            if (same) for (int i = 0; i < count; i++)
                if (!last[i].position.Equals(placements[i].position) || last[i].yaw != placements[i].yaw || last[i].isOpen != placements[i].isOpen) { same = false; break; }
            if (same) return;
            Clear();
            last = new AquariumDrainPlacement[count];
            if (count == 0) return;
            var prefab = Resources.Load<GameObject>("AquariumDrains/ReturnDrain");
            if (prefab == null) { Debug.LogError("Missing AquariumDrains/ReturnDrain prefab"); return; }
            group = new GameObject("Round return drains"); group.transform.SetParent(parent, false);
            for (int i = 0; i < count; i++)
            {
                last[i] = placements[i]; var p = placements[i].position;
                var instance = Object.Instantiate(prefab, group.transform);
                instance.name = "Return drain " + (i + 1).ToString("00");
                instance.transform.SetPositionAndRotation(new Vector3(p.x, p.y, p.z), Quaternion.Euler(0, placements[i].yaw, 0));
                instance.GetComponent<AquariumDrain>().SetOpen(placements[i].isOpen);
                apertures[i] = new Vector4(p.x, p.y, p.z, AquariumDrainLayout.OpeningRadius + .008f);
                Shader.SetGlobalVector(HoleIds[i], apertures[i]);
            }
            Shader.SetGlobalInt("_AquariumDrainCount", count);
        }

        public void Clear()
        {
            if (group != null) { group.SetActive(false); if (Application.isPlaying) Object.Destroy(group); else Object.DestroyImmediate(group); }
            group = null; last = null;
            Shader.SetGlobalInt("_AquariumDrainCount", 0);
            for (int i = 0; i < 4; i++) Shader.SetGlobalVector(HoleIds[i], Vector4.zero);
        }
    }
}

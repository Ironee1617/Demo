using UnityEngine;

namespace PrototypeLab.Aquarium
{
    public sealed class AquariumHarpoonGun : MonoBehaviour
    {
        public Transform muzzle, ropeExit, loadedHarpoon;
        public float headLength = .314f;
        // Length of the steel rod behind the spear head; the line is tied at its tail.
        public float shaftLength = .75f;
        public void SetLoaded(bool ready) { loadedHarpoon.gameObject.SetActive(ready); }
    }
}

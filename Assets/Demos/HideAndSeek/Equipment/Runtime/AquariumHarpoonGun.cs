using UnityEngine;

namespace PrototypeLab.Aquarium
{
    public sealed class AquariumHarpoonGun : MonoBehaviour
    {
        public Transform muzzle, ropeExit, loadedHarpoon;
        public float headLength = .314f;
        public void SetLoaded(bool ready) { loadedHarpoon.gameObject.SetActive(ready); }
    }
}

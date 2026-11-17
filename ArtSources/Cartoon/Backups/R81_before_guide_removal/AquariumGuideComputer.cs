using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R81: the fish-guide computer in the divers' changing room (built by AquariumEnvironment.GuideComputer).
    // Its screen is made of ready-built pages (one per species and an idle title page); Show switches between them,
    // so the monitor in the room shows the page the diver is reading. The idle page's hint blinks and the screen's
    // glow breathes a little.
    public sealed class AquariumGuideComputer : MonoBehaviour
    {
        public GameObject attract, hint;
        public GameObject[] pages;
        public Light glow;
        public float glowIntensity = .8f;
        private int shown = -1;

        public int PageCount { get { return pages == null ? 0 : pages.Length; } }
        public int Shown { get { return shown; } }

        // page < 0: the idle title page.
        public void Show(int page)
        {
            if (pages == null || page >= pages.Length) page = -1;
            if (page < 0) page = -1;
            if (page == shown) return;
            shown = page;
            if (attract != null) attract.SetActive(page < 0);
            for (int i = 0; i < pages.Length; i++) if (pages[i] != null) pages[i].SetActive(i == page);
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            if (hint != null && shown < 0)
            {
                bool on = Mathf.Repeat(t, 1.5f) < 1.05f;
                if (hint.activeSelf != on) hint.SetActive(on);
            }
            if (glow != null) glow.intensity = glowIntensity * (1 + .05f * Mathf.Sin(t * 2.1f) + .02f * Mathf.Sin(t * 7.3f));
        }
    }
}

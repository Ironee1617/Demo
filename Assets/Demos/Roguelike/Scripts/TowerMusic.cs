using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>Two streaming decks preserve a theme's loop across floors and fade between themes.</summary>
    public sealed class TowerMusic : MonoBehaviour
    {
        internal const float DefaultVolume = .18f;
        internal const float CrossfadeSeconds = 1.25f;
        private readonly AudioSource[] decks = new AudioSource[2];
        private readonly float[] fadeFrom = new float[2];
        private int active = -1;
        private float fadeTime;
        private bool fading;
        internal TowerThemeId? CurrentTheme { get; private set; }

        internal static TowerMusic Create(Transform parent)
        {
            var root = new GameObject("Theme music");
            root.transform.SetParent(parent, false);
            return root.AddComponent<TowerMusic>();
        }

        private void Awake()
        {
            for (int i = 0; i < decks.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = true; source.spatialBlend = 0;
                source.priority = 32; source.volume = 0; source.dopplerLevel = 0;
                decks[i] = source;
            }
        }

        /// <summary>
        /// Tracks are still stored under the painted board names; each mythology plays its board's track. Hell slows
        /// the ember track into a dirge, the abyss drags the tomb track lower still, and the village plays the garden track.
        /// </summary>
        internal static string Track(TowerThemeId theme, out float pitch)
        {
            switch (theme)
            {
                case TowerThemeId.Hell: pitch = .86f; return "lava";
                case TowerThemeId.Abyss: pitch = .72f; return "ruins";
                case TowerThemeId.Village: pitch = .94f; return "jungle";
                default: pitch = 1; return TowerThemeMonsterArt.Sheet(theme);
            }
        }

        public void PlayTheme(TowerThemeId theme)
        {
            if (CurrentTheme == theme && active >= 0 && decks[active].clip != null) return;
            float pitch;
            var clip = Resources.Load<AudioClip>("Tower/Music/" + Track(theme, out pitch));
            if (clip == null) { Debug.LogWarning("Missing tower theme music: " + theme, this); return; }

            int incoming = -1;
            for (int i = 0; i < decks.Length; i++) if (decks[i].clip == clip && Mathf.Approximately(decks[i].pitch, pitch)) incoming = i;
            if (incoming < 0)
            {
                // If a run is restarted during a fade, retain the louder deck.
                incoming = decks[0].volume <= decks[1].volume ? 0 : 1;
                decks[incoming].Stop(); decks[incoming].volume = 0; decks[incoming].clip = clip; decks[incoming].pitch = pitch;
                decks[incoming].Play();
            }
            active = incoming; CurrentTheme = theme; fadeTime = 0; fading = true;
            for (int i = 0; i < decks.Length; i++) fadeFrom[i] = decks[i].volume;
        }

        private void Update()
        {
            if (!fading) return;
            fadeTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(fadeTime / CrossfadeSeconds);
            float eased = progress * progress * (3 - 2 * progress);
            for (int i = 0; i < decks.Length; i++)
                decks[i].volume = Mathf.Lerp(fadeFrom[i], i == active ? DefaultVolume : 0, eased);
            if (progress < 1) return;
            fading = false;
            for (int i = 0; i < decks.Length; i++) if (i != active)
            { decks[i].Stop(); decks[i].clip = null; }
        }

        private void OnDisable() { foreach (var source in decks) if (source) source.Pause(); }
        private void OnEnable() { foreach (var source in decks) if (source) source.UnPause(); }
        private void OnDestroy()
        {
            foreach (var source in decks) if (source)
            { source.Stop(); source.clip = null; Destroy(source); }
        }
    }
}

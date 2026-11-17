using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R75: sound pass 2 and the no-hunger rules on the HUD side.
    //  - Effect sounds are positional: full within a few metres, fading fast, silent past their hearing range.
    //  - UI buttons: a soft bubble "plip" on click and a faint tick when the pointer moves onto a button (synthesised).
    //  - The E (eat) slot shows the eat cooldown (AquariumModel.EatReadyAt).
    public sealed partial class AquariumGame
    {
        private const float FeedVolume = .9f;        // was .38
        private const float AlienHearing = 18f;      // morph, dash, glue, invisibility, starving...
        private const float EffectVolume = .9f;      // the main source used to play these at .24 (= .75 x SfxScale)
        private const int CountdownBeeps = 10;
        private int lastBeepSecond = -1;

        // Full up to `near`, then a quick (squared) fade to nothing at `hearing`.
        private static float Falloff(float distance, float hearing)
        {
            if (hearing <= 0) return 1;
            float near = Mathf.Min(3f, hearing * .15f);
            if (distance <= near) return 1;
            float t = Mathf.Clamp01((distance - near) / Mathf.Max(.01f, hearing - near));
            return (1 - t) * (1 - t);
        }

        private static float HearingOf(string kind)
        {
            switch (kind)
            {
                case "shot": return 30f;
                case "splash": return 35f;
                case "escape": return 30f;
                case "lidOpen": case "lidClosed": return 25f;
                case "hatch": return 22f;
                case "screwOut": case "squeezePop": return 20f;
                case "eat": case "burp": return 18f;
                case "egg": return 15f;
                default: return 20f;
            }
        }

        // The effect loop in Update (AquariumGame.cs) hands every effect sound here.
        private void PlayEffectSound(WaterEffect fx, AudioClip clip)
        {
            if (shotPool[0] == null) { audioSource.PlayOneShot(clip); return; }   // no R74 sources (no recorded set)
            PlayClipAt(clip, fx.position, fx.owner, HearingOf(fx.kind), EffectVolume);
        }

        // HUD: the E slot's cooldown fill.
        private static float EatProgress(Swimmer p, AquariumSnapshot state)
        {
            if (state.time < state.huntAt) return -1;
            float ready = AquariumModel.EatReadyAt(p, state.time, state.endsAt);
            if (ready <= state.time) return -1;
            float cd = AquariumModel.EatCooldownAt(state.time, state.endsAt);
            return 1 - Mathf.Clamp01((ready - state.time) / cd);
        }

        // ---- UI button sounds ------------------------------------------------------------------------------
        private AudioSource uiSource;
        private AudioClip uiClick, uiHover;
        private int hoverNow, hoverLast;

        private bool UiButton(Rect rect, string text, GUIStyle style) { NoteHover(rect); bool b = GUI.Button(rect, text, style); if (b) UiClick(); return b; }
        private bool UiButton(Rect rect, GUIContent content, GUIStyle style) { NoteHover(rect); bool b = GUI.Button(rect, content, style); if (b) UiClick(); return b; }
        private bool UiButton(Rect rect, string text) { NoteHover(rect); bool b = GUI.Button(rect, text); if (b) UiClick(); return b; }

        private void NoteHover(Rect rect)
        {
            var e = Event.current;
            if (e == null || e.type != EventType.Repaint || !GUI.enabled || !rect.Contains(e.mousePosition)) return;
            var at = GUIUtility.GUIToScreenPoint(rect.position);
            hoverNow = 1 + Mathf.RoundToInt(at.x) * 7919 + Mathf.RoundToInt(at.y) * 104729 + Mathf.RoundToInt(rect.width) * 31;
        }

        private void UiClick()
        {
            EnsureUiSounds();
            uiSource.PlayOneShot(uiClick, .55f);
        }

        // Update: a tick when the pointer lands on a new button (read from last frame's OnGUI).
        private void UpdateUiSounds()
        {
            if (hoverNow != 0 && hoverNow != hoverLast) { EnsureUiSounds(); uiSource.PlayOneShot(uiHover, .3f); }
            hoverLast = hoverNow; hoverNow = 0;
        }

        private void EnsureUiSounds()
        {
            if (uiSource != null) return;
            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.playOnAwake = false; uiSource.spatialBlend = 0; uiSource.volume = .6f;
            uiClick = UiClickSound(); uiHover = UiHoverSound();
        }

        // A round little bubble pop: a pitch that springs up, a soft body and a short airy tail.
        private static AudioClip UiClickSound()
        {
            const int rate = 44100; int n = (int)(rate * .11f);
            var data = new float[n]; float phase = 0, phase2 = 0; var noise = new System.Random(75); float air = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float f = 520 + 760 * (1 - Mathf.Exp(-t * 55));
                phase += f * 2 * Mathf.PI / rate; phase2 += f * 2.01f * 2 * Mathf.PI / rate;
                float env = Mathf.Clamp01(t / .004f) * Mathf.Exp(-t * 38);
                air = Mathf.Lerp(air, (float)noise.NextDouble() * 2 - 1, .35f);
                data[i] = (Mathf.Sin(phase) * .8f + Mathf.Sin(phase2) * .12f) * env * .55f + air * Mathf.Exp(-t * 90) * .05f;
            }
            var clip = AudioClip.Create("ui click", n, 1, rate, false); clip.SetData(data, 0); return clip;
        }

        // A faint glassy tick.
        private static AudioClip UiHoverSound()
        {
            const int rate = 44100; int n = (int)(rate * .045f);
            var data = new float[n]; float phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                phase += (1650 + 300 * t / .045f) * 2 * Mathf.PI / rate;
                data[i] = Mathf.Sin(phase) * Mathf.Clamp01(t / .002f) * Mathf.Exp(-t * 120) * .35f;
            }
            var clip = AudioClip.Create("ui hover", n, 1, rate, false); clip.SetData(data, 0); return clip;
        }
    }
}

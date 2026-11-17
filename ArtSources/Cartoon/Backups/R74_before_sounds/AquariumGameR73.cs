using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R73: the new sound set (synthesised WAVs in Resources/AquariumAudio, the old procedural tones stay as the
    // fallback), the starving warning (below 20% hunger: a blurry red border that breathes, and the stomach growls),
    // and the lobby's "fill the empty seats with AI" switch.
    public sealed partial class AquariumGame
    {
        private static readonly string[] SoundFiles = { "eat", "shot", "burp", "morph", "caught", "hatch", "scan", "egg", "screwOut",
            "lidOpen", "lidClosed", "squeezePop", "escape", "alarm", "feed", "splash" };
        private AudioClip growlSound;
        private Texture2D starveTexture;
        private bool starving;
        private float growlAt, growlPlayedAt = -100;

        // Called at the end of Awake: every key that has a file uses it.
        private void LoadSoundSet()
        {
            foreach (var key in SoundFiles)
            {
                var clip = Resources.Load<AudioClip>("AquariumAudio/" + key);
                if (clip != null) sounds[key] = clip;
            }
            var ping = Resources.Load<AudioClip>("AquariumAudio/ping");
            if (ping != null) pingSound = ping;
            growlSound = Resources.Load<AudioClip>("AquariumAudio/growl");
            // R73b: the looping squeak and ratchet of a screw being wrung out (UpdateScrewSound).
            var screw = Resources.Load<AudioClip>("AquariumAudio/screwTurn");
            if (screw != null)
            {
                screwSource = gameObject.AddComponent<AudioSource>();
                screwSource.clip = screw; screwSource.loop = true; screwSource.playOnAwake = false; screwSource.spatialBlend = 0; screwSource.volume = 0;
            }
        }

        private bool Starving(Swimmer p, AquariumSnapshot state)
        {
            return p != null && !session.IsSpectator && p.id == session.LocalId && p.alive && p.team == Team.Hider && p.swallowedBy == 0
                && state.phase == RoundPhase.Playing && state.time >= state.huntAt
                && AquariumModel.HungerLeft(p, state.time) < AquariumModel.HungerMax(p) * AquariumModel.StarvingFraction;
        }

        // Update: the growl when hunger first drops under 20%, then every few seconds while it stays there.
        private AudioSource screwSource;
        private const float ScrewHearing = 9f;   // R73i: heard only close by (it was the drain hearing distance, 25 m)
        // R73b: while an alien works a screw, its ratchet loops: full for the one doing it, fading out over the
        // drain hearing distance for everyone else (the diver can hear it nearby), panned toward it.
        private void UpdateScrewSound()
        {
            if (screwSource == null) return;
            float gain = 0, pan = 0;
            var state = session.Connected ? session.State : null;
            if (state != null && state.phase == RoundPhase.Playing && state.players != null && !pause)
            {
                var listener = SoundListener();
                foreach (var p in state.players)
                {
                    if (!p.alive || p.drainTask != DrainTask.Screw) continue;
                    float g = listener == null ? 0 : p.id == listener.id ? 1 : Mathf.Clamp01(1 - (p.position - listener.position).Length / ScrewHearing);
                    if (g > gain)
                    {
                        gain = g;
                        var delta = new Vector3(p.position.x - listener.position.x, 0, p.position.z - listener.position.z);
                        pan = p.id == listener.id || delta.sqrMagnitude < .01f ? 0 : Vector3.Dot(view.Camera.transform.right, delta.normalized) * .6f;
                    }
                }
            }
            float target = .55f * gain;
            screwSource.volume = Mathf.MoveTowards(screwSource.volume, target, Time.unscaledDeltaTime * 3f);
            screwSource.panStereo = Mathf.Lerp(screwSource.panStereo, pan, Time.unscaledDeltaTime * 6f);
            if (screwSource.volume > .001f && !screwSource.isPlaying) screwSource.Play();
            else if (screwSource.volume <= .001f && screwSource.isPlaying) screwSource.Pause();
        }

        private void UpdateHungerWarning()
        {
            var state = session.Connected ? session.State : null;
            bool now = state != null && Starving(Local(), state);
            if (now && !starving) growlAt = Time.unscaledTime;
            starving = now;
            if (!now || pause || Time.unscaledTime < growlAt) return;
            if (growlSound != null) audioSource.PlayOneShot(growlSound, .9f);
            growlPlayedAt = Time.unscaledTime;
            growlAt = Time.unscaledTime + Random.Range(4.5f, 6.5f);
        }

        // PlayHUD: the soft red rim, stronger the emptier the stomach, swelling with each growl.
        private void HungerBorder(AquariumSnapshot state, Swimmer p, float width, float height)
        {
            if (!Starving(p, state) || Event.current.type != EventType.Repaint) return;
            if (starveTexture == null) starveTexture = StarveTexture();
            float max = AquariumModel.HungerMax(p), left = AquariumModel.HungerLeft(p, state.time);
            float severity = 1 - Mathf.Clamp01(left / (max * AquariumModel.StarvingFraction));
            float since = Time.unscaledTime - growlPlayedAt, swell = since < 1.8f ? Mathf.Sin(Mathf.Clamp01(since / 1.8f) * Mathf.PI) : 0;
            float breathe = .5f + .5f * Mathf.Sin(Time.unscaledTime * 2.1f);
            float alpha = Mathf.Clamp01(.42f + .38f * severity + .12f * breathe + .25f * swell);
            float grow = 1 + .06f * swell + .04f * severity;   // the rim creeps further in
            var old = GUI.color;
            GUI.color = new Color(.78f, .04f, .05f, alpha);
            float w = width * grow, h = height * grow;
            GUI.DrawTexture(new Rect((width - w) / 2, (height - h) / 2, w, h), starveTexture, ScaleMode.StretchToFill, true);
            GUI.color = old;
            // the reason, small, above the vitals
            Label(24, height - 196, 260, 22, "배가 너무 고파요!  물고기를 먹어야 해요", Tinted(small, new Color(1f, .55f, .5f)));
        }

        // A soft rounded-rect vignette with smoky blotches (low resolution, so stretching blurs it further).
        private static Texture2D StarveTexture()
        {
            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "starve rim" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size * 2 - 1, v = (y + .5f) / size * 2 - 1;
                    float qx = Mathf.Max(Mathf.Abs(u) - .42f, 0), qy = Mathf.Max(Mathf.Abs(v) - .30f, 0);
                    float d = Mathf.Sqrt(qx * qx + qy * qy) / .62f;   // 0 inside, ~1 at the corners
                    float edge = Mathf.Max(d, Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) * .9f - .2f);
                    float smoke = Mathf.PerlinNoise(x * .09f + 3.1f, y * .09f + 7.7f) * .6f + Mathf.PerlinNoise(x * .21f, y * .21f + 1.3f) * .4f;
                    float a = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.38f + smoke * .2f, 1.02f, edge));
                    a = Mathf.Pow(a, 1.2f) * (.8f + .2f * smoke);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            tex.SetPixels32(pixels); tex.Apply(false, true);
            return tex;
        }

        // Lobby, online host: fill the empty alien seats with AI (on) or play with the people in the room only (off).
        private void FillAISwitch(Rect rect)
        {
            bool on = session.FillWithAI, host = session.IsHost && !session.Practice;
            string label = on ? "빈자리 AI 채우기: 켬" : "빈자리 AI 채우기: 끔";
            if (host)
            {
                if (Button(rect, label)) { session.SetFillWithAI(!on); notice = !on ? "빈자리를 AI 외계인으로 채웁니다." : "AI 없이 방에 있는 사람끼리만 합니다."; }
            }
            else
            {
                Shape(rect, new Color(0.04f, 0.12f, 0.17f, .6f), 10);
                GUI.Label(rect, label, Tinted(smallCentred, on ? cyan : muted));
            }
        }

        // How many other players picked each alien (duplicates are allowed: the cards show the count).
        private int OthersWithAlien(int kind)
        {
            if (session.State == null || session.State.players == null) return 0;
            int count = 0;
            foreach (var p in session.State.players) if (p.id != session.LocalId && !p.bot && p.alienKind == kind) count++;
            return count;
        }
    }
}

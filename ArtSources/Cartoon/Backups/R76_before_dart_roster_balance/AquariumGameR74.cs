using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R74: the recorded sound set from D:\수족관술래잡기\사운드 (copied to Resources/AquariumAudio/Real with English
    // names). It replaces the R73 synthesised WAVs where both exist and adds what the game had no sound for:
    // music (menu and lobby, the hunt), ending stingers, the hide countdown and the one minute warning, loops
    // (bubbles, swimming, the diver's breathing, the harpoon reel, the lid being dragged off, the tracker's scan),
    // the alien abilities, the drone, the harpoon hit and miss, the starved death and the unmorph.
    // Sounds that would give a hider away are heard only nearby (AlienHearing), panned toward their source.
    public sealed partial class AquariumGame
    {
        private readonly Dictionary<string, AudioClip> real = new Dictionary<string, AudioClip>();
        private AudioSource musicA, musicB, bubbleLoop, swimLoop, breathLoop, reelLoop, lidLoop, xrayLoop;
        private readonly AudioSource[] shotPool = new AudioSource[8];
        private int shotNext;
        private bool realLoaded;
        private AudioClip musicWanted;
        private readonly HashSet<int> r74Seen = new HashSet<int>();
        private readonly Dictionary<int, bool> r74Alive = new Dictionary<int, bool>();
        private int r74Round = -1;
        private bool minuteWarned, droneWasOn;
        private RoundPhase r74Phase = RoundPhase.Lobby;
        private struct LaterSound { public float at; public string key; public int owner; }
        private readonly List<LaterSound> later = new List<LaterSound>();

        // SfxScale keeps the new one-shots level with the old ones (the main source plays at .24).
        private const float MenuMusicVolume = .26f, PlayMusicVolume = .10f, SfxScale = .32f, LoopScale = .45f;

        // Called from LoadSoundSet (R73), after the synthesised set.
        private void LoadRealSounds()
        {
            foreach (var clip in Resources.LoadAll<AudioClip>("AquariumAudio/Real")) real[clip.name] = clip;
            if (real.Count == 0) return;
            realLoaded = true;
            // Same keys as before: the effect loop in Update plays them as it always did.
            foreach (var key in new[] { "shot", "burp", "egg", "screwOut", "lidOpen", "lidClosed", "squeezePop", "escape", "alarm", "feed", "splash", "eat" })
                if (real.ContainsKey(key)) sounds[key] = real[key];
            if (real.ContainsKey("eat")) sounds["hatch"] = real["eat"];   // 먹기,부화: one file for both
            // morph / unmorph share one effect: R74 tells them apart (TrackR74Effects), so the loop skips it.
            if (real.ContainsKey("morph")) sounds.Remove("morph");
            sounds.Remove("caught");   // R75: no jingle when an alien dies (the hit sounds cover it)
            if (real.ContainsKey("ping")) pingSound = real["ping"];
            if (real.ContainsKey("screwTurn") && screwSource != null) screwSource.clip = real["screwTurn"];

            musicA = NewSource(true); musicB = NewSource(true);
            bubbleLoop = NewLoop("ambBubbles"); swimLoop = NewLoop("swim"); breathLoop = NewLoop("breath");
            reelLoop = NewLoop("reel"); lidLoop = NewLoop("lidLoop"); xrayLoop = NewLoop("xray");
            for (int i = 0; i < shotPool.Length; i++) shotPool[i] = NewSource(false);
        }

        private AudioSource NewSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = loop; s.playOnAwake = false; s.spatialBlend = 0; s.volume = 0;
            return s;
        }

        private AudioSource NewLoop(string key)
        {
            AudioClip clip; if (!real.TryGetValue(key, out clip)) return null;
            var s = NewSource(true); s.clip = clip; return s;
        }

        private AudioClip Real(string key) { AudioClip c; return real.TryGetValue(key, out c) ? c : null; }

        // A one-shot: full for its owner, fading out over `hearing` metres for everyone else, panned toward it.
        // hearing <= 0 = heard everywhere at full volume.
        private void PlayAt(string key, Point position, int owner, float hearing, float volume)
        {
            PlayClipAt(Real(key), position, owner, hearing, volume);
        }

        private void PlayClipAt(AudioClip clip, Point position, int owner, float hearing, float volume)
        {
            if (clip == null) return;
            float gain = 1, pan = 0;
            var listener = SoundListener();
            if (hearing > 0)
            {
                if (listener == null) return;
                if (listener.id != owner)
                {
                    var delta = position - listener.position;
                    gain = Falloff(delta.Length, hearing);
                    var flat = new Vector3(delta.x, 0, delta.z);
                    if (flat.sqrMagnitude > .01f) pan = Vector3.Dot(view.Camera.transform.right, flat.normalized) * .6f;
                }
            }
            if (gain <= 0) return;
            // A free source first; only when all are busy does the oldest one get cut.
            AudioSource s = null;
            for (int i = 0; i < shotPool.Length && s == null; i++) { var c = shotPool[(shotNext + i) % shotPool.Length]; if (!c.isPlaying) s = c; }
            if (s == null) s = shotPool[shotNext];
            shotNext = (System.Array.IndexOf(shotPool, s) + 1) % shotPool.Length;
            s.Stop(); s.clip = clip; s.volume = volume * gain * SfxScale; s.panStereo = pan; s.pitch = 1; s.Play();
        }

        private void PlayFlat(string key, float volume) { PlayAt(key, new Point(), 0, 0, volume); }

        // Long cues (the countdown, the minute warning, the ending stingers) on their own source, never cut by effects.
        private AudioSource cueSource;
        private void PlayCue(string key, float volume)
        {
            var clip = Real(key); if (clip == null) return;
            if (cueSource == null) cueSource = NewSource(false);
            cueSource.Stop(); cueSource.clip = clip; cueSource.volume = volume * SfxScale; cueSource.Play();
        }

        // ---- Update ---------------------------------------------------------------------------------------
        private void UpdateR74Sounds()
        {
            UpdateUiSounds();   // R75
            if (!realLoaded) return;
            var state = session.Connected ? session.State : null;
            UpdateMusic(state);
            if (state == null) { StopLoops(); r74Seen.Clear(); r74Alive.Clear(); later.Clear(); r74Round = -1; r74Phase = RoundPhase.Lobby; return; }
            if (state.round != r74Round) { r74Round = state.round; r74Seen.Clear(); r74Alive.Clear(); later.Clear(); minuteWarned = false; lastBeepSecond = -1; }

            // The ending: win / lose for the players of a side, the neutral stinger for spectators or no winner.
            if (state.phase == RoundPhase.Finished && r74Phase == RoundPhase.Playing)
            {
                var me = session.IsSpectator ? null : PlayerById(state.players, session.LocalId);
                string key = me == null || state.winner < 0 ? "endNeutral" : state.winner == (int)me.team ? "win" : "lose";
                PlayCue(key, .9f);
            }
            r74Phase = state.phase;

            if (state.phase != RoundPhase.Playing || pause) { StopLoops(); if (state.phase != RoundPhase.Playing && cueSource != null && cueSource.clip != null && (cueSource.clip.name == "countdown" || cueSource.clip.name == "timeWarn")) cueSource.Stop(); return; }
            TrackR74Effects(state);
            TimeCues(state);
            UpdateLoops(state);
        }

        private void UpdateMusic(AquariumSnapshot state)
        {
            AudioClip want = null; float volume = 0;
            if (state == null || state.phase == RoundPhase.Lobby) { want = Real("musicMenu"); volume = MenuMusicVolume; }
            else if (state.phase == RoundPhase.Playing) { want = Real("musicPlay"); volume = PlayMusicVolume; }
            // Finished: the music fades out under the ending stinger.
            if (want != musicWanted)
            {
                musicWanted = want;
                var t = musicA; musicA = musicB; musicB = t;   // A = the one coming in
                if (want != null) { musicA.clip = want; musicA.volume = 0; musicA.Play(); }
            }
            float step = Time.unscaledDeltaTime / 1.2f;
            if (musicA.clip != null && musicWanted != null) musicA.volume = Mathf.MoveTowards(musicA.volume, volume, step * volume * 2);
            else musicA.volume = Mathf.MoveTowards(musicA.volume, 0, step);
            musicB.volume = Mathf.MoveTowards(musicB.volume, 0, step);
            if (musicB.isPlaying && musicB.volume <= 0) musicB.Stop();
            if (musicA.isPlaying && musicWanted == null && musicA.volume <= 0) musicA.Stop();
        }

        private void TrackR74Effects(AquariumSnapshot state)
        {
            var local = session.IsSpectator ? null : PlayerById(state.players, session.LocalId);
            if (state.effects != null)
                foreach (var e in state.effects)
                {
                    if (!r74Seen.Add(e.id) || state.time - e.born > 1f) continue;
                    switch (e.kind)
                    {
                        case "morph":
                        {
                            // Morph and unmorph send the same effect: the alien standing on it tells which.
                            Swimmer who = null; float best = 1.5f;
                            foreach (var p in state.players) { if (p.team != Team.Hider || !p.alive) continue; float d = (p.position - e.position).Length; if (d < best) { best = d; who = p; } }
                            bool back = who != null && who.species < 0;
                            string key = back ? "unmorph" : Random.value < .5f || Real("morphAlt") == null ? "morph" : "morphAlt";
                            PlayAt(key, e.position, who != null ? who.id : 0, AlienHearing, .8f);
                            break;
                        }
                        case "inkBomb": PlayAt("inkBomb", e.position, e.owner, 25f, .9f); break;
                        case "dash": PlayAt("dash", e.position, e.owner, AlienHearing, .8f); break;
                        case "glue": PlayAt("glue", e.position, e.owner, AlienHearing, .9f); break;
                        case "miss": PlayAt("miss", e.position, 0, AlienHearing, .8f); break;
                        case "vanish":
                            PlayAt("vanish", e.position, e.owner, AlienHearing, .8f);
                            later.Add(new LaterSound { at = Time.unscaledTime + AquariumModel.InvisibleSeconds, key = "unvanish", owner = e.owner });
                            break;
                        case "shock":
                            PlayAt("shock", e.position, e.owner, 25f, .9f);
                            // The diver caught in it hears the stun crackle for the whole freeze.
                            if (local != null && local.alive && local.team == Team.Seeker && (local.position - e.position).Length < AquariumModel.ShockRadius)
                                PlayFlat("stun", .8f);
                            break;
                        case "harpoonAlien":
                            PlayAt("harpoonHit", e.position, 0, 35f, .9f);
                            PlayAt("alienHit", e.position, 0, 35f, .8f);
                            break;
                    }
                }

            // Later sounds: the end of an invisibility (if its alien is still alive and swimming).
            for (int i = later.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < later[i].at) continue;
                var p = PlayerById(state.players, later[i].owner);
                if (p != null && p.alive && !p.escaped) PlayAt(later[i].key, p.position, p.id, AlienHearing, .8f);
                later.RemoveAt(i);
            }

            // A starved alien: its last groan.
            foreach (var p in state.players)
            {
                bool was; bool had = r74Alive.TryGetValue(p.id, out was);
                r74Alive[p.id] = p.alive;
                if (had && was && !p.alive && p.deathReason == "starved") PlayAt("starve", p.position, p.id, AlienHearing, .9f);
            }

            // The drone diver switching to the drone and back.
            bool droneOn = local != null && local.alive && local.team == Team.Seeker && local.droneControl;
            if (droneOn != droneWasOn) PlayFlat(droneOn ? "droneOn" : "droneOff", .8f);
            droneWasOn = droneOn;
        }

        private void TimeCues(AquariumSnapshot state)
        {
            // The hide countdown ends as the hunt starts (the clip is about 11 s; the hide is 20 s).
            // R75: one beep on every whole second of the last 10 (the old clip drifted off the beat).
            int secondsLeft = Mathf.CeilToInt(state.huntAt - state.time);
            if (state.time < state.huntAt && secondsLeft <= CountdownBeeps && secondsLeft != lastBeepSecond)
            { lastBeepSecond = secondsLeft; PlayFlat("countdownBeep", secondsLeft <= 3 ? .95f : .75f); }
            // One minute left in the hunt.
            float left = state.endsAt - Mathf.Max(state.time, state.huntAt);
            if (!minuteWarned && state.time >= state.huntAt && left <= 60f && left > 55f) { minuteWarned = true; PlayCue("timeWarn", .8f); }
            if (left <= 55f) minuteWarned = true;
        }

        private void UpdateLoops(AquariumSnapshot state)
        {
            var listener = SoundListener();
            var me = session.IsSpectator ? null : PlayerById(state.players, session.LocalId);
            bool inWater = me != null && me.alive && !me.escaped && me.swallowedBy == 0
                && (me.team == Team.Hider || (state.time >= state.huntAt && (me.entryAt <= 0 || state.time >= me.entryAt)));
            float dt = Time.unscaledDeltaTime;

            // Bubbles: the tank's bed, quiet, for everyone in the water (and the spectators).
            Fade(bubbleLoop, inWater || session.IsSpectator ? .22f : 0, 1, dt * .5f);

            // Swimming: louder the faster the local player moves (not while drone flying, working a drain or stunned).
            float speed = me == null ? 0 : me.velocity.Length;
            bool swimming = inWater && !me.droneControl && me.drainTask == DrainTask.None && me.stunnedUntil <= state.time;
            Fade(swimLoop, swimming ? Mathf.Clamp01((speed - .4f) / 3.5f) * .42f : 0, Mathf.Lerp(.95f, 1.15f, Mathf.Clamp01(speed / 6f)), dt * 1.5f);

            // The diver's regulator: breathing all the time, twice as fast when out of breath.
            bool diver = inWater && me.team == Team.Seeker;
            bool panting = diver && me.stamina < AquariumModel.StaminaMax(me) * .3f;
            Fade(breathLoop, diver ? .5f : 0, panting ? 2f : 1f, dt * .8f);

            // The harpoon line reeling a spear home: full for its diver, nearby for everyone else.
            float reel = 0; float reelPan = 0;
            if (state.harpoons != null && listener != null)
                foreach (var h in state.harpoons)
                {
                    if (!h.returning || h.stowedAt > 0 && h.stowedAt <= state.time) continue;
                    float g = h.owner == listener.id ? 1 : Falloff((h.position - listener.position).Length, 15f);
                    if (g > reel) { reel = g; reelPan = h.owner == listener.id ? 0 : Pan(h.position, listener); }
                }
            Fade(reelLoop, reel * .6f, 1, dt * 4f, reelPan);

            // An alien dragging a lid off (same hearing as the screw ratchet).
            float lid = 0, lidPan = 0;
            if (listener != null)
                foreach (var p in state.players)
                {
                    if (!p.alive || p.drainTask != DrainTask.Lid) continue;
                    float g = p.id == listener.id ? 1 : Falloff((p.position - listener.position).Length, ScrewHearing);
                    if (g > lid) { lid = g; lidPan = p.id == listener.id ? 0 : Pan(p.position, listener); }
                }
            Fade(lidLoop, lid * .6f, 1, dt * 3f, lidPan);

            // The tracker's bio scan beeping fast while it lasts (its diver only).
            bool scanning = false;
            if (me != null && me.alive && state.effects != null)
                foreach (var e in state.effects)
                    if (e.kind == "xray" && e.owner == me.id && state.time < e.born + e.duration) { scanning = true; break; }
            Fade(xrayLoop, scanning ? .45f : 0, 1, dt * 4f);
        }

        private float Pan(Point at, Swimmer listener)
        {
            var d = at - listener.position; var flat = new Vector3(d.x, 0, d.z);
            return flat.sqrMagnitude < .01f ? 0 : Vector3.Dot(view.Camera.transform.right, flat.normalized) * .6f;
        }

        private static void Fade(AudioSource s, float target, float pitch, float step, float pan = 0)
        {
            if (s == null) return;
            s.volume = Mathf.MoveTowards(s.volume, target * LoopScale, Mathf.Max(step, .0001f));
            s.pitch = Mathf.MoveTowards(s.pitch, pitch, step * 4);
            s.panStereo = Mathf.Lerp(s.panStereo, pan, .2f);
            if (s.volume > .001f && !s.isPlaying) s.Play();
            else if (s.volume <= .001f && s.isPlaying) s.Pause();
        }

        private AudioSource[] loops;
        private void StopLoops()
        {
            if (loops == null) loops = new[] { bubbleLoop, swimLoop, breathLoop, reelLoop, lidLoop, xrayLoop };
            foreach (var s in loops)
                if (s != null) { s.volume = 0; if (s.isPlaying) s.Pause(); }
        }
    }
}

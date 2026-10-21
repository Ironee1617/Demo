using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Opt-in audio integration checks; floor clearing below is a fixture, not a combat simulation.
    public sealed class TowerMusicSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class MusicReport
        {
            public bool success, stereoStreaming, playbackAdvances, sameThemeContinuous, themeBoundaryCrossfades;
            public bool loopWraps, disablePauses, reenableResumes, lifecycleCleanup, soundEffectsIndependent;
            public int tracks, pauseRequestedSamples, pauseSettledSamples, pausedSamples;
            public string finishedUtc;
            public List<string> samples = new List<string>(), errors = new List<string>();
        }
        private readonly MusicReport report = new MusicReport();
        private TowerGame game;
        private string reportPath;
        private bool initialized, finished, quitOnComplete;
        private float deadline;
        public bool IsFinished { get { return finished; } }
        public bool Succeeded { get { return finished && report.success; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Enter()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-towerMusicSmoke") < 0) return;
            int index = Array.IndexOf(args, "-towerMusicReport");
            string path = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.persistentDataPath, "tower-music.json");
            new GameObject("Tower music validation").AddComponent<TowerMusicSmokeDriver>().Initialize(null, path, true);
        }

        public void Initialize(TowerGame tower, string outputReportPath, bool quit = false)
        {
            if (initialized) throw new InvalidOperationException("Create a new music driver for each suite run.");
            if (!Path.IsPathRooted(outputReportPath)) throw new ArgumentException("Music report path must be absolute.");
            initialized = true; game = tower; reportPath = outputReportPath; quitOnComplete = quit;
            deadline = Time.realtimeSinceStartup + 120; Application.logMessageReceived += RecordLog;
            StartCoroutine(Guard(Suite()));
        }
        private void RecordLog(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message + "\n" + trace); }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }
        private void Update()
        { if (initialized && !finished && Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Finish(new TimeoutException("Music suite exceeded 120 seconds.")); } }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private IEnumerator Guard(IEnumerator body)
        {
            while (true)
            {
                bool more = false; object next = null; Exception error = null;
                try { more = body.MoveNext(); if (more) next = body.Current; } catch (Exception ex) { error = ex; }
                if (error != null) { Finish(error); yield break; }
                if (!more) yield break;
                yield return next;
            }
        }
        private static AudioSource Audible(TowerMusic music)
        {
            AudioSource result = null;
            foreach (var source in music.GetComponents<AudioSource>())
                if (source.clip && (result == null || source.volume > result.volume)) result = source;
            return result;
        }
        private void AdvanceFloor()
        {
            foreach (var enemy in game.enemies) if (enemy.root) Destroy(enemy.root);
            game.enemies.Clear();
            Require(game.run.ClearFloor(), "The floor fixture could not clear.");
            Require(game.run.ChooseOffer(game.run.Offers[0].Id), "The floor fixture could not choose its reward.");
            game.NextFloor();
        }
        private IEnumerator Suite()
        {
            yield return null;
            if (!game) game = FindFirstObjectByType<TowerGame>();
            Require(game != null, "TowerGame did not initialize.");
            game.SmokeControlled = true; game.SuppressProgressPersistence = true; game.enabled = false;
            game.NewRun(74);
            var music = game.GetComponentInChildren<TowerMusic>();
            Require(music != null, "TowerGame did not create theme music.");
            var effects = game.GetComponent<AudioSource>();
            Require(effects != null && effects.transform != music.transform, "BGM must have independent sound-effect volume.");
            float effectsVolume = effects.volume;
            yield return new WaitForSecondsRealtime(TowerMusic.CrossfadeSeconds + .2f);
            var first = Audible(music); var firstClip = first.clip; int before = first.timeSamples;
            AdvanceFloor();
            yield return new WaitForSecondsRealtime(.2f);
            Require(game.run.Floor == 2 && Audible(music) == first && first.clip == firstClip && first.timeSamples > before,
                "Advancing within the same theme restarted its music.");
            report.sameThemeContinuous = true;
            while (game.run.Floor < 5) AdvanceFloor();
            var oldTheme = music.CurrentTheme;
            AdvanceFloor();
            Require(game.run.Floor == 6 && game.run.Theme.Id != oldTheme && music.CurrentTheme == game.run.Theme.Id,
                "The new floor theme did not select its own music.");
            yield return new WaitForSecondsRealtime(.25f);
            var decks = music.GetComponents<AudioSource>(); int audible = 0; float sum = 0;
            foreach (var source in decks) { if (source.isPlaying && source.volume > .001f) audible++; sum += source.volume; }
            Require(audible == 2 && sum <= TowerMusic.DefaultVolume + .005f, "Theme change did not crossfade both decks at a bounded volume.");
            yield return new WaitForSecondsRealtime(TowerMusic.CrossfadeSeconds + .2f);
            Require(Array.FindAll(decks, source => source.isPlaying).Length == 1, "Outgoing music did not stop after the fade.");
            report.themeBoundaryCrossfades = true;

            foreach (var theme in TowerThemeCatalog.All)
            {
                var clip = Resources.Load<AudioClip>("Tower/Music/" + theme.Id.ToString().ToLowerInvariant());
                Require(clip != null && clip.length >= 90 && clip.length <= 120.1f, "Missing track or duration outside 90–120 seconds: " + theme.Id);
                Require(clip.channels == 2 && clip.loadType == AudioClipLoadType.Streaming, "Music must remain stereo and stream from disk: " + theme.Id);
                music.PlayTheme(theme.Id);
                yield return new WaitForSecondsRealtime(TowerMusic.CrossfadeSeconds + .2f);
                var source = Audible(music);
                Require(source != null && source.clip == clip && source.loop && source.spatialBlend == 0 && source.isPlaying, "Theme did not play as a 2D loop: " + theme.Id);
                before = source.timeSamples;
                yield return new WaitForSecondsRealtime(.2f);
                Require(source.timeSamples > before && Mathf.Abs(source.volume - TowerMusic.DefaultVolume) < .001f, "Playback did not advance at the expected volume: " + theme.Id);
                report.tracks++; report.samples.Add(theme.Id + ": " + clip.length.ToString("F2") + " s, " + clip.frequency + " Hz, stereo streaming");
            }
            report.stereoStreaming = report.playbackAdvances = true;
            var looping = Audible(music);
            looping.time = looping.clip.length - .25f;
            yield return new WaitForSecondsRealtime(.7f);
            Require(looping.isPlaying && looping.time < 2, "The music did not wrap at its loop boundary.");
            report.loopWraps = true;
            music.enabled = false; report.pauseRequestedSamples = looping.timeSamples;
            // Pause is consumed by the audio thread; allow its in-flight buffer to finish.
            yield return new WaitForSecondsRealtime(.15f);
            before = looping.timeSamples; report.pauseSettledSamples = before;
            yield return new WaitForSecondsRealtime(.2f);
            report.pausedSamples = looping.timeSamples;
            Require(!looping.isPlaying && report.pausedSamples == before,
                "Disabled music did not pause: playing=" + looping.isPlaying + ", samples=" + before + " -> " + report.pausedSamples);
            report.disablePauses = true;
            music.enabled = true;
            yield return new WaitForSecondsRealtime(.2f);
            Require(looping.isPlaying && looping.timeSamples > before, "Re-enabled music did not resume.");
            report.reenableResumes = true;
            Require(effects.volume == effectsVolume && effects.clip == null, "BGM changed the sound-effect source.");
            report.soundEffectsIndependent = true;
            Destroy(music.gameObject);
            yield return null;
            foreach (var source in decks) Require(source == null, "A music source survived its owner.");
            report.lifecycleCleanup = true;
            Finish(null);
        }
        private void Finish(Exception error)
        {
            if (finished) return; finished = true;
            if (error != null) report.errors.Add(error.ToString());
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= RecordLog;
            if (quitOnComplete) Application.Quit(report.success ? 0 : 1);
            else Debug.Log("Tower music suite " + (report.success ? "PASS: " : "FAIL: ") + reportPath);
        }
    }
}

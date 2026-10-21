using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Opt-in review of the actual sprite path and gameplay animation entry points.
    public sealed class TowerEdenReview : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool success;
            public int characters, distinctPoses, animationFrames;
            public List<string> checks = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        readonly Report report = new Report();
        readonly List<TowerActorArt> actors = new List<TowerActorArt>();
        Camera cameraView;
        RenderTexture target;
        Texture2D pixels;
        string output;
        bool finished;
        const float Dt = 1f / 24;
        public void Initialize(TowerGame game, string directory)
        {
            output = directory;
            Directory.CreateDirectory(output);
            game.SuppressProgressPersistence = true;
            game.SmokeControlled = true;
            game.enabled = false;
            game.view.enabled = false;
            Application.logMessageReceived += Log;
            StartCoroutine(Suite());
        }
        void Log(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception) report.errors.Add(message + "\n" + trace); }
        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
        void ValidateActor(int index)
        {
            var owner = new GameObject("Eden animation probe");
            owner.transform.position = new Vector3(1000, 0, 0);
            owner.transform.localScale = Vector3.one * .82f;
            try
            {
                var art = TowerActorArt.Create(owner.transform, index);
                var eden = art.GetComponent<TowerEdenArt>();
                Require(eden != null && art.GetComponent<TowerMythRig>() == null, "Eden did not select the pixel renderer.");
                var visited = new HashSet<int> { eden.CurrentFrame };
                Action<bool, bool, float, float, float, int> step = (moving, grounded, vertical, speed, dt, facing) =>
                {
                    art.SetLocomotion(grounded, vertical, false, speed);
                    art.SetPose(moving, false, false, facing, dt);
                    visited.Add(eden.CurrentFrame);
                    Require(Mathf.Abs(eden.Renderer.bounds.min.y - owner.transform.position.y) < .003f,
                        index + ": the measured pixel baseline left the actor root.");
                };
                step(false, true, 0, 0, .5f, 1);
                for (int i = 0; i < 60; i++) step(true, true, 0, 5, 1f / 60, 1);
                step(false, false, 5, 0, Dt, 1);
                Require(eden.CurrentFrame == 6, "Rising jump pose missing.");
                step(false, false, -3, 0, Dt, 1);
                Require(eden.CurrentFrame == 7, "Falling jump pose missing.");
                step(false, true, 0, 0, Dt, 1);
                Require(eden.CurrentFrame == 8, "Landing pose missing.");
                step(false, true, 0, 0, .2f, 1);
                art.PlayAttack(1);
                step(false, true, 0, 0, art.AttackHitTime(1) - .01f, 1);
                Require(eden.CurrentFrame == 9, "Attack released before damage contact.");
                step(false, true, 0, 0, .02f, 1);
                Require(eden.CurrentFrame == 10, "Attack did not release with damage contact.");
                step(false, true, 0, 0, .21f, 1);
                Require(eden.CurrentFrame == 11, "Attack follow-through missing.");
                step(true, true, 0, 5, .4f, 1);
                Require(eden.CurrentFrame >= 2 && eden.CurrentFrame <= 5, "Attack did not recover into running.");
                art.PlayCast(SkillEffect.HealPulse, 1);
                step(false, true, 0, 0, art.CastReleaseTime(SkillEffect.HealPulse, 1) - .01f, 1);
                Require(eden.CurrentFrame == 12, "Support cast released early.");
                step(false, true, 0, 0, .02f, 1);
                Require(eden.CurrentFrame == 13, "Support cast release is out of sync.");
                step(false, true, 0, 0, .5f, 1);
                art.PlayAttack(1); step(false, true, 0, 0, .2f, 1);
                art.InterruptAction(); step(true, true, 0, 5, Dt, 1);
                Require(eden.CurrentFrame >= 2 && eden.CurrentFrame <= 5, "Interrupted attack did not recover.");
                var sprite = eden.Renderer.sprite;
                step(true, true, 0, 5, 0, -1);
                Require(sprite == eden.Renderer.sprite && eden.Renderer.flipX, "Pause/facing changed the current pose.");
                art.SetPose(false, false, true, 1, Dt); visited.Add(eden.CurrentFrame);
                Require(eden.CurrentFrame == 14, "Hurt pose missing.");
                art.PlayDeath(); step(false, true, 0, 0, Dt, 1);
                Require(eden.CurrentFrame == 15, "Defeat pose missing.");
                Require(visited.Count == TowerEdenArt.FrameCount, "Not all sixteen authored poses were reached: " + index + "/" + visited.Count);
                var frames = TowerEdenArt.Frames(index);
                for (int i = 0; i < frames.Length; i++)
                {
                    Require(frames[i].texture.filterMode == FilterMode.Point, "Pixel sheet is blurred.");
                    Require(Mathf.Approximately(frames[0].pixelsPerUnit, frames[i].pixelsPerUnit), "An action resized the character.");
                }
                Require(TowerMythArt.PortraitSprite(index) == frames[0], "Selection portrait uses stale artwork.");
                report.characters++; report.distinctPoses += visited.Count;
                report.checks.Add(art.ProfileName + ": 16 poses, floor, jump/landing, damage/cast timing, recovery, pause, facing, fixed scale, portrait.");
            }
            finally { Destroy(owner); }
        }
        IEnumerator Suite()
        {
            Exception failure = null;
            try
            {
                for (int index = 100; index <= 104; index++) ValidateActor(index);
                var other = new GameObject("Other mythology preservation");
                var art = TowerActorArt.Create(other.transform, 105);
                Require(art.GetComponent<TowerMythRig>() != null && art.GetComponent<TowerEdenArt>() == null, "Non-Eden artwork changed path.");
                Destroy(other);
                Setup();
            }
            catch (Exception ex) { failure = ex; }
            if (failure != null) { Finish(failure); yield break; }
            for (int frame = 0; frame < 144; frame++)
            {
                try
                {
                    foreach (var art in actors)
                    {
                        int segment = frame / 24, local = frame % 24;
                        bool jumping = segment == 2 && local < 20;
                        bool moving = segment == 1;
                        float t = local / 20f;
                        float jump = jumping ? Mathf.Sin(t * Mathf.PI) * .85f : 0;
                        var position = art.transform.parent.position; position.y = jump;
                        art.transform.parent.position = position;
                        if (segment == 3 && local == 0) art.PlayAttack(.8f);
                        if (segment == 4 && local == 0) art.PlayCast(SkillEffect.HealPulse, .8f);
                        if (segment == 5 && local == 12) art.PlayDeath();
                        art.SetLocomotion(!jumping, jumping ? (local < 10 ? 5 : -5) : 0, false, moving ? 5 : 0);
                        art.SetPose(moving, false, segment == 5 && local == 0, 1, Dt);
                    }
                    Capture("animation-" + frame.ToString("000"));
                    report.animationFrames++;
                }
                catch (Exception ex) { failure = ex; }
                if (failure != null) { Finish(failure); yield break; }
                yield return null;
            }
            Finish(null);
        }
        void Setup()
        {
            cameraView = new GameObject("Eden pixel review camera").AddComponent<Camera>();
            cameraView.enabled = false; cameraView.orthographic = true; cameraView.orthographicSize = 1.85f;
            cameraView.aspect = 1280f / 360; cameraView.cullingMask = 1 << 30;
            cameraView.clearFlags = CameraClearFlags.SolidColor; cameraView.backgroundColor = new Color(.035f, .055f, .09f);
            cameraView.allowHDR = cameraView.allowMSAA = false;
            cameraView.transform.position = new Vector3(0, 1.3f, -10);
            target = new RenderTexture(1280, 360, 0, RenderTextureFormat.ARGB32) { antiAliasing = 1, filterMode = FilterMode.Point };
            target.Create(); cameraView.targetTexture = target;
            pixels = new Texture2D(1280, 360, TextureFormat.RGBA32, false);
            for (int i = 0; i < 5; i++)
            {
                var owner = new GameObject("Eden preview " + i);
                owner.transform.position = new Vector3((i - 2) * 2.5f, 0, 0);
                var art = TowerActorArt.Create(owner.transform, 100 + i); actors.Add(art);
                foreach (Transform child in owner.GetComponentsInChildren<Transform>()) child.gameObject.layer = 30;
            }
            var floor = new GameObject("Visible ground baseline"); floor.layer = 30;
            floor.transform.position = new Vector3(0, -.015f, 0); floor.transform.localScale = new Vector3(14, .03f, 1);
            var line = floor.AddComponent<SpriteRenderer>();
            line.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            line.color = new Color(.24f, .32f, .42f);
        }
        void Capture(string name)
        {
            cameraView.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 360), 0, 0); pixels.Apply();
            RenderTexture.active = previous;
            string path = Path.Combine(output, name + ".png");
            File.WriteAllBytes(path, pixels.EncodeToPNG()); report.screenshots.Add(path);
        }
        void Finish(Exception failure)
        {
            if (finished) return; finished = true;
            if (failure != null) report.errors.Add(failure.ToString());
            report.success = report.errors.Count == 0 && report.characters == 5 && report.distinctPoses == 80 && report.animationFrames == 144;
            File.WriteAllText(Path.Combine(output, "eden-report.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.success ? 0 : 1);
        }
        void OnDestroy()
        {
            Application.logMessageReceived -= Log;
            if (target != null) { target.Release(); Destroy(target); }
            if (pixels != null) Destroy(pixels);
        }
    }
}

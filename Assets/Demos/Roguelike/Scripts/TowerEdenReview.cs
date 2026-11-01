using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Opt-in review (-towerEdenReview) of the pixel god path: every companion, from the Eden angels on,
    // is drawn by TowerGodPixelArt in the style of chuchuking's Zeus. Checks floor contact, body frames,
    // weapon timing against damage release, pause, facing, fixed scale and portraits, then records a strip.
    public sealed class TowerEdenReview : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool success;
            public int characters, distinctPoses, animationFrames;
            public List<string> checks = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        public const int Characters = 50;
        static readonly int[] Preview = { 105, 100, 108, 109, 111, 115, 125, 148 };
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

        static float Px(float units, Transform owner) { return units * TowerGodPixelArt.PixelsPerUnit / Mathf.Abs(owner.lossyScale.y); }

        void ValidateActor(int index)
        {
            var owner = new GameObject("Pixel god animation probe");
            owner.transform.position = new Vector3(1000, 0, 0);
            owner.transform.localScale = Vector3.one * .82f;
            try
            {
                var art = TowerActorArt.Create(owner.transform, index);
                var god = art.GetComponent<TowerGodPixelArt>();
                Require(god != null, index + ": the companion did not select the pixel god renderer.");
                var front = god.WeaponPivot.parent;
                var visited = new HashSet<int> { god.CurrentFrame };
                Action<bool, bool, float, float, float, int> step = (moving, grounded, vertical, speed, dt, facing) =>
                {
                    art.SetLocomotion(grounded, vertical, false, speed);
                    art.SetPose(moving, false, false, facing, dt);
                    visited.Add(god.CurrentFrame);
                    Require(Mathf.Abs(god.Body.transform.position.y - owner.transform.position.y) < .003f,
                        index + ": the boots left the actor root.");
                };
                for (int i = 0; i < 30; i++) step(false, true, 0, 0, 1f / 15, 1);
                for (int i = 0; i < 60; i++) step(true, true, 0, 5, 1f / 60, 1);
                step(false, false, 5, 0, Dt, 1);
                Require(god.CurrentFrame == TowerGodPixelArt.Jump, index + ": rising jump frame missing.");
                step(false, false, -3, 0, Dt, 1);
                Require(god.CurrentFrame == TowerGodPixelArt.Fall, index + ": falling frame missing.");
                step(false, true, 0, 0, .3f, 1);

                // Attacks: the weapon (or the bow string fist) must move sharply across the damage release.
                art.PlayAttack(1);
                step(false, true, 0, 0, art.AttackReleaseTime(1) - .01f, 1);
                float before = god.WeaponPivot.eulerAngles.z; Vector3 frontBefore = front.position;
                var backFist = god.transform.Find("Pixel god facing/Pixel god pose/Back fist");
                Vector3 backBefore = backFist != null ? backFist.position : Vector3.zero;
                step(false, true, 0, 0, .1f, 1);
                float turned = Mathf.Abs(Mathf.DeltaAngle(before, god.WeaponPivot.eulerAngles.z));
                float moved = Px(Vector3.Distance(frontBefore, front.position), owner.transform);
                float drawn = backFist != null ? Px(Vector3.Distance(backBefore, backFist.position), owner.transform) : 0;
                Require(turned > 25 || moved > 3 || drawn > 3, index + ": the " + god.Kind + " strike does not land on the damage release (" + turned + " deg, " + moved + " px).");
                step(true, true, 0, 5, 1, 1);
                Require(god.CurrentFrame >= TowerGodPixelArt.Run && god.CurrentFrame < TowerGodPixelArt.Run + 4, index + ": attack did not recover into running.");

                // Casting lifts the focus overhead before the release.
                step(false, true, 0, 0, .3f, 1);
                float rest = front.localPosition.y * TowerGodPixelArt.PixelsPerUnit;
                art.PlayCast(SkillEffect.HealPulse, 1);
                step(false, true, 0, 0, art.CastReleaseTime(SkillEffect.HealPulse, 1) - .01f, 1);
                Require(front.localPosition.y * TowerGodPixelArt.PixelsPerUnit - rest > 6, index + ": casting did not raise the focus.");
                step(false, true, 0, 0, 1, 1);

                // A zero-time update (pause) keeps the pose; facing only mirrors.
                var sprite = god.Body.sprite; var hand = front.localPosition;
                step(true, true, 0, 5, 0, -1);
                Require(sprite == god.Body.sprite && hand == front.localPosition && god.transform.Find("Pixel god facing").localScale.x < 0,
                    index + ": pause or facing changed the current pose.");
                art.SetPose(false, false, true, 1, Dt); visited.Add(god.CurrentFrame);
                Require(god.CurrentFrame == TowerGodPixelArt.Hurt, index + ": hurt frame missing.");
                art.PlayDeath(); for (int i = 0; i < 12; i++) art.SetPose(false, false, false, 1, Dt);
                Require(Mathf.Abs(Mathf.DeltaAngle(0, god.transform.Find("Pixel god facing/Pixel god pose").localEulerAngles.z)) > 60,
                    index + ": defeat did not topple the body.");
                Require(visited.Count == TowerGodPixelArt.BodyFrames, "Not every body frame was reached: " + index + "/" + visited.Count);

                var frames = TowerGodPixelArt.Frames(index);
                for (int i = 0; i < frames.Length; i++)
                {
                    Require(frames[i].texture.filterMode == FilterMode.Point, "Pixel sheet is blurred: " + index);
                    Require(Mathf.Approximately(frames[0].pixelsPerUnit, frames[i].pixelsPerUnit), "An action resized the character: " + index);
                }
                Require(TowerMythArt.PortraitSprite(index) == TowerGodPixelArt.CardSprite(index), "Selection portrait uses stale artwork: " + index);
                report.characters++; report.distinctPoses += visited.Count;
                report.checks.Add(art.ProfileName + " (" + god.Kind + "): 11 frames, floor, jump/fall, strike on release, cast lift, pause, facing, defeat, portrait.");
            }
            finally { Destroy(owner); }
        }
        IEnumerator Suite()
        {
            Exception failure = null;
            try
            {
                for (int index = TowerMythArt.FirstArtIndex; index < TowerMythArt.FirstArtIndex + TowerMythArt.CharacterCount; index++) ValidateActor(index);
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
            cameraView = new GameObject("Pixel god review camera").AddComponent<Camera>();
            cameraView.enabled = false; cameraView.orthographic = true; cameraView.orthographicSize = 1.85f;
            cameraView.aspect = 1280f / 360; cameraView.cullingMask = 1 << 30;
            cameraView.clearFlags = CameraClearFlags.SolidColor; cameraView.backgroundColor = new Color(.035f, .055f, .09f);
            cameraView.allowHDR = cameraView.allowMSAA = false;
            cameraView.transform.position = new Vector3(0, 1.3f, -10);
            target = new RenderTexture(1280, 360, 0, RenderTextureFormat.ARGB32) { antiAliasing = 1, filterMode = FilterMode.Point };
            target.Create(); cameraView.targetTexture = target;
            pixels = new Texture2D(1280, 360, TextureFormat.RGBA32, false);
            for (int i = 0; i < Preview.Length; i++)
            {
                var owner = new GameObject("Pixel god preview " + Preview[i]);
                owner.transform.position = new Vector3((i - (Preview.Length - 1) * .5f) * 1.6f, 0, 0);
                owner.transform.localScale = Vector3.one * .82f;
                var art = TowerActorArt.Create(owner.transform, Preview[i]); actors.Add(art);
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
            report.success = report.errors.Count == 0 && report.characters == Characters
                && report.distinctPoses == Characters * TowerGodPixelArt.BodyFrames && report.animationFrames == 144;
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

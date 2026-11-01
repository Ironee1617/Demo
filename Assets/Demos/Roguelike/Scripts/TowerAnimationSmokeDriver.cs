using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Deterministic actual-renderer QA. Roots move at production scale against
    // stationary world floors; mesh boundary probes measure the visible soles.
    public sealed class TowerAnimationSmokeDriver : MonoBehaviour
    {
        const float Dt = 1f / 60, BodyHeight = 1.75f, ProductionScale = .82f;
        const int Width = 1280, Height = 720, PreviewFrames = 240, RunFrames = 180;
        const int GaitWarmupFrames = 45, GaitMeasuredFrames = 120;
        const int StopMeasuredFrames = 24;
        [Serializable] sealed class CharacterReport
        {
            public int artIndex, column, row, meshParts, runGeometrySamples, spriteIdentityChanges;
            public string name, role;
            public bool articulatedRun, stableScale = true, stableArtwork = true, pausedPose = true, mirroredFacing;
            public float maximumVertexStep, maximumHeightStep, minimumHeight = float.MaxValue, maximumHeight;
        }
        [Serializable] sealed class GaitReport
        {
            public float worldSpeed, maximumContactSlipRatio, meanContactSlipRatio, maximumContactDrift, minimumClearance = float.MaxValue, maximumFootLift, leftStride, rightStride;
            public int contactSamples, airborneSamples, hoverBodySamples;
            public float minimumHoverHeight = float.MaxValue, maximumHoverHeight;
            public bool rootMatched, groundedContacts, alternatingStride;
            public FootContactReport leftFoot = new FootContactReport(), rightFoot = new FootContactReport();
            public bool rigidShoes, walkingSupport;
            public int measuredFrames;
            [NonSerialized] public float slipSum;
            [NonSerialized] public Vector2 minimumLeft = Vector2.one * float.MaxValue, maximumLeft = Vector2.one * float.MinValue, minimumRight = Vector2.one * float.MaxValue, maximumRight = Vector2.one * float.MinValue;
        }
        [Serializable] sealed class FootContactReport
        {
            public int sampledFrames, stanceFrames, groundedStanceFrames, stancePairs, stanceEntries;
            public int maximumGapFrame, maximumSlipFrame, maximumShoeStretchFrame;
            public float minimumClearance = float.MaxValue, maximumFootLift, maximumStanceGap;
            public float maximumContactSlipRatio, maximumFrameDrift, maximumStanceDrift, maximumShoeStretch, maximumShoeAngle;
            public float contactCoverage;
            public bool contactMaintained, rootMatched, rigidShoe;
        }
        sealed class FootProbe
        {
            public int[] vertices, core;
            public int heel, toe;
            public float restMinimumY, stanceDrift;
            public bool previousStance;
        }
        [Serializable] sealed class StopFootReport
        {
            public int sampledFrames, lockedFrames, landingFrame = -1;
            public bool initiallyPlanted, settled, stationary, rigidShoe;
            public float initialClearance, finalClearance, minimumClearance = float.MaxValue;
            public float maximumPlantedGap, maximumWorldDrift, maximumShoeStretch;
        }
        [Serializable] sealed class StopSpeedReport
        {
            public float worldSpeed;
            public int sampledFrames;
            public bool passed, finiteGeometry = true;
            public float minimumHoverClearance = float.MaxValue;
            public StopFootReport leftFoot = new StopFootReport(), rightFoot = new StopFootReport();
        }
        [Serializable] sealed class StopReport
        {
            public int artIndex, stopSamples;
            public string name;
            public bool hovering, passed = true;
            public float commonFloor, groundTolerance, driftTolerance;
            public List<StopSpeedReport> speeds = new List<StopSpeedReport>();
        }
        sealed class StopProbe
        {
            public FootProbe shoe;
            public float[] lockedWorldX;
            public bool locked;
        }
        [Serializable] sealed class CatalogReport
        {
            public int artIndex, vertexCount, motionSamples, distinctRunGeometry, sourceTextureId, faceProbeCount, weaponProbeCount, actionContactSamples, leftSoleVertex, rightSoleVertex, weaponAxisFirst, weaponAxisSecond;
            public string name, role, maximumVertexStepMotion, maximumGarmentStretchMotion, maximumWeaponStretchMotion, maximumActionSlipMotion;
            public int maximumVertexStepFrame, maximumVertexStepVertex, maximumVertexStepPart, maximumGarmentStretchFrame, maximumWeaponStretchFrame, maximumActionSlipFrame;
            public bool hovering, articulatedRun, finiteTopology = true, groundedFeet, rootMatchedGait, continuousActionSteps, dynamicAttack, dynamicCast, rigidFace, garmentCoreCovered, garmentCoreMeasured, garmentProbeOccluded, rigidGarment, weaponCoreMeasured, weaponProbeOccluded, rigidWeapon, rigidShoes, pausedPose = true, attackRecovery, castRecovery, interruptedRecovery, stableScale = true, stableArtwork = true;
            public float maximumVertexStep, maximumFaceStretch, maximumSourceMappingError, maximumGarmentStretch, maximumWeaponStretch, maximumActionStanceSlipRatio, maximumActionContactDrift, minimumActionClearance = float.MaxValue, attackHandExcursion, castHandExcursion, attackTorsoAngle, castTorsoAngle, attackWeaponArc, attackReturnError, castReturnError, interruptedReturnError;
            public Vector2 garmentProbeCenter, leftSoleRest, rightSoleRest, bodyHipProbeRestPoint, bodyShoulderProbeRestPoint, maximumVertexStepRestPoint;
            public Vector2[] faceProbeRestPoints, garmentProbeRestPoints, weaponProbeRestPoints;
            public int[] weaponIndices;
            public int leftFootProbeCount, rightFootProbeCount;
            public float commonFloor, groundTolerance, maximumShoeStretch;
            public FootContactReport leftActionFoot = new FootContactReport(), rightActionFoot = new FootContactReport();
            public List<GaitReport> gaits = new List<GaitReport>();
            [NonSerialized] public Vector3[] previousGeometry, restGeometry;
            [NonSerialized] public MeshProbe[] faceProbes, garmentProbes;
            [NonSerialized] public MeshProbe bodyHipProbe, bodyShoulderProbe;
            [NonSerialized] public int[] geometryParts;
            [NonSerialized] public FootProbe leftFootProbe, rightFootProbe;
            [NonSerialized] public Matrix4x4 previousRenderToWorld;
            [NonSerialized] public float minimumWeaponAngle = float.MaxValue, maximumWeaponAngle = float.MinValue;
            [NonSerialized] public bool contactsReady, weaponAngleReady;
            [NonSerialized] public float previousWeaponAngle, continuousWeaponAngle;
            [NonSerialized] public CompanionRole combatRole;
        }
        [Serializable] sealed class AnimationReport
        {
            public string mode = "towerAnimationSmoke", probeContract = "non-eden-rendered-foot-surfaces-v4", finishedUtc, frameDirectory;
            public string stopProbeContract = "planted-stop-surfaces-v1";
            public bool success, baseline, naturalMotion;
            public int sampledFrames, previewFrames, framesPerSecond = 60, width = Width, height = Height, catalogCharacters, catalogMotionSamples, garmentCoresMeasured, garmentProbesOccluded, weaponCoresMeasured, weaponProbesOccluded, groundedCharacters, hoveringCharacters;
            public float actorScale = ProductionScale;
            public int gaitWarmupFrames = GaitWarmupFrames, gaitMeasuredFrames = GaitMeasuredFrames;
            public int stopCharacters, stopSamples, stopWarmupFrames = GaitWarmupFrames, stopMeasuredFrames = StopMeasuredFrames;
            public List<CharacterReport> characters = new List<CharacterReport>();
            public List<CatalogReport> catalog = new List<CatalogReport>();
            public List<TransitionReport> transitions = new List<TransitionReport>();
            public List<StopReport> stopping = new List<StopReport>();
            public List<string> frameMotions = new List<string>(), failures = new List<string>(), errors = new List<string>(), screenshots = new List<string>(), runScreenshots = new List<string>();
        }
        [Serializable] sealed class TransitionReport
        {
            public int artIndex, sampledTransitions, largestVertex, largestPart = -1;
            public string name, largestTransition;
            public Vector3 largestBefore, largestAfter;
            public float startStep, stopStep, jumpStep, apexStep, landingStep, dashEntryStep, dashExitStep, hurtStep, turnStep, maximumStep;
            public bool continuous, pausedPose = true;
        }
        struct MeshProbe { public int a, b, c; public Vector3 weights; }
        sealed class Fixture
        {
            public TowerActorArt art;
            public CharacterReport report;
            public Vector3[] previousVertices;
            public Sprite[] previousSprites;
            public float previousHeight;
            public readonly HashSet<ulong> runGeometry = new HashSet<ulong>();
        }
        readonly AnimationReport report = new AnimationReport();
        readonly List<Fixture> fixtures = new List<Fixture>();
        Transform gallery, floor;
        Camera cameraView;
        RenderTexture target;
        Texture2D pixels;
        string outputDirectory, reportPath;
        bool finishing;

        public void Initialize(TowerGame game)
        {
            var args = Environment.GetCommandLineArgs();
            outputDirectory = Argument(args, "-towerAnimationCapture") ?? Path.Combine(Application.persistentDataPath, "tower-animation");
            reportPath = Argument(args, "-towerAnimationReport") ?? Path.Combine(outputDirectory, "animation-report.json");
            report.baseline = Array.IndexOf(args, "-towerAnimationBaseline") >= 0; report.frameDirectory = outputDirectory;
            game.SmokeControlled = true; game.enabled = false;
            if (game.view) game.view.enabled = false;
            foreach (Transform child in game.transform) child.gameObject.SetActive(false);
            Application.logMessageReceived += Log; StartCoroutine(Guard(Suite()));
        }
        static string Argument(string[] args, string key)
        { int at = Array.IndexOf(args, key); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
        void Log(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message + "\n" + trace); }
        IEnumerator Guard(IEnumerator body)
        {
            while (true)
            {
                bool more = false; Exception error = null;
                try { more = body.MoveNext(); } catch (Exception ex) { error = ex; }
                if (error != null) { Finish(error); yield break; }
                if (!more) { Finish(null); yield break; }
                yield return body.Current;
            }
        }
        IEnumerator Suite()
        {
            Require(Path.IsPathRooted(outputDirectory) && Path.IsPathRooted(reportPath), "Animation output paths must be absolute.");
            Directory.CreateDirectory(outputDirectory); Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            SetupPreview();
            for (int frame = 0; frame < PreviewFrames; frame++)
            {
                float speed = frame < 15 || frame >= 207 ? 0 : frame < 45 ? 1.5f : frame < 75 ? 3 : 5;
                string phase = frame < 15 ? "IDLE" : frame < 45 ? "WALK 1.5" : frame < 75 ? "RUN 3" : frame < 90 ? "RUN 5" : frame < 126 ? "ATTACK + RUN" : frame < 153 ? "RECOVERY + RUN" : frame < 189 ? "CAST + RUN" : frame < 207 ? "RUN 5" : "STOP + IDLE";
                AdvancePreview(speed, frame == 90, frame == 153, frame);
                report.frameMotions.Add(phase); Capture(frame); report.sampledFrames++;
                if (frame % 6 == 5) yield return null;
            }
            foreach (var fixture in fixtures) ValidatePreview(fixture);
            for (int frame = 0; frame < RunFrames; frame++)
            {
                float speed = frame < 60 ? 1.5f : frame < 120 ? 3 : 5;
                AdvancePreview(speed, false, false, -1); Capture(frame, "run-"); report.previewFrames++;
                if (frame % 6 == 5) yield return null;
            }
            for (int index = 0; index <= 16; index++)
                if (index != 15) { ValidateTransitions(index); yield return null; }
            for (int index = TowerMythArt.FirstArtIndex; index < TowerMythArt.FirstArtIndex + TowerMythArt.CharacterCount; index++)
            { ValidateTransitions(index); yield return null; }
            report.naturalMotion = report.failures.Count == 0;
        }
        void ValidateTransitions(int index)
        {
            var owner = new GameObject("Locomotion transition fixture " + index); owner.transform.localScale = Vector3.one * ProductionScale;
            try
            {
                var art = TowerActorArt.Create(owner.transform, index); SetLayer(owner.transform, 31);
                var entry = new TransitionReport { artIndex = index, name = art.ProfileName }; report.transitions.Add(entry);
                SettleTransition(art, false, true, 0, false, 0, 1);
                entry.startStep = TransitionStep(art, entry, "start", true, true, 0, false, 3, false, 1);
                SettleTransition(art, true, true, 0, false, 3, 1);
                entry.stopStep = TransitionStep(art, entry, "stop", false, true, 0, false, 0, false, 1);
                SettleTransition(art, false, true, 0, false, 0, 1);
                entry.jumpStep = TransitionStep(art, entry, "jump", false, false, 6, false, 0, false, 1);
                SettleTransition(art, false, false, .1f, false, 0, 1);
                entry.apexStep = TransitionStep(art, entry, "apex", false, false, -.1f, false, 0, false, 1);
                SettleTransition(art, false, false, -6, false, 0, 1);
                entry.landingStep = TransitionStep(art, entry, "landing", false, true, 0, false, 0, false, 1);
                SettleTransition(art, true, true, 0, false, 3, 1);
                entry.dashEntryStep = TransitionStep(art, entry, "dash entry", true, true, 0, true, 7, false, 1);
                SettleTransition(art, true, true, 0, true, 7, 1);
                entry.dashExitStep = TransitionStep(art, entry, "dash exit", true, true, 0, false, 3, false, 1);
                SettleTransition(art, false, true, 0, false, 0, 1);
                entry.hurtStep = TransitionStep(art, entry, "hurt", false, true, 0, false, 0, true, 1);
                SettleTransition(art, true, true, 0, false, 3, 1);
                entry.turnStep = TransitionStep(art, entry, "turn", true, true, 0, false, -3, false, -1);
                entry.continuous = entry.maximumStep <= .18f;
                Check(entry.continuous, entry.name + ": " + entry.largestTransition + " changes the rendered pose by " + entry.maximumStep.ToString("F3") + " body heights in one 60 Hz frame.");
                Check(entry.pausedPose, entry.name + ": paused locomotion transition changes geometry.");
            }
            finally { owner.SetActive(false); Destroy(owner); }
        }
        static void SettleTransition(TowerActorArt art, bool moving, bool grounded, float vertical, bool dash, float speed, int facing)
        {
            for (int frame = 0; frame < 45; frame++)
            {
                art.transform.parent.position += Vector3.right * speed * Dt;
                art.SetLocomotion(grounded, vertical, dash, Mathf.Abs(speed), speed); art.SetPose(moving, false, false, facing, Dt);
            }
        }
        static float TransitionStep(TowerActorArt art, TransitionReport entry, string name, bool moving, bool grounded, float vertical, bool dash, float speed, bool hurt, int facing)
        {
            var before = Vertices(art); int previousFacing = art.Facing;
            art.transform.parent.position += Vector3.right * speed * Dt;
            art.SetLocomotion(grounded, vertical, dash, Mathf.Abs(speed), speed); art.SetPose(moving, false, hurt, facing, Dt);
            var after = Vertices(art);
            // Mirroring is an intentional view change. Compare articulated poses
            // in the same facing so this catches gait resets, not the mirror itself.
            if (previousFacing != facing) for (int vertex = 0; vertex < before.Length; vertex++) before[vertex].x *= -1;
            float step = VertexError(before, after) / BodyHeight;
            if (step > entry.maximumStep)
            {
                entry.maximumStep = step; entry.largestTransition = name;
                float maximum = 0;
                for (int vertex = 0; vertex < before.Length; vertex++)
                    if (Vector3.Distance(before[vertex], after[vertex]) > maximum)
                    { maximum = Vector3.Distance(before[vertex], after[vertex]); entry.largestVertex = vertex; entry.largestBefore = before[vertex]; entry.largestAfter = after[vertex]; }
            }
            var paused = Pose(art); art.SetPose(moving, false, hurt, facing, 0); entry.pausedPose &= Same(paused, Pose(art), .000001f);
            entry.sampledTransitions++; return step;
        }
        void SetupPreview()
        {
            gallery = new GameObject("Moving animation quality fixtures").transform;
            floor = new GameObject("Stationary world floor reference").transform;
            cameraView = new GameObject("Animation quality camera").AddComponent<Camera>(); cameraView.enabled = false;
            cameraView.orthographic = true; cameraView.orthographicSize = 2.65f; cameraView.aspect = Width / (float)Height;
            cameraView.clearFlags = CameraClearFlags.SolidColor; cameraView.backgroundColor = new Color(.025f, .04f, .065f);
            cameraView.cullingMask = 1 << 30; cameraView.allowHDR = cameraView.allowMSAA = false;
            cameraView.transform.position = new Vector3(0, .4f, -10);
            target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32) { antiAliasing = 1, filterMode = FilterMode.Point }; target.Create(); cameraView.targetTexture = target;
            pixels = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            int[] indices = { 106, 108, 109, 105, 118, 133, 0 };
            for (int slot = 0; slot < indices.Length; slot++)
            {
                int row = slot / 4, col = slot % 4;
                var owner = new GameObject("Quality actor " + indices[slot]).transform; owner.SetParent(gallery, false);
                owner.localPosition = new Vector3((col - 1.5f) * 2.22f, row == 0 ? .85f : -1.62f, 0); owner.localScale = Vector3.one * ProductionScale;
                var art = TowerActorArt.Create(owner, indices[slot]);
                var entry = new CharacterReport { artIndex = indices[slot], column = col, row = row, name = art.ProfileName, role = indices[slot] == 0 ? "Hero" : TowerRun.Definitions[indices[slot] - 100].Role.ToString() };
                var fixture = new Fixture { art = art, report = entry };
                art.SetLocomotion(true, 0, false, 0, 0); art.SetPose(false, false, false, 1, 0);
                entry.meshParts = art.GetComponentsInChildren<MeshFilter>().Length + art.GetComponentsInChildren<SpriteRenderer>().Length; fixtures.Add(fixture); report.characters.Add(entry);
            }
            for (int row = 0; row < 2; row++)
            {
                float y = (row == 0 ? .85f : -1.62f) - .035f;
                TowerWorld.SpriteObject(floor, "World floor " + row, new Vector2(25, y), new Vector2(70, .014f), new Color(.3f, .4f, .45f), -1);
                for (int tick = -10; tick <= 65; tick++)
                    TowerWorld.SpriteObject(floor, "Meter reference " + row + " / " + tick, new Vector2(tick, y - .06f), new Vector2(.012f, .12f), tick % 5 == 0 ? new Color(.7f, .6f, .3f) : new Color(.25f, .34f, .4f), -1);
            }
            SetLayer(gallery, 30); SetLayer(floor, 30);
        }
        void AdvancePreview(float speed, bool attack, bool cast, int frame)
        {
            gallery.position += Vector3.right * speed * Dt;
            cameraView.transform.position = new Vector3(gallery.position.x, .4f, -10);
            foreach (var fixture in fixtures)
            {
                if (attack) fixture.art.PlayAttack(.6f);
                if (cast) fixture.art.PlayCast(SkillEffect.Fireball, .6f);
                fixture.art.SetLocomotion(true, 0, false, speed, speed); fixture.art.SetPose(speed > 0, false, false, 1, Dt);
                if (frame < 0) continue;
                MeasurePreview(fixture, frame);
                var before = Pose(fixture.art); fixture.art.SetPose(speed > 0, false, false, 1, 0);
                fixture.report.pausedPose &= Same(before, Pose(fixture.art), .000001f);
                if (frame >= 45 && frame < 90) fixture.runGeometry.Add(GeometryHash(fixture.art));
            }
        }
        void ValidatePreview(Fixture fixture)
        {
            var entry = fixture.report; entry.runGeometrySamples = fixture.runGeometry.Count;
            entry.articulatedRun = entry.meshParts > 0 && entry.runGeometrySamples >= 20;
            fixture.art.SetPose(false, false, false, 1, 0); var right = Vertices(fixture.art);
            fixture.art.SetPose(false, false, false, -1, 0); var left = Vertices(fixture.art);
            entry.mirroredFacing = right.Length == left.Length && right.Length > 0;
            for (int vertex = 0; vertex < right.Length && entry.mirroredFacing; vertex++)
                entry.mirroredFacing &= Vector3.Distance(new Vector3(-right[vertex].x, right[vertex].y, right[vertex].z), left[vertex]) < .001f;
            if (entry.artIndex >= 100)
            {
                Check(entry.articulatedRun, entry.name + ": running must articulate the rendered model.");
                Check(entry.stableScale && entry.stableArtwork, entry.name + ": animation rescales or replaces its source artwork.");
            }
            Check(entry.pausedPose, entry.name + ": a zero-time update changes its pose.");
            if (entry.artIndex >= 100) Check(entry.mirroredFacing, entry.name + ": left/right poses do not mirror around the grounded root.");
            fixture.art.SetPose(false, false, false, 1, 0);
        }
        void MeasurePreview(Fixture fixture, int frame)
        {
            var art = fixture.art; var entry = fixture.report; var renderers = art.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds; foreach (var renderer in renderers) { bounds.Encapsulate(renderer.bounds); if (entry.artIndex >= 100) entry.stableScale &= Vector3.Distance(Abs(renderer.transform.localScale), Vector3.one) < .00001f; }
            entry.minimumHeight = Mathf.Min(entry.minimumHeight, bounds.size.y); entry.maximumHeight = Mathf.Max(entry.maximumHeight, bounds.size.y);
            if (frame > 0) entry.maximumHeightStep = Mathf.Max(entry.maximumHeightStep, Mathf.Abs(bounds.size.y - fixture.previousHeight)); fixture.previousHeight = bounds.size.y;
            var sprites = art.GetComponentsInChildren<SpriteRenderer>(); var identities = new Sprite[sprites.Length];
            for (int sprite = 0; sprite < sprites.Length; sprite++)
            {
                identities[sprite] = sprites[sprite].sprite;
                // Pixel gods flip body frames cut from one sheet; replacing that sheet is what counts as new artwork.
                if (fixture.previousSprites != null && fixture.previousSprites[sprite] != identities[sprite] &&
                    (fixture.previousSprites[sprite] == null || identities[sprite] == null || fixture.previousSprites[sprite].texture != identities[sprite].texture)) entry.spriteIdentityChanges++;
            }
            fixture.previousSprites = identities; entry.stableArtwork = entry.spriteIdentityChanges == 0;
            var vertices = Vertices(art);
            if (fixture.previousVertices != null && fixture.previousVertices.Length == vertices.Length)
                for (int vertex = 0; vertex < vertices.Length; vertex++) entry.maximumVertexStep = Mathf.Max(entry.maximumVertexStep, Vector3.Distance(vertices[vertex], fixture.previousVertices[vertex]));
            fixture.previousVertices = vertices;
        }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static float VertexError(Vector3[] a, Vector3[] b)
        { if (a.Length != b.Length) return float.MaxValue; float maximum = 0; for (int vertex = 0; vertex < a.Length; vertex++) maximum = Mathf.Max(maximum, Vector3.Distance(a[vertex], b[vertex])); return maximum; }
        static ulong GeometryHash(TowerActorArt art)
        {
            ulong hash = 1469598103934665603UL;
            foreach (var filter in art.GetComponentsInChildren<MeshFilter>()) foreach (var vertex in filter.sharedMesh.vertices)
            { hash = (hash ^ (uint)Mathf.RoundToInt(vertex.x * 100000)) * 1099511628211UL; hash = (hash ^ (uint)Mathf.RoundToInt(vertex.y * 100000)) * 1099511628211UL; }
            foreach (var sprite in art.GetComponentsInChildren<SpriteRenderer>())
            {
                var p = art.transform.InverseTransformPoint(sprite.transform.position);
                hash = (hash ^ (uint)Mathf.RoundToInt(p.x * 100000)) * 1099511628211UL; hash = (hash ^ (uint)Mathf.RoundToInt(p.y * 100000)) * 1099511628211UL;
                hash = (hash ^ (uint)(sprite.sprite != null ? sprite.sprite.GetHashCode() : 0)) * 1099511628211UL;
            }
            return hash;
        }
        static Vector3[] Vertices(TowerActorArt art)
        {
            var values = new List<Vector3>();
            foreach (var filter in art.GetComponentsInChildren<MeshFilter>()) foreach (var vertex in filter.sharedMesh.vertices) values.Add(art.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)));
            foreach (var sprite in art.GetComponentsInChildren<SpriteRenderer>()) if (sprite.sprite != null) foreach (var vertex in sprite.sprite.vertices) values.Add(art.transform.InverseTransformPoint(sprite.transform.TransformPoint(vertex)));
            return values.ToArray();
        }
        static Vector3[] Pose(TowerActorArt art)
        {
            var values = new List<Vector3>(Vertices(art));
            foreach (var node in art.GetComponentsInChildren<Transform>()) { values.Add(node.localPosition); values.Add(node.localEulerAngles); values.Add(node.localScale); }
            return values.ToArray();
        }
        static bool Same(Vector3[] a, Vector3[] b, float tolerance)
        { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (Vector3.Distance(a[i], b[i]) > tolerance) return false; return true; }
        static Vector3 Abs(Vector3 value) { return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z)); }
        static void SetLayer(Transform root, int layer) { foreach (var child in root.GetComponentsInChildren<Transform>()) child.gameObject.layer = layer; }
        void Capture(int frame, string stem = "frame-")
        {
            cameraView.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false); pixels.Apply(false, false); RenderTexture.active = previous;
            string path = Path.Combine(outputDirectory, stem + frame.ToString("D3") + ".png"); File.WriteAllBytes(path, pixels.EncodeToPNG());
            if (stem == "frame-") report.screenshots.Add(path); else report.runScreenshots.Add(path);
        }
        void Check(bool condition, string message) { if (!condition) report.failures.Add(message); }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        void Finish(Exception error)
        {
            if (finishing) return; finishing = true; Application.logMessageReceived -= Log;
            if (error != null) report.errors.Add(error.ToString()); report.finishedUtc = DateTime.UtcNow.ToString("o");
            report.success = report.errors.Count == 0 && (report.baseline || report.naturalMotion);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Cleanup(); enabled = false; Application.Quit(report.success ? 0 : 1);
        }
        void Cleanup()
        {
            if (cameraView) { cameraView.targetTexture = null; Destroy(cameraView.gameObject); cameraView = null; }
            if (target) { target.Release(); Destroy(target); target = null; } if (pixels) { Destroy(pixels); pixels = null; }
            if (gallery) { Destroy(gallery.gameObject); gallery = null; } if (floor) { Destroy(floor.gameObject); floor = null; }
        }
        void OnDestroy() { Application.logMessageReceived -= Log; Cleanup(); }
    }
}

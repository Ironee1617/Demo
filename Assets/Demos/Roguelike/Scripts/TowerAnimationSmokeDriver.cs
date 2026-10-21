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
            if (!report.baseline)
            {
                for (int index = TowerMythArt.FirstArtIndex; index < TowerMythArt.FirstArtIndex + TowerMythArt.CharacterCount; index++)
                { if(!TowerEdenArt.Contains(index)) ValidateCharacter(index); yield return null; }
                Check(report.catalogCharacters == 45, "The source-point suite did not validate all 45 mesh companions. Eden pixel clips use -towerEdenReview.");
                for (int index = TowerMythArt.FirstArtIndex; index < TowerMythArt.FirstArtIndex + TowerMythArt.CharacterCount; index++)
                { if(!TowerEdenArt.Contains(index)) ValidateStopping(index); yield return null; }
                Check(report.stopCharacters == 45 && report.stopSamples == 3240, "The stop-contact suite did not validate all 45 mesh companions at three speeds.");
            }
            for (int index = 0; index <= 16; index++)
                if (index != 15) { ValidateTransitions(index); yield return null; }
            for (int index = TowerMythArt.FirstArtIndex; index < TowerMythArt.FirstArtIndex + TowerMythArt.CharacterCount; index++)
            { if(!TowerEdenArt.Contains(index)) ValidateTransitions(index); yield return null; }
            report.naturalMotion = report.failures.Count == 0;
        }
        void ValidateStopping(int index)
        {
            var owner = new GameObject("Stopped painted-foot fixture " + index);
            owner.transform.localScale = Vector3.one * ProductionScale;
            try
            {
                var art = TowerActorArt.Create(owner.transform, index); SetLayer(owner.transform, 31);
                var rig = art.GetComponent<TowerMythRig>();
                Require(rig, "Stopping fixture has no source rig: " + index);
                var entry = new StopReport { artIndex = index, name = art.ProfileName, hovering = rig.Hovering };
                report.stopping.Add(entry);
                var rest = rig.CaptureRestVertices();
                FootProbe left = null, right = null;
                if (!entry.hovering)
                {
                    var parts = rig.CapturePartIndices();
                    var modes = rig.Renderer.GetComponent<MeshFilter>().sharedMesh.uv2;
                    left = BuildFootProbe(rest, parts, modes, 9); right = BuildFootProbe(rest, parts, modes, 12);
                    entry.commonFloor = owner.transform.position.y;
                    entry.groundTolerance = Mathf.Max(.003f, rig.MeshCellPadding * ProductionScale * 2);
                    // A fifth of a painted pixel admits float noise, not visible skating.
                    entry.driftTolerance = Mathf.Max(.00025f, rig.MeshCellPadding * ProductionScale * .2f);
                }
                foreach (float speed in new[] { 1.5f, 3f, 5f })
                {
                    for (int frame = 0; frame < GaitWarmupFrames; frame++)
                    {
                        owner.transform.position += Vector3.right * speed * Dt;
                        art.SetLocomotion(true, 0, false, speed, speed); art.SetPose(true, false, false, 1, Dt);
                    }
                    var sample = new StopSpeedReport { worldSpeed = speed }; entry.speeds.Add(sample);
                    var before = rig.CaptureVertices();
                    StopProbe lp = null, rp = null;
                    if (!entry.hovering)
                    {
                        lp = BeginStopProbe(left, before, rig.Renderer.transform, rig.LeftFootInStance, entry, sample.leftFoot);
                        rp = BeginStopProbe(right, before, rig.Renderer.transform, rig.RightFootInStance, entry, sample.rightFoot);
                    }
                    for (int frame = 0; frame < StopMeasuredFrames; frame++)
                    {
                        // Keep the root stationary for every measured stop frame.
                        art.SetLocomotion(true, 0, false, 0, 0); art.SetPose(false, false, false, 1, Dt);
                        var current = rig.CaptureVertices();
                        sample.finiteGeometry &= current.Length == rest.Length;
                        foreach (var vertex in current) sample.finiteGeometry &= Finite(vertex.x) && Finite(vertex.y);
                        if (entry.hovering)
                        {
                            float lift = rig.Renderer.transform.TransformPoint(rig.SampleSourcePoint(rig.RestHipPosition)).y -
                                owner.transform.position.y - rig.RestHipPosition.y * ProductionScale;
                            sample.minimumHoverClearance = Mathf.Min(sample.minimumHoverClearance, lift);
                        }
                        else
                        {
                            MeasureStoppedFoot(lp, rest, current, rig.Renderer.transform, rig.LeftFootInStance, frame, entry, sample.leftFoot);
                            MeasureStoppedFoot(rp, rest, current, rig.Renderer.transform, rig.RightFootInStance, frame, entry, sample.rightFoot);
                        }
                        sample.sampledFrames++; entry.stopSamples++; report.stopSamples++;
                    }
                    if (!entry.hovering)
                    {
                        FinishStoppedFoot(sample.leftFoot, entry); FinishStoppedFoot(sample.rightFoot, entry);
                        sample.passed = sample.finiteGeometry && StopFootPassed(sample.leftFoot) && StopFootPassed(sample.rightFoot);
                    }
                    else sample.passed = sample.finiteGeometry && sample.minimumHoverClearance > .04f;
                    entry.passed &= sample.passed;
                    Check(sample.passed, entry.name + ": stopping at " + speed + " breaks painted-foot contact: L " +
                        StopFootSummary(sample.leftFoot) + "; R " + StopFootSummary(sample.rightFoot) + ".");
                }
                report.stopCharacters++;
            }
            finally { owner.SetActive(false); Destroy(owner); }
        }
        static StopProbe BeginStopProbe(FootProbe shoe, Vector3[] current, Transform renderer, bool stance, StopReport entry, StopFootReport foot)
        {
            foot.initiallyPlanted = stance;
            foot.initialClearance = renderer.TransformPoint(current[LowestVertex(current, shoe.vertices)]).y - entry.commonFloor;
            var probe = new StopProbe { shoe = shoe };
            if (stance)
            {
                LockStoppedFoot(probe, current, renderer); foot.landingFrame = 0;
            }
            return probe;
        }
        static void LockStoppedFoot(StopProbe probe, Vector3[] current, Transform renderer)
        {
            probe.locked = true; probe.lockedWorldX = new float[probe.shoe.core.Length];
            for (int i = 0; i < probe.shoe.core.Length; i++)
                probe.lockedWorldX[i] = renderer.TransformPoint(current[probe.shoe.core[i]]).x;
        }
        static void MeasureStoppedFoot(StopProbe probe, Vector3[] rest, Vector3[] current, Transform renderer,
            bool stance, int frame, StopReport entry, StopFootReport foot)
        {
            float clearance = renderer.TransformPoint(current[LowestVertex(current, probe.shoe.vertices)]).y - entry.commonFloor;
            foot.sampledFrames++; foot.finalClearance = clearance;
            foot.minimumClearance = Mathf.Min(foot.minimumClearance, clearance);
            foot.maximumShoeStretch = Mathf.Max(foot.maximumShoeStretch, MeshStrain(rest, current, probe.shoe.core));
            float angle = Mathf.Abs(Vector2.SignedAngle(rest[probe.shoe.toe] - rest[probe.shoe.heel],
                current[probe.shoe.toe] - current[probe.shoe.heel]));
            // A descending, rotating shoe may brush the coarse floor tolerance.
            // Start its stationary-contact test only when nearly flat and landed.
            if (!probe.locked && stance && Mathf.Abs(clearance) <= entry.driftTolerance && angle <= .2f)
            { LockStoppedFoot(probe, current, renderer); foot.landingFrame = frame + 1; }
            if (!probe.locked) return;
            foot.lockedFrames++;
            foot.maximumPlantedGap = Mathf.Max(foot.maximumPlantedGap, Mathf.Abs(clearance));
            for (int i = 0; i < probe.shoe.core.Length; i++)
                foot.maximumWorldDrift = Mathf.Max(foot.maximumWorldDrift,
                    Mathf.Abs(renderer.TransformPoint(current[probe.shoe.core[i]]).x - probe.lockedWorldX[i]));
        }
        static void FinishStoppedFoot(StopFootReport foot, StopReport entry)
        {
            foot.settled = foot.landingFrame >= 0 && foot.landingFrame <= 15 &&
                Mathf.Abs(foot.finalClearance) <= entry.groundTolerance + .00001f &&
                foot.minimumClearance >= -entry.groundTolerance - .00001f &&
                foot.maximumPlantedGap <= entry.groundTolerance + .00001f;
            foot.stationary = foot.lockedFrames >= (foot.initiallyPlanted ? StopMeasuredFrames : 9) &&
                foot.maximumWorldDrift <= entry.driftTolerance + .00001f;
            foot.rigidShoe = foot.sampledFrames == StopMeasuredFrames && foot.maximumShoeStretch <= .02f;
        }
        static bool StopFootPassed(StopFootReport foot) { return foot.settled && foot.stationary && foot.rigidShoe; }
        static string StopFootSummary(StopFootReport foot)
        { return "land " + foot.landingFrame + ", drift " + foot.maximumWorldDrift.ToString("F5") + ", gap " + foot.maximumPlantedGap.ToString("F5"); }
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
                var myth = art.GetComponent<TowerMythRig>();
                if (myth != null) entry.largestPart = myth.CapturePartIndices()[entry.largestVertex];
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
                entry.meshParts = art.GetComponentsInChildren<MeshFilter>().Length; fixtures.Add(fixture); report.characters.Add(entry);
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
        void ValidateCharacter(int index)
        {
            var owner = new GameObject("Production-scale moving source fixture " + index); owner.transform.localScale = Vector3.one * ProductionScale;
            try
            {
                var art = TowerActorArt.Create(owner.transform, index); var reference = TowerActorArt.Create(owner.transform, index); SetLayer(owner.transform, 31);
                var rig = art.GetComponent<TowerMythRig>(); var referenceRig = reference.GetComponent<TowerMythRig>();
                Require(rig && referenceRig, "The mythology actor has no articulated source rig: " + index);
                var entry = new CatalogReport { artIndex = index, name = art.ProfileName, combatRole = TowerRun.Definitions[index - 100].Role, hovering = index == 126 };
                entry.role = entry.combatRole.ToString(); report.catalog.Add(entry);
                if (entry.hovering) report.hoveringCharacters++; else report.groundedCharacters++;
                var filters = art.GetComponentsInChildren<MeshFilter>(); var renderers = art.GetComponentsInChildren<Renderer>();
                var initialUV = new Vector2[filters.Length][]; var textures = new Texture[renderers.Length];
                for (int mesh = 0; mesh < filters.Length; mesh++) initialUV[mesh] = filters[mesh].sharedMesh.uv;
                for (int renderer = 0; renderer < renderers.Length; renderer++) textures[renderer] = renderers[renderer].sharedMaterial.mainTexture;
                entry.sourceTextureId = rig.Renderer.sharedMaterial.mainTexture.GetInstanceID();
                entry.previousGeometry = rig.CaptureVertices(); entry.vertexCount = entry.previousGeometry.Length;
                entry.restGeometry = rig.CaptureRestVertices(); BuildSourceProbes(rig, entry);
                if (!entry.hovering)
                {
                    entry.leftSoleRest = (entry.restGeometry[entry.leftFootProbe.heel] + entry.restGeometry[entry.leftFootProbe.toe]) * .5f;
                    entry.rightSoleRest = (entry.restGeometry[entry.rightFootProbe.heel] + entry.restGeometry[entry.rightFootProbe.toe]) * .5f;
                    // Use the physical actor origin, not the painted atlas bounds.
                    // A source sole below zero must not redefine the test's floor.
                    entry.commonFloor = owner.transform.position.y;
                    entry.groundTolerance = Mathf.Max(.003f, rig.MeshCellPadding * ProductionScale * 2);
                }
                var geometry = new HashSet<ulong>();
                foreach (float speed in new[] { 1.5f, 3f, 5f })
                {
                    var gait = new GaitReport { worldSpeed = speed }; entry.gaits.Add(gait); entry.contactsReady = false;
                    art.SetLocomotion(true, 0, false, speed, speed); reference.SetLocomotion(true, 0, false, speed, speed);
                    for (int frame = 0; frame < GaitWarmupFrames + GaitMeasuredFrames; frame++)
                    {
                        StepPair(owner.transform, art, reference, rig, referenceRig, entry, speed, frame >= GaitWarmupFrames ? gait : null, false, null);
                        if (frame >= GaitWarmupFrames) geometry.Add(GeometryHash(art));
                        if (frame == GaitWarmupFrames - 1)
                        {
                            entry.contactsReady = false;
                            if (!entry.hovering) entry.leftFootProbe.stanceDrift = entry.rightFootProbe.stanceDrift = 0;
                        }
                    }
                    gait.meanContactSlipRatio = gait.contactSamples == 0 ? 0 : gait.slipSum / gait.contactSamples;
                    if (!entry.hovering)
                    { gait.leftStride = gait.maximumLeft.x - gait.minimumLeft.x; gait.rightStride = gait.maximumRight.x - gait.minimumRight.x; }
                    else gait.minimumClearance = 0;
                    FinishFootReport(gait.leftFoot, entry.groundTolerance);
                    FinishFootReport(gait.rightFoot, entry.groundTolerance);
                    gait.rootMatched = gait.leftFoot.rootMatched && gait.rightFoot.rootMatched;
                    gait.groundedContacts = !entry.hovering && gait.leftFoot.contactMaintained && gait.rightFoot.contactMaintained;
                    gait.rigidShoes = gait.leftFoot.rigidShoe && gait.rightFoot.rigidShoe;
                    gait.alternatingStride = gait.leftFoot.maximumFootLift > entry.groundTolerance &&
                        gait.rightFoot.maximumFootLift > entry.groundTolerance && gait.leftStride > .10f && gait.rightStride > .10f;
                    // At ordinary walk speed a support foot must remain on the floor.
                    // Running is allowed to have its deliberate flight interval.
                    gait.walkingSupport = speed > 1.5f || gait.airborneSamples <= 2;
                    if (entry.hovering)
                        Check(gait.hoverBodySamples == GaitMeasuredFrames && gait.minimumHoverHeight > .04f && gait.maximumHoverHeight - gait.minimumHoverHeight > .015f && geometry.Count > 15, entry.name + ": hovering body must stay aloft and move through its visible hover cycle.");
                    else
                    {
                        Check(gait.rootMatched, entry.name + ": actual rendered soles skate at " + speed + " world units/s (max error " + (100 * gait.maximumContactSlipRatio).ToString("F1") + "%, " + gait.contactSamples + " contact pairs).");
                        Check(gait.groundedContacts && gait.alternatingStride && gait.walkingSupport, entry.name + ": rendered heel/toe contact failed at " + speed + ": L " + FootSummary(gait.leftFoot) + "; R " + FootSummary(gait.rightFoot) + ", unsupported " + gait.airborneSamples + ".");
                        Check(gait.rigidShoes, entry.name + ": shoe shape deforms at " + speed + " (L " + (100 * gait.leftFoot.maximumShoeStretch).ToString("F2") + "%, R " + (100 * gait.rightFoot.maximumShoeStretch).ToString("F2") + "%).");
                    }
                }
                entry.distinctRunGeometry = geometry.Count; entry.articulatedRun = geometry.Count >= 120;
                entry.rootMatchedGait = true; entry.groundedFeet = true;
                foreach (var gait in entry.gaits) { entry.rootMatchedGait &= gait.rootMatched; entry.groundedFeet &= gait.groundedContacts; }
                var before = Pose(art); art.SetPose(true, false, false, 1, 0); entry.pausedPose &= Same(before, Pose(art), .000001f);
                art.PlayAttack(.6f); before = Pose(art); art.SetPose(true, false, false, 1, 0); entry.pausedPose &= Same(before, Pose(art), .000001f);
                for (int frame = 0; frame < 72; frame++) StepPair(owner.transform, art, reference, rig, referenceRig, entry, 5, null, true, frame < 36 ? "attack" : null);
                entry.attackReturnError = VertexError(rig.CaptureVertices(), referenceRig.CaptureVertices()); entry.attackRecovery = entry.attackReturnError <= .015f;
                art.PlayCast(SkillEffect.Fireball, .6f);
                for (int frame = 0; frame < 72; frame++) StepPair(owner.transform, art, reference, rig, referenceRig, entry, 5, null, true, frame < 36 ? "cast" : null);
                entry.castReturnError = VertexError(rig.CaptureVertices(), referenceRig.CaptureVertices()); entry.castRecovery = entry.castReturnError <= .015f;
                art.PlayAttack(.6f);
                for (int frame = 0; frame < 9; frame++) StepPair(owner.transform, art, reference, rig, referenceRig, entry, 5, null, true, null);
                art.InterruptAction();
                for (int frame = 0; frame < 36; frame++) StepPair(owner.transform, art, reference, rig, referenceRig, entry, 5, null, true, null);
                entry.interruptedReturnError = VertexError(rig.CaptureVertices(), referenceRig.CaptureVertices()); entry.interruptedRecovery = entry.interruptedReturnError <= .015f;
                FinishFootReport(entry.leftActionFoot, entry.groundTolerance); FinishFootReport(entry.rightActionFoot, entry.groundTolerance);
                entry.continuousActionSteps = entry.leftActionFoot.rootMatched && entry.rightActionFoot.rootMatched &&
                    entry.leftActionFoot.contactMaintained && entry.rightActionFoot.contactMaintained;
                entry.rigidShoes = entry.maximumShoeStretch <= .02f;
                entry.attackWeaponArc = entry.minimumWeaponAngle == float.MaxValue ? 0 : entry.maximumWeaponAngle - entry.minimumWeaponAngle;
                float attackReach = entry.combatRole == CompanionRole.Archer ? .04f : .08f;
                entry.dynamicAttack = entry.attackHandExcursion >= attackReach && (entry.combatRole != CompanionRole.Vanguard || entry.attackTorsoAngle >= 8 && entry.attackWeaponArc >= 40);
                entry.dynamicCast = entry.castHandExcursion >= .08f;
                for (int mesh = 0; mesh < filters.Length; mesh++)
                {
                    var uv = filters[mesh].sharedMesh.uv; entry.stableArtwork &= uv.Length == initialUV[mesh].Length;
                    for (int vertex = 0; vertex < uv.Length && entry.stableArtwork; vertex++) entry.stableArtwork &= uv[vertex] == initialUV[mesh][vertex];
                }
                for (int renderer = 0; renderer < renderers.Length; renderer++)
                { entry.stableScale &= Vector3.Distance(Abs(renderers[renderer].transform.localScale), Vector3.one) < .00001f; entry.stableArtwork &= renderers[renderer].sharedMaterial.mainTexture == textures[renderer]; }
                entry.rigidFace = entry.faceProbeCount == 4 && entry.maximumFaceStretch <= .02f;
                entry.rigidGarment = entry.garmentCoreMeasured && entry.maximumGarmentStretch <= .10f;
                entry.rigidWeapon = entry.weaponCoreMeasured && entry.maximumWeaponStretch <= .10f;
                Check(entry.articulatedRun && entry.finiteTopology, entry.name + ": rendered geometry is static, changes topology, or is non-finite.");
                if (!entry.hovering) Check(entry.continuousActionSteps, entry.name + ": action entry/recovery breaks world foot contact (max slip " + (entry.maximumActionStanceSlipRatio * 100).ToString("F1") + "%, " + entry.maximumActionSlipMotion + ", sample " + entry.maximumActionSlipFrame + "; L " + FootSummary(entry.leftActionFoot) + "; R " + FootSummary(entry.rightActionFoot) + ").");
                Check(entry.dynamicAttack, entry.name + ": attack silhouette is too weak (hand " + entry.attackHandExcursion.ToString("F3") + "H, torso " + entry.attackTorsoAngle.ToString("F1") + "deg, weapon arc " + entry.attackWeaponArc.ToString("F1") + "deg).");
                Check(entry.dynamicCast, entry.name + ": cast does not visibly articulate the hands (" + entry.castHandExcursion.ToString("F3") + "H).");
                Check(entry.maximumSourceMappingError <= .0025f, entry.name + ": source feature sampling disagrees with actual painted face triangles.");
                Check(entry.rigidFace, entry.name + ": rendered face stretches (" + (100 * entry.maximumFaceStretch).ToString("F1") + "%).");
                if (!entry.hovering) Check(entry.rigidShoes, entry.name + ": painted shoe shape stretches during gait/action (" + (100 * entry.maximumShoeStretch).ToString("F2") + "%).");
                if (entry.garmentCoreMeasured) Check(entry.rigidGarment, entry.name + ": protected garment core stretches (" + (100 * entry.maximumGarmentStretch).ToString("F1") + "%).");
                if (entry.weaponCoreMeasured) Check(entry.rigidWeapon, entry.name + ": rendered weapon bends (" + (100 * entry.maximumWeaponStretch).ToString("F1") + "%).");
                Check(entry.pausedPose, entry.name + ": animation advances with zero elapsed time.");
                Check(entry.attackRecovery && entry.castRecovery && entry.interruptedRecovery, entry.name + ": action fails to return to same-clock locomotion (" + entry.attackReturnError.ToString("F4") + "/" + entry.castReturnError.ToString("F4") + "/" + entry.interruptedReturnError.ToString("F4") + ").");
                Check(entry.maximumVertexStep <= Mathf.Max(BodyHeight, art.RenderedHeight) * .25f, entry.name + ": geometry jumps over a quarter body height in one sixty-Hz frame.");
                Check(entry.stableScale && entry.stableArtwork, entry.name + ": animation rescales the body or replaces source UVs/texture.");
                report.catalogCharacters++; report.catalogMotionSamples += entry.motionSamples;
            }
            finally { owner.SetActive(false); Destroy(owner); }
        }
        void BuildSourceProbes(TowerMythRig rig, CatalogReport entry)
        {
            // Vertex probes are selected from actual painted part geometry, not a virtual
            // full-cell grid or a classifier fallback in transparent source gaps.
            int[] parts = rig.CapturePartIndices(); entry.geometryParts = parts;
            var mesh = rig.Renderer.GetComponent<MeshFilter>().sharedMesh;
            int[] triangles = mesh.triangles; Vector2[] modes = mesh.uv2;
            // Nezha flies on intact wheels. It has no planted biped soles; its
            // body clearance/cycle is measured separately without invented feet.
            entry.leftSoleVertex = entry.rightSoleVertex = -1;
            if (!entry.hovering)
            {
                entry.leftFootProbe = BuildFootProbe(entry.restGeometry, parts, modes, 9);
                entry.rightFootProbe = BuildFootProbe(entry.restGeometry, parts, modes, 12);
                entry.leftFootProbeCount = entry.leftFootProbe.vertices.Length;
                entry.rightFootProbeCount = entry.rightFootProbe.vertices.Length;
                entry.leftSoleVertex = LowestVertex(entry.restGeometry, entry.leftFootProbe.vertices);
                entry.rightSoleVertex = LowestVertex(entry.restGeometry, entry.rightFootProbe.vertices);
            }
            entry.bodyHipProbe = NearestBodyProbe(entry.restGeometry, parts, triangles, 0, rig.RestHipPosition);
            entry.bodyShoulderProbe = NearestBodyProbe(entry.restGeometry, parts, triangles, 1, rig.RestShoulderPosition);
            entry.bodyHipProbeRestPoint = SampleProbe(entry.restGeometry, entry.bodyHipProbe);
            entry.bodyShoulderProbeRestPoint = SampleProbe(entry.restGeometry, entry.bodyShoulderProbe);
            entry.faceProbes = SourcePatch(entry.restGeometry, parts, triangles, modes, 2, rig.RestHeadPosition, .025f, .035f, null, out entry.faceProbeRestPoints);
            entry.faceProbeCount = entry.faceProbes.Length;
            entry.garmentCoreCovered = rig.RestHipPosition.y - rig.GarmentHemY > .18f;
            entry.garmentProbes = new MeshProbe[0]; entry.garmentProbeRestPoints = new Vector2[0];
            if (entry.garmentCoreCovered)
            {
                Vector2 desired = new Vector2(rig.RestHipPosition.x - .07f, Mathf.Lerp(rig.GarmentHemY, rig.RestHipPosition.y, .55f));
                entry.garmentProbes = SourcePatch(entry.restGeometry, parts, triangles, modes, 0, desired, .018f, .018f, p => GarmentProbe(rig, p), out entry.garmentProbeRestPoints);
                if (entry.garmentProbeRestPoints.Length == 4)
                    foreach (var point in entry.garmentProbeRestPoints) entry.garmentProbeCenter += point * .25f;
            }
            entry.garmentCoreMeasured = entry.garmentProbes.Length == 4; entry.garmentProbeOccluded = entry.garmentCoreCovered && !entry.garmentCoreMeasured;
            if (entry.garmentCoreMeasured) report.garmentCoresMeasured++; if (entry.garmentProbeOccluded) report.garmentProbesOccluded++;
            var positions = new List<int>(); var candidates = new List<Vector2>();
            for (int sample = 0; sample < 5; sample++) candidates.Add(Vector2.Lerp(rig.RestWeaponBasePosition, rig.RestWeaponTipPosition, sample * .25f));
            Vector2 radii = rig.WeaponTipRadii;
            if (radii.x > 0 && radii.y > 0)
            { candidates.Add(rig.RestWeaponTipPosition + Vector2.right * radii.x * .5f); candidates.Add(rig.RestWeaponTipPosition - Vector2.right * radii.x * .5f); candidates.Add(rig.RestWeaponTipPosition + Vector2.up * radii.y * .5f); candidates.Add(rig.RestWeaponTipPosition - Vector2.up * radii.y * .5f); }
            foreach (var point in candidates)
            {
                int nearest = NearestVertex(entry.restGeometry, parts, 14, point, null);
                if (nearest >= 0 && Vector2.Distance(entry.restGeometry[nearest], point) < .12f && !positions.Contains(nearest)) positions.Add(nearest);
            }
            entry.weaponIndices = positions.ToArray(); entry.weaponProbeRestPoints = RestPoints(entry.restGeometry, entry.weaponIndices);
            float longestAxis = 0;
            foreach (int a in entry.weaponIndices) foreach (int b in entry.weaponIndices)
            {
                float length = Vector3.Distance(entry.restGeometry[a], entry.restGeometry[b]);
                if (length > longestAxis) { longestAxis = length; entry.weaponAxisFirst = a; entry.weaponAxisSecond = b; }
            }
            entry.weaponProbeCount = positions.Count; entry.weaponCoreMeasured = positions.Count >= 3; entry.weaponProbeOccluded = !entry.weaponCoreMeasured;
            if (entry.weaponCoreMeasured) report.weaponCoresMeasured++; else report.weaponProbesOccluded++;
            Check(entry.faceProbeCount == 4, entry.name + ": visible face source probes are unavailable.");
        }
        static FootProbe BuildFootProbe(Vector3[] rest, int[] parts, Vector2[] modes, int part)
        {
            var vertices = new List<int>();
            int minX = -1, maxX = -1, minY = -1, maxY = -1;
            for (int i = 0; i < rest.Length; i++)
            {
                if (parts[i] != part || modes[i].x > .5f) continue;
                vertices.Add(i);
                if (minX < 0 || rest[i].x < rest[minX].x) minX = i;
                if (maxX < 0 || rest[i].x > rest[maxX].x) maxX = i;
                if (minY < 0 || rest[i].y < rest[minY].y) minY = i;
                if (maxY < 0 || rest[i].y > rest[maxY].y) maxY = i;
            }
            Require(vertices.Count >= 4 && rest[maxX].x - rest[minX].x > .02f, "No visible painted shoe surface for part " + part);
            // Heel and toe are the lowest painted boundary within the outer fifth
            // of the shoe. All shoe vertices still contribute to floor clearance,
            // so a rotating toe cannot penetrate unseen behind one fixed probe.
            float span = rest[maxX].x - rest[minX].x;
            int heel = -1, toe = -1;
            foreach (int i in vertices)
            {
                if (rest[i].x <= rest[minX].x + span * .22f && (heel < 0 || rest[i].y < rest[heel].y)) heel = i;
                if (rest[i].x >= rest[maxX].x - span * .22f && (toe < 0 || rest[i].y < rest[toe].y)) toe = i;
            }
            var core = new List<int>();
            foreach (int i in new[] { minX, maxX, minY, maxY, heel, toe }) if (!core.Contains(i)) core.Add(i);
            return new FootProbe { vertices = vertices.ToArray(), core = core.ToArray(), heel = heel, toe = toe, restMinimumY = rest[minY].y };
        }
        static int LowestVertex(Vector3[] points, int[] indices)
        { int best = indices[0]; foreach (int i in indices) if (points[i].y < points[best].y) best = i; return best; }
        static void FinishFootReport(FootContactReport foot, float tolerance)
        {
            foot.contactCoverage = foot.stanceFrames == 0 ? 0 : foot.groundedStanceFrames / (float)foot.stanceFrames;
            foot.contactMaintained = foot.stanceFrames >= 12 && foot.contactCoverage >= .95f &&
                foot.minimumClearance >= -tolerance - .00001f && foot.maximumStanceGap <= tolerance * 2 + .00001f;
            foot.rootMatched = foot.stancePairs >= 8 && foot.maximumContactSlipRatio <= .20f &&
                foot.maximumStanceDrift <= BodyHeight * ProductionScale * .02f;
            foot.rigidShoe = foot.sampledFrames > 0 && foot.maximumShoeStretch <= .02f;
        }
        static string FootSummary(FootContactReport foot)
        { return foot.groundedStanceFrames + "/" + foot.stanceFrames + " stance frames, gap " + foot.maximumStanceGap.ToString("F4") + " at " + foot.maximumGapFrame + ", min " + foot.minimumClearance.ToString("F4"); }
        static int NearestVertex(Vector3[] rest, int[] parts, int part, Vector2 point, Predicate<Vector2> eligible)
        {
            int nearest = -1; float distance = float.MaxValue;
            for (int i = 0; i < rest.Length; i++)
            {
                if (parts[i] != part || eligible != null && !eligible(rest[i])) continue;
                float candidate = ((Vector2)rest[i] - point).sqrMagnitude;
                if (candidate < distance) { distance = candidate; nearest = i; }
            }
            return nearest;
        }
        static MeshProbe[] SourcePatch(Vector3[] rest, int[] parts, int[] triangles, Vector2[] modes, int part, Vector2 desired, float x, float y, Predicate<Vector2> eligible, out Vector2[] positions)
        {
            // A strip may span a complete painted row with no interior vertices.
            // Select points inside its actual opaque triangles instead of mistaking
            // missing contour vertices for missing face/cloth geometry.
            var centers = new List<Vector2> { desired };
            float searchX = part == 2 ? .04f : .22f, searchY = part == 2 ? .04f : .25f;
            for (float dx = -searchX; dx <= searchX + .001f; dx += .02f)
                for (float dy = -searchY; dy <= searchY + .001f; dy += .02f)
                    centers.Add(desired + new Vector2(dx, dy));
            centers.Sort((a, b) => (a - desired).sqrMagnitude.CompareTo((b - desired).sqrMagnitude));
            foreach (var center in centers)
            {
                var probes = new List<MeshProbe>(); var points = new List<Vector2>();
                foreach (var offset in new[] { new Vector2(-x, -y), new Vector2(x, -y), new Vector2(x, y), new Vector2(-x, y) })
                {
                    Vector2 point = center + offset; MeshProbe probe;
                    if (eligible != null && !eligible(point) || !FindSourceTriangle(rest, parts, triangles, modes, part, point, out probe)) break;
                    probes.Add(probe); points.Add(point);
                }
                if (probes.Count == 4) { positions = points.ToArray(); return probes.ToArray(); }
            }
            positions = new Vector2[0]; return new MeshProbe[0];
        }
        static bool FindSourceTriangle(Vector3[] rest, int[] parts, int[] triangles, Vector2[] modes, int part, Vector2 point, out MeshProbe result)
        {
            for (int triangle = 0; triangle + 2 < triangles.Length; triangle += 3)
            {
                int a = triangles[triangle], b = triangles[triangle + 1], c = triangles[triangle + 2];
                if (parts[a] != part || parts[b] != part || parts[c] != part || modes[a].x > .5f) continue;
                Vector2 first = rest[b] - rest[a], second = rest[c] - rest[a], offset = point - (Vector2)rest[a];
                float determinant = first.x * second.y - first.y * second.x;
                if (Mathf.Abs(determinant) < .0000001f) continue;
                float u = (offset.x * second.y - offset.y * second.x) / determinant, v = (first.x * offset.y - first.y * offset.x) / determinant;
                if (u < -.0001f || v < -.0001f || u + v > 1.0001f) continue;
                result = new MeshProbe { a = a, b = b, c = c, weights = new Vector3(1 - u - v, u, v) }; return true;
            }
            result = new MeshProbe(); return false;
        }
        static MeshProbe NearestBodyProbe(Vector3[] rest, int[] parts, int[] triangles, int part, Vector2 desired)
        {
            var result = new MeshProbe { a = -1 }; float closest = float.MaxValue;
            for (int triangle = 0; triangle + 2 < triangles.Length; triangle += 3)
            {
                int a = triangles[triangle], b = triangles[triangle + 1], c = triangles[triangle + 2];
                if (parts[a] != part || parts[b] != part || parts[c] != part) continue;
                Vector2 ab = rest[b] - rest[a], ac = rest[c] - rest[a], offset = desired - (Vector2)rest[a];
                float determinant = ab.x * ac.y - ab.y * ac.x;
                if (Mathf.Abs(determinant) < .0000001f) continue;
                float u = (offset.x * ac.y - offset.y * ac.x) / determinant, v = (ab.x * offset.y - ab.y * offset.x) / determinant;
                if (u >= 0 && v >= 0 && u + v <= 1)
                    return new MeshProbe { a = a, b = b, c = c, weights = new Vector3(1 - u - v, u, v) };
                for (int edge = 0; edge < 3; edge++)
                {
                    int first = edge == 0 ? a : edge == 1 ? a : b, second = edge == 0 ? b : c;
                    Vector2 start = rest[first], line = rest[second] - rest[first];
                    float t = Mathf.Clamp01(Vector2.Dot(desired - start, line) / Mathf.Max(.0000001f, line.sqrMagnitude));
                    float distance = (start + line * t - desired).sqrMagnitude;
                    if (distance >= closest) continue;
                    closest = distance;
                    result = new MeshProbe { a = a, b = b, c = c, weights = edge == 0 ? new Vector3(1 - t, t, 0) : edge == 1 ? new Vector3(1 - t, 0, t) : new Vector3(0, 1 - t, t) };
                }
            }
            Require(result.a >= 0, "No rendered body surface for part " + part); return result;
        }
        static Vector2 SampleProbe(Vector3[] geometry, MeshProbe probe)
        { return geometry[probe.a] * probe.weights.x + geometry[probe.b] * probe.weights.y + geometry[probe.c] * probe.weights.z; }
        static float ProbeStrain(Vector3[] geometry, MeshProbe[] probes, Vector2[] rest)
        {
            float maximum = 0;
            for (int a = 0; a < probes.Length; a++) for (int b = a + 1; b < probes.Length; b++)
            {
                float length = Vector2.Distance(rest[a], rest[b]); if (length < .015f) continue;
                maximum = Mathf.Max(maximum, Mathf.Abs(Vector2.Distance(SampleProbe(geometry, probes[a]), SampleProbe(geometry, probes[b])) / length - 1));
            }
            return maximum;
        }
        static Vector2[] RestPoints(Vector3[] rest, int[] indices)
        { var points = new Vector2[indices.Length]; for (int i = 0; i < indices.Length; i++) points[i] = rest[indices[i]]; return points; }
        static bool GarmentProbe(TowerMythRig rig, Vector2 point)
        {
            if (point.y < rig.GarmentHemY + .04f || point.y > rig.RestHipPosition.y - .025f || Mathf.Abs(point.x - rig.RestHipPosition.x) > .25f) return false;
            if (Vector2.Distance(point, rig.RestLeftHandPosition) < .12f || Vector2.Distance(point, rig.RestRightHandPosition) < .12f) return false;
            Vector2 start = rig.RestWeaponBasePosition, end = rig.RestWeaponTipPosition, line = end - start;
            float progress = Mathf.Clamp01(Vector2.Dot(point - start, line) / Mathf.Max(.00001f, line.sqrMagnitude));
            if (rig.WeaponWidth > 0 && Vector2.Distance(point, start + line * progress) < rig.WeaponWidth * .5f + .04f) return false;
            Vector2 radii = rig.WeaponTipRadii + Vector2.one * .04f, tip = point - end;
            return !(rig.WeaponTipRadii.x > 0 && rig.WeaponTipRadii.y > 0 && tip.x * tip.x / (radii.x * radii.x) + tip.y * tip.y / (radii.y * radii.y) < 1);
        }
        static void StepPair(Transform owner, TowerActorArt art, TowerActorArt reference, TowerMythRig rig, TowerMythRig referenceRig, CatalogReport entry, float speed, GaitReport gait, bool actionContacts, string action)
        {
            owner.position += Vector3.right * speed * Dt;
            art.SetPose(true, false, false, 1, Dt); reference.SetPose(true, false, false, 1, Dt);
            entry.stableScale &= Vector3.Distance(Abs(rig.Renderer.transform.localScale), Vector3.one) < .00001f;
            entry.stableArtwork &= rig.Renderer.sharedMaterial.mainTexture.GetInstanceID() == entry.sourceTextureId;
            var current = rig.CaptureVertices(); entry.finiteTopology &= current.Length == entry.vertexCount;
            if (gait != null) gait.measuredFrames++;
            if (entry.hovering)
            {
                if (gait != null)
                {
                    Vector2 body = rig.Renderer.transform.TransformPoint(rig.SampleSourcePoint(rig.RestHipPosition));
                    float height = body.y - owner.position.y - rig.RestHipPosition.y * ProductionScale;
                    gait.minimumHoverHeight = Mathf.Min(gait.minimumHoverHeight, height);
                    gait.maximumHoverHeight = Mathf.Max(gait.maximumHoverHeight, height); gait.hoverBodySamples++;
                }
            }
            else
            {
                Vector2 left = (current[entry.leftFootProbe.heel] + current[entry.leftFootProbe.toe]) * .5f;
                Vector2 right = (current[entry.rightFootProbe.heel] + current[entry.rightFootProbe.toe]) * .5f;
                float leftClearance = MeasureFoot(entry, entry.leftFootProbe, current, rig.Renderer.transform, rig.LeftFootInStance,
                    speed, gait, gait != null ? gait.leftFoot : actionContacts ? entry.leftActionFoot : null, actionContacts, art.CurrentMotion);
                float rightClearance = MeasureFoot(entry, entry.rightFootProbe, current, rig.Renderer.transform, rig.RightFootInStance,
                    speed, gait, gait != null ? gait.rightFoot : actionContacts ? entry.rightActionFoot : null, actionContacts, art.CurrentMotion);
                if (gait != null)
                {
                    Vector2 leftOffset = left - entry.leftSoleRest, rightOffset = right - entry.rightSoleRest;
                    gait.minimumLeft = Vector2.Min(gait.minimumLeft, leftOffset); gait.maximumLeft = Vector2.Max(gait.maximumLeft, leftOffset);
                    gait.minimumRight = Vector2.Min(gait.minimumRight, rightOffset); gait.maximumRight = Vector2.Max(gait.maximumRight, rightOffset);
                    gait.minimumClearance = Mathf.Min(gait.minimumClearance, leftClearance, rightClearance);
                    gait.maximumFootLift = Mathf.Max(gait.maximumFootLift, leftClearance, rightClearance);
                    if (leftClearance > entry.groundTolerance + .00001f && rightClearance > entry.groundTolerance + .00001f) gait.airborneSamples++;
                }
                if (actionContacts) entry.minimumActionClearance = Mathf.Min(entry.minimumActionClearance, leftClearance, rightClearance);
                entry.contactsReady = true;
            }
            for (int vertex = 0; vertex < current.Length; vertex++)
            {
                Vector3 p = current[vertex]; entry.finiteTopology &= Finite(p.x) && Finite(p.y);
                if (vertex < entry.previousGeometry.Length)
                {
                    float step = Vector3.Distance(entry.previousGeometry[vertex], p);
                    if (step > entry.maximumVertexStep) { entry.maximumVertexStep = step; entry.maximumVertexStepFrame = entry.motionSamples; entry.maximumVertexStepVertex = vertex; entry.maximumVertexStepPart = entry.geometryParts[vertex]; entry.maximumVertexStepRestPoint = entry.restGeometry[vertex]; entry.maximumVertexStepMotion = art.CurrentMotion; }
                }
            }
            entry.maximumFaceStretch = Mathf.Max(entry.maximumFaceStretch, ProbeStrain(current, entry.faceProbes, entry.faceProbeRestPoints));
            float strain = ProbeStrain(current, entry.garmentProbes, entry.garmentProbeRestPoints);
            if (strain > entry.maximumGarmentStretch) { entry.maximumGarmentStretch = strain; entry.maximumGarmentStretchFrame = entry.motionSamples; entry.maximumGarmentStretchMotion = art.CurrentMotion; }
            strain = MeshStrain(entry.restGeometry, current, entry.weaponIndices);
            if (strain > entry.maximumWeaponStretch) { entry.maximumWeaponStretch = strain; entry.maximumWeaponStretchFrame = entry.motionSamples; entry.maximumWeaponStretchMotion = art.CurrentMotion; }
            for (int sample = 0; sample < entry.faceProbes.Length; sample++)
                entry.maximumSourceMappingError = Mathf.Max(entry.maximumSourceMappingError, Vector2.Distance(SampleProbe(current, entry.faceProbes[sample]), rig.SampleSourcePoint(entry.faceProbeRestPoints[sample])));
            if (action != null) MeasureAction(rig, referenceRig, entry, action, current);
            entry.previousRenderToWorld = rig.Renderer.transform.localToWorldMatrix;
            entry.previousGeometry = current; entry.motionSamples++;
        }
        static float MeasureFoot(CatalogReport entry, FootProbe probe, Vector3[] current, Transform renderer,
            bool stance, float speed, GaitReport gait, FootContactReport foot, bool action, string motion)
        {
            int support = LowestVertex(current, probe.vertices);
            Vector2 world = renderer.TransformPoint(current[support]);
            float clearance = world.y - entry.commonFloor;
            float strain = MeshStrain(entry.restGeometry, current, probe.core);
            entry.maximumShoeStretch = Mathf.Max(entry.maximumShoeStretch, strain);
            if (foot != null)
            {
                foot.sampledFrames++;
                foot.minimumClearance = Mathf.Min(foot.minimumClearance, clearance);
                foot.maximumFootLift = Mathf.Max(foot.maximumFootLift, clearance);
                if (strain > foot.maximumShoeStretch) { foot.maximumShoeStretch = strain; foot.maximumShoeStretchFrame = entry.motionSamples; }
                foot.maximumShoeAngle = Mathf.Max(foot.maximumShoeAngle, Mathf.Abs(Vector2.SignedAngle(
                    entry.restGeometry[probe.toe] - entry.restGeometry[probe.heel], current[probe.toe] - current[probe.heel])));
                if (stance)
                {
                    foot.stanceFrames++;
                    if (Mathf.Abs(clearance) <= entry.groundTolerance + .00001f) foot.groundedStanceFrames++;
                    if (Mathf.Abs(clearance) > foot.maximumStanceGap)
                    { foot.maximumStanceGap = Mathf.Abs(clearance); foot.maximumGapFrame = entry.motionSamples; }
                    if (!entry.contactsReady || !probe.previousStance) { probe.stanceDrift = 0; foot.stanceEntries++; }
                    else
                    {
                        // Track the same painted point across this pair, even when
                        // the lowest point changes from heel to toe during a roll.
                        // Do not discard off-floor or forward-skating stance pairs.
                        Vector2 before = entry.previousRenderToWorld.MultiplyPoint3x4(entry.previousGeometry[support]);
                        float signedDrift = world.x - before.x, drift = Mathf.Abs(signedDrift);
                        float slip = drift / Mathf.Max(.0001f, Mathf.Abs(speed) * Dt);
                        probe.stanceDrift += signedDrift; foot.stancePairs++;
                        foot.maximumFrameDrift = Mathf.Max(foot.maximumFrameDrift, drift);
                        foot.maximumStanceDrift = Mathf.Max(foot.maximumStanceDrift, Mathf.Abs(probe.stanceDrift));
                        if (slip > foot.maximumContactSlipRatio)
                        { foot.maximumContactSlipRatio = slip; foot.maximumSlipFrame = entry.motionSamples; }
                        if (gait != null)
                        {
                            gait.contactSamples++; gait.slipSum += slip;
                            gait.maximumContactSlipRatio = Mathf.Max(gait.maximumContactSlipRatio, slip);
                            gait.maximumContactDrift = Mathf.Max(gait.maximumContactDrift, drift);
                        }
                        if (action)
                        {
                            entry.actionContactSamples++; entry.maximumActionContactDrift = Mathf.Max(entry.maximumActionContactDrift, drift);
                            if (slip > entry.maximumActionStanceSlipRatio)
                            { entry.maximumActionStanceSlipRatio = slip; entry.maximumActionSlipFrame = entry.motionSamples; entry.maximumActionSlipMotion = motion; }
                        }
                    }
                }
            }
            if (!stance) probe.stanceDrift = 0;
            probe.previousStance = stance;
            return clearance;
        }
        static void MeasureAction(TowerMythRig rig, TowerMythRig reference, CatalogReport entry, string action, Vector3[] current)
        {
            var referenceGeometry = reference.CaptureVertices();
            Vector2 hip = SampleProbe(current, entry.bodyHipProbe), referenceHip = SampleProbe(referenceGeometry, entry.bodyHipProbe);
            float left = Vector2.Distance(rig.SampleSourcePoint(rig.RestLeftHandPosition) - hip, reference.SampleSourcePoint(reference.RestLeftHandPosition) - referenceHip) / BodyHeight;
            float right = Vector2.Distance(rig.SampleSourcePoint(rig.RestRightHandPosition) - hip, reference.SampleSourcePoint(reference.RestRightHandPosition) - referenceHip) / BodyHeight;
            Vector2 torso = SampleProbe(current, entry.bodyShoulderProbe) - hip, referenceTorso = SampleProbe(referenceGeometry, entry.bodyShoulderProbe) - referenceHip;
            float angle = Vector2.Angle(torso, referenceTorso);
            if (action == "cast") { entry.castHandExcursion = Mathf.Max(entry.castHandExcursion, left, right); entry.castTorsoAngle = Mathf.Max(entry.castTorsoAngle, angle); return; }
            entry.attackHandExcursion = Mathf.Max(entry.attackHandExcursion, left, right); entry.attackTorsoAngle = Mathf.Max(entry.attackTorsoAngle, angle);
            Vector2 weapon = current[entry.weaponAxisSecond] - current[entry.weaponAxisFirst];
            Vector2 source = entry.restGeometry[entry.weaponAxisSecond] - entry.restGeometry[entry.weaponAxisFirst];
            float weaponAngle = Vector2.SignedAngle(source, weapon);
            entry.continuousWeaponAngle = entry.weaponAngleReady ? entry.continuousWeaponAngle + Mathf.DeltaAngle(entry.previousWeaponAngle, weaponAngle) : weaponAngle;
            entry.previousWeaponAngle = weaponAngle; entry.weaponAngleReady = true;
            entry.minimumWeaponAngle = Mathf.Min(entry.minimumWeaponAngle, entry.continuousWeaponAngle); entry.maximumWeaponAngle = Mathf.Max(entry.maximumWeaponAngle, entry.continuousWeaponAngle);
        }
        static float MeshStrain(Vector3[] rest, Vector3[] current, int[] indices)
        {
            float maximum = 0;
            for (int a = 0; a < indices.Length; a++) for (int b = a + 1; b < indices.Length; b++)
            {
                int first = indices[a], second = indices[b]; float length = Vector3.Distance(rest[first], rest[second]); if (length < .015f) continue;
                maximum = Mathf.Max(maximum, Mathf.Abs(Vector3.Distance(current[first], current[second]) / length - 1));
            }
            return maximum;
        }
        void MeasurePreview(Fixture fixture, int frame)
        {
            var art = fixture.art; var entry = fixture.report; var renderers = art.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds; foreach (var renderer in renderers) { bounds.Encapsulate(renderer.bounds); if (entry.artIndex >= 100) entry.stableScale &= Vector3.Distance(Abs(renderer.transform.localScale), Vector3.one) < .00001f; }
            entry.minimumHeight = Mathf.Min(entry.minimumHeight, bounds.size.y); entry.maximumHeight = Mathf.Max(entry.maximumHeight, bounds.size.y);
            if (frame > 0) entry.maximumHeightStep = Mathf.Max(entry.maximumHeightStep, Mathf.Abs(bounds.size.y - fixture.previousHeight)); fixture.previousHeight = bounds.size.y;
            var sprites = art.GetComponentsInChildren<SpriteRenderer>(); var identities = new Sprite[sprites.Length];
            for (int sprite = 0; sprite < sprites.Length; sprite++) { identities[sprite] = sprites[sprite].sprite; if (fixture.previousSprites != null && fixture.previousSprites[sprite] != identities[sprite]) entry.spriteIdentityChanges++; }
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

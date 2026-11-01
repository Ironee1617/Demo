using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Opt-in production art validation; never runs in an ordinary game.
    public sealed class TowerArtSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class ArtReport
        {
            public string finishedUtc;
            public bool success, animationsVerified, profilesVerified, timingRangesVerified, pixelPortraitsVerified, pointFilteringVerified, meshAfterimageVerified, effectsVerified, effectsExpired, attackBlendVerified;
            public int characters, motionSamples, changedMotionSamples, distinctProfiles, visibleEffects, animatedEffects;
            public List<string> animationChecks = new List<string>(), effectChecks = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        private sealed class Character { public string name; public int art; public CompanionRole role; }
        private readonly ArtReport report = new ArtReport();
        private readonly List<Character> characters = new List<Character>();
        private readonly List<string> labels = new List<string>();
        private readonly List<Rect> labelRects = new List<Rect>();
        private TowerGame game; private Camera cameraView; private Transform gallery; private Font font;
        private string prefix, reportPath, title, captureSuffix; private bool finishing;
        private const int Width = 1600, Height = 900;
        private static readonly string[] Motions = { "대기", "이동", "공격", "시전", "피격", "점프", "회피" };
        private static readonly BindingFlags HiddenStatic = BindingFlags.NonPublic | BindingFlags.Static;

        public void Initialize(TowerGame tower)
        {
            game = tower; string[] args = Environment.GetCommandLineArgs();
            prefix = Arg(args, "-towerArtCapture") ?? Path.Combine(Application.persistentDataPath, "tower-art");
            reportPath = Arg(args, "-towerArtReport") ?? Path.Combine(Application.persistentDataPath, "tower-art.json");
            Application.logMessageReceived += RecordLog;
            game.SmokeControlled = true; game.enabled = false; game.view.enabled = false;
            foreach (Transform child in game.transform) child.gameObject.SetActive(false);
            cameraView = new GameObject("Art verification camera").AddComponent<Camera>();
            cameraView.orthographic = true; cameraView.orthographicSize = 7.2f; cameraView.aspect = Width / (float)Height;
            cameraView.clearFlags = CameraClearFlags.SolidColor; cameraView.backgroundColor = new Color(.025f, .04f, .065f);
            cameraView.transform.position = new Vector3(0, 0, -10); font = Resources.Load<Font>("Tower/Font");
            characters.Add(new Character { name = "플레이어", art = 0, role = CompanionRole.Vanguard });
            foreach (var d in TowerRun.Definitions) characters.Add(new Character { name = d.Name, art = d.ArtIndex, role = d.Role });
            StartCoroutine(Guard(Suite()));
        }
        private static string Arg(string[] args, string key)
        { int at = Array.IndexOf(args, key); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
        private void RecordLog(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + condition + "\n" + trace); }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }
        private IEnumerator Guard(IEnumerator body)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(body);
            while (stack.Count > 0)
            {
                bool more = false; object value = null; Exception failure = null;
                try { more = stack.Peek().MoveNext(); if (more) value = stack.Peek().Current; } catch (Exception ex) { failure = ex; }
                if (failure != null) { Finish(failure); yield break; }
                if (!more) { stack.Pop(); continue; }
                var nested = value as IEnumerator; if (nested != null) stack.Push(nested); else yield return value;
            }
        }
        private IEnumerator Suite()
        {
            Require(Path.IsPathRooted(prefix) && Path.IsPathRooted(reportPath), "Art paths must be absolute.");
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            Require(characters.Count == TowerRun.Definitions.Count + 1 && characters.Count == 51, "Gallery must cover player and fifty mythology companions.");
            report.characters = characters.Count; var profiles = new HashSet<string>();
            bool portraits = true, filtering = true, timings = true;
            int pages = (characters.Count + 3) / 4;
            for (int page = 0; page < pages; page++)
            {
                NewGallery(); title = "신화 픽셀 캐릭터 · 동작 검증 " + (page + 1) + " / " + pages;
                for (int col = 0; col < 7; col++) Label(new Rect(col * 228 + 12, 58, 210, 24), Motions[col]);
                for (int row = 0; row < 4; row++) for (int motion = 0; motion < 7; motion++)
                {
                    int characterIndex = page * 4 + row; if (characterIndex >= characters.Count) continue;
                    var ch = characters[characterIndex];
                    float x = -12.8f + (motion + .5f) * (25.6f / 7);
                    float y = 7.2f - (86 + row * 195 + 184) * (14.4f / Height);
                    var root = new GameObject(ch.name + " " + Motions[motion]).transform;
                    root.SetParent(gallery, false); root.position = new Vector3(x, y, 0);
                    var art = TowerActorArt.Create(root, ch.art);
                    art.SetLocomotion(true, 0, false); art.SetPose(false, false, false, 1, .01f);
                    var initial = Pose(art); StartMotion(art, ch.role, motion); Advance(art, motion, 0);
                    int steps = motion == 4 ? 2 : motion == 2 ? Mathf.CeilToInt(art.AttackHitTime(.6f) / .04f) : motion == 3 ? Mathf.CeilToInt(art.CastReleaseTime(SkillEffect.Fireball, .6f) / .04f) : 4;
                    var first = Pose(art); for (int frame = 1; frame <= steps; frame++) Advance(art, motion, frame);
                    Require(Different(initial, Pose(art)) && (motion != 0 || Different(first, Pose(art))), ch.name + " motion did not change: " + Motions[motion]);
                    report.motionSamples++; report.changedMotionSamples++;
                    report.animationChecks.Add(ch.name + " / " + Motions[motion] + " / " + art.ProfileName + " / " + art.MotionName + ": PASS");
                    profiles.Add(art.ProfileName);
                    float hit = art.AttackHitTime(.6f), release = art.AttackReleaseTime(.6f), cast = art.CastReleaseTime(SkillEffect.Fireball, .6f);
                    timings &= hit > 0 && hit < .6f && release >= hit && release < .6f && cast > 0 && cast < .6f;
                    foreach (var r in art.GetComponentsInChildren<Renderer>()) filtering &= PointFiltered(r);
                    var portrait = TowerActorArt.Portrait(ch.art); portraits &= portrait != null && portrait.filterMode == FilterMode.Point;
                    if (page == 0 && row == 0 && motion == 6) VerifyAfterimage(art);
                    TowerWorld.SpriteObject(gallery, "Gallery ground", new Vector2(x, y - .05f), new Vector2(3.1f, .015f), new Color(.3f, .4f, .45f), -1);
                    if (motion == 0) { VerifyBlend(ch.art); Label(new Rect(12, 86 + row * 195, 215, 42), ch.name + " · " + art.ProfileName); }
                }
                yield return Capture("motion-" + (page + 1));
            }
            report.distinctProfiles = profiles.Count; report.profilesVerified = profiles.Count == characters.Count;
            report.animationsVerified = report.motionSamples == characters.Count * Motions.Length && report.changedMotionSamples == report.motionSamples;
            report.pixelPortraitsVerified = portraits; report.pointFilteringVerified = filtering; report.timingRangesVerified = timings;
            report.attackBlendVerified = true;
            Require(report.profilesVerified && portraits && filtering && timings, "Profile, portrait, filtering or timing check failed.");
            yield return Effects(); Finish(null);
        }
        private static void StartMotion(TowerActorArt art, CompanionRole role, int motion)
        {
            if (motion == 2) art.PlayAttack(.6f, 0);
            if (motion == 3) art.PlayCast(role == CompanionRole.Mage ? SkillEffect.Fireball : role == CompanionRole.Archer ? SkillEffect.MultiShot : role == CompanionRole.Healer ? SkillEffect.HealPulse : SkillEffect.Shockwave, .6f);
            if (motion == 5) art.SetLocomotion(false, 6, false);
            if (motion == 6) art.SetLocomotion(true, 0, true);
        }
        private void VerifyBlend(int index)
        {
            var root = new GameObject("Attack recovery fixture"); root.transform.SetParent(gallery, false); root.transform.position = Vector3.one * 1000;
            var referenceRoot = new GameObject("Live locomotion reference"); referenceRoot.transform.SetParent(root.transform, false);
            var attackRoot = new GameObject("Attack sample"); attackRoot.transform.SetParent(root.transform, false);
            var reference = TowerActorArt.Create(referenceRoot.transform, index); var sample = TowerActorArt.Create(attackRoot.transform, index);
            reference.SetLocomotion(true, 0, false, 5); sample.SetLocomotion(true, 0, false, 5);
            reference.SetPose(true, false, false, 1, .1f); sample.SetPose(true, false, false, 1, .1f);
            sample.PlayAttack(.4f); sample.SetPose(true, false, false, 1, 0);
            Require(PoseClose(reference, sample, .001f, .05f), "Starting an attack snapped away from current locomotion: " + index);
            for (int frame = 0; frame < 79; frame++) { reference.SetPose(true, false, false, 1, .005f); sample.SetPose(true, false, false, 1, .005f); }
            Require(PoseClose(reference, sample, .1f, 2), "Attack recovery did not blend toward the live locomotion pose: " + index);
            reference.SetPose(true, false, false, 1, .01f); sample.SetPose(true, false, false, 1, .01f);
            Require(PoseClose(reference, sample, .001f, .05f), "Attack completion did not reconnect to live locomotion: " + index);
            root.SetActive(false); Destroy(root);
        }
        private static bool PoseClose(TowerActorArt reference, TowerActorArt sample, float positionTolerance, float angleTolerance)
        {
            var a = reference.GetComponentsInChildren<Transform>(); var b = sample.GetComponentsInChildren<Transform>();
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (Vector3.Distance(a[i].localPosition, b[i].localPosition) > positionTolerance || Quaternion.Angle(a[i].localRotation, b[i].localRotation) > angleTolerance) return false;
            var first = MeshVertices(reference); var second = MeshVertices(sample);
            if (first.Count != second.Count) return false;
            for (int i = 0; i < first.Count; i++)
                if (Vector3.Distance(first[i], second[i]) > positionTolerance) return false;
            return true;
        }
        private static void Advance(TowerActorArt art, int motion, int frame)
        {
            if (motion == 1 || motion == 6) art.SetLocomotion(true, 0, motion == 6, motion == 6 ? 7 : 3);
            if (motion == 5) art.SetLocomotion(false, frame < 2 ? 5 : -3, false);
            art.SetPose(motion == 1 || motion == 6, false, motion == 4 && frame == 0, 1, .04f);
        }
        private static float[] Pose(TowerActorArt art)
        {
            var values = new List<float>();
            var god = art.GetComponent<TowerGodPixelArt>();
            if (god != null) values.Add(god.CurrentFrame);
            foreach (var t in art.GetComponentsInChildren<Transform>())
            { var p = t.localPosition; var r = t.localEulerAngles; var s = t.localScale; values.AddRange(new[] { p.x, p.y, p.z, r.x, r.y, r.z, s.x, s.y, s.z }); }
            foreach (var vertex in MeshVertices(art)) values.AddRange(new[] { vertex.x, vertex.y, vertex.z });
            return values.ToArray();
        }
        private static List<Vector3> MeshVertices(TowerActorArt art)
        {
            var vertices = new List<Vector3>();
            foreach (var filter in art.GetComponentsInChildren<MeshFilter>())
                foreach (var vertex in filter.sharedMesh.vertices)
                    vertices.Add(art.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)));
            return vertices;
        }
        private static bool Different(float[] a, float[] b)
        { if (a.Length != b.Length) return true; for (int i = 0; i < a.Length; i++) if (Mathf.Abs(a[i] - b[i]) > .00001f) return true; return false; }
        private static bool PointFiltered(Renderer r)
        {
            var sprite = r as SpriteRenderer;
            Texture texture = sprite != null && sprite.sprite != null ? sprite.sprite.texture : r.sharedMaterial != null ? r.sharedMaterial.mainTexture : null;
            return texture != null && texture.filterMode == FilterMode.Point;
        }
        // Combat effects are generated at 128 pixels per unit with trilinear filtering; pixel-art actors stay point filtered.
        private static bool HighResolution(Renderer r)
        {
            var sprite = r as SpriteRenderer;
            Texture texture = sprite != null && sprite.sprite != null ? sprite.sprite.texture : r.sharedMaterial != null ? r.sharedMaterial.mainTexture : null;
            return texture != null && texture.filterMode != FilterMode.Point && Mathf.Min(texture.width, texture.height) >= 32 && Mathf.Max(texture.width, texture.height) >= 64;
        }
        private void VerifyAfterimage(TowerActorArt art)
        {
            var service = TowerVfx.Create(gallery); service.Afterimage(art.transform, .25f);
            var copies = service.GetComponentsInChildren<MeshFilter>();
            Require(copies.Length > 0 && Visible(service.transform) > 0, "A weighted-mesh actor did not produce a visible dash afterimage.");
            var frozen = new List<Vector3[]>(); foreach (var copy in copies) frozen.Add(copy.sharedMesh.vertices);
            var before = Pose(art); art.SetPose(true, false, false, 1, .08f);
            Require(Different(before, Pose(art)), "The source pose did not advance during the afterimage fixture.");
            for (int mesh = 0; mesh < copies.Length; mesh++)
            {
                var after = copies[mesh].sharedMesh.vertices;
                Require(after.Length == frozen[mesh].Length, "An afterimage mesh changed topology.");
                for (int vertex = 0; vertex < after.Length; vertex++) Require(after[vertex] == frozen[mesh][vertex], "Afterimage vertices followed the live actor instead of freezing its pose.");
            }
            service.Tick(.3f); Require(service.ActiveCount == 0, "A dash afterimage did not expire.");
            report.meshAfterimageVerified = true;
        }
        private IEnumerator Effects()
        {
            NewGallery(); title = "실제 스킬 VFX · 시전 단계 " + Enum.GetValues(typeof(SkillEffect)).Length + "종";
            var services = new List<TowerVfx>(); var roots = new List<Transform>(); var snapshots = new List<string>(); var positions = new List<Vector2>();
            var colorMethod = typeof(TowerGame).GetMethod("SkillColor", BindingFlags.NonPublic | BindingFlags.Instance);
            var effects = (SkillEffect[])Enum.GetValues(typeof(SkillEffect));
            for (int i = 0; i < effects.Length; i++)
            {
                var parent = new GameObject(effects[i] + " gallery").transform; parent.SetParent(gallery, false); roots.Add(parent);
                var service = TowerVfx.Create(parent); services.Add(service);
                // Seven to a row so all twenty-eight effects stay in the captured view.
                var pos = new Vector2(-9.6f + (i % 7) * 3.2f, 4.5f - (i / 7) * 3.2f); positions.Add(pos);
                var color = (Color)colorMethod.Invoke(game, new object[] { effects[i] });
                service.Cast(effects[i], pos, 1.2f, color); service.Tick(.08f);
                Require(service.ActiveCount > 0 && Visible(parent) > 0, "Invisible Cast VFX: " + effects[i]);
                report.visibleEffects++; snapshots.Add(Visuals(parent));
                Label(new Rect((i % 7) * 228 + 10, 78 + (i / 7) * 200, 220, 24), effects[i].ToString());
            }
            yield return Capture("effects-cast");
            for (int i = 0; i < effects.Length; i++)
            {
                services[i].Tick(.12f); Require(snapshots[i] != Visuals(roots[i]), "VFX did not animate: " + effects[i]); report.animatedEffects++;
                var color = (Color)colorMethod.Invoke(game, new object[] { effects[i] });
                services[i].Impact(effects[i], positions[i], 1.4f, color); services[i].Tick(.08f);
                Require(Visible(roots[i]) > 0, "Invisible Impact VFX: " + effects[i]);
                foreach (var r in roots[i].GetComponentsInChildren<Renderer>()) Require(HighResolution(r), "VFX texture is not a filtered high-resolution sprite: " + r.name);
                report.effectChecks.Add(effects[i] + ": Cast visible / Tick animated / Impact visible");
            }
            title = "실제 스킬 VFX · 충돌 단계 " + effects.Length + "종"; yield return Capture("effects-impact");
            bool expired = true; foreach (var service in services) { service.Tick(10); expired &= service.ActiveCount == 0; }
            report.effectsVerified = report.visibleEffects == effects.Length && report.animatedEffects == effects.Length; report.effectsExpired = expired;
            Require(report.effectsVerified && expired, "VFX visibility, animation or cleanup failed.");
        }
        private static int Visible(Transform parent)
        { int count = 0; foreach (var r in parent.GetComponentsInChildren<Renderer>()) { var sprite = r as SpriteRenderer; if (sprite != null && sprite.color.a <= .02f) continue; if (r.enabled && r.gameObject.activeInHierarchy && r.bounds.size.sqrMagnitude > .0001f) count++; } return count; }
        private static string Visuals(Transform parent)
        {
            string value = "";
            foreach (var r in parent.GetComponentsInChildren<SpriteRenderer>())
                value += r.transform.localPosition.ToString("F4") + r.transform.localScale.ToString("F4") + r.transform.localEulerAngles.ToString("F4") + r.color + (r.sprite == null ? "null" : r.sprite.GetInstanceID().ToString());
            return value;
        }
        private void NewGallery()
        { if (gallery != null) { gallery.gameObject.SetActive(false); Destroy(gallery.gameObject); } gallery = new GameObject("Production gallery").transform; labels.Clear(); labelRects.Clear(); }
        private void Label(Rect rect, string label) { labelRects.Add(rect); labels.Add(label); }
        private void DrawLabels()
        {
            GUI.matrix = Matrix4x4.identity; GUI.color = Color.white; GUI.contentColor = Color.white; GUI.enabled = true;
            var style = new GUIStyle { font = font, fontSize = 25, wordWrap = true }; style.normal.textColor = new Color(.85f, .9f, .92f);
            GUI.Label(new Rect(18, 10, 1540, 42), title, style); style.fontSize = 15;
            for (int i = 0; i < labels.Count; i++) GUI.Label(labelRects[i], labels[i], style);
            style.fontSize = 12; GUI.Label(new Rect(18, 878, 1540, 20), "Production animation and VFX / Pixel humanoids / High-resolution light effects", style);
        }
        private IEnumerator Capture(string suffix)
        {
            string charactersToDraw = title + "Production animation and VFX / Pixel humanoids / High-resolution light effects" + string.Join(" ", labels.ToArray());
            foreach (int size in new[] { 25, 15, 12 }) font.RequestCharactersInTexture(charactersToDraw, size, FontStyle.Normal);
            yield return null;
            yield return null;
            captureSuffix = suffix;
            float limit = Time.realtimeSinceStartup + 8;
            yield return null;
            while (captureSuffix != null)
            {
                Require(Time.realtimeSinceStartup < limit, "Gallery capture timed out: " + suffix);
                yield return null;
            }
            yield return null;
        }
        private void LateUpdate()
        {
            if (finishing || captureSuffix == null) return;
            string suffix = captureSuffix; captureSuffix = null;
            try { CaptureNow(suffix); } catch (Exception ex) { Finish(ex); }
        }
        private void CaptureNow(string suffix)
        {
            var target = new RenderTexture(Width, Height, 24); var previous = RenderTexture.active;
            var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            object state = null; bool container = false, began = false, matrix = false; Event previousEvent = Event.current; var gui = typeof(GUIUtility);
            try
            {
                state = Activator.CreateInstance(gui.Assembly.GetType("UnityEngine.ObjectGUIState"), true);
                gui.GetMethod("BeginContainer", HiddenStatic).Invoke(null, new[] { state }); container = true;
                gui.GetMethod("BeginGUI", HiddenStatic).Invoke(null, new object[] { 0, GetInstanceID(), 0 }); began = true;
                Event.current = new Event { type = EventType.Repaint };
                cameraView.targetTexture = target; cameraView.Render(); cameraView.targetTexture = null; RenderTexture.active = target;
                GL.PushMatrix(); matrix = true; GL.LoadPixelMatrix(0, Width, Height, 0);
                DrawLabels(); GL.Flush(); pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply();
                string path = prefix + "-" + suffix + ".png"; File.WriteAllBytes(path, pixels.EncodeToPNG()); report.screenshots.Add(path);
            }
            finally
            {
                if (matrix) GL.PopMatrix();
                if (began) gui.GetMethod("EndGUI", HiddenStatic).Invoke(null, new object[] { 0 });
                if (container) gui.GetMethod("EndContainer", HiddenStatic).Invoke(null, null);
                if (state != null) ((IDisposable)state).Dispose(); if (previousEvent != null) Event.current = previousEvent;
                cameraView.targetTexture = null; RenderTexture.active = previous; target.Release(); Destroy(target); Destroy(pixels);
            }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Finish(Exception error)
        {
            if (finishing) return; finishing = true; if (error != null) report.errors.Add(error.ToString());
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true)); Application.Quit(report.success ? 0 : 1);
        }
    }
}

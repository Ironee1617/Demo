using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    // R77 review captures (Play mode only, nothing saved to the scene):
    //  * Ending: a practice round is finished with a chosen ending and the real HUD is captured (through the
    //    smoke driver's HUD capture) at fixed title ages, with the title clock set for each frame.
    //  * Reel: a small stage (own view, model, cameras) spears an alien in front of the diver and records the
    //    reel-in from the diver's eye, from the side and from another alien, with the jelly on or off.
    public static class AquariumR77QA
    {
        public static string Out => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "ClaudeQA");
        public static string Status = "idle";
        static Action tick;
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static void Run(Action step) { Stop(); tick = step; EditorApplication.update += Update; }
        static void Stop() { EditorApplication.update -= Update; tick = null; }
        static void Update()
        {
            if (!EditorApplication.isPlaying) { Status = "stopped: not playing"; Stop(); return; }
            try { tick?.Invoke(); } catch (Exception e) { Status = "error: " + e; Stop(); }
        }

        // ------------------------------------------------------------------ ending title
        // scenario: 0 diver wins WIPED OUT, 1 alien ESCAPED, 2 diver YOU'RE FIRED (time), 3 alien sees WIPED OUT
        public static string StartEnding(string folder, int scenario, float[] ages, float[] finalAges)
        {
            var game = UnityEngine.Object.FindFirstObjectByType<AquariumGame>();
            if (game == null) return "no game";
            var session = (AquariumSession)typeof(AquariumGame).GetField("session", Any).GetValue(game);
            var smoke = typeof(AquariumGame).GetField("smoke", Any).GetValue(game);
            var smokeView = smoke.GetType().GetField("view", Any);
            if (smokeView.GetValue(smoke) == null) smokeView.SetValue(smoke, typeof(AquariumGame).GetField("view", Any).GetValue(game));
            float curAge = 0; bool curFinal = false;
            string dir = Path.Combine(Out, folder); Directory.CreateDirectory(dir);
            bool alienView = scenario == 1 || scenario == 3;
            session.StartPractice("QA", alienView ? Team.Hider : Team.Seeker);
            int frame = 0, index = -1; bool final = false; string pending = null; int wait = 0;
            var log = new List<string>();
            Run(() =>
            {
                frame++;
                if (frame == 20)
                {
                    var model = session.Model;
                    var reason = scenario == 0 || scenario == 3 ? WinReason.AllAliensCaught : scenario == 1 ? WinReason.AllAliensEscaped : WinReason.TimeExpired;
                    var team = scenario == 0 || scenario == 3 ? Team.Seeker : Team.Hider;
                    if (scenario == 1) foreach (var p in model.Players) if (p.team == Team.Hider) p.escaped = true;
                    typeof(AquariumModel).GetMethod("Finish", Any).Invoke(model, new object[] { team, reason });
                }
                if (frame < 40) return;
                if (pending != null)
                {
                    // Hold the clock on the wanted age until the capture has been drawn.
                    if ((bool)smoke.GetType().GetField("hudPending", Any).GetValue(smoke))
                    {
                        if (++wait < 120) return;
                    }
                    if (!File.Exists(pending) && ++wait < 120) return;
                    pending = null;
                }
                index++;
                var list = final ? finalAges : ages;
                if (index >= list.Length)
                {
                    if (!final && finalAges != null && finalAges.Length > 0)
                    {
                        final = true; index = -1;
                        typeof(AquariumModel).GetProperty("ReviewEndsAt", Any).SetValue(session.Model, session.Model.Time);
                        frame = 30; return;
                    }
                    AquariumGame.QaTitleAge = AquariumGame.QaFinalAge = -1;
                    File.WriteAllLines(Path.Combine(dir, "log.txt"), log);
                    Status = "done " + folder + " " + Screen.width + "x" + Screen.height; Stop(); return;
                }
                float age = list[index];
                float now = Time.unscaledTime;
                if (!final) AquariumGame.QaTitleAge = age; else { AquariumGame.QaTitleAge = 9; AquariumGame.QaFinalAge = age; }
                typeof(AquariumGame).GetField("r77Seen", Any).SetValue(game, now);
                string file = Path.Combine(dir, (final ? "final_" : "title_") + index.ToString("00") + "_" + age.ToString("0.000") + ".png");
                if (File.Exists(file)) File.Delete(file);
                smoke.GetType().GetField("hudCapturePath", Any).SetValue(smoke, file);
                smoke.GetType().GetField("hudPending", Any).SetValue(smoke, true);
                pending = file; wait = 0; curAge = age; curFinal = final;
                log.Add(Path.GetFileName(file) + " title=" + AquariumModel.EndingTitle(session.State.winReason, session.State.players.FirstOrDefault(p => p.id == session.LocalId)));
            });
            return "started";
        }

        // ------------------------------------------------------------------ sheets and GIFs
        // crop = x, y, w, h as fractions of the image from its top left.
        static Color[] Crop(Texture2D tex, Rect crop, int w, out int h)
        {
            float cw = crop.width * tex.width, ch = crop.height * tex.height;
            h = Mathf.Max(1, Mathf.RoundToInt(w * ch / cw));
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = crop.x + crop.width * (x + .5f) / w, v = 1 - (crop.y + crop.height * (1 - (y + .5f) / h));
                    px[y * w + x] = tex.GetPixelBilinear(u, v);
                }
            return px;
        }
        static string[] Frames(string folder, string pattern)
        {
            var files = Directory.GetFiles(Path.Combine(Out, folder), pattern); Array.Sort(files, StringComparer.Ordinal); return files;
        }
        public static string Sheet(string folder, string pattern, Rect crop, int cols, int thumb, string outFile, int step = 1)
        {
            var files = Frames(folder, pattern).Where((f, i) => i % step == 0).ToArray(); if (files.Length == 0) return "no frames";
            var tex = new Texture2D(2, 2); int h = 0; var cells = new List<Color[]>();
            foreach (var f in files) { tex.LoadImage(File.ReadAllBytes(f)); cells.Add(Crop(tex, crop, thumb, out h)); }
            int rows = (cells.Count + cols - 1) / cols, gap = 4;
            var sheet = new Texture2D(cols * (thumb + gap) + gap, rows * (h + gap) + gap, TextureFormat.RGB24, false);
            var bg = new Color[sheet.width * sheet.height]; for (int i = 0; i < bg.Length; i++) bg[i] = new Color(.08f, .09f, .11f); sheet.SetPixels(bg);
            for (int i = 0; i < cells.Count; i++) { int c = i % cols, r = i / cols; sheet.SetPixels(gap + c * (thumb + gap), sheet.height - (r + 1) * (h + gap), thumb, h, cells[i]); }
            sheet.Apply(); File.WriteAllBytes(Path.Combine(Out, outFile), sheet.EncodeToJPG(85));
            UnityEngine.Object.DestroyImmediate(sheet); UnityEngine.Object.DestroyImmediate(tex);
            return outFile + " " + files.Length;
        }
        public static string Gif(string folder, string pattern, Rect crop, int width, int delayCs, string outFile, int holdLast = 0, int step = 1)
        {
            var files = Frames(folder, pattern).Where((f, i) => i % step == 0).ToArray(); if (files.Length == 0) return "no frames";
            var tex = new Texture2D(2, 2); int h = 0;
            using (var stream = new FileStream(Path.Combine(Out, outFile), FileMode.Create))
            {
                var w = new BinaryWriter(stream);
                w.Write(System.Text.Encoding.ASCII.GetBytes("GIF89a"));
                tex.LoadImage(File.ReadAllBytes(files[0])); Crop(tex, crop, width, out h);
                w.Write((ushort)width); w.Write((ushort)h); w.Write((byte)0xF7); w.Write((byte)0); w.Write((byte)0);
                for (int i = 0; i < 256; i++)
                {
                    int r = i % 6, g = (i / 6) % 7, b = i / 42; if (i >= 252) { r = g = b = 0; }
                    w.Write((byte)(r * 51)); w.Write((byte)(g * 255 / 6)); w.Write((byte)(Mathf.Min(b, 5) * 51));
                }
                w.Write(new byte[] { 0x21, 0xFF, 0x0B }); w.Write(System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0")); w.Write(new byte[] { 3, 1, 0, 0, 0 });
                int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                for (int fi = 0; fi < files.Length; fi++)
                {
                    tex.LoadImage(File.ReadAllBytes(files[fi])); int hh; var px = Crop(tex, crop, width, out hh);
                    var idx = new byte[width * h];
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < width; x++)
                        {
                            var c = px[(h - 1 - Mathf.Min(y, hh - 1)) * width + x];
                            float d = (bayer[(y & 3) * 4 + (x & 3)] + .5f) / 16f - .5f;
                            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 5 + d), 0, 5), g = Mathf.Clamp(Mathf.RoundToInt(c.g * 6 + d), 0, 6), b = Mathf.Clamp(Mathf.RoundToInt(c.b * 5 + d), 0, 5);
                            idx[y * width + x] = (byte)(r + g * 6 + b * 42);
                        }
                    int delay = fi == files.Length - 1 ? delayCs + holdLast : delayCs;
                    w.Write(new byte[] { 0x21, 0xF9, 4, 0 }); w.Write((ushort)delay); w.Write((byte)0); w.Write((byte)0);
                    w.Write((byte)0x2C); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)width); w.Write((ushort)h); w.Write((byte)0);
                    Lzw(w, idx);
                }
                w.Write((byte)0x3B); w.Flush();
            }
            UnityEngine.Object.DestroyImmediate(tex);
            return outFile + " " + files.Length + " frames " + width + "x" + h;
        }
        static void Lzw(BinaryWriter w, byte[] idx)
        {
            w.Write((byte)8);
            var output = new List<byte>(); int acc = 0, bits = 0, size = 9, next = 258; bool clearFlag = false;
            var dict = new Dictionary<int, int>();
            Action<int> emit = code =>
            {
                acc |= code << bits; bits += size;
                while (bits >= 8) { output.Add((byte)(acc & 255)); acc >>= 8; bits -= 8; }
                if (clearFlag) { size = 9; clearFlag = false; }
                else if (next > (1 << size) - 1 && size < 12) size++;
            };
            clearFlag = true; emit(256);
            int prefix = idx[0];
            for (int i = 1; i < idx.Length; i++)
            {
                int k = idx[i], key = (prefix << 8) | k, code;
                if (dict.TryGetValue(key, out code)) { prefix = code; continue; }
                emit(prefix);
                if (next < 4096) dict[key] = next++;
                else { clearFlag = true; emit(256); dict.Clear(); next = 258; }
                prefix = k;
            }
            emit(prefix); emit(257);
            if (bits > 0) output.Add((byte)(acc & 255));
            for (int i = 0; i < output.Count; i += 255)
            {
                int n = Math.Min(255, output.Count - i); w.Write((byte)n);
                for (int j = 0; j < n; j++) w.Write(output[i + j]);
            }
            w.Write((byte)0);
        }

        // ------------------------------------------------------------------ reel-in
        sealed class Stage
        {
            public GameObject parent; public AquariumView view; public AquariumModel model; public AquariumSnapshot state;
            public Camera side, other; public RenderTexture eyeRt, sideRt, otherRt;
        }
        static Stage stage;
        static GameObject hiddenGame;

        public static string StartReel(string folder, bool jelly, bool corpse, int every)
        {
            AquariumSkewerJelly.Enabled = jelly;
            var game = UnityEngine.Object.FindFirstObjectByType<AquariumGame>();
            if (game != null) { hiddenGame = game.gameObject; hiddenGame.SetActive(false); }
            string dir = Path.Combine(Out, folder); Directory.CreateDirectory(dir);
            var s = stage = new Stage();
            s.parent = new GameObject("R77 QA stage"); s.view = new AquariumView(s.parent.transform);
            s.model = new AquariumModel(); s.model.AddPlayer(1, "Alien", Team.Hider); s.model.AddPlayer(2, "Diver", Team.Seeker); s.model.AddPlayer(3, "Alien 2", Team.Hider);
            s.model.SetRole(2, Team.Seeker, true); s.model.SetRole(3, Team.Hider, true);
            if (!s.model.StartRound()) return "round did not start";
            s.model.Players.RemoveAll(p => p.filled);
            for (int i = 0; i < 2000 && s.model.Time < s.model.HuntAt + .1f; i++) s.model.Tick(1f / 30);
            var diver = s.model.Player(2); diver.position = new Point(2.3f, 9, 4); diver.facing = new Point(0, 0, 1); diver.entryAt = 0;
            var alien = s.model.Player(1); alien.position = new Point(2.3f, 9, 9.5f); alien.facing = new Point(-1, 0, 0); alien.lastMealAt = float.MaxValue;
            if (corpse) { alien.alive = false; alien.deathReason = "starved"; alien.deathAt = s.model.Time - 10; alien.diedAt = s.model.Time - 10; }
            else alien.harpoonHits = AquariumModel.HitsToKill(alien) - 1;
            s.model.Player(3).position = new Point(-20, 8, 3); s.model.Player(3).lastMealAt = float.MaxValue;
            foreach (var f in s.model.Fish) { f.position = new Point(-20, 8, -5); f.facing = new Point(1, 0); f.turnAt = float.MaxValue; f.steering = new Point(); f.swimSpeed = 0; }
            s.eyeRt = new RenderTexture(960, 600, 24) { antiAliasing = 4 }; s.sideRt = new RenderTexture(960, 600, 24) { antiAliasing = 4 }; s.otherRt = new RenderTexture(960, 600, 24) { antiAliasing = 4 };
            s.view.Camera.targetTexture = s.eyeRt; s.view.Camera.fieldOfView = 62; s.view.Camera.enabled = true;
            foreach (var c in s.view.Camera.GetComponentsInChildren<Camera>(true)) if (c != s.view.Camera) c.targetTexture = s.eyeRt;
            s.side = MakeCamera("R77 side", s.sideRt); s.other = MakeCamera("R77 other alien", s.otherRt);
            int frame = 0, shot = -1; float t0 = 0;
            Vector3 sideAt = Vector3.zero;
            var log = new List<string> { "frame,t,returning,seated,stretch,swing" };
            Run(() =>
            {
                frame++;
                float dt = Mathf.Clamp(Time.unscaledDeltaTime, .005f, .05f);
                // Last frame's images (rendered after LateUpdate: arm drag and spring bones included).
                if (shot >= 0 && frame % every == 0)
                {
                    string n = shot.ToString("000") + "_" + (s.model.Time - t0).ToString("0.00");
                    Save(s.eyeRt, Path.Combine(dir, "eye_" + n + ".jpg"));
                    Save(s.sideRt, Path.Combine(dir, "side_" + n + ".jpg"));
                    Save(s.otherRt, Path.Combine(dir, "other_" + n + ".jpg"));
                }
                if (frame == 3) { s.model.SetInput(2, new SwimInput { aimZ = 1, action = true }); t0 = s.model.Time; shot = 0; }
                else if (frame == 4) s.model.SetInput(2, new SwimInput { aimZ = 1 });
                s.model.Tick(dt);
                s.state = JsonUtility.FromJson<AquariumSnapshot>(JsonUtility.ToJson(s.model.Snapshot()));
                // The diver's eye.
                var d = s.model.Player(2);
                s.view.Camera.transform.SetPositionAndRotation(new Vector3(d.position.x, d.position.y + .45f, d.position.z), Quaternion.LookRotation(new Vector3(0, -.05f, 1)));
                s.view.Draw(s.state, 2, dt);
                s.view.PresentEquipment(s.state, 2, true);
                var body = s.parent.GetComponentsInChildren<AquariumCreature>().Select(c => c.transform.parent).FirstOrDefault(t => t != null && t.name == "alien" && (t.position - new Vector3(-20, 8, 3)).sqrMagnitude > 4);
                var focus = new Vector3(d.position.x, d.position.y, d.position.z + 3.2f);
                // Side view: beside the body (square to the reel line), following it while it is on the spear.
                if (body != null && s.state.harpoons.Length > 0) sideAt = Vector3.Lerp(sideAt == Vector3.zero ? body.position : sideAt, body.position + new Vector3(0, .1f, 0), .35f);
                var sideFocus = sideAt == Vector3.zero ? focus : sideAt;
                s.side.transform.position = sideFocus + new Vector3(2.7f, .55f, 1.9f); s.side.transform.LookAt(sideFocus);
                var otherAt = body != null ? body.position : focus;
                s.other.transform.position = new Vector3(d.position.x - 2.6f, d.position.y + .8f, d.position.z + 8.5f); s.other.transform.LookAt(Vector3.Lerp(otherAt, focus, .4f));
                if (shot >= 0) shot++;
                if (s.state.harpoons.Length > 0)
                {
                    var h = s.state.harpoons[0];
                    float stretch = 0, swing = 0;
                    var creature = body != null ? body.GetComponentInChildren<AquariumCreature>() : null;
                    if (creature != null) { var sc = creature.transform.localScale; stretch = sc.x / Mathf.Max(.0001f, sc.y); }
                    log.Add(shot + "," + (s.model.Time - t0).ToString("0.000") + "," + h.returning + "," + (s.model.Time >= h.retrievedAt) + "," + stretch.ToString("0.000") + "," + (creature != null ? creature.transform.localEulerAngles.x.ToString("0.0") : "-"));
                }
                if (shot > 0 && (s.model.Time - t0 > 7.5f || shot > 900))
                {
                    File.WriteAllLines(Path.Combine(dir, "log.csv"), log);
                    Status = "done " + folder + " frames=" + shot; EndReel();
                }
            });
            return "started";
        }
        static Camera MakeCamera(string name, RenderTexture rt)
        {
            var c = new GameObject(name).AddComponent<Camera>(); c.transform.SetParent(stage.parent.transform);
            c.targetTexture = rt; c.fieldOfView = 46; c.nearClipPlane = .05f; c.farClipPlane = 400;
            c.clearFlags = stage.view.Camera.clearFlags; c.backgroundColor = stage.view.Camera.backgroundColor;
            return c;
        }
        static void Save(RenderTexture rt, string file)
        {
            var old = RenderTexture.active; RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
            File.WriteAllBytes(file, image.EncodeToJPG(88)); RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image);
        }
        public static void EndReel()
        {
            Stop();
            AquariumSkewerJelly.Enabled = true;
            if (stage != null)
            {
                stage.view.Camera.targetTexture = null;
                try { stage.view.Dispose(); } catch (Exception) { }
                UnityEngine.Object.Destroy(stage.parent);
                foreach (var rt in new[] { stage.eyeRt, stage.sideRt, stage.otherRt }) if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
                stage = null;
            }
            if (hiddenGame != null) { hiddenGame.SetActive(true); hiddenGame = null; }
        }
    }
}

using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    // R78a review helpers (Play mode only, nothing saved): a per-frame step runner, Game view captures and the OS
    // pointer (to hover real IMGUI controls for the hover checks).
    public static class AquariumR78aQA
    {
        public static string Out => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "ClaudeQA");
        public static string Status = "idle";
        public static string Log = "";
        static Action tick;

        public static void Run(Action step) { Stop(); tick = step; EditorApplication.update += Update; }
        public static void Stop() { EditorApplication.update -= Update; tick = null; }
        static void Update()
        {
            if (!EditorApplication.isPlaying) { Status = "stopped: not playing"; Stop(); return; }
            try { tick?.Invoke(); } catch (Exception e) { Status = "error: " + e; Stop(); }
        }

#if UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential)] struct P { public int x, y; }
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out P p);
        public static void SetPointer(int x, int y) { SetCursorPos(x, y); }
        public static Vector2 Pointer() { P p; GetCursorPos(out p); return new Vector2(p.x, p.y); }
#else
        public static void SetPointer(int x, int y) { }
        public static Vector2 Pointer() { return Vector2.zero; }
#endif
    }
}

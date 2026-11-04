using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    // R66: builds a measurement player (release by default) with the opt-in frame logger (AquariumFrameLogR66) and
    // starts it once. The build runs on the next editor tick so a tool call that schedules it returns at once.
    // Scenes-in-build and player settings are left as they are (frame timing stats are switched on only for the build).
    public static class AquariumR66PerfBuild
    {
        public static string LastResult = "";
        static bool pending;

        public static string Schedule(string label, string runs, float seconds, bool screens, bool development, bool autoRun)
        { return Schedule(label, runs, seconds, screens, development, autoRun, false, ""); }
        // api: "" keeps the project's graphics API list; "d3d11" or "d3d12" puts that one first for this build only.
        public static string Schedule(string label, string runs, float seconds, bool screens, bool development, bool autoRun, bool ab, string api)
        { return Schedule(label, runs, seconds, screens, development, autoRun, ab, api, ""); }
        // extra: more config lines for the logger, e.g. "fast=1" (the pause menu's faster picture).
        public static string Schedule(string label, string runs, float seconds, bool screens, bool development, bool autoRun, bool ab, string api, string extra)
        {
            if (pending || BuildPipeline.isBuildingPlayer) return "busy";
            string dir = "D:/Demo/Builds/R66/" + label;
            string stamp = dir + "/scheduled.txt";
            if (File.Exists(stamp) && (DateTime.Now - File.GetLastWriteTime(stamp)).TotalMinutes < 15) return "already scheduled recently: " + stamp;
            Directory.CreateDirectory(dir);
            File.WriteAllText(stamp, DateTime.Now.ToString("o"));
            File.WriteAllText(dir + "/aq_r66_perf.txt", "out=D:/Demo/Temp/ClaudeQA/R66_" + label + "\nlabel=" + label + "\nseconds=" + seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "\nuncap=1\nscreens=" + (screens ? "1" : "0") + "\nab=" + (ab ? "1" : "0") + "\nruns=" + runs + "\n" + (extra.Length > 0 ? extra + "\n" : ""));
            pending = true; LastResult = "scheduled " + label;
            EditorApplication.CallbackFunction once = null;
            once = () => { EditorApplication.update -= once; Build(dir, development, autoRun, api); };
            EditorApplication.update += once;
            return "scheduled " + dir;
        }

        static void Build(string dir, bool development, bool autoRun, string api)
        {
            bool oldStats = PlayerSettings.enableFrameTimingStats;
            var oldApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            bool oldAuto = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
            var t0 = DateTime.Now;
            try
            {
                PlayerSettings.enableFrameTimingStats = true;
                if (api == "d3d11") { PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false); PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11, UnityEngine.Rendering.GraphicsDeviceType.Direct3D12 }); }
                if (api == "d3d12") { PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false); PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D12, UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 }); }
                var options = BuildOptions.None;
                if (development) options |= BuildOptions.Development;
                if (autoRun) options |= BuildOptions.AutoRunPlayer;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Demos/HideAndSeek/30_HideAndSeek.unity" },
                    locationPathName = dir + "/AquariumDemo.exe", target = BuildTarget.StandaloneWindows64,
                    options = options, extraScriptingDefines = new[] { "AQUARIUM_PERF" }
                });
                LastResult = report.summary.result + " in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + "s, errors " + report.summary.totalErrors;
            }
            catch (Exception e) { LastResult = "build exception: " + e.Message; }
            finally
            {
                PlayerSettings.enableFrameTimingStats = oldStats;
                if (api.Length > 0) { PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, oldAuto); if (!oldAuto) PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, oldApis); }
                pending = false;
            }
            File.WriteAllText(dir + "/build_result.txt", LastResult);
            Debug.Log("AQUARIUM_R66_PERF_BUILD " + LastResult);
        }
    }
}

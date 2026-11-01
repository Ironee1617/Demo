using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    // R52 QA helpers: preview-scene renders of the diver prefabs and mesh sanity statistics.
    public static class R52DiverQA
    {
        const string Equipment = "Assets/Demos/HideAndSeek/Equipment/Resources/AquariumEquipment";

        // views: "name|prefab|focus|yaw|pitch|dist|fov|mode|clip|time;..."  focus = bone name or "centre" or "x,y,z"
        // mode: 0 textured, 1 textured + wire, 2 white clay, 3 clay + wire
        public static string Render(string views, string outDir, int w, int h)
        {
            Directory.CreateDirectory(outDir);
            var log = "";
            foreach (var spec in views.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = spec.Split('|');
                log += RenderOne(p[1], p[2], float.Parse(p[3]), float.Parse(p[4]), float.Parse(p[5]), float.Parse(p[6]), int.Parse(p[7]),
                    p.Length > 8 ? p[8] : "", p.Length > 9 ? float.Parse(p[9]) : 0f, Path.Combine(outDir, p[0] + ".png"), w, h) + "\n";
            }
            return log;
        }

        static string RenderOne(string prefabName, string focus, float yaw, float pitch, float dist, float fov, int mode, string clip, float time, string path, int W, int H)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var prevAmb = RenderSettings.ambientLight; var prevMode = RenderSettings.ambientMode;
            Material clay = null; RenderTexture rt = null; Camera cam = null; Texture2D tex = null;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Equipment + "/" + prefabName + ".prefab");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
                if (!string.IsNullOrEmpty(clip))
                {
                    var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(Equipment + "/" + clip + ".anim");
                    if (c != null) { c.SampleAnimation(go, time); }
                }
                var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
                smr.updateWhenOffscreen = true;
                if (mode >= 2)
                {
                    clay = new Material(Shader.Find("Standard")); clay.color = new Color(.85f, .85f, .85f); clay.SetFloat("_Glossiness", .45f);
                    var mats = new Material[smr.sharedMaterials.Length]; for (int i = 0; i < mats.Length; i++) mats[i] = clay; smr.sharedMaterials = mats;
                }
                Vector3 target;
                if (focus == "centre") { var b = smr.bounds; target = b.center; }
                else if (focus.Contains(",")) { var f = focus.Split(','); target = new Vector3(float.Parse(f[0]), float.Parse(f[1]), float.Parse(f[2])); }
                else { var t = go.GetComponentsInChildren<Transform>(true).First(x => x.name == focus); target = t.position; }
                var camGo = new GameObject("QA cam"); SceneManager_Move(camGo, scene);
                cam = camGo.AddComponent<Camera>(); cam.scene = scene; cam.fieldOfView = fov; cam.nearClipPlane = .01f; cam.farClipPlane = 100;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.18f, .3f, .4f);
                var rot = Quaternion.Euler(pitch, yaw, 0);
                cam.transform.position = target - rot * Vector3.forward * dist; cam.transform.rotation = rot;
                var lightGo = new GameObject("QA light"); SceneManager_Move(lightGo, scene);
                var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.1f;
                light.transform.rotation = rot * Quaternion.Euler(35, -30, 0);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.5f, .55f, .6f);
                rt = new RenderTexture(W, H, 24) { antiAliasing = 8 }; cam.targetTexture = rt;
                cam.Render();
                if (mode == 1 || mode == 3)
                {
                    cam.clearFlags = CameraClearFlags.Nothing;
                    var wire = new Material(Shader.Find("Unlit/Color")); wire.color = mode == 1 ? Color.cyan : new Color(.1f, .1f, .1f);
                    var mats = new Material[smr.sharedMaterials.Length]; for (int i = 0; i < mats.Length; i++) mats[i] = wire; smr.sharedMaterials = mats;
                    GL.wireframe = true; cam.Render(); GL.wireframe = false;
                }
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(path, tex.EncodeToPNG());
                return Path.GetFileName(path) + " ok";
            }
            catch (Exception e) { return Path.GetFileName(path) + " ERR " + e.Message; }
            finally
            {
                GL.wireframe = false;
                RenderSettings.ambientLight = prevAmb; RenderSettings.ambientMode = prevMode;
                if (cam != null) cam.targetTexture = null;
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (clay != null) UnityEngine.Object.DestroyImmediate(clay);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void SceneManager_Move(GameObject g, UnityEngine.SceneManagement.Scene s) { UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g, s); }

        public static string Bones(string prefabName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Equipment + "/" + prefabName + ".prefab");
            var smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            return string.Join(" ", smr.bones.Select(b => b.name + "(" + b.position.x.ToString("0.00") + "," + b.position.y.ToString("0.00") + "," + b.position.z.ToString("0.00") + ")").ToArray())
                + " bounds=" + smr.sharedMesh.bounds;
        }

        public static string Stats(string meshName)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Equipment + "/" + meshName + ".asset");
            return StatsOf(mesh, meshName);
        }

        // Mesh statistics: degenerate / sliver triangles, open edges, flipped faces, normal creases at shared positions.
        public static string StatsOf(Mesh mesh, string meshName)
        {
            var v = mesh.vertices; var n = mesh.normals; var t = mesh.triangles;
            Func<Vector3, Vector3Int> key = p => new Vector3Int(Mathf.RoundToInt(p.x * 20000), Mathf.RoundToInt(p.y * 20000), Mathf.RoundToInt(p.z * 20000));
            var pid = new int[v.Length]; var map = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < v.Length; i++) { var k = key(v[i]); int id; if (!map.TryGetValue(k, out id)) { id = map.Count; map[k] = id; } pid[i] = id; }
            int degen = 0, sliver = 0, flipped = 0, dupTri = 0;
            var edges = new Dictionary<long, int>();
            var triSet = new HashSet<string>();
            for (int i = 0; i < t.Length; i += 3)
            {
                int a = t[i], b = t[i + 1], c = t[i + 2];
                var fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]); float area = fn.magnitude * .5f;
                if (area < 1e-10f || pid[a] == pid[b] || pid[b] == pid[c] || pid[a] == pid[c]) { degen++; continue; }
                float A = Vector3.Angle(v[b] - v[a], v[c] - v[a]), B = Vector3.Angle(v[a] - v[b], v[c] - v[b]); float C = 180 - A - B;
                float mn = Mathf.Min(A, Mathf.Min(B, C)); if (mn < 3f) sliver++;
                var avg = n[a] + n[b] + n[c]; if (Vector3.Dot(avg, fn) < 0) flipped++;
                int[] q = { pid[a], pid[b], pid[c] }; Array.Sort(q); if (!triSet.Add(q[0] + "_" + q[1] + "_" + q[2])) dupTri++;
                for (int e = 0; e < 3; e++)
                {
                    int x = q[e], y = q[(e + 1) % 3]; if (e == 2) { x = q[0]; y = q[2]; }
                    long k = ((long)Mathf.Min(x, y) << 32) | (uint)Mathf.Max(x, y); int cnt; edges.TryGetValue(k, out cnt); edges[k] = cnt + 1;
                }
            }
            int open = edges.Values.Count(x => x == 1), nonMan = edges.Values.Count(x => x > 2);
            var byPos = new Dictionary<int, List<int>>();
            for (int i = 0; i < v.Length; i++) { List<int> l; if (!byPos.TryGetValue(pid[i], out l)) byPos[pid[i]] = l = new List<int>(); l.Add(i); }
            int crease = 0, splitPos = 0;
            foreach (var l in byPos.Values) { if (l.Count < 2) continue; splitPos++; bool cr = false; for (int i = 1; i < l.Count; i++) if (Vector3.Angle(n[l[0]], n[l[i]]) > 20) cr = true; if (cr) crease++; }
            return meshName + ": verts=" + v.Length + " positions=" + map.Count + " tris=" + t.Length / 3 + " sub=" + mesh.subMeshCount + " degenerate=" + degen + " sliver(<3deg)=" + sliver
                + " flipped=" + flipped + " dupTri=" + dupTri + " openEdges=" + open + " nonManifold=" + nonMan + " splitPositions=" + splitPos + " normalCreases=" + crease
                + " uv2=" + mesh.uv2.Length;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Roguelike.Editor
{
    // Review the production meshes, including intermediate action silhouettes.
    // This creates only temporary objects; it never opens or saves a scene.
    public static class TowerLimbAudit
    {
        const int Width = 480, Height = 600;
        static readonly string[] Labels = { "Rest", "Part ownership", "Walk A", "Walk B",
            "Anticipation", "Contact", "Follow through", "Recovery", "Cast", "Contact mirrored" };
        [Serializable] public sealed class Result
        {
            public int artIndex, vertices;
            public string name;
            public string[] poses = Labels;
            public float attackVertexExcursion, recoveryVertexError;
        }
        [Serializable] sealed class Profiles { public TowerMythRig.Profile[] profiles; }
        [Serializable] sealed class LandmarkReport
        {
            public bool success;
            public int characters, visibleHands, soles, independentSourceProbes, excludedForeignFragments;
            public float isisPaintedCalfTravel, isisHiddenShinTravel;
            public List<string> errors = new List<string>();
        }
        public static string VerifyLandmarks(string path)
        {
            var report = new LandmarkReport();
            // Independently measured pixels on weapon tips, petals and crowns.
            int[,] edgeProbes = {
                {110,205,57,14},{114,181,233,14},{115,184,38,14},{115,214,39,14},
                {117,126,33,2},{117,173,54,14},{112,145,40,2},{112,170,40,2},
                {112,141,56,2},{112,142,61,2},
                {122,87,288,9},{122,134,284,12},
                {141,61,281,9},{141,143,281,12},{141,75,250,8},{141,147,238,11},
                {145,214,203,14},{145,216,204,14},{145,217,205,14},{145,218,205,14},{145,220,206,14},
                {141,152,258,14},{143,131,238,14},{146,243,219,14},
                {147,258,223,14},{147,270,225,14},{120,199,77,14},
                {136,206,50,14},{144,202,58,14},{149,218,7,14}
            };
            var profiles = JsonUtility.FromJson<Profiles>(Resources.Load<TextAsset>("Tower/Myths/RigData").text).profiles;
            foreach (var profile in profiles)
            {
                var root = new GameObject("Source landmark audit") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    var actor = TowerActorArt.Create(root.transform, profile.artIndex);
                    var rig = actor.GetComponentInChildren<TowerMythRig>();
                    var sprites = (Sprite[])typeof(TowerMythArt).GetMethod("Frames",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(null, new object[] { profile.artIndex });
                    int height = Mathf.RoundToInt(sprites[0].rect.height);
                    Landmark(rig, report, profile.artIndex, "left hand", profile.leftHandX, height-profile.leftHandY, 4, 3);
                    report.visibleHands++;
                    // Tyr's far arm is fully occluded in the source painting.
                    if (profile.artIndex != 114)
                    {
                        Landmark(rig, report, profile.artIndex, "right hand", profile.rightHandX, height-profile.rightHandY, 6, 3);
                        report.visibleHands++;
                    }
                    Landmark(rig, report, profile.artIndex, "left sole", profile.leftFootX, height-profile.leftFootY, 9, 4);
                    Landmark(rig, report, profile.artIndex, "right sole", profile.rightFootX, height-profile.rightFootY, 12, 4);
                    report.soles += 2; report.characters++;
                    for(int probe=0;probe<edgeProbes.GetLength(0);probe++)
                        if(edgeProbes[probe,0]==profile.artIndex)
                        {
                            Landmark(rig,report,profile.artIndex,"measured outer detail",
                                edgeProbes[probe,1],edgeProbes[probe,2],edgeProbes[probe,3],0);
                            report.independentSourceProbes++;
                        }
                    if (profile.artIndex == 105)
                    {
                        Landmark(rig, report, 105, "measured exposed left fist", 84, 174, 4, 1);
                        Landmark(rig, report, 105, "measured far shoulder", 160, 112, 5, 2);
                        Landmark(rig, report, 105, "far shoe dark sole edge", 140, 258, 12, 1);
                        Landmark(rig, report, 105, "chest cloth excluded from far arm", 154, 140, 13, 0);
                        Landmark(rig, report, 105, "exposed far forearm", 161, 141, 6, 0);
                        Landmark(rig, report, 105, "far forearm gold cuff", 170, 141, 6, 0);
                        report.independentSourceProbes += 6;
                    }
                    if (profile.artIndex == 118)
                    {
                        Landmark(rig, report, 118, "measured upper arm", 72, 155, 3, 1);
                        Landmark(rig, report, 118, "chest excluded from head", 100, 177, 1, 2);
                        report.independentSourceProbes += 2;
                    }
                    if (profile.artIndex == 122) VerifyIsisShins(actor, rig, profile, height, report);
                    if (profile.artIndex == 106)
                    {
                        ExcludedFragment(rig,report,106,224,214);
                        Landmark(rig,report,106,"preserved trident",198,58,14,1);
                        Landmark(rig,report,106,"preserved cape",60,210,13,1);
                    }
                    if (profile.artIndex == 142)
                    {
                        ExcludedFragment(rig,report,142,225,208);
                        ExcludedFragment(rig,report,142,226,245);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            report.success = report.errors.Count == 0 && report.characters == 50;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            return JsonUtility.ToJson(report, true);
        }
        static void Landmark(TowerMythRig rig, LandmarkReport report, int index, string name, float x, float y, int part, int radius)
        {
            int px = Mathf.RoundToInt(x), py = Mathf.RoundToInt(y);
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                    if (rig.SourcePixelOwner(px + dx, py + dy) == part) return;
            report.errors.Add(index + " " + name + " at (" + px + "," + py + ") is not attached to source part " + part);
        }
        static void VerifyIsisShins(TowerActorArt actor, TowerMythRig rig, TowerMythRig.Profile profile,
            int height, LandmarkReport report)
        {
            // Source ownership alone missed calves that were classified as legs
            // but weighted entirely to the body. Check the rendered vertices,
            // including the generated shin behind the far sandal.
            var rest = rig.CaptureRestVertices(); var owners = rig.CapturePartIndices();
            float unit = 1.75f / profile.bodyHeight;
            var source = new[] { new Vector2(98, 265), new Vector2(144, 270) };
            var probes = new int[2]; var travel = new float[2];
            for (int side = 0; side < 2; side++)
            {
                Vector2 point = new Vector2(source[side].x - profile.groundX,
                    height - source[side].y - profile.groundY) * unit;
                float distance = float.MaxValue; probes[side] = -1;
                for (int v = 0; v < rest.Length; v++)
                    if (owners[v] == (side == 0 ? 8 : 11) && Vector2.Distance(rest[v], point) < distance)
                    { probes[side] = v; distance = Vector2.Distance(rest[v], point); }
                if (distance > unit * 3) report.errors.Add("122 missing measured " + (side == 0 ? "painted calf" : "hidden shin"));
            }
            for (int frame = 0; frame < 120; frame++)
            {
                actor.transform.parent.position += Vector3.right * (3f / 60);
                actor.SetLocomotion(true, 0, false, 3, 3); actor.SetPose(true, false, false, 1, 1f / 60);
                var posed = rig.CaptureVertices();
                Vector2 hipShift = rig.SampleSourcePoint(rig.RestHipPosition) - rig.RestHipPosition;
                for (int side = 0; side < 2; side++)
                    if (probes[side] >= 0)
                        travel[side] = Mathf.Max(travel[side],
                            ((Vector2)(posed[probes[side]] - rest[probes[side]]) - hipShift).magnitude);
            }
            report.isisPaintedCalfTravel = travel[0]; report.isisHiddenShinTravel = travel[1];
            for (int side = 0; side < 2; side++)
                if (travel[side] < .05f) report.errors.Add("122 " + (side == 0 ? "painted calf" : "hidden shin") + " remains fixed to the body while walking");
        }
        static void ExcludedFragment(TowerMythRig rig,LandmarkReport report,int index,int x,int y)
        {
            if(rig.SourcePixelOwner(x,y)!=255)report.errors.Add(index+" retains foreign atlas fragment at "+x+","+y);
            else report.excludedForeignFragments++;
        }
        public static string Capture(int first, int count, string directory)
        {
            if (!Path.IsPathRooted(directory)) throw new ArgumentException("Use an absolute review directory.");
            Directory.CreateDirectory(directory);
            var cameraObject = new GameObject("Temporary limb audit camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.075f, .09f, .12f);
            camera.cullingMask = 1 << 31; camera.nearClipPlane = .01f; camera.farClipPlane = 30;
            var target = new RenderTexture(Width, Height, 24);
            var readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            try
            {
                for (int index = first; index < first + count; index++) CaptureCharacter(index, directory, camera, readback);
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(readback);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
            return "Captured " + count + " characters, ten actual mesh poses each.";
        }
        static void CaptureCharacter(int index, string directory, Camera camera, Texture2D readback)
        {
            var roots = new List<GameObject>();
            var actors = new List<TowerActorArt>();
            var bounds = new Bounds();
            bool boundsReady = false;
            var result = new Result { artIndex = index };
            var sheet = new Texture2D(Width * 5, Height * 2, TextureFormat.RGB24, false);
            try
            {
                for (int pose = 0; pose < Labels.Length; pose++)
                {
                    var root = new GameObject("Limb review " + index + " " + Labels[pose]) { hideFlags = HideFlags.HideAndDontSave };
                    roots.Add(root);
                    var actor = TowerActorArt.Create(root.transform, index);
                    actors.Add(actor); result.name = actor.ProfileName;
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                    Advance(actor, .35f, false, 1);
                    if (pose == 2 || pose == 3) Advance(actor, pose == 2 ? .18f : .44f, true, 1);
                    if (pose >= 4 && pose != 8)
                    {
                        actor.PlayAttack(.8f);
                        float contact = actor.AttackReleaseTime(.8f);
                        float age = pose == 4 ? contact * .55f : pose == 5 || pose == 9 ? contact :
                            pose == 6 ? contact + (.8f - contact) * .35f : 1.15f;
                        Advance(actor, age, false, pose == 9 ? -1 : 1);
                    }
                    if (pose == 8)
                    {
                        actor.PlayCast(SkillEffect.Fireball, .8f);
                        Advance(actor, actor.CastReleaseTime(SkillEffect.Fireball, .8f), false, 1);
                    }
                    var rig = actor.GetComponentInChildren<TowerMythRig>();
                    if (pose < 2 && rig != null)
                    {
                        var mesh = rig.Renderer.GetComponent<MeshFilter>().sharedMesh;
                        mesh.vertices = rig.CaptureRestVertices(); mesh.RecalculateBounds();
                        if (pose == 1)
                        {
                            int[] owners = rig.CapturePartIndices();
                            var colors = new Color32[owners.Length]; var modes = new Vector2[owners.Length];
                            for (int v = 0; v < owners.Length; v++)
                            {
                                colors[v] = Color.HSVToRGB((owners[v] * .618034f) % 1, .72f, .96f);
                                modes[v] = Vector2.right;
                            }
                            mesh.colors32 = colors; mesh.uv2 = modes;
                        }
                    }
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                    {
                        if (!renderer.enabled) continue;
                        if (!boundsReady) { bounds = renderer.bounds; boundsReady = true; }
                        else bounds.Encapsulate(renderer.bounds);
                    }
                    root.SetActive(false);
                }
                camera.orthographicSize = Mathf.Max(bounds.extents.y + .20f, (bounds.extents.x + .15f) * Height / Width);
                camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10);
                for (int pose = 0; pose < roots.Count; pose++)
                {
                    roots[pose].SetActive(true);
                    camera.Render(); RenderTexture.active = camera.targetTexture;
                    readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); readback.Apply();
                    sheet.SetPixels((pose % 5) * Width, (1 - pose / 5) * Height, Width, Height, readback.GetPixels());
                    roots[pose].SetActive(false);
                }
                var rest = Vertices(actors[0]); var contactVertices = Vertices(actors[5]); var recovered = Vertices(actors[7]);
                result.vertices = rest.Length;
                if (rest.Length != contactVertices.Length || rest.Length != recovered.Length) throw new InvalidOperationException("Pose topology changed: " + index);
                for (int v = 0; v < rest.Length; v++)
                {
                    result.attackVertexExcursion = Mathf.Max(result.attackVertexExcursion, Vector3.Distance(rest[v], contactVertices[v]));
                    result.recoveryVertexError = Mathf.Max(result.recoveryVertexError, Vector3.Distance(rest[v], recovered[v]));
                }
                if (result.attackVertexExcursion < .025f) throw new InvalidOperationException("No readable attack geometry: " + index);
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(directory, index + ".png"), sheet.EncodeToPNG());
                File.WriteAllText(Path.Combine(directory, index + ".json"), JsonUtility.ToJson(result, true));
            }
            finally
            {
                foreach (var root in roots) UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(sheet);
            }
        }
        static Vector3[] Vertices(TowerActorArt actor)
        {
            var result = new List<Vector3>();
            foreach (var filter in actor.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    foreach (var vertex in filter.sharedMesh.vertices) result.Add(filter.transform.TransformPoint(vertex));
            return result.ToArray();
        }
        static void Advance(TowerActorArt actor, float duration, bool moving, int facing)
        {
            const float dt = 1f / 60;
            for (float age = 0; age < duration;)
            {
                float step = Mathf.Min(dt, duration - age); age += step;
                actor.SetLocomotion(true, 0, false, moving ? 3 : 0, moving ? 3 * facing : 0);
                actor.SetPose(moving, false, false, facing, step);
            }
        }
    }
}

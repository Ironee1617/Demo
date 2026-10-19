using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    // Octopus alien: re-rig + a complete new set of hand-keyed clips (Disney 12 principles).
    //
    // Rig    : the imported surface was skinned 100% to Body (arms moved only by blend shapes).
    //          Rerig() adds a fitted 5-joint chain per arm and a Neck/Crown/Antenna head chain, then
    //          welds coincident positions and diffuses the weights over the welded surface, so head,
    //          mouth collar and arms deform as one skin (no cracks between shells).
    // Clips  : Octo_* assets fill every alien slot (Idle, Swim, Bite, TentacleEat, Eaten, Harpoon,
    //          Morph, Unmorph, SpitRecoil, SpitOut). Generated Alien_* clips are left as they are.
    // Morph  : AquariumMorphMotion clips (alien + fish) layer keyed motion over the shape morph.
    // Drag   : AlienArmDrag bends the arms against the real travel direction at runtime.
    public static class AlienOctopusAnimation
    {
        const string Folder = "Assets/Demos/HideAndSeek/Characters/Resources/AquariumCharacters";
        const string PrefabPath = Folder + "/Alien.prefab", FishPath = Folder + "/Fish0.prefab";
        const string OutFolder = Folder + "/AlienOctopus";
        const float Frame = 1f / 30f;
        const int Joints = 5;
        static readonly string[] HeadBones = { "Neck", "Crown", "Antenna" };
        static readonly float[] HeadPivotY = { -.20f, .20f, .58f };

        // ================================================================ RIG
        static bool IsArmVertex(Vector2 uv) => (int)(uv.x * 4) + 4 * (int)(uv.y * 4) == 15;

        static void Rerig(AquariumRig rig)
        {
            var skin = rig.skin; var oldBones = skin.bones;
            if (oldBones.Any(b => b.name == "Arm0_0")) return;
            var mesh = skin.sharedMesh; var verts = mesh.vertices; var uv = mesh.uv; var tris = mesh.triangles;
            var body = oldBones.First(b => b.name == "Body"); int bodyIndex = System.Array.IndexOf(oldBones, body);
            var space = rig.transform; var newBones = new List<Transform>();

            var head = new Transform[3]; Transform parent = body;
            for (int h = 0; h < 3; h++)
            {
                head[h] = new GameObject(HeadBones[h]).transform; head[h].SetParent(parent, false);
                head[h].position = space.TransformPoint(new Vector3(0, HeadPivotY[h], 0)); head[h].localRotation = Quaternion.identity;
                newBones.Add(head[h]); parent = head[h];
            }

            var roots = new Vector3[6]; var angles = new float[6];
            for (int i = 0; i < 6; i++)
            {
                roots[i] = space.InverseTransformPoint(oldBones.First(b => b.name == "Tentacle" + i + "_0").position);
                angles[i] = Mathf.Atan2(roots[i].x, roots[i].z) * Mathf.Rad2Deg;
            }
            var owner = new int[verts.Length]; var members = Enumerable.Range(0, 6).Select(_ => new List<int>()).ToArray();
            for (int v = 0; v < verts.Length; v++)
            {
                owner[v] = -1; if (!IsArmVertex(uv[v])) continue;
                float a = Mathf.Atan2(verts[v].x, verts[v].z) * Mathf.Rad2Deg; int best = 0; float bestDelta = 999;
                for (int i = 0; i < 6; i++) { float d = Mathf.Abs(Mathf.DeltaAngle(a, angles[i])); if (d < bestDelta) { bestDelta = d; best = i; } }
                owner[v] = best; members[best].Add(v);
            }
            // Suckers live in other UV islands: hand them to the arm they sit on.
            var armVerts = members.SelectMany((m, i) => m.Select(v => (v, i))).ToArray();
            for (int v = 0; v < verts.Length; v++)
            {
                if (owner[v] >= 0 || verts[v].y > -.10f) continue;
                float best = .045f * .045f; int arm = -1;
                foreach (var (a, i) in armVerts) { float d = (verts[a] - verts[v]).sqrMagnitude; if (d < best) { best = d; arm = i; } }
                if (arm >= 0) owner[v] = arm;
            }
            var lines = new List<Vector3>[6]; var lengths = new float[6]; var armBones = new Transform[6, Joints];
            for (int i = 0; i < 6; i++)
            {
                float maxD = members[i].Max(v => Vector3.Distance(verts[v], roots[i]));
                const int bins = 24; var sum = new Vector3[bins]; var count = new int[bins];
                foreach (int v in members[i]) { int b = Mathf.Min(bins - 1, (int)(Vector3.Distance(verts[v], roots[i]) / maxD * bins)); sum[b] += verts[v]; count[b]++; }
                var line = new List<Vector3> { roots[i] };
                for (int b = 0; b < bins; b++) if (count[b] > 0) { var c = sum[b] / count[b]; if (Vector3.Distance(c, line[line.Count - 1]) > .01f) line.Add(c); }
                lines[i] = line; float length = 0; for (int k = 1; k < line.Count; k++) length += Vector3.Distance(line[k - 1], line[k]); lengths[i] = length;
                parent = body;
                for (int k = 0; k < Joints; k++)
                {
                    var t = new GameObject("Arm" + i + "_" + k).transform; t.SetParent(parent, false);
                    t.position = space.TransformPoint(PointAt(line, length * k / Joints)); t.localRotation = Quaternion.identity;
                    armBones[i, k] = t; newBones.Add(t); parent = t;
                }
            }

            var bones = oldBones.Concat(newBones).ToArray(); int Index(Transform t) => System.Array.IndexOf(bones, t);
            // ---- initial weights per vertex
            var initial = new Dictionary<int, float>[verts.Length];
            for (int v = 0; v < verts.Length; v++)
            {
                var w = new Dictionary<int, float>();
                if (owner[v] < 0)
                {
                    float y = verts[v].y;
                    float neck = Smooth(-.34f, -.08f, y), crown = Smooth(.04f, .36f, y), antenna = Smooth(.56f, .70f, y);
                    Add(w, bodyIndex, 1 - neck); Add(w, Index(head[0]), neck * (1 - crown)); Add(w, Index(head[1]), crown * (1 - antenna)); Add(w, Index(head[2]), antenna);
                }
                else
                {
                    int i = owner[v]; float u = Project(lines[i], verts[v]); float seg = lengths[i] / Joints;
                    int s = Mathf.Clamp(Mathf.FloorToInt(u / seg), 0, Joints - 1); float f = Mathf.Clamp(u / seg - s, 0, 1.5f);
                    if (f < .5f) { Add(w, Index(armBones[i, s]), .5f + f); Add(w, s == 0 ? bodyIndex : Index(armBones[i, s - 1]), .5f - f); }
                    else if (s < Joints - 1) { Add(w, Index(armBones[i, s]), 1.5f - f); Add(w, Index(armBones[i, s + 1]), f - .5f); }
                    else Add(w, Index(armBones[i, s]), 1);
                }
                initial[v] = w;
            }
            // ---- one skin: weld coincident positions, then diffuse weights over the welded surface
            var nodeOf = new int[verts.Length]; var nodes = new Dictionary<Vector3Int, int>();
            for (int v = 0; v < verts.Length; v++)
            {
                var key = new Vector3Int(Mathf.RoundToInt(verts[v].x * 20000), Mathf.RoundToInt(verts[v].y * 20000), Mathf.RoundToInt(verts[v].z * 20000));
                if (!nodes.TryGetValue(key, out int n)) nodes[key] = n = nodes.Count; nodeOf[v] = n;
            }
            int N = nodes.Count; var neighbours = new HashSet<int>[N]; for (int n = 0; n < N; n++) neighbours[n] = new HashSet<int>();
            for (int t = 0; t < tris.Length; t += 3)
                for (int e = 0; e < 3; e++) { int a = nodeOf[tris[t + e]], b = nodeOf[tris[t + (e + 1) % 3]]; if (a != b) { neighbours[a].Add(b); neighbours[b].Add(a); } }
            var field = new Dictionary<int, float>[N]; for (int n = 0; n < N; n++) field[n] = new Dictionary<int, float>();
            var members2 = new int[N]; for (int v = 0; v < verts.Length; v++) { members2[nodeOf[v]]++; foreach (var p in initial[v]) Add(field[nodeOf[v]], p.Key, p.Value); }
            for (int n = 0; n < N; n++) Normalize(field[n]);
            for (int iteration = 0; iteration < 8; iteration++)
            {
                var next = new Dictionary<int, float>[N];
                for (int n = 0; n < N; n++)
                {
                    var w = new Dictionary<int, float>(); foreach (var p in field[n]) Add(w, p.Key, .5f * p.Value);
                    if (neighbours[n].Count > 0) { float share = .5f / neighbours[n].Count; foreach (int m in neighbours[n]) foreach (var p in field[m]) Add(w, p.Key, share * p.Value); }
                    else foreach (var p in field[n]) Add(w, p.Key, .5f * p.Value);
                    Prune(w); Normalize(w); next[n] = w;
                }
                field = next;
            }
            var weights = new BoneWeight[verts.Length]; for (int v = 0; v < verts.Length; v++) weights[v] = ToBoneWeight(field[nodeOf[v]]);
            // ---- seam normals: coincident vertices with similar normals share one normal
            var normals = mesh.normals; var sumNormal = new Vector3[N];
            for (int v = 0; v < verts.Length; v++) sumNormal[nodeOf[v]] += normals[v];
            for (int v = 0; v < verts.Length; v++) { var avg = sumNormal[nodeOf[v]].normalized; if (Vector3.Dot(avg, normals[v]) > .7f) normals[v] = avg; }
            mesh.normals = normals;

            mesh.bindposes = mesh.bindposes.Concat(newBones.Select(b => b.worldToLocalMatrix * space.localToWorldMatrix)).ToArray();
            mesh.boneWeights = weights; EditorUtility.SetDirty(mesh);
            skin.bones = bones;
            rig.restPositions = rig.restPositions.Concat(newBones.Select(b => b.localPosition)).ToArray();
            rig.restRotations = rig.restRotations.Concat(newBones.Select(b => b.localRotation)).ToArray();
            rig.restScales = rig.restScales.Concat(newBones.Select(b => b.localScale)).ToArray();
        }
        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));
        static void Add(Dictionary<int, float> w, int bone, float value) { if (value <= 1e-5f) return; w[bone] = (w.TryGetValue(bone, out var old) ? old : 0) + value; }
        static void Normalize(Dictionary<int, float> w) { float t = w.Values.Sum(); if (t <= 0) return; foreach (var k in w.Keys.ToArray()) w[k] /= t; }
        static void Prune(Dictionary<int, float> w) { foreach (var k in w.Where(p => p.Value < .004f).Select(p => p.Key).ToArray()) w.Remove(k); }
        static BoneWeight ToBoneWeight(Dictionary<int, float> w)
        {
            var top = w.OrderByDescending(p => p.Value).Take(4).ToArray(); float total = top.Sum(p => p.Value); var r = new BoneWeight();
            if (top.Length > 0) { r.boneIndex0 = top[0].Key; r.weight0 = top[0].Value / total; }
            if (top.Length > 1) { r.boneIndex1 = top[1].Key; r.weight1 = top[1].Value / total; }
            if (top.Length > 2) { r.boneIndex2 = top[2].Key; r.weight2 = top[2].Value / total; }
            if (top.Length > 3) { r.boneIndex3 = top[3].Key; r.weight3 = top[3].Value / total; }
            return r;
        }
        static Vector3 PointAt(List<Vector3> line, float u)
        {
            for (int k = 1; k < line.Count; k++) { float l = Vector3.Distance(line[k - 1], line[k]); if (u <= l) return Vector3.Lerp(line[k - 1], line[k], u / l); u -= l; }
            return line[line.Count - 1];
        }
        static float Project(List<Vector3> line, Vector3 p)
        {
            float best = float.MaxValue, at = 0, walked = 0;
            for (int k = 1; k < line.Count; k++)
            {
                var a = line[k - 1]; var ab = line[k] - a; float l = ab.magnitude; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / (l * l));
                float d = (a + ab * t - p).sqrMagnitude; if (d < best) { best = d; at = walked + t * l; }
                walked += l;
            }
            return at;
        }

        // ================================================================ RECORDING
        sealed class Recorder
        {
            readonly Dictionary<(string, System.Type, string), AnimationCurve> curves = new Dictionary<(string, System.Type, string), AnimationCurve>();
            readonly Dictionary<string, Quaternion> lastRotation = new Dictionary<string, Quaternion>();
            public void Key(string path, System.Type type, string prop, float time, float value)
            {
                time = Mathf.Round(time * 30f) / 30f; var id = (path, type, prop);
                if (!curves.TryGetValue(id, out var curve)) curves[id] = curve = new AnimationCurve();
                for (int i = 0; i < curve.length; i++) if (Mathf.Abs(curve[i].time - time) < 1e-4f) { curve.MoveKey(i, new Keyframe(time, value)); return; }
                curve.AddKey(new Keyframe(time, value));
            }
            public void Shape(string name, float time, float value) => Key("Deforming surface", typeof(SkinnedMeshRenderer), "blendShape." + name, time, value);
            public void Transform(string path, Transform t, float time)
            {
                var p = t.localPosition; var s = t.localScale; var q = t.localRotation;
                if (lastRotation.TryGetValue(path, out var prev) && Quaternion.Dot(prev, q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                lastRotation[path] = q; var T = typeof(Transform);
                Key(path, T, "m_LocalPosition.x", time, p.x); Key(path, T, "m_LocalPosition.y", time, p.y); Key(path, T, "m_LocalPosition.z", time, p.z);
                Key(path, T, "m_LocalRotation.x", time, q.x); Key(path, T, "m_LocalRotation.y", time, q.y); Key(path, T, "m_LocalRotation.z", time, q.z); Key(path, T, "m_LocalRotation.w", time, q.w);
                Key(path, T, "m_LocalScale.x", time, s.x); Key(path, T, "m_LocalScale.y", time, s.y); Key(path, T, "m_LocalScale.z", time, s.z);
            }
            public AnimationClip Build(string name, bool loop)
            {
                var clip = new AnimationClip { name = name, legacy = true, frameRate = 30, wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever };
                foreach (var pair in curves)
                {
                    var curve = pair.Value;
                    for (int i = 0; i < curve.length; i++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                        AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    }
                    if (loop && curve.length > 1)
                    {
                        var a = curve[0]; var b = curve[curve.length - 1]; float slope = (a.outTangent + b.inTangent) * .5f;
                        a.inTangent = a.outTangent = slope; b.inTangent = b.outTangent = slope; curve.MoveKey(0, a); curve.MoveKey(curve.length - 1, b);
                    }
                    clip.SetCurve(pair.Key.Item1, pair.Key.Item2, pair.Key.Item3, curve);
                }
                clip.EnsureQuaternionContinuity(); return clip;
            }
        }

        // ================================================================ POSER
        sealed class Poser
        {
            public GameObject go; public AquariumRig rig; public Transform body; public Transform[] head = new Transform[3];
            public Transform[,] arm = new Transform[6, Joints];
            public Vector3[] dir = new Vector3[6], radial = new Vector3[6], bend = new Vector3[6]; public float[] side = new float[6];
            public Mesh mesh => rig.skin.sharedMesh;
            public float End = 3f;
            public Poser()
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                rig = go.GetComponent<AquariumRig>(); body = rig.skin.bones.First(b => b.name == "Body");
                for (int h = 0; h < 3; h++) head[h] = rig.skin.bones.First(b => b.name == HeadBones[h]);
                for (int i = 0; i < 6; i++)
                {
                    for (int k = 0; k < Joints; k++) arm[i, k] = rig.skin.bones.First(b => b.name == "Arm" + i + "_" + k);
                    var root = arm[i, 0].localPosition; var tip = arm[i, Joints - 1].position - arm[i, 0].position;
                    dir[i] = body.InverseTransformDirection(tip).normalized; radial[i] = new Vector3(root.x, 0, root.z).normalized; side[i] = Mathf.Sign(root.x);
                    bend[i] = Vector3.Cross(dir[i], Vector3.down).normalized; // + curls under/inward, - flicks up/out
                }
            }
            public string Path(Transform t) => AnimationUtility.CalculateTransformPath(t, go.transform);
            public void Reset() { var b = rig.skin.bones; for (int i = 0; i < b.Length; i++) { b[i].localPosition = rig.restPositions[i]; b[i].localRotation = rig.restRotations[i]; b[i].localScale = rig.restScales[i]; } }
            public void Body(Vector3 offset, Vector3 euler, Vector3 scale) { body.localPosition = rig.restPositions[System.Array.IndexOf(rig.skin.bones, body)] + offset; body.localRotation = Quaternion.Euler(euler); body.localScale = scale; }
            // Head follows the body a beat late; neck, crown and antenna share the lean (overlapping action).
            public void Head(Vector3 lean, Vector3 crownScale)
            {
                head[0].localRotation = Quaternion.Euler(lean * .35f); head[1].localRotation = Quaternion.Euler(lean * .45f); head[2].localRotation = Quaternion.Euler(lean * .9f);
                head[1].localScale = crownScale;
            }
            static readonly float[] Share = { 0, .15f, .22f, .28f, .35f };
            // Swing the whole arm to aim along `aim`, then spread `curl` degrees down the chain (tips curl most).
            public void Arm(int i, Vector3 aim, float curl, float wave = 0, float phase = 0)
            {
                arm[i, 0].localRotation = Quaternion.FromToRotation(dir[i], aim.normalized);
                for (int k = 1; k < Joints; k++) arm[i, k].localRotation = Quaternion.AngleAxis(curl * Share[k] + wave * Mathf.Sin(phase - k * .75f) * (.6f + .2f * k), bend[i]);
            }
            public void Swing(int i, Vector3 from, Vector3 to)
            {
                arm[i, 0].localRotation = Quaternion.FromToRotation(from.normalized, to.normalized);
                for (int k = 1; k < Joints; k++) arm[i, k].localRotation = Quaternion.identity;
            }
            public void Record(Recorder rec, float time, System.Func<int, int, float> armLag, float headLag)
            {
                rec.Transform(Path(rig.skin.bones[0]), rig.skin.bones[0], time);
                rec.Transform(Path(body), body, time);
                for (int h = 0; h < 3; h++) rec.Transform(Path(head[h]), head[h], Mathf.Clamp(time + headLag * (h + 1), 0, End));
                for (int i = 0; i < 6; i++) for (int k = 0; k < Joints; k++) rec.Transform(Path(arm[i, k]), arm[i, k], Mathf.Clamp(time + armLag(i, k), 0, End));
            }
            public void ZeroShapes(Recorder rec, float time, System.Func<string, float> value = null)
            {
                for (int s = 0; s < mesh.blendShapeCount; s++) { string n = mesh.GetBlendShapeName(s); rec.Shape(n, time, value == null ? 0 : value(n)); }
            }
        }
        static int Group(Poser p, int i) { var r = p.radial[i]; return r.z > .3f ? 0 : r.z > -.3f ? 1 : 2; }
        static readonly Vector3 Up = Vector3.up, Down = Vector3.down, Fwd = Vector3.forward, Back = Vector3.back;

        struct Key
        {
            public float t, curl, wave; public System.Func<Vector3, Vector3, int, Vector3> aim;
            public Vector3 pos, rot, scale, lean, crown;
            public Key(float t, System.Func<Vector3, Vector3, int, Vector3> aim, float curl, Vector3 pos, Vector3 rot, Vector3 scale, Vector3 lean, Vector3 crown, float wave = 0)
            { this.t = t; this.aim = aim; this.curl = curl; this.pos = pos; this.rot = rot; this.scale = scale; this.lean = lean; this.crown = crown; this.wave = wave; }
        }
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static readonly Vector3 One = Vector3.one, Zero = Vector3.zero;

        // Pose-to-pose: every key poses body, head and arms; arms trail by group + joint, head by link.
        static void Keys(Poser p, Recorder rec, Key[] keys, int armLagFrames = 1, int headLagFrames = 2)
        {
            foreach (var k in keys)
            {
                p.Reset(); p.Body(k.pos, k.rot, k.scale); p.Head(k.lean, k.crown);
                for (int i = 0; i < 6; i++) p.Arm(i, k.aim(p.radial[i], p.dir[i], i), k.curl, k.wave, k.t * 9 + i * 1.3f);
                bool edge = k.t <= 0 || k.t >= p.End - 1e-4f;
                p.Record(rec, k.t, (i, j) => edge ? 0 : (Group(p, i) + j) * armLagFrames * Frame, edge ? 0 : headLagFrames * Frame);
            }
        }
        static void Blink(Recorder rec, Poser p, (float t, float v)[] eye, (float t, float v)[] belly = null)
        {
            var times = eye.Select(e => e.t).Concat(belly == null ? new float[0] : belly.Select(b => b.t)).Distinct().OrderBy(x => x).ToArray();
            foreach (float t in times)
            {
                float e = Sample(eye, t), b = belly == null ? 0 : Sample(belly, t);
                p.ZeroShapes(rec, t, n => n == "BellyOpen" ? b : n.StartsWith("Blink") ? BlinkWeight(n, e) : 0);
            }
        }
        static float Sample((float t, float v)[] keys, float t)
        {
            for (int i = 0; i < keys.Length - 1; i++)
                if (t >= keys[i].t && t <= keys[i + 1].t) return Mathf.Lerp(keys[i].v, keys[i + 1].v, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(keys[i].t, keys[i + 1].t, t)));
            return keys[keys.Length - 1].v;
        }
        static float BlinkWeight(string name, float v)
        {
            for (int i = 1; i <= 12; i++)
            {
                string key = i % 3 == 0 ? "Blink_" + (i * 100 / 12) : "BlinkStep_" + i.ToString("00");
                if (key == name) return 100 * (i == 12 ? Mathf.Clamp01(v * 12 - 11) : Mathf.Max(0, 1 - Mathf.Abs(v * 12 - i)));
            }
            return 0;
        }

        // ================================================================ IDLE (2.4 s loop)
        // Breathing mantle, a slow travelling ripple down every arm (staggered phase per arm),
        // head drifting a beat behind the breath, one blink.
        static AnimationClip BuildIdle(Poser p)
        {
            var rec = new Recorder(); p.End = 2.4f;
            for (int f = 0; f <= 24; f++)
            {
                float t = f * .1f, w = t / 2.4f * Mathf.PI * 2;
                float breath = Mathf.Sin(w);
                p.Reset();
                p.Body(V(0, .012f * breath, 0), V(1.2f * Mathf.Sin(w + .6f), 0, 1.0f * Mathf.Sin(w * 1f + 2f)), V(1 - .012f * breath, 1 + .02f * breath, 1 - .012f * breath));
                p.Head(V(2.5f * Mathf.Sin(w - .9f), 0, 1.5f * Mathf.Sin(w + 1.2f)), V(1 + .015f * Mathf.Sin(w - .5f), 1 - .02f * Mathf.Sin(w - .5f), 1));
                for (int i = 0; i < 6; i++)
                {
                    float ph = w + i * 1.05f;
                    var aim = p.dir[i] + Up * (.10f * Mathf.Sin(ph)) + p.radial[i] * (.05f * Mathf.Sin(ph + 1.2f));
                    p.arm[i, 0].localRotation = Quaternion.FromToRotation(p.dir[i], aim.normalized);
                    for (int k = 1; k < Joints; k++) p.arm[i, k].localRotation = Quaternion.AngleAxis(6f + 9f * Mathf.Sin(ph - k * .8f) * (.5f + .25f * k), p.bend[i]);
                }
                p.Record(rec, t, (i, j) => 0, 0);
            }
            Blink(rec, p, new[] { (0f, 0f), (1.55f, 0f), (1.65f, 1f), (1.80f, 0f), (2.4f, 0f) });
            return rec.Build("Octo_Idle", true);
        }

        // ================================================================ SWIM (2 jet strokes / 2.4 s)
        static readonly float[] SwimKeys = { 0f, .10f, .20f, .55f, .90f };
        static readonly float[] SwimOpen = { 1f, 0f, 0f, 0f, .55f };
        static readonly float[] SwimSqueeze = { 0f, 1f, .3f, 0f, 0f };
        static readonly Vector3[] SwimScale = { V(1.026f, 1.026f, .95f), V(.953f, .953f, 1.10f), V(.98f, .98f, 1.04f), One, V(1.012f, 1.012f, .976f) };
        static readonly Vector3[] SwimOffset = { V(0, -.012f, -.01f), V(0, .004f, .03f), V(0, .002f, .02f), V(0, 0, .005f), V(0, -.008f, -.004f) };
        static readonly float[] SwimPitch = { .5f, -6f, -4f, -2f, -.5f };
        static readonly float[] HeadSwimPitch = { 6f, -12f, 7f, 1f, 4f };
        static readonly Vector3[] CrownSwimScale = { V(1.04f, .97f, 1f), V(.96f, 1.06f, .98f), V(1.01f, .99f, 1f), One, V(1.03f, .98f, 1f) };
        static AnimationClip BuildSwim(Poser p)
        {
            var rec = new Recorder(); p.End = 2.4f;
            for (int stroke = 0; stroke < 2; stroke++)
                for (int k = 0; k < SwimKeys.Length; k++)
                {
                    float time = stroke * 1.2f + SwimKeys[k], sway = stroke == 0 ? 1 : -1;
                    p.Reset();
                    p.Body(SwimOffset[k], V(SwimPitch[k], k == 3 ? sway : 0, k == 3 ? 1.6f * sway : 0), SwimScale[k]);
                    p.Head(V(HeadSwimPitch[k], k == 3 ? -1.5f * sway : 0, k == 3 ? -3f * sway : 0), CrownSwimScale[k]);
                    for (int i = 0; i < 6; i++)
                    {
                        float lane = (i % 3 == 0 ? .38f : i % 3 == 1 ? .60f : .23f) * p.side[i], o = SwimOpen[k];
                        var trailing = V(lane, -.16f, -1); var tight = V(lane * (1 - .45f * SwimSqueeze[k]), -.16f, -1);
                        p.Swing(i, trailing, o > 0 ? V(lane * (1 + 1.3f * o), -.16f - .22f * o, -1) : tight);
                    }
                    bool seam = stroke == 0 && k == 0;
                    p.Record(rec, time, (i, j) => seam ? 0 : Group(p, i) * Frame, seam ? 0 : 2 * Frame);
                    if (seam) p.Record(rec, 2.4f, (i, j) => 0, 0);
                }
            for (int f = 0; f <= 72; f++)
            {
                float t = f * Frame, s = Mathf.Repeat(t, 1.2f), stroke = Mathf.Floor(t / 1.2f);
                float local = s < .2f ? 5 * Mathf.SmoothStep(0, 1, s / .2f) : 5 + 3 * (1 - Mathf.Pow(1 - (s - .2f), 2));
                float phase = Mathf.Repeat(stroke * 8 + local, 16);
                for (int n = 0; n < 16; n++) { float d = Mathf.Repeat(phase - n + 8, 16) - 8; rec.Shape("StreamWave_" + n.ToString("00"), t, 100 * Mathf.Max(0, 1 - Mathf.Abs(d))); }
                rec.Shape("Streamline_1000", t, 100);
            }
            return rec.Build("Octo_Swim", true);
        }

        // ================================================================ EAT (3 s, belly mouth contract kept)
        static AnimationClip BuildEat(Poser p, string name)
        {
            var rec = new Recorder(); p.End = 3f;
            Keys(p, rec, new[]
            {
                new Key(0.00f, (r,d,i) => d,                                0,   Zero,               Zero,            One,                  Zero,           One),
                new Key(0.22f, (r,d,i) => r*.8f + Up*.45f + Back*.2f,     -30,   V(0,.03f,-.02f),    V(8,0,0),        V(1.07f,.90f,1.04f),  V(-12,0,0),     V(1.03f,.95f,1.02f)),
                new Key(0.50f, (r,d,i) => r*.5f + Fwd*.85f + Down*.45f,   -12,   V(0,-.01f,.06f),    V(-14,0,0),      V(.96f,.96f,1.085f),  V(-6,0,0),      V(.97f,1.05f,.99f)),
                new Key(0.72f, (r,d,i) => r*.55f + Fwd*.65f + Down*.5f,    95,   V(0,-.015f,.04f),   V(-10,0,0),      V(1f,1.02f,.98f),     V(12,0,0),      V(1.02f,.97f,1.01f)),
                new Key(1.10f, (r,d,i) => r*.62f + Fwd*.15f + Down*.72f,  120,   V(0,0,.01f),        V(-6,0,0),       V(1.02f,.98f,1f),     V(6,0,0),       One),
                new Key(1.35f, (r,d,i) => r*.52f + Fwd*.12f + Down*.8f,   145,   V(0,-.01f,.01f),    V(-5,0,1.5f),    V(1.04f,.92f,1.045f), V(9,0,-4),      V(1.05f,.92f,1.04f)),
                new Key(1.55f, (r,d,i) => r*.62f + Fwd*.15f + Down*.72f,  120,   V(0,0,.01f),        V(-6,0,0),       One,                  V(3,0,2),       V(.98f,1.03f,.99f)),
                new Key(1.75f, (r,d,i) => r*.52f + Fwd*.12f + Down*.8f,   145,   V(0,-.01f,.01f),    V(-5,0,-1.5f),   V(1.035f,.93f,1.04f), V(9,0,4),       V(1.05f,.92f,1.04f)),
                new Key(1.95f, (r,d,i) => r*.65f + Fwd*.12f + Down*.7f,   110,   V(0,0,.005f),       V(-4,0,0),       One,                  V(2,0,0),       One),
                new Key(2.40f, (r,d,i) => r + Up*.15f,                    -45,   V(0,.01f,-.01f),    V(3,0,0),        V(.985f,1.03f,.985f), V(-8,0,0),      V(.98f,1.04f,.99f)),
                new Key(2.70f, (r,d,i) => d + Down*.1f,                    12,   Zero,               V(-1,0,0),       V(1.005f,.99f,1.005f),V(3,0,0),       V(1.01f,.99f,1f)),
                new Key(3.00f, (r,d,i) => d,                                0,   Zero,               Zero,            One,                  Zero,           One),
            });
            Blink(rec, p, new[] { (0f, 0f), (.40f, 0f), (.55f, .38f), (1.10f, .38f), (1.32f, 0f), (1.70f, 0f), (1.78f, 1f), (1.90f, 0f), (3f, 0f) },
                          new[] { (0f, 0f), (.45f, 0f), (.90f, 100f), (1.98f, 100f), (2.65f, 0f), (3f, 0f) });
            return rec.Build(name, false);
        }

        // ================================================================ EATEN (3 s, victim alien)
        // Startle -> flailing struggle (arms out of phase) -> arms fold back as it is drawn in head first -> limp.
        static AnimationClip BuildEaten(Poser p)
        {
            var rec = new Recorder(); p.End = 3f;
            System.Func<Vector3, Vector3, int, Vector3> flailA = (r, d, i) => r * .8f + (i % 2 == 0 ? Up * .5f : Down * .3f);
            System.Func<Vector3, Vector3, int, Vector3> flailB = (r, d, i) => r * .8f + (i % 2 == 0 ? Down * .3f : Up * .5f);
            Keys(p, rec, new[]
            {
                new Key(0.00f, (r,d,i) => d,                     0,  Zero,             Zero,              One,                  Zero,          One),
                new Key(0.15f, (r,d,i) => r + Up*.35f,         -45,  V(0,.03f,0),      V(-6,0,0),         V(.95f,1.09f,.96f),   V(-10,0,0),    V(.96f,1.07f,.97f)),
                new Key(0.35f, flailA,                          30,  V(0,.02f,0),      V(4,6,-8),         V(1.04f,.95f,1.01f),  V(6,-6,8),     V(1.03f,.97f,1f), 25),
                new Key(0.55f, flailB,                         -25,  V(0,.02f,0),      V(-3,-6,8),        V(.97f,1.04f,.99f),   V(-5,6,-8),    V(.97f,1.04f,1f), 25),
                new Key(0.75f, flailA,                          35,  V(0,.01f,0),      V(5,4,-6),         V(1.03f,.96f,1.01f),  V(7,-4,6),     V(1.03f,.97f,1f), 20),
                new Key(1.00f, (r,d,i) => r*.35f + Up*.75f + Back*.3f, 60, V(0,0,0),   V(-4,0,0),         V(.96f,1.06f,.98f),   V(-6,0,0),     V(.97f,1.04f,.99f)),
                new Key(1.40f, (r,d,i) => r*.2f + Up*.85f + Back*.35f, 40, Zero,       V(-2,0,0),         V(.94f,1.08f,.98f),   V(-3,0,0),     V(.97f,1.04f,.99f)),
                new Key(2.10f, (r,d,i) => r*.2f + Up*.85f + Back*.35f, 25, Zero,       Zero,              V(.95f,1.07f,.98f),   Zero,          One),
                new Key(3.00f, (r,d,i) => r*.2f + Up*.85f + Back*.35f, 20, Zero,       Zero,              V(.95f,1.07f,.98f),   Zero,          One),
            });
            Blink(rec, p, new[] { (0f, 0f), (.15f, 0f), (.6f, .3f), (1.1f, .92f), (3f, .92f) });
            return rec.Build("Octo_Eaten", false);
        }

        // ================================================================ HARPOON (3 s, death)
        // Impact jolt -> flail with decaying overshoot -> slow roll belly-up, arms go limp, eye closes.
        static AnimationClip BuildHarpoon(Poser p)
        {
            var rec = new Recorder(); p.End = 3f;
            Keys(p, rec, new[]
            {
                new Key(0.00f, (r,d,i) => d,                   0,   Zero,               Zero,             One,                  Zero,          One),
                new Key(0.08f, (r,d,i) => r + Up*.3f,        -55,   V(0,.02f,-.03f),    V(-10,0,15),      V(1.10f,.88f,1.03f),  V(-14,0,-10),  V(1.06f,.92f,1.02f)),
                new Key(0.30f, (r,d,i) => r*.7f + Down*.6f,   80,   V(0,0,-.02f),       V(6,0,25),        V(.95f,1.05f,1f),     V(10,0,12),    V(.97f,1.04f,1f), 20),
                new Key(0.55f, (r,d,i) => r + Up*.1f,        -20,   V(0,0,-.01f),       V(-2,0,45),       V(1.02f,.98f,1f),     V(-5,0,-6),    One, 12),
                new Key(0.90f, (r,d,i) => r*.85f + Down*.35f, 40,   Zero,               V(0,0,90),        One,                  V(3,0,4),      One, 6),
                new Key(1.50f, (r,d,i) => r*.9f + Down*.2f,   22,   Zero,               V(0,0,150),       V(1.01f,.99f,1f),     V(1,0,0),      One),
                new Key(2.10f, (r,d,i) => r*.95f + Down*.12f, 18,   Zero,               V(0,0,180),       One,                  Zero,          One),
                new Key(3.00f, (r,d,i) => r*.95f + Down*.12f, 18,   Zero,               V(0,0,180),       One,                  Zero,          One),
            });
            Blink(rec, p, new[] { (0f, 0f), (.08f, .5f), (.3f, .2f), (1.2f, 1f), (3f, 1f) });
            return rec.Build("Octo_Harpoon", false);
        }

        // ================================================================ SPIT RECOIL (1.2 s, carrier spits the meal out)
        // Inflate (anticipation) -> mouth turned forward, mantle squeezes, arms fan back from the jet -> head whip -> settle.
        static AnimationClip BuildSpitRecoil(Poser p)
        {
            var rec = new Recorder(); p.End = 1.2f;
            Keys(p, rec, new[]
            {
                new Key(0.00f, (r,d,i) => d,                           0,  Zero,             Zero,          One,                   Zero,          One),
                new Key(0.05f, (r,d,i) => d + Down*.2f,               40,  V(0,-.01f,0),     V(-12,0,0),    V(1.08f,.92f,1.06f),   V(8,0,0),      V(1.05f,.94f,1.03f)),
                new Key(0.14f, (r,d,i) => r*.6f + Back*.7f + Up*.25f, -35, V(0,0,-.04f),     V(-55,0,0),    V(.93f,1.06f,1.04f),   V(-14,0,0),    V(.95f,1.07f,.98f)),
                new Key(0.32f, (r,d,i) => r*.7f + Back*.55f + Up*.1f, -10, V(0,0,-.02f),     V(-48,0,0),    V(.98f,1.02f,1f),      V(8,0,0),      V(1.02f,.98f,1f)),
                new Key(0.60f, (r,d,i) => r*.85f + Back*.2f,           18, V(0,0,-.005f),    V(-20,0,0),    V(1.01f,.99f,1f),      V(-3,0,0),     One),
                new Key(0.90f, (r,d,i) => d,                           -6, Zero,             V(-4,0,0),     One,                   V(1,0,0),      One),
                new Key(1.20f, (r,d,i) => d,                            0, Zero,             Zero,          One,                   Zero,          One),
            });
            Blink(rec, p, new[] { (0f, 0f), (.05f, .7f), (.20f, .7f), (.40f, 0f), (1.2f, 0f) },
                          new[] { (0f, 0f), (.04f, 0f), (.10f, 100f), (.30f, 100f), (.70f, 0f), (1.2f, 0f) });
            return rec.Build("Octo_SpitRecoil", false);
        }

        // ================================================================ SPIT OUT (0.85 s, expelled victim)
        // Tucked ball -> arms burst open past rest (follow through) -> swept back by the jet -> regains swim shape.
        static AnimationClip BuildSpitOut(Poser p)
        {
            var rec = new Recorder(); p.End = .85f;
            Keys(p, rec, new[]
            {
                new Key(0.00f, (r,d,i) => r*.3f + Down*.6f + Back*.4f, 150, V(0,0,0),        V(10,0,0),    V(1.05f,.9f,1.05f),    V(10,0,0),     V(1.04f,.93f,1.03f)),
                new Key(0.18f, (r,d,i) => r + Up*.3f,                  -40, V(0,.02f,0),     V(-8,0,10),   V(.94f,1.08f,.97f),    V(-12,0,-8),   V(.96f,1.06f,.98f)),
                new Key(0.42f, (r,d,i) => r*.45f + Back*.85f,           -5, V(0,0,.02f),     V(-4,0,-6),   V(.97f,.97f,1.06f),    V(6,0,5),      V(1.02f,.98f,1f)),
                new Key(0.65f, (r,d,i) => r*.6f + Back*.6f + Down*.15f,  8, Zero,            V(-2,0,2),    V(1f,1f,1.02f),        V(-2,0,-1),    One),
                new Key(0.85f, (r,d,i) => r*.7f + Back*.45f + Down*.2f,  5, Zero,            Zero,         One,                   Zero,          One),
            });
            Blink(rec, p, new[] { (0f, .9f), (.18f, 0f), (.85f, 0f) });
            return rec.Build("Octo_SpitOut", false);
        }

        // ================================================================ MORPH (layered over the shape morph, 0.7 s halves)
        // Alien leaving (-> fish): squash, twist, arms spiral in and tuck toward the body midpoint.
        static AnimationClip BuildMorphLeave(Poser p)
        {
            var rec = new Recorder(); p.End = .7f;
            Keys(p, rec, new[]
            {
                new Key(0.00f, (r,d,i) => d,                        0,  Zero,            Zero,          One,                  Zero,           One),
                new Key(0.12f, (r,d,i) => r*.8f + Up*.4f,         -30,  V(0,-.02f,0),    V(6,0,0),      V(1.08f,.9f,1.06f),   V(-10,0,0),     V(1.04f,.94f,1.02f)),
                new Key(0.32f, (r,d,i) => r*.5f + Down*.5f + Back*.5f, 120, V(0,.01f,0), V(-4,25,20),   V(.95f,1.04f,1.06f),  V(6,-10,-10),   V(.96f,1.05f,1f), 15),
                new Key(0.52f, (r,d,i) => r*.25f + Down*.5f + Back*.7f, 170, Zero,       V(0,40,30),    V(.96f,.98f,1.04f),   V(3,-6,-6),     V(.98f,1.02f,1f)),
                new Key(0.70f, (r,d,i) => d,                        0,  Zero,            Zero,          One,                  Zero,           One),
            });
            return rec.Build("Octo_MorphLeave", false);
        }
        // Alien arriving (fish -> alien): arms burst out past rest, mantle stretches up, recoils, settles.
        static AnimationClip BuildMorphArrive(Poser p)
        {
            var rec = new Recorder(); p.End = .7f;
            Keys(p, rec, new[]
            {
                new Key(0.00f, (r,d,i) => d,                    0,  Zero,            Zero,          One,                  Zero,          One),
                new Key(0.18f, (r,d,i) => r + Up*.35f,        -50,  V(0,.03f,0),     V(-6,-15,-8),  V(.93f,1.12f,.95f),   V(-12,8,6),    V(.95f,1.08f,.97f)),
                new Key(0.38f, (r,d,i) => d + Down*.15f,       30,  V(0,-.01f,0),    V(3,6,3),      V(1.05f,.93f,1.03f),  V(8,-4,-3),    V(1.04f,.94f,1.02f)),
                new Key(0.55f, (r,d,i) => r + Up*.05f,        -12,  Zero,            V(-1,-2,-1),   V(.99f,1.02f,.99f),   V(-3,1,1),     One),
                new Key(0.70f, (r,d,i) => d,                    0,  Zero,            Zero,          One,                  Zero,          One),
            });
            return rec.Build("Octo_MorphArrive", false);
        }
        // Fish halves: single body bone - wiggle, roll and squash/stretch.
        static AnimationClip BuildFish(bool leaving)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FishPath));
            try
            {
                var rig = go.GetComponent<AquariumRig>(); var body = rig.skin.bones.First(b => b.name == "Body"); int bi = System.Array.IndexOf(rig.skin.bones, body);
                string path = AnimationUtility.CalculateTransformPath(body, go.transform); var rec = new Recorder();
                var keys = leaving
                    ? new[] { (0f, Zero, One), (.12f, V(0, 12, 0), V(1.03f, .96f, 1.04f)), (.26f, V(0, -18, 10), V(.96f, 1.03f, 1.06f)), (.42f, V(0, 24, 30), V(1.04f, .95f, .97f)), (.58f, V(0, -10, 40), V(1.05f, 1.05f, .9f)), (.70f, Zero, One) }
                    : new[] { (0f, Zero, One), (.16f, V(0, -20, -25), V(.94f, 1.04f, 1.10f)), (.32f, V(0, 14, -8), V(1.04f, .97f, .97f)), (.48f, V(0, -7, 2), V(.99f, 1.01f, 1.02f)), (.60f, V(0, 3, 0), One), (.70f, Zero, One) };
                foreach (var (t, euler, scale) in keys)
                {
                    body.localPosition = rig.restPositions[bi]; body.localRotation = Quaternion.Euler(euler); body.localScale = scale;
                    rec.Transform(path, body, t);
                }
                return rec.Build(leaving ? "FishOcto_MorphLeave" : "FishOcto_MorphArrive", false);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ================================================================ APPLY
        static AnimationClip Save(AnimationClip clip)
        {
            if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder(Folder, "AlienOctopus");
            string path = OutFolder + "/" + clip.name + ".anim"; var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (old == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
            EditorUtility.CopySerialized(clip, old); Object.DestroyImmediate(clip); EditorUtility.SetDirty(old); return old;
        }

        [MenuItem("Aquarium/Characters/Apply octopus alien rig and animation")]
        public static void Apply()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try { Rerig(root.GetComponent<AquariumRig>()); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();

            var clips = new Dictionary<string, AnimationClip>(); AnimationClip leave, arrive;
            var poser = new Poser();
            try
            {
                clips["Idle"] = Save(BuildIdle(poser)); clips["Swim"] = Save(BuildSwim(poser));
                clips["TentacleEat"] = Save(BuildEat(poser, "Octo_TentacleEat")); clips["Bite"] = Save(BuildEat(poser, "Octo_Bite"));
                clips["Eaten"] = Save(BuildEaten(poser)); clips["Harpoon"] = Save(BuildHarpoon(poser));
                clips["SpitRecoil"] = Save(BuildSpitRecoil(poser)); clips["SpitOut"] = Save(BuildSpitOut(poser));
                leave = Save(BuildMorphLeave(poser)); arrive = Save(BuildMorphArrive(poser));
                clips["Morph"] = leave; clips["Unmorph"] = arrive;
            }
            finally { Object.DestroyImmediate(poser.go); }

            root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var rig = root.GetComponent<AquariumRig>();
                for (int i = 0; i < rig.clips.Length; i++)
                {
                    string n = rig.clips[i].name; string slot = n.Contains("_") ? n.Substring(n.LastIndexOf('_') + 1) : n;
                    if (clips.TryGetValue(slot, out var replacement)) rig.clips[i] = replacement;
                }
                var morph = root.GetComponent<AquariumMorphMotion>(); if (morph == null) morph = root.AddComponent<AquariumMorphMotion>(); morph.leave = leave; morph.arrive = arrive;
                if (root.GetComponent<AlienArmDrag>() == null) root.AddComponent<AlienArmDrag>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var fishLeave = Save(BuildFish(true)); var fishArrive = Save(BuildFish(false));
            root = PrefabUtility.LoadPrefabContents(FishPath);
            try
            {
                var morph = root.GetComponent<AquariumMorphMotion>(); if (morph == null) morph = root.AddComponent<AquariumMorphMotion>(); morph.leave = fishLeave; morph.arrive = fishArrive;
                PrefabUtility.SaveAsPrefabAsset(root, FishPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("ALIEN_OCTOPUS_ANIMATION_OK");
        }
    }
}

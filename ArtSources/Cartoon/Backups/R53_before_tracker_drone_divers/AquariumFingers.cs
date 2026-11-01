using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R31: finger joints for the diver's cartoon gloves. The painted diver has one bone per hand, so the glove
    // could never close. This finds the three fingers and the thumb on the glove surface itself (vertices welded,
    // cut past the knuckles, then grown back to the palm over the mesh), adds a knuckle and a middle joint to
    // each digit and re-skins those vertices with a soft ramp across both joints. Built once per source mesh and
    // hand; every diver (and the first-person receiving hand) shares the re-skinned copy. Nothing is saved.
    public sealed class AquariumFingers
    {
        private sealed class Prepared
        {
            public Mesh mesh;
            public Vector3[] baseLocal, midLocal, curlAxis, spreadAxis; // hand-bone local, per digit (3 fingers, thumb)
            public float[] side;
            public Vector3 gripLocal, fingerLocal, palmLocal;
        }
        private static readonly Dictionary<string, Prepared> cache = new Dictionary<string, Prepared>();
        private static readonly HashSet<Mesh> prepared = new HashSet<Mesh>();
        private static readonly string[] names = { "Finger1", "Finger2", "Finger3", "Thumb" };

        private readonly Transform hand;
        private readonly Transform[] proximal = new Transform[4], distal = new Transform[4];
        private readonly Prepared data;
        public Transform Hand { get { return hand; } }
        // The middle of the palm, a little in front of it: where a held catch sits.
        public Vector3 Grip { get { return hand.TransformPoint(data.gripLocal); } }
        public Vector3 GripLocal { get { return data.gripLocal; } }
        // The glove's own axes in the hand bone's frame: where the fingers point and which way the palm faces.
        public Vector3 FingerLocal { get { return data.fingerLocal; } }
        public Vector3 PalmLocal { get { return data.palmLocal; } }

        private AquariumFingers(Transform hand, Prepared data) { this.hand = hand; this.data = data; }

        public static AquariumFingers Attach(AquariumRig rig, string handName, bool rightHand)
        {
            if (rig == null || rig.skin == null || rig.skin.sharedMesh == null) return null;
            var skin = rig.skin; var source = skin.sharedMesh;
            if (prepared.Contains(source)) return null;
            var bones = skin.bones;
            int hi = Array.FindIndex(bones, b => b != null && b.name == handName);
            if (hi < 0) return null;
            string key = source.GetInstanceID() + ":" + handName;
            Prepared data;
            if (!cache.TryGetValue(key, out data))
            {
                try { data = Build(source, hi, rightHand); }
                catch (Exception e) { Debug.LogWarning("AquariumFingers: " + e.Message); data = null; }
                cache[key] = data;
                if (data != null) prepared.Add(data.mesh);
            }
            if (data == null) return null;
            var hand = bones[hi];
            var result = new AquariumFingers(hand, data);
            var all = new Transform[bones.Length + 8];
            Array.Copy(bones, all, bones.Length);
            for (int k = 0; k < 4; k++)
            {
                var p = new GameObject(names[k] + " knuckle").transform; p.SetParent(hand, false);
                p.localPosition = data.baseLocal[k]; p.localRotation = Quaternion.identity; p.gameObject.layer = hand.gameObject.layer;
                var d = new GameObject(names[k] + " middle").transform; d.SetParent(p, false);
                d.localPosition = data.midLocal[k] - data.baseLocal[k]; d.localRotation = Quaternion.identity; d.gameObject.layer = hand.gameObject.layer;
                result.proximal[k] = p; result.distal[k] = d;
                all[bones.Length + 2 * k] = p; all[bones.Length + 2 * k + 1] = d;
            }
            skin.sharedMesh = data.mesh; skin.bones = all;
            // Keep the rest pose tables in step with the bone list for any code that walks all bones.
            if (rig.restPositions != null && rig.restPositions.Length == bones.Length)
            {
                var rp = new Vector3[all.Length]; var rr = new Quaternion[all.Length]; var rs = new Vector3[all.Length];
                Array.Copy(rig.restPositions, rp, bones.Length); Array.Copy(rig.restRotations, rr, bones.Length); Array.Copy(rig.restScales, rs, bones.Length);
                for (int i = bones.Length; i < all.Length; i++) { rp[i] = all[i].localPosition; rr[i] = Quaternion.identity; rs[i] = Vector3.one; }
                rig.restPositions = rp; rig.restRotations = rr; rig.restScales = rs;
            }
            result.Pose(0, 0);
            return result;
        }

        // curl: -.4 (stretched wide open) .. 0 (as modelled) .. 1 (closed round a grip); spread: 0..1 fans the digits.
        // wiggle adds a per-finger flutter (cartoon "gimme" fingers) at the given time.
        public void Pose(float curl, float spread, float wiggle = 0, float time = 0)
        {
            for (int k = 0; k < 4; k++)
            {
                if (proximal[k] == null) continue;
                bool thumb = k == 3;
                float c = curl + wiggle * Mathf.Sin(time * 31f + k * 1.9f);
                if (thumb) c = c * .85f;
                // R49: a full grip bends 50 + 58 degrees (was 56 + 68, which folded the palm-side skin through itself).
                float prox = c >= 0 ? c * (thumb ? 38f : 50f) : c * (thumb ? 30f : 42f);
                float mid = c >= 0 ? c * (thumb ? 40f : 58f) : c * (thumb ? 18f : 28f);
                float fan = spread * (thumb ? 22f : 13f) * data.side[k];
                proximal[k].localRotation = Quaternion.AngleAxis(fan, data.spreadAxis[k]) * Quaternion.AngleAxis(prox, data.curlAxis[k]);
                distal[k].localRotation = Quaternion.AngleAxis(mid, data.curlAxis[k]);
            }
        }

        // ---- building -------------------------------------------------------------------------------------------

        private static float HandWeight(BoneWeight w, int hi)
        {
            return (w.boneIndex0 == hi ? w.weight0 : 0) + (w.boneIndex1 == hi ? w.weight1 : 0) + (w.boneIndex2 == hi ? w.weight2 : 0) + (w.boneIndex3 == hi ? w.weight3 : 0);
        }
        private static float Percentile(List<float> values, float q)
        {
            var copy = new List<float>(values); copy.Sort();
            return copy.Count == 0 ? 0 : copy[Mathf.Clamp(Mathf.FloorToInt(q * (copy.Count - 1)), 0, copy.Count - 1)];
        }
        private static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }

        private static Prepared Build(Mesh source, int hi, bool rightHand)
        {
            var v = source.vertices; var bw = source.boneWeights; var tris = source.triangles; int n = v.Length;
            if (bw == null || bw.Length != n) throw new Exception("no bone weights");
            var wh = new float[n];
            for (int i = 0; i < n; i++) wh[i] = HandWeight(bw[i], hi);

            // The glove's frame: wrist ring (half-weighted cuff), fingertip direction, thumb side, palm normal.
            Vector3 wrist = Vector3.zero, handSum = Vector3.zero; int ringCount = 0, handCount = 0;
            for (int i = 0; i < n; i++)
            {
                if (wh[i] > .5f) { handSum += v[i]; handCount++; }
                if (wh[i] > .25f && wh[i] < .75f) { wrist += v[i]; ringCount++; }
            }
            if (handCount < 200 || ringCount < 8) throw new Exception("no glove on bone " + hi);
            wrist /= ringCount;
            var f0 = (handSum / handCount - wrist).normalized;
            var along = new List<float>();
            for (int i = 0; i < n; i++) if (wh[i] > .5f) along.Add(Vector3.Dot(v[i] - wrist, f0));
            float tipCut = Percentile(along, .85f); Vector3 tipSum = Vector3.zero; int tipCount = 0;
            for (int i = 0; i < n; i++) if (wh[i] > .5f && Vector3.Dot(v[i] - wrist, f0) > tipCut) { tipSum += v[i]; tipCount++; }
            var F = (tipSum / Mathf.Max(1, tipCount) - wrist).normalized;
            along.Clear();
            for (int i = 0; i < n; i++) if (wh[i] > .5f) along.Add(Vector3.Dot(v[i] - wrist, F));
            float lo = Percentile(along, .25f), hiA = Percentile(along, .75f), best = -1; Vector3 T = Vector3.up;
            for (int i = 0; i < n; i++)
            {
                if (wh[i] <= .5f) continue;
                var rel = v[i] - wrist; float a = Vector3.Dot(rel, F); if (a < lo || a > hiA) continue;
                var perp = rel - a * F; if (perp.magnitude > best) { best = perp.magnitude; T = perp.normalized; }
            }
            var N = (rightHand ? Vector3.Cross(F, T) : Vector3.Cross(T, F)).normalized;
            T = (rightHand ? Vector3.Cross(N, F) : Vector3.Cross(F, N)).normalized;

            // Weld the glove surface (seams and hard edges split vertices) so cuts give whole digits.
            var ids = new List<int>(); var rep = new int[n]; var welded = new Dictionary<long, int>();
            for (int i = 0; i < n; i++)
            {
                rep[i] = i; if (wh[i] <= .02f) continue;
                ids.Add(i);
                long key = ((long)(Mathf.RoundToInt(v[i].x * 10000) + 1048576) << 42) | ((long)(Mathf.RoundToInt(v[i].y * 10000) + 1048576) << 21) | (long)(Mathf.RoundToInt(v[i].z * 10000) + 1048576);
                int r; if (welded.TryGetValue(key, out r)) rep[i] = r; else welded[key] = i;
            }
            var A = new Dictionary<int, float>(); var B = new Dictionary<int, float>();
            float L = 0, bm = 0;
            foreach (int i in ids) { if (rep[i] != i) continue; var rel = v[i] - wrist; A[i] = Vector3.Dot(rel, F); B[i] = Vector3.Dot(rel, T); L = Mathf.Max(L, A[i]); bm = Mathf.Max(bm, B[i]); }
            var adjacency = new Dictionary<int, List<int>>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int x = rep[tris[t]], y = rep[tris[t + 1]], z = rep[tris[t + 2]];
                if (!A.ContainsKey(x) || !A.ContainsKey(y) || !A.ContainsKey(z)) continue;
                Link(adjacency, x, y); Link(adjacency, y, z); Link(adjacency, z, x);
            }
            Func<int, bool> fingerZone = i => A[i] > .60f * L;
            var fingers = Components(adjacency, A.Keys, fingerZone);
            fingers.Sort((p, q) => q.Count.CompareTo(p.Count));
            Func<int, bool> thumbZone = i => B[i] > .6f * bm && A[i] < .6f * L;
            var thumbs = Components(adjacency, A.Keys, thumbZone);
            if (fingers.Count < 3 || fingers[2].Count < 30 || thumbs.Count == 0) throw new Exception("could not find three fingers and a thumb");
            List<int> thumb = null; float thumbB = float.MinValue;
            foreach (var c in thumbs) { float m = 0; foreach (int i in c) m += B[i]; m /= c.Count; if (c.Count >= 30 && m > thumbB) { thumbB = m; thumb = c; } }
            if (thumb == null) throw new Exception("no thumb");
            // Order the three fingers from the thumb side down: index, middle, outer.
            fingers = fingers.GetRange(0, 3);
            fingers.Sort((p, q) => MeanOf(q, B).CompareTo(MeanOf(p, B)));

            // Grow each fingertip back to the palm over the surface (nearest tip wins).
            var owner = new Dictionary<int, int>(); var queue = new Queue<int>();
            for (int k = 0; k < 3; k++) foreach (int i in fingers[k]) { owner[i] = k; queue.Enqueue(i); }
            while (queue.Count > 0)
            {
                int u = queue.Dequeue(); List<int> next; if (!adjacency.TryGetValue(u, out next)) continue;
                foreach (int w in next) if (!owner.ContainsKey(w) && A[w] > .42f * L) { owner[w] = owner[u]; queue.Enqueue(w); }
            }

            var baseP = new Vector3[4]; var dir = new Vector3[4]; var len = new float[4]; var radius = new float[4];
            for (int k = 0; k < 4; k++)
            {
                var pts = new List<Vector3>();
                if (k < 3) { foreach (var kv in owner) if (kv.Value == k) pts.Add(v[kv.Key]); }
                else foreach (int i in thumb) pts.Add(v[i]);
                Vector3 cen = Vector3.zero; foreach (var p in pts) cen += p; cen /= pts.Count;
                Vector3 d;
                if (k < 3) { d = Principal(pts, cen, F); if (Vector3.Dot(d, F) < 0) d = -d; }
                else
                {
                    Vector3 ring = Vector3.zero; int rc = 0;
                    foreach (int i in thumb) if (B[i] < .68f * bm) { ring += v[i]; rc++; }
                    ring = rc > 0 ? ring / rc : cen;
                    var dist = new List<float>(); foreach (var p in pts) dist.Add((p - ring).magnitude);
                    float far = Percentile(dist, .9f); Vector3 tip = Vector3.zero; int tc = 0;
                    foreach (var p in pts) if ((p - ring).magnitude >= far) { tip += p; tc++; }
                    d = (tip / Mathf.Max(1, tc) - ring).normalized; if (Vector3.Dot(d, T) < 0) d = -d;
                }
                float pmin = float.MaxValue, pmax = float.MinValue;
                foreach (var p in pts) { float s = Vector3.Dot(p - cen, d); pmin = Mathf.Min(pmin, s); pmax = Mathf.Max(pmax, s); }
                float start = k < 3 ? pmin + .18f * (pmax - pmin) : pmin - .30f * (pmax - pmin);
                baseP[k] = cen + d * start; dir[k] = d; len[k] = pmax - start;
                var lateral = new List<float>(); foreach (var p in pts) { var q = p - cen; lateral.Add((q - Vector3.Dot(q, d) * d).magnitude); }
                radius[k] = Percentile(lateral, .5f);
            }

            // Re-skin: palm -> knuckle -> middle ramps along each digit, scaled by the vertex's own hand weight.
            // R49: the share of the hand weight each joint takes is worked out per welded surface point and then
            // smoothed over the glove surface. With hard per-digit ownership, triangles bridging two touching
            // fingers (the webbing) or the palm and a finger were torn apart when the hand curled (stretched up to
            // 35x and ~700 flipped faces at a full grip), which showed as broken, torn patches on the gloves.
            int boneCount = source.bindposes.Length;
            var weights = (BoneWeight[])bw.Clone();
            var share = new Dictionary<int, float[]>();
            foreach (int r in A.Keys)
            {
                var f9 = new float[9]; f9[0] = 1; share[r] = f9;
                int k = -1; int o;
                if (owner.TryGetValue(r, out o)) k = o;
                else
                {
                    var q = v[r] - baseP[3]; float s3 = Vector3.Dot(q, dir[3]);
                    if (s3 > -.12f * len[3] && (q - s3 * dir[3]).magnitude < radius[3] * 1.9f) k = 3;
                }
                if (k < 0) continue;
                float s = Vector3.Dot(v[r] - baseP[k], dir[k]);
                // R49: wider ramps so the inside of a bent joint folds softly instead of collapsing into itself.
                float w1 = Smooth(-.24f * len[k], .2f * len[k], s), w2 = Smooth(.3f * len[k], .7f * len[k], s);
                f9[0] = 1 - w1; f9[1 + 2 * k] = w1 - w2; f9[2 + 2 * k] = w2;
            }
            var next9 = new Dictionary<int, float[]>();
            for (int pass = 0; pass < 6; pass++)
            {
                foreach (var kv in share)
                {
                    List<int> around; var mine = kv.Value; float[] o9;
                    if (!next9.TryGetValue(kv.Key, out o9)) next9[kv.Key] = o9 = new float[9];
                    if (!adjacency.TryGetValue(kv.Key, out around) || around.Count == 0) { Array.Copy(mine, o9, 9); continue; }
                    for (int c = 0; c < 9; c++) o9[c] = 0;
                    foreach (int w in around) { var nb9 = share[w]; for (int c = 0; c < 9; c++) o9[c] += nb9[c]; }
                    for (int c = 0; c < 9; c++) o9[c] = Mathf.Lerp(mine[c], o9[c] / around.Count, .6f);
                }
                var swap = share; share = next9; next9 = swap;
            }
            var slots = new List<KeyValuePair<int, float>>(12);
            foreach (int i in ids)
            {
                float[] f9; if (!share.TryGetValue(rep[i], out f9) || f9[0] >= .999f) continue;
                float h = wh[i];
                slots.Clear();
                var b = bw[i];
                AddSlot(slots, b.boneIndex0, b.weight0, hi); AddSlot(slots, b.boneIndex1, b.weight1, hi); AddSlot(slots, b.boneIndex2, b.weight2, hi); AddSlot(slots, b.boneIndex3, b.weight3, hi);
                if (h * f9[0] > 0) slots.Add(new KeyValuePair<int, float>(hi, h * f9[0]));
                for (int c = 1; c < 9; c++) if (h * f9[c] > .001f) slots.Add(new KeyValuePair<int, float>(boneCount + c - 1, h * f9[c]));
                slots.Sort((p, q) => q.Value.CompareTo(p.Value));
                float total = 0; for (int j = 0; j < Mathf.Min(4, slots.Count); j++) total += slots[j].Value;
                if (total <= 0) continue;
                var nw = new BoneWeight();
                for (int j = 0; j < 4; j++)
                {
                    int bone = j < slots.Count ? slots[j].Key : 0; float w = j < slots.Count ? slots[j].Value / total : 0;
                    if (j == 0) { nw.boneIndex0 = bone; nw.weight0 = w; } else if (j == 1) { nw.boneIndex1 = bone; nw.weight1 = w; }
                    else if (j == 2) { nw.boneIndex2 = bone; nw.weight2 = w; } else { nw.boneIndex3 = bone; nw.weight3 = w; }
                }
                weights[i] = nw;
            }

            var handBind = source.bindposes[hi];
            var data = new Prepared
            {
                baseLocal = new Vector3[4], midLocal = new Vector3[4], curlAxis = new Vector3[4], spreadAxis = new Vector3[4], side = new float[4]
            };
            var binds = new Matrix4x4[boneCount + 8];
            Array.Copy(source.bindposes, binds, boneCount);
            var nL = handBind.MultiplyVector(N).normalized; var tL = handBind.MultiplyVector(T).normalized;
            for (int k = 0; k < 4; k++)
            {
                var mid = baseP[k] + dir[k] * len[k] * .5f;
                data.baseLocal[k] = handBind.MultiplyPoint3x4(baseP[k]); data.midLocal[k] = handBind.MultiplyPoint3x4(mid);
                binds[boneCount + 2 * k] = Matrix4x4.Translate(-data.baseLocal[k]) * handBind;
                binds[boneCount + 2 * k + 1] = Matrix4x4.Translate(-data.midLocal[k]) * handBind;
                var dL = handBind.MultiplyVector(dir[k]).normalized;
                // Curl turns the tip toward the palm side.
                var curl = Vector3.Cross(dL, nL).normalized;
                if (Vector3.Dot(Quaternion.AngleAxis(20, curl) * dL, nL) < Vector3.Dot(dL, nL)) curl = -curl;
                data.curlAxis[k] = curl;
                // Spread turns each digit away from the middle finger; the thumb away from the fingers.
                var fan = nL;
                if (k < 3)
                {
                    float offset = Vector3.Dot(baseP[k] - baseP[1], T);
                    data.side[k] = k == 1 ? 0 : Mathf.Sign(offset);
                    if (Vector3.Dot(Quaternion.AngleAxis(10, fan) * dL, tL) < Vector3.Dot(dL, tL)) fan = -fan;
                }
                else
                {
                    var fL = handBind.MultiplyVector(F).normalized; data.side[k] = 1;
                    if (Vector3.Dot(Quaternion.AngleAxis(10, fan) * dL, fL) > Vector3.Dot(dL, fL)) fan = -fan;
                }
                data.spreadAxis[k] = fan;
            }
            // The palm's middle, just in front of it (where a held catch sits).
            Vector3 palm = Vector3.zero; int pc = 0;
            for (int i = 0; i < n; i++) if (wh[i] > .5f) { float a = Vector3.Dot(v[i] - wrist, F); if (a > .30f * L && a < .62f * L) { palm += v[i]; pc++; } }
            palm = pc > 0 ? palm / pc : wrist + F * (.45f * L);
            data.gripLocal = handBind.MultiplyPoint3x4(palm + N * (radius[1] * 1.6f));
            data.fingerLocal = handBind.MultiplyVector(F).normalized; data.palmLocal = nL;

            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = source.name + " (fingers)"; mesh.hideFlags = HideFlags.HideAndDontSave;
            mesh.boneWeights = weights; mesh.bindposes = binds;
            data.mesh = mesh;
            return data;
        }

        private static float MeanOf(List<int> part, Dictionary<int, float> values)
        {
            float m = 0; foreach (int i in part) m += values[i]; return part.Count > 0 ? m / part.Count : 0;
        }
        private static void AddSlot(List<KeyValuePair<int, float>> slots, int bone, float weight, int hand)
        {
            if (weight <= 0 || bone == hand) return;
            slots.Add(new KeyValuePair<int, float>(bone, weight));
        }
        private static void Link(Dictionary<int, List<int>> adjacency, int a, int b)
        {
            if (a == b) return;
            List<int> l; if (!adjacency.TryGetValue(a, out l)) adjacency[a] = l = new List<int>(); if (!l.Contains(b)) l.Add(b);
            if (!adjacency.TryGetValue(b, out l)) adjacency[b] = l = new List<int>(); if (!l.Contains(a)) l.Add(a);
        }
        private static List<List<int>> Components(Dictionary<int, List<int>> adjacency, IEnumerable<int> all, Func<int, bool> inside)
        {
            var result = new List<List<int>>(); var seen = new HashSet<int>(); var stack = new Stack<int>();
            foreach (int s in all)
            {
                if (seen.Contains(s) || !inside(s)) continue;
                var part = new List<int>(); seen.Add(s); stack.Push(s);
                while (stack.Count > 0)
                {
                    int u = stack.Pop(); part.Add(u); List<int> next;
                    if (adjacency.TryGetValue(u, out next)) foreach (int w in next) if (!seen.Contains(w) && inside(w)) { seen.Add(w); stack.Push(w); }
                }
                result.Add(part);
            }
            return result;
        }
        private static Vector3 Principal(List<Vector3> pts, Vector3 cen, Vector3 guess)
        {
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (var p in pts) { var q = p - cen; xx += q.x * q.x; xy += q.x * q.y; xz += q.x * q.z; yy += q.y * q.y; yz += q.y * q.z; zz += q.z * q.z; }
            var d = guess.normalized;
            for (int i = 0; i < 40; i++)
            {
                var e = new Vector3(xx * d.x + xy * d.y + xz * d.z, xy * d.x + yy * d.y + yz * d.z, xz * d.x + yz * d.y + zz * d.z);
                if (e.sqrMagnitude < 1e-12f) break; d = e.normalized;
            }
            return d;
        }
    }
}

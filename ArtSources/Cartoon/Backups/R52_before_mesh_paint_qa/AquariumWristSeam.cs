using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R51: the painted atlas splits glove and sleeve with a jagged line through the wrist triangles. This cuts the
    // diver surface itself along a clean ring at the wrist (new vertices on the cut, skin weights blended), paints
    // the glove side and the sleeve side with clean flat texels on either side of the cut (a crisp, straight edge)
    // and raises a soft rolled rubber lip on the glove side: one surface, no extra objects.
    public static class AquariumWristSeam
    {
        const string Equipment = "Assets/Demos/HideAndSeek/Equipment/Resources/AquariumEquipment";
        static readonly Color32 Glove = new Color32(66, 58, 62, 255);
        static bool IsGlove(Color32 c) { return Mathf.Abs(c.r - Glove.r) + Mathf.Abs(c.g - Glove.g) + Mathf.Abs(c.b - Glove.b) <= 36; }
        static bool IsDark(Color c) { return c.r < .35f && c.g < .35f; }
        static bool IsSuit(Color32 c) { return c.r > 170 && c.g > 140 && c.b < 140; }

        public static string SealInPlace()
        {
            string report = "";
            foreach (string name in new[] { "Diver", "DiverHands" })
            {
                var rig = AssetDatabase.LoadAssetAtPath<GameObject>(Equipment + "/" + name + ".prefab").GetComponent<AquariumRig>();
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Equipment + "/" + name + ".asset");
                report += name + ": " + Seal(mesh, rig.skin.bones, rig.skin.transform) + "; ";
                EditorUtility.SetDirty(mesh);
            }
            AssetDatabase.SaveAssets();
            return report;
        }

        struct Ring { public Vector3 centre, axis; public float radius, half; public int hand, fore; }

        public static string Seal(Mesh mesh, Transform[] bones, Transform space)
        {
            var atlasPath = AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<Material>(Equipment + "/Diver_Cartoon.mat").mainTexture);
            var image = new Texture2D(2, 2); image.LoadImage(File.ReadAllBytes(atlasPath));
            int W = image.width, H = image.height; var px = image.GetPixels32();
            Func<Vector2, Color32> texel = q => px[Mathf.Clamp((int)(q.y * H), 0, H - 1) * W + Mathf.Clamp((int)(q.x * W), 0, W - 1)];
            var v = new List<Vector3>(mesh.vertices); var n = new List<Vector3>(mesh.normals); var uv = new List<Vector2>(mesh.uv);
            var uv2 = new List<Vector2>(mesh.uv2); bool hasUv2 = uv2.Count == v.Count;
            var bw = new List<BoneWeight>(mesh.boneWeights); var tris = new List<int>(mesh.triangles);
            Func<Vector3, Vector3> toBone = p => space.TransformPoint(p);
            var rings = new List<Ring>();
            foreach (var side in new[] { "L", "R" })
            {
                int hand = Array.FindIndex(bones, b => b.name == "Hand" + side), fore = Array.FindIndex(bones, b => b.name == "Forearm" + side);
                if (hand < 0 || fore < 0) continue;
                var dark = new Func<int, bool>(i => IsDark((Color)texel(uv[i])));
                var seam = new List<Vector3>();
                for (int t = 0; t < tris.Count; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    bool da = dark(a), db = dark(b), dc = dark(c);
                    if ((da && db && dc) || (!da && !db && !dc)) continue;
                    if (!Arm(bw[a], hand, fore) || !Arm(bw[b], hand, fore) || !Arm(bw[c], hand, fore)) continue;
                    seam.Add(v[a]); seam.Add(v[b]); seam.Add(v[c]);
                }
                if (seam.Count < 12) continue;
                var axisW = (bones[hand].position - bones[fore].position).normalized;
                var axis = space.InverseTransformDirection(axisW).normalized;
                var ctr = Vector3.zero; foreach (var p in seam) ctr += p; ctr /= seam.Count;
                float lo = 1e9f, hi = -1e9f; foreach (var p in seam) { float al = Vector3.Dot(p - ctr, axis); lo = Mathf.Min(lo, al); hi = Mathf.Max(hi, al); }
                ctr += axis * ((lo + hi) * .5f);
                float r = 0; foreach (var p in seam) r += Vector3.ProjectOnPlane(p - ctr, axis).magnitude; r /= seam.Count;
                // the arm's centre line through the seam (the seam centroid can sit off-centre)
                var slab = new List<Vector3>(); float half = (hi - lo) * .5f;
                for (int i = 0; i < v.Count; i++) if (Arm(bw[i], hand, fore) && Mathf.Abs(Vector3.Dot(v[i] - ctr, axis)) < Mathf.Max(half, r * .1f)) slab.Add(v[i]);
                var mid = Vector3.zero; foreach (var p in slab) mid += p; if (slab.Count > 0) { mid /= slab.Count; ctr += Vector3.ProjectOnPlane(mid - ctr, axis); }
                rings.Add(new Ring { centre = ctr, axis = axis, radius = r, half = half, hand = hand, fore = fore });
            }
            if (rings.Count == 0) return "no wrist seam";
            // clean texels: the glove (well inside its paint) and the sleeve (yellow suit paint)
            Vector2 gloveUv = Vector2.zero, suitUv = Vector2.zero; int bestG = -1, bestS = -1;
            for (int i = 0; i < uv.Count; i++)
            {
                var c = texel(uv[i]); int score = 0;
                for (int k = 0; k < 9; k++) score += IsGlove(texel(uv[i] + new Vector2((k % 3 - 1) * 4f / W, (k / 3 - 1) * 4f / H))) ? 1 : 0;
                if (IsGlove(c) && score > bestG) { bestG = score; gloveUv = uv[i]; }
                int s2 = 0; for (int k = 0; k < 9; k++) s2 += IsSuit(texel(uv[i] + new Vector2((k % 3 - 1) * 4f / W, (k / 3 - 1) * 4f / H))) ? 1 : 0;
                if (IsSuit(c) && s2 > bestS) { bestS = s2; suitUv = uv[i]; }
            }
            int added = 0;
            foreach (var ring in rings)
            {
                float lip = ring.radius * .34f, band = ring.half * 2.6f + lip;
                Func<int, bool> inArm = i => Arm(bw[i], ring.hand, ring.fore) && Vector3.ProjectOnPlane(v[i] - ring.centre, ring.axis).magnitude < ring.radius * 2.2f;
                Func<int, float> d = i => Vector3.Dot(v[i] - ring.centre, ring.axis);
                // extra rings of vertices so the lip is round, then the colour cut itself (split = separate glove / sleeve copies)
                foreach (var at in new[] { lip, lip * .62f, lip * .3f, -ring.half * 1.6f })
                    added += Cut(v, n, uv, uv2, hasUv2, bw, tris, inArm, i => d(i) - at, false, null);
                var gloveCopy = new Dictionary<int, int>();
                added += Cut(v, n, uv, uv2, hasUv2, bw, tris, inArm, d, true, gloveCopy);
                var cutVerts = new HashSet<int>(gloveCopy.Keys); foreach (var g in gloveCopy.Values) cutVerts.Add(g);
                // paint: near the cut, every arm vertex whose paint does not match its side of the cut moves to the
                // closest clean texel of the right paint (vertices already on the right paint keep their UVs, so the
                // surrounding triangles do not smear across the atlas)
                var gloveSide = new HashSet<int>(gloveCopy.Values);
                // The whole glove (hand and wrist past the cut) takes one clean glove texel: it is flat cartoon paint,
                // and a single texel cannot smear. The sleeve keeps its own paint; only stray glove texels near the cut
                // move to the closest clean suit paint.
                for (int i = 0; i < v.Count; i++)
                {
                    if (!Arm(bw[i], ring.hand, ring.fore)) continue;
                    float di = d(i); bool glove = gloveSide.Contains(i) || di > 1e-5f;
                    if (glove) { uv[i] = gloveUv; continue; }
                    if (!inArm(i) || -di > band + ring.radius * .6f) continue;
                    if (!IsSuit(texel(uv[i]))) uv[i] = Nearest(px, W, H, uv[i], false, suitUv);
                }
                // triangles the cut did not split (at the edge of the arm region) can still mix the glove texel with
                // the original paint; settle each one on a single side so nothing smears across the atlas
                for (int t2 = 0; t2 < tris.Count; t2 += 3)
                {
                    int ta = tris[t2], tb = tris[t2 + 1], tc = tris[t2 + 2];
                    bool ga = uv[ta] == gloveUv, gb = uv[tb] == gloveUv, gc = uv[tc] == gloveUv;
                    if ((ga && gb && gc) || (!ga && !gb && !gc)) continue;
                    float m = (d(ta) + d(tb) + d(tc)) / 3;
                    if (m >= 0) { uv[ta] = uv[tb] = uv[tc] = gloveUv; continue; }
                    var keep = !ga ? uv[ta] : !gb ? uv[tb] : uv[tc];
                    if (ga) uv[ta] = keep; if (gb) uv[tb] = keep; if (gc) uv[tc] = keep;
                }
                // rolled rubber lip on the glove side: a soft round ridge just past the cut
                for (int i = 0; i < v.Count; i++)
                {
                    if (!inArm(i)) continue; float di = d(i); if (di <= 0 || di >= lip) continue;
                    var radial = Vector3.ProjectOnPlane(v[i] - ring.centre, ring.axis); if (radial.sqrMagnitude < 1e-10f) continue;
                    float x = di / lip; float bump = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(x, .8f)), 1.3f);
                    v[i] += radial.normalized * (ring.radius * .11f * bump);
                }
            }
            mesh.SetVertices(v); mesh.SetUVs(0, uv); if (hasUv2) mesh.SetUVs(1, uv2); mesh.boneWeights = bw.ToArray(); mesh.SetTriangles(tris, 0);
            // smooth normals across UV splits (welded by position) so the cut reads as one surface
            mesh.RecalculateNormals();
            var normals = mesh.normals; var groups = new Dictionary<Vector3Int, Vector3>();
            var verts = mesh.vertices;
            Func<Vector3, Vector3Int> key = p => new Vector3Int(Mathf.RoundToInt(p.x * 20000), Mathf.RoundToInt(p.y * 20000), Mathf.RoundToInt(p.z * 20000));
            for (int i = 0; i < verts.Length; i++) { var k = key(verts[i]); Vector3 s; groups.TryGetValue(k, out s); groups[k] = s + normals[i]; }
            for (int i = 0; i < verts.Length; i++) normals[i] = groups[key(verts[i])].normalized;
            mesh.normals = normals; mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return rings.Count + " wrists, " + added + " vertices added";
        }

        // The closest texel to q (same UV island, so neighbours interpolate over clean paint) whose 5x5 block is all
        // glove (or all suit) paint; the fallback when none is found within reach.
        static Vector2 Nearest(Color32[] px, int W, int H, Vector2 q, bool glove, Vector2 fallback)
        {
            int x0 = Mathf.Clamp((int)(q.x * W), 0, W - 1), y0 = Mathf.Clamp((int)(q.y * H), 0, H - 1);
            Func<int, int, bool> clean = (x, y) =>
            {
                for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                { int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= W || yy >= H) return false; var c = px[yy * W + xx]; if (glove ? !IsGlove(c) : !IsSuit(c)) return false; }
                return true;
            };
            if (clean(x0, y0)) return q;
            for (int r = 1; r <= 96; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int x = x0 + dx, y = y0 + dy; if (x < 2 || y < 2 || x >= W - 2 || y >= H - 2 || !clean(x, y)) continue;
                        return new Vector2((x + .5f) / W, (y + .5f) / H);
                    }
            return fallback;
        }

        static bool Arm(BoneWeight w, int hand, int fore) { return w.boneIndex0 == hand || w.boneIndex0 == fore; }

        // Splits every arm triangle that crosses side(i) = 0. With split, the cut vertices are made twice (the copy
        // on the positive side is returned in gloveCopy: original-or-new index -> positive-side index).
        static int Cut(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<Vector2> uv2, bool hasUv2, List<BoneWeight> bw, List<int> tris,
            Func<int, bool> inArm, Func<int, float> side, bool split, Dictionary<int, int> gloveCopy)
        {
            var edgeNeg = new Dictionary<long, int>(); var edgePos = new Dictionary<long, int>(); int added = 0;
            var output = new List<int>(tris.Count + 64);
            Func<int, int, bool, int> point = (a, b, positive) =>
            {
                long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                var map = split && positive ? edgePos : edgeNeg;
                int idx; if (map.TryGetValue(k, out idx)) return idx;
                float sa = side(a), sb = side(b), t = sa / (sa - sb);
                v.Add(Vector3.Lerp(v[a], v[b], t)); n.Add(Vector3.Lerp(n[a], n[b], t).normalized); uv.Add(Vector2.Lerp(uv[a], uv[b], t));
                if (hasUv2) uv2.Add(Vector2.Lerp(uv2[a], uv2[b], t)); bw.Add(Blend(bw[a], bw[b], t));
                idx = v.Count - 1; map[k] = idx; added++; return idx;
            };
            // vertices exactly on the plane get a positive-side copy when splitting
            Func<int, int> onPlanePos = i =>
            {
                int c; if (gloveCopy.TryGetValue(i, out c)) return c;
                v.Add(v[i]); n.Add(n[i]); uv.Add(uv[i]); if (hasUv2) uv2.Add(uv2[i]); bw.Add(bw[i]);
                c = v.Count - 1; gloveCopy[i] = c; added++; return c;
            };
            for (int t = 0; t < tris.Count; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                if (!inArm(a) || !inArm(b) || !inArm(c)) { output.Add(a); output.Add(b); output.Add(c); continue; }
                float sa = side(a), sb = side(b), sc = side(c);
                const float eps = 1e-6f;
                if (Mathf.Abs(sa) < eps) sa = 0; if (Mathf.Abs(sb) < eps) sb = 0; if (Mathf.Abs(sc) < eps) sc = 0;
                bool pos = sa >= 0 && sb >= 0 && sc >= 0 && (sa > 0 || sb > 0 || sc > 0), neg = sa <= 0 && sb <= 0 && sc <= 0;
                if (pos || neg)
                {
                    if (split && pos) { if (sa == 0) a = onPlanePos(a); if (sb == 0) b = onPlanePos(b); if (sc == 0) c = onPlanePos(c); }
                    output.Add(a); output.Add(b); output.Add(c); continue;
                }
                // rotate so that a is the lone vertex on its side
                int[] idx = { a, b, c }; float[] s = { sa, sb, sc };
                int lone = -1, zeros = (s[0] == 0 ? 1 : 0) + (s[1] == 0 ? 1 : 0) + (s[2] == 0 ? 1 : 0);
                if (zeros == 1) { for (int k = 0; k < 3 && lone < 0; k++) if (s[k] != 0) lone = k; }
                else for (int k = 0; k < 3; k++) { int p = (k + 1) % 3, q = (k + 2) % 3; if ((s[k] > 0) != (s[p] > 0) && (s[k] > 0) != (s[q] > 0)) lone = k; }
                if (lone < 0) { output.Add(a); output.Add(b); output.Add(c); continue; }
                int A = idx[lone], B = idx[(lone + 1) % 3], C = idx[(lone + 2) % 3]; float SA = s[lone], SB = s[(lone + 1) % 3], SC = s[(lone + 2) % 3];
                if (SB == 0 || SC == 0)
                {
                    // one corner on the plane: split into two triangles
                    int onV = SB == 0 ? B : C, other = SB == 0 ? C : B;
                    int mA = point(A, other, SA > 0), mO = point(A, other, SA <= 0);
                    if (!split) mO = mA;
                    int onA = split && SA > 0 ? onPlanePos(onV) : onV, onO = split && SA <= 0 ? onPlanePos(onV) : onV;
                    if (SB == 0) { output.Add(A); output.Add(onA); output.Add(mA); output.Add(mO); output.Add(onO); output.Add(C); }
                    else { output.Add(A); output.Add(mA); output.Add(onA); output.Add(mO); output.Add(B); output.Add(onO); }
                    continue;
                }
                bool aPos = SA > 0;
                int ab1 = point(A, B, aPos), ac1 = point(A, C, aPos), ab2 = point(A, B, !aPos), ac2 = point(A, C, !aPos);
                if (!split) { ab2 = ab1; ac2 = ac1; }
                output.Add(A); output.Add(ab1); output.Add(ac1);
                output.Add(ab2); output.Add(B); output.Add(C);
                output.Add(ab2); output.Add(C); output.Add(ac2);
            }
            tris.Clear(); tris.AddRange(output);
            return added;
        }

        static BoneWeight Blend(BoneWeight a, BoneWeight b, float t)
        {
            var w = new Dictionary<int, float>();
            Action<int, float> add = (i, x) => { if (x <= 0) return; float s; w.TryGetValue(i, out s); w[i] = s + x; };
            add(a.boneIndex0, a.weight0 * (1 - t)); add(a.boneIndex1, a.weight1 * (1 - t)); add(a.boneIndex2, a.weight2 * (1 - t)); add(a.boneIndex3, a.weight3 * (1 - t));
            add(b.boneIndex0, b.weight0 * t); add(b.boneIndex1, b.weight1 * t); add(b.boneIndex2, b.weight2 * t); add(b.boneIndex3, b.weight3 * t);
            var list = new List<KeyValuePair<int, float>>(w); list.Sort((x, y) => y.Value.CompareTo(x.Value));
            float total = 0; for (int i = 0; i < list.Count && i < 4; i++) total += list[i].Value;
            var r = new BoneWeight();
            if (list.Count > 0) { r.boneIndex0 = list[0].Key; r.weight0 = list[0].Value / total; }
            if (list.Count > 1) { r.boneIndex1 = list[1].Key; r.weight1 = list[1].Value / total; }
            if (list.Count > 2) { r.boneIndex2 = list[2].Key; r.weight2 = list[2].Value / total; }
            if (list.Count > 3) { r.boneIndex3 = list[3].Key; r.weight3 = list[3].Value / total; }
            return r;
        }
    }
}

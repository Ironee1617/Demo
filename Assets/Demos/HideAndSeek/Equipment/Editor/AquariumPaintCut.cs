using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R52: the painted diver atlas is a low-resolution bake (about 3-4 texels per triangle edge, many small UV
    // islands padded with glove paint), so every colour boundary rendered soft, jagged and speckled with seam bleed.
    // This pass reads the paint into the mesh itself: each surface point gets a score per cartoon colour (sampled from
    // the atlas, welded across UV seams and smoothed over the surface), the mesh is cut along the iso-lines where the
    // winning colour changes, and every flat-colour piece samples one clean swatch (written into a corner of the atlas).
    // Boundaries become exact geometric edges: crisp at any distance, no mip or compression blur, no seam specks.
    // The goggle lens (painted gradient + highlight) keeps its atlas paint.
    public static class AquariumPaintCut
    {
        const string Equipment = "Assets/Demos/HideAndSeek/Equipment/Resources/AquariumEquipment";
        static string SourceAtlas => Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtSources/DiverHarpoon/Diver_Atlas.png"));

        // The cartoon palette of the diver sheet (flat paint).
        public static readonly Color32[] Palette =
        {
            new Color32(66, 58, 62, 255),    // 0 glove / gear dark
            new Color32(247, 184, 62, 255),  // 1 suit yellow
            new Color32(244, 233, 220, 255), // 2 white suit
            new Color32(198, 202, 208, 255), // 3 silver metal
            new Color32(242, 214, 196, 255), // 4 skin
            new Color32(82, 52, 46, 255),    // 5 moustache brown
            new Color32(150, 150, 158, 255), // 6 grey
            new Color32(244, 166, 146, 255), // 7 nose pink
            new Color32(92, 86, 92, 255),    // 8 dark grey
            new Color32(176, 92, 88, 255),   // 9 mouth red
        };
        public const int Lens = 10, Labels = 11, Glove = 0;
        const int Swatch = 64;
        public static float Sigma = .01f;
        public static float SplitLength = .012f, SpeckArea = 3e-4f;

        static bool IsLens(Color32 c) { return (c.b - c.r > 50 && c.b > 120) || (c.b - c.r >= 28 && c.b > 225); } // blue glass or its pale highlight
        // Nearest palette colour; a texel that is an anti-aliased blend of two palette colours (the bake's soft edges)
        // goes to the nearer of the two, never to a third colour that happens to sit close to the blend.
        static int[] lut;
        public static int Classify(Color32 c)
        {
            if (IsLens(c)) return Lens;
            if (lut == null)
            {
                lut = new int[32 * 32 * 32];
                for (int i = 0; i < lut.Length; i++) lut[i] = ClassifySlow(new Vector3((i >> 10) * 8 + 4, ((i >> 5) & 31) * 8 + 4, (i & 31) * 8 + 4));
            }
            return lut[(c.r >> 3) << 10 | (c.g >> 3) << 5 | (c.b >> 3)];
        }
        static int ClassifySlow(Vector3 c)
        {
            int best = 0; float bd = float.MaxValue;
            for (int k = 0; k < Palette.Length; k++) { float d = (c - P(k)).sqrMagnitude; if (d < bd) { bd = d; best = k; } }
            if (bd < 18 * 18) return best;
            int bi = best; float bb = bd * .35f;
            for (int i = 0; i < Palette.Length; i++)
                for (int j = i + 1; j < Palette.Length; j++)
                {
                    var a = P(i); var ab = P(j) - a; float t = Mathf.Clamp01(Vector3.Dot(c - a, ab) / ab.sqrMagnitude);
                    float d = (c - (a + ab * t)).sqrMagnitude;
                    if (d < bb) { bb = d; bi = t < .5f ? i : j; }
                }
            return bi;
        }
        static Vector3 P(int k) { return new Vector3(Palette[k].r, Palette[k].g, Palette[k].b); }

        // swatch k: a 64 x 64 block in the top right corner of the atlas (texture coordinates, y up)
        static RectInt SwatchRect(int k, int W, int H) { return new RectInt(W - 8 * Swatch + (k % 8) * Swatch, H - Swatch * (k / 8 + 1), Swatch, Swatch); }
        public static Vector2 SwatchUV(int k, int W, int H) { var r = SwatchRect(k, W, H); return new Vector2((r.x + Swatch * .5f) / W, (r.y + Swatch * .5f) / H); }

        // The project atlas = the source sheet, the lens paint grown a few texels outward (the lens edge is now a mesh
        // edge and may sit just outside the painted blue) and the flat swatches.
        public static string PaintAtlas()
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false); image.LoadImage(File.ReadAllBytes(SourceAtlas));
            int W = image.width, H = image.height; var px = image.GetPixels32();
            // the glass edge in the sheet is an anti-aliased blend with the silver frame: those texels are dropped
            // (within 2 texels of non-glass paint and not deep blue) and refilled from the glass inside
            var lens = new bool[px.Length]; var frontier = new List<int>();
            for (int i = 0; i < px.Length; i++) lens[i] = IsLens(px[i]);
            var edge = new bool[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                if (!lens[i] || px[i].b - px[i].r > 90) continue;
                int x = i % W, y = i / W;
                for (int dy = -2; dy <= 2 && !edge[i]; dy++) for (int dx = -2; dx <= 2; dx++)
                    {
                        int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                        if (!lens[yy * W + xx]) { edge[i] = true; break; }
                    }
            }
            for (int i = 0; i < px.Length; i++) { if (edge[i]) lens[i] = false; else if (lens[i]) frontier.Add(i); }
            for (int step = 0; step < 8; step++)
            {
                var next = new List<int>();
                foreach (int i in frontier)
                {
                    int x = i % W, y = i / W;
                    for (int d = 0; d < 4; d++)
                    {
                        int xx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), yy = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue; int j = yy * W + xx;
                        if (lens[j]) continue; lens[j] = true; px[j] = px[i]; next.Add(j);
                    }
                }
                frontier = next;
            }
            for (int k = 0; k < Palette.Length; k++)
            {
                var r = SwatchRect(k, W, H);
                for (int y = r.y; y < r.yMax; y++) for (int x = r.x; x < r.xMax; x++) px[y * W + x] = Palette[k];
            }
            image.SetPixels32(px); image.Apply();
            string path = Equipment + "/Diver_Atlas.png";
            File.WriteAllBytes(path, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.maxTextureSize = 2048; importer.sRGBTexture = true; importer.mipmapEnabled = true; importer.anisoLevel = 4;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            return "atlas painted (" + W + "x" + H + ")";
        }

        public static string CutInPlace()
        {
            string report = "";
            foreach (string name in new[] { "Diver", "DiverHands" })
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Equipment + "/" + name + ".asset");
                report += name + ": " + Cut(mesh) + "; ";
                EditorUtility.SetDirty(mesh);
            }
            report += PaintAtlas();
            AssetDatabase.SaveAssets();
            return report;
        }

        struct Corner { public int tri, c; }

        public static string Cut(Mesh mesh)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false); image.LoadImage(File.ReadAllBytes(SourceAtlas));
            int W = image.width, H = image.height; var px = image.GetPixels32(); UnityEngine.Object.DestroyImmediate(image);
            Func<Vector2, int> labelAt = q => Classify(px[Mathf.Clamp((int)(q.y * H), 0, H - 1) * W + Mathf.Clamp((int)(q.x * W), 0, W - 1)]);

            var v = mesh.vertices; var n = mesh.normals; var uv = mesh.uv; var uv2 = mesh.uv2; var bw = mesh.boneWeights; var tris = mesh.triangles;
            bool hasUv2 = uv2.Length == v.Length; int sourceVerts = v.Length;
            int refined = Refine(ref v, ref n, ref uv, ref uv2, hasUv2, ref bw, ref tris, labelAt);
            int nv = v.Length, nt = tris.Length / 3;

            // the R51 glove texel (the whole glove past the wrist cut samples it): the most common exact UV
            var uvCount = new Dictionary<Vector2, int>(); Vector2 gloveUv = new Vector2(-1, -1); int gloveN = 0;
            foreach (var q in uv) { int c; uvCount.TryGetValue(q, out c); uvCount[q] = ++c; if (c > gloveN) { gloveN = c; gloveUv = q; } }
            if (gloveN < 50 || labelAt(gloveUv) != Glove) gloveUv = new Vector2(-1, -1);

            // per-corner colour votes: 15 samples per triangle (drawn in from the edges, away from island bleed)
            var cornerScore = new float[nt * 3, Labels]; var area = new float[nt]; var pure = new int[nt];
            var bary = new List<Vector3>();
            for (int i = 0; i <= 4; i++) for (int j = 0; j <= 4 - i; j++) bary.Add(new Vector3(i, j, 4 - i - j) / 4f);
            for (int t = 0; t < nt; t++)
            {
                int a = tris[3 * t], b = tris[3 * t + 1], c = tris[3 * t + 2];
                area[t] = Vector3.Cross(v[b] - v[a], v[c] - v[a]).magnitude * .5f;
                int first = -2;
                foreach (var b0 in bary)
                {
                    var w = b0 * .7f + Vector3.one * (.3f / 3);
                    int l = labelAt(uv[a] * w.x + uv[b] * w.y + uv[c] * w.z);
                    first = first == -2 ? l : first == l ? l : -1;
                    float s = area[t] + 1e-9f;
                    cornerScore[3 * t, l] += s * b0.x * b0.x; cornerScore[3 * t + 1, l] += s * b0.y * b0.y; cornerScore[3 * t + 2, l] += s * b0.z * b0.z;
                }
                pure[t] = first;
            }

            // weld by position (scores agree across UV seams), except the R51 wrist cut: its glove copies stay glove
            Func<Vector3, Vector3Int> key = p => new Vector3Int(Mathf.RoundToInt(p.x * 20000), Mathf.RoundToInt(p.y * 20000), Mathf.RoundToInt(p.z * 20000));
            var weld = new int[nv]; var map = new Dictionary<Vector3Int, int>(); var mapGlove = new Dictionary<Vector3Int, int>(); int nw = 0;
            var fixedGlove = new List<bool>();
            for (int i = 0; i < nv; i++)
            {
                bool g = uv[i] == gloveUv; var k = key(v[i]); var m = g ? mapGlove : map; int id;
                if (!m.TryGetValue(k, out id)) { id = nw++; m[k] = id; fixedGlove.Add(g); }
                weld[i] = id;
            }
            var score = new float[nw, Labels]; var vArea = new float[nw];
            for (int t = 0; t < nt; t++) for (int c = 0; c < 3; c++)
                {
                    int w = weld[tris[3 * t + c]]; vArea[w] += area[t] / 3;
                    for (int l = 0; l < Labels; l++) score[w, l] += cornerScore[3 * t + c, l];
                }
            Action<int> normalise = w =>
            {
                float s = 0; for (int l = 0; l < Labels; l++) s += score[w, l];
                if (s <= 0) { score[w, Glove] = 1; return; }
                for (int l = 0; l < Labels; l++) score[w, l] /= s;
            };
            for (int w = 0; w < nw; w++)
            {
                if (fixedGlove[w]) { for (int l = 0; l < Labels; l++) score[w, l] = l == Glove ? 1 : 0; continue; }
                normalise(w);
            }
            // neighbours over the welded surface
            var nb = new List<int>[nw]; for (int w = 0; w < nw; w++) nb[w] = new List<int>();
            for (int t = 0; t < nt; t++) for (int c = 0; c < 3; c++)
                {
                    int a = weld[tris[3 * t + c]], b = weld[tris[3 * t + (c + 1) % 3]];
                    if (a == b) continue; if (!nb[a].Contains(b)) nb[a].Add(b); if (!nb[b].Contains(a)) nb[b].Add(a);
                }
            // smooth: a Gaussian over the surface (sigma in metres, so dense and sparse parts of the mesh smooth alike)
            // rounds off the bake's saw teeth so the cut lines run clean
            var wpos = new Vector3[nw]; for (int i = 0; i < nv; i++) wpos[weld[i]] = v[i];
            {
                var next = new float[nw, Labels]; var stamp = new int[nw]; for (int w = 0; w < nw; w++) stamp[w] = -1;
                float reach = Sigma * 2.5f, inv = 1f / (2 * Sigma * Sigma); var queue = new List<int>();
                for (int w = 0; w < nw; w++)
                {
                    if (fixedGlove[w]) { for (int l = 0; l < Labels; l++) next[w, l] = score[w, l]; continue; }
                    queue.Clear(); queue.Add(w); stamp[w] = w; float total = 0;
                    for (int q = 0; q < queue.Count; q++)
                    {
                        int o = queue[q]; float d2 = (wpos[o] - wpos[w]).sqrMagnitude, g = Mathf.Exp(-d2 * inv) * (vArea[o] + 1e-9f);
                        total += g; for (int l = 0; l < Labels; l++) next[w, l] += g * score[o, l];
                        foreach (int x in nb[o]) if (stamp[x] != w && (wpos[x] - wpos[w]).sqrMagnitude < reach * reach) { stamp[x] = w; queue.Add(x); }
                    }
                    for (int l = 0; l < Labels; l++) next[w, l] /= total;
                }
                score = next;
            }
            // specks: a patch of a colour smaller than a few triangles gives way to its surroundings
            Func<int, int> win = w => { int best = 0; for (int l = 1; l < Labels; l++) if (score[w, l] > score[w, best]) best = l; return best; };
            int specks = 0;
            for (int pass = 0; pass < 3; pass++)
            {
                var seen = new bool[nw]; int changed = 0;
                for (int s0 = 0; s0 < nw; s0++)
                {
                    if (seen[s0] || fixedGlove[s0]) continue;
                    int lab = win(s0); var region = new List<int> { s0 }; seen[s0] = true; float ar = 0;
                    for (int q = 0; q < region.Count; q++)
                    {
                        int w = region[q]; ar += vArea[w];
                        foreach (int o in nb[w]) if (!seen[o] && !fixedGlove[o] && win(o) == lab) { seen[o] = true; region.Add(o); }
                    }
                    if (ar > SpeckArea) continue;
                    foreach (int w in region) { score[w, lab] = 0; normalise(w); }
                    changed++;
                }
                specks += changed; if (changed == 0) break;
            }

            // uv2 (metallic, smoothness) per colour: the most common value on that colour, for corners of another colour
            var uv2Mode = new Vector2[Labels];
            if (hasUv2)
                for (int l = 0; l < Labels; l++)
                {
                    var counts = new Dictionary<Vector2, int>(); int bestC = 0;
                    for (int t = 0; t < nt; t++)
                    {
                        if (pure[t] != l) continue;
                        var q = uv2[tris[3 * t]]; q = new Vector2(Mathf.Round(q.x * 100) / 100, Mathf.Round(q.y * 100) / 100);
                        int c; counts.TryGetValue(q, out c); counts[q] = ++c; if (c > bestC) { bestC = c; uv2Mode[l] = q; }
                    }
                }

            // cut every triangle along the lines where the winning colour changes
            var ov = new List<Vector3>(nv * 5 / 4); var on = new List<Vector3>(); var ouv = new List<Vector2>(); var ouv2 = new List<Vector2>(); var obw = new List<BoneWeight>();
            var ot = new List<int>(tris.Length * 5 / 4);
            var outMap = new Dictionary<string, int>();
            var labelTris = new int[Labels];
            var sw = new Vector2[Labels]; for (int l = 0; l < Palette.Length; l++) sw[l] = SwatchUV(l, W, H);
            var cand = new List<int>(); var poly = new List<Vector3>(); var clip = new List<Vector3>();
            for (int t = 0; t < nt; t++)
            {
                int[] ix = { tris[3 * t], tris[3 * t + 1], tris[3 * t + 2] };
                int[] wi = { weld[ix[0]], weld[ix[1]], weld[ix[2]] };
                int[] wl = { win(wi[0]), win(wi[1]), win(wi[2]) };
                cand.Clear();
                if (wl[0] == wl[1] && wl[1] == wl[2]) cand.Add(wl[0]);
                else
                    for (int l = 0; l < Labels; l++)
                        if (l == wl[0] || l == wl[1] || l == wl[2] || score[wi[0], l] > .05f || score[wi[1], l] > .05f || score[wi[2], l] > .05f) cand.Add(l);
                foreach (int k in cand)
                {
                    poly.Clear(); poly.Add(new Vector3(1, 0, 0)); poly.Add(new Vector3(0, 1, 0)); poly.Add(new Vector3(0, 0, 1));
                    if (cand.Count > 1)
                        foreach (int j in cand)
                        {
                            if (j == k || poly.Count < 3) continue;
                            float f0 = score[wi[0], k] - score[wi[0], j], f1 = score[wi[1], k] - score[wi[1], j], f2 = score[wi[2], k] - score[wi[2], j];
                            clip.Clear();
                            for (int e = 0; e < poly.Count; e++)
                            {
                                var P = poly[e]; var Q = poly[(e + 1) % poly.Count];
                                float fp = P.x * f0 + P.y * f1 + P.z * f2, fq = Q.x * f0 + Q.y * f1 + Q.z * f2;
                                bool ip = fp >= 0, iq = fq >= 0;
                                if (ip) clip.Add(P);
                                if (ip != iq) { float s = fp / (fp - fq); var X = P + (Q - P) * s; if (P.x == 0 && Q.x == 0) X.x = 0; if (P.y == 0 && Q.y == 0) X.y = 0; if (P.z == 0 && Q.z == 0) X.z = 0; clip.Add(X); }
                            }
                            var tmp = poly; poly = clip; clip = tmp;
                        }
                    if (poly.Count < 3) continue;
                    // drop pieces with no area
                    float pa = 0; for (int e = 1; e + 1 < poly.Count; e++) pa += Vector3.Cross(poly[e] - poly[0], poly[e + 1] - poly[0]).magnitude;
                    if (pa < 1e-6f) continue;
                    var idx = new int[poly.Count];
                    for (int e = 0; e < poly.Count; e++) idx[e] = Emit(poly[e], k, ix, wl, v, n, uv, uv2, hasUv2, uv2Mode, bw, sw, ov, on, ouv, ouv2, obw, outMap);
                    for (int e = 1; e + 1 < poly.Count; e++)
                    {
                        if (idx[0] == idx[e] || idx[e] == idx[e + 1] || idx[0] == idx[e + 1]) continue;
                        ot.Add(idx[0]); ot.Add(idx[e]); ot.Add(idx[e + 1]); labelTris[k]++;
                    }
                }
            }
            mesh.Clear(false);
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(ov); mesh.SetNormals(on); mesh.SetUVs(0, ouv); if (hasUv2) mesh.SetUVs(1, ouv2); mesh.boneWeights = obw.ToArray();
            mesh.SetTriangles(ot, 0); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            string perColour = ""; for (int l = 0; l < Labels; l++) perColour += labelTris[l] + (l + 1 < Labels ? "/" : "");
            return sourceVerts + " -> " + ov.Count + " vertices (" + refined + " edge splits), " + nt + " -> " + ot.Count / 3 + " triangles, " + specks + " specks, per colour " + perColour;
        }

        // One output vertex at barycentric b of the source triangle, painted colour k; shared between pieces of the same
        // colour at the same place (lens pieces also by their atlas UV).
        static int Emit(Vector3 b, int k, int[] ix, int[] wl, Vector3[] v, Vector3[] n, Vector2[] uv, Vector2[] uv2, bool hasUv2, Vector2[] uv2Mode, BoneWeight[] bw, Vector2[] sw,
            List<Vector3> ov, List<Vector3> on, List<Vector2> ouv, List<Vector2> ouv2, List<BoneWeight> obw, Dictionary<string, int> outMap)
        {
            var p = v[ix[0]] * b.x + v[ix[1]] * b.y + v[ix[2]] * b.z;
            if (b.x == 1) p = v[ix[0]]; else if (b.y == 1) p = v[ix[1]]; else if (b.z == 1) p = v[ix[2]];
            var q = uv[ix[0]] * b.x + uv[ix[1]] * b.y + uv[ix[2]] * b.z;
            string key = k + ":" + Mathf.RoundToInt(p.x * 200000) + "," + Mathf.RoundToInt(p.y * 200000) + "," + Mathf.RoundToInt(p.z * 200000);
            if (k == Lens) key += ":" + Mathf.RoundToInt(q.x * 65536) + "," + Mathf.RoundToInt(q.y * 65536);
            int id; if (outMap.TryGetValue(key, out id)) return id;
            ov.Add(p);
            var nn = n[ix[0]] * b.x + n[ix[1]] * b.y + n[ix[2]] * b.z; on.Add(nn.sqrMagnitude > 1e-12f ? nn.normalized : n[ix[0]]);
            ouv.Add(k == Lens ? q : sw[k]);
            if (hasUv2)
            {
                var m = Vector2.zero; float[] bb = { b.x, b.y, b.z };
                for (int c = 0; c < 3; c++) m += bb[c] * (wl[c] == k ? uv2[ix[c]] : uv2Mode[k]);
                ouv2.Add(m);
            }
            obw.Add(Blend(bw[ix[0]], bw[ix[1]], bw[ix[2]], b));
            id = ov.Count - 1; outMap[key] = id; return id;
        }

        // Long triangles that a colour boundary crosses (the fins are long slivers) would draw the boundary as saw
        // teeth: their edges are split at the midpoint (flat, the shape does not change) until short enough, so the
        // paint can be read and cut at a finer step there. Seam copies split alike (same midpoint position).
        static int Refine(ref Vector3[] va, ref Vector3[] na, ref Vector2[] uva, ref Vector2[] uv2a, bool hasUv2, ref BoneWeight[] bwa, ref int[] ta, Func<Vector2, int> labelAt)
        {
            var v = new List<Vector3>(va); var n = new List<Vector3>(na); var uv = new List<Vector2>(uva); var uv2 = new List<Vector2>(uv2a); var bw = new List<BoneWeight>(bwa);
            var tris = new List<int>(ta); int splits = 0;
            Func<Vector3, Vector3Int> key = p => new Vector3Int(Mathf.RoundToInt(p.x * 20000), Mathf.RoundToInt(p.y * 20000), Mathf.RoundToInt(p.z * 20000));
            Vector3[] probe = { new Vector3(.8f, .1f, .1f), new Vector3(.1f, .8f, .1f), new Vector3(.1f, .1f, .8f), new Vector3(.45f, .45f, .1f), new Vector3(.1f, .45f, .45f), new Vector3(.45f, .1f, .45f), Vector3.one / 3 };
            for (int round = 0; round < 4; round++)
            {
                var pid = new Dictionary<Vector3Int, int>(); var id = new int[v.Count];
                for (int i = 0; i < v.Count; i++) { var k = key(v[i]); int q; if (!pid.TryGetValue(k, out q)) { q = pid.Count; pid[k] = q; } id[i] = q; }
                Func<int, int, long> ek = (a, b) => { int x = id[a], y = id[b]; return x < y ? ((long)x << 32) | (uint)y : ((long)y << 32) | (uint)x; };
                var marked = new HashSet<long>();
                for (int t = 0; t < tris.Count; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    int first = -1; bool mixed = false;
                    foreach (var w in probe) { int l = labelAt(uv[a] * w.x + uv[b] * w.y + uv[c] * w.z); if (first < 0) first = l; else if (l != first) { mixed = true; break; } }
                    if (!mixed) continue;
                    if ((v[a] - v[b]).magnitude > SplitLength) marked.Add(ek(a, b));
                    if ((v[b] - v[c]).magnitude > SplitLength) marked.Add(ek(b, c));
                    if ((v[c] - v[a]).magnitude > SplitLength) marked.Add(ek(c, a));
                }
                if (marked.Count == 0) break;
                var mids = new Dictionary<long, int>();
                Func<int, int, int> mid = (a, b) =>
                {
                    long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; int m;
                    if (mids.TryGetValue(k, out m)) return m;
                    v.Add((v[a] + v[b]) * .5f); n.Add((n[a] + n[b]).normalized); uv.Add((uv[a] + uv[b]) * .5f); if (hasUv2) uv2.Add((uv2[a] + uv2[b]) * .5f);
                    bw.Add(Blend(bw[a], bw[b], bw[a], new Vector3(.5f, .5f, 0)));
                    m = v.Count - 1; mids[k] = m; splits++; return m;
                };
                var output = new List<int>(tris.Count * 2);
                for (int t = 0; t < tris.Count; t += 3)
                {
                    int[] c = { tris[t], tris[t + 1], tris[t + 2] };
                    bool[] m = { marked.Contains(ek(c[0], c[1])), marked.Contains(ek(c[1], c[2])), marked.Contains(ek(c[2], c[0])) };
                    int count = (m[0] ? 1 : 0) + (m[1] ? 1 : 0) + (m[2] ? 1 : 0);
                    if (count == 0) { output.Add(c[0]); output.Add(c[1]); output.Add(c[2]); continue; }
                    int rot = 0;
                    if (count == 1) rot = m[0] ? 0 : m[1] ? 1 : 2;
                    else if (count == 2) rot = !m[2] ? 0 : !m[0] ? 1 : 2;
                    int A = c[rot], B = c[(rot + 1) % 3], C = c[(rot + 2) % 3];
                    if (count == 1) { int ab = mid(A, B); output.AddRange(new[] { A, ab, C, ab, B, C }); }
                    else if (count == 2)
                    {
                        int ab = mid(A, B), bc = mid(B, C);
                        output.AddRange(new[] { ab, B, bc });
                        if ((v[A] - v[bc]).sqrMagnitude < (v[ab] - v[C]).sqrMagnitude) output.AddRange(new[] { A, ab, bc, A, bc, C });
                        else output.AddRange(new[] { A, ab, C, ab, bc, C });
                    }
                    else { int ab = mid(A, B), bc = mid(B, C), ca = mid(C, A); output.AddRange(new[] { A, ab, ca, ab, B, bc, ca, bc, C, ab, bc, ca }); }
                }
                tris = output;
            }
            va = v.ToArray(); na = n.ToArray(); uva = uv.ToArray(); uv2a = hasUv2 ? uv2.ToArray() : uv2a; bwa = bw.ToArray(); ta = tris.ToArray();
            return splits;
        }

        static BoneWeight Blend(BoneWeight a, BoneWeight b, BoneWeight c, Vector3 t)
        {
            var w = new Dictionary<int, float>();
            Action<int, float> add = (i, x) => { if (x <= 0) return; float s; w.TryGetValue(i, out s); w[i] = s + x; };
            BoneWeight[] src = { a, b, c }; float[] f = { t.x, t.y, t.z };
            for (int s = 0; s < 3; s++)
            {
                if (f[s] <= 0) continue; var q = src[s];
                add(q.boneIndex0, q.weight0 * f[s]); add(q.boneIndex1, q.weight1 * f[s]); add(q.boneIndex2, q.weight2 * f[s]); add(q.boneIndex3, q.weight3 * f[s]);
            }
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

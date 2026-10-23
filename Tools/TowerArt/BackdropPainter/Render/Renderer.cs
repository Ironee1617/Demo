using System.Numerics;

namespace BackdropPainter.Render;

/// <summary>Sun, sky ambient, haze and fog for one layer. Colours are linear radiance.</summary>
public sealed class Lighting
{
    public Vector3 SunDir = Vector3.Normalize(new Vector3(-.5f, .7f, -.5f));
    public Vector3 Sun = new(3, 2.8f, 2.5f);
    public Vector3 SkyZenith = new(.35f, .5f, .8f), SkyHorizon = new(.6f, .65f, .7f), Bounce = new(.25f, .22f, .18f);
    public float Ambient = 1;
    /// <summary>Sky colour by image height; null leaves misses transparent.</summary>
    public Func<float, float, Vector3> Background;
    /// <summary>Aerial perspective colour by image height.</summary>
    public Func<float, Vector3> Haze = _ => new Vector3(.7f, .75f, .8f);
    /// <summary>Extra viewing distance this layer sits behind the play field, and how fast haze thickens with it.</summary>
    public float Distance, HazeDensity = .01f, HazeMax = 1;
    public float FogBase = -100, FogFalloff = 1, FogDensity;
    public Vector3 FogColor = new(.8f);
    public float Exposure = 1, ShadowK = 12, ShadowReach = 16, AoScale = 1;
    public float BloomThreshold = 1.4f, BloomStrength;
    public float Saturation = 1;
    public Vector3 Tint = Vector3.One;

    public Vector3 SkyLight(Vector3 n)
    {
        float up = n.Y * .5f + .5f;
        var sky = R.Mix(SkyHorizon, SkyZenith, R.Clamp01(n.Y));
        return R.Mix(Bounce, sky, R.Smooth(0, 1, up)) * Ambient;
    }
}

/// <summary>Orthographic camera tilted slightly downward so floors and steps show their tops.</summary>
public sealed class Camera
{
    public readonly float Pitch, Back, MaxDist;
    public readonly Vector3 Forward, Up;
    public Camera(float pitchDegrees = 4, float back = 80, float maxDist = 260)
    {
        Pitch = pitchDegrees * MathF.PI / 180; Back = back; MaxDist = maxDist;
        Forward = new Vector3(0, -MathF.Sin(Pitch), MathF.Cos(Pitch));
        Up = new Vector3(0, MathF.Cos(Pitch), MathF.Sin(Pitch));
    }
    public Vector3 Origin(float x, float v) => new Vector3(x, 0, 0) + Up * v - Forward * Back;
    /// <summary>World y that appears at image height <paramref name="v"/> for a point at depth <paramref name="z"/>.</summary>
    public float YFor(float v, float z) => (v - z * MathF.Sin(Pitch)) / MathF.Cos(Pitch);
}

/// <summary>
/// Sphere-traces a <see cref="Scene"/> into a horizontally repeating layer: soft shadows, ambient occlusion,
/// specular and translucent foliage, water reflections, volumetric media, aerial perspective, adaptive
/// supersampling on silhouettes, bloom and ACES tone mapping.
/// </summary>
public sealed class Renderer
{
    readonly Scene scene; readonly Lighting light; readonly Camera cam;
    public int Samples = 3;
    public float MarchEpsilon = .0015f;

    public Renderer(Scene scene, Lighting light, Camera cam) { this.scene = scene; this.light = light; this.cam = cam; }

    struct Px { public Vector3 C; public float A, Depth; }

    public void Render(Canvas canvas)
    {
        int W = canvas.W, H = canvas.H; float ppu = canvas.Ppu;
        var buf = new Px[W * H];
        var primary = new int[W][]; var wide = new int[W][]; var volumes = new int[W][];
        for (int i = 0; i < W; i++)
        {
            float x = (i + .5f) / ppu;
            primary[i] = scene.Candidates(x, .6f);
            wide[i] = scene.Candidates(x, light.ShadowReach + .6f);
            volumes[i] = scene.VolumeCandidates(x);
        }
        Parallel.For(0, H, j =>
        {
            for (int i = 0; i < W; i++)
            {
                float x = (i + .5f) / ppu, v = canvas.Bottom + (j + .5f) / ppu;
                buf[j * W + i] = Sample(x, v, primary[i], wide[i], volumes[i]);
            }
        });
        // Supersample silhouettes, depth steps and high-contrast texels.
        var refine = new bool[W * H];
        Parallel.For(0, H, j =>
        {
            for (int i = 0; i < W; i++)
            {
                var c = buf[j * W + i]; float lc = R.Luma(c.C);
                for (int dj = -1; dj <= 1 && !refine[j * W + i]; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        int jj = j + dj; if (jj < 0 || jj >= H) continue;
                        int ii = (i + di + W) % W; var o = buf[jj * W + ii];
                        if (MathF.Abs(o.A - c.A) > .3f || MathF.Abs(o.Depth - c.Depth) > .35f || MathF.Abs(R.Luma(o.C) - lc) > .18f * MathF.Max(.25f, lc))
                        { refine[j * W + i] = true; break; }
                    }
            }
        });
        int n = Math.Max(1, Samples);
        Parallel.For(0, H, j =>
        {
            for (int i = 0; i < W; i++)
            {
                if (!refine[j * W + i]) continue;
                Vector3 c = Vector3.Zero; float a = 0;
                for (int sy = 0; sy < n; sy++)
                    for (int sx = 0; sx < n; sx++)
                    {
                        float jx = (sx + .5f + (R.Hash01(i, j, sx * 7 + sy, 3) - .5f) * .6f) / n;
                        float jy = (sy + .5f + (R.Hash01(i, j, sx * 7 + sy, 4) - .5f) * .6f) / n;
                        var s = Sample((i + jx) / ppu, canvas.Bottom + (j + jy) / ppu, primary[i], wide[i], volumes[i]);
                        c += s.C; a += s.A;
                    }
                buf[j * W + i] = new Px { C = c / (n * n), A = a / (n * n), Depth = buf[j * W + i].Depth };
            }
        });
        Finish(canvas, buf);
    }

    void Finish(Canvas canvas, Px[] buf)
    {
        int W = canvas.W, H = canvas.H;
        var hdr = new Vector3[W * H];
        for (int k = 0; k < hdr.Length; k++) hdr[k] = buf[k].C;
        if (light.BloomStrength > 0) Bloom(hdr, buf, W, H, canvas.Ppu);
        Parallel.For(0, H, j =>
        {
            for (int i = 0; i < W; i++)
            {
                int k = j * W + i; float a = R.Clamp01(buf[k].A);
                if (a <= .002f) { canvas.P[k] = Col.Clear; continue; }
                var c = hdr[k] / MathF.Max(a, 1e-4f);
                c = R.Saturate(c * light.Exposure * light.Tint, light.Saturation);
                c = R.Aces(c);
                // Sub-quantum noise keeps smooth gradients from banding after 8-bit export.
                float g = (R.Hash01(i, j, 0, 77) - .5f) / 255f;
                canvas.P[k] = new Col(R.Clamp01(R.ToSrgb(c.X) + g), R.Clamp01(R.ToSrgb(c.Y) + g), R.Clamp01(R.ToSrgb(c.Z) + g), a);
            }
        });
    }

    void Bloom(Vector3[] hdr, Px[] buf, int W, int H, float ppu)
    {
        int w = (W + 3) / 4, h = (H + 3) / 4;
        var small = new Vector3[w * h];
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                Vector3 s = Vector3.Zero; int cnt = 0;
                for (int dj = 0; dj < 4; dj++)
                    for (int di = 0; di < 4; di++)
                    {
                        int jj = j * 4 + dj, ii = i * 4 + di; if (jj >= H || ii >= W) continue;
                        var c = hdr[jj * W + ii] * light.Exposure; float l = R.Luma(c);
                        if (l > light.BloomThreshold) s += c * ((l - light.BloomThreshold) / l);
                        cnt++;
                    }
                small[j * w + i] = cnt > 0 ? s / cnt : Vector3.Zero;
            }
        int radius = Math.Max(2, (int)(ppu * .6f / 4));
        var tmp = new Vector3[w * h];
        float[] kern = new float[radius * 2 + 1]; float ks = 0;
        for (int k = -radius; k <= radius; k++) { kern[k + radius] = MathF.Exp(-k * k / (radius * radius * .35f)); ks += kern[k + radius]; }
        for (int k = 0; k < kern.Length; k++) kern[k] /= ks;
        for (int pass = 0; pass < 2; pass++)
        {
            Parallel.For(0, h, j =>
            {
                for (int i = 0; i < w; i++)
                {
                    Vector3 s = Vector3.Zero;
                    for (int k = -radius; k <= radius; k++) s += small[j * w + ((i + k) % w + w) % w] * kern[k + radius];
                    tmp[j * w + i] = s;
                }
            });
            Parallel.For(0, h, j =>
            {
                for (int i = 0; i < w; i++)
                {
                    Vector3 s = Vector3.Zero;
                    for (int k = -radius; k <= radius; k++) { int jj = Math.Clamp(j + k, 0, h - 1); s += tmp[jj * w + i] * kern[k + radius]; }
                    small[j * w + i] = s;
                }
            });
        }
        Parallel.For(0, H, j =>
        {
            for (int i = 0; i < W; i++)
            {
                // Bilinear upsample so the glow has no block edges.
                float fx = (i + .5f) / 4 - .5f, fy = (j + .5f) / 4 - .5f;
                int x0 = (int)MathF.Floor(fx), y0 = Math.Clamp((int)MathF.Floor(fy), 0, h - 1), y1 = Math.Min(h - 1, y0 + 1);
                float tx = fx - x0, ty = R.Clamp01(fy - y0);
                int xa = ((x0 % w) + w) % w, xb = (xa + 1) % w;
                var top = R.Mix(small[y0 * w + xa], small[y0 * w + xb], tx); var bottom = R.Mix(small[y1 * w + xa], small[y1 * w + xb], tx);
                var b = R.Mix(top, bottom, ty) / light.Exposure * light.BloomStrength;
                hdr[j * W + i] += b * MathF.Max(buf[j * W + i].A, R.Clamp01(R.Luma(b) * 4));
                buf[j * W + i].A = MathF.Max(buf[j * W + i].A, R.Clamp01(R.Luma(b * light.Exposure) * 2));
            }
        });
    }

    Px Sample(float x, float v, int[] cand, int[] wide, int[] vols)
    {
        var ro = cam.Origin(x, v); var rd = cam.Forward;
        float t = 0, tHit = -1; int mat = -1;
        for (int step = 0; step < 220 && t < cam.MaxDist; step++)
        {
            float d = scene.Map(ro + rd * t, cand, out int m);
            if (d < MarchEpsilon * (1 + t * .02f)) { tHit = t; mat = m; break; }
            t += d * .9f;
        }
        Vector3 col = Vector3.Zero; float alpha = 0, depth = 1e4f;
        if (tHit >= 0)
        {
            var p = ro + rd * tHit;
            col = Shade(p, rd, mat, cand, wide, v);
            col = Atmosphere(col, p, v, tHit);
            alpha = 1; depth = tHit;
        }
        else if (light.Background != null) { col = light.Background(x, v); alpha = 1; }
        if (vols.Length > 0) Media(ro, rd, tHit >= 0 ? tHit : cam.MaxDist, vols, ref col, ref alpha, v);
        return new Px { C = col * (alpha > 0 ? 1 : 0), A = alpha, Depth = depth };
    }

    Vector3 Atmosphere(Vector3 col, Vector3 p, float v, float t)
    {
        float depth = light.Distance + p.Z;
        float haze = MathF.Min(light.HazeMax, 1 - MathF.Exp(-light.HazeDensity * MathF.Max(0, depth)));
        col = R.Mix(col, light.Haze(v), haze);
        if (light.FogDensity > 0)
        {
            float fog = R.Clamp01(light.FogDensity * MathF.Exp(-(p.Y - light.FogBase) / light.FogFalloff));
            col = R.Mix(col, light.FogColor, fog);
        }
        return col;
    }

    public Vector3 Normal(Vector3 p, int[] cand)
    {
        const float e = .0015f;
        var k1 = new Vector3(1, -1, -1); var k2 = new Vector3(-1, -1, 1); var k3 = new Vector3(-1, 1, -1); var k4 = new Vector3(1, 1, 1);
        var n = k1 * scene.Map(p + k1 * e, cand) + k2 * scene.Map(p + k2 * e, cand) + k3 * scene.Map(p + k3 * e, cand) + k4 * scene.Map(p + k4 * e, cand);
        float l = n.Length();
        return l > 0 ? n / l : Vector3.UnitY;
    }

    float SoftShadow(Vector3 ro, Vector3 rd, int[] cand)
    {
        float res = 1, t = .02f, ph = 1e10f;
        for (int i = 0; i < 96 && t < light.ShadowReach; i++)
        {
            float h = scene.Map(ro + rd * t, cand);
            if (h < .0008f) return 0;
            // Improved penumbra estimate; when the distance jumps (bounding-box steps) y can exceed h and would
            // zero the light, so it is held just below h.
            float y = MathF.Min(h * h / (2 * ph), h * .99f), d = MathF.Sqrt(MathF.Max(0, h * h - y * y));
            res = MathF.Min(res, light.ShadowK * d / MathF.Max(.0001f, t - y));
            ph = h; t += R.Clamp01(h) * .9f + .01f;
        }
        res = R.Clamp01(res);
        return res * res * (3 - 2 * res);
    }

    float Occlusion(Vector3 p, Vector3 n, int[] cand)
    {
        float occ = 0, sca = 1;
        for (int i = 0; i < 5; i++)
        {
            float h = (.02f + .16f * i / 4f) * light.AoScale;
            occ += (h - scene.Map(p + n * h, cand)) * sca;
            sca *= .85f;
        }
        return R.Clamp01(1 - 2.2f * occ / light.AoScale);
    }

    /// <summary>Diagnostics for one image point: hit, material, normals, sun term, shadow and occlusion.</summary>
    public string Probe(float x, float v)
    {
        var cand = scene.Candidates(x, .6f); var wide = scene.Candidates(x, light.ShadowReach + .6f);
        var ro = cam.Origin(x, v); var rd = cam.Forward; float t = 0; int mat = -1; bool hit = false;
        for (int step = 0; step < 220 && t < cam.MaxDist; step++)
        {
            float d = scene.Map(ro + rd * t, cand, out int m);
            if (d < MarchEpsilon * (1 + t * .02f)) { mat = m; hit = true; break; }
            t += d * .9f;
        }
        if (!hit) return "miss";
        var p = ro + rd * t; var n = Normal(p, cand);
        var s = new Surface { Albedo = new Vector3(.5f), Rough = .8f, Spec = .04f, N = n, Ao = 1 };
        scene.GetMaterial(mat)?.Invoke(p, ref s);
        float shadow = SoftShadow(p + n * .01f, light.SunDir, wide), ao = Occlusion(p, n, cand);
        string blocker = "";
        float st = .02f; var so = p + n * .01f;
        for (int i = 0; i < 96 && st < light.ShadowReach; i++)
        {
            float h = scene.Map(so + light.SunDir * st, wide, out int bm);
            if (h < .0008f) { blocker = $" blocked at t={st:F3} q={so + light.SunDir * st} mat={bm}"; break; }
            st += R.Clamp01(h) * .9f + .01f;
        }
        return $"p={p} mat={mat} n={n} sN={s.N} albedo={s.Albedo} ndl={Vector3.Dot(Vector3.Normalize(s.N), light.SunDir):F3} shadow={shadow:F3} ao={ao:F3}{blocker}";
    }

    Vector3 Shade(Vector3 p, Vector3 rd, int mat, int[] cand, int[] wide, float v)
    {
        var n = Normal(p, cand);
        var s = new Surface { Albedo = new Vector3(.5f), Rough = .8f, Spec = .04f, N = n, Ao = 1 };
        scene.GetMaterial(mat)?.Invoke(p, ref s);
        var N = Vector3.Normalize(s.N);
        if (Vector3.Dot(N, rd) > 0) N = Vector3.Normalize(N - rd * Vector3.Dot(N, rd) * 1.01f);
        var L = light.SunDir;
        float ndl = Vector3.Dot(N, L);
        float shadow = ndl > -.3f ? SoftShadow(p + n * .01f, L, wide) : 0;
        float ao = Occlusion(p, n, cand) * s.Ao;
        if (s.Water) return Water(p, rd, N, s, shadow, wide, v);
        Vector3 diffuse;
        if (s.Trans > 0)
        {
            // Foliage: wrapped diffuse plus light passing through backlit leaves.
            float wrap = R.Clamp01((ndl + .45f) / 1.45f);
            float through = MathF.Pow(R.Clamp01(Vector3.Dot(rd, L) * .5f + .5f), 3) * s.Trans;
            diffuse = s.Albedo * (light.Sun * (wrap * (1 - s.Trans * .4f) + through) * shadow + light.SkyLight(N) * ao);
        }
        else diffuse = s.Albedo * (light.Sun * MathF.Max(ndl, 0) * shadow + light.SkyLight(N) * ao);
        var H = Vector3.Normalize(L - rd);
        float ndh = MathF.Max(Vector3.Dot(N, H), 0), vdh = MathF.Max(Vector3.Dot(-rd, H), 0);
        float rough = MathF.Max(.04f, s.Rough), a2 = rough * rough * rough * rough;
        float dd = ndh * ndh * (a2 - 1) + 1, D = a2 / (MathF.PI * dd * dd);
        var F0 = R.Mix(new Vector3(s.Spec), s.Albedo, s.Metal);
        var F = F0 + (Vector3.One - F0) * MathF.Pow(1 - vdh, 5);
        var spec = light.Sun * F * (D * .25f * MathF.Max(ndl, 0) * shadow);
        // Metals and glossy stone also mirror a little of the sky.
        var refl = Vector3.Reflect(rd, N);
        float fres = MathF.Pow(1 - MathF.Max(Vector3.Dot(-rd, N), 0), 5);
        var env = light.SkyLight(refl) * (F0 + (Vector3.One - F0) * fres) * (1 - rough) * ao;
        return diffuse * (1 - s.Metal) + spec + env + s.Emit;
    }

    Vector3 Water(Vector3 p, Vector3 rd, Vector3 N, Surface s, float shadow, int[] wide, float v)
    {
        var refl = Vector3.Reflect(rd, N);
        float cos = MathF.Max(Vector3.Dot(-rd, N), 0), fres = .02f + .98f * MathF.Pow(1 - cos, 5);
        fres = MathF.Max(fres, .25f);
        Vector3 reflected;
        // Trace the mirrored ray through the scene; when it escapes, the sky above the water mirrors in.
        float t = .03f; bool hit = false;
        for (int i = 0; i < 90 && t < 40; i++)
        {
            float d = scene.Map(p + refl * t, wide, out int m);
            if (d < .002f && m >= 0 && m != -1) { hit = true; break; }
            t += MathF.Max(d * .9f, .01f);
        }
        if (hit)
        {
            var q = p + refl * t; var nq = Normal(q, wide);
            var sq = new Surface { Albedo = new Vector3(.5f), Rough = .8f, Spec = .04f, N = nq, Ao = 1 };
            scene.Map(q, wide, out int mq); scene.GetMaterial(mq)?.Invoke(q, ref sq);
            if (sq.Water) reflected = light.SkyLight(refl);
            else reflected = sq.Albedo * (light.Sun * MathF.Max(Vector3.Dot(sq.N, light.SunDir), 0) * .6f + light.SkyLight(sq.N)) + sq.Emit;
            reflected = R.Mix(reflected, light.Haze(v), .25f);
        }
        else
        {
            // The escaped mirror ray shows the sky at the image height where it leaves the scene.
            float escapeV = Vector3.Dot(p + refl * t, cam.Up);
            reflected = light.Background != null ? light.Background(p.X, escapeV) : light.SkyLight(refl);
        }
        var body = s.Albedo * light.SkyLight(Vector3.UnitY) * .35f + s.Albedo * light.Sun * .08f * shadow;
        var H = Vector3.Normalize(light.SunDir - rd);
        float glint = MathF.Pow(MathF.Max(Vector3.Dot(N, H), 0), 400) * 30 * shadow;
        return R.Mix(body, reflected, fres) + light.Sun * glint + s.Emit;
    }

    void Media(Vector3 ro, Vector3 rd, float tEnd, int[] vols, ref Vector3 col, ref float alpha, float v)
    {
        float T = 1; Vector3 acc = Vector3.Zero;
        foreach (int vi in vols)
        {
            var vol = scene.Volumes[vi];
            var lo = scene.Local(ro, vol.Cx);
            // Ray/box overlap along the march direction.
            float t0 = 0, t1 = tEnd;
            for (int axis = 1; axis < 3; axis++)
            {
                float o = axis == 1 ? lo.Y : lo.Z, d = axis == 1 ? rd.Y : rd.Z, mn = axis == 1 ? vol.Min.Y : vol.Min.Z, mx = axis == 1 ? vol.Max.Y : vol.Max.Z;
                if (MathF.Abs(d) < 1e-6f) { if (o < mn || o > mx) { t0 = 1; t1 = 0; } continue; }
                float a = (mn - o) / d, b = (mx - o) / d; if (a > b) (a, b) = (b, a);
                t0 = MathF.Max(t0, a); t1 = MathF.Min(t1, b);
            }
            if (lo.X < vol.Min.X || lo.X > vol.Max.X || t1 <= t0) continue;
            float dt = (t1 - t0) / vol.Steps;
            float jitter = R.Hash01((int)(ro.X * 977), (int)(v * 977), vi, 5);
            for (int k = 0; k < vol.Steps && T > .01f; k++)
            {
                float tt = t0 + (k + jitter) * dt;
                var q = lo + rd * tt;
                float dens = vol.Density(q);
                if (dens <= 0) continue;
                float lightDepth = 0, ls = vol.LightReach / vol.LightSteps; var dl = vol.Coarse ?? vol.Density;
                for (int l = 1; l <= vol.LightSteps; l++) lightDepth += MathF.Max(0, dl(q + light.SunDir * (l * ls))) * ls;
                float sunT = MathF.Exp(-lightDepth) * .7f + MathF.Exp(-lightDepth * .25f) * .3f;
                float cosT = Vector3.Dot(rd, light.SunDir), g = vol.Anisotropy;
                float phase = (1 - g * g) / (4 * MathF.PI * MathF.Pow(1 + g * g - 2 * g * cosT, 1.5f)) * 4 * MathF.PI;
                float powder = 1 - MathF.Exp(-dens * 2);
                var scatter = vol.Albedo * (light.Sun * sunT * phase * (.5f + .5f * powder) + light.SkyLight(Vector3.UnitY) * vol.Ambient * (.55f + .45f * R.Clamp01((q.Y - vol.Min.Y) / (vol.Max.Y - vol.Min.Y)))) + (vol.EmitAt != null ? vol.EmitAt(q) : vol.Emit);
                float ext = MathF.Exp(-dens * dt);
                acc += T * scatter * (1 - ext);
                T *= ext;
            }
        }
        if (T >= .999f) return;
        var hazed = R.Mix(acc, light.Haze(v) * (1 - T), R.Clamp01(1 - MathF.Exp(-light.HazeDensity * light.Distance)) * .7f);
        col = hazed + col * T;
        alpha = 1 - T * (1 - alpha);
    }
}

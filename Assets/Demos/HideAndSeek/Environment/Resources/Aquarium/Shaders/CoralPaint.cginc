// R63: painted patterns for the reef's corals and sea stars, shared by "Aquarium/Living Reef" and
// "Aquarium/Lobby Diorama". The pattern is picked per vertex by the colour's alpha (code = floor(a * 32),
// written by AquariumEnvironment.PaintCoral / SeaStar); any other alpha (old meshes, shells, kelp) is left as is.
//   1 staghorn polyps    2 scroll / plate ridges   3 sea fan speckles   4 brain grooves   5 finger polyps
//   6 tube bands, pores  7 sea star, light dots    8 sea star, dark dots 9 brittle star bands
//  10 bubble polyps     11 cushion star            12 table coral ribs and polyps
// Everything is computed from the tissue uv (along/around a part) and the world position: no textures.
#ifndef AQUARIUM_CORAL_PAINT
#define AQUARIUM_CORAL_PAINT

float CP_Hash(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453); }
// Distance to the one jittered polyp centre of this cell (cell units, 0 at the centre).
float CP_Cell(float3 p)
{
    float3 c = floor(p), f = frac(p) - .5;
    float3 j = float3(CP_Hash(c), CP_Hash(c + 17.1), CP_Hash(c + 31.7)) - .5;
    return length(f - j * .36);
}
// Polyps: a darker ring round a lighter, softly glowing centre.
void CP_Polyps(float3 p, float freq, float size, float fade, inout float3 albedo, inout float glow)
{
    float d = CP_Cell(p * freq);
    float ring = (1 - smoothstep(size, size + .07, d)) * smoothstep(size * .45, size * .45 + .07, d);
    float core = 1 - smoothstep(size * .35, size * .35 + .06, d);
    albedo = lerp(albedo, albedo * .62, ring * .75 * fade);
    albedo = lerp(albedo, saturate(albedo * 1.25 + float3(.10, .09, .07)), core * fade);
    glow += core * fade;
}
// Returns the pattern's extra glow (multiplied by the albedo and _Fluoro by the caller).
float CoralPaint(float alpha, float2 uv, float3 wp, float distanceFade, inout float3 albedo)
{
    // Screen-space change of the across-arm coordinate: large where it wraps between two arms (and on far stars).
    float fwL = fwidth(uv.y);
    int code = (int)floor(alpha * 32 + .001);
    if (code < 1 || code > 12) return 0;
    float fade = distanceFade, glow = 0;
    if (code == 1 || code == 5)
    {
        CP_Polyps(wp, code == 1 ? 46 : 38, .26, fade, albedo, glow);
    }
    else if (code == 2)
    {
        float t = uv.x, a = uv.y;
        float ridge = sin(t * 44 + sin(a * 5) * .8);
        float rib = pow(abs(sin(a * 13)), 10) * smoothstep(.15, .5, t);
        albedo *= lerp(1, .74, smoothstep(.45, 1, ridge) * fade);
        albedo = lerp(albedo, saturate(albedo * 1.3 + .06), rib * .6 * fade);
        albedo = lerp(albedo, saturate(albedo * 1.25 + .08), smoothstep(.86, .99, t) * fade);
    }
    else if (code == 3)
    {
        float d = CP_Cell(wp * 70);
        float dotm = 1 - smoothstep(.17, .24, d);
        albedo = lerp(albedo, saturate(albedo * 1.45 + float3(.12, .1, .08)), dotm * .8 * fade);
        glow += dotm * .5 * fade;
    }
    else if (code == 4)
    {
        float3 q = wp * 33;
        float m = sin(q.x + 1.7 * sin(q.z * .9 + q.y * .6)) + sin(q.z * 1.1 + 1.5 * sin(q.x * .8 - q.y * .5)) + .6 * sin(q.y * 1.3 + q.x * .4);
        float groove = 1 - smoothstep(.07, .24, abs(m));
        float3 deep = lerp(albedo * .42, albedo.gbr * .55, .35);
        albedo = lerp(albedo, deep, groove * fade);
        albedo = lerp(albedo, saturate(albedo * 1.18 + .04), smoothstep(.75, 1.4, abs(m)) * fade);
    }
    else if (code == 6)
    {
        float band = smoothstep(.55, 1, sin(uv.y * 25.1));
        albedo *= lerp(1, .8, band * fade);
        float d = CP_Cell(wp * 52);
        albedo *= 1 - .4 * (1 - smoothstep(.13, .19, d)) * fade;
    }
    else if (code == 7 || code == 8 || code == 11)
    {
        // Sea stars: uv.x = 0 at the centre to 1 at the arm tip (8 dot steps), uv.y = across the arm in the same
        // steps (99 = underside). Tubercles sit on a staggered grid, so wide arms get more rows than thin ones.
        float under = abs(uv.y) < 50 ? 1 : 0;
        float t = uv.x, L = uv.y, a = t * 8;
        float row = floor(L + .5), off = abs(row) % 2 * .5;
        float d = length(float2(frac(a + off) - .5, L - row));
        float dotm = (1 - smoothstep(.2, .28, d)) * smoothstep(.13, .2, t) * (1 - smoothstep(.9, .98, t)) * under * (1 - smoothstep(.3, .6, fwL));
        float3 light = saturate(albedo * .55 + float3(.48, .44, .36));
        if (code == 8) albedo = lerp(albedo, albedo * .42, dotm * fade);
        else albedo = lerp(albedo, light, dotm * fade);
        // Centre motif: a pale ring round a darker eye, and (cushion stars) a darker line down every arm.
        float ring = smoothstep(.07, .10, t) * (1 - smoothstep(.15, .18, t)) * under;
        albedo = lerp(albedo, light, ring * .85 * fade);
        albedo = lerp(albedo, albedo * .55, (1 - smoothstep(.035, .055, t)) * under * fade);
        if (code == 11) albedo = lerp(albedo, albedo * .66, (1 - smoothstep(.22, .45, abs(L))) * smoothstep(.2, .3, t) * (1 - smoothstep(.85, .95, t)) * under * fade);
        glow += dotm * .25 * fade;
    }
    else if (code == 9)
    {
        float under = abs(uv.y) < 50 ? 1 : 0;
        float t = uv.x;
        float band = smoothstep(.2, .65, sin(t * 44)) * smoothstep(.24, .32, t);
        albedo = lerp(albedo, albedo * .5, band * under * fade);
        float motif = (1 - smoothstep(.2, .45, abs(uv.y))) * (1 - smoothstep(.18, .24, t)) * smoothstep(.04, .07, t);
        albedo = lerp(albedo, saturate(albedo * .55 + float3(.45, .42, .36)), motif * under * fade);
    }
    else if (code == 10)
    {
        CP_Polyps(wp, 30, .3, fade, albedo, glow);
        glow += .35 * fade;
    }
    else if (code == 12)
    {
        float rib = smoothstep(.4, 1, sin(uv.x * 175.9));
        albedo *= lerp(1, .8, rib * smoothstep(.02, .2, uv.y) * fade);
        CP_Polyps(wp, 44, .22, fade * .8, albedo, glow);
    }
    return glow;
}
#endif

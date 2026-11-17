// R85: the tank's shared water current. Everything that moves with the water asks here, so seaweed, anemones,
// the suspended specks and the bubbles all lean the same way: "Aquarium/Living Reef", "Aquarium/Lobby Diorama",
// "Aquarium/Water Motes" and "Aquarium/Water Bubbles" (AquariumWaterLife.cs mirrors AqCurrentDir for the drift).
// Tuning lives in this file: the numbers marked TUNE.
#ifndef AQUARIUM_CURRENT
#define AQUARIUM_CURRENT
float4 _AqPush[4];    // swimmers the plants lean away from: world xyz, w = strength 0..1 (AquariumWaterLife)
float _AqSwayCheap;   // 1 in the fast graphics mode: no flutter, no leaning away
// The current's heading wanders slowly over about a quarter turn each way (radians, on the xz plane).
float2 AqCurrentDir(float t) { float a = .6 + .55 * sin(t * .043) + .35 * sin(t * .071 + 1.3); return float2(cos(a), sin(a)); }
// Displacement of one vertex of a plant or a tentacle.
//  wp     world position at rest
//  along  0 at the root to 1 at the tip of a tentacle (tissue uv.y; only used for soft tissue)
//  w      anchored bend weight (uv2.x: 0 at the root; taller plants carry larger weights)
//  phase  per plant / per tentacle phase (uv2.y)
//  sway   the material's _Sway, soft the material's _Soft
float3 AquariumSway(float3 wp, float along, float w, float phase, float sway, float soft, float t)
{
    float amp = sway * w;
    float2 dir = AqCurrentDir(t), side = float2(-dir.y, dir.x);
    along = saturate(along);
    // The tip answers later than the root: a wave travels up the blade.
    float lag = soft > .5 ? along * 1.5 : 1.45 * sqrt(abs(w));                                   // TUNE tip lag (radians)
    // A slow swell that sweeps across the tank with the current, in gusts; the plant leans with it.
    float swell = sin(t * .80 - dot(wp.xz, dir) * .38 + phase * .6 - lag);                        // TUNE swell speed .80, wavelength
    float gust = .62 + .38 * sin(t * .21 - dot(wp.xz, side) * .16 + phase * .3);
    float cross = sin(t * .55 + phase * 1.7 - lag * 1.25 + wp.x * .29);
    float2 xz = dir * ((.30 + .70 * swell) * gust * .30) + side * (cross * .12);                  // TUNE lean .30, swell reach .30 m, sideways .12 m per unit weight
    float gain = 1 + soft * .7;                                                                   // TUNE anemones move this much more
    float3 d = float3(xz.x, 0, xz.y) * amp * gain;
    if (soft > .5)
    {
        // Tentacles curl: a wave runs from the base to the tip, each tentacle on its own heading and beat.
        float a = phase * 7.31, curl = sin(t * 1.7 + phase * 5.3 - along * 5.2);                  // TUNE curl speed 1.7
        d.xz += float2(cos(a), sin(a)) * (curl * amp * .26 * along);                              // TUNE curl reach .26
        d.y += cos(t * 1.3 + phase * 4.1 - along * 4.4) * amp * .15 * along;
    }
    if (_AqSwayCheap < .5)
    {
        // A smaller, quicker flutter on top.
        float f = sin(t * 2.7 + phase * 3.1 - lag * 3.4 + wp.y * 2.1), g = cos(t * 2.1 + phase * 2.3 - lag * 2.6);
        d.xz += (side * (f * .045) + dir * (g * .025)) * (amp * (1 - soft * .5));                 // TUNE flutter .045 m
        // Lean away from a swimmer passing close (1.4 m).
        for (int i = 0; i < 4; i++)
        {
            float3 r = wp - _AqPush[i].xyz;
            float k = saturate(1 - dot(r, r) / 1.96) * _AqPush[i].w; k *= k;
            d.xz += r.xz / (length(r.xz) + .08) * (k * min(abs(amp) * gain, .9) * .45);            // TUNE push .45 m
        }
    }
    // Bending, not stretching: the blade dips as it leans.
    float reach = dot(d.xz, d.xz);
    d.y -= min(reach * .5 / (.5 + 1.6 * abs(w)), sqrt(reach) * .4);
    return d;
}
#endif

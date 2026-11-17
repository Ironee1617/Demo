// R85: ambient bubbles (AquariumWaterLife.cs). Camera-facing quads animated here from the shared reef clock:
//  kind 0  a bubble of a stream: released in bursts from a spot on the sand, a rock or a coral, wobbling and
//          growing as it rises, gone at the surface;
//  kind 1  the little ring it leaves on the surface (a flat quad just under the water plane);
//  kind 2  a bubble shed by a fast swimmer (48 slots written by the game, _AqTrail).
// Same pearl look as the aeration vents (rim, a small highlight); nothing is HDR.
Shader "Aquarium/Water Bubbles"
{
    Properties { _Color ("Tint", Color) = (.73,.96,1,.62) }
    SubShader
    {
        Tags { "Queue"="Transparent+6" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "AquariumCurrent.cginc"
            float4 _Color; float4 _AqTrail[48]; float4 _AqWater;
            float _AquariumPreviewTime, _AquariumPreviewTimeEnabled;
            // vertex: where the stream starts (world); uv: corner; d1: kind, seed, radius, rise speed;
            // d2: birth within the burst, cycle length, cycle offset, surface height (kind 2: x = slot)
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 d1 : TEXCOORD1; float4 d2 : TEXCOORD2; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 k : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                float t = lerp(_Time.y, _AquariumPreviewTime, _AquariumPreviewTimeEnabled);
                float kind = v.d1.x, seed = v.d1.y, size = v.d1.z, speed = v.d1.w, fade = 1;
                float3 wp; bool lying = false;
                if (kind < 1.5)
                {
                    float top = v.d2.w, tau = fmod(t + v.d2.z, v.d2.y) - v.d2.x;
                    float T = max(.1, (top - v.vertex.y) / speed), tt = clamp(tau, 0, T), u = tt / T;
                    float wob = .02 + .06 * u; float2 dir = AqCurrentDir(t);
                    wp = v.vertex.xyz + float3(0, speed * tt, 0);
                    wp.x += sin(tt * 4.3 + seed * 37) * wob + dir.x * .02 * tt;
                    wp.z += cos(tt * 3.4 + seed * 19) * wob + dir.y * .02 * tt;
                    if (kind < .5) { fade = step(0, tau) * step(tau, T) * saturate(tau * 5); size *= 1 + .7 * u; }
                    else { float age = tau - T; fade = step(0, age) * saturate(1 - age / .8) * .5; size *= 2 + 8 * saturate(age / .8); wp.y = top - .012; lying = true; }
                }
                else
                {
                    float4 b = _AqTrail[(int)(v.d2.x + .5)]; float age = t - b.w;
                    fade = step(0, age) * saturate(1 - age / 1.7) * saturate(age * 8) * step(.5, b.w);
                    float spread = saturate(age * 2.5);
                    wp = b.xyz + float3((seed - .5) * .5 * spread + sin(age * 6 + seed * 30) * .04, age * (.35 + .3 * speed) + (speed - .5) * .2 * spread, (frac(seed * 7.3) - .5) * .5 * spread + cos(age * 5 + seed * 17) * .04);
                    size *= 1 + age * .35;
                    fade *= saturate((_AqWater.x - .05 - wp.y) * 4);
                }
                float dist = distance(wp, _WorldSpaceCameraPos);
                fade *= saturate(exp2(-unity_FogParams.y * dist));
                if (lying) { wp.xz += v.uv * size; o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1)); }
                else
                {
                    float3 view = mul(UNITY_MATRIX_V, float4(wp, 1)).xyz;
                    float pixel = 2 * max(.01, -view.z) / (abs(UNITY_MATRIX_P[1][1]) * _ScreenParams.y), least = pixel * 1.1;
                    fade *= saturate(size / least); size = max(size, least);
                    view.xy += v.uv * size;
                    o.pos = mul(UNITY_MATRIX_P, float4(view, 1));
                }
                o.uv = v.uv; o.k = float2(kind, fade * _AqWater.y);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float r = length(i.uv), a; float3 col = _Color.rgb;
                if (i.k.x > .5 && i.k.x < 1.5) a = smoothstep(.55, .82, r) * smoothstep(1, .88, r) * .6;
                else
                {
                    float rim = smoothstep(.5, .9, r) * smoothstep(1, .93, r);
                    float hl = saturate(1 - length(i.uv - float2(-.36, .4)) * 3), low = saturate(1 - length(i.uv - float2(.3, -.42)) * 3.4);
                    a = .07 * step(r, 1) + rim * .85 + hl * hl * 1.2 + low * low * .35;
                    col = min(col + hl * hl * .35, .95);
                }
                return fixed4(col, saturate(a * _Color.a * i.k.y));
            }
            ENDCG
        }
    }
    FallBack Off
}

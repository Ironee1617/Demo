// R85: suspended matter in the tank's water (AquariumWaterLife.cs). One static mesh of camera-facing quads that
// wraps round the camera inside a box, so the cost is the same wherever the camera is: fine marine-snow specks
// that drift with the current and catch the light shafts, and a few larger bits that come and go (an algae
// strand, a little cluster of specks, a food crumb, a tiny bubble). Nothing here is HDR: colours stay under .82.
Shader "Aquarium/Water Motes"
{
    Properties { _Caustics ("Refracted light", 2D) = "black" {} _Tint ("Speck tint", Color) = (.86,.95,.93,1) _Strength ("Opacity", Float) = 1 }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "AquariumCurrent.cginc"
            sampler2D _Caustics; float4 _Tint; float _Strength;
            float4 _AqDrift;   // xyz: how far the current has carried the water so far
            float4 _AqWater;   // x: water surface height, y: overall opacity, z: half width of the box, w: half height
            float _AquariumPreviewTime, _AquariumPreviewTimeEnabled;
            // vertex: a seed in 0..1 cubed; uv: the quad corner; data: kind, size (m), two random numbers
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 data : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 tint : TEXCOORD1; float2 kind : TEXCOORD2; };
            v2f vert(appdata v)
            {
                v2f o;
                float t = lerp(_Time.y, _AquariumPreviewTime, _AquariumPreviewTimeEnabled);
                float kind = v.data.x, size = v.data.y, r1 = v.data.z, r2 = v.data.w;
                float3 S = float3(_AqWater.z, _AqWater.w, _AqWater.z) * 2;
                float3 p = v.vertex.xyz * S + _AqDrift.xyz * (.55 + .9 * r1);
                // specks hang or settle very slowly, strands and clusters settle, crumbs sink, tiny bubbles rise
                float rise = kind < .5 ? -.006 * (r2 - .3) : kind < 1.5 ? -.012 : kind < 2.5 ? -.008 : kind < 3.5 ? -.035 : .2;
                p.y += rise * t;
                p += float3(sin(t * .61 + r1 * 40), sin(t * .47 + r2 * 23) * .6, cos(t * .53 + r1 * 31)) * (.035 + .03 * r2);
                float3 rel = p - _WorldSpaceCameraPos; rel -= S * floor(rel / S + .5);
                float3 wp = _WorldSpaceCameraPos + rel;
                float3 e = 1 - abs(rel) / (S * .5);
                float dist = length(rel);
                float fade = smoothstep(0, .25, min(e.x, min(e.y, e.z)));
                // never on the lens: specks fade inside 0.9 m, the larger bits inside 1.6 m
                fade *= kind < .5 ? smoothstep(.3, .9, dist) : smoothstep(.7, 1.6, dist);
                fade *= saturate((_AqWater.x - .05 - wp.y) * 3) * step(-3, wp.y);
                // the larger bits come and go
                if (kind > .5) fade *= smoothstep(.45, .75, sin(t * (.045 + .035 * r2) + r1 * 43));
                // murk and distance swallow them like everything else
                fade *= saturate(exp2(-unity_FogParams.y * dist));
                // light: the same refracted network as the sand, followed back up the slanted shafts
                float depth = _AqWater.x - wp.y;
                float2 lp = wp.xz + float2(.325, .246) * depth;
                float2 cuv = lp * .18 + float2(sin(lp.y * .4 + t * .32), cos(lp.x * .3 + t * .24)) * .025;
                float a = tex2Dlod(_Caustics, float4(cuv + t * float2(.013, .009), 0, 2)).r, b = tex2Dlod(_Caustics, float4(cuv * 1.31 + t * float2(-.011, .016), 0, 2)).r;
                float beam = saturate(min(a, b) * .9 + max(a, b) * .35);
                float light = .78 + .9 * beam;
                // never thinner than about a pixel: a far speck dims instead of flickering
                float3 view = mul(UNITY_MATRIX_V, float4(wp, 1)).xyz;
                float pixel = 2 * max(.01, -view.z) / (abs(UNITY_MATRIX_P[1][1]) * _ScreenParams.y);
                float least = pixel * 1.3;
                fade *= saturate(size / least); size = max(size, least);
                float2 c = v.uv;
                if (kind > .5 && kind < 1.5)
                {
                    // an algae strand: long and thin, slowly turning and tumbling
                    float ang = r1 * 6.2832 + t * (.12 + .2 * r2) * (r1 > .5 ? 1 : -1), cs = cos(ang), sn = sin(ang);
                    c.y *= .3 + .7 * abs(cos(t * .31 + r2 * 9));
                    c = float2(c.x * cs - c.y * .45 * sn, c.x * sn + c.y * .45 * cs);
                }
                view.xy += c * size;
                o.pos = mul(UNITY_MATRIX_P, float4(view, 1));
                o.uv = v.uv; o.kind = float2(kind, r1);
                float3 col = _Tint.rgb * light; float alpha = (.28 + .40 * r2) * (.55 + .45 * beam);
                if (kind > 3.5) { col = float3(.8, .96, 1) * (.7 + .4 * beam); alpha = .6; }
                else if (kind > 2.5) { col = float3(.70, .50, .30) * (.6 + .5 * beam); alpha = .85; }
                else if (kind > 1.5) { alpha = .5; }
                else if (kind > .5) { col = float3(.30, .48, .22) * (.65 + .5 * beam); alpha = .7; }
                o.tint = float4(min(col, .82), alpha * fade * _AqWater.y * _Strength);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv; float r = length(uv), kind = i.kind.x, rnd = i.kind.y, a;
                float3 col = i.tint.rgb;
                if (kind < .5) a = smoothstep(1, .2, r);
                else if (kind < 1.5)
                {
                    float y = uv.y + sin(uv.x * 3.2 + rnd * 20) * .35 + sin(uv.x * 7.1 + rnd * 5) * .12;
                    a = smoothstep(.42, .12, abs(y)) * smoothstep(1, .6, abs(uv.x)) * (.75 + .25 * sin(uv.x * 9 + rnd * 3));
                }
                else if (kind < 2.5)
                {
                    a = 0;
                    for (int n = 0; n < 6; n++)
                    {
                        float2 c = float2(sin(rnd * 31 + n * 2.4), cos(rnd * 17 + n * 3.7)) * .62;
                        float d = length(uv - c) / (.16 + .07 * sin(n * 1.9 + rnd * 9));
                        a = max(a, saturate(1 - d));
                    }
                }
                else if (kind < 3.5)
                {
                    float ang = atan2(uv.y, uv.x), rr = r / (.72 + .16 * sin(ang * 3 + rnd * 12) + .08 * sin(ang * 7 + rnd * 4));
                    a = smoothstep(1, .7, rr); col *= .8 + .3 * saturate(uv.y * .5 + .5);
                }
                else
                {
                    float rim = smoothstep(.55, .9, r) * smoothstep(1, .93, r), hl = saturate(1 - length(uv - float2(-.35, .38)) * 3.2);
                    a = rim * .75 + hl * hl * .9 + .06 * step(r, 1);
                }
                return fixed4(col, saturate(i.tint.a * a));
            }
            ENDCG
        }
    }
    FallBack Off
}

// R56b: the character-select diorama (AquariumEnvironment.LobbyBackdrop). Art-directed rather than physically lit so
// the backdrop always reads as a calm, glowing aquarium behind the characters, whatever the stage lights do:
//  * soft wrap light from the surface + a cool sky/ground ambient, a cyan rim, a little extra saturation;
//  * two scrolling caustic layers on every upward face;
//  * a warm pool of light that follows the picked character (_Spot, set from the preview);
//  * depth haze into the water colour (darker low, brighter toward the surface), so the reef layers fade back;
//  * _Background draws the water wall itself: the same water colour, a soft sun glow and surface ripples.
Shader "Aquarium/Lobby Diorama"
{
    Properties
    {
        _Caustics ("Refracted light", 2D) = "black" {}
        _Sway ("Current strength", Float) = 0
        _Background ("Water wall", Float) = 0
        _Saturation ("Saturation", Float) = 1.3
        _Moss ("Moss on top faces", Float) = 0
        _Ripple ("Sand ripples", Float) = 0
        _Glow ("Self glow", Float) = 0
        _Spot ("Light pool (stage xz, strength)", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _Caustics; float _Sway, _Background, _Saturation, _Moss, _Ripple, _Glow; float4 _Spot;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR; float2 uv:TEXCOORD0; float2 data:TEXCOORD1; };
            struct v2f { float4 pos:SV_POSITION; float4 color:COLOR; float3 obj:TEXCOORD0; float3 normal:TEXCOORD1; float3 world:TEXCOORD2; float2 uv:TEXCOORD3; };

            float3 WaterAt(float y)
            {
                float h = saturate((y + 1.0) / 6.5);
                float3 deep = float3(.02, .11, .19), mid = float3(.05, .36, .50), shallow = float3(.40, .84, .90);
                return lerp(lerp(deep, mid, saturate(h * 1.8)), shallow, saturate(h * 1.8 - .8));
            }
            v2f vert(appdata v)
            {
                float t = _Time.y, weight = v.data.x, phase = v.data.y;
                float2 wave = float2(sin(t * .91 + phase + v.vertex.y * .58) * .20 + sin(t * 1.43 + phase) * .035, cos(t * .72 + phase + v.vertex.y * .41) * .13);
                v.vertex.xz += _Sway * weight * wave;
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; o.obj = v.vertex.xyz;
                o.normal = UnityObjectToWorldNormal(v.normal); o.world = mul(unity_ObjectToWorld, v.vertex).xyz; o.uv = v.uv; return o;
            }
            fixed4 frag(v2f i, float face : VFACE) : SV_Target
            {
                float t = _Time.y;
                if (_Background > .5)
                {
                    float3 c = WaterAt(i.obj.y);
                    float2 s = float2((i.obj.x) / 8.0, (i.obj.y - 4.6) / 2.6);
                    float sun = exp(-dot(s, s));
                    c += float3(.55, .88, .92) * sun * .55;
                    float band = smoothstep(1.5, 4.5, i.obj.y);
                    c += band * .035 * sin(i.obj.x * 2.3 + sin(i.obj.x * .7 + t * .5) * 2.5 + t * .7);
                    c += band * .025 * sin(i.obj.x * 3.9 - t * .9 + sin(i.obj.y * 2.0 + t * .3));
                    return fixed4(c, 1);
                }
                float3 n = normalize(i.normal) * (face >= 0 ? 1 : -1);
                float3 albedo = i.color.rgb;
                float grey = dot(albedo, float3(.3, .59, .11)); albedo = saturate(lerp(grey.xxx, albedo, _Saturation));
                if (_Moss > 0)
                {
                    float m = smoothstep(.35, .85, n.y) * _Moss * (.55 + .45 * sin(i.obj.x * 3.1 + i.obj.z * 2.3 + sin(i.obj.y * 5.0)));
                    albedo = lerp(albedo, float3(.30, .56, .38), saturate(m));
                }
                if (_Ripple > 0) albedo *= 1 + _Ripple * .07 * sin(i.obj.x * 5.0 + sin(i.obj.z * 1.7) * 2.2 + i.obj.z * 1.3);
                float3 L = normalize(float3(-.30, .88, .38));
                float wrap = saturate((dot(n, L) + .4) / 1.4);
                float3 ambient = lerp(float3(.07, .17, .25), float3(.42, .72, .82), n.y * .5 + .5);
                float2 cuv = i.obj.xz * .21 + float2(sin(i.obj.z * .4 + t * .32), cos(i.obj.x * .3 + t * .24)) * .02;
                float ca = tex2D(_Caustics, cuv + t * float2(.020, .013)).r, cb = tex2D(_Caustics, cuv * 1.37 + t * float2(-.016, .021)).r;
                float caustic = (min(ca, cb) * 1.5 + max(ca, cb) * .3) * saturate(n.y * 1.3 + .05);
                float3 lit = albedo * (ambient * .6 + float3(1, .95, .84) * wrap * .78) + albedo * caustic * float3(.55, .95, 1.0) * .85;
                float2 d = i.obj.xz - _Spot.xy; float pool = exp(-dot(d, d) / 1.4) * _Spot.z * saturate(n.y * .8 + .25);
                lit += albedo * pool * float3(1, .9, .72) * .85;
                float3 V = normalize(_WorldSpaceCameraPos - i.world);
                lit += pow(1 - saturate(abs(dot(n, V))), 3) * float3(.20, .62, .72) * .45;
                lit += albedo * _Glow;
                float dist = distance(_WorldSpaceCameraPos, i.world);
                float haze = 1 - exp(-max(0, dist - 8.0) * .095);
                lit = lerp(lit, WaterAt(i.obj.y), saturate(haze));
                return fixed4(lit, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}

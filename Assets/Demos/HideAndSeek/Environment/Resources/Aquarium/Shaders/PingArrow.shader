// R73h: the aliens' ping arrow and its ring (AquariumGamePing3D). Unlit, glowing in the pinger's alien colour with a
// bright rim and a band of light sweeping down the arrow; drawn faintly through rocks and the tube (first pass), and
// fully where it is in plain view (second pass). No fog, so a far ping still reads.
Shader "Aquarium/Ping Arrow"
{
    Properties
    {
        _Color ("Alien colour", Color) = (1,1,1,1)
        _Fade ("Fade", Float) = 1
        _Ring ("Ring mode", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+60" "RenderType"="Transparent" "IgnoreProjector"="True" }
        CGINCLUDE
        #include "UnityCG.cginc"
        float4 _Color; float _Fade, _Ring;
        struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
        struct v2f { float4 pos:SV_POSITION; float3 n:TEXCOORD0; float3 v:TEXCOORD1; float2 uv:TEXCOORD2; float h:TEXCOORD3; };
        v2f vert(appdata i)
        {
            v2f o; o.pos = UnityObjectToClipPos(i.vertex); o.n = UnityObjectToWorldNormal(i.normal);
            o.v = normalize(_WorldSpaceCameraPos - mul(unity_ObjectToWorld, i.vertex).xyz); o.uv = i.uv; o.h = i.vertex.y; return o;
        }
        float4 shade(v2f i, float strength)
        {
            float3 c = _Color.rgb;
            if (_Ring > .5)
            {
                // uv.x: 0 inner edge .. 1 outer edge of the ring
                float a = smoothstep(0, .35, i.uv.x) * (1 - smoothstep(.55, 1, i.uv.x));
                return float4(lerp(c, 1, .35) * 1.6, a * _Fade * strength);
            }
            float rim = pow(1 - saturate(abs(dot(normalize(i.n), i.v))), 2.2);
            float sweep = frac(-i.h * .9 + _Time.y * 1.4);
            float band = smoothstep(.0, .08, sweep) * (1 - smoothstep(.08, .2, sweep));
            float3 col = c * (.75 + .25 * saturate(i.n.y * .5 + .5)) + lerp(c, 1, .6) * rim * 1.3 + band * lerp(c, 1, .7) * .9;
            return float4(col * 1.25, (.82 + .18 * rim) * _Fade * strength);
        }
        ENDCG
        // behind something: a faint ghost
        Pass
        {
            ZWrite Off ZTest Greater Cull Back Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target { return shade(i, .3); }
            ENDCG
        }
        // in plain view
        Pass
        {
            ZWrite Off ZTest LEqual Cull Back Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target { return shade(i, 1); }
            ENDCG
        }
    }
    FallBack Off
}

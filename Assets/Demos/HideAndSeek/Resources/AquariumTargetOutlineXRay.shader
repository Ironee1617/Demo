// R29: a rim drawn round a part over everything in front of it (the alien's own body included), so the
// screws near an alien can always be found. Pass 1 stamps the part's silhouette into two stencil bits;
// pass 2 lays a faint glow over the parts of it that are hidden behind something (once per pixel);
// pass 3 draws a screen-space widened shell only outside the silhouette (once per pixel).
// _ClipBelow hides the part of the mesh below a local height (a screw's shank sunk in the lid).
Shader "Aquarium/TargetOutlineXRay"
{
    Properties
    {
        _OutlineColor ("Outline color", Color) = (0.55, 0.97, 1, 0.9)
        _OutlinePixels ("Outline width (pixels at 1080p)", Range(0, 8)) = 2.5
        _GhostAlpha ("Hidden fill opacity", Range(0, 1)) = 0.28
        _ClipBelow ("Hide the mesh below this local height", Float) = -1000
    }
    SubShader
    {
        Tags { "Queue"="Overlay+10" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" }
        CGINCLUDE
        #include "UnityCG.cginc"
        float4 _OutlineColor;
        float _OutlinePixels, _ClipBelow, _GhostAlpha;
        struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
        struct v2f { float4 position : SV_POSITION; float height : TEXCOORD0; };
        v2f Plain(appdata v)
        {
            v2f o; o.position = UnityObjectToClipPos(v.vertex); o.height = v.vertex.y; return o;
        }
        // Pulled 1.5 cm toward the camera so the part's own visible faces never pass the "behind" test.
        v2f Near(appdata v)
        {
            v2f o; float3 view = UnityObjectToViewPos(v.vertex);
            view -= normalize(view) * 0.015;
            o.position = mul(UNITY_MATRIX_P, float4(view, 1)); o.height = v.vertex.y; return o;
        }
        v2f Shell(appdata v)
        {
            v2f o; o.position = UnityObjectToClipPos(v.vertex); o.height = v.vertex.y;
            float3 viewNormal = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
            float2 toward = mul((float2x2)UNITY_MATRIX_P, viewNormal.xy) * _ScreenParams.xy;
            float len = length(toward);
            if (len > 1e-5)
            {
                float pixels = _OutlinePixels * _ScreenParams.y / 1080.0;
                o.position.xy += toward / len * pixels * 2.0 / _ScreenParams.xy * o.position.w;
            }
            return o;
        }
        fixed4 Mark(v2f i) : SV_Target { clip(i.height - _ClipBelow); return 0; }
        fixed4 Ghost(v2f i) : SV_Target { clip(i.height - _ClipBelow); return fixed4(_OutlineColor.rgb, _OutlineColor.a * _GhostAlpha); }
        fixed4 Rim(v2f i) : SV_Target { clip(i.height - _ClipBelow); return _OutlineColor; }
        ENDCG
        Pass
        {
            ZWrite Off ZTest Always Cull Off ColorMask 0
            Stencil { Ref 96 WriteMask 96 Comp Always Pass Replace }
            CGPROGRAM
            #pragma vertex Plain
            #pragma fragment Mark
            ENDCG
        }
        Pass
        {
            // Only where the part is behind something (its own visible faces fail the test).
            ZWrite Off ZTest Greater Cull Back Blend SrcAlpha OneMinusSrcAlpha
            Stencil { Ref 64 ReadMask 64 WriteMask 64 Comp Equal Pass Zero }
            CGPROGRAM
            #pragma vertex Near
            #pragma fragment Ghost
            ENDCG
        }
        Pass
        {
            ZWrite Off ZTest Always Cull Off Blend SrcAlpha OneMinusSrcAlpha
            Stencil { Ref 32 ReadMask 32 WriteMask 32 Comp NotEqual Pass Replace }
            CGPROGRAM
            #pragma vertex Shell
            #pragma fragment Rim
            ENDCG
        }
    }
    Fallback Off
}

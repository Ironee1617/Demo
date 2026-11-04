// Painted light for the tower's combat effects, in the look of hand-painted action-game effects.
// The sprite's alpha (times its brightest channel) is read as a light level. Its outer edge is cut crisp (the silhouette
// of a drawn stroke), and inside it the level runs through a painted colour ramp: a deep saturated edge, the tint, a hot
// pale band and a white core. The ramp is smooth with a hint of brush-blended steps, so strokes keep their gradients and
// texture instead of turning into flat posterised shapes.
// Vertex rgb carries the tint already scaled by its strength; vertex alpha is the fade, which erodes the shape from
// its thin edge inward (the ramp shrinks with it) instead of dimming the whole sprite evenly.
// The colour is painted partly over the backdrop (premultiplied, _Opacity of the strength, most in the coloured body and
// least in the white core, which reads as added light), so strokes still read on a pale sky.
Shader "PrototypeLab/Tower/EffectToon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Erode ("Fade erosion", Range(0, 1)) = 0.8
        _Opacity ("Paint opacity", Range(0, 1)) = 0.58
        _Steps ("Brush steps", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };

            sampler2D _MainTex;
            float4 _Color;
            float _Erode, _Opacity, _Steps;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 t = tex2D(_MainTex, i.texcoord);
                float peak = max(t.r, max(t.g, t.b));
                float level = t.a * peak;
                float strength = max(i.color.r, max(i.color.g, i.color.b));
                float3 hue = (i.color.rgb / max(strength, 0.0001)) * (t.rgb / max(peak, 0.0001));
                float fade = saturate(i.color.a);
                float cut = (1 - fade) * _Erode, span = max(1 - cut, 0.08);
                float l = (level - cut) / span;
                float w = fwidth(level) / span * 0.8 + 0.005;
                float cover = smoothstep(0.05 - w, 0.05 + w, l);
                // The ramp position (from the stroke's own level, so an eroding stroke keeps its colours and thins to its
                // bright spine instead of growing a deep-coloured rim), nudged toward four brush-blended steps.
                float g = saturate((level - 0.05) / 0.95), s = g * 4, f = floor(s);
                g = lerp(g, (f + smoothstep(0.3, 0.7, s - f)) / 4, _Steps);
                float3 c = lerp(hue * hue * 0.85, hue, smoothstep(0, 0.35, g));
                c = lerp(c, lerp(hue, float3(1, 1, 1), 0.5), smoothstep(0.45, 0.75, g));
                c = lerp(c, lerp(hue, float3(1, 1, 1), 0.95), smoothstep(0.78, 0.95, g));
                float dim = saturate(fade * 2.5) * cover;
                return float4(c * strength * (0.85 + 0.3 * g) * dim, _Opacity * saturate(strength) * (1 - 0.5 * g) * dim);
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}

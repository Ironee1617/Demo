// Painted ink for the tower's smoke, debris and dark contrast layers: the sprite's alpha is cut at a brushed edge (firm,
// with a short soft falloff), so smoke reads as painted silhouettes. Vertex alpha both thins the shape (the cut rises
// as it fades, eroding the thin edges first) and lowers its opacity; the sprite's own colours keep their baked shading.
Shader "PrototypeLab/Tower/EffectInk"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
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
                float a = saturate(i.color.a);
                float cut = 0.22 + (1 - a) * 0.5;
                float w = fwidth(t.a) * 0.7 + 0.04;
                float cover = smoothstep(cut - w, cut + w, t.a);
                return float4(t.rgb * i.color.rgb, cover * pow(a, 0.6));
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}

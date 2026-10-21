Shader "PrototypeLab/MythModel"
{
    Properties
    {
        _MainTex ("Original pixel artwork", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 mode : TEXCOORD1; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float2 mode : TEXCOORD1; fixed4 color : COLOR; };
            sampler2D _MainTex;
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv;
                o.mode = v.mode; o.color = v.color * _Color; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 texel = i.mode.x > .5 ? fixed4(1,1,1,1) : tex2D(_MainTex, i.uv);
                fixed4 c = texel * i.color;
                // A restrained contrast lift keeps faces and equipment readable
                // at gameplay scale without smoothing the painted pixel palette.
                c.rgb = saturate((c.rgb - .22) * 1.035 + .22);
                clip(c.a - .015); return c;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}

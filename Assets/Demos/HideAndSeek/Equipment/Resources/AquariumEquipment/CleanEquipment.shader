Shader "Aquarium/Clean Equipment"
{
    Properties
    {
        _Color("Part colour", Color) = (1,1,1,1)
        _Metallic("Metallic", Range(0,1)) = 0
        _Glossiness("Smoothness", Range(0,1)) = .4
        _Detail("Fine fabric grain", Range(0,.1)) = 0
        _MainTex("UV reference", 2D) = "white" {}
        _SurfaceTex("Metal / smoothness / fabric", 2D) = "white" {}
        _UseAtlas("Painted UV atlas", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 250
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        fixed4 _Color;
        half _Metallic, _Glossiness, _Detail;
        half _UseAtlas;
        sampler2D _MainTex, _SurfaceTex;
        struct Input { float2 uv_MainTex; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_MainTex * 280;
            half3 surface = tex2D(_SurfaceTex, IN.uv_MainTex).rgb;
            float detail = lerp(_Detail, surface.b * .1, _UseAtlas) * (1 - saturate(length(fwidth(uv))));
            o.Albedo = _Color.rgb * lerp(half3(1,1,1), tex2D(_MainTex, IN.uv_MainTex).rgb, _UseAtlas);
            o.Metallic = lerp(_Metallic, surface.r, _UseAtlas);
            o.Smoothness = lerp(_Glossiness, surface.g, _UseAtlas);
            o.Normal = normalize(float3(sin(uv.x * 6.283) * detail, sin(uv.y * 6.283) * detail, 1));
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}

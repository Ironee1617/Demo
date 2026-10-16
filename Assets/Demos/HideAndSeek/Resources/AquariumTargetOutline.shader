Shader "Aquarium/TargetOutline"
{
    Properties
    {
        _OutlineColor ("Outline color", Color) = (1, 0.86, 0.25, 1)
        _OutlineWidth ("Outline width", Range(0, 0.1)) = 0.035
    }
    SubShader
    {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" }
        Pass
        {
            Cull Front
            ZWrite Off
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _OutlineColor;
            float _OutlineWidth;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 position : SV_POSITION; };
            v2f vert(appdata input)
            {
                v2f output;
                float3 worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                float3 worldNormal = UnityObjectToWorldNormal(input.normal);
                output.position = UnityWorldToClipPos(worldPosition + worldNormal * _OutlineWidth);
                return output;
            }
            fixed4 frag(v2f input) : SV_Target { return _OutlineColor; }
            ENDCG
        }
    }
    Fallback Off
}

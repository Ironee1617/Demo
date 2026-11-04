Shader "Aquarium/Gallery Finish"
{
    Properties { _MainTex("Source",2D)="white" {} _Bloom("Bloom",2D)="black" {} _Underwater("Submerged",Float)=0 }
    SubShader {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex,_Bloom;
        float4 _MainTex_TexelSize;
        float _Underwater;
        half4 bright(v2f_img i):SV_Target {
            half3 c=tex2D(_MainTex,i.uv).rgb;
            return half4(c*saturate((max(c.r,max(c.g,c.b))-.8)*1.8),1);
        }
        half4 blur(v2f_img i):SV_Target {
            float2 d=_MainTex_TexelSize.xy*2;
            return (tex2D(_MainTex,i.uv+float2(d.x,d.y))+tex2D(_MainTex,i.uv+float2(-d.x,d.y))+tex2D(_MainTex,i.uv+float2(d.x,-d.y))+tex2D(_MainTex,i.uv-d))*.25;
        }
        half4 finish(v2f_img i):SV_Target {
            half3 c=tex2D(_MainTex,i.uv).rgb+tex2D(_Bloom,i.uv).rgb*.19;
            float2 q=(i.uv-.5)*2;
            float vignette=1-dot(q,q)*.09;
            c=lerp(c,c*float3(.83,1.02,1.05),_Underwater*.65);
            #if defined(UNITY_COLORSPACE_GAMMA)
            c=GammaToLinearSpace(c);
            #endif
            c=max(0,c*1.08);
            c=(c*(2.51*c+.03))/(c*(2.43*c+.59)+.14);
            c=lerp(dot(c,half3(.2126,.7152,.0722)).xxx,c,1.12);
            #if defined(UNITY_COLORSPACE_GAMMA)
            c=LinearToGammaSpace(max(0,c));
            #endif
            return half4(saturate(c*vignette),1);
        }
        ENDCG
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment bright
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment blur
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment finish
            ENDCG }
    }
}

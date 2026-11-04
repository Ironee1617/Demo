Shader "Aquarium/Gallery Finish"
{
    Properties { _MainTex("Source",2D)="white" {} _Bloom("Bloom",2D)="black" {} _Underwater("Submerged",Float)=0 _Grade("R73 map colour grade",Color)=(1,1,1,1) }
    SubShader {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex,_Bloom;
        float4 _MainTex_TexelSize;
        float _Underwater;
        float4 _Grade;   // R73: the grotto's dim purple grade (white elsewhere)
        // R67: the bloom used to turn a small bright glint (a highlight on the divers' mask glass, frame or
        // fittings) into a 3 x 3 grid of soft white dots 16 px apart: the bright pass read one bilinear tap per
        // quarter-res texel (a glint smaller than 4 px was caught on some frames and missed on others, so the dots
        // came and went as the diver bobbed) and both blur passes used four diagonal taps 2 texels out (a lattice,
        // not a glow). Now the bright pass averages the whole 4 x 4 block and the blur is a smooth 4 x 4 kernel of
        // bilinear taps (about the same width as before), so a glint blooms into one soft glow.
        // R68: one NaN or infinite pixel (a shader dividing by zero somewhere in the scene) used to poison the whole
        // bloom kernel round it: the blur spread it over about 13 x 13 quarter-res texels and the finish turned that
        // into a solid black square. Non-finite input now counts as black and the bloom source is capped at 8, far
        // above any ordinary highlight (the characters are capped at 3), so a single bad pixel can never spread.
        // (Tested on the exponent bits, which the compiler cannot fold away as it may a float NaN test.)
        float3 Finite(float3 c){ uint3 e=asuint(c)&0x7f800000; return any(e==0x7f800000) ? float3(0,0,0) : c; }
        half3 bright1(float2 uv){
            float3 c=clamp(Finite(tex2D(_MainTex,uv).rgb),0,8);
            return c*saturate((max(c.r,max(c.g,c.b))-.8)*1.8);
        }
        half4 bright(v2f_img i):SV_Target {
            float2 d=_MainTex_TexelSize.xy;
            return half4((bright1(i.uv+float2(-d.x,-d.y))+bright1(i.uv+float2(d.x,-d.y))+bright1(i.uv+float2(-d.x,d.y))+bright1(i.uv+d))*.25,1);
        }
        half4 blur(v2f_img i):SV_Target {
            float2 d=_MainTex_TexelSize.xy;
            const float o[4]={-2.5,-.5,.5,2.5};
            half4 s=0;
            for(int y=0;y<4;y++) for(int x=0;x<4;x++) s+=tex2D(_MainTex,i.uv+float2(o[x],o[y])*d);
            return half4(Finite(s.rgb*.0625),1);
        }
        half4 finish(v2f_img i):SV_Target {
            half3 c=Finite(tex2D(_MainTex,i.uv).rgb)+Finite(tex2D(_Bloom,i.uv).rgb)*.19;
            float2 q=(i.uv-.5)*2;
            float vignette=1-dot(q,q)*.09;
            c=lerp(c,c*float3(.83,1.02,1.05),_Underwater*.65);
            c*=_Grade.rgb;
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

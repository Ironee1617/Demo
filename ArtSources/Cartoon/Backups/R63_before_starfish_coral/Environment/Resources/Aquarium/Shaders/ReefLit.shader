Shader "Aquarium/Reef Lit"
{
    Properties {
        _Color ("Tint", Color) = (1,1,1,1)
        _Caustics ("Caustic network", 2D) = "black" {}
        _Smoothness ("Smoothness", Range(0,1)) = 0.28
        _Underwater ("Underwater", Range(0,1)) = 1
        _Sway ("Current strength", Float) = 0
        _Sand ("Sand detail", Float) = 0
        _Emission ("Emission", Float) = 0
        _Paving ("Continuous limestone paving", Float) = 0
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        #include "UnityCG.cginc"
        sampler2D _Caustics;
        fixed4 _Color;
        half _Smoothness, _Underwater, _Sway, _Sand, _Emission, _Paving;
        float _AquariumPreviewTime, _AquariumPreviewTimeEnabled;
        float WaterTime() { return lerp(_Time.y,_AquariumPreviewTime,_AquariumPreviewTimeEnabled); }
        struct Input { float3 worldPos; float3 worldNormal; float4 color : COLOR; };
        void vert(inout appdata_full v) {
            // UV2.x stores anchored bend weight; roots remain planted in the substrate.
            float w=v.texcoord1.x;
            float phase=v.texcoord1.y;
            float t=WaterTime();
            v.vertex.x += _Sway*w*(sin(t*1.08+phase+v.vertex.y*.7)*.23 + sin(t*1.8+phase)*.06);
            v.vertex.z += _Sway*w*cos(t*.87+phase+v.vertex.y*.5)*.14;
            v.normal.xz += _Sway*w*.08*float2(cos(t+phase),sin(t+phase));
        }
        void surf(Input IN, inout SurfaceOutputStandard o) {
            float3 p=IN.worldPos;
            float time=WaterTime();
            float grain=frac(sin(dot(floor(p.xz*190),float2(12.9898,78.233)))*43758.5453);
            float ripple=sin(p.x*13+p.z*4+sin(p.z*1.6)*2)*.5+.5;
            float variation=1 + _Sand*(ripple*.075+grain*.07-.09);
            float underwater=_Underwater*saturate((12.12-p.y)*3);
            float2 uv=p.xz*.18+float2(sin(p.z*.4+time*.32),cos(p.x*.3+time*.24))*.025;
            float a=tex2D(_Caustics,uv+time*float2(.013,.009)).r;
            float b=tex2D(_Caustics,uv*1.31+time*float2(-.011,.016)).r;
            float caustic=min(a,b)*.85 + max(a,b)*.48;
            float top=.28+.72*saturate(IN.worldNormal.y);
            float2 tileUV=p.xz/1.15;
            tileUV.x+=fmod(floor(tileUV.y),2)*.5;
            float2 edge=min(frac(tileUV),1-frac(tileUV));
            float joint=1-smoothstep(.004,.014,min(edge.x,edge.y));
            float tileShade=frac(sin(dot(floor(tileUV),float2(53.17,17.31)))*4375.21);
            float paving=lerp(1,lerp(.92,1.04,tileShade)*(1-joint*.17),_Paving*saturate(IN.worldNormal.y));
            o.Albedo=IN.color.rgb*_Color.rgb*variation*paving;
            o.Metallic=0;
            o.Smoothness=_Smoothness;
            o.Occlusion=lerp(.72,1,IN.color.a);
            o.Emission=underwater*caustic*top*float3(.05,.25,.29) + o.Albedo*_Emission;
            o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

Shader "Aquarium/Stratified Limestone"
{
    Properties {
        _Caustics ("Refracted light", 2D) = "black" {}
        _Color ("Limestone tint", Color) = (1,1,1,1)
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _Caustics;fixed4 _Color;
        float _AquariumPreviewTime,_AquariumPreviewTimeEnabled;
        struct Input {float3 worldPos;float3 worldNormal;};
        float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
        float noise(float3 p){
            float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);
            return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),
                lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+float3(1,1,1)),f.x),f.y),f.z);
        }
        void surf(Input IN,inout SurfaceOutputStandard o){
            float3 p=IN.worldPos;float time=lerp(_Time.y,_AquariumPreviewTime,_AquariumPreviewTimeEnabled);
            float broad=noise(p*1.3),grain=noise(p*13.7),micro=noise(p*51);
            float folds=p.y*3.1+noise(p*1.7)*1.9+sin(p.x*.31+p.z*.48)*.8;
            float bedding=frac(folds);float seam=(1-smoothstep(.012,.10,min(bedding,1-bedding)))*smoothstep(.32,.68,noise(p*.7));
            float fracture=smoothstep(.63,.78,noise(p*7.2));
            float3 stone=lerp(float3(.27,.28,.25),float3(.56,.49,.37),broad);
            stone*=lerp(.66,1.18,grain)*(1-seam*.24)*(1-fracture*.27)*(micro*.16+.90);
            float algae=saturate((IN.worldNormal.y-.25)*.55)*noise(p*2.3);
            stone=lerp(stone,float3(.25,.31,.21),algae*.5);
            float2 uv=p.xz*.18+float2(sin(p.z*.4+time*.32),cos(p.x*.3+time*.24))*.025;
            float a=tex2D(_Caustics,uv+time*float2(.013,.009)).r;
            float b=tex2D(_Caustics,uv*1.31+time*float2(-.011,.016)).r;
            float caustic=min(a,b)*.85+max(a,b)*.48;
            o.Albedo=stone*_Color.rgb;o.Smoothness=.16+grain*.10;o.Metallic=0;
            o.Occlusion=1-seam*.22-fracture*.10;
            o.Emission=caustic*(.22+.78*saturate(IN.worldNormal.y))*float3(.05,.27,.31);
            o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

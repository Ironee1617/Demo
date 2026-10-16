Shader "Aquarium/Natural Granular Sand"
{
 Properties {
  _MainTex("4K close-range sand grains",2D)="white"{}
  _NormalTex("4K grain normal, linear RGB",2D)="bump"{}
  _ARM("Grain AO and roughness",2D)="white"{}
  _Caustics("Refracted light",2D)="black"{}
  _Color("Pale carbonate sand",Color)=(.88,.84,.74,1)
  _WorldScale("Grain tiles per metre",Float)=.48
  _NormalStrength("Millimetre grain relief",Float)=.22
 }
 SubShader {
  Tags {"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
  #pragma target 3.0
  #include "UnityCG.cginc"
  sampler2D _MainTex,_NormalTex,_ARM,_Caustics;
  float4 _Color;float _WorldScale,_NormalStrength;
  float _AquariumPreviewTime,_AquariumPreviewTimeEnabled;
  struct Input {float3 worldPos;float3 worldNormal;float3 geometricNormal;INTERNAL_DATA};
  void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.geometricNormal=UnityObjectToWorldNormal(v.normal);}
  float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
  float ripple(float2 p){
   float mask=smoothstep(.30,.68,noise(p*.13+17));
   float phase=dot(p,float2(10.5,3.0))+noise(p*.7)*3.0+noise(p*.21)*5.0;
   return (sin(phase)+.2*sin(phase*2+.4))*.007*mask;
  }
  void surf(Input IN,inout SurfaceOutputStandard o){
   float2 p=IN.worldPos.xz;
   // Two real grain samples have independent scale/orientation, preventing an obvious
   // repeating stamp. The source is recoloured to match the reference's pale seabed.
   float2 u=p*_WorldScale,v=float2(p.x*.8+p.y*.6,-p.x*.6+p.y*.8)*_WorldScale*1.73+float2(.31,.73);
   float3 a=tex2D(_MainTex,u).rgb,b=tex2D(_MainTex,v).rgb;
   float grain=dot(a*.72+b*.28,float3(.299,.587,.114));
   float patches=noise(p*.20+8)*.55+noise(p*.047)*.45;
   float tone=.28+grain*.82+(patches-.5)*.13;
   o.Albedo=_Color.rgb*tone;
   float3 na=tex2D(_NormalTex,u).rgb*2-1,nb=tex2D(_NormalTex,v).rgb*2-1;
   float2 detail=na.xy*.7+float2(nb.x*.8-nb.y*.6,nb.x*.6+nb.y*.8)*.3;
   float e=.015;
   float2 slope=float2(ripple(p+float2(e,0))-ripple(p-float2(e,0)),ripple(p+float2(0,e))-ripple(p-float2(0,e)))/(2*e);
   float3 n=normalize(IN.geometricNormal),world=normalize(n+float3(detail.x*_NormalStrength-slope.x,0,detail.y*_NormalStrength-slope.y));
   float3 T=WorldNormalVector(IN,float3(1,0,0)),B=WorldNormalVector(IN,float3(0,1,0)),N=WorldNormalVector(IN,float3(0,0,1));
   o.Normal=normalize(float3(dot(world,T),dot(world,B),dot(world,N)));
   float3 arm=tex2D(_ARM,u).rgb;
   o.Metallic=0;o.Smoothness=.035;o.Occlusion=lerp(.92,1,arm.r);o.Alpha=1;
   float time=lerp(_Time.y,_AquariumPreviewTime,_AquariumPreviewTimeEnabled);
   float2 ca=p*.18+float2(sin(p.y*.4+time*.32),cos(p.x*.3+time*.24))*.025;
   float caustic=min(tex2D(_Caustics,ca+time*float2(.013,.009)).r,tex2D(_Caustics,ca*1.31+time*float2(-.011,.016)).r);
   o.Emission=caustic*float3(.045,.065,.067)*saturate(world.y);
  }
  ENDCG
 }
 FallBack "Diffuse"
}

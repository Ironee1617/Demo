Shader "Aquarium/Living Reef"
{
 Properties {
  _Color("Tint",Color)=(1,1,1,1) _Caustics("Refracted light",2D)="black"{}
  _Sway("Current strength",Float)=0 _Soft("Soft tissue",Range(0,1))=0
  _PolypDetail("Polyp relief",Range(0,1))=.35 _Smoothness("Wet tissue",Range(0,1))=.22
  _Pattern("R63 painted coral and sea star patterns",Range(0,1))=0 _Fluoro("R63 fluorescent polyps",Range(0,1))=0
 }
 SubShader {
  Tags {"RenderType"="Opaque"}
  Cull Off
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
  #pragma target 3.0
  #include "UnityCG.cginc"
  #include "CoralPaint.cginc"
  #include "AquariumCurrent.cginc"
  sampler2D _Caustics;fixed4 _Color;half _Sway,_Soft,_PolypDetail,_Smoothness,_Pattern,_Fluoro;
  float _AquariumPreviewTime,_AquariumPreviewTimeEnabled;
  float ReefTime(){return lerp(_Time.y,_AquariumPreviewTime,_AquariumPreviewTimeEnabled);}
  struct Input {float3 worldPos;float3 worldNormal;float4 color:COLOR;float2 tissue;float facing:VFACE;INTERNAL_DATA};
  void vert(inout appdata_full v,out Input o) {
   UNITY_INITIALIZE_OUTPUT(Input,o);o.tissue=v.texcoord.xy;
   float weight=v.texcoord1.x,phase=v.texcoord1.y,t=ReefTime();
   // R85: the shared current (AquariumCurrent.cginc): lean, swell, flutter, curling tentacles, leaning away from swimmers.
   float3 restWorld=mul(unity_ObjectToWorld,v.vertex).xyz;
   float3 bend=AquariumSway(restWorld,v.texcoord.y,weight,phase,_Sway,_Soft,t);
   v.vertex.xyz+=bend;v.normal.xz+=bend.xz*.18;
   // Soft bodies breathe a little (the column and the tentacle roots, not the tips).
   v.vertex.xyz+=v.normal*(_Soft*.004*sin(t*1.25+phase*.37+restWorld.y*3)*(1-saturate(abs(weight)*8)));
   // R68: never a zero normal or tangent (NaN after normalize, a black square after the bloom).
   if(dot(v.normal,v.normal)<1e-12)v.normal=float3(0,1,0);if(dot(v.tangent.xyz,v.tangent.xyz)<1e-12)v.tangent=float4(abs(v.normal.y)<.9?normalize(cross(float3(0,1,0),v.normal)):float3(1,0,0),1);
  }
  void surf(Input IN,inout SurfaceOutputStandard o) {
   float3 p=IN.worldPos;float t=ReefTime();
   float grain=frac(sin(dot(floor(p*240),float3(12.9898,78.233,41.31)))*43758.5453);
   float cells=sin(p.x*113+sin(p.y*89))*sin(p.z*107+p.y*83);
   float detail=1+_PolypDetail*(cells*.045+(grain-.5)*.08);
   float fineRib=sin(IN.tissue.x*113.1+sin(IN.tissue.y*11)*.24);
   o.Albedo=IN.color.rgb*_Color.rgb*detail*(1+_Soft*fineRib*.025);
   float paintGlow=0;
   if(_Pattern>.5){float3 a=o.Albedo;paintGlow=CoralPaint(IN.color.a,IN.tissue,p,saturate(1.6-distance(_WorldSpaceCameraPos,p)*.045),a);o.Albedo=a;}
   o.Normal=float3(0,0,IN.facing>=0?1:-1);
   o.Metallic=0;o.Smoothness=_Smoothness;o.Occlusion=.9;
   float2 uv=p.xz*.18+float2(sin(p.z*.4+t*.32),cos(p.x*.3+t*.24))*.025;
   float a=tex2D(_Caustics,uv+t*float2(.013,.009)).r,b=tex2D(_Caustics,uv*1.31+t*float2(-.011,.016)).r;
   float caustic=min(a,b)*.65+max(a,b)*.25;
   float3 normalW=normalize(WorldNormalVector(IN,o.Normal));
   float3 view=normalize(_WorldSpaceCameraPos-p);
   float rim=pow(1-saturate(abs(dot(normalW,view))),2);
   float through=pow(saturate(dot(-normalW,normalize(_WorldSpaceLightPos0.xyz))),2);
   o.Emission=caustic*(.25+.75*saturate(normalW.y))*float3(.025,.09,.10)+o.Albedo*_Soft*(.018+rim*.055+through*.075)+o.Albedo*paintGlow*_Fluoro*.35;
   o.Alpha=1;
  }
  ENDCG
 }
 FallBack "Diffuse"
}

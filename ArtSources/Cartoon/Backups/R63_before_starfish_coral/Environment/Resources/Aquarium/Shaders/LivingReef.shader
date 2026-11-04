Shader "Aquarium/Living Reef"
{
 Properties {
  _Color("Tint",Color)=(1,1,1,1) _Caustics("Refracted light",2D)="black"{}
  _Sway("Current strength",Float)=0 _Soft("Soft tissue",Range(0,1))=0
  _PolypDetail("Polyp relief",Range(0,1))=.35 _Smoothness("Wet tissue",Range(0,1))=.22
 }
 SubShader {
  Tags {"RenderType"="Opaque"}
  Cull Off
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
  #pragma target 3.0
  #include "UnityCG.cginc"
  sampler2D _Caustics;fixed4 _Color;half _Sway,_Soft,_PolypDetail,_Smoothness;
  float _AquariumPreviewTime,_AquariumPreviewTimeEnabled;
  float ReefTime(){return lerp(_Time.y,_AquariumPreviewTime,_AquariumPreviewTimeEnabled);}
  struct Input {float3 worldPos;float3 worldNormal;float4 color:COLOR;float2 tissue;float facing:VFACE;INTERNAL_DATA};
  void vert(inout appdata_full v,out Input o) {
   UNITY_INITIALIZE_OUTPUT(Input,o);o.tissue=v.texcoord.xy;
   float weight=v.texcoord1.x,phase=v.texcoord1.y,t=ReefTime();
   float2 wave=float2(sin(t*.91+phase+v.vertex.y*.58)*.20+sin(t*1.43+phase)*.035,cos(t*.72+phase+v.vertex.y*.41)*.13);
   v.vertex.xz+=_Sway*weight*wave;
   v.normal.xz+=_Sway*weight*.045*float2(cos(t*.91+phase),sin(t*.72+phase));
  }
  void surf(Input IN,inout SurfaceOutputStandard o) {
   float3 p=IN.worldPos;float t=ReefTime();
   float grain=frac(sin(dot(floor(p*240),float3(12.9898,78.233,41.31)))*43758.5453);
   float cells=sin(p.x*113+sin(p.y*89))*sin(p.z*107+p.y*83);
   float detail=1+_PolypDetail*(cells*.045+(grain-.5)*.08);
   float fineRib=sin(IN.tissue.x*113.1+sin(IN.tissue.y*11)*.24);
   o.Albedo=IN.color.rgb*_Color.rgb*detail*(1+_Soft*fineRib*.025);
   o.Normal=float3(0,0,IN.facing>=0?1:-1);
   o.Metallic=0;o.Smoothness=_Smoothness;o.Occlusion=.9;
   float2 uv=p.xz*.18+float2(sin(p.z*.4+t*.32),cos(p.x*.3+t*.24))*.025;
   float a=tex2D(_Caustics,uv+t*float2(.013,.009)).r,b=tex2D(_Caustics,uv*1.31+t*float2(-.011,.016)).r;
   float caustic=min(a,b)*.65+max(a,b)*.25;
   float3 normalW=normalize(WorldNormalVector(IN,o.Normal));
   float3 view=normalize(_WorldSpaceCameraPos-p);
   float rim=pow(1-saturate(abs(dot(normalW,view))),2);
   float through=pow(saturate(dot(-normalW,normalize(_WorldSpaceLightPos0.xyz))),2);
   o.Emission=caustic*(.25+.75*saturate(normalW.y))*float3(.025,.09,.10)+o.Albedo*_Soft*(.018+rim*.055+through*.075);
   o.Alpha=1;
  }
  ENDCG
 }
 FallBack "Diffuse"
}

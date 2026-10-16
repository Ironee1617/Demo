Shader "Aquarium/Scanned Geology"
{
 Properties {
  _MainTex("4K photographed albedo",2D)="white"{} _NormalTex("4K surface normal (linear RGB)",2D)="bump"{}
  _ARM("Ambient occlusion / roughness / metallic",2D)="white"{} _Caustics("Refracted light",2D)="black"{}
  _Color("Mineral / sand tint",Color)=(1,1,1,1) _WorldScale("Tiles per metre",Float)=.37
  _NormalStrength("Surface relief",Float)=1 _Sand("Sand material",Float)=0
 }
 SubShader {
  Tags {"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
  #pragma target 3.0
  #include "UnityCG.cginc"
  sampler2D _MainTex,_NormalTex,_ARM,_Caustics;float4 _Color;float _WorldScale,_NormalStrength,_Sand;
  float _AquariumPreviewTime,_AquariumPreviewTimeEnabled;
  struct Input {float3 worldPos;float3 worldNormal;float3 geometricNormal;INTERNAL_DATA};
  void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.geometricNormal=UnityObjectToWorldNormal(v.normal);}
  void surf(Input IN,inout SurfaceOutputStandard o){
   float3 p=IN.worldPos,n=normalize(IN.geometricNormal),weights=pow(abs(n),float3(6,6,6));weights/=dot(weights,1);
   float3 signs=sign(n);float2 ux=p.zy*_WorldScale,uy=p.xz*_WorldScale,uz=p.xy*_WorldScale;
   float3 albedo=tex2D(_MainTex,ux).rgb*weights.x+tex2D(_MainTex,uy).rgb*weights.y+tex2D(_MainTex,uz).rgb*weights.z;
   float3 arm=tex2D(_ARM,ux).rgb*weights.x+tex2D(_ARM,uy).rgb*weights.y+tex2D(_ARM,uz).rgb*weights.z;
   float3 nx=tex2D(_NormalTex,ux).rgb*2-1,ny=tex2D(_NormalTex,uy).rgb*2-1,nz=tex2D(_NormalTex,uz).rgb*2-1;
   float3 detail=float3(0,nx.y,nx.x)*weights.x+float3(ny.x,0,ny.y)*weights.y+float3(nz.x,nz.y,0)*weights.z;
   detail-=n*dot(detail,n);float3 perturbed=normalize(n+detail*_NormalStrength);
   float3 T=WorldNormalVector(IN,float3(1,0,0)),B=WorldNormalVector(IN,float3(0,1,0)),N=WorldNormalVector(IN,float3(0,0,1));
   o.Normal=normalize(float3(dot(perturbed,T),dot(perturbed,B),dot(perturbed,N)));
   float macro=.92+.08*sin(p.x*.43+sin(p.z*.33))*sin(p.y*.7+p.z*.28);
   float time=lerp(_Time.y,_AquariumPreviewTime,_AquariumPreviewTimeEnabled);
   float2 uv=p.xz*.18+float2(sin(p.z*.4+time*.32),cos(p.x*.3+time*.24))*.025;
   float a=tex2D(_Caustics,uv+time*float2(.013,.009)).r,b=tex2D(_Caustics,uv*1.31+time*float2(-.011,.016)).r;
   float caustic=min(a,b)*.65+max(a,b)*.28;
   // Sand stays warm, with fine grains and a broad dune profile supplied by the actual mesh.
   o.Albedo=albedo*_Color.rgb*macro;o.Metallic=0;o.Smoothness=lerp(.28,.10,_Sand)*(1-arm.g);
   o.Occlusion=lerp(.40,1,arm.r);o.Alpha=1;
   o.Emission=caustic*(.13+.87*saturate(perturbed.y))*float3(.045,.20,.23)*lerp(1,.55,_Sand);
  }
  ENDCG
 }
 FallBack "Diffuse"
}

Shader "Aquarium/Painted Geology"
{
 // R58: the twin tanks' rocks. The Scanned Geology stone (same 4K textures, triplanar, caustics) with the
 // painted variation baked into the rock meshes: vertex rgb tints and darkens the creases, vertex alpha lays
 // a soft moss on the upper faces and hollows, and fine sand dust settles on the flat tops.
 Properties {
  _MainTex("4K photographed albedo",2D)="white"{} _NormalTex("4K surface normal (linear RGB)",2D)="bump"{}
  _ARM("Ambient occlusion / roughness / metallic",2D)="white"{} _Caustics("Refracted light",2D)="black"{}
  _Color("Mineral tint",Color)=(1,1,1,1) _WorldScale("Tiles per metre",Float)=.42
  _NormalStrength("Surface relief",Float)=.8 _Sand("Sand material",Float)=0
  _MossColor("Moss",Color)=(.37,.55,.38,1) _DustColor("Sand dust",Color)=(.85,.8,.66,1) _Dust("Sand dust amount",Range(0,1))=.55
 }
 SubShader {
  Tags {"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
  #pragma target 3.0
  #include "UnityCG.cginc"
  sampler2D _MainTex,_NormalTex,_ARM,_Caustics;float4 _Color,_MossColor,_DustColor;float _WorldScale,_NormalStrength,_Sand,_Dust;
  float _AquariumPreviewTime,_AquariumPreviewTimeEnabled;
  struct Input {float3 worldPos;float3 worldNormal;float3 geometricNormal;float4 paint;INTERNAL_DATA};
  // R68: a vertex with a zero normal or tangent (a welded point on a flat spot of a meshed field) made every
  // normalize() round it NaN, so the triangles touching it drew NaN and the bloom spread them into a black square.
  void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);if(dot(v.normal,v.normal)<1e-12)v.normal=float3(0,1,0);if(dot(v.tangent.xyz,v.tangent.xyz)<1e-12)v.tangent=float4(abs(v.normal.y)<.9?normalize(cross(float3(0,1,0),v.normal)):float3(1,0,0),1);o.geometricNormal=UnityObjectToWorldNormal(v.normal);o.paint=v.color;}
  void surf(Input IN,inout SurfaceOutputStandard o){
   float3 p=IN.worldPos,n=normalize(IN.geometricNormal),weights=pow(abs(n),float3(6,6,6));weights/=dot(weights,1);
   float2 ux=p.zy*_WorldScale,uy=p.xz*_WorldScale,uz=p.xy*_WorldScale;
   float3 albedo=tex2D(_MainTex,ux).rgb*weights.x+tex2D(_MainTex,uy).rgb*weights.y+tex2D(_MainTex,uz).rgb*weights.z;
   float3 arm=tex2D(_ARM,ux).rgb*weights.x+tex2D(_ARM,uy).rgb*weights.y+tex2D(_ARM,uz).rgb*weights.z;
   float3 nx=tex2D(_NormalTex,ux).rgb*2-1,ny=tex2D(_NormalTex,uy).rgb*2-1,nz=tex2D(_NormalTex,uz).rgb*2-1;
   float3 detail=float3(0,nx.y,nx.x)*weights.x+float3(ny.x,0,ny.y)*weights.y+float3(nz.x,nz.y,0)*weights.z;
   detail-=n*dot(detail,n);float3 perturbed=normalize(n+detail*_NormalStrength);
   float3 T=WorldNormalVector(IN,float3(1,0,0)),B=WorldNormalVector(IN,float3(0,1,0)),N=WorldNormalVector(IN,float3(0,0,1));
   o.Normal=normalize(float3(dot(perturbed,T),dot(perturbed,B),dot(perturbed,N)));
   float macro=.92+.08*sin(p.x*.43+sin(p.z*.33))*sin(p.y*.7+p.z*.28);
   float luminance=dot(albedo,float3(.3,.5,.2));
   float3 colour=albedo*_Color.rgb*macro*IN.paint.rgb;
   // Moss: a soft green film that keeps the stone's grain.
   float moss=saturate(IN.paint.a);
   colour=lerp(colour,_MossColor.rgb*(.55+.9*luminance)*lerp(.8,1.05,IN.paint.g),moss*.85);
   // Sand dust on the flat tops, broken up by the stone's own grain.
   float up=saturate((n.y-.55)*2.6);
   float grain=tex2D(_MainTex,uy*2.7).g;
   float dust=up*_Dust*saturate((grain-.32)*2.2+up*.35)*(1-moss*.6);
   colour=lerp(colour,_DustColor.rgb*(.88+.24*grain),dust);
   float time=lerp(_Time.y,_AquariumPreviewTime,_AquariumPreviewTimeEnabled);
   float2 uv=p.xz*.18+float2(sin(p.z*.4+time*.32),cos(p.x*.3+time*.24))*.025;
   float a=tex2D(_Caustics,uv+time*float2(.013,.009)).r,b=tex2D(_Caustics,uv*1.31+time*float2(-.011,.016)).r;
   float caustic=min(a,b)*.65+max(a,b)*.28;
   o.Albedo=colour;o.Metallic=0;o.Smoothness=.26*(1-arm.g)*(1-moss*.5);
   o.Occlusion=lerp(.45,1,arm.r);o.Alpha=1;
   o.Emission=caustic*(.13+.87*saturate(perturbed.y))*float3(.045,.20,.23);
  }
  ENDCG
 }
 FallBack "Diffuse"
}

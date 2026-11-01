Shader "Aquarium/Marine Hardware"
{
 Properties {
  _Color("Finish",Color)=(.36,.48,.5,1)
  _Metallic("Metal",Range(0,1))=.65
  _Smoothness("Polish",Range(0,1))=.55
  _Caustics("Water light",2D)="black"{}
 }
 SubShader {
  Tags {"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow
  #pragma target 3.0
  fixed4 _Color; half _Metallic,_Smoothness;
  sampler2D _Caustics;
  float _AquariumPreviewTime,_AquariumPreviewTimeEnabled;
  struct Input { float3 worldPos; float3 worldNormal; };
  void surf(Input IN,inout SurfaceOutputStandard o) {
   float3 p=IN.worldPos;
   // Subtle directional machining, with no blotchy colour noise.
   float a=p.x*830+p.z*117,b=p.z*380+p.y*211;
   float grain=sin(a)*sin(b)*saturate(1-max(fwidth(a),fwidth(b))*.25);
   o.Albedo=_Color.rgb*(.985+grain*.006);
   o.Metallic=_Metallic;o.Smoothness=saturate(_Smoothness+grain*.012);o.Occlusion=1;o.Alpha=1;
   float t=lerp(_Time.y,_AquariumPreviewTime,_AquariumPreviewTimeEnabled);
   float2 uv=p.xz*.18+float2(sin(p.z*.4+t*.32),cos(p.x*.3+t*.24))*.025;
   float ca=min(tex2D(_Caustics,uv+t*float2(.013,.009)).r,tex2D(_Caustics,uv*1.31+t*float2(-.011,.016)).r);
   o.Emission=ca*float3(.025,.055,.06)*saturate(IN.worldNormal.y);
  }
  ENDCG
 }
 FallBack "Standard"
}

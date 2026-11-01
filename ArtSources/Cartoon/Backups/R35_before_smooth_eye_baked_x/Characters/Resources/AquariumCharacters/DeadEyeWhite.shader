// R34: a dead alien's iris dome. Same surface as "Aquarium/Cartoon Character" (atlas x species tint, metal and
// gloss from uv2, ingestion warp), but the iris paint - its colour, the pupil and a lash line - turns into the
// eye white sampled at _WhiteUV. Light, unsaturated paint stays: a variant's painted lid skin and the catch-lights.
Shader "Aquarium/Dead Eye White" {
 Properties {
  _MainTex("Original painted colors",2D)="white"{}
  _Tint("Species tint",Color)=(1,1,1,1)
  _WhiteUV("Eye white texel (uv)",Vector)=(.5,.5,0,0)
 }
 SubShader {
  Tags {"RenderType"="Opaque"} LOD 250
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
  #pragma target 3.0
  #include "../../Runtime/AquariumIngestion.cginc"
  sampler2D _MainTex; fixed4 _Tint; float4 _WhiteUV;
  struct Input {float2 uv_MainTex; float2 surface;};
  void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.surface=v.texcoord1.xy;IngestVertex(v);}
  void surf(Input IN,inout SurfaceOutputStandard o){
   fixed3 c=tex2D(_MainTex,IN.uv_MainTex).rgb;
   fixed3 eyeWhite=tex2Dlod(_MainTex,float4(_WhiteUV.xy,0,0)).rgb;
   float lo=min(c.r,min(c.g,c.b)),hi=max(c.r,max(c.g,c.b));
   float keep=smoothstep(.55,.70,lo)*(1-smoothstep(.12,.22,hi-lo));
   o.Albedo=lerp(eyeWhite,c,keep)*_Tint.rgb;o.Metallic=IN.surface.x;o.Smoothness=IN.surface.y;o.Alpha=1;
  }
  ENDCG
 } FallBack "Standard"
}

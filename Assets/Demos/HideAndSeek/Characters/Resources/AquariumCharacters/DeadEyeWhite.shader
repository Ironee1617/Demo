// R34: a dead alien's iris dome. Same surface as "Aquarium/Cartoon Character" (atlas x species tint, metal and
// gloss from uv2, ingestion warp), but the iris paint - its colour, the pupil and a lash line - turns into the
// eye white sampled at _WhiteUV.
// R35: with _Ink = 1 the same surface draws the dead X in _InkColor (the X is part of the dead alien's mesh).
// R35: no paint is kept at all - the whole dead eye (iris dome, the white's rim and the white out to the socket's
// crease) is the one flat eye white. Keeping light paint left a hairline where the iris's soft edge was, and
// stair-stepped patches of a painted lid's shade (green, yellow) on the white.
Shader "Aquarium/Dead Eye White" {
 Properties {
  _MainTex("Original painted colors",2D)="white"{}
  _Tint("Species tint",Color)=(1,1,1,1)
  _WhiteUV("Eye white texel (uv)",Vector)=(.5,.5,0,0)
  _InkColor("X ink",Color)=(.035,.03,.06,1)
  _Ink("Draw the X",Float)=0
 }
 SubShader {
  Tags {"RenderType"="Opaque"} LOD 250
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
  #pragma target 3.0
  #include "../../Runtime/AquariumIngestion.cginc"
  sampler2D _MainTex; fixed4 _Tint,_InkColor; float4 _WhiteUV; float _Ink;
  struct Input {float2 uv_MainTex; float2 surface;};
  void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.surface=v.texcoord1.xy;IngestVertex(v);}
  void surf(Input IN,inout SurfaceOutputStandard o){
   fixed3 eyeWhite=tex2Dlod(_MainTex,float4(_WhiteUV.xy,0,0)).rgb;
   o.Albedo=lerp(eyeWhite*_Tint.rgb,_InkColor.rgb,_Ink);o.Metallic=IN.surface.x;o.Smoothness=IN.surface.y;o.Alpha=1;
  }
  ENDCG
 } FallBack "Standard"
}

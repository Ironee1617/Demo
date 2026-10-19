Shader "Aquarium/Cartoon Character" {
 Properties {
  _MainTex("Original painted colors",2D)="white"{}
  _Tint("Species tint",Color)=(1,1,1,1)
 }
 SubShader {
  Tags {"RenderType"="Opaque"} LOD 250
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
  #pragma target 3.0
  #include "AquariumIngestion.cginc"
  sampler2D _MainTex; fixed4 _Tint;
  struct Input {float2 uv_MainTex; float2 surface;};
  void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.surface=v.texcoord1.xy;IngestVertex(v);}
  void surf(Input IN,inout SurfaceOutputStandard o){o.Albedo=tex2D(_MainTex,IN.uv_MainTex).rgb*_Tint.rgb;o.Metallic=IN.surface.x;o.Smoothness=IN.surface.y;o.Alpha=1;}
  ENDCG
 } FallBack "Standard"
}

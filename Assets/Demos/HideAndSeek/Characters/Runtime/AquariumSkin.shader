Shader "Aquarium/Articulated Skin" {
 Properties {
  _MainTex("4K Albedo",2D)="white"{} _BumpMap("4K Normal",2D)="bump"{} _RM("Roughness / Metal",2D)="white"{}
  _Tint("Species Tint",Color)=(1,1,1,1) _Morph("Metamorphosis",Range(0,1))=0 _MawOpen("Hidden mouth",Range(0,1))=0
 }
 SubShader {
 Tags {"RenderType"="Opaque"} LOD 250
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
 #pragma target 3.0
 sampler2D _MainTex,_BumpMap,_RM; fixed4 _Tint; float _Morph,_MawOpen;
 #include "AquariumIngestion.cginc"
 void vert(inout appdata_full v){IngestVertex(v);}
 struct Input {float2 uv_MainTex;float2 uv_BumpMap;float4 screenPos;float4 color:COLOR;};
 void surf(Input IN,inout SurfaceOutputStandard o) {
  fixed4 c=tex2D(_MainTex,IN.uv_MainTex);fixed3 rm=tex2D(_RM,IN.uv_MainTex).rgb;
  o.Albedo=lerp(c.rgb,IN.color.rgb,saturate(IN.color.a*_MawOpen))*_Tint.rgb;
  o.Normal=normalize(lerp(UnpackNormal(tex2D(_BumpMap,IN.uv_BumpMap)),float3(0,0,1),_Morph));o.Metallic=rm.b*.25;o.Smoothness=clamp(1-rm.g,.18,.75);
  o.Alpha=1;
 }
 ENDCG
 } FallBack "Standard"
}

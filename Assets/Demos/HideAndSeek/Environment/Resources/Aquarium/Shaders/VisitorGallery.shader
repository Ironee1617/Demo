Shader "Aquarium/Dim Visitor Gallery"
{
 Properties {_Color("Tint",Color)=(1,1,1,1) _Paving("Floor tiles",Float)=0 _Exposure("Interior exposure",Float)=.22}
 SubShader {
  Tags {"RenderType"="Opaque"} Cull Off
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  float4 _Color;float _Paving,_Exposure;
  struct Input{float3 worldPos;float3 worldNormal;float4 color:COLOR;};
  float grain(float3 p){return frac(sin(dot(floor(p),float3(127.1,311.7,87.3)))*43758.5453);}
  void surf(Input IN,inout SurfaceOutputStandard o){
   float3 p=IN.worldPos;float g=grain(p*180),stone=grain(p*4)*.045+g*.04+.93;
   float2 tile=p.xz/1.1;tile.x+=fmod(floor(tile.y),2)*.5;float2 edge=min(frac(tile),1-frac(tile));
   float joint=1-smoothstep(.003,.012,min(edge.x,edge.y));
   float panels=1-smoothstep(.003,.014,min(frac(p.y/1.25),1-frac(p.y/1.25)));
   float pattern=lerp(1-panels*.12,1-joint*.25,_Paving*saturate(IN.worldNormal.y));
   o.Albedo=IN.color.rgb*_Color.rgb*stone*pattern*_Exposure;
   o.Smoothness=.26;o.Metallic=.03;o.Occlusion=.8;
   // Low bounced blue from the tank plus restrained pools under the pendants.
   float pool=pow(.5+.5*cos(p.x*.62+p.z*.26),10)*exp(-abs(p.y-.23)*.15);
   o.Emission=IN.color.rgb*float3(.018,.024,.026)+pool*float3(.011,.008,.004);o.Alpha=1;
  }
  ENDCG
 }
 FallBack "Diffuse"
}

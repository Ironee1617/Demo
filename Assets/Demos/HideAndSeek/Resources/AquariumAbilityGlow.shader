// R47: additive ability light (shock shell and flash, electric arcs, the drone's red eye glow).
// _Rim 1: bright at the silhouette (shells), 0: flat (lines, vertex colours if _VertexColor), -1: bright at the centre (glows).
Shader "Aquarium/Ability Glow"
{
 Properties
 {
  _Color("Color (HDR)",Color)=(1,1,1,1)
  _Rim("Rim 1, flat 0, core -1",Float)=0
  _Power("Falloff power",Float)=2
  _VertexColor("Use vertex colours",Float)=0
  [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=0
 }
 SubShader
 {
  Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" }
  Blend One One ZWrite Off Cull [_Cull]
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   float4 _Color; float _Rim,_Power,_VertexColor;
   struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR; };
   struct v2f { float4 pos:SV_POSITION; float3 n:TEXCOORD0; float3 v:TEXCOORD1; float4 color:COLOR; UNITY_FOG_COORDS(2) };
   v2f vert(appdata i)
   {
    v2f o; o.pos=UnityObjectToClipPos(i.vertex); o.n=UnityObjectToWorldNormal(i.normal);
    o.v=_WorldSpaceCameraPos-mul(unity_ObjectToWorld,i.vertex).xyz; o.color=lerp(float4(1,1,1,1),i.color,_VertexColor);
    UNITY_TRANSFER_FOG(o,o.pos); return o;
   }
   fixed4 frag(v2f i):SV_Target
   {
    float k=1;
    if(_Rim>.5 || _Rim<-.5)
    {
     float facing=abs(dot(normalize(i.n+float3(0,0,1e-5)),normalize(i.v)));
     k=_Rim>.5?pow(1-facing,_Power):pow(facing,_Power);
    }
    float4 c=i.color*_Color; c.rgb*=c.a*k;
    UNITY_APPLY_FOG_COLOR(i.fogCoord,c,fixed4(0,0,0,0));
    return float4(c.rgb,0);
   }
   ENDCG
  }
 }
 Fallback Off
}
